using System.Collections.Generic;
using Epacenur.Core;
using UnityEngine;

namespace Epacenur.Runtime
{
    /// Something you gather from, built from a NodeDef: a tree, a rock, a fishing spot. Each success takes
    /// a life; with none left it's used up (a stump, an empty rock) until it grows back. Fishing spots never
    /// run out but move along their shore now and then. Using it: Activities.Gather.
    public sealed class NodeObject : Interactable
    {
        public NodeDef Def { get; private set; }
        public int LivesLeft { get; private set; }
        public bool IsEmpty { get; private set; }

        public override string Action => Def.Action;
        public override string DisplayName => IsEmpty ? Def.EmptyName : Def.Name;
        public override string Examine => IsEmpty ? Def.EmptyExamine : Def.Examine;
        public override bool HasAction => !IsEmpty || Def.EmptyMessage != null;
        public override void Interact() => Game.Instance.Player.Gather(this);

        Transform full, empty, shaker;
        ChunkBatch batch;
        bool shakeTurns;      // a tree's crown wobbles; anything else jiggles sideways
        Collider hitbox;
        float refillAt, shake;
        System.Random rng;

        // Fishing spots
        List<Tile> shore;
        Transform[] rings, bubbles;
        float nextMove;

        /// Whether it can be drawn as part of its chunk's merged mesh now: not a fishing spot (they move) and not
        /// shaking under a tool.
        public bool Batchable => rings == null && shake <= 0f;

        /// The renderers of how it looks now (full or used up), not counting glowing bits.
        public IEnumerable<Renderer> SolidRenderers()
        {
            var root = IsEmpty ? empty : full;
            if (root == null || !root.gameObject.activeSelf)
                yield break;
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>())
                if (r.gameObject.name != "Glow")
                    yield return r;
        }

        /// Joins its chunk's merged mesh (World.AddNode).
        public void JoinBatch(ChunkBatch b)
        {
            if (rings != null)
                return;
            batch = b;
            b.Add(this);
        }

        void Changed()
        {
            if (batch != null)
                batch.Invalidate();
        }

        public static NodeObject Create(NodeDef def, Tile tile, Transform parent, System.Random rng, List<Tile> shore = null)
        {
            var go = new GameObject(def.Name + " " + tile);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = World.Local(tile);
            var node = go.AddComponent<NodeObject>();
            node.Def = def;
            node.Tile = tile;
            node.rng = rng;
            node.shore = shore;
            if (def.Look == NodeLook.FishingSpot)
            {
                node.BuildRipples();
                node.MoveTo(tile);
            }
            else
            {
                go.transform.localRotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
                node.full = ModelBuilder.Build(def.Model, go.transform, "Full");
                if (def.Look == NodeLook.Tree)
                    node.full.localScale = Vector3.one * (0.92f + (float)rng.NextDouble() * 0.16f);  // a little variety
                node.shaker = ModelBuilder.Find(node.full, "crown");
                node.shakeTurns = node.shaker != null;
                if (node.shaker == null)
                    node.shaker = node.full;
                node.BuildHitbox();
            }
            node.Refill();
            return node;
        }

        void BuildHitbox()
        {
            if (Def.Look == NodeLook.Tree)
            {
                // One thin collider for clicking, aiming and blocking WASD: the trunk, not the whole crown.
                var capsule = gameObject.AddComponent<CapsuleCollider>();
                capsule.radius = 0.45f;
                hitbox = capsule;
            }
            else
            {
                hitbox = ModelBuilder.AddBox(gameObject, Def.Model);
                hitbox.isTrigger = !Def.Blocks;
            }
        }

        void SetHitboxHeight(bool tall)
        {
            if (hitbox is CapsuleCollider capsule)
            {
                capsule.height = tall ? 3.6f : 0.9f;
                capsule.center = new Vector3(0f, capsule.height / 2f, 0f);
            }
        }

        /// Grows back (or starts out) full.
        public void Refill()
        {
            IsEmpty = false;
            LivesLeft = Gathering.RollLives(Def, rng);
            if (full != null)
                full.gameObject.SetActive(true);
            if (empty != null)
                empty.gameObject.SetActive(false);
            SetHitboxHeight(true);
            Changed();
            enabled = rings != null;  // nothing to do each frame until it's worked on
        }

