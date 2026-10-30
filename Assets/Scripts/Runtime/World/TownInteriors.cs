using Epacenur.Core;
using UnityEngine;

namespace Epacenur.Runtime
{
    /// Draws what furnishes a town building (Core: Furnisher lays it out): beds, hearths, cupboards and shelves,
    /// barrels and crates; a bank's strongboxes and bookcase; a chapel's pews and candle stands; a smithy's
    /// trough and coal; a workshop's wool and hides; and a runner down the floor from the door. Each piece is
    /// built facing +z (into the room), its back to the wall at -z, and turned to its place; each is solid.
    public static class TownInteriors
    {
        const float Back = -0.8f;  // the wall's inner face, behind a piece against it

        static readonly Color Wood = new Color(0.45f, 0.3f, 0.17f), DarkWood = new Color(0.32f, 0.21f, 0.13f), LightWood = new Color(0.6f, 0.45f, 0.28f),
            Iron = new Color(0.27f, 0.27f, 0.29f), Brass = new Color(0.78f, 0.62f, 0.28f), StoneC = new Color(0.58f, 0.56f, 0.52f),
            Burlap = new Color(0.66f, 0.56f, 0.38f), Linen = new Color(0.9f, 0.88f, 0.82f), Flame = new Color(1f, 0.78f, 0.35f), Ember = new Color(1f, 0.45f, 0.12f);
        static readonly Color[] Blankets = { new Color(0.55f, 0.18f, 0.16f), new Color(0.2f, 0.32f, 0.55f), new Color(0.3f, 0.45f, 0.25f), new Color(0.6f, 0.5f, 0.25f) };
        static readonly Color[] Goods = { new Color(0.55f, 0.25f, 0.2f), new Color(0.25f, 0.4f, 0.55f), new Color(0.7f, 0.65f, 0.5f), new Color(0.35f, 0.5f, 0.3f),
            new Color(0.75f, 0.55f, 0.3f), new Color(0.5f, 0.35f, 0.5f) };

        /// Furnishes `b`, its floor at height `floor` (map space).
        public static void Build(Transform root, Building b, float floor)
        {
            var rng = new System.Random(b.Footprint.X * 31 ^ b.Footprint.Z * 7919 ^ (int)b.Kind);
            var room = new GameObject(b.Kind + " furniture").transform;
            room.SetParent(root, false);
            Runner(room, b, floor);
            foreach (var f in b.Furniture)
            {
                var t = new GameObject(f.Kind.ToString()).transform;
                t.SetParent(room, false);
                t.localPosition = new Vector3(f.CentreX, floor, f.CentreZ);
                t.localRotation = Quaternion.LookRotation(new Vector3(f.Facing.X, 0f, f.Facing.Z));
                float h = Piece(t, f, rng);
                var box = t.gameObject.AddComponent<BoxCollider>();
                bool wall = f.Kind != Furniture.Pew;
                box.center = new Vector3(0f, h / 2f, wall ? -0.3f : 0f);
                box.size = new Vector3(f.Length - 0.1f, h, wall ? 0.9f : 0.7f);
            }
        }

        /// A rug down the floor from the door to what's at the back (a long red one in the bank and chapel).
        static void Runner(Transform room, Building b, float floor)
        {
            var inward = Furnisher.Inward(b);
            var start = new Vector3(b.Door.X + inward.X, floor, b.Door.Z + inward.Z);
            var end = new Vector3(b.Feature.X - inward.X, floor, b.Feature.Z - inward.Z);
            float length = (end - start).magnitude + 0.9f;
            if (length < 1.5f)
                return;
            bool grand = b.Kind == BuildingKind.Bank || b.Kind == BuildingKind.Chapel;
            if (b.Kind == BuildingKind.Smithy)
                return;  // no rug by a forge
            var colour = grand ? new Color(0.55f, 0.12f, 0.12f) : Blankets[(b.Footprint.X + b.Footprint.Z) & 3] * 0.9f;
            var rug = new GameObject("Rug").transform;
            rug.SetParent(room, false);
            rug.localPosition = (start + end) / 2f;
            rug.localRotation = Quaternion.LookRotation(new Vector3(inward.X, 0f, inward.Z));
            Visuals.Prim(PrimitiveType.Cube, rug, new Vector3(0f, 0.012f, 0f), new Vector3(grand ? 0.9f : 0.8f, 0.02f, length), colour);
            Visuals.Prim(PrimitiveType.Cube, rug, new Vector3(0f, 0.016f, 0f), new Vector3(grand ? 0.66f : 0.56f, 0.02f, length - 0.24f), grand ? Brass * 0.85f : colour * 1.25f);
            Visuals.Prim(PrimitiveType.Cube, rug, new Vector3(0f, 0.02f, 0f), new Vector3(grand ? 0.56f : 0.46f, 0.02f, length - 0.34f), colour);
        }

