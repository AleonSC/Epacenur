using Epacenur.Core;

namespace Epacenur.Content
{
    /// Firemaking: light logs with a tinderbox. Fires are stations you cook on (see CookingModule).
    public sealed class FiremakingModule : IContentModule
    {
        public const string Skill = "firemaking", Tinderbox = "tinderbox", Ashes = "ashes", Fire = "fire";

        static void Burnable(ContentDb db, string logs, int level, double xp, float minSeconds)
        {
            const double perLevel = 191.0 / 42.0;  // from 64/256 to certain in 42 levels
            int low = (int)System.Math.Round(64 - perLevel * (level - 1)), high = (int)System.Math.Round(low + perLevel * 98);
            db.Add(new BurnableDef
            {
                LogItemId = logs, Level = level, Xp = xp, LowChance = low, HighChance = high,
                MinBurnSeconds = minSeconds, MaxBurnSeconds = minSeconds * 2f,
            });
        }

        public void Register(ContentDb db)
        {
            var orange = new Rgb(0.95f, 0.3f, 0.05f);
            var yellow = new Rgb(1f, 0.75f, 0.15f);
            db.Skill(Skill, "Firemaking", new Rgb(1f, 0.55f, 0.1f), new Icon(22f)  // a flame
                .Rect(5f, 10f, 12f, 9f, orange).Rect(7f, 5f, 8f, 6f, orange).Rect(9f, 1f, 4f, 5f, orange)
                .Rect(8f, 12f, 6f, 6f, yellow).Rect(10f, 8f, 3f, 5f, yellow).Rect(4f, 19f, 14f, 2f, Rgb.Wood));

            var grey = new Rgb(0.5f, 0.5f, 0.52f);
            var tinderbox = db.Item(Tinderbox, "Tinderbox", "Useful for lighting a fire.", 1, grey);
            tinderbox.ToolType = "tinderbox";
            tinderbox.ToolTier = 1;
            tinderbox.Icon = new Icon().Rect(9f, 15f, 22f, 14f, grey).Rect(9f, 13f, 22f, 4f, grey * 0.7f).Rect(27f, 8f, 4f, 5f, new Rgb(1f, 0.55f, 0.1f));
            tinderbox.GroundModel = new Model().Cube(0f, 0.04f, 0f, 0.18f, 0.08f, 0.12f, grey).Cube(0f, 0.085f, 0f, 0.19f, 0.015f, 0.13f, grey * 0.7f);

            var ashGrey = new Rgb(0.45f, 0.45f, 0.45f);
            var ashes = db.Item(Ashes, "Ashes", "A heap of ashes.", 2, ashGrey);
            ashes.Icon = new Icon().Rect(9f, 24f, 22f, 7f, ashGrey).Rect(13f, 19f, 14f, 6f, ashGrey * 1.2f).Rect(17f, 15f, 6f, 5f, ashGrey * 1.35f);
            ashes.GroundModel = new Model().Sphere(0f, 0.02f, 0f, 0.3f, 0.08f, 0.26f, ashGrey).Sphere(0.05f, 0.04f, 0.03f, 0.14f, 0.07f, 0.12f, ashGrey * 0.8f);

            // Logs: about 1 in 4 attempts at the level needed, certain 42 levels later (normal logs: 65 / 513,
            // as before). Better logs burn longer. OSRS levels and xp.
            Burnable(db, WoodcuttingModule.Logs, 1, 40, 60f);
            Burnable(db, WoodcuttingModule.OakLogs, 15, 60, 70f);
            Burnable(db, WoodcuttingModule.WillowLogs, 30, 90, 80f);
            Burnable(db, WoodcuttingModule.MapleLogs, 45, 135, 90f);
            Burnable(db, WoodcuttingModule.YewLogs, 60, 202.5, 110f);

            // A lit fire is a station (the game draws its flickering flames itself).
            db.Add(new StationDef { Id = Fire, Name = "Fire", Action = "Cook", Examine = "A hot fire.", Kind = StationKind.Make });
        }
    }
}
