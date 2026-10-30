using System.Collections.Generic;
using Epacenur.Content;
using Epacenur.Core;
using UnityEngine;

namespace Epacenur.Runtime
{
    /// Builds the overworld's 3D pieces from the generator (Core: Overworld): a chunk's ground, roads,
    /// ponds, trees, rocks and fishing spot, and a region's buildings, goblin camp, shrine, quarry and place names.
    public static class OverworldBuilder
    {
        static readonly Color Tilled = new Color(0.4f, 0.3f, 0.19f);
        static readonly Color Planks = new Color(0.5f, 0.36f, 0.22f), Water = new Color(0.2f, 0.42f, 0.7f), Sand = new Color(0.76f, 0.68f, 0.48f);
        static readonly Color Stone = new Color(0.62f, 0.6f, 0.56f);
        static readonly Color Gold = new Color(1f, 0.85f, 0.3f), Paving = new Color(0.58f, 0.58f, 0.6f), Gravel = new Color(0.5f, 0.47f, 0.42f);

        // ---- Chunks ----

        public static void BuildChunk(World world, int cx, int cz)
        {
            var ow = world.Overworld;
            int size = Overworld.ChunkSize, x0 = cx * size, z0 = cz * size;
            var root = world.BuildRoot;
            var terrain = new Terrain[size, size];
            for (int x = 0; x < size; x++)
                for (int z = 0; z < size; z++)
                    terrain[x, z] = ow.At(new Tile(x0 + x, z0 + z));

            var heights = Ground(world, x0, z0, size, root);

            // Dirt roads and the trampled ground of camps and cave mouths, fading into the ground round them.
            DirtPaths.Build(world, heights, x0, z0, size, root);
            // Ground overlays (paving, gravel, floors, fields): sheets that follow the ground.
            Sheet(ow, terrain, x0, z0, root, Paving, 0f, 0.025f, false, (k, t) => k == Terrain.Stone);
            Sheet(ow, terrain, x0, z0, root, Gravel, 0f, 0.02f, false, (k, t) => k == Terrain.Quarry || (k == Terrain.Rock && ow.PlanAt(t).Kind == RegionKind.Quarry));
            Sheet(ow, terrain, x0, z0, root, Planks, 0f, 0.03f, false, (k, t) => k == Terrain.Floor || k == Terrain.Door);
            Sheet(ow, terrain, x0, z0, root, Tilled, 0f, 0.02f, false, (k, t) => k == Terrain.Field);
            // Water lies at its own level (it fills the valley floors), over a sandy edge that follows the banks;
            // bridges carry you over rivers.
            Sheet(ow, terrain, x0, z0, root, Sand, 0.35f, 0.02f, false, (k, t) => k == Terrain.Water || k == Terrain.Bridge);
            Sheet(ow, terrain, x0, z0, root, Water, 0f, 0f, true, (k, t) => k == Terrain.Water || k == Terrain.Bridge);
            Bridges(ow, terrain, x0, z0, root);

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
                    else if (terrain[x, z] == Terrain.Essence)
                        world.AddNode(Spawning.NodeAt(ow, t, Terrain.Essence)?.Id ?? RunecraftingModule.EssenceRock, t, rng);
                }

            // Grass, flowers and bushes over the open ground.
            Foliage.Build(ow, terrain, x0, z0, heights, root, new System.Random((int)(ow.Hash(cx, cz, 15) * int.MaxValue)));

            // A fishing spot on a pond's shore, moving along the part of it in this chunk.
            var shore = ow.Shore(tiles);
            if (shore.Count >= 4)
            {
                var at = shore[rng.Next(shore.Count)];
                world.AddNode(Spawning.NodeAt(ow, at, Terrain.Water)?.Id ?? FishingModule.NetSpot, at, rng, shore);
            }
        }

        const int BareKey = 1000;  // ground parts from here on are bare ground (earth, rock, snow), not a biome's

