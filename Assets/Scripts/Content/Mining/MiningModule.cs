using System;
using Epacenur.Core;

namespace Epacenur.Content
{
    /// Mining: copper and tin rocks, ores and the bronze pickaxe.
    public sealed class MiningModule : IContentModule
    {
        public const string Skill = "mining", CopperOre = "copper_ore", TinOre = "tin_ore", BronzePickaxe = "bronze_pickaxe",
            CopperRock = "copper_rock", TinRock = "tin_rock";

        /// A pickaxe pointing forward (+z) from the grip, its head across the end.
        public static Model Pickaxe(Rgb metal) => new Model()
            .Cube(0f, 0f, 0.25f, 0.05f, 0.05f, 0.6f, Looks.Handle)
            .Cube(0f, 0f, 0.52f, 0.04f, 0.42f, 0.06f, metal)
            .Cube(0f, 0.21f, 0.49f, 0.035f, 0.05f, 0.08f, metal)
            .Cube(0f, -0.21f, 0.49f, 0.035f, 0.05f, 0.08f, metal);

        public void Register(ContentDb db)
        {
            db.Skill(Skill, "Mining", new Rgb(0.6f, 0.55f, 0.5f), new Icon(22f)  // a pickaxe
                .Turned(9.75f, 3f, 2.5f, 18f, Rgb.Wood, -35f).Turned(2f, 2f, 18f, 2.5f, Rgb.Steel, -35f));

            var copper = new Rgb(0.8f, 0.45f, 0.2f);
            var tin = new Rgb(0.75f, 0.75f, 0.72f);
            var copperOre = db.Item(CopperOre, "Copper ore", "This needs refining.", 3, copper);
            copperOre.Icon = Looks.OreIcon(copper);
            copperOre.GroundModel = Looks.OreModel(copper);
            var tinOre = db.Item(TinOre, "Tin ore", "This needs refining.", 3, tin);
            tinOre.Icon = Looks.OreIcon(tin);
            tinOre.GroundModel = Looks.OreModel(tin);

            var pick = db.Item(BronzePickaxe, "Bronze pickaxe", "Used for mining.", 1, Looks.Bronze);
            pick.ToolType = "pickaxe";
            pick.ToolTier = 1;
            pick.Icon = new Icon()
                .Turned(18.5f, 8f, 3f, 26f, Rgb.Wood, -35f).Turned(8f, 8f, 24f, 3f, Looks.Bronze, -35f)
                .Turned(8f, 11f, 3f, 3f, Looks.Bronze, -35f).Turned(29f, 11f, 3f, 3f, Looks.Bronze, -35f);
            pick.HeldModel = Pickaxe(Looks.Bronze);
            pick.GroundModel = Looks.OnGround(pick.HeldModel);

            // As in OSRS: level 1, 17.5 xp, one ore each and then empty for 2.4 s.
            Rock(db, CopperRock, "Copper rocks", "copper", CopperOre, new Rgb(0.85f, 0.45f, 0.2f));
            Rock(db, TinRock, "Tin rocks", "tin", TinOre, new Rgb(0.82f, 0.82f, 0.8f));
        }

        static void Rock(ContentDb db, string id, string name, string metal, string ore, Rgb oreColour)
        {
            var empty = new Model()
                .Sphere(0f, 0.32f, 0f, 0.95f, 0.7f, 0.85f, Looks.Rock)
                .Sphere(0.22f, 0.2f, 0.18f, 0.55f, 0.45f, 0.55f, new Rgb(0.42f, 0.4f, 0.38f))
                .Sphere(-0.25f, 0.18f, -0.1f, 0.5f, 0.4f, 0.55f, new Rgb(0.42f, 0.4f, 0.38f));
            var full = new Model().Include(empty);
            for (int i = 0; i < 6; i++)  // streaks of ore on the surface
            {
                float a = i * 1.05f + 0.2f;
                full.Sphere((float)Math.Cos(a) * 0.38f, 0.35f + (i % 3) * 0.1f, (float)Math.Sin(a) * 0.33f, 0.16f, 0.1f, 0.16f, oreColour);
            }
            db.Add(new NodeDef
            {
                Id = id, Name = name, Action = "Mine", Examine = "A rock containing " + metal + ".",
                EmptyName = "Rocks", EmptyExamine = "There is currently no ore available in this rock.",
                Skill = Skill, ToolType = "pickaxe",
                Yields = new[] { new NodeYield { ItemId = ore, Level = 1, Xp = 17.5, LowChance = 100, HighChance = 350, Message = "You manage to mine some " + metal + "." } },
                AttemptTicks = 4, FirstPersonTicks = 2, MinLives = 1, MaxLives = 1, RespawnSeconds = 2.4f, Look = NodeLook.Rock,
                Model = full, EmptyModel = empty,
                StartMessage = "You swing your pick at the rock.",
                NeedToolMessage = "You need a pickaxe to mine this rock.",
                FullMessage = "Your inventory is too full to hold any more ore.",
                LevelMessage = "You need a Mining level of {0} to mine this rock.",
                EmptyMessage = "There is currently no ore available in this rock.",
            });
        }
    }
}
