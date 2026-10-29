using System;
using System.Numerics;
using System.Text;

namespace Epacenur.Core
{
    /// The RuneScape experience curve, carried on past 99 all the way to level 999.
    ///
    /// The experience needed for level L is
    ///     floor( 1/4 * sum over l = 1 .. L-1 of floor(l + 300 * 2^(l/7)) )
    /// so the gap between levels grows by 2^(1/7) (about 10.4%) a level: level 99 is 13,034,431 xp,
    /// level 120 is 104,273,167 and 200m sits between 126 and 127, exactly as in RuneScape.
    /// There is no 200m cap: the cap is the experience for level 999, about 6.59 x 10^45,
    /// far beyond a 64-bit integer, so experience is a BigInteger.
    public static class Experience
    {
        public const int MaxLevel = 999;

        // table[level] = total experience needed for that level (index 0 unused).
        static readonly BigInteger[] table = BuildTable();

        /// Experience at which a skill is maxed (level 999). Experience never goes above it.
        public static BigInteger MaxXp => table[MaxLevel];

        static BigInteger[] BuildTable()
        {
            var t = new BigInteger[MaxLevel + 1];
            BigInteger points = BigInteger.Zero;
            for (int level = 1; level < MaxLevel; level++)
            {
                points += new BigInteger(Math.Floor(level + 300.0 * Math.Pow(2.0, level / 7.0)));
                t[level + 1] = points / 4;
            }
            return t;
        }

        /// Total experience needed to reach a level (1..999).
        public static BigInteger XpForLevel(int level)
        {
            if (level <= 1)
                return BigInteger.Zero;
            return table[Math.Min(level, MaxLevel)];
        }

        /// The level a total experience amount gives (1..999).
        public static int LevelForXp(BigInteger xp)
        {
            int lo = 1, hi = MaxLevel;  // highest level whose requirement is <= xp
            while (lo < hi)
            {
                int mid = (lo + hi + 1) / 2;
                if (table[mid] <= xp)
                    lo = mid;
                else
                    hi = mid - 1;
            }
            return lo;
        }

        /// How far through the current level the experience is, 0..1 (1 at level 999).
        public static double LevelProgress(BigInteger xp)
        {
            int level = LevelForXp(xp);
            if (level >= MaxLevel)
                return 1.0;
            BigInteger start = table[level], end = table[level + 1];
            return (double)(xp - start) / (double)(end - start);
        }

        /// Experience left until the next level (0 at level 999).
        public static BigInteger XpToNextLevel(BigInteger xp)
        {
            int level = LevelForXp(xp);
            return level >= MaxLevel ? BigInteger.Zero : table[level + 1] - xp;
        }

        /// 13034431 -> "13,034,431".
        public static string Format(BigInteger xp)
        {
            string digits = BigInteger.Abs(xp).ToString();
            var sb = new StringBuilder(digits.Length + digits.Length / 3 + 1);
            if (xp.Sign < 0)
                sb.Append('-');
            for (int i = 0; i < digits.Length; i++)
            {
                if (i > 0 && (digits.Length - i) % 3 == 0)
                    sb.Append(',');
                sb.Append(digits[i]);
            }
            return sb.ToString();
        }

        /// Short form for tight spaces: "950", "13.0M", "104B", "6.59e45".
        public static string FormatShort(BigInteger xp)
        {
            if (xp < 100000)
                return Format(xp);
            double d = (double)xp;
            if (d < 1e7) return (d / 1e3).ToString("0.#") + "K";
            if (d < 1e10) return (d / 1e6).ToString("0.#") + "M";
            if (d < 1e13) return (d / 1e9).ToString("0.#") + "B";
            if (d < 1e16) return (d / 1e12).ToString("0.#") + "T";
            return d.ToString("0.00e0");
        }
    }
}
