using System;

namespace Epacenur.Core
{
    /// The game's sound effects. There are no sound files: each is made here from tones, sweeps and filtered
    /// noise (Synth.Make), the same every time.
    public enum Sound
    {
        Click, Open, Chop, Mine, Splash, Pick, Hit, Miss, Hurt, Kill, LevelUp, Pickup, Drop, Eat, Bury, Fire,
        Smith, Craft, Build, Remove, Coins, Spell, Arrow, Prayer, Teleport, Death, Error,
    }

    /// Makes the sound effects: mono samples (-1..1) at Rate samples a second.
    public static class Synth
    {
        public const int Rate = 22050;
        public const float Peak = 0.8f;  // every sound is scaled so its loudest sample is this

        enum Wave { Sine, Triangle, Square, Saw }

        /// The samples for a sound.
        public static float[] Make(Sound sound)
        {
            var rng = new Random(1000 + (int)sound);
            float[] b;
            switch (sound)
            {
                case Sound.Click:
                    b = Buffer(0.03);
                    Tone(b, 0, 0.03, 1800, 1500, 0.6, Wave.Sine, 0.001, 0.006);
                    break;
                case Sound.Open:
                    b = Buffer(0.16);
                    Tone(b, 0, 0.14, 230, 170, 0.7, Wave.Triangle, 0.003, 0.05);
                    Noise(b, rng, 0, 0.06, 0.4, 0.3, 0.02);
                    break;
                case Sound.Chop:  // an axe biting into wood: a dull knock
                    b = Buffer(0.18);
                    Noise(b, rng, 0, 0.14, 1.0, 0.12, 0.035);
                    Tone(b, 0, 0.12, 150, 85, 0.9, Wave.Sine, 0.001, 0.035);
                    Noise(b, rng, 0, 0.01, 0.5, 1.0, 0.004);
                    break;
                case Sound.Mine:  // a pick on rock: a bright, ringing clink
                    b = Buffer(0.4);
                    Noise(b, rng, 0, 0.02, 0.6, 1.0, 0.006);
                    Tone(b, 0, 0.4, 1250, 1240, 0.5, Wave.Sine, 0.001, 0.08);
                    Tone(b, 0, 0.4, 1960, 1950, 0.35, Wave.Sine, 0.001, 0.06);
                    Tone(b, 0, 0.4, 2730, 2720, 0.25, Wave.Sine, 0.001, 0.04);
                    break;
                case Sound.Splash:
                    b = Buffer(0.5);
                    Noise(b, rng, 0, 0.45, 1.0, 0.25, 0.12);
                    for (int i = 0; i < 4; i++)
                        Tone(b, 0.05 + rng.NextDouble() * 0.3, 0.05, 500 + rng.Next(400), 1300 + rng.Next(600), 0.25, Wave.Sine, 0.002, 0.02);
                    break;
                case Sound.Pick:  // pulling a plant
                    b = Buffer(0.12);
                    Noise(b, rng, 0, 0.09, 0.8, 0.5, 0.025);
                    Tone(b, 0.02, 0.05, 700, 950, 0.3, Wave.Sine, 0.003, 0.02);
                    break;
                case Sound.Hit:
                    b = Buffer(0.22);
                    Tone(b, 0, 0.2, 170, 55, 1.0, Wave.Sine, 0.001, 0.06);
                    Noise(b, rng, 0, 0.08, 0.7, 0.2, 0.02);
                    break;
                case Sound.Miss:  // a swish through the air
                    b = Buffer(0.2);
                    HighNoise(b, rng, 0, 0.18, 0.6, 0.06, 0.05);
                    break;
                case Sound.Hurt:
                    b = Buffer(0.24);
                    Tone(b, 0, 0.22, 130, 70, 0.8, Wave.Square, 0.002, 0.07);
                    Noise(b, rng, 0, 0.08, 0.6, 0.25, 0.025);
                    break;
                case Sound.Kill:
                    b = Buffer(0.4);
                    Tone(b, 0, 0.38, 320, 80, 0.8, Wave.Triangle, 0.002, 0.12);
                    break;
                case Sound.LevelUp:  // a rising major arpeggio, then the chord held
                {
                    b = Buffer(1.3);
                    double[] notes = { 523.25, 659.25, 783.99, 1046.5 };
                    for (int i = 0; i < notes.Length; i++)
                    {
                        Tone(b, i * 0.11, 0.5, notes[i], notes[i], 0.55, Wave.Triangle, 0.004, 0.18);
                        Tone(b, i * 0.11, 0.5, notes[i] * 2, notes[i] * 2, 0.12, Wave.Sine, 0.004, 0.12);
                    }
                    foreach (var n in notes)
                        Tone(b, 0.48, 0.8, n, n, 0.3, Wave.Sine, 0.01, 0.35);
                    break;
                }
                case Sound.Pickup:
                    b = Buffer(0.14);
                    Tone(b, 0, 0.06, 660, 660, 0.6, Wave.Sine, 0.002, 0.03);
                    Tone(b, 0.06, 0.07, 990, 990, 0.6, Wave.Sine, 0.002, 0.035);
                    break;
                case Sound.Drop:
                    b = Buffer(0.12);
                    Tone(b, 0, 0.1, 190, 120, 0.8, Wave.Sine, 0.001, 0.03);
                    Noise(b, rng, 0, 0.05, 0.4, 0.3, 0.015);
                    break;
                case Sound.Eat:  // three crunches
                    b = Buffer(0.3);
                    for (int i = 0; i < 3; i++)
                        Noise(b, rng, i * 0.09, 0.06, 0.9, 0.45, 0.018);
                    break;
                case Sound.Bury:  // earth over bones
                    b = Buffer(0.45);
                    Noise(b, rng, 0, 0.35, 0.8, 0.1, 0.12);
                    Noise(b, rng, 0.18, 0.25, 0.6, 0.1, 0.08);
                    break;
                case Sound.Fire:  // a soft roar with crackles in it
                    b = Buffer(0.8);
                    Noise(b, rng, 0, 0.75, 0.35, 0.15, 0.3);
                    for (int i = 0; i < 16; i++)
                        HighNoise(b, rng, rng.NextDouble() * 0.7, 0.006, 0.8, 0.0005, 0.002);
                    break;
                case Sound.Smith:  // a hammer on an anvil
                    b = Buffer(0.6);
                    Noise(b, rng, 0, 0.015, 0.7, 1.0, 0.004);
                    Tone(b, 0, 0.6, 820, 815, 0.5, Wave.Sine, 0.001, 0.15);
                    Tone(b, 0, 0.6, 1310, 1305, 0.35, Wave.Sine, 0.001, 0.12);
                    Tone(b, 0, 0.6, 2150, 2140, 0.25, Wave.Sine, 0.001, 0.08);
                    Tone(b, 0, 0.6, 3100, 3090, 0.15, Wave.Sine, 0.001, 0.05);
                    break;
                case Sound.Craft:
                    b = Buffer(0.18);
                    Tone(b, 0, 0.06, 520, 520, 0.5, Wave.Triangle, 0.002, 0.02);
                    Tone(b, 0.08, 0.08, 390, 390, 0.5, Wave.Triangle, 0.002, 0.025);
                    break;
                case Sound.Build:  // two knocks of a hammer on wood
                    b = Buffer(0.3);
                    for (int i = 0; i < 2; i++)
                    {
                        Tone(b, i * 0.14, 0.09, 220, 160, 0.8, Wave.Sine, 0.001, 0.025);
                        Noise(b, rng, i * 0.14, 0.03, 0.6, 0.35, 0.01);
                    }
                    break;
                case Sound.Remove:  // a creak
                    b = Buffer(0.35);
                    Tremolo(b, 0, 0.32, 270, 170, 0.5, 18);
                    Noise(b, rng, 0.2, 0.1, 0.4, 0.3, 0.03);
                    break;
                case Sound.Coins:
                    b = Buffer(0.35);
                    for (int i = 0; i < 5; i++)
                    {
                        double f = 2300 + rng.Next(1300);
                        Tone(b, rng.NextDouble() * 0.2, 0.13, f, f, 0.35, Wave.Sine, 0.001, 0.04);
                    }
                    break;
                case Sound.Spell:  // a rising shimmer
                    b = Buffer(0.5);
                    Vibrato(b, 0, 0.45, 300, 1200, 0.6, 9, 0.03, 0.2);
                    Vibrato(b, 0.03, 0.42, 450, 1800, 0.3, 11, 0.02, 0.15);
                    break;
                case Sound.Arrow:  // a bowstring's twang and the arrow's flight
                    b = Buffer(0.18);
                    HighNoise(b, rng, 0, 0.05, 0.6, 0.002, 0.02);
                    Tone(b, 0, 0.14, 900, 300, 0.5, Wave.Triangle, 0.001, 0.04);
                    break;
                case Sound.Prayer:  // a soft chime
                    b = Buffer(1.0);
                    Tone(b, 0, 1.0, 880, 880, 0.45, Wave.Sine, 0.005, 0.35);
                    Tone(b, 0, 1.0, 1318.5, 1318.5, 0.3, Wave.Sine, 0.005, 0.3);
                    Tone(b, 0, 1.0, 1760, 1760, 0.15, Wave.Sine, 0.005, 0.2);
                    break;
                case Sound.Teleport:
                    b = Buffer(0.9);
                    Tone(b, 0, 0.8, 200, 1600, 0.6, Wave.Triangle, 0.05, 0.4);
                    HighNoise(b, rng, 0.1, 0.7, 0.25, 0.2, 0.3);
                    break;
                case Sound.Death:  // falling notes
                {
                    b = Buffer(1.0);
                    double[] notes = { 392, 329.6, 261.6, 196 };
                    for (int i = 0; i < notes.Length; i++)
                        Tone(b, i * 0.18, 0.35, notes[i], notes[i] * 0.97, 0.6, Wave.Triangle, 0.005, 0.15);
                    break;
                }
                case Sound.Error:
                    b = Buffer(0.2);
                    Tone(b, 0, 0.07, 150, 150, 0.5, Wave.Square, 0.002, 0.04);
                    Tone(b, 0.1, 0.07, 150, 150, 0.5, Wave.Square, 0.002, 0.04);
                    break;
                default:
                    b = Buffer(0.05);
                    break;
            }
            Normalize(b);
            return b;
        }

