using Epacenur.Core;
using UnityEngine;

namespace Epacenur.Runtime
{
    /// Builds the pieces of the world between and round the towns: town walls (stone round a city, a wooden
    /// palisade round a town) with their gates, wells, signposts, and the ruins and landmarks out in the wilds.
    /// Each is made of models (Core: Model), and a town's walls are merged into one mesh.
    public static class Landmarks
    {
        static readonly Rgb Stone = new Rgb(0.6f, 0.58f, 0.54f), Plinth = new Rgb(0.45f, 0.43f, 0.4f), Slate = new Rgb(0.32f, 0.33f, 0.38f);
        static readonly Rgb Wood = new Rgb(0.45f, 0.31f, 0.18f), DarkWood = new Rgb(0.32f, 0.22f, 0.13f), Banner = new Rgb(0.6f, 0.12f, 0.12f);
        static readonly Rgb Moss = new Rgb(0.3f, 0.42f, 0.2f), Pale = new Rgb(0.78f, 0.76f, 0.7f), WellWater = new Rgb(0.12f, 0.22f, 0.36f);

        // ---- Town walls ----

        // A stretch of stone wall along x (one tile), sunk into the ground so it meets it on a slope, with a
        // merlon on top.
        static readonly Model StoneWall = new Model()
            .Cube(0f, 1.1f, 0f, 1.04f, 3f, 0.8f, Stone)
            .Cube(0f, 0.05f, 0f, 1.06f, 0.3f, 0.92f, Plinth)
            .Cube(-0.24f, 2.82f, 0f, 0.48f, 0.44f, 0.86f, Stone * 0.95f);

        static readonly Model StoneTower = new Model()
            .Cylinder(0f, 1.6f, 0f, 1.7f, 2f, 1.7f, Stone)
            .Cylinder(0f, 3.72f, 0f, 1.9f, 0.14f, 1.9f, Stone * 0.92f)
            .Cylinder(0f, 4.15f, 0f, 1.55f, 0.3f, 1.55f, Slate)
            .Cylinder(0f, 4.65f, 0f, 1.05f, 0.25f, 1.05f, Slate * 0.9f)
            .Cylinder(0f, 5.05f, 0f, 0.5f, 0.2f, 0.5f, Slate * 0.85f)
            .Cylinder(0f, 5.8f, 0f, 0.04f, 0.6f, 0.04f, DarkWood)
            .Cube(0.28f, 6.1f, 0f, 0.5f, 0.3f, 0.02f, Banner);

        // Beside a gate: a taller pillar, and over the gate itself a lintel with a banner hanging from it.
        static readonly Model StonePillar = new Model()
            .Cube(0f, 1.7f, 0f, 1.2f, 4.2f, 1.1f, Stone * 0.97f)
            .Cube(0f, 3.92f, 0f, 1.3f, 0.24f, 1.2f, Plinth);

        static readonly Model StoneLintel = new Model()
            .Cube(0f, 3.35f, 0f, 1.08f, 0.9f, 0.9f, Stone * 0.97f)
            .Cube(0f, 2.72f, 0.47f, 0.5f, 0.6f, 0.03f, Banner)
            .Cube(0f, 2.72f, -0.47f, 0.5f, 0.6f, 0.03f, Banner);

        // A palisade: three sharpened stakes and a rail.
        static readonly Model Palisade = Stakes();

        static Model Stakes()
        {
            var m = new Model();
            for (int i = -1; i <= 1; i++)
            {
                float x = i * 0.34f;
                var c = i == 0 ? Wood : Wood * 0.9f;
                m.Cylinder(x, 0.85f, 0f, 0.28f, 1.25f, 0.28f, c);
                m.Cube(x, 2.15f, 0f, 0.17f, 0.17f, 0.17f, c * 0.9f).Turn(45f, 0f, 45f);
            }
            return m.Cube(0f, 1.3f, -0.16f, 1.04f, 0.12f, 0.06f, DarkWood);
        }

        static readonly Model PalisadePost = new Model()
            .Cylinder(0f, 1.1f, 0f, 0.48f, 1.5f, 0.48f, Wood * 0.85f)
            .Cube(0f, 2.65f, 0f, 0.3f, 0.3f, 0.3f, Wood * 0.8f).Turn(45f, 0f, 45f);

        static readonly Model PalisadeGatePost = new Model()
            .Cylinder(0f, 1.3f, 0f, 0.42f, 1.7f, 0.42f, Wood * 0.85f)
            .Cube(0f, 3.05f, 0f, 0.28f, 0.28f, 0.28f, Wood * 0.8f).Turn(45f, 0f, 45f);

        static readonly Model PalisadeLintel = new Model()
            .Cube(0f, 2.9f, 0f, 1.6f, 0.24f, 0.3f, DarkWood);

