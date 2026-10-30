using Epacenur.Core;

namespace Epacenur.Content
{
    /// Woodcutting: trees from normal to yew, their logs, and a hatchet of each metal. Better trees grow
    /// deeper in the wilderness (each tree's Spawns), and each has a biome it likes.
    public sealed class WoodcuttingModule : IContentModule
    {
        public const string Skill = "woodcutting", Logs = "logs", OakLogs = "oak_logs", WillowLogs = "willow_logs",
            MapleLogs = "maple_logs", YewLogs = "yew_logs", BronzeHatchet = "bronze_hatchet",
            Tree = "tree", Birch = "birch_tree", Pine = "pine_tree", DeadTree = "dead_tree", Blossom = "blossom_tree", Oak = "oak_tree", Willow = "willow_tree", Maple = "maple_tree", Yew = "yew_tree";

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

            // Trees (OSRS levels, xp, and chances with a bronze hatchet), and where they grow: the wilderness level
            // from which they grow anywhere, and a biome where they're commoner (from a lower level).
            var trunk = new Rgb(0.42f, 0.28f, 0.15f);
            TreeNode(db, Tree, "Tree", "One of the most common trees in the land.", Logs, "logs", 1, 25, 64, 200, 3, 6, 12f,
                trunk, new Rgb(0.22f, 0.55f, 0.2f), 1f, 0, 6, Biome.Meadow, 0);
            Kin(db, Birch, "Birch", "A slender tree with pale bark.", new Rgb(0.86f, 0.84f, 0.78f), new Rgb(0.48f, 0.7f, 0.3f), 0.9f,
                new SpawnRule { Weight = 4, Biome = Biome.Meadow }, new SpawnRule { Weight = 3.5, Biome = Biome.Woodland });
            Kin(db, Pine, "Pine tree", "A tall pine, smelling of resin.", new Rgb(0.42f, 0.25f, 0.15f), new Rgb(0.14f, 0.36f, 0.2f), 1.15f,
                new SpawnRule { Weight = 12, Biome = Biome.Highlands }, new SpawnRule { Weight = 1.5, Biome = Biome.Woodland });
            Kin(db, Blossom, "Blossom tree", "Covered in pink blossom.", new Rgb(0.38f, 0.26f, 0.2f), new Rgb(0.95f, 0.62f, 0.75f), 0.95f,
                new SpawnRule { Weight = 2.5, Biome = Biome.Meadow });
            Kin(db, DeadTree, "Dead tree", "This tree has long since died.", new Rgb(0.36f, 0.31f, 0.26f), new Rgb(0.36f, 0.31f, 0.26f), 1f,
                new SpawnRule { Weight = 5, Biome = Biome.Wasteland }, new SpawnRule { Weight = 0.6, Biome = Biome.Highlands });
            TreeNode(db, Oak, "Oak", "A beautiful old oak.", OakLogs, "oak logs", 15, 37.5, 32, 100, 3, 8, 14f,
                new Rgb(0.4f, 0.3f, 0.18f), new Rgb(0.27f, 0.5f, 0.18f), 1.25f, 0, 2.5, Biome.Woodland, 0);
            TreeNode(db, Willow, "Willow", "A droopy tree.", WillowLogs, "willow logs", 30, 67.5, 16, 50, 3, 10, 14f,
                new Rgb(0.45f, 0.38f, 0.25f), new Rgb(0.45f, 0.62f, 0.3f), 1.1f, 3, 2, Biome.Marsh, 0);
            TreeNode(db, Maple, "Maple tree", "It's a maple tree.", MapleLogs, "maple logs", 45, 100, 8, 25, 3, 10, 35f,
                new Rgb(0.4f, 0.24f, 0.13f), new Rgb(0.82f, 0.38f, 0.12f), 1.2f, 10, 1.5, Biome.Woodland, 6);
            TreeNode(db, Yew, "Yew", "A tough-looking yew tree.", YewLogs, "yew logs", 60, 175, 4, 12, 4, 12, 60f,
                new Rgb(0.32f, 0.2f, 0.12f), new Rgb(0.12f, 0.32f, 0.14f), 1.4f, 20, 0.8, Biome.Wasteland, 18);
        }

        static void LogItem(ContentDb db, string id, string name, string examine, int value, Rgb bark, Rgb end)
        {
            var logs = db.Item(id, name, examine, value, bark);
            logs.Icon = Looks.LogIcon(bark, end);
            logs.GroundModel = Looks.LogModel(bark, end);
        }

