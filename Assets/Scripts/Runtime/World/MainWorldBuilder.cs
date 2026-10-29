using System.Collections.Generic;
using Epacenur.Content;
using Epacenur.Core;
using UnityEngine;

namespace Epacenur.Runtime
{
    /// Turns a generated MainWorldLayout into the 3D world: ground, roads, ponds with fishing spots,
    /// forests, buildings (bank, general store, chapel, houses) and the goblin camp.
    public static class MainWorldBuilder
    {
        static readonly Color Grass = new Color(0.34f, 0.53f, 0.25f), GrassLine = new Color(0.29f, 0.46f, 0.21f);
        static readonly Color Dirt = new Color(0.55f, 0.45f, 0.3f), CampDirt = new Color(0.45f, 0.38f, 0.27f);
        static readonly Color Planks = new Color(0.5f, 0.36f, 0.22f), Water = new Color(0.2f, 0.42f, 0.7f), Sand = new Color(0.76f, 0.68f, 0.48f);
        static readonly Color Stone = new Color(0.62f, 0.6f, 0.56f), Timber = new Color(0.55f, 0.4f, 0.25f), Trim = new Color(0.35f, 0.25f, 0.16f);

        /// Builds the layout with its (0, 0) at `origin` in the world grid. Returns the spawn tile.
        public static Tile Build(World world, MainWorldLayout layout, Tile origin)
        {
            System.Func<Tile, Tile> G = t => new Tile(t.X + origin.X, t.Z + origin.Z);
            var region = new RectInt(origin.X, origin.Z, layout.Width, layout.Height);
            var root = world.transform;
            var rng = new System.Random(layout.Seed);

            world.BuildGround(region, Grass, GrassLine);
            world.BuildWalls(region, new Color(0.45f, 0.36f, 0.26f));  // a wooden palisade round the edge

            // Flat overlays, one strip per run of tiles in a row: roads, the camp, floors, water (with a sandy edge).
            Strips(layout, Terrain.Path, origin, root, Dirt, 0.012f, 0.02f, 0f);
            Strips(layout, Terrain.Camp, origin, root, CampDirt, 0.012f, 0.02f, 0f);
            Strips(layout, Terrain.Floor, origin, root, Planks, 0.015f, 0.03f, 0f);
            Strips(layout, Terrain.Door, origin, root, Planks, 0.015f, 0.03f, 0f);
            Strips(layout, Terrain.Water, origin, root, Sand, 0.008f, 0.016f, 0.35f);
            Strips(layout, Terrain.Water, origin, root, Water, 0.025f, 0.03f, 0f);
            for (int x = 0; x < layout.Width; x++)
                for (int z = 0; z < layout.Height; z++)
                    if (layout.Map[x, z] == Terrain.Water)
                        world.Grid.SetBlocked(G(new Tile(x, z)), true);

            // Forests.
            for (int x = 0; x < layout.Width; x++)
                for (int z = 0; z < layout.Height; z++)
                    if (layout.Map[x, z] == Terrain.Tree)
                        world.AddNode(WoodcuttingModule.Tree, G(new Tile(x, z)), rng);

            // Buildings.
            foreach (var b in layout.Buildings)
                BuildBuilding(world, b, G);

            // Fishing spots: one per pond, two on big ones.
            foreach (var shore in layout.PondShores)
            {
                var tiles = shore.ConvertAll(t => G(t));
                int spots = tiles.Count >= 14 ? 2 : 1;
                for (int i = 0; i < spots; i++)
                    world.AddNode(FishingModule.NetSpot, tiles[rng.Next(tiles.Count)], rng, tiles);
            }

            // The goblin camp: a few tents and goblins.
            var camp = layout.GoblinCamp;
            if (camp.Width > 0)
            {
                var home = new RectInt(camp.X + origin.X + 1, camp.Z + origin.Z + 1, camp.Width - 2, camp.Height - 2);
                for (int i = 0; i < 2; i++)
                    Tent(world, G(new Tile(camp.X + 2 + i * (camp.Width - 5), camp.ZMax - 2)));
                for (int i = 0; i < 4; i++)
                    world.AddNpc(ContentDb.Current.Npc(CombatModule.Goblin), new Tile(home.x + rng.Next(home.width), home.y + rng.Next(home.height - 2)), home);
                world.AddLabel(World.TileCenter(G(camp.Center)) + Vector3.up * 0.4f, "Goblin camp", new Color(1f, 0.6f, 0.5f));
            }

            var spawn = G(layout.Spawn);
            world.AddLabel(World.TileCenter(spawn) + new Vector3(0f, 0.3f, 2f), "Epacenur", new Color(1f, 0.9f, 0.5f));
            return spawn;
        }