        static float[] Buffer(double seconds) => new float[(int)(seconds * Rate)];

        /// The value of a wave at a phase (in cycles), softened: the square and saw are made of a few
        /// harmonics, so they buzz rather than rasp.
        static double WaveAt(Wave w, double phase)
        {
            double a = phase * 2 * Math.PI;
            switch (w)
            {
                case Wave.Triangle:
                    double p = phase - Math.Floor(phase);
                    return p < 0.5 ? 4 * p - 1 : 3 - 4 * p;
                case Wave.Square:
                    return Math.Sin(a) + Math.Sin(3 * a) / 3 + Math.Sin(5 * a) / 5;
                case Wave.Saw:
                    return Math.Sin(a) - Math.Sin(2 * a) / 2 + Math.Sin(3 * a) / 3;
                default:
                    return Math.Sin(a);
            }
        }

        /// The envelope at time t into a sound: rising over `attack`, then dying away (halving about every 0.7 decay).
        static double Envelope(double t, double attack, double decay) => (t < attack ? t / attack : 1) * Math.Exp(-Math.Max(0, t - attack) / decay);

        /// A tone sweeping from f0 to f1 (Hz, evenly in pitch) from `start` for `length` seconds.
        static void Tone(float[] b, double start, double length, double f0, double f1, double amp, Wave w, double attack, double decay)
        {
            int s0 = (int)(start * Rate), n = (int)(length * Rate);
            double phase = 0;
            for (int k = 0; k < n && s0 + k < b.Length; k++)
            {
                double t = (double)k / Rate;
                double f = f0 * Math.Pow(f1 / f0, t / length);
                phase += f / Rate;
                b[s0 + k] += (float)(WaveAt(w, phase) * amp * Envelope(t, attack, decay));
            }
        }

