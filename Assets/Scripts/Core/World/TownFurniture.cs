using System;
using System.Collections.Generic;

namespace Epacenur.Core
{
    /// The things that furnish a town building (the 3D side draws them).
    public enum Furniture
    {
        Bed, Chest, Cupboard, Shelves, Barrel, Crates, Sacks, Hearth,   // houses, shops, workshops
        Bookcase, Strongbox, Desk,                                      // the bank
        Pew, Candles,                                                   // the chapel
        Trough, CoalHeap, WeaponRack, Grindstone,                       // the smithy
        WoolBaskets, HideRack,                                          // the workshop
    }

    /// One piece of furniture: at `At` (and, two tiles long, at `At` + `Along` too), its front facing `Facing`
    /// (a step, into the room). Its tiles are blocked.
    public sealed class FurniturePiece
    {
        public Furniture Kind;
        public Tile At, Facing, Along;
        public int Length = 1;

        public IEnumerable<Tile> Tiles
        {
            get
            {
                yield return At;
                if (Length > 1)
                    yield return new Tile(At.X + Along.X, At.Z + Along.Z);
            }
        }

        /// The middle of it (map space, tile units).
        public float CentreX => At.X + (Length - 1) * Along.X / 2f;
        public float CentreZ => At.Z + (Length - 1) * Along.Z / 2f;

        public FurniturePiece Moved(int dx, int dz) => new FurniturePiece
        {
            Kind = Kind, At = new Tile(At.X + dx, At.Z + dz), Facing = Facing, Along = Along, Length = Length,
        };
    }

    /// Furnishes town buildings: what each kind holds, mostly backed against its walls (tall things not in front
    /// of windows), a chapel's pews in rows either side of its aisle. The way from the door to what's at the
    /// back stays clear, the front of each station too, and no floor is ever closed off.
    public static class Furnisher
    {
        /// Whether wall tile `t` (on the building's edge, not a corner) has a window: every other tile along its
        /// wall, counted out from the door on the door's wall, never beside the door.
        public static bool HasWindow(Building b, Tile t)
        {
            var a = b.Footprint;
            bool alongX = t.Z == a.Z || t.Z == a.ZMax - 1;
            int along = alongX ? t.X : t.Z, from = alongX ? a.X : a.Z, to = alongX ? a.XMax - 1 : a.ZMax - 1;
            if (along <= from || along >= to)
                return false;
            bool doorWall = alongX ? b.Door.Z == t.Z && (b.Door.Z == a.Z || b.Door.Z == a.ZMax - 1)
                : b.Door.X == t.X && (b.Door.X == a.X || b.Door.X == a.XMax - 1);
            if (doorWall)
            {
                int door = alongX ? b.Door.X : b.Door.Z;
                return Math.Abs(along - door) > 1 && (along - door) % 2 == 0;
            }
            return (along - from) % 2 == 0;
        }

        /// The step from the door into the room.
        public static Tile Inward(Building b)
        {
            var a = b.Footprint;
            if (b.Door.X == a.X) return new Tile(1, 0);
            if (b.Door.X == a.XMax - 1) return new Tile(-1, 0);
            if (b.Door.Z == a.Z) return new Tile(0, 1);
            return new Tile(0, -1);
        }

        static Furniture[] Wanted(BuildingKind kind, Random rng)
        {
            switch (kind)
            {
                case BuildingKind.Bank: return new[] { Furniture.Strongbox, Furniture.Bookcase, Furniture.Desk, Furniture.Strongbox, Furniture.Candles };
                case BuildingKind.Shop: return new[] { Furniture.Shelves, Furniture.Barrel, Furniture.Shelves, Furniture.Crates, Furniture.Sacks };
                case BuildingKind.Chapel: return new[] { Furniture.Candles, Furniture.Candles };
                case BuildingKind.Smithy: return new[] { Furniture.Trough, Furniture.CoalHeap, Furniture.WeaponRack, Furniture.Grindstone };
                case BuildingKind.Workshop: return new[] { Furniture.WoolBaskets, Furniture.HideRack, Furniture.Shelves, Furniture.Crates };
                default:
                    var list = new List<Furniture> { Furniture.Bed };
                    if (rng.Next(10) < 7)
                        list.Add(Furniture.Hearth);
                    list.Add(rng.Next(2) == 0 ? Furniture.Cupboard : Furniture.Shelves);
                    list.Add(Furniture.Chest);
                    list.Add(new[] { Furniture.Barrel, Furniture.Sacks, Furniture.Crates }[rng.Next(3)]);
                    return list.ToArray();
            }
        }

