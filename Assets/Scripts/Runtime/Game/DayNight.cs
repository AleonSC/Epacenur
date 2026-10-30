using UnityEngine;

namespace Epacenur.Runtime
{
    /// Day and night: the sun crosses the sky, the sky and fog go orange at dawn and dusk and dark blue at
    /// night, and a dim blue moon lights the world until morning. A whole day takes CycleMinutes of play.
    public sealed class DayNight
    {
        public const float CycleMinutes = 24f;  // one in-game hour a real minute
        const float SunriseAt = 0.25f;          // 06:00

        /// 0 at midnight, 0.25 at sunrise, 0.5 at noon, 0.75 at sunset.
        public float Time;
        /// Days since the world began.
        public int Day;

        readonly Light sun;
        readonly Camera cam;

        static readonly Color DaySky = new Color(0.62f, 0.78f, 0.92f), DuskSky = new Color(0.93f, 0.6f, 0.42f), NightSky = new Color(0.04f, 0.06f, 0.13f);
        static readonly Color DayAmbient = new Color(0.5f, 0.53f, 0.58f), NightAmbient = new Color(0.13f, 0.15f, 0.24f);
        static readonly Color SunColour = new Color(1f, 0.96f, 0.88f), LowSunColour = new Color(1f, 0.62f, 0.38f), MoonColour = new Color(0.55f, 0.65f, 1f);

        public DayNight(Light sun, Camera cam, float time, int day)
        {
            this.sun = sun;
            this.cam = cam;
            Time = time;
            Day = day;
            Apply();
        }

        public void Update(float dt)
        {
            Time += dt / (CycleMinutes * 60f);
            if (Time >= 1f)
            {
                Time -= 1f;
                Day++;
            }
            Apply();
        }

        /// "14:05".
        public string Clock
        {
            get
            {
                int minutes = (int)(Time * 24f * 60f);
                return (minutes / 60).ToString("00") + ":" + (minutes % 60).ToString("00");
            }
        }

        public bool IsNight => Height < -0.05f;
        /// Lamps go on a little before sunset and off a little after sunrise.
        public bool LampsLit => Height < 0.12f;

        /// How high the sun is: 1 overhead at noon, 0 on the horizon, -1 at midnight.
        float Height => Mathf.Sin((Time - SunriseAt) * Mathf.PI * 2f);

        void Apply()
        {
            float h = Height;
            float day = Smooth(-0.12f, 0.25f, h);            // 0 at night, 1 in full day
            float dusk = 1f - Mathf.Clamp01(Mathf.Abs(h + 0.02f) / 0.22f);  // near the horizon
            var sky = Color.Lerp(NightSky, DaySky, day);
            sky = Color.Lerp(sky, DuskSky, dusk * 0.65f);
            cam.backgroundColor = sky;
            RenderSettings.fogColor = sky;
            RenderSettings.ambientLight = Color.Lerp(NightAmbient, DayAmbient, day);

            // The sun swings from east to west; after dark the moon takes over, low in the south.
            float yaw = Time * 360f - 90f;
            if (h > -0.05f)
            {
                sun.transform.rotation = Quaternion.Euler(Mathf.Max(4f, h * 65f), yaw, 0f);
                sun.color = Color.Lerp(LowSunColour, SunColour, Smooth(0f, 0.35f, h));
                sun.intensity = 1.1f * Smooth(-0.05f, 0.15f, h);
            }
            else
            {
                sun.transform.rotation = Quaternion.Euler(Mathf.Max(12f, -h * 50f), yaw + 180f, 0f);
                sun.color = MoonColour;
                sun.intensity = 0.28f * Smooth(-0.05f, -0.25f, h);
            }
        }

        static float Smooth(float from, float to, float x)
        {
            float t = Mathf.Clamp01((x - from) / (to - from));
            return t * t * (3f - 2f * t);
        }
    }
}
