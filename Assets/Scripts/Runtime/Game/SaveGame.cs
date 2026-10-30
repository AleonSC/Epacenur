using System;
using System.Collections.Generic;
using System.Numerics;
using Epacenur.Content;
using Epacenur.Core;
using UnityEngine;

namespace Epacenur.Runtime
{
    /// Keeps a character's skills, inventory and money pouch, bank, worn equipment, tool belt and where they
    /// stood in each world between sessions (in PlayerPrefs, as JSON, one key per character). Items and
    /// skills are saved by their text ids, so adding content never breaks a save; ids the game no longer
    /// knows are dropped.
    public static class SaveGame
    {
        /// Where the one save lived before there were characters (Profiles turns it into one).
        public const string LegacyKey = "Epacenur.Save.v1";
        public static string KeyFor(CharacterInfo c) => "Epacenur.Character." + c.id;
        public const int Format = 3;        // 3: text ids. Older saves used numbers (see LegacyItemIds).
        public const int WorldVersion = 3;  // 3: the endless overworld. Older saves start in the town square.

        [Serializable]
        class Data
        {
            public int format;
            public string[] skills;       // skill ids
            public string[] xpTenths;     // experience in tenths, as text (it can be far bigger than a long)
            public string[] items;        // inventory, by slot ("" = empty)
            public int[] counts;
            public string[] bank;
            public long[] bankCounts;
            public string[] equipped;     // by EquipSlot
            public string[] tools;        // on the tool belt
            public int style;
            public string autocast;       // spell id, or null
            public SlayerState slayer;
            public MarketState market;    // trading post offers and prices
            public bool hasEnergy;
            public float energy;          // run energy, percent
            public int pouch;
            public int hp;
            public int prayerPoints;
            public float x, z;            // older formats: where you stood
            public int tileX, tileZ, map;  // format 3 before worlds: where you stood
            public int worldVersion;
            // Where you stand in each world (by world id): map (MapId) and tile.
            public string[] placeWorlds;
            public int[] placeMaps, placeX, placeZ;
            public bool hasGrave;
            public Grave grave;

            // Older formats (read only).
            public int[] itemIds, bankIds, equipIds, toolIds;
            public bool hasPouch, hatchetGiven, miningToolsGiven, hasHp, hasPrayer;
        }

        /// Format 1-2 item numbers, by their old number.
        static readonly string[] LegacyItemIds =
        {
            null, WoodcuttingModule.Logs, ItemIds.Coins, FiremakingModule.Tinderbox, CombatModule.BronzeSword, CombatModule.WoodenShield,
            FiremakingModule.Ashes, FishingModule.SmallFishingNet, FishingModule.RawShrimps, FishingModule.RawAnchovies,
            WoodcuttingModule.BronzeHatchet, CookingModule.Shrimps, CookingModule.Anchovies, CookingModule.BurntShrimp, CookingModule.BurntFish,
            PrayerModule.Bones, MiningModule.CopperOre, MiningModule.TinOre, SmithingModule.BronzeBar, MiningModule.BronzePickaxe,
            SmithingModule.Hammer, SmithingModule.BronzeFullHelm, SmithingModule.BronzePlatebody, SmithingModule.BronzePlatelegs,
        };

        static string Legacy(int id) => id > 0 && id < LegacyItemIds.Length ? LegacyItemIds[id] : null;

        static string[] LegacyAll(int[] ids)
        {
            if (ids == null)
                return null;
            var r = new string[ids.Length];
            for (int i = 0; i < ids.Length; i++)
                r[i] = Legacy(ids[i]);
            return r;
        }

