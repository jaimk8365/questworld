/// The Arcade mini-game engine — a gentle flappy-flight game.
/// Pure F# (rendering lives in ViewArcade.fs), so physics, scoring and
/// collisions are unit-tested on .NET like the rest of the game logic.
module QuestWorld.Arcade

open System

// Field dimensions (logical pixels; the view scales to fit).
let fieldW = 320.0
let fieldH = 420.0
let playerX = 60.0
let playerR = 15.0
let groundH = 22.0

// Physics — tuned forgiving for ages 8–10.
let gravity = 0.5
let flapVelocity = -7.0
let scrollSpeed = 2.2
let obstacleW = 54.0
let gapH = 158.0
let obstacleSpacing = 195.0

// Economy.
let tokenCost = 10
let newBestBonus = 5

type Obstacle =
    { x: float
      gapY: float
      passed: bool
      hasStar: bool
      starTaken: bool }

type Phase =
    | Flying
    | GameOver

type Game =
    { phase: Phase
      y: float
      vy: float
      obstacles: Obstacle list
      score: int
      stars: int
      ticks: int }

let private newObstacle (rng: Random) (x: float) =
    { x = x
      gapY = 50.0 + rng.NextDouble() * (fieldH - gapH - groundH - 100.0)
      passed = false
      hasStar = rng.Next(100) < 60
      starTaken = false }

let newGame (rng: Random) : Game =
    { phase = Flying
      y = fieldH / 2.0
      vy = 0.0
      obstacles = [ newObstacle rng (fieldW + 150.0) ]
      score = 0
      stars = 0
      ticks = 0 }

let flap (game: Game) : Game =
    if game.phase = Flying then { game with vy = flapVelocity } else game

/// One physics tick (~33ms). Moves the world, applies gravity, collects
/// stars, scores passed obstacles and detects collisions.
let step (rng: Random) (game: Game) : Game =
    if game.phase <> Flying then game
    else
        let vy = game.vy + gravity
        let y = max 12.0 (game.y + vy) // soft ceiling
        let moved = game.obstacles |> List.map (fun o -> { o with x = o.x - scrollSpeed })

        // Score passes and collect stars in one sweep.
        let swept, gainedScore, gainedStars =
            moved
            |> List.fold
                (fun (acc, s, st) o ->
                    let o, s =
                        if not o.passed && o.x + obstacleW < playerX
                        then { o with passed = true }, s + 1
                        else o, s
                    let starX = o.x + obstacleW / 2.0
                    let starY = o.gapY + gapH / 2.0
                    let o, st =
                        // generous pickup window — it should feel magnetic to kids
                        if o.hasStar && not o.starTaken
                           && abs (starX - playerX) < 36.0 && abs (starY - y) < 48.0
                        then { o with starTaken = true }, st + 1
                        else o, st
                    (o :: acc, s, st))
                ([], 0, 0)
            |> fun (os, s, st) -> List.rev os, s, st

        // Drop obstacles that scrolled off; keep the conveyor stocked
        // (new ones always spawn off-screen to the right).
        let visible = swept |> List.filter (fun o -> o.x > -obstacleW - 10.0)
        let obstacles =
            match visible |> List.tryLast with
            | Some last when last.x < fieldW + 10.0 -> visible @ [ newObstacle rng (last.x + obstacleSpacing) ]
            | None -> [ newObstacle rng (fieldW + 40.0) ]
            | _ -> visible

        let hitGround = y >= fieldH - groundH - playerR
        let hitObstacle =
            obstacles
            |> List.exists (fun o ->
                playerX + playerR > o.x
                && playerX - playerR < o.x + obstacleW
                && (y - playerR < o.gapY || y + playerR > o.gapY + gapH))

        { game with
            phase = (if hitGround || hitObstacle then GameOver else Flying)
            y = y
            vy = vy
            obstacles = obstacles
            score = game.score + gainedScore
            stars = game.stars + gainedStars
            ticks = game.ticks + 1 }