        /// The walls round a town: a stretch of wall on each tile of the ring, towers (or thick posts) at the
        /// corners, and at each gate two pillars with a lintel over the road.
        public static void TownWalls(World world, RegionPlan plan)
        {
            var a = plan.Walls;
            if (a.Width <= 0)
                return;
            var ow = world.Overworld;
            var root = new GameObject("Town walls").transform;
            root.SetParent(world.BuildRoot, false);
            bool stone = plan.StoneWalls;
            int x0 = a.X, z0 = a.Z, x1 = a.XMax - 1, z1 = a.ZMax - 1;
            for (int x = x0; x <= x1; x++)
                for (int z = z0; z <= z1; z++)
                {
                    if (x != x0 && x != x1 && z != z0 && z != z1)
                        continue;
                    var t = new Tile(x, z);
                    bool corner = (x == x0 || x == x1) && (z == z0 || z == z1);
                    bool alongX = z == z0 || z == z1;
                    var at = new Vector3(x, (float)ow.Elevation(t), z);
                    float yaw = alongX ? 0f : 90f;
                    if (plan.Gates.Contains(t))
                    {
                        Piece(stone ? StoneLintel : PalisadeLintel, root, at, yaw);
                        continue;
                    }
                    if (ow.At(t) != Terrain.TownWall)
                        continue;
                    bool byGate = alongX ? plan.Gates.Contains(new Tile(x - 1, z)) || plan.Gates.Contains(new Tile(x + 1, z))
                        : plan.Gates.Contains(new Tile(x, z - 1)) || plan.Gates.Contains(new Tile(x, z + 1));
                    var model = corner ? (stone ? StoneTower : PalisadePost) : byGate ? (stone ? StonePillar : PalisadeGatePost) : stone ? StoneWall : Palisade;
                    // Outward-facing: the palisade's rail and the merlons alternate, so turn every other tile round.
                    Piece(model, root, at, yaw + ((x + z) % 2 == 0 ? 0f : 180f));
                }
            ModelBuilder.Merge(root, true);
        }

        static void Piece(Model model, Transform root, Vector3 at, float yaw)
        {
            var t = ModelBuilder.Build(model, root, "Piece");
            t.localPosition = at;
            t.localRotation = Quaternion.Euler(0f, yaw, 0f);
        }

        // ---- Wells and signposts ----

        public static readonly Model WellLook = new Model()
            .Cylinder(0f, 0.35f, 0f, 1f, 0.4f, 1f, Stone)
            .Cylinder(0f, 0.76f, 0f, 1.06f, 0.04f, 1.06f, Plinth)
            .Cylinder(0f, 0.72f, 0f, 0.8f, 0.02f, 0.8f, WellWater)
            .Cube(-0.46f, 1.25f, 0f, 0.1f, 1.7f, 0.1f, Wood)
            .Cube(0.46f, 1.25f, 0f, 0.1f, 1.7f, 0.1f, Wood)
            .Cylinder(0f, 1.8f, 0f, 0.07f, 0.48f, 0.07f, DarkWood).Turn(0f, 0f, 90f)
            .Cube(0f, 1.55f, 0f, 0.02f, 0.45f, 0.02f, new Rgb(0.7f, 0.62f, 0.45f))
            .Cylinder(0f, 1.3f, 0f, 0.2f, 0.12f, 0.2f, DarkWood)
            .Cube(0f, 2.28f, -0.27f, 1.25f, 0.06f, 0.66f, Slate).Turn(35f, 0f, 0f)
            .Cube(0f, 2.28f, 0.27f, 1.25f, 0.06f, 0.66f, Slate).Turn(-35f, 0f, 0f);

        /// A signpost with an arm pointing out along +x (the way the road goes) and one back.
        public static readonly Model SignpostLook = new Model()
            .Cylinder(0f, 1.05f, 0f, 0.12f, 1.05f, 0.12f, Wood)
            .Cube(0f, 2.12f, 0f, 0.16f, 0.06f, 0.16f, DarkWood)
            .Cube(0.38f, 1.78f, 0f, 0.76f, 0.2f, 0.05f, new Rgb(0.72f, 0.58f, 0.38f))
            .Cube(0.77f, 1.78f, 0f, 0.14f, 0.14f, 0.05f, new Rgb(0.72f, 0.58f, 0.38f)).Turn(0f, 0f, 45f)
            .Cube(-0.38f, 1.46f, 0f, 0.76f, 0.2f, 0.05f, new Rgb(0.66f, 0.52f, 0.34f))
            .Cube(-0.77f, 1.46f, 0f, 0.14f, 0.14f, 0.05f, new Rgb(0.66f, 0.52f, 0.34f)).Turn(0f, 0f, 45f);

