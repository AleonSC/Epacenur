using System;

namespace Epacenur.Core
{
    /// The kinds of weather that pass over the land.
    public enum Sky { Clear, Cloudy, Rain, Storm, Fog }

    /// The weather where you stand, each 0..1: how overcast, how hard it rains or snows, how misty, how windy,
    /// and how stormy (thunder and lightning).
    public struct LocalWeather
    {
        public double Clouds, Rain, Snow, Mist, Wind, Storm;
    }

    /// The weather: one sky over the whole land that changes every few minutes (clear skies cloud over, clouds
    /// bring rain, rain sometimes builds to a storm, and mornings can be foggy), easing from one to the next.
    /// Where you are shapes it (At): marshes are misty, the wasteland is dry, rain falls as snow high in the
    /// mountains, and fog drifts across the land in banks on the wind.
    public sealed class Weather
    {
        public const double ChangeSeconds = 45;  // how long one kind of weather takes to give way to the next

        public Sky Now { get; private set; } = Sky.Clear;
        public Sky Was { get; private set; } = Sky.Clear;
        /// 0 just after a change, to 1 once the new weather has fully set in.
        public double Blend { get; private set; } = 1;
        /// The way the wind blows (radians: 0 towards +x, turning towards +z), and the seconds the weather has run.
        public double WindAngle { get; private set; }
        public double Clock { get; private set; }

        double left;
        readonly Random rng;
        readonly ValueNoise banks;

        public Weather(int seed)
        {
            rng = new Random(seed);
            banks = new ValueNoise(seed + 4242);
            WindAngle = rng.NextDouble() * Math.PI * 2;
            left = Duration(Now);
        }

        /// Fired when the weather turns (from, to).
        public event Action<Sky, Sky> Changed;

        /// Time passes: the wind wanders, and when its time is up the weather turns to the next.
        public void Advance(double seconds)
        {
            Clock += seconds;
            Blend = Math.Min(1, Blend + seconds / ChangeSeconds);
            WindAngle += (rng.NextDouble() - 0.5) * 0.02 * seconds;
            left -= seconds;
            if (left > 0)
                return;
            var next = NextAfter(Now, rng.NextDouble());
            left = Duration(next);
            if (next == Now)
                return;
            Was = Now;
            Now = next;
            Blend = 0;
            Changed?.Invoke(Was, Now);
        }

        /// Sets the weather straight away (dev tools).
        public void Set(Sky sky)
        {
            Was = Now;
            Now = sky;
            Blend = 1;
            left = Duration(sky);
        }

        double Duration(Sky s) => (s == Sky.Storm ? 90 : s == Sky.Fog ? 150 : 180) + rng.NextDouble() * 300;

        /// What follows a kind of weather (`roll` 0..1).
        public static Sky NextAfter(Sky s, double roll)
        {
            switch (s)
            {
                case Sky.Clear: return roll < 0.4 ? Sky.Clear : roll < 0.85 ? Sky.Cloudy : Sky.Fog;
                case Sky.Cloudy: return roll < 0.3 ? Sky.Clear : roll < 0.45 ? Sky.Cloudy : roll < 0.9 ? Sky.Rain : Sky.Fog;
                case Sky.Rain: return roll < 0.45 ? Sky.Cloudy : roll < 0.7 ? Sky.Storm : roll < 0.9 ? Sky.Rain : Sky.Clear;
                case Sky.Storm: return roll < 0.7 ? Sky.Rain : Sky.Cloudy;
                default: return roll < 0.5 ? Sky.Clear : Sky.Cloudy;
            }
        }

