using System.Collections.Generic;
using Epacenur.Core;
using UnityEngine;

namespace Epacenur.Runtime
{
    /// Fighting monsters (both views) and the melee abilities on the action bar.
    public sealed partial class PlayerController
    {
        /// The monster you're walking to or fighting in tile mode (monsters wait for you rather than meet you).
        public Npc AttackTarget => attackTarget;
        public bool IsFighting => fighting;
        /// Ability cooldowns, for the action bar.
        public readonly AbilityCooldowns Cooldowns = new AbilityCooldowns();
        /// Damage you've taken lately, for hitsplats over your head.
        public readonly List<Npc.Hitsplat> Hitsplats = new List<Npc.Hitsplat>();
        public float LastHurtTime { get; private set; } = -100f;

        Npc attackTarget;           // the monster being walked to or fought (tile mode)
        Npc hoverNpc;               // the monster under the mouse (tile mode) or crosshair (first person)
        Npc lastTarget;             // the last monster you hit, for abilities when nothing else is targeted
        bool fighting;
        /// When your next attack can land (game time). It's kept across clicks and target changes, so
        /// clicking a monster again can never hit faster than your weapon allows.
        float nextAttackAt;

        // A bleed (Dismember): hits still to land on a monster.
        sealed class Bleed { public Npc Npc; public int[] Hits; public int Next; public float NextTime; }
        readonly List<Bleed> bleeds = new List<Bleed>();

        /// First person: one attack per click, no faster than the weapon's speed.
        void FirstPersonAttack(Npc npc)
        {
            if (swingCooldown > 0f || Time.time < nextAttackAt || npc.IsDead || !CanFight())
                return;
            swingCooldown = G.Equipment.AttackTicks * GameTime.TickSeconds;
            SwingT = 1f;
            npc.Engage(transform.position);
            Attack(npc);
        }

        // ---- Combat (both modes) ----

        /// For now you need both a sword and a shield worn to fight.
        bool CanFight()
        {
            if (!G.Equipment.IsWearing(EquipSlot.Weapon) || !G.Equipment.IsWearing(EquipSlot.Shield))
            {
                GameLog.Add("You need to wield a sword and shield to fight.");
                return false;
            }
            return true;
        }

        void StartAttack(Npc npc)
        {
            if (npc == attackTarget && !npc.IsDead)
                return;  // already on it: clicking again changes nothing
            CancelAction();
            if (!CanFight())
                return;
            attackTarget = npc;
            npc.Engage(transform.position);
        }

        /// Tile mode: walk up to the monster, then hit it every few ticks until it dies.
        Vector3 UpdateAttack(float dt)
        {
            var npc = attackTarget;
            if (npc == null || npc.IsDead)
            {
                CancelAction();
                return Vector3.zero;
            }
            Vector3 flat = transform.position - World.TileCenter(CurrentTile);
            flat.y = 0f;
            bool inPlace = CurrentTile.IsCardinalNeighbour(npc.Tile) && flat.sqrMagnitude < 0.01f;
            if (!inPlace)
            {
                fighting = false;
                if (pathIndex >= path.Count)
                {
                    var p = Pathfinder.PathToInteract(G.World.Grid, StartTile(), npc.Tile);
                    if (p == null)
                    {
                        GameLog.Add("I can't reach that!");
                        CancelAction();
                        return Vector3.zero;
                    }
                    SetPath(p);
                    if (path.Count == 0)
                        return Vector3.zero;
                }
                return FollowPath(dt);
            }

            npc.Engage(transform.position);
            Face(npc.transform.position - transform.position, dt);
            // The first hit lands as soon as you arrive, if your weapon is ready.
            fighting = true;
            if (Time.time >= nextAttackAt && Attack(npc))
                CancelAction();
            return Vector3.zero;
        }

