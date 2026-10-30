using Epacenur.Core;

namespace Epacenur.Content
{
    /// Thieving: pick the pockets of the people in towns (men and women everywhere; farmers, warriors, guards
    /// and knights in towns further out), and steal from the two market stalls in every town square. OSRS levels and xp.
    /// Get caught picking a pocket and you're hit and stunned for a few seconds.
    public sealed class ThievingModule : IContentModule
    {
        public const string Skill = "thieving", Man = "man", Woman = "woman", Farmer = "farmer", Warrior = "warrior", Guard = "guard", Knight = "knight",
            Bread = "bread", Silk = "silk", BakeryStall = "bakery_stall", SilkStall = "silk_stall", HerbStall = "herb_stall", RuneStall = "rune_stall";

        static readonly Rgb Skin = new Rgb(0.88f, 0.7f, 0.55f);

        public void Register(ContentDb db)
        {
            db.Skill(Skill, "Thieving", new Rgb(0.55f, 0.35f, 0.6f), new Icon(22f)  // a mask
                .Rect(3f, 7f, 16f, 7f, new Rgb(0.15f, 0.12f, 0.15f)).Rect(5f, 9f, 4f, 3f, new Rgb(0.95f, 0.95f, 0.95f))
                .Rect(13f, 9f, 4f, 3f, new Rgb(0.95f, 0.95f, 0.95f)).Rect(1f, 9f, 2f, 2f, new Rgb(0.15f, 0.12f, 0.15f)).Rect(19f, 9f, 2f, 2f, new Rgb(0.15f, 0.12f, 0.15f)));

            var crust = new Rgb(0.8f, 0.58f, 0.3f);
            var bread = db.Item(Bread, "Bread", "Nice crispy bread.", 12, crust);
            bread.Heals = 5;
            bread.Icon = new Icon().Rect(8f, 15f, 24f, 13f, crust).Rect(10f, 12f, 20f, 4f, crust * 1.1f).Rect(12f, 16f, 2f, 6f, crust * 0.8f)
                .Rect(19f, 16f, 2f, 6f, crust * 0.8f).Rect(26f, 16f, 2f, 6f, crust * 0.8f);
            bread.GroundModel = new Model().Capsule(0f, 0.08f, 0f, 0.22f, 0.16f, 0.14f, crust).Turn(0f, 0f, 90f);
            var silkColour = new Rgb(0.95f, 0.92f, 0.75f);
            var silk = db.Item(Silk, "Silk", "It's a sheet of silk.", 60, silkColour);
            silk.Icon = new Icon().Rect(8f, 10f, 24f, 20f, silkColour).Rect(8f, 10f, 24f, 3f, silkColour * 0.85f).Rect(14f, 17f, 12f, 2f, silkColour * 0.9f);
            silk.GroundModel = new Model().Cube(0f, 0.015f, 0f, 0.4f, 0.03f, 0.32f, silkColour);

            // Townsfolk.
            Person(db, Man, "Man", "One of the town's citizens.", 2, 7, 1, 1, 1, 8, 180, 240, 1,
                Bodies.Humanoid(Skin, new Rgb(0.45f, 0.5f, 0.3f), new Rgb(0.35f, 0.28f, 0.2f), new Rgb(0.3f, 0.2f, 0.1f)), 0, 3)
                .Pickpocket.Loot.Add(ItemIds.Coins, 3, 3, 1);
            Person(db, Woman, "Woman", "One of the town's citizens.", 2, 7, 1, 1, 1, 8, 180, 240, 1,
                Bodies.Humanoid(Skin, new Rgb(0.6f, 0.3f, 0.35f), new Rgb(0.4f, 0.25f, 0.3f), new Rgb(0.55f, 0.35f, 0.15f)), 0, 3)
                .Pickpocket.Loot.Add(ItemIds.Coins, 3, 3, 1);
            Person(db, Farmer, "Farmer", "He grows the crops in this area.", 7, 12, 3, 3, 10, 14.5, 150, 240, 1,
                Bodies.Humanoid(Skin * 0.95f, new Rgb(0.55f, 0.45f, 0.25f), new Rgb(0.25f, 0.3f, 0.45f), new Rgb(0.6f, 0.5f, 0.2f))
                    .Cylinder(0f, 1.9f, 0f, 0.6f, 0.03f, 0.6f, new Rgb(0.85f, 0.75f, 0.4f)), 0, 2)  // a straw hat
                .Pickpocket.Loot.Add(ItemIds.Coins, 9, 9, 9).Add("grimy_guam", 1, 1, 1);
            Person(db, Warrior, "Warrior", "He looks tough.", 18, 20, 15, 15, 25, 26, 100, 240, 2,
                Bodies.Humanoid(Skin, new Rgb(0.5f, 0.5f, 0.55f), new Rgb(0.3f, 0.25f, 0.2f), new Rgb(0.2f, 0.15f, 0.1f)), 200, 2)
                .Pickpocket.Loot.Add(ItemIds.Coins, 18, 18, 1);
            Person(db, Guard, "Guard", "He tries to keep order around here.", 21, 22, 18, 18, 40, 46.8, 80, 240, 2,
                Bodies.Humanoid(Skin, Metals.Iron.Colour, new Rgb(0.3f, 0.3f, 0.35f), new Rgb(0.2f, 0.15f, 0.1f))
                    .Sphere(0f, 1.74f, -0.02f, 0.38f, 0.32f, 0.38f, Metals.Iron.Colour), 400, 2)  // a helmet
                .Pickpocket.Loot.Add(ItemIds.Coins, 30, 30, 1);
            Person(db, Knight, "Knight", "A knight of the realm.", 46, 52, 40, 40, 55, 84.3, 50, 240, 3,
                Bodies.Humanoid(Skin, Metals.Steel.Colour, Metals.Steel.Colour, new Rgb(0.2f, 0.15f, 0.1f))
                    .Sphere(0f, 1.74f, -0.02f, 0.39f, 0.4f, 0.39f, Metals.Steel.Colour).Cube(0f, 1.15f, 0.18f, 0.3f, 0.35f, 0.02f, new Rgb(0.7f, 0.15f, 0.15f)), 700, 1)
                .Pickpocket.Loot.Add(ItemIds.Coins, 50, 50, 1);

            // Market stalls: a table under an awning, with its goods on it (gone once stolen, back after a while).
            Stall(db, BakeryStall, "Bakery stall", new Rgb(0.85f, 0.6f, 0.3f), crust, 5, 4f, 0, 900, 3,
                new NodeYield { ItemId = Bread, Level = 5, Xp = 16, LowChance = 256, HighChance = 256, Message = "You steal a loaf of bread." });
            Stall(db, SilkStall, "Silk stall", new Rgb(0.85f, 0.85f, 0.95f), silkColour, 20, 8f, 150, int.MaxValue, 2,
                new NodeYield { ItemId = Silk, Level = 20, Xp = 24, LowChance = 256, HighChance = 256, Message = "You steal some silk." });
            Stall(db, HerbStall, "Herb stall", new Rgb(0.4f, 0.65f, 0.35f), new Rgb(0.3f, 0.55f, 0.2f), 35, 15f, 300, int.MaxValue, 2,
                Herb("grimy_ranarr", 35, 20), Herb("grimy_harralander", 35, 50), Herb("grimy_tarromin", 35, 100), Herb("grimy_guam", 35, 256));
            Stall(db, RuneStall, "Rune stall", new Rgb(0.4f, 0.45f, 0.85f), RunecraftingModule.EssenceColour, 50, 20f, 600, int.MaxValue, 1.5,
                Rune(RunecraftingModule.Law, 30), Rune(RunecraftingModule.Nature, 50), Rune(RunecraftingModule.Fire, 100),
                Rune(RunecraftingModule.Water, 128), Rune(RunecraftingModule.Earth, 180), Rune(RunecraftingModule.Air, 256));
        }

