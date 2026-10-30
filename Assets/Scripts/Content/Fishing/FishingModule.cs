using Epacenur.Core;

namespace Epacenur.Content
{
    /// Fishing: net spots for shrimps and anchovies everywhere; further out, lure spots (trout and salmon,
    /// with a fly fishing rod and feathers), lobster cages and harpooning (tuna, swordfish).
    public sealed class FishingModule : IContentModule
    {
        public const string Skill = "fishing", SmallFishingNet = "small_fishing_net", RawShrimps = "raw_shrimps",
            RawAnchovies = "raw_anchovies", NetSpot = "net_fishing_spot",
            FlyFishingRod = "fly_fishing_rod", LobsterPot = "lobster_pot", Harpoon = "harpoon", Feathers = "feathers",
            RawTrout = "raw_trout", RawSalmon = "raw_salmon", RawLobster = "raw_lobster", RawTuna = "raw_tuna", RawSwordfish = "raw_swordfish",
            LureSpot = "lure_fishing_spot", CageSpot = "cage_fishing_spot", HarpoonSpot = "harpoon_fishing_spot";

        public void Register(ContentDb db)
        {
            var blue = new Rgb(0.4f, 0.65f, 0.95f);
            db.Skill(Skill, "Fishing", new Rgb(0.3f, 0.6f, 0.95f), new Icon(22f)  // a fish
                .Rect(3f, 8f, 12f, 7f, blue).Rect(5f, 6f, 8f, 11f, blue).Rect(15f, 6f, 3f, 11f, blue * 0.75f)
                .Rect(5f, 10f, 2f, 2f, new Rgb(0.05f, 0.05f, 0.1f)));

            var twine = new Rgb(0.75f, 0.72f, 0.6f);
            var netWood = new Rgb(0.5f, 0.34f, 0.18f);
            var mesh = new Rgb(0.82f, 0.8f, 0.68f);
            var net = db.Item(SmallFishingNet, "Small fishing net", "Useful for catching small fish.", 5, twine);
            net.ToolType = "net";
            net.ToolTier = 1;
            var netIcon = new Icon().Rect(7f, 5f, 18f, 18f, netWood).Rect(9f, 7f, 14f, 14f, twine);
            for (int k = 0; k < 3; k++)
                netIcon.Rect(12f + k * 3.5f, 7f, 1f, 14f, twine * 0.6f).Rect(9f, 10f + k * 3.5f, 14f, 1f, twine * 0.6f);
            net.Icon = netIcon.Rect(23f, 21f, 4f, 13f, netWood);
            net.HeldModel = new Model()
                .Cylinder(0f, 0f, 0.2f, 0.03f, 0.2f, 0.03f, netWood).Turn(90f, 0f, 0f)
                .Cylinder(0f, 0f, 0.55f, 0.32f, 0.008f, 0.32f, netWood)
                .Cylinder(0f, -0.008f, 0.55f, 0.288f, 0.008f, 0.288f, mesh)
                .Sphere(0f, -0.07f, 0.55f, 0.26f, 0.14f, 0.26f, mesh);
            net.GroundModel = new Model().Child(net.HeldModel, 0f, 0.03f, -0.2f, 90f, 0f, 0f);

            var shrimpPink = new Rgb(0.95f, 0.55f, 0.55f);
            var shrimps = db.Item(RawShrimps, "Raw shrimps", "I should try cooking this.", 5, shrimpPink);
            shrimps.Icon = Looks.FishIcon(shrimpPink, 2);
            shrimps.GroundModel = Looks.Fish(shrimpPink, 2);
            var anchovyBlue = new Rgb(0.5f, 0.6f, 0.75f);
            var anchovies = db.Item(RawAnchovies, "Raw anchovies", "I should try cooking this.", 15, anchovyBlue);
            anchovies.Icon = Looks.FishIcon(anchovyBlue, 3);
            anchovies.GroundModel = Looks.Fish(anchovyBlue, 3);

            // A net fishing spot: anchovies from level 15, shrimps from 1. It never runs out, but moves
            // along the shore every 40-90 s. The game draws its ripples itself.
            var netSpot = db.Add(new NodeDef
            {
                Id = NetSpot, Name = "Fishing spot", Action = "Net", Examine = "I can see small fish swimming in the water.",
                Skill = Skill, ToolType = "net",
                Yields = new[]
                {
                    new NodeYield { ItemId = RawAnchovies, Level = 15, Xp = 40, LowChance = 24, HighChance = 128, Message = "You catch some anchovies." },
                    new NodeYield { ItemId = RawShrimps, Level = 1, Xp = 10, LowChance = 48, HighChance = 256, Message = "You catch some shrimps." },
                },
                AttemptTicks = 5, FirstPersonTicks = 2, MinLives = 0, MaxLives = 0,
                MoveMinSeconds = 40f, MoveMaxSeconds = 90f, Look = NodeLook.FishingSpot, Blocks = false,
                StartMessage = "You cast out your net...",
                NeedToolMessage = "You need a small fishing net to fish here.",
                FullMessage = "You can't carry any more fish.",
                LevelMessage = "You need a Fishing level of {0} to fish here.",
            });
            netSpot.Spawns.Add(new SpawnRule { On = Terrain.Water, Weight = 4 });

            // Tools and bait.
            var feathers = db.Item(Feathers, "Feathers", "Used for fly-fishing.", 2, new Rgb(0.95f, 0.95f, 0.93f));
            feathers.Stackable = true;
            feathers.Icon = new Icon().Turned(17f, 6f, 6f, 26f, new Rgb(0.95f, 0.95f, 0.93f), 25f).Turned(19.5f, 6f, 1f, 28f, new Rgb(0.7f, 0.68f, 0.65f), 25f);
            feathers.GroundModel = new Model().Cube(0f, 0.02f, 0f, 0.08f, 0.02f, 0.3f, new Rgb(0.95f, 0.95f, 0.93f)).Turn(0f, 30f, 0f)
                .Cube(0.06f, 0.03f, 0f, 0.07f, 0.02f, 0.28f, new Rgb(0.9f, 0.9f, 0.88f)).Turn(0f, -20f, 0f);
            var rodWood = new Rgb(0.55f, 0.4f, 0.22f);
            Tool(db, FlyFishingRod, "Fly fishing rod", "Useful for fly fishing.", 5, "fly_rod", rodWood,
                new Icon().Turned(19f, 2f, 2f, 36f, rodWood, 35f).Rect(26f, 24f, 6f, 6f, new Rgb(0.3f, 0.3f, 0.32f)),
                new Model().Cylinder(0f, 0f, 0.55f, 0.025f, 0.6f, 0.025f, rodWood).Turn(90f, 0f, 0f)
                    .Cylinder(0f, -0.05f, 0.12f, 0.08f, 0.03f, 0.08f, new Rgb(0.3f, 0.3f, 0.32f)).Turn(0f, 0f, 90f));
            var cane = new Rgb(0.62f, 0.5f, 0.3f);
            Tool(db, LobsterPot, "Lobster pot", "Useful for catching lobsters.", 20, "lobster_pot", cane,
                new Icon().Rect(8f, 12f, 24f, 18f, cane).Rect(10f, 14f, 20f, 14f, cane * 0.7f).Rect(8f, 9f, 24f, 4f, cane * 1.15f)
                    .Rect(13f, 12f, 1f, 18f, cane * 1.2f).Rect(19f, 12f, 1f, 18f, cane * 1.2f).Rect(25f, 12f, 1f, 18f, cane * 1.2f),
                new Model().Cube(0f, 0f, 0.25f, 0.34f, 0.26f, 0.42f, cane).Cylinder(0f, 0.13f, 0.25f, 0.17f, 0.21f, 0.17f, cane * 1.15f).Turn(90f, 0f, 0f));
            Tool(db, Harpoon, "Harpoon", "Useful for catching really big fish.", 5, "harpoon", Rgb.Steel,
                new Icon().Turned(19f, 6f, 2.5f, 30f, Rgb.Wood, 35f).Turned(18f, 1f, 5f, 8f, Rgb.Steel, 35f),
                new Model().Cube(0f, 0f, 0.3f, 0.04f, 0.04f, 0.7f, Looks.Handle).Cube(0f, 0f, 0.7f, 0.03f, 0.1f, 0.14f, Rgb.Steel)
                    .Cube(0f, 0.06f, 0.62f, 0.03f, 0.05f, 0.05f, Rgb.Steel));

            // The fish themselves (OSRS levels and xp).
            Raw(db, RawTrout, "Raw trout", 20, new Rgb(0.6f, 0.62f, 0.55f), 1);
            Raw(db, RawSalmon, "Raw salmon", 30, new Rgb(0.85f, 0.55f, 0.48f), 1);
            Raw(db, RawLobster, "Raw lobster", 100, new Rgb(0.4f, 0.32f, 0.35f), 1);
            Raw(db, RawTuna, "Raw tuna", 70, new Rgb(0.42f, 0.48f, 0.62f), 1);
            Raw(db, RawSwordfish, "Raw swordfish", 160, new Rgb(0.55f, 0.55f, 0.72f), 1);

            Spot(db, LureSpot, "Lure", "Fish are rising to the surface here.", "fly_rod", "a fly fishing rod", Feathers, 60, 3,
                new NodeYield { ItemId = RawSalmon, Level = 30, Xp = 70, LowChance = 16, HighChance = 96, Message = "You catch a salmon." },
                new NodeYield { ItemId = RawTrout, Level = 20, Xp = 50, LowChance = 32, HighChance = 192, Message = "You catch a trout." });
            Spot(db, CageSpot, "Cage", "Lobsters scuttle about on the bottom here.", "lobster_pot", "a lobster pot", null, 250, 1.5,
                new NodeYield { ItemId = RawLobster, Level = 40, Xp = 90, LowChance = 6, HighChance = 95, Message = "You catch a lobster." });
            Spot(db, HarpoonSpot, "Harpoon", "Big fish swim deep down here.", "harpoon", "a harpoon", null, 250, 1.5,
                new NodeYield { ItemId = RawSwordfish, Level = 50, Xp = 100, LowChance = 4, HighChance = 48, Message = "You catch a swordfish." },
                new NodeYield { ItemId = RawTuna, Level = 35, Xp = 80, LowChance = 8, HighChance = 64, Message = "You catch a tuna." });
        }

