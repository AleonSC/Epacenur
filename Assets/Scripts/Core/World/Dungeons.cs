using System;
using System.Collections.Generic;

namespace Epacenur.Core
{
    /// A dungeon under a cave mouth: rooms in a grid of cells, joined by corridors (every room reachable),
    /// the ladder up in the first room and the boss in the room furthest from it.
    public sealed class DungeonLayout
    {
        public int Width, Height;
        public bool[,] Floor;                                   // walkable floor; the rest is rock
        public readonly List<Area> Rooms = new List<Area>();
        public Tile Ladder;                                      // where you arrive, by the ladder up
        public int LadderRoom, BossRoom;
        public readonly List<Tile> Torches = new List<Tile>();   // on a wall beside each room
        public int Tier;                                         // mines: the mine's tier (0: a monsters' dungeon)
        public readonly Dictionary<Tile, string> Ores = new Dictionary<Tile, string>();  // mines: the ore rocks, by tile
        public bool IsMine => Tier > 0;

        public bool IsFloor(Tile t) => t.X >= 0 && t.Z >= 0 && t.X < Width && t.Z < Height && Floor[t.X, t.Z];
    }

    public static class Dungeons
    {
        public const int Cells = 4, CellSize = 14, Border = 2;

        public static DungeonLayout Generate(int seed, int cells = Cells)
        {
            var rng = new Random(seed);
            var d = new DungeonLayout { Width = cells * CellSize + Border * 2, Height = cells * CellSize + Border * 2 };
            d.Floor = new bool[d.Width, d.Height];
            // A room in each cell, of its own size and place.
            for (int cz = 0; cz < cells; cz++)
                for (int cx = 0; cx < cells; cx++)
                {
                    int w = 6 + rng.Next(6), h = 6 + rng.Next(6);
                    int x = Border + cx * CellSize + 1 + rng.Next(CellSize - w - 1), z = Border + cz * CellSize + 1 + rng.Next(CellSize - h - 1);
                    d.Rooms.Add(new Area(x, z, w, h));
                }
            // Corridors: a random tree over the cells (so every room is reachable), plus a few loops.
            var linked = new bool[cells * cells];
            var edges = new List<int[]>();
            var stack = new Stack<int>();
            int start = rng.Next(cells * cells);
            linked[start] = true;
            stack.Push(start);
            while (stack.Count > 0)
            {
                int c = stack.Peek();
                var next = new List<int>();
                foreach (int n in Neighbours(c, cells))
                    if (!linked[n])
                        next.Add(n);
                if (next.Count == 0)
                {
                    stack.Pop();
                    continue;
                }
                int pick = next[rng.Next(next.Count)];
                linked[pick] = true;
                edges.Add(new[] { c, pick });
                stack.Push(pick);
            }
            for (int i = 0; i < 3; i++)
            {
                int c = rng.Next(cells * cells);
                var ns = new List<int>(Neighbours(c, cells));
                edges.Add(new[] { c, ns[rng.Next(ns.Count)] });
            }
            foreach (var r in d.Rooms)
                Carve(d, r);
            foreach (var e in edges)
                Corridor(d, d.Rooms[e[0]].Center, d.Rooms[e[1]].Center, rng.Next(2) == 0);

            // The ladder in the start room; the boss as far from it (in corridors) as can be.
            d.LadderRoom = start;
            d.Ladder = d.Rooms[start].Center;
            var dist = Distances(d, d.Ladder);
            int best = start, bestDist = -1;
            for (int i = 0; i < d.Rooms.Count; i++)
            {
                var c = d.Rooms[i].Center;
                if (dist[c.X, c.Z] > bestDist)
                {
                    bestDist = dist[c.X, c.Z];
                    best = i;
                }
            }
            d.BossRoom = best;
            // A torch on the rock just outside each room's north wall (in the middle), where there's rock.
            foreach (var r in d.Rooms)
            {
                var t = new Tile(r.X + r.Width / 2, r.ZMax);
                if (!d.IsFloor(t))
                    d.Torches.Add(t);
            }
            return d;
        }

