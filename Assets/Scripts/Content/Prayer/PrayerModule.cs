using Epacenur.Core;

namespace Epacenur.Content
{
    /// Prayer: bury bones for xp; the altar recharges prayer points. (The prayers themselves are in Core: Prayers.)
    public sealed class PrayerModule : IContentModule
    {
        public const string Bones = "bones", Altar = "altar";

        /// The prayer star, also used for the prayer bar.
        public static Icon Star(Rgb c) => new Icon(22f)
            .Rect(4f, 9.5f, 14f, 3f, c).Rect(9.5f, 4f, 3f, 14f, c).Diamond(11f, 11f, 5f, c).Rect(9.5f, 9.5f, 3f, 3f, Rgb.White);

        public void Register(ContentDb db)
        {
            db.Skill(SkillIds.Prayer, "Prayer", new Rgb(0.95f, 0.92f, 0.6f), Star(new Rgb(1f, 0.95f, 0.55f)));

            var white = new Rgb(0.93f, 0.9f, 0.8f);
            var bones = db.Item(Bones, "Bones", "Bones are for burying!", 1, white);
            bones.BuryXp = 4.5;
            bones.Icon = new Icon()
                .Turned(17f, 9f, 6f, 22f, white, -35f)
                .Turned(13f, 6f, 6f, 6f, white, -35f).Turned(21f, 6f, 6f, 6f, white, -35f)
                .Turned(13f, 28f, 6f, 6f, white, -35f).Turned(21f, 28f, 6f, 6f, white, -35f);
            var bone = new Model().Cylinder(0f, 0f, 0f, 0.06f, 0.15f, 0.06f, white);
            foreach (float y in new[] { -0.15f, 0.15f })
                foreach (float x in new[] { -0.035f, 0.035f })
                    bone.Sphere(x, y, 0f, 0.07f, 0.07f, 0.07f, white);
            bones.GroundModel = new Model().Child(bone, 0f, 0.04f, 0f, 90f, 30f, 0f);

            // A stone altar with a cloth, a gold symbol and two candles.
            var stone = new Rgb(0.72f, 0.7f, 0.66f);
            var dark = new Rgb(0.55f, 0.53f, 0.5f);
            var cloth = new Rgb(0.92f, 0.9f, 0.85f);
            var gold = new Rgb(0.9f, 0.74f, 0.25f);
            var altar = new Model()
                .Cube(0f, 0.08f, 0f, 0.98f, 0.16f, 0.9f, dark)
                .Cube(0f, 0.45f, 0f, 0.82f, 0.6f, 0.72f, stone)
                .Cube(0f, 0.8f, 0f, 0.95f, 0.1f, 0.85f, dark)
                .Cube(0f, 0.86f, 0f, 0.55f, 0.02f, 0.87f, cloth)
                .Cube(0f, 0.62f, -0.44f, 0.55f, 0.48f, 0.02f, cloth)
                .Cube(0f, 0.62f, -0.452f, 0.08f, 0.44f, 0.01f, gold)
                .Cube(0f, 0.7f, -0.452f, 0.26f, 0.06f, 0.01f, gold);
            foreach (float x in new[] { -0.33f, 0.33f })
                altar.Cylinder(x, 0.93f, 0.1f, 0.07f, 0.08f, 0.07f, new Rgb(0.96f, 0.93f, 0.8f))
                     .Sphere(x, 1.04f, 0.1f, 0.04f, 0.07f, 0.04f, new Rgb(1f, 0.75f, 0.2f)).Glowing();
            db.Add(new StationDef
            {
                Id = Altar, Name = "Altar", Action = "Pray-at", Examine = "An altar to the gods.",
                Kind = StationKind.Altar, Model = altar, Label = "Altar", LabelHeight = 1.6f,
            });
        }
    }
}
