using System;
using Epacenur.Core;
using UnityEngine;

namespace Epacenur.Runtime
{
    /// Builds a town's buildings (Core: Building) from primitives: walls of timber frame, stone, brick or planks
    /// on a stone plinth, at the town's ground height; windows with shutters; a doorway with its door swung
    /// open; a pitched roof of thatch, tiles, slate or shingles with gables; and touches for each kind (a chapel's
    /// bell tower and tall coloured windows, a bank's columns, a shop's striped awning, a smithy's chimney).
    /// Each building's look comes from its place, so it's the same every time; a town leans to one roof.
    /// Roofs hide while you're inside (tile mode).
    public static class TownBuildings
    {
        public enum Walls { Timber, Stone, Brick, Planks }
        public enum Roofing { Thatch, Tiles, Slate, Shingles }

        const float Thick = 0.35f;
        /// How far a building's floor stands above the ground (the floor covers the terrain's planks).
        public const float FloorTop = 0.035f;

        static readonly Color Plaster = new Color(0.87f, 0.81f, 0.67f), Beam = new Color(0.32f, 0.21f, 0.13f),
            StoneC = new Color(0.63f, 0.61f, 0.57f), BrickC = new Color(0.6f, 0.32f, 0.23f), PlankC = new Color(0.55f, 0.4f, 0.25f),
            Plinth = new Color(0.47f, 0.46f, 0.43f), Glass = new Color(0.24f, 0.33f, 0.42f), Frame = new Color(0.28f, 0.19f, 0.12f);
        static readonly Color[] Shutters = { new Color(0.24f, 0.42f, 0.27f), new Color(0.22f, 0.35f, 0.55f), new Color(0.58f, 0.2f, 0.17f), new Color(0.7f, 0.55f, 0.22f) };
        static readonly Color[] StainedGlass = { new Color(0.75f, 0.2f, 0.2f), new Color(0.2f, 0.35f, 0.8f), new Color(0.85f, 0.7f, 0.2f), new Color(0.25f, 0.6f, 0.3f) };

        static Color RoofColour(Roofing r) => r == Roofing.Thatch ? new Color(0.72f, 0.6f, 0.34f) : r == Roofing.Tiles ? new Color(0.6f, 0.26f, 0.17f)
            : r == Roofing.Slate ? new Color(0.33f, 0.36f, 0.42f) : new Color(0.44f, 0.3f, 0.2f);

        static Color WallColour(Walls w) => w == Walls.Timber ? Plaster : w == Walls.Stone ? StoneC : w == Walls.Brick ? BrickC : PlankC;

        /// One side of a building: tiles `from`..`to` (inclusive) along x (at z = `at`) or along z (at x = `at`),
        /// facing `outward`.
        struct Side
        {
            public int From, To, At;
            public bool AlongX;
            public Vector3 Outward;
            public float Mid => (From + To) / 2f;
            public float Length => To - From + 1;
            /// Where tile `t` of this side is, at height y (map space), on the wall's centre line.
            public Vector3 Point(float t, float y) => AlongX ? new Vector3(t, y, At) : new Vector3(At, y, t);
            /// Where the wall starts and ends along its side: the walls along x run out to the corners' outer faces,
            /// the walls along z fit between them (so nothing overlaps or sticks out at a corner).
            public float Start => AlongX ? From - Thick / 2f : From + Thick / 2f;
            public float End => AlongX ? To + Thick / 2f : To - Thick / 2f;
            /// Which way "right" along the wall is, seen from outside, as a sign along its axis.
            public float RightSign => AlongX ? (Outward.z > 0 ? 1f : -1f) : (Outward.x > 0 ? -1f : 1f);
        }

