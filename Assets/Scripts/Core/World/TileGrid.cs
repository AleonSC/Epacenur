using System;
using System.Collections.Generic;

namespace Epacenur.Core
{
    /// A tile on the world grid. Tile (x, z) is the 1 x 1 square centred on world position (x, 0, z).
    public struct Tile : IEquatable<Tile>
    {
        public int X, Z;
        public Tile(int x, int z) { X = x; Z = z; }

        public bool Equals(Tile o) => X == o.X && Z == o.Z;
        public override bool Equals(object o) => o is Tile t && Equals(t);
        public override int GetHashCode() => X * 73856093 ^ Z * 19349663;
        public static bool operator ==(Tile a, Tile b) => a.Equals(b);
        public static bool operator !=(Tile a, Tile b) => !a.Equals(b);
        public override string ToString() => "(" + X + ", " + Z + ")";

        /// Chebyshev distance: the number of steps when diagonal steps are allowed.
        public int StepsTo(Tile o) => Math.Max(Math.Abs(X - o.X), Math.Abs(Z - o.Z));

        /// Directly north, south, east or west of the other tile.
        public bool IsCardinalNeighbour(Tile o) => Math.Abs(X - o.X) + Math.Abs(Z - o.Z) == 1;
    }

    /// Which tiles can be walked on. Pathfinding works on any of these: a fixed-size TileGrid, or the
    /// endless ChunkedGrid of the overworld.
    public abstract class WalkGrid
    {
        public abstract bool IsWalkable(Tile t);
        public abstract void SetBlocked(Tile t, bool value);

        /// Whether a single step from a tile to a neighbour is allowed. As in RuneScape, a diagonal
        /// step needs both tiles it cuts past to be free, so you can't squeeze between two trees.
        public virtual bool CanStep(Tile from, int dx, int dz)
        {
            var to = new Tile(from.X + dx, from.Z + dz);
            if (!IsWalkable(to))
                return false;
            if (dx != 0 && dz != 0)
                return IsWalkable(new Tile(from.X + dx, from.Z)) && IsWalkable(new Tile(from.X, from.Z + dz));
            return true;
        }

        static readonly List<Tile> NoLinks = new List<Tile>();

        /// Tiles you can step to from this one besides its eight neighbours (none, unless the grid joins on
        /// places of its own: see BaseWalkGrid). Each is one step.
        public virtual List<Tile> Links(Tile t) => NoLinks;

        /// About how many steps apart two tiles are (for the pathfinder's guesses).
        public virtual int Estimate(Tile a, Tile b) => a.StepsTo(b);
    }

    /// A fixed-size grid of tiles, (0, 0) to (Width - 1, Height - 1); everything outside is blocked.
    public sealed class TileGrid : WalkGrid
    {
        public readonly int Width, Height;
        readonly bool[,] blocked;

        public TileGrid(int width, int height)
        {
            Width = width;
            Height = height;
            blocked = new bool[width, height];
        }

        public bool InBounds(Tile t) => t.X >= 0 && t.Z >= 0 && t.X < Width && t.Z < Height;
        public override bool IsWalkable(Tile t) => InBounds(t) && !blocked[t.X, t.Z];
        public override void SetBlocked(Tile t, bool value) { if (InBounds(t)) blocked[t.X, t.Z] = value; }
    }

    /// An endless grid held as square chunks: only the chunks that are loaded exist, and everything
    /// outside them is blocked (so a path can't wander into the unknown).
    public sealed class ChunkedGrid : WalkGrid
    {
        public readonly int ChunkSize;
        readonly Dictionary<long, bool[]> chunks = new Dictionary<long, bool[]>();

        public ChunkedGrid(int chunkSize) { ChunkSize = chunkSize; }

        public static long Key(int cx, int cz) => ((long)cx << 32) ^ (uint)cz;

        /// Which chunk a tile is in (floor division, so negative tiles work too).
        public int ChunkOf(int tileCoord) => tileCoord >= 0 ? tileCoord / ChunkSize : (tileCoord + 1) / ChunkSize - 1;

        public bool IsLoaded(int cx, int cz) => chunks.ContainsKey(Key(cx, cz));
        public int LoadedCount => chunks.Count;

        /// Adds a chunk: `blocked(tile)` says which of its tiles can't be walked on.
        public void Load(int cx, int cz, Func<Tile, bool> blocked)
        {
            var cells = new bool[ChunkSize * ChunkSize];
            for (int x = 0; x < ChunkSize; x++)
                for (int z = 0; z < ChunkSize; z++)
                    cells[x * ChunkSize + z] = blocked(new Tile(cx * ChunkSize + x, cz * ChunkSize + z));
            chunks[Key(cx, cz)] = cells;
        }

        public void Unload(int cx, int cz) => chunks.Remove(Key(cx, cz));
        public void Clear() => chunks.Clear();

        bool[] Cells(Tile t, out int index)
        {
            int cx = ChunkOf(t.X), cz = ChunkOf(t.Z);
            index = (t.X - cx * ChunkSize) * ChunkSize + (t.Z - cz * ChunkSize);
            return chunks.TryGetValue(Key(cx, cz), out var cells) ? cells : null;
        }

