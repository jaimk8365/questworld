/// The reusable quest engine: period keys, completion flow, approval flow,
/// reward granting, loot rolls and badge awards. Pure — unit tested on .NET.
module QuestWorld.QuestEngine

open System
open QuestWorld.Domain
open QuestWorld.Progression
open QuestWorld.Catalog

// ------------------------------------------------------------- period logic

let dayKey (date: DateTime) = date.ToString("yyyy-MM-dd")

/// Monday of the week containing `date`.
let weekKey (date: DateTime) =
    let daysSinceMonday = (int date.DayOfWeek + 6) % 7
    dayKey (date.AddDays(float (-daysSinceMonday)))

let periodKey (quest: Quest) (today: DateTime) =
    match quest.recurrence with
    | OnceOff -> "once"
    | EveryDay -> dayKey today
    | EveryWeek -> weekKey today

// ------------------------------------------------------------ quest status

let completionFor (data: AppData) (userId: string) (quest: Quest) (today: DateTime) =
    let key = periodKey quest today
    data.completions
    |> List.tryFind (fun c -> c.questId = quest.id && c.userId = userId && c.periodKey = key)

let statusFor (data: AppData) (userId: string) (quest: Quest) (today: DateTime) =
    match completionFor data userId quest today with
    | Some c -> c.status
    | None -> Available

/// Active quests assigned to a user, with their status for the current period.
let questsForUser (data: AppData) (userId: string) (today: DateTime) =
    data.quests
    |> List.filter (fun q -> q.active && q.assignedTo |> List.contains userId)
    |> List.map (fun q -> q, statusFor data userId q today)

let pendingApprovals (data: AppData) =
    data.completions
    |> List.filter (fun c -> c.status = PendingApproval)
    |> List.choose (fun c ->
        data.quests
        |> List.tryFind (fun q -> q.id = c.questId)
        |> Option.map (fun q -> c, q))

// ------------------------------------------------------------ badge engine

/// Arcade progress with a safe default for users who never played (or v1 saves).
let arcadeOf (user: User) : ArcadeProgress =
    user.arcade |> Option.defaultValue { tokens = 0; bestScore = 0; totalRuns = 0 }

let badgeCtxFor (data: AppData) (user: User) : BadgeCtx =
    let mine = data.completions |> List.filter (fun c -> c.userId = user.id && c.status = Completed)
    let ofType t =
        mine
        |> List.filter (fun c ->
            data.quests
            |> List.exists (fun q -> q.id = c.questId && q.questType = t))
        |> List.length
    let arcade = arcadeOf user
    { level = levelForXp user.xp
      totalCompleted = List.length mine
      choreCompleted = ofType Chore
      behaviourCompleted = ofType Behaviour
      cosmeticsOwned = List.length user.inventory.owned
      arcadeBest = arcade.bestScore
      arcadeRuns = arcade.totalRuns }

let newlyEarnedBadges (data: AppData) (user: User) : string list =
    let ctx = badgeCtxFor data user
    badgeDefs
    |> List.filter (fun b -> b.earned ctx && not (user.badges |> List.contains b.id))
    |> List.map (fun b -> b.id)

// -------------------------------------------------------------- loot boxes

/// 25% chance of a loot box per completed quest.
/// Box contents: 60% coins, 25% XP boost, 15% cosmetic (falls back to coins).
let rollLoot (rng: Random) (user: User) : LootResult option =
    if rng.Next(100) >= 25 then None
    else
        let roll = rng.Next(100)
        if roll < 60 then Some (CoinDrop (5 + rng.Next(16)))
        elif roll < 85 then Some (XpBoost (10 + rng.Next(21)))
        else
            let unowned =
                cosmeticsForTheme user.theme
                |> List.filter (fun c -> not (user.inventory.owned |> List.contains c.id))
            match unowned with
            | [] -> Some (CoinDrop 25)
            | xs -> Some (CosmeticDrop (xs |> List.item (rng.Next(List.length xs))).id)

// -------------------------------------------------------- reward + complete

let private replaceUser (data: AppData) (user: User) =
    { data with users = data.users |> List.map (fun u -> if u.id = user.id then user else u) }

let private applyLoot (user: User) (loot: LootResult option) =
    match loot with
    | None -> user
    | Some (CoinDrop c) -> { user with coins = user.coins + c }
    | Some (XpBoost x) -> { user with xp = user.xp + x }
    | Some (CosmeticDrop id) ->
        { user with inventory = { user.inventory with owned = id :: user.inventory.owned } }

