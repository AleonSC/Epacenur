using Epacenur.Core;
using UnityEngine;

namespace Epacenur.Runtime
{
    /// A rock with ore in it. "Mine" it with a pickaxe: one ore per success, then it's empty
    /// (the coloured streaks vanish) until it refills a few seconds later.
    public sealed class RockObject : Interactable
    {
        public RockDef Def { get; private set; }
        public bool IsEmpty { get; private set; }
        public override string Action => "Mine";
        public override string DisplayName => IsEmpty ? "Rocks" : Def.Name;
        public override string Examine => IsEmpty ? "There is currently no ore available in this rock." : "A rock containing " + ItemDefs.Get(Def.OreItemId).Name.Split(' ')[0].ToLowerInvariant() + ".";
        public override void Interact() => Game.Instance.Player.MineAt(this);

        GameObject ore;
        Transform body;
        float refillAt, shake;

        public static RockObject Create(RockDef def, Tile tile, Transform parent, System.Random rng)
        {
            var go = new GameObject(def.Name + " " + tile);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = World.TileCenter(tile);
            go.transform.localRotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
            var rock = go.AddComponent<RockObject>();
            rock.Def = def;
            rock.Tile = tile;
            rock.Build(rng);
            return rock;
        }

        void Build(System.Random rng)
        {
            body = new GameObject("Rock").transform;
            body.SetParent(transform, false);
            var grey = new Color(0.5f, 0.48f, 0.45f);
            var dark = new Color(0.42f, 0.4f, 0.38f);
            Visuals.Prim(PrimitiveType.Sphere, body, new Vector3(0f, 0.32f, 0f), new Vector3(0.95f, 0.7f, 0.85f), grey);
            Visuals.Prim(PrimitiveType.Sphere, body, new Vector3(0.22f, 0.2f, 0.18f), new Vector3(0.55f, 0.45f, 0.55f), dark);
            Visuals.Prim(PrimitiveType.Sphere, body, new Vector3(-0.25f, 0.18f, -0.1f), new Vector3(0.5f, 0.4f, 0.55f), dark);
            // Streaks of ore on the surface.
            ore = new GameObject("Ore");
            ore.transform.SetParent(body, false);
            var oreColour = new Color(Def.OreR, Def.OreG, Def.OreB);
            for (int i = 0; i < 6; i++)
            {
                float a = i * 1.05f + (float)rng.NextDouble() * 0.4f;
                var pos = new Vector3(Mathf.Cos(a) * 0.38f, 0.35f + (i % 3) * 0.1f, Mathf.Sin(a) * 0.33f);
                Visuals.Prim(PrimitiveType.Sphere, ore.transform, pos, new Vector3(0.16f, 0.1f, 0.16f), oreColour);
            }
            var box = gameObject.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.35f, 0f);
            box.size = new Vector3(0.95f, 0.7f, 0.95f);
        }

        /// It gave its ore: empty until it refills.
        public void Deplete()
        {
            IsEmpty = true;
            ore.SetActive(false);
            refillAt = Time.time + Def.RespawnSeconds;
        }

        /// A pickaxe strikes it.
        public void Shake() => shake = 0.2f;

        void Update()
        {
            if (IsEmpty && Time.time >= refillAt)
            {
                IsEmpty = false;
                ore.SetActive(true);
            }
            if (shake > 0f)
            {
                shake -= Time.deltaTime;
                body.localPosition = new Vector3(Mathf.Sin(shake * 90f) * shake * 0.15f, 0f, 0f);
            }
        }
    }
}
