using System;
using System.Collections.Generic;

namespace Epacenur.Core
{
    /// A tier of mine: what ore its rocks hold (the first is the most common).
    public sealed class MineTier
    {
        public int Tier;
        public string Name;          // "copper and tin"
        public string[] Rocks;       // node ids
        public double[] Shares;      // how many of the rocks are each
    }

    /// Mines, down the shafts that stand where quarries used to be dug. Each mine has a tier, picked when the
    /// world is made but only found out when you climb down: tier 1 copper, tin and limestone, 2 iron, 3 coal, 4 mithril,
    /// 5 adamantite. Deeper in the wilderness the better tiers are likelier; even near town you might strike lucky.
    /// Underground it's a few chambers joined by tunnels, with ore rocks all through them.
    public static class Mines
    {
        public static readonly MineTier[] Tiers =
        {
            new MineTier { Tier = 1, Name = "copper, tin and limestone", Rocks = new[] { "copper_rock", "tin_rock", "limestone_rock" }, Shares = new[] { 0.4, 0.4, 0.2 } },
            new MineTier { Tier = 2, Name = "iron (and some copper, tin and limestone)", Rocks = new[] { "iron_rock", "copper_rock", "tin_rock", "limestone_rock" }, Shares = new[] { 0.55, 0.15, 0.15, 0.15 } },
            new MineTier { Tier = 3, Name = "coal and iron", Rocks = new[] { "coal_rock", "iron_rock" }, Shares = new[] { 0.6, 0.4 } },
            new MineTier { Tier = 4, Name = "mithril and coal", Rocks = new[] { "mithril_rock", "coal_rock" }, Shares = new[] { 0.45, 0.55 } },
            new MineTier { Tier = 5, Name = "adamantite and mithril", Rocks = new[] { "adamantite_rock", "mithril_rock", "coal_rock" }, Shares = new[] { 0.4, 0.3, 0.3 } },
        };

        /// How likely each tier is at a wilderness level: centred on 1 + danger / 10, falling away either side.
        public static double[] Chances(int danger)
        {
            double mean = Math.Min(5, 1 + danger / 10.0);
            var w = new double[Tiers.Length];
            double total = 0;
            for (int i = 0; i < w.Length; i++)
            {
                double d = (i + 1) - mean;
                w[i] = Math.Exp(-d * d / 1.6);
                total += w[i];
            }
            for (int i = 0; i < w.Length; i++)
                w[i] /= total;
            return w;
        }

        /// A mine's tier (the starter mine, just north of the start, is always tier 1).
        public static MineTier TierOf(Overworld ow, RegionPlan plan)
        {
            if (plan.RX == 0 && plan.RZ == 1)
                return Tiers[0];
            var w = Chances(ow.Danger(plan.CaveMouth));
            double roll = ow.Hash(plan.RX, plan.RZ, 63);
            for (int i = 0; i < w.Length; i++)
            {
                if (roll < w[i])
                    return Tiers[i];
                roll -= w[i];
            }
            return Tiers[Tiers.Length - 1];
        }

        /// The mine's layout: chambers and tunnels (as a dungeon's, smaller), and its ore rocks. Rocks stand on a
        /// loose lattice, three apart, only where every tile round them is floor, so they never block the way.
        public static DungeonLayout Generate(int seed, MineTier tier)
        {
            var d = Dungeons.Generate(seed, 3);
            d.Tier = tier.Tier;
            var rng = new Random(seed ^ 0x5A5A5A);
            for (int x = 1; x < d.Width - 1; x++)
                for (int z = 1; z < d.Height - 1; z++)
                {
                    if (x % 3 != 1 || z % 3 != 1)
                        continue;
                    var t = new Tile(x, z);
                    if (Math.Abs(t.X - d.Ladder.X) <= 2 && Math.Abs(t.Z - d.Ladder.Z) <= 2)
                        continue;  // room to stand round the way up
                    bool open = true;
                    for (int dx = -1; dx <= 1 && open; dx++)
                        for (int dz = -1; dz <= 1 && open; dz++)
                            if (!d.IsFloor(new Tile(x + dx, z + dz)))
                                open = false;
                    if (!open || rng.NextDouble() < 0.2)
                        continue;
                    double roll = rng.NextDouble();
                    int k = 0;
                    while (k < tier.Rocks.Length - 1 && roll >= tier.Shares[k])
                        roll -= tier.Shares[k++];
                    d.Ores[t] = tier.Rocks[k];
                }
            return d;
        }
    }
}
