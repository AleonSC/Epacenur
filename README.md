# Epacenur

A RuneScape-style skilling game for **Unity 6**. You can play top down, clicking tiles, or in first person.
For now it's a **dev area** with plots for testing skills, a **bank booth** and a **shop**: **Woodcutting** with basic trees,
**Firemaking**, **Fishing** at a pond, **Cooking** on fires, and **goblins** to fight (Attack, Strength, Defence and Hitpoints).

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
- **Panel tabs**: **Inv** (inventory and money pouch), **Worn** (equipment), **Tools** (tool belt), **Pray** (prayers) and **Skills**.
- **Inventory**: 28 slots, one log per slot. Click a sword or shield to wield it. Click any other item and then another
  slot to swap them. Shift-click an item to **drop it on the ground** (or use Drop in its right-click menu).
- **Skills** tab: laid out like RuneScape 3's skills panel. It's a grid of cells, each with the skill's icon and its level
  shown twice, stacked (current over base; they match until there are boosts). Hover a cell for its xp, the next level and
  a progress bar. The footer shows total level, combat level (OSRS formula with melee and Prayer; no Ranged or Magic yet) and total xp.
- **H** shows or hides the help, and **F1** opens the dev tools.
- Progress (skills, inventory, bank, worn equipment, attack style, position) saves every 15 s and when you quit.

## Money pouch

Coins live in a **money pouch** shown under the inventory, as in RuneScape 3, so they never take a backpack slot.

- Coins you get go straight into the pouch: shop sales, coins picked up off the ground, coins withdrawn from the bank, dev tools.
  It holds up to **2,147,483,647**; anything past that lands in the backpack as a stack.
- Shops spend from the pouch first, then from any coins in your backpack.
- Right-click the pouch to **Withdraw-1 / 10 / 100 / 1,000 / All** coins into your backpack as a stack, for example to drop them.
  Click a coin stack in the backpack (or right-click, **Add to pouch**) to put it back.
- At the bank, click the pouch or press **Deposit money pouch** to bank every coin in it. **Deposit inventory** leaves the pouch alone.
- The amount uses RuneScape's colours: yellow below 100K, white below 10M, green above. Left-click it to examine.
- Saves from before the pouch move their coins into it when loaded.

## Tool belt

The **Tools** tab is RuneScape 3's tool belt. Tools on it are used automatically, as if they were in your backpack,
so they don't take up slots.

- Click a tool in your backpack, or right-click it and choose **Add to tool belt**, to move it onto the belt. It stays there for good:
  it can't be taken off or dropped, and a second copy stays in the backpack.
- It can hold the **bronze hatchet** (woodcutting), the **tinderbox** (firemaking) and the **small fishing net** (fishing). The tab shows every tool it can hold,
  with the ones you have in full colour.
- Tools still work from the backpack too. The list of tools is `ToolBelt.Tools` in `Assets/Scripts/Core/ToolBelt.cs`.

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
- It stocks 5 each of a **Tinderbox** (1 gp), a **Bronze sword** (26 gp), a **Wooden shield** (20 gp) a **Small fishing net** (5 gp) and a **Bronze hatchet** (16 gp). The stock is in
  `Shops.DevAreaStore()` in `Assets/Scripts/Core/Shop.cs`.
- Click an item to buy it, using the **1 / 5 / 10 / 50** buttons for how many. Shift-click buys 50. Right-click for Value, Buy 1 / 5 / 10 / 50 and Examine (your own items get Value and Sell …).
- While the shop is open, click your own items to sell them. It's a general store, so it buys anything but coins, for 40% of the item's
  value (logs sell for 1 gp). Items you sell show up in its stock.
- Stock drifts back to normal by one every 6 seconds, and items players sold slowly disappear.
- New saves start with **100 coins**. The dev tools can add 1,000 more.
- The sword and shield can be wielded, the tinderbox lights fires and the net catches fish (see below).

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
- The **Goblins** plot, in the middle, has 4 goblins (level 2, 5 hitpoints) that wander around it. They leave you alone
  until you attack, then **fight back**: they chase you (but not far from their plot) and hit you every 4 ticks. A goblin's
  max hit is 1, and its hit chance is rolled against your Defence level and bonus, Defence prayers and attack style
  (Defensive adds 3), using OSRS's formulas. They give up if you run more than 12 tiles away or leave the area round their plot.
