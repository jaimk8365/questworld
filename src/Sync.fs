/// Cross-device sync client. The shared world lives as data.json in a
/// private GitHub repo; each device pulls, merges (Merge.mergeData) and
/// pushes back. Compare-and-swap via the file's git sha stops concurrent
/// writers clobbering each other. Fable-only module.
module QuestWorld.Sync

open Fable.Core
open Fable.Core.JsInterop
open Browser.WebStorage
open QuestWorld.Domain
open QuestWorld.Codec

let [<Literal>] private TokenKey = "questworld-sync-token"
let owner = "jaimk8365"
let repo = "questworld-sync"
let private apiUrl =
    sprintf "https://api.github.com/repos/%s/%s/contents/data.json" owner repo

// ------------------------------------------------------------ token store

let loadToken () : string =
    try
        match localStorage.getItem TokenKey with
        | null -> ""
        | t -> t.Trim()
    with _ -> ""

let saveToken (token: string) =
    try
        if token.Trim() = "" then localStorage.removeItem TokenKey
        else localStorage.setItem (TokenKey, token.Trim())
    with _ -> ()

// --------------------------------------------------------------- plumbing

[<Emit("fetch($0, $1)")>]
let private fetchJs (url: string) (init: obj) : JS.Promise<obj> = jsNative

// unicode-safe base64 (the JSON contains emoji)
[<Emit("btoa(unescape(encodeURIComponent($0)))")>]
let private b64encode (s: string) : string = jsNative

[<Emit("decodeURIComponent(escape(atob($0.replace(/\\s/g, ''))))")>]
let private b64decode (s: string) : string = jsNative

let private headers (token: string) =
    createObj [
        "Authorization" ==> ("Bearer " + token)
        "Accept" ==> "application/vnd.github+json"
    ]

/// (remote data if the file exists and parses, file sha if the file exists)
let private getRemote (token: string) : JS.Promise<AppData option * string option> =
    promise {
        let! res = fetchJs apiUrl (createObj [ "headers" ==> headers token; "cache" ==> "no-store" ])
        let status: int = res?status
        if status = 404 then
            return None, None // first ever sync — file not created yet
        elif status <> 200 then
            return failwith (sprintf "GitHub said %d — check the sync key" status)
        else
            let! body = res?json ()
            let sha: string = body?sha
            let text = b64decode (body?content |> string)
            match deserializeData text with
            | Ok data -> return Some data, Some sha
            | Error _ -> return None, Some sha
    }

let private putRemote (token: string) (data: AppData) (sha: string option) : JS.Promise<unit> =
    promise {
        let body =
            createObj [
                "message" ==> "QuestWorld sync"
                "content" ==> b64encode (serializeData data)
                match sha with
                | Some s -> "sha" ==> s
                | None -> ()
            ]
        let! res =
            fetchJs apiUrl (createObj [
                "method" ==> "PUT"
                "headers" ==> headers token
                "body" ==> JS.JSON.stringify body
            ])
        let status: int = res?status
        if status <> 200 && status <> 201 then
            return failwith (sprintf "push failed (%d)" status)
    }

// ------------------------------------------------------------------ sync

let mutable private inFlight = false

/// Pull remote, merge with local, push if anything changed.
/// Returns the merged data. Retries once on a write race.
let syncOnce (token: string) (local: AppData) : JS.Promise<AppData> =
    if inFlight then promise { return local }
    else
        inFlight <- true
        promise {
            try
                let mutable result = local
                let mutable attempts = 0
                let mutable finished = false
                while not finished && attempts < 3 do
                    attempts <- attempts + 1
                    let! remote, sha = getRemote token
                    let merged =
                        match remote with
                        // sound on/off stays a per-device preference
                        | Some r -> { Merge.mergeData r local with settings = local.settings }
                        | None -> local
                    if remote = Some merged then
                        result <- merged
                        finished <- true // nothing new to push
                    else
                        try
                            do! putRemote token merged sha
                            result <- merged
                            finished <- true
                        with _ when attempts < 3 ->
                            () // sha race with another device — loop and retry
                inFlight <- false
                return result
            with e ->
                inFlight <- false
                return raise e
        }
