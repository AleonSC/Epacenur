using System;
using System.Collections.Generic;
using System.Numerics;

namespace Epacenur.Core
{
    /// Ids of the skills the core rules use (combat). Every other skill's id lives in its content module.
    public static class SkillIds
    {
        public const string Attack = "attack", Strength = "strength", Defence = "defence", Hitpoints = "hitpoints", Prayer = "prayer";
    }

    /// A player's experience in every skill (the skills in ContentDb.Current), by skill id.
    /// Experience is kept in tenths so half-xp rewards (like RuneScape's 67.5 for a willow log) add up exactly.
    public sealed class SkillSet
    {
        readonly Dictionary<string, BigInteger> tenths = new Dictionary<string, BigInteger>();
        static readonly BigInteger MaxTenths = Experience.MaxXp * 10;

        /// (skill id, xp gained in tenths) after every gain.
        public event Action<string, BigInteger> XpGained;
        /// (skill id, new level) when a gain crosses one or more levels.
        public event Action<string, int> LevelledUp;

        public SkillSet()
        {
            foreach (var def in ContentDb.Current.Skills)
                tenths[def.Id] = Experience.XpForLevel(def.StartLevel) * 10;
        }

        public BigInteger GetXpTenths(string id) => tenths.TryGetValue(id, out var t) ? t : BigInteger.Zero;
        /// Whole experience points (rounded down), what the level is based on.
        public BigInteger GetXp(string id) => GetXpTenths(id) / 10;
        public int GetLevel(string id) => Experience.LevelForXp(GetXp(id));

        public int TotalLevel
        {
            get
            {
                int total = 0;
                foreach (var def in ContentDb.Current.Skills)
                    total += GetLevel(def.Id);
                return total;
            }
        }

        /// Adds experience, e.g. AddXp("woodcutting", 25) or 67.5. Stops at the level-999 cap.
        public void AddXp(string id, double xp) => AddXpTenths(id, new BigInteger(Math.Round(xp * 10.0)));

        public void AddXpTenths(string id, BigInteger amount)
        {
            if (amount.Sign <= 0)
                return;
            BigInteger before = GetXpTenths(id);
            BigInteger after = BigInteger.Min(before + amount, MaxTenths);
            if (after == before)
                return;
            int oldLevel = Experience.LevelForXp(before / 10);
            tenths[id] = after;
            XpGained?.Invoke(id, after - before);
            int newLevel = Experience.LevelForXp(after / 10);
            if (newLevel > oldLevel)
                LevelledUp?.Invoke(id, newLevel);
        }

        /// Sets experience directly (dev tools, loading a save). No events.
        public void SetXpTenths(string id, BigInteger value)
        {
            if (value.Sign < 0)
                value = BigInteger.Zero;
            tenths[id] = BigInteger.Min(value, MaxTenths);
        }

        public void SetLevel(string id, int level) => SetXpTenths(id, Experience.XpForLevel(level) * 10);
    }
}