        /// Builds a building's shell and its furniture (TownInteriors; the stations and people in it are OverworldBuilder's). `townSeed`: the town's look.
        public static void Build(Transform root, Building b, int townSeed)
        {
            var a = b.Footprint;
            var rng = new System.Random(a.X * 73856093 ^ a.Z * 19349663 ^ (int)b.Kind * 83492791);
            var town = new System.Random(townSeed);
            var townRoof = (Roofing)town.Next(4);

            Walls walls;
            Roofing roof;
            float height;
            switch (b.Kind)
            {
                case BuildingKind.Bank: walls = Walls.Stone; roof = Roofing.Slate; height = 2.8f; break;
                case BuildingKind.Chapel: walls = Walls.Stone; roof = townRoof == Roofing.Thatch ? Roofing.Slate : townRoof; height = 3.1f; break;
                case BuildingKind.Smithy: walls = rng.Next(2) == 0 ? Walls.Stone : Walls.Brick; roof = townRoof == Roofing.Thatch ? Roofing.Tiles : townRoof; height = 2.5f; break;
                case BuildingKind.Shop:
                case BuildingKind.Workshop: walls = rng.Next(3) == 0 ? Walls.Planks : Walls.Timber; roof = rng.Next(3) == 0 ? (Roofing)rng.Next(4) : townRoof; height = 2.4f; break;
                default: walls = (Walls)new[] { 0, 0, 2, 3 }[rng.Next(4)]; roof = rng.Next(4) == 0 ? (Roofing)rng.Next(4) : townRoof; height = 2.3f; break;
            }
            var lo = new Tile(a.X, a.Z);
            var hi = new Tile(a.XMax - 1, a.ZMax - 1);
            float y0 = World.Height(new Tile((lo.X + hi.X) / 2, (lo.Z + hi.Z) / 2));
            var shutter = Shutters[rng.Next(Shutters.Length)];
            bool flowers = b.Kind == BuildingKind.House && rng.Next(2) == 0;

            var sides = new[]
            {
                new Side { From = lo.X, To = hi.X, At = lo.Z, AlongX = true, Outward = Vector3.back },
                new Side { From = lo.X, To = hi.X, At = hi.Z, AlongX = true, Outward = Vector3.forward },
                new Side { From = lo.Z, To = hi.Z, At = lo.X, AlongX = false, Outward = Vector3.left },
                new Side { From = lo.Z, To = hi.Z, At = hi.X, AlongX = false, Outward = Vector3.right },
            };
            var shell = new GameObject(b.Kind + " walls").transform;
            shell.SetParent(root, false);
            foreach (var side in sides)
            {
                int door = side.AlongX ? (b.Door.Z == side.At ? b.Door.X : int.MinValue) : (b.Door.X == side.At ? b.Door.Z : int.MinValue);
                WallRun(shell, side, door, y0, height, walls);
                Facing(shell, side, door, y0, height, walls);
                for (int t = side.From + 1; t < side.To; t++)
                {
                    // Every other tile, counted out from the door on its wall (so it has windows either side).
                    int from = door != int.MinValue ? door : side.From;
                    if (Mathf.Abs(t - door) <= 1 || (t - from) % 2 != 0)
                        continue;
                    if (b.Kind == BuildingKind.Chapel)
                        TallWindow(shell, side, t, y0, height, StainedGlass[(t + side.At) % StainedGlass.Length]);
                    else
                        Window(shell, side, t, y0, height, walls == Walls.Stone ? Frame : shutter, flowers);
                }
                if (door != int.MinValue)
                    Doorway(shell, side, door, y0, height, walls, b.Kind);
            }
            Corners(shell, lo, hi, y0, height, walls);

            // The roof: its ridge runs along the wall with the door (so the door is under the eaves, and the
            // gables are on the ends). It hides while you're inside.
            bool ridgeAlongX = b.Door.Z == lo.Z || b.Door.Z == hi.Z;
            var top = new GameObject(b.Kind + " roof").transform;
            top.SetParent(root, false);
            top.localPosition = new Vector3((lo.X + hi.X) / 2f, 0f, (lo.Z + hi.Z) / 2f);
            top.localRotation = Quaternion.Euler(0f, ridgeAlongX ? 0f : 90f, 0f);
            float length = ridgeAlongX ? hi.X - lo.X : hi.Z - lo.Z, span = ridgeAlongX ? hi.Z - lo.Z : hi.X - lo.X;
            float pitch = b.Kind == BuildingKind.Chapel ? 50f : roof == Roofing.Thatch ? 45f : 36f;
            float ridge = Roof(top, length, span, y0 + height, pitch, roof, WallColour(walls), walls == Walls.Timber);
            if (b.Kind == BuildingKind.Chapel)
                BellTower(top, length, y0 + height, ridge);
            else if (b.Kind == BuildingKind.Smithy)
                Chimney(top, length / 2f - 0.7f, span / 4f, y0, ridge + 0.9f, 0.8f, true);
            else
            {
                // A house's chimney rises from its hearth, behind the fireplace.
                var hearth = b.Furniture.Find(f => f.Kind == Furniture.Hearth);
                if (hearth != null)
                {
                    float dx = hearth.CentreX - hearth.Facing.X * 0.55f - top.localPosition.x, dz = hearth.CentreZ - hearth.Facing.Z * 0.55f - top.localPosition.z;
                    Chimney(top, ridgeAlongX ? dx : -dz, ridgeAlongX ? dz : dx, y0, ridge + 0.5f, 0.6f, false);
                }
            }
            top.gameObject.AddComponent<RoofHider>().Footprint = a;
            Floor(root, b, lo, hi, y0);
            TownInteriors.Build(root, b, y0 + FloorTop);
            if (b.Kind == BuildingKind.Farm)
                Fence(root, b, y0);
        }

        // ---- The floor ----