        /// The chunk's ground: a sheet over the hills, a corner at each tile corner (neighbouring chunks
        /// work out the same heights along their shared edge, so there are no gaps).
        static float[] Ground(World world, int x0, int z0, int size, Transform root)
        {
            var ow = world.Overworld;
            int n = size + 1;
            var verts = new Vector3[n * n];
            var uv = new Vector2[n * n];
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                {
                    float x = x0 - 0.5f + i, z = z0 - 0.5f + j;
                    verts[i * n + j] = new Vector3(x, (float)ow.Elevation(x, z), z);
                    uv[i * n + j] = new Vector2(i / (float)size, j / (float)size);  // 0..1 across the chunk (the material repeats its texture)
                }
            // Each tile's two triangles go in the part of the mesh for its biome and band of danger, which has
            // that ground's material.
            var parts = new Dictionary<int, List<int>>();
            for (int i = 0; i < size; i++)
                for (int j = 0; j < size; j++)
                {
                    var tile = new Tile(x0 + i, z0 + j);
                    int a0 = i * n + j;
                    // Bare earth, rock or snow where it's steep or high; otherwise the biome's ground.
                    var bare = ow.GroundAt(tile, verts[a0].y, verts[a0 + n].y, verts[a0 + 1].y, verts[a0 + n + 1].y);
                    int key = bare != Core.Ground.Grass ? BareKey + (int)bare : (int)ow.BiomeAt(tile) * World.DangerBands + World.DangerBand(ow.Danger(tile));
                    if (!parts.TryGetValue(key, out var tris))
                        parts[key] = tris = new List<int>();
                    int a = i * n + j, b = a + 1, c = a + n, d = c + 1;
                    tris.Add(a); tris.Add(b); tris.Add(c);
                    tris.Add(c); tris.Add(b); tris.Add(d);
                }
            var mesh = new Mesh { name = "Ground" };
            mesh.vertices = verts;
            mesh.uv = uv;
            mesh.subMeshCount = parts.Count;
            var mats = new Material[parts.Count];
            int part = 0;
            foreach (var kv in parts)
            {
                mesh.SetTriangles(kv.Value, part);
                mats[part++] = kv.Key >= BareKey ? world.BareMaterial((Core.Ground)(kv.Key - BareKey))
                    : world.GroundMaterial((Biome)(kv.Key / World.DangerBands), kv.Key % World.DangerBands);
            }
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            var go = new GameObject("Ground");
            go.layer = World.FarLayer;
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = mats;
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
            go.AddComponent<OwnedMesh>().Mesh = mesh;
            var heights = new float[n * n];
            for (int k = 0; k < heights.Length; k++)
                heights[k] = verts[k].y;
            return heights;
        }

        /// A sheet of quads, one per tile that `which` picks (grown by `grow` each side): at the water's surface
        /// (`water`), or just over the ground, following it.
        static void Sheet(Overworld ow, Terrain[,] terrain, int x0, int z0, Transform root, Color colour, float grow, float lift, bool water,
            System.Func<Terrain, Tile, bool> which)
        {
            int size = terrain.GetLength(0);
            var verts = new List<Vector3>();
            var flow = new List<Vector2>();  // water: which way and how fast it runs, for its ripples
            var tris = new List<int>();
            for (int x = 0; x < size; x++)
                for (int z = 0; z < size; z++)
                {
                    if (!which(terrain[x, z], new Tile(x0 + x, z0 + z)))
                        continue;
                    float cx = x0 + x, cz = z0 + z, r = 0.5f + grow;
                    int start = verts.Count;
                    foreach (var c in new[] { new Vector2(-r, -r), new Vector2(-r, r), new Vector2(r, -r), new Vector2(r, r) })
                    {
                        float px = cx + c.x, pz = cz + c.y;
                        double y = water ? ow.WaterSurface(px, pz) : ow.Elevation(px, pz) + lift;
                        verts.Add(new Vector3(px, (float)y, pz));
                        if (water)
                        {
                            ow.RiverFlow(px, pz, out double fx, out double fz);
                            flow.Add(new Vector2((float)fx, (float)fz));
                        }
                    }
                    tris.Add(start); tris.Add(start + 1); tris.Add(start + 2);
                    tris.Add(start + 2); tris.Add(start + 1); tris.Add(start + 3);
                }
            if (verts.Count == 0)
                return;
            var mesh = new Mesh { name = water ? "Water" : "Shore" };
            mesh.vertices = verts.ToArray();
            if (water)
                mesh.uv2 = flow.ToArray();
            mesh.triangles = tris.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            var go = new GameObject(mesh.name);
            go.layer = World.FarLayer;
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = water ? Visuals.WaterMat(colour) : Visuals.Mat(colour);
            go.AddComponent<OwnedMesh>().Mesh = mesh;
        }