        static GameObject Box(Transform t, float x, float y, float z, float sx, float sy, float sz, Color c) =>
            Visuals.Prim(PrimitiveType.Cube, t, new Vector3(x, y, z), new Vector3(sx, sy, sz), c);

        static GameObject Glow(Transform t, PrimitiveType type, Vector3 at, Vector3 size, Color c)
        {
            var go = Visuals.Prim(type, t, at, size, c);
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = ModelBuilder.GlowMaterial(c);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            go.name = "Glow";
            return go;
        }

        /// Builds one piece; returns its height (for its collider).
        static float Piece(Transform t, FurniturePiece f, System.Random rng)
        {
            switch (f.Kind)
            {
                case Furniture.Bed: return Bed(t, rng);
                case Furniture.Chest: return Chest(t, Wood, Iron, false);
                case Furniture.Strongbox: return Chest(t, DarkWood, Iron * 0.8f, true);
                case Furniture.Cupboard: return Cupboard(t);
                case Furniture.Shelves: return Shelves(t, rng, false);
                case Furniture.Bookcase: return Shelves(t, rng, true);
                case Furniture.Barrel: return Barrels(t, rng);
                case Furniture.Crates: return Crates(t, rng);
                case Furniture.Sacks: return Sacks(t);
                case Furniture.Hearth: return Hearth(t);
                case Furniture.Desk: return Desk(t);
                case Furniture.Pew: return Pew(t, f.Length);
                case Furniture.Candles: return Candles(t);
                case Furniture.Trough: return Trough(t);
                case Furniture.CoalHeap: return CoalHeap(t);
                case Furniture.WeaponRack: return WeaponRack(t);
                case Furniture.Grindstone: return Grindstone(t);
                case Furniture.WoolBaskets: return WoolBaskets(t);
                default: return HideRack(t);
            }
        }

        // A bed two tiles long against the wall: a frame, mattress, blanket, pillow, and a headboard.
        static float Bed(Transform t, System.Random rng)
        {
            float z = Back + 0.5f;
            var blanket = Blankets[rng.Next(Blankets.Length)];
            foreach (float x in new[] { -0.9f, 0.9f })
                foreach (float dz in new[] { -0.4f, 0.4f })
                    Box(t, x, 0.18f, z + dz, 0.08f, 0.36f, 0.08f, DarkWood);
            Box(t, 0f, 0.3f, z, 1.88f, 0.14f, 0.9f, Wood);
            Box(t, 0f, 0.42f, z, 1.8f, 0.12f, 0.82f, Linen);
            Box(t, -0.15f, 0.49f, z + 0.02f, 1.4f, 0.06f, 0.88f, blanket);
            Box(t, -0.15f, 0.5f, z + 0.02f, 0.06f, 0.06f, 0.9f, blanket * 0.75f);  // a stripe
            Visuals.Prim(PrimitiveType.Sphere, t, new Vector3(0.68f, 0.52f, z), new Vector3(0.36f, 0.12f, 0.6f), Linen * 1.05f);
            Box(t, 0.92f, 0.5f, z, 0.08f, 0.85f, 0.9f, Wood);
            Box(t, 0.92f, 0.95f, z, 0.12f, 0.06f, 0.96f, DarkWood);
            Box(t, -0.92f, 0.38f, z, 0.08f, 0.5f, 0.9f, Wood);
            return 0.95f;
        }

