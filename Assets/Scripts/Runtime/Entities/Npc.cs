using System.Collections.Generic;
using Epacenur.Core;
using UnityEngine;

namespace Epacenur.Runtime
{
    /// A monster you can attack, built from an NpcDef. It wanders round its plot until you attack it,
    /// then fights back: it chases you (not far from its plot) and hits you every few ticks.
    /// When it dies it drops its loot on the ground and comes back after its respawn time.
    public sealed class Npc : MonoBehaviour
    {
        public const float HitsplatSeconds = 1f;

        public struct Hitsplat
        {
            public int Damage;
            public float Time;
        }

        public NpcDef Def { get; private set; }
        public int Hitpoints { get; private set; }
        public bool IsDead { get; private set; }
        /// The tile it's standing on (it moves, so this changes).
        public Tile Tile => World.TileAt(transform.position);
        public float LastHitTime { get; private set; } = -100f;
        public float StunnedUntil { get; private set; } = -100f;
        public bool IsStunned => Time.time < StunnedUntil;
        public readonly List<Hitsplat> Hitsplats = new List<Hitsplat>();
        public string HoverName => Def.Name + "  (level-" + Def.CombatLevel + ")";

        RectInt home;
        Transform model;
        Collider hitbox;
        readonly List<Tile> path = new List<Tile>();
        int pathIndex;
        float nextWander, engagedUntil, diedAt, nextAttack;
        bool retaliating;  // fighting back against the player
        float aggroAfter;  // aggressive monsters: when it next looks for someone to attack
        public bool IsFighting => retaliating && !IsDead;
        System.Random rng;