/// Grants a quest's rewards to a user, rolls loot and awards badges.
let private grantRewards (data: AppData) (userId: string) (quest: Quest) (rng: Random) : AppData * CompletionOutcome =
    let user = data.users |> List.find (fun u -> u.id = userId)
    let levelBefore = levelForXp user.xp
    let loot = rollLoot rng user
    let rewarded =
        { user with xp = user.xp + quest.reward.xp; coins = user.coins + quest.reward.coins }
        |> fun u -> applyLoot u loot
    let dataAfterReward = replaceUser data rewarded
    let newBadges = newlyEarnedBadges dataAfterReward rewarded
    let final = { rewarded with badges = rewarded.badges @ newBadges }
    let outcome =
        { quest = quest
          reward = quest.reward
          loot = loot
          levelBefore = levelBefore
          levelAfter = levelForXp final.xp
          newBadges = newBadges
          pendingApproval = false }
    replaceUser data final, outcome

/// Child taps "Done!". Auto-approve quests grant instantly;
/// approval quests go to PendingApproval (rewards on parent approval).
let markDone (data: AppData) (userId: string) (questId: string) (today: DateTime) (rng: Random) : AppData * CompletionOutcome option =
    match data.quests |> List.tryFind (fun q -> q.id = questId) with
    | None -> data, None
    | Some quest when not (quest.assignedTo |> List.contains userId) -> data, None
    | Some quest ->
        match statusFor data userId quest today with
        | PendingApproval | Completed -> data, None // already claimed this period
        | Available ->
            let key = periodKey quest today
            let completion status =
                { questId = quest.id; userId = userId; periodKey = key
                  status = status; completedAt = today.ToString("yyyy-MM-dd HH:mm") }
            if quest.requiresApproval then
                let data' = { data with completions = completion PendingApproval :: data.completions }
                data', Some { quest = quest; reward = quest.reward; loot = None
                              levelBefore = 0; levelAfter = 0; newBadges = []
                              pendingApproval = true }
            else
                let withCompletion = { data with completions = completion Completed :: data.completions }
                let data', outcome = grantRewards withCompletion userId quest rng
                data', Some outcome

/// Parent approves a pending completion — rewards are granted now.
let approve (data: AppData) (completion: QuestCompletion) (rng: Random) : AppData * CompletionOutcome option =
    match data.quests |> List.tryFind (fun q -> q.id = completion.questId) with
    | None -> data, None
    | Some quest ->
        let stillPending =
            data.completions
            |> List.exists (fun c ->
                c.questId = completion.questId && c.userId = completion.userId
                && c.periodKey = completion.periodKey && c.status = PendingApproval)
        if not stillPending then data, None
        else
            let updated =
                data.completions
                |> List.map (fun c ->
                    if c.questId = completion.questId && c.userId = completion.userId && c.periodKey = completion.periodKey
                    then { c with status = Completed }
                    else c)
            let data', outcome = grantRewards { data with completions = updated } completion.userId quest rng
            data', Some outcome

/// Parent rejects — the quest becomes Available again for that period.
let reject (data: AppData) (completion: QuestCompletion) : AppData =
    { data with
        completions =
            data.completions
            |> List.filter (fun c ->
                not (c.questId = completion.questId && c.userId = completion.userId
                     && c.periodKey = completion.periodKey && c.status = PendingApproval)) }

// -------------------------------------------------------------------- shop

