using System;
using System.Collections.Generic;

namespace Epacenur.Core
{
    // Everything in the game is described by these definitions, registered by content modules
    // (Assets/Scripts/Content) into the ContentDb. They are plain data: ids are text, like "bronze_bar".

    /// Where an item is worn (the slots of the equipment tab).
    public enum EquipSlot
    {
        None = -1,
        Head, Cape, Neck, Ammo, Weapon, Body, Shield, Legs, Hands, Feet, Ring,
    }

    /// The parts of the player model that worn items attach to.
    ///   Body: the model's own space (feet at y = 0, facing +z).
    ///   LegLeft, LegRight, ArmLeft, ArmRight: the limb's pivot (hip or shoulder), hanging down -y.
    ///   Hand: the right hand, pointing forward (+z) from the grip: weapons and tools.
    ///   ShieldArm: the outside of the left forearm, facing out (-x): shields.
    public enum Bone { Body, LegLeft, LegRight, ArmLeft, ArmRight, Hand, ShieldArm }

    public struct WornPart
    {
        public Bone Bone;
        public Model Model;
    }

    /// The verbs the game knows for items in the inventory. Most are worked out from the item
    /// (anything with a Slot can be worn, anything with Heals eaten...); ItemDef.Verbs adds others.
    public static class Verbs
    {
        public const string Wield = "Wield", Wear = "Wear", Eat = "Eat", Bury = "Bury", Light = "Light", Cook = "Cook",
            AddToToolBelt = "Add to tool belt", AddToPouch = "Add to pouch", Use = "Use", Drop = "Drop", Examine = "Examine",
            Drink = "Drink";
    }

    /// How a melee attack lands. Armour defends against each differently, as in RuneScape.
    public enum AttackType { Stab, Slash, Crush, Ranged, Magic }

    /// An item's (or a monster's) combat bonuses: accuracy and defence against each attack type, and
    /// melee strength (max hit). Worn items' bonuses add up.
    public struct Bonuses
    {
        public int StabAttack, SlashAttack, CrushAttack, RangedAttack, MagicAttack;
        public int StabDefence, SlashDefence, CrushDefence, RangedDefence, MagicDefence;
        public int Strength;        // melee: max hit
        public int RangedStrength;  // arrows: max hit with a bow

        public int Attack(AttackType t) => t == AttackType.Stab ? StabAttack : t == AttackType.Slash ? SlashAttack : t == AttackType.Crush ? CrushAttack
            : t == AttackType.Ranged ? RangedAttack : MagicAttack;
        public int Defence(AttackType t) => t == AttackType.Stab ? StabDefence : t == AttackType.Slash ? SlashDefence : t == AttackType.Crush ? CrushDefence
            : t == AttackType.Ranged ? RangedDefence : MagicDefence;

        /// The same defence against every attack type (most armour is close to even).
        public static Bonuses AllDefence(int d) => new Bonuses { StabDefence = d, SlashDefence = d, CrushDefence = d, RangedDefence = d, MagicDefence = d };

        public static Bonuses operator +(Bonuses a, Bonuses b) => new Bonuses
        {
            StabAttack = a.StabAttack + b.StabAttack, SlashAttack = a.SlashAttack + b.SlashAttack, CrushAttack = a.CrushAttack + b.CrushAttack,
            RangedAttack = a.RangedAttack + b.RangedAttack, MagicAttack = a.MagicAttack + b.MagicAttack,
            StabDefence = a.StabDefence + b.StabDefence, SlashDefence = a.SlashDefence + b.SlashDefence, CrushDefence = a.CrushDefence + b.CrushDefence,
            RangedDefence = a.RangedDefence + b.RangedDefence, MagicDefence = a.MagicDefence + b.MagicDefence,
            Strength = a.Strength + b.Strength, RangedStrength = a.RangedStrength + b.RangedStrength,
        };
    }

