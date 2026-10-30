using System;

namespace Epacenur.Core
{
    public static class Thieving
    {
        /// Seconds you're stunned when caught picking a pocket.
        public const float StunSeconds = 5f;
        /// Ticks between attempts at the same pocket.
        public const int PickTicks = 2;

        /// Chance (0..1) of picking someone's pocket without being caught.
        public static double Chance(PickpocketDef p, int level) => Gathering.Chance(p.LowChance, p.HighChance, level);
    }
}
