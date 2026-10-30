using System.Collections.Generic;
using Epacenur.Content;
using Epacenur.Core;
using UnityEngine;

namespace Epacenur.Runtime
{
    /// Text floating over a spot in the world (place names), drawn by the HUD.
    public struct WorldLabel
    {
        public Vector3 Position;  // in map space (see World.Local)
        public string Text;
        public Color Color;
        public object Owner;
    }

    /// The two places you can be: the endless overworld, and the dev plot for testing skills. Nothing
    /// connects them; the dev tools teleport between them.
    public enum MapId { Overworld = 0, DevPlot = 1 }

    /// The map you're on and everything on it.
    ///
    /// The overworld (Core: Overworld) never ends: it's built a 16 x 16 tile chunk at a time round the
    /// player and taken down again behind them. Towns and goblin camps belong to 64 x 64 tile regions,
    /// built while any chunk of their region is. Everything a chunk or region built is owned by it and
    /// goes when it does (a tree you felled grows back if you walk away and return).
    ///
    /// Positions: tile (x, z) lies at map-space (x, 0, z), which is what objects on the map use
    /// (World.Local). The map itself sits at -Origin, so world space stays near the player however far
    /// they walk (floating origin): World.TileCenter and World.TileAt convert.
    public sealed class World : MonoBehaviour
    {
        public const int LoadRadius = 5;     // chunks loaded in each direction round the player
        public const int ChunksPerFrame = 2; // streaming budget
        const int OriginShiftTiles = 256;    // move the origin when the player gets this far from it

        public static Tile Origin { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Origin = new Tile(0, 0);
            heightMap = null;
            heights.Clear();
        }

        /// The ground's height comes from this (the overworld's hills); the dev plot is flat.
        static Overworld heightMap;
        static readonly Dictionary<long, float> heights = new Dictionary<long, float>();

        /// How high the ground is at the centre of a tile.
        public static float Height(Tile t)
        {
            if (heightMap == null)
                return 0f;
            long key = ChunkedGrid.Key(t.X, t.Z);
            if (heights.TryGetValue(key, out float h))
                return h;
            if (heights.Count > 250000)
                heights.Clear();
            h = (float)heightMap.Elevation(t);
            heights[key] = h;
            return h;
        }

        /// A tile's centre on the ground, in world space.
        public static Vector3 TileCenter(Tile t) => new Vector3(t.X - Origin.X, Height(t), t.Z - Origin.Z);
        public static Tile TileAt(Vector3 p) => new Tile(Mathf.RoundToInt(p.x) + Origin.X, Mathf.RoundToInt(p.z) + Origin.Z);
        /// Where a tile is in map space (on the ground): the local position of things on the map.
        public static Vector3 Local(Tile t) => new Vector3(t.X, Height(t), t.Z);
        /// A map-space point in world space.
        public static Vector3 ToWorld(Vector3 local) => local - new Vector3(Origin.X, 0f, Origin.Z);

        /// What a chunk, region or the dev plot built: taken down together.
        public sealed class Owner
        {
            public Transform Root;
            public readonly List<Component> Objects = new List<Component>();
        }

        public MapId Map { get; private set; }
        public Overworld Overworld { get; private set; }
        public WalkGrid Grid => Map == MapId.DevPlot ? (WalkGrid)devGrid : chunkGrid;
        /// Where you start: the overworld's first town square.
        public Tile MainSpawn => Overworld.Spawn;
        public Tile Spawn => MainSpawn;
        public Tile DevSpawn => DevArea.Spawn;

        public readonly List<NodeObject> Nodes = new List<NodeObject>();
        public readonly List<StationObject> Stations = new List<StationObject>();
        public readonly List<ShopKeeper> ShopKeepers = new List<ShopKeeper>();
        public readonly List<Npc> Npcs = new List<Npc>();
        public readonly List<GroundItem> GroundItems = new List<GroundItem>();
        public readonly List<Fire> Fires = new List<Fire>();
        public readonly List<WorldLabel> Labels = new List<WorldLabel>();
        public readonly List<SkillArea> Areas = new List<SkillArea>();

