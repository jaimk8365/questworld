/// Child game UI — themed for Thea (DragonDream) and Levi (BlockCraft).
module QuestWorld.ViewChild

open System
open Feliz
open QuestWorld.Domain
open QuestWorld.Progression
open QuestWorld.QuestEngine
open QuestWorld.Catalog
open QuestWorld.Adventure
open QuestWorld.Story
open QuestWorld.State
open QuestWorld.ViewShared

/// Themed copy so each world feels hand-made.
let private sectionTitle (theme: ProfileTheme) (qtype: QuestType) =
    match theme, qtype with
    | DragonDream, Daily -> "✨ Today's Magic"
    | DragonDream, Weekly -> "🌙 This Week's Wonders"
    | DragonDream, Behaviour -> "💖 Heart Quests"
    | DragonDream, Chore -> "🏰 Castle Care"
    | DragonDream, Bonus -> "🎁 Secret Missions"
    | _, Daily -> "⛏️ Daily Missions"
    | _, Weekly -> "🗺️ Weekly Expeditions"
    | _, Behaviour -> "🛡️ Hero Training"
    | _, Chore -> "🏗️ Base Upkeep"
    | _, Bonus -> "💎 Treasure Hunts"

let private encouragement (theme: ProfileTheme) =
    match theme with
    | DragonDream -> "Your dragon believes in you! 🐲"
    | _ -> "New blocks to mine, hero! ⛏️"

let private questCard (theme: ProfileTheme) (quest: Quest, status: QuestStatus) dispatch =
    Html.div [
        prop.className (
            match status with
            | Available | Rejected -> "quest-card"
            | PendingApproval -> "quest-card q-pending"
            | Completed -> "quest-card q-done")
        prop.children [
            Html.div [ prop.className "q-icon"; prop.text quest.icon ]
            Html.div [
                prop.className "q-info"
                prop.children [
                    Html.div [ prop.className "q-title"; prop.text quest.title ]
                    if quest.description <> "" then
                        Html.div [ prop.className "q-desc"; prop.text quest.description ]
                    rewardChips quest.reward
                ]
            ]
            match status with
            | Available | Rejected ->
                Html.button [
                    prop.className "btn done-btn"
                    prop.text "I did it!"
                    prop.onClick (fun _ -> dispatch (CompleteQuest quest.id))
                ]
            | PendingApproval ->
                Html.div [ prop.className "status-tag tag-pending"; prop.text "⏳ Checking…" ]
            | Completed ->
                Html.div [ prop.className "status-tag tag-done"; prop.text "✅ Done!" ]
        ]
    ]

let private questsTab (model: Model) (user: User) dispatch =
    let quests = questsForUser model.data user.id DateTime.Now
    let featured = featuredMissions model.data user.id DateTime.Now
    let adventure = adventureProgress model.data user.id DateTime.Now
    let doneCount = quests |> List.filter (fun (_, s) -> s = Completed) |> List.length
    let total = List.length quests
    let sections =
        [ Daily; Chore; Behaviour; Weekly; Bonus ]
        |> List.choose (fun t ->
            match quests |> List.filter (fun (q, _) -> q.questType = t) with
            | [] -> None
            | qs -> Some (t, qs))
    Html.div [
        prop.className "quests-tab"
        prop.children [
            match welcomeBack model.data user.id DateTime.Now with
            | Some welcome ->
                Html.div [
                    prop.className "welcome-back"
                    prop.children [
                        Html.h2 [ prop.text welcome.title ]
                        Html.p [ prop.text welcome.message ]
                    ]
                ]
            | None -> Html.none
            Html.section [
                prop.className "adventure-card"
                prop.children [
                    Html.div [
                        prop.className "adventure-heading"
                        prop.children [
                            Html.div [
                                Html.h2 [ prop.text "Today's Adventure" ]
                                Html.p [ prop.text "Three missions, one little adventure. Every other quest is still below." ]
                            ]
                            Html.span [ prop.className "adventure-count"; prop.text (sprintf "%d/%d" adventure.doneCount adventure.totalCount) ]
                        ]
                    ]
                    Html.div [ prop.className "adventure-meter"; prop.children [
                        Html.div [ prop.className "adventure-meter-fill"; prop.style [ style.width (length.percent (if adventure.totalCount = 0 then 0.0 else float adventure.doneCount / float adventure.totalCount * 100.0)) ] ]
                    ] ]
                    for mission in featured do questCard user.theme mission dispatch
                    if adventure.complete then Html.div [ prop.className "adventure-complete"; prop.text "Adventure complete! Your world is growing ✨" ]
                ]
            ]
            Html.div [
                prop.className "day-progress"
                prop.children [
                    Html.span [ prop.text (sprintf "🔥 %d of %d quests done" doneCount total) ]
                    Html.div [
                        prop.className "day-bar"
                        prop.children [
                            Html.div [
                                prop.className "day-fill"
                                prop.style [ style.width (length.percent (if total = 0 then 0.0 else float doneCount / float total * 100.0)) ]
                            ]
                        ]
                    ]
                ]
            ]
            Html.h2 [ prop.className "all-quests-title"; prop.text "All Quests" ]
            if List.isEmpty sections then
                Html.p [ prop.className "empty-note"; prop.text "No quests yet — ask the Quest Master!" ]
            for (qtype, qs) in sections do
                Html.div [
                    prop.className "quest-section"
                    prop.children [
                        Html.h3 [ prop.className "section-title"; prop.text (sectionTitle user.theme qtype) ]
                        Html.div [ prop.children (qs |> List.map (fun q -> questCard user.theme q dispatch)) ]
                    ]
                ]
            Html.p [ prop.className "encourage"; prop.text (encouragement user.theme) ]
        ]
    ]

