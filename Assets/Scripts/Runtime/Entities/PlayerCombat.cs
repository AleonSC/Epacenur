using System.Collections.Generic;
using Epacenur.Core;
using UnityEngine;

namespace Epacenur.Runtime
{
    /// Fighting monsters (both views): melee, or from a distance with a bow or a spell; and the melee abilities on the action bar.
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

        /// Wielding a bow (or another ranged weapon).
        public bool UsingRanged => ContentDb.Current.WeaponKind(G.Equipment.WeaponKind)?.Ranged ?? false;

        /// Autocasting a combat spell (chosen in the Magic tab): attacks cast it instead of using the weapon.
        public bool UsingMagic => G.Autocast != null;

        /// Attacking from a distance (a bow or a spell) rather than next to the monster.
        public bool AttacksFromRange => UsingMagic || UsingRanged;

        /// Spells reach this far, in tiles.
        public const int SpellRange = 10;
        const int SpellTicks = 5;

        /// How far you can attack from, in tiles: next to it for melee; a bow's range (2 more on Longrange); a spell's.
        public int AttackRange => UsingMagic ? SpellRange : UsingRanged
            ? G.Equipment[EquipSlot.Weapon].Def.Range + (G.CurrentStyle.Style == CombatStyle.Longrange ? 2 : 0) : 1;

        /// Seconds between attacks: the weapon's speed (a tick less on Rapid).
        float AttackSeconds => UsingMagic ? SpellTicks * GameTime.TickSeconds : (G.Equipment.AttackTicks - (UsingRanged && G.CurrentStyle.Style == CombatStyle.Rapid ? 1 : 0)) * GameTime.TickSeconds;

        /// First person: one attack per click, no faster than the weapon's speed.
        void FirstPersonAttack(Npc npc)
        {
            if (swingCooldown > 0f || Time.time < nextAttackAt || npc.IsDead || !CanWound(npc))
                return;
            swingCooldown = AttackSeconds;
            SwingT = 1f;
            npc.Engage(transform.position);
            Attack(npc);
        }

        // ---- Combat (both modes) ----

        /// Some monsters can only be hurt by someone with a Slayer level (they say so, and you don't attack).
        bool CanWound(Npc npc)
        {
            int need = npc.Def.SlayerLevel;
            if (need <= 0 || G.Skills.GetLevel(Epacenur.Content.SlayerModule.Skill) >= need)
                return true;
            GameLog.Add("You need a Slayer level of " + need + " to know how to wound this monster.");
            return false;
        }

        /// Accuracy and max hit against a monster: the weapon and style's, and a slayer helmet's boost on task.
        void Rolls(Npc npc, out double hitChance, out int maxHit)
        {
            BaseRolls(npc, out hitChance, out maxHit);
            if (G.Slayer.task == npc.Def.Id && G.Equipment.IsWearing(EquipSlot.Head)
                && G.Equipment[EquipSlot.Head].ItemId == Epacenur.Content.SlayerModule.Helmet)
            {
                hitChance = Mathf.Min(1f, (float)(hitChance * Epacenur.Content.SlayerModule.HelmetBoost));
                maxHit = (int)(maxHit * Epacenur.Content.SlayerModule.HelmetBoost);
            }
        }

        void BaseRolls(Npc npc, out double hitChance, out int maxHit)
        {
            var style = G.CurrentStyle;
            var b = G.Equipment.Bonuses;
            if (UsingMagic)
            {
                hitChance = CombatRules.MagicHitChance(G.Level(SkillIds.Magic), b.MagicAttack, npc.Def.DefenceLevel, npc.Def.Bonuses.MagicDefence);
                maxHit = G.Autocast.MaxHit;
                return;
            }
            if (UsingRanged)
            {
                hitChance = CombatRules.HitChance(G.Level(SkillIds.Ranged), b.RangedAttack, style.Style,
                    npc.Def.DefenceLevel, npc.Def.Bonuses.RangedDefence);
                maxHit = CombatRules.RangedMaxHit(G.Level(SkillIds.Ranged), b.RangedStrength, style.Style);
                return;
            }
            hitChance = CombatRules.HitChance(G.Level(SkillIds.Attack), b.Attack(style.Type), style.Style,
                npc.Def.DefenceLevel, npc.Def.Bonuses.Defence(style.Type), G.Prayer.AttackMultiplier);
            maxHit = CombatRules.MaxHit(G.Level(SkillIds.Strength), b.Strength, style.Style, G.Prayer.StrengthMultiplier);
        }

