using System.Collections.Generic;
using Epacenur.Core;
using UnityEngine;

namespace Epacenur.Runtime
{
    /// Life about the land that's only there to be seen: butterflies over the meadows by day, flocks of birds
    /// crossing the sky, and fireflies over the grass at night. Only out in the overworld, never in a storm; the
    /// butterflies and fireflies keep in to calm, dry weather.
    public sealed class AmbientLife : MonoBehaviour
    {
        const int MaxButterflies = 10, MaxFireflies = 18;
        const float Near = 22f, TooFar = 34f;

        sealed class Critter
        {
            public Transform Body, WingL, WingR;
            public Vector3 Pos, Target;     // map-local (tile coordinates)
            public float Phase, Speed, Retarget;
        }

        sealed class Flock
        {
            public readonly List<Critter> Birds = new List<Critter>();
            public Vector3 Dir;
            public float Left;
        }

        readonly List<Critter> butterflies = new List<Critter>(), fireflies = new List<Critter>();
        readonly List<Flock> flocks = new List<Flock>();
        float nextFlock = 15f;
        Transform holder;

        static readonly Color[] Wings =
        {
            new Color(0.98f, 0.82f, 0.2f), new Color(0.95f, 0.95f, 0.95f), new Color(0.95f, 0.5f, 0.15f),
            new Color(0.45f, 0.6f, 1f), new Color(0.85f, 0.45f, 0.85f),
        };

        void Update()
        {
            var g = Game.Instance;
            if (g == null || g.Player == null || g.World == null)
                return;
            // Kept on the map, so they move with it when the origin shifts.
            if (holder == null || holder.parent != g.World.transform)
            {
                if (holder != null)
                    Destroy(holder.gameObject);
                holder = new GameObject("Ambient life").transform;
                holder.SetParent(g.World.transform, false);
                butterflies.Clear();
                fireflies.Clear();
                flocks.Clear();
            }
            bool outside = g.World.Map == MapId.Overworld;
            var here = g.Weather != null ? g.Weather.Here : new LocalWeather();
            bool calm = here.Rain + here.Snow < 0.1 && here.Wind < 0.7 && here.Storm < 0.2;
            bool day = !g.Clock.IsNight && g.Clock.Time > 0.27f && g.Clock.Time < 0.76f;
            var tile = g.Player.CurrentTile;
            var ow = g.World.Overworld;
            var biome = outside ? ow.BiomeAt(tile) : Biome.Wasteland;
            bool green = biome == Biome.Meadow || biome == Biome.Woodland || biome == Biome.Marsh;
            var me = new Vector3(tile.X, 0f, tile.Z);
            float dt = Time.deltaTime;

            int wantButterflies = outside && day && calm && green ? (biome == Biome.Meadow ? MaxButterflies : MaxButterflies / 2) : 0;
            int wantFireflies = outside && g.Clock.IsNight && calm && green && biome != Biome.Woodland ? MaxFireflies : 0;
            Keep(butterflies, wantButterflies, me, ow, false);
            Keep(fireflies, wantFireflies, me, ow, true);
            foreach (var b in butterflies)
                Flutter(b, ow, dt, 0.45f, 1.4f);
            foreach (var f in fireflies)
            {
                Flutter(f, ow, dt, 0.4f, 0.4f);
                float blink = Mathf.Sin(Time.time * 2.1f + f.Phase * 7f);
                f.Body.localScale = Vector3.one * (blink > 0.2f ? 0.08f : 0.02f);
            }

            // Now and then a flock of birds flies over, by day, out of a storm.
            nextFlock -= dt;
            if (nextFlock <= 0f)
            {
                nextFlock = Random.Range(25f, 70f);
                if (outside && day && here.Storm < 0.3 && here.Rain < 0.6 && flocks.Count < 2)
                    flocks.Add(NewFlock(me, ow));
            }
            for (int i = flocks.Count - 1; i >= 0; i--)
            {
                var fl = flocks[i];
                fl.Left -= dt;
                foreach (var bird in fl.Birds)
                {
                    bird.Pos += fl.Dir * bird.Speed * dt;
                    float flap = Mathf.Sin(Time.time * 9f + bird.Phase) * 35f;
                    bird.WingL.localRotation = Quaternion.Euler(0f, 0f, flap);
                    bird.WingR.localRotation = Quaternion.Euler(0f, 0f, -flap);
                    bird.Body.localPosition = bird.Pos + Vector3.up * Mathf.Sin(Time.time * 1.3f + bird.Phase) * 0.3f;
                }
                if (fl.Left <= 0f || !outside)
                {
                    foreach (var bird in fl.Birds)
                        Destroy(bird.Body.gameObject);
                    flocks.RemoveAt(i);
                }
            }
        }

        /// Keeps `want` of them about (adding new ones round you, taking away ones left far behind or not wanted).
        void Keep(List<Critter> list, int want, Vector3 me, Overworld ow, bool glow)
        {
            for (int i = list.Count - 1; i >= 0; i--)
            {
                var c = list[i];
                var off = c.Pos - me;
                off.y = 0f;
                if (list.Count > want || off.magnitude > TooFar)
                {
                    Destroy(c.Body.gameObject);
                    list.RemoveAt(i);
                }
            }
            while (list.Count < want)
                list.Add(glow ? NewFirefly(me, ow) : NewButterfly(me, ow));
        }

        Vector3 SpotNear(Vector3 me, Overworld ow, float height)
        {
            float ang = Random.value * Mathf.PI * 2f, r = Random.Range(3f, Near);
            float x = me.x + Mathf.Cos(ang) * r, z = me.z + Mathf.Sin(ang) * r;
            return new Vector3(x, (float)ow.Elevation(x, z) + height, z);
        }

