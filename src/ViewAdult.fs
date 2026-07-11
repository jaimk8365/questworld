/// Parent dashboard — overview, approvals, quest builder, settings.
module QuestWorld.ViewAdult

open System
open Feliz
open QuestWorld.Domain
open QuestWorld.Progression
open QuestWorld.QuestEngine
open QuestWorld.Catalog
open QuestWorld.State
open QuestWorld.ViewShared

let private kidCard (model: Model) (kid: User) =
    let quests = questsForUser model.data kid.id DateTime.Now
    let doneToday = quests |> List.filter (fun (_, s) -> s = Completed) |> List.length
    let pending = quests |> List.filter (fun (_, s) -> s = PendingApproval) |> List.length
    let stage = avatarStageFor kid.theme (levelForXp kid.xp)
    Html.div [
        prop.className "kid-card"
        prop.children [
            Html.div [
                prop.className "kid-card-head"
                prop.children [
                    Html.span [ prop.className "kid-emoji"; prop.text stage.emoji ]
                    Html.div [
                        prop.children [
                            Html.div [ prop.className "kid-name"; prop.text kid.displayName ]
                            Html.div [ prop.className "kid-stage"; prop.text (sprintf "Level %d · %s" (levelForXp kid.xp) stage.name) ]
                        ]
                    ]
                ]
            ]
            xpBar kid.xp
            Html.div [
                prop.className "kid-stats"
                prop.children [
                    Html.div [ prop.className "kid-stat"; prop.text (sprintf "🪙 %d coins" kid.coins) ]
                    Html.div [ prop.className "kid-stat"; prop.text (sprintf "✅ %d done today" doneToday) ]
                    Html.div [ prop.className "kid-stat"; prop.text (sprintf "⏳ %d waiting" pending) ]
                    Html.div [ prop.className "kid-stat"; prop.text (sprintf "🏅 %d badges" (List.length kid.badges)) ]
                    match kid.arcade with
                    | Some a -> Html.div [ prop.className "kid-stat"; prop.text (sprintf "🎮 best %d" a.bestScore) ]
                    | None -> Html.none
                ]
            ]
        ]
    ]

/// This week's family arcade scoreboard.
let private scoreboard (model: Model) =
    let games = [ "flight", "🛫 Flight"; "memory", "🃏 Memory" ]
    Html.div [
        prop.children [
            Html.h3 [ prop.className "admin-title"; prop.text "🏆 This week's Arcade scoreboard" ]
            Html.div [
                prop.className "kid-grid"
                prop.children [
                    for gameId, label in games do
                        let scores = weeklyScores model.data gameId DateTime.Now
                        Html.div [
                            prop.className "kid-card"
                            prop.children [
                                Html.div [ prop.className "kid-name"; prop.text label ]
                                if List.isEmpty scores then
                                    Html.p [ prop.className "empty-note"; prop.text "No games played yet this week." ]
                                else
                                    for rank, s in List.indexed scores do
                                        let name =
                                            model.data.users
                                            |> List.tryFind (fun u -> u.id = s.userId)
                                            |> Option.map (fun u -> u.displayName)
                                            |> Option.defaultValue "?"
                                        Html.div [
                                            prop.className "score-row"
                                            prop.children [
                                                Html.span [ prop.text (match rank with 0 -> "🥇" | 1 -> "🥈" | _ -> "🥉") ]
                                                Html.span [ prop.className "score-name"; prop.text name ]
                                                Html.span [ prop.className "score-value"; prop.text (string s.score) ]
                                            ]
                                        ]
                            ]
                        ]
                ]
            ]
        ]
    ]

let private overviewTab (model: Model) =
    let kids = model.data.users |> List.filter (fun u -> u.role = Child)
    Html.div [
        prop.className "admin-section"
        prop.children [
            Html.h3 [ prop.className "admin-title"; prop.text "Children's progress" ]
            Html.div [ prop.className "kid-grid"; prop.children (kids |> List.map (kidCard model)) ]
            scoreboard model
        ]
    ]