        /// A kind of weather's own amounts: clouds, rain, mist, wind, storm.
        static void Amounts(Sky s, out double clouds, out double rain, out double mist, out double wind, out double storm)
        {
            switch (s)
            {
                case Sky.Cloudy: clouds = 0.6; rain = 0; mist = 0.1; wind = 0.45; storm = 0; break;
                case Sky.Rain: clouds = 0.85; rain = 0.65; mist = 0.2; wind = 0.55; storm = 0; break;
                case Sky.Storm: clouds = 1; rain = 1; mist = 0.25; wind = 1; storm = 1; break;
                case Sky.Fog: clouds = 0.45; rain = 0; mist = 0.85; wind = 0.08; storm = 0; break;
                default: clouds = 0.08; rain = 0; mist = 0; wind = 0.25; storm = 0; break;
            }
        }

        /// The weather over the whole land now (easing from the last kind to this one).
        public LocalWeather Overall
        {
            get
            {
                Amounts(Was, out double c0, out double r0, out double m0, out double w0, out double s0);
                Amounts(Now, out double c1, out double r1, out double m1, out double w1, out double s1);
                double b = Blend * Blend * (3 - 2 * Blend);
                return new LocalWeather
                {
                    Clouds = c0 + (c1 - c0) * b, Rain = r0 + (r1 - r0) * b, Mist = m0 + (m1 - m0) * b,
                    Wind = w0 + (w1 - w0) * b, Storm = s0 + (s1 - s0) * b,
                };
            }
        }

        public const double SnowFrom = 30;  // rain falls as snow above this height (and up on the peaks, any cloud brings a little)

        /// The weather at a place: the sky over all, shaped by the biome, the height, the time of day (0..1, 0.25
        /// sunrise) and where the fog banks have drifted to.
        public LocalWeather At(Biome biome, double height, double timeOfDay, double x, double z)
        {
            var w = Overall;
            switch (biome)
            {
                case Biome.Marsh: w.Mist = Math.Min(1, w.Mist + 0.3); w.Rain = Math.Min(1, w.Rain * 1.15); break;
                case Biome.Wasteland: w.Rain *= 0.35; w.Clouds *= 0.8; w.Wind = Math.Min(1, w.Wind + 0.1); break;
                case Biome.Highlands: w.Wind = Math.Min(1, w.Wind + 0.2); break;
                case Biome.Woodland: w.Wind *= 0.8; break;  // the trees break it
            }
            // Mist in the early morning, thickest in the valleys.
            double dawn = 1 - Math.Min(1, Math.Abs(timeOfDay - 0.27) / 0.07);
            w.Mist = Math.Min(1, w.Mist + dawn * (height < 10 ? 0.35 : 0.15));
            // Fog lies in banks that drift on the wind: thick in some places, thin in others.
            if (w.Mist > 0)
            {
                double drift = Clock * (0.3 + w.Wind * 1.2);
                double bx = x - Math.Cos(WindAngle) * drift, bz = z - Math.Sin(WindAngle) * drift;
                double bank = banks.Fractal(bx / 90.0, bz / 90.0, 2);
                w.Mist = Math.Max(0, Math.Min(1, w.Mist * (0.45 + 1.1 * bank)));
            }
            // High up, rain is snow.
            double cold = Math.Max(0, Math.Min(1, (height - SnowFrom) / 6));
            w.Snow = w.Rain * cold;
            w.Rain *= 1 - cold;
            if (height > Overworld.SnowLine)
                w.Snow = Math.Max(w.Snow, w.Clouds * 0.35);
            return w;
        }

        /// What to say in the chat when the weather turns (null: nothing worth saying).
        public static string Describe(Sky from, Sky to)
        {
            switch (to)
            {
                case Sky.Rain: return from == Sky.Storm ? "The storm passes, leaving steady rain." : "It starts to rain.";
                case Sky.Storm: return "Thunder rumbles in the distance. A storm is coming.";
                case Sky.Fog: return "A fog rolls in.";
                case Sky.Clear: return from == Sky.Fog ? "The fog lifts." : "The sky clears.";
                case Sky.Cloudy: return from == Sky.Rain || from == Sky.Storm ? "The rain stops." : from == Sky.Fog ? "The fog lifts." : null;
                default: return null;
            }
        }
    }
}
