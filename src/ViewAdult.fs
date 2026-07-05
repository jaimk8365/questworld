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
            Html.h3 [ prop.className "admin-title"; prop.text "Danger zone" ]
            Html.button [
                prop.className "btn btn-reject"
                prop.text "Reset ALL data to defaults"
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
                    let pendingCount = pendingApprovals model.data |> List.length
                    for (tab, label) in
                        [ OverviewTab, "Overview"
                          ApprovalsTab, (if pendingCount > 0 then sprintf "Approvals (%d)" pendingCount else "Approvals")
                          BuilderTab, "Quests"
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
                    | SettingsTab -> settingsTab model dispatch
                ]
            ]
        ]
    ]
