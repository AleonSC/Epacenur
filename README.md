# Epacenur

A RuneScape-style skilling game for **Unity 6**. You can play top down, clicking tiles, or in first person.
For now it's a **dev area** with plots for testing skills and a **bank booth**. The only skill so far is **Woodcutting**, with basic trees.

There are no art assets: everything is built from Unity's primitive shapes when you press Play,
so there are no scenes or prefabs to wire up.

## Opening the project

1. Install [Unity Hub](https://unity.com/download) and a **Unity 6** editor (6000.0 LTS).
2. In Unity Hub: **Add → Add project from disk** and pick this folder. If Hub says the exact editor
   version isn't installed, pick any Unity 6 version you have.
3. When the import finishes, press **Play**. The first time it opens, the project creates
   `Assets/Scenes/DevArea.unity` for you. The game also starts from any scene, including Unity's default one.
4. Commit the `.meta` files Unity generates on first import.

The **Epacenur** menu has *Build Windows Game*, *Open Dev Area Scene* and *Delete Saved Progress*.

## Controls

| | Tile mode (default) | First person |
|---|---|---|
| Move | Left-click a tile to walk there (pathfinding), or **WASD** | **WASD** |
| Chop a tree | Left-click it: you walk next to it and chop automatically until it becomes a stump or your inventory is full | Look at it and **left-click once per chop** (within reach) |
| Camera | Arrow keys or middle-drag to rotate, scroll to zoom | Mouse look. **Esc** frees the mouse; click the view to grab it again |
| Use the bank | Left-click the bank booth (by spawn): you walk up to it and the bank opens | Look at it and left-click (within reach) |
| Switch view | **V** | **V** |

- **Inventory**: 28 slots, one log per slot. Click a slot and then another to swap them. Right-click or shift-click to drop an item.
- **Skills** tab: level (out of 999), xp, xp to the next level and a progress bar. Hover for the exact xp.
- **H** shows or hides the help, and **F1** opens the dev tools.
- Progress (skills, inventory, position) saves every 15 s and when you quit.

## Bank

- The bank booth stands just north-east of spawn. Clicking it opens the bank window.
- Each kind of item takes **one bank slot and stacks there**, even items like logs that take a slot each in the
  inventory. The bank has 400 slots. Counts show RuneScape style: yellow below 100K, then K, then M in green.
- While the bank is open, click an inventory item to deposit it, or click a bank slot to withdraw. The **1 / 5 / 10 / All**
  buttons set how many. Right-click or shift-click always moves all of it. **Deposit inventory** empties your backpack in one go.
  You only get as many items as you have free inventory slots.
- The bank closes with **X**, **Esc**, or when you walk away from the booth. It's saved along with everything else.

## Woodcutting

- A chop succeeds RuneScape style: the chance rises in a straight line from 25% at level 1 to 79% at level 99
  for a basic tree, and keeps rising past 99 until it is certain (around level 140).
- Each success gives **one log in its own inventory slot** and 25 xp, and takes one of the tree's
  **lives** (3–6, rolled each time it grows). With no lives left it becomes a stump, then grows back after 12 s.
- Tile mode makes a chop attempt every 4 ticks (2.4 s) while you stand next to the tree. In first person each
  click is one attempt, at most one every 2 ticks (1.2 s). The crosshair turns green when a tree is in reach and your
  swing is ready, and yellow while you recover. These timings are in `WoodcuttingRules` in `Assets/Scripts/Core/Woodcutting.cs`.
- You can't chop with a full inventory, and you stop when it fills up. No axe is needed yet.

## Experience: levels 1 to 999

The xp curve is RuneScape's, `xp(L) = floor(1/4 * Σ_{l=1}^{L-1} floor(l + 300 * 2^(l/7)))`: the gap between levels grows
by 2^(1/7) ≈ 10.4% a level. Level 99 is 13,034,431 xp, 120 is 104,273,167 and 200m is level 126, as in RuneScape.
There's **no 200m cap**: levels go up to **999**, which needs

**6,590,753,875,914,258,720,611,083,014,045,249,960,396,472,940 xp** (about 6.59 × 10^45).

That is far more than a 64-bit number holds, so xp is a `BigInteger`, kept in tenths so half-xp rewards
(like a willow log's 67.5) add up exactly. See `Assets/Scripts/Core/Experience.cs`.

## Dev tools (F1)

- Each skill: set the level (−1, +1, 1, 99, 120, 999) or add 1K / 1M / 1B xp
- Empty the inventory or fill it with logs
- Empty the bank, or put 1,000 logs in it
- Regrow every tree
- Teleport to spawn or to a skill's plot
- Save now, or reset everything

## Code layout

| Folder | What's there |
|---|---|
| `Assets/Scripts/Core` | Game rules with no Unity dependency: xp curve, skills, items, inventory and bank, tree definitions and chop chances, tile grid and A* pathfinding |
| `Assets/Scripts/Runtime` | The Unity side: `Game` (startup), `World` and `DevArea` (the map and its plots), `TreeObject`, `BankBooth`, `PlayerController` (both view modes, walking, chopping), `CameraRig`, `Hud` (all the UI, built with IMGUI), `SaveGame` |
| `Assets/Editor` | Creates the scene and build settings, and the Epacenur menu |
| `Tools/Tests` | `run.sh`: tests for the rules in Core (xp table, skills, inventory, bank, pathfinding) |
| `Tools/CompileCheck` | `check.sh`: compiles every script against stubbed Unity APIs, without Unity |

Both scripts need mono (`apt-get install mono-mcs mono-runtime`).

### Adding a skill

1. Add it to `SkillId` and `SkillDefs.All` in `Assets/Scripts/Core/Skills.cs`. It then shows up in the Skills tab and the dev tools.
2. Subclass `SkillArea` (see `WoodcuttingArea` in `Assets/Scripts/Runtime/DevArea.cs`) and put it in
   `DevArea.CreateAreas()` in place of one of the empty plots.
3. Give out xp with `Game.Instance.Skills.AddXp(SkillId.YourSkill, amount)`. Xp drops and level-up messages are automatic.

### Adding a tree or an item

- Trees: add a `TreeDef` in `Assets/Scripts/Core/Woodcutting.cs` (level, xp, lives, respawn time, chop chances, colours)
  and place it in a skill area with `world.AddTree(...)`.
- Items: add an `ItemDef` to `ItemDefs` in `Assets/Scripts/Core/Inventory.cs`.
