namespace Epacenur.Core
{
    /// A colour (0..1 per channel). Core has no Unity, so it has its own.
    public struct Rgb
    {
        public float R, G, B;
        public Rgb(float r, float g, float b) { R = r; G = g; B = b; }

        /// Darker (f < 1) or lighter (f > 1).
        public Rgb Shade(float f) => new Rgb(R * f, G * f, B * f);
        public static Rgb operator *(Rgb c, float f) => c.Shade(f);

        public static readonly Rgb White = new Rgb(1f, 1f, 1f);
        public static readonly Rgb Black = new Rgb(0f, 0f, 0f);
        public static readonly Rgb Wood = new Rgb(0.5f, 0.32f, 0.16f);
        public static readonly Rgb Steel = new Rgb(0.78f, 0.8f, 0.84f);
        public static readonly Rgb Gold = new Rgb(0.9f, 0.72f, 0.2f);
    }

    /// A 3D vector for model specs (Core has no Unity).
    public struct Vec3
    {
        public float X, Y, Z;
        public Vec3(float x, float y, float z) { X = x; Y = y; Z = z; }

        public static readonly Vec3 Zero = new Vec3(0f, 0f, 0f);
        public static readonly Vec3 One = new Vec3(1f, 1f, 1f);
        public static Vec3 operator +(Vec3 a, Vec3 b) => new Vec3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static Vec3 operator -(Vec3 a, Vec3 b) => new Vec3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static Vec3 operator *(Vec3 a, float f) => new Vec3(a.X * f, a.Y * f, a.Z * f);
    }

    /// The game's clock: everything runs on RuneScape's 0.6 s tick.
    public static class GameTime
    {
        public const float TickSeconds = 0.6f;
        public static float Ticks(int ticks) => ticks * TickSeconds;
    }
}