        static NodeYield Herb(string id, int level, int chance) => new NodeYield
        {
            ItemId = id, Level = level, Xp = 40, LowChance = chance, HighChance = chance, Message = "You steal a herb.",
        };

        static NodeYield Rune(string id, int chance) => new NodeYield
        {
            ItemId = id, Level = 50, Xp = 60, LowChance = chance, HighChance = chance, Message = "You steal a rune.",
        };

        static NpcDef Person(ContentDb db, string id, string name, string examine, int combat, int hp, int attack, int defence,
            int level, double xp, int low, int high, int stunDamage, Model model, int minDistance, double weight)
        {
            var npc = db.Add(new NpcDef
            {
                Id = id, Name = name, Examine = examine, CombatLevel = combat, Hitpoints = hp,
                AttackLevel = attack, DefenceLevel = defence, Bonuses = Bonuses.AllDefence(defence / 3), AttackType = AttackType.Crush,
                MaxHit = 1 + combat / 12, AttackTicks = 4, RespawnSeconds = 20f, Height = 1.95f, WalkSpeed = 1.3f, Model = model,
                Pickpocket = new PickpocketDef { Level = level, Xp = xp, LowChance = low, HighChance = high, StunDamage = stunDamage },
            });
            npc.AlwaysDrops.Add(PrayerModule.Bones);
            npc.Drops.Add(ItemIds.Coins, 1 + combat / 3, 3 + combat, 1).Add(null, 0, 0, 2);
            npc.Spawns.Add(new NpcSpawnRule { Region = RegionKind.Town, Resident = true, MinDistance = minDistance, Weight = weight, MinGroup = 2, MaxGroup = 3 });
            return npc;
        }

