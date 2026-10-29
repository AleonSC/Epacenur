using System;
using System.Collections.Generic;

namespace Epacenur.Core
{
    /// RuneScape 3's basic melee abilities. Each hits for a share of your max hit (min..max multiplier)
    /// and has its own cooldown, and using one starts a short global cooldown for all of them.
    public enum AbilityId
    {
        Slice, Smash, Barge, Havoc, Sever, Punish, Dismember, Backhand, Kick,
    }

    public enum AbilityEffect
    {
        None,
        Stun,     // the target stands still for a few seconds
        Area,     // also hits every other monster next to the target
        Charge,   // usable from up to ChargeRange tiles away: rush to the target, then hit
        Execute,  // triple damage against a target below half health
        Bleed,    // the damage lands as several hits over a few seconds
    }

    public sealed class AbilityDef
    {
        public AbilityId Id;
        public string Name, Description;
        public string Skill;  // skill id
        public int Level;
        public float CooldownSeconds;
        public double MinMultiplier, MaxMultiplier;
        public AbilityEffect Effect;
        public float R, G, B;  // icon colour
    }

    public static class Abilities
    {
        public const float GlobalCooldownSeconds = 1.8f;  // 3 ticks
        public const float StunSeconds = 3f;
        public const int ChargeRange = 10;                // tiles
        public const int BleedHits = 3;
        public const float BleedInterval = 1.2f;

        /// The action bar's 9 slots, in order (keys 1-9).
        public static readonly AbilityDef[] All =
        {
            new AbilityDef { Id = AbilityId.Slice, Name = "Slice", Skill = SkillIds.Attack, Level = 1, CooldownSeconds = 3f,
                MinMultiplier = 0.3, MaxMultiplier = 1.2, R = 0.85f, G = 0.75f, B = 0.3f,
                Description = "A quick cut." },
            new AbilityDef { Id = AbilityId.Smash, Name = "Smash", Skill = SkillIds.Strength, Level = 1, CooldownSeconds = 10f,
                MinMultiplier = 0.4, MaxMultiplier = 1.25, Effect = AbilityEffect.Area, R = 0.75f, G = 0.45f, B = 0.2f,
                Description = "Hits your target and every monster next to it." },
            new AbilityDef { Id = AbilityId.Barge, Name = "Barge", Skill = SkillIds.Attack, Level = 3, CooldownSeconds = 20f,
                MinMultiplier = 0.4, MaxMultiplier = 1.0, Effect = AbilityEffect.Charge, R = 0.6f, G = 0.6f, B = 0.65f,
                Description = "Charge a monster up to 10 tiles away and hit it." },
            new AbilityDef { Id = AbilityId.Havoc, Name = "Havoc", Skill = SkillIds.Attack, Level = 7, CooldownSeconds = 10f,
                MinMultiplier = 0.5, MaxMultiplier = 1.25, R = 0.9f, G = 0.35f, B = 0.2f,
                Description = "A strong strike." },
            new AbilityDef { Id = AbilityId.Sever, Name = "Sever", Skill = SkillIds.Attack, Level = 8, CooldownSeconds = 15f,
                MinMultiplier = 0.4, MaxMultiplier = 1.9, R = 0.75f, G = 0.15f, B = 0.2f,
                Description = "A heavy slash that can hit very hard." },
            new AbilityDef { Id = AbilityId.Punish, Name = "Punish", Skill = SkillIds.Strength, Level = 8, CooldownSeconds = 3f,
                MinMultiplier = 0.2, MaxMultiplier = 1.0, Effect = AbilityEffect.Execute, R = 0.55f, G = 0.2f, B = 0.6f,
                Description = "Triple damage against a target below half health." },
            new AbilityDef { Id = AbilityId.Dismember, Name = "Dismember", Skill = SkillIds.Strength, Level = 14, CooldownSeconds = 15f,
                MinMultiplier = 1.0, MaxMultiplier = 2.0, Effect = AbilityEffect.Bleed, R = 0.6f, G = 0.05f, B = 0.1f,
                Description = "Makes the target bleed: three hits over a few seconds." },
            new AbilityDef { Id = AbilityId.Backhand, Name = "Backhand", Skill = SkillIds.Attack, Level = 15, CooldownSeconds = 15f,
                MinMultiplier = 0.2, MaxMultiplier = 1.0, Effect = AbilityEffect.Stun, R = 0.3f, G = 0.55f, B = 0.85f,
                Description = "A backhanded blow that stuns the target for 3 seconds." },
            new AbilityDef { Id = AbilityId.Kick, Name = "Kick", Skill = SkillIds.Strength, Level = 15, CooldownSeconds = 15f,
                MinMultiplier = 0.2, MaxMultiplier = 1.0, Effect = AbilityEffect.Stun, R = 0.35f, G = 0.75f, B = 0.45f,
                Description = "A kick that stuns the target for 3 seconds." },
        };

        public static AbilityDef Get(AbilityId id) => All[(int)id];

        /// Damage of one use: 0 on a miss; otherwise max hit times a random multiplier, at least 1.
        /// Execute triples it against a target below half health. For Bleed this is the total of all the hits.
        public static int RollDamage(Random rng, AbilityDef def, double hitChance, int maxHit, double targetHealthFraction)
        {
            if (rng.NextDouble() >= hitChance)
                return 0;
            double mult = def.MinMultiplier + rng.NextDouble() * (def.MaxMultiplier - def.MinMultiplier);
            if (def.Effect == AbilityEffect.Execute && targetHealthFraction < 0.5)
                mult *= 3;
            return Math.Max(1, (int)Math.Round(maxHit * mult));
        }

        /// Splits a bleed's total damage into BleedHits hits (earlier hits take any remainder).
        public static int[] SplitBleed(int total)
        {
            var hits = new int[BleedHits];
            for (int i = 0; i < BleedHits; i++)
                hits[i] = total / BleedHits + (i < total % BleedHits ? 1 : 0);
            return hits;
        }
    }

    /// Cooldowns for the ability bar, by game time in seconds (so it can be tested without Unity).
    public sealed class AbilityCooldowns
    {
        readonly Dictionary<AbilityId, float> readyAt = new Dictionary<AbilityId, float>();
        float globalReadyAt;

        public bool IsReady(AbilityId id, float now) => now >= globalReadyAt && now >= ReadyAt(id);

        /// Seconds until it can be used: the longer of its own cooldown and the global one.
        public float Remaining(AbilityId id, float now) => Math.Max(0f, Math.Max(ReadyAt(id), globalReadyAt) - now);

        /// The fraction of its own cooldown still to go (for the dark overlay on the bar), 0..1.
        public float Fraction(AbilityId id, float now)
        {
            var def = Abilities.Get(id);
            float own = Math.Max(0f, ReadyAt(id) - now) / def.CooldownSeconds;
            float global = Math.Max(0f, globalReadyAt - now) / Abilities.GlobalCooldownSeconds;
            return Math.Min(1f, Math.Max(own, global));
        }

        public void Use(AbilityId id, float now)
        {
            readyAt[id] = now + Abilities.Get(id).CooldownSeconds;
            globalReadyAt = now + Abilities.GlobalCooldownSeconds;
        }

        public void Reset()
        {
            readyAt.Clear();
            globalReadyAt = 0f;
        }

        float ReadyAt(AbilityId id) => readyAt.TryGetValue(id, out float t) ? t : 0f;
    }
}