    /// One way to fight with a kind of weapon, e.g. a sword's "Slash": the attack type it uses and the
    /// skill it trains (CombatStyle).
    public sealed class WeaponStyle
    {
        public string Name;
        public AttackType Type;
        public CombatStyle Style;
        public WeaponStyle(string name, AttackType type, CombatStyle style) { Name = name; Type = type; Style = style; }
    }

    /// A kind of weapon (sword, mace, dagger, shortbow... or "unarmed"): the attack styles it offers.
    public sealed class WeaponKindDef
    {
        public string Id, Name;
        public WeaponStyle[] Styles;
        public bool Ranged;  // bows: fire ammo from a distance (ItemDef.Range) and train Ranged
    }

    /// A level needed for something, e.g. Attack 20 to wield a mithril sword.
    public struct SkillReq
    {
        public string Skill;
        public int Level;
        public SkillReq(string skill, int level) { Skill = skill; Level = level; }
    }

    /// Where the overworld puts a node (trees and rocks on their terrain, fishing spots on ponds), by
    /// distance from the start: within the range, nodes are picked by weight. Further out, better things grow.
    public sealed class SpawnRule
    {
        public Terrain On;                    // Tree, Rock, Flax (plants) or Water (fishing spots)
        public int MinDistance, MaxDistance = int.MaxValue;  // tiles from the start
        public double Weight = 1;
    }

    /// Where the overworld puts a monster: in which kind of region, how far out, how many together.
    public sealed class NpcSpawnRule
    {
        public RegionKind Region;
        public int MinDistance, MaxDistance = int.MaxValue;
        public double Weight = 1;
        public int MinGroup = 3, MaxGroup = 5;
        public bool Resident;  // towns: one of its people (towns get their animals and two groups of people)
    }

    public sealed class ItemDef
    {
        public string Id, Name, Examine;
        public int Value;            // in coins: shops sell at this price and buy back for less
        public bool Stackable;       // stackable items share one slot; everything else takes a slot each
        public Rgb Colour;

        public Icon Icon;            // in the inventory and every other slot (Size 40)
        public Model GroundModel;    // lying on the ground, about half a tile across, resting on y = 0
        public Model HeldModel;      // in the hand (weapons, tools, pointing +z from the grip) or on the arm (shields)

        // Equipment
        public EquipSlot Slot = EquipSlot.None;
        public Bonuses Bonuses;
        public string WeaponKind;    // weapons: "sword", "mace"... (their attack styles)
        public int AttackTicks = 4;  // weapons: game ticks between attacks (a dagger 4, a two-handed sword 7)
        public bool TwoHanded;       // weapons: can't be used with a shield
        public int Range;            // ranged weapons: how far they shoot, in tiles
        public string AmmoType;      // ranged weapons: what they fire ("arrow"); ammo: what it is
        public int AmmoTier;         // bows: the best arrows they can fire; arrows: their tier
        public string InfiniteRune;  // staffs: the rune they stand in for, as many as you need (staff of air: air runes)
        public readonly List<WornPart> Worn = new List<WornPart>();  // armour: what shows on the body

        // Uses
        public string ToolType;      // "hatchet", "pickaxe", "hammer"... (can go on the tool belt)
        public int ToolTier;         // better tools of a type have higher tiers (and gather faster)
        /// Levels needed to wear it (weapons, armour) or use it as a tool (hatchets, pickaxes).
        public readonly List<SkillReq> Requires = new List<SkillReq>();
        public int Heals;            // food: hitpoints healed when eaten
        public PotionDef Potion;     // potions: what a dose does when drunk
        public string Process;       // an in-hand recipe its own verb does (ProcessVerb), e.g. cleaning a grimy herb
        public string ProcessVerb;   // "Clean"
        public double BuryXp;        // bones: Prayer xp when buried

        /// Extra things you can do with it from the inventory, beyond what the game works out itself
        /// (Wield/Wear, Eat, Bury, Light, Cook, Add to tool belt, Add to pouch, Drop, Examine). Each needs a
        /// handler registered in the game (Runtime: ItemVerbs).
        public readonly List<string> Verbs = new List<string>();