        public static Npc Create(NpcDef def, Tile tile, RectInt home, Transform parent, System.Random rng)
        {
            var go = new GameObject(def.Name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = World.Local(tile);
            go.transform.localRotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
            var npc = go.AddComponent<Npc>();
            npc.Def = def;
            npc.home = home;
            npc.rng = rng;
            npc.Hitpoints = def.Hitpoints;
            npc.nextWander = Time.time + 1f + (float)rng.NextDouble() * 4f;
            npc.model = ModelBuilder.Build(def.Model, go.transform);
            // A trigger, so it can be clicked and aimed at without pushing anyone around.
            var col = go.AddComponent<CapsuleCollider>();
            col.isTrigger = true;
            col.radius = 0.4f;
            col.height = def.Height;
            col.center = new Vector3(0f, def.Height / 2f, 0f);
            npc.hitbox = col;
            return npc;
        }

        /// Being attacked: stop wandering and face the attacker for a while.
        public void Engage(Vector3 attacker)
        {
            engagedUntil = Time.time + 6f;
            path.Clear();
            Vector3 d = attacker - transform.position;
            d.y = 0f;
            if (d.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.LookRotation(d);
        }

        /// Stands still for a while (having its pocket picked), without turning round.
        public void Hold(float seconds)
        {
            engagedUntil = Mathf.Max(engagedUntil, Time.time + seconds);
            path.Clear();
            pathIndex = 0;
        }

        /// Caught you with a hand in its pocket: turns and hits you.
        public void Catch(Vector3 thief)
        {
            Engage(thief);
            attackAnim = 0.4f;
        }

        /// Stunned: stands still (it won't even wander) for a while.
        public void Stun(float seconds)
        {
            StunnedUntil = Time.time + seconds;
            engagedUntil = Mathf.Max(engagedUntil, StunnedUntil);
            path.Clear();
        }

        /// Takes a hit (0 shows a blue miss splat). Returns true when that killed it.
        public bool TakeHit(int damage)
        {
            if (IsDead)
                return false;
            damage = Mathf.Min(damage, Hitpoints);
            Hitpoints -= damage;
            LastHitTime = Time.time;
            if (!retaliating)
            {
                retaliating = true;
                // It swings back after a moment, not instantly.
                nextAttack = Mathf.Max(nextAttack, Time.time + 2f * GameTime.TickSeconds);
            }
            Hitsplats.Add(new Hitsplat { Damage = damage, Time = Time.time });
            Sfx.PlayAt(damage > 0 ? Sound.Hit : Sound.Miss, transform.position + Vector3.up);
            if (Hitpoints > 0)
                return false;
            Sfx.PlayAt(Sound.Kill, transform.position + Vector3.up, 0.8f);
            Die();
            return true;
        }

        void Die()
        {
            IsDead = true;
            retaliating = false;
            diedAt = Time.time;
            hitbox.enabled = false;
            path.Clear();
            Game.Instance.OnNpcKilled(Def);
            var tile = Game.Instance.World.NearestWalkable(Tile);
            foreach (var item in Def.AlwaysDrops)
                Game.Instance.World.SpawnGroundItem(item, 1, tile);
            if (Def.Drops != null && Def.Drops.Roll(rng, out string itemId, out int count))
                Game.Instance.World.SpawnGroundItem(itemId, count, Game.Instance.World.NearestWalkable(Tile));
        }

        void Respawn()
        {
            IsDead = false;
            Hitpoints = Def.Hitpoints;
            Hitsplats.Clear();
            LastHitTime = -100f;
            StunnedUntil = -100f;
            retaliating = false;
            path.Clear();
            pathIndex = 0;
            transform.position = World.TileCenter(RandomHomeTile());
            model.localRotation = Quaternion.identity;
            model.localPosition = Vector3.zero;
            model.gameObject.SetActive(true);
            hitbox.enabled = true;
        }

        /// Aggressive monsters attack a player who comes within 4 tiles of them (and near their home), unless
        /// the player's combat level is more than twice theirs, as in RuneScape.
        void LookForTrouble()
        {
            aggroAfter = Time.time + GameTime.TickSeconds;
            var g = Game.Instance;
            var player = g.Player;
            Vector3 d = player.transform.position - transform.position;
            d.y = 0f;
            if (d.magnitude > 4f || CombatRules.CombatLevel(g.Skills) > Def.CombatLevel * 2)
                return;
            var t = player.CurrentTile;
            if (t.X < home.x - 3 || t.X >= home.x + home.width + 3 || t.Z < home.y - 3 || t.Z >= home.y + home.height + 3)
                return;
            retaliating = true;
            nextAttack = Mathf.Max(nextAttack, Time.time + 2f * GameTime.TickSeconds);
        }

        /// Fighting back: walk up next to the player, then hit them every AttackTicks.
        /// It gives up if the player gets far away or leaves the area around its plot.
        void UpdateFighting()
        {
            var player = Game.Instance.Player;
            Vector3 d = player.transform.position - transform.position;
            d.y = 0f;
            Tile playerTile = World.TileAt(player.transform.position);
            bool nearHome = playerTile.X >= home.x - 3 && playerTile.X < home.x + home.width + 3
                && playerTile.Z >= home.y - 3 && playerTile.Z < home.y + home.height + 3;
            if (d.magnitude > 12f || !nearHome)
            {
                retaliating = false;
                aggroAfter = Time.time + 10f;  // it doesn't come straight back for you
                path.Clear();
                engagedUntil = 0f;
                return;
            }
            if (IsStunned)
                return;

            Vector3 offTile = transform.position - World.TileCenter(Tile);
            offTile.y = 0f;
            bool inRange = (Def.AttackRange > 1 ? Tile.StepsTo(playerTile) <= Def.AttackRange && Tile != playerTile : Tile.IsCardinalNeighbour(playerTile))
                && offTile.sqrMagnitude < 0.02f;
            if (inRange)
            {
                path.Clear();
                if (d.sqrMagnitude > 0.01f)
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(d), 1f - Mathf.Exp(-12f * Time.deltaTime));
                if (Time.time >= nextAttack)
                {
                    nextAttack = Time.time + Def.AttackTicks * GameTime.TickSeconds;
                    attackAnim = 0.4f;
                    // A boss's special every few swings: a different kind of attack, harder (pray against it).
                    bool special = Def.SpecialEvery > 0 && ++attacks % Def.SpecialEvery == 0;
                    var type = special ? Def.SpecialType : Def.AttackType;
                    if (special && Def.SpecialMessage != null)
                        GameLog.Add("<color=#ff8060>" + Def.SpecialMessage + "</color>");
                    if (type == AttackType.Magic || type == AttackType.Ranged)
                    {
                        Sfx.PlayAt(Sound.Spell, transform.position + Vector3.up, 0.7f);
                        Projectile.Spell(transform.position + Vector3.up * Def.Height * 0.7f + transform.forward * 0.3f, player.transform, 1.2f,
                            type == AttackType.Magic ? new Rgb(0.7f, 0.3f, 0.9f) : new Rgb(0.95f, 0.5f, 0.15f));
                    }
                    player.TakeMonsterHit(this, type, special ? Def.SpecialMaxHit : Def.MaxHit);
                }
                return;
            }

            // The player is walking up to fight it: wait for them rather than dancing around.
            if (player.AttackTarget == this && player.Mode == ViewMode.Tile && !player.AttacksFromRange)
                return;
            if (pathIndex >= path.Count || !path[path.Count - 1].IsCardinalNeighbour(playerTile))
            {
                // Re-plan from the tile it's stepping into, so it doesn't turn back mid-step.
                Tile from = pathIndex < path.Count ? path[pathIndex] : Tile;
                path.Clear();
                pathIndex = 0;
                path.Add(from);
                var p = Pathfinder.PathToInteract(Game.Instance.World.Grid, from, playerTile);
                if (p != null)
                    path.AddRange(p);
            }
            Step(Def.WalkSpeed * 1.6f);
        }

        void Step(float speed)
        {
            Vector3 full = World.TileCenter(path[pathIndex]) - transform.position;
            Vector3 to = full;
            to.y = 0f;
            float step = speed * Time.deltaTime;
            if (to.magnitude <= step)
            {
                transform.position += full;
                if (++pathIndex >= path.Count)
                {
                    path.Clear();
                    pathIndex = 0;
                }
            }
            else
            {
                transform.position += to.normalized * step + Vector3.up * full.y * (step / to.magnitude);  // up and down the hills
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to), 1f - Mathf.Exp(-8f * Time.deltaTime));
            }
        }