/// Prizes waiting to be physically handed over.
let private redemptionQueue (model: Model) dispatch =
    let pending = pendingRedemptions model.data
    Html.div [
        prop.children [
            Html.h3 [ prop.className "admin-title"; prop.text "🎁 Prizes to hand over" ]
            if List.isEmpty pending then
                Html.p [ prop.className "empty-note"; prop.text "No prizes waiting." ]
            for (redemption, prize, child) in pending do
                Html.div [
                    prop.className "approval-row"
                    prop.children [
                        Html.div [
                            prop.className "approval-info"
                            prop.children [
                                Html.div [ prop.className "approval-title"; prop.text (sprintf "%s %s" prize.icon prize.title) ]
                                Html.div [ prop.className "approval-sub"
                                           prop.text (sprintf "%s · bought %s · %d 🪙" child.displayName redemption.redeemedAt prize.cost) ]
                            ]
                        ]
                        Html.div [
                            prop.className "approval-actions"
                            prop.children [
                                Html.button [
                                    prop.className "btn btn-approve"
                                    prop.text "Given ✓"
                                    prop.onClick (fun _ -> dispatch (FulfillRedemption redemption.id))
                                ]
                                Html.button [
                                    prop.className "btn btn-reject"
                                    prop.text "Refund"
                                    prop.onClick (fun _ -> dispatch (RefundRedemption redemption.id))
                                ]
                            ]
                        ]
                    ]
                ]
        ]
    ]

let private approvalsTab (model: Model) dispatch =
    let pending = pendingApprovals model.data
    Html.div [
        prop.className "admin-section"
        prop.children [
            Html.h3 [ prop.className "admin-title"; prop.text "Waiting for approval" ]
            if List.isEmpty pending then
                Html.p [ prop.className "empty-note"; prop.text "Nothing waiting — all caught up! ✨" ]
            for (completion, quest) in pending do
                let child =
                    model.data.users
                    |> List.tryFind (fun u -> u.id = completion.userId)
                    |> Option.map (fun u -> u.displayName)
                    |> Option.defaultValue "?"
                Html.div [
                    prop.className "approval-row"
                    prop.children [
                        Html.div [
                            prop.className "approval-info"
                            prop.children [
                                Html.div [ prop.className "approval-title"; prop.text (sprintf "%s %s" quest.icon quest.title) ]
                                Html.div [ prop.className "approval-sub"
                                           prop.text (sprintf "%s · claimed %s · +%d XP, +%d 🪙" child completion.completedAt quest.reward.xp quest.reward.coins) ]
                            ]
                        ]
                        Html.div [
                            prop.className "approval-actions"
                            prop.children [
                                Html.button [
                                    prop.className "btn btn-approve"
                                    prop.text "Approve ✓"
                                    prop.onClick (fun _ -> dispatch (ApproveCompletion completion))
                                ]
                                Html.button [
                                    prop.className "btn btn-reject"
                                    prop.text "Not yet"
                                    prop.onClick (fun _ -> dispatch (RejectCompletion completion))
                                ]
                            ]
                        ]
                    ]
                ]
            redemptionQueue model dispatch
        ]
    ]

// ---------------------------------------------------------------- prizes

let private prizesTab (model: Model) dispatch =
    let f = model.prizeForm
    let set form = dispatch (PrizeFormChanged form)
    Html.div [
        prop.className "admin-section"
        prop.children [
            Html.h3 [ prop.className "admin-title"; prop.text "Create a real-world prize" ]
            Html.p [ prop.className "empty-note"
                     prop.text "Prizes appear in the kids' Shop tab. When a child buys one, coins are taken straight away and it shows up in Approvals for you to hand over." ]
            Html.div [
                prop.className "builder-form"
                prop.children [
                    Html.label [
                        prop.className "field"
                        prop.children [
                            Html.span [ prop.className "field-label"; prop.text "Prize" ]
                            Html.input [
                                prop.className "input"
                                prop.value f.title
                                prop.placeholder "e.g. Movie night pick"
                                prop.onChange (fun (v: string) -> set { f with title = v }) ]
                        ]
                    ]
                    Html.label [
                        prop.className "field"
                        prop.children [
                            Html.span [ prop.className "field-label"; prop.text "Icon (emoji)" ]
                            Html.input [
                                prop.className "input input-short"
                                prop.value f.icon
                                prop.onChange (fun (v: string) -> set { f with icon = v }) ]
                        ]
                    ]
                    Html.label [
                        prop.className "field"
                        prop.children [
                            Html.span [ prop.className "field-label"; prop.text "Cost (coins)" ]
                            Html.input [
                                prop.className "input input-short"; prop.type' "number"
                                prop.value (string f.cost)
                                prop.onChange (fun (v: string) -> set { f with cost = (match Int32.TryParse v with true, n -> n | _ -> f.cost) }) ]
                        ]
                    ]
                    match model.prizeMessage with
                    | Some m -> Html.div [ prop.className "builder-message"; prop.text m ]
                    | None -> Html.none
                    Html.button [
                        prop.className "btn btn-primary"
                        prop.text "Add prize"
                        prop.onClick (fun _ -> dispatch SubmitPrize)
                    ]
                ]
            ]
            Html.h3 [ prop.className "admin-title"; prop.text "All prizes" ]
            if List.isEmpty (prizesOf model.data) then
                Html.p [ prop.className "empty-note"; prop.text "No prizes yet — add the first one above!" ]
            Html.div [
                prop.children [
                    for prize in prizesOf model.data do
                        Html.div [
                            prop.className (if prize.active then "quest-admin-row" else "quest-admin-row inactive")
                            prop.children [
                                Html.span [ prop.className "qa-icon"; prop.text prize.icon ]
                                Html.div [
                                    prop.className "qa-info"
                                    prop.children [
                                        Html.div [ prop.className "qa-title"; prop.text prize.title ]
                                        Html.div [ prop.className "qa-sub"; prop.text (sprintf "%d 🪙" prize.cost) ]
                                    ]
                                ]
                                Html.button [
                                    prop.className "btn btn-tiny"
                                    prop.text (if prize.active then "Pause" else "Resume")
                                    prop.onClick (fun _ -> dispatch (TogglePrizeActive (prize.id, not prize.active)))
                                ]
                                Html.button [
                                    prop.className "btn btn-tiny btn-reject"
                                    prop.text "Delete"
                                    prop.onClick (fun _ -> dispatch (DeletePrize prize.id))
                                ]
                            ]
                        ]
                ]
            ]
        ]
    ]