        static bool Tall(Furniture f) => f == Furniture.Cupboard || f == Furniture.Shelves || f == Furniture.Bookcase || f == Furniture.Hearth
            || f == Furniture.WeaponRack || f == Furniture.HideRack;

        static int LengthOf(Furniture f) => f == Furniture.Bed ? 2 : 1;

        /// Lays out `b`'s furniture (the same for the same building and seed).
        public static List<FurniturePiece> Furnish(Building b, int seed)
        {
            var a = b.Footprint;
            var rng = new Random(seed ^ a.X * 73856093 ^ a.Z * 19349663 ^ (int)b.Kind * 83492791);
            var placed = new List<FurniturePiece>();
            var taken = new HashSet<Tile> { b.Feature };
            if (b.HasFeature2)
                taken.Add(b.Feature2);

            // Kept clear: inside the door, the way to the back, and the front of each station.
            var inward = Inward(b);
            var keep = new HashSet<Tile>();
            for (var t = new Tile(b.Door.X + inward.X, b.Door.Z + inward.Z); Inside(a, t) && t != b.Feature; t = new Tile(t.X + inward.X, t.Z + inward.Z))
                keep.Add(t);
            if (b.HasFeature2)
                keep.Add(new Tile(b.Feature2.X + inward.X, b.Feature2.Z + inward.Z));
            var doorIn = new Tile(b.Door.X + inward.X, b.Door.Z + inward.Z);
            foreach (var n in Neighbours(doorIn))
                if (Inside(a, n))
                    keep.Add(n);  // room to step in

            if (b.Kind == BuildingKind.Chapel)
                Pews(b, inward, placed, taken, keep, doorIn);

            // Against the walls: every inside tile next to a wall, facing away from it.
            var spots = new List<FurniturePiece>();
            for (int x = a.X + 1; x < a.XMax - 1; x++)
                for (int z = a.Z + 1; z < a.ZMax - 1; z++)
                {
                    var t = new Tile(x, z);
                    foreach (var face in new[] { new Tile(1, 0), new Tile(-1, 0), new Tile(0, 1), new Tile(0, -1) })
                    {
                        var wall = new Tile(x - face.X, z - face.Z);
                        if (Inside(a, wall))
                            continue;
                        spots.Add(new FurniturePiece { At = t, Facing = face, Along = new Tile(face.Z, -face.X) });
                        spots.Add(new FurniturePiece { At = t, Facing = face, Along = new Tile(-face.Z, face.X) });
                    }
                }
            foreach (var want in Wanted(b.Kind, rng))
            {
                // The chapel's candles stand either side of the altar; everything else, anywhere it fits.
                var order = new List<FurniturePiece>(spots);
                Shuffle(order, rng);
                if (want == Furniture.Candles && b.Kind == BuildingKind.Chapel)
                    order.Sort((p, q) => Near(p.At, b.Feature).CompareTo(Near(q.At, b.Feature)));
                foreach (var spot in order)
                {
                    var f = new FurniturePiece { Kind = want, At = spot.At, Facing = spot.Facing, Along = spot.Along, Length = LengthOf(want) };
                    if (Fits(b, f, taken, keep, doorIn))
                    {
                        placed.Add(f);
                        foreach (var t in f.Tiles)
                            taken.Add(t);
                        break;
                    }
                }
            }
            return placed;
        }

