# Epacenur

A RuneScape-style skilling game for **Unity 6**. You can play top down, clicking tiles, or in first person.
For now it's a **dev area** with plots for testing skills, a **bank booth** and a **shop**: **Woodcutting** with basic trees,
and **goblins** to fight (Attack, Strength, Defence and Hitpoints).

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
| Trade with the shop keeper | Left-click him (beside the bank booth) | Look at him and left-click |
| Attack a goblin (sword and shield worn) | Left-click it: you walk up and keep hitting it until it dies | Look at it and left-click once per hit (within reach) |
| Pick up an item on the ground | Left-click it ("Take"): you walk onto its tile and pick it up | Look at it and left-click (within reach) |
| Switch view | **V** | **V** |

- **Right-click menu**: right-click anything for RuneScape's "Choose Option" menu, listing every option for
  what's under the cursor, e.g. *Attack Goblin (level-2)*, *Take Coins (12)*, *Walk here*, *Examine Goblin*, *Cancel*.
  Items lying on the tile are all listed. The top option is what left-click does, and the top-left text shows it
  ("Chop down Tree / 2 more options"). Inventory items get Wield / Wear, Drop and Examine. Worn items get Remove and Examine.
  In first person, right-click lists the options for what you aim at (within reach), and the mouse is grabbed again
  once you choose. The menu closes when you pick something, click elsewhere, or move the mouse away from it.
- **Panel tabs**: **Items** (inventory), **Worn** (equipment) and **Skills**.
- **Inventory**: 28 slots, one log per slot. Click a sword or shield to wield it. Click any other item and then another
  slot to swap them. Shift-click an item to **drop it on the ground** (or use Drop in its right-click menu).
- **Skills** tab: laid out like RuneScape 3's skills panel. It's a grid of cells, each with the skill's icon and its level
  shown twice, stacked (current over base; they match until there are boosts). Hover a cell for its xp, the next level and
  a progress bar. The footer shows total level, combat level (OSRS formula, melee only for now) and total xp.
- **H** shows or hides the help, and **F1** opens the dev tools.
- Progress (skills, inventory, bank, worn equipment, attack style, position) saves every 15 s and when you quit.

## Bank

- The bank booth stands just north-east of spawn. Clicking it opens the bank window.
- Each kind of item takes **one bank slot and stacks there**, even items like logs that take a slot each in the
  inventory. The bank has 400 slots. Counts show RuneScape style: yellow below 100K, then K, then M in green.
- While the bank is open, click an inventory item to deposit it, or click a bank slot to withdraw. The **1 / 5 / 10 / All**
  buttons set how many. Shift-click moves all of it, and right-click lists Deposit-1 / 5 / 10 / All (Withdraw-… in the bank). **Deposit inventory** empties your backpack in one go.
  You only get as many items as you have free inventory slots.
- The bank closes with **X**, **Esc**, or when you walk away from the booth. It's saved along with everything else.

## Shop

- The **shop keeper** stands two tiles east of the bank booth. "Trade" opens the **Dev Area General Store**.
- It stocks 5 each of a **Tinderbox** (1 gp), a **Bronze sword** (26 gp) and a **Wooden shield** (20 gp). The stock is in
  `Shops.DevAreaStore()` in `Assets/Scripts/Core/Shop.cs`.
- Click an item to buy it, using the **1 / 5 / 10 / 50** buttons for how many. Shift-click buys 50. Right-click for Value, Buy 1 / 5 / 10 / 50 and Examine (your own items get Value and Sell …).
- While the shop is open, click your own items to sell them. It's a general store, so it buys anything but coins, for 40% of the item's
  value (logs sell for 1 gp). Items you sell show up in its stock.
- Stock drifts back to normal by one every 6 seconds, and items players sold slowly disappear.
- New saves start with **100 coins**. The dev tools can add 1,000 more.
- The sword and shield can be wielded (see below). The tinderbox is just an item for now: there is no firemaking yet.

## Items on the ground

- Dropped items land on your tile and show as small 3D models: logs, a coin pile, a tinderbox, a sword, a shield.
  Coins dropped on the same tile join one pile.
- Click one to walk there and pick it up. In first person, look at it and click. Hovering shows "Take Logs" or "Take Coins (12)".
- They don't block walking, and they disappear after 3 minutes.

## Equipment and combat

- The **Worn** tab is laid out like RuneScape's equipment screen: head; cape, neck and ammo; weapon, body and shield; legs;
  gloves, boots and ring. Click a worn item to take it off (you need a free inventory slot). Below that are
  your attack, strength and defence bonuses, and the **attack style**: which skill your hits train.
