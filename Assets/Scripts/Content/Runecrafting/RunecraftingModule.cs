using Epacenur.Core;

namespace Epacenur.Content
{
    /// Runecrafting: mine rune essence (it never runs out; boulders of it ring every altar), take it to an altar in a ring of standing
    /// stones out in the wilds, and craft it all into that altar's runes at once (OSRS levels and xp).
    /// The air altar is always just east of the start; the better an altar's runes, the deeper in the wilderness it lies.
    public sealed class RunecraftingModule : IContentModule
    {
        public const string Skill = "runecrafting", Essence = "rune_essence", EssenceRock = "essence_rock",
            Air = "air_rune", Mind = "mind_rune", Water = "water_rune", Earth = "earth_rune", Fire = "fire_rune",
            Body = "body_rune", Nature = "nature_rune", Law = "law_rune";

        public static readonly Rgb EssenceColour = new Rgb(0.78f, 0.8f, 0.86f);

        /// A rune: a pale stone with its element's mark.
        public static Icon RuneIcon(Rgb mark) => new Icon()
            .Rect(11f, 11f, 18f, 18f, EssenceColour * 0.85f).Rect(13f, 9f, 14f, 2f, EssenceColour * 0.85f).Rect(13f, 29f, 14f, 2f, EssenceColour * 0.7f)
            .Diamond(20f, 20f, 6f, mark).Rect(19f, 16f, 2f, 8f, mark * 1.3f);

        public void Register(ContentDb db)
        {
            db.Skill(Skill, "Runecrafting", new Rgb(0.75f, 0.75f, 0.8f), new Icon(22f)  // a rune
                .Rect(4f, 4f, 14f, 14f, EssenceColour * 0.85f).Diamond(11f, 11f, 5f, new Rgb(0.4f, 0.75f, 1f)));

            var ess = db.Item(Essence, "Rune essence", "An uncharged rune stone.", 4, EssenceColour);
            ess.Icon = new Icon().Rect(11f, 11f, 18f, 18f, EssenceColour).Rect(13f, 9f, 14f, 2f, EssenceColour).Rect(13f, 29f, 14f, 2f, EssenceColour * 0.8f)
                .Rect(14f, 13f, 5f, 3f, EssenceColour * 1.15f);
            ess.GroundModel = new Model().Cube(0f, 0.06f, 0f, 0.18f, 0.12f, 0.18f, EssenceColour);

            // Essence rocks: big pale boulders round each altar's ring of stones. Mined forever.
            var boulder = new Model()
                .Sphere(0f, 0.45f, 0f, 1.2f, 0.95f, 1.05f, EssenceColour * 0.9f)
                .Sphere(0.3f, 0.3f, 0.25f, 0.6f, 0.5f, 0.6f, EssenceColour * 0.8f)
                .Sphere(-0.35f, 0.75f, -0.1f, 0.4f, 0.35f, 0.4f, EssenceColour * 1.05f)
                .Cube(0.15f, 0.95f, 0.1f, 0.14f, 0.3f, 0.14f, new Rgb(0.7f, 0.85f, 1f)).Turn(10f, 30f, 15f).Glowing()  // faintly glowing crystals
                .Cube(-0.4f, 0.55f, 0.35f, 0.1f, 0.22f, 0.1f, new Rgb(0.7f, 0.85f, 1f)).Turn(-20f, 0f, 25f).Glowing();
            var node = db.Add(new NodeDef
            {
                Id = EssenceRock, Name = "Rune essence", Action = "Mine", Examine = "A pale rock that hums faintly.",
                EmptyName = "Rune essence", EmptyExamine = "A pale rock that hums faintly.",
                Skill = MiningModule.Skill, ToolType = "pickaxe",
                Yields = new[] { new NodeYield { ItemId = Essence, Level = 1, Xp = 5, LowChance = 150, HighChance = 256, Message = "You manage to mine an unshaped stone." } },
                AttemptTicks = 4, FirstPersonTicks = 2, MinLives = 0, MaxLives = 0, Look = NodeLook.Rock,
                Model = boulder, EmptyModel = boulder,
                StartMessage = "You swing your pick at the rock.",
                NeedToolMessage = "You need a pickaxe you can use to mine this rock.",
                FullMessage = "Your inventory is too full to hold any more essence.",
                LevelMessage = "You need a Mining level of {0} to mine this rock.",
            });
            node.Spawns.Add(new SpawnRule { On = Terrain.Essence });

            // The runes and their altars: level, xp per essence, value, colour, and the wilderness levels the altar lies at.
            Rune(db, Air, "Air", 1, 5, 4, new Rgb(0.85f, 0.9f, 0.95f), 0, 30, 3, true);
            Rune(db, Mind, "Mind", 2, 5.5, 3, new Rgb(0.95f, 0.6f, 0.2f), 0, 40, 3);
            Rune(db, Water, "Water", 5, 6, 4, new Rgb(0.25f, 0.5f, 0.95f), 3, 50, 3);
            Rune(db, Earth, "Earth", 9, 6.5, 4, new Rgb(0.45f, 0.32f, 0.18f), 6, 36, 3);
            Rune(db, Fire, "Fire", 14, 7, 4, new Rgb(0.95f, 0.3f, 0.12f), 10, 46, 3);
            Rune(db, Body, "Body", 20, 7.5, 7, new Rgb(0.3f, 0.35f, 0.75f), 15, int.MaxValue, 2);
            Rune(db, Nature, "Nature", 44, 9, 180, new Rgb(0.2f, 0.75f, 0.25f), 25, int.MaxValue, 2);
            Rune(db, Law, "Law", 54, 9.5, 240, new Rgb(0.2f, 0.2f, 0.85f), 32, int.MaxValue, 2);
        }

