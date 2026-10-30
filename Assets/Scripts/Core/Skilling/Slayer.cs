using System;
using System.Collections.Generic;

namespace Epacenur.Core
{
    /// Your Slayer task: which monster, how many are left, and the points you've earned for finished tasks.
    [Serializable]
    public sealed class SlayerState
    {
        public string task;      // the monster's id, or null with no task
        public int left, assigned;
        public int points, done; // points to spend; tasks finished in a row
    }

    public static class Slayer
    {
        public const string Skill = "slayer";
        public const int PointsPerTask = 10, BonusEvery = 10, BonusPoints = 50, SkipCost = 10;

        /// Monsters a master might set you: ones the world has (they have spawns), that aren't townsfolk, that your
        /// Slayer level lets you hurt, and no tougher than you can handle (combat level up to your own + 10).
        public static List<NpcDef> Candidates(ContentDb db, SkillSet skills)
        {
            int combat = CombatRules.CombatLevel(skills), slayer = skills.GetLevel(Skill);
            var list = new List<NpcDef>();
            foreach (var n in db.Npcs)
                if (n.Spawns.Count > 0 && n.Pickpocket == null && !n.Boss && n.SlayerLevel <= slayer && n.CombatLevel <= combat + 10 && n.CombatLevel >= combat / 4)
                    list.Add(n);
            return list;
        }

        /// A new task: a monster from the candidates and how many (more for weaker monsters). False if there's none.
        public static bool Assign(SlayerState s, ContentDb db, SkillSet skills, Random rng)
        {
            var list = Candidates(db, skills);
            if (list.Count == 0)
                return false;
            var npc = list[rng.Next(list.Count)];
            s.task = npc.Id;
            s.assigned = s.left = 10 + rng.Next(11) + (npc.CombatLevel < 10 ? 10 : 0);
            return true;
        }

        /// A kill: on task, it counts down and gives Slayer xp (the monster's hitpoints). Returns the xp (0: not on task).
        /// When the task is done, points are added (more every tenth task in a row) and the task cleared.
        public static double Kill(SlayerState s, NpcDef npc, out bool finished)
        {
            finished = false;
            if (s.task == null || s.task != npc.Id || s.left <= 0)
                return 0;
            s.left--;
            if (s.left == 0)
            {
                finished = true;
                s.done++;
                s.points += s.done % BonusEvery == 0 ? BonusPoints : PointsPerTask;
                s.task = null;
            }
            return npc.Hitpoints;
        }

        /// Gives up the task for SkipCost points (true if you had them). Finishing streaks aren't lost.
        public static bool Skip(SlayerState s)
        {
            if (s.task == null || s.points < SkipCost)
                return false;
            s.points -= SkipCost;
            s.task = null;
            s.left = 0;
            return true;
        }
    }
}
