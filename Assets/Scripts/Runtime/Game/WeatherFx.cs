using Epacenur.Core;
using UnityEngine;

namespace Epacenur.Runtime
{
    /// Shows the weather (Core: Weather) where you are: clouds darken the sky and soften the shadows, mist pulls
    /// the fog in, rain and snow fall round you (not indoors), storms flash and thunder, the rain patters, and
    /// the wind bends the grass and trees (the Sway shader reads it). Nothing of it underground.
    public sealed class WeatherFx : MonoBehaviour
    {
        const int MaxDrops = 1500;
        const float Box = 26f;          // drops fall in a box this far either side of the camera
        const float Settle = 3f;        // seconds for the weather here to catch up with a change (walking into a marsh...)

        public Weather Weather { get; private set; }
        /// The weather here now (eased).
        public LocalWeather Here;

        Mesh mesh;
        Vector3[] verts, drops;
        Color[] colours;
        float[] speeds;
        int active;
        bool snowing;
        AudioSource rain;
        float flash, nextThunder = 8f, thunderIn = -1f, thunderLoud;
        System.Random rng;

        public static WeatherFx Create(GameObject host, int seed)
        {
            var fx = host.AddComponent<WeatherFx>();
            fx.Init(seed);
            return fx;
        }

        void Init(int seed)
        {
            rng = new System.Random(seed);
            Weather = new Weather(seed);
            Weather.Changed += (from, to) =>
            {
                string say = Weather.Describe(from, to);
                var g = Game.Instance;
                if (say != null && g != null && g.World.Map != MapId.Dungeon)
                    GameLog.Add(say);
            };

            // Rain and snow: one mesh of quads round the camera, moved every frame.
            verts = new Vector3[MaxDrops * 4];
            colours = new Color[MaxDrops * 4];
            drops = new Vector3[MaxDrops];
            speeds = new float[MaxDrops];
            var tris = new int[MaxDrops * 6];
            for (int i = 0; i < MaxDrops; i++)
            {
                tris[i * 6] = i * 4; tris[i * 6 + 1] = i * 4 + 1; tris[i * 6 + 2] = i * 4 + 2;
                tris[i * 6 + 3] = i * 4 + 2; tris[i * 6 + 4] = i * 4 + 1; tris[i * 6 + 5] = i * 4 + 3;
                drops[i] = new Vector3(Rand(-Box, Box), Rand(-4f, 22f), Rand(-Box, Box));
                speeds[i] = Rand(0.8f, 1.2f);
            }
            mesh = new Mesh { name = "Precipitation", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.MarkDynamic();
            mesh.vertices = verts;
            mesh.colors = colours;
            mesh.triangles = tris;
            var go = new GameObject("Rain and snow");
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = Visuals.ParticleMat(Color.white) ?? Visuals.Mat(new Color(0.75f, 0.8f, 0.88f));
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;

            rain = gameObject.AddComponent<AudioSource>();
            rain.clip = Sfx.ClipFor(Sound.Rain);
            rain.loop = true;
            rain.playOnAwake = false;
            rain.spatialBlend = 0f;
            rain.volume = 0f;
        }

        float Rand(float a, float b) => a + (float)rng.NextDouble() * (b - a);

        void OnDestroy()
        {
            if (mesh != null)
                Destroy(mesh);
            Shader.SetGlobalVector("_EpWind", Vector4.zero);
        }

        void Update()
        {
            var g = Game.Instance;
            if (g == null || g.Player == null)
                return;
            float dt = Time.deltaTime;
            Weather.Advance(dt);

            // The weather where you stand.
            var tile = g.Player.CurrentTile;
            var want = new LocalWeather();
            bool indoors = false;
            if (g.World.Map == MapId.Overworld)
            {
                var ow = g.World.Overworld;
                want = Weather.At(ow.BiomeAt(tile), ow.Elevation(tile), g.Clock.Time, tile.X, tile.Z);
                var k = ow.At(tile);
                indoors = k == Terrain.Floor || k == Terrain.Door;
            }
            else if (g.World.Map == MapId.DevPlot)
                want = Weather.Overall;
            float ease = 1f - Mathf.Exp(-dt / Settle);
            Here.Clouds += (want.Clouds - Here.Clouds) * ease;
            Here.Rain += (want.Rain - Here.Rain) * ease;
            Here.Snow += (want.Snow - Here.Snow) * ease;
            Here.Mist += (want.Mist - Here.Mist) * ease;
            Here.Wind += (want.Wind - Here.Wind) * ease;
            Here.Storm += (want.Storm - Here.Storm) * ease;

            // The wind, for the swaying plants: the way it blows, gusting.
            float t = Time.time;
            float gust = 0.75f + 0.18f * Mathf.Sin(t * 0.63f) + 0.12f * Mathf.Sin(t * 1.71f + 1.3f);
            float strength = (0.15f + (float)Here.Wind) * gust;
            float angle = (float)Weather.WindAngle;
            var wind = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * strength;
            Shader.SetGlobalVector("_EpWind", new Vector4(wind.x, wind.y, strength, 0f));
            Shader.SetGlobalFloat("_EpTime", t);

            Storms(dt);
            g.Clock.Overcast = (float)Here.Clouds;
            g.Clock.Mist = Mathf.Clamp01((float)Here.Mist + 0.25f * (float)(Here.Rain + Here.Snow));
            g.Clock.Flash = flash;

            Precipitation(dt, indoors, wind, g.Player.transform.position.y);
            float loud = Sfx.Volume * Mathf.Clamp01((float)Here.Rain * 0.55f + (float)Here.Storm * 0.2f) * (indoors ? 0.45f : 1f);
            rain.volume = loud;
            if (loud > 0.001f && !rain.isPlaying)
                rain.Play();
            else if (loud <= 0.001f && rain.isPlaying)
                rain.Stop();
        }

        /// Lightning now and then in a storm (a flash or two), its thunder a moment later.
        void Storms(float dt)
        {
            flash = Mathf.Max(0f, flash - dt * 4f);
            if (thunderIn >= 0f)
            {
                thunderIn -= dt;
                if (thunderIn < 0f)
                    Sfx.Play(Sound.Thunder, thunderLoud, 0.12f);
            }
            if (Here.Storm < 0.4)
                return;
            nextThunder -= dt;
            if (nextThunder > 0f)
                return;
            nextThunder = Rand(6f, 20f);
            flash = 1f;
            if (rng.NextDouble() < 0.5)
                Invoke(nameof(Flicker), Rand(0.12f, 0.25f));
            thunderIn = Rand(0.4f, 2.4f);           // the further off, the later...
            thunderLoud = 1.1f - thunderIn * 0.25f;  // ...and the quieter
        }

        void Flicker() => flash = 0.8f;

        /// Rain (streaks along the way they fall) or snow (flakes drifting down, turning to face you), falling in a
        /// box that follows the camera; none indoors.
        void Precipitation(float dt, bool indoors, Vector2 wind, float ground)
        {
            var cam = Camera.main;
            double fall = Here.Rain + Here.Snow;
            snowing = Here.Snow > Here.Rain;
            int want = indoors || cam == null ? 0 : Mathf.RoundToInt(MaxDrops * Mathf.Clamp01((float)fall));
            if (want == 0 && active == 0)
                return;
            active = want;
            var eye = cam != null ? cam.transform.position : Vector3.zero;
            // The box stands on the ground where you are and reaches well over the camera.
            var c = new Vector3(eye.x, ground, eye.z);
            float top = Mathf.Max(14f, eye.y - ground + 8f);
            var right = cam != null ? cam.transform.right : Vector3.right;
            var up = cam != null ? cam.transform.up : Vector3.up;
            var fallVel = snowing ? new Vector3(wind.x * 1.5f, -1.6f, wind.y * 1.5f) : new Vector3(wind.x * 4f, -16f, wind.y * 4f);
            var rainCol = new Color(0.78f, 0.82f, 0.92f, 0.32f);
            var snowCol = new Color(1f, 1f, 1f, 0.9f);
            float t = Time.time;
            for (int i = 0; i < MaxDrops; i++)
            {
                int v = i * 4;
                if (i >= active)
                {
                    verts[v] = verts[v + 1] = verts[v + 2] = verts[v + 3] = c;
                    continue;
                }
                var d = drops[i];
                var vel = fallVel * speeds[i];
                if (snowing)
                    vel += new Vector3(Mathf.Sin(t * 1.3f + i) * 0.4f, 0f, Mathf.Cos(t * 1.1f + i * 0.7f) * 0.4f);
                d += vel * dt;
                // Keep it in the box round the camera: wrap round sideways; back to the top once it's fallen through.
                if (d.y < -2f || d.y > top + 2f)
                    d = new Vector3(Rand(-Box, Box), Rand(top - 8f, top), Rand(-Box, Box));
                d.x = Wrap(d.x);
                d.z = Wrap(d.z);
                drops[i] = d;
                var p = c + d;
                if (snowing)
                {
                    float s = 0.05f;
                    verts[v] = p - right * s - up * s;
                    verts[v + 1] = p - right * s + up * s;
                    verts[v + 2] = p + right * s - up * s;
                    verts[v + 3] = p + right * s + up * s;
                }
                else
                {
                    var tail = p - vel * 0.04f;
                    var across = Vector3.Cross(vel, p - eye).normalized * 0.012f;
                    verts[v] = p - across;
                    verts[v + 1] = tail - across;
                    verts[v + 2] = p + across;
                    verts[v + 3] = tail + across;
                }
                var col = snowing ? snowCol : rainCol;
                colours[v] = colours[v + 1] = colours[v + 2] = colours[v + 3] = col;
            }
            mesh.vertices = verts;
            mesh.colors = colours;
            mesh.bounds = new Bounds(c, Vector3.one * (Box * 2f + 30f));
        }

        static float Wrap(float x) => x < -Box ? x + Box * 2f : x > Box ? x - Box * 2f : x;
    }
}
