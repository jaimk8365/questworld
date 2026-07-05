/// JSON (de)serialization for persistence. Thoth.Json in the browser,
/// Thoth.Json.Net in the .NET test suite — same auto-coder semantics,
/// which lets the tests verify the exact persistence round-trip.
module QuestWorld.Codec

#if FABLE_COMPILER
open Thoth.Json
#else
open Thoth.Json.Net
#endif

open QuestWorld.Domain

let serializeData (data: AppData) : string =
    Encode.Auto.toString (0, data)

let deserializeData (json: string) : Result<AppData, string> =
    Decode.Auto.fromString<AppData> json

let serializeSession (s: LoginSession) : string =
    Encode.Auto.toString (0, s)

let deserializeSession (json: string) : Result<LoginSession, string> =
    Decode.Auto.fromString<LoginSession> json