        // A chest (or a bank's strongbox, with coins heaped on it): iron bands, a lock.
        static float Chest(Transform t, Color wood, Color iron, bool coins)
        {
            float z = Back + 0.3f;
            Box(t, 0f, 0.24f, z, 0.82f, 0.46f, 0.5f, wood);
            Box(t, 0f, 0.5f, z, 0.86f, 0.08f, 0.54f, wood * 0.85f);
            foreach (float x in new[] { -0.28f, 0.28f })
                Box(t, x, 0.27f, z, 0.06f, 0.56f, 0.56f, iron);
            Box(t, 0f, 0.38f, z + 0.27f, 0.12f, 0.14f, 0.03f, Brass);
            if (coins)
            {
                for (int i = 0; i < 4; i++)
                    Visuals.Prim(PrimitiveType.Cylinder, t, new Vector3(-0.2f + i * 0.13f, 0.55f + (i % 2) * 0.02f, z + (i % 2) * 0.08f - 0.04f), new Vector3(0.1f, 0.01f + i % 3 * 0.01f, 0.1f), Brass * 1.1f);
                Visuals.Prim(PrimitiveType.Sphere, t, new Vector3(0.22f, 0.66f, z), new Vector3(0.24f, 0.26f, 0.22f), Burlap);
                Box(t, 0.22f, 0.8f, z, 0.08f, 0.03f, 0.08f, Brass);
            }
            return coins ? 0.8f : 0.55f;
        }

        // A tall cupboard: two doors with knobs, a cornice, on short feet.
        static float Cupboard(Transform t)
        {
            float z = Back + 0.26f;
            Box(t, 0f, 0.95f, z, 0.9f, 1.7f, 0.5f, Wood);
            Box(t, 0f, 1.84f, z + 0.02f, 0.98f, 0.1f, 0.56f, DarkWood);
            Box(t, 0f, 0.12f, z + 0.02f, 0.94f, 0.08f, 0.54f, DarkWood);
            foreach (float x in new[] { -0.215f, 0.215f })
            {
                Box(t, x, 1.0f, z + 0.255f, 0.38f, 1.42f, 0.02f, LightWood * 0.9f);
                Box(t, x, 1.0f, z + 0.265f, 0.28f, 1.26f, 0.01f, Wood * 0.9f);
                Visuals.Prim(PrimitiveType.Sphere, t, new Vector3(x * 0.25f, 1.0f, z + 0.28f), Vector3.one * 0.05f, Brass);
            }
            return 1.9f;
        }

        // Open shelves stocked with jars, pots and bundles (or a bookcase, rows of books).
        static float Shelves(Transform t, System.Random rng, bool books)
        {
            float z = Back + 0.21f, w = 0.92f;
            var wood = books ? DarkWood : Wood;
            foreach (float x in new[] { -w / 2f, w / 2f })
                Box(t, x, 0.95f, z, 0.06f, 1.9f, 0.42f, wood);
            Box(t, 0f, 0.95f, z - 0.19f, w, 1.9f, 0.03f, wood * 0.8f);
            for (int s = 0; s < 4; s++)
            {
                float y = 0.1f + s * 0.52f;
                Box(t, 0f, y, z, w, 0.05f, 0.42f, wood);
                if (s == 3)
                    break;
                if (books)
                {
                    float x = -w / 2f + 0.06f;
                    while (x < w / 2f - 0.1f)
                    {
                        float bw = 0.05f + (float)rng.NextDouble() * 0.04f, bh = 0.3f + (float)rng.NextDouble() * 0.12f;
                        var book = Box(t, x + bw / 2f, y + 0.03f + bh / 2f, z + 0.02f, bw, bh, 0.28f, Goods[rng.Next(Goods.Length)] * 0.8f);
                        if (rng.Next(7) == 0)
                            book.transform.localRotation = Quaternion.Euler(0f, 0f, 12f);
                        x += bw + 0.008f;
                    }
                }
                else
                    for (int i = 0; i < 3; i++)
                    {
                        float x = -0.28f + i * 0.28f + ((float)rng.NextDouble() - 0.5f) * 0.08f;
                        var c = Goods[rng.Next(Goods.Length)];
                        switch (rng.Next(3))
                        {
                            case 0: Visuals.Prim(PrimitiveType.Cylinder, t, new Vector3(x, y + 0.14f, z + 0.03f), new Vector3(0.16f, 0.12f, 0.16f), c); break;
                            case 1: Visuals.Prim(PrimitiveType.Sphere, t, new Vector3(x, y + 0.12f, z + 0.03f), new Vector3(0.2f, 0.2f, 0.2f), c); break;
                            default: Box(t, x, y + 0.1f, z + 0.03f, 0.2f, 0.16f, 0.22f, Burlap * (0.8f + (float)rng.NextDouble() * 0.3f)); break;
                        }
                    }
            }
            return 1.9f;
        }

