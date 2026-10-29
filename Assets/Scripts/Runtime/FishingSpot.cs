using System.Collections.Generic;
using Epacenur.Core;
using UnityEngine;

namespace Epacenur.Runtime
{
    /// Rippling water where fish can be caught. It sits on a water tile and you fish it from the bank
    /// next to it. Every so often it moves to another spot along the shore, as in RuneScape.
    public sealed class FishingSpot : Interactable
    {
        public FishingSpotDef Def { get; private set; }
        public override string Action => Def.Action;
        public override string DisplayName => Def.Name;
        public override string Examine => Def.Examine;
        public override void Interact() => Game.Instance.Player.FishAt(this);

        List<Tile> shore;  // water tiles it can move between
        Transform[] rings;
        Transform[] bubbles;
        float nextMove;

        public static FishingSpot Create(FishingSpotDef def, Tile tile, List<Tile> shore, Transform parent)
        {
            var go = new GameObject(def.Name);
            go.transform.SetParent(parent, false);
            var spot = go.AddComponent<FishingSpot>();
            spot.Def = def;
            spot.shore = shore;
            spot.Build();
            spot.MoveTo(tile);
            return spot;
        }

        void Build()
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
        }

        void MoveTo(Tile tile)
        {
            Tile = tile;
            transform.localPosition = World.TileCenter(tile);
            nextMove = Time.time + Random.Range(40f, 90f);
        }

        void Update()
        {
            // Rings spread out and fade (shrink back), bubbles bob up.
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

            if (Time.time >= nextMove)
            {
                // Move to another free shore tile.
                var spots = Game.Instance.World.FishingSpots;
                for (int tries = 0; tries < 10; tries++)
                {
                    var t = shore[Random.Range(0, shore.Count)];
                    bool taken = false;
                    foreach (var other in spots)
                        if (other != this && other.Tile == t)
                            taken = true;
                    if (!taken && t != Tile)
                    {
                        MoveTo(t);
                        return;
                    }
                }
                nextMove = Time.time + 30f;
            }
        }
    }
}
