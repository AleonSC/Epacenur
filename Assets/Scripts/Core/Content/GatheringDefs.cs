using System.Collections.Generic;

namespace Epacenur.Core
{
    // ---- Hunter ----

    /// A creature caught in a trap: which trap, the level and xp, the biomes it lives in, and what it leaves.
    public sealed class CreatureDef
    {
        public string Id, Name;
        public string Trap;                   // the trap kind that catches it: "snare" (birds), "box" (animals)
        public int Level;
        public double Xp;
        public int LowChance, HighChance;     // chance (out of 256) a laid trap catches it, at level 1 and 99
        public readonly List<Biome> Biomes = new List<Biome>();
        public readonly List<Ingredient> Loot = new List<Ingredient>();  // always, each catch
        public int ExtraLootMax;              // up to this many more of the first loot item (feathers)
        public Model Model;                   // shown in the trap once caught (small, on y = 0)
    }

    // ---- Farming ----

    public enum PatchKind { Herb, Allotment }

    /// What a seed grows into: where it's planted, the level, xp for planting and for each harvest, how long
    /// it takes and how much it gives.
    public sealed class SeedDef
    {
        public string SeedId, Name;           // "Guam seed", grows "Guam"
        public PatchKind Patch;
        public int Level;
        public double PlantXp, HarvestXp;
        public string Product;
        public int MinYield, MaxYield;
        public int GrowMinutes;               // real minutes, planted to ready (it keeps growing while you're away)
        public Rgb Colour;                    // the plant's colour
    }
}