        // Two barrels (or one and a bucket), banded with iron.
        static float Barrels(Transform t, System.Random rng)
        {
            float z = Back + 0.35f;
            Barrel(t, new Vector3(-0.18f, 0f, z), 1f);
            if (rng.Next(2) == 0)
                Barrel(t, new Vector3(0.28f, 0f, z + 0.15f), 0.7f);
            else
            {
                Visuals.Prim(PrimitiveType.Cylinder, t, new Vector3(0.3f, 0.13f, z + 0.2f), new Vector3(0.26f, 0.13f, 0.26f), LightWood);
                Visuals.Prim(PrimitiveType.Cylinder, t, new Vector3(0.3f, 0.24f, z + 0.2f), new Vector3(0.22f, 0.01f, 0.22f), new Color(0.3f, 0.45f, 0.55f));
            }
            return 0.9f;
        }

        static void Barrel(Transform t, Vector3 at, float s)
        {
            Visuals.Prim(PrimitiveType.Cylinder, t, at + new Vector3(0f, 0.42f * s, 0f), new Vector3(0.52f * s, 0.42f * s, 0.52f * s), Wood);
            Visuals.Prim(PrimitiveType.Cylinder, t, at + new Vector3(0f, 0.42f * s, 0f), new Vector3(0.58f * s, 0.2f * s, 0.58f * s), Wood * 1.08f);  // the bulge
            foreach (float y in new[] { 0.14f, 0.7f })
                Visuals.Prim(PrimitiveType.Cylinder, t, at + new Vector3(0f, y * s, 0f), new Vector3(0.56f * s, 0.03f, 0.56f * s), Iron);
            Visuals.Prim(PrimitiveType.Cylinder, t, at + new Vector3(0f, 0.84f * s, 0f), new Vector3(0.48f * s, 0.01f, 0.48f * s), LightWood);
        }

        // Crates, stacked a little askew.
        static float Crates(Transform t, System.Random rng)
        {
            float z = Back + 0.32f;
            Crate(t, new Vector3(-0.14f, 0f, z), 0.55f, 0f);
            Crate(t, new Vector3(0.28f, 0f, z + 0.08f), 0.4f, 12f);
            Crate(t, new Vector3(-0.1f, 0.55f, z), 0.4f, (float)rng.NextDouble() * 30f - 15f);
            return 0.95f;
        }

        static void Crate(Transform t, Vector3 at, float s, float turn)
        {
            var c = new GameObject("Crate").transform;
            c.SetParent(t, false);
            c.localPosition = at;
            c.localRotation = Quaternion.Euler(0f, turn, 0f);
            Box(c, 0f, s / 2f, 0f, s, s, s, LightWood);
            foreach (float y in new[] { 0.08f * s, 0.92f * s })
                Box(c, 0f, y, 0f, s + 0.02f, 0.06f, s + 0.02f, Wood);
            Box(c, 0f, s / 2f, s / 2f + 0.005f, 0.06f, s, 0.01f, Wood).transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
        }

        // Sacks of grain or flour, slumped against the wall.
        static float Sacks(Transform t)
        {
            float z = Back + 0.3f;
            for (int i = 0; i < 3; i++)
            {
                var at = new Vector3(-0.25f + i * 0.25f, 0.24f, z + (i == 1 ? 0.12f : 0f));
                Visuals.Prim(PrimitiveType.Sphere, t, at, new Vector3(0.36f, 0.5f, 0.34f), Burlap * (0.9f + i * 0.06f));
                Visuals.Prim(PrimitiveType.Cylinder, t, at + new Vector3(0f, 0.27f, 0f), new Vector3(0.1f, 0.05f, 0.1f), Burlap * 0.75f);
            }
            return 0.6f;
        }