        public override bool IsWalkable(Tile t)
        {
            var cells = Cells(t, out int i);
            return cells != null && !cells[i];
        }

        public override void SetBlocked(Tile t, bool value)
        {
            var cells = Cells(t, out int i);
            if (cells != null)
                cells[i] = value;
        }
    }

    /// A* over the tile grid with 8-way movement.
    public static class Pathfinder
    {
        static readonly int[] Dx = { 1, -1, 0, 0, 1, 1, -1, -1 };
        static readonly int[] Dz = { 0, 0, 1, -1, 1, -1, 1, -1 };

        /// Path to a tile, not including the start. If the tile can't be reached (a tree, a wall),
        /// walks to the reachable tile closest to it instead, like clicking a blocked tile in RuneScape.
        /// Empty when already there or nothing better is reachable.
        public static List<Tile> PathTo(WalkGrid grid, Tile start, Tile target)
        {
            return Find(grid, start, t => t == target, t => grid.Estimate(t, target));
        }

        /// Path to any free tile directly north, south, east or west of an object (to interact with it).
        /// Returns null when no such tile can be reached.
        public static List<Tile> PathToInteract(WalkGrid grid, Tile start, Tile objectTile)
        {
            if (start.IsCardinalNeighbour(objectTile))
                return new List<Tile>();
            var path = Find(grid, start, t => t.IsCardinalNeighbour(objectTile), t => Math.Max(0, grid.Estimate(t, objectTile) - 1));
            if (path.Count == 0 || !path[path.Count - 1].IsCardinalNeighbour(objectTile))
                return null;
            return path;
        }

        static List<Tile> Find(WalkGrid grid, Tile start, Func<Tile, bool> isGoal, Func<Tile, int> heuristic, int maxNodes = 20000)
        {
            var came = new Dictionary<Tile, Tile>();
            var cost = new Dictionary<Tile, int> { [start] = 0 };
            var open = new MinHeap();
            open.Push(start, heuristic(start));
            Tile best = start;
            int bestH = heuristic(start), bestG = 0;
            int expanded = 0;

            while (open.Count > 0 && expanded++ < maxNodes)
            {
                Tile cur = open.Pop();
                int g = cost[cur];
                if (isGoal(cur))
                    return Build(came, start, cur);
                int h = heuristic(cur);
                if (h < bestH || (h == bestH && g < bestG))
                {
                    best = cur;
                    bestH = h;
                    bestG = g;
                }
                for (int i = 0; i < 8; i++)
                {
                    if (!grid.CanStep(cur, Dx[i], Dz[i]))
                        continue;
                    var next = new Tile(cur.X + Dx[i], cur.Z + Dz[i]);
                    // Straight steps cost 10, diagonals 11: both are one tick in RuneScape, the extra 1
                    // just prefers straighter-looking paths when they are equally short.
                    int ng = g + (i < 4 ? 10 : 11);
                    if (cost.TryGetValue(next, out int old) && old <= ng)
                        continue;
                    cost[next] = ng;
                    came[next] = cur;
                    open.Push(next, ng + heuristic(next) * 10);
                }
                foreach (var next in grid.Links(cur))
                {
                    if (!grid.IsWalkable(next))
                        continue;
                    int ng = g + 10;
                    if (cost.TryGetValue(next, out int old) && old <= ng)
                        continue;
                    cost[next] = ng;
                    came[next] = cur;
                    open.Push(next, ng + heuristic(next) * 10);
                }
            }
            return Build(came, start, best);
        }

        static List<Tile> Build(Dictionary<Tile, Tile> came, Tile start, Tile end)
        {
            var path = new List<Tile>();
            for (Tile t = end; t != start; t = came[t])
                path.Add(t);
            path.Reverse();
            return path;
        }

        /// Binary heap of tiles by priority (.NET Standard 2.1 has no PriorityQueue).
        sealed class MinHeap
        {
            readonly List<KeyValuePair<int, Tile>> items = new List<KeyValuePair<int, Tile>>();
            public int Count => items.Count;

            public void Push(Tile t, int priority)
            {
                items.Add(new KeyValuePair<int, Tile>(priority, t));
                int i = items.Count - 1;
                while (i > 0)
                {
                    int p = (i - 1) / 2;
                    if (items[p].Key <= items[i].Key)
                        break;
                    Swap(p, i);
                    i = p;
                }
            }

            void Swap(int a, int b)
            {
                var t = items[a];
                items[a] = items[b];
                items[b] = t;
            }

            public Tile Pop()
            {
                Tile top = items[0].Value;
                int last = items.Count - 1;
                items[0] = items[last];
                items.RemoveAt(last);
                int i = 0;
                while (true)
                {
                    int l = i * 2 + 1, r = l + 1, m = i;
                    if (l < items.Count && items[l].Key < items[m].Key) m = l;
                    if (r < items.Count && items[r].Key < items[m].Key) m = r;
                    if (m == i)
                        break;
                    Swap(m, i);
                    i = m;
                }
                return top;
            }
        }
    }
}
