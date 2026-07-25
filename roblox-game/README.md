# Lantern Hollow

An original Roblox horror-lite escape game. It's built in the same spirit as
kid-favorite Roblox games like *Doors* and *Piggy*: spooky, suspenseful, and
full of jumpscares, but with **no gore, blood, or graphic violence** — safe
for the same audience that already plays those games.

## The premise

You and your friends wander into **Lantern Hollow**, an abandoned carnival at
midnight. Something called **The Ringmaster** haunts the grounds. To escape:

1. Explore the four wings of the carnival (Toy Wing, Mirror Wing, Greenhouse
   Wing, Clocktower Wing) and **light all 6 lanterns** by holding `E` near
   each one.
2. Avoid **The Ringmaster** — a wandering, red-eyed shadow that hunts down
   the nearest player once it notices them.
3. Hold `Shift` to sprint away from danger, but watch your **Courage**
   meter — sprinting drains it, and you can't sprint again until it
   recovers.
4. If The Ringmaster catches you, you're jumpscared and dragged to the
   **Jail**. You're stuck there until a friend holds `E` on the rescue lever
   to free everyone trapped.
5. Once every lantern is lit, the **Exit Gate** unlocks — reach it to
   escape and win the round. Your **Escapes** count is saved and shown on
   the leaderboard.

Rounds loop automatically: intermission → warmup → hunt → win/lose →
back to the lobby.

## What makes it original

- **Courage, not a flashlight battery.** Sprinting is tied to a "Courage"
  meter that reframes the usual horror-game stamina bar around the game's
  theme, and ties narratively into "don't panic-run."
- **Rescue-lever jail instead of elimination.** Getting caught isn't a dead
  end — it's a co-op moment where teammates have to come save you, which
  keeps the game social and non-punishing for younger players.
- **Puzzle + chase hybrid.** Progress (lighting lanterns) and danger (the
  monster hunting you) run at the same time, so there's always a
  press-your-luck decision to make.

## Project structure (Rojo)

This repo holds the game as plain-text Luau source, synced into Roblox
Studio with [Rojo](https://rojo.space/) — the standard way Roblox teams keep
game code in git. Nothing needs to be hand-built in Studio: `WorldBuilder`
procedurally generates the entire map (lobby, carnival wings, lanterns,
jail, gate, lighting/fog) when the server starts.

```
roblox-game/
  default.project.json         Rojo project file
  src/
    ReplicatedStorage/
      Modules/
        GameConfig.lua          All tunable numbers (speeds, timers, etc.)
    ServerScriptService/
      RemotesSetup.server.lua   Creates the RemoteEvents used by the game
      WorldBuilder.server.lua   Procedurally builds the entire map
      PlayerDataManager.lua     DataStore load/save for the Escapes stat
      PlayerSetup.server.lua    leaderstats + per-player round attributes
      CourageSystem.server.lua  Sprint/Courage meter logic
      LanternManager.lua        Lantern lighting + win-condition tracking
      JailManager.lua           Catch / jail / rescue logic
      MonsterAI.lua             The Ringmaster's patrol/chase brain
      GameLoop.server.lua       Round state machine tying it all together
    StarterPlayerScripts/
      HUD.client.lua            All UI: banner, courage bar, jumpscare, etc.
```

## Running it

1. Install [Roblox Studio](https://create.roblox.com/) and the
   [Rojo plugin](https://rojo.space/docs/v7/getting-started/installation/)
   (or use the `rojo` CLI).
2. From this folder, run:
   ```
   rojo serve
   ```
3. In Roblox Studio, open the Rojo plugin panel and click **Connect**.
4. Press **Play** (ideally with a couple of local server test players via
   Studio's "Start" / multiple-player test mode) to try it out.

No Rojo? You can also run `rojo build -o LanternHollow.rbxlx` and open the
resulting file directly in Studio.

**Persistence note:** the Escapes counter uses `DataStoreService`. In Studio
this only works if you enable *Game Settings → Security → Enable Studio
Access to API Services*; on a published game it works automatically. If
it's off, the game still runs fine — saving just no-ops with a warning.

## Customizing

- All balance numbers (monster speed, courage drain, round length, lantern
  count, etc.) live in `GameConfig.lua` — tweak freely.
- Swap the placeholder sounds (`rbxasset://sounds/...`, built into every
  Roblox client so they always work) for your own uploaded audio in
  `JailManager.lua`'s catch sound and anywhere else you want a custom sting.
- `WorldBuilder.server.lua` generates positions from a few constants
  (`PLAZA_RADIUS`, `WING_DISTANCE`, etc.) — nudge those, or add more wings
  to the `WINGS` table, to reshape the map without touching any other file.
- Want a scarier monster model or real animations? Replace the primitive
  parts in `MonsterAI.lua`'s `buildMonster()` with an imported rig — the
  AI logic (patrol/chase/catch) doesn't care what the model looks like as
  long as it keeps a `Humanoid` + `HumanoidRootPart` + `Head`.
