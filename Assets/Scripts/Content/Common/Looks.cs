using Epacenur.Core;

namespace Epacenur.Content
{
    /// Colours and shapes several modules share.
    public static class Looks
    {
        /// A craggy rock (about a tile across, three quarters high): tilted blocks of `stone` heaped on a darker
        /// slab, lighter on top, a crack, and loose stones round its foot. `seed` gives each kind of rock its own
        /// shape (each one is also turned and sized a little differently where it stands).
        public static Model Crag(Rgb stone, int seed)
        {
            var rng = new System.Random(seed);
            System.Func<float, float> wiggle = range => ((float)rng.NextDouble() - 0.5f) * 2f * range;
            var m = new Model()
                .Cube(0f, 0.1f, 0f, 1f, 0.22f, 0.88f, stone * 0.78f).Turn(wiggle(3f), 20f + wiggle(25f), wiggle(3f))                       // the slab it sits on
                .Cube(0.02f, 0.36f, 0f, 0.7f, 0.6f, 0.64f, stone).Turn(8f + wiggle(6f), 35f + wiggle(30f), 12f + wiggle(6f))                // the main block
                .Cube(0.27f, 0.28f, 0.2f, 0.5f, 0.44f, 0.46f, stone * 0.9f).Turn(-12f + wiggle(8f), 70f + wiggle(30f), 18f + wiggle(8f))
                .Cube(-0.28f, 0.26f, -0.13f, 0.48f, 0.42f, 0.5f, stone * 1.05f).Turn(15f + wiggle(8f), 10f + wiggle(30f), -14f + wiggle(8f))
                .Cube(-0.04f, 0.6f, 0.02f, 0.42f, 0.22f, 0.38f, stone * 1.14f).Turn(18f + wiggle(8f), 50f + wiggle(40f), -10f + wiggle(8f))  // a lighter cap
                .Cube(0.06f, 0.42f, 0.31f, 0.03f, 0.34f, 0.04f, stone * 0.45f).Turn(0f, 35f, 22f);                                          // a crack
            for (int i = 0; i < 3; i++)  // loose stones round the foot
            {
                float a = i * 2.2f + wiggle(0.5f), r = 0.52f + wiggle(0.06f), s = 0.11f + (float)rng.NextDouble() * 0.07f;
                m.Cube((float)System.Math.Cos(a) * r, s * 0.4f, (float)System.Math.Sin(a) * r, s, s * 0.8f, s * 1.1f, stone * (0.85f + i * 0.08f))
                    .Turn(wiggle(20f), wiggle(180f), wiggle(20f));
            }
            return m;
        }

        /// A seed for a name (the same every time, unlike string.GetHashCode).
        public static int Seed(string name)
        {
            int h = 17;
            foreach (char c in name)
                h = h * 31 + c;
            return h & 0xffff;
        }

        /// Ore showing in a crag: crystals jutting out of its faces with brighter tips, and flecks between them.
        /// `glow`: the tips shine faintly (the rarest ores).
        public static Model Veins(Model crag, Rgb ore, int seed, bool glow = false)
        {
            var rng = new System.Random(seed * 7 + 3);
            var m = new Model().Include(crag);
            // Clusters of crystals: round its sides and on its top, pointing out of it (each one's length, y, is
            // tilted outward, then turned to face round), each with a brighter tip.
            for (int i = 0; i < 9; i++)
            {
                bool top = i >= 6;
                float a = top ? i * 2.4f + (float)rng.NextDouble() : i * 1.05f + (float)rng.NextDouble() * 0.5f;
                float r = top ? 0.16f + (float)rng.NextDouble() * 0.1f : 0.42f + (float)rng.NextDouble() * 0.06f;
                float h = top ? 0.64f + (float)rng.NextDouble() * 0.06f : 0.24f + (i % 3) * 0.13f;
                float x = (float)System.Math.Cos(a) * r, z = (float)System.Math.Sin(a) * r * 0.9f;
                float deg = a * 57.2958f, tilt = top ? -15f - (float)rng.NextDouble() * 20f : -45f - (float)rng.NextDouble() * 25f;
                float len = 0.24f + (float)rng.NextDouble() * 0.12f, thick = 0.09f + (float)rng.NextDouble() * 0.03f;
                m.Cube(x, h, z, thick, len, thick, ore).Turn(0f, -deg, tilt);
                float tip = len * 0.5f;
                double t = tilt * System.Math.PI / 180.0;
                float outward = (float)-System.Math.Sin(t) * tip, up = (float)System.Math.Cos(t) * tip;
                m.Cube(x + (float)System.Math.Cos(a) * outward, h + up, z + (float)System.Math.Sin(a) * outward * 0.9f, thick * 0.8f, thick * 0.8f, thick * 0.8f, ore * 1.35f)
                    .Turn(45f, -deg, tilt);
                if (glow)
                    m.Glowing();
            }
            // Patches of ore on its faces.
            for (int i = 0; i < 4; i++)
            {
                float a = i * 1.57f + 0.8f + (float)rng.NextDouble() * 0.4f;
                m.Sphere((float)System.Math.Cos(a) * 0.4f, 0.3f + (i % 2) * 0.16f, (float)System.Math.Sin(a) * 0.36f, 0.2f, 0.14f, 0.2f, ore * 0.85f)
                    .Turn(0f, -a * 57.2958f, -50f);
            }
            return m;
        }

