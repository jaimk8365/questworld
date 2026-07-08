/// Memory-match mini-game engine ("Dragon Pairs" / "Miner's Match").
/// Turn-based — no timer needed. Pure and unit-tested on .NET.
module QuestWorld.Memory

open System

let pairCount = 8

type CardState =
    | FaceDown
    | FaceUp
    | Matched

type Card =
    { face: string
      state: CardState }

type Phase =
    | Playing
    | Done

type Game =
    { cards: Card list
      firstPick: int option
      locked: (int * int) option   // a mismatched pair still showing
      flips: int
      mismatches: int
      phase: Phase }

let newGame (rng: Random) (faces: string list) : Game =
    let deck =
        faces
        |> List.truncate pairCount
        |> List.collect (fun f -> [ f; f ])
        |> List.toArray
    // Fisher–Yates shuffle
    for i in Array.length deck - 1 .. -1 .. 1 do
        let j = rng.Next(i + 1)
        let tmp = deck.[i]
        deck.[i] <- deck.[j]
        deck.[j] <- tmp
    { cards = deck |> Array.toList |> List.map (fun f -> { face = f; state = FaceDown })
      firstPick = None
      locked = None
      flips = 0
      mismatches = 0
      phase = Playing }

let private setCard i state cards =
    cards |> List.mapi (fun idx c -> if idx = i then { c with state = state } else c)

/// Flip a mismatched pair back face-down (fired by a timer, or implicitly
/// by the next tap so fast players are never blocked).
let resolve (game: Game) : Game =
    match game.locked with
    | Some (a, b) ->
        { game with
            cards = game.cards |> setCard a FaceDown |> setCard b FaceDown
            locked = None }
    | None -> game

let flip (i: int) (game: Game) : Game =
    let game = resolve game
    if game.phase = Done || i < 0 || i >= List.length game.cards then game
    else
        let card = game.cards |> List.item i
        if card.state <> FaceDown then game
        else
            match game.firstPick with
            | None ->
                { game with
                    cards = setCard i FaceUp game.cards
                    firstPick = Some i
                    flips = game.flips + 1 }
            | Some j ->
                let first = game.cards |> List.item j
                let flips = game.flips + 1
                if first.face = card.face then
                    let cards = game.cards |> setCard i Matched |> setCard j Matched
                    { game with
                        cards = cards
                        firstPick = None
                        flips = flips
                        phase = if cards |> List.forall (fun c -> c.state = Matched) then Done else Playing }
                else
                    { game with
                        cards = setCard i FaceUp game.cards
                        firstPick = None
                        locked = Some (i, j)
                        flips = flips
                        mismatches = game.mismatches + 1 }

/// Fewer mistakes = higher score. Perfect game scores 20.
let score (game: Game) = max 5 (20 - game.mismatches)

/// Coin payout — always a bit less than the 10-coin token, so the Arcade
/// stays a treat, not an income source.
let coinsFor (game: Game) = max 1 (8 - game.mismatches)