        // A stone fireplace with a mantel, logs and embers; its chimney rises behind it (TownBuildings).
        static float Hearth(Transform t)
        {
            float z = Back + 0.3f;
            Box(t, 0f, 0.6f, z, 1.0f, 1.2f, 0.6f, StoneC);
            Box(t, 0f, 0.42f, z + 0.12f, 0.6f, 0.62f, 0.4f, new Color(0.1f, 0.08f, 0.07f));   // the fire's mouth
            Box(t, 0f, 0.05f, z + 0.38f, 1.1f, 0.1f, 0.3f, StoneC * 0.85f);                    // the hearthstone
            Box(t, 0f, 1.24f, z + 0.06f, 1.16f, 0.08f, 0.72f, DarkWood);                       // the mantel
            foreach (float x in new[] { -0.08f, 0.08f })
                Visuals.Prim(PrimitiveType.Cylinder, t, new Vector3(x, 0.16f, z + 0.22f), new Vector3(0.09f, 0.22f, 0.09f), DarkWood).transform.localRotation = Quaternion.Euler(0f, x * 300f, 90f);
            Glow(t, PrimitiveType.Sphere, new Vector3(0f, 0.18f, z + 0.24f), new Vector3(0.3f, 0.1f, 0.18f), Ember);
            Glow(t, PrimitiveType.Sphere, new Vector3(0f, 0.32f, z + 0.24f), new Vector3(0.12f, 0.22f, 0.1f), Flame);
            Visuals.Prim(PrimitiveType.Cylinder, t, new Vector3(-0.35f, 1.36f, z + 0.1f), new Vector3(0.1f, 0.08f, 0.1f), Goods[1]);
            Visuals.Prim(PrimitiveType.Sphere, t, new Vector3(0.32f, 1.35f, z + 0.1f), new Vector3(0.14f, 0.14f, 0.14f), Goods[4]);
            return 1.3f;
        }

        // A clerk's desk: a ledger, an ink pot, a candle, and a stool tucked in.
        static float Desk(Transform t)
        {
            float z = Back + 0.32f;
            Box(t, 0f, 0.74f, z, 0.95f, 0.06f, 0.56f, Wood);
            foreach (float x in new[] { -0.42f, 0.42f })
                Box(t, x, 0.36f, z, 0.07f, 0.72f, 0.5f, DarkWood);
            Box(t, -0.12f, 0.79f, z + 0.04f, 0.36f, 0.04f, 0.26f, new Color(0.45f, 0.15f, 0.12f));
            Box(t, -0.12f, 0.815f, z + 0.04f, 0.32f, 0.01f, 0.22f, Linen);
            Visuals.Prim(PrimitiveType.Cylinder, t, new Vector3(0.22f, 0.8f, z + 0.1f), new Vector3(0.06f, 0.03f, 0.06f), new Color(0.1f, 0.1f, 0.15f));
            Visuals.Prim(PrimitiveType.Cylinder, t, new Vector3(0.34f, 0.84f, z - 0.12f), new Vector3(0.05f, 0.07f, 0.05f), Linen);
            Glow(t, PrimitiveType.Sphere, new Vector3(0.34f, 0.94f, z - 0.12f), new Vector3(0.04f, 0.07f, 0.04f), Flame);
            Visuals.Prim(PrimitiveType.Cylinder, t, new Vector3(0f, 0.25f, z + 0.42f), new Vector3(0.36f, 0.025f, 0.36f), Wood);
            Visuals.Prim(PrimitiveType.Cylinder, t, new Vector3(0f, 0.12f, z + 0.42f), new Vector3(0.08f, 0.12f, 0.08f), DarkWood);
            return 0.95f;
        }

        // A pew (one or two tiles long), facing the altar: seat, back, ends.
        static float Pew(Transform t, int length)
        {
            float w = length - 0.15f;
            Box(t, 0f, 0.44f, 0.05f, w, 0.06f, 0.42f, Wood);
            Box(t, 0f, 0.72f, -0.18f, w, 0.5f, 0.05f, Wood);
            Box(t, 0f, 0.98f, -0.18f, w + 0.04f, 0.05f, 0.09f, DarkWood);
            foreach (float x in new[] { -w / 2f, w / 2f })
                Box(t, x, 0.48f, -0.02f, 0.06f, 0.96f, 0.56f, DarkWood);
            if (length > 1)
                Box(t, 0f, 0.22f, 0.05f, 0.06f, 0.44f, 0.3f, DarkWood);
            return 1f;
        }