        /// A crag that's been mined out: chipped, darker hollows where its ore was.
        public static Model Chipped(Model crag, Rgb stone)
        {
            var m = new Model().Include(crag);
            for (int i = 0; i < 5; i++)
            {
                float a = i * 1.26f + 0.3f, r = i == 4 ? 0.1f : 0.42f, h = i == 4 ? 0.68f : 0.28f + (i % 2) * 0.16f;
                m.Sphere((float)System.Math.Cos(a) * r, h, (float)System.Math.Sin(a) * r * 0.9f, 0.22f, 0.12f, 0.2f, stone * (0.5f + (i % 2) * 0.08f))
                    .Turn(0f, -a * 57.2958f, i == 4 ? 0f : -55f);
            }
            return m;
        }

        public static readonly Rgb Bronze = new Rgb(0.72f, 0.5f, 0.28f);
        public static readonly Rgb Handle = new Rgb(0.45f, 0.3f, 0.16f);
        public static readonly Rgb Iron = new Rgb(0.45f, 0.46f, 0.5f);
        public static readonly Rgb Rock = new Rgb(0.5f, 0.48f, 0.45f);
        public static readonly Rgb Dark = new Rgb(0.1f, 0.08f, 0.06f);

        /// A held model (pointing +z from the grip) lying flat on the ground.
        public static Model OnGround(Model held, float z = -0.25f) => new Model().Child(held, 0f, 0.03f, z, 0f, 0f, 90f);

        /// Three logs, for an inventory slot and lying on the ground.
        public static Icon LogIcon(Rgb bark, Rgb end)
        {
            var icon = new Icon();
            for (int k = 0; k < 3; k++)
            {
                float y = 9f + k * 8f, x = 6f + (k == 1 ? 4f : 0f);
                icon.Rect(x, y, 24f, 7f, k == 1 ? bark * 0.85f : bark).Rect(x + 24f, y, 4f, 7f, end);
            }
            return icon;
        }

        public static Model LogModel(Rgb bark, Rgb end)
        {
            var m = new Model();
            for (int k = 0; k < 3; k++)
            {
                float lx = -0.08f + k * 0.08f, ly = 0.06f + (k == 1 ? 0.08f : 0f), lz = k * 0.04f;
                m.Cylinder(lx, ly, lz, 0.1f, 0.2f, 0.1f, k == 1 ? bark * 0.85f : bark).Turn(0f, 0f, 90f)
                 .Cylinder(lx - 0.2f, ly, lz, 0.09f, 0.004f, 0.09f, end).Turn(0f, 0f, 90f);  // the cut end
            }
            return m;
        }

        /// A bar of metal.
        public static Icon BarIcon(Rgb c) => new Icon().Rect(8f, 22f, 24f, 8f, c * 0.8f).Rect(11f, 15f, 18f, 7f, c).Rect(12f, 16f, 16f, 2f, c * 1.3f);
        public static Model BarModel(Rgb c) => new Model().Cube(0f, 0.05f, 0f, 0.32f, 0.1f, 0.14f, c);

        /// A lump of raw meat (or cooked, or burnt).
        public static Icon MeatIcon(Rgb c) => new Icon().Rect(9f, 13f, 22f, 15f, c).Rect(12f, 10f, 14f, 4f, c).Rect(14f, 16f, 10f, 3f, c * 1.25f).Rect(26f, 22f, 6f, 4f, new Rgb(0.95f, 0.92f, 0.85f));
        public static Model MeatModel(Rgb c) => new Model().Sphere(0f, 0.06f, 0f, 0.26f, 0.12f, 0.2f, c).Cylinder(0.14f, 0.06f, 0f, 0.04f, 0.06f, 0.04f, new Rgb(0.95f, 0.92f, 0.85f)).Turn(0f, 0f, 90f);

        /// `count` small fish lying side by side (raw, cooked or burnt).
        public static Model Fish(Rgb c, int count)
        {
            var m = new Model();
            for (int i = 0; i < count; i++)
            {
                var fish = new Model()
                    .Sphere(0f, 0f, 0f, 0.06f, 0.05f, 0.2f, c)
                    .Cube(0f, 0f, -0.11f, 0.01f, 0.06f, 0.05f, c * 0.8f).Turn(45f, 0f, 0f);
                m.Child(fish, -0.08f + i * 0.08f, 0.03f, (i % 2) * 0.04f, 0f, i * 25f, 0f);
            }
            return m;
        }