        /// One melee attack. Returns true when it killed the monster.
        bool Attack(Npc npc)
        {
            lastTarget = npc;
            nextAttackAt = Time.time + G.Equipment.AttackTicks * GameTime.TickSeconds;
            attackAnim = 0.4f;
            var eq = G.Equipment;
            var style = G.Style;
            double chance = CombatRules.HitChance(G.Skills.GetLevel(SkillIds.Attack), eq.AttackBonus, style, npc.Def.DefenceLevel, npc.Def.DefenceBonus, G.Prayer.AttackMultiplier);
            int maxHit = CombatRules.MaxHit(G.Skills.GetLevel(SkillIds.Strength), eq.StrengthBonus, style, G.Prayer.StrengthMultiplier);
            int damage = Mathf.Min(CombatRules.RollDamage(G.Rng, chance, maxHit), npc.Hitpoints);
            bool killed = npc.TakeHit(damage);
            CombatRules.AwardXp(G.Skills, style, damage);
            if (killed)
                GameLog.Add("You defeat the " + npc.Def.Name.ToLowerInvariant() + ".");
            return killed;
        }

        /// A monster swings at you: Protect from Melee blocks it; otherwise it rolls against your Defence.
        /// If you're standing idle (tile mode, sword and shield worn) you fight back, like RuneScape's auto retaliate.
        public void TakeMonsterHit(Npc npc)
        {
            int damage = 0;
            if (!G.Prayer.ProtectsFromMelee)
            {
                double chance = CombatRules.MonsterHitChance(npc.Def, G.Skills.GetLevel(SkillIds.Defence), G.Equipment.DefenceBonus, G.Style, G.Prayer.DefenceMultiplier);
                damage = CombatRules.RollDamage(G.Rng, chance, npc.Def.MaxHit);
            }
            G.HurtPlayer(damage);

            bool idle = attackTarget == null && Activity == null && pathIndex >= path.Count;
            bool armed = G.Equipment.IsWearing(EquipSlot.Weapon) && G.Equipment.IsWearing(EquipSlot.Shield);
            if (Mode == ViewMode.Tile && idle && armed && !npc.IsDead)
            {
                attackTarget = npc;
                npc.Engage(transform.position);
            }
        }

        public void ShowDamage(int damage)
        {
            LastHurtTime = Time.time;
            Hitsplats.Add(new Npc.Hitsplat { Damage = damage, Time = Time.time });
        }

        // ---- Abilities (action bar, keys 1-9) ----

        /// Whether the ability's level requirement is met (the bar greys out the ones that aren't).
        public bool HasLevelFor(AbilityDef def) => G.Skills.GetLevel(def.Skill) >= def.Level;

        /// Uses an ability on your target: the monster you're fighting, else the one under the mouse
        /// or crosshair, else the last one you hit.
        public void UseAbility(AbilityId id)
        {
            var def = Abilities.Get(id);
            if (!HasLevelFor(def))
            {
                GameLog.Add("You need " + ContentDb.Current.Skill(def.Skill).Name + " level " + def.Level + " to use " + def.Name + ".");
                return;
            }
            if (!Cooldowns.IsReady(id, Time.time) || !CanFight())
                return;

            Npc target = AbilityTarget(def.Effect == AbilityEffect.Charge);
            if (target == null)
            {
                GameLog.Add("You need a target to use " + def.Name + ".");
                return;
            }
            float reach = Mode == ViewMode.FirstPerson ? FirstPersonReach : 1.6f;
            if (FlatDistance(target) > reach)
            {
                if (def.Effect != AbilityEffect.Charge || FlatDistance(target) > Abilities.ChargeRange + 0.5f)
                {
                    GameLog.Add("Your target is too far away.");
                    return;
                }
                // Barge: rush to the target.
                var p = Pathfinder.PathToInteract(G.World.Grid, StartTile(), target.Tile);
                if (p == null)
                {
                    GameLog.Add("I can't reach that!");
                    return;
                }
                if (p.Count > 0)
                    Teleport(p[p.Count - 1]);
                Vector3 look = target.transform.position - transform.position;
                look.y = 0f;
                if (look.sqrMagnitude > 0.01f)
                    transform.rotation = Quaternion.LookRotation(look);
            }

            Cooldowns.Use(id, Time.time);
            attackAnim = 0.4f;
            SwingT = 1f;
            target.Engage(transform.position);
            lastTarget = target;

            int damage = RollAbility(def, target);
            if (def.Effect == AbilityEffect.Bleed)
            {
                var hits = Abilities.SplitBleed(damage);
                bleeds.Add(new Bleed { Npc = target, Hits = hits, Next = 1, NextTime = Time.time + Abilities.BleedInterval });
                AbilityHit(target, hits[0]);
            }
            else
            {
                if (def.Effect == AbilityEffect.Stun && damage > 0)
                    target.Stun(Abilities.StunSeconds);
                if (def.Effect == AbilityEffect.Area)
                    foreach (var other in G.World.Npcs)
                        if (other != target && !other.IsDead && (other.transform.position - target.transform.position).magnitude <= 1.6f)
                        {
                            other.Engage(transform.position);
                            AbilityHit(other, RollAbility(def, other));
                        }
                AbilityHit(target, damage);
            }

            // Keep fighting it afterwards; the next auto-attack waits a full weapon swing.
            float swing = G.Equipment.AttackTicks * GameTime.TickSeconds;
            if (Mode == ViewMode.Tile && !target.IsDead)
            {
                if (attackTarget != target)
                {
                    CancelAction();
                    attackTarget = target;
                }
                fighting = FlatDistance(target) <= 1.6f;
                nextAttackAt = Mathf.Max(nextAttackAt, Time.time + swing);
            }
            else
            {
                swingCooldown = Mathf.Max(swingCooldown, swing);
                nextAttackAt = Mathf.Max(nextAttackAt, Time.time + swing);
            }
        }

