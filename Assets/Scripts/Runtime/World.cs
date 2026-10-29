using System.Collections.Generic;
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
    public sealed class World : MonoBehaviour
    {
        public TileGrid Grid { get; private set; }
        public readonly List<TreeObject> Trees = new List<TreeObject>();
        public readonly List<BankBooth> BankBooths = new List<BankBooth>();
        public readonly List<ShopKeeper> ShopKeepers = new List<ShopKeeper>();
        public readonly List<Npc> Npcs = new List<Npc>();
        public readonly List<GroundItem> GroundItems = new List<GroundItem>();
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
            Grid = new TileGrid(DevArea.Width, DevArea.Height);
            Spawn = DevArea.Spawn;
            BuildGround();
            BuildWalls();
            AddLabel(TileCenter(Spawn) + new Vector3(0f, 0.2f, 2.5f), "Dev Area", new Color(1f, 0.9f, 0.5f));
            AddBankBooth(DevArea.BankBooth);
            AddShopKeeper(DevArea.ShopKeeper, Shops.DevAreaStore());

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

        void BuildGround()
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Ground";
            ground.transform.SetParent(transform, false);
            // The top face is at y = 0 and the edges sit on tile boundaries (x = -0.5 .. Width - 0.5).
            ground.transform.localPosition = new Vector3((Grid.Width - 1) * 0.5f, -0.25f, (Grid.Height - 1) * 0.5f);
            ground.transform.localScale = new Vector3(Grid.Width, 0.5f, Grid.Height);
            var mat = Visuals.NewMat(Color.white);
            mat.mainTexture = Visuals.TileTexture(new Color(0.33f, 0.52f, 0.24f), new Color(0.27f, 0.44f, 0.2f));
            mat.mainTextureScale = new Vector2(Grid.Width, Grid.Height);
            ground.GetComponent<Renderer>().sharedMaterial = mat;

            // Darker grass beyond the walls, so the dev area doesn't float in the sky.
            Visuals.Prim(PrimitiveType.Cube, transform, new Vector3((Grid.Width - 1) * 0.5f, -0.3f, (Grid.Height - 1) * 0.5f),
                new Vector3(Grid.Width + 200f, 0.5f, Grid.Height + 200f), new Color(0.24f, 0.4f, 0.19f));
        }

        // A low stone wall on the outermost ring of tiles.
        void BuildWalls()
        {
            int w = Grid.Width, h = Grid.Height;
            for (int x = 0; x < w; x++)
            {
                Grid.SetBlocked(new Tile(x, 0), true);
                Grid.SetBlocked(new Tile(x, h - 1), true);
            }
            for (int z = 0; z < h; z++)
            {
                Grid.SetBlocked(new Tile(0, z), true);
                Grid.SetBlocked(new Tile(w - 1, z), true);
            }
            var stone = new Color(0.55f, 0.55f, 0.58f);
            const float height = 0.8f;
            Visuals.Prim(PrimitiveType.Cube, transform, new Vector3((w - 1) * 0.5f, height / 2, 0f), new Vector3(w, height, 1f), stone, true);
            Visuals.Prim(PrimitiveType.Cube, transform, new Vector3((w - 1) * 0.5f, height / 2, h - 1), new Vector3(w, height, 1f), stone, true);
            Visuals.Prim(PrimitiveType.Cube, transform, new Vector3(0f, height / 2, (h - 1) * 0.5f), new Vector3(1f, height, h - 2), stone, true);
            Visuals.Prim(PrimitiveType.Cube, transform, new Vector3(w - 1, height / 2, (h - 1) * 0.5f), new Vector3(1f, height, h - 2), stone, true);
        }

        public void AddLabel(Vector3 position, string text, Color color)
        {
            Labels.Add(new WorldLabel { Position = position, Text = text, Color = color });
        }

        public BankBooth AddBankBooth(Tile tile)
        {
            var booth = BankBooth.Create(tile, transform);
            Grid.SetBlocked(tile, true);
            BankBooths.Add(booth);
            AddLabel(TileCenter(tile) + Vector3.up * 2.6f, "Bank", new Color(1f, 0.85f, 0.3f));
            return booth;
        }

        public ShopKeeper AddShopKeeper(Tile tile, Shop shop)
        {
            var npc = ShopKeeper.Create(tile, shop, transform);
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
        public GroundItem SpawnGroundItem(int itemId, int count, Tile tile)
        {
            var def = ItemDefs.Get(itemId);
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

        public TreeObject AddTree(TreeDef def, Tile tile, System.Random rng)
        {
            var tree = TreeObject.Create(def, tile, transform, rng);
            Grid.SetBlocked(tile, true);
            Trees.Add(tree);
            return tree;
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
