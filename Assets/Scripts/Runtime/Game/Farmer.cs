using System.Collections.Generic;
using Epacenur.Core;

namespace Epacenur.Runtime
{
    /// What a town farm's farmer does: tells you what he has to rent out, and rents you his patches (right-click),
    /// once each, for coins. The patches are in the fenced field by his farmhouse.
    public static class Farmer
    {
        static Game G => Game.Instance;

        /// The farm whose farmer stands on this tile, or null.
        static Building FarmOf(Tile farmer)
        {
            var ow = G.World.Overworld;
            if (ow == null || G.World.Map != MapId.Overworld)
                return null;
            return ow.PlanAt(farmer).Buildings.Find(b => b.Kind == BuildingKind.Farm && b.Feature == farmer);
        }

        static PatchKind KindOf(int i) => i % 2 == 0 ? PatchKind.Herb : PatchKind.Allotment;

        static string Name(PatchKind kind, int n = 1) => kind == PatchKind.Herb ? (n == 1 ? "herb patch" : "herb patches") : (n == 1 ? "allotment" : "allotments");

        /// The farm's patches of a kind that you don't rent yet.
        static List<Tile> ToRent(Building farm, PatchKind kind)
        {
            var free = new List<Tile>();
            for (int i = 0; i < farm.Patches.Count; i++)
                if (KindOf(i) == kind && !G.World.Patch(farm.Patches[i]).rented)
                    free.Add(farm.Patches[i]);
            return free;
        }

        /// "Talk-to": what's left to rent, and at what price.
        public static void Talk(Tile farmer)
        {
            var farm = FarmOf(farmer);
            if (farm == null)
            {
                GameLog.Add("\"Fine weather for growing.\"");
                return;
            }
            int allotments = ToRent(farm, PatchKind.Allotment).Count, herbs = ToRent(farm, PatchKind.Herb).Count;
            if (allotments + herbs == 0)
            {
                GameLog.Add("\"Every patch in my field is yours now. Look after them!\"");
                return;
            }
            var offers = new List<string>();
            if (allotments > 0)
                offers.Add(allotments + " " + Name(PatchKind.Allotment, allotments) + " (" + Farming.RentCost(PatchKind.Allotment) + " coins each)");
            if (herbs > 0)
                offers.Add(herbs + " " + Name(PatchKind.Herb, herbs) + " (" + Farming.RentCost(PatchKind.Herb) + " coins each)");
            GameLog.Add("\"I've " + string.Join(" and ", offers) + " to rent out in my field. Pay once and the patch is yours.\" (Right-click him to rent one.)");
        }

        /// Right-click: rent an allotment, or a herb patch.
        public static void AddOptions(Tile farmer, List<MenuOption> options)
        {
            var farm = FarmOf(farmer);
            if (farm == null)
                return;
            foreach (var kind in new[] { PatchKind.Allotment, PatchKind.Herb })
            {
                if (ToRent(farm, kind).Count == 0)
                    continue;
                var k = kind;
                options.Add(new MenuOption("Rent", MenuOption.Thing(Name(kind)) + " (" + Farming.RentCost(kind) + " coins)", () => Rent(farm, k)));
            }
        }

        static void Rent(Building farm, PatchKind kind)
        {
            var free = ToRent(farm, kind);
            if (free.Count == 0)
            {
                GameLog.Add("You already rent all his " + Name(kind, 2) + ".");
                return;
            }
            string problem = Farming.Rent(G.World.Patch(free[0]), kind, G.Inventory);
            if (problem != null)
            {
                GameLog.Add(problem);
                return;
            }
            Sfx.Play(Sound.Coins);
            GameLog.Add("\"It's yours: plant what you like.\" You rent " + (kind == PatchKind.Herb ? "a herb patch" : "an allotment") + " in the farmer's field.");
        }
    }
}
