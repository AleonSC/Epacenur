namespace Epacenur.Core
{
    /// Checking an item's level requirements (ItemDef.Requires).
    public static class Requirements
    {
        /// The first requirement not met, or null if all are (or there's no skill set to check).
        public static SkillReq? Missing(ItemDef def, SkillSet skills)
        {
            if (def == null || skills == null)
                return null;
            foreach (var r in def.Requires)
                if (skills.GetLevel(r.Skill) < r.Level)
                    return r;
            return null;
        }

        public static bool Meets(ItemDef def, SkillSet skills) => Missing(def, skills) == null;

        /// "You need an Attack level of 20 to wield the mithril sword.", or null if you can.
        public static string Problem(ItemDef def, SkillSet skills, string verb)
        {
            var miss = Missing(def, skills);
            if (miss == null)
                return null;
            string skill = ContentDb.Current.Skill(miss.Value.Skill).Name;
            string a = "AEIOU".IndexOf(skill[0]) >= 0 ? "an " : "a ";
            return "You need " + a + skill + " level of " + miss.Value.Level + " to " + verb + " the " + def.Name.ToLowerInvariant() + ".";
        }
    }
}
