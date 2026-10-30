using System.Collections.Generic;
using Epacenur.Content;
using Epacenur.Core;
using UnityEngine;

namespace Epacenur.Runtime
{
    /// Builds the overworld's 3D pieces from the generator (Core: Overworld): a chunk's ground, roads,
    /// ponds, trees, rocks and fishing spot, and a region's buildings, goblin camp and place names.
    public static class OverworldBuilder
    {
        static readonly Color Dirt = new Color(0.55f, 0.45f, 0.3f), CampDirt = new Color(0.45f, 0.38f, 0.27f);
        static readonly Color Planks = new Color(0.5f, 0.36f, 0.22f), Water = new Color(0.2f, 0.42f, 0.7f), Sand = new Color(0.76f, 0.68f, 0.48f);
        static readonly Color Stone = new Color(0.62f, 0.6f, 0.56f), Timber = new Color(0.55f, 0.4f, 0.25f), Trim = new Color(0.35f, 0.25f, 0.16f);
        static readonly Color Gold = new Color(1f, 0.85f, 0.3f), Paving = new Color(0.58f, 0.58f, 0.6f);

        // ---- Chunks ----

        public static void BuildChunk(World world, int cx, int cz, Material groundMat)
        {
            var ow = world.Overworld;
            int size = Overworld.ChunkSize, x0 = cx * size, z0 = cz * size;
            var root = world.BuildRoot;
            var terrain = new Terrain[size, size];
            for (int x = 0; x < size; x++)
                for (int z = 0; z < size; z++)
                    terrain[x, z] = ow.At(new Tile(x0 + x, z0 + z));

            Ground(ow, x0, z0, size, root, groundMat);

            // Flat overlays, one strip per run of tiles in a row: roads, camps, floors, water (with a sandy edge).
            Strips(terrain, Terrain.Path, x0, z0, root, Dirt, 0.012f, 0.02f, 0f);
            Strips(terrain, Terrain.Camp, x0, z0, root, CampDirt, 0.012f, 0.02f, 0f);
            Strips(terrain, Terrain.Stone, x0, z0, root, Paving, 0.014f, 0.025f, 0f);
            Strips(terrain, Terrain.Floor, x0, z0, root, Planks, 0.015f, 0.03f, 0f);
            Strips(terrain, Terrain.Door, x0, z0, root, Planks, 0.015f, 0.03f, 0f);
            Strips(terrain, Terrain.Water, x0, z0, root, Sand, 0.008f, 0.016f, 0.35f);
            Strips(terrain, Terrain.Water, x0, z0, root, Water, 0.025f, 0.03f, 0f);

            // Trees and rocks. Each chunk has its own random numbers, so it looks the same every time.
            var rng = new System.Random((int)(ow.Hash(cx, cz, 11) * int.MaxValue));
            var tiles = new List<Tile>();
            for (int x = 0; x < size; x++)
                for (int z = 0; z < size; z++)
                {
                    var t = new Tile(x0 + x, z0 + z);
                    tiles.Add(t);
                    // Which tree or rock grows here depends on how far out it is (the nodes' Spawns).
                    if (terrain[x, z] == Terrain.Tree)
                        world.AddNode(Spawning.NodeAt(ow, t, Terrain.Tree)?.Id ?? WoodcuttingModule.Tree, t, rng);
                    else if (terrain[x, z] == Terrain.Rock)
                        world.AddNode(Spawning.NodeAt(ow, t, Terrain.Rock)?.Id ?? MiningModule.CopperRock, t, rng);
                    else if (terrain[x, z] == Terrain.Stall)
                        world.AddNode(Spawning.NodeAt(ow, t, Terrain.Stall)?.Id ?? ThievingModule.BakeryStall, t, rng);
                    else if (terrain[x, z] == Terrain.Flax)
                        world.AddNode(Spawning.NodeAt(ow, t, Terrain.Flax)?.Id ?? CraftingModule.FlaxPlant, t, rng);
                }

            // A fishing spot on a pond's shore, moving along the part of it in this chunk.
            var shore = ow.Shore(tiles);
            if (shore.Count >= 4)
            {
                var at = shore[rng.Next(shore.Count)];
                world.AddNode(Spawning.NodeAt(ow, at, Terrain.Water)?.Id ?? FishingModule.NetSpot, at, rng, shore);
            }
        }

