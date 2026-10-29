using Epacenur.Core;

namespace Epacenur.Content
{
    /// Fishing: net fishing spots for shrimps and anchovies.
    public sealed class FishingModule : IContentModule
    {
        public const string Skill = "fishing", SmallFishingNet = "small_fishing_net", RawShrimps = "raw_shrimps",
            RawAnchovies = "raw_anchovies", NetSpot = "net_fishing_spot";

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
            db.Add(new NodeDef
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
        }
    }
}
