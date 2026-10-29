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
        public int AttackLevel, AttackBonus, MaxHit;  // fighting back
        public int AttackTicks = 4;
        public float RespawnSeconds;
        public DropTable Drops;
        public ItemDef[] AlwaysDrops = new ItemDef[0];  // dropped every kill, e.g. bones
        public float R, G, B;  // skin colour
    }

    public static class NpcDefs
    {
        /// Fights back when attacked (max hit 1, as in OSRS). Always drops bones and some coins.
        public static readonly NpcDef Goblin = new NpcDef
        {
            Name = "Goblin", Examine = "An ugly green creature.", CombatLevel = 2, Hitpoints = 5, DefenceLevel = 1, DefenceBonus = -2, RespawnSeconds = 15f,
            AttackLevel = 1, AttackBonus = 0, MaxHit = 1, AttackTicks = 4,
            AlwaysDrops = new[] { ItemDefs.Bones },
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

        /// The largest hit you can do. `prayer` multiplies Strength first (1.05 for Burst of Strength...).
        public static int MaxHit(int strengthLevel, int strengthBonus, CombatStyle style, double prayer = 1.0)
        {
            int effective = (int)Math.Floor(strengthLevel * prayer) + 8 + (style == CombatStyle.Aggressive ? 3 : 0);
            return (int)Math.Floor(0.5 + effective * (strengthBonus + 64) / 640.0);
        }

        /// Chance (0..1) that your attack hits a monster at all. `prayer` multiplies Attack first.
        public static double HitChance(int attackLevel, int attackBonus, CombatStyle style, int targetDefence, int targetDefenceBonus, double prayer = 1.0)
        {
            double attackRoll = ((int)Math.Floor(attackLevel * prayer) + 8 + (style == CombatStyle.Accurate ? 3 : 0)) * (double)(attackBonus + 64);
            double defenceRoll = (targetDefence + 9) * (double)(targetDefenceBonus + 64);
            return Accuracy(attackRoll, defenceRoll);
        }

        /// Chance (0..1) that a monster's attack hits you, against your Defence (times `prayer`),
        /// defence bonus and style (Defensive adds 3).
        public static double MonsterHitChance(NpcDef npc, int defenceLevel, int defenceBonus, CombatStyle style, double prayer = 1.0)
        {
            double attackRoll = (npc.AttackLevel + 9) * (double)(npc.AttackBonus + 64);
            double defenceRoll = ((int)Math.Floor(defenceLevel * prayer) + 8 + (style == CombatStyle.Defensive ? 3 : 0)) * (double)(defenceBonus + 64);
            return Accuracy(attackRoll, defenceRoll);
        }

        static double Accuracy(double attackRoll, double defenceRoll) =>
            attackRoll > defenceRoll
                ? 1.0 - (defenceRoll + 2) / (2 * (attackRoll + 1))
                : attackRoll / (2 * (defenceRoll + 1));

        /// One attack: 0 on a miss, otherwise 0..maxHit (a hit can still roll 0, like RuneScape).
        public static int RollDamage(Random rng, double hitChance, int maxHit)
        {
            if (rng.NextDouble() >= hitChance)
                return 0;
            return rng.Next(0, maxHit + 1);
        }

        /// RuneScape's (OSRS) combat level, melee only (no Ranged or Magic yet): 1/4 of (Defence + Hitpoints
        /// + half of Prayer) plus 0.325 of (Attack + Strength). A new player is level 3.
        public static int CombatLevel(SkillSet s)
        {
            double basePart = 0.25 * (s.GetLevel(SkillId.Defence) + s.GetLevel(SkillId.Hitpoints) + s.GetLevel(SkillId.Prayer) / 2);
            double melee = 0.325 * (s.GetLevel(SkillId.Attack) + s.GetLevel(SkillId.Strength));
            return (int)Math.Floor(basePart + melee);
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
