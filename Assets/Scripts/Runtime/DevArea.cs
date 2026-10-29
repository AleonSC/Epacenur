using System.Collections.Generic;
using Epacenur.Core;
using UnityEngine;

namespace Epacenur.Runtime
{
    /// One plot of the dev area for testing a skill. To add a skill area: subclass this and add it
    /// to DevArea.CreateAreas() (or replace one of the empty plots).
    public abstract class SkillArea
    {
        public abstract string Name { get; }
        /// The plot's tiles: x, z of its lower-left tile and its size in tiles.
        public RectInt Plot;
        public virtual Color GroundColor => new Color(0.36f, 0.55f, 0.27f);

        /// Where the dev tools' teleport puts you: just in front of the plot.
        public virtual Tile TeleportTile => new Tile(Plot.x + Plot.width / 2, Plot.y - 2);

        public abstract void Build(World world);
    }

    /// The layout of the dev area.
    public static class DevArea
    {
        public const int Width = 48, Height = 48;
        public static readonly Tile Spawn = new Tile(24, 10);
        public static readonly Tile BankBooth = new Tile(28, 13);
        public static readonly Tile ShopKeeper = new Tile(30, 13);

        public static List<SkillArea> CreateAreas()
        {
            return new List<SkillArea>
            {
                new WoodcuttingArea { Plot = new RectInt(3, 26, 13, 16) },
                new EmptyPlot { Plot = new RectInt(18, 26, 12, 16) },
                new EmptyPlot { Plot = new RectInt(32, 26, 13, 16) },
            };
        }
    }

    /// Basic trees for woodcutting.
    public sealed class WoodcuttingArea : SkillArea
    {
        public override string Name => "Woodcutting";
        public override Color GroundColor => new Color(0.3f, 0.47f, 0.22f);

        public override void Build(World world)
        {
            // A loose grid of trees, roughly 3 tiles apart, always at least one free tile between them
            // so every tree can be reached.
            var rng = new System.Random(1234);
            for (int x = Plot.x + 1; x < Plot.xMax - 1; x += 3)
                for (int z = Plot.y + 1; z < Plot.yMax - 2; z += 3)
                {
                    var tile = new Tile(x + rng.Next(0, 2), z + rng.Next(0, 2));
                    world.AddTree(TreeDefs.Basic, tile, rng);
                }
        }
    }

    /// A plot kept free for the next skill.
    public sealed class EmptyPlot : SkillArea
    {
        public override string Name => "Empty plot (next skill)";
        public override Color GroundColor => new Color(0.42f, 0.4f, 0.3f);
        public override void Build(World world) { }
    }
}
