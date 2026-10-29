using Epacenur.Content;
using Epacenur.Core;
using UnityEngine;

namespace Epacenur.Runtime
{
    /// How the right arm moves during an activity.
    public enum ArmPose { None, Swing, Hammer, HoldOut, NetOut, Strike }

    /// Something the player keeps doing, a game tick at a time, until it's done or interrupted: chopping
    /// a tree, smelting bars, lighting a fire. The player runs one at a time (PlayerController.Activity).
    /// To add a new kind: subclass this and start it from the player.
    public abstract class Activity
    {
        protected static Game G => Game.Instance;

        /// Shown top left while nothing's under the mouse, e.g. "Woodcutting...".
        public abstract string Status { get; }
        /// What the player turns to face.
        public abstract Transform Target { get; }
        /// The tool shown in the hand (null: whatever's wielded).
        public virtual ItemDef Tool => null;
        public virtual ArmPose Pose => ArmPose.None;
        public virtual bool Kneels => false;

        int ticksLeft;

        protected Activity(int firstTickIn) { ticksLeft = firstTickIn; }

        /// Checked every frame; false stops the activity (the target's gone, or you've moved away).
        public abstract bool StillValid(PlayerController p);

        /// Called every game tick. Returns false once the activity is finished.
        public bool Tick(PlayerController p)
        {
            if (--ticksLeft > 0)
                return true;
            ticksLeft = Interval;
            return Step(p);
        }

        /// Ticks between steps.
        protected abstract int Interval { get; }

        /// One step (an attempt, one item made). Returns false once finished.
        protected abstract bool Step(PlayerController p);
    }

    /// Gathering from a node: chopping, mining, fishing. Each attempt rolls the node's yields.
    public sealed class GatherActivity : Activity
    {
        readonly NodeObject node;
        readonly Tile startTile;

        public GatherActivity(NodeObject node) : base(1)  // the first attempt a tick in
        {
            this.node = node;
            startTile = node.Tile;
        }

        public override string Status => ContentDb.Current.Skill(node.Def.Skill).Name + "...";
        public override Transform Target => node != null ? node.transform : null;
        public override ItemDef Tool => node != null ? FindTool(node.Def) : null;
        public override ArmPose Pose => node.Def.Look == NodeLook.FishingSpot ? ArmPose.NetOut : ArmPose.Swing;
        protected override int Interval => node.Def.AttemptTicks;

        static ItemDef FindTool(NodeDef def) => def.ToolType == null ? null : ToolBelt.Find(G.ToolBelt, G.Inventory, def.ToolType);

        public override bool StillValid(PlayerController p)
        {
            if (node == null || node.IsEmpty)
                return false;
            if (node.Tile != startTile)
            {
                GameLog.Add("The " + node.Def.Name.ToLowerInvariant() + " has moved.");
                return false;
            }
            return p.Mode != ViewMode.Tile || p.CurrentTile.IsCardinalNeighbour(node.Tile);
        }

        protected override bool Step(PlayerController p) => Attempt(node);

        /// Why the node can't be used right now (said in the chat box), or null.
        public static string Problem(NodeObject node)
        {
            var def = node.Def;
            if (def.ToolType != null && FindTool(def) == null)
                return def.NeedToolMessage;
            int level = G.Skills.GetLevel(def.Skill);
            if (level < def.MinLevel)
                return string.Format(def.LevelMessage, def.MinLevel);
            if (node.IsEmpty)
                return def.EmptyMessage;
            if (!HasRoom(def, level))
                return def.FullMessage;
            return null;
        }

        static bool HasRoom(NodeDef def, int level)
        {
            foreach (var y in def.Yields)
                if (level >= y.Level && G.Inventory.HasRoomFor(y.ItemId))
                    return true;
            return false;
        }

        /// One attempt: maybe an item, xp and one of the node's lives. Returns whether gathering can go on
        /// (false when the node is used up or the inventory is full).
        public static bool Attempt(NodeObject node)
        {
            if (node.IsEmpty)
                return false;
            var def = node.Def;
            int level = G.Skills.GetLevel(def.Skill);
            if (!HasRoom(def, level))
            {
                GameLog.Add(def.FullMessage);
                return false;
            }
            node.Shake();
            var got = Gathering.Roll(def, level, G.Rng);
            if (got == null || !G.Inventory.HasRoomFor(got.ItemId))
                return true;  // nothing this time
            G.Inventory.Add(got.ItemId);
            G.Skills.AddXp(def.Skill, got.Xp);
            GameLog.Add(got.Message);
            if (node.TakeLife())
                return false;
            if (!HasRoom(def, level))
            {
                GameLog.Add(def.FullMessage);
                return false;
            }
            return true;
        }
    }

    /// Making things at a station: smelting at a furnace, smithing at an anvil, cooking on a fire.
    /// Makes `count` (int.MaxValue: as many as you can) of the chosen recipe; with none chosen it goes
    /// through every recipe the station has that you can make (everything raw you carry, on a fire).
    public sealed class MakeActivity : Activity
    {
        public const float Reach = 2.6f;

        readonly StationObject station;
        readonly string stationName;  // kept: a fire can burn out (be destroyed) mid-activity
        readonly RecipeDef chosen;
        int left;
        RecipeDef current;

        MakeActivity(StationObject station, RecipeDef chosen, RecipeDef first, int count) : base(1)
        {
            this.station = station;
            stationName = station.Def.Name.ToLowerInvariant();
            this.chosen = chosen;
            current = first;
            left = count;
        }