        /// A tree: a trunk flaring into roots, with a leafy crown (which shakes when chopped), then a stump for a while.
        static NodeDef TreeNode(ContentDb db, string id, string name, string examine, string logs, string logName, int level, double xp,
            int low, int high, int minLives, int maxLives, float respawn, Rgb trunk, Rgb leaf, float size, int minDanger, double weight,
            Biome likes, int likesFrom)
        {
            var crown = id == Oak ? OakCrown(leaf) : id == Willow ? WillowCrown(leaf) : id == Maple ? MapleCrown(leaf)
                : id == Yew ? YewCrown(leaf) : id == Birch ? BirchCrown(leaf) : id == Pine ? PineCrown(leaf) : id == Blossom ? BlossomCrown(leaf)
                : id == DeadTree ? DeadCrown(leaf) : RoundCrown(leaf);
            float h = 2.5f * size;
            var node = new NodeDef
            {
                Id = id, Name = name, Action = "Chop down", Examine = examine,
                EmptyName = "Tree stump", EmptyExamine = "This tree has been cut down.",
                Skill = Skill, ToolType = "hatchet",
                Yields = new[] { new NodeYield { ItemId = logs, Level = level, Xp = xp, LowChance = low, HighChance = high, Message = "You get some " + logName + "." } },
                AttemptTicks = 4, FirstPersonTicks = 2, MinLives = minLives, MaxLives = maxLives, RespawnSeconds = respawn, Look = NodeLook.Tree,
                Model = Trunk(trunk, size, h, id == Oak).Child(crown, 0f, h, 0f, scale: size, name: "crown"),
                EmptyModel = new Model()
                    .Include(Roots(trunk, size))
                    .Cylinder(0f, 0.18f, 0f, 0.42f * size, 0.18f, 0.42f * size, trunk)
                    .Cylinder(0f, 0.365f, 0f, 0.36f * size, 0.01f, 0.36f * size, new Rgb(0.78f, 0.64f, 0.42f))
                    .Cylinder(0f, 0.367f, 0f, 0.2f * size, 0.01f, 0.2f * size, new Rgb(0.68f, 0.54f, 0.34f)),  // a growth ring
                StartMessage = "You swing your axe at the tree.",
                NeedToolMessage = "You need a hatchet you can use to chop down this tree.",
                FullMessage = "Your inventory is too full to hold any more logs.",
                LevelMessage = "You need a Woodcutting level of {0} to chop down this tree.",
            };
            node.Spawns.Add(new SpawnRule { On = Terrain.Tree, MinDanger = minDanger, Weight = weight });
            node.Spawns.Add(new SpawnRule { On = Terrain.Tree, MinDanger = likesFrom, Weight = weight * 1.5, Biome = likes });
            db.Add(node);
            return node;
        }

        /// The everyday trees' kin, for logs like a plain tree's: birches in the meadows and woods, pines up in
        /// the highlands, blossom trees in the meadows, and dead trees in the wasteland. Each grows only where it
        /// belongs.
        static void Kin(ContentDb db, string id, string name, string examine, Rgb trunk, Rgb leaf, float size, params SpawnRule[] where)
        {
            var node = TreeNode(db, id, name, examine, Logs, "logs", 1, 25, 64, 200, 3, 6, 12f, trunk, leaf, size, 0, 0, Biome.Meadow, 0);
            node.Spawns.Clear();
            foreach (var w in where)
            {
                w.On = Terrain.Tree;
                node.Spawns.Add(w);
            }
        }

        /// Four roots spreading out from the foot of the trunk.
        static Model Roots(Rgb bark, float size)
        {
            var m = new Model();
            for (int i = 0; i < 4; i++)
            {
                // Each leans in from the ground outside the trunk to the trunk's side.
                float a = i * 90f + 20f, r = 0.24f * size, rad = a * (float)System.Math.PI / 180f;
                m.Capsule((float)System.Math.Sin(rad) * r, 0.1f, (float)System.Math.Cos(rad) * r, 0.2f * size, 0.2f * size, 0.2f * size, bark * 0.9f)
                 .Turn(-60f, a, 0f);
            }
            return m;
        }