        public bool IsTool => ToolType != null;
    }

    public sealed class SkillDef
    {
        public string Id, Name;
        public Rgb Colour;           // its xp drops
        public int StartLevel = 1;   // Hitpoints starts at 10, as in RuneScape
        public Icon Icon;            // Size 22
    }

    // ---- Gathering: trees, rocks, fishing spots ----

    /// One thing a node can give, e.g. a fishing spot gives anchovies from level 15 and shrimps from level 1.
    public sealed class NodeYield
    {
        public string ItemId;
        public int Level;
        public double Xp;
        public int LowChance, HighChance;  // chance per attempt, out of 256, at level 1 and level 99 (it keeps rising past 99)
        public string Message;             // "You get some logs."
    }

    /// How a node looks and behaves beyond its models (the game adds the extras).
    public enum NodeLook { Tree, Rock, FishingSpot, Plant, Stall }

    /// Something you gather from: a tree, a rock, a fishing spot.
    public sealed class NodeDef
    {
        public string Id, Name, Examine, Action;     // Action: the menu verb, "Chop down", "Mine", "Net"
        public string EmptyName, EmptyExamine;       // once used up: "Tree stump", "Rocks"
        public string Skill, ToolType;               // the skill it trains and the tool it needs
        public NodeYield[] Yields;                   // best first: each attempt tries the best one you have the level for first
        public int AttemptTicks = 4;                 // tile mode: an attempt every this many ticks
        public int FirstPersonTicks = 2;             // first person: one attempt per click, no faster than this
        public int MinLives = 1, MaxLives = 1;       // successes before it's used up (0: never, e.g. fishing spots)
        public float RespawnSeconds;                 // how long it stays used up
        public float MoveMinSeconds, MoveMaxSeconds; // fishing spots move along their shore every so often (0: never)
        public string BaitItemId;                    // used up with each catch (feathers for fly fishing), or null
        public readonly List<SpawnRule> Spawns = new List<SpawnRule>();  // where the overworld grows it (none: placed by hand)
        public NodeLook Look;
        public Model Model, EmptyModel;
        public bool Blocks = true;                   // stands on its tile (trees, rocks); fishing spots sit on water instead
        public string StartMessage;                  // "You swing your axe at the tree."
        public string NeedToolMessage;               // "You need a hatchet to chop down this tree."
        public string FullMessage;                   // "Your inventory is too full to hold any more logs."
        public string LevelMessage;                  // "You need a Woodcutting level of {0} to chop down this tree."
        public string EmptyMessage;                  // trying it while used up: "There is currently no ore available in this rock."
                                                     // (null: a used-up node can only be examined, like a stump)

        public int MinLevel
        {
            get
            {
                int min = int.MaxValue;
                foreach (var y in Yields)
                    min = Math.Min(min, y.Level);
                return min;
            }
        }
    }

    // ---- Making: smelting, smithing, cooking ----

    public struct Ingredient
    {
        public string ItemId;
        public int Count;
        public Ingredient(string itemId, int count) { ItemId = itemId; Count = count; }
    }

    /// Something made from other items at a station (furnace, anvil, fire), or in your hands from the inventory
    /// (Station = Making.InHand: a knife on logs, a bowstring on a bow).
    public sealed class RecipeDef
    {
        public string Id, Name;          // Name: what's made, "Bronze platebody"
        public string Station;           // where: "furnace", "anvil", "fire"
        public string Skill;
        public int Level;
        public double Xp;
        public Ingredient[] Inputs;
        public string Product;
        public string ToolType;          // needed on top of the inputs, e.g. "hammer" at an anvil
        public string FailProduct;       // can fail (cooking burns): what you get instead, and no xp
        public int LowChance, HighChance;// success chance out of 256 at level 1 and 99 when it can fail
        public int Ticks = 4;
        public int ProductCount = 1;     // made each time, e.g. 15 arrow shafts from a log
        public int Batch = 1;            // made at once (and the xp times this), as many as you can up to this: 15 arrows
        public string SuccessMessage;    // "You successfully cook some shrimps."
        public string FailMessage;       // "You accidentally burn the shrimps."
    }

