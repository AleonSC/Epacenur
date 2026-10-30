using Epacenur.Core;

namespace Epacenur.Content
{
    /// Woodcutting: trees from normal to yew, their logs, and a hatchet of each metal. Better trees grow
    /// further from the start (each tree's Spawns).
    public sealed class WoodcuttingModule : IContentModule
    {
        public const string Skill = "woodcutting", Logs = "logs", OakLogs = "oak_logs", WillowLogs = "willow_logs",
            MapleLogs = "maple_logs", YewLogs = "yew_logs", BronzeHatchet = "bronze_hatchet",
            Tree = "tree", Oak = "oak_tree", Willow = "willow_tree", Maple = "maple_tree", Yew = "yew_tree";

        /// A small axe pointing forward (+z) from the grip.
        public static Model Axe(Rgb head) => new Model()
            .Cube(0f, 0f, 0.25f, 0.05f, 0.05f, 0.6f, Looks.Handle)
            .Cube(0f, 0.08f, 0.5f, 0.03f, 0.2f, 0.14f, head);

        public void Register(ContentDb db)
        {
            db.Skill(Skill, "Woodcutting", new Rgb(0.35f, 0.75f, 0.3f), new Icon(22f)  // a hatchet
                .Turned(9.75f, 1f, 2.5f, 20f, Rgb.Wood, -40f).Turned(9.75f, 1f, 8f, 6f, Rgb.Steel, -40f));

            var end = new Rgb(0.85f, 0.7f, 0.45f);
            LogItem(db, Logs, "Logs", "A number of wooden logs.", 4, new Rgb(0.55f, 0.36f, 0.18f), end);
            LogItem(db, OakLogs, "Oak logs", "Logs cut from an oak tree.", 20, new Rgb(0.6f, 0.45f, 0.25f), new Rgb(0.88f, 0.76f, 0.52f));
            LogItem(db, WillowLogs, "Willow logs", "Logs cut from a willow tree.", 16, new Rgb(0.5f, 0.42f, 0.28f), new Rgb(0.85f, 0.8f, 0.6f));
            LogItem(db, MapleLogs, "Maple logs", "Logs cut from a maple tree.", 45, new Rgb(0.62f, 0.32f, 0.16f), new Rgb(0.9f, 0.68f, 0.45f));
            LogItem(db, YewLogs, "Yew logs", "Logs cut from a yew tree.", 160, new Rgb(0.4f, 0.24f, 0.14f), new Rgb(0.78f, 0.55f, 0.38f));

            // A hatchet of each metal: better ones chop faster (OSRS levels to use them).
            foreach (var m in Metals.All)
            {
                var hatchet = db.Item(m.ItemId("hatchet"), m.Name + " hatchet", "A woodcutter's hatchet.", m.Value(16), m.Colour);
                hatchet.ToolType = "hatchet";
                hatchet.ToolTier = m.Tier;
                if (m.ToolLevel > 1)
                    hatchet.Requires.Add(new SkillReq(Skill, m.ToolLevel));
                hatchet.Icon = new Icon()
                    .Turned(18.5f, 7f, 3f, 27f, Rgb.Wood, -35f).Turned(18.5f, 6f, 11f, 4f, m.Colour, -35f)
                    .Turned(18.5f, 10f, 11f, 4f, m.Colour, -35f).Turned(27f, 5f, 3f, 10f, m.Colour * 1.25f, -35f);
                hatchet.HeldModel = Axe(m == Metals.Bronze ? new Rgb(0.6f, 0.62f, 0.66f) : m.Colour);
                hatchet.GroundModel = Looks.OnGround(hatchet.HeldModel);
            }

            // Trees (OSRS levels, xp, and chances with a bronze hatchet), and where they grow.
            var trunk = new Rgb(0.42f, 0.28f, 0.15f);
            TreeNode(db, Tree, "Tree", "One of the most common trees in the land.", Logs, "logs", 1, 25, 64, 200, 3, 6, 12f,
                trunk, new Rgb(0.22f, 0.55f, 0.2f), 1f, 0, 6);
            TreeNode(db, Oak, "Oak", "A beautiful old oak.", OakLogs, "oak logs", 15, 37.5, 32, 100, 3, 8, 14f,
                new Rgb(0.4f, 0.3f, 0.18f), new Rgb(0.27f, 0.5f, 0.18f), 1.25f, 30, 2.5);
            TreeNode(db, Willow, "Willow", "A droopy tree.", WillowLogs, "willow logs", 30, 67.5, 16, 50, 3, 10, 14f,
                new Rgb(0.45f, 0.38f, 0.25f), new Rgb(0.45f, 0.62f, 0.3f), 1.1f, 120, 2);
            TreeNode(db, Maple, "Maple tree", "It's a maple tree.", MapleLogs, "maple logs", 45, 100, 8, 25, 3, 10, 35f,
                new Rgb(0.4f, 0.24f, 0.13f), new Rgb(0.82f, 0.38f, 0.12f), 1.2f, 250, 1.5);
            TreeNode(db, Yew, "Yew", "A tough-looking yew tree.", YewLogs, "yew logs", 60, 175, 4, 12, 4, 12, 60f,
                new Rgb(0.32f, 0.2f, 0.12f), new Rgb(0.12f, 0.32f, 0.14f), 1.4f, 500, 0.8);
        }