        /// One flat box per horizontal run of `kind` tiles. `grow` widens each run (for sandy pond edges).
        /// The chunk's ground: a sheet over the hills, a corner at each tile corner (neighbouring chunks
        /// work out the same heights along their shared edge, so there are no gaps).
        static void Ground(Overworld ow, int x0, int z0, int size, Transform root, Material groundMat)
        {
            int n = size + 1;
            var verts = new Vector3[n * n];
            var uv = new Vector2[n * n];
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                {
                    float x = x0 - 0.5f + i, z = z0 - 0.5f + j;
                    verts[i * n + j] = new Vector3(x, (float)ow.Elevation(x, z), z);
                    uv[i * n + j] = new Vector2(i / (float)size, j / (float)size);  // the material repeats its tile texture per tile
                }
            var tris = new int[size * size * 6];
            int k = 0;
            for (int i = 0; i < size; i++)
                for (int j = 0; j < size; j++)
                {
                    int a = i * n + j, b = a + 1, c = a + n, d = c + 1;
                    tris[k++] = a; tris[k++] = b; tris[k++] = c;
                    tris[k++] = c; tris[k++] = b; tris[k++] = d;
                }
            var mesh = new Mesh { name = "Ground" };
            mesh.vertices = verts;
            mesh.uv = uv;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            var go = new GameObject("Ground");
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = groundMat;
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
            go.AddComponent<OwnedMesh>().Mesh = mesh;
        }

        static void Strips(Terrain[,] terrain, Terrain kind, int x0, int z0, Transform root, Color colour, float y, float thickness, float grow)
        {
            int size = terrain.GetLength(0);
            for (int z = 0; z < size; z++)
            {
                int x = 0;
                while (x < size)
                {
                    if (terrain[x, z] != kind)
                    {
                        x++;
                        continue;
                    }
                    int start = x;
                    while (x < size && terrain[x, z] == kind)
                        x++;
                    float len = x - start;
                    var pos = new Vector3(x0 + start + (len - 1) / 2f, y + World.Height(new Tile(x0 + start, z0 + z)), z0 + z);
                    Visuals.Prim(PrimitiveType.Cube, root, pos, new Vector3(len + grow * 2f, thickness, 1f + grow * 2f), colour);
                }
            }
        }

        // ---- Regions ----

        public static void BuildRegion(World world, RegionPlan plan)
        {
            if (plan.Kind == RegionKind.Town)
            {
                foreach (var b in plan.Buildings)
                    BuildBuilding(world, b);
                bool home = plan.RX == 0 && plan.RZ == 0;
                world.AddLabel(World.Local(plan.Center) + new Vector3(0f, 0.3f, 2f), home ? "Epacenur" : TownName(world.Overworld, plan), new Color(1f, 0.9f, 0.5f));
            }
            else if (plan.Kind == RegionKind.Camp)
            {
                foreach (var tent in plan.Tents)
                    Tent(world, tent);
                world.AddLabel(World.Local(plan.Camp.Center) + Vector3.up * 0.4f, RegionName(world.Overworld, plan), new Color(1f, 0.6f, 0.5f));
            }
            else if (plan.Kind == RegionKind.Course)
            {
                var course = Spawning.CourseIn(world.Overworld, plan);
                if (course != null)
                {
                    foreach (var p in plan.Obstacles)
                        world.AddObstacle(course, p);
                    var mid = new Tile(plan.CourseCorner.X + AgilityCourse.Width / 2, plan.CourseCorner.Z + AgilityCourse.Depth / 2);
                    world.AddLabel(World.Local(mid) + Vector3.up * 0.6f, course.Name + " (Agility " + course.Level + ")", new Color(0.6f, 0.8f, 1f));
                }
            }
            else if (plan.Kind == RegionKind.Shrine)
            {
                // A ring of standing stones round a runecrafting altar (which altar: Spawning, by distance).
                var altar = Spawning.ShrineAltar(world.Overworld, plan);
                if (altar != null)
                    world.AddStation(altar.Id, plan.Altar);
                foreach (var stone in plan.Stones)
                    StandingStone(world, stone, (int)(world.Overworld.Hash(stone.X, stone.Z, 7) * 4));
            }
            SpawnMonsters(world, plan);
        }