        /// A trunk narrowing as it rises, with darker bark streaks and a branch or two reaching into the crown.
        static Model Trunk(Rgb bark, float size, float h, bool branchy)
        {
            float r = 0.36f * size;  // the trunk's width
            var m = new Model().Include(Roots(bark, size))
                .Cylinder(0f, h * 0.3f, 0f, r * 1.12f, h * 0.3f, r * 1.12f, bark)
                .Cylinder(0f, h * 0.75f, 0f, r * 0.85f, h * 0.25f, r * 0.85f, bark * 1.05f)
                .Cube(r * 0.4f, h * 0.3f, r * 0.33f, 0.05f, h * 0.45f, 0.05f, bark * 0.75f)   // darker streaks in the bark
                .Cube(-r * 0.3f, h * 0.72f, -r * 0.25f, 0.05f, h * 0.3f, 0.05f, bark * 0.75f)
                .Cylinder(r * 1.2f, h * 0.82f, 0f, 0.09f * size, 0.45f * size, 0.09f * size, bark).Turn(0f, 0f, -50f)
                .Cylinder(-r, h * 0.9f, r * 0.6f, 0.08f * size, 0.4f * size, 0.08f * size, bark).Turn(30f, 0f, 45f);
            if (branchy)
                m.Cylinder(0f, h * 0.7f, -r * 1.3f, 0.1f * size, 0.55f * size, 0.1f * size, bark).Turn(-55f, 0f, 0f);
            return m;
        }

        /// Clumps of leaves round a point: darker underneath, lighter on top, so the crown has some depth.
        static Model Clumps(Model m, Rgb leaf, float cx, float cy, float cz, float radius, int count, float clump, int seed)
        {
            for (int i = 0; i < count; i++)
            {
                float a = (i * 2.4f + seed) % 6.283f, y = ((i * 7 + seed) % 5) / 4f - 0.5f;  // spread round and up and down
                float rr = radius * (1f - y * y * 0.6f);
                float s = clump * (0.8f + ((i * 3 + seed) % 4) * 0.12f);
                m.Sphere(cx + (float)System.Math.Cos(a) * rr, cy + y * radius * 0.8f, cz + (float)System.Math.Sin(a) * rr, s, s * 0.85f, s, leaf * (0.82f + (y + 0.5f) * 0.35f));
            }
            return m;
        }

        /// A plain tree's crown: a round mass of leaves.
        static Model RoundCrown(Rgb leaf)
        {
            var m = new Model().Sphere(0f, 0.3f, 0f, 1.8f, 1.5f, 1.8f, leaf * 0.85f);
            Clumps(m, leaf, 0f, 0.3f, 0f, 0.75f, 8, 1.05f, 1);
            return m.Sphere(0.1f, 0.95f, -0.05f, 1.1f, 0.85f, 1.1f, leaf * 1.15f);
        }

        /// An oak's: broad and lumpy, wider than it is tall.
        static Model OakCrown(Rgb leaf)
        {
            var m = new Model().Sphere(0f, 0.25f, 0f, 2.4f, 1.4f, 2.3f, leaf * 0.82f);
            Clumps(m, leaf, 0f, 0.25f, 0f, 1.05f, 11, 1.05f, 3);
            return m.Sphere(-0.2f, 0.8f, 0.15f, 1.3f, 0.8f, 1.3f, leaf * 1.12f).Sphere(0.55f, 0.7f, -0.35f, 0.9f, 0.6f, 0.9f, leaf * 1.18f);
        }

        /// A willow's: a dome with long fronds hanging all round it, nearly to the ground.
        static Model WillowCrown(Rgb leaf)
        {
            var m = new Model().Sphere(0f, 0.3f, 0f, 2f, 1.3f, 2f, leaf * 0.9f).Sphere(0f, 0.75f, 0f, 1.3f, 0.8f, 1.3f, leaf * 1.1f);
            for (int i = 0; i < 14; i++)
            {
                float a = i * 6.283f / 14f, r = 0.88f + (i % 2) * 0.12f, len = 0.75f + (i % 3) * 0.18f;
                m.Capsule((float)System.Math.Cos(a) * r, -len + 0.1f, (float)System.Math.Sin(a) * r, 0.22f, len, 0.22f, leaf * (i % 2 == 0 ? 0.95f : 0.8f))
                 .Turn((float)System.Math.Sin(a) * 8f, 0f, -(float)System.Math.Cos(a) * 8f);  // hanging slightly outwards
            }
            return m;
        }

        /// A maple's: a tall crown in autumn reds, oranges and golds.
        static Model MapleCrown(Rgb leaf)
        {
            var red = new Rgb(0.72f, 0.2f, 0.1f);
            var gold = new Rgb(0.92f, 0.62f, 0.15f);
            var m = new Model().Sphere(0f, 0.45f, 0f, 1.8f, 1.9f, 1.8f, leaf * 0.85f);
            Clumps(m, leaf, 0f, 0.4f, 0f, 0.7f, 5, 1f, 2);
            Clumps(m, red, 0f, 0.55f, 0f, 0.72f, 4, 0.9f, 5);
            Clumps(m, gold, 0f, 0.7f, 0f, 0.55f, 3, 0.8f, 4);
            return m.Sphere(0f, 1.3f, 0f, 0.9f, 0.8f, 0.9f, leaf * 1.1f);
        }

