namespace Epacenur.Core
{
    /// A kind of rock. To add iron, coal...: add a RockDef here and place it in a skill area.
    public sealed class RockDef
    {
        public string Name;
        public int OreItemId;
        public int Level;
        public double Xp;
        public int LowChance, HighChance;  // chance of ore per swing, out of 256, at level 1 and 99
        public float RespawnSeconds;       // empty this long after giving its ore
        public float OreR, OreG, OreB;     // the coloured streaks in the rock
    }

    public static class MiningRules
    {
        /// Ticks between swings (tile mode); first person is one swing per click.
        public const int AttemptTicks = 4;
        public const int FirstPersonSwingTicks = 2;

        /// Pickaxes that can mine (bronze only so far).
        public static readonly ItemDef[] Pickaxes = { ItemDefs.BronzePickaxe };

        // Copper and tin, as in OSRS: level 1, 17.5 xp, one ore each and then empty for a moment.
        public static readonly RockDef Copper = new RockDef
        {
            Name = "Copper rocks", OreItemId = ItemDefs.CopperOre.Id, Level = 1, Xp = 17.5, LowChance = 100, HighChance = 350,
            RespawnSeconds = 2.4f, OreR = 0.85f, OreG = 0.45f, OreB = 0.2f,
        };

        public static readonly RockDef Tin = new RockDef
        {
            Name = "Tin rocks", OreItemId = ItemDefs.TinOre.Id, Level = 1, Xp = 17.5, LowChance = 100, HighChance = 350,
            RespawnSeconds = 2.4f, OreR = 0.82f, OreG = 0.82f, OreB = 0.8f,
        };

        public static double SuccessChance(RockDef rock, int level) => GatherRules.Chance(rock.LowChance, rock.HighChance, level);
    }
}
