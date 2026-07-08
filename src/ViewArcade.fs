/// The Arcade tab — token booth, the flight game itself, results screen.
module QuestWorld.ViewArcade

open Feliz
open QuestWorld.Domain
open QuestWorld.Progression
open QuestWorld.QuestEngine
open QuestWorld.State

let private gameName (theme: ProfileTheme) =
    match theme with
    | DragonDream -> "🌸 Sky Dance"
    | _ -> "⛏️ Cave Flight"

let private memoryName (theme: ProfileTheme) =
    match theme with
    | DragonDream -> "🃏 Dragon Pairs"
    | _ -> "🃏 Miner's Match"

let private collectible (theme: ProfileTheme) =
    match theme with
    | DragonDream -> "⭐"
    | _ -> "💎"

/// The playfield. Tap anywhere to flap.
let private gameField (model: Model) (user: User) (game: Arcade.Game) dispatch =
    let stage = avatarStageFor user.theme (levelForXp user.xp)
    let star = collectible user.theme
    Html.div [
        prop.className "arcade-field"
        prop.onPointerDown (fun e -> e.preventDefault (); dispatch ArcadeFlap)
        prop.children [
            // obstacles: a top and bottom column per obstacle
            for o in game.obstacles do
                Html.div [
                    prop.className "arcade-col arcade-col-top"
                    prop.style [
                        style.left (length.px o.x)
                        style.height (length.px o.gapY)
                    ]
                ]
                Html.div [
                    prop.className "arcade-col arcade-col-bottom"
                    prop.style [
                        style.left (length.px o.x)
                        style.top (length.px (o.gapY + Arcade.gapH))
                        style.height (length.px (Arcade.fieldH - Arcade.groundH - o.gapY - Arcade.gapH))
                    ]
                ]
                if o.hasStar && not o.starTaken then
                    Html.div [
                        prop.className "arcade-star"
                        prop.style [
                            style.left (length.px (o.x + Arcade.obstacleW / 2.0 - 12.0))
                            style.top (length.px (o.gapY + Arcade.gapH / 2.0 - 12.0))
                        ]
                        prop.text star
                    ]
            // the player — the child's own avatar
            Html.div [
                prop.className "arcade-player"
                prop.style [
                    style.left (length.px (Arcade.playerX - 16.0))
                    style.top (length.px (game.y - 16.0))
                    style.transform (transform.rotate (max -25.0 (min 45.0 (game.vy * 4.0))))
                ]
                prop.text stage.emoji
            ]
            Html.div [ prop.className "arcade-ground" ]
            Html.div [
                prop.className "arcade-hud"
                prop.text (sprintf "%d  ·  %s %d" game.score star game.stars)
            ]
            if game.ticks < 45 then
                Html.div [ prop.className "arcade-hint"; prop.text "Tap to fly! 👆" ]
            // results panel
            if game.phase = Arcade.GameOver then
                let arcade = arcadeOf user
                Html.div [
                    prop.className "arcade-over pop-in"
                    prop.children [
                        Html.h3 [ prop.text (if (model.arcadeResult |> Option.map (fun r -> r.newBest) |> Option.defaultValue false)
                                             then "🏆 NEW BEST!" else "Nice flying!") ]
                        Html.div [ prop.className "arcade-over-score"; prop.text (sprintf "Score: %d" game.score) ]
                        match model.arcadeResult with
                        | Some r when r.coinsEarned > 0 ->
                            Html.div [ prop.className "arcade-over-coins"; prop.text (sprintf "+%d 🪙 earned!" r.coinsEarned) ]
                        | _ -> Html.none
                        match model.arcadeResult with
                        | Some r ->
                            for b in r.newBadges do
                                match Catalog.badgeById b with
                                | Some badge -> Html.div [ prop.className "arcade-over-badge"; prop.text (sprintf "%s New badge: %s!" badge.icon badge.name) ]
                                | None -> Html.none
                        | None -> Html.none
                        Html.div [ prop.className "arcade-over-best"; prop.text (sprintf "Best: %d" arcade.bestScore) ]
                        Html.div [
                            prop.className "pw-buttons"
                            prop.children [
                                Html.button [
                                    prop.className "btn btn-ghost"
                                    prop.text "Back"
                                    prop.onClick (fun e -> e.stopPropagation (); dispatch ArcadeExit)
                                ]
                                Html.button [
                                    prop.className "btn btn-primary"
                                    prop.disabled ((arcadeOf user).tokens < 1)
                                    prop.text "Fly again (1 🎟️)"
                                    prop.onClick (fun e -> e.stopPropagation (); dispatch ArcadeStart)
                                ]
                            ]
                        ]
                    ]
                ]
        ]
    ]