        // ---- Ruins and landmarks ----

        static readonly Model Watchtower = WatchtowerLook();

        static Model WatchtowerLook()
        {
            var m = new Model()
                .Cylinder(0f, 1.4f, 0f, 2.7f, 1.8f, 2.7f, Stone * 0.92f)
                .Cylinder(0f, 0.02f, 0f, 3.1f, 0.25f, 3.1f, Plinth)
                .Cube(0f, 0.9f, -1.3f, 0.8f, 1.6f, 0.2f, new Rgb(0.12f, 0.11f, 0.1f));  // a dark doorway
            // A broken top: blocks standing up round the rim to different heights.
            for (int i = 0; i < 9; i++)
            {
                float ang = i * 40f * Mathf.Deg2Rad, r = 1.15f, h = 0.4f + ((i * 7) % 5) * 0.35f;
                m.Cube(Mathf.Cos(ang) * r, 3.2f + h / 2f, Mathf.Sin(ang) * r, 0.7f, h, 0.5f, Stone * (0.88f + (i % 3) * 0.05f)).Turn(0f, -i * 40f, 0f);
            }
            // Moss and fallen stones.
            m.Sphere(0.9f, 2.4f, 1f, 0.7f, 0.9f, 0.4f, Moss);
            m.Sphere(-1.2f, 1.2f, 0.6f, 0.5f, 0.8f, 0.5f, Moss * 0.9f);
            m.Cube(1.55f, 0.15f, -1.2f, 0.5f, 0.35f, 0.4f, Stone * 0.85f).Turn(0f, 30f, 10f);
            m.Cube(-1.5f, 0.12f, -1.35f, 0.4f, 0.3f, 0.45f, Stone * 0.8f).Turn(0f, -20f, 0f);
            return m;
        }

        // A ruined cottage's walls: a stretch along x at three heights (they've fallen to different heights).
        static readonly Model[] CottageWall =
        {
            new Model().Cube(0f, 0.3f, 0f, 1.02f, 1f, 0.45f, Stone * 0.9f),
            new Model().Cube(0f, 0.65f, 0f, 1.02f, 1.7f, 0.45f, Stone * 0.9f).Cube(0.2f, 1.55f, 0f, 0.5f, 0.2f, 0.47f, Moss),
            new Model().Cube(0f, 1f, 0f, 1.02f, 2.4f, 0.45f, Stone * 0.9f).Cube(0f, 1.4f, 0f, 0.4f, 0.5f, 0.5f, new Rgb(0.12f, 0.11f, 0.1f)),
        };

        static readonly Model CottageRubble = new Model()
            .Cube(0f, 0.12f, 0f, 0.5f, 0.3f, 0.4f, Stone * 0.85f).Turn(0f, 25f, 8f)
            .Cube(0.3f, 0.1f, 0.25f, 0.35f, 0.25f, 0.3f, Stone * 0.8f).Turn(0f, -15f, 0f)
            .Cylinder(-0.2f, 0.12f, -0.1f, 0.14f, 0.7f, 0.14f, DarkWood).Turn(0f, 0f, 80f);  // a fallen beam

        static readonly Model Statue = new Model()
            .Cube(0f, 0.35f, 0f, 0.95f, 0.9f, 0.95f, Plinth)
            .Cube(0f, 0.85f, 0f, 1f, 0.1f, 1f, Stone)
            .Cube(-0.12f, 1.3f, 0f, 0.16f, 0.8f, 0.2f, Pale)
            .Cube(0.12f, 1.3f, 0f, 0.16f, 0.8f, 0.2f, Pale)
            .Capsule(0f, 2.05f, 0f, 0.5f, 0.5f, 0.32f, Pale)
            .Sphere(0f, 2.72f, 0f, 0.3f, 0.32f, 0.3f, Pale)
            .Capsule(-0.36f, 2.05f, 0f, 0.13f, 0.32f, 0.13f, Pale)
            .Capsule(0.36f, 2.5f, 0f, 0.13f, 0.32f, 0.13f, Pale).Turn(0f, 0f, 20f)
            .Cube(0.48f, 3.1f, 0f, 0.06f, 1.1f, 0.12f, Pale * 0.95f)   // a sword held up
            .Cube(0.48f, 2.6f, 0f, 0.3f, 0.06f, 0.12f, Pale * 0.95f)
            .Sphere(0.35f, 0.88f, 0.35f, 0.3f, 0.12f, 0.3f, Moss);