        public static void Save(Game game)
        {
            var old = Read(KeyFor(game.Character));
            var skills = ContentDb.Current.Skills;
            // In a dungeon you're saved outside its cave mouth (dungeons aren't kept to come back to inside).
            game.World.SurfacePlace(game.Player.CurrentTile, out MapId spotMap, out Tile spot);
            var d = new Data
            {
                format = Format,
                skills = new string[skills.Count],
                xpTenths = new string[skills.Count],
                items = new string[Inventory.Size],
                counts = new int[Inventory.Size],
                tileX = spot.X,
                tileZ = spot.Z,
                map = (int)spotMap,
                worldVersion = WorldVersion,
                style = game.StyleIndex,
                autocast = game.Autocast?.Id,
                slayer = game.Slayer,
                hasEnergy = true,
                energy = (float)game.Energy.Value,
                pouch = game.Inventory.PouchCoins,
                hp = game.Health.Current,
                prayerPoints = game.Prayer.Points,
                tools = new List<string>(game.ToolBelt.Owned).ToArray(),
            };
            for (int i = 0; i < skills.Count; i++)
            {
                d.skills[i] = skills[i].Id;
                d.xpTenths[i] = game.Skills.GetXpTenths(skills[i].Id).ToString();
            }
            for (int i = 0; i < Inventory.Size; i++)
            {
                d.items[i] = game.Inventory[i].ItemId ?? "";
                d.counts[i] = game.Inventory[i].Count;
            }
            d.bank = new string[game.Bank.UsedSlots];
            d.bankCounts = new long[game.Bank.UsedSlots];
            for (int i = 0; i < game.Bank.UsedSlots; i++)
            {
                d.bank[i] = game.Bank[i].ItemId;
                d.bankCounts[i] = game.Bank[i].Count;
            }
            d.equipped = new string[Equipment.SlotCount];
            for (int i = 0; i < Equipment.SlotCount; i++)
                d.equipped[i] = game.Equipment[(EquipSlot)i].ItemId ?? "";

            // Where you are in this world; where you were in the others stays.
            var worlds = new List<string>();
            var maps = new List<int>();
            var xs = new List<int>();
            var zs = new List<int>();
            for (int i = 0; old != null && old.placeWorlds != null && i < old.placeWorlds.Length; i++)
                if (old.placeWorlds[i] != game.WorldInfo.id)
                {
                    worlds.Add(old.placeWorlds[i]);
                    maps.Add(old.placeMaps[i]);
                    xs.Add(old.placeX[i]);
                    zs.Add(old.placeZ[i]);
                }
            worlds.Add(game.WorldInfo.id);
            maps.Add((int)spotMap);
            xs.Add(spot.X);
            zs.Add(spot.Z);
            d.placeWorlds = worlds.ToArray();
            d.placeMaps = maps.ToArray();
            d.placeX = xs.ToArray();
            d.placeZ = zs.ToArray();
            game.Trading.savedAt = DateTime.UtcNow.Ticks;
            d.market = game.Trading;
            d.hasGrave = game.Grave != null;
            d.grave = game.Grave;
            PlayerPrefs.SetString(KeyFor(game.Character), JsonUtility.ToJson(d));
            var state = new WorldState();
            state.items.AddRange(game.World.Piles);
            state.explored.AddRange(game.World.Explored);
            state.patches.AddRange(game.World.SavedPatches);
            foreach (var b in game.World.Bases.All)
                state.bases.Add(BaseSave.From(b));
            state.hasBed = game.BedBase > 0;
            state.bedBase = game.BedBase;
            state.bedCell = game.BedCell;
            state.hasTime = true;
            state.time = game.Clock.Time;
            state.day = game.Clock.Day;
            PlayerPrefs.SetString(PilesKey(game.WorldInfo), JsonUtility.ToJson(state));

            // What the main menu shows.
            game.Character.totalLevel = game.Skills.TotalLevel;
            game.Character.combatLevel = CombatRules.CombatLevel(game.Skills);
            game.Character.lastPlayed = game.WorldInfo.lastPlayed = DateTime.UtcNow.Ticks;
            Profiles.SetLast(game.Character, game.WorldInfo);
            Profiles.Write();
        }

        /// What a world keeps between sessions: the items on its ground, the regions seen, the bases built (and the
        /// bed last slept in), and the time of day.
        [Serializable] public class WorldState
        {
            public List<GroundPile> items = new List<GroundPile>();
            public List<long> explored = new List<long>();
            public List<PatchState> patches = new List<PatchState>();  // farming patches you rent or have something growing in
            public List<BaseSave> bases = new List<BaseSave>();  // what's been built
            public bool hasBed;    // the bed last slept in: its base and cell
            public int bedBase;
            public long bedCell;
            public bool hasTime;   // older saves: no time of day yet
            public float time;     // DayNight.Time
            public int day;
        }

        static string PilesKey(WorldInfo w) => "Epacenur.WorldItems." + w.id;

        /// The items left lying on the ground in a world, and the regions seen.
        public static WorldState LoadWorldState(WorldInfo w)
        {
            if (!PlayerPrefs.HasKey(PilesKey(w)))
                return new WorldState();
            try { return JsonUtility.FromJson<WorldState>(PlayerPrefs.GetString(PilesKey(w))) ?? new WorldState(); }
            catch (Exception) { return new WorldState(); }
        }

        public static void DeletePiles(WorldInfo w) => PlayerPrefs.DeleteKey(PilesKey(w));

        static Data Read(string key)
        {
            if (!PlayerPrefs.HasKey(key))
                return null;
            try
            {
                return JsonUtility.FromJson<Data>(PlayerPrefs.GetString(key));
            }
            catch (Exception e)
            {
                Debug.LogWarning("Epacenur: ignoring an unreadable save (" + e.Message + ").");
                return null;
            }
        }