        readonly System.Random rng = new System.Random();
        ChunkedGrid chunkGrid;
        TileGrid devGrid;
        Owner dev, building;
        readonly Dictionary<long, Owner> chunks = new Dictionary<long, Owner>();
        readonly Dictionary<long, Owner> regions = new Dictionary<long, Owner>();
        readonly Dictionary<long, int> regionChunks = new Dictionary<long, int>();
        Transform backdrop;
        Material groundMat;
        // The minimap's colours: per loaded chunk (overworld) or for the whole dev plot.
        readonly Dictionary<long, Color32[]> chunkColours = new Dictionary<long, Color32[]>();
        Color32[] devColours;
        readonly HashSet<Tile> devWater = new HashSet<Tile>();

        /// Items lying on the ground anywhere in this world (both maps), by chunk ("dev" for the dev plot).
        /// They stay until picked up, loaded or not, and are saved with the world.
        public readonly List<GroundPile> Piles = new List<GroundPile>();
        readonly Dictionary<long, List<GroundPile>> pilesByChunk = new Dictionary<long, List<GroundPile>>();
        const long DevPiles = long.MinValue;

        /// Regions of the overworld that have been seen (loaded round a player), for the world map.
        public readonly HashSet<long> Explored = new HashSet<long>();

        /// Makes the world for a seed, with the items left lying in it and the regions already seen, and goes to `map`.
        public static World Create(int seed, MapId map, List<GroundPile> piles, List<long> explored)
        {
            var world = new GameObject("World").AddComponent<World>();
            world.Overworld = new Overworld(seed);
            if (explored != null)
                world.Explored.UnionWith(explored);
            if (piles != null)
                foreach (var p in piles)
                    world.AddPile(p);
            world.chunkGrid = new ChunkedGrid(Overworld.ChunkSize);
            // Darker grass far out under everything (it follows the player), so the map never floats in the sky.
            world.backdrop = Visuals.Prim(PrimitiveType.Cube, null, new Vector3(0f, -0.3f, 0f), new Vector3(480f, 0.5f, 480f), new Color(0.24f, 0.4f, 0.19f)).transform;
            world.backdrop.name = "Backdrop";
            world.Enter(map, world.Overworld.Spawn, LoadRadius);
            return world;
        }

        void OnDestroy()
        {
            if (backdrop != null)
                Destroy(backdrop.gameObject);
        }

        /// Goes to a map (building it) and loads round `at`: `radius` chunks now, the rest as you play.
        void Enter(MapId map, Tile at, int radius)
        {
            UnloadAll();
            Map = map;
            heightMap = map == MapId.Overworld ? Overworld : null;
            heights.Clear();
            SetOrigin(map == MapId.DevPlot ? new Tile(0, 0) : Snap(at));
            if (map == MapId.DevPlot)
                BuildDevPlot();
            else
                LoadAround(at, radius, int.MaxValue);
        }

        /// Teleports the player, changing map if need be.
        public void Travel(MapId map, Tile tile)
        {
            if (map != Map)
                Enter(map, tile, 1);
            else if (map == MapId.Overworld)
                LoadAround(tile, 1, int.MaxValue);
            var player = Game.Instance.Player;
            player.Teleport(NearestWalkable(tile));
            if (map == MapId.Overworld)
                ShiftOriginIfFar(tile, true);
        }

        void Update()
        {
            var player = Game.Instance.Player;
            if (player == null)
                return;
            var p = player.transform.position;
            backdrop.position = new Vector3(p.x, -0.3f, p.z);
            UpdateGravestone();
            if (Map != MapId.Overworld)
                return;
            var tile = player.CurrentTile;
            ShiftOriginIfFar(tile, false);
            LoadAround(tile, LoadRadius, ChunksPerFrame);
            UnloadBeyond(tile, LoadRadius + 1);
        }

