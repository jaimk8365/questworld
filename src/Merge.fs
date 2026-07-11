/// Cross-device merge for sync. Combines two AppData snapshots into one:
///  • users        — per-user last-writer-wins by touchedAt (xp breaks ties,
///                   since xp only ever grows)
///  • completions  — union by (quest, user, period); newer stamp wins,
///                   higher status breaks ties (Completed > Pending > Rejected)
///  • redemptions  — union by id; a settled one (fulfilled/cancelled) wins
///  • arcadeScores — union by (user, game, week), keeping the max score
///  • quests+prizes— taken wholesale from the higher catalogRev, so parent
///                   edits (and deletions) propagate cleanly
/// The merge is idempotent and symmetric, so devices converge no matter
/// the order they sync in. Pure — unit-tested on .NET.
module QuestWorld.Merge

open QuestWorld.Domain

let private statusRank =
    function
    | Completed -> 3
    | PendingApproval -> 2
    | Rejected -> 1
    | Available -> 0

let private newerUser (a: User) (b: User) : User =
    match a.touchedAt, b.touchedAt with
    | Some ta, Some tb when ta > tb -> a
    | Some ta, Some tb when tb > ta -> b
    | Some _, None -> a
    | None, Some _ -> b
    | _ -> if a.xp >= b.xp then a else b

let private mergeUsers (xs: User list) (ys: User list) : User list =
    let ofY = ys |> List.map (fun u -> u.id, u) |> Map.ofList
    let merged =
        xs
        |> List.map (fun x ->
            match ofY.TryFind x.id with
            | Some y -> newerUser x y
            | None -> x)
    let extraIds = merged |> List.map (fun u -> u.id) |> Set.ofList
    merged @ (ys |> List.filter (fun y -> not (extraIds.Contains y.id)))

let private completionStamp (c: QuestCompletion) =
    c.updatedAt |> Option.defaultValue c.completedAt

let private newerCompletion (a: QuestCompletion) (b: QuestCompletion) : QuestCompletion =
    let ta, tb = completionStamp a, completionStamp b
    if ta > tb then a
    elif tb > ta then b
    elif statusRank a.status >= statusRank b.status then a
    else b

let private completionKey (c: QuestCompletion) = c.questId, c.userId, c.periodKey

let private mergeCompletions (xs: QuestCompletion list) (ys: QuestCompletion list) : QuestCompletion list =
    let ofY = ys |> List.map (fun c -> completionKey c, c) |> Map.ofList
    let merged =
        xs
        |> List.map (fun x ->
            match ofY.TryFind (completionKey x) with
            | Some y -> newerCompletion x y
            | None -> x)
    let seen = merged |> List.map completionKey |> Set.ofList
    merged @ (ys |> List.filter (fun y -> not (seen.Contains (completionKey y))))

let private settled (r: Redemption) = r.fulfilled || (r.cancelled |> Option.defaultValue false)

let private mergeRedemptions (xs: Redemption list) (ys: Redemption list) : Redemption list =
    let ofY = ys |> List.map (fun r -> r.id, r) |> Map.ofList
    let merged =
        xs
        |> List.map (fun x ->
            match ofY.TryFind x.id with
            | Some y -> if settled y && not (settled x) then y else x
            | None -> x)
    let seen = merged |> List.map (fun r -> r.id) |> Set.ofList
    merged @ (ys |> List.filter (fun y -> not (seen.Contains y.id)))

let private scoreKey (s: ArcadeScore) = s.userId, s.game, s.weekKey

let private mergeScores (xs: ArcadeScore list) (ys: ArcadeScore list) : ArcadeScore list =
    let ofY = ys |> List.map (fun s -> scoreKey s, s) |> Map.ofList
    let merged =
        xs
        |> List.map (fun x ->
            match ofY.TryFind (scoreKey x) with
            | Some y -> if y.score > x.score then y else x
            | None -> x)
    let seen = merged |> List.map scoreKey |> Set.ofList
    merged @ (ys |> List.filter (fun y -> not (seen.Contains (scoreKey y))))

let mergeData (a: AppData) (b: AppData) : AppData =
    let revA = a.catalogRev |> Option.defaultValue 0
    let revB = b.catalogRev |> Option.defaultValue 0
    let catalogSource = if revA >= revB then a else b
    { a with
        users = mergeUsers a.users b.users
        completions = mergeCompletions (a.completions) (b.completions)
        quests = catalogSource.quests
        prizes = catalogSource.prizes
        catalogRev = Some (max revA revB)
        redemptions =
            Some (mergeRedemptions
                    (a.redemptions |> Option.defaultValue [])
                    (b.redemptions |> Option.defaultValue []))
        arcadeScores =
            Some (mergeScores
                    (a.arcadeScores |> Option.defaultValue [])
                    (b.arcadeScores |> Option.defaultValue [])) }