        /// A sweeping sine with a wobble in its pitch (`rate` Hz, `depth` as a fraction).
        static void Vibrato(float[] b, double start, double length, double f0, double f1, double amp, double rate, double depth, double decay)
        {
            int s0 = (int)(start * Rate), n = (int)(length * Rate);
            double phase = 0;
            for (int k = 0; k < n && s0 + k < b.Length; k++)
            {
                double t = (double)k / Rate;
                double f = f0 * Math.Pow(f1 / f0, t / length) * (1 + depth * Math.Sin(2 * Math.PI * rate * t));
                phase += f / Rate;
                b[s0 + k] += (float)(Math.Sin(phase * 2 * Math.PI) * amp * Envelope(t, 0.02, decay));
            }
        }

        /// A buzzing sweep pulsing `rate` times a second (a creak).
        static void Tremolo(float[] b, double start, double length, double f0, double f1, double amp, double rate)
        {
            int s0 = (int)(start * Rate), n = (int)(length * Rate);
            double phase = 0;
            for (int k = 0; k < n && s0 + k < b.Length; k++)
            {
                double t = (double)k / Rate;
                phase += f0 * Math.Pow(f1 / f0, t / length) / Rate;
                double pulse = 0.5 + 0.5 * Math.Sin(2 * Math.PI * rate * t);
                b[s0 + k] += (float)(WaveAt(Wave.Saw, phase) * amp * pulse * Envelope(t, 0.01, length * 0.6));
            }
        }

