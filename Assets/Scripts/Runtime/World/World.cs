using System.Collections.Generic;
using Epacenur.Content;
using Epacenur.Core;
using UnityEngine;

namespace Epacenur.Runtime
{
    /// Text floating over a spot in the world (plot names), drawn by the HUD.
    public struct WorldLabel
    {
        public Vector3 Position;
        public string Text;
        public Color Color;
    }

    /// The tile grid and everything standing on it. Tile (x, z) is centred on world (x, 0, z).
    ///
    /// The grid holds two separate, walled regions: the dev area (DevRegion, tiles 0..47) for testing
    /// skills, and the procedurally generated main world (MainRegion) where you start. Nothing connects
    /// them: the dev tools teleport between them.
    public sealed class World : MonoBehaviour
    {
        public const int MainSize = 128;
        public const int MainSeed = 2024;  // change for a different main world (saved positions may then land oddly)
        public static readonly RectInt DevRegion = new RectInt(0, 0, DevArea.Width, DevArea.Height);
        public static readonly RectInt MainRegion = new RectInt(DevArea.Width + 12, 0, MainSize, MainSize);

        public MainWorldLayout MainLayout { get; private set; }
        /// Where you start in the main world (its town square).
        public Tile MainSpawn { get; private set; }
        public Tile DevSpawn => DevArea.Spawn;

        public TileGrid Grid { get; private set; }
        public readonly List<NodeObject> Nodes = new List<NodeObject>();
        public readonly List<StationObject> Stations = new List<StationObject>();
        public readonly List<ShopKeeper> ShopKeepers = new List<ShopKeeper>();
        public readonly List<Npc> Npcs = new List<Npc>();
        public readonly List<GroundItem> GroundItems = new List<GroundItem>();
        public readonly List<Fire> Fires = new List<Fire>();
        readonly System.Random rng = new System.Random();
        public readonly List<WorldLabel> Labels = new List<WorldLabel>();
        public readonly List<SkillArea> Areas = new List<SkillArea>();
        public Tile Spawn { get; private set; }

        public static Vector3 TileCenter(Tile t) => new Vector3(t.X, 0f, t.Z);
        public static Tile TileAt(Vector3 p) => new Tile(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.z));

        public static World Create()
        {
            var world = new GameObject("World").AddComponent<World>();
            world.Build();
            return world;
        }

        void Build()
        {
            Grid = new TileGrid(MainRegion.xMax, MainRegion.yMax);
            // Everything outside the two regions is solid.
            for (int x = 0; x < Grid.Width; x++)
                for (int z = 0; z < Grid.Height; z++)
                    if (!DevRegion.Contains(new Vector2Int(x, z)) && !MainRegion.Contains(new Vector2Int(x, z)))
                        Grid.SetBlocked(new Tile(x, z), true);
            BuildBackdrop();
            BuildDevArea();

            MainLayout = WorldGen.Generate(MainSize, MainSize, MainSeed);
            MainSpawn = MainWorldBuilder.Build(this, MainLayout, new Tile(MainRegion.x, MainRegion.y));
            Spawn = MainSpawn;
        }