        /// A success: one life less. Returns true when that used it up.
        public bool TakeLife()
        {
            if (IsEmpty)
                return true;
            if (Def.MaxLives <= 0 || Cheats.InfiniteResources)
                return false;  // never runs out
            LivesLeft--;
            if (LivesLeft > 0)
                return false;
            IsEmpty = true;
            refillAt = Time.time + Def.RespawnSeconds;
            // The used-up look is only built the first time (there are a lot of trees).
            if (empty == null && Def.EmptyModel != null)
                empty = ModelBuilder.Build(Def.EmptyModel, transform, "Empty");
            if (full != null)
                full.gameObject.SetActive(false);
            if (empty != null)
                empty.gameObject.SetActive(true);
            SetHitboxHeight(false);
            Changed();
            enabled = true;  // to grow back
            return true;
        }

        /// A tool strikes it: it shakes (drawn on its own meanwhile).
        public void Shake()
        {
            bool was = shake > 0f;
            shake = shakeTurns ? 0.3f : 0.2f;
            enabled = true;
            if (!was)
                Changed();
        }

        // ---- Fishing spots: rings spread out over the water and bubbles rise ----

        void BuildRipples()
        {
            rings = new Transform[3];
            for (int i = 0; i < rings.Length; i++)
                rings[i] = Visuals.Prim(PrimitiveType.Cylinder, transform, new Vector3(0f, 0.045f, 0f), Vector3.one, new Color(0.85f, 0.93f, 1f)).transform;
            bubbles = new Transform[5];
            for (int i = 0; i < bubbles.Length; i++)
                bubbles[i] = Visuals.Prim(PrimitiveType.Sphere, transform, Vector3.zero, Vector3.one * 0.06f, new Color(0.9f, 0.96f, 1f)).transform;
            var box = gameObject.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = new Vector3(0f, 0.1f, 0f);
            box.size = new Vector3(0.95f, 0.3f, 0.95f);
            hitbox = box;
        }

        void MoveTo(Tile tile)
        {
            Tile = tile;
            transform.localPosition = World.Local(tile);
            nextMove = Time.time + Random.Range(Def.MoveMinSeconds, Def.MoveMaxSeconds);
        }

        void AnimateRipples()
        {
            for (int i = 0; i < rings.Length; i++)
            {
                float t = (Time.time * 0.6f + i / (float)rings.Length) % 1f;
                float s = 0.2f + t * 0.75f;
                rings[i].localScale = new Vector3(s, 0.004f * (1f - t) + 0.001f, s);
            }
            for (int i = 0; i < bubbles.Length; i++)
            {
                float t = (Time.time * 0.8f + i * 0.37f) % 1f;
                float a = i * 2.4f;
                bubbles[i].localPosition = new Vector3(Mathf.Cos(a) * 0.25f, 0.03f + t * 0.12f, Mathf.Sin(a) * 0.25f);
            }
        }

        /// Moves to another shore tile no other spot is on.
        void Wander()
        {
            if (shore == null || shore.Count < 2 || Def.MoveMaxSeconds <= 0f)
                return;
            var nodes = Game.Instance.World.Nodes;
            for (int tries = 0; tries < 10; tries++)
            {
                var t = shore[Random.Range(0, shore.Count)];
                bool taken = t == Tile;
                foreach (var other in nodes)
                    if (other != this && other.Tile == t)
                        taken = true;
                if (!taken)
                {
                    MoveTo(t);
                    return;
                }
            }
            nextMove = Time.time + 30f;
        }

        void Update()
        {
            if (rings != null)
            {
                AnimateRipples();
                if (Def.MoveMaxSeconds > 0f && Time.time >= nextMove)
                    Wander();
            }
            if (IsEmpty && Time.time >= refillAt)
                Refill();
            if (shake > 0f && shaker != null)
            {
                shake -= Time.deltaTime;
                if (shake <= 0f)
                    Changed();  // still again: back into the merged mesh
                if (shakeTurns)
                {
                    float a = Mathf.Sin(shake * 60f) * shake * 12f;
                    shaker.localRotation = shake > 0f ? Quaternion.Euler(a, 0f, a * 0.5f) : Quaternion.identity;
                }
                else
                {
                    shaker.localPosition = shake > 0f ? new Vector3(Mathf.Sin(shake * 90f) * shake * 0.15f, 0f, 0f) : Vector3.zero;
                }
            }
            // Idle (not a fishing spot, not growing back, not shaking): stop being updated every frame.
            if (rings == null && !IsEmpty && shake <= 0f)
                enabled = false;
        }
    }
}
