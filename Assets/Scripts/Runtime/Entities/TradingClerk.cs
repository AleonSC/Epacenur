using Epacenur.Core;
using UnityEngine;

namespace Epacenur.Runtime
{
    /// The clerk at a city's trading post: behind a counter, with the board of offers beside them. "Trade"
    /// opens the trading post.
    public sealed class TradingClerk : Interactable
    {
        public override string Action => "Trade";
        public override string DisplayName => "Trading clerk";
        public override string Examine => "Keeps the trading post's board of offers.";
        public override void Interact() => Game.Instance.Hud.OpenWindow(new TradingPostWindow(), this);

        Quaternion facing;

        public static TradingClerk Create(Tile tile, Transform parent)
        {
            var go = new GameObject("Trading clerk " + tile);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = World.Local(tile);
            go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);  // faces south, over the square
            var npc = go.AddComponent<TradingClerk>();
            npc.Tile = tile;
            npc.facing = go.transform.localRotation;
            npc.BuildModel();
            return npc;
        }

        void BuildModel()
        {
            var t = transform;
            var skin = new Color(0.8f, 0.62f, 0.48f);
            var coat = new Color(0.18f, 0.24f, 0.45f);
            Visuals.Prim(PrimitiveType.Capsule, t, new Vector3(0f, 0.95f, 0f), new Vector3(0.58f, 0.62f, 0.44f), coat);
            Visuals.Prim(PrimitiveType.Cube, t, new Vector3(0f, 1.15f, 0.21f), new Vector3(0.1f, 0.4f, 0.04f), new Color(0.95f, 0.85f, 0.4f));  // gold sash
            Visuals.Prim(PrimitiveType.Cube, t, new Vector3(0f, 0.3f, 0f), new Vector3(0.44f, 0.5f, 0.3f), new Color(0.2f, 0.18f, 0.16f));
            Visuals.Prim(PrimitiveType.Sphere, t, new Vector3(0f, 1.64f, 0f), new Vector3(0.38f, 0.4f, 0.38f), skin);
            Visuals.Prim(PrimitiveType.Cylinder, t, new Vector3(0f, 1.9f, 0f), new Vector3(0.34f, 0.1f, 0.34f), new Color(0.12f, 0.12f, 0.14f));  // cap
            Visuals.Prim(PrimitiveType.Capsule, t, new Vector3(-0.36f, 1.1f, 0f), new Vector3(0.15f, 0.3f, 0.15f), coat);
            Visuals.Prim(PrimitiveType.Capsule, t, new Vector3(0.36f, 1.1f, 0f), new Vector3(0.15f, 0.3f, 0.15f), coat);

            var col = gameObject.AddComponent<CapsuleCollider>();
            col.radius = 0.4f;
            col.height = 2.1f;
            col.center = new Vector3(0f, 1.05f, 0f);
        }

        /// The booth: a counter in front of the clerk and a notice board of offers behind, pinned with papers.
        public static void Booth(Tile tile, Transform parent)
        {
            var root = new GameObject("Trading post").transform;
            root.SetParent(parent, false);
            root.localPosition = World.Local(tile);
            var wood = new Color(0.45f, 0.3f, 0.17f);
            Visuals.Prim(PrimitiveType.Cube, root, new Vector3(0f, 0.5f, -0.42f), new Vector3(0.98f, 1f, 0.14f), wood);
            Visuals.Prim(PrimitiveType.Cube, root, new Vector3(0f, 1.03f, -0.42f), new Vector3(1.04f, 0.06f, 0.24f), wood * 1.2f);
            Visuals.Prim(PrimitiveType.Cube, root, new Vector3(-0.42f, 1.3f, 0.42f), new Vector3(0.08f, 2.6f, 0.08f), wood);
            Visuals.Prim(PrimitiveType.Cube, root, new Vector3(0.42f, 1.3f, 0.42f), new Vector3(0.08f, 2.6f, 0.08f), wood);
            Visuals.Prim(PrimitiveType.Cube, root, new Vector3(0f, 2.1f, 0.42f), new Vector3(0.9f, 0.8f, 0.05f), new Color(0.55f, 0.4f, 0.24f));
            var papers = new[] { new Color(0.95f, 0.93f, 0.85f), new Color(0.92f, 0.85f, 0.65f) };
            for (int i = 0; i < 6; i++)
                Visuals.Prim(PrimitiveType.Cube, root, new Vector3(-0.28f + (i % 3) * 0.28f, 1.95f + (i / 3) * 0.32f, 0.39f),
                    new Vector3(0.2f, 0.24f, 0.01f), papers[i % 2]);
            Visuals.Prim(PrimitiveType.Cube, root, new Vector3(0f, 2.62f, 0.42f), new Vector3(1.1f, 0.12f, 0.2f), wood * 0.8f);  // a little roof
        }

        void Update()
        {
            Vector3 toPlayer = Game.Instance.Player.transform.position - transform.position;
            toPlayer.y = 0f;
            Quaternion want = toPlayer.magnitude < 4f && toPlayer.sqrMagnitude > 0.01f ? Quaternion.LookRotation(toPlayer) : facing;
            transform.rotation = Quaternion.Slerp(transform.rotation, want, 1f - Mathf.Exp(-5f * Time.deltaTime));
        }
    }
}
