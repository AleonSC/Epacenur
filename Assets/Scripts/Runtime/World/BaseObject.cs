using System;
using System.Collections.Generic;
using Epacenur.Content;
using Epacenur.Core;
using UnityEngine;

namespace Epacenur.Runtime
{
    /// A base as saved with its world: where it is, and every piece (and what's in each chest).
    [Serializable]
    public sealed class BaseSave
    {
        public int id;
        public double x, z, angle, floor;
        public List<PieceSave> pieces = new List<PieceSave>();

        [Serializable]
        public sealed class PieceSave
        {
            public int kind;        // 0 foundation, 1 edge, 2 roof, 3 furniture
            public long key;        // Structure.Key or EdgeKey
            public string piece;
            public int facing;
            public string[] items;  // a chest's contents
            public long[] counts;
        }

        public static BaseSave From(Structure s)
        {
            var b = new BaseSave { id = s.Id, x = s.X, z = s.Z, angle = s.Angle, floor = s.Floor };
            foreach (var p in s.Foundations)
                b.pieces.Add(new PieceSave { kind = 0, key = p.Key, piece = p.Value });
            foreach (var p in s.Edges)
                b.pieces.Add(new PieceSave { kind = 1, key = p.Key, piece = p.Value });
            foreach (var p in s.Roofs)
                b.pieces.Add(new PieceSave { kind = 2, key = p.Key, piece = p.Value });
            foreach (var p in s.Furniture)
            {
                var save = new PieceSave { kind = 3, key = p.Key, piece = p.Value.Piece, facing = p.Value.Facing };
                if (p.Value.Store != null)
                {
                    save.items = new string[p.Value.Store.UsedSlots];
                    save.counts = new long[p.Value.Store.UsedSlots];
                    for (int i = 0; i < p.Value.Store.UsedSlots; i++)
                    {
                        save.items[i] = p.Value.Store[i].ItemId;
                        save.counts[i] = p.Value.Store[i].Count;
                    }
                }
                b.pieces.Add(save);
            }
            return b;
        }

        /// Adds this base to a set (pieces whose content no longer exists are left out).
        public void Restore(BaseSet set)
        {
            var db = ContentDb.Current;
            var s = set.Create(x, z, angle, floor, id);
            foreach (var p in pieces)
            {
                var def = db.Piece(p.piece);
                if (def == null)
                    continue;
                if (p.kind == 0) s.Foundations[p.key] = p.piece;
                else if (p.kind == 1) s.Edges[p.key] = p.piece;
                else if (p.kind == 2) s.Roofs[p.key] = p.piece;
                else
                {
                    var f = new Furnishing { Piece = p.piece, Facing = p.facing };
                    if (def.Use == FurnitureUse.Chest)
                    {
                        f.Store = new Bank(ConstructionModule.ChestSlots);
                        for (int i = 0; p.items != null && p.counts != null && i < p.items.Length && i < p.counts.Length; i++)
                            if (db.Item(p.items[i]) != null)
                                f.Store.Add(p.items[i], p.counts[i]);
                    }
                    s.Furniture[p.key] = f;
                }
            }
            if (s.Foundations.Count == 0)
                set.Remove(s);
        }
    }

    /// Marks a built piece of a base (on the GameObject that holds it): which base, what kind, where.
    public sealed class BasePart : MonoBehaviour
    {
        public Structure Base;
        public PieceKind Kind;
        public long Key;            // Structure.Key (cells) or EdgeKey (edges)
        public PieceDef Def;
    }

    /// A piece of a base you can click: every piece can be examined (and removed, with a hammer); a chest opens
    /// and a bed is slept in.
    public sealed class PieceObject : Interactable
    {
        public BasePart Part;
        Furnishing Furnishing => Part.Base.Furniture.TryGetValue(Part.Key, out var f) ? f : null;

        public override string Action => Part.Def.Use == FurnitureUse.Chest ? "Open" : Part.Def.Use == FurnitureUse.Bed ? "Sleep in" : "";
        public override bool HasAction => Part.Def.Use == FurnitureUse.Chest || Part.Def.Use == FurnitureUse.Bed;
        public override string DisplayName => Part.Def.Name;
        public override string Examine => Part.Def.Examine;

        public void Init(BasePart part, Tile tile)
        {
            Part = part;
            Tile = tile;
        }

        public override void Interact()
        {
            var g = Game.Instance;
            var f = Furnishing;
            if (f == null)
                return;
            if (Part.Def.Use == FurnitureUse.Chest)
                g.Hud.OpenChest(f.Store, this);
            else if (Part.Def.Use == FurnitureUse.Bed)
                g.SetBed(Part.Base, Part.Key);
        }
    }