- **Auto retaliate**: if a goblin hits you while you're standing idle in tile mode with a sword and shield worn, you fight back.
- Hits use RuneScape's melee formulas: hit chance from your Attack level and bonus against the goblin's defence, and
  max hit from Strength. With the sword, a hit every 4 ticks (2.4 s). Red hitsplats show damage, blue ones a miss,
  and a health bar shows over a goblin you're fighting.
- Each damage point gives 4 xp in the chosen style's skill plus 1.33 Hitpoints xp. Hitpoints starts at level 10.
- A dead goblin always drops **bones**, plus **1–30 coins** (weighted towards small amounts), and respawns after 15 s.
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
- A stunned goblin stops moving and attacking for 3 s (it shows "Stunned").
- The abilities are defined in `Assets/Scripts/Core/Abilities.cs`.

## Firemaking

- You need a **tinderbox** (the shop sells them). Click logs in your inventory to light them, or right-click them and choose **Light**.
  They go down on your tile and you kneel to light them. Logs lying on the ground can be lit too (right-click, **Light**):
  you walk onto their tile first.
- Each attempt (every 3 ticks) has a chance to catch: about 1 in 4 at level 1, certain from level 43. Moving stops you.
- A lit fire gives **40 xp**. It flickers, glows, blocks its tile and burns for 1–2 minutes, then leaves **ashes** on the ground.
  You step off it to the west (or another free side) in tile mode.
- Fires can't be lit on a tile that already has one. The **Firemaking** plot, south-west of spawn, is open ground for it, but any free tile works.
- Rules are in `Assets/Scripts/Core/Firemaking.cs`. Fires are `Assets/Scripts/Runtime/Fire.cs`.

## Fishing

- The **Fishing** plot has a pond with three rippling **fishing spots** along its edge. Spots move to another place
  on the shore every 40–90 s, which stops you fishing ("The fishing spot has moved.").
- You need a **small fishing net** (5 gp from the shop). Click a spot (**Net**) to walk up to it and fish automatically, one attempt every 5 ticks,
  until your inventory is full. In first person each click is one cast.
- Each attempt tries the best fish you have the level for first: **raw anchovies** (level 15, 40 xp), then **raw shrimps** (level 1, 10 xp).
  Cook them on a fire (see Cooking).
- Rules are in `Assets/Scripts/Core/Fishing.cs`. Spots are `Assets/Scripts/Runtime/FishingSpot.cs`.

## Cooking

- Cook raw fish on a fire. Click a fire (**Cook Fire**), or click raw fish in your inventory (or right-click, **Cook**), which walks you to the nearest fire.
  In tile mode you cook everything raw you're carrying, one item every 4 ticks, starting with the kind you clicked.
  In first person, stand at the fire and each click cooks one.
- Each item either cooks (**Shrimps** or **Anchovies**, 30 xp) or burns (**Burnt shrimp** or **Burnt fish**, no xp), in its own inventory slot.
  At level 1 about a third cook. Both stop burning at **level 34**, as in RuneScape.
- Cooking stops if the fire goes out or you move away.
- Rules are in `Assets/Scripts/Core/Cooking.cs`.

## Health and eating

- A **health bar** sits above the action bar (red, with the numbers on it, turning orange then bright red as it runs low). In tile mode
  a small bar also shows over your head while you're hurt, with hitsplats when you're hit.
- Max health is your **Hitpoints level** (10 to start), as in Old School RuneScape. Gaining a Hitpoints level adds to your current health too.
  Lost health comes back **1 point a minute**.
- **Eat** cooked food by clicking it in your inventory (or right-click, **Eat**): **shrimps heal 3**, **anchovies heal 1**. Burnt food can't be eaten.
  A bite takes 3 ticks and holds back your next attack by as much.
- Goblins hurt you when you fight them. The dev tools also have **Hurt me 3**, **Heal fully** and **5 shrimps** for testing.
  At 0 health you die ("Oh dear, you are dead!") and wake up at spawn with full health, keeping your items.
- Health is saved. Rules are in `Assets/Scripts/Core/Health.cs`.

## Prayer