        /// Returns false when there was no save.
        public static bool Load(Game game)
        {
            var d = Read(KeyFor(game.Character));
            if (d == null)
                return false;
            if (d.format < 3)
                Upgrade(d);

            var db = ContentDb.Current;
            for (int i = 0; d.skills != null && i < d.skills.Length && i < d.xpTenths.Length; i++)
                if (db.Skill(d.skills[i]) != null && BigInteger.TryParse(d.xpTenths[i], out var tenths))
                    game.Skills.SetXpTenths(d.skills[i], tenths);

            for (int i = 0; d.items != null && i < Inventory.Size && i < d.items.Length && i < d.counts.Length; i++)
                if (db.Item(d.items[i]) != null)
                    game.Inventory.Set(i, new ItemStack(d.items[i], d.counts[i]));

            for (int i = 0; d.bank != null && d.bankCounts != null && i < d.bank.Length && i < d.bankCounts.Length; i++)
                if (db.Item(d.bank[i]) != null)
                    game.Bank.Add(d.bank[i], d.bankCounts[i]);

            for (int i = 0; d.equipped != null && i < d.equipped.Length && i < Equipment.SlotCount; i++)
                if (db.Item(d.equipped[i]) != null)
                    game.Equipment.Set((EquipSlot)i, new ItemStack(d.equipped[i], 1));
            game.StyleIndex = System.Math.Max(0, System.Math.Min(d.style, 3));
            game.Autocast = db.Spell(d.autocast);
            if (d.slayer != null)
                game.Slayer = d.slayer;
            if (d.market != null && d.market.offers != null)
            {
                game.Trading = d.market;
                if (game.Trading.shiftItems == null || game.Trading.shifts == null || game.Trading.shiftItems.Count != game.Trading.shifts.Count)
                {
                    game.Trading.shiftItems = new List<string>();
                    game.Trading.shifts = new List<double>();
                }
                game.Trading.offers.RemoveAll(o => db.Item(o.item) == null);  // items the game no longer has
                game.CatchUpMarket();
            }
            if (d.hasEnergy)
                game.Energy.Set(d.energy);
            if (d.tools != null)
                foreach (var id in d.tools)
                    if (db.Item(id) != null)
                        game.ToolBelt.Add(id);
            game.Health.SetMax(game.Skills.GetLevel(SkillIds.Hitpoints));
            game.Health.Set(d.hp > 0 ? d.hp : game.Health.Max);
            game.Prayer.SetMax(game.Skills.GetLevel(SkillIds.Prayer));
            game.Prayer.SetPoints(d.prayerPoints);
            game.Inventory.SetPouch(d.pouch);
            game.Grave = d.hasGrave ? d.grave : null;

            int place = d.placeWorlds == null ? -1 : Array.IndexOf(d.placeWorlds, game.WorldInfo.id);
            if (place >= 0)
                game.World.Travel(d.placeMaps[place] == (int)MapId.DevPlot ? MapId.DevPlot : MapId.Overworld, new Tile(d.placeX[place], d.placeZ[place]));
            else if (d.placeWorlds == null && d.worldVersion >= WorldVersion && game.WorldInfo.seed == Profiles.DefaultSeed)
                game.World.Travel(d.map == (int)MapId.DevPlot ? MapId.DevPlot : MapId.Overworld, new Tile(d.tileX, d.tileZ));
            else
                game.World.Travel(MapId.Overworld, game.World.MainSpawn);  // new to this world: its first town
            return true;
        }

        /// Brings a format 1-2 save up to date: numbered items to text ids, skill names to ids, and the
        /// things older versions didn't have (pouch, tool belt starters, health, prayer).
        static void Upgrade(Data d)
        {
            if (d.skills != null)
                for (int i = 0; i < d.skills.Length; i++)
                    d.skills[i] = d.skills[i].ToLowerInvariant();  // "Woodcutting" -> "woodcutting"
            d.items = LegacyAll(d.itemIds);
            d.bank = LegacyAll(d.bankIds);
            d.equipped = LegacyAll(d.equipIds);
            var tools = new List<string>();
            if (d.toolIds != null)
                foreach (int id in d.toolIds)
                    if (Legacy(id) != null)
                        tools.Add(Legacy(id));
            if (!d.hatchetGiven)
                tools.Add(WoodcuttingModule.BronzeHatchet);
            if (!d.miningToolsGiven)
            {
                tools.Add(MiningModule.BronzePickaxe);
                tools.Add(SmithingModule.Hammer);
            }
            d.tools = tools.ToArray();
            if (!d.hasHp)
                d.hp = 0;  // full
            if (!d.hasPrayer)
                d.prayerPoints = int.MaxValue;  // full (clamped to the maximum)
            if (!d.hasPouch && d.items != null)
            {
                // Before the money pouch, coins sat in a slot: move them in.
                for (int i = 0; i < d.items.Length; i++)
                    if (d.items[i] == ItemIds.Coins)
                    {
                        d.pouch += d.counts[i];
                        d.items[i] = null;
                    }
            }
            d.format = Format;
        }

        /// Wipes a character's progress (they stay in the list, starting afresh).
        public static void Delete(CharacterInfo c) => PlayerPrefs.DeleteKey(KeyFor(c));
    }
}