        static void Tool(ContentDb db, string id, string name, string examine, int value, string type, Rgb colour, Icon icon, Model held)
        {
            var tool = db.Item(id, name, examine, value, colour);
            tool.ToolType = type;
            tool.ToolTier = 1;
            tool.Icon = icon;
            tool.HeldModel = held;
            tool.GroundModel = Looks.OnGround(held);
        }

        static void Raw(ContentDb db, string id, string name, int value, Rgb colour, int count)
        {
            var fish = db.Item(id, name, "I should try cooking this.", value, colour);
            fish.Icon = Looks.FishIcon(colour, count);
            fish.GroundModel = Looks.Fish(colour, count);
        }

        /// A fishing spot: never runs out, moves along the shore now and then, and lies `minDistance` or more
        /// tiles from the start. Yields best first.
        static void Spot(ContentDb db, string id, string action, string examine, string toolType, string toolName, string bait,
            int minDistance, double weight, params NodeYield[] yields)
        {
            var spot = db.Add(new NodeDef
            {
                Id = id, Name = "Fishing spot", Action = action, Examine = examine,
                Skill = Skill, ToolType = toolType, BaitItemId = bait, Yields = yields,
                AttemptTicks = 5, FirstPersonTicks = 2, MinLives = 0, MaxLives = 0,
                MoveMinSeconds = 40f, MoveMaxSeconds = 90f, Look = NodeLook.FishingSpot, Blocks = false,
                StartMessage = "You attempt to catch a fish...",
                NeedToolMessage = "You need " + toolName + " to fish here.",
                FullMessage = "You can't carry any more fish.",
                LevelMessage = "You need a Fishing level of {0} to fish here.",
            });
            spot.Spawns.Add(new SpawnRule { On = Terrain.Water, MinDistance = minDistance, Weight = weight });
        }
    }
}