        Npc AbilityTarget(bool longRange)
        {
            if (attackTarget != null && !attackTarget.IsDead)
                return attackTarget;
            if (hoverNpc != null && !hoverNpc.IsDead)
                return hoverNpc;
            if (longRange && Mode == ViewMode.FirstPerson)
            {
                // Barge in first person: whatever is under the crosshair, however far.
                var cam = G.Rig.Cam.transform;
                if (Physics.Raycast(cam.position, cam.forward, out RaycastHit hit, Abilities.ChargeRange + 2f))
                {
                    var npc = hit.collider.GetComponentInParent<Npc>();
                    if (npc != null && !npc.IsDead)
                        return npc;
                }
            }
            return lastTarget != null && !lastTarget.IsDead ? lastTarget : null;
        }

        int RollAbility(AbilityDef def, Npc npc)
        {
            var eq = G.Equipment;
            double chance = CombatRules.HitChance(G.Skills.GetLevel(SkillIds.Attack), eq.AttackBonus, G.Style, npc.Def.DefenceLevel, npc.Def.DefenceBonus, G.Prayer.AttackMultiplier);
            int maxHit = CombatRules.MaxHit(G.Skills.GetLevel(SkillIds.Strength), eq.StrengthBonus, G.Style, G.Prayer.StrengthMultiplier);
            return Abilities.RollDamage(G.Rng, def, chance, maxHit, (double)npc.Hitpoints / npc.Def.Hitpoints);
        }

        void AbilityHit(Npc npc, int damage)
        {
            if (npc.IsDead)
                return;
            damage = Mathf.Min(damage, npc.Hitpoints);
            bool killed = npc.TakeHit(damage);
            CombatRules.AwardXp(G.Skills, G.Style, damage);
            if (killed)
            {
                GameLog.Add("You defeat the " + npc.Def.Name.ToLowerInvariant() + ".");
                if (npc == attackTarget)
                    CancelAction();
            }
        }

        void UpdateBleeds()
        {
            for (int i = bleeds.Count - 1; i >= 0; i--)
            {
                var b = bleeds[i];
                if (b.Npc == null || b.Npc.IsDead)
                {
                    bleeds.RemoveAt(i);
                    continue;
                }
                if (Time.time < b.NextTime)
                    continue;
                AbilityHit(b.Npc, b.Hits[b.Next]);
                b.Next++;
                b.NextTime += Abilities.BleedInterval;
                if (b.Next >= b.Hits.Length)
                    bleeds.RemoveAt(i);
            }
        }

    }
}
