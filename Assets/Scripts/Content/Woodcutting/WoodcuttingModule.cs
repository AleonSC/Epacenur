using Epacenur.Core;

namespace Epacenur.Content
{
    /// Woodcutting: trees, logs and the bronze hatchet.
    public sealed class WoodcuttingModule : IContentModule
    {
        public const string Skill = "woodcutting", Logs = "logs", BronzeHatchet = "bronze_hatchet", Tree = "tree";

        /// A small axe pointing forward (+z) from the grip.
        public static Model Axe(Rgb head) => new Model()
            .Cube(0f, 0f, 0.25f, 0.05f, 0.05f, 0.6f, Looks.Handle)
            .Cube(0f, 0.08f, 0.5f, 0.03f, 0.2f, 0.14f, head);

        public void Register(ContentDb db)
        {
            db.Skill(Skill, "Woodcutting", new Rgb(0.35f, 0.75f, 0.3f), new Icon(22f)  // a hatchet
                .Turned(9.75f, 1f, 2.5f, 20f, Rgb.Wood, -40f).Turned(9.75f, 1f, 8f, 6f, Rgb.Steel, -40f));

            var bark = new Rgb(0.55f, 0.36f, 0.18f);
            var logs = db.Item(Logs, "Logs", "A number of wooden logs.", 4, bark);
            var end = new Rgb(0.85f, 0.7f, 0.45f);
            var logIcon = new Icon();
            var logModel = new Model();
            for (int k = 0; k < 3; k++)
            {
                float y = 9f + k * 8f, x = 6f + (k == 1 ? 4f : 0f);
                logIcon.Rect(x, y, 24f, 7f, k == 1 ? bark * 0.85f : bark).Rect(x + 24f, y, 4f, 7f, end);
                float lx = -0.08f + k * 0.08f, ly = 0.06f + (k == 1 ? 0.08f : 0f), lz = k * 0.04f;
                logModel.Cylinder(lx, ly, lz, 0.1f, 0.2f, 0.1f, k == 1 ? bark * 0.85f : bark).Turn(0f, 0f, 90f)
                        .Cylinder(lx - 0.2f, ly, lz, 0.09f, 0.004f, 0.09f, end).Turn(0f, 0f, 90f);  // the cut end
            }
            logs.Icon = logIcon;
            logs.GroundModel = logModel;

            var hatchet = db.Item(BronzeHatchet, "Bronze hatchet", "A woodcutter's hatchet.", 16, Looks.Bronze);
            hatchet.ToolType = "hatchet";
            hatchet.ToolTier = 1;
            hatchet.Icon = new Icon()
                .Turned(18.5f, 7f, 3f, 27f, Rgb.Wood, -35f).Turned(18.5f, 6f, 11f, 4f, Looks.Bronze, -35f)
                .Turned(18.5f, 10f, 11f, 4f, Looks.Bronze, -35f).Turned(27f, 5f, 3f, 10f, Looks.Bronze * 1.25f, -35f);
            hatchet.HeldModel = Axe(new Rgb(0.6f, 0.62f, 0.66f));
            hatchet.GroundModel = Looks.OnGround(hatchet.HeldModel);

            // A tree: trunk and a leafy crown (which shakes when chopped). 3-6 logs, then a stump for 12 s.
            var trunk = new Rgb(0.42f, 0.28f, 0.15f);
            var leaf = new Rgb(0.22f, 0.55f, 0.2f);
            var crown = new Model()
                .Sphere(0f, 0.35f, 0f, 1.9f, 1.6f, 1.9f, leaf)
                .Sphere(0.45f, -0.1f, 0.3f, 1.2f, 1.1f, 1.2f, leaf * 0.82f)
                .Sphere(-0.4f, 0.05f, -0.35f, 1.25f, 1.1f, 1.25f, leaf * 0.82f);
            db.Add(new NodeDef
            {
                Id = Tree, Name = "Tree", Action = "Chop down", Examine = "One of the most common trees in the land.",
                EmptyName = "Tree stump", EmptyExamine = "This tree has been cut down.",
                Skill = Skill, ToolType = "hatchet",
                Yields = new[] { new NodeYield { ItemId = Logs, Level = 1, Xp = 25, LowChance = 64, HighChance = 200, Message = "You get some logs." } },
                AttemptTicks = 4, FirstPersonTicks = 2, MinLives = 3, MaxLives = 6, RespawnSeconds = 12f, Look = NodeLook.Tree,
                Model = new Model().Cylinder(0f, 1.25f, 0f, 0.36f, 1.25f, 0.36f, trunk).Child(crown, 0f, 2.5f, 0f, name: "crown"),
                EmptyModel = new Model()
                    .Cylinder(0f, 0.18f, 0f, 0.42f, 0.18f, 0.42f, trunk)
                    .Cylinder(0f, 0.365f, 0f, 0.36f, 0.01f, 0.36f, new Rgb(0.78f, 0.64f, 0.42f)),
                StartMessage = "You swing your axe at the tree.",
                NeedToolMessage = "You need a hatchet to chop down this tree.",
                FullMessage = "Your inventory is too full to hold any more logs.",
                LevelMessage = "You need a Woodcutting level of {0} to chop down this tree.",
            });
        }
    }
}