        static void Rune(ContentDb db, string id, string element, int level, double xp, int value, Rgb colour,
            int minDanger, int maxDanger, double weight, bool starter = false)
        {
            var rune = db.Item(id, element + " rune", "One of the basic " + element.ToLowerInvariant() + " runes.", value, colour);
            rune.Stackable = true;
            rune.Icon = RuneIcon(colour);
            rune.GroundModel = new Model().Cube(0f, 0.04f, 0f, 0.14f, 0.08f, 0.14f, EssenceColour).Sphere(0f, 0.085f, 0f, 0.08f, 0.02f, 0.08f, colour);

            // The altar: a stone block with a glowing orb of its element over it.
            string altar = element.ToLowerInvariant() + "_altar";
            var stone = new Rgb(0.5f, 0.5f, 0.52f);
            var station = db.Add(new StationDef
            {
                Id = altar, Name = element + " altar", Action = "Craft-rune", Kind = StationKind.Craft,
                Examine = "A mysterious power emanates from the " + element.ToLowerInvariant() + " altar.",
                Label = element + " altar", LabelHeight = 2.2f, HasLight = true, LightColour = colour,
                Model = new Model()
                    .Cube(0f, 0.1f, 0f, 1.5f, 0.2f, 1.5f, stone * 0.85f)
                    .Cube(0f, 0.5f, 0f, 1.1f, 0.6f, 1.1f, stone)
                    .Cube(0f, 0.83f, 0f, 1.25f, 0.08f, 1.25f, stone * 1.1f)
                    .Sphere(0f, 1.25f, 0f, 0.4f, 0.4f, 0.4f, colour).Glowing(),
            });
            station.Spawns.Add(new StationSpawnRule { MinDanger = minDanger, MaxDanger = maxDanger, Weight = weight, Starter = starter });
            db.Add(new RecipeDef
            {
                Id = "craft_" + id, Name = element + " rune", Station = altar, Skill = Skill, Level = level, Xp = xp, Ticks = 1, Batch = 28,
                Inputs = new[] { new Ingredient(Essence, 1) }, Product = id,
                SuccessMessage = "You bind the temple's power into " + element.ToLowerInvariant() + " runes.",
            });
        }
    }
}
