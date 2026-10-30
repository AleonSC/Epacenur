using System.Collections.Generic;
using Epacenur.Content;
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
        public static readonly Tile Altar = new Tile(26, 13);
        public static readonly Tile Furnace = new Tile(24, 13);
        public static readonly Tile Anvil = new Tile(22, 13);
        public static readonly Tile SpinningWheel = new Tile(20, 13);
        public static readonly Tile TanningRack = new Tile(18, 13);

        public static List<SkillArea> CreateAreas()
        {
            return new List<SkillArea>
            {
                new MiningArea { Plot = new RectInt(3, 26, 13, 16) },
                new GoblinArea { Plot = new RectInt(18, 26, 12, 16) },
                new FishingArea { Plot = new RectInt(32, 26, 13, 16) },
                new FiremakingArea { Plot = new RectInt(3, 14, 13, 9) },
                new FlaxArea { Plot = new RectInt(32, 14, 13, 9) },
                new RunecraftingArea { Plot = new RectInt(17, 16, 14, 8) },
                new AgilityArea { Plot = new RectInt(32, 1, AgilityCourse.Width + 1, AgilityCourse.Depth + 2) },
                new ThievingArea { Plot = new RectInt(3, 2, 13, 10) },
            };
        }
    }

    /// A row or two of every kind of rock, for testing mining (smelt the ores at the furnace by the altar).
    public sealed class MiningArea : SkillArea
    {
        public override string Name => "Mining";
        public override Color GroundColor => new Color(0.48f, 0.44f, 0.38f);

        public override void Build(World world)
        {
            // Rows of rocks, every kind in turn, with a free tile between them.
            var rng = new System.Random(4321);
            int i = 0;
            for (int x = Plot.x + 1; x < Plot.xMax - 1; x += 3)
                for (int z = Plot.y + 1; z < Plot.yMax - 2; z += 3)
                {
                    var tile = new Tile(x + rng.Next(0, 2), z + rng.Next(0, 2));
                    world.AddNode(MiningModule.Rocks[i++ % MiningModule.Rocks.Length], tile, rng);
                }
        }
    }

    /// Basic trees for woodcutting (no longer on the dev plot: the main world's forests have plenty).
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
                    world.AddNode(WoodcuttingModule.Tree, tile, rng);
                }
        }
    }

    /// Goblins to fight: they wander round the plot, don't fight back yet, and drop coins.
    public sealed class GoblinArea : SkillArea
    {
        public const int Count = 4;
        public override string Name => "Goblins";
        public override Color GroundColor => new Color(0.45f, 0.42f, 0.28f);

        public override void Build(World world)
        {
            var rng = new System.Random(99);
            for (int i = 0; i < Count; i++)
            {
                var tile = new Tile(Plot.x + 1 + rng.Next(Plot.width - 2), Plot.y + 1 + rng.Next(Plot.height - 3));
                world.AddNpc(ContentDb.Current.Npc(CombatModule.Goblin), tile, new RectInt(Plot.x + 1, Plot.y + 1, Plot.width - 2, Plot.height - 3));
            }
        }
    }

    /// A pond with net fishing spots along its edge.
    public sealed class FishingArea : SkillArea
    {
        public override string Name => "Fishing";
        public override Color GroundColor => new Color(0.32f, 0.5f, 0.3f);

        public override void Build(World world)
        {
            var pond = new RectInt(Plot.x + 2, Plot.y + 3, Plot.width - 4, Plot.height - 7);
            world.AddWater(pond);
            // Spots sit on water tiles with dry land straight next to them.
            var shore = new List<Tile>();
            for (int x = pond.x; x < pond.x + pond.width; x++)
                for (int z = pond.y; z < pond.y + pond.height; z++)
                {
                    var t = new Tile(x, z);
                    if (world.Grid.IsWalkable(new Tile(x - 1, z)) || world.Grid.IsWalkable(new Tile(x + 1, z))
                        || world.Grid.IsWalkable(new Tile(x, z - 1)) || world.Grid.IsWalkable(new Tile(x, z + 1)))
                        shore.Add(t);
                }
            var rng = new System.Random(7);
            // One spot of each kind.
            foreach (var spot in new[] { FishingModule.NetSpot, FishingModule.LureSpot, FishingModule.CageSpot, FishingModule.HarpoonSpot })
                world.AddNode(spot, shore[rng.Next(shore.Count)], rng, shore);
        }
    }

    /// Flax plants to pick (spin them at the spinning wheel by the furnace).
    public sealed class FlaxArea : SkillArea
    {
        public override string Name => "Flax";
        public override Color GroundColor => new Color(0.4f, 0.5f, 0.3f);

        public override void Build(World world)
        {
            var rng = new System.Random(55);
            for (int x = Plot.x + 1; x < Plot.xMax - 1; x += 2)
                for (int z = Plot.y + 1; z < Plot.yMax - 2; z += 2)
                    world.AddNode(CraftingModule.FlaxPlant, new Tile(x, z), rng);
        }
    }

    /// Every runecrafting altar, two rows of four, and essence rocks to mine for them.
    public sealed class RunecraftingArea : SkillArea
    {
        public override string Name => "Runecrafting";
        public override Color GroundColor => new Color(0.55f, 0.55f, 0.58f);

        public override void Build(World world)
        {
            var altars = new List<StationDef>();
            foreach (var s in ContentDb.Current.Stations)
                if (s.Kind == StationKind.Craft)
                    altars.Add(s);
            for (int i = 0; i < altars.Count; i++)
                world.AddStation(altars[i].Id, new Tile(Plot.x + 1 + (i % 4) * 3, Plot.y + 2 + (i / 4) * 3));
            var rng = new System.Random(77);
            for (int z = Plot.y + 1; z < Plot.yMax - 1; z += 2)
                world.AddNode(RunecraftingModule.EssenceRock, new Tile(Plot.xMax - 1, z), rng);
        }
    }

    /// The easiest agility course, laid out as in the overworld.
    public sealed class AgilityArea : SkillArea
    {
        public override string Name => "Agility";
        public override Color GroundColor => new Color(0.45f, 0.5f, 0.35f);
        public override Tile TeleportTile => new Tile(Plot.x + 1, Plot.y);

        public override void Build(World world)
        {
            var course = ContentDb.Current.Course(AgilityModule.FieldCourse);
            foreach (var p in AgilityCourse.Layout(new Tile(Plot.x + 1, Plot.y + 1)))
                world.AddObstacle(course, p);
        }
    }

    /// One of each market stall, and one of each kind of townsfolk wandering round them.
    public sealed class ThievingArea : SkillArea
    {
        public override string Name => "Thieving";
        public override Color GroundColor => new Color(0.5f, 0.45f, 0.4f);

        public override void Build(World world)
        {
            var rng = new System.Random(66);
            var stalls = new[] { ThievingModule.BakeryStall, ThievingModule.SilkStall, ThievingModule.HerbStall, ThievingModule.RuneStall };
            for (int i = 0; i < stalls.Length; i++)
                world.AddNode(stalls[i], new Tile(Plot.x + 1 + i * 3, Plot.yMax - 2), rng);
            var home = new RectInt(Plot.x + 1, Plot.y + 1, Plot.width - 2, Plot.height - 4);
            foreach (var npc in ContentDb.Current.Npcs)
                if (npc.Pickpocket != null)
                    world.AddNpc(npc, new Tile(home.x + rng.Next(home.width), home.y + rng.Next(home.height)), home);
        }
    }

    /// Open ground for lighting fires (any free tile works; this is just room to do it).
    public sealed class FiremakingArea : SkillArea
    {
        public override string Name => "Firemaking";
        public override Color GroundColor => new Color(0.5f, 0.45f, 0.32f);
        public override void Build(World world) { }
    }

    /// A plot kept free for the next skill.
    public sealed class EmptyPlot : SkillArea
    {
        public override string Name => "Empty plot (next skill)";
        public override Color GroundColor => new Color(0.42f, 0.4f, 0.3f);
        public override void Build(World world) { }
    }
}