// --------------------------------------------------------------- builder

let private field (label: string) (control: ReactElement) =
    Html.label [
        prop.className "field"
        prop.children [ Html.span [ prop.className "field-label"; prop.text label ]; control ]
    ]

let private questTypeName = function
    | Daily -> "Daily" | Weekly -> "Weekly" | Behaviour -> "Behaviour" | Chore -> "Chore" | Bonus -> "Bonus"

let private recurrenceName = function
    | OnceOff -> "One-off" | EveryDay -> "Every day" | EveryWeek -> "Every week"

let private builderTab (model: Model) dispatch =
    let f = model.questForm
    let set form = dispatch (FormChanged form)
    Html.div [
        prop.className "admin-section"
        prop.children [
            Html.h3 [ prop.className "admin-title"; prop.text "Create a quest" ]
            Html.div [
                prop.className "builder-form"
                prop.children [
                    field "Title" (Html.input [
                        prop.className "input"
                        prop.value f.title
                        prop.placeholder "e.g. Water the plants"
                        prop.onChange (fun (v: string) -> set { f with title = v }) ])
                    field "Description" (Html.input [
                        prop.className "input"
                        prop.value f.description
                        prop.placeholder "What does success look like?"
                        prop.onChange (fun (v: string) -> set { f with description = v }) ])
                    field "Icon (emoji)" (Html.input [
                        prop.className "input input-short"
                        prop.value f.icon
                        prop.onChange (fun (v: string) -> set { f with icon = v }) ])
                    field "Type" (Html.select [
                        prop.className "input"
                        prop.value (questTypeName f.questType)
                        prop.onChange (fun (v: string) ->
                            let t = match v with
                                    | "Daily" -> Daily | "Weekly" -> Weekly | "Behaviour" -> Behaviour
                                    | "Bonus" -> Bonus | _ -> Chore
                            set { f with questType = t })
                        prop.children [ for t in [ Daily; Weekly; Behaviour; Chore; Bonus ] ->
                                          Html.option [ prop.value (questTypeName t); prop.text (questTypeName t) ] ] ])
                    field "Repeats" (Html.select [
                        prop.className "input"
                        prop.value (recurrenceName f.recurrence)
                        prop.onChange (fun (v: string) ->
                            let r = match v with
                                    | "One-off" -> OnceOff | "Every week" -> EveryWeek | _ -> EveryDay
                            set { f with recurrence = r })
                        prop.children [ for r in [ EveryDay; EveryWeek; OnceOff ] ->
                                          Html.option [ prop.value (recurrenceName r); prop.text (recurrenceName r) ] ] ])
                    field "XP reward" (Html.input [
                        prop.className "input input-short"; prop.type' "number"
                        prop.value (string f.xp)
                        prop.onChange (fun (v: string) -> set { f with xp = (match Int32.TryParse v with true, n -> n | _ -> f.xp) }) ])
                    field "Coin reward" (Html.input [
                        prop.className "input input-short"; prop.type' "number"
                        prop.value (string f.coins)
                        prop.onChange (fun (v: string) -> set { f with coins = (match Int32.TryParse v with true, n -> n | _ -> f.coins) }) ])
                    Html.div [
                        prop.className "check-row"
                        prop.children [
                            Html.label [ prop.children [
                                Html.input [ prop.type' "checkbox"; prop.isChecked f.assignThea
                                             prop.onChange (fun (b: bool) -> set { f with assignThea = b }) ]
                                Html.span [ prop.text " Thea" ] ] ]
                            Html.label [ prop.children [
                                Html.input [ prop.type' "checkbox"; prop.isChecked f.assignLevi
                                             prop.onChange (fun (b: bool) -> set { f with assignLevi = b }) ]
                                Html.span [ prop.text " Levi" ] ] ]
                            Html.label [ prop.children [
                                Html.input [ prop.type' "checkbox"; prop.isChecked f.requiresApproval
                                             prop.onChange (fun (b: bool) -> set { f with requiresApproval = b }) ]
                                Html.span [ prop.text " Needs my approval" ] ] ]
                        ]
                    ]
                    match model.builderMessage with
                    | Some m -> Html.div [ prop.className "builder-message"; prop.text m ]
                    | None -> Html.none
                    Html.button [
                        prop.className "btn btn-primary"
                        prop.text "Create quest"
                        prop.onClick (fun _ -> dispatch SubmitQuest)
                    ]
                ]
            ]
            Html.h3 [ prop.className "admin-title"; prop.text "All quests" ]
            Html.div [
                prop.children [
                    for quest in model.data.quests do
                        Html.div [
                            prop.className (if quest.active then "quest-admin-row" else "quest-admin-row inactive")
                            prop.children [
                                Html.span [ prop.className "qa-icon"; prop.text quest.icon ]
                                Html.div [
                                    prop.className "qa-info"
                                    prop.children [
                                        Html.div [ prop.className "qa-title"; prop.text quest.title ]
                                        Html.div [ prop.className "qa-sub"
                                                   prop.text (sprintf "%s · %s · +%d XP · %s"
                                                                (questTypeName quest.questType)
                                                                (recurrenceName quest.recurrence)
                                                                quest.reward.xp
                                                                (if quest.requiresApproval then "needs approval" else "auto-approve")) ]
                                    ]
                                ]
                                Html.button [
                                    prop.className "btn btn-tiny"
                                    prop.text (if quest.active then "Pause" else "Resume")
                                    prop.onClick (fun _ -> dispatch (ToggleQuestActive (quest.id, not quest.active)))
                                ]
                                Html.button [
                                    prop.className "btn btn-tiny btn-reject"
                                    prop.text "Delete"
                                    prop.onClick (fun _ -> dispatch (DeleteQuest quest.id))
                                ]
                            ]
                        ]
                ]
            ]
        ]
    ]

