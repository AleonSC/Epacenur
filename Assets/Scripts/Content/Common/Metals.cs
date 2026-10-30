using System;
using Epacenur.Core;

namespace Epacenur.Content
{
    /// One metal's place in the tiers: the levels to smith, wear and use it, and how much stronger and
    /// pricier its things are than bronze's. Every metal gets the same set of weapons, armour and tools
    /// (SmithingModule, WoodcuttingModule, MiningModule), with ids like "iron_scimitar".
    public sealed class Metal
    {
        public string Id, Name;
        public Rgb Colour;
        public int Tier;            // tools: 1 bronze .. 5 adamant (gather faster)
        public int SmithLevel;      // to smith its daggers; other items need a few levels more
        public double XpPerBar;     // smithing xp for each bar used
        public int WearLevel;       // Attack to wield its weapons, Defence to wear its armour
        public int ToolLevel;       // Woodcutting / Mining to use its hatchet / pickaxe
        public double StatFactor;   // bonuses compared with bronze
        public double ValueFactor;  // value compared with bronze

        public string ItemId(string thing) => Id + "_" + thing;
        public string Bar => Id + "_bar";

        /// A bronze bonus scaled to this metal (penalties stay as they are).
        public int Stat(int bronze) => bronze <= 0 ? bronze : (int)Math.Round(bronze * StatFactor);
        public int Value(int bronze) => (int)Math.Round(bronze * ValueFactor);

        public Bonuses Scale(Bonuses b) => new Bonuses
        {
            StabAttack = Stat(b.StabAttack), SlashAttack = Stat(b.SlashAttack), CrushAttack = Stat(b.CrushAttack), RangedAttack = Stat(b.RangedAttack),
            StabDefence = Stat(b.StabDefence), SlashDefence = Stat(b.SlashDefence), CrushDefence = Stat(b.CrushDefence), RangedDefence = Stat(b.RangedDefence),
            Strength = Stat(b.Strength), RangedStrength = Stat(b.RangedStrength),
        };
    }

    /// The metals, weakest first (OSRS levels and xp).
    public static class Metals
    {
        public static readonly Metal Bronze = new Metal { Id = "bronze", Name = "Bronze", Colour = new Rgb(0.72f, 0.5f, 0.28f), Tier = 1,
            SmithLevel = 1, XpPerBar = 12.5, WearLevel = 1, ToolLevel = 1, StatFactor = 1.0, ValueFactor = 1 };
        public static readonly Metal Iron = new Metal { Id = "iron", Name = "Iron", Colour = new Rgb(0.45f, 0.44f, 0.46f), Tier = 2,
            SmithLevel = 15, XpPerBar = 25, WearLevel = 1, ToolLevel = 1, StatFactor = 1.45, ValueFactor = 3.5 };
        public static readonly Metal Steel = new Metal { Id = "steel", Name = "Steel", Colour = new Rgb(0.68f, 0.7f, 0.74f), Tier = 3,
            SmithLevel = 30, XpPerBar = 37.5, WearLevel = 5, ToolLevel = 6, StatFactor = 2.4, ValueFactor = 12.5 };
        public static readonly Metal Mithril = new Metal { Id = "mithril", Name = "Mithril", Colour = new Rgb(0.33f, 0.38f, 0.62f), Tier = 4,
            SmithLevel = 50, XpPerBar = 50, WearLevel = 20, ToolLevel = 21, StatFactor = 3.3, ValueFactor = 32 };
        public static readonly Metal Adamant = new Metal { Id = "adamant", Name = "Adamant", Colour = new Rgb(0.33f, 0.52f, 0.38f), Tier = 5,
            SmithLevel = 70, XpPerBar = 62.5, WearLevel = 30, ToolLevel = 31, StatFactor = 4.6, ValueFactor = 80 };

        public static readonly Metal[] All = { Bronze, Iron, Steel, Mithril, Adamant };
    }
}