        void StartAttack(Npc npc)
        {
            if (npc == attackTarget && !npc.IsDead)
                return;  // already on it: clicking again changes nothing
            if (!CanWound(npc))
                return;
            CancelAction();
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
            bool centred = flat.sqrMagnitude < 0.01f;
            bool inPlace = AttacksFromRange
                ? centred && CurrentTile.StepsTo(npc.Tile) <= AttackRange && CurrentTile != npc.Tile
                : centred && CurrentTile.IsCardinalNeighbour(npc.Tile);
            if (inPlace && AttacksFromRange && pathIndex < path.Count)
            {
                path.Clear();  // close enough to shoot: stop here
                pathIndex = 0;
            }
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

        /// One attack (melee, an arrow from a bow, or a spell). Returns true when it killed the monster.
        bool Attack(Npc npc)
        {
            var spell = G.Autocast;
            bool ranged = spell == null && UsingRanged;
            if (spell != null)
            {
                string problem = Magic.Problem(spell, G.Skills, G.Inventory, G.Equipment);
                if (problem != null)
                {
                    GameLog.Add(problem);
                    CancelAction();
                    return false;
                }
                Magic.UseRunes(spell, G.Inventory, G.Equipment);
                Projectile.Spell(transform.position + Vector3.up * 1.3f + transform.forward * 0.4f, npc.transform, npc.Def.Height * 0.55f, spell.Colour);
                Sfx.Play(Sound.Spell, 0.8f);
            }
            else if (ranged)
            {
                string problem = G.Equipment.AmmoProblem();
                if (problem != null)
                {
                    GameLog.Add(problem);
                    CancelAction();
                    return false;
                }
                // An arrow flies; most land on the ground under the target, to be picked up again.
                string ammo = G.Equipment[EquipSlot.Ammo].ItemId;
                G.Equipment.UseAmmo();
                Projectile.Fire(transform.position + Vector3.up * 1.3f + transform.forward * 0.4f, npc, ammo);
                Sfx.Play(Sound.Arrow, 0.8f);
                if (G.Rng.NextDouble() < 0.8)
                    G.World.SpawnGroundItem(ammo, 1, npc.Tile);
            }
            lastTarget = npc;
            nextAttackAt = Time.time + AttackSeconds;
            attackAnim = 0.4f;
            Rolls(npc, out double chance, out int maxHit);
            int damage = Mathf.Min(CombatRules.RollDamage(G.Rng, chance, maxHit), npc.Hitpoints);
            bool killed = npc.TakeHit(damage);
            if (spell != null)
                CombatRules.AwardMagicXp(G.Skills, spell, damage);
            else
                CombatRules.AwardXp(G.Skills, G.CurrentStyle.Style, damage, ranged);
            if (killed)
                GameLog.Add("You defeat the " + npc.Def.Name.ToLowerInvariant() + ".");
            return killed;
        }

        /// A monster swings at you: Protect from Melee blocks it; otherwise it rolls against your Defence.
        /// If you're standing idle (tile mode) you fight back, like RuneScape's auto retaliate.
        public void TakeMonsterHit(Npc npc)
        {
            int damage = 0;
            bool melee = npc.Def.AttackType != AttackType.Ranged && npc.Def.AttackType != AttackType.Magic;
            if (!(melee && G.Prayer.ProtectsFromMelee))
            {
                double chance = CombatRules.MonsterHitChance(npc.Def, G.Level(SkillIds.Defence),
                    G.Equipment.Bonuses.Defence(npc.Def.AttackType), G.CurrentStyle.Style, G.Prayer.DefenceMultiplier);
                damage = CombatRules.RollDamage(G.Rng, chance, npc.Def.MaxHit);
            }
            G.HurtPlayer(damage);

            bool idle = attackTarget == null && Activity == null && pathIndex >= path.Count;
            if (Mode == ViewMode.Tile && idle && !npc.IsDead)
            {
                attackTarget = npc;
                npc.Engage(transform.position);
            }
        }

        public void ShowDamage(int damage)
        {
            LastHurtTime = Time.time;
            Hitsplats.Add(new Npc.Hitsplat { Damage = damage, Time = Time.time });
            Sfx.Play(damage > 0 ? Sound.Hurt : Sound.Miss, 0.8f);
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
            if (!Cooldowns.IsReady(id, Time.time))
                return;
            if (AttacksFromRange)
            {
                GameLog.Add(UsingMagic ? "Turn off autocast (in the Magic tab) to use " + def.Name + "."
                    : "You need a melee weapon (or none) to use " + def.Name + ".");
                return;
            }

            Npc target = AbilityTarget(def.Effect == AbilityEffect.Charge);
            if (target == null)
            {
                GameLog.Add("You need a target to use " + def.Name + ".");
                return;
            }
            if (!CanWound(target))
                return;
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
            Rolls(npc, out double chance, out int maxHit);
            return Abilities.RollDamage(G.Rng, def, chance, maxHit, (double)npc.Hitpoints / npc.Def.Hitpoints);
        }

        void AbilityHit(Npc npc, int damage)
        {
            if (npc.IsDead)
                return;
            damage = Mathf.Min(damage, npc.Hitpoints);
            bool killed = npc.TakeHit(damage);
            CombatRules.AwardXp(G.Skills, G.CurrentStyle.Style, damage, UsingRanged);
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