        /// The floor, right out to the walls (the terrain's planks stop short of them): boards, or stone flags
        /// in the bank and the chapel.
        static void Floor(Transform root, Building b, Tile lo, Tile hi, float y0)
        {
            float x0 = lo.X + Thick / 2f, x1 = hi.X - Thick / 2f, z0 = lo.Z + Thick / 2f, z1 = hi.Z - Thick / 2f;
            bool stone = b.Kind == BuildingKind.Bank || b.Kind == BuildingKind.Chapel;
            var colour = stone ? StoneC * 0.92f : new Color(0.56f, 0.41f, 0.26f);
            var floor = new GameObject(b.Kind + " floor").transform;
            floor.SetParent(root, false);
            Visuals.Prim(PrimitiveType.Cube, floor, new Vector3((x0 + x1) / 2f, y0 + FloorTop - 0.05f, (z0 + z1) / 2f), new Vector3(x1 - x0, 0.1f, z1 - z0), colour);
            // Board joints across the room (along its longer side), or a grid of flagstone joints.
            var line = colour * 0.8f;
            bool alongX = x1 - x0 >= z1 - z0;
            float y = y0 + FloorTop + 0.004f;
            float step = stone ? 0.5f : 0.25f;
            for (float t = (alongX ? z0 : x0) + step; t < (alongX ? z1 : x1) - 0.05f; t += step)
                Visuals.Prim(PrimitiveType.Cube, floor, alongX ? new Vector3((x0 + x1) / 2f, y, t) : new Vector3(t, y, (z0 + z1) / 2f),
                    alongX ? new Vector3(x1 - x0, 0.008f, 0.025f) : new Vector3(0.025f, 0.008f, z1 - z0), line);
            if (stone)
                for (float t = (alongX ? x0 : z0) + 0.5f; t < (alongX ? x1 : z1) - 0.05f; t += 0.5f)
                    Visuals.Prim(PrimitiveType.Cube, floor, alongX ? new Vector3(t, y, (z0 + z1) / 2f) : new Vector3((x0 + x1) / 2f, y, t),
                        alongX ? new Vector3(0.025f, 0.008f, z1 - z0) : new Vector3(x1 - x0, 0.008f, 0.025f), line);
        }

        // ---- Walls ----

        /// The wall itself (solid: WASD can't walk through it), in two pieces round a doorway, on a plinth.
        static void WallRun(Transform root, Side s, int door, float y0, float h, Walls walls)
        {
            var colour = WallColour(walls);
            for (int piece = 0; piece < 2; piece++)
            {
                int from = piece == 0 ? s.From : door + 1, to = piece == 0 ? (door >= s.From && door <= s.To ? door - 1 : s.To) : s.To;
                if (piece == 1 && !(door >= s.From && door <= s.To))
                    break;
                if (to < from)
                    continue;
                float start = from == s.From ? s.Start : from - 0.5f, end = to == s.To ? s.End : to + 0.5f;
                float mid = (start + end) / 2f, len = end - start;
                var size = s.AlongX ? new Vector3(len, h, Thick) : new Vector3(Thick, h, len);
                Visuals.Prim(PrimitiveType.Cube, root, s.Point(mid, y0 + h / 2f), size, colour, true);
                // The plinth: out to the corner on an x side, between the x sides' plinths on a z side.
                float grow = s.AlongX ? 0.07f : -0.07f;
                // (At a doorway it steps 2 cm out past the wall's end, so their faces don't meet.)
                float ps = from == s.From ? start - grow : start - 0.02f, pe = to == s.To ? end + grow : end + 0.02f;
                var plinth = s.AlongX ? new Vector3(pe - ps, 0.3f, Thick + 0.14f) : new Vector3(Thick + 0.14f, 0.3f, pe - ps);
                Visuals.Prim(PrimitiveType.Cube, root, s.Point((ps + pe) / 2f, y0 + 0.15f), plinth, Plinth);
            }
            // A band along the top, standing a little proud of the wall's top (so the two never share a face).
            float bs = s.AlongX ? s.Start - 0.04f : s.Start + 0.04f, be = s.AlongX ? s.End + 0.04f : s.End - 0.04f;
            var band = s.AlongX ? new Vector3(be - bs, 0.14f, Thick + 0.08f) : new Vector3(Thick + 0.08f, 0.14f, be - bs);
            Visuals.Prim(PrimitiveType.Cube, root, s.Point((bs + be) / 2f, y0 + h - 0.05f), band, walls == Walls.Timber ? Beam : walls == Walls.Stone ? StoneC * 0.82f : Frame);
        }

