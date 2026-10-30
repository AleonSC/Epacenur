using System;
using Epacenur.Core;

namespace Epacenur.Content
{
    /// Mining: rocks from copper and tin to adamantite, their ores, and a pickaxe of each metal.
    /// Better rocks lie deeper in the wilderness (each rock's Spawns).
    public sealed class MiningModule : IContentModule
    {
        public const string Skill = "mining", CopperOre = "copper_ore", TinOre = "tin_ore", IronOre = "iron_ore", Coal = "coal",
            MithrilOre = "mithril_ore", AdamantiteOre = "adamantite_ore", BronzePickaxe = "bronze_pickaxe",
            CopperRock = "copper_rock", TinRock = "tin_rock", IronRock = "iron_rock", CoalRock = "coal_rock",
            MithrilRock = "mithril_rock", AdamantiteRock = "adamantite_rock";

        /// Every rock, weakest first (the dev plot's mining area has a row of each).
        public static readonly string[] Rocks = { CopperRock, TinRock, IronRock, CoalRock, MithrilRock, AdamantiteRock };

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

            Ore(db, CopperOre, "Copper ore", 3, new Rgb(0.8f, 0.45f, 0.2f));
            Ore(db, TinOre, "Tin ore", 3, new Rgb(0.75f, 0.75f, 0.72f));
            Ore(db, IronOre, "Iron ore", 17, new Rgb(0.5f, 0.3f, 0.24f));
            Ore(db, Coal, "Coal", 45, new Rgb(0.12f, 0.12f, 0.12f), "Hmm a non-renewable energy source!");
            Ore(db, MithrilOre, "Mithril ore", 162, Metals.Mithril.Colour);
            Ore(db, AdamantiteOre, "Adamantite ore", 400, Metals.Adamant.Colour);

            // A pickaxe of each metal: better ones mine faster (OSRS levels to use them).
            foreach (var m in Metals.All)
            {
                var pick = db.Item(m.ItemId("pickaxe"), m.Name + " pickaxe", "Used for mining.", m.Value(1) + (m == Metals.Bronze ? 0 : 50 * m.Tier), m.Colour);
                pick.ToolType = "pickaxe";
                pick.ToolTier = m.Tier;
                if (m.ToolLevel > 1)
                    pick.Requires.Add(new SkillReq(Skill, m.ToolLevel));
                pick.Icon = new Icon()
                    .Turned(18.5f, 8f, 3f, 26f, Rgb.Wood, -35f).Turned(8f, 8f, 24f, 3f, m.Colour, -35f)
                    .Turned(8f, 11f, 3f, 3f, m.Colour, -35f).Turned(29f, 11f, 3f, 3f, m.Colour, -35f);
                pick.HeldModel = Pickaxe(m.Colour);
                pick.GroundModel = Looks.OnGround(pick.HeldModel);
            }

            // Rocks (OSRS levels, xp and chances with a bronze pickaxe), and where they lie: copper and tin
            // round the towns, the rest deeper in the wilderness (from a level), commoner in some biomes.
            Rock(db, CopperRock, "Copper rocks", "copper", CopperOre, 1, 17.5, 100, 350, 2.4f, new Rgb(0.85f, 0.45f, 0.2f), 0, 40, 3);
            Rock(db, TinRock, "Tin rocks", "tin", TinOre, 1, 17.5, 100, 350, 2.4f, new Rgb(0.82f, 0.82f, 0.8f), 0, 40, 3);
            Rock(db, IronRock, "Iron rocks", "iron", IronOre, 15, 35, 96, 350, 5.4f, new Rgb(0.55f, 0.32f, 0.25f), 2, int.MaxValue, 3);
            Rock(db, CoalRock, "Coal rocks", "coal", Coal, 30, 50, 16, 100, 20f, new Rgb(0.1f, 0.1f, 0.1f), 6, int.MaxValue, 2.5, Biome.Highlands, 3);
            Rock(db, MithrilRock, "Mithril rocks", "mithril", MithrilOre, 55, 80, 4, 50, 60f, Metals.Mithril.Colour * 1.2f, 16, int.MaxValue, 1.2, Biome.Highlands, 12);
            Rock(db, AdamantiteRock, "Adamantite rocks", "adamantite", AdamantiteOre, 70, 95, 2, 25, 120f, Metals.Adamant.Colour * 1.2f, 28, int.MaxValue, 0.8, Biome.Wasteland, 26);
        }

        static void Ore(ContentDb db, string id, string name, int value, Rgb c, string examine = "This needs refining.")
        {
            var ore = db.Item(id, name, examine, value, c);
            ore.Icon = Looks.OreIcon(c);
            ore.GroundModel = Looks.OreModel(c);
        }

        static void Rock(ContentDb db, string id, string name, string metal, string ore, int level, double xp, int low, int high,
            float respawn, Rgb oreColour, int minDanger, int maxDanger, double weight,
            Biome? likes = null, int likesFrom = 0)
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
            var node = new NodeDef
            {
                Id = id, Name = name, Action = "Mine", Examine = "A rock containing " + metal + ".",
                EmptyName = "Rocks", EmptyExamine = "There is currently no ore available in this rock.",
                Skill = Skill, ToolType = "pickaxe",
                Yields = new[] { new NodeYield { ItemId = ore, Level = level, Xp = xp, LowChance = low, HighChance = high, Message = "You manage to mine some " + metal + "." } },
                AttemptTicks = 4, FirstPersonTicks = 2, MinLives = 1, MaxLives = 1, RespawnSeconds = respawn, Look = NodeLook.Rock,
                Model = full, EmptyModel = empty,
                StartMessage = "You swing your pick at the rock.",
                NeedToolMessage = "You need a pickaxe you can use to mine this rock.",
                FullMessage = "Your inventory is too full to hold any more ore.",
                LevelMessage = "You need a Mining level of {0} to mine this rock.",
                EmptyMessage = "There is currently no ore available in this rock.",
            };
            node.Spawns.Add(new SpawnRule { On = Terrain.Rock, MinDanger = minDanger, MaxDanger = maxDanger, Weight = weight });
            if (likes != null)
                node.Spawns.Add(new SpawnRule { On = Terrain.Rock, MinDanger = likesFrom, MaxDanger = maxDanger, Weight = weight, Biome = likes });
            db.Add(node);
        }
    }
}
