using Epacenur.Content;
using Epacenur.Core;
using UnityEngine;

namespace Epacenur.Runtime
{
    /// Laying hunter's traps, and harvesting farming patches.
    public sealed partial class PlayerController
    {
        /// "Lay" on a trap in the inventory: it goes down on your tile.
        public void LayTrap(int slot)
        {
            var def = G.Inventory[slot].Def;
            if (def == null || def.TrapKind == null)
                return;
            string problem = Requirements.Problem(def, G.Skills, "lay");
            if (problem != null)
            {
                GameLog.Add(problem);
                return;
            }
            int level = G.Skills.GetLevel(Hunter.Skill), max = Hunter.MaxTraps(level);
            if (G.World.Traps.Count >= max)
            {
                GameLog.Add("You can only have " + max + " trap" + (max == 1 ? "" : "s") + " laid at your Hunter level.");
                return;
            }
            var tile = CurrentTile;
            foreach (var t in G.World.Traps)
                if (t.Tile == tile)
                {
                    GameLog.Add("There's already a trap here.");
                    return;
                }
            CancelAction();
            G.Inventory.RemoveAt(slot);
            G.World.AddTrap(tile, def);
            Kneel(0.8f);
            GameLog.Add("You lay the " + def.Name.ToLowerInvariant() + ". Step away and wait for something to come along.");
        }

        /// Harvest a grown patch, one at a time, until it's empty or you're full.
        public void StartHarvest(PatchObject patch)
        {
            CancelAction();
            Activity = new HarvestActivity(patch);
        }

        sealed class HarvestActivity : Activity
        {
            readonly PatchObject patch;

            public HarvestActivity(PatchObject patch) : base(1) { this.patch = patch; }

            public override string Status => "Farming...";
            public override Transform Target => patch != null ? patch.transform : null;
            public override bool Kneels => true;
            protected override int Interval => 3;

            public override bool StillValid(PlayerController p) => patch != null
                && (p.Mode != ViewMode.Tile || p.CurrentTile.IsCardinalNeighbour(patch.Tile) || p.FlatDistance(patch) < 1.6f);

            protected override bool Step(PlayerController p) => patch.HarvestOne();
        }
    }
}