        /// What shows on a wall's outside face: a timber frame's beams and braces, a stone wall's courses,
        /// brick courses, or the boards of a plank wall. Not over the doorway.
        static void Facing(Transform root, Side s, int door, float y0, float h, Walls walls)
        {
            float face = Thick / 2f + 0.012f;
            for (float t = s.From; t <= s.To + 0.001f; t += 0.5f)
            {
                bool nearDoor = Mathf.Abs(t - door) < 0.75f;
                switch (walls)
                {
                    case Walls.Timber:
                        // Posts every tile and a half, a rail half way up, braces in some bays.
                        if (Mathf.Abs((t - s.From) % 1.5f) < 0.01f && !nearDoor)
                            Strip(root, s, t, y0 + h / 2f, face, 0.14f, h, Beam);
                        if (!nearDoor)
                            Strip(root, s, t + 0.25f, y0 + h * 0.48f, face, 0.52f, 0.1f, Beam);
                        if (Mathf.Abs((t - s.From) % 3f - 0.5f) < 0.01f && t + 1f <= s.To && Mathf.Abs(t + 0.5f - door) > 1.2f)
                        {
                            Tilt(Strip(root, s, t + 0.5f, y0 + h * 0.24f, face, 0.62f, 0.1f, Beam), s, 40f);
                        }
                        break;
                    case Walls.Planks:
                        if (!nearDoor)
                            Strip(root, s, t, y0 + h / 2f, face, 0.03f, h - 0.1f, PlankC * 0.75f);
                        break;
                }
            }
            if (walls == Walls.Stone || walls == Walls.Brick)
            {
                float step = walls == Walls.Stone ? 0.42f : 0.2f;
                var line = walls == Walls.Stone ? StoneC * 0.8f : new Color(0.72f, 0.66f, 0.58f);
                for (float y = 0.3f + step; y < h - 0.15f; y += step)
                {
                    // A course line along the wall, broken at the doorway.
                    float a = s.From + 0.3f, b = s.To - 0.3f;  // between the corners' quoins
                    if (door >= s.From && door <= s.To && y < 2.05f)
                    {
                        if (door - 0.6f > a)
                            Strip(root, s, (a + door - 0.6f) / 2f, y0 + y, face, door - 0.6f - a, 0.03f, line);
                        if (b > door + 0.6f)
                            Strip(root, s, (door + 0.6f + b) / 2f, y0 + y, face, b - (door + 0.6f), 0.03f, line);
                    }
                    else
                        Strip(root, s, s.Mid, y0 + y, face, b - a, 0.03f, line);
                }
            }
        }

        /// A flat strip on a wall's outside face: `along` wide, `tall` high, at tile position t along the side.
        static GameObject Strip(Transform root, Side s, float t, float y, float face, float along, float tall, Color c)
        {
            var size = s.AlongX ? new Vector3(along, tall, 0.03f) : new Vector3(0.03f, tall, along);
            return Visuals.Prim(PrimitiveType.Cube, root, s.Point(t, y) + s.Outward * face, size, c);
        }

        /// Turns a strip in the wall's plane (a brace), by `degrees`.
        static void Tilt(GameObject strip, Side s, float degrees) =>
            strip.transform.localRotation = s.AlongX ? Quaternion.Euler(0f, 0f, degrees) : Quaternion.Euler(degrees, 0f, 0f);

        /// The corners: dark posts on a timber or plank building, quoin stones on stone or brick.
        static void Corners(Transform root, Tile lo, Tile hi, float y0, float h, Walls walls)
        {
            foreach (var c in new[] { new Vector2(lo.X, lo.Z), new Vector2(hi.X, lo.Z), new Vector2(lo.X, hi.Z), new Vector2(hi.X, hi.Z) })
            {
                if (walls == Walls.Timber || walls == Walls.Planks)
                {
                    Visuals.Prim(PrimitiveType.Cube, root, new Vector3(c.x, y0 + (h + 0.05f) / 2f, c.y), new Vector3(Thick + 0.11f, h + 0.05f, Thick + 0.11f), Beam);
                    continue;
                }
                var quoin = walls == Walls.Stone ? StoneC * 1.12f : new Color(0.75f, 0.7f, 0.62f);
                for (int k = 0; k * 0.42f + 0.3f < h - 0.2f; k++)
                {
                    float y = y0 + 0.3f + k * 0.42f + 0.21f;
                    var size = k % 2 == 0 ? new Vector3(Thick + 0.24f, 0.36f, Thick + 0.06f) : new Vector3(Thick + 0.06f, 0.36f, Thick + 0.24f);
                    Visuals.Prim(PrimitiveType.Cube, root, new Vector3(c.x, y, c.y), size, quoin);
                }
            }
        }

        /// A side's local frame at tile t: origin on the wall's outside face at y, z outward, x along the wall.
        static Transform FaceAt(Transform root, Side s, float t, float y)
        {
            var f = new GameObject("Window").transform;
            f.SetParent(root, false);
            f.localPosition = s.Point(t, y) + s.Outward * (Thick / 2f);
            f.localRotation = Quaternion.LookRotation(s.Outward);
            return f;
        }

