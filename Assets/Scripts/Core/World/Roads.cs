using System;
using System.Collections.Generic;

namespace Epacenur.Core
{
    /// A signpost where a road leaves a town: which way the road goes (DX, DZ) and the town it leads to.
    public sealed class Signpost
    {
        public Tile At;
        public int DX, DZ;
        public Settlement To;
    }

    /// Ruins and landmarks out in the wild.
    public enum LandmarkKind { None, Watchtower, Cottage, Statue, Obelisk, Cairn }

    /// Roads between towns, and the landmarks out in the wilds.
    public sealed partial class Overworld
    {
        /// A dirt road between two neighbouring towns, from the end of one's road out of town to the end of the
        /// other's, winding gently.
        sealed class Road
        {
            public Settlement From, To;
            public double Ax, Az, Bx, Bz, Ux, Uz, Length;
            public double Amp, P1, P2;
            public double MinX, MaxX, MinZ, MaxZ;  // its bounding box, with room for the bends
        }

        public const double RoadHalfWidth = 0.8;  // a band this far either side of the road's line: 2 tiles wide, never broken
        readonly Dictionary<long, Road> roads = new Dictionary<long, Road>();
        readonly Dictionary<long, byte[]> roadTiles = new Dictionary<long, byte[]>();

        /// The road from the settlement in cell (cx, cz) to the one east of it (east) or north of it, or null if
        /// either cell has none.
        Road RoadFrom(int cx, int cz, bool east)
        {
            long key = ChunkedGrid.Key(cx * 2 + (east ? 1 : 0), cz);
            if (roads.TryGetValue(key, out var road))
                return road;
            var a = SettlementInCell(cx, cz);
            var b = east ? SettlementInCell(cx + 1, cz) : SettlementInCell(cx, cz + 1);
            if (a != null && b != null)
            {
                road = new Road { From = a, To = b };
                // From just past the end of one town's road out (along the middle of its region) to just short of the other's.
                if (east)
                {
                    road.Ax = (a.RX + 1) * RegionSize; road.Az = a.Center.Z;
                    road.Bx = b.RX * RegionSize - 1; road.Bz = b.Center.Z;
                }
                else
                {
                    road.Ax = a.Center.X; road.Az = (a.RZ + 1) * RegionSize;
                    road.Bx = b.Center.X; road.Bz = b.RZ * RegionSize - 1;
                }
                double dx = road.Bx - road.Ax, dz = road.Bz - road.Az;
                road.Length = Math.Sqrt(dx * dx + dz * dz);
                road.Ux = dx / road.Length;
                road.Uz = dz / road.Length;
                road.Amp = road.Length * (0.04 + Hash(cx, cz, east ? 74 : 75) * 0.06);
                road.P1 = Hash(cx, cz, east ? 76 : 77) * Math.PI * 2;
                road.P2 = Hash(cx, cz, east ? 78 : 79) * Math.PI * 2;
                double pad = road.Amp + 4;
                road.MinX = Math.Min(road.Ax, road.Bx) - pad; road.MaxX = Math.Max(road.Ax, road.Bx) + pad;
                road.MinZ = Math.Min(road.Az, road.Bz) - pad; road.MaxZ = Math.Max(road.Az, road.Bz) + pad;
            }
            if (roads.Count > 4096)
                roads.Clear();
            roads[key] = road;
            return road;
        }

        /// How far a road's line bends aside at `s` (0..1 along it): not at all at its ends, gently between.
        static double Bend(Road r, double s) => Math.Sin(Math.PI * s) * r.Amp * (0.7 * Math.Sin(2 * Math.PI * s + r.P1) + 0.3 * Math.Sin(4.6 * Math.PI * s + r.P2));

        /// How far a point is from the middle of a road (in tiles, roughly; very far with no road near).
        public double RoadDistance(double x, double z)
        {
            int cx = CellOf(RegionOf((int)Math.Floor(x))), cz = CellOf(RegionOf((int)Math.Floor(z)));
            double best = double.MaxValue;
            for (int a = cx - 1; a <= cx + 1; a++)
                for (int b = cz - 1; b <= cz + 1; b++)
                {
                    best = Math.Min(best, DistanceTo(RoadFrom(a, b, true), x, z));
                    best = Math.Min(best, DistanceTo(RoadFrom(a, b, false), x, z));
                }
            return best;
        }

