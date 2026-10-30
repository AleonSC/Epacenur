using Epacenur.Content;
using Epacenur.Core;
using UnityEngine;

namespace Epacenur.Runtime
{
    /// The dev tools window (F1): set skill levels, spawn items, teleport, reset.
    public sealed class DevTools
    {
        static Game G => Game.Instance;
        bool show, placed;
        Rect rect = new Rect(0f, 0f, 300f, 10f);
        int skill;  // which skill the level buttons change
        static readonly System.Collections.Generic.List<SkillArea> plots = DevArea.CreateAreas();  // for their names and where they are
        Hud hud;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Cheats.InfiniteResources = false;

        public void Toggle() => show = !show;
        public bool Contains(Vector2 p) => show && rect.Contains(p);

        public void Draw(Hud hud)
        {
            if (!show)
                return;
            this.hud = hud;
            if (!placed)
            {
                rect = new Rect(hud.W - Minimap.Size - 330f, 10f, 300f, 10f);
                placed = true;
            }
            rect = GUILayout.Window(1, rect, DrawWindow, "Dev tools (F1)");
        }

        static void Give(string id, int count = 1) => G.Inventory.Add(id, count);

        void DrawWindow(int id)
        {
            // One skill at a time (< > to pick it), so the window stays short as skills are added.
            var skills = ContentDb.Current.Skills;
            skill = (skill + skills.Count) % skills.Count;
            var def = skills[skill];
            int level = G.Skills.GetLevel(def.Id);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("<", GUILayout.Width(26f))) skill--;
            GUILayout.Label("<b>" + def.Name + "</b>  level " + level + "  (" + Experience.FormatShort(G.Skills.GetXp(def.Id)) + " xp)", Gui.Rich);
            if (GUILayout.Button(">", GUILayout.Width(26f))) skill++;
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("-1")) G.Skills.SetLevel(def.Id, Mathf.Max(1, level - 1));
            if (GUILayout.Button("+1")) G.Skills.SetLevel(def.Id, Mathf.Min(Experience.MaxLevel, level + 1));
            foreach (int l in new[] { 1, 99, 120, 999 })
                if (GUILayout.Button(l.ToString())) G.Skills.SetLevel(def.Id, l);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("+1K xp")) G.Skills.AddXp(def.Id, 1e3);
            if (GUILayout.Button("+1M xp")) G.Skills.AddXp(def.Id, 1e6);
            if (GUILayout.Button("+1B xp")) G.Skills.AddXp(def.Id, 1e9);
            GUILayout.EndHorizontal();
            GUILayout.Space(6f);

            bool infinite = GUILayout.Toggle(Cheats.InfiniteResources, " Infinite resources");
            if (infinite != Cheats.InfiniteResources)
            {
                Cheats.InfiniteResources = infinite;
                GameLog.Add(infinite ? "Infinite resources on: nothing you have is used up, and nothing you gather from runs out."
                    : "Infinite resources off.");
            }
            GUILayout.Space(6f);

            GUILayout.Label("<b>Weather</b> (now: " + G.Weather.Weather.Now + ")", Gui.Rich);
            GUILayout.BeginHorizontal();
            foreach (Sky sky in System.Enum.GetValues(typeof(Sky)))
                if (GUILayout.Button(sky.ToString()))
                    G.Weather.Weather.Set(sky);
            GUILayout.EndHorizontal();
            GUILayout.Space(6f);

            GUILayout.Label("<b>Inventory</b>", Gui.Rich);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Empty it")) { G.Inventory.Clear(); hud.Panel.Inventory.ClearSelection(); }
            if (GUILayout.Button("Fill with logs")) Give(WoodcuttingModule.Logs, Inventory.Size);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Empty bank")) G.Bank.Clear();
            if (GUILayout.Button("+1,000 logs in bank")) G.Bank.Add(WoodcuttingModule.Logs, 1000);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("+1,000 coins")) Give(ItemIds.Coins, 1000);
            if (GUILayout.Button("Sword + shield")) { Give(CombatModule.BronzeSword); Give(CombatModule.WoodenShield); }
            if (GUILayout.Button("Tools")) { Give(WoodcuttingModule.BronzeHatchet); Give(FiremakingModule.Tinderbox); Give(FishingModule.SmallFishingNet); }
            if (GUILayout.Button("Fill belt")) foreach (var t in ContentDb.Current.Tools) G.ToolBelt.Add(t.Id);
            if (GUILayout.Button("Building kit"))
            {
                G.ToolBelt.Add(SmithingModule.Hammer); G.ToolBelt.Add(ConstructionModule.Saw);
                Give(ConstructionModule.Nails, 500); Give(ConstructionModule.Plank, 8); Give(ConstructionModule.Limestone, 6); Give(ConstructionModule.OakPlank, 4);
                G.Bank.Add(ConstructionModule.Plank, 300); G.Bank.Add(ConstructionModule.Limestone, 200); G.Bank.Add(ConstructionModule.OakPlank, 100);
                G.Bank.Add(CraftingModule.Leather, 10); G.Bank.Add(SmithingModule.BronzeBar, 20); G.Bank.Add(Metals.Iron.Bar, 20);
                GameLog.Add("Hammer and saw on your belt; planks, limestone and the rest in your bank. G opens the build menu.");
            }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Hurt me 3")) G.HurtPlayer(3);
            if (GUILayout.Button("Heal fully")) G.Health.Set(G.Health.Max);
            if (GUILayout.Button("5 shrimps")) Give(CookingModule.Shrimps, 5);
            if (GUILayout.Button("Recharge prayer")) G.Prayer.Recharge();
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("5 copper + 5 tin")) { Give(MiningModule.CopperOre, 5); Give(MiningModule.TinOre, 5); }
            if (GUILayout.Button("5 bronze bars")) Give(SmithingModule.BronzeBar, 5);
            if (GUILayout.Button("Pickaxe + hammer")) { Give(MiningModule.BronzePickaxe); Give(SmithingModule.Hammer); }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            // One button per metal: 5 of its bars (more metals than bronze to test smithing).
            foreach (var m in Metals.All)
                if (GUILayout.Button("5 " + m.Name.ToLowerInvariant())) Give(m.Bar, 5);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Fishing gear")) { foreach (var t in new[] { FishingModule.SmallFishingNet, FishingModule.FlyFishingRod, FishingModule.LobsterPot, FishingModule.Harpoon }) G.ToolBelt.Add(t); Give(FishingModule.Feathers, 200); }
            if (GUILayout.Button("Adamant gear")) foreach (var t in new[] { "adamant_scimitar", "adamant_full_helm", "adamant_platebody", "adamant_platelegs" }) Give(t);
            if (GUILayout.Button("5 lobsters")) Give(CookingModule.Lobster, 5);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Ranged kit")) { Give("maple_shortbow"); Give("adamant_arrow", 300); }
            if (GUILayout.Button("Crafting kit")) { G.ToolBelt.Add(CraftingModule.Needle); Give(CraftingModule.Thread, 50); Give(CraftingModule.Leather, 10); Give(CraftingModule.Flax, 10); Give(CombatModule.Cowhide, 5); }
            if (GUILayout.Button("Hunter kit")) { Give(HunterModule.BirdSnare, 3); Give(HunterModule.BoxTrap, 2); }
            if (GUILayout.Button("Farming kit"))
            {
                G.ToolBelt.Add(FarmingModule.Spade);
                foreach (var s in ContentDb.Current.Seeds)
                    Give(s.SeedId, 5);
            }
            if (GUILayout.Button("Grow all patches"))
            {
                foreach (var p in G.World.SavedPatches)
                    if (p.seed != null)
                        p.planted = System.DateTime.UtcNow.AddDays(-1).Ticks;
                GameLog.Add("Every planted patch is now fully grown.");
            }
            if (GUILayout.Button("Slayer points +100")) G.Slayer.points += 100;
            if (GUILayout.Button("Herblore kit"))
            {
                foreach (var h in new[] { "guam", "tarromin", "harralander", "ranarr", "irit", "kwuarm" })
                    Give("grimy_" + h, 2);
                Give(HerbloreModule.VialOfWater, 6); Give(HerbloreModule.EyeOfNewt, 10); Give(HerbloreModule.LimpwurtRoot); Give(HerbloreModule.SnapeGrass);
            }
            if (GUILayout.Button("Magic kit"))
            {
                Give(MagicModule.StaffOfAir); Give(MagicModule.WizardHat); Give(MagicModule.WizardRobe); Give(MagicModule.WizardSkirt);
                foreach (var r in new[] { RunecraftingModule.Mind, RunecraftingModule.Water, RunecraftingModule.Earth, RunecraftingModule.Fire, RunecraftingModule.Body })
                    Give(r, 500);
                Give(RunecraftingModule.Air, 500); Give(RunecraftingModule.Nature, 100); Give(RunecraftingModule.Law, 50);
            }
            if (GUILayout.Button("Fletching kit")) { G.ToolBelt.Add(FletchingModule.Knife); Give(WoodcuttingModule.Logs, 5); Give(WoodcuttingModule.YewLogs, 5); Give(FletchingModule.Bowstring, 50); Give(FishingModule.Feathers, 150); Give("bronze_arrowtips", 150); }
            GUILayout.EndHorizontal();

            GUILayout.Label("<b>World</b>", Gui.Rich);
            if (GUILayout.Button("Regrow every tree and refill every rock"))
                foreach (var n in G.World.Nodes)
                    if (n.IsEmpty)
                        n.Refill();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Teleport to dev plot"))
                Teleport(MapId.DevPlot, G.World.DevSpawn, "You teleport to the dev plot.");
            if (GUILayout.Button("Teleport to main world"))
                Teleport(MapId.Overworld, G.World.MainSpawn, "You teleport to the town square.");
            if (GUILayout.Button("Into Epacenur caves"))
                G.World.GoUnderground(G.World.Overworld.Plan(0, -1));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            foreach (var area in plots)
                if (!(area is EmptyPlot) && GUILayout.Button("Go to " + area.Name))
                    Teleport(MapId.DevPlot, area.TeleportTile, "You teleport to the " + area.Name.ToLowerInvariant() + " plot.");
            GUILayout.EndHorizontal();

            GUILayout.Label("<b>Save</b>", Gui.Rich);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Save now")) { SaveGame.Save(G); GameLog.Add("Saved."); }
            if (GUILayout.Button("Reset everything")) ResetEverything();
            GUILayout.EndHorizontal();
            GUI.DragWindow();
        }

        void Teleport(MapId map, Tile tile, string message)
        {
            hud.CloseWindow();
            G.World.Travel(map, tile);
            GameLog.Add(message);
        }

        void ResetEverything()
        {
            SaveGame.Delete(G.Character);
            foreach (var def in ContentDb.Current.Skills)
                G.Skills.SetLevel(def.Id, def.StartLevel);
            foreach (EquipSlot slot in System.Enum.GetValues(typeof(EquipSlot)))
                if (slot != EquipSlot.None)
                    G.Equipment.Set(slot, default(ItemStack));
            G.Inventory.Clear();
            G.Inventory.SetPouch(0);
            G.Inventory.Add(ItemIds.Coins, 100);
            G.ToolBelt.Clear();
            G.GiveStartingTools();
            G.Health.SetMax(G.Skills.GetLevel(SkillIds.Hitpoints));
            G.Health.Set(G.Health.Max);
            G.Prayer.DeactivateAll();
            G.Prayer.SetMax(G.Skills.GetLevel(SkillIds.Prayer));
            G.Prayer.Recharge();
            G.Bank.Clear();
            G.World.Travel(MapId.Overworld, G.World.Spawn);
            GameLog.Add("Progress reset.");
        }
    }
}