        // ---- Floating origin ----

        static Tile Snap(Tile t) => new Tile(Mathf.RoundToInt(t.X / 64f) * 64, Mathf.RoundToInt(t.Z / 64f) * 64);

        void SetOrigin(Tile o)
        {
            Origin = o;
            transform.position = -new Vector3(o.X, 0f, o.Z);
        }

        /// Far from the origin, floats lose precision: move the origin to the player, and everything
        /// that isn't on the map (the player, the camera) with it.
        void ShiftOriginIfFar(Tile tile, bool force)
        {
            if (!force && tile.StepsTo(Origin) < OriginShiftTiles)
                return;
            var next = Snap(tile);
            if (next == Origin)
                return;
            var delta = new Vector3(Origin.X - next.X, 0f, Origin.Z - next.Z);
            SetOrigin(next);
            var g = Game.Instance;
            g.Player.ShiftBy(delta);
            g.Rig.ShiftBy(delta);
        }

        // ---- Chunks and regions ----

        void LoadAround(Tile center, int radius, int budget)
        {
            int pcx = chunkGrid.ChunkOf(center.X), pcz = chunkGrid.ChunkOf(center.Z);
            // Nearest rings first.
            for (int r = 0; r <= radius && budget > 0; r++)
                for (int dx = -r; dx <= r && budget > 0; dx++)
                    for (int dz = -r; dz <= r && budget > 0; dz++)
                    {
                        if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dz)) != r)
                            continue;
                        int cx = pcx + dx, cz = pcz + dz;
                        if (chunks.ContainsKey(ChunkedGrid.Key(cx, cz)))
                            continue;
                        LoadChunk(cx, cz);
                        budget--;
                    }
        }

        void UnloadBeyond(Tile center, int radius)
        {
            int pcx = chunkGrid.ChunkOf(center.X), pcz = chunkGrid.ChunkOf(center.Z);
            List<long> far = null;
            foreach (var pair in chunks)
            {
                int cx = (int)(pair.Key >> 32), cz = (int)(uint)pair.Key;
                if (Mathf.Abs(cx - pcx) > radius || Mathf.Abs(cz - pcz) > radius)
                    (far ?? (far = new List<long>())).Add(pair.Key);
            }
            if (far != null)
                foreach (var key in far)
                    UnloadChunk((int)(key >> 32), (int)(uint)key);
        }

        static long RegionKey(int cx, int cz)
        {
            const int per = Overworld.RegionSize / Overworld.ChunkSize;
            int rx = cx >= 0 ? cx / per : (cx + 1) / per - 1, rz = cz >= 0 ? cz / per : (cz + 1) / per - 1;
            return ChunkedGrid.Key(rx, rz);
        }

        void LoadChunk(int cx, int cz)
        {
            long key = ChunkedGrid.Key(cx, cz);
            var owner = NewOwner("Chunk " + cx + ", " + cz);
            chunks[key] = owner;
            chunkGrid.Load(cx, cz, Overworld.IsBlocked);
            chunkColours[key] = ChunkColours(cx, cz);
            building = owner;
            OverworldBuilder.BuildChunk(this, cx, cz, GroundMaterial());
            ShowPiles(key);
            building = null;

            long rkey = RegionKey(cx, cz);
            Explored.Add(rkey);
            regionChunks.TryGetValue(rkey, out int n);
            regionChunks[rkey] = n + 1;
            if (n == 0)
            {
                var plan = Overworld.Plan((int)(rkey >> 32), (int)(uint)rkey);
                var region = NewOwner("Region " + plan.RX + ", " + plan.RZ);
                regions[rkey] = region;
                building = region;
                OverworldBuilder.BuildRegion(this, plan);
                building = null;
            }
        }

        void UnloadChunk(int cx, int cz)
        {
            long key = ChunkedGrid.Key(cx, cz);
            if (!chunks.TryGetValue(key, out var owner))
                return;
            chunks.Remove(key);
            chunkColours.Remove(key);
            TakeDown(owner);
            chunkGrid.Unload(cx, cz);
            long rkey = RegionKey(cx, cz);
            int n = regionChunks[rkey] - 1;
            if (n > 0)
            {
                regionChunks[rkey] = n;
                return;
            }
            regionChunks.Remove(rkey);
            if (regions.TryGetValue(rkey, out var region))
            {
                regions.Remove(rkey);
                TakeDown(region);
            }
        }

        void UnloadAll()
        {
            foreach (var owner in chunks.Values)
                TakeDown(owner);
            foreach (var owner in regions.Values)
                TakeDown(owner);
            chunks.Clear();
            chunkColours.Clear();
            devColours = null;
            devWater.Clear();
            regions.Clear();
            regionChunks.Clear();
            chunkGrid.Clear();
            if (dev != null)
                TakeDown(dev);
            dev = null;
            devGrid = null;
            Areas.Clear();
        }

        Owner NewOwner(string name)
        {
            var root = new GameObject(name).transform;
            root.SetParent(transform, false);
            return new Owner { Root = root };
        }

        /// Removes everything an owner built from the world.
        void TakeDown(Owner owner)
        {
            foreach (var c in owner.Objects)
            {
                Nodes.Remove(c as NodeObject);
                Stations.Remove(c as StationObject);
                ShopKeepers.Remove(c as ShopKeeper);
                Npcs.Remove(c as Npc);
                GroundItems.Remove(c as GroundItem);
                Fires.Remove(c as Fire);
            }
            Labels.RemoveAll(l => l.Owner == owner);
            Destroy(owner.Root.gameObject);
        }

        /// Who owns something placed on a tile now: the chunk it's in (or the dev plot).
        Owner OwnerAt(Tile tile)
        {
            if (building != null)
                return building;
            if (Map == MapId.DevPlot)
                return dev;
            chunks.TryGetValue(ChunkedGrid.Key(chunkGrid.ChunkOf(tile.X), chunkGrid.ChunkOf(tile.Z)), out var owner);
            return owner;
        }

        T Own<T>(T thing, Owner owner) where T : Component
        {
            owner.Objects.Add(thing);
            return thing;
        }

        /// The parent for things built now (the chunk, region or dev plot being built).
        public Transform BuildRoot => (building ?? dev).Root;

        Material GroundMaterial()
        {
            if (groundMat == null)
            {
                groundMat = Visuals.NewMat(Color.white);
                groundMat.mainTexture = Visuals.TileTexture(new Color(0.34f, 0.53f, 0.25f), new Color(0.29f, 0.46f, 0.21f));
                groundMat.mainTextureScale = new Vector2(Overworld.ChunkSize, Overworld.ChunkSize);
            }
            return groundMat;
        }

        // ---- The dev plot ----

        void BuildDevPlot()
        {
            devGrid = new TileGrid(DevArea.Width, DevArea.Height);
            dev = NewOwner("Dev plot");
            var region = new RectInt(0, 0, DevArea.Width, DevArea.Height);
            BuildGround(region, new Color(0.33f, 0.52f, 0.24f), new Color(0.27f, 0.44f, 0.2f));
            BuildWalls(region);
            AddLabel(Local(DevArea.Spawn) + new Vector3(0f, 0.2f, -1.5f), "Dev Area", new Color(1f, 0.9f, 0.5f));
            AddStation(CommonModule.BankBooth, DevArea.BankBooth);
            AddShopKeeper(DevArea.ShopKeeper, ShopsModule.GeneralStore);
            AddStation(PrayerModule.Altar, DevArea.Altar);
            AddStation(SmithingModule.Furnace, DevArea.Furnace);
            AddStation(SmithingModule.Anvil, DevArea.Anvil);
            AddStation(CraftingModule.SpinningWheel, DevArea.SpinningWheel);
            AddStation(CraftingModule.TanningRack, DevArea.TanningRack);

            Areas.AddRange(DevArea.CreateAreas());
            foreach (var area in Areas)
            {
                var plot = area.Plot;
                var center = new Vector3(plot.x + (plot.width - 1) * 0.5f, 0f, plot.y + (plot.height - 1) * 0.5f);
                Visuals.Prim(PrimitiveType.Cube, dev.Root, new Vector3(center.x, 0.01f, center.z), new Vector3(plot.width, 0.02f, plot.height), area.GroundColor);
                AddLabel(new Vector3(center.x, 0.3f, plot.y + plot.height - 0.5f), area.Name, Color.white);
                area.Build(this);
            }
            ShowPiles(DevPiles);
            DevColours();
        }

        /// A walled area's ground: a slab whose top is at y = 0, edges on tile boundaries, with a faint line round each tile.
        public void BuildGround(RectInt region, Color fill, Color line)
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Ground";
            ground.transform.SetParent(BuildRoot, false);
            ground.transform.localPosition = new Vector3(region.x + (region.width - 1) * 0.5f, -0.25f, region.y + (region.height - 1) * 0.5f);
            ground.transform.localScale = new Vector3(region.width, 0.5f, region.height);
            var mat = Visuals.NewMat(Color.white);
            mat.mainTexture = Visuals.TileTexture(fill, line);
            mat.mainTextureScale = new Vector2(region.width, region.height);
            ground.GetComponent<Renderer>().sharedMaterial = mat;
        }

        /// A low wall on an area's outermost ring of tiles.
        public void BuildWalls(RectInt region, Color? colour = null)
        {
            int x0 = region.x, z0 = region.y, x1 = region.xMax - 1, z1 = region.yMax - 1;
            for (int x = x0; x <= x1; x++)
            {
                Grid.SetBlocked(new Tile(x, z0), true);
                Grid.SetBlocked(new Tile(x, z1), true);
            }
            for (int z = z0; z <= z1; z++)
            {
                Grid.SetBlocked(new Tile(x0, z), true);
                Grid.SetBlocked(new Tile(x1, z), true);
            }
            var stone = colour ?? new Color(0.55f, 0.55f, 0.58f);
            const float height = 0.8f;
            float w = region.width, h = region.height, cx = x0 + (w - 1) * 0.5f, cz = z0 + (h - 1) * 0.5f;
            var root = BuildRoot;
            Visuals.Prim(PrimitiveType.Cube, root, new Vector3(cx, height / 2, z0), new Vector3(w, height, 1f), stone, true);
            Visuals.Prim(PrimitiveType.Cube, root, new Vector3(cx, height / 2, z1), new Vector3(w, height, 1f), stone, true);
            Visuals.Prim(PrimitiveType.Cube, root, new Vector3(x0, height / 2, cz), new Vector3(1f, height, h - 2), stone, true);
            Visuals.Prim(PrimitiveType.Cube, root, new Vector3(x1, height / 2, cz), new Vector3(1f, height, h - 2), stone, true);
        }

        /// Water on the dev plot: blocked tiles under a blue surface.
        public void AddWater(RectInt area)
        {
            for (int x = area.x; x < area.x + area.width; x++)
                for (int z = area.y; z < area.y + area.height; z++)
                {
                    Grid.SetBlocked(new Tile(x, z), true);
                    devWater.Add(new Tile(x, z));
                }
            var center = new Vector3(area.x + (area.width - 1) * 0.5f, 0.025f, area.y + (area.height - 1) * 0.5f);
            Visuals.Prim(PrimitiveType.Cube, BuildRoot, center, new Vector3(area.width, 0.03f, area.height), new Color(0.2f, 0.42f, 0.7f));
            // A sandy bank round the edge.
            Visuals.Prim(PrimitiveType.Cube, BuildRoot, center - Vector3.up * 0.01f, new Vector3(area.width + 0.6f, 0.03f, area.height + 0.6f), new Color(0.76f, 0.68f, 0.48f));
        }

        // ---- Things on the map ----

        /// Map-space position; drawn by the HUD while its owner is loaded.
        public void AddLabel(Vector3 position, string text, Color color)
        {
            Labels.Add(new WorldLabel { Position = position, Text = text, Color = color, Owner = building ?? dev });
        }

        /// A station (bank booth, altar, furnace...) on a tile, which it blocks, with its name floating over it.
        public StationObject AddStation(string stationId, Tile tile)
        {
            var def = ContentDb.Current.Station(stationId);
            var owner = OwnerAt(tile);
            var station = Own(StationObject.Create(def, tile, owner.Root), owner);
            Grid.SetBlocked(tile, true);
            Stations.Add(station);
            if (def.Label != null)
                AddLabel(Local(tile) + Vector3.up * def.LabelHeight, def.Label, new Color(1f, 0.85f, 0.3f));
            return station;
        }

        /// A gathering node (tree, rock, fishing spot). Trees and rocks block their tile; a fishing spot sits
        /// on water and moves between the `shore` tiles.
        public NodeObject AddNode(string nodeId, Tile tile, System.Random random, List<Tile> shore = null)
        {
            var def = ContentDb.Current.Node(nodeId);
            var owner = OwnerAt(tile);
            var node = Own(NodeObject.Create(def, tile, owner.Root, random, shore), owner);
            if (def.Blocks)
                Grid.SetBlocked(tile, true);
            Nodes.Add(node);
            return node;
        }

        /// A shop keeper running a shop (each keeper has its own stock).
        public ShopKeeper AddShopKeeper(Tile tile, string shopId)
        {
            var owner = OwnerAt(tile);
            var npc = Own(ShopKeeper.Create(tile, new Shop(ContentDb.Current.Shop(shopId)), owner.Root), owner);
            Grid.SetBlocked(tile, true);
            ShopKeepers.Add(npc);
            AddLabel(Local(tile) + Vector3.up * 2.6f, "Shop", new Color(1f, 0.85f, 0.3f));
            return npc;
        }

        public Npc AddNpc(NpcDef def, Tile tile, RectInt home)
        {
            var owner = OwnerAt(tile);
            var npc = Own(Npc.Create(def, tile, home, owner.Root, rng), owner);
            Npcs.Add(npc);
            return npc;
        }

        long PileKey(int map, Tile t) => map == (int)MapId.DevPlot ? DevPiles : ChunkedGrid.Key(chunkGrid.ChunkOf(t.X), chunkGrid.ChunkOf(t.Z));

        void AddPile(GroundPile p)
        {
            Piles.Add(p);
            long key = PileKey(p.map, p.Tile);
            if (!pilesByChunk.TryGetValue(key, out var list))
                pilesByChunk[key] = list = new List<GroundPile>();
            list.Add(p);
        }

        /// Shows the items lying in a chunk (or on the dev plot) that's just been built.
        void ShowPiles(long key)
        {
            if (!pilesByChunk.TryGetValue(key, out var list))
                return;
            var owner = building ?? dev;
            foreach (var p in list)
                if (ContentDb.Current.Item(p.item) != null)
                    GroundItems.Add(Own(GroundItem.Create(p, owner.Root, rng), owner));
        }

        /// Puts an item on the ground, where it stays until picked up. A stackable item joins a pile of the
        /// same item on that tile.
        public GroundItem SpawnGroundItem(string itemId, int count, Tile tile)
        {
            var def = ContentDb.Current.Item(itemId);
            var owner = OwnerAt(tile);
            if (def == null || count <= 0 || owner == null)
                return null;
            if (def.Stackable)
                foreach (var g in GroundItems)
                    if (g.ItemId == itemId && g.Tile == tile)
                    {
                        g.Pile.count += count;
                        return g;
                    }
            var pile = new GroundPile { item = itemId, count = count, x = tile.X, z = tile.Z, map = (int)Map };
            AddPile(pile);
            var item = Own(GroundItem.Create(pile, owner.Root, rng), owner);
            GroundItems.Add(item);
            return item;
        }

        /// Leaves items on the ground anywhere, loaded or not (a gravestone collapsing).
        public void PlacePile(string itemId, int count, MapId map, Tile tile)
        {
            if (map == Map && OwnerAt(tile) != null)
            {
                SpawnGroundItem(itemId, count, tile);
                return;
            }
            AddPile(new GroundPile { item = itemId, count = count, x = tile.X, z = tile.Z, map = (int)map });
        }

        /// The square of the town nearest a tile (where you wake up after dying), searching a few regions out.
        public Tile NearestTown(Tile t)
        {
            int rx = Overworld.RegionOf(t.X), rz = Overworld.RegionOf(t.Z);
            Tile best = MainSpawn;
            int bestSteps = int.MaxValue;
            for (int dx = -6; dx <= 6; dx++)
                for (int dz = -6; dz <= 6; dz++)
                {
                    var plan = Overworld.Plan(rx + dx, rz + dz);
                    if (plan.Kind != RegionKind.Town || plan.Center.StepsTo(t) >= bestSteps)
                        continue;
                    best = plan.Center;
                    bestSteps = plan.Center.StepsTo(t);
                }
            return best;
        }

        // ---- Minimap colours ----

        static readonly Color32 MapGrass = new Color32(74, 122, 52, 255), MapTree = new Color32(36, 82, 30, 255),
            MapWater = new Color32(52, 98, 170, 255), MapWall = new Color32(120, 105, 90, 255), MapFloor = new Color32(140, 104, 68, 255),
            MapPath = new Color32(160, 132, 90, 255), MapCamp = new Color32(118, 92, 62, 255), MapRock = new Color32(120, 116, 110, 255),
            MapFlax = new Color32(110, 140, 175, 255), MapStone = new Color32(165, 165, 170, 255),
            MapEssence = new Color32(205, 210, 225, 255), MapQuarry = new Color32(128, 120, 108, 255),
            MapBlocked = new Color32(90, 90, 95, 255);

        public static Color32 TerrainColour(Terrain t)
        {
            switch (t)
            {
                case Terrain.Tree: return MapTree;
                case Terrain.Water: return MapWater;
                case Terrain.Wall: return MapWall;
                case Terrain.Floor: case Terrain.Door: return MapFloor;
                case Terrain.Path: return MapPath;
                case Terrain.Camp: return MapCamp;
                case Terrain.Rock: return MapRock;
                case Terrain.Flax: return MapFlax;
                case Terrain.Stone: return MapStone;
                case Terrain.Essence: return MapEssence;
                case Terrain.Quarry: return MapQuarry;
                default: return MapGrass;
            }
        }

        /// Hills on the maps: slopes facing the north-west lighter, the others darker, high ground a touch
        /// lighter. `step`: how far apart the samples are (the world map's zoomed-out pixels are far apart).
        public static Color32 HillShade(Color32 c, Overworld ow, Tile t, int step)
        {
            double slope = (ow.Elevation(t.X - step, t.Z + step) - ow.Elevation(t.X + step, t.Z - step)) / step;
            float f = 1f + Mathf.Clamp((float)slope * 0.18f, -0.3f, 0.3f) + (float)ow.Elevation(t) * 0.012f;
            return new Color32((byte)Mathf.Clamp(c.r * f, 0f, 255f), (byte)Mathf.Clamp(c.g * f, 0f, 255f), (byte)Mathf.Clamp(c.b * f, 0f, 255f), c.a);
        }

        Color32[] ChunkColours(int cx, int cz)
        {
            int size = Overworld.ChunkSize;
            var c = new Color32[size * size];
            for (int x = 0; x < size; x++)
                for (int z = 0; z < size; z++)
                {
                    var t = new Tile(cx * size + x, cz * size + z);
                    c[x * size + z] = HillShade(TerrainColour(Overworld.At(t)), Overworld, t, 1);
                }
            return c;
        }

        void DevColours()
        {
            devColours = new Color32[DevArea.Width * DevArea.Height];
            for (int x = 0; x < DevArea.Width; x++)
                for (int z = 0; z < DevArea.Height; z++)
                {
                    var t = new Tile(x, z);
                    devColours[x * DevArea.Height + z] = devWater.Contains(t) ? MapWater : devGrid.IsWalkable(t) ? MapGrass : MapBlocked;
                }
        }

        /// A tile's colour on the minimap; false where nothing is loaded.
        public bool TryMapColour(Tile t, out Color32 c)
        {
            c = default(Color32);
            if (Map == MapId.DevPlot)
            {
                if (devColours == null || t.X < 0 || t.Z < 0 || t.X >= DevArea.Width || t.Z >= DevArea.Height)
                    return false;
                c = devColours[t.X * DevArea.Height + t.Z];
                return true;
            }
            int size = Overworld.ChunkSize, cx = chunkGrid.ChunkOf(t.X), cz = chunkGrid.ChunkOf(t.Z);
            if (!chunkColours.TryGetValue(ChunkedGrid.Key(cx, cz), out var colours))
                return false;
            c = colours[(t.X - cx * size) * size + (t.Z - cz * size)];
            return true;
        }

        // ---- Your gravestone ----

        Gravestone gravestone;

        /// Shows your gravestone while its spot is loaded (and takes it away once it's gone).
        void UpdateGravestone()
        {
            var g = Game.Instance.Grave;
            bool here = g != null && g.world == Game.Instance.WorldInfo.id && g.map == (int)Map && OwnerAt(g.Tile) != null;
            if (!here)
            {
                if (gravestone != null)
                    Destroy(gravestone.gameObject);
                gravestone = null;
                return;
            }
            if (gravestone == null || gravestone.Tile != g.Tile)
            {
                if (gravestone != null)
                    Destroy(gravestone.gameObject);
                var owner = OwnerAt(g.Tile);
                gravestone = Own(Gravestone.Create(g, owner.Root), owner);
            }
        }

        /// Picked up (or lit): gone for good.
        public void RemoveGroundItem(GroundItem item)
        {
            GroundItems.Remove(item);
            Piles.Remove(item.Pile);
            if (pilesByChunk.TryGetValue(PileKey(item.Pile.map, item.Tile), out var list))
                list.Remove(item.Pile);
            Destroy(item.gameObject);
        }

        public Fire AddFire(Tile tile, float burnSeconds)
        {
            var owner = OwnerAt(tile);
            var fire = Own(Fire.Create(tile, burnSeconds, owner.Root), owner);
            Grid.SetBlocked(tile, true);
            Fires.Add(fire);
            Stations.Add(fire);
            return fire;
        }

        /// Puts a fire out; a fire that burned down leaves ashes.
        public void RemoveFire(Fire fire, bool leaveAshes)
        {
            Fires.Remove(fire);
            Stations.Remove(fire);
            Grid.SetBlocked(fire.Tile, false);
            if (leaveAshes)
                SpawnGroundItem(FiremakingModule.Ashes, 1, fire.Tile);
            Destroy(fire.gameObject);
        }

        /// A walkable tile at or near the given one (for teleports and getting un-stuck).
        public Tile NearestWalkable(Tile t)
        {
            for (int r = 0; r < 10; r++)
                for (int dx = -r; dx <= r; dx++)
                    for (int dz = -r; dz <= r; dz++)
                    {
                        var c = new Tile(t.X + dx, t.Z + dz);
                        if (Grid.IsWalkable(c))
                            return c;
                    }
            return t;
        }
    }
}
