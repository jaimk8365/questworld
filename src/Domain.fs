/// QuestWorld core data models.
/// Pure F# — shared between the Fable (browser) build and the .NET test build.
module QuestWorld.Domain

type ProfileTheme =
    | DragonDream // Thea — pastel dragons
    | BlockCraft  // Levi — pixel block world
    | AdminClean  // Parent dashboard

type Role =
    | Child
    | Parent

type QuestType =
    | Daily
    | Weekly
    | Behaviour
    | Chore
    | Bonus

type Recurrence =
    | OnceOff
    | EveryDay
    | EveryWeek

type QuestStatus =
    | Available
    | PendingApproval
    | Completed

type Reward =
    { xp: int
      coins: int }

type Quest =
    { id: string
      title: string
      description: string
      icon: string
      questType: QuestType
      reward: Reward
      assignedTo: string list   // user ids
      recurrence: Recurrence
      requiresApproval: bool
      active: bool }

/// One record per (quest, user, period). Period key is "once",
/// a day key ("2026-07-05") or a week key (Monday's day key).
type QuestCompletion =
    { questId: string
      userId: string
      periodKey: string
      status: QuestStatus
      completedAt: string }

type CosmeticKind =
    | Skin
    | Accessory
    | Trail
    | Background

type Cosmetic =
    { id: string
      name: string
      icon: string
      kind: CosmeticKind
      price: int
      theme: ProfileTheme }

type Inventory =
    { owned: string list      // cosmetic ids
      equipped: string list }

type AvatarStage =
    { minLevel: int
      name: string
      emoji: string
      blurb: string }

type LootResult =
    | CoinDrop of int
    | XpBoost of int
    | CosmeticDrop of string  // cosmetic id

type ArcadeProgress =
    { tokens: int
      bestScore: int
      totalRuns: int }

type User =
    { id: string
      username: string
      displayName: string
      passwordHash: string
      role: Role
      theme: ProfileTheme
      xp: int
      coins: int
      inventory: Inventory
      badges: string list
      // option so v1 saves (which lack the field) still decode — None = never played
      arcade: ArcadeProgress option }

type LoginSession =
    { userId: string
      loggedInAt: string }

type Settings =
    { soundOn: bool }

type AppData =
    { schemaVersion: int
      users: User list
      quests: Quest list
      completions: QuestCompletion list
      settings: Settings }

/// Everything the UI needs to celebrate a completion.
type CompletionOutcome =
    { quest: Quest
      reward: Reward
      loot: LootResult option
      levelBefore: int
      levelAfter: int
      newBadges: string list
      pendingApproval: bool }