    /// What a station does when used.
    public enum StationKind
    {
        Make,         // opens the make window: choose what and how many (furnace, anvil, fire)
        Bank,         // opens the bank
        Altar,        // recharges Prayer points
        Craft,        // makes its one recipe straight away, as many as you can (runecrafting altars)
    }

    /// Where the overworld puts a station in a shrine region (runecrafting altars), by distance from the start.
    public sealed class StationSpawnRule
    {
        public int MinDistance, MaxDistance = int.MaxValue;
        public double Weight = 1;
        public bool Starter;  // the shrine just east of the start always has this one
    }

    /// A fixed object you use: bank booth, altar, furnace, anvil (fires are stations too, lit by players).
    public sealed class StationDef
    {
        public string Id, Name, Action, Examine;  // Action: the menu verb, "Smelt", "Smith", "Bank", "Pray-at"
        public StationKind Kind;
        public Model Model;                       // faces -z (its front)
        public bool HasLight;                     // casts a warm glow (furnace)
        public Rgb LightColour;
        public string Label;                      // the name floating over it, e.g. "Bank" (null: none)
        public float LabelHeight = 2f;            // how high it floats
        public string Help;                       // Make: a hint at the bottom of the make window
        public readonly List<StationSpawnRule> Spawns = new List<StationSpawnRule>();  // shrines the overworld builds round it
    }

    // ---- Herblore ----

    /// A skill raised above its level for a while: flat + percent of the level (OSRS: an attack potion is 3 + 10%).
    public struct SkillBoost
    {
        public string Skill;
        public int Flat;
        public double Percent;
        public SkillBoost(string skill, int flat, double percent) { Skill = skill; Flat = flat; Percent = percent; }
    }

    /// One dose of a potion: what drinking it does, and what's left after (the next dose down, or a vial).
    public sealed class PotionDef
    {
        public readonly List<SkillBoost> Boosts = new List<SkillBoost>();
        public int PrayerFlat;           // prayer points restored: flat + percent of the Prayer level
        public double PrayerPercent;
        public double Energy;            // run energy restored (percent)
        public string Leaves;            // the item left behind: the potion with a dose less, or an empty vial
        public string Message;           // "You drink some of your attack potion."
    }

    // ---- Agility ----

    /// How an obstacle looks (the game builds it to the length of its crossing).
    public enum ObstacleLook { Log, Wall, Rope, Pipe, Stones, Net }

    /// One obstacle of an agility course.
    public sealed class ObstacleDef
    {
        public string Name, Action, Examine;  // "Log balance", "Walk-across"
        public ObstacleLook Look;
        public int Level;
        public double Xp;
        public int LowChance, HighChance;     // success out of 256 at level 1 and 99 (256: never fails)
        public int FailDamage;                // up to this many hitpoints lost when you fall
        public string Success, Fail;          // "You walk carefully across the slippery log...", "You slip and fall!"
    }

    /// An agility course: five obstacles round a loop (a course region), and a bonus for each full lap.
    public sealed class CourseDef
    {
        public string Id, Name;
        public int Level;                     // the obstacles' lowest level, shown on the map
        public double LapXp;                  // for crossing all five in order
        public ObstacleDef[] Obstacles = new ObstacleDef[0];
        public readonly List<StationSpawnRule> Spawns = new List<StationSpawnRule>();  // where courses are, by distance
    }

    // ---- Thieving ----

    /// Picking someone's pocket: the level and xp, the chance, what you can get, and how hard they hit back.
    public sealed class PickpocketDef
    {
        public int Level;
        public double Xp;
        public int LowChance, HighChance;     // success out of 256 at level 1 and 99
        public DropTable Loot = new DropTable();
        public int StunDamage;                // up to this many hitpoints when caught
    }

    // ---- Magic ----