- **Train it** by burying bones (goblins always drop them): click them in your inventory, or right-click and choose **Bury**.
  Each gives **4.5 Prayer xp**, one every 2 ticks.
- **Prayer points** equal your Prayer level. They show in the blue bar beside the health bar; click that bar to turn every prayer off.
  Recharge them at the **altar** next to the bank (**Pray-at Altar**, two tiles west of the bank booth). You kneel to pray.
- The **Pray** tab holds Old School RuneScape's first melee prayers. Click one to turn it on or off. Prayers of the same kind replace each other.

| Prayer | Level | Effect | Drains a point every |
|---|---|---|---|
| Thick Skin | 1 | +5% Defence | 36 s |
| Burst of Strength | 4 | +5% Strength | 36 s |
| Clarity of Thought | 7 | +5% Attack | 36 s |
| Rock Skin | 10 | +10% Defence | 6 s |
| Superhuman Strength | 13 | +10% Strength | 6 s |
| Improved Reflexes | 16 | +10% Attack | 6 s |
| Protect from Melee | 43 | blocks monsters' melee hits completely | 3 s |

- Boosts apply to your effective levels in the combat formulas, abilities included. Protect from Melee shows its symbol over
  your head (top of the view in first person).
- Drain works like OSRS: each tick the active prayers' drain effects add up, and every 60 costs a point. At 0 every prayer switches off.
- Prayer counts towards your combat level (half of it, in the Defence and Hitpoints part). Dying recharges your points. Points are saved.
- Rules are in `Assets/Scripts/Core/Prayer.cs`. The altar is `Assets/Scripts/Runtime/Altar.cs`.

## The player model

Built from primitive shapes like everything else: hair with a fringe, eyes, brows, nose, mouth and ears; steel shoulder pads;
a blue tunic with gold trim and a flared skirt; a belt with a gold buckle and pouch; trousers; cuffed boots; and a red cape with a gold hem.
It has a walk cycle (legs, arms and a bobbing step) and idle breathing, and the cape flies back as you move. You kneel to light fires,
hold the net out to fish, and carry your sword, shield or net as needed. The axe only appears while you chop (in first person, when you aim at a tree).

## Woodcutting

- A chop succeeds RuneScape style: the chance rises in a straight line from 25% at level 1 to 79% at level 99
  for a basic tree, and keeps rising past 99 until it is certain (around level 140).
- Each success gives **one log in its own inventory slot** and 25 xp, and takes one of the tree's
  **lives** (3–6, rolled each time it grows). With no lives left it becomes a stump, then grows back after 12 s.
- Tile mode makes a chop attempt every 4 ticks (2.4 s) while you stand next to the tree. In first person each
  click is one attempt, at most one every 2 ticks (1.2 s). The crosshair turns green when a tree is in reach and your
  swing is ready, and yellow while you recover. These timings are in `WoodcuttingRules` in `Assets/Scripts/Core/Woodcutting.cs`.
- You need a **Bronze hatchet**, in your backpack or on your tool belt. New characters start with one on the belt,
  older saves are given one, and the shop sells them (16 gp). Without one: "You need a hatchet to chop down this tree."
- You can't chop with a full inventory, and you stop when it fills up.

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
- Add 1,000 coins, a sword and shield, or the tools (hatchet, tinderbox, net) to your backpack, or fill the tool belt
- Regrow every tree
- Teleport to spawn or to a skill's plot
- Save now, or reset everything

## Code layout

| Folder | What's there |
|---|---|
| `Assets/Scripts/Core` | Game rules with no Unity dependency: xp curve, skills, items, inventory, bank, shops, equipment, combat formulas, monsters and drop tables, tree definitions and chop chances, tile grid and A* pathfinding |
| `Assets/Scripts/Runtime` | The Unity side: `Game` (startup), `World` and `DevArea` (the map and its plots), `TreeObject`, `Interactable` with `BankBooth` and `ShopKeeper`, `Npc` (monsters), `Fire`, `FishingSpot`, `GroundItem` and `ItemModels`, `PlayerController` (the model, walking, woodcutting, combat and what's on each right-click menu) and `PlayerSkilling` (firemaking and fishing), `OptionMenu` (both view modes, walking, chopping), `CameraRig`, `Hud` (all the UI, built with IMGUI), `SaveGame` |
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