        static readonly Model Obelisk = new Model()
            .Cube(0f, 0.15f, 0f, 0.98f, 0.4f, 0.98f, Plinth)
            .Cube(0f, 1.7f, 0f, 0.6f, 2.8f, 0.6f, new Rgb(0.3f, 0.3f, 0.34f))
            .Cube(0f, 3.25f, 0f, 0.45f, 0.3f, 0.45f, new Rgb(0.28f, 0.28f, 0.32f))
            .Cube(0f, 3.5f, 0f, 0.28f, 0.28f, 0.28f, new Rgb(0.85f, 0.72f, 0.3f)).Turn(45f, 0f, 45f)
            .Cube(0f, 1.9f, 0.305f, 0.12f, 1.6f, 0.01f, new Rgb(0.4f, 0.6f, 0.95f))   // runes down each face
            .Cube(0f, 1.9f, -0.305f, 0.12f, 1.6f, 0.01f, new Rgb(0.4f, 0.6f, 0.95f))
            .Cube(0.305f, 1.9f, 0f, 0.01f, 1.6f, 0.12f, new Rgb(0.4f, 0.6f, 0.95f))
            .Cube(-0.305f, 1.9f, 0f, 0.01f, 1.6f, 0.12f, new Rgb(0.4f, 0.6f, 0.95f));

        static readonly Model Cairn = new Model()
            .Sphere(0f, 0.15f, 0f, 1f, 0.5f, 0.95f, Stone * 0.85f)
            .Sphere(0.05f, 0.5f, -0.03f, 0.75f, 0.4f, 0.7f, Stone * 0.95f)
            .Sphere(-0.03f, 0.82f, 0.02f, 0.55f, 0.32f, 0.5f, Stone * 0.9f)
            .Sphere(0f, 1.08f, 0f, 0.36f, 0.24f, 0.36f, Stone)
            .Sphere(0.02f, 1.27f, 0f, 0.2f, 0.16f, 0.2f, Stone * 1.05f);

        /// A region's ruin or landmark, with its name over it.
        public static void Landmark(World world, RegionPlan plan)
        {
            var ow = world.Overworld;
            var area = plan.LandmarkArea;
            var root = new GameObject(plan.Landmark.ToString()).transform;
            root.SetParent(world.BuildRoot, false);
            float turn = (int)(ow.Hash(plan.RX, plan.RZ, 22) * 4) * 90f;
            var mid = new Vector3(area.X + (area.Width - 1) / 2f, 0f, area.Z + (area.Height - 1) / 2f);
            mid.y = (float)ow.Elevation(mid.x, mid.z);
            float top = 3f;
            switch (plan.Landmark)
            {
                case LandmarkKind.Watchtower: Piece(Watchtower, root, mid, turn); top = 5.4f; break;
                case LandmarkKind.Statue: Piece(Statue, root, mid, turn); top = 4.3f; break;
                case LandmarkKind.Obelisk: Piece(Obelisk, root, mid, turn); top = 4.3f; break;
                case LandmarkKind.Cairn: Piece(Cairn, root, mid, turn); top = 2.2f; break;
                case LandmarkKind.Cottage:
                    for (int x = area.X; x < area.XMax; x++)
                        for (int z = area.Z; z < area.ZMax; z++)
                        {
                            var t = new Tile(x, z);
                            var at = new Vector3(x, (float)ow.Elevation(t), z);
                            if (plan.Blocked.Contains(t))
                            {
                                bool alongX = z == area.Z || z == area.ZMax - 1;
                                int h = (int)(ow.Hash(x, z, 23) * 3);
                                Piece(CottageWall[h], root, at, alongX ? 0f : 90f);
                            }
                            else if (ow.Hash(x, z, 24) < 0.3)
                                Piece(CottageRubble, root, at, (float)ow.Hash(x, z, 25) * 360f);
                        }
                    top = 3.2f;
                    break;
            }
            ModelBuilder.Merge(root, true);
            world.AddLabel(mid + Vector3.up * top, LandmarkName(ow, plan), new Color(0.85f, 0.85f, 0.75f));
        }

        /// What a landmark is called (on its label and the world map).
        public static string LandmarkName(Overworld ow, RegionPlan plan)
        {
            string[] heroes = { "Aldric", "Maren", "Torvald", "Isolde", "Beren", "Halvar", "Elspeth", "Corwin" };
            string hero = heroes[(int)(ow.Hash(plan.RX, plan.RZ, 26) * heroes.Length)];
            switch (plan.Landmark)
            {
                case LandmarkKind.Watchtower: return "Old watchtower";
                case LandmarkKind.Cottage: return "Ruined cottage";
                case LandmarkKind.Statue: return "Statue of " + hero;
                case LandmarkKind.Obelisk: return "Rune obelisk";
                case LandmarkKind.Cairn: return hero + "'s cairn";
                default: return null;
            }
        }
    }
}
