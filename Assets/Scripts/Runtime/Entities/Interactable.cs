using Epacenur.Core;
using UnityEngine;

namespace Epacenur.Runtime
{
    /// Something on a tile you click to use: a tree, a bank booth, a fire, the shop keeper.
    /// Tile mode walks you next to it first; in first person it has to be within reach.
    public abstract class Interactable : MonoBehaviour
    {
        public Tile Tile { get; protected set; }
        /// The verb and name for the hover text, e.g. "Trade" + "Shop keeper".
        public abstract string Action { get; }
        public abstract string DisplayName { get; }
        public abstract string Examine { get; }
        /// False when it can only be examined (a tree stump).
        public virtual bool HasAction => true;
        /// Where you have to stand to use it (an agility obstacle's start), or null for any tile beside it.
        public virtual Tile? ApproachTile => null;

        /// More right-click options after its main one (a slayer master's rewards).
        public virtual void AddOptions(System.Collections.Generic.List<MenuOption> options) { }
        public abstract void Interact();
    }
}
