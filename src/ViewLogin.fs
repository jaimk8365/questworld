/// Profile picker + password entry.
module QuestWorld.ViewLogin

open Feliz
open QuestWorld.Domain
open QuestWorld.Progression
open QuestWorld.State

let private profileCard (user: User) dispatch =
    let stage = avatarStageFor user.theme (levelForXp user.xp)
    let themeClass =
        match user.theme with
        | DragonDream -> "profile-card card-dragon"
        | BlockCraft -> "profile-card card-block"
        | AdminClean -> "profile-card card-admin"
    Html.button [
        prop.className themeClass
        prop.onClick (fun _ -> dispatch (SelectProfile user.id))
        prop.children [
            Html.div [ prop.className "profile-emoji"; prop.text stage.emoji ]
            Html.div [ prop.className "profile-name"; prop.text user.displayName ]
            Html.div [
                prop.className "profile-sub"
                prop.text (
                    match user.role with
                    | Parent -> "Quest Master"
                    | Child -> sprintf "Level %d · %s" (levelForXp user.xp) stage.name)
            ]
        ]
    ]

let private passwordPanel (model: Model) (user: User) dispatch =
    Html.div [
        prop.className "pw-panel pop-in"
        prop.children [
            Html.div [ prop.className "profile-emoji"; prop.text (avatarStageFor user.theme (levelForXp user.xp)).emoji ]
            Html.h2 [ prop.text (sprintf "Hi %s!" user.displayName) ]
            Html.p [ prop.className "pw-hint"; prop.text "What's the secret word?" ]
            Html.input [
                prop.className "pw-input"
                prop.type' "password"
                prop.autoFocus true
                prop.placeholder "Secret word…"
                prop.value model.passwordInput
                prop.onChange (fun (v: string) -> dispatch (PasswordChanged v))
                prop.onKeyUp (fun e -> if e.key = "Enter" then dispatch AttemptLogin)
            ]
            match model.loginError with
            | Some err -> Html.div [ prop.className "login-error"; prop.text err ]
            | None -> Html.none
            Html.div [
                prop.className "pw-buttons"
                prop.children [
                    Html.button [
                        prop.className "btn btn-ghost"
                        prop.text "Back"
                        prop.onClick (fun _ -> dispatch BackToProfiles)
                    ]
                    Html.button [
                        prop.className "btn btn-primary"
                        prop.text "Let's go!"
                        prop.onClick (fun _ -> dispatch AttemptLogin)
                    ]
                ]
            ]
        ]
    ]

let view (model: Model) dispatch =
    Html.div [
        prop.className "login-screen"
        prop.children [
            Html.h1 [ prop.className "app-title"; prop.text "🗺️ QuestWorld" ]
            Html.p [ prop.className "app-tagline"; prop.text "Real quests. Real rewards. Real you." ]
            match model.selectedProfile |> Option.bind (fun id -> model.data.users |> List.tryFind (fun u -> u.id = id)) with
            | Some user -> passwordPanel model user dispatch
            | None ->
                Html.div [
                    prop.className "profile-grid"
                    prop.children (model.data.users |> List.map (fun u -> profileCard u dispatch))
                ]
        ]
    ]