let private settingsTab (model: Model) dispatch =
    Html.div [
        prop.className "admin-section"
        prop.children [
            Html.h3 [ prop.className "admin-title"; prop.text "Settings" ]
            Html.div [
                prop.className "settings-row"
                prop.children [
                    Html.span [ prop.text "Sound effects" ]
                    Html.button [
                        prop.className "btn btn-small"
                        prop.text (if model.data.settings.soundOn then "On 🔊" else "Off 🔇")
                        prop.onClick (fun _ -> dispatch ToggleSound)
                    ]
                ]
            ]
            Html.h3 [ prop.className "admin-title"; prop.text "Change a password" ]
            Html.div [
                prop.className "builder-form"
                prop.children [
                    field "Account" (Html.select [
                        prop.className "input"
                        prop.value model.pwTargetUser
                        prop.onChange (fun (v: string) -> dispatch (PwTargetChanged v))
                        prop.children [ for u in model.data.users ->
                                          Html.option [ prop.value u.id; prop.text u.displayName ] ] ])
                    field "New password" (Html.input [
                        prop.className "input"
                        prop.type' "password"
                        prop.value model.pwNewValue
                        prop.onChange (fun (v: string) -> dispatch (PwValueChanged v)) ])
                    match model.pwMessage with
                    | Some m -> Html.div [ prop.className "builder-message"; prop.text m ]
                    | None -> Html.none
                    Html.button [
                        prop.className "btn btn-primary"
                        prop.text "Update password"
                        prop.onClick (fun _ -> dispatch SubmitPassword)
                    ]
                ]
            ]
            Html.h3 [ prop.className "admin-title"; prop.text "☁️ Device sync" ]
            Html.div [
                prop.className "builder-form"
                prop.children [
                    if model.syncToken = "" then
                        Html.p [ prop.className "empty-note"
                                 prop.text "Paste the family sync key to share one world across all devices — quests, approvals, prizes and progress stay in step everywhere." ]
                        field "Sync key" (Html.input [
                            prop.className "input"
                            prop.type' "password"
                            prop.placeholder "github_pat_…"
                            prop.value model.syncTokenInput
                            prop.onChange (fun (v: string) -> dispatch (SyncTokenInputChanged v)) ])
                        Html.button [
                            prop.className "btn btn-primary"
                            prop.text "Turn sync on"
                            prop.onClick (fun _ -> dispatch SaveSyncToken)
                        ]
                    else
                        Html.p [ prop.className "empty-note"; prop.text "Sync is ON — this device shares the family world." ]
                        Html.div [
                            prop.className "pw-buttons"
                            prop.children [
                                Html.button [
                                    prop.className "btn btn-small"
                                    prop.text "Sync now"
                                    prop.onClick (fun _ -> dispatch SyncNow)
                                ]
                                Html.button [
                                    prop.className "btn btn-small btn-reject"
                                    prop.text "Turn off"
                                    prop.onClick (fun _ -> dispatch DisableSync)
                                ]
                            ]
                        ]
                    match model.syncStatus with
                    | Some s -> Html.div [ prop.className "builder-message"; prop.text s ]
                    | None -> Html.none
                ]
            ]
            Html.h3 [ prop.className "admin-title"; prop.text "🔔 Alerts on this device" ]
            Html.div [
                prop.className "builder-form"
                prop.children [
                    Html.p [ prop.className "empty-note"
                             prop.text "Pick whose alerts THIS device should receive (Thea's iPad → Thea, your phone → Quest Master). Works from the Home-Screen app on iOS 16.4+; needs sync on." ]
                    field "Alerts for" (Html.select [
                        prop.className "input"
                        prop.value model.notifyTarget
                        prop.onChange (fun (v: string) -> dispatch (NotifyTargetChanged v))
                        prop.children [ for u in model.data.users ->
                                          Html.option [ prop.value u.id; prop.text u.displayName ] ] ])
                    Html.button [
                        prop.className "btn btn-primary"
                        prop.text "Enable alerts on this device"
                        prop.onClick (fun _ -> dispatch EnableNotifications)
                    ]
                    match model.notifySubscribed with
                    | Some id when model.notifyStatus = None ->
                        let name =
                            model.data.users
                            |> List.tryFind (fun u -> u.id = id)
                            |> Option.map (fun u -> u.displayName)
                            |> Option.defaultValue id
                        Html.div [ prop.className "builder-message"; prop.text (sprintf "This device gets %s's alerts 🔔" name) ]
                    | _ -> Html.none
                    match model.notifyStatus with
                    | Some s -> Html.div [ prop.className "builder-message"; prop.text s ]
                    | None -> Html.none
                ]
            ]
            Html.h3 [ prop.className "admin-title"; prop.text "Danger zone" ]
            Html.button [
                prop.className "btn btn-reject"
                prop.text (if model.syncToken = "" then "Reset ALL data to defaults"
                           else "Reset ALL data (spreads to every synced device!)")
                prop.onClick (fun _ -> dispatch ResetAllData)
            ]
        ]
    ]