        /// A birch's: light and airy, small clumps of leaves spread high and loose.
        static Model BirchCrown(Rgb leaf)
        {
            var m = new Model().Sphere(0f, 0.6f, 0f, 1.2f, 1.7f, 1.2f, leaf * 0.85f);
            Clumps(m, leaf, 0f, 0.7f, 0f, 0.6f, 9, 0.7f, 6);
            return m.Sphere(0.1f, 1.55f, 0f, 0.6f, 0.6f, 0.6f, leaf * 1.15f);
        }

        /// A pine's: tiers of dark needles, each narrower than the one below, to a sharp top.
        static Model PineCrown(Rgb leaf)
        {
            var m = new Model();
            for (int k = 0; k < 5; k++)
            {
                float w = 2.2f - k * 0.42f;
                m.Cylinder(0f, -0.5f + k * 0.62f, 0f, w, 0.16f, w, leaf * (0.82f + k * 0.07f));
                m.Cylinder(0f, -0.32f + k * 0.62f, 0f, w * 0.72f, 0.14f, w * 0.72f, leaf * (0.9f + k * 0.07f));
            }
            return m.Cylinder(0f, 2.75f, 0f, 0.22f, 0.35f, 0.22f, leaf * 1.2f);
        }

        /// A blossom tree's: a round crown thick with pink flowers, a few green leaves showing through.
        static Model BlossomCrown(Rgb blossom)
        {
            var m = new Model().Sphere(0f, 0.3f, 0f, 1.9f, 1.4f, 1.9f, new Rgb(0.4f, 0.55f, 0.28f));
            Clumps(m, blossom, 0f, 0.35f, 0f, 0.78f, 10, 0.95f, 2);
            Clumps(m, new Rgb(0.98f, 0.9f, 0.93f), 0f, 0.5f, 0f, 0.7f, 4, 0.55f, 7);
            return m.Sphere(0f, 0.95f, 0f, 1.1f, 0.75f, 1.1f, blossom * 1.05f);
        }

        /// A dead tree's: bare, crooked branches.
        static Model DeadCrown(Rgb bark)
        {
            var m = new Model();
            for (int i = 0; i < 6; i++)
            {
                float a = i * 60f + (i % 2) * 17f, rad = a * (float)System.Math.PI / 180f;
                float len = 0.6f + (i % 3) * 0.2f, lean = 40f + (i % 3) * 12f;
                m.Cylinder((float)System.Math.Sin(rad) * 0.35f, -0.3f + (i % 3) * 0.3f, (float)System.Math.Cos(rad) * 0.35f, 0.09f, len, 0.09f, bark * (0.9f + (i % 2) * 0.1f))
                 .Turn(lean * (float)System.Math.Cos(rad), 0f, -lean * (float)System.Math.Sin(rad));
                m.Cylinder((float)System.Math.Sin(rad) * 0.9f, 0.35f + (i % 3) * 0.3f, (float)System.Math.Cos(rad) * 0.9f, 0.05f, 0.3f, 0.05f, bark * 0.85f)
                 .Turn(-20f * (float)System.Math.Cos(rad), 0f, 20f * (float)System.Math.Sin(rad));
            }
            return m.Cylinder(0f, 0.2f, 0f, 0.12f, 0.6f, 0.12f, bark);
        }

        /// A yew's: dark tiers of needles, narrowing to a point.
        static Model YewCrown(Rgb leaf)
        {
            var m = new Model();
            for (int k = 0; k < 4; k++)
            {
                float w = 2.3f - k * 0.5f;
                m.Sphere(0f, -0.3f + k * 0.62f, 0f, w, 0.75f, w, leaf * (0.85f + k * 0.1f));
                for (int i = 0; i < 5; i++)  // shaggy edges
                {
                    float a = i * 1.2566f + k * 0.6f;
                    m.Sphere((float)System.Math.Cos(a) * w * 0.38f, -0.38f + k * 0.62f, (float)System.Math.Sin(a) * w * 0.38f, w * 0.38f, 0.45f, w * 0.38f, leaf * 0.8f);
                }
            }
            return m.Sphere(0f, 2.2f, 0f, 0.4f, 0.6f, 0.4f, leaf * 1.2f);
        }
    }
}