        /// A window: frame, glass, sill and a shutter either side (and maybe a box of flowers).
        static void Window(Transform root, Side s, int t, float y0, float h, Color shutter, bool flowers)
        {
            var f = FaceAt(root, s, t, y0 + h * 0.56f);
            f.localPosition += s.Outward * 0.03f;  // clear of the beams and courses on the wall behind it
            Visuals.Prim(PrimitiveType.Cube, f, new Vector3(0f, 0f, 0f), new Vector3(0.6f, 0.7f, 0.06f), Frame);
            Visuals.Prim(PrimitiveType.Cube, f, new Vector3(0f, 0f, 0.03f), new Vector3(0.48f, 0.58f, 0.02f), Glass);
            Visuals.Prim(PrimitiveType.Cube, f, new Vector3(0f, 0f, 0.045f), new Vector3(0.04f, 0.58f, 0.02f), Frame);
            Visuals.Prim(PrimitiveType.Cube, f, new Vector3(0f, 0.02f, 0.045f), new Vector3(0.48f, 0.04f, 0.02f), Frame);
            Visuals.Prim(PrimitiveType.Cube, f, new Vector3(0f, -0.39f, 0.04f), new Vector3(0.7f, 0.06f, 0.14f), Frame * 1.2f);
            Visuals.Prim(PrimitiveType.Cube, f, new Vector3(-0.44f, 0f, 0.005f), new Vector3(0.24f, 0.7f, 0.07f), shutter);
            Visuals.Prim(PrimitiveType.Cube, f, new Vector3(0.44f, 0f, 0.005f), new Vector3(0.24f, 0.7f, 0.07f), shutter);
            if (!flowers)
                return;
            Visuals.Prim(PrimitiveType.Cube, f, new Vector3(0f, -0.5f, 0.12f), new Vector3(0.6f, 0.14f, 0.16f), new Color(0.4f, 0.27f, 0.16f));
            var bloom = new[] { new Color(0.9f, 0.3f, 0.3f), new Color(0.95f, 0.85f, 0.3f), new Color(0.85f, 0.45f, 0.75f) };
            for (int k = 0; k < 5; k++)
                Visuals.Prim(PrimitiveType.Sphere, f, new Vector3(-0.24f + k * 0.12f, -0.4f, 0.13f), Vector3.one * 0.1f, k % 2 == 0 ? new Color(0.25f, 0.55f, 0.22f) : bloom[(t + k) % 3]);
        }

        /// A chapel's tall, pointed window of coloured glass.
        static void TallWindow(Transform root, Side s, int t, float y0, float h, Color glass)
        {
            var f = FaceAt(root, s, t, y0 + h * 0.5f);
            f.localPosition += s.Outward * 0.03f;
            Visuals.Prim(PrimitiveType.Cube, f, new Vector3(0f, 0f, 0f), new Vector3(0.46f, 1.3f, 0.06f), StoneC * 1.15f);
            Visuals.Prim(PrimitiveType.Cube, f, new Vector3(0f, -0.05f, 0.03f), new Vector3(0.32f, 1.1f, 0.02f), glass);
            var point = Visuals.Prim(PrimitiveType.Cube, f, new Vector3(0f, 0.5f, 0.032f), new Vector3(0.226f, 0.226f, 0.02f), glass * 0.85f);
            point.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            Visuals.Prim(PrimitiveType.Cube, f, new Vector3(0f, 0.05f, 0.045f), new Vector3(0.03f, 1.0f, 0.02f), Frame);
            Visuals.Prim(PrimitiveType.Cube, f, new Vector3(0f, -0.66f, 0.04f), new Vector3(0.5f, 0.06f, 0.14f), StoneC);
        }

