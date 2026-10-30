using Epacenur.Core;

namespace Epacenur.Content
{
    /// Hunter: lay a trap on the ground (Lay, from the inventory), wait, and check it: whether something's caught
    /// depends on your level, and what on the biome you're in. Bird snares catch birds (feathers, bones and meat
    /// to cook); box traps (Hunter 27) catch ferrets and chinchompas, worth selling. You can have one more trap
    /// laid every 20 levels. OSRS levels and xp.
    public sealed class HunterModule : IContentModule
    {
        public const string Skill = Hunter.Skill, BirdSnare = "bird_snare", BoxTrap = "box_trap", RawBirdMeat = "raw_bird_meat",
            RoastBirdMeat = "roast_bird_meat", Ferret = "ferret", Chinchompa = "chinchompa", RedChinchompa = "red_chinchompa";

        static readonly Rgb Wood = new Rgb(0.55f, 0.4f, 0.24f), Twine = new Rgb(0.8f, 0.72f, 0.5f);

        public void Register(ContentDb db)
        {
            db.Skill(Skill, "Hunter", new Rgb(0.55f, 0.5f, 0.3f), new Icon(22f)  // a paw print
                .Rect(7f, 10f, 8f, 7f, new Rgb(0.45f, 0.3f, 0.18f)).Rect(5f, 5f, 3f, 3f, new Rgb(0.45f, 0.3f, 0.18f)).Rect(9f, 3f, 3f, 3f, new Rgb(0.45f, 0.3f, 0.18f))
                .Rect(14f, 5f, 3f, 3f, new Rgb(0.45f, 0.3f, 0.18f)));

            var snare = db.Item(BirdSnare, "Bird snare", "A simple bird catcher. Lay it on the ground.", 6, Twine);
            snare.TrapKind = "snare";
            snare.Icon = new Icon().Turned(18f, 6f, 3f, 28f, Wood, 20f).Rect(10f, 24f, 18f, 2f, Twine).Rect(10f, 24f, 2f, 8f, Twine).Rect(26f, 24f, 2f, 8f, Twine);
            snare.GroundModel = SnareModel();
            var box = db.Item(BoxTrap, "Box trap", "For trapping small animals. Lay it on the ground.", 38, Wood);
            box.TrapKind = "box";
            box.Requires.Add(new SkillReq(Skill, 27));
            box.Icon = new Icon().Rect(8f, 14f, 24f, 16f, Wood).Rect(8f, 14f, 24f, 2f, Wood * 0.75f).Rect(18f, 18f, 4f, 4f, Looks.Dark);
            box.GroundModel = BoxModel(false);

            CookingModule.Meat(db, RawBirdMeat, "Raw bird meat", "I need to cook this first.", 2, new Rgb(0.92f, 0.7f, 0.62f), 0);
            CookingModule.Meat(db, RoastBirdMeat, "Roast bird meat", "A nicely roasted bird.", 10, new Rgb(0.72f, 0.45f, 0.22f), 6);
            CookingModule.Cook(db, "cook_bird_meat", RawBirdMeat, RoastBirdMeat, CookingModule.BurntMeat, "bird meat", 11, 62.5, 44);

            var ferret = db.Item(Ferret, "Ferret", "This could be useful if the rabbits round here ever got out of hand.", 120, new Rgb(0.85f, 0.78f, 0.62f));
            ferret.Icon = new Icon().Rect(6f, 18f, 24f, 8f, new Rgb(0.85f, 0.78f, 0.62f)).Rect(28f, 16f, 6f, 6f, new Rgb(0.85f, 0.78f, 0.62f))
                .Rect(30f, 17f, 2f, 2f, Looks.Dark).Rect(2f, 20f, 6f, 3f, new Rgb(0.6f, 0.5f, 0.38f));
            ferret.GroundModel = FerretModel();
            Chin(db, Chinchompa, "Chinchompa", "Grey, fluffy and highly explosive.", 60, new Rgb(0.62f, 0.62f, 0.6f));
            Chin(db, RedChinchompa, "Red chinchompa", "A carnivorous chinchompa. Handle with care.", 140, new Rgb(0.75f, 0.25f, 0.18f));

            // Birds, in snares (OSRS levels, xp).
            Creature(db, "crimson_swift", "Crimson swift", "snare", 1, 34, 140, 250, Bird(new Rgb(0.85f, 0.15f, 0.12f)), BirdLoot(), Biome.Meadow, Biome.Woodland);
            Creature(db, "copper_longtail", "Copper longtail", "snare", 9, 61, 110, 240, Bird(new Rgb(0.75f, 0.45f, 0.2f)), BirdLoot(), Biome.Woodland, Biome.Highlands);
            Creature(db, "cerulean_twitch", "Cerulean twitch", "snare", 11, 64.7, 100, 240, Bird(new Rgb(0.2f, 0.45f, 0.85f)), BirdLoot(), Biome.Marsh, Biome.Meadow);
            Creature(db, "tropical_wagtail", "Tropical wagtail", "snare", 19, 95, 80, 230, Bird(new Rgb(0.95f, 0.8f, 0.2f)), BirdLoot(), Biome.Meadow, Biome.Marsh, Biome.Wasteland);
            // Animals, in box traps.
            Creature(db, "ferret_animal", "Ferret", "box", 27, 115, 90, 230, FerretModel(), new[] { new Ingredient(Ferret, 1) }, Biome.Woodland, Biome.Meadow);
            Creature(db, "chinchompa_animal", "Chinchompa", "box", 53, 198.4, 60, 220, ChinModel(new Rgb(0.62f, 0.62f, 0.6f)),
                new[] { new Ingredient(Chinchompa, 1) }, Biome.Highlands, Biome.Woodland);
            Creature(db, "red_chinchompa_animal", "Red chinchompa", "box", 63, 265, 50, 210, ChinModel(new Rgb(0.75f, 0.25f, 0.18f)),
                new[] { new Ingredient(RedChinchompa, 1) }, Biome.Wasteland, Biome.Highlands);
        }