        static double DistanceTo(Road r, double x, double z)
        {
            if (r == null || x < r.MinX || x > r.MaxX || z < r.MinZ || z > r.MaxZ)
                return double.MaxValue;
            double px = x - r.Ax, pz = z - r.Az;
            double along = px * r.Ux + pz * r.Uz, aside = -px * r.Uz + pz * r.Ux;
            double s = along / r.Length;
            if (s < -0.002 || s > 1.002)
            {
                // Past an end: how far from it.
                double ex = s < 0 ? r.Ax : r.Bx, ez = s < 0 ? r.Az : r.Bz;
                return Math.Sqrt((x - ex) * (x - ex) + (z - ez) * (z - ez));
            }
            s = Math.Max(0, Math.Min(1, s));
            // Across the bent line, measured square to it.
            double slope = (Bend(r, Math.Min(1, s + 0.001)) - Bend(r, Math.Max(0, s - 0.001))) / (0.002 * r.Length);
            return Math.Abs(aside - Bend(r, s)) / Math.Sqrt(1 + slope * slope);
        }

        /// Whether a tile is on a road between towns (not in a town, where its own roads run, nor on anything
        /// planned: it runs on over a shrine's paving or a camp's trampled ground). Worked out once and remembered by chunk.
        public bool IsRoad(Tile t)
        {
            int cx = FloorDiv(t.X, ChunkSize), cz = FloorDiv(t.Z, ChunkSize);
            long key = ChunkedGrid.Key(cx, cz);
            if (!roadTiles.TryGetValue(key, out var tiles))
            {
                if (roadTiles.Count > 4096)
                    roadTiles.Clear();
                tiles = new byte[ChunkSize * ChunkSize];
                for (int i = 0; i < tiles.Length; i++)
                    tiles[i] = Unknown;
                roadTiles[key] = tiles;
            }
            int k = (t.X - cx * ChunkSize) * ChunkSize + (t.Z - cz * ChunkSize);
            if (tiles[k] == Unknown)
            {
                var plan = PlanAt(t);
                bool road = false;
                if (plan.Kind != RegionKind.Town && plan.At(t) == Terrain.Grass && !plan.Blocked.Contains(t) && !NearLandmark(plan, t))
                {
                    double d = RoadDistance(t.X, t.Z);
                    road = d < RoadHalfWidth || (d < RoadHalfWidth + 3 && BlockedOnRoad(plan, t));
                }
                tiles[k] = road ? (byte)1 : (byte)0;
            }
            return tiles[k] == 1;
        }

        /// Whether something blocks the road near a tile (a cave mouth, a standing stone...): the road widens to go
        /// round it.
        bool BlockedOnRoad(RegionPlan plan, Tile t)
        {
            foreach (var b in plan.Blocked)
                if (Math.Abs(b.X - t.X) <= 4 && Math.Abs(b.Z - t.Z) <= 4 && RoadDistance(b.X, b.Z) < RoadHalfWidth + 1)
                    return true;
            for (int dx = -4; dx <= 4; dx++)
                for (int dz = -4; dz <= 4; dz++)
                {
                    var n = new Tile(t.X + dx, t.Z + dz);
                    if (PlanAt(n) == plan && !TownLayout.IsWalkable(plan.At(n)) && RoadDistance(n.X, n.Z) < RoadHalfWidth + 1)
                        return true;
                }
            return false;
        }

        /// The signposts where a town's roads out lead to its neighbours: just outside the edge of town, beside the road.
        void PlanSignposts(RegionPlan plan)
        {
            int cx = CellOf(plan.RX), cz = CellOf(plan.RZ);
            var b = plan.Bounds;
            var c = plan.Center;
            // East, west, north, south: the road (if a town lies that way), and two spots beside it to try.
            var ways = new[]
            {
                new { Road = RoadFrom(cx, cz, true), Out = true, DX = 1, DZ = 0, A = new Tile(b.XMax - 2, c.Z + 1), B = new Tile(b.XMax - 2, c.Z - 1) },
                new { Road = RoadFrom(cx - 1, cz, true), Out = false, DX = -1, DZ = 0, A = new Tile(b.X + 1, c.Z - 1), B = new Tile(b.X + 1, c.Z + 1) },
                new { Road = RoadFrom(cx, cz, false), Out = true, DX = 0, DZ = 1, A = new Tile(c.X - 1, b.ZMax - 2), B = new Tile(c.X + 1, b.ZMax - 2) },
                new { Road = RoadFrom(cx, cz - 1, false), Out = false, DX = 0, DZ = -1, A = new Tile(c.X + 1, b.Z + 1), B = new Tile(c.X - 1, b.Z + 1) },
            };
            foreach (var w in ways)
            {
                if (w.Road == null)
                    continue;
                foreach (var spot in new[] { w.A, w.B })
                {
                    var local = new Tile(spot.X - b.X, spot.Z - b.Z);
                    if (plan.Town.Map[local.X, local.Z] != Terrain.Grass || plan.Blocked.Contains(spot))
                        continue;
                    plan.Signposts.Add(new Signpost { At = spot, DX = w.DX, DZ = w.DZ, To = w.Out ? w.Road.To : w.Road.From });
                    plan.Blocked.Add(spot);
                    break;
                }
            }
        }

        // ---- Town walls ----

