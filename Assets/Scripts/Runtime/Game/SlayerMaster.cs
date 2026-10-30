using System.Collections.Generic;
using Epacenur.Content;
using Epacenur.Core;

namespace Epacenur.Runtime
{
    /// What a slayer master does: sets tasks, says how yours is going, and spends your Slayer points.
    public static class SlayerMaster
    {
        static Game G => Game.Instance;

        /// "Get-task": a new task if you have none, otherwise how many are left.
        public static void Talk()
        {
            var s = G.Slayer;
            var db = ContentDb.Current;
            if (s.task != null && s.left > 0)
            {
                GameLog.Add("\"You're still hunting " + Plural(db.Npc(s.task)) + "; you have " + s.left + " to go.\"");
                return;
            }
            if (!Slayer.Assign(s, db, G.Skills, G.Rng))
            {
                GameLog.Add("\"I've nothing for you right now.\"");
                return;
            }
            GameLog.Add("\"Your new task is to kill " + s.left + " " + Plural(db.Npc(s.task)) + ".\" (" + s.points + " Slayer points)");
        }

        static string Plural(NpcDef npc) => npc == null ? "monsters" : npc.Name.ToLowerInvariant() + (npc.Name.EndsWith("s") ? "" : "s");

        /// Right-click: rewards for Slayer points.
        public static void AddOptions(List<MenuOption> options)
        {
            options.Add(new MenuOption("Skip-task", MenuOption.Thing("(" + Slayer.SkipCost + " points)"), () =>
            {
                if (G.Slayer.task == null)
                    GameLog.Add("You don't have a task to skip.");
                else if (Slayer.Skip(G.Slayer))
                    GameLog.Add("\"Fine, forget that one.\" You have " + G.Slayer.points + " Slayer points left.");
                else
                    GameLog.Add("You need " + Slayer.SkipCost + " Slayer points to skip a task. You have " + G.Slayer.points + ".");
            }));
            options.Add(new MenuOption("Buy", MenuOption.Item("Slayer helmet") + " (" + SlayerModule.HelmetCost + " points)", () =>
            {
                if (G.Slayer.points < SlayerModule.HelmetCost)
                    GameLog.Add("You need " + SlayerModule.HelmetCost + " Slayer points for a slayer helmet. You have " + G.Slayer.points + ".");
                else if (G.Inventory.Add(SlayerModule.Helmet) == 0)
                    GameLog.Add("You don't have room for it.");
                else
                {
                    G.Slayer.points -= SlayerModule.HelmetCost;
                    GameLog.Add("You buy a slayer helmet. Wear it on task to hit harder and more often.");
                }
            }));
        }

        /// The line under the minimap: "Task: 12 goblins left".
        public static string Status()
        {
            var s = G.Slayer;
            if (s.task == null || s.left <= 0)
                return null;
            return "Task: " + s.left + " " + Plural(ContentDb.Current.Npc(s.task)) + " left";
        }
    }
}
