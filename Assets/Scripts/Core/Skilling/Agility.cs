using System;
using System.Collections.Generic;

namespace Epacenur.Core
{
    /// Run energy (0-100%): running uses it up, walking or standing gets it back, faster with a higher Agility level.
    public sealed class RunEnergy
    {
        public const double Max = 100;
        public const double DrainPerSecond = 0.9;  // a full bar lasts about 110 seconds of running

        public double Value { get; private set; } = Max;
        public bool CanRun => Value > 0;

        /// Per second while not running: 0.4% at Agility 1, about 1.2% at 99.
        public static double RestorePerSecond(int agility) => (8 + Math.Min(agility, 99) / 6.0) * 0.05;

        public void Run(double seconds) => Value = Math.Max(0, Value - DrainPerSecond * seconds);
        public void Rest(double seconds, int agility) => Value = Math.Min(Max, Value + RestorePerSecond(agility) * seconds);
        public void Restore(double percent) => Value = Math.Min(Max, Value + percent);
        public void Set(double v) => Value = Math.Max(0, Math.Min(Max, v));
    }

    /// One obstacle's place on a course: where you start, the tiles it covers (you can't walk on them),
    /// and where you land.
    public sealed class ObstaclePlacement
    {
        public int Index;
        public Tile Start, End;
        public readonly List<Tile> Covered = new List<Tile>();
    }

    /// Courses are laid out the same way in every course region: a loop of path round a rectangle,
    /// the five obstacles along it in order (two along the south side heading east, one up the east side,
    /// one back along the north, one down the west side to where you started).
    public static class AgilityCourse
    {
        public const int Obstacles = 5;
        public const int Width = 14, Depth = 10;  // the loop's size in tiles

        /// The obstacles of a course whose loop has its south-west corner at `corner`.
        public static List<ObstaclePlacement> Layout(Tile corner)
        {
            int x = corner.X, z = corner.Z;
            var list = new List<ObstaclePlacement>
            {
                Line(0, x, z, 1, 0, 4),
                Line(1, x + 8, z, 1, 0, 2),
                Line(2, x + Width - 1, z + 1, 0, 1, 4),
                Line(3, x + Width - 2, z + Depth - 1, -1, 0, 3),
                Line(4, x, z + Depth - 2, 0, -1, 4),
            };
            return list;
        }

        static ObstaclePlacement Line(int index, int sx, int sz, int dx, int dz, int length)
        {
            var p = new ObstaclePlacement { Index = index, Start = new Tile(sx, sz), End = new Tile(sx + dx * (length + 1), sz + dz * (length + 1)) };
            for (int i = 1; i <= length; i++)
                p.Covered.Add(new Tile(sx + dx * i, sz + dz * i));
            return p;
        }

        /// The loop of path round the course (the obstacles' covered tiles aren't path).
        public static bool OnLoop(Tile corner, Tile t)
        {
            int lx = t.X - corner.X, lz = t.Z - corner.Z;
            if (lx < 0 || lz < 0 || lx >= Width || lz >= Depth)
                return false;
            return lx == 0 || lz == 0 || lx == Width - 1 || lz == Depth - 1;
        }
    }

    public static class Agility
    {
        /// Chance (0..1) of crossing an obstacle without falling.
        public static double SuccessChance(ObstacleDef o, int level) => Gathering.Chance(o.LowChance, o.HighChance, level);
    }

    /// Which obstacle of a course you should do next: doing all five in order is a lap (with its bonus xp).
    public sealed class LapTracker
    {
        string course;
        int next;

        /// You crossed obstacle `index` of `courseId`. True when that finished a lap.
        public bool Crossed(string courseId, int index)
        {
            if (courseId != course || index != next)
            {
                course = courseId;
                next = index == 0 ? 1 : -1;  // starting part way round doesn't count
                return false;
            }
            next++;
            if (next < AgilityCourse.Obstacles)
                return false;
            next = 0;
            return true;
        }
    }
}