        /// Walls round a town, a few tiles out from everything built, with a gate wherever a road goes through:
        /// stone round a city, a wooden palisade round some towns, none round the rest.
        void PlanWalls(RegionPlan plan)
        {
            if (!plan.City && Hash(plan.RX, plan.RZ, 17) >= 0.5)
                return;
            var map = plan.Town.Map;
            int x0 = int.MaxValue, z0 = int.MaxValue, x1 = int.MinValue, z1 = int.MinValue;
            for (int x = 0; x < RegionSize; x++)
                for (int z = 0; z < RegionSize; z++)
                {
                    var t = map[x, z];
                    if (t == Terrain.Grass || t == Terrain.Path)
                        continue;
                    x0 = Math.Min(x0, x); x1 = Math.Max(x1, x); z0 = Math.Min(z0, z); z1 = Math.Max(z1, z);
                }
            var extras = new List<Tile>(plan.Town.Lamps);
            if (plan.Town.HasWell)
                extras.Add(plan.Town.Well);
            foreach (var l in extras)
            {
                x0 = Math.Min(x0, l.X); x1 = Math.Max(x1, l.X); z0 = Math.Min(z0, l.Z); z1 = Math.Max(z1, l.Z);
            }
            if (x0 > x1)
                return;
            x0 = Math.Max(2, x0 - 3); z0 = Math.Max(2, z0 - 3);
            x1 = Math.Min(RegionSize - 3, x1 + 3); z1 = Math.Min(RegionSize - 3, z1 + 3);
            plan.StoneWalls = plan.City;
            plan.Walls = new Area(plan.Bounds.X + x0, plan.Bounds.Z + z0, x1 - x0 + 1, z1 - z0 + 1);
            for (int x = x0; x <= x1; x++)
                for (int z = z0; z <= z1; z++)
                {
                    if (x != x0 && x != x1 && z != z0 && z != z1)
                        continue;
                    if (map[x, z] == Terrain.Path)
                        plan.Gates.Add(new Tile(plan.Bounds.X + x, plan.Bounds.Z + z));
                    else if (map[x, z] == Terrain.Grass)
                        map[x, z] = Terrain.TownWall;
                }
        }

        // ---- Landmarks ----

        /// Now and then, out in the wilds, a ruin or a landmark: an old watchtower, a ruined cottage, a statue, an
        /// obelisk or a cairn. Never by a river, in a lake or on a road.
        void PlanLandmark(RegionPlan plan)
        {
            if (Hash(plan.RX, plan.RZ, 18) >= 0.3)
                return;
            var kinds = new[] { LandmarkKind.Watchtower, LandmarkKind.Cottage, LandmarkKind.Statue, LandmarkKind.Obelisk, LandmarkKind.Cairn };
            var kind = kinds[(int)(Hash(plan.RX, plan.RZ, 19) * kinds.Length)];
            int dx = (int)(Hash(plan.RX, plan.RZ, 20) * 24) - 12, dz = (int)(Hash(plan.RX, plan.RZ, 21) * 24) - 12;
            int w = kind == LandmarkKind.Watchtower ? 3 : kind == LandmarkKind.Cottage ? 6 : 1;
            int h = kind == LandmarkKind.Watchtower ? 3 : kind == LandmarkKind.Cottage ? 5 : 1;
            var area = new Area(plan.Center.X + dx - w / 2, plan.Center.Z + dz - h / 2, w, h);
            var mid = area.Center;
            if (RiverNear(mid) || RoadDistance(mid.X, mid.Z) < 10 || PondNoise(mid.X, mid.Z) > PondLevel(mid.X, mid.Z) - 0.15)
                return;
            plan.Landmark = kind;
            plan.LandmarkArea = area;
            for (int x = area.X; x < area.XMax; x++)
                for (int z = area.Z; z < area.ZMax; z++)
                {
                    var t = new Tile(x, z);
                    // A ruined cottage: broken walls round the edge, with a gap for its door and two more where they've fallen.
                    if (kind == LandmarkKind.Cottage)
                    {
                        bool edge = x == area.X || z == area.Z || x == area.XMax - 1 || z == area.ZMax - 1;
                        bool gap = (z == area.Z && x == area.X + 2) || (x == area.XMax - 1 && z == area.Z + 2) || (z == area.ZMax - 1 && x == area.X + 3);
                        if (!edge || gap)
                            continue;
                    }
                    plan.Blocked.Add(t);
                }
        }

        /// Whether a tile is kept clear round a landmark (no trees or water against it).
        internal static bool NearLandmark(RegionPlan plan, Tile t) => plan.Landmark != LandmarkKind.None
            && t.X >= plan.LandmarkArea.X - 2 && t.X < plan.LandmarkArea.XMax + 2 && t.Z >= plan.LandmarkArea.Z - 2 && t.Z < plan.LandmarkArea.ZMax + 2;
    }
}