        static void Stall(ContentDb db, string id, string name, Rgb awning, Rgb goods, int level, float respawn,
            int minDistance, int maxDistance, double weight, params NodeYield[] yields)
        {
            var wood = new Rgb(0.5f, 0.34f, 0.2f);
            var bare = new Model()
                .Cube(0f, 0.45f, 0f, 1.1f, 0.08f, 0.75f, wood)
                .Cube(-0.48f, 0.22f, -0.3f, 0.07f, 0.45f, 0.07f, wood).Cube(0.48f, 0.22f, -0.3f, 0.07f, 0.45f, 0.07f, wood)
                .Cube(-0.48f, 0.22f, 0.3f, 0.07f, 0.45f, 0.07f, wood).Cube(0.48f, 0.22f, 0.3f, 0.07f, 0.45f, 0.07f, wood)
                .Cube(-0.5f, 1.05f, 0.33f, 0.06f, 1.2f, 0.06f, wood).Cube(0.5f, 1.05f, 0.33f, 0.06f, 1.2f, 0.06f, wood)
                .Cube(-0.5f, 0.85f, -0.33f, 0.06f, 0.8f, 0.06f, wood).Cube(0.5f, 0.85f, -0.33f, 0.06f, 0.8f, 0.06f, wood)
                .Cube(0f, 1.5f, 0f, 1.25f, 0.05f, 0.95f, awning).Turn(-12f, 0f, 0f);
            var full = new Model().Include(bare);
            for (int i = 0; i < 4; i++)
                full.Cube(-0.36f + i * 0.24f, 0.55f, (i % 2) * 0.15f - 0.1f, 0.18f, 0.12f, 0.18f, goods * (0.9f + 0.05f * i));
            var node = db.Add(new NodeDef
            {
                Id = id, Name = name, Action = "Steal-from", Examine = "A market stall (Thieving " + level + ").",
                EmptyName = name, EmptyExamine = "There's nothing on it right now.",
                Skill = Skill, Yields = yields,
                AttemptTicks = 2, FirstPersonTicks = 2, MinLives = 1, MaxLives = 1, RespawnSeconds = respawn, Look = NodeLook.Stall,
                Model = full, EmptyModel = bare,
                StartMessage = "You try to steal from the stall...",
                FullMessage = "You don't have room for that.",
                LevelMessage = "You need a Thieving level of {0} to steal from this stall.",
                EmptyMessage = "There's nothing on the stall right now.",
            });
            node.Spawns.Add(new SpawnRule { On = Terrain.Stall, MinDistance = minDistance, MaxDistance = maxDistance, Weight = weight });
        }
    }
}
