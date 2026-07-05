/// Level curve and avatar evolution. Pure — unit tested on .NET.
module QuestWorld.Progression

open QuestWorld.Domain

let maxLevel = 50

/// XP required to advance FROM `level` to `level + 1`.
/// Short early levels = fast first dopamine hits, then a gentle ramp.
let xpToLevelUp (level: int) = 40 + (level - 1) * 20

/// Current level for a lifetime XP total (1-based, capped).
let levelForXp (xp: int) =
    let mutable level = 1
    let mutable remaining = xp
    while level < maxLevel && remaining >= xpToLevelUp level do
        remaining <- remaining - xpToLevelUp level
        level <- level + 1
    level

/// (xp gained inside the current level, xp needed for the next level)
let levelProgress (xp: int) =
    let mutable level = 1
    let mutable remaining = xp
    while level < maxLevel && remaining >= xpToLevelUp level do
        remaining <- remaining - xpToLevelUp level
        level <- level + 1
    if level >= maxLevel then (0, 1) else (remaining, xpToLevelUp level)

/// Avatar evolution ladders per theme.
let avatarStages (theme: ProfileTheme) : AvatarStage list =
    match theme with
    | DragonDream ->
        [ { minLevel = 1;  name = "Mystery Egg";      emoji = "🥚";  blurb = "Something magical is stirring inside…" }
          { minLevel = 3;  name = "Dragon Hatchling"; emoji = "🐣";  blurb = "She hatched! Tiny wings, big dreams." }
          { minLevel = 5;  name = "Baby Dragon";      emoji = "🐲";  blurb = "First sparkles! She copies everything you do." }
          { minLevel = 8;  name = "Sparkle Dragon";   emoji = "🐉";  blurb = "Her scales shimmer pink and gold." }
          { minLevel = 12; name = "Sky Dancer";       emoji = "🦋";  blurb = "She loops through rainbow clouds." }
          { minLevel = 16; name = "Crystal Dragon";   emoji = "✨";  blurb = "Made of pure starlight and courage." }
          { minLevel = 20; name = "Legendary Queen";  emoji = "👑";  blurb = "Queen of the Dragon Realm. All hail Thea!" } ]
    | BlockCraft ->
        [ { minLevel = 1;  name = "Wooden Rookie";      emoji = "🪵"; blurb = "Everyone starts by punching trees." }
          { minLevel = 3;  name = "Stone Scout";        emoji = "🪨"; blurb = "Upgraded! Time to dig deeper." }
          { minLevel = 5;  name = "Iron Adventurer";    emoji = "⚔️"; blurb = "Full iron kit. Cave-ready." }
          { minLevel = 8;  name = "Gold Explorer";      emoji = "🏆"; blurb = "Shiny armour, treasure maps unlocked." }
          { minLevel = 12; name = "Redstone Engineer";  emoji = "⚡"; blurb = "Builds machines that build things." }
          { minLevel = 16; name = "Diamond Knight";     emoji = "💎"; blurb = "Blue sparkle armour. Almost unbeatable." }
          { minLevel = 20; name = "Netherite Legend";   emoji = "🔥"; blurb = "Forged in lava. The stuff of legends, Levi!" } ]
    | AdminClean ->
        [ { minLevel = 1; name = "Quest Master"; emoji = "🧭"; blurb = "Keeper of the QuestWorld." } ]

let avatarStageFor (theme: ProfileTheme) (level: int) : AvatarStage =
    avatarStages theme
    |> List.filter (fun s -> s.minLevel <= level)
    |> List.last

let nextAvatarStage (theme: ProfileTheme) (level: int) : AvatarStage option =
    avatarStages theme |> List.tryFind (fun s -> s.minLevel > level)