        /// The doorway: a frame and lintel, the door swung open against the wall, and the kind's touch over it.
        static void Doorway(Transform root, Side s, int t, float y0, float h, Walls walls, BuildingKind kind)
        {
            var f = FaceAt(root, s, t, y0);
            var frame = walls == Walls.Stone ? StoneC * 1.12f : Beam;
            Visuals.Prim(PrimitiveType.Cube, f, new Vector3(-0.5f, 1.05f, 0.045f), new Vector3(0.14f, 2.1f, 0.1f), frame);
            Visuals.Prim(PrimitiveType.Cube, f, new Vector3(0.5f, 1.05f, 0.045f), new Vector3(0.14f, 2.1f, 0.1f), frame);
            // Above the doorway, the wall closes over it.
            Visuals.Prim(PrimitiveType.Cube, f, new Vector3(0f, (2.1f + h) / 2f, -Thick / 2f), new Vector3(1.0f, h - 2.1f, Thick), WallColour(walls));
            Visuals.Prim(PrimitiveType.Cube, f, new Vector3(0f, 2.14f, 0.04f), new Vector3(1.2f, 0.14f, 0.1f), frame);
            var door = new Color(0.42f, 0.27f, 0.15f);
            Visuals.Prim(PrimitiveType.Cube, f, new Vector3(0.92f, 1.0f, 0.06f), new Vector3(0.66f, 1.95f, 0.05f), door);
            for (int k = 0; k < 3; k++)  // its boards' battens
                Visuals.Prim(PrimitiveType.Cube, f, new Vector3(0.92f, 0.35f + k * 0.65f, 0.09f), new Vector3(0.62f, 0.05f, 0.02f), door * 0.7f);
            switch (kind)
            {
                case BuildingKind.Bank:
                    foreach (float x in new[] { -0.82f, 0.82f })
                    {
                        Visuals.Prim(PrimitiveType.Cylinder, f, new Vector3(x, 1.2f, 0.18f), new Vector3(0.3f, 1.2f, 0.3f), StoneC * 1.18f);
                        Visuals.Prim(PrimitiveType.Cube, f, new Vector3(x, 2.45f, 0.18f), new Vector3(0.42f, 0.1f, 0.42f), StoneC * 1.05f);
                    }
                    Visuals.Prim(PrimitiveType.Cube, f, new Vector3(0f, 2.56f, 0.2f), new Vector3(2.2f, 0.16f, 0.46f), StoneC * 1.1f);
                    Visuals.Prim(PrimitiveType.Cube, f, new Vector3(0f, 2.66f, 0.22f), new Vector3(1.6f, 0.05f, 0.44f), new Color(0.85f, 0.68f, 0.22f));
                    break;
                case BuildingKind.Shop:
                    Awning(f, h, true);
                    break;
                case BuildingKind.Workshop:
                    Awning(f, h, false);
                    break;
            }
        }

        /// A sloping awning over a doorway: striped canvas (a shop) or boards (a workshop).
        static void Awning(Transform f, float h, bool striped)
        {
            const int strips = 6;
            for (int k = 0; k < strips; k++)
            {
                var c = striped ? (k % 2 == 0 ? new Color(0.78f, 0.2f, 0.18f) : new Color(0.92f, 0.88f, 0.78f)) : new Color(0.5f, 0.36f, 0.22f) * (k % 2 == 0 ? 1f : 0.9f);
                var piece = Visuals.Prim(PrimitiveType.Cube, f, new Vector3(-0.9f + 1.8f * (k + 0.5f) / strips, h - 0.38f, 0.42f), new Vector3(1.8f / strips + 0.005f, 0.04f, 0.9f), c);
                piece.transform.localRotation = Quaternion.Euler(-22f, 0f, 0f);
            }
            foreach (float x in new[] { -0.88f, 0.88f })
            {
                var arm = Visuals.Prim(PrimitiveType.Cube, f, new Vector3(x, h - 0.6f, 0.38f), new Vector3(0.04f, 0.04f, 0.8f), Beam);
                arm.transform.localRotation = Quaternion.Euler(28f, 0f, 0f);
            }
        }

        // ---- Roofs ----