    /// A base, built: its pieces from Unity primitives, on a root turned to the base's angle (cell (i, j) at
    /// local (i, y, j)), and its stations. Roofs hide while you're inside (tile mode), so you can see in.
    public sealed class BaseObject : MonoBehaviour
    {
        public const float WallHeight = 2.2f, WallThick = 0.16f, SlabThick = 0.2f;

        public Structure S { get; private set; }
        World world;
        Transform pieces;
        readonly List<GameObject> roofs = new List<GameObject>();
        readonly List<StationObject> stations = new List<StationObject>();
        bool roofsShown = true;

        public static BaseObject Create(World world, Structure s)
        {
            var go = new GameObject("Base " + s.Id);
            go.transform.SetParent(world.transform, false);
            var b = go.AddComponent<BaseObject>();
            b.world = world;
            b.S = s;
            return b;
        }

        public void TakeDown()
        {
            Clear();
            Destroy(gameObject);
        }

        void Clear()
        {
            foreach (var st in stations)
                world.Stations.Remove(st);
            stations.Clear();
            roofs.Clear();
            for (int i = transform.childCount - 1; i >= 0; i--)
                Destroy(transform.GetChild(i).gameObject);
        }

        public void Rebuild()
        {
            Clear();
            pieces = new GameObject("Pieces").transform;
            pieces.SetParent(transform, false);
            pieces.localPosition = new Vector3((float)S.X, 0f, (float)S.Z);
            pieces.localRotation = Quaternion.Euler(0f, (float)S.Angle, 0f);
            var db = ContentDb.Current;
            float floor = (float)S.Floor;
            foreach (var p in S.Foundations)
            {
                Structure.Unkey(p.Key, out int i, out int j);
                var def = db.Piece(p.Value);
                var go = Holder(def, PieceKind.Foundation, p.Key, i, j);
                Foundation(go.transform, new Vector3(i, floor, j), def, null, true);
                Supports(go.transform, i, j, def);
            }
            foreach (var p in S.Edges)
            {
                Structure.UnEdgeKey(p.Key, out int i, out int j, out int side);
                var def = db.Piece(p.Value);
                // The edge's own cell if it has a floor, else the one across it.
                int ci = S.HasFoundation(i, j) ? i : side == 1 ? i + 1 : i, cj = S.HasFoundation(i, j) ? j : side == 0 ? j + 1 : j;
                var go = Holder(def, def.Kind, p.Key, ci, cj);
                var edge = new GameObject("Edge").transform;
                edge.SetParent(go.transform, false);
                edge.localPosition = new Vector3(i, floor, j);
                edge.localRotation = Quaternion.Euler(0f, side == 1 ? 90f : 0f, 0f);
                Edge(edge, def, null, true);
            }
            foreach (var p in S.Roofs)
            {
                Structure.Unkey(p.Key, out int i, out int j);
                var def = db.Piece(p.Value);
                var go = Holder(def, PieceKind.Roof, p.Key, i, j);
                Roof(go.transform, new Vector3(i, floor, j), def, null, true,
                    !S.Roofs.ContainsKey(Structure.Key(i, j + 1)), !S.Roofs.ContainsKey(Structure.Key(i + 1, j)),
                    !S.Roofs.ContainsKey(Structure.Key(i, j - 1)), !S.Roofs.ContainsKey(Structure.Key(i - 1, j)));
                roofs.Add(go);
            }
            foreach (var p in S.Furniture)
            {
                Structure.Unkey(p.Key, out int i, out int j);
                var def = db.Piece(p.Value.Piece);
                var cell = BaseSet.CellTile(S, i, j);
                if (def.Use == FurnitureUse.Station)
                {
                    // A station is a StationObject like any other, standing in the cell.
                    var station = StationObject.Create(db.Station(def.Station), cell, transform);
                    station.transform.localRotation = Quaternion.Euler(0f, (float)S.Angle + p.Value.Facing * 90f, 0f);
                    var part = station.gameObject.AddComponent<BasePart>();
                    part.Base = S;
                    part.Kind = PieceKind.Furniture;
                    part.Key = p.Key;
                    part.Def = def;
                    stations.Add(station);
                    world.Stations.Add(station);
                    continue;
                }
                var go = Holder(def, PieceKind.Furniture, p.Key, i, j);
                var model = ModelBuilder.Build(def.Model, go.transform, "Model");
                model.localPosition = new Vector3(i, floor, j);
                model.localRotation = Quaternion.Euler(0f, p.Value.Facing * 90f, 0f);
                var box = ModelBuilder.AddBox(model.gameObject, def.Model);
                box.isTrigger = false;
            }
            roofsShown = true;
        }