        void BuildDevArea()
        {
            BuildGround(DevRegion, new Color(0.33f, 0.52f, 0.24f), new Color(0.27f, 0.44f, 0.2f));
            BuildWalls(DevRegion);
            AddLabel(TileCenter(DevArea.Spawn) + new Vector3(0f, 0.2f, -1.5f), "Dev Area", new Color(1f, 0.9f, 0.5f));
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
                Visuals.Prim(PrimitiveType.Cube, transform, new Vector3(center.x, 0.01f, center.z),
                    new Vector3(plot.width, 0.02f, plot.height), area.GroundColor);
                AddLabel(new Vector3(center.x, 0.3f, plot.y + plot.height - 0.5f), area.Name, Color.white);
                area.Build(this);
            }
        }

        /// Darker grass far out under everything, so the regions don't float in the sky.
        void BuildBackdrop()
        {
            Visuals.Prim(PrimitiveType.Cube, transform, new Vector3((Grid.Width - 1) * 0.5f, -0.3f, (Grid.Height - 1) * 0.5f),
                new Vector3(Grid.Width + 240f, 0.5f, Grid.Height + 240f), new Color(0.24f, 0.4f, 0.19f));
        }

        /// A region's ground: a slab whose top is at y = 0, edges on tile boundaries, with a faint line round each tile.
        public void BuildGround(RectInt region, Color fill, Color line)
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Ground";
            ground.transform.SetParent(transform, false);
            ground.transform.localPosition = new Vector3(region.x + (region.width - 1) * 0.5f, -0.25f, region.y + (region.height - 1) * 0.5f);
            ground.transform.localScale = new Vector3(region.width, 0.5f, region.height);
            var mat = Visuals.NewMat(Color.white);
            mat.mainTexture = Visuals.TileTexture(fill, line);
            mat.mainTextureScale = new Vector2(region.width, region.height);
            ground.GetComponent<Renderer>().sharedMaterial = mat;
        }

        /// A low wall on a region's outermost ring of tiles.
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
            Visuals.Prim(PrimitiveType.Cube, transform, new Vector3(cx, height / 2, z0), new Vector3(w, height, 1f), stone, true);
            Visuals.Prim(PrimitiveType.Cube, transform, new Vector3(cx, height / 2, z1), new Vector3(w, height, 1f), stone, true);
            Visuals.Prim(PrimitiveType.Cube, transform, new Vector3(x0, height / 2, cz), new Vector3(1f, height, h - 2), stone, true);
            Visuals.Prim(PrimitiveType.Cube, transform, new Vector3(x1, height / 2, cz), new Vector3(1f, height, h - 2), stone, true);
        }

        public void AddLabel(Vector3 position, string text, Color color)
        {
            Labels.Add(new WorldLabel { Position = position, Text = text, Color = color });
        }

        /// A station (bank booth, altar, furnace...) on a tile, which it blocks, with its name floating over it.
        public StationObject AddStation(string stationId, Tile tile)
        {
            var def = ContentDb.Current.Station(stationId);
            var station = StationObject.Create(def, tile, transform);
            Grid.SetBlocked(tile, true);
            Stations.Add(station);
            if (def.Label != null)
                AddLabel(TileCenter(tile) + Vector3.up * def.LabelHeight, def.Label, new Color(1f, 0.85f, 0.3f));
            return station;
        }

        /// A gathering node (tree, rock, fishing spot). Trees and rocks block their tile; a fishing spot sits
        /// on water and moves between the `shore` tiles.
        public NodeObject AddNode(string nodeId, Tile tile, System.Random random, List<Tile> shore = null)
        {
            var def = ContentDb.Current.Node(nodeId);
            var node = NodeObject.Create(def, tile, transform, random, shore);
            if (def.Blocks)
                Grid.SetBlocked(tile, true);
            Nodes.Add(node);
            return node;
        }

        /// A shop keeper running a shop (each keeper has its own stock).
        public ShopKeeper AddShopKeeper(Tile tile, string shopId)
        {
            var npc = ShopKeeper.Create(tile, new Shop(ContentDb.Current.Shop(shopId)), transform);
            Grid.SetBlocked(tile, true);
            ShopKeepers.Add(npc);
            AddLabel(TileCenter(tile) + Vector3.up * 2.6f, "Shop", new Color(1f, 0.85f, 0.3f));
            return npc;
        }

        public Npc AddNpc(NpcDef def, Tile tile, RectInt home)
        {
            var npc = Npc.Create(def, tile, home, transform, rng);
            Npcs.Add(npc);
            return npc;
        }

        /// Puts an item on the ground. A stackable item joins a pile of the same item on that tile.
        public GroundItem SpawnGroundItem(string itemId, int count, Tile tile)
        {
            var def = ContentDb.Current.Item(itemId);
            if (def == null || count <= 0)
                return null;
            if (def.Stackable)
                foreach (var g in GroundItems)
                    if (g.ItemId == itemId && g.Tile == tile)
                    {
                        g.AddCount(count);
                        return g;
                    }
            var item = GroundItem.Create(itemId, count, tile, transform, rng);
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
            var fire = Fire.Create(tile, burnSeconds, transform);
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

        /// Water: blocked tiles under a blue surface.
        public void AddWater(RectInt area)
        {
            for (int x = area.x; x < area.x + area.width; x++)
                for (int z = area.y; z < area.y + area.height; z++)
                    Grid.SetBlocked(new Tile(x, z), true);
            var center = new Vector3(area.x + (area.width - 1) * 0.5f, 0.025f, area.y + (area.height - 1) * 0.5f);
            Visuals.Prim(PrimitiveType.Cube, transform, center, new Vector3(area.width, 0.03f, area.height), new Color(0.2f, 0.42f, 0.7f));
            // A sandy bank round the edge.
            Visuals.Prim(PrimitiveType.Cube, transform, center - Vector3.up * 0.01f, new Vector3(area.width + 0.6f, 0.03f, area.height + 0.6f), new Color(0.76f, 0.68f, 0.48f));
        }

        /// A walkable tile next to the given one (for teleports and getting un-stuck).
        public Tile NearestWalkable(Tile t)
        {
            t = Grid.Clamp(t);
            for (int r = 0; r < 10; r++)
                for (int dx = -r; dx <= r; dx++)
                    for (int dz = -r; dz <= r; dz++)
                    {
                        var c = new Tile(t.X + dx, t.Z + dz);
                        if (Grid.IsWalkable(c))
                            return c;
                    }
            return Spawn;
        }
    }
}
