/// Static game content: cosmetics, badges, seed users and seed quests.
module QuestWorld.Catalog

open QuestWorld.Domain
open QuestWorld.Auth

// ---------------------------------------------------------------- cosmetics

let cosmetics : Cosmetic list =
    [ // Thea — DragonDream
      { id = "c-heart-glasses"; name = "Heart Glasses";   icon = "😍"; kind = Accessory;  price = 40;  theme = DragonDream }
      { id = "c-sparkle-wings"; name = "Sparkle Wings";   icon = "🧚"; kind = Skin;       price = 60;  theme = DragonDream }
      { id = "c-golden-crown";  name = "Golden Crown";    icon = "👑"; kind = Accessory;  price = 90;  theme = DragonDream }
      { id = "c-rainbow-trail"; name = "Rainbow Trail";   icon = "🌈"; kind = Trail;      price = 120; theme = DragonDream }
      { id = "c-star-castle";   name = "Star Castle";     icon = "🏰"; kind = Background; price = 150; theme = DragonDream }
      { id = "c-moon-pet";      name = "Moon Kitten";     icon = "🐱"; kind = Accessory;  price = 200; theme = DragonDream }
      // Levi — BlockCraft
      { id = "c-wood-sword";    name = "Wooden Sword";    icon = "🗡️"; kind = Accessory;  price = 40;  theme = BlockCraft }
      { id = "c-gold-helmet";   name = "Golden Helmet";   icon = "🪖"; kind = Accessory;  price = 60;  theme = BlockCraft }
      { id = "c-creeper-skin";  name = "Creeper Skin";    icon = "🟩"; kind = Skin;       price = 90;  theme = BlockCraft }
      { id = "c-tnt-trail";     name = "TNT Trail";       icon = "🧨"; kind = Trail;      price = 120; theme = BlockCraft }
      { id = "c-nether-bg";     name = "Nether Portal";   icon = "🌋"; kind = Background; price = 150; theme = BlockCraft }
      { id = "c-ender-pet";     name = "Ender Buddy";     icon = "🐙"; kind = Accessory;  price = 200; theme = BlockCraft } ]

let cosmeticById (id: string) = cosmetics |> List.tryFind (fun c -> c.id = id)

let cosmeticsForTheme (theme: ProfileTheme) = cosmetics |> List.filter (fun c -> c.theme = theme)

/// Card faces for the memory mini-game, per theme.
let memoryFaces (theme: ProfileTheme) : string list =
    match theme with
    | DragonDream -> [ "🐲"; "🦄"; "🌈"; "⭐"; "👑"; "💖"; "🌸"; "🧚" ]
    | _ -> [ "💎"; "⛏️"; "🧨"; "🐷"; "🗡️"; "🔥"; "🌋"; "🟩" ]

// ------------------------------------------------------------------- badges

type BadgeCtx =
    { level: int
      totalCompleted: int
      choreCompleted: int
      behaviourCompleted: int
      cosmeticsOwned: int
      arcadeBest: int
      arcadeRuns: int }

type BadgeDef =
    { id: string
      name: string
      icon: string
      description: string
      earned: BadgeCtx -> bool }

let badgeDefs : BadgeDef list =
    [ { id = "b-first";     name = "First Steps";     icon = "🌟"; description = "Complete your very first quest";  earned = fun c -> c.totalCompleted >= 1 }
      { id = "b-ten";       name = "Quest Apprentice"; icon = "🎖️"; description = "Complete 10 quests";             earned = fun c -> c.totalCompleted >= 10 }
      { id = "b-twentyfive"; name = "Quest Hero";      icon = "🏅"; description = "Complete 25 quests";             earned = fun c -> c.totalCompleted >= 25 }
      { id = "b-fifty";     name = "Quest Champion";   icon = "🏆"; description = "Complete 50 quests";             earned = fun c -> c.totalCompleted >= 50 }
      { id = "b-hundred";   name = "Quest Legend";     icon = "💫"; description = "Complete 100 quests";            earned = fun c -> c.totalCompleted >= 100 }
      { id = "b-chores";    name = "Chore Machine";    icon = "🧹"; description = "Complete 15 chore quests";       earned = fun c -> c.choreCompleted >= 15 }
      { id = "b-kindness";  name = "Kindness Star";    icon = "💖"; description = "Complete 10 behaviour quests";   earned = fun c -> c.behaviourCompleted >= 10 }
      { id = "b-level5";    name = "Rising Star";      icon = "🚀"; description = "Reach level 5";                  earned = fun c -> c.level >= 5 }
      { id = "b-level10";   name = "Superstar";        icon = "🌠"; description = "Reach level 10";                 earned = fun c -> c.level >= 10 }
      { id = "b-level20";   name = "Mythic";           icon = "🐲"; description = "Reach level 20";                 earned = fun c -> c.level >= 20 }
      { id = "b-collector"; name = "Collector";        icon = "🎒"; description = "Own 3 cosmetics";                earned = fun c -> c.cosmeticsOwned >= 3 }
      { id = "b-arcade";    name = "Game On";          icon = "🎮"; description = "Play your first Arcade flight";  earned = fun c -> c.arcadeRuns >= 1 }
      { id = "b-ace";       name = "Ace Pilot";        icon = "✈️"; description = "Score 20 in one Arcade flight";  earned = fun c -> c.arcadeBest >= 20 } ]

