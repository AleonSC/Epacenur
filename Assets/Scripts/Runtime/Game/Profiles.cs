using System;
using System.Collections.Generic;
using UnityEngine;

namespace Epacenur.Runtime
{
    /// A saved character: their skills, items, bank and where they stand in each world.
    [Serializable]
    public sealed class CharacterInfo
    {
        public string id, name;
        public int totalLevel, combatLevel;
        public long created, lastPlayed;  // DateTime ticks (UTC)
    }

    /// A saved world: an overworld made from a seed. Characters can play in any world.
    [Serializable]
    public sealed class WorldInfo
    {
        public string id, name;
        public int seed;
        public long created, lastPlayed;
    }

    /// The lists of characters and worlds the main menu shows (in PlayerPrefs, as JSON). Each character's
    /// progress is saved under its own key (SaveGame).
    public static class Profiles
    {
        const string CharactersKey = "Epacenur.Characters", WorldsKey = "Epacenur.Worlds", LastKey = "Epacenur.LastPlayed";
        public const int DefaultSeed = 2024;

        [Serializable] class CharacterList { public List<CharacterInfo> items = new List<CharacterInfo>(); }
        [Serializable] class WorldList { public List<WorldInfo> items = new List<WorldInfo>(); }
        [Serializable] class Last { public string character, world; }

        static CharacterList characters;
        static WorldList worlds;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            characters = null;
            worlds = null;
        }

        public static List<CharacterInfo> Characters { get { Load(); return characters.items; } }
        public static List<WorldInfo> Worlds { get { Load(); return worlds.items; } }

        static void Load()
        {
            if (characters != null)
                return;
            characters = Read<CharacterList>(CharactersKey) ?? new CharacterList();
            worlds = Read<WorldList>(WorldsKey) ?? new WorldList();
            bool changed = false;
            // There's always at least one world: the original.
            if (worlds.items.Count == 0)
            {
                worlds.items.Add(NewWorldInfo("Epacenur", DefaultSeed));
                changed = true;
            }
            // A save from before characters becomes the first character.
            if (characters.items.Count == 0 && PlayerPrefs.HasKey(SaveGame.LegacyKey))
            {
                var c = NewCharacterInfo("Adventurer");
                PlayerPrefs.SetString(SaveGame.KeyFor(c), PlayerPrefs.GetString(SaveGame.LegacyKey));
                PlayerPrefs.DeleteKey(SaveGame.LegacyKey);
                characters.items.Add(c);
                SetLast(c, worlds.items[0]);
                changed = true;
            }
            if (changed)
                Write();
        }

        static T Read<T>(string key) where T : class
        {
            if (!PlayerPrefs.HasKey(key))
                return null;
            try { return JsonUtility.FromJson<T>(PlayerPrefs.GetString(key)); }
            catch (Exception e) { Debug.LogWarning("Epacenur: ignoring unreadable " + key + " (" + e.Message + ")."); return null; }
        }

        public static void Write()
        {
            PlayerPrefs.SetString(CharactersKey, JsonUtility.ToJson(characters));
            PlayerPrefs.SetString(WorldsKey, JsonUtility.ToJson(worlds));
            PlayerPrefs.Save();
        }

        static string NewId() => Guid.NewGuid().ToString("N").Substring(0, 12);

        static CharacterInfo NewCharacterInfo(string name)
        {
            long now = DateTime.UtcNow.Ticks;
            return new CharacterInfo { id = NewId(), name = name, totalLevel = 0, combatLevel = 3, created = now, lastPlayed = 0 };
        }

        static WorldInfo NewWorldInfo(string name, int seed)
        {
            return new WorldInfo { id = NewId(), name = name, seed = seed, created = DateTime.UtcNow.Ticks };
        }

        /// Why a name can't be used, or null. Names are 1-12 letters, digits and spaces, and unique.
        public static string NameProblem(string name, bool character)
        {
            name = (name ?? "").Trim();
            if (name.Length == 0)
                return "Type a name first.";
            if (name.Length > 12)
                return "Names can be at most 12 characters.";
            foreach (char ch in name)
                if (!char.IsLetterOrDigit(ch) && ch != ' ')
                    return "Names can only have letters, digits and spaces.";
            if (character ? Characters.Exists(c => string.Equals(c.name, name, StringComparison.OrdinalIgnoreCase))
                          : Worlds.Exists(w => string.Equals(w.name, name, StringComparison.OrdinalIgnoreCase)))
                return "There's already one called " + name + ".";
            return null;
        }

        public static CharacterInfo CreateCharacter(string name)
        {
            var c = NewCharacterInfo(name.Trim());
            Characters.Add(c);
            Write();
            return c;
        }

        public static WorldInfo CreateWorld(string name, int seed)
        {
            var w = NewWorldInfo(name.Trim(), seed);
            Worlds.Add(w);
            Write();
            return w;
        }

        public static void DeleteCharacter(CharacterInfo c)
        {
            Characters.Remove(c);
            PlayerPrefs.DeleteKey(SaveGame.KeyFor(c));
            Write();
        }

        /// Deleting a world keeps the characters; they just forget where they stood in it.
        public static void DeleteWorld(WorldInfo w)
        {
            Worlds.Remove(w);
            SaveGame.DeletePiles(w);
            Write();
        }

        public static void SetLast(CharacterInfo c, WorldInfo w)
        {
            PlayerPrefs.SetString(LastKey, JsonUtility.ToJson(new Last { character = c.id, world = w.id }));
        }

        /// The character and world played last (either may be null).
        public static void GetLast(out CharacterInfo c, out WorldInfo w)
        {
            var last = Read<Last>(LastKey);
            c = last == null ? null : Characters.Find(x => x.id == last.character);
            w = last == null ? null : Worlds.Find(x => x.id == last.world);
        }

        /// Wipes every character and world (Epacenur menu in the editor).
        public static void DeleteAll()
        {
            foreach (var c in Characters)
                PlayerPrefs.DeleteKey(SaveGame.KeyFor(c));
            foreach (var w in Worlds)
                SaveGame.DeletePiles(w);
            PlayerPrefs.DeleteKey(CharactersKey);
            PlayerPrefs.DeleteKey(WorldsKey);
            PlayerPrefs.DeleteKey(LastKey);
            PlayerPrefs.DeleteKey(SaveGame.LegacyKey);
            PlayerPrefs.Save();
            ResetStatics();
        }
    }
}