let buyCosmetic (data: AppData) (userId: string) (cosmeticId: string) : Result<AppData, string> =
    match data.users |> List.tryFind (fun u -> u.id = userId), cosmeticById cosmeticId with
    | None, _ -> Error "User not found."
    | _, None -> Error "Item not found."
    | Some user, Some cosmetic ->
        if user.inventory.owned |> List.contains cosmeticId then Error "Already owned!"
        elif user.coins < cosmetic.price then Error "Not enough coins yet — keep questing!"
        else
            let user' =
                { user with
                    coins = user.coins - cosmetic.price
                    inventory = { user.inventory with owned = cosmeticId :: user.inventory.owned } }
            // Buying can unlock the Collector badge.
            let data' = replaceUser data user'
            let newBadges = newlyEarnedBadges data' user'
            Ok (replaceUser data' { user' with badges = user'.badges @ newBadges })

let toggleEquip (data: AppData) (userId: string) (cosmeticId: string) : AppData =
    match data.users |> List.tryFind (fun u -> u.id = userId) with
    | None -> data
    | Some user when not (user.inventory.owned |> List.contains cosmeticId) -> data
    | Some user ->
        let inv = user.inventory
        let equipped =
            if inv.equipped |> List.contains cosmeticId
            then inv.equipped |> List.filter ((<>) cosmeticId)
            else cosmeticId :: inv.equipped
        replaceUser data { user with inventory = { inv with equipped = equipped } }

// ------------------------------------------------------------- arcade

/// Level required before the Arcade tab unlocks.
let arcadeUnlockLevel = 3

let arcadeUnlockedFor (user: User) =
    levelForXp user.xp >= arcadeUnlockLevel

let buyArcadeToken (data: AppData) (userId: string) : Result<AppData, string> =
    match data.users |> List.tryFind (fun u -> u.id = userId) with
    | None -> Error "User not found."
    | Some user when not (arcadeUnlockedFor user) -> Error "The Arcade unlocks at level 3!"
    | Some user when user.coins < Arcade.tokenCost -> Error "Not enough coins — quests pay for flights!"
    | Some user ->
        let arcade = arcadeOf user
        Ok (replaceUser data
                { user with
                    coins = user.coins - Arcade.tokenCost
                    arcade = Some { arcade with tokens = arcade.tokens + 1 } })

/// Spends one token to start a run. Returns None if no token available.
let spendArcadeToken (data: AppData) (userId: string) : AppData option =
    match data.users |> List.tryFind (fun u -> u.id = userId) with
    | Some user when (arcadeOf user).tokens >= 1 && arcadeUnlockedFor user ->
        let arcade = arcadeOf user
        Some (replaceUser data { user with arcade = Some { arcade with tokens = arcade.tokens - 1 } })
    | _ -> None

type ArcadeRunResult =
    { coinsEarned: int
      newBest: bool
      newBadges: string list }

// -- weekly scoreboard --------------------------------------------------

let arcadeScoresOf (data: AppData) = data.arcadeScores |> Option.defaultValue []

/// Keeps the best score per (user, game, week).
let recordArcadeScore (data: AppData) (userId: string) (game: string) (score: int) (today: DateTime) : AppData =
    let wk = weekKey today
    let scores = arcadeScoresOf data
    let scores' =
        match scores |> List.tryFind (fun s -> s.userId = userId && s.game = game && s.weekKey = wk) with
        | Some existing when existing.score >= score -> scores
        | Some _ ->
            scores
            |> List.map (fun s ->
                if s.userId = userId && s.game = game && s.weekKey = wk then { s with score = score } else s)
        | None -> { userId = userId; game = game; score = score; weekKey = wk } :: scores
    { data with arcadeScores = Some scores' }

/// This week's scores for a game, best first.
let weeklyScores (data: AppData) (game: string) (today: DateTime) : ArcadeScore list =
    arcadeScoresOf data
    |> List.filter (fun s -> s.game = game && s.weekKey = weekKey today)
    |> List.sortByDescending (fun s -> s.score)

/// Lifetime best for a game, from the weekly records.
let lifetimeBest (data: AppData) (userId: string) (game: string) : int =
    arcadeScoresOf data
    |> List.filter (fun s -> s.userId = userId && s.game = game)
    |> List.fold (fun best s -> max best s.score) 0

// -- run settlement ------------------------------------------------------

/// Settles a finished flight: stars become coins, a new best score pays a
/// bonus, records update, badges may unlock.
let finishArcadeRun (data: AppData) (userId: string) (score: int) (starsCollected: int) (today: DateTime) : AppData * ArcadeRunResult =
    match data.users |> List.tryFind (fun u -> u.id = userId) with
    | None -> data, { coinsEarned = 0; newBest = false; newBadges = [] }
    | Some user ->
        let arcade = arcadeOf user
        let newBest = score > arcade.bestScore
        let coinsEarned = starsCollected + (if newBest then Arcade.newBestBonus else 0)
        let updated =
            { user with
                coins = user.coins + coinsEarned
                arcade =
                    Some { tokens = arcade.tokens
                           bestScore = max arcade.bestScore score
                           totalRuns = arcade.totalRuns + 1 } }
        let data' = recordArcadeScore (replaceUser data updated) userId "flight" score today
        let newBadges = newlyEarnedBadges data' updated
        let final = { updated with badges = updated.badges @ newBadges }
        replaceUser data' final, { coinsEarned = coinsEarned; newBest = newBest; newBadges = newBadges }

/// Settles a finished memory game: fewer mistakes = more coins.
let finishMemoryRun (data: AppData) (userId: string) (game: Memory.Game) (today: DateTime) : AppData * ArcadeRunResult =
    match data.users |> List.tryFind (fun u -> u.id = userId) with
    | None -> data, { coinsEarned = 0; newBest = false; newBadges = [] }
    | Some user ->
        let score = Memory.score game
        let coinsEarned = Memory.coinsFor game
        let newBest = score > lifetimeBest data userId "memory"
        let arcade = arcadeOf user
        let updated =
            { user with
                coins = user.coins + coinsEarned
                arcade = Some { arcade with totalRuns = arcade.totalRuns + 1 } }
        let data' = recordArcadeScore (replaceUser data updated) userId "memory" score today
        let newBadges = newlyEarnedBadges data' updated
        let final = { updated with badges = updated.badges @ newBadges }
        replaceUser data' final, { coinsEarned = coinsEarned; newBest = newBest; newBadges = newBadges }

// ------------------------------------------------------------- prize shop

let prizesOf (data: AppData) = data.prizes |> Option.defaultValue []
let redemptionsOf (data: AppData) = data.redemptions |> Option.defaultValue []

let activePrizes (data: AppData) = prizesOf data |> List.filter (fun p -> p.active)

let addPrize (data: AppData) (prize: Prize) : AppData =
    { data with prizes = Some (prizesOf data @ [ prize ]) }

let setPrizeActive (data: AppData) (prizeId: string) (active: bool) : AppData =
    { data with
        prizes = Some (prizesOf data |> List.map (fun p -> if p.id = prizeId then { p with active = active } else p)) }

/// A child spends coins on a prize. Coins leave immediately; the parent
/// gets a "hand it over" item in Approvals.
let redeemPrize (data: AppData) (userId: string) (prizeId: string) (now: DateTime) : Result<AppData * Prize, string> =
    match data.users |> List.tryFind (fun u -> u.id = userId),
          prizesOf data |> List.tryFind (fun p -> p.id = prizeId) with
    | None, _ -> Error "User not found."
    | _, None -> Error "That prize is gone!"
    | _, Some prize when not prize.active -> Error "That prize isn't available right now."
    | Some user, Some prize when user.coins < prize.cost -> Error "Not enough coins yet — keep questing!"
    | Some user, Some prize ->
        let redemption =
            { id = "r-" + string now.Ticks
              prizeId = prize.id
              userId = userId
              redeemedAt = now.ToString("yyyy-MM-dd HH:mm")
              fulfilled = false }
        let data' =
            { replaceUser data { user with coins = user.coins - prize.cost } with
                redemptions = Some (redemption :: redemptionsOf data) }
        Ok (data', prize)

let pendingRedemptions (data: AppData) : (Redemption * Prize * User) list =
    redemptionsOf data
    |> List.filter (fun r -> not r.fulfilled)
    |> List.choose (fun r ->
        match prizesOf data |> List.tryFind (fun p -> p.id = r.prizeId),
              data.users |> List.tryFind (fun u -> u.id = r.userId) with
        | Some p, Some u -> Some (r, p, u)
        | _ -> None)

let fulfillRedemption (data: AppData) (redemptionId: string) : AppData =
    { data with
        redemptions =
            Some (redemptionsOf data
                  |> List.map (fun r -> if r.id = redemptionId then { r with fulfilled = true } else r)) }

/// Parent cancels a pending redemption — coins go back to the child.
let refundRedemption (data: AppData) (redemptionId: string) : AppData =
    match redemptionsOf data |> List.tryFind (fun r -> r.id = redemptionId && not r.fulfilled) with
    | None -> data
    | Some r ->
        let refund =
            prizesOf data
            |> List.tryFind (fun p -> p.id = r.prizeId)
            |> Option.map (fun p -> p.cost)
            |> Option.defaultValue 0
        let data' =
            match data.users |> List.tryFind (fun u -> u.id = r.userId) with
            | Some user -> replaceUser data { user with coins = user.coins + refund }
            | None -> data
        { data' with redemptions = Some (redemptionsOf data |> List.filter (fun x -> x.id <> redemptionId)) }

/// Deleting a prize refunds any pending redemptions of it first.
let deletePrize (data: AppData) (prizeId: string) : AppData =
    let data' =
        redemptionsOf data
        |> List.filter (fun r -> r.prizeId = prizeId && not r.fulfilled)
        |> List.fold (fun d r -> refundRedemption d r.id) data
    { data' with
        prizes = Some (prizesOf data' |> List.filter (fun p -> p.id <> prizeId))
        redemptions = Some (redemptionsOf data' |> List.filter (fun r -> r.prizeId <> prizeId)) }

// ----------------------------------------------------------- quest admin

let addQuest (data: AppData) (quest: Quest) : AppData =
    { data with quests = data.quests @ [ quest ] }

let setQuestActive (data: AppData) (questId: string) (active: bool) : AppData =
    { data with quests = data.quests |> List.map (fun q -> if q.id = questId then { q with active = active } else q) }

let deleteQuest (data: AppData) (questId: string) : AppData =
    { data with
        quests = data.quests |> List.filter (fun q -> q.id <> questId)
        completions = data.completions |> List.filter (fun c -> c.questId <> questId) }