        /// A pitched roof along local x (its centre at the parent's origin): two slopes overhanging the walls, a
        /// ridge, rows of thatch, tiles, slates or shingles, and the gables filled at each end. Returns the ridge's height.
        static float Roof(Transform top, float length, float span, float eaves, float pitch, Roofing roofing, Color gable, bool timberGable)
        {
            float tan = Mathf.Tan(pitch * Mathf.Deg2Rad), cos = Mathf.Cos(pitch * Mathf.Deg2Rad);
            float halfIn = span / 2f + Thick / 2f, overhang = 0.4f, endOver = 0.3f;
            float thick = roofing == Roofing.Thatch ? 0.3f : 0.12f;
            float ridge = eaves + 0.04f + halfIn * tan;
            float run = halfIn + overhang, slope = run / cos, along = length + Thick + endOver * 2f;
            var colour = RoofColour(roofing);
            for (int side = -1; side <= 1; side += 2)
            {
                float u = run / 2f;
                var slab = Visuals.Prim(PrimitiveType.Cube, top, new Vector3(0f, ridge - u * tan + thick / 2f / cos, side * u), new Vector3(along, thick, slope), colour);
                slab.transform.localRotation = Quaternion.Euler(side * pitch, 0f, 0f);
                // Rows: lines running along the roof, down its slope.
                int rows = roofing == Roofing.Thatch ? 3 : 6;
                var row = roofing == Roofing.Thatch ? colour * 0.86f : colour * 0.78f;
                for (int k = 1; k <= rows; k++)
                {
                    float uk = run * k / (rows + 1f);
                    var line = Visuals.Prim(PrimitiveType.Cube, top, new Vector3(0f, ridge - uk * tan + (thick + 0.012f) / cos, side * uk),
                        new Vector3(along + 0.01f, 0.02f, roofing == Roofing.Thatch ? 0.12f : 0.05f), row);
                    line.transform.localRotation = Quaternion.Euler(side * pitch, 0f, 0f);
                }
            }
            Visuals.Prim(PrimitiveType.Cube, top, new Vector3(0f, ridge + thick * 0.9f, 0f), new Vector3(along + 0.04f, 0.16f, 0.3f), colour * 0.72f);
            // The gables: a diamond in the end wall's plane, its top half above the eaves (the bottom is in the wall).
            for (int end = -1; end <= 1; end += 2)
            {
                var g = new GameObject("Gable").transform;
                g.SetParent(top, false);
                g.localPosition = new Vector3(end * length / 2f, eaves + 0.04f, 0f);
                g.localScale = new Vector3(Thick - 0.02f, (ridge - eaves - 0.04f) / 0.7071f, halfIn / 0.7071f);
                var diamond = Visuals.Prim(PrimitiveType.Cube, g, Vector3.zero, Vector3.one, gable);
                diamond.transform.localRotation = Quaternion.Euler(45f, 0f, 0f);
                // A little round vent near the top, and on a timber gable, a king post.
                float vy = eaves + 0.04f + (ridge - eaves) * 0.45f;
                Visuals.Prim(PrimitiveType.Cylinder, top, new Vector3(end * (length / 2f + Thick / 2f), vy, 0f), new Vector3(0.34f, 0.02f, 0.34f), Frame)
                    .transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                if (timberGable)
                    Visuals.Prim(PrimitiveType.Cube, top, new Vector3(end * (length / 2f + Thick / 2f + 0.01f), (eaves + ridge) / 2f, 0f), new Vector3(0.03f, ridge - eaves, 0.12f), Beam);
            }
            return ridge;
        }

        // ---- A farm's field ----

        static readonly Color FenceWood = new Color(0.5f, 0.38f, 0.24f);

        /// The fence round a farm's field: a post on every fence tile, two rails between neighbours, and at each
        /// gap a gate swung open against the fence. Solid (a collider on each fence tile).
        static void Fence(Transform root, Building b, float y0)
        {
            var f = b.Field;
            var fence = new GameObject("Farm fence").transform;
            fence.SetParent(root, false);
            Func<int, int, bool> isFence = (x, z) => (x == f.X || z == f.Z || x == f.XMax - 1 || z == f.ZMax - 1)
                && x >= f.X && x < f.XMax && z >= f.Z && z < f.ZMax && !b.Gates.Contains(new Tile(x, z));
            for (int x = f.X; x < f.XMax; x++)
                for (int z = f.Z; z < f.ZMax; z++)
                {
                    if (!isFence(x, z))
                        continue;
                    var post = new GameObject("Fence post").transform;
                    post.SetParent(fence, false);
                    post.localPosition = new Vector3(x, y0, z);
                    Visuals.Prim(PrimitiveType.Cube, post, new Vector3(0f, 0.5f, 0f), new Vector3(0.14f, 1f, 0.14f), FenceWood * 0.85f);
                    Visuals.Prim(PrimitiveType.Cube, post, new Vector3(0f, 1.02f, 0f), new Vector3(0.18f, 0.05f, 0.18f), FenceWood * 0.7f);
                    var box = post.gameObject.AddComponent<BoxCollider>();
                    box.center = new Vector3(0f, 0.5f, 0f);
                    box.size = new Vector3(1f, 1f, 1f);
                    // Rails to the next post along +x and +z (so each pair gets one set).
                    if (isFence(x + 1, z))
                        foreach (float y in new[] { 0.4f, 0.8f })
                            Visuals.Prim(PrimitiveType.Cube, post, new Vector3(0.5f, y, 0f), new Vector3(1f, 0.08f, 0.05f), FenceWood);
                    if (isFence(x, z + 1))
                        foreach (float y in new[] { 0.4f, 0.8f })
                            Visuals.Prim(PrimitiveType.Cube, post, new Vector3(0f, y, 0.5f), new Vector3(0.05f, 0.08f, 1f), FenceWood);
                }
            foreach (var gate in b.Gates)
            {
                // Swung open into the field, hinged on one side of the gap.
                bool across = gate.X == f.X || gate.X == f.XMax - 1;  // the gap is in a fence running along z
                var inward = gate.X == f.X ? Vector3.right : gate.X == f.XMax - 1 ? Vector3.left : gate.Z == f.Z ? Vector3.forward : Vector3.back;
                var hinge = new GameObject("Gate").transform;
                hinge.SetParent(fence, false);
                hinge.localPosition = new Vector3(gate.X, y0, gate.Z) + (across ? new Vector3(0f, 0f, -0.45f) : new Vector3(-0.45f, 0f, 0f));
                hinge.localRotation = Quaternion.LookRotation(inward);
                // The leaf lies along local +z (into the field), against the fence beside it.
                foreach (float y in new[] { 0.3f, 0.6f, 0.9f })
                    Visuals.Prim(PrimitiveType.Cube, hinge, new Vector3(0f, y, 0.45f), new Vector3(0.05f, 0.07f, 0.85f), FenceWood * 1.1f);
                foreach (float z in new[] { 0.05f, 0.85f })
                    Visuals.Prim(PrimitiveType.Cube, hinge, new Vector3(0f, 0.6f, z), new Vector3(0.06f, 0.75f, 0.07f), FenceWood);
                Visuals.Prim(PrimitiveType.Cube, hinge, new Vector3(0f, 0.6f, 0.45f), new Vector3(0.04f, 0.06f, 1.05f), FenceWood * 0.8f)
                    .transform.localRotation = Quaternion.Euler(-34f, 0f, 0f);
            }
        }