        /// The GameObject that holds a piece: its BasePart, and a PieceObject to click.
        GameObject Holder(PieceDef def, PieceKind kind, long key, int i, int j)
        {
            var go = new GameObject(def.Name);
            go.transform.SetParent(pieces, false);
            var part = go.AddComponent<BasePart>();
            part.Base = S;
            part.Kind = kind;
            part.Key = key;
            part.Def = def;
            go.AddComponent<PieceObject>().Init(part, BaseSet.CellTile(S, i, j));
            return go;
        }

        /// Posts down to the ground under a floor that stands above it, and steps up to it on its open edges.
        void Supports(Transform parent, int i, int j, PieceDef def)
        {
            float floor = (float)S.Floor, under = floor - SlabThick;
            var post = ModelBuilder.ToColor(def.Trim);
            for (int c = 0; c < 4; c++)
            {
                float di = (c & 1) == 0 ? -0.42f : 0.42f, dj = c < 2 ? -0.42f : 0.42f;
                S.ToMap(i + di, j + dj, out double x, out double z);
                float g = (float)world.Overworld.Elevation(x, z);
                if (under - g > 0.04f)
                    Visuals.Prim(PrimitiveType.Cube, parent, new Vector3(i + di, (under + g - 0.1f) / 2f, j + dj), new Vector3(0.14f, under - g + 0.1f, 0.14f), post);
            }
            for (int d = 0; d < 4; d++)
            {
                int dx = d == 0 ? 1 : d == 1 ? -1 : 0, dz = d == 2 ? 1 : d == 3 ? -1 : 0;
                if (S.HasFoundation(i + dx, j + dz) || !S.Open(i, j, dx, dz))
                    continue;
                S.ToMap(i + dx * 0.9, j + dz * 0.9, out double x, out double z);
                float g = (float)world.Overworld.Elevation(x, z), rise = floor - g;
                if (rise < 0.24f)
                    continue;
                // As many steps as it takes to keep each one low enough to walk up.
                int n = Mathf.CeilToInt(rise / 0.26f) - 1;
                for (int k = 1; k <= n; k++)
                {
                    float top = g + rise * k / (n + 1), out_ = 0.5f + 0.24f * (n - k) + 0.12f;
                    var at = new Vector3(i + dx * out_, (top + g - 0.2f) / 2f, j + dz * out_);
                    var size = dx != 0 ? new Vector3(0.24f, top - g + 0.2f, 0.8f) : new Vector3(0.8f, top - g + 0.2f, 0.24f);
                    Visuals.Prim(PrimitiveType.Cube, parent, at, size, ModelBuilder.ToColor(def.Colour) * 0.9f, true);
                }
            }
        }

        public void Update()
        {
            // Roofs hide while you're inside (in tile mode, where the camera looks down from above).
            var g = Game.Instance;
            bool inside = g != null && g.Player != null && g.Player.Mode == ViewMode.Tile
                && g.World.Bases.TryCell(g.Player.CurrentTile, out var s, out int i, out int j) && s == S && S.HasFoundation(i, j);
            if (inside == !roofsShown)
                return;
            roofsShown = !inside;
            foreach (var r in roofs)
                if (r != null)
                    r.SetActive(roofsShown);
        }

        // ---- The pieces' shapes (also used for the ghost of a piece being placed: a tint, no colliders) ----

        static Color Tinted(Rgb c, Color? tint) => tint ?? ModelBuilder.ToColor(c);

        /// A floor slab, its top at `at.y`, with a darker edging under it.
        public static void Foundation(Transform parent, Vector3 at, PieceDef def, Color? tint, bool solid)
        {
            Visuals.Prim(PrimitiveType.Cube, parent, at + Vector3.down * (SlabThick / 2f), new Vector3(1f, SlabThick, 1f), Tinted(def.Colour, tint), solid);
            Visuals.Prim(PrimitiveType.Cube, parent, at + Vector3.down * (SlabThick + 0.02f), new Vector3(1.02f, 0.04f, 1.02f), Tinted(def.Trim, tint));
            // Boards or flagstones: faint lines across the top.
            var line = tint ?? ModelBuilder.ToColor(def.Colour) * 0.82f;
            for (int k = 0; k < 2; k++)
                Visuals.Prim(PrimitiveType.Cube, parent, at + new Vector3(-0.17f + k * 0.34f, 0.002f, 0f), new Vector3(0.02f, 0.004f, 0.98f), line);
        }

