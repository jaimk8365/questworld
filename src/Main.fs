/// App entry point: root view (theme switch) + Elmish program.
module QuestWorld.Main

open Feliz
open Elmish
open Elmish.React
open QuestWorld.Domain
open QuestWorld.State

let private themeClass = function
    | DragonDream -> "app theme-dragon"
    | BlockCraft -> "app theme-block"
    | AdminClean -> "app theme-admin"

let view (model: Model) dispatch =
    let rootClass =
        match currentUser model with
        | Some user -> themeClass user.theme
        | None ->
            // Login screen previews the selected profile's theme.
            match model.selectedProfile |> Option.bind (fun id -> model.data.users |> List.tryFind (fun u -> u.id = id)) with
            | Some u -> themeClass u.theme
            | None -> "app theme-login"
    Html.div [
        prop.className rootClass
        prop.children [
            match currentUser model with
            | None -> ViewLogin.view model dispatch
            | Some user ->
                match user.role with
                | Parent -> ViewAdult.view model user dispatch
                | Child -> ViewChild.view model user dispatch
            match model.celebration with
            | Some c -> ViewShared.celebrationOverlay model c dispatch
            | None -> Html.none
        ]
    ]

Program.mkProgram init update view
|> Program.withReactSynchronous "root"
|> Program.run