let private mapTab (model: Model) (user: User) =
    let chapter = chapterFor user.theme
    let progress = chapterProgress model.data user.id chapter
    let places =
        match user.theme with
        | DragonDream -> [ "Dragon Nest", "🐉", true; "Moon Forest", "🌙", progress.completedSteps >= 1; "Crystal Garden", "🌸", progress.completedSteps >= 2; "Star Castle", "🏰", progress.complete ]
        | _ -> [ "Base Camp", "🏡", true; "The Mines", "⛏️", progress.completedSteps >= 1; "Village", "🏘️", progress.completedSteps >= 2; "Hidden Fortress", "🏰", progress.complete ]
    Html.div [
        prop.className "world-map"
        prop.children [
            Html.div [ prop.className "map-intro"; prop.children [
                Html.h2 [ prop.text "QuestWorld Map" ]
                Html.p [ prop.text "Complete story missions to reveal more of your world." ]
            ] ]
            Html.div [ prop.className "map-path"; prop.children [
                for (name, icon, unlocked) in places do
                    Html.div [
                        prop.className (if unlocked then "map-place unlocked" else "map-place locked")
                        prop.children [
                            Html.div [ prop.className "map-place-icon"; prop.text (if unlocked then icon else "🔒") ]
                            Html.div [ prop.className "map-place-name"; prop.text name ]
                            Html.div [ prop.className "map-place-state"; prop.text (if unlocked then "Discovered" else "Story locked") ]
                        ]
                    ]
            ] ]
            Html.section [ prop.className "story-card"; prop.children [
                Html.div [ prop.className "story-kicker"; prop.text "CHAPTER 1" ]
                Html.h2 [ prop.text chapter.title ]
                Html.p [ prop.text chapter.intro ]
                Html.div [ prop.className "story-progress"; prop.text (sprintf "%d of %d missions complete" progress.completedSteps progress.totalSteps) ]
                for i, step in chapter.steps |> List.indexed do
                    Html.div [ prop.className (if i < progress.completedSteps then "story-step done" elif i = progress.completedSteps then "story-step current" else "story-step locked"); prop.children [
                        Html.span [ prop.className "story-step-icon"; prop.text (if i < progress.completedSteps then "✅" elif i = progress.completedSteps then step.icon else "🔒") ]
                        Html.div [ Html.strong step.title; Html.p step.description ]
                    ] ]
                if progress.complete then Html.div [ prop.className "chapter-reward"; prop.text ("🏆 " + chapter.rewardText) ]
            ] ]
        ]
    ]

let private prizeSection (model: Model) (user: User) dispatch =
    let prizes = activePrizes model.data
    if List.isEmpty prizes then Html.none
    else
        Html.div [
            prop.children [
                Html.h3 [ prop.className "section-title"; prop.text "🎁 Real Prizes" ]
                Html.p [ prop.className "prize-note"; prop.text "Spend your coins on real-life rewards — the Quest Master hands them over!" ]
                Html.div [
                    prop.children [
                        for prize in prizes do
                            Html.div [
                                prop.className "prize-row"
                                prop.children [
                                    Html.span [ prop.className "prize-icon"; prop.text prize.icon ]
                                    Html.div [
                                        prop.className "prize-info"
                                        prop.children [ Html.div [ prop.className "prize-title"; prop.text prize.title ] ]
                                    ]
                                    Html.button [
                                        prop.className "btn btn-small btn-buy"
                                        prop.disabled (user.coins < prize.cost)
                                        prop.text (sprintf "%d 🪙" prize.cost)
                                        prop.onClick (fun _ -> dispatch (RedeemPrize prize.id))
                                    ]
                                ]
                            ]
                    ]
                ]
            ]
        ]