- The **Bronze sword** (+4 attack, +5 strength) and **Wooden shield** (+5 defence) are the only equipment so far.
  You need **both** worn to fight.
- The **Goblins** plot, in the middle, has 4 goblins (level 2, 5 hitpoints) that wander around it. They stop and turn to
  face you when attacked but **don't fight back yet**.
- Hits use RuneScape's melee formulas: hit chance from your Attack level and bonus against the goblin's defence, and
  max hit from Strength. With the sword, a hit every 4 ticks (2.4 s). Red hitsplats show damage, blue ones a miss,
  and a health bar shows over a goblin you're fighting.
- Each damage point gives 4 xp in the chosen style's skill plus 1.33 Hitpoints xp. Hitpoints starts at level 10.
- A dead goblin drops **1–30 coins** on the ground (weighted towards small amounts) and respawns after 15 s.
  Its stats and drops are in `NpcDefs.Goblin` in `Assets/Scripts/Core/Combat.cs`.

## Melee abilities (action bar)

The bar above the chat box holds RuneScape 3's nine basic melee abilities. Use them with keys **1–9** or by clicking.
Right-click a slot for Activate and Examine, and hover one for what it does. Like normal attacks, they need a sword and shield worn.

| Key | Ability | Needs | Cooldown | Damage (× max hit) | Effect |
|---|---|---|---|---|---|
| 1 | Slice | Attack 1 | 3 s | 0.3–1.2 | |
| 2 | Smash | Strength 1 | 10 s | 0.4–1.25 | also hits every monster next to the target |
| 3 | Barge | Attack 3 | 20 s | 0.4–1.0 | charge a monster up to 10 tiles away |
| 4 | Havoc | Attack 7 | 10 s | 0.5–1.25 | |
| 5 | Sever | Attack 8 | 15 s | 0.4–1.9 | |
| 6 | Punish | Strength 8 | 3 s | 0.2–1.0 | ×3 against a target below half health |
| 7 | Dismember | Strength 14 | 15 s | 1.0–2.0 | lands as 3 bleed hits, 1.2 s apart |
| 8 | Backhand | Attack 15 | 15 s | 0.2–1.0 | stuns for 3 s |
| 9 | Kick | Strength 15 | 15 s | 0.2–1.0 | stuns for 3 s |

- Using any ability starts a 1.8 s global cooldown for all of them. The slot darkens and fills back up as it recovers,
  with the seconds left on longer cooldowns. Slots you lack the level for are dimmed and show the level needed.
- An ability still rolls to hit like a normal attack. When it hits, it does at least 1. Damage gives the usual xp for your attack style.
- Target: the monster you're fighting, else the one under the mouse (tile mode) or crosshair (first person), else the last one you hit.
  It must be next to you (within reach in first person), except for Barge.
- In tile mode you keep auto-attacking the target afterwards, and the next auto-attack waits a full weapon swing.
- Goblins don't fight back yet, so a stun just stops them in place (they show "Stunned").
- The abilities are defined in `Assets/Scripts/Core/Abilities.cs`.

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

- A skill (pick it with **<** and **>**): set the level (−1, +1, 1, 99, 120, 999) or add 1K / 1M / 1B xp
- Empty the inventory or fill it with logs
- Empty the bank, or put 1,000 logs in it
- Add 1,000 coins, or a sword and shield
- Regrow every tree
- Teleport to spawn or to a skill's plot
- Save now, or reset everything

## Code layout

| Folder | What's there |
|---|---|
| `Assets/Scripts/Core` | Game rules with no Unity dependency: xp curve, skills, items, inventory, bank, shops, equipment, combat formulas, monsters and drop tables, tree definitions and chop chances, tile grid and A* pathfinding |
| `Assets/Scripts/Runtime` | The Unity side: `Game` (startup), `World` and `DevArea` (the map and its plots), `TreeObject`, `Interactable` with `BankBooth` and `ShopKeeper`, `Npc` (monsters), `GroundItem` and `ItemModels`, `PlayerController` (including what's on each right-click menu), `OptionMenu` (both view modes, walking, chopping), `CameraRig`, `Hud` (all the UI, built with IMGUI), `SaveGame` |
| `Assets/Editor` | Creates the scene and build settings, and the Epacenur menu |
| `Tools/Tests` | `run.sh`: tests for the rules in Core (xp table, skills, inventory, bank, shop, equipment, combat, pathfinding) |
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
