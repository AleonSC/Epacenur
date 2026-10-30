using Epacenur.Core;

namespace Epacenur.Content
{
    /// Farming: every town has four patches by its roads, two for herbs and two allotments. Use a seed on an
    /// empty patch to plant it; it grows in real time, even while you're logged out, and once grown you pick
    /// the herbs (grimy, for Herblore) or dig up the crop with a spade, several times over. OSRS levels and xp.
    /// Seeds come from the general store (potatoes), farmers' pockets, seed stalls and monsters' drops.
    public sealed class FarmingModule : IContentModule
    {
        public const string Skill = Farming.Skill, Spade = "spade", PotatoSeed = "potato_seed", GuamSeed = "guam_seed",
            HerbPatch = "herb_patch", Allotment = "allotment", SeedStall = "seed_stall";

        static readonly Rgb SeedBrown = new Rgb(0.55f, 0.42f, 0.25f);

        public void Register(ContentDb db)
        {
            db.Skill(Skill, "Farming", new Rgb(0.35f, 0.6f, 0.25f), new Icon(22f)  // a sprout
                .Rect(4f, 16f, 14f, 4f, new Rgb(0.45f, 0.32f, 0.18f)).Rect(10f, 7f, 2f, 9f, new Rgb(0.3f, 0.65f, 0.25f))
                .Turned(4f, 6f, 7f, 4f, new Rgb(0.35f, 0.7f, 0.3f), -25f).Turned(11f, 4f, 7f, 4f, new Rgb(0.35f, 0.7f, 0.3f), 25f));

            var steel = Rgb.Steel;
            var spade = db.Item(Spade, "Spade", "A slightly muddy spade.", 3, steel);
            spade.ToolType = "spade";
            spade.ToolTier = 1;
            spade.Icon = new Icon().Turned(18.5f, 4f, 3f, 22f, Rgb.Wood, 30f).Turned(10f, 23f, 10f, 12f, steel, 30f);
            spade.HeldModel = new Model().Cube(0f, 0f, 0.3f, 0.04f, 0.04f, 0.7f, Rgb.Wood).Cube(0f, 0f, 0.72f, 0.2f, 0.02f, 0.24f, steel);
            spade.GroundModel = Looks.OnGround(spade.HeldModel);

            // Herbs (their grimy herbs come from Herblore).
            Herb(db, "guam", "Guam", 9, 11, 12.5, new Rgb(0.35f, 0.65f, 0.25f));
            Herb(db, "tarromin", "Tarromin", 19, 16, 18, new Rgb(0.55f, 0.6f, 0.2f));
            Herb(db, "harralander", "Harralander", 26, 21.5, 24, new Rgb(0.65f, 0.5f, 0.2f));
            Herb(db, "ranarr", "Ranarr", 32, 27, 30.5, new Rgb(0.25f, 0.55f, 0.45f));
            Herb(db, "irit", "Irit", 44, 43, 48.5, new Rgb(0.6f, 0.65f, 0.35f));
            Herb(db, "kwuarm", "Kwuarm", 56, 54.5, 69, new Rgb(0.45f, 0.4f, 0.25f));
            // Allotments: crops to eat or sell.
            Crop(db, "potato", "Potato", 1, 8, 9, 40, 1, 4, new Rgb(0.75f, 0.6f, 0.38f));
            Crop(db, "onion", "Onion", 5, 9.5, 10.5, 40, 1, 4, new Rgb(0.88f, 0.75f, 0.5f));
            Crop(db, "cabbage", "Cabbage", 7, 10, 11.5, 40, 1, 5, new Rgb(0.45f, 0.7f, 0.3f));
            Crop(db, "tomato", "Tomato", 12, 12.5, 14, 40, 2, 8, new Rgb(0.88f, 0.2f, 0.15f));
            Crop(db, "sweetcorn", "Sweetcorn", 20, 17, 19, 60, 3, 12, new Rgb(0.95f, 0.85f, 0.3f));
            Crop(db, "strawberry", "Strawberry", 31, 26, 29, 60, 5, 30, new Rgb(0.9f, 0.15f, 0.25f));

            // Where seeds come from besides the general store: farmers' pockets, a seed stall, goblins.
            db.Npc(ThievingModule.Farmer).Pickpocket.Loot.Add(PotatoSeed, 1, 4, 6).Add(GuamSeed, 1, 1, 1);
            db.Npc(CombatModule.Goblin).Drops.Add(PotatoSeed, 1, 3, 2).Add(GuamSeed, 1, 1, 1);
            db.Npc(CombatModule.Bandit).Drops.Add("ranarr_seed", 1, 1, 1).Add("tarromin_seed", 1, 2, 2);
            db.Npc(CombatModule.HillGiant).Drops.Add("irit_seed", 1, 1, 1).Add("kwuarm_seed", 1, 1, 1);
            ThievingModule.Stall(db, SeedStall, "Seed stall", new Rgb(0.5f, 0.65f, 0.3f), SeedBrown, 27, 10f, 100, int.MaxValue, 2,
                Seed("ranarr_seed", 15), Seed("harralander_seed", 40), Seed("strawberry_seed", 60), Seed("tomato_seed", 120), Seed("onion_seed", 160),
                Seed(PotatoSeed, 256));
        }

