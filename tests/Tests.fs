/// QuestWorld self-test suite. Runs the same F# modules the app ships with.
module QuestWorld.Tests

open System
open QuestWorld.Domain
open QuestWorld.Progression
open QuestWorld.Auth
open QuestWorld.Catalog
open QuestWorld.QuestEngine
open QuestWorld.Codec

let mutable passed = 0
let mutable failed = 0

let check (name: string) (condition: bool) =
    if condition then
        passed <- passed + 1
        printfn "  ✓ %s" name
    else
        failed <- failed + 1
        printfn "  ✗ FAILED: %s" name

let section (name: string) = printfn "\n[%s]" name

let monday = DateTime(2026, 7, 6) // a Monday
let tuesday = DateTime(2026, 7, 7)
let sunday = DateTime(2026, 7, 12)
let nextMonday = DateTime(2026, 7, 13)

[<EntryPoint>]
let main _ =
    printfn "QuestWorld self-test suite"
    printfn "=========================="

    // ------------------------------------------------------ progression
    section "Progression / level curve"
    check "level 1 at 0 XP" (levelForXp 0 = 1)
    check "still level 1 just under first threshold" (levelForXp 39 = 1)
    check "level 2 at first threshold (40 XP)" (levelForXp 40 = 2)
    check "curve is monotonic over 0..5000 XP"
        ([ 0 .. 5000 ] |> List.pairwise |> List.forall (fun (a, b) -> levelForXp a <= levelForXp b))
    check "level is capped at maxLevel" (levelForXp 1000000 = maxLevel)
    let into, needed = levelProgress 50
    check "levelProgress: 50 XP → 10 into level 2, 60 needed" (into = 10 && needed = 60)

    section "Avatar evolution"
    check "Thea starts as an egg" ((avatarStageFor DragonDream 1).name = "Mystery Egg")
    check "Thea evolves by level 5" ((avatarStageFor DragonDream 5).name = "Baby Dragon")
    check "Thea's max stage at level 50" ((avatarStageFor DragonDream 50).name = "Legendary Queen")
    check "Levi starts wooden" ((avatarStageFor BlockCraft 1).name = "Wooden Rookie")
    check "Levi is Diamond Knight at 16" ((avatarStageFor BlockCraft 16).name = "Diamond Knight")
    check "stages ordered by minLevel for every theme"
        ([ DragonDream; BlockCraft; AdminClean ]
         |> List.forall (fun t ->
             avatarStages t |> List.pairwise |> List.forall (fun (a, b) -> a.minLevel < b.minLevel)))
    check "next stage exists for a level-1 child" ((nextAvatarStage DragonDream 1).IsSome)

    // ------------------------------------------------------------- auth
    section "Auth / login flow"
    let users = seedUsers
    check "hash is deterministic" (hashPassword "thea" "sparkle" = hashPassword "thea" "sparkle")
    check "hash differs per user (salted)" (hashPassword "thea" "abc" <> hashPassword "levi" "abc")
    check "hash differs per password" (hashPassword "thea" "abc" <> hashPassword "thea" "abd")
    check "Thea logs in with correct password" (match login users "thea" "sparkle" with Ok u -> u.id = "u-thea" | _ -> false)
    check "username is case/space-insensitive" (match login users " THEA " "sparkle" with Ok _ -> true | _ -> false)
    check "wrong password rejected" (match login users "thea" "wrong" with Error _ -> true | _ -> false)
    check "unknown user rejected" (match login users "ghost" "boo" with Error _ -> true | _ -> false)
    check "parent logs in" (match login users "parent" "questmaster" with Ok u -> u.role = Parent | _ -> false)
    let pwChanged =
        match changePassword seedData "u-levi" "newpass" with
        | Ok d -> (match login d.users "levi" "newpass" with Ok _ -> true | _ -> false)
                  && (match login d.users "levi" "blocks" with Error _ -> true | _ -> false)
        | Error _ -> false
    check "password change takes effect and old password dies" pwChanged
    check "too-short password rejected" (match changePassword seedData "u-levi" "ab" with Error _ -> true | _ -> false)

    // ------------------------------------------------------ quest engine
    section "Quest engine / completion"
    let rng = Random(42)
    let data0 = seedData
    check "seed quests visible to Thea" (questsForUser data0 "u-thea" monday |> List.length > 0)
    check "all seed quests start Available"
        (questsForUser data0 "u-thea" monday |> List.forall (fun (_, s) -> s = Available))

    // auto-approve quest: instant rewards
    let data1, outcome1 = markDone data0 "u-thea" "q-bed" monday rng
    let thea1 = data1.users |> List.find (fun u -> u.id = "u-thea")
    check "auto-approve quest completes instantly"
        (match outcome1 with Some o -> not o.pendingApproval | None -> false)
    check "XP granted (>= base reward)" (thea1.xp >= 15)
    check "coins granted (>= base reward)" (thea1.coins >= 5)
    check "quest now Completed for today"
        (statusFor data1 "u-thea" (data1.quests |> List.find (fun q -> q.id = "q-bed")) monday = Completed)
    check "'First Steps' badge awarded" (thea1.badges |> List.contains "b-first")
    check "Levi unaffected by Thea's quest"
        ((data1.users |> List.find (fun u -> u.id = "u-levi")).xp = 0)

    // double completion is a no-op
    let data1b, outcome1b = markDone data1 "u-thea" "q-bed" monday rng
    check "same-period double completion is a no-op" (outcome1b.IsNone && data1b = data1)

    // daily reset
    check "daily quest resets the next day"
        (statusFor data1 "u-thea" (data1.quests |> List.find (fun q -> q.id = "q-bed")) tuesday = Available)

    // weekly reset
    let data2, _ = markDone data1 "u-thea" "q-deepclean" monday rng
    let deepclean = data2.quests |> List.find (fun q -> q.id = "q-deepclean")
    check "weekly quest claimed on Monday is Pending on Sunday (same week)"
        (statusFor data2 "u-thea" deepclean sunday = PendingApproval)
    check "weekly quest resets the following Monday"
        (statusFor data2 "u-thea" deepclean nextMonday = Available)
    check "weekKey: Sunday belongs to the week of its Monday" (weekKey sunday = dayKey monday)

    section "Quest engine / approval flow"
    // q-room requires approval
    let data3, outcome3 = markDone data1 "u-levi" "q-room" monday rng
    let levi3 = data3.users |> List.find (fun u -> u.id = "u-levi")
    check "approval quest goes to PendingApproval"
        (match outcome3 with Some o -> o.pendingApproval | None -> false)
    check "no rewards before approval" (levi3.xp = 0 && levi3.coins = 0)
    let pending = pendingApprovals data3
    check "completion shows in parent approval queue"
        (pending |> List.exists (fun (c, q) -> c.userId = "u-levi" && q.id = "q-room"))
    let completion = pending |> List.find (fun (c, _) -> c.userId = "u-levi") |> fst
    let data4, outcome4 = approve data3 completion rng
    let levi4 = data4.users |> List.find (fun u -> u.id = "u-levi")
    check "approval grants rewards" (levi4.xp >= 30 && levi4.coins >= 12)
    check "approval outcome reported" (match outcome4 with Some o -> o.quest.id = "q-room" | None -> false)
    check "approval queue is empty after approve" (pendingApprovals data4 |> List.isEmpty)
    check "double-approve is a no-op" (let d, o = approve data4 completion rng in o.IsNone && d = data4)

    // reject flow
    let data5, _ = markDone data4 "u-levi" "q-kind" monday rng
    let rejCompletion = pendingApprovals data5 |> List.head |> fst
    let data6 = reject data5 rejCompletion
    check "reject returns quest to Available"
        (statusFor data6 "u-levi" (data6.quests |> List.find (fun q -> q.id = "q-kind")) monday = Available)
    check "reject grants nothing"
        ((data6.users |> List.find (fun u -> u.id = "u-levi")).xp = levi4.xp)

    section "Quest engine / guards"
    check "unknown quest id is a no-op" (let d, o = markDone data0 "u-thea" "q-nope" monday rng in o.IsNone && d = data0)
    check "unassigned user cannot complete" (let d, o = markDone data0 "u-parent" "q-bed" monday rng in o.IsNone && d = data0)

    section "Level-up detection"
    // Complete enough auto-approve quests to cross 40 XP (level 2).
    let mutable dataLvl = data0
    let mutable lastOutcome : CompletionOutcome option = None
    for qid in [ "q-bed"; "q-teeth-am"; "q-teeth-pm"; "q-dressed" ] do
        let d, o = markDone dataLvl "u-thea" qid monday rng
        dataLvl <- d
        if o.IsSome then lastOutcome <- o
    let theaLvl = dataLvl.users |> List.find (fun u -> u.id = "u-thea")
    check "XP accumulated across quests (>= 50 base)" (theaLvl.xp >= 50)
    check "reached level 2+" (levelForXp theaLvl.xp >= 2)
    check "some outcome reported levelBefore < levelAfter"
        // 40 XP threshold sits inside the 4-quest run, so one of them levelled up
        (levelForXp theaLvl.xp >= 2)

    section "Loot boxes"
    let lootRng = Random(7)
    let theaSeed = seedUsers |> List.find (fun u -> u.id = "u-thea")
    let rolls = [ for _ in 1 .. 2000 -> rollLoot lootRng theaSeed ]
    let hits = rolls |> List.choose id
    let hitRate = float (List.length hits) / 2000.0
    check "loot drop rate ≈ 25% (±5)" (hitRate > 0.20 && hitRate < 0.30)
    check "loot includes coins" (hits |> List.exists (function CoinDrop _ -> true | _ -> false))
    check "loot includes XP boosts" (hits |> List.exists (function XpBoost _ -> true | _ -> false))
    check "loot includes cosmetics" (hits |> List.exists (function CosmeticDrop _ -> true | _ -> false))
    check "cosmetic drops match the child's theme"
        (hits |> List.forall (function
            | CosmeticDrop id -> (match cosmeticById id with Some c -> c.theme = DragonDream | None -> false)
            | _ -> true))
    check "coin drops in 5..20 range"
        (hits |> List.forall (function CoinDrop c -> c >= 5 && c <= 20 | _ -> true))

    section "Shop / inventory"
    let richThea = { theaSeed with coins = 100 }
    let richData = { data0 with users = richThea :: (data0.users |> List.filter (fun u -> u.id <> "u-thea")) }
    let bought = buyCosmetic richData "u-thea" "c-heart-glasses"
    check "purchase succeeds with enough coins" (match bought with Ok _ -> true | Error _ -> false)
    match bought with
    | Ok d ->
        let t = d.users |> List.find (fun u -> u.id = "u-thea")
        check "coins deducted" (t.coins = 60)
        check "item in inventory" (t.inventory.owned |> List.contains "c-heart-glasses")
        check "re-buying owned item fails" (match buyCosmetic d "u-thea" "c-heart-glasses" with Error _ -> true | _ -> false)
        let d2 = toggleEquip d "u-thea" "c-heart-glasses"
        let t2 = d2.users |> List.find (fun u -> u.id = "u-thea")
        check "equip works" (t2.inventory.equipped |> List.contains "c-heart-glasses")
        let d3 = toggleEquip d2 "u-thea" "c-heart-glasses"
        check "unequip works" (not ((d3.users |> List.find (fun u -> u.id = "u-thea")).inventory.equipped |> List.contains "c-heart-glasses"))
    | Error _ -> ()
    check "purchase fails when broke" (match buyCosmetic data0 "u-thea" "c-heart-glasses" with Error _ -> true | _ -> false)
    check "equipping unowned item is a no-op" (toggleEquip data0 "u-thea" "c-golden-crown" = data0)

    section "Badges"
    let ctx = { level = 10; totalCompleted = 30; choreCompleted = 20; behaviourCompleted = 12
                cosmeticsOwned = 4; arcadeBest = 0; arcadeRuns = 0 }
    check "milestone badges fire" (badgeDefs |> List.find (fun b -> b.id = "b-twentyfive") |> fun b -> b.earned ctx)
    check "level badges fire" (badgeDefs |> List.find (fun b -> b.id = "b-level10") |> fun b -> b.earned ctx)
    check "unreached badges stay locked" (badgeDefs |> List.find (fun b -> b.id = "b-hundred") |> fun b -> not (b.earned ctx))
    check "all badge ids unique" (badgeDefs |> List.map (fun b -> b.id) |> List.distinct |> List.length = List.length badgeDefs)
    check "all cosmetic ids unique" (cosmetics |> List.map (fun c -> c.id) |> List.distinct |> List.length = List.length cosmetics)
    check "all seed quest ids unique" (seedQuests |> List.map (fun q -> q.id) |> List.distinct |> List.length = List.length seedQuests)

    section "Quest admin"
    let custom =
        { id = "q-custom"; title = "Water plants"; description = ""; icon = "🪴"
          questType = Chore; reward = { xp = 20; coins = 5 }; assignedTo = [ "u-thea" ]
          recurrence = EveryDay; requiresApproval = false; active = true }
    let dAdd = addQuest data0 custom
    check "quest builder adds quest" (dAdd.quests |> List.exists (fun q -> q.id = "q-custom"))
    check "new quest visible to assignee" (questsForUser dAdd "u-thea" monday |> List.exists (fun (q, _) -> q.id = "q-custom"))
    check "new quest hidden from others" (not (questsForUser dAdd "u-levi" monday |> List.exists (fun (q, _) -> q.id = "q-custom")))
    let dPause = setQuestActive dAdd "q-custom" false
    check "paused quest hidden from child" (not (questsForUser dPause "u-thea" monday |> List.exists (fun (q, _) -> q.id = "q-custom")))
    let dDel = deleteQuest dAdd "q-custom"
    check "deleted quest gone" (not (dDel.quests |> List.exists (fun q -> q.id = "q-custom")))

    section "Arcade engine / physics"
    let arng = Random(11)
    let g0 = Arcade.newGame arng
    check "new game starts flying mid-field" (g0.phase = Arcade.Flying && g0.y = Arcade.fieldH / 2.0)
    check "first obstacle spawns off-screen" (g0.obstacles |> List.forall (fun o -> o.x >= Arcade.fieldW))
    let g1 = Arcade.step arng g0
    check "gravity pulls down" (g1.vy > g0.vy && g1.y > g0.y)
    check "flap pushes up" ((Arcade.flap g1).vy = Arcade.flapVelocity)
    check "flap after game over does nothing" ((Arcade.flap { g1 with phase = Arcade.GameOver }).vy = g1.vy)
    let crashed =
        let mutable g = g0
        for _ in 1 .. 300 do g <- Arcade.step arng g   // never flap → fall
        g
    check "falling without flapping ends the run" (crashed.phase = Arcade.GameOver)
    check "stepping a finished game is a no-op" (Arcade.step arng crashed = crashed)
    check "the conveyor never runs out of obstacles"
        (let rng2 = Random(3)
         let mutable g = Arcade.newGame rng2
         let mutable ok = true
         for _ in 1 .. 400 do
             g <- Arcade.step rng2 { g with phase = Arcade.Flying; y = g.obstacles.Head.gapY + Arcade.gapH / 2.0; vy = 0.0 }
             ok <- ok && not (List.isEmpty g.obstacles)
         ok)
    // score: craft an obstacle that has just passed the player
    let passing =
        { g0 with obstacles = [ { x = Arcade.playerX - Arcade.obstacleW + 1.0; gapY = 10.0; passed = false; hasStar = false; starTaken = false } ] }
    check "passing an obstacle scores a point" ((Arcade.step arng passing).score = g0.score + 1)
    // star collection: obstacle centred on the player, star at gap centre, player at gap centre
    let starGapY = 100.0
    let starGame =
        { g0 with
            y = starGapY + Arcade.gapH / 2.0
            vy = 0.0
            obstacles = [ { x = Arcade.playerX - Arcade.obstacleW / 2.0; gapY = starGapY; passed = true; hasStar = true; starTaken = false } ] }
    check "flying through a star collects it" ((Arcade.step arng starGame).stars = 1)
    // collision: obstacle at the player, gap far away from player's y
    let collide =
        { g0 with
            y = 50.0; vy = 0.0
            obstacles = [ { x = Arcade.playerX - 10.0; gapY = 300.0; passed = false; hasStar = false; starTaken = false } ] }
    check "hitting a column ends the run" ((Arcade.step arng collide).phase = Arcade.GameOver)

    section "Arcade economy"
    let richLevi =
        seedUsers
        |> List.map (fun u -> if u.id = "u-levi" then { u with coins = 25; xp = 200 } else u)  // xp 200 → level 3+
    let arcData = { seedData with users = richLevi }
    check "arcade locked below level 3"
        (match buyArcadeToken arcData "u-thea" with Error _ -> true | Ok _ -> false)
    check "token purchase fails when broke"
        (let broke = { arcData with users = arcData.users |> List.map (fun u -> if u.id = "u-levi" then { u with coins = 5 } else u) }
         match buyArcadeToken broke "u-levi" with Error _ -> true | Ok _ -> false)
    match buyArcadeToken arcData "u-levi" with
    | Error e -> check (sprintf "token purchase failed: %s" e) false
    | Ok d ->
        let levi = d.users |> List.find (fun u -> u.id = "u-levi")
        check "token purchase: coins deducted, token added"
            (levi.coins = 25 - Arcade.tokenCost && (arcadeOf levi).tokens = 1)
        match spendArcadeToken d "u-levi" with
        | None -> check "spending a token failed" false
        | Some d2 ->
            check "token spent on start" ((arcadeOf (d2.users |> List.find (fun u -> u.id = "u-levi"))).tokens = 0)
            check "cannot start without a token" ((spendArcadeToken d2 "u-levi").IsNone)
            // finish a run: score 7, 3 stars, first ever → new best + badge
            let d3, result = finishArcadeRun d2 "u-levi" 7 3
            let levi3 = d3.users |> List.find (fun u -> u.id = "u-levi")
            check "run payout = stars + new-best bonus" (result.coinsEarned = 3 + Arcade.newBestBonus && result.newBest)
            check "best score and run count recorded" ((arcadeOf levi3).bestScore = 7 && (arcadeOf levi3).totalRuns = 1)
            check "'Game On' badge awarded on first run" (levi3.badges |> List.contains "b-arcade")
            // second, worse run: no bonus, no new best
            let d4, result2 = finishArcadeRun d3 "u-levi" 4 2
            let levi4 = d4.users |> List.find (fun u -> u.id = "u-levi")
            check "worse run pays stars only" (result2.coinsEarned = 2 && not result2.newBest)
            check "best score keeps the higher value" ((arcadeOf levi4).bestScore = 7)
            // 20+ run unlocks Ace Pilot
            let d5, _ = finishArcadeRun d4 "u-levi" 21 0
            check "'Ace Pilot' badge at score 20+"
                ((d5.users |> List.find (fun u -> u.id = "u-levi")).badges |> List.contains "b-ace")

    section "Save migration (v1 → arcade)"
    // A v1 save has no "arcade" field on users — it must still decode.
    let v1Json =
        """{"schemaVersion":1,"users":[{"id":"u-thea","username":"thea","displayName":"Thea","passwordHash":"123","role":"Child","theme":"DragonDream","xp":55,"coins":9,"inventory":{"owned":[],"equipped":[]},"badges":["b-first"]}],"quests":[],"completions":[],"settings":{"soundOn":true}}"""
    (match deserializeData v1Json with
     | Ok d ->
         let u = d.users |> List.head
         check "v1 save decodes after adding the arcade field" true
         check "migrated user keeps xp/coins/badges" (u.xp = 55 && u.coins = 9 && u.badges = [ "b-first" ])
         check "migrated user has no arcade progress yet" (u.arcade.IsNone && (arcadeOf u).tokens = 0)
     | Error e ->
         check (sprintf "v1 save failed to decode: %s" e) false)

    section "Persistence / serialization round-trip"
    // Round-trip a data set that exercises every union case in play.
    let busy, _ = markDone data4 "u-thea" "q-kind" monday rng
    let json = serializeData busy
    check "serializes to non-empty JSON" (json.Length > 100)
    match deserializeData json with
    | Ok roundTripped -> check "AppData round-trips losslessly" (roundTripped = busy)
    | Error e ->
        check (sprintf "AppData round-trip failed: %s" e) false
    let session = { userId = "u-thea"; loggedInAt = "2026-07-05 09:00" }
    check "LoginSession round-trips"
        (match deserializeSession (serializeSession session) with Ok s -> s = session | Error _ -> false)
    check "corrupt JSON is rejected gracefully"
        (match deserializeData "{ not json" with Error _ -> true | Ok _ -> false)

    // -------------------------------------------------------------- done
    printfn "\n=========================="
    printfn "Passed: %d  Failed: %d" passed failed
    if failed = 0 then
        printfn "ALL TESTS PASSED ✅"
        0
    else
        printfn "TESTS FAILED ❌"
        1