        /// A plank deck over each bridge tile (you walk on it), with a post at each corner of the bridge's sides.
        static void Bridges(Overworld ow, Terrain[,] terrain, int x0, int z0, Transform root)
        {
            int size = terrain.GetLength(0);
            for (int x = 0; x < size; x++)
                for (int z = 0; z < size; z++)
                {
                    if (terrain[x, z] != Terrain.Bridge)
                        continue;
                    var t = new Tile(x0 + x, z0 + z);
                    float deck = (float)ow.SurfaceHeight(t);
                    Visuals.Prim(PrimitiveType.Cube, root, new Vector3(t.X, deck - 0.06f, t.Z), new Vector3(1.02f, 0.12f, 1.02f), Planks, true);
                    // Rails along the sides that aren't more bridge (or land you step off onto).
                    var n = new[] { new Tile(t.X + 1, t.Z), new Tile(t.X - 1, t.Z), new Tile(t.X, t.Z + 1), new Tile(t.X, t.Z - 1) };
                    for (int i = 0; i < 4; i++)
                    {
                        if (ow.At(n[i]) != Terrain.Water)
                            continue;
                        bool alongX = i >= 2;  // a neighbour north or south: the rail runs along x
                        float ox = i == 0 ? 0.45f : i == 1 ? -0.45f : 0f, oz = i == 2 ? 0.45f : i == 3 ? -0.45f : 0f;
                        Visuals.Prim(PrimitiveType.Cube, root, new Vector3(t.X + ox, deck + 0.35f, t.Z + oz),
                            alongX ? new Vector3(1f, 0.08f, 0.08f) : new Vector3(0.08f, 0.08f, 1f), Planks * 0.8f);
                        Visuals.Prim(PrimitiveType.Cube, root, new Vector3(t.X + ox, deck + 0.18f, t.Z + oz), new Vector3(0.1f, 0.42f, 0.1f), Planks * 0.75f);
                    }
                }
        }

        // ---- Regions ----

