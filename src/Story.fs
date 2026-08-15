/// Additive story/chapter layer. Chapters point at existing quests, so old
/// saves, parent-created quests, approvals, and rewards keep their meaning.
module QuestWorld.Story

open QuestWorld.Domain

type StoryStep =
    { title: string
      description: string
      icon: string
      questId: string }

type StoryChapter =
    { id: string
      theme: ProfileTheme
      title: string
      intro: string
      rewardText: string
      mapArea: string
      mapIcon: string
      steps: StoryStep list }

type ChapterProgress =
    { completedSteps: int
      totalSteps: int
      nextStep: StoryStep option
      complete: bool }

let private dragonDream =
    { id = "dragondream-moon-egg"
      theme = DragonDream
      title = "The Lost Moon Egg"
      intro = "A moon-dragon egg has gone dim. Gather three Moon Sparks to wake it safely."
      rewardText = "Moon Dragon friendship unlocked"
      mapArea = "Dragon Nest"
      mapIcon = "🐉"
      steps =
        [ { title = "Prepare the Nest"; description = "Smooth your bed so the egg has a safe nest."; icon = "🪺"; questId = "q-bed" }
          { title = "Find a Moon Spark"; description = "Finish homework to reveal the first Moon Spark."; icon = "✨"; questId = "q-homework" }
          { title = "Clear the Hatchery"; description = "Tidy your room and make space for the hatchling."; icon = "🌙"; questId = "q-room" } ] }

let private blockCraft =
    { id = "blockcraft-hidden-fortress"
      theme = BlockCraft
      title = "Build the Hidden Fortress"
      intro = "A secret blueprint needs three strong blocks before the fortress can rise."
      rewardText = "Hidden Fortress area unlocked"
      mapArea = "Base Camp"
      mapIcon = "🏰"
      steps =
        [ { title = "Lay the Foundation"; description = "Make your bed to place the first foundation blocks."; icon = "🧱"; questId = "q-bed" }
          { title = "Mine the Blueprint"; description = "Finish homework to uncover the hidden blueprint."; icon = "💎"; questId = "q-homework" }
          { title = "Build the Walls"; description = "Tidy your room to raise the fortress walls."; icon = "🛡️"; questId = "q-room" } ] }

let chapterFor theme =
    match theme with
    | DragonDream -> dragonDream
    | BlockCraft -> blockCraft
    | AdminClean -> dragonDream

let private completedEver (data: AppData) userId questId =
    data.completions
    |> List.exists (fun c -> c.userId = userId && c.questId = questId && c.status = Completed)

let chapterProgress (data: AppData) userId (chapter: StoryChapter) =
    let completed =
        chapter.steps
        |> List.takeWhile (fun step -> completedEver data userId step.questId)
        |> List.length
    { completedSteps = completed
      totalSteps = List.length chapter.steps
      nextStep = chapter.steps |> List.tryItem completed
      complete = completed = List.length chapter.steps }