        static Ingredient[] BirdLoot() => new[] { new Ingredient(FishingModule.Feathers, 5), new Ingredient(PrayerModule.Bones, 1), new Ingredient(RawBirdMeat, 1) };

        static void Creature(ContentDb db, string id, string name, string trap, int level, double xp, int low, int high, Model model, Ingredient[] loot,
            params Biome[] biomes)
        {
            var c = new CreatureDef { Id = id, Name = name, Trap = trap, Level = level, Xp = xp, LowChance = low, HighChance = high, Model = model };
            c.Biomes.AddRange(biomes);
            c.Loot.AddRange(loot);
            if (trap == "snare")
                c.ExtraLootMax = 5;
            db.Add(c);
        }

        static void Chin(ContentDb db, string id, string name, string examine, int value, Rgb c)
        {
            var item = db.Item(id, name, examine, value, c);
            item.Stackable = true;
            item.Icon = new Icon().Rect(12f, 14f, 16f, 14f, c).Rect(13f, 10f, 4f, 5f, c).Rect(23f, 10f, 4f, 5f, c)
                .Rect(15f, 18f, 2f, 2f, Looks.Dark).Rect(23f, 18f, 2f, 2f, Looks.Dark).Rect(19f, 22f, 2f, 2f, new Rgb(0.9f, 0.6f, 0.6f));
            item.GroundModel = ChinModel(c);
        }

        /// A little bird sitting in the snare.
        static Model Bird(Rgb c) => new Model()
            .Sphere(0f, 0.12f, 0f, 0.16f, 0.13f, 0.2f, c).Sphere(0f, 0.2f, 0.09f, 0.1f, 0.1f, 0.1f, c * 1.1f)
            .Cube(0f, 0.2f, 0.16f, 0.03f, 0.03f, 0.06f, new Rgb(0.95f, 0.75f, 0.2f)).Cube(0f, 0.13f, -0.13f, 0.1f, 0.03f, 0.1f, c * 0.8f);

        static Model FerretModel() => new Model()
            .Capsule(0f, 0.08f, 0f, 0.12f, 0.2f, 0.12f, new Rgb(0.85f, 0.78f, 0.62f)).Turn(90f, 0f, 0f)
            .Sphere(0f, 0.1f, 0.22f, 0.11f, 0.1f, 0.12f, new Rgb(0.85f, 0.78f, 0.62f))
            .Cube(0f, 0.06f, -0.27f, 0.05f, 0.05f, 0.14f, new Rgb(0.6f, 0.5f, 0.38f));

        static Model ChinModel(Rgb c) => new Model()
            .Sphere(0f, 0.12f, 0f, 0.24f, 0.22f, 0.24f, c)
            .Sphere(-0.07f, 0.24f, 0.02f, 0.06f, 0.08f, 0.04f, c).Sphere(0.07f, 0.24f, 0.02f, 0.06f, 0.08f, 0.04f, c)
            .Cube(-0.05f, 0.15f, 0.11f, 0.03f, 0.03f, 0.02f, Looks.Dark).Cube(0.05f, 0.15f, 0.11f, 0.03f, 0.03f, 0.02f, Looks.Dark);

        /// A bent stick and a noose of twine.
        public static Model SnareModel() => new Model()
            .Cylinder(0f, 0.25f, -0.12f, 0.03f, 0.25f, 0.03f, Wood).Turn(-25f, 0f, 0f)
            .Cylinder(0f, 0.01f, 0.05f, 0.25f, 0.01f, 0.25f, Twine)
            .Cylinder(0f, 0.05f, 0.18f, 0.02f, 0.05f, 0.02f, Wood);

        /// A wooden box, its lid propped open (or shut, with something inside).
        public static Model BoxModel(bool shut)
        {
            var m = new Model()
                .Cube(0f, 0.12f, 0f, 0.36f, 0.04f, 0.55f, Wood)
                .Cube(-0.17f, 0.18f, 0f, 0.03f, 0.14f, 0.55f, Wood).Cube(0.17f, 0.18f, 0f, 0.03f, 0.14f, 0.55f, Wood)
                .Cube(0f, 0.18f, -0.26f, 0.36f, 0.14f, 0.03f, Wood);
            if (shut)
                m.Cube(0f, 0.27f, 0f, 0.38f, 0.04f, 0.57f, Wood * 0.85f).Cube(0f, 0.18f, 0.27f, 0.36f, 0.14f, 0.03f, Wood * 0.9f);
            else
                m.Cube(0f, 0.36f, 0.05f, 0.38f, 0.04f, 0.5f, Wood * 0.85f).Turn(-30f, 0f, 0f).Cylinder(0f, 0.2f, 0.25f, 0.02f, 0.12f, 0.02f, Twine);
            return m;
        }
    }
}
