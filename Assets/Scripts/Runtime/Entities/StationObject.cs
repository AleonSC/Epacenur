using System.Collections.Generic;
using Epacenur.Core;
using UnityEngine;

namespace Epacenur.Runtime
{
    /// A fixed object you use, built from a StationDef: a bank booth, an altar, a furnace, an anvil.
    /// What using it does depends on its kind (StationKind). Fires are stations too (Fire.cs).
    public class StationObject : Interactable
    {
        public StationDef Def { get; private set; }
        public override string Action => Def.Action;
        public override string DisplayName => Def.Name;
        public override string Examine => Def.Examine;

        public override void Interact()
        {
            var g = Game.Instance;
            switch (Def.Kind)
            {
                case StationKind.Bank:
                    g.Hud.OpenBank(this);
                    break;
                case StationKind.Altar:
                    g.Player.Kneel(1.2f);
                    if (g.Prayer.Points >= g.Prayer.Max)
                    {
                        GameLog.Add("You already have full Prayer points.");
                        return;
                    }
                    g.Prayer.Recharge();
                    GameLog.Add("You pray at the altar... You recharge your Prayer points.");
                    break;
                case StationKind.MakeWindow:
                    g.Hud.OpenMaking(this);
                    break;
                default:
                    g.Player.StartMaking(this, null, int.MaxValue);
                    break;
            }
        }

        Light glow;
        float lightIntensity;
        readonly List<Transform> flames = new List<Transform>();
        readonly List<Vector3> flameSizes = new List<Vector3>();
        float seed;

        public static StationObject Create(StationDef def, Tile tile, Transform parent)
        {
            var go = new GameObject(def.Name + " " + tile);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = World.TileCenter(tile);
            var station = go.AddComponent<StationObject>();
            station.Init(def, tile);
            ModelBuilder.AddBox(go, def.Model);
            return station;
        }

        protected void Init(StationDef def, Tile tile)
        {
            Def = def;
            Tile = tile;
            seed = Random.value * 10f;
            var model = ModelBuilder.Build(def.Model, transform);
            // Glowing parts taller than they're wide are flames (candles): they flicker.
            foreach (var r in model.GetComponentsInChildren<Renderer>())
            {
                var s = r.transform.localScale;
                if (r.gameObject.name == "Glow" && s.y > s.x)
                {
                    flames.Add(r.transform);
                    flameSizes.Add(s);
                }
            }
            if (def.HasLight)
                glow = AddLight(new Vector3(0f, 0.5f, -0.8f), ModelBuilder.ToColor(def.LightColour), 3.5f, 1.2f);
        }

        protected Light AddLight(Vector3 at, Color colour, float range, float intensity)
        {
            var light = new GameObject("Light").AddComponent<Light>();
            light.transform.SetParent(transform, false);
            light.transform.localPosition = at;
            light.type = LightType.Point;
            light.color = colour;
            light.range = range;
            light.intensity = intensity;
            glow = light;
            lightIntensity = intensity;
            return light;
        }

        protected virtual void Update()
        {
            float time = Time.time + seed;
            if (glow != null)
                glow.intensity = lightIntensity * (0.95f + Mathf.Sin(time * 7f) * 0.12f);
            for (int i = 0; i < flames.Count; i++)
            {
                var s = flameSizes[i];
                flames[i].localScale = new Vector3(s.x, s.y * (1f + Mathf.Sin(time * (11f + i * 4f)) * 0.15f), s.z);
            }
        }
    }
}