let view (model: Model) (user: User) dispatch =
    Html.div [
        prop.className "admin-screen"
        prop.children [
            Html.header [
                prop.className "admin-header"
                prop.children [
                    Html.div [ prop.className "admin-brand"; prop.text "🧭 QuestWorld · Quest Master" ]
                    Html.button [
                        prop.className "btn btn-tiny btn-ghost"
                        prop.text "Log out"
                        prop.onClick (fun _ -> dispatch Logout)
                    ]
                ]
            ]
            Html.nav [
                prop.className "admin-tabs"
                prop.children [
                    let pendingCount =
                        (pendingApprovals model.data |> List.length)
                        + (pendingRedemptions model.data |> List.length)
                    for (tab, label) in
                        [ OverviewTab, "Overview"
                          ApprovalsTab, (if pendingCount > 0 then sprintf "Approvals (%d)" pendingCount else "Approvals")
                          BuilderTab, "Quests"
                          PrizesTab, "Prizes"
                          SettingsTab, "Settings" ] do
                        Html.button [
                            prop.className (if tab = model.adminTab then "tab-btn active" else "tab-btn")
                            prop.text label
                            prop.onClick (fun _ -> dispatch (AdminTabChanged tab))
                        ]
                ]
            ]
            Html.main [
                prop.className "admin-main"
                prop.children [
                    match model.adminTab with
                    | OverviewTab -> overviewTab model
                    | ApprovalsTab -> approvalsTab model dispatch
                    | BuilderTab -> builderTab model dispatch
                    | PrizesTab -> prizesTab model dispatch
                    | SettingsTab -> settingsTab model dispatch
                ]
            ]
        ]
    ]