    public enum SpellKind
    {
        Combat,        // autocast at monsters
        HomeTeleport,  // to the start, after a long cast you can't move during
        TownTeleport,  // to the nearest town
        Alchemy,       // turns an item into coins (Value is the share of its value)
    }

    /// A spell: its level, runes, xp and what it does.
    public sealed class SpellDef
    {
        public string Id, Name, Description;
        public SpellKind Kind;
        public int Level;
        public double Xp;               // given for casting it (combat spells: also 2 per damage)
        public Ingredient[] Runes = new Ingredient[0];
        public int MaxHit;              // combat spells
        public double Value;            // alchemy: share of the item's value paid in coins
        public int CastTicks;           // home teleport: how long the cast takes
        public Rgb Colour;              // its projectile and icon
        public Icon Icon;               // Size 22
    }

    // ---- Shops ----

    public struct ShopStock
    {
        public string ItemId;
        public int Count;
        public ShopStock(string itemId, int count) { ItemId = itemId; Count = count; }
    }

    public sealed class ShopDef
    {
        public string Id, Name;
        public string KeeperName = "Shop keeper";
        public string KeeperExamine = "I can buy things from him.";
        public ShopStock[] Stock;
        public bool BuysAnything = true;   // a general store buys anything (but coins); a specialist only its own stock
        public double SellRate = 0.4;      // what it pays, as a share of the item's value
    }

    // ---- Monsters ----

    public sealed class DropEntry
    {
        public string ItemId;
        public int Min, Max, Weight;
    }

    /// A monster's possible drops. One entry is rolled by weight each kill; an entry with no item is "nothing".
    public sealed class DropTable
    {
        public readonly List<DropEntry> Entries = new List<DropEntry>();

        public DropTable Add(string itemId, int min, int max, int weight)
        {
            Entries.Add(new DropEntry { ItemId = itemId, Min = min, Max = max, Weight = weight });
            return this;
        }

        /// The dropped item and amount, or false for no drop.
        public bool Roll(Random rng, out string itemId, out int count)
        {
            itemId = null;
            count = 0;
            int total = 0;
            foreach (var e in Entries)
                total += e.Weight;
            if (total <= 0)
                return false;
            int pick = rng.Next(total);
            foreach (var e in Entries)
            {
                if (pick < e.Weight)
                {
                    itemId = e.ItemId;
                    count = rng.Next(e.Min, e.Max + 1);
                    return itemId != null;
                }
                pick -= e.Weight;
            }
            return false;
        }
    }

    public sealed class NpcDef
    {
        public string Id, Name, Examine;
        public int CombatLevel, Hitpoints;
        public int DefenceLevel;
        public Bonuses Bonuses;                       // its defence against each attack type
        public int AttackLevel, AttackBonus, MaxHit;  // fighting back
        public AttackType AttackType = AttackType.Crush;  // what your armour defends against
        public int AttackTicks = 4;
        public int AttackRange = 1;                   // tiles it attacks from: 1 is next to you; more casts from afar
        public float RespawnSeconds;
        public float WalkSpeed = 1.6f;                // tiles per second
        public DropTable Drops = new DropTable();
        public readonly List<string> AlwaysDrops = new List<string>();  // every kill, e.g. bones
        public Model Model;                           // feet at y = 0, facing +z
        public float Height = 1.5f;                   // for its health bar and click box
        public bool Aggressive;                       // attacks players nearby whose combat level is up to twice its own
        public PickpocketDef Pickpocket;              // townsfolk: can have their pockets picked
        public readonly List<NpcSpawnRule> Spawns = new List<NpcSpawnRule>();  // where the overworld puts it
    }

    // ---- Firemaking ----

    /// Logs you can burn.
    public sealed class BurnableDef
    {
        public string LogItemId;
        public int Level;
        public double Xp;
        public int LowChance, HighChance;  // chance to light per attempt, out of 256, at level 1 and 99
        public float MinBurnSeconds, MaxBurnSeconds;
    }
}