        public static Icon FishIcon(Rgb c, int count)
        {
            var icon = new Icon();
            for (int k = 0; k < count; k++)
            {
                float y = 10f + k * 8f;
                icon.Rect(8f, y, 18f, 5f, c).Rect(26f, y - 1f, 4f, 7f, c * 0.75f).Rect(10f, y + 1f, 2f, 2f, new Rgb(0.1f, 0.1f, 0.1f));
            }
            return icon;
        }

        /// A lump of rock with flecks of ore.
        public static Icon OreIcon(Rgb ore) => new Icon()
            .Rect(9f, 14f, 22f, 16f, Rock).Rect(12f, 11f, 15f, 4f, Rock).Rect(12f, 30f, 17f, 3f, Rock * 0.8f)
            .Rect(13f, 16f, 4f, 3f, ore).Rect(21f, 13f, 4f, 3f, ore).Rect(24f, 22f, 4f, 3f, ore).Rect(15f, 25f, 4f, 3f, ore);

        public static Model OreModel(Rgb ore) => new Model()
            .Sphere(0f, 0.07f, 0f, 0.22f, 0.15f, 0.2f, Rock)
            .Sphere(0.05f, 0.11f, 0.04f, 0.08f, 0.05f, 0.08f, ore)
            .Sphere(-0.06f, 0.1f, -0.03f, 0.07f, 0.05f, 0.07f, ore);
    }

    /// Monsters' bodies.
    public static class Bodies
    {
        /// A person standing (feet at y = 0, facing +z): legs, body, arms, head.
        public static Model Humanoid(Rgb skin, Rgb shirt, Rgb trousers, Rgb hair)
        {
            return new Model()
                .Capsule(-0.12f, 0.42f, 0f, 0.17f, 0.42f, 0.17f, trousers)
                .Capsule(0.12f, 0.42f, 0f, 0.17f, 0.42f, 0.17f, trousers)
                .Capsule(0f, 1.15f, 0f, 0.5f, 0.42f, 0.32f, shirt)
                .Capsule(-0.33f, 1.12f, 0.02f, 0.13f, 0.32f, 0.13f, shirt)
                .Capsule(0.33f, 1.12f, 0.02f, 0.13f, 0.32f, 0.13f, shirt)
                .Sphere(-0.33f, 0.78f, 0.04f, 0.11f, 0.11f, 0.11f, skin)
                .Sphere(0.33f, 0.78f, 0.04f, 0.11f, 0.11f, 0.11f, skin)
                .Sphere(0f, 1.68f, 0f, 0.33f, 0.36f, 0.33f, skin)
                .Sphere(0f, 1.76f, -0.03f, 0.35f, 0.26f, 0.35f, hair)
                .Cube(-0.065f, 1.7f, 0.15f, 0.045f, 0.045f, 0.02f, Looks.Dark)
                .Cube(0.065f, 1.7f, 0.15f, 0.045f, 0.045f, 0.02f, Looks.Dark);
        }

        /// A four-legged animal (cow, wolf): body along z, a head in front, a tail behind. `size` scales it.
        public static Model Quadruped(Rgb body, Rgb head, Rgb legs, float length, float height, float width)
        {
            float legH = height * 0.45f, bodyY = legH + height * 0.25f;
            var m = new Model()
                .Capsule(0f, bodyY, 0f, width, length * 0.5f, height * 0.5f, body).Turn(90f, 0f, 0f)
                .Cube(0f, bodyY + height * 0.15f, length * 0.55f, width * 0.6f, height * 0.4f, length * 0.32f, head)
                .Cube(0f, bodyY + height * 0.05f, -length * 0.55f, width * 0.12f, height * 0.12f, length * 0.25f, body).Turn(30f, 0f, 0f);
            foreach (float x in new[] { -width * 0.3f, width * 0.3f })
                foreach (float z in new[] { -length * 0.33f, length * 0.33f })
                    m.Cube(x, legH / 2f, z, width * 0.22f, legH, width * 0.22f, legs);
            return m;
        }
    }

    /// Short ways to register definitions.
    public static class Register
    {
        public static ItemDef Item(this ContentDb db, string id, string name, string examine, int value, Rgb colour)
            => db.Add(new ItemDef { Id = id, Name = name, Examine = examine, Value = value, Colour = colour });

        public static SkillDef Skill(this ContentDb db, string id, string name, Rgb colour, Icon icon, int startLevel = 1)
            => db.Add(new SkillDef { Id = id, Name = name, Colour = colour, Icon = icon, StartLevel = startLevel });
    }
}
