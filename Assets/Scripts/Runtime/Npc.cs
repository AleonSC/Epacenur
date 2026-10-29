using System.Collections.Generic;
using Epacenur.Core;
using UnityEngine;

namespace Epacenur.Runtime
{
    /// A monster you can attack (goblins for now). It wanders round its plot until you attack it,
    /// then fights back: it chases you (not far from its plot) and hits you every few ticks.
    /// When it dies it drops its loot on the ground and comes back after its respawn time.
    public sealed class Npc : MonoBehaviour
    {
        public const float WalkSpeed = 1.6f;   // tiles per second
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
        public bool IsFighting => retaliating && !IsDead;
        System.Random rng;

        public static Npc Create(NpcDef def, Tile tile, RectInt home, Transform parent, System.Random rng)
        {
            var go = new GameObject(def.Name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = World.TileCenter(tile);
            go.transform.localRotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
            var npc = go.AddComponent<Npc>();
            npc.Def = def;
            npc.home = home;
            npc.rng = rng;
            npc.Hitpoints = def.Hitpoints;
            npc.nextWander = Time.time + 1f + (float)rng.NextDouble() * 4f;
            npc.BuildGoblin();
            // A trigger, so it can be clicked and aimed at without pushing anyone around.
            var col = go.AddComponent<CapsuleCollider>();
            col.isTrigger = true;
            col.radius = 0.4f;
            col.height = 1.5f;
            col.center = new Vector3(0f, 0.75f, 0f);
            npc.hitbox = col;
            return npc;
        }

        void BuildGoblin()
        {
            model = new GameObject("Model").transform;
            model.SetParent(transform, false);
            var skin = new Color(Def.R, Def.G, Def.B);
            var cloth = new Color(0.45f, 0.32f, 0.2f);
            Visuals.Prim(PrimitiveType.Capsule, model, new Vector3(0f, 0.72f, 0f), new Vector3(0.45f, 0.4f, 0.35f), cloth);
            Visuals.Prim(PrimitiveType.Capsule, model, new Vector3(-0.12f, 0.22f, 0f), new Vector3(0.14f, 0.22f, 0.14f), skin);
            Visuals.Prim(PrimitiveType.Capsule, model, new Vector3(0.12f, 0.22f, 0f), new Vector3(0.14f, 0.22f, 0.14f), skin);
            Visuals.Prim(PrimitiveType.Capsule, model, new Vector3(-0.29f, 0.75f, 0.05f), new Vector3(0.11f, 0.24f, 0.11f), skin);
            Visuals.Prim(PrimitiveType.Capsule, model, new Vector3(0.29f, 0.75f, 0.05f), new Vector3(0.11f, 0.24f, 0.11f), skin);
            Visuals.Prim(PrimitiveType.Sphere, model, new Vector3(0f, 1.2f, 0.02f), new Vector3(0.36f, 0.34f, 0.36f), skin);
            Visuals.Prim(PrimitiveType.Cube, model, new Vector3(0f, 1.17f, 0.18f), new Vector3(0.07f, 0.1f, 0.08f), skin * 0.85f);       // nose
            Visuals.Prim(PrimitiveType.Cube, model, new Vector3(-0.08f, 1.25f, 0.16f), new Vector3(0.05f, 0.04f, 0.03f), new Color(0.8f, 0.1f, 0.05f));
            Visuals.Prim(PrimitiveType.Cube, model, new Vector3(0.08f, 1.25f, 0.16f), new Vector3(0.05f, 0.04f, 0.03f), new Color(0.8f, 0.1f, 0.05f));
            Visuals.Prim(PrimitiveType.Cube, model, new Vector3(-0.22f, 1.26f, 0f), new Vector3(0.2f, 0.05f, 0.08f), skin)                // ears
                .transform.localRotation = Quaternion.Euler(0f, 0f, -25f);
            Visuals.Prim(PrimitiveType.Cube, model, new Vector3(0.22f, 1.26f, 0f), new Vector3(0.2f, 0.05f, 0.08f), skin)
                .transform.localRotation = Quaternion.Euler(0f, 0f, 25f);
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
                nextAttack = Mathf.Max(nextAttack, Time.time + 2f * WoodcuttingRulesTick);
            }
            Hitsplats.Add(new Hitsplat { Damage = damage, Time = Time.time });
            if (Hitpoints > 0)
                return false;
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
            var tile = Game.Instance.World.NearestWalkable(Tile);
            foreach (var item in Def.AlwaysDrops)
                Game.Instance.World.SpawnGroundItem(item.Id, 1, tile);
            if (Def.Drops != null && Def.Drops.Roll(rng, out int itemId, out int count))
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

        const float WoodcuttingRulesTick = WoodcuttingRules.TickSeconds;

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
                path.Clear();
                engagedUntil = 0f;
                return;
            }
            if (IsStunned)
                return;

            Vector3 offTile = transform.position - World.TileCenter(Tile);
            offTile.y = 0f;
            bool inRange = Tile.IsCardinalNeighbour(playerTile) && offTile.sqrMagnitude < 0.02f;
            if (inRange)
            {
                path.Clear();
                if (d.sqrMagnitude > 0.01f)
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(d), 1f - Mathf.Exp(-12f * Time.deltaTime));
                if (Time.time >= nextAttack)
                {
                    nextAttack = Time.time + Def.AttackTicks * WoodcuttingRulesTick;
                    attackAnim = 0.4f;
                    player.TakeMonsterHit(this);
                }
                return;
            }

            // The player is walking up to fight it: wait for them rather than dancing around.
            if (player.AttackTarget == this && player.Mode == ViewMode.Tile)
                return;
            if (pathIndex >= path.Count || !path[path.Count - 1].IsCardinalNeighbour(playerTile))
            {
                path.Clear();
                pathIndex = 0;
                var p = Pathfinder.PathToInteract(Game.Instance.World.Grid, Tile, playerTile);
                if (p != null)
                    path.AddRange(p);
                if (path.Count == 0)
                    path.Add(Tile);  // back onto its tile's centre
            }
            Step(WalkSpeed * 1.6f);
        }

        void Step(float speed)
        {
            Vector3 to = World.TileCenter(path[pathIndex]) - transform.position;
            to.y = 0f;
            float step = speed * Time.deltaTime;
            if (to.magnitude <= step)
            {
                transform.position += to;
                if (++pathIndex >= path.Count)
                {
                    path.Clear();
                    pathIndex = 0;
                }
            }
            else
            {
                transform.position += to.normalized * step;
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to), 1f - Mathf.Exp(-8f * Time.deltaTime));
            }
        }

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
            if (retaliating)
            {
                UpdateFighting();
                return;
            }
            if (Time.time < engagedUntil)
                return;
            if (pathIndex < path.Count)
                Step(WalkSpeed);
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
