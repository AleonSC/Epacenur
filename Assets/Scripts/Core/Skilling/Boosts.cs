using System;
using System.Collections.Generic;

namespace Epacenur.Core
{
    /// Skills raised above their level for a while (potions). A boost wears off by a level a minute.
    /// A new boost replaces a smaller one; it never stacks on top.
    public sealed class Boosts
    {
        public const float DecaySeconds = 60f;
        readonly Dictionary<string, int> amounts = new Dictionary<string, int>();

        public int Of(string skill) => amounts.TryGetValue(skill, out int n) ? n : 0;

        /// The level used for everything that checks it: the real level plus any boost.
        public int Level(SkillSet skills, string skill) => skills.GetLevel(skill) + Of(skill);

        /// Applies a boost; returns how much the skill is raised by now.
        public int Apply(SkillSet skills, SkillBoost b)
        {
            int amount = b.Flat + (int)Math.Floor(skills.GetLevel(b.Skill) * b.Percent);
            if (amount > Of(b.Skill))
                amounts[b.Skill] = amount;
            return Of(b.Skill);
        }

        /// Every boost falls by one (call once a minute).
        public void Decay()
        {
            foreach (var k in new List<string>(amounts.Keys))
            {
                if (amounts[k] <= 1)
                    amounts.Remove(k);
                else
                    amounts[k]--;
            }
        }

        public void Clear() => amounts.Clear();
        public bool Any => amounts.Count > 0;
    }
}
