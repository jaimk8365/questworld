/// Reward sound engine — WebAudio chiptune blips, zero asset files.
/// Fable-only module. All calls are fire-and-forget and exception-safe
/// (iOS Safari only unlocks audio after a user gesture; every call here
/// happens inside a tap handler, so that constraint is satisfied).
module QuestWorld.Sounds

open Fable.Core
open Fable.Core.JsInterop

[<Emit("new (window.AudioContext || window.webkitAudioContext)()")>]
let private createAudioContext () : obj = jsNative

let mutable private ctx : obj option = None

let private getCtx () =
    match ctx with
    | Some c -> c
    | None ->
        let c = createAudioContext ()
        ctx <- Some c
        c

/// Play a single tone `startAt` seconds from now.
let private tone (freq: float) (startAt: float) (duration: float) (waveform: string) (volume: float) =
    try
        let c = getCtx ()
        if c?state = "suspended" then c?resume () |> ignore
        let now: float = c?currentTime
        let osc = c?createOscillator ()
        let gain = c?createGain ()
        osc?``type`` <- waveform
        osc?frequency?value <- freq
        gain?gain?setValueAtTime (volume, now + startAt) |> ignore
        gain?gain?exponentialRampToValueAtTime (0.001, now + startAt + duration) |> ignore
        osc?connect (gain) |> ignore
        gain?connect (c?destination) |> ignore
        osc?start (now + startAt) |> ignore
        osc?stop (now + startAt + duration) |> ignore
    with _ -> ()

/// Quest complete — a rising major arpeggio.
let questComplete () =
    tone 523.25 0.00 0.15 "triangle" 0.25 // C5
    tone 659.25 0.10 0.15 "triangle" 0.25 // E5
    tone 783.99 0.20 0.25 "triangle" 0.25 // G5
    tone 1046.5 0.30 0.40 "triangle" 0.30 // C6

/// Coin pickup — two quick square blips (very "video game").
let coin () =
    tone 987.77 0.00 0.08 "square" 0.15
    tone 1318.5 0.08 0.20 "square" 0.15

/// Level up — triumphant fanfare.
let levelUp () =
    tone 392.00 0.00 0.20 "triangle" 0.28
    tone 523.25 0.15 0.20 "triangle" 0.28
    tone 659.25 0.30 0.20 "triangle" 0.28
    tone 783.99 0.45 0.50 "triangle" 0.32
    tone 1046.5 0.60 0.60 "triangle" 0.25

/// Loot box reveal — mysterious shimmer.
let loot () =
    tone 880.0 0.00 0.10 "sine" 0.2
    tone 1108.7 0.10 0.10 "sine" 0.2
    tone 1318.5 0.20 0.10 "sine" 0.2
    tone 1760.0 0.30 0.40 "sine" 0.25

/// Gentle tap/blip for buttons.
let tap () =
    tone 660.0 0.0 0.06 "sine" 0.12

/// Sad-trombone-lite for errors (kept gentle — no punishment vibes).
let oops () =
    tone 330.0 0.0 0.15 "sine" 0.15
    tone 293.66 0.15 0.25 "sine" 0.15
