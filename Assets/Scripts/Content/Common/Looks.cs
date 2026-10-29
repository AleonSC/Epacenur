using Epacenur.Core;

namespace Epacenur.Content
{
    /// Colours and shapes several modules share.
    public static class Looks
    {
        public static readonly Rgb Bronze = new Rgb(0.72f, 0.5f, 0.28f);
        public static readonly Rgb Handle = new Rgb(0.45f, 0.3f, 0.16f);
        public static readonly Rgb Iron = new Rgb(0.45f, 0.46f, 0.5f);
        public static readonly Rgb Rock = new Rgb(0.5f, 0.48f, 0.45f);
        public static readonly Rgb Dark = new Rgb(0.1f, 0.08f, 0.06f);

        /// A held model (pointing +z from the grip) lying flat on the ground.
        public static Model OnGround(Model held, float z = -0.25f) => new Model().Child(held, 0f, 0.03f, z, 0f, 0f, 90f);

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

    /// Short ways to register definitions.
    public static class Register
    {
        public static ItemDef Item(this ContentDb db, string id, string name, string examine, int value, Rgb colour)
            => db.Add(new ItemDef { Id = id, Name = name, Examine = examine, Value = value, Colour = colour });

        public static SkillDef Skill(this ContentDb db, string id, string name, Rgb colour, Icon icon, int startLevel = 1)
            => db.Add(new SkillDef { Id = id, Name = name, Colour = colour, Icon = icon, StartLevel = startLevel });
    }
}