        public override string Status => station.Def.Action + "ing...";
        public override Transform Target => station != null ? station.transform : null;
        public override ItemDef Tool => current != null && current.ToolType != null ? ToolBelt.Find(G.ToolBelt, G.Inventory, current.ToolType) : null;
        public override ArmPose Pose => current != null && current.ToolType == "hammer" ? ArmPose.Hammer : ArmPose.HoldOut;
        protected override int Interval => current != null ? current.Ticks : 4;

        /// Starts making at a station, or says why not and returns null. `prefer`: with no recipe chosen,
        /// an item to use first (the raw fish you clicked "Cook" on).
        public static MakeActivity Start(StationObject station, RecipeDef recipe, int count, string prefer = null)
        {
            var first = recipe ?? Next(station, prefer);
            if (first == null)
            {
                var recipes = ContentDb.Current.RecipesAt(station.Def.Id);
                if (recipes.Count == 1)
                    GameLog.Add(Problem(recipes[0]) ?? "You can't make anything here.");
                else
                    GameLog.Add("You have nothing to " + station.Def.Action.ToLowerInvariant() + ".");
                return null;
            }
            string problem = Problem(first);
            if (problem != null)
            {
                GameLog.Add(problem);
                return null;
            }
            return new MakeActivity(station, recipe, first, count) { prefer = prefer };
        }

        string prefer;

        static string Problem(RecipeDef r)
        {
            bool hasTool = r.ToolType == null || ToolBelt.Find(G.ToolBelt, G.Inventory, r.ToolType) != null;
            return Making.Problem(r, G.Inventory, G.Skills.GetLevel(r.Skill), hasTool);
        }

        /// The next recipe at the station you have the inputs for (the one using `prefer` first).
        static RecipeDef Next(StationObject station, string prefer)
        {
            RecipeDef any = null;
            foreach (var r in ContentDb.Current.RecipesAt(station.Def.Id))
            {
                if (Making.CanMake(r, G.Inventory) < 1)
                    continue;
                if (prefer != null && System.Array.Exists(r.Inputs, i => i.ItemId == prefer))
                    return r;
                if (any == null)
                    any = r;
            }
            return any;
        }

        public override bool StillValid(PlayerController p)
        {
            if (station == null)
            {
                GameLog.Add("The " + stationName + " has gone out.");
                return false;
            }
            return p.FlatDistance(station) <= Reach;
        }

        protected override bool Step(PlayerController p)
        {
            var r = chosen ?? Next(station, prefer);
            if (r == null)
                return false;
            current = r;
            string problem = Problem(r);
            if (problem != null)
            {
                GameLog.Add(problem);
                return false;
            }
            string made = Making.Make(r, G.Inventory, G.Skills.GetLevel(r.Skill), G.Rng, out bool failed);
            if (made == null)
                return false;
            if (failed)
            {
                if (r.FailMessage != null)
                    GameLog.Add(r.FailMessage);
            }
            else
            {
                G.Skills.AddXp(r.Skill, r.Xp);
                if (r.SuccessMessage != null)
                    GameLog.Add(r.SuccessMessage);
            }
            left--;
            if (left <= 0)
                return false;
            return chosen != null ? Making.CanMake(chosen, G.Inventory) > 0 : Next(station, prefer) != null;
        }
    }

    /// Lighting logs on the ground with a tinderbox: an attempt every few ticks until they catch.
    public sealed class LightActivity : Activity
    {
        readonly GroundItem logs;

        public LightActivity(GroundItem logs) : base(1)
        {
            this.logs = logs;
            GameLog.Add("You attempt to light the logs.");
        }

        public override string Status => ContentDb.Current.Skill(FiremakingModule.Skill).Name + "...";
        public override Transform Target => logs != null ? logs.transform : null;
        public override ArmPose Pose => ArmPose.Strike;
        public override bool Kneels => true;
        protected override int Interval => Firemaking.AttemptTicks;

        public override bool StillValid(PlayerController p) => logs != null;  // someone took them, or they vanished

        protected override bool Step(PlayerController p)
        {
            var burn = ContentDb.Current.Burnable(logs.ItemId);
            if (G.Rng.NextDouble() >= Firemaking.LightChance(burn, G.Skills.GetLevel(FiremakingModule.Skill)))
                return true;
            Tile tile = logs.Tile;
            G.World.RemoveGroundItem(logs);
            G.World.AddFire(tile, Random.Range(burn.MinBurnSeconds, burn.MaxBurnSeconds));
            G.Skills.AddXp(FiremakingModule.Skill, burn.Xp);
            GameLog.Add("The fire catches and the logs begin to burn.");
            p.StepOffFire(tile);
            return false;
        }

        /// Why these logs can't be lit on that tile, or null.
        public static string Problem(BurnableDef burn, Tile tile)
        {
            if (ToolBelt.Find(G.ToolBelt, G.Inventory, "tinderbox") == null)
                return "You need a tinderbox to light a fire.";
            if (G.Skills.GetLevel(FiremakingModule.Skill) < burn.Level)
                return "You need a Firemaking level of " + burn.Level + " to burn these logs.";
            foreach (var f in G.World.Fires)
                if (f.Tile == tile)
                    return "You can't light a fire here.";
            return null;
        }
    }
}