        /// A wall, doorway or window along +x, on the north edge of the cell at the parent's origin (whose y is the floor).
        public static void Edge(Transform parent, PieceDef def, Color? tint, bool solid)
        {
            var c = Tinted(def.Colour, tint);
            var trim = Tinted(def.Trim, tint);
            float h = WallHeight, t = WallThick, z = 0.5f;
            switch (def.Kind)
            {
                case PieceKind.Doorway:
                    const float post = 0.14f;
                    Visuals.Prim(PrimitiveType.Cube, parent, new Vector3(-0.5f + post / 2f, h / 2f, z), new Vector3(post, h, t + 0.02f), c, solid);
                    Visuals.Prim(PrimitiveType.Cube, parent, new Vector3(0.5f - post / 2f, h / 2f, z), new Vector3(post, h, t + 0.02f), c, solid);
                    Visuals.Prim(PrimitiveType.Cube, parent, new Vector3(0f, h - 0.18f, z), new Vector3(1f, 0.36f, t), c, solid);
                    Visuals.Prim(PrimitiveType.Cube, parent, new Vector3(0f, h - 0.37f, z), new Vector3(0.74f, 0.04f, t + 0.04f), trim);
                    break;
                case PieceKind.Window:
                    Visuals.Prim(PrimitiveType.Cube, parent, new Vector3(0f, 0.45f, z), new Vector3(1f + t, 0.9f, t), c, solid);
                    Visuals.Prim(PrimitiveType.Cube, parent, new Vector3(0f, h - 0.3f, z), new Vector3(1f + t, 0.6f, t), c, solid);
                    Visuals.Prim(PrimitiveType.Cube, parent, new Vector3(-0.36f, 1.25f, z), new Vector3(0.28f + t, 0.7f, t), c, solid);
                    Visuals.Prim(PrimitiveType.Cube, parent, new Vector3(0.36f, 1.25f, z), new Vector3(0.28f + t, 0.7f, t), c, solid);
                    Visuals.Prim(PrimitiveType.Cube, parent, new Vector3(0f, 1.25f, z), new Vector3(0.44f, 0.7f, 0.03f), tint ?? new Color(0.5f, 0.68f, 0.82f), solid);
                    Visuals.Prim(PrimitiveType.Cube, parent, new Vector3(0f, 1.25f, z), new Vector3(0.03f, 0.7f, 0.05f), trim);
                    Visuals.Prim(PrimitiveType.Cube, parent, new Vector3(0f, 0.92f, z), new Vector3(0.56f, 0.05f, t + 0.08f), trim);
                    break;
                default:
                    Visuals.Prim(PrimitiveType.Cube, parent, new Vector3(0f, h / 2f, z), new Vector3(1f + t, h, t), c, solid);
                    // Seams between the boards or courses of stone.
                    for (int k = 1; k < 4; k++)
                        Visuals.Prim(PrimitiveType.Cube, parent, new Vector3(0f, h * k / 4f, z), new Vector3(1f + t + 0.01f, 0.025f, t + 0.01f), tint ?? c * 0.85f);
                    break;
            }
            if (def.Kind != PieceKind.Doorway)
                Visuals.Prim(PrimitiveType.Cube, parent, new Vector3(0f, h + 0.03f, z), new Vector3(1f + t + 0.04f, 0.06f, t + 0.04f), trim);
            Visuals.Prim(PrimitiveType.Cube, parent, new Vector3(0f, 0.05f, z), new Vector3(def.Kind == PieceKind.Doorway ? 0.3f : 1f + t + 0.02f, 0.1f, t + 0.03f), trim);
        }

        /// A roof over a cell (at `at`: the cell's centre at floor height), with a fascia along its open sides.
        public static void Roof(Transform parent, Vector3 at, PieceDef def, Color? tint, bool solid, bool north, bool east, bool south, bool west)
        {
            float top = at.y + WallHeight + 0.06f;
            var c = Tinted(def.Colour, tint);
            var trim = Tinted(def.Trim, tint);
            Visuals.Prim(PrimitiveType.Cube, parent, new Vector3(at.x, top + 0.06f, at.z), new Vector3(1.06f, 0.12f, 1.06f), c, solid);
            // Shingle or slate rows.
            for (int k = 0; k < 3; k++)
                Visuals.Prim(PrimitiveType.Cube, parent, new Vector3(at.x, top + 0.122f, at.z - 0.33f + k * 0.33f), new Vector3(1.06f, 0.006f, 0.04f), tint ?? c * 0.8f);
            if (north) Visuals.Prim(PrimitiveType.Cube, parent, new Vector3(at.x, top, at.z + 0.53f), new Vector3(1.08f, 0.2f, 0.06f), trim);
            if (south) Visuals.Prim(PrimitiveType.Cube, parent, new Vector3(at.x, top, at.z - 0.53f), new Vector3(1.08f, 0.2f, 0.06f), trim);
            if (east) Visuals.Prim(PrimitiveType.Cube, parent, new Vector3(at.x + 0.53f, top, at.z), new Vector3(0.06f, 0.2f, 1.08f), trim);
            if (west) Visuals.Prim(PrimitiveType.Cube, parent, new Vector3(at.x - 0.53f, top, at.z), new Vector3(0.06f, 0.2f, 1.08f), trim);
        }
    }
}
