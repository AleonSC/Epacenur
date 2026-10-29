using Epacenur.Core;

namespace Epacenur.Content
{
    /// Cooking: raw fish on a fire become food (or burn). Food heals (Health).
    public sealed class CookingModule : IContentModule
    {
        public const string Skill = "cooking", Shrimps = "shrimps", Anchovies = "anchovies", BurntShrimp = "burnt_shrimp", BurntFish = "burnt_fish";

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

            // On a fire: 30 xp each; both stop burning at level 34, as in RuneScape.
            Cook(db, "cook_shrimps", FishingModule.RawShrimps, Shrimps, BurntShrimp, "shrimps");
            Cook(db, "cook_anchovies", FishingModule.RawAnchovies, Anchovies, BurntFish, "anchovies");
        }

        static void Fish(ContentDb db, string id, string name, string examine, int value, Rgb colour, int count, int heals)
        {
            var item = db.Item(id, name, examine, value, colour);
            item.Icon = Looks.FishIcon(colour, count);
            item.GroundModel = Looks.Fish(colour, count);
            item.Heals = heals;
        }

        static void Cook(ContentDb db, string id, string raw, string cooked, string burnt, string what)
        {
            db.Add(new RecipeDef
            {
                Id = id, Name = db.Item(cooked).Name, Station = FiremakingModule.Fire, Skill = Skill, Level = 1, Xp = 30,
                Inputs = new[] { new Ingredient(raw, 1) }, Product = cooked, FailProduct = burnt, LowChance = 80, HighChance = 605,
                Ticks = 4,
                SuccessMessage = "You successfully cook some " + what + ".",
                FailMessage = "You accidentally burn the " + what + ".",
            });
        }
    }
}
