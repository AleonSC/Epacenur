using Epacenur.Core;

namespace Epacenur.Content
{
    /// Agility: courses of five obstacles round a loop of path, out in the countryside (course regions),
    /// harder and better further out; the field course is always just west of the start. A higher level
    /// also brings back your run energy faster.
    public sealed class AgilityModule : IContentModule
    {
        public const string Skill = "agility", FieldCourse = "field_course";

        public void Register(ContentDb db)
        {
            db.Skill(Skill, "Agility", new Rgb(0.3f, 0.45f, 0.8f), new Icon(22f)  // a running figure
                .Rect(10f, 2f, 4f, 4f, new Rgb(0.95f, 0.85f, 0.7f)).Turned(8f, 6f, 4f, 8f, new Rgb(0.3f, 0.45f, 0.8f), 15f)
                .Turned(5f, 13f, 3f, 7f, new Rgb(0.3f, 0.45f, 0.8f), 40f).Turned(11f, 13f, 3f, 7f, new Rgb(0.3f, 0.45f, 0.8f), -30f)
                .Turned(4f, 7f, 7f, 2f, new Rgb(0.95f, 0.85f, 0.7f), -20f).Turned(12f, 7f, 7f, 2f, new Rgb(0.95f, 0.85f, 0.7f), 30f));

            Course(db, FieldCourse, "Field course", 1, 7, 39, 0, 600, true,
                new[] { "Log balance", "Low wall", "Balancing rope", "Obstacle pipe", "Stepping stones" });
            Course(db, "hill_course", "Hill course", 20, 13, 70, 180, 1400, false,
                new[] { "Fallen tree", "Stone wall", "Rope bridge", "Narrow tunnel", "Rocky ford" });
            Course(db, "ranger_course", "Ranger's course", 40, 22, 125, 400, int.MaxValue, false,
                new[] { "Mossy log", "High wall", "Tightrope", "Hollow log", "River stones" });
            Course(db, "cliff_course", "Cliff course", 60, 34, 200, 700, int.MaxValue, false,
                new[] { "Slippery log", "Crumbling wall", "Swaying rope", "Dark crevice", "Leaping stones" });
            Course(db, "sky_course", "Sky course", 80, 48, 300, 1000, int.MaxValue, false,
                new[] { "Narrow beam", "Sheer wall", "Long tightrope", "Cramped pipe", "Gap stones" });
        }

        static readonly ObstacleLook[] Looks = { ObstacleLook.Log, ObstacleLook.Wall, ObstacleLook.Rope, ObstacleLook.Pipe, ObstacleLook.Stones };
        static readonly string[] Actions = { "Walk-across", "Climb-over", "Walk-on", "Squeeze-through", "Cross" };
        static readonly string[] Success =
        {
            "You walk carefully across.", "You climb over the wall.", "You make it across the rope.",
            "You squeeze through.", "You leap across the stones.",
        };
        static readonly string[] Fail =
        {
            "You lose your footing and fall!", "You slip and fall off the wall!", "You slip and fall off the rope!",
            "You get stuck and scrape yourself!", "You slip on a stone and fall in!",
        };

        /// A course of five obstacles at one level: each about 75% safe at that level and certain 30 levels on
        /// (the easiest course's wall and pipe never fail); a fall costs a few hitpoints, more on harder courses.
        static void Course(ContentDb db, string id, string name, int level, double xp, double lapXp, int minDistance, int maxDistance,
            bool starter, string[] names)
        {
            int low = (int)(192 - 2.13 * (level - 1)), high = low + 209;
            var c = new CourseDef { Id = id, Name = name, Level = level, LapXp = lapXp, Obstacles = new ObstacleDef[AgilityCourse.Obstacles] };
            for (int i = 0; i < AgilityCourse.Obstacles; i++)
            {
                bool safe = level == 1 && (i == 1 || i == 3);
                c.Obstacles[i] = new ObstacleDef
                {
                    Name = names[i], Action = Actions[i], Look = Looks[i], Level = level, Xp = xp + (i == 2 ? xp * 0.2 : 0),
                    Examine = "An obstacle on the " + name.ToLowerInvariant() + " (Agility " + level + ").",
                    LowChance = safe ? 256 : low, HighChance = safe ? 256 : high,
                    FailDamage = 1 + level / 12, Success = Success[i], Fail = Fail[i],
                };
            }
            c.Spawns.Add(new StationSpawnRule { MinDistance = minDistance, MaxDistance = maxDistance, Weight = 1, Starter = starter });
            db.Add(c);
        }
    }
}
