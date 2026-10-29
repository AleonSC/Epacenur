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
        public const int Seed = 2024;        // change for a different overworld (saved positions may then land oddly)
        public const int LoadRadius = 5;     // chunks loaded in each direction round the player
        public const int ChunksPerFrame = 2; // streaming budget
        const int OriginShiftTiles = 256;    // move the origin when the player gets this far from it

        public static Tile Origin { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Origin = new Tile(0, 0);

        public static Vector3 TileCenter(Tile t) => new Vector3(t.X - Origin.X, 0f, t.Z - Origin.Z);
        public static Tile TileAt(Vector3 p) => new Tile(Mathf.RoundToInt(p.x) + Origin.X, Mathf.RoundToInt(p.z) + Origin.Z);
        /// Where a tile is in map space: the local position of things on the map.
        public static Vector3 Local(Tile t) => new Vector3(t.X, 0f, t.Z);
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

        public static World Create(MapId map, Tile at)
        {
            var world = new GameObject("World").AddComponent<World>();
            world.Overworld = new Overworld(Seed);
            world.chunkGrid = new ChunkedGrid(Overworld.ChunkSize);
            // Darker grass far out under everything (it follows the player), so the map never floats in the sky.
            world.backdrop = Visuals.Prim(PrimitiveType.Cube, null, new Vector3(0f, -0.3f, 0f), new Vector3(480f, 0.5f, 480f), new Color(0.24f, 0.4f, 0.19f)).transform;
            world.backdrop.name = "Backdrop";
            world.Enter(map, at, LoadRadius);
            return world;
        }

        /// Goes to a map (building it) and loads round `at`: `radius` chunks now, the rest as you play.
        void Enter(MapId map, Tile at, int radius)
        {
            UnloadAll();
            Map = map;
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
            building = owner;
            OverworldBuilder.BuildChunk(this, cx, cz, GroundMaterial());
            building = null;

            long rkey = RegionKey(cx, cz);
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

            Areas.AddRange(DevArea.CreateAreas());
            foreach (var area in Areas)
            {
                var plot = area.Plot;
                var center = new Vector3(plot.x + (plot.width - 1) * 0.5f, 0f, plot.y + (plot.height - 1) * 0.5f);
                Visuals.Prim(PrimitiveType.Cube, dev.Root, new Vector3(center.x, 0.01f, center.z), new Vector3(plot.width, 0.02f, plot.height), area.GroundColor);
                AddLabel(new Vector3(center.x, 0.3f, plot.y + plot.height - 0.5f), area.Name, Color.white);
                area.Build(this);
            }
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
                    Grid.SetBlocked(new Tile(x, z), true);
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

        /// Puts an item on the ground. A stackable item joins a pile of the same item on that tile.
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
                        g.AddCount(count);
                        return g;
                    }
            var item = Own(GroundItem.Create(itemId, count, tile, owner.Root, rng), owner);
            GroundItems.Add(item);
            return item;
        }

        public void RemoveGroundItem(GroundItem item)
        {
            GroundItems.Remove(item);
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