let private shopTab (model: Model) (user: User) dispatch =
    let items = cosmeticsForTheme user.theme
    Html.div [
        prop.className "shop-tab"
        prop.children [
            Html.h3 [ prop.className "section-title"
                      prop.text (match user.theme with DragonDream -> "🏪 The Sparkle Shop" | _ -> "🏪 The Trading Post") ]
            match model.shopMessage with
            | Some m -> Html.div [ prop.className "shop-message"; prop.text m ]
            | None -> Html.none
            Html.div [
                prop.className "shop-grid"
                prop.children [
                    for item in items do
                        let owned = user.inventory.owned |> List.contains item.id
                        let equipped = user.inventory.equipped |> List.contains item.id
                        Html.div [
                            prop.className (if owned then "shop-item owned" else "shop-item")
                            prop.children [
                                Html.div [ prop.className "shop-icon"; prop.text item.icon ]
                                Html.div [ prop.className "shop-name"; prop.text item.name ]
                                if owned then
                                    Html.button [
                                        prop.className (if equipped then "btn btn-small btn-equipped" else "btn btn-small")
                                        prop.text (if equipped then "Wearing ✓" else "Wear it")
                                        prop.onClick (fun _ -> dispatch (ToggleEquip item.id))
                                    ]
                                else
                                    Html.button [
                                        prop.className "btn btn-small btn-buy"
                                        prop.disabled (user.coins < item.price)
                                        prop.text (sprintf "%d 🪙" item.price)
                                        prop.onClick (fun _ -> dispatch (BuyCosmetic item.id))
                                    ]
                            ]
                        ]
                ]
            ]
            prizeSection model user dispatch
        ]
    ]

let private badgesTab (model: Model) (user: User) =
    let ctx = badgeCtxFor model.data user
    Html.div [
        prop.className "badges-tab"
        prop.children [
            Html.h3 [ prop.className "section-title"; prop.text "🏅 Badge Collection" ]
            Html.div [
                prop.className "badge-grid"
                prop.children [
                    for b in badgeDefs do
                        let earned = user.badges |> List.contains b.id || b.earned ctx
                        Html.div [
                            prop.className (if earned then "badge-card earned" else "badge-card")
                            prop.children [
                                Html.div [ prop.className "badge-icon"; prop.text (if earned then b.icon else "🔒") ]
                                Html.div [ prop.className "badge-name"; prop.text b.name ]
                                Html.div [ prop.className "badge-desc"; prop.text b.description ]
                            ]
                        ]
                ]
            ]
            // Avatar evolution preview — the "what's next" motivator.
            Html.h3 [ prop.className "section-title"; prop.text "🌱 Your Evolution" ]
            Html.div [
                prop.className "evolution-track"
                prop.children [
                    let level = levelForXp user.xp
                    for stage in avatarStages user.theme do
                        Html.div [
                            prop.className (if stage.minLevel <= level then "evo-step reached" else "evo-step")
                            prop.children [
                                Html.div [ prop.className "evo-emoji"; prop.text (if stage.minLevel <= level then stage.emoji else "❓") ]
                                Html.div [ prop.className "evo-name"; prop.text (if stage.minLevel <= level then stage.name else sprintf "Level %d" stage.minLevel) ]
                            ]
                        ]
                ]
            ]
        ]
    ]

let private tabBar (theme: ProfileTheme) (active: ChildTab) dispatch =
    let tabs =
        match theme with
        | DragonDream -> [ QuestsTab, "🪄", "Today"; MapTab, "🗺️", "World"; ShopTab, "🛍️", "Shop"; ArcadeTab, "🎮", "Arcade"; BadgesTab, "🏅", "Badges" ]
        | _ -> [ QuestsTab, "⛏️", "Today"; MapTab, "🗺️", "World"; ShopTab, "📦", "Shop"; ArcadeTab, "🎮", "Arcade"; BadgesTab, "🏅", "Badges" ]
    Html.div [
        prop.className "tabbar"
        prop.children [
            for (tab, icon, label) in tabs do
                Html.button [
                    prop.className (if tab = active then "tab-btn active" else "tab-btn")
                    prop.onClick (fun _ -> dispatch (ChildTabChanged tab))
                    prop.children [
                        Html.span [ prop.className "tab-icon"; prop.text icon ]
                        Html.span [ prop.text label ]
                    ]
                ]
        ]
    ]

let view (model: Model) (user: User) dispatch =
    let stage = avatarStageFor user.theme (levelForXp user.xp)
    Html.div [
        prop.className "child-screen"
        prop.children [
            Html.header [
                prop.className "child-header"
                prop.children [
                    avatarDisplay user false
                    Html.div [
                        prop.className "header-info"
                        prop.children [
                            Html.div [
                                prop.className "header-name-row"
                                prop.children [
                                    Html.span [ prop.className "header-name"; prop.text user.displayName ]
                                    levelChip user.xp
                                ]
                            ]
                            Html.div [ prop.className "header-stage"; prop.text stage.name ]
                            xpBar user.xp
                        ]
                    ]
                    Html.div [
                        prop.className "header-right"
                        prop.children [
                            coinPill user.coins
                            Html.button [
                                prop.className "btn btn-tiny btn-ghost"
                                prop.text "Log out"
                                prop.onClick (fun _ -> dispatch Logout)
                            ]
                        ]
                    ]
                ]
            ]
            Html.main [
                prop.className "child-main"
                prop.children [
                    match model.childTab with
                    | QuestsTab -> questsTab model user dispatch
                    | MapTab -> mapTab model user
                    | ShopTab -> shopTab model user dispatch
                    | ArcadeTab -> ViewArcade.view model user dispatch
                    | BadgesTab -> badgesTab model user
                ]
            ]
            tabBar user.theme model.childTab dispatch
        ]
    ]