/// The memory-match playfield.
let private memoryField (model: Model) (user: User) (game: Memory.Game) dispatch =
    Html.div [
        prop.className "memory-wrap"
        prop.children [
            Html.h3 [ prop.className "section-title"; prop.text (memoryName user.theme) ]
            Html.div [ prop.className "memory-hud"
                       prop.text (sprintf "Flips: %d · Oops: %d" game.flips game.mismatches) ]
            Html.div [
                prop.className "memory-grid"
                prop.children [
                    for i, card in List.indexed game.cards do
                        Html.button [
                            prop.className (
                                match card.state with
                                | Memory.FaceDown -> "mem-card"
                                | Memory.FaceUp -> "mem-card mem-up"
                                | Memory.Matched -> "mem-card mem-matched")
                            prop.text (match card.state with Memory.FaceDown -> "❔" | _ -> card.face)
                            prop.onClick (fun _ -> dispatch (MemoryFlip i))
                        ]
                ]
            ]
            if game.phase = Memory.Done then
                let arcade = arcadeOf user
                Html.div [
                    prop.className "arcade-over-inline pop-in"
                    prop.children [
                        Html.h3 [ prop.text (if (model.arcadeResult |> Option.map (fun r -> r.newBest) |> Option.defaultValue false)
                                             then "🏆 NEW BEST!" else "All pairs found!") ]
                        Html.div [ prop.className "arcade-over-score"; prop.text (sprintf "Score: %d" (Memory.score game)) ]
                        match model.arcadeResult with
                        | Some r when r.coinsEarned > 0 ->
                            Html.div [ prop.className "arcade-over-coins"; prop.text (sprintf "+%d 🪙 earned!" r.coinsEarned) ]
                        | _ -> Html.none
                        Html.div [
                            prop.className "pw-buttons"
                            prop.children [
                                Html.button [
                                    prop.className "btn btn-ghost"
                                    prop.text "Back"
                                    prop.onClick (fun _ -> dispatch MemoryExit)
                                ]
                                Html.button [
                                    prop.className "btn btn-primary"
                                    prop.disabled (arcade.tokens < 1)
                                    prop.text "Play again (1 🎟️)"
                                    prop.onClick (fun _ -> dispatch MemoryStart)
                                ]
                            ]
                        ]
                    ]
                ]
        ]
    ]

/// Booth screen: buy tokens, pick a game, see records.
let private booth (model: Model) (user: User) dispatch =
    let arcade = arcadeOf user
    let memBest = lifetimeBest model.data user.id "memory"
    Html.div [
        prop.className "arcade-booth"
        prop.children [
            Html.h3 [ prop.className "section-title"; prop.text "🎮 The Arcade" ]
            Html.div [
                prop.className "arcade-stats"
                prop.children [
                    Html.div [ prop.className "kid-stat"; prop.text (sprintf "🎟️ %d tokens" arcade.tokens) ]
                    Html.div [ prop.className "kid-stat"; prop.text (sprintf "🛫 %d games played" arcade.totalRuns) ]
                ]
            ]
            match model.arcadeMessage with
            | Some m -> Html.div [ prop.className "shop-message"; prop.text m ]
            | None -> Html.none
            Html.button [
                prop.className "btn"
                prop.disabled (user.coins < Arcade.tokenCost)
                prop.text (sprintf "Buy token · %d 🪙" Arcade.tokenCost)
                prop.onClick (fun _ -> dispatch BuyArcadeToken)
            ]
            Html.div [
                prop.className "game-grid"
                prop.children [
                    Html.div [
                        prop.className "game-card"
                        prop.children [
                            Html.div [ prop.className "game-card-icon"; prop.text "🛫" ]
                            Html.div [ prop.className "game-card-name"; prop.text (gameName user.theme) ]
                            Html.p [ prop.className "game-card-blurb"
                                     prop.text (match user.theme with
                                                | DragonDream -> "Tap to fly through the clouds — catch stars!"
                                                | _ -> "Tap to fly through the pillars — grab diamonds!") ]
                            Html.div [ prop.className "game-card-best"; prop.text (sprintf "Best: %d" arcade.bestScore) ]
                            Html.button [
                                prop.className "btn btn-primary btn-small"
                                prop.disabled (arcade.tokens < 1)
                                prop.text "Play (1 🎟️)"
                                prop.onClick (fun _ -> dispatch ArcadeStart)
                            ]
                        ]
                    ]
                    Html.div [
                        prop.className "game-card"
                        prop.children [
                            Html.div [ prop.className "game-card-icon"; prop.text "🃏" ]
                            Html.div [ prop.className "game-card-name"; prop.text (memoryName user.theme) ]
                            Html.p [ prop.className "game-card-blurb"
                                     prop.text "Find all 8 pairs — fewer mistakes, more coins!" ]
                            Html.div [ prop.className "game-card-best"; prop.text (sprintf "Best: %d" memBest) ]
                            Html.button [
                                prop.className "btn btn-primary btn-small"
                                prop.disabled (arcade.tokens < 1)
                                prop.text "Play (1 🎟️)"
                                prop.onClick (fun _ -> dispatch MemoryStart)
                            ]
                        ]
                    ]
                ]
            ]
            Html.p [ prop.className "arcade-payout"
                     prop.text (sprintf "Catching %s pays coins back — and beating your best score earns +%d 🪙!"
                                        (collectible user.theme) Arcade.newBestBonus) ]
        ]
    ]

let private lockedView (user: User) =
    let level = levelForXp user.xp
    Html.div [
        prop.className "arcade-locked"
        prop.children [
            Html.div [ prop.className "arcade-lock-icon"; prop.text "🔒" ]
            Html.h3 [ prop.text "The Arcade" ]
            Html.p [ prop.text (sprintf "Unlocks at level %d — you're level %d!" arcadeUnlockLevel level) ]
            Html.p [ prop.className "encourage"; prop.text "Keep questing, it's worth it… 🎮" ]
        ]
    ]

let view (model: Model) (user: User) dispatch =
    if not (arcadeUnlockedFor user) then lockedView user
    else
        match model.arcadeGame, model.memoryGame with
        | Some game, _ -> gameField model user game dispatch
        | _, Some game -> memoryField model user game dispatch
        | None, None -> booth model user dispatch
