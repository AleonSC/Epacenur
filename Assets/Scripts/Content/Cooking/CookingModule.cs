using Epacenur.Core;

namespace Epacenur.Content
{
    /// Cooking: raw fish and meat on a fire become food (or burn). Food heals (Health). Better fish need
    /// higher levels, heal more, and stop burning later (OSRS levels and xp).
    public sealed class CookingModule : IContentModule
    {
        public const string Skill = "cooking", Shrimps = "shrimps", Anchovies = "anchovies", BurntShrimp = "burnt_shrimp", BurntFish = "burnt_fish",
            Trout = "trout", Salmon = "salmon", Tuna = "tuna", Lobster = "lobster", Swordfish = "swordfish",
            RawBeef = "raw_beef", RawChicken = "raw_chicken", CookedMeat = "cooked_meat", CookedChicken = "cooked_chicken", BurntMeat = "burnt_meat";

        public void Register(ContentDb db)
        {
            var white = new Rgb(0.95f, 0.95f, 0.92f);
            db.Skill(Skill, "Cooking", new Rgb(0.7f, 0.4f, 0.85f), new Icon(22f)  // a chef's hat
                .Rect(3f, 3f, 7f, 7f, white).Rect(8f, 1f, 7f, 7f, white).Rect(13f, 3f, 6f, 7f, white)
                .Rect(5f, 8f, 12f, 8f, white).Rect(5f, 16f, 12f, 3f, new Rgb(0.75f, 0.75f, 0.72f)));

            Fish(db, Shrimps, "Shrimps", "Some nicely cooked shrimps.", 5, new Rgb(1f, 0.45f, 0.3f), 2, heals: 3);
            Fish(db, Anchovies, "Anchovies", "Some nicely cooked anchovies.", 15, new Rgb(0.62f, 0.48f, 0.36f), 3, heals: 1);
            Fish(db, BurntShrimp, "Burnt shrimp", "Oops!", 1, new Rgb(0.2f, 0.15f, 0.12f), 2, heals: 0);
            Fish(db, BurntFish, "Burnt fish", "Oops!", 1, new Rgb(0.17f, 0.15f, 0.14f), 3, heals: 0);

            Fish(db, Trout, "Trout", "Some nicely cooked trout.", 20, new Rgb(0.82f, 0.62f, 0.45f), 1, heals: 7);
            Fish(db, Salmon, "Salmon", "Some nicely cooked salmon.", 30, new Rgb(0.95f, 0.55f, 0.4f), 1, heals: 9);
            Fish(db, Tuna, "Tuna", "Wow, this is a big fish.", 70, new Rgb(0.75f, 0.55f, 0.45f), 1, heals: 10);
            Fish(db, Lobster, "Lobster", "This looks tricky to eat.", 120, new Rgb(0.9f, 0.3f, 0.2f), 1, heals: 12);
            Fish(db, Swordfish, "Swordfish", "I'd better be careful eating this!", 200, new Rgb(0.8f, 0.65f, 0.6f), 1, heals: 14);

            // Meat from cows and chickens.
            Meat(db, RawBeef, "Raw beef", "I need to cook this first.", 1, new Rgb(0.8f, 0.35f, 0.35f), 0);
            Meat(db, RawChicken, "Raw chicken", "I need to cook this first.", 1, new Rgb(0.95f, 0.8f, 0.72f), 0);
            Meat(db, CookedMeat, "Cooked meat", "Mmm, this looks tasty.", 4, new Rgb(0.6f, 0.38f, 0.22f), 3);
            Meat(db, CookedChicken, "Cooked chicken", "Mmm, this looks tasty.", 4, new Rgb(0.78f, 0.55f, 0.3f), 3);
            Meat(db, BurntMeat, "Burnt meat", "Oops!", 1, new Rgb(0.18f, 0.14f, 0.12f), 0);

            // On a fire: about 1 in 3 at the level needed, never burnt from the stop level (OSRS levels).
            Cook(db, "cook_shrimps", FishingModule.RawShrimps, Shrimps, BurntShrimp, "shrimps", 1, 30, 34);
            Cook(db, "cook_anchovies", FishingModule.RawAnchovies, Anchovies, BurntFish, "anchovies", 1, 30, 34);
            Cook(db, "cook_beef", RawBeef, CookedMeat, BurntMeat, "meat", 1, 30, 34);
            Cook(db, "cook_chicken", RawChicken, CookedChicken, BurntMeat, "chicken", 1, 30, 34);
            Cook(db, "cook_trout", FishingModule.RawTrout, Trout, BurntFish, "trout", 15, 70, 50);
            Cook(db, "cook_salmon", FishingModule.RawSalmon, Salmon, BurntFish, "salmon", 25, 90, 58);
            Cook(db, "cook_tuna", FishingModule.RawTuna, Tuna, BurntFish, "tuna", 30, 100, 64);
            Cook(db, "cook_lobster", FishingModule.RawLobster, Lobster, BurntFish, "lobster", 40, 120, 74);
            Cook(db, "cook_swordfish", FishingModule.RawSwordfish, Swordfish, BurntFish, "swordfish", 45, 140, 86);
        }

        public static void Meat(ContentDb db, string id, string name, string examine, int value, Rgb colour, int heals)
        {
            var item = db.Item(id, name, examine, value, colour);
            item.Icon = Looks.MeatIcon(colour);
            item.GroundModel = Looks.MeatModel(colour);
            item.Heals = heals;
        }

        static void Fish(ContentDb db, string id, string name, string examine, int value, Rgb colour, int count, int heals)
        {
            var item = db.Item(id, name, examine, value, colour);
            item.Icon = Looks.FishIcon(colour, count);
            item.GroundModel = Looks.Fish(colour, count);
            item.Heals = heals;
        }

        /// A recipe on a fire. The chance goes in a straight line from about 30% at `level` to certain at `stopBurning`
        /// (shrimps: 80 / 603).
        public static void Cook(ContentDb db, string id, string raw, string cooked, string burnt, string what, int level, double xp, int stopBurning)
        {
            double perLevel = (256.0 - 80.0) / (stopBurning - level);
            int low = (int)System.Math.Round(80 - perLevel * (level - 1)), high = (int)System.Math.Round(low + perLevel * 98);
            db.Add(new RecipeDef
            {
                Id = id, Name = db.Item(cooked).Name, Station = FiremakingModule.Fire, Skill = Skill, Level = level, Xp = xp,
                Inputs = new[] { new Ingredient(raw, 1) }, Product = cooked, FailProduct = burnt, LowChance = low, HighChance = high,
                Ticks = 4,
                SuccessMessage = "You successfully cook some " + what + ".",
                FailMessage = "You accidentally burn the " + what + ".",
            });
        }
    }
}