        static NodeYield Seed(string id, int chance) => new NodeYield
        {
            ItemId = id, Level = 27, Xp = 10, LowChance = chance, HighChance = chance, Message = "You steal some seeds.",
        };

        static ItemDef SeedItem(ContentDb db, string id, string name, int value, Rgb colour, bool herb)
        {
            var seed = db.Item(id, name, herb ? "Plant this in a herb patch." : "Plant this in an allotment.", value, SeedBrown);
            seed.Stackable = true;
            seed.Icon = new Icon().Rect(13f, 14f, 4f, 4f, SeedBrown).Rect(21f, 12f, 4f, 4f, SeedBrown).Rect(17f, 20f, 4f, 4f, SeedBrown)
                .Rect(24f, 21f, 4f, 4f, SeedBrown).Rect(11f, 23f, 4f, 4f, SeedBrown).Rect(19f, 13f, 2f, 2f, colour);
            seed.GroundModel = new Model().Sphere(0f, 0.03f, 0f, 0.12f, 0.04f, 0.12f, SeedBrown);
            return seed;
        }

        static void Herb(ContentDb db, string herb, string name, int level, double plantXp, double harvestXp, Rgb colour)
        {
            SeedItem(db, herb + "_seed", name + " seed", 5 + level * 2, colour, true);
            db.Add(new SeedDef
            {
                SeedId = herb + "_seed", Name = name, Patch = PatchKind.Herb, Level = level, PlantXp = plantXp, HarvestXp = harvestXp,
                Product = "grimy_" + herb, MinYield = 3, MaxYield = 8, GrowMinutes = 80, Colour = colour,
            });
        }

        static void Crop(ContentDb db, string crop, string name, int level, double plantXp, double harvestXp, int minutes, int heals, int value, Rgb colour)
        {
            SeedItem(db, crop + "_seed", name + " seed", 1 + level, colour, false);
            var item = db.Item(crop, name, "Freshly grown " + name.ToLowerInvariant() + ".", value, colour);
            item.Heals = heals;
            item.Icon = new Icon().Rect(12f, 13f, 16f, 15f, colour).Rect(14f, 11f, 12f, 3f, colour * 1.1f).Rect(18f, 8f, 3f, 4f, new Rgb(0.3f, 0.6f, 0.25f));
            item.GroundModel = new Model().Sphere(0f, 0.08f, 0f, 0.2f, 0.16f, 0.2f, colour);
            db.Add(new SeedDef
            {
                SeedId = crop + "_seed", Name = name, Patch = PatchKind.Allotment, Level = level, PlantXp = plantXp, HarvestXp = harvestXp,
                Product = crop, MinYield = 3, MaxYield = 10, GrowMinutes = minutes, Colour = colour,
            });
        }
    }
}