let badgeById (id: string) = badgeDefs |> List.tryFind (fun b -> b.id = id)

// --------------------------------------------------------------- seed users

let private emptyInventory = { owned = []; equipped = [] }

let seedUsers : User list =
    [ { id = "u-thea";  username = "thea";  displayName = "Thea"
        passwordHash = hashPassword "thea" "sparkle"
        role = Child; theme = DragonDream; xp = 0; coins = 0
        inventory = emptyInventory; badges = []; arcade = None }
      { id = "u-levi";  username = "levi";  displayName = "Levi"
        passwordHash = hashPassword "levi" "blocks"
        role = Child; theme = BlockCraft; xp = 0; coins = 0
        inventory = emptyInventory; badges = []; arcade = None }
      { id = "u-parent"; username = "parent"; displayName = "Quest Master"
        passwordHash = hashPassword "parent" "questmaster"
        role = Parent; theme = AdminClean; xp = 0; coins = 0
        inventory = emptyInventory; badges = []; arcade = None } ]

// -------------------------------------------------------------- seed quests

let private q id title desc icon qtype xp coins assignees recurrence approval =
    { id = id; title = title; description = desc; icon = icon
      questType = qtype; reward = { xp = xp; coins = coins }
      assignedTo = assignees; recurrence = recurrence
      requiresApproval = approval; active = true }

let bothKids = [ "u-thea"; "u-levi" ]

let seedQuests : Quest list =
    [ // Daily quests — quick wins, auto-approved
      q "q-bed"      "Make your bed"          "Smooth covers, pillow on top!"          "🛏️" Daily 15 5  bothKids   EveryDay false
      q "q-teeth-am" "Brush teeth (morning)"  "Two whole minutes of sparkly teeth."    "🪥" Daily 10 3  bothKids   EveryDay false
      q "q-teeth-pm" "Brush teeth (night)"    "Goodnight, clean teeth!"                "🌙" Daily 10 3  bothKids   EveryDay false
      q "q-dressed"  "Dressed & ready by 8"   "Clothes on, shoes found, bag packed."   "🎒" Daily 15 5  bothKids   EveryDay false
      q "q-homework" "Homework done"          "Show a grown-up when you finish."       "📚" Daily 25 8  bothKids   EveryDay true
      // Chores — parent checks these
      q "q-room"     "Tidy your room"         "Floor clear, toys home, bin emptied."   "🧸" Chore 30 12 bothKids   EveryDay true
      q "q-pet"      "Feed the pet"           "Fresh food AND fresh water."            "🐾" Chore 15 5  bothKids   EveryDay true
      q "q-table"    "Set or clear the table" "Helper of the mealtime!"                "🍽️" Chore 15 6  bothKids   EveryDay true
      q "q-shower"   "Shower or bath"         "Soap. Actual soap. With bubbles."       "🛁" Chore 15 5  bothKids   EveryDay false
      // Weekly quests
      q "q-deepclean" "Room deep clean"       "Under the bed counts. Yes, really."     "🧽" Weekly 60 25 bothKids  EveryWeek true
      q "q-laundry"   "Laundry helper"        "Sort, carry, or fold one load."         "🧺" Weekly 40 15 bothKids  EveryWeek true
      // Behaviour quests — parent approves
      q "q-kind"     "Kind words day"         "Say 3 kind things to someone."          "💝" Behaviour 25 10 bothKids EveryDay true
      q "q-calm"     "Calm body"              "Big feelings handled with calm hands."  "🧘" Behaviour 25 10 bothKids EveryDay true
      q "q-listen"   "First-time listening"   "Asked once. Done once. Magic!"          "👂" Behaviour 25 10 bothKids EveryDay true
      q "q-routine"  "Morning routine solo"   "Whole routine, zero reminders."         "☀️" Behaviour 30 12 bothKids EveryDay true
      // Bonus
      q "q-surprise" "Surprise mission!"      "Ask the Quest Master for today's secret mission…" "🎁" Bonus 50 20 bothKids OnceOff true ]

// -------------------------------------------------------------- seed prizes

/// Example real-world prizes — the parent edits these in the Prizes tab.
let seedPrizes : Prize list =
    [ { id = "p-screen";  title = "30 mins extra screen time"; icon = "📺"; cost = 40; active = true }
      { id = "p-dinner";  title = "Choose Friday dinner";      icon = "🍕"; cost = 60; active = true }
      { id = "p-latebed"; title = "Stay up 30 mins later";     icon = "🌙"; cost = 50; active = true } ]

let seedData : AppData =
    { schemaVersion = 1
      users = seedUsers
      quests = seedQuests
      completions = []
      settings = { soundOn = true }
      arcadeScores = Some []
      prizes = Some seedPrizes
      redemptions = Some [] }
