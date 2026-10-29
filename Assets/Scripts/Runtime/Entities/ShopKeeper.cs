using Epacenur.Core;
using UnityEngine;

namespace Epacenur.Runtime
{
    /// An NPC standing on a tile who runs a shop (from a ShopDef). "Trade" opens the shop window.
    public sealed class ShopKeeper : Interactable
    {
        /// Seconds between restock steps (each moves every item's stock one closer to normal).
        public const float RestockSeconds = 6f;

        public Shop Shop { get; private set; }
        public override string Action => "Trade";
        public override string DisplayName => Shop.Def.KeeperName;
        public override string Examine => Shop.Def.KeeperExamine;
        public override void Interact() => Game.Instance.Hud.OpenShop(this);

        float nextRestock;
        Quaternion facing;

        public static ShopKeeper Create(Tile tile, Shop shop, Transform parent)
        {
            var go = new GameObject("Shop keeper " + tile);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = World.Local(tile);
            go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);  // faces south, towards spawn
            var npc = go.AddComponent<ShopKeeper>();
            npc.Tile = tile;
            npc.Shop = shop;
            npc.facing = go.transform.localRotation;
            npc.BuildModel();
            return npc;
        }

        /// Which way it stands when nobody's close (towards the shop door).
        public void SetFacing(Quaternion rotation)
        {
            facing = rotation;
            transform.rotation = rotation;
        }

        void BuildModel()
        {
            var t = transform;
            var skin = new Color(0.85f, 0.66f, 0.5f);
            Visuals.Prim(PrimitiveType.Capsule, t, new Vector3(0f, 0.95f, 0f), new Vector3(0.6f, 0.62f, 0.45f), new Color(0.55f, 0.18f, 0.16f));
            Visuals.Prim(PrimitiveType.Cube, t, new Vector3(0f, 0.85f, 0.2f), new Vector3(0.44f, 0.75f, 0.06f), new Color(0.92f, 0.9f, 0.82f)); // apron
            Visuals.Prim(PrimitiveType.Cube, t, new Vector3(0f, 0.3f, 0f), new Vector3(0.44f, 0.5f, 0.3f), new Color(0.25f, 0.22f, 0.2f));
            Visuals.Prim(PrimitiveType.Sphere, t, new Vector3(0f, 1.64f, 0f), new Vector3(0.38f, 0.4f, 0.38f), skin);
            Visuals.Prim(PrimitiveType.Cube, t, new Vector3(0f, 1.6f, 0.15f), new Vector3(0.24f, 0.16f, 0.12f), new Color(0.4f, 0.26f, 0.14f)); // beard
            Visuals.Prim(PrimitiveType.Cylinder, t, new Vector3(0f, 1.86f, 0f), new Vector3(0.36f, 0.04f, 0.36f), new Color(0.2f, 0.3f, 0.2f));     // hat brim
            Visuals.Prim(PrimitiveType.Cylinder, t, new Vector3(0f, 1.97f, 0f), new Vector3(0.24f, 0.1f, 0.24f), new Color(0.2f, 0.3f, 0.2f));
            Visuals.Prim(PrimitiveType.Capsule, t, new Vector3(-0.36f, 1.1f, 0f), new Vector3(0.15f, 0.3f, 0.15f), skin);
            Visuals.Prim(PrimitiveType.Capsule, t, new Vector3(0.36f, 1.1f, 0f), new Vector3(0.15f, 0.3f, 0.15f), skin);

            var col = gameObject.AddComponent<CapsuleCollider>();
            col.radius = 0.4f;
            col.height = 2.1f;
            col.center = new Vector3(0f, 1.05f, 0f);
        }

        void Update()
        {
            if (Time.time >= nextRestock)
            {
                nextRestock = Time.time + RestockSeconds;
                Shop.Restock();
            }

            // Turn to face the player when they come close, otherwise back to the counter.
            Vector3 toPlayer = Game.Instance.Player.transform.position - transform.position;
            toPlayer.y = 0f;
            Quaternion want = toPlayer.magnitude < 4f && toPlayer.sqrMagnitude > 0.01f ? Quaternion.LookRotation(toPlayer) : facing;
            transform.rotation = Quaternion.Slerp(transform.rotation, want, 1f - Mathf.Exp(-5f * Time.deltaTime));
        }
    }
}
