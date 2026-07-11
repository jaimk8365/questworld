/// Push-notification client. Each device subscribes for ONE profile
/// (chosen in parent Settings); the subscription is stored as subs.json in
/// the private sync repo, where the GitHub Action sends alerts through it.
/// Fable-only module.
module QuestWorld.Notifications

open Fable.Core
open Fable.Core.JsInterop
open Browser.WebStorage

let [<Literal>] private LocalKey = "questworld-notify-user"

/// Public half of the VAPID keypair (private half lives in repo secrets).
let [<Literal>] vapidPublicKey =
    "BJH9DNnFmqt1BAKFBkUpdlBVk6RB4Sa5ATf9UMOUFrOlNp1GFI3K26YXdtuXjynyxiHLzMhwj7ZSKrsCBOif2oU"

let private subsUrl =
    sprintf "https://api.github.com/repos/%s/%s/contents/subs.json" Sync.owner Sync.repo

[<Emit("fetch($0, $1)")>]
let private fetchJs (url: string) (init: obj) : JS.Promise<obj> = jsNative

[<Emit("btoa(unescape(encodeURIComponent($0)))")>]
let private b64encode (s: string) : string = jsNative

[<Emit("decodeURIComponent(escape(atob($0.replace(/\\s/g, ''))))")>]
let private b64decode (s: string) : string = jsNative

[<Emit("Notification.requestPermission()")>]
let private requestPermission () : JS.Promise<string> = jsNative

[<Emit("typeof Notification !== 'undefined' && 'serviceWorker' in navigator && 'PushManager' in window")>]
let private pushSupported () : bool = jsNative

[<Emit("navigator.serviceWorker.ready")>]
let private swReady () : JS.Promise<obj> = jsNative

// VAPID key must be handed to the browser as a Uint8Array.
[<Emit("(function(s){var p='='.repeat((4-s.length%4)%4);var b=atob((s+p).replace(/-/g,'+').replace(/_/g,'/'));var a=new Uint8Array(b.length);for(var i=0;i<b.length;i++)a[i]=b.charCodeAt(i);return a;})($0)")>]
let private urlB64ToUint8Array (s: string) : obj = jsNative

let private headers (token: string) =
    createObj [
        "Authorization" ==> ("Bearer " + token)
        "Accept" ==> "application/vnd.github+json"
    ]

/// Which profile this device is subscribed for (local marker, for the UI).
let subscribedFor () : string option =
    try
        match localStorage.getItem LocalKey with
        | null | "" -> None
        | v -> Some v
    with _ -> None

/// Upload/replace this device's subscription in subs.json (keyed by endpoint).
let private uploadSubscription (token: string) (userId: string) (subJson: string) : JS.Promise<unit> =
    promise {
        let! res = fetchJs subsUrl (createObj [ "headers" ==> headers token; "cache" ==> "no-store" ])
        let status: int = res?status
        let mutable existing: obj = box [||]
        let mutable sha: string option = None
        if status = 200 then
            let! body = res?json ()
            sha <- Some (body?sha |> string)
            try existing <- JS.JSON.parse (b64decode (body?content |> string))
            with _ -> existing <- box [||]
        elif status <> 404 then
            return failwith (sprintf "GitHub said %d — check the sync key" status)
        let sub = JS.JSON.parse subJson
        let endpoint: string = sub?endpoint
        let entry =
            createObj [
                "userId" ==> userId
                "endpoint" ==> endpoint
                "subscription" ==> sub
                "addedAt" ==> System.DateTime.Now.ToString("yyyy-MM-dd HH:mm")
            ]
        let filtered: obj = emitJsExpr (existing, endpoint) "($0 || []).filter(function(e){return e && e.endpoint !== $1})"
        let updated: obj = emitJsExpr (filtered, entry) "$0.concat([$1])"
        let body =
            createObj [
                "message" ==> "register notification device"
                "content" ==> b64encode (JS.JSON.stringify updated)
                match sha with
                | Some s -> "sha" ==> s
                | None -> ()
            ]
        let! put =
            fetchJs subsUrl (createObj [
                "method" ==> "PUT"
                "headers" ==> headers token
                "body" ==> JS.JSON.stringify body
            ])
        let putStatus: int = put?status
        if putStatus <> 200 && putStatus <> 201 then
            return failwith (sprintf "could not save device (%d)" putStatus)
    }

/// Full enable flow. Must be called from a user tap (iOS requirement).
let enable (token: string) (userId: string) : JS.Promise<Result<string, string>> =
    promise {
        try
            if not (pushSupported ()) then
                return Error "This device can't do alerts here — on iPad/iPhone, open QuestWorld from its Home-Screen icon (iOS 16.4+) and try again."
            else
                let! permission = requestPermission ()
                if permission <> "granted" then
                    return Error "Notifications were not allowed. Enable them in Settings → Notifications → QuestWorld, then try again."
                else
                    let! reg = swReady ()
                    let! sub =
                        reg?pushManager?subscribe (
                            createObj [
                                "userVisibleOnly" ==> true
                                "applicationServerKey" ==> urlB64ToUint8Array vapidPublicKey
                            ])
                    do! uploadSubscription token userId (JS.JSON.stringify sub)
                    try localStorage.setItem (LocalKey, userId) with _ -> ()
                    return Ok userId
        with e ->
            return Error (if isNull e.Message then "something went wrong" else e.Message)
    }