        /// Noise through a simple low-pass filter: `smooth` from 1 (bright, unfiltered) down towards 0 (a dull rumble).
        static void Noise(float[] b, Random rng, double start, double length, double amp, double smooth, double decay)
        {
            int s0 = (int)(start * Rate), n = (int)(length * Rate);
            double y = 0;
            for (int k = 0; k < n && s0 + k < b.Length; k++)
            {
                y += smooth * (rng.NextDouble() * 2 - 1 - y);
                b[s0 + k] += (float)(y * amp * Envelope((double)k / Rate, 0.001, decay) * (1 + (1 - smooth) * 2));
            }
        }

        /// Noise with its low end taken out: a hiss (swishes, crackles).
        static void HighNoise(float[] b, Random rng, double start, double length, double amp, double attack, double decay)
        {
            int s0 = (int)(start * Rate), n = (int)(length * Rate);
            double low = 0;
            for (int k = 0; k < n && s0 + k < b.Length; k++)
            {
                double x = rng.NextDouble() * 2 - 1;
                low += 0.1 * (x - low);
                b[s0 + k] += (float)((x - low) * amp * Envelope((double)k / Rate, attack, decay));
            }
        }

        /// Scales the sound so its loudest sample is Peak, and fades its very end so it doesn't click.
        static void Normalize(float[] b)
        {
            float max = 0;
            foreach (var v in b)
                max = Math.Max(max, Math.Abs(v));
            if (max <= 0)
                return;
            float scale = Peak / max;
            int fade = Math.Min(b.Length, Rate / 200);
            for (int k = 0; k < b.Length; k++)
            {
                b[k] *= scale;
                int left = b.Length - 1 - k;
                if (left < fade)
                    b[k] *= (float)left / fade;
            }
        }
    }
}