        static IEnumerable<int> Neighbours(int c, int cells)
        {
            int x = c % cells, z = c / cells;
            if (x > 0) yield return c - 1;
            if (x < cells - 1) yield return c + 1;
            if (z > 0) yield return c - cells;
            if (z < cells - 1) yield return c + cells;
        }

        static void Carve(DungeonLayout d, Area a)
        {
            for (int x = a.X; x < a.XMax; x++)
                for (int z = a.Z; z < a.ZMax; z++)
                    d.Floor[x, z] = true;
        }

        /// An L-shaped corridor two tiles wide between two points.
        static void Corridor(DungeonLayout d, Tile a, Tile b, bool xFirst)
        {
            var corner = xFirst ? new Tile(b.X, a.Z) : new Tile(a.X, b.Z);
            Line(d, a, corner);
            Line(d, corner, b);
        }

        static void Line(DungeonLayout d, Tile a, Tile b)
        {
            int x0 = Math.Min(a.X, b.X), x1 = Math.Max(a.X, b.X), z0 = Math.Min(a.Z, b.Z), z1 = Math.Max(a.Z, b.Z);
            for (int x = x0; x <= x1 + 1; x++)
                for (int z = z0; z <= z1 + 1; z++)
                    if (x < d.Width - 1 && z < d.Height - 1)
                        d.Floor[x, z] = true;
        }

        /// Steps from a tile to every floor tile (int.MaxValue: can't be reached).
        public static int[,] Distances(DungeonLayout d, Tile from)
        {
            var dist = new int[d.Width, d.Height];
            for (int x = 0; x < d.Width; x++)
                for (int z = 0; z < d.Height; z++)
                    dist[x, z] = int.MaxValue;
            var q = new Queue<Tile>();
            dist[from.X, from.Z] = 0;
            q.Enqueue(from);
            while (q.Count > 0)
            {
                var t = q.Dequeue();
                foreach (var n in new[] { new Tile(t.X + 1, t.Z), new Tile(t.X - 1, t.Z), new Tile(t.X, t.Z + 1), new Tile(t.X, t.Z - 1) })
                    if (d.IsFloor(n) && dist[n.X, n.Z] == int.MaxValue)
                    {
                        dist[n.X, n.Z] = dist[t.X, t.Z] + 1;
                        q.Enqueue(n);
                    }
            }
            return dist;
        }

        /// The monsters in a dungeon, room by room (not the ladder's or the boss's room): a group of whatever lives
        /// in dungeons at this wilderness level (NpcSpawnRule with Region Dungeon), and the boss for the level.
        public static List<Spawning.Group> Groups(int danger, int rooms, Random rng, out NpcDef boss)
        {
            var groups = new List<Spawning.Group>();
            var db = ContentDb.Current;
            var options = new List<NpcDef>();
            var weights = new List<double>();
            boss = null;
            double bossWeight = 0;
            foreach (var n in db.Npcs)
                foreach (var r in n.Spawns)
                    if (r.Region == RegionKind.Dungeon && danger >= r.MinDanger && danger < r.MaxDanger)
                    {
                        if (n.Boss)
                        {
                            // The boss whose range fits best: the highest one allowed.
                            if (boss == null || n.CombatLevel > boss.CombatLevel)
                                boss = n;
                            bossWeight += r.Weight;
                        }
                        else
                        {
                            options.Add(n);
                            weights.Add(r.Weight);
                        }
                    }
            if (options.Count == 0)
                return groups;
            double total = 0;
            foreach (var w in weights)
                total += w;
            for (int i = 0; i < rooms; i++)
            {
                double pick = rng.NextDouble() * total;
                int k = 0;
                while (k < options.Count - 1 && pick >= weights[k])
                    pick -= weights[k++];
                var npc = options[k];
                int size = 2;
                foreach (var r in npc.Spawns)
                    if (r.Region == RegionKind.Dungeon)
                        size = r.MinGroup + rng.Next(r.MaxGroup - r.MinGroup + 1);
                groups.Add(new Spawning.Group { Npc = npc, Count = size });
            }
            return groups;
        }
    }
}
