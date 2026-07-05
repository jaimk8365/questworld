/// Persistent local storage (browser localStorage). Fable-only module.
module QuestWorld.Storage

open Browser.WebStorage
open QuestWorld.Domain
open QuestWorld.Codec

let [<Literal>] private DataKey = "questworld-data-v1"
let [<Literal>] private SessionKey = "questworld-session-v1"

let saveData (data: AppData) =
    try localStorage.setItem (DataKey, serializeData data)
    with _ -> ()

let loadData () : AppData option =
    try
        match localStorage.getItem DataKey with
        | null | "" -> None
        | json ->
            match deserializeData json with
            | Ok data -> Some data
            | Error _ -> None // schema mismatch → fall back to seed
    with _ -> None

let saveSession (session: LoginSession option) =
    try
        match session with
        | Some s -> localStorage.setItem (SessionKey, serializeSession s)
        | None -> localStorage.removeItem SessionKey
    with _ -> ()

let loadSession () : LoginSession option =
    try
        match localStorage.getItem SessionKey with
        | null | "" -> None
        | json ->
            match deserializeSession json with
            | Ok s -> Some s
            | Error _ -> None
    with _ -> None