        public static void BuildRegion(World world, RegionPlan plan)
        {
            if (plan.Kind == RegionKind.Town)
            {
                var master = plan.Buildings.Find(x => x.Kind == BuildingKind.House);
                foreach (var b in plan.Buildings)
                    BuildBuilding(world, b, (int)(world.Overworld.Hash(plan.RX, plan.RZ, 16) * int.MaxValue), b == master);
                foreach (var lamp in plan.Lamps)
                    LampPost.Create(lamp, world.BuildRoot);
                if (plan.HasTradingPost)
                    world.AddTradingClerk(plan.TradingPost);
                if (plan.HasWell)
                    world.AddWell(plan.Well);
                string here = RegionName(world.Overworld, plan);
                foreach (var sign in plan.Signposts)
                    world.AddSignpost(sign, here);
                Landmarks.TownWalls(world, plan);
                for (int i = 0; i < plan.Patches.Count; i++)
                    world.AddPatch(plan.Patches[i], i % 2 == 0 ? PatchKind.Herb : PatchKind.Allotment);
                world.AddLabel(World.Local(plan.Center) + new Vector3(0f, 0.3f, 2f), RegionName(world.Overworld, plan), new Color(1f, 0.9f, 0.5f));
            }
            else if (plan.Kind == RegionKind.Camp)
            {
                foreach (var tent in plan.Tents)
                    Tent(world, tent);
                world.AddLabel(World.Local(plan.Camp.Center) + Vector3.up * 0.4f, RegionName(world.Overworld, plan), new Color(1f, 0.6f, 0.5f));
            }
            else if (plan.Kind == RegionKind.Cave)
            {
                string name = RegionName(world.Overworld, plan);
                world.AddCaveMouth(plan, name);
                world.AddLabel(World.Local(Overworld.CaveApproach(plan)) + Vector3.up * 3.6f, name, new Color(0.85f, 0.75f, 1f));
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
            else if (plan.Kind == RegionKind.Quarry)
            {
                // The ore rocks themselves grow with their chunks (Terrain.Rock); here, the spoil heaps and the cart.
                foreach (var heap in plan.Rubble)
                    Rubble(world, heap, (int)(world.Overworld.Hash(heap.X, heap.Z, 8) * 3));
                MineCart(world, plan.Cart, Spawning.QuarryOre(world.Overworld, plan));
                world.AddLabel(World.Local(plan.Cart) + Vector3.up * 1.6f, RegionName(world.Overworld, plan), new Color(0.85f, 0.85f, 0.8f));
            }
            else if (plan.Kind == RegionKind.Wild && plan.Landmark != LandmarkKind.None)
                Landmarks.Landmark(world, plan);
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
            if (plan.Kind == RegionKind.Wild)
                return Landmarks.LandmarkName(ow, plan);
            if (plan.Kind == RegionKind.Town)
                return plan.RX == 0 && plan.RZ == 0 ? "Epacenur" : TownName(ow, plan);
            if (plan.Kind == RegionKind.Cave)
            {
                string[] names = { "Echo", "Shadow", "Hollow", "Dripstone", "Gloom", "Crag", "Old", "Deep", "Bone", "Ember" };
                string[] kinds = { " cave", " caverns", " pit", " hole", " delve" };
                if (plan.RX == 0 && plan.RZ == -1)
                    return "Epacenur caves";
                return names[(int)(ow.Hash(plan.RX, plan.RZ, 83) * names.Length)] + kinds[(int)(ow.Hash(plan.RX, plan.RZ, 84) * kinds.Length)];
            }
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
            if (plan.Kind == RegionKind.Quarry)
            {
                var ore = Spawning.QuarryOre(ow, plan);
                return ore != null ? ore.Name.Replace(" rocks", "") + " quarry" : "Quarry";
            }
            if (plan.Kind != RegionKind.Camp)
                return null;
            var groups = Spawning.GroupsIn(ow, plan);
            return (groups.Count > 0 ? groups[0].Npc.Name : "Goblin") + " camp";
        }

        /// A town or city's name.
        public static string SettlementName(Overworld ow, Settlement s) => RegionName(ow, ow.Plan(s.RX, s.RZ));

        /// A made-up name for a town, the same every time.
        public static string TownName(Overworld ow, RegionPlan plan)
        {
            string[] starts = { "Ash", "Bram", "Cor", "Dun", "Fen", "Glen", "Hal", "Kel", "Lor", "Mar", "Nor", "Os", "Rav", "Thorn", "Wil" };
            string[] ends = { "by", "ford", "holm", "mere", "stead", "ton", "wick", "dale", "hollow", "bridge" };
            return starts[(int)(ow.Hash(plan.RX, plan.RZ, 13) * starts.Length)] + ends[(int)(ow.Hash(plan.RX, plan.RZ, 14) * ends.Length)];
        }

        static void BuildBuilding(World world, Building b, int townSeed, bool slayerMaster)
        {
            var root = world.BuildRoot;
            TownBuildings.Build(root, b, townSeed);
            var door = b.Door;

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
                case BuildingKind.Farm:
                    // The farmer, who rents out the patches in the field by his house.
                    world.AddStation(FarmingModule.PatchFarmer, feature).transform.localRotation = Quaternion.LookRotation(toDoor);
                    world.AddLabel(signAt, "Farm", Gold);
                    break;
                case BuildingKind.Workshop:
                    world.AddStation(CraftingModule.SpinningWheel, feature).transform.localRotation = Quaternion.LookRotation(-toDoor);
                    world.AddStation(CraftingModule.TanningRack, b.Feature2).transform.localRotation = Quaternion.LookRotation(-toDoor);
                    world.AddLabel(signAt, "Workshop", Gold);
                    break;
                default:
                    if (slayerMaster)
                    {
                        // One house in every town is the slayer master's.
                        world.AddStation(SlayerModule.Master, feature).transform.localRotation = Quaternion.LookRotation(toDoor);
                        world.AddLabel(signAt, "Slayer master", Gold);
                    }
                    else
                        Table(root, feature);
                    break;
            }
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

        /// A heap of broken stone in a quarry (`look`: which of three shapes; its tile is blocked by the plan).
        static void Rubble(World world, Tile tile, int look)
        {
            var t = new GameObject("Rubble").transform;
            t.SetParent(world.BuildRoot, false);
            t.localPosition = World.Local(tile);
            t.localRotation = Quaternion.Euler(0f, look * 70f, 0f);
            Visuals.Prim(PrimitiveType.Sphere, t, new Vector3(0f, 0.15f, 0f), new Vector3(1.1f, 0.6f, 1f), Stone * 0.8f);
            for (int i = 0; i < 5 + look; i++)
            {
                float a = i * 1.3f, r = 0.25f + (i % 2) * 0.12f;
                Visuals.Prim(PrimitiveType.Cube, t, new Vector3(Mathf.Cos(a) * r, 0.35f + (i % 3) * 0.08f, Mathf.Sin(a) * r), Vector3.one * (0.22f + (i % 3) * 0.06f),
                    Stone * (0.7f + (i % 4) * 0.08f)).transform.localRotation = Quaternion.Euler(i * 23f, i * 47f, i * 31f);
            }
            var box = t.gameObject.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.3f, 0f);
            box.size = new Vector3(1f, 0.6f, 1f);
        }

