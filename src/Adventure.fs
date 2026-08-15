/// Daily focus and gentle return logic. This derives from existing completion
/// records and introduces no new synced fields or streak penalties.
module QuestWorld.Adventure

open System
open QuestWorld.Domain
open QuestWorld.QuestEngine
open QuestWorld.Story

type AdventureProgress =
    { doneCount: int
      totalCount: int
      complete: bool }

type WelcomeBack =
    { title: string
      message: string
      suggestedQuestId: string option }

let private stableSeed (userId: string) (today: DateTime) =
    (userId |> Seq.sumBy int) + today.DayOfYear

let featuredMissions (data: AppData) userId (today: DateTime) =
    let all = questsForUser data userId today
    let theme = data.users |> List.tryFind (fun u -> u.id = userId) |> Option.map (fun u -> u.theme)
    let storyQuest =
        theme
        |> Option.bind (fun t -> (chapterProgress data userId (chapterFor t)).nextStep)
        |> Option.map (fun s -> s.questId)
    let rotated =
        match all with
        | [] -> []
        | xs ->
            let offset = stableSeed userId today % List.length xs
            (xs |> List.skip offset) @ (xs |> List.take offset)
    let prioritised =
        match storyQuest with
        | None -> rotated
        | Some id ->
            (all |> List.filter (fun (q, _) -> q.id = id)) @
            (rotated |> List.filter (fun (q, _) -> q.id <> id))
    prioritised |> List.truncate 3

let adventureProgress data userId today =
    let missions = featuredMissions data userId today
    let doneCount = missions |> List.filter (fun (_, status) -> status = Completed) |> List.length
    { doneCount = doneCount; totalCount = List.length missions
      complete = not (List.isEmpty missions) && doneCount = List.length missions }

let private latestActivity (data: AppData) userId =
    data.completions
    |> List.filter (fun c -> c.userId = userId)
    |> List.choose (fun c ->
        match DateTime.TryParse c.completedAt with
        | true, value -> Some value
        | _ -> None)
    |> List.sortDescending
    |> List.tryHead

let welcomeBack (data: AppData) userId (today: DateTime) =
    let hasBeenAway =
        match latestActivity data userId with
        | None -> false
        | Some last -> (today.Date - last.Date).TotalDays >= 2.0
    if not hasBeenAway then None
    else
        let user = data.users |> List.tryFind (fun u -> u.id = userId)
        let title, message =
            match user |> Option.map (fun u -> u.theme) with
            | Some DragonDream -> "Welcome back, Dragon Keeper! 🐉", "Your dragon missed you. Pick one easy mission whenever you're ready — nothing was lost."
            | Some BlockCraft -> "Welcome back, Builder! 🧱", "Your base is safe. Pick one easy mission whenever you're ready — nothing was lost."
            | _ -> "Welcome back!", "Continue whenever you're ready — nothing was lost."
        let suggested = featuredMissions data userId today |> List.tryHead |> Option.map (fun (q, _) -> q.id)
        Some { title = title; message = message; suggestedQuestId = suggested }