        // A tall iron stand of lit candles.
        static float Candles(Transform t)
        {
            float z = Back + 0.4f;
            Visuals.Prim(PrimitiveType.Cylinder, t, new Vector3(0f, 0.03f, z), new Vector3(0.36f, 0.03f, 0.36f), Iron);
            Visuals.Prim(PrimitiveType.Cylinder, t, new Vector3(0f, 0.6f, z), new Vector3(0.05f, 0.6f, 0.05f), Iron);
            Box(t, 0f, 1.2f, z, 0.6f, 0.03f, 0.05f, Iron);
            Box(t, 0f, 1.2f, z, 0.05f, 0.03f, 0.6f, Iron);
            foreach (var p in new[] { new Vector3(0f, 0f, 0f), new Vector3(0.27f, 0f, 0f), new Vector3(-0.27f, 0f, 0f), new Vector3(0f, 0f, 0.27f), new Vector3(0f, 0f, -0.27f) })
            {
                float h = p == Vector3.zero ? 0.28f : 0.18f;
                Visuals.Prim(PrimitiveType.Cylinder, t, new Vector3(p.x, 1.22f + h / 2f, z + p.z), new Vector3(0.06f, h / 2f, 0.06f), Linen);
                Glow(t, PrimitiveType.Sphere, new Vector3(p.x, 1.25f + h + 0.03f, z + p.z), new Vector3(0.045f, 0.08f, 0.045f), Flame);
            }
            return 1.6f;
        }

        // A water trough for quenching, of stone.
        static float Trough(Transform t)
        {
            float z = Back + 0.3f;
            Box(t, 0f, 0.3f, z, 0.9f, 0.6f, 0.5f, StoneC * 0.9f);
            Box(t, 0f, 0.56f, z, 0.76f, 0.04f, 0.36f, new Color(0.22f, 0.36f, 0.45f));
            Box(t, 0.25f, 0.7f, z, 0.03f, 0.4f, 0.03f, Iron).transform.localRotation = Quaternion.Euler(0f, 0f, 25f);  // tongs left in it
            return 0.6f;
        }

        // A bin heaped with coal, a shovel in it.
        static float CoalHeap(Transform t)
        {
            float z = Back + 0.32f;
            Box(t, 0f, 0.16f, z - 0.24f, 0.9f, 0.32f, 0.06f, Wood);
            foreach (float x in new[] { -0.43f, 0.43f })
                Box(t, x, 0.16f, z, 0.06f, 0.32f, 0.54f, Wood);
            var coal = new Color(0.1f, 0.1f, 0.11f);
            for (int i = 0; i < 9; i++)
            {
                float a = i * 2.1f, r = 0.08f + (i % 3) * 0.1f;
                Visuals.Prim(PrimitiveType.Sphere, t, new Vector3(Mathf.Cos(a) * r, 0.18f + (i % 4) * 0.06f, z + Mathf.Sin(a) * r * 0.6f), Vector3.one * (0.22f + (i % 3) * 0.06f), coal * (1f + (i % 3) * 0.2f));
            }
            Box(t, 0.2f, 0.55f, z - 0.05f, 0.04f, 0.7f, 0.04f, Wood).transform.localRotation = Quaternion.Euler(-15f, 0f, -20f);
            return 0.6f;
        }