        static void LogItem(ContentDb db, string id, string name, string examine, int value, Rgb bark, Rgb end)
        {
            var logs = db.Item(id, name, examine, value, bark);
            logs.Icon = Looks.LogIcon(bark, end);
            logs.GroundModel = Looks.LogModel(bark, end);
        }

        /// A tree: a trunk and a leafy crown (which shakes when chopped), then a stump for a while.
        static void TreeNode(ContentDb db, string id, string name, string examine, string logs, string logName, int level, double xp,
            int low, int high, int minLives, int maxLives, float respawn, Rgb trunk, Rgb leaf, float size, int minDistance, double weight)
        {
            var crown = new Model()
                .Sphere(0f, 0.35f, 0f, 1.9f, 1.6f, 1.9f, leaf)
                .Sphere(0.45f, -0.1f, 0.3f, 1.2f, 1.1f, 1.2f, leaf * 0.82f)
                .Sphere(-0.4f, 0.05f, -0.35f, 1.25f, 1.1f, 1.25f, leaf * 0.82f);
            if (id == Willow)  // long drooping fronds
                crown.Cylinder(0.6f, -0.6f, 0.2f, 0.5f, 0.7f, 0.5f, leaf * 0.9f).Cylinder(-0.55f, -0.6f, -0.25f, 0.5f, 0.7f, 0.5f, leaf * 0.9f)
                     .Cylinder(0f, -0.6f, 0.6f, 0.45f, 0.65f, 0.45f, leaf * 0.85f);
            float h = 2.5f * size;
            var node = new NodeDef
            {
                Id = id, Name = name, Action = "Chop down", Examine = examine,
                EmptyName = "Tree stump", EmptyExamine = "This tree has been cut down.",
                Skill = Skill, ToolType = "hatchet",
                Yields = new[] { new NodeYield { ItemId = logs, Level = level, Xp = xp, LowChance = low, HighChance = high, Message = "You get some " + logName + "." } },
                AttemptTicks = 4, FirstPersonTicks = 2, MinLives = minLives, MaxLives = maxLives, RespawnSeconds = respawn, Look = NodeLook.Tree,
                Model = new Model().Cylinder(0f, h / 2f, 0f, 0.36f * size, h / 2f, 0.36f * size, trunk).Child(crown, 0f, h, 0f, scale: size, name: "crown"),
                EmptyModel = new Model()
                    .Cylinder(0f, 0.18f, 0f, 0.42f * size, 0.18f, 0.42f * size, trunk)
                    .Cylinder(0f, 0.365f, 0f, 0.36f * size, 0.01f, 0.36f * size, new Rgb(0.78f, 0.64f, 0.42f)),
                StartMessage = "You swing your axe at the tree.",
                NeedToolMessage = "You need a hatchet you can use to chop down this tree.",
                FullMessage = "Your inventory is too full to hold any more logs.",
                LevelMessage = "You need a Woodcutting level of {0} to chop down this tree.",
            };
            node.Spawns.Add(new SpawnRule { On = Terrain.Tree, MinDistance = minDistance, Weight = weight });
            db.Add(node);
        }
    }
}
