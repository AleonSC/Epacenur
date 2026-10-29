using Epacenur.Core;
using UnityEngine;

namespace Epacenur.Runtime
{
    /// Something on a tile you click to use: the bank booth, the shop keeper.
    /// Tile mode walks you next to it first; in first person it has to be within reach.
    public abstract class Interactable : MonoBehaviour
    {
        public Tile Tile { get; protected set; }
        /// The verb and name for the hover text, e.g. "Trade" + "Shop keeper".
        public abstract string Action { get; }
        public abstract string DisplayName { get; }
        public abstract string Examine { get; }
        public abstract void Interact();
    }
}
