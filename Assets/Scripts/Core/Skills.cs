using System;
using System.Collections.Generic;
using System.Numerics;

namespace Epacenur.Core
{
    /// Every skill in the game. To add a skill: add it here, then give it a SkillDef in SkillDefs.All.
    public enum SkillId
    {
        Attack,
        Strength,
        Defence,
        Hitpoints,
        Woodcutting,
    }

    public sealed class SkillDef
    {
        public SkillId Id;
        public string Name;
        public float R, G, B;  // colour of the skill's icon and xp drops
        public int StartLevel = 1;  // Hitpoints starts at 10, as in RuneScape
    }

    public static class SkillDefs
    {
        public static readonly SkillDef[] All =
        {
            new SkillDef { Id = SkillId.Attack, Name = "Attack", R = 0.85f, G = 0.55f, B = 0.15f },
            new SkillDef { Id = SkillId.Strength, Name = "Strength", R = 0.2f, G = 0.7f, B = 0.55f },
            new SkillDef { Id = SkillId.Defence, Name = "Defence", R = 0.45f, G = 0.6f, B = 0.95f },
            new SkillDef { Id = SkillId.Hitpoints, Name = "Hitpoints", R = 0.9f, G = 0.25f, B = 0.3f, StartLevel = 10 },
            new SkillDef { Id = SkillId.Woodcutting, Name = "Woodcutting", R = 0.35f, G = 0.75f, B = 0.3f },
        };

        public static SkillDef Get(SkillId id)
        {
            foreach (var def in All)
                if (def.Id == id)
                    return def;
            throw new ArgumentException("No SkillDef for " + id + ": add one to SkillDefs.All.");
        }
    }

    /// A player's experience in every skill.
    /// Experience is kept in tenths so half-xp rewards (like RuneScape's 67.5 for a willow log) add up exactly.
    public sealed class SkillSet
    {
        readonly Dictionary<SkillId, BigInteger> tenths = new Dictionary<SkillId, BigInteger>();
        static readonly BigInteger MaxTenths = Experience.MaxXp * 10;

        /// (skill, xp gained in tenths) after every gain.
        public event Action<SkillId, BigInteger> XpGained;
        /// (skill, new level) when a gain crosses one or more levels.
        public event Action<SkillId, int> LevelledUp;

        public SkillSet()
        {
            foreach (var def in SkillDefs.All)
                tenths[def.Id] = Experience.XpForLevel(def.StartLevel) * 10;
        }

        public BigInteger GetXpTenths(SkillId id) => tenths.TryGetValue(id, out var t) ? t : BigInteger.Zero;
        /// Whole experience points (rounded down), what the level is based on.
        public BigInteger GetXp(SkillId id) => GetXpTenths(id) / 10;
        public int GetLevel(SkillId id) => Experience.LevelForXp(GetXp(id));

        public int TotalLevel
        {
            get
            {
                int total = 0;
                foreach (var def in SkillDefs.All)
                    total += GetLevel(def.Id);
                return total;
            }
        }

        /// Adds experience, e.g. AddXp(SkillId.Woodcutting, 25) or 67.5. Stops at the level-999 cap.
        public void AddXp(SkillId id, double xp) => AddXpTenths(id, new BigInteger(Math.Round(xp * 10.0)));

        public void AddXpTenths(SkillId id, BigInteger amount)
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
        public void SetXpTenths(SkillId id, BigInteger value)
        {
            if (value.Sign < 0)
                value = BigInteger.Zero;
            tenths[id] = BigInteger.Min(value, MaxTenths);
        }

        public void SetLevel(SkillId id, int level) => SetXpTenths(id, Experience.XpForLevel(level) * 10);
    }
}