        int attacks;  // swings so far (a boss's special comes every SpecialEvery)

        float attackAnim;

        Tile RandomHomeTile()
        {
            var grid = Game.Instance.World.Grid;
            for (int tries = 0; tries < 30; tries++)
            {
                var t = new Tile(home.x + rng.Next(home.width), home.y + rng.Next(home.height));
                if (grid.IsWalkable(t))
                    return t;
            }
            return Tile;
        }

        void Update()
        {
            Hitsplats.RemoveAll(h => Time.time - h.Time > HitsplatSeconds);
            if (IsDead)
            {
                // Topple over and sink, then vanish until respawn.
                float t = Time.time - diedAt;
                model.localRotation = Quaternion.Euler(Mathf.Min(1f, t / 0.5f) * -85f, 0f, 0f);
                model.localPosition = new Vector3(0f, -Mathf.Max(0f, t - 0.8f) * 0.6f, 0f);
                if (t > 1.6f)
                    model.gameObject.SetActive(false);
                if (t > Def.RespawnSeconds)
                    Respawn();
                return;
            }
            // A lunge forward when it swings.
            attackAnim = Mathf.Max(0f, attackAnim - Time.deltaTime);
            model.localPosition = new Vector3(0f, 0f, Mathf.Sin(attackAnim / 0.4f * Mathf.PI) * 0.25f);
            if (!retaliating && Def.Aggressive && Time.time >= aggroAfter)
                LookForTrouble();
            if (retaliating)
            {
                UpdateFighting();
                return;
            }
            if (Time.time < engagedUntil)
                return;
            if (pathIndex < path.Count)
                Step(Def.WalkSpeed);
            else if (Time.time >= nextWander)
            {
                nextWander = Time.time + 3f + (float)rng.NextDouble() * 5f;
                Tile goal = RandomHomeTile();
                if (goal.StepsTo(Tile) <= 5)
                {
                    path.Clear();
                    pathIndex = 0;
                    path.AddRange(Pathfinder.PathTo(Game.Instance.World.Grid, Tile, goal));
                }
            }
        }
    }
}