        /// The region's monsters (Spawning: by kind of region and distance): a camp's occupants in the camp,
        /// otherwise each group round a spot of open ground of its own.
        static void SpawnMonsters(World world, RegionPlan plan)
        {
            var ow = world.Overworld;
            var rng = new System.Random((int)(ow.Hash(plan.RX, plan.RZ, 12) * int.MaxValue));
            var groups = Spawning.GroupsIn(ow, plan);
            for (int g = 0; g < groups.Count; g++)
            {
                RectInt home;
                if (plan.Kind == RegionKind.Camp)
                    home = new RectInt(plan.Camp.X + 1, plan.Camp.Z + 1, plan.Camp.Width - 2, plan.Camp.Height - 2);
                else
                {
                    // A spot of open ground: in a town, round the edge of the square; elsewhere anywhere in the region.
                    Tile spot = plan.Center;
                    for (int tries = 0; tries < 40; tries++)
                    {
                        var t = plan.Kind == RegionKind.Town
                            ? new Tile(plan.Center.X + rng.Next(-18, 19), plan.Center.Z + rng.Next(-18, 19))
                            : new Tile(plan.Bounds.X + 8 + rng.Next(Overworld.RegionSize - 16), plan.Bounds.Z + 8 + rng.Next(Overworld.RegionSize - 16));
                        if (!ow.IsBlocked(t) && (plan.Kind != RegionKind.Town || t.StepsTo(plan.Center) > 6))
                        {
                            spot = t;
                            break;
                        }
                    }
                    home = new RectInt(spot.X - 4, spot.Z - 4, 9, 9);
                }
                for (int i = 0; i < groups[g].Count; i++)
                {
                    // Start on an open tile in the home area.
                    var at = new Tile(home.x + home.width / 2, home.y + home.height / 2);
                    for (int tries = 0; tries < 20; tries++)
                    {
                        var t = new Tile(home.x + rng.Next(home.width), home.y + rng.Next(home.height));
                        if (!ow.IsBlocked(t))
                        {
                            at = t;
                            break;
                        }
                    }
                    world.AddNpc(groups[g].Npc, at, home);
                }
            }
        }

        /// What a region is called on the world map (null for countryside).
        public static string RegionName(Overworld ow, RegionPlan plan)
        {
            if (plan.Kind == RegionKind.Town)
                return plan.RX == 0 && plan.RZ == 0 ? "Epacenur" : TownName(ow, plan);
            if (plan.Kind == RegionKind.Course)
            {
                var course = Spawning.CourseIn(ow, plan);
                return course != null ? course.Name : null;
            }
            if (plan.Kind == RegionKind.Shrine)
            {
                var altar = Spawning.ShrineAltar(ow, plan);
                return altar != null ? altar.Name : null;
            }
            if (plan.Kind != RegionKind.Camp)
                return null;
            var groups = Spawning.GroupsIn(ow, plan);
            return (groups.Count > 0 ? groups[0].Npc.Name : "Goblin") + " camp";
        }

        /// A made-up name for a town, the same every time.
        public static string TownName(Overworld ow, RegionPlan plan)
        {
            string[] starts = { "Ash", "Bram", "Cor", "Dun", "Fen", "Glen", "Hal", "Kel", "Lor", "Mar", "Nor", "Os", "Rav", "Thorn", "Wil" };
            string[] ends = { "by", "ford", "holm", "mere", "stead", "ton", "wick", "dale", "hollow", "bridge" };
            return starts[(int)(ow.Hash(plan.RX, plan.RZ, 13) * starts.Length)] + ends[(int)(ow.Hash(plan.RX, plan.RZ, 14) * ends.Length)];
        }