        /// One flat box per horizontal run of `kind` tiles. `grow` widens each run (for sandy pond edges).
        static void Strips(MainWorldLayout layout, Terrain kind, Tile origin, Transform root, Color colour, float y, float thickness, float grow)
        {
            for (int z = 0; z < layout.Height; z++)
            {
                int x = 0;
                while (x < layout.Width)
                {
                    if (layout.Map[x, z] != kind)
                    {
                        x++;
                        continue;
                    }
                    int start = x;
                    while (x < layout.Width && layout.Map[x, z] == kind)
                        x++;
                    float len = x - start;
                    var pos = new Vector3(origin.X + start + (len - 1) / 2f, y, origin.Z + z);
                    Visuals.Prim(PrimitiveType.Cube, root, pos, new Vector3(len + grow * 2f, thickness, 1f + grow * 2f), colour);
                }
            }
        }

        static void BuildBuilding(World world, Building b, System.Func<Tile, Tile> G)
        {
            var root = world.transform;
            var a = b.Footprint;
            bool stone = b.Kind == BuildingKind.Bank || b.Kind == BuildingKind.Chapel;
            var wallColour = stone ? Stone : Timber;
            const float height = 1.7f, thick = 0.45f;

            // Walls: the four sides, the door's side in two pieces, plus a beam over the door.
            var lo = G(new Tile(a.X, a.Z));
            var hi = G(new Tile(a.XMax - 1, a.ZMax - 1));
            var door = G(b.Door);
            WallRun(root, lo.X, hi.X, lo.Z, true, door, height, thick, wallColour);
            WallRun(root, lo.X, hi.X, hi.Z, true, door, height, thick, wallColour);
            WallRun(root, lo.Z, hi.Z, lo.X, false, door, height, thick, wallColour);
            WallRun(root, lo.Z, hi.Z, hi.X, false, door, height, thick, wallColour);
            for (int x = lo.X; x <= hi.X; x++)
                for (int z = lo.Z; z <= hi.Z; z++)
                    if (x == lo.X || z == lo.Z || x == hi.X || z == hi.Z)
                        world.Grid.SetBlocked(new Tile(x, z), new Tile(x, z) != door);
            bool doorOnX = door.Z == lo.Z || door.Z == hi.Z;  // door in a wall running along x
            Visuals.Prim(PrimitiveType.Cube, root, World.TileCenter(door) + Vector3.up * (height - 0.15f),
                doorOnX ? new Vector3(1f, 0.3f, thick + 0.05f) : new Vector3(thick + 0.05f, 0.3f, 1f), Trim);
            // Darker trim along the top of the walls.
            float cx = (lo.X + hi.X) / 2f, cz = (lo.Z + hi.Z) / 2f;
            float w = hi.X - lo.X + 1, d = hi.Z - lo.Z + 1;
            Visuals.Prim(PrimitiveType.Cube, root, new Vector3(cx, height, lo.Z), new Vector3(w, 0.08f, thick + 0.08f), Trim);
            Visuals.Prim(PrimitiveType.Cube, root, new Vector3(cx, height, hi.Z), new Vector3(w, 0.08f, thick + 0.08f), Trim);
            Visuals.Prim(PrimitiveType.Cube, root, new Vector3(lo.X, height, cz), new Vector3(thick + 0.08f, 0.08f, d), Trim);
            Visuals.Prim(PrimitiveType.Cube, root, new Vector3(hi.X, height, cz), new Vector3(thick + 0.08f, 0.08f, d), Trim);

            // What's inside, against the back wall, facing the door.
            var feature = G(b.Feature);
            Vector3 toDoor = World.TileCenter(door) - World.TileCenter(feature);
            toDoor.y = 0f;
            var facingDoor = Quaternion.LookRotation(toDoor);
            var outside = G(WorldGen.OutsideDoor(b));
            Vector3 signAt = World.TileCenter(outside) + Vector3.up * 2.3f;
            switch (b.Kind)
            {
                case BuildingKind.Bank:
                    // The booth's counter faces -z, so turn its back to the door.
                    world.AddStation(CommonModule.BankBooth, feature).transform.rotation = Quaternion.LookRotation(-toDoor);
                    world.AddLabel(signAt, "Bank", new Color(1f, 0.85f, 0.3f));
                    break;
                case BuildingKind.Shop:
                    world.AddShopKeeper(feature, ShopsModule.GeneralStore).SetFacing(facingDoor);
                    world.AddLabel(signAt, "General Store", new Color(1f, 0.85f, 0.3f));
                    break;
                case BuildingKind.Chapel:
                    world.AddStation(PrayerModule.Altar, feature).transform.rotation = Quaternion.LookRotation(-toDoor);
                    world.AddLabel(signAt, "Chapel", new Color(1f, 0.85f, 0.3f));
                    break;
                default:
                    Table(world, feature);
                    break;
            }
        }