        /// A stone chimney rising from the floor through the roof (local roof frame), capped; a smithy's glows.
        static void Chimney(Transform top, float x, float z, float floor, float to, float width, bool forge)
        {
            float h = to - floor;
            Visuals.Prim(PrimitiveType.Cube, top, new Vector3(x, floor + h / 2f, z), new Vector3(width, h, width), StoneC * 0.92f);
            Visuals.Prim(PrimitiveType.Cube, top, new Vector3(x, to + 0.05f, z), new Vector3(width + 0.12f, 0.1f, width + 0.12f), StoneC * 0.75f);
            Visuals.Prim(PrimitiveType.Cube, top, new Vector3(x, to + 0.11f, z), new Vector3(width - 0.2f, 0.04f, width - 0.2f),
                forge ? new Color(1f, 0.55f, 0.15f) : new Color(0.12f, 0.1f, 0.09f));
        }

        /// A chapel's bell tower at one end: a stone shaft above the roof, an open belfry with a bell, a tiered spire.
        static void BellTower(Transform top, float length, float eaves, float ridge)
        {
            float x = length / 2f - 0.7f, w = 1.3f, belfry = ridge + 0.3f;
            Visuals.Prim(PrimitiveType.Cube, top, new Vector3(x, (eaves + belfry) / 2f, 0f), new Vector3(w, belfry - eaves, w), StoneC);
            // The belfry: four corner piers round an opening, the bell inside.
            foreach (float dx in new[] { -0.5f, 0.5f })
                foreach (float dz in new[] { -0.5f, 0.5f })
                    Visuals.Prim(PrimitiveType.Cube, top, new Vector3(x + dx, belfry + 0.5f, dz), new Vector3(0.3f, 1f, 0.3f), StoneC * 1.08f);
            Visuals.Prim(PrimitiveType.Cube, top, new Vector3(x, belfry + 1.05f, 0f), new Vector3(w + 0.1f, 0.12f, w + 0.1f), StoneC * 0.9f);
            Visuals.Prim(PrimitiveType.Sphere, top, new Vector3(x, belfry + 0.5f, 0f), new Vector3(0.42f, 0.5f, 0.42f), new Color(0.75f, 0.58f, 0.25f));
            for (int k = 0; k < 5; k++)
            {
                float s = w * (1f - k * 0.19f);
                Visuals.Prim(PrimitiveType.Cube, top, new Vector3(x, belfry + 1.2f + k * 0.32f, 0f), new Vector3(s, 0.34f, s), RoofColour(Roofing.Slate) * (1f - k * 0.03f));
            }
            Visuals.Prim(PrimitiveType.Sphere, top, new Vector3(x, belfry + 2.95f, 0f), Vector3.one * 0.22f, new Color(0.85f, 0.7f, 0.3f));
        }
    }

    /// Hides a building's roof (and what's on it) while you're inside, in tile mode, so you can see in.
    public sealed class RoofHider : MonoBehaviour
    {
        public Area Footprint;
        bool shown = true;
        Transform[] children;

        void Update()
        {
            var g = Game.Instance;
            if (g == null || g.Player == null)
                return;
            var t = World.GroundTile(g.Player.CurrentTile);
            bool inside = g.Player.Mode == ViewMode.Tile && Footprint.Contains(t);
            if (inside != shown)
                return;
            shown = !inside;
            if (children == null)
            {
                children = new Transform[transform.childCount];
                for (int i = 0; i < children.Length; i++)
                    children[i] = transform.GetChild(i);
            }
            foreach (var c in children)
                if (c != null)
                    c.gameObject.SetActive(shown);
        }
    }
}