        static int Near(Tile t, Tile to) => Math.Abs(t.X - to.X) + Math.Abs(t.Z - to.Z);

        /// Rows of pews across the chapel, either side of its aisle, every other row, facing the altar.
        static void Pews(Building b, Tile inward, List<FurniturePiece> placed, HashSet<Tile> taken, HashSet<Tile> keep, Tile doorIn)
        {
            var a = b.Footprint;
            var across = new Tile(inward.Z, inward.X);  // along a row
            var back = new Tile(-inward.X, -inward.Z);    // pews face the altar, away from the door
            for (int row = 2; ; row += 2)
            {
                var aisle = new Tile(doorIn.X + inward.X * row, doorIn.Z + inward.Z * row);
                // Stop short of the altar, leaving a row free in front of it.
                if (!Inside(a, aisle) || Near(aisle, b.Feature) < 2)
                    break;
                foreach (int side in new[] { 1, -1 })
                {
                    var first = new Tile(aisle.X + across.X * side, aisle.Z + across.Z * side);
                    var step = new Tile(across.X * side, across.Z * side);
                    var two = new FurniturePiece { Kind = Furniture.Pew, At = first, Facing = back, Along = step, Length = 2 };
                    var one = new FurniturePiece { Kind = Furniture.Pew, At = first, Facing = back, Along = step, Length = 1 };
                    foreach (var f in new[] { two, one })
                        if (Fits(b, f, taken, keep, doorIn))
                        {
                            placed.Add(f);
                            foreach (var t in f.Tiles)
                                taken.Add(t);
                            break;
                        }
                }
            }
        }

        static bool Fits(Building b, FurniturePiece f, HashSet<Tile> taken, HashSet<Tile> keep, Tile doorIn)
        {
            foreach (var t in f.Tiles)
                if (!Inside(b.Footprint, t) || taken.Contains(t) || keep.Contains(t))
                    return false;
            if (Tall(f.Kind))
                foreach (var t in f.Tiles)
                    if (HasWindow(b, new Tile(t.X - f.Facing.X, t.Z - f.Facing.Z)))
                        return false;
            // A pew stands out in the room; the rest have their backs to a wall (both tiles).
            if (f.Kind != Furniture.Pew)
                foreach (var t in f.Tiles)
                    if (Inside(b.Footprint, new Tile(t.X - f.Facing.X, t.Z - f.Facing.Z)))
                        return false;
            // Nothing may close off any of the floor.
            var blocked = new HashSet<Tile>(taken);
            foreach (var t in f.Tiles)
                blocked.Add(t);
            return AllReachable(b.Footprint, blocked, doorIn);
        }

        static bool AllReachable(Area a, HashSet<Tile> blocked, Tile from)
        {
            var seen = new HashSet<Tile> { from };
            var queue = new Queue<Tile>();
            queue.Enqueue(from);
            while (queue.Count > 0)
                foreach (var n in Neighbours(queue.Dequeue()))
                    if (Inside(a, n) && !blocked.Contains(n) && seen.Add(n))
                        queue.Enqueue(n);
            for (int x = a.X + 1; x < a.XMax - 1; x++)
                for (int z = a.Z + 1; z < a.ZMax - 1; z++)
                {
                    var t = new Tile(x, z);
                    if (!blocked.Contains(t) && !seen.Contains(t))
                        return false;
                }
            return true;
        }

        static bool Inside(Area a, Tile t) => t.X > a.X && t.X < a.XMax - 1 && t.Z > a.Z && t.Z < a.ZMax - 1;

        static Tile[] Neighbours(Tile t) => new[] { new Tile(t.X + 1, t.Z), new Tile(t.X - 1, t.Z), new Tile(t.X, t.Z + 1), new Tile(t.X, t.Z - 1) };

        static void Shuffle<T>(List<T> list, Random rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                var x = list[i];
                list[i] = list[j];
                list[j] = x;
            }
        }
    }
}
