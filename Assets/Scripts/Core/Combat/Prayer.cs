using System;
using System.Collections.Generic;

namespace Epacenur.Core
{
    public enum PrayerId
    {
        ThickSkin, BurstOfStrength, ClarityOfThought, RockSkin, SuperhumanStrength, ImprovedReflexes, ProtectFromMelee,
    }

    /// Prayers in the same group replace each other (only one Defence boost at a time, and so on).
    public enum PrayerGroup { Defence, Strength, Attack, Protection }

    public sealed class PrayerDef
    {
        public PrayerId Id;
        public string Name, Description;
        public int Level;
        public int DrainEffect;   // OSRS drain effect: higher drains faster
        public PrayerGroup Group;
        public double Boost;      // 0.05 = +5% to the group's skill
    }

    /// Old School RuneScape's first melee prayers.
    public static class Prayers
    {
        public static readonly PrayerDef[] All =
        {
            new PrayerDef { Id = PrayerId.ThickSkin, Name = "Thick Skin", Level = 1, DrainEffect = 1, Group = PrayerGroup.Defence, Boost = 0.05,
                Description = "Increases your Defence by 5%." },
            new PrayerDef { Id = PrayerId.BurstOfStrength, Name = "Burst of Strength", Level = 4, DrainEffect = 1, Group = PrayerGroup.Strength, Boost = 0.05,
                Description = "Increases your Strength by 5%." },
            new PrayerDef { Id = PrayerId.ClarityOfThought, Name = "Clarity of Thought", Level = 7, DrainEffect = 1, Group = PrayerGroup.Attack, Boost = 0.05,
                Description = "Increases your Attack by 5%." },
            new PrayerDef { Id = PrayerId.RockSkin, Name = "Rock Skin", Level = 10, DrainEffect = 6, Group = PrayerGroup.Defence, Boost = 0.10,
                Description = "Increases your Defence by 10%." },
            new PrayerDef { Id = PrayerId.SuperhumanStrength, Name = "Superhuman Strength", Level = 13, DrainEffect = 6, Group = PrayerGroup.Strength, Boost = 0.10,
                Description = "Increases your Strength by 10%." },
            new PrayerDef { Id = PrayerId.ImprovedReflexes, Name = "Improved Reflexes", Level = 16, DrainEffect = 6, Group = PrayerGroup.Attack, Boost = 0.10,
                Description = "Increases your Attack by 10%." },
            new PrayerDef { Id = PrayerId.ProtectFromMelee, Name = "Protect from Melee", Level = 43, DrainEffect = 12, Group = PrayerGroup.Protection,
                Description = "Protects you from monsters' melee attacks." },
        };

        public static PrayerDef Get(PrayerId id) => All[(int)id];

        /// Burying bones: Prayer xp per item (null if it can't be buried).
        public static double? BuryXp(int itemId) => itemId == ItemDefs.Bones.Id ? 4.5 : (double?)null;
        public const int BuryTicks = 2;
    }

    /// Your prayer points and active prayers. Points max out at your Prayer level and drain each tick
    /// while prayers are on (OSRS: the drain effects add up each tick; every 60 drains a point).
    public sealed class PrayerBook
    {
        public const int DrainResistance = 60;  // 60 + 2 x prayer bonus; there's no prayer bonus gear yet

        readonly HashSet<PrayerId> active = new HashSet<PrayerId>();
        int drainCounter;

        public int Points { get; private set; }
        public int Max { get; private set; }
        public bool IsActive(PrayerId id) => active.Contains(id);
        public bool AnyActive => active.Count > 0;
        public float Fraction => Max > 0 ? (float)Points / Max : 0f;

        public event Action Changed;

        public PrayerBook(int max)
        {
            Max = Points = Math.Max(1, max);
        }

        /// Follows the Prayer level; a level gained adds a point, as in RuneScape.
        public void SetMax(int max)
        {
            max = Math.Max(1, max);
            if (max == Max)
                return;
            if (max > Max)
                Points += max - Max;
            Max = max;
            Points = Math.Min(Points, Max);
            Changed?.Invoke();
        }

        /// Turns a prayer on or off. Turning one on turns off any other in its group.
        /// Returns why it couldn't, or null.
        public string Toggle(PrayerId id, int prayerLevel)
        {
            var def = Prayers.Get(id);
            if (active.Remove(id))
            {
                Changed?.Invoke();
                return null;
            }
            if (prayerLevel < def.Level)
                return "You need a Prayer level of " + def.Level + " to use " + def.Name + ".";
            if (Points <= 0)
                return "You have run out of Prayer points; you can recharge at an altar.";
            active.RemoveWhere(other => Prayers.Get(other).Group == def.Group);
            active.Add(id);
            Changed?.Invoke();
            return null;
        }

        public void DeactivateAll()
        {
            if (active.Count == 0)
                return;
            active.Clear();
            Changed?.Invoke();
        }

        /// One game tick of drain. Returns true if it just ran out (and every prayer switched off).
        public bool Tick()
        {
            if (active.Count == 0)
                return false;
            foreach (var id in active)
                drainCounter += Prayers.Get(id).DrainEffect;
            bool changed = false;
            while (drainCounter >= DrainResistance && Points > 0)
            {
                drainCounter -= DrainResistance;
                Points--;
                changed = true;
            }
            if (changed)
                Changed?.Invoke();
            if (Points > 0)
                return false;
            drainCounter = 0;
            DeactivateAll();
            return true;
        }

        /// Praying at an altar: points back to full.
        public void Recharge()
        {
            Points = Max;
            Changed?.Invoke();
        }

        public void SetPoints(int points)
        {
            Points = Math.Max(0, Math.Min(points, Max));
            Changed?.Invoke();
        }

        double Boost(PrayerGroup group)
        {
            foreach (var id in active)
            {
                var def = Prayers.Get(id);
                if (def.Group == group)
                    return def.Boost;
            }
            return 0.0;
        }

        public double AttackMultiplier => 1.0 + Boost(PrayerGroup.Attack);
        public double StrengthMultiplier => 1.0 + Boost(PrayerGroup.Strength);
        public double DefenceMultiplier => 1.0 + Boost(PrayerGroup.Defence);
        public bool ProtectsFromMelee => IsActive(PrayerId.ProtectFromMelee);
    }
}