        // A rack on the wall with swords and a shield.
        static float WeaponRack(Transform t)
        {
            float z = Back + 0.1f;
            foreach (float y in new[] { 0.5f, 1.3f })
                Box(t, 0f, y, z, 0.95f, 0.08f, 0.1f, Wood);
            foreach (float x in new[] { -0.44f, 0.44f })
                Box(t, x, 0.8f, z, 0.07f, 1.6f, 0.1f, DarkWood);
            foreach (float x in new[] { -0.28f, -0.12f })
            {
                Box(t, x, 0.95f, z + 0.08f, 0.05f, 0.95f, 0.015f, new Color(0.75f, 0.77f, 0.8f));
                Box(t, x, 0.45f, z + 0.08f, 0.18f, 0.03f, 0.04f, Iron);
                Box(t, x, 0.34f, z + 0.08f, 0.04f, 0.18f, 0.04f, DarkWood);
            }
            Visuals.Prim(PrimitiveType.Cylinder, t, new Vector3(0.2f, 0.95f, z + 0.1f), new Vector3(0.48f, 0.02f, 0.48f), new Color(0.5f, 0.2f, 0.15f)).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Visuals.Prim(PrimitiveType.Sphere, t, new Vector3(0.2f, 0.95f, z + 0.12f), new Vector3(0.1f, 0.1f, 0.05f), Iron);
            return 1.6f;
        }

        // A grindstone on a wooden frame, with a crank.
        static float Grindstone(Transform t)
        {
            float z = Back + 0.38f;
            foreach (float x in new[] { -0.25f, 0.25f })
                Box(t, x, 0.35f, z, 0.07f, 0.7f, 0.5f, Wood);
            Box(t, 0f, 0.12f, z, 0.56f, 0.07f, 0.5f, Wood);
            Visuals.Prim(PrimitiveType.Cylinder, t, new Vector3(0f, 0.72f, z), new Vector3(0.62f, 0.07f, 0.62f), StoneC * 1.05f).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            Visuals.Prim(PrimitiveType.Cylinder, t, new Vector3(0f, 0.72f, z), new Vector3(0.05f, 0.34f, 0.05f), Iron).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            Box(t, 0.36f, 0.62f, z, 0.03f, 0.22f, 0.03f, Iron);
            return 1.05f;
        }

        // Baskets of wool, one spilling over.
        static float WoolBaskets(Transform t)
        {
            float z = Back + 0.32f;
            var wool = new Color(0.93f, 0.92f, 0.88f);
            foreach (var at in new[] { new Vector3(-0.22f, 0f, z), new Vector3(0.26f, 0f, z + 0.1f) })
            {
                Visuals.Prim(PrimitiveType.Cylinder, t, at + new Vector3(0f, 0.2f, 0f), new Vector3(0.42f, 0.2f, 0.42f), Burlap * 0.85f);
                Visuals.Prim(PrimitiveType.Cylinder, t, at + new Vector3(0f, 0.39f, 0f), new Vector3(0.45f, 0.025f, 0.45f), Wood);
                for (int i = 0; i < 3; i++)
                    Visuals.Prim(PrimitiveType.Sphere, t, at + new Vector3(-0.08f + i * 0.08f, 0.44f + (i % 2) * 0.04f, (i - 1) * 0.06f), Vector3.one * 0.2f, wool * (0.95f + i * 0.02f));
            }
            Visuals.Prim(PrimitiveType.Sphere, t, new Vector3(0.02f, 0.08f, z + 0.3f), new Vector3(0.18f, 0.12f, 0.18f), new Color(0.85f, 0.8f, 0.62f));  // a ball of yarn
            return 0.6f;
        }

        // A frame with a hide stretched on it, leaning on the wall.
        static float HideRack(Transform t)
        {
            float z = Back + 0.28f;
            var frame = new GameObject("Frame").transform;
            frame.SetParent(t, false);
            frame.localPosition = new Vector3(0f, 0f, z);
            frame.localRotation = Quaternion.Euler(-12f, 0f, 0f);
            foreach (float x in new[] { -0.42f, 0.42f })
                Box(frame, x, 0.8f, 0f, 0.07f, 1.6f, 0.07f, LightWood);
            foreach (float y in new[] { 0.3f, 1.45f })
                Box(frame, 0f, y, 0f, 0.92f, 0.06f, 0.06f, LightWood);
            Box(frame, 0f, 0.88f, 0.03f, 0.7f, 1.0f, 0.02f, new Color(0.55f, 0.36f, 0.22f));
            Box(frame, 0f, 0.88f, 0.042f, 0.5f, 0.7f, 0.01f, new Color(0.62f, 0.43f, 0.28f));
            return 1.6f;
        }
    }
}
