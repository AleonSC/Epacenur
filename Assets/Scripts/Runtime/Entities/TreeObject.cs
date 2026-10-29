using Epacenur.Core;
using UnityEngine;

namespace Epacenur.Runtime
{
    /// A tree on one tile. Each successful chop takes a life; at zero it becomes a stump,
    /// then grows back after its TreeDef's respawn time.
    public sealed class TreeObject : MonoBehaviour
    {
        public TreeDef Def { get; private set; }
        public Tile Tile { get; private set; }
        public int LivesLeft { get; private set; }
        public bool IsStump { get; private set; }

        GameObject alive, stump;
        Transform crown;
        CapsuleCollider hitbox;
        float regrowAt, shake;
        System.Random rng;

        public static TreeObject Create(TreeDef def, Tile tile, Transform parent, System.Random rng)
        {
            var go = new GameObject(def.Name + " " + tile);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = World.TileCenter(tile);
            go.transform.localRotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
            var tree = go.AddComponent<TreeObject>();
            tree.Def = def;
            tree.Tile = tile;
            tree.rng = rng;
            tree.BuildModel((float)rng.NextDouble());
            tree.Regrow();
            return tree;
        }

        void BuildModel(float variety)
        {
            var bark = Visuals.Rgb(Def.TrunkR, Def.TrunkG, Def.TrunkB);
            var leaf = Visuals.Rgb(Def.LeafR, Def.LeafG, Def.LeafB);
            var leafDark = leaf * 0.82f;
            leafDark.a = 1f;
            float h = 2.2f + variety * 0.6f;

            alive = new GameObject("Alive");
            alive.transform.SetParent(transform, false);
            Visuals.Prim(PrimitiveType.Cylinder, alive.transform, new Vector3(0f, h / 2, 0f), new Vector3(0.36f, h / 2, 0.36f), bark);
            crown = new GameObject("Crown").transform;
            crown.SetParent(alive.transform, false);
            crown.localPosition = new Vector3(0f, h, 0f);
            Visuals.Prim(PrimitiveType.Sphere, crown, new Vector3(0f, 0.35f, 0f), new Vector3(1.9f, 1.6f, 1.9f), leaf);
            Visuals.Prim(PrimitiveType.Sphere, crown, new Vector3(0.45f, -0.1f, 0.3f), new Vector3(1.2f, 1.1f, 1.2f), leafDark);
            Visuals.Prim(PrimitiveType.Sphere, crown, new Vector3(-0.4f, 0.05f, -0.35f), new Vector3(1.25f, 1.1f, 1.25f), leafDark);


            // One collider for clicking (tile mode), aiming (first person) and blocking WASD movement.
            hitbox = gameObject.AddComponent<CapsuleCollider>();
            hitbox.radius = 0.45f;
        }

        // The stump is only built the first time the tree is chopped down (there are a lot of trees).
        void BuildStump()
        {
            var bark = Visuals.Rgb(Def.TrunkR, Def.TrunkG, Def.TrunkB);
            stump = new GameObject("Stump");
            stump.transform.SetParent(transform, false);
            Visuals.Prim(PrimitiveType.Cylinder, stump.transform, new Vector3(0f, 0.18f, 0f), new Vector3(0.42f, 0.18f, 0.42f), bark);
            Visuals.Prim(PrimitiveType.Cylinder, stump.transform, new Vector3(0f, 0.365f, 0f), new Vector3(0.36f, 0.01f, 0.36f), new Color(0.78f, 0.64f, 0.42f));
        }

        /// Chopped down: becomes a stump.
        void Fell()
        {
            if (stump == null)
                BuildStump();
            IsStump = true;
            LivesLeft = 0;
            alive.SetActive(false);
            stump.SetActive(true);
            hitbox.height = 0.9f;
            hitbox.center = new Vector3(0f, 0.45f, 0f);
            regrowAt = Time.time + Def.RespawnSeconds;
        }

        public void Regrow()
        {
            IsStump = false;
            LivesLeft = rng.Next(Def.MinLives, Def.MaxLives + 1);
            alive.SetActive(true);
            if (stump != null)
                stump.SetActive(false);
            hitbox.height = 3.6f;
            hitbox.center = new Vector3(0f, 1.8f, 0f);
        }

        /// A successful chop: one life less. Returns true when that felled the tree.
        public bool TakeLife()
        {
            if (IsStump)
                return true;
            LivesLeft--;
            if (LivesLeft > 0)
                return false;
            Fell();
            return true;
        }

        /// Wobble the leaves when an axe hits.
        public void Shake() => shake = 0.3f;

        void Update()
        {
            if (IsStump && Time.time >= regrowAt)
                Regrow();
            if (shake > 0f)
            {
                shake -= Time.deltaTime;
                float a = Mathf.Sin(shake * 60f) * shake * 12f;
                crown.localRotation = Quaternion.Euler(a, 0f, a * 0.5f);
                if (shake <= 0f)
                    crown.localRotation = Quaternion.identity;
            }
        }
    }
}