        /// A wooden mine cart heaped with ore, on a short stretch of track (its tile is blocked by the plan).
        static void MineCart(World world, Tile tile, NodeDef rock)
        {
            var t = new GameObject("Mine cart").transform;
            t.SetParent(world.BuildRoot, false);
            t.localPosition = World.Local(tile);
            var wood = new Color(0.42f, 0.29f, 0.17f);
            var iron = new Color(0.3f, 0.3f, 0.32f);
            foreach (float x in new[] { -0.3f, 0.3f })  // rails, on sleepers
                Visuals.Prim(PrimitiveType.Cube, t, new Vector3(x, 0.05f, 0f), new Vector3(0.05f, 0.05f, 1f), iron);
            foreach (float z in new[] { -0.35f, 0f, 0.35f })
                Visuals.Prim(PrimitiveType.Cube, t, new Vector3(0f, 0.02f, z), new Vector3(0.85f, 0.04f, 0.12f), wood * 0.8f);
            Visuals.Prim(PrimitiveType.Cube, t, new Vector3(0f, 0.45f, 0f), new Vector3(0.7f, 0.45f, 0.8f), wood);
            Visuals.Prim(PrimitiveType.Cube, t, new Vector3(0f, 0.68f, 0f), new Vector3(0.74f, 0.05f, 0.84f), iron);  // an iron rim
            foreach (float x in new[] { -0.3f, 0.3f })
                foreach (float z in new[] { -0.25f, 0.25f })
                    Visuals.Prim(PrimitiveType.Cylinder, t, new Vector3(x, 0.17f, z), new Vector3(0.22f, 0.03f, 0.22f), iron)
                        .transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            var ore = new Color(0.55f, 0.5f, 0.45f);
            if (rock != null && rock.Yields.Length > 0)
                ore = ModelBuilder.ToColor(ContentDb.Current.Item(rock.Yields[0].ItemId).Colour);
            for (int i = 0; i < 5; i++)
                Visuals.Prim(PrimitiveType.Sphere, t, new Vector3(-0.18f + (i % 3) * 0.18f, 0.72f + (i / 3) * 0.06f, -0.15f + (i % 2) * 0.3f), Vector3.one * 0.24f, ore);
            var box = t.gameObject.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.4f, 0f);
            box.size = new Vector3(0.8f, 0.8f, 0.9f);
        }

        /// A goblin tent: a sloping hide roof on poles (its tile is blocked by the plan).
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
        public Texture2D Texture;  // and a texture and material made for it, if any
        public Material Material;
        void OnDestroy()
        {
            if (Mesh != null)
                Destroy(Mesh);
            if (Texture != null)
                Destroy(Texture);
            if (Material != null)
                Destroy(Material);
        }
    }
}
