using Epacenur.Core;
using UnityEngine;

namespace Epacenur.Runtime
{
    /// Drinking potions, and an item's own verb that makes something from it in your hands (cleaning a herb).
    public sealed partial class PlayerController
    {
        public void Drink(int slot)
        {
            if (eatCooldown > 0f)
                return;
            var p = Potions.Drink(G.Inventory, slot, G.Skills, G.Boosts, G.Prayer, G.Energy);
            if (p == null)
                return;
            float delay = Potions.DrinkTicks * GameTime.TickSeconds;
            eatCooldown = delay;
            GameLog.Add(p.Message);
            var left = ContentDb.Current.Item(p.Leaves);
            if (left != null && left.Potion != null)
            {
                int doses = left.Id[left.Id.Length - 1] - '0';  // "attack_potion_2"
                GameLog.Add("You have " + doses + " dose" + (doses == 1 ? "" : "s") + " of potion left.");
            }
            else
                GameLog.Add("You have finished your potion.");
        }

        /// "Clean" on a grimy herb: makes its recipe once, in your hands.
        public void Process(int slot)
        {
            var def = G.Inventory[slot].Def;
            var recipe = def != null ? ContentDb.Current.Recipe(def.Process) : null;
            if (recipe != null)
                StartMaking(null, recipe, 1);
        }
    }
}
