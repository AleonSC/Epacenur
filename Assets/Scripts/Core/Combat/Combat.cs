using System;
using System.Collections.Generic;

namespace Epacenur.Core
{
    /// Which skill your melee hits train, as in RuneScape: 4 xp per damage to it, plus 1.33 Hitpoints xp.
    public enum CombatStyle
    {
        Accurate,    // Attack: +3 effective Attack (ranged: Ranged, +3 effective Ranged)
        Aggressive,  // Strength: +3 effective Strength
        Defensive,   // Defence: +3 effective Defence
        Rapid,       // ranged: Ranged, a tick faster
        Longrange,   // ranged: Ranged and Defence, 2 tiles further
    }

    /// RuneScape's melee formulas (OSRS). Accuracy compares your Attack and your attack bonus for the
    /// style's attack type (stab, slash or crush) with the target's Defence and its defence against that type.
    public static class CombatRules
    {
        public const double XpPerDamage = 4.0, HitpointsXpPerDamage = 4.0 / 3.0;

        /// The largest hit you can do. `prayer` multiplies Strength first (1.05 for Burst of Strength...).
        public static int MaxHit(int strengthLevel, int strengthBonus, CombatStyle style, double prayer = 1.0)
        {
            int effective = (int)Math.Floor(strengthLevel * prayer) + 8 + (style == CombatStyle.Aggressive ? 3 : 0);
            return (int)Math.Floor(0.5 + effective * (strengthBonus + 64) / 640.0);
        }

        /// The largest hit with a bow: from Ranged and the arrows' ranged strength (Accurate adds 3, like OSRS).
        public static int RangedMaxHit(int rangedLevel, int rangedStrength, CombatStyle style, double prayer = 1.0)
        {
            int effective = (int)Math.Floor(rangedLevel * prayer) + 8 + (style == CombatStyle.Accurate ? 3 : 0);
            return (int)Math.Floor(0.5 + effective * (rangedStrength + 64) / 640.0);
        }

        /// Chance (0..1) that a spell hits: Magic and magic attack against the monster's Defence and magic defence.
        public static double MagicHitChance(int magicLevel, int magicAttack, int targetDefence, int targetMagicDefence)
        {
            double attackRoll = (magicLevel + 8) * (double)(magicAttack + 64);
            double defenceRoll = (targetDefence + 9) * (double)(targetMagicDefence + 64);
            return Accuracy(attackRoll, defenceRoll);
        }

        /// A combat spell's xp: its own xp even when it splashes, 2 Magic xp per damage, and 1.33 Hitpoints.
        public static void AwardMagicXp(SkillSet skills, SpellDef spell, int damage)
        {
            skills.AddXp(SkillIds.Magic, spell.Xp + 2.0 * damage);
            if (damage > 0)
                skills.AddXp(SkillIds.Hitpoints, damage * HitpointsXpPerDamage);
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
            double defenceRoll = ((int)Math.Floor(defenceLevel * prayer) + 8 + (style == CombatStyle.Defensive || style == CombatStyle.Longrange ? 3 : 0)) * (double)(defenceBonus + 64);
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

        /// RuneScape's (OSRS) combat level: 1/4 of (Defence + Hitpoints + half of Prayer), plus the better of
        /// melee (0.325 of Attack + Strength), ranged and magic (0.325 of 1.5 x the level). A new player is level 3.
        public static int CombatLevel(SkillSet s)
        {
            double basePart = 0.25 * (s.GetLevel(SkillIds.Defence) + s.GetLevel(SkillIds.Hitpoints) + s.GetLevel(SkillIds.Prayer) / 2);
            double melee = 0.325 * (s.GetLevel(SkillIds.Attack) + s.GetLevel(SkillIds.Strength));
            double ranged = 0.325 * (s.GetLevel(SkillIds.Ranged) * 3 / 2);
            double magic = 0.325 * (s.GetLevel(SkillIds.Magic) * 3 / 2);
            return (int)Math.Floor(basePart + Math.Max(melee, Math.Max(ranged, magic)));
        }

        /// The skill a melee style trains (ranged styles: see AwardXp).
        public static string TrainedSkill(CombatStyle style) =>
            style == CombatStyle.Accurate ? SkillIds.Attack : style == CombatStyle.Aggressive ? SkillIds.Strength
            : style == CombatStyle.Defensive ? SkillIds.Defence : SkillIds.Ranged;

        /// Gives the xp for dealing `damage`: 4 per damage to the style's skill (with a bow, Ranged; Longrange
        /// splits it between Ranged and Defence), plus 1.33 Hitpoints.
        public static void AwardXp(SkillSet skills, CombatStyle style, int damage, bool ranged = false)
        {
            if (damage <= 0)
                return;
            if (ranged && style == CombatStyle.Longrange)
            {
                skills.AddXp(SkillIds.Ranged, damage * XpPerDamage / 2);
                skills.AddXp(SkillIds.Defence, damage * XpPerDamage / 2);
            }
            else
            {
                skills.AddXp(ranged ? SkillIds.Ranged : TrainedSkill(style), damage * XpPerDamage);
            }
            skills.AddXp(SkillIds.Hitpoints, damage * HitpointsXpPerDamage);
        }
    }
}
