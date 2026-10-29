using System;
using System.Numerics;
using Epacenur.Core;
using UnityEngine;

namespace Epacenur.Runtime
{
    /// Keeps skills, the inventory and where you stood between sessions (in PlayerPrefs).
    public static class SaveGame
    {
        public const string Key = "Epacenur.Save.v1";

        [Serializable]
        class Data
        {
            public string[] skills;       // SkillId names
            public string[] xpTenths;     // experience in tenths, as text (it can be far bigger than a long)
            public int[] itemIds;
            public int[] counts;
            public float x, z;
        }

        public static void Save(Game game)
        {
            var defs = SkillDefs.All;
            var d = new Data
            {
                skills = new string[defs.Length],
                xpTenths = new string[defs.Length],
                itemIds = new int[Inventory.Size],
                counts = new int[Inventory.Size],
                x = game.Player.transform.position.x,
                z = game.Player.transform.position.z,
            };
            for (int i = 0; i < defs.Length; i++)
            {
                d.skills[i] = defs[i].Id.ToString();
                d.xpTenths[i] = game.Skills.GetXpTenths(defs[i].Id).ToString();
            }
            for (int i = 0; i < Inventory.Size; i++)
            {
                d.itemIds[i] = game.Inventory[i].ItemId;
                d.counts[i] = game.Inventory[i].Count;
            }
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(d));
            PlayerPrefs.Save();
        }

        /// Returns false when there was no save.
        public static bool Load(Game game)
        {
            if (!PlayerPrefs.HasKey(Key))
                return false;
            Data d;
            try
            {
                d = JsonUtility.FromJson<Data>(PlayerPrefs.GetString(Key));
            }
            catch (Exception e)
            {
                Debug.LogWarning("Epacenur: ignoring an unreadable save (" + e.Message + ").");
                return false;
            }
            if (d == null)
                return false;

            for (int i = 0; d.skills != null && i < d.skills.Length && i < d.xpTenths.Length; i++)
                if (Enum.TryParse(d.skills[i], out SkillId id) && BigInteger.TryParse(d.xpTenths[i], out var tenths))
                    game.Skills.SetXpTenths(id, tenths);

            for (int i = 0; d.itemIds != null && i < Inventory.Size && i < d.itemIds.Length; i++)
                if (ItemDefs.Get(d.itemIds[i]) != null)
                    game.Inventory.Set(i, new ItemStack { ItemId = d.itemIds[i], Count = d.counts[i] });

            var tile = game.World.NearestWalkable(new Tile(Mathf.RoundToInt(d.x), Mathf.RoundToInt(d.z)));
            game.Player.Teleport(tile);
            return true;
        }

        public static void Delete() => PlayerPrefs.DeleteKey(Key);
    }
}