        static void BuildBuilding(World world, Building b)
        {
            var root = world.BuildRoot;
            var a = b.Footprint;
            bool stone = b.Kind == BuildingKind.Bank || b.Kind == BuildingKind.Chapel || b.Kind == BuildingKind.Smithy;
            var wallColour = stone ? Stone : Timber;
            const float height = 1.7f, thick = 0.45f;

            // Walls: the four sides, the door's side in two pieces, plus a beam over the door.
            var lo = new Tile(a.X, a.Z);
            var hi = new Tile(a.XMax - 1, a.ZMax - 1);
            var door = b.Door;
            WallRun(root, lo.X, hi.X, lo.Z, true, door, height, thick, wallColour);
            WallRun(root, lo.X, hi.X, hi.Z, true, door, height, thick, wallColour);
            WallRun(root, lo.Z, hi.Z, lo.X, false, door, height, thick, wallColour);
            WallRun(root, lo.Z, hi.Z, hi.X, false, door, height, thick, wallColour);
            bool doorOnX = door.Z == lo.Z || door.Z == hi.Z;  // door in a wall running along x
            Visuals.Prim(PrimitiveType.Cube, root, World.Local(door) + Vector3.up * (height - 0.15f),
                doorOnX ? new Vector3(1f, 0.3f, thick + 0.05f) : new Vector3(thick + 0.05f, 0.3f, 1f), Trim);
            // Darker trim along the top of the walls.
            float cx = (lo.X + hi.X) / 2f, cz = (lo.Z + hi.Z) / 2f;
            float w = hi.X - lo.X + 1, d = hi.Z - lo.Z + 1;
            Visuals.Prim(PrimitiveType.Cube, root, new Vector3(cx, height, lo.Z), new Vector3(w, 0.08f, thick + 0.08f), Trim);
            Visuals.Prim(PrimitiveType.Cube, root, new Vector3(cx, height, hi.Z), new Vector3(w, 0.08f, thick + 0.08f), Trim);
            Visuals.Prim(PrimitiveType.Cube, root, new Vector3(lo.X, height, cz), new Vector3(thick + 0.08f, 0.08f, d), Trim);
            Visuals.Prim(PrimitiveType.Cube, root, new Vector3(hi.X, height, cz), new Vector3(thick + 0.08f, 0.08f, d), Trim);

            // What's inside, against the back wall, facing the door.
            var feature = b.Feature;
            Vector3 toDoor = World.Local(door) - World.Local(feature);
            var facingDoor = Quaternion.LookRotation(toDoor);
            Vector3 signAt = World.Local(WorldGen.OutsideDoor(b)) + Vector3.up * 2.3f;
            switch (b.Kind)
            {
                case BuildingKind.Bank:
                    // The booth's counter faces -z, so turn its back to the door.
                    world.AddStation(CommonModule.BankBooth, feature).transform.localRotation = Quaternion.LookRotation(-toDoor);
                    world.AddLabel(signAt, "Bank", Gold);
                    break;
                case BuildingKind.Shop:
                    world.AddShopKeeper(feature, ShopsModule.GeneralStore).SetFacing(facingDoor);
                    world.AddLabel(signAt, "General Store", Gold);
                    break;
                case BuildingKind.Chapel:
                    world.AddStation(PrayerModule.Altar, feature).transform.localRotation = Quaternion.LookRotation(-toDoor);
                    world.AddLabel(signAt, "Chapel", Gold);
                    break;
                case BuildingKind.Smithy:
                    world.AddStation(SmithingModule.Furnace, feature).transform.localRotation = Quaternion.LookRotation(-toDoor);
                    world.AddStation(SmithingModule.Anvil, b.Feature2).transform.localRotation = Quaternion.LookRotation(-toDoor);
                    world.AddLabel(signAt, "Smithy", Gold);
                    break;
                case BuildingKind.Workshop:
                    world.AddStation(CraftingModule.SpinningWheel, feature).transform.localRotation = Quaternion.LookRotation(-toDoor);
                    world.AddStation(CraftingModule.TanningRack, b.Feature2).transform.localRotation = Quaternion.LookRotation(-toDoor);
                    world.AddLabel(signAt, "Workshop", Gold);
                    break;
                default:
                    Table(root, feature);
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

        /// A table in a house (its tile is blocked by the plan).
        static void Table(Transform root, Tile tile)
        {
            var t = new GameObject("Table").transform;
            t.SetParent(root, false);
            t.localPosition = World.Local(tile);
            var wood = new Color(0.45f, 0.3f, 0.17f);
            Visuals.Prim(PrimitiveType.Cube, t, new Vector3(0f, 0.7f, 0f), new Vector3(0.9f, 0.08f, 0.7f), wood);
            foreach (float x in new[] { -0.38f, 0.38f })
                foreach (float z in new[] { -0.28f, 0.28f })
                    Visuals.Prim(PrimitiveType.Cube, t, new Vector3(x, 0.35f, z), new Vector3(0.07f, 0.7f, 0.07f), wood);
            Visuals.Prim(PrimitiveType.Cylinder, t, new Vector3(0.15f, 0.78f, 0f), new Vector3(0.15f, 0.04f, 0.15f), new Color(0.85f, 0.85f, 0.8f));
            var box = t.gameObject.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.4f, 0f);
            box.size = new Vector3(0.9f, 0.8f, 0.7f);
        }

        /// A goblin tent: a sloping hide roof on poles (its tile is blocked by the plan).
        /// A tall grey stone, leaning a little (`look`: which of four shapes).
        static void StandingStone(World world, Tile tile, int look)
        {
            var t = new GameObject("Standing stone").transform;
            t.SetParent(world.BuildRoot, false);
            t.localPosition = World.Local(tile);
            float h = 1.6f + look * 0.25f;
            var stone = Visuals.Prim(PrimitiveType.Cube, t, new Vector3(0f, h / 2f, 0f), new Vector3(0.55f, h, 0.4f), Stone * (0.85f + look * 0.05f));
            stone.transform.localRotation = Quaternion.Euler(look * 2f - 3f, look * 40f, look % 2 == 0 ? 3f : -2f);
            var box = t.gameObject.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, h / 2f, 0f);
            box.size = new Vector3(0.7f, h, 0.7f);
        }

        static void Tent(World world, Tile tile)
        {
            var t = new GameObject("Tent").transform;
            t.SetParent(world.BuildRoot, false);
            t.localPosition = World.Local(tile);
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
        }
    }

    /// Frees a mesh made in code when its object goes (chunks come and go as you walk).
    public sealed class OwnedMesh : MonoBehaviour
    {
        public Mesh Mesh;
        void OnDestroy()
        {
            if (Mesh != null)
                Destroy(Mesh);
        }
    }
}
