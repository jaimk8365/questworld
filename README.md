# 🗺️ QuestWorld

**▶ Play it: https://jaimk8365.github.io/questworld/**
(hosted free on GitHub Pages; works offline after first load thanks to the service worker)

A behaviour & chore RPG for Thea (10), Levi (8) and the Quest Master (parent).
Built with **F# + Fable + Elmish + Feliz (React)** — MVU architecture, compiled to
JavaScript, served as a mobile web app that installs to the home screen like a
native game.

## Profiles & default passwords

| Profile | Username | Password | Theme |
|---|---|---|---|
| Thea | `thea` | `sparkle` | 🐲 DragonDream — pastel dragons, sparkles |
| Levi | `levi` | `blocks` | ⛏️ BlockCraft — pixel block world |
| Parent | `parent` | `questmaster` | 🧭 Clean admin dashboard |

**Change the passwords on first run**: log in as parent → Settings → Change a password.

## Running it

Prereqs (already installed on this Mac): Node 24, .NET 8 SDK at `~/.dotnet`.

```bash
export PATH="$HOME/.dotnet:$PATH"   # or add to ~/.zshrc
cd ~/QuestWorld
npm install        # first time only
npm start          # Fable watch + Vite dev server on http://localhost:5173
```

Other commands:

```bash
npm test           # run the 79-test logic suite (.NET)
npm run build      # production bundle → dist/
npm run preview    # serve the production bundle
```

## Putting it on the iPads / iPhone

1. On each device, open Safari and visit **https://jaimk8365.github.io/questworld/**
2. Tap **Share → Add to Home Screen**. QuestWorld installs with its own icon
   and runs full-screen like a real app — and keeps working offline.
3. Each device logs into its own profile and **stays logged in** (sessions and
   all progress persist in that device's local storage).

Each device keeps its own save. If you later want one shared world across all
three devices, see "Future expansions" below — the storage layer is a single
module ready to swap for a cloud sync backend.

## Updating the live game

After changing any code:

```bash
cd ~/QuestWorld && ./deploy.sh
```

That single command runs the test suite, rebuilds, and publishes. The live
site updates about a minute later; devices pick it up next time they open the
app with internet (the service worker fetches fresh pages network-first).

The dev server (`npm start`) is still there for local development, and also
still works as a home-network fallback at `http://<mac-ip>:5173`.

## Architecture (MVU)

```
src/
  Domain.fs       — all data models (User, Quest, Reward, Avatar, Inventory, …)
  Progression.fs  — XP curve, levels, avatar evolution ladders
  Auth.fs         — salted/iterated hashing, login, password change
  Catalog.fs      — cosmetics shop, badge definitions, seed users & quests
  QuestEngine.fs  — period keys (daily/weekly reset), completion, approval,
                    loot boxes, badge awards, shop, quest admin
  Codec.fs        — JSON round-trip (Thoth.Json / Thoth.Json.Net)
  Storage.fs      — localStorage persistence          (browser only)
  Sounds.fs       — WebAudio chiptune reward sounds   (browser only)
  State.fs        — Elmish Model / Msg / init / update
  ViewShared.fs   — XP bar, avatar, confetti, celebration overlay
  ViewLogin.fs    — profile picker + password entry
  ViewChild.fs    — themed child game UI (quests / shop / badges)
  ViewAdult.fs    — parent dashboard (overview / approvals / builder / settings)
  Main.fs         — theme switch + Elmish program entry
tests/
  Tests.fs        — 79 assertions over the exact modules the app ships
```

Everything above `Storage.fs` is pure F# with no browser dependency — that's
what lets the .NET test suite exercise the real game logic.

## Game rules

- **Auto-approve quests** (bed, teeth, shower…) reward instantly with a
  celebration: XP, coins, confetti, sound, possible loot box.
- **Approval quests** (homework, room, behaviour…) go to the parent's
  Approvals queue; rewards land when approved. "Not yet" quietly returns the
  quest to Available — no punishment mechanics.
- **Daily quests reset at midnight, weekly quests reset on Monday** (computed
  from period keys, no scheduler needed).
- **Loot boxes**: 25% drop chance per completion — bonus coins, XP boosts, or a
  rare cosmetic from the child's own theme.
- **Avatars evolve** at levels 3, 5, 8, 12, 16, 20 (egg → Legendary Queen for
  Thea; wooden rookie → Netherite Legend for Levi).
- **Coins** buy cosmetics in each child's themed shop; equipped cosmetics float
  around the avatar.

## Future expansions (hooks already in place)

- **Cloud sync**: replace `Storage.fs` with an API-backed implementation;
  `Codec.fs` already produces the wire format.
- **New profiles/themes**: add a `ProfileTheme` case, an avatar ladder in
  `Progression.fs`, cosmetics in `Catalog.fs`, and a CSS theme block.
- **Streak bonuses**: `QuestCompletion.periodKey` gives you the full history —
  a `streakFor` function in `QuestEngine.fs` is a natural addition.
- **Offline PWA**: add a service worker; the manifest is already wired.
- **Real reward redemptions**: add a parent-managed "prize shop" (screen time,
  outings) purchasable with coins.
