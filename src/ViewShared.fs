/// Shared UI components: XP bar, coin pill, quest cards, celebration overlay.
module QuestWorld.ViewShared

open Feliz
open QuestWorld.Domain
open QuestWorld.Progression
open QuestWorld.Catalog
open QuestWorld.State

let coinPill (coins: int) =
    Html.div [
        prop.className "coin-pill"
        prop.children [
            Html.span [ prop.className "coin-icon"; prop.text "🪙" ]
            Html.span [ prop.text (string coins) ]
        ]
    ]

let xpBar (xp: int) =
    let intoLevel, needed = levelProgress xp
    let pct = float intoLevel / float needed * 100.0
    Html.div [
        prop.className "xp-bar"
        prop.children [
            Html.div [
                prop.className "xp-fill"
                prop.style [ style.width (length.percent pct) ]
            ]
            Html.span [
                prop.className "xp-label"
                prop.text (sprintf "%d / %d XP" intoLevel needed)
            ]
        ]
    ]

let levelChip (xp: int) =
    Html.div [
        prop.className "level-chip"
        prop.text (sprintf "Lv %d" (levelForXp xp))
    ]

/// Avatar with equipped cosmetics floating around it.
let avatarDisplay (user: User) (big: bool) =
    let stage = avatarStageFor user.theme (levelForXp user.xp)
    let equippedIcons =
        user.inventory.equipped
        |> List.choose cosmeticById
        |> List.map (fun c -> c.icon)
    Html.div [
        prop.className (if big then "avatar-wrap avatar-big" else "avatar-wrap")
        prop.children [
            Html.div [ prop.className "avatar-emoji"; prop.text stage.emoji ]
            if not (List.isEmpty equippedIcons) then
                Html.div [
                    prop.className "avatar-cosmetics"
                    prop.children (equippedIcons |> List.map (fun i -> Html.span [ prop.text i ]))
                ]
        ]
    ]

let rewardChips (reward: Reward) =
    Html.div [
        prop.className "reward-chips"
        prop.children [
            Html.span [ prop.className "chip chip-xp"; prop.text (sprintf "+%d XP" reward.xp) ]
            if reward.coins > 0 then
                Html.span [ prop.className "chip chip-coin"; prop.text (sprintf "+%d 🪙" reward.coins) ]
        ]
    ]

/// Deterministic pseudo-random confetti so re-renders don't reshuffle.
let confetti (palette: string list) =
    Html.div [
        prop.className "confetti-layer"
        prop.children [
            for i in 0 .. 39 do
                let left = (i * 53 + 17) % 100
                let delay = float ((i * 37) % 90) / 100.0
                let dur = 1.6 + float ((i * 29) % 100) / 100.0
                let color = palette |> List.item (i % List.length palette)
                Html.span [
                    prop.className "confetti-piece"
                    prop.style [
                        style.left (length.percent left)
                        style.backgroundColor color
                        style.custom ("animationDelay", sprintf "%.2fs" delay)
                        style.custom ("animationDuration", sprintf "%.2fs" dur)
                    ]
                ]
        ]
    ]

let private lootLine (loot: LootResult) =
    match loot with
    | CoinDrop c -> sprintf "🎁 Loot box! +%d bonus coins!" c
    | XpBoost x -> sprintf "🎁 Loot box! +%d bonus XP!" x
    | CosmeticDrop id ->
        match cosmeticById id with
        | Some cos -> sprintf "🎁 RARE LOOT! You found: %s %s!" cos.icon cos.name
        | None -> "🎁 Loot box!"

/// Full-screen celebration overlay after completing a quest.
let celebrationOverlay (model: Model) (celebration: Celebration) dispatch =
    let user = model.data.users |> List.tryFind (fun u -> u.id = celebration.forUser)
    let o = celebration.outcome
    let palette =
        match user |> Option.map (fun u -> u.theme) with
        | Some BlockCraft -> [ "#5bc236"; "#8b5a2b"; "#3aa0ff"; "#ffd700"; "#e74c3c" ]
        | _ -> [ "#ff9ecf"; "#c9a0ff"; "#ffd700"; "#9fe8ff"; "#ffb6e1" ]
    Html.div [
        prop.className "celebration-overlay"
        prop.children [
            confetti palette
            Html.div [
                prop.className "celebration-card pop-in"
                prop.children [
                    if o.pendingApproval then
                        Html.div [ prop.className "celebration-emoji"; prop.text "📨" ]
                        Html.h2 [ prop.text "Sent to the Quest Master!" ]
                        Html.p [ prop.className "celebration-sub"
                                 prop.text (sprintf "“%s” is waiting for approval. Rewards land the moment it's approved!" o.quest.title) ]
                    else
                        match user with
                        | Some u -> avatarDisplay u true
                        | None -> Html.none
                        Html.h2 [ prop.text "Quest Complete!" ]
                        Html.p [ prop.className "celebration-sub"; prop.text o.quest.title ]
                        Html.div [
                            prop.className "celebration-rewards"
                            prop.children [
                                Html.div [ prop.className "reward-line xp-line"; prop.text (sprintf "+%d XP" o.reward.xp) ]
                                if o.reward.coins > 0 then
                                    Html.div [ prop.className "reward-line coin-line"; prop.text (sprintf "+%d 🪙" o.reward.coins) ]
                            ]
                        ]
                        match o.loot with
                        | Some loot -> Html.div [ prop.className "loot-line shimmer"; prop.text (lootLine loot) ]
                        | None -> Html.none
                        if o.levelAfter > o.levelBefore then
                            Html.div [
                                prop.className "levelup-banner"
                                prop.children [
                                    Html.div [ prop.text (sprintf "⬆️ LEVEL UP! You are now level %d!" o.levelAfter) ]
                                    match user with
                                    | Some u ->
                                        let stage = avatarStageFor u.theme o.levelAfter
                                        let prev = avatarStageFor u.theme o.levelBefore
                                        if stage.name <> prev.name then
                                            Html.div [ prop.className "evolve-line"
                                                       prop.text (sprintf "%s Your avatar evolved into %s!" stage.emoji stage.name) ]
                                        else Html.none
                                    | None -> Html.none
                                    if o.levelBefore < 3 && o.levelAfter >= 3 then
                                        Html.div [ prop.className "evolve-line"
                                                   prop.text "🎮 The ARCADE is now OPEN! Check your new tab!" ]
                                ]
                            ]
                        for badgeId in o.newBadges do
                            match badgeById badgeId with
                            | Some b ->
                                Html.div [ prop.className "badge-line"
                                           prop.text (sprintf "%s New badge: %s!" b.icon b.name) ]
                            | None -> Html.none
                    Html.button [
                        prop.className "btn btn-primary btn-big"
                        prop.text (if o.pendingApproval then "Okay!" else "Awesome!")
                        prop.onClick (fun _ -> dispatch DismissCelebration)
                    ]
                ]
            ]
        ]
    ]