        Critter NewButterfly(Vector3 me, Overworld ow)
        {
            var c = new Critter { Phase = Random.value * 10f, Speed = Random.Range(0.8f, 1.4f) };
            c.Body = new GameObject("Butterfly").transform;
            c.Body.SetParent(holder, false);
            var colour = Wings[Random.Range(0, Wings.Length)];
            Visuals.Prim(PrimitiveType.Cube, c.Body, Vector3.zero, new Vector3(0.025f, 0.025f, 0.1f), new Color(0.15f, 0.12f, 0.1f));
            c.WingL = Wing(c.Body, -1f, colour);
            c.WingR = Wing(c.Body, 1f, colour);
            c.Pos = SpotNear(me, ow, Random.Range(0.4f, 1.4f));
            c.Target = c.Pos;
            c.Body.localPosition = c.Pos;
            return c;
        }

        static Transform Wing(Transform body, float side, Color colour)
        {
            var hinge = new GameObject("Wing").transform;
            hinge.SetParent(body, false);
            var w = Visuals.Prim(PrimitiveType.Cube, hinge, new Vector3(side * 0.07f, 0f, 0f), new Vector3(0.13f, 0.005f, 0.12f), colour);
            w.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return hinge;
        }

        Critter NewFirefly(Vector3 me, Overworld ow)
        {
            var c = new Critter { Phase = Random.value * 10f, Speed = Random.Range(0.3f, 0.6f) };
            var go = Visuals.Prim(PrimitiveType.Sphere, holder, Vector3.zero, Vector3.one * 0.08f, new Color(0.85f, 1f, 0.4f));
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = ModelBuilder.GlowMaterial(new Color(0.85f, 1f, 0.4f));
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            c.Body = go.transform;
            c.Pos = SpotNear(me, ow, Random.Range(0.3f, 1.6f));
            c.Target = c.Pos;
            c.Body.localPosition = c.Pos;
            return c;
        }

        /// Drifts towards a spot near the ground, picking a new one now and then; wings (if any) flap.
        void Flutter(Critter c, Overworld ow, float dt, float low, float high)
        {
            c.Retarget -= dt;
            if (c.Retarget <= 0f || (c.Target - c.Pos).magnitude < 0.2f)
            {
                c.Retarget = Random.Range(1.5f, 4f);
                float x = c.Pos.x + Random.Range(-4f, 4f), z = c.Pos.z + Random.Range(-4f, 4f);
                c.Target = new Vector3(x, (float)ow.Elevation(x, z) + Random.Range(low, high), z);
            }
            var step = c.Target - c.Pos;
            var move = step.normalized * Mathf.Min(step.magnitude, c.Speed * dt);
            move.y += Mathf.Sin(Time.time * 6f + c.Phase) * 0.6f * dt;  // bobbing
            c.Pos += move;
            c.Body.localPosition = c.Pos;
            if (move.sqrMagnitude > 1e-6f)
                c.Body.localRotation = Quaternion.LookRotation(new Vector3(move.x, 0f, move.z).normalized + Vector3.forward * 1e-4f);
            if (c.WingL != null)
            {
                float flap = Mathf.Sin(Time.time * 22f + c.Phase) * 60f;
                c.WingL.localRotation = Quaternion.Euler(0f, 0f, flap);
                c.WingR.localRotation = Quaternion.Euler(0f, 0f, -flap);
            }
        }

        /// A flock of 3-7 birds in a V, high up, flying over you from one side to the other.
        Flock NewFlock(Vector3 me, Overworld ow)
        {
            var fl = new Flock();
            float ang = Random.value * Mathf.PI * 2f;
            fl.Dir = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
            var side = new Vector3(-fl.Dir.z, 0f, fl.Dir.x);
            var start = me - fl.Dir * 90f + side * Random.Range(-25f, 25f);
            float y = (float)ow.Elevation(me.x, me.z) + Random.Range(22f, 34f);
            int n = Random.Range(3, 8);
            float speed = Random.Range(7f, 10f);
            fl.Left = 180f / speed;
            var dark = new Color(0.12f, 0.12f, 0.14f);
            for (int i = 0; i < n; i++)
            {
                var b = new Critter { Phase = Random.value * 10f, Speed = speed };
                b.Body = new GameObject("Bird").transform;
                b.Body.SetParent(holder, false);
                b.Body.localRotation = Quaternion.LookRotation(fl.Dir);
                Visuals.Prim(PrimitiveType.Capsule, b.Body, Vector3.zero, new Vector3(0.18f, 0.18f, 0.4f), dark).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                b.WingL = BirdWing(b.Body, -1f, dark);
                b.WingR = BirdWing(b.Body, 1f, dark);
                int row = (i + 1) / 2, s = i % 2 == 0 ? 1 : -1;
                b.Pos = new Vector3(start.x, y, start.z) - fl.Dir * row * 1.6f + side * s * row * 1.4f;
                b.Body.localPosition = b.Pos;
                fl.Birds.Add(b);
            }
            return fl;
        }

        static Transform BirdWing(Transform body, float side, Color c)
        {
            var hinge = new GameObject("Wing").transform;
            hinge.SetParent(body, false);
            Visuals.Prim(PrimitiveType.Cube, hinge, new Vector3(side * 0.35f, 0f, 0f), new Vector3(0.6f, 0.03f, 0.22f), c);
            return hinge;
        }

        void OnDestroy()
        {
            if (holder != null)
                Destroy(holder.gameObject);
        }
    }
}