        /// A straight wall from `from` to `to` (inclusive tiles) along x (at z = `at`) or along z (at x = `at`),
        /// leaving a gap where the door is.
        static void WallRun(Transform root, int from, int to, int at, bool alongX, Tile door, float height, float thick, Color colour)
        {
            int doorPos = alongX ? (door.Z == at ? door.X : int.MinValue) : (door.X == at ? door.Z : int.MinValue);
            if (doorPos >= from && doorPos <= to)
            {
                if (doorPos > from)
                    WallPiece(root, from, doorPos - 1, at, alongX, height, thick, colour);
                if (doorPos < to)
                    WallPiece(root, doorPos + 1, to, at, alongX, height, thick, colour);
            }
            else
            {
                WallPiece(root, from, to, at, alongX, height, thick, colour);
            }
        }

        static void WallPiece(Transform root, int from, int to, int at, bool alongX, float height, float thick, Color colour)
        {
            float mid = (from + to) / 2f, len = to - from + 1;
            var pos = alongX ? new Vector3(mid, height / 2f, at) : new Vector3(at, height / 2f, mid);
            var size = alongX ? new Vector3(len, height, thick) : new Vector3(thick, height, len);
            Visuals.Prim(PrimitiveType.Cube, root, pos, size, colour, true);  // keeps its collider: WASD can't walk through
        }

        /// A table in a house: blocks its tile.
        static void Table(World world, Tile tile)
        {
            var t = new GameObject("Table").transform;
            t.SetParent(world.transform, false);
            t.localPosition = World.TileCenter(tile);
            var wood = new Color(0.45f, 0.3f, 0.17f);
            Visuals.Prim(PrimitiveType.Cube, t, new Vector3(0f, 0.7f, 0f), new Vector3(0.9f, 0.08f, 0.7f), wood);
            foreach (float x in new[] { -0.38f, 0.38f })
                foreach (float z in new[] { -0.28f, 0.28f })
                    Visuals.Prim(PrimitiveType.Cube, t, new Vector3(x, 0.35f, z), new Vector3(0.07f, 0.7f, 0.07f), wood);
            Visuals.Prim(PrimitiveType.Cylinder, t, new Vector3(0.15f, 0.78f, 0f), new Vector3(0.15f, 0.04f, 0.15f), new Color(0.85f, 0.85f, 0.8f));
            var box = t.gameObject.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.4f, 0f);
            box.size = new Vector3(0.9f, 0.8f, 0.7f);
            world.Grid.SetBlocked(tile, true);
        }

        /// A goblin tent: a sloping hide roof on poles. Blocks its tile.
        static void Tent(World world, Tile tile)
        {
            var t = new GameObject("Tent").transform;
            t.SetParent(world.transform, false);
            t.localPosition = World.TileCenter(tile);
            var hide = new Color(0.55f, 0.42f, 0.3f);
            var left = Visuals.Prim(PrimitiveType.Cube, t, new Vector3(-0.3f, 0.55f, 0f), new Vector3(0.05f, 1.3f, 1.2f), hide);
            left.transform.localRotation = Quaternion.Euler(0f, 0f, -30f);
            var right = Visuals.Prim(PrimitiveType.Cube, t, new Vector3(0.3f, 0.55f, 0f), new Vector3(0.05f, 1.3f, 1.2f), hide);
            right.transform.localRotation = Quaternion.Euler(0f, 0f, 30f);
            Visuals.Prim(PrimitiveType.Cylinder, t, new Vector3(0f, 1.1f, 0f), new Vector3(0.05f, 0.65f, 0.05f), new Color(0.35f, 0.24f, 0.14f))
                .transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var box = t.gameObject.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.6f, 0f);
            box.size = new Vector3(1f, 1.2f, 1.2f);
            world.Grid.SetBlocked(tile, true);
        }
    }
}
