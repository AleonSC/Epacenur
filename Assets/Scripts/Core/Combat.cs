using System;
using System.Collections.Generic;

namespace Epacenur.Core
{
    /// Which skill your melee hits train, as in RuneScape: 4 xp per damage to it, plus 1.33 Hitpoints xp.
    public enum CombatStyle
    {
        Accurate,    // Attack: +3 effective Attack
        Aggressive,  // Strength: +3 effective Strength
        Defensive,   // Defence: +3 effective Defence
    }

    public sealed class DropEntry
    {
        public int ItemId, Min, Max, Weight;
    }

    /// A monster's possible drops. One entry is rolled by weight each kill.
    public sealed class DropTable
    {
        public readonly List<DropEntry> Entries = new List<DropEntry>();

        public DropTable Add(ItemDef item, int min, int max, int weight)
        {
            Entries.Add(new DropEntry { ItemId = item.Id, Min = min, Max = max, Weight = weight });
            return this;
        }

        /// Returns the dropped item and amount, or false for no drop.
        public bool Roll(Random rng, out int itemId, out int count)
        {
            itemId = count = 0;
            int total = 0;
            foreach (var e in Entries)
                total += e.Weight;
            if (total <= 0)
                return false;
            int pick = rng.Next(total);
            foreach (var e in Entries)
            {
                if (pick < e.Weight)
                {
                    itemId = e.ItemId;
                    count = rng.Next(e.Min, e.Max + 1);
                    return true;
                }
                pick -= e.Weight;
            }
            return false;
        }
    }

    /// A kind of NPC you can fight. To add one: add an NpcDef here and put it in a skill area.
    public sealed class NpcDef
    {
        public string Name;
        public string Examine;
        public int CombatLevel;
        public int Hitpoints;
        public int DefenceLevel;
        public int DefenceBonus;
        public float RespawnSeconds;
        public DropTable Drops;
        public float R, G, B;  // skin colour
    }

    public static class NpcDefs
    {
        /// Doesn't fight back yet. Always drops some coins.
        public static readonly NpcDef Goblin = new NpcDef
        {
            Name = "Goblin", Examine = "An ugly green creature.", CombatLevel = 2, Hitpoints = 5, DefenceLevel = 1, DefenceBonus = -2, RespawnSeconds = 15f,
            Drops = new DropTable()
                .Add(ItemDefs.Coins, 1, 5, 50)
                .Add(ItemDefs.Coins, 6, 15, 35)
                .Add(ItemDefs.Coins, 16, 30, 15),
            R = 0.35f, G = 0.6f, B = 0.25f,
        };
    }

    /// RuneScape's melee formulas (OSRS), simplified: no prayers or potions.
    public static class CombatRules
    {
        public const double XpPerDamage = 4.0, HitpointsXpPerDamage = 4.0 / 3.0;

        /// The largest hit you can do.
        public static int MaxHit(int strengthLevel, int strengthBonus, CombatStyle style)
        {
            int effective = strengthLevel + 8 + (style == CombatStyle.Aggressive ? 3 : 0);
            return (int)Math.Floor(0.5 + effective * (strengthBonus + 64) / 640.0);
        }

        /// Chance (0..1) that an attack hits the target at all.
        public static double HitChance(int attackLevel, int attackBonus, CombatStyle style, int targetDefence, int targetDefenceBonus)
        {
            double attackRoll = (attackLevel + 8 + (style == CombatStyle.Accurate ? 3 : 0)) * (double)(attackBonus + 64);
            double defenceRoll = (targetDefence + 9) * (double)(targetDefenceBonus + 64);
            return attackRoll > defenceRoll
                ? 1.0 - (defenceRoll + 2) / (2 * (attackRoll + 1))
                : attackRoll / (2 * (defenceRoll + 1));
        }

        /// One attack: 0 on a miss, otherwise 0..maxHit (a hit can still roll 0, like RuneScape).
        public static int RollDamage(Random rng, double hitChance, int maxHit)
        {
            if (rng.NextDouble() >= hitChance)
                return 0;
            return rng.Next(0, maxHit + 1);
        }

        public static SkillId TrainedSkill(CombatStyle style) =>
            style == CombatStyle.Accurate ? SkillId.Attack : style == CombatStyle.Aggressive ? SkillId.Strength : SkillId.Defence;

        /// Gives the xp for dealing `damage`.
        public static void AwardXp(SkillSet skills, CombatStyle style, int damage)
        {
            if (damage <= 0)
                return;
            skills.AddXp(TrainedSkill(style), damage * XpPerDamage);
            skills.AddXp(SkillId.Hitpoints, damage * HitpointsXpPerDamage);
        }
    }
}
