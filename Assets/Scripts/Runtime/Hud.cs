using System.Collections.Generic;
using System.Numerics;
using Epacenur.Core;
using UnityEngine;
using Matrix4x4 = UnityEngine.Matrix4x4;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;

namespace Epacenur.Runtime
{
    /// Everything on screen: the inventory / equipment / skills panel, chat box, xp drops, level-ups,
    /// hitsplats and health bars,
    /// the action under the mouse, the first-person crosshair, the bank and shop windows and the dev tools (F1).
    public sealed class Hud : MonoBehaviour
    {
        const float Slot = 40f, Gap = 4f, Pad = 10f, TabH = 26f;
        const float PanelW = Pad * 2 + Slot * 4 + Gap * 3;
        const float PouchH = 30f;  // the money pouch under the inventory
        const float PanelH = Pad * 2 + TabH + 6f + Slot * 7 + Gap * 6 + Gap + PouchH;

        enum Tab { Inventory, Equipment, ToolBelt, Prayer, Skills }

        struct XpDrop
        {
            public SkillId Skill;
            public string Text;
            public float Born;
        }

        const int BankColumns = 8;
        static readonly int[] BankAmounts = { 1, 5, 10, int.MaxValue };  // int.MaxValue = All
        static readonly int[] ShopAmounts = { 1, 5, 10, 50 };

        Tab tab = Tab.Inventory;
        Interactable openAt;          // the bank booth or shop keeper whose window is open
        int bankAmount = 1, shopAmount = 1;
        UnityEngine.Vector2 bankScroll;
        bool showDev, showHelp = true;
        int devSkill;  // which skill the dev tools' level buttons change
        readonly OptionMenu menu = new OptionMenu();
        int selectedSlot = -1;
        Rect devRect = new Rect(0f, 0f, 300f, 10f);
        bool devPlaced;
        readonly List<XpDrop> drops = new List<XpDrop>();
        string levelUpText;
        float levelUpUntil;
        float scale = 1f;
        GUIStyle label, small, title, centered, rich, rightLabel, rightSmall, tabStyle;

        Game G => Game.Instance;
        float W => Screen.width / scale;
        float H => Screen.height / scale;
        Rect PanelRect => new Rect(W - PanelW - 10f, H - PanelH - 10f, PanelW, PanelH);
        Rect ChatRect => new Rect(10f, H - 160f, Mathf.Clamp(W - PanelW - 40f, 200f, 520f), 150f);
        Rect WindowRect
        {
            get
            {
                float w = Pad * 2 + BankColumns * (Slot + Gap) - Gap + 18f, h = 400f;
                return new Rect(Mathf.Max(10f, (W - PanelW - 20f - w) / 2f), Mathf.Max(60f, (H - h) / 2f - 50f), w, h);
            }
        }
        Rect BarRect => new Rect(10f, ChatRect.y - 58f, 12f + Abilities.All.Length * (Slot + Gap) - Gap, 52f);
        Rect HealthRect => new Rect(BarRect.x, BarRect.y - 24f, BarRect.width, 22f);
        public bool BankOpen => openAt is BankBooth;
        public bool ShopOpen => openAt is ShopKeeper;

        void Update()
        {
            scale = Mathf.Clamp(Screen.height / 760f, 0.75f, 3f);
            if (Input.GetKeyDown(KeyCode.F1))
                showDev = !showDev;
            if (Input.GetKeyDown(KeyCode.H))
                showHelp = !showHelp;
            if (openAt != null)
            {
                // Like RuneScape, the bank or shop closes when you walk away.
                Vector3 d = openAt.transform.position - G.Player.transform.position;
                d.y = 0f;
                if (Input.GetKeyDown(KeyCode.Escape) || d.magnitude > 2.2f)
                    CloseWindow();
            }
        }

        public void OpenBank(BankBooth booth) => OpenWindow(booth);
        public void OpenShop(ShopKeeper npc) => OpenWindow(npc);

        void OpenWindow(Interactable at)
        {
            openAt = at;
            bankScroll = UnityEngine.Vector2.zero;
            tab = Tab.Inventory;
            selectedSlot = -1;
            G.Rig.LockCursor(false);  // first person: free the mouse to use the window
        }

        public void CloseWindow() => openAt = null;

        public bool MenuOpen => menu.IsOpen;
        Vector2 MouseGui => new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y) / scale;

        /// Opens the right-click menu at the mouse (or mid-screen in first person, where relock is true).
        public void OpenMenu(List<MenuOption> options, bool relock)
        {
            menu.Open(options, relock ? new Vector2(W / 2f, H / 2f) : MouseGui, new Vector2(W, H), relock);
        }

        /// Whether the mouse is over a panel (so clicks there don't walk or chop).
        public bool IsMouseOverUi()
        {
            if (Cursor.lockState == CursorLockMode.Locked)
                return false;
            var m = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y) / scale;
            return (menu.IsOpen && menu.Rect.Contains(m)) || PanelRect.Contains(m) || ChatRect.Contains(m) || BarRect.Contains(m) || HealthRect.Contains(m) || (showDev && devRect.Contains(m)) || (openAt != null && WindowRect.Contains(m));
        }

        public void AddXpDrop(SkillId skill, BigInteger tenths)
        {
            string xp = tenths % 10 == 0 ? Experience.Format(tenths / 10) : Experience.Format(tenths / 10) + "." + (tenths % 10);
            drops.Add(new XpDrop { Skill = skill, Text = "+" + xp + " xp", Born = Time.time });
        }

        public void ShowLevelUp(SkillId skill, int level)
        {
            levelUpText = "Congratulations! " + SkillDefs.Get(skill).Name + " level " + level;
            levelUpUntil = Time.time + 4f;
        }

        void MakeStyles()
        {
            if (label != null)
                return;
            label = new GUIStyle(GUI.skin.label) { fontSize = 13, richText = true };
            label.normal.textColor = Color.white;
            small = new GUIStyle(label) { fontSize = 11 };
            title = new GUIStyle(label) { fontSize = 15, fontStyle = FontStyle.Bold };
            centered = new GUIStyle(label) { alignment = TextAnchor.MiddleCenter };
            rich = new GUIStyle(label) { wordWrap = true };
            rightLabel = new GUIStyle(label) { alignment = TextAnchor.UpperRight };
            rightSmall = new GUIStyle(small) { alignment = TextAnchor.UpperRight, wordWrap = true };
            tabStyle = new GUIStyle(GUI.skin.button) { fontSize = 10, padding = new RectOffset(1, 1, 2, 2) };
        }

        void OnGUI()
        {
            MakeStyles();
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            menu.HandleInput();  // before anything else, so a click on the menu goes nowhere else
            DrawWorldLabels();
            DrawCombatOverlays();
            DrawTopLeft();
            DrawXpDrops();
            DrawLevelUp();
            DrawChat();
            DrawActionBar();
            DrawHealthBar();
            DrawPanel();
            if (BankOpen)
                DrawBank();
            else if (ShopOpen)
                DrawShop();
            if (G.Player.Mode == ViewMode.FirstPerson)
                DrawCrosshair();
            if (showDev)
            {
                if (!devPlaced)
                {
                    devRect = new Rect(W - 310f, 10f, 300f, 10f);
                    devPlaced = true;
                }
                devRect = GUILayout.Window(1, devRect, DrawDevWindow, "Dev tools (F1)");
            }
            menu.Draw();
        }

        public static void Fill(Rect r, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
        }

        void Shadowed(Rect r, string text, GUIStyle style)
        {
            var old = style.normal.textColor;
            style.normal.textColor = new Color(0f, 0f, 0f, 0.85f);
            GUI.Label(new Rect(r.x + 1f, r.y + 1f, r.width, r.height), StripColour(text), style);
            style.normal.textColor = old;
            GUI.Label(r, text, style);
        }

        static string StripColour(string s) => System.Text.RegularExpressions.Regex.Replace(s, "<color=[^>]*>|</color>", "");

        void DrawWorldLabels()
        {
            var cam = G.Rig.Cam;
            foreach (var l in G.World.Labels)
            {
                Vector3 sp = cam.WorldToScreenPoint(l.Position);
                if (sp.z <= 0f || sp.z > 60f)
                    continue;
                var c = l.Color;
                c.a = Mathf.Clamp01((60f - sp.z) / 15f);
                var r = new Rect(sp.x / scale - 120f, (Screen.height - sp.y) / scale - 12f, 240f, 24f);
                var old = centered.normal.textColor;
                centered.normal.textColor = c;
                GUI.Label(r, "<b>" + l.Text + "</b>", centered);
                centered.normal.textColor = old;
            }
        }

        void DrawTopLeft()
        {
            var p = G.Player;
            // What left-click does, e.g. "Attack Goblin (level-2) / 2 more options".
            string action = p.HoverText;
            if (action.Length == 0 && p.IsChopping)
                action = "Woodcutting...";
            else if (action.Length == 0 && p.IsFighting)
                action = "Fighting...";
            else if (action.Length == 0 && p.IsLighting)
                action = "Firemaking...";
            else if (action.Length == 0 && p.IsFishing)
                action = "Fishing...";
            else if (action.Length == 0 && p.IsCooking)
                action = "Cooking...";
            Shadowed(new Rect(10f, 8f, 600f, 22f), action, title);

            string mode = p.Mode == ViewMode.Tile ? "Tile mode" : "First person";
            Shadowed(new Rect(10f, 30f, 600f, 20f), "<color=#ffd966>" + mode + "</color>   (V to switch, H for help)", small);
            if (!showHelp)
                return;
            string help = p.Mode == ViewMode.Tile
                ? "Left-click: walk / chop / attack / take\nWASD: walk freely\nArrow keys or middle-drag: rotate camera\nScroll: zoom\nRight-click anything: all its options\n1-9: melee abilities (sword and shield worn)\nClick a sword or shield: wield it.  Shift-click an item: drop it\nF1: dev tools"
                : "Mouse: look    WASD: walk\nLeft-click: one chop or attack per click, take items\nEsc: free the mouse (click the view to grab it again)\nRight-click: all the options for what you aim at\n1-9: melee abilities (sword and shield worn)\nClick a sword or shield: wield it.  Shift-click an item: drop it\nF1: dev tools";
            Shadowed(new Rect(10f, 50f, 440f, 150f), help, small);
        }

        void DrawCrosshair()
        {
            var p = G.Player;
            Color c = p.AimingAtTarget ? (p.SwingReady ? new Color(0.4f, 1f, 0.4f) : new Color(1f, 0.85f, 0.3f)) : new Color(1f, 1f, 1f, 0.8f);
            float cx = W / 2f, cy = H / 2f;
            Fill(new Rect(cx - 8f, cy - 1f, 16f, 2f), c);
            Fill(new Rect(cx - 1f, cy - 8f, 2f, 16f), c);
            if (Cursor.lockState != CursorLockMode.Locked)
                Shadowed(new Rect(cx - 150f, cy + 20f, 300f, 22f), "Click to look around", centered);
        }

        void DrawXpDrops()
        {
            drops.RemoveAll(d => Time.time - d.Born > 2.2f);
            float x = PanelRect.x - 170f, baseY = 90f;
            for (int i = 0; i < drops.Count; i++)
            {
                var d = drops[i];
                float age = Time.time - d.Born;
                var def = SkillDefs.Get(d.Skill);
                float alpha = Mathf.Clamp01((2.2f - age) / 0.6f);
                var r = new Rect(x, baseY - age * 30f + (drops.Count - 1 - i) * 18f, 160f, 20f);
                Fill(new Rect(r.x, r.y + 4f, 12f, 12f), new Color(def.R, def.G, def.B, alpha));
                var old = label.normal.textColor;
                label.normal.textColor = new Color(1f, 1f, 1f, alpha);
                GUI.Label(new Rect(r.x + 16f, r.y, r.width, r.height), d.Text, label);
                label.normal.textColor = old;
            }
        }

        void DrawLevelUp()
        {
            if (Time.time > levelUpUntil || levelUpText == null)
                return;
            var r = new Rect(W / 2f - 220f, 70f, 440f, 44f);
            Fill(r, new Color(0.1f, 0.08f, 0.04f, 0.8f));
            Shadowed(r, "<color=#ffd966><b>" + levelUpText + "</b></color>", centered);
        }

        void DrawChat()
        {
            var r = ChatRect;
            Fill(r, new Color(0.12f, 0.1f, 0.07f, 0.7f));
            var msgs = GameLog.Messages;
            int lines = 7;
            float y = r.y + 4f;
            for (int i = Mathf.Max(0, msgs.Count - lines); i < msgs.Count; i++)
            {
                Shadowed(new Rect(r.x + 6f, y, r.width - 12f, 20f), msgs[i], small);
                y += 20f;
            }
        }

        // ---- Inventory / equipment / skills panel ----

        void DrawPanel()
        {
            var r = PanelRect;
            Fill(r, new Color(0.24f, 0.2f, 0.15f, 0.92f));
            string[] names = { "Inv", "Worn", "Tools", "Pray", "Skills" };
            float tabW = (r.width - Pad * 2 - 3f * (names.Length - 1)) / names.Length;
            for (int i = 0; i < names.Length; i++)
                if (GUI.Toggle(new Rect(r.x + Pad + i * (tabW + 3f), r.y + Pad, tabW, TabH), (int)tab == i, names[i], tabStyle))
                    tab = (Tab)i;
            var body = new Rect(r.x + Pad, r.y + Pad + TabH + 6f, r.width - Pad * 2, r.height - Pad * 2 - TabH - 6f);
            if (tab == Tab.Inventory)
                DrawInventory(body);
            else if (tab == Tab.Equipment)
                DrawEquipment(body);
            else if (tab == Tab.ToolBelt)
                DrawToolBelt(body);
            else if (tab == Tab.Prayer)
                DrawPrayers(body);
            else
                DrawSkills(body);
        }

        void DrawInventory(Rect body)
        {
            var inv = G.Inventory;
            var e = Event.current;
            string tooltip = null;
            for (int i = 0; i < Inventory.Size; i++)
            {
                var slot = new Rect(body.x + (i % 4) * (Slot + Gap), body.y + (i / 4) * (Slot + Gap), Slot, Slot);
                Fill(slot, i == selectedSlot ? new Color(0.6f, 0.5f, 0.2f, 0.9f) : new Color(0f, 0f, 0f, 0.25f));
                var stack = inv[i];
                if (!stack.IsEmpty)
                {
                    DrawItemIcon(slot, stack.Def);
                    if (stack.Def.Stackable && stack.Count > 1)
                        Shadowed(new Rect(slot.x + 2f, slot.y, slot.width, 16f), "<color=#ffff00>" + stack.Count + "</color>", small);
                    if (slot.Contains(e.mousePosition))
                        tooltip = stack.Def.Name;
                }

                if (e.type == EventType.MouseDown && slot.Contains(e.mousePosition) && e.button == 1)
                {
                    if (!stack.IsEmpty)
                        OpenMenu(InventoryOptions(i, stack), false);
                    e.Use();
                }
                else if (e.type == EventType.MouseDown && slot.Contains(e.mousePosition) && BankOpen)
                {
                    // With the bank open, clicking an item deposits it (shift-click: all of it).
                    if (!stack.IsEmpty)
                        G.Bank.Deposit(inv, stack.ItemId, e.shift ? int.MaxValue : bankAmount);
                    e.Use();
                }
                else if (e.type == EventType.MouseDown && slot.Contains(e.mousePosition) && ShopOpen)
                {
                    // With a shop open, clicking an item sells it (shift-click: all of it).
                    if (!stack.IsEmpty)
                        SellToShop(stack.Def, e.shift ? int.MaxValue : shopAmount);
                    e.Use();
                }
                else if (e.type == EventType.MouseDown && slot.Contains(e.mousePosition))
                {
                    bool drop = e.shift;
                    if (drop && !stack.IsEmpty)
                    {
                        Drop(i);
                    }
                    else if (!drop)
                    {
                        // Click one slot, then another, to swap them. Equipment is worn on click instead.
                        if (selectedSlot == -1 && !stack.IsEmpty && stack.Def.Slot != EquipSlot.None)
                            G.Equipment.Equip(inv, i);
                        else if (selectedSlot == -1 && !stack.IsEmpty && stack.ItemId == ItemDefs.Coins.Id)
                            inv.AddToPouch(i);
                        else if (selectedSlot == -1 && !stack.IsEmpty && Prayers.BuryXp(stack.ItemId) != null)
                            G.Player.Bury(i);
                        else if (selectedSlot == -1 && !stack.IsEmpty && Foods.For(stack.ItemId) != null)
                            G.Player.Eat(i);
                        else if (selectedSlot == -1 && !stack.IsEmpty && CookingRules.For(stack.ItemId) != null)
                            G.Player.CookFromInventory(stack.ItemId);
                        else if (selectedSlot == -1 && !stack.IsEmpty && ToolBelt.IsTool(stack.ItemId) && !G.ToolBelt.Has(stack.ItemId))
                            AddToToolBelt(i);
                        else if (selectedSlot == -1 && !stack.IsEmpty && FiremakingRules.For(stack.ItemId) != null && G.Player.HasTinderbox)
                            G.Player.LightFromInventory(i);
                        else if (selectedSlot == -1 && !stack.IsEmpty)
                            selectedSlot = i;
                        else if (selectedSlot >= 0)
                        {
                            if (selectedSlot != i)
                                inv.Swap(selectedSlot, i);
                            selectedSlot = -1;
                        }
                    }
                    e.Use();
                }
            }
            DrawPouch(new Rect(body.x, body.y + 7 * (Slot + Gap), body.width, PouchH));
            if (tooltip != null)
                Shadowed(new Rect(e.mousePosition.x + 14f, e.mousePosition.y - 6f, 200f, 20f), "<color=#ffcc66>" + tooltip + "</color>", small);
        }

        // ---- Tool belt ----

        void AddToToolBelt(int slot)
        {
            var def = G.Inventory[slot].Def;
            if (G.ToolBelt.AddFromInventory(G.Inventory, slot, out string problem))
                GameLog.Add("You add the " + def.Name.ToLowerInvariant() + " to your tool belt.");
            else
                GameLog.Add(problem);
        }

        /// Every tool the belt can hold: the ones on it in full colour, the rest faded.
        void DrawToolBelt(Rect body)
        {
            var e = Event.current;
            Shadowed(new Rect(body.x, body.y - 2f, body.width, 18f), "<b>TOOL BELT</b>", small);
            float top = body.y + 20f;
            string tooltip = null;
            var tools = ToolBelt.Tools;
            for (int i = 0; i < tools.Length; i++)
            {
                var def = tools[i];
                bool has = G.ToolBelt.Has(def.Id);
                var slot = new Rect(body.x + (i % 4) * (Slot + Gap), top + (i / 4) * (Slot + Gap), Slot, Slot);
                Bevel(slot);
                DrawItemIcon(slot, def);
                if (!has)
                    Fill(new Rect(slot.x + 1f, slot.y + 1f, slot.width - 2f, slot.height - 2f), new Color(0.12f, 0.11f, 0.09f, 0.7f));
                if (slot.Contains(e.mousePosition))
                {
                    tooltip = def.Name + (has ? " (on your belt)" : ": not on your belt yet");
                    if (e.type == EventType.MouseDown)
                    {
                        if (e.button == 1)
                            OpenMenu(new List<MenuOption> { new MenuOption("Examine", MenuOption.Item(def.Name), () => GameLog.Add(def.Examine)) }, false);
                        e.Use();
                    }
                }
            }
            int rows = (tools.Length + 3) / 4;
            GUI.Label(new Rect(body.x, top + rows * (Slot + Gap) + 4f, body.width, 80f),
                G.ToolBelt.Owned.GetEnumerator().MoveNext()
                    ? "Tools here are used automatically, as if they were in your backpack. They stay on the belt for good."
                    : "Right-click a tool in your backpack and choose Add to tool belt. It's then used from here and frees the slot.",
                rich);
            if (tooltip != null)
                Shadowed(new Rect(e.mousePosition.x - 210f, e.mousePosition.y + 16f, 210f, 20f), "<color=#ffcc66>" + tooltip + "</color>", rightSmall);
        }

        // ---- Money pouch ----

        /// The coin count, RuneScape colours: yellow below 100K, white below 10M, green above.
        static string PouchText(int coins)
        {
            string colour = coins < 100000 ? "#ffff00" : coins < 10000000 ? "#ffffff" : "#00ff80";
            return "<color=" + colour + ">" + Experience.Format(coins) + "</color>";
        }

        void DrawPouch(Rect r)
        {
            var e = Event.current;
            int coins = G.Inventory.PouchCoins;
            Bevel(r);
            // A little coin stack.
            var gold = new Color(0.95f, 0.8f, 0.25f);
            var dark = new Color(0.7f, 0.55f, 0.1f);
            for (int k = 0; k < 3; k++)
            {
                Fill(new Rect(r.x + 7f, r.y + 18f - k * 5f, 16f, 5f), dark);
                Fill(new Rect(r.x + 7f, r.y + 18f - k * 5f, 16f, 3f), gold);
            }
            GUI.Label(new Rect(r.x + 28f, r.y + 6f, 80f, 18f), "Coins", small);
            Shadowed(new Rect(r.x, r.y + 6f, r.width - 8f, 18f), "<b>" + PouchText(coins) + "</b>", rightSmall);

            if (r.Contains(e.mousePosition) && e.type == EventType.MouseDown)
            {
                if (e.button == 1)
                    OpenMenu(PouchOptions(), false);
                else if (BankOpen)
                    G.Bank.DepositPouch(G.Inventory);
                else
                    ExaminePouch();
                e.Use();
            }
        }

        void ExaminePouch() => GameLog.Add("Your money pouch contains " + Experience.Format(G.Inventory.PouchCoins) + " coins.");

        List<MenuOption> PouchOptions()
        {
            string name = MenuOption.Item("Money pouch");
            var o = new List<MenuOption>();
            if (BankOpen)
                o.Add(new MenuOption("Deposit", name, () => G.Bank.DepositPouch(G.Inventory)));
            foreach (int a in new[] { 1, 10, 100, 1000, int.MaxValue })
            {
                int n = a;
                o.Add(new MenuOption("Withdraw-" + (n == int.MaxValue ? "All" : Experience.Format(n)), name, () => WithdrawFromPouch(n)));
            }
            o.Add(new MenuOption("Examine", name, ExaminePouch));
            return o;
        }

        /// Coins out of the pouch into the backpack, e.g. to drop them.
        void WithdrawFromPouch(int amount)
        {
            if (G.Inventory.PouchCoins == 0)
                GameLog.Add("Your money pouch is empty.");
            else if (G.Inventory.WithdrawFromPouch(amount) == 0)
                GameLog.Add("You don't have enough inventory space.");
        }

        // ---- Right-click options for items in the panel and windows ----

        List<MenuOption> InventoryOptions(int slot, ItemStack stack)
        {
            var def = stack.Def;
            string name = MenuOption.Item(def.Name);
            var o = new List<MenuOption>();
            if (BankOpen)
            {
                foreach (int a in BankAmounts)
                {
                    int n = a;
                    o.Add(new MenuOption("Deposit-" + (n == int.MaxValue ? "All" : n.ToString()), name, () => G.Bank.Deposit(G.Inventory, def.Id, n)));
                }
            }
            else if (ShopOpen)
            {
                o.Add(new MenuOption("Value", name, () => SellToShop(def, 0)));
                foreach (int a in ShopAmounts)
                {
                    int n = a;
                    o.Add(new MenuOption("Sell " + n, name, () => SellToShop(def, n)));
                }
            }
            else
            {
                if (def.Slot != EquipSlot.None)
                    o.Add(new MenuOption(def.Slot == EquipSlot.Weapon ? "Wield" : "Wear", name, () =>
                    {
                        if (G.Inventory[slot].ItemId == def.Id)
                            G.Equipment.Equip(G.Inventory, slot);
                    }));
                if (Prayers.BuryXp(def.Id) != null)
                    o.Add(new MenuOption("Bury", name, () =>
                    {
                        if (G.Inventory[slot].ItemId == def.Id)
                            G.Player.Bury(slot);
                    }));
                if (Foods.For(def.Id) != null)
                    o.Add(new MenuOption("Eat", name, () =>
                    {
                        if (G.Inventory[slot].ItemId == def.Id)
                            G.Player.Eat(slot);
                    }));
                if (CookingRules.For(def.Id) != null)
                    o.Add(new MenuOption("Cook", name, () => G.Player.CookFromInventory(def.Id)));
                if (ToolBelt.IsTool(def.Id))
                    o.Add(new MenuOption("Add to tool belt", name, () =>
                    {
                        if (G.Inventory[slot].ItemId == def.Id)
                            AddToToolBelt(slot);
                    }));
                if (def == ItemDefs.Coins)
                    o.Add(new MenuOption("Add to pouch", name, () =>
                    {
                        if (G.Inventory[slot].ItemId == def.Id)
                            G.Inventory.AddToPouch(slot);
                    }));
                if (FiremakingRules.For(def.Id) != null)
                    o.Add(new MenuOption("Light", name, () =>
                    {
                        if (G.Inventory[slot].ItemId == def.Id)
                            G.Player.LightFromInventory(slot);
                    }));
                o.Add(new MenuOption("Drop", name, () =>
                {
                    if (G.Inventory[slot].ItemId == def.Id)
                        Drop(slot);
                }));
            }
            o.Add(new MenuOption("Examine", name, () => GameLog.Add(def.Examine)));
            return o;
        }

        /// Drops an inventory slot's items on the ground under you.
        void Drop(int slot)
        {
            var stack = G.Inventory[slot];
            if (stack.IsEmpty)
                return;
            G.Inventory.RemoveAt(slot);
            G.World.SpawnGroundItem(stack.ItemId, stack.Count, World.TileAt(G.Player.transform.position));
            selectedSlot = -1;
        }

        List<MenuOption> BankOptions(BankSlot s)
        {
            var def = s.Def;
            string name = MenuOption.Item(def.Name);
            var o = new List<MenuOption>();
            foreach (int a in BankAmounts)
            {
                long n = a == int.MaxValue ? long.MaxValue : a;
                o.Add(new MenuOption("Withdraw-" + (a == int.MaxValue ? "All" : a.ToString()), name, () => Withdraw(def.Id, n)));
            }
            o.Add(new MenuOption("Examine", name, () => GameLog.Add(def.Examine)));
            return o;
        }

        void Withdraw(int itemId, long amount)
        {
            int index = G.Bank.IndexOf(itemId);
            if (index >= 0 && G.Bank.Withdraw(G.Inventory, index, amount) == 0 && G.Inventory.IsFull)
                GameLog.Add("You don't have enough inventory space.");
        }

        List<MenuOption> ShopOptions(Shop shop, ShopItem item)
        {
            var def = item.Def;
            string name = MenuOption.Item(def.Name);
            var o = new List<MenuOption> { new MenuOption("Value", name, () => GameLog.Add(def.Name + ": currently costs " + Shop.BuyPrice(def) + " coins.")) };
            foreach (int a in ShopAmounts)
            {
                int n = a;
                o.Add(new MenuOption("Buy " + n, name, () => Buy(shop, def.Id, n)));
            }
            o.Add(new MenuOption("Examine", name, () => GameLog.Add(def.Examine)));
            return o;
        }

        void Buy(Shop shop, int itemId, int amount)
        {
            int index = shop.Items.FindIndex(it => it.ItemId == itemId);
            if (index >= 0 && shop.Buy(G.Inventory, index, amount, out string problem) < amount && problem != null)
                GameLog.Add(problem);
        }

        List<MenuOption> EquipOptions(EquipSlot slot, ItemDef def)
        {
            string name = MenuOption.Item(def.Name);
            return new List<MenuOption>
            {
                new MenuOption("Remove", name, () => Unequip(slot)),
                new MenuOption("Examine", name, () => GameLog.Add(def.Examine)),
            };
        }

        void Unequip(EquipSlot slot)
        {
            if (!G.Equipment.Unequip(slot, G.Inventory))
                GameLog.Add("You don't have enough free inventory space to do that.");
        }

        // ---- Equipment ----

        // Where each slot sits in the 3-column layout (column, row), like RuneScape's worn equipment.
        static readonly (EquipSlot slot, int col, int row)[] EquipLayout =
        {
            (EquipSlot.Head, 1, 0),
            (EquipSlot.Cape, 0, 1), (EquipSlot.Neck, 1, 1), (EquipSlot.Ammo, 2, 1),
            (EquipSlot.Weapon, 0, 2), (EquipSlot.Body, 1, 2), (EquipSlot.Shield, 2, 2),
            (EquipSlot.Legs, 1, 3),
            (EquipSlot.Hands, 0, 4), (EquipSlot.Feet, 1, 4), (EquipSlot.Ring, 2, 4),
        };

        void DrawEquipment(Rect body)
        {
            var eq = G.Equipment;
            var e = Event.current;
            const float rowH = 46f;
            float colGap = (body.width - Slot * 3) / 2f;
            Shadowed(new Rect(body.x, body.y - 2f, body.width, 18f), "<b>Worn Equipment</b>", small);
            float top = body.y + 18f;

            // Connecting lines: down the middle column, across the rows with side slots,
            // and from the weapon and shield down to the gloves and ring.
            var line = new Color(0.1f, 0.08f, 0.06f, 0.9f);
            float cx = body.x + Slot + colGap + Slot / 2f;
            Fill(new Rect(cx - 1f, top + Slot, 2f, rowH * 4 - Slot), line);
            foreach (int row in new[] { 1, 2, 4 })
            {
                float y = top + row * rowH + Slot / 2f;
                Fill(new Rect(body.x + Slot, y - 1f, body.width - Slot * 2, 2f), line);
            }
            Fill(new Rect(body.x + Slot / 2f - 1f, top + 2 * rowH + Slot, 2f, rowH * 2 - Slot), line);           // weapon down to hands
            Fill(new Rect(body.x + body.width - Slot / 2f - 1f, top + 2 * rowH + Slot, 2f, rowH * 2 - Slot), line); // shield down to ring

            string tooltip = null;
            foreach (var place in EquipLayout)
            {
                var slot = place.slot;
                var r = new Rect(body.x + place.col * (Slot + colGap), top + place.row * rowH, Slot, Slot);
                Fill(r, new Color(0.12f, 0.1f, 0.07f, 1f));
                Fill(new Rect(r.x + 1f, r.y + 1f, r.width - 2f, r.height - 2f), new Color(0.3f, 0.26f, 0.2f, 1f));
                var worn = eq[slot];
                if (worn.IsEmpty)
                {
                    // An empty slot shows what goes there, faintly.
                    var old = centered.normal.textColor;
                    centered.normal.textColor = new Color(1f, 1f, 1f, 0.25f);
                    GUI.Label(r, "<size=9>" + slot + "</size>", centered);
                    centered.normal.textColor = old;
                }
                else
                {
                    DrawItemIcon(r, worn.Def);
                    if (r.Contains(e.mousePosition))
                        tooltip = "Remove " + worn.Def.Name;
                    if (e.type == EventType.MouseDown && r.Contains(e.mousePosition))
                    {
                        if (e.button == 1)
                            OpenMenu(EquipOptions(slot, worn.Def), false);
                        else
                            Unequip(slot);
                        e.Use();
                    }
                }
            }

            // Bonuses and attack style.
            float y0 = top + 5 * rowH - 4f;
            GUI.Label(new Rect(body.x, y0, body.width, 18f),
                "Att <color=#ffff00>+" + eq.AttackBonus + "</color>  Str <color=#ffff00>+" + eq.StrengthBonus + "</color>  Def <color=#ffff00>+" + eq.DefenceBonus + "</color>", small);
            GUI.Label(new Rect(body.x, y0 + 16f, body.width, 18f), "Attack style (trains):", small);
            string[] styles = { "Attack", "Strength", "Defence" };
            float bw = (body.width - 8f) / 3f;
            for (int i = 0; i < 3; i++)
                if (GUI.Toggle(new Rect(body.x + i * (bw + 4f), y0 + 34f, bw, 22f), (int)G.Style == i, styles[i], GUI.skin.button))
                    G.Style = (CombatStyle)i;
            if (tooltip != null)
                Shadowed(new Rect(e.mousePosition.x - 150f, e.mousePosition.y + 16f, 200f, 20f), "<color=#ffcc66>" + tooltip + "</color>", small);
        }

        // ---- Action bar: the melee abilities, keys 1-9 ----

        void DrawActionBar()
        {
            var r = BarRect;
            var e = Event.current;
            var p = G.Player;
            float now = Time.time;
            bool armed = G.Equipment.IsWearing(EquipSlot.Weapon) && G.Equipment.IsWearing(EquipSlot.Shield);
            Fill(r, new Color(0.24f, 0.2f, 0.15f, 0.92f));
            AbilityDef tip = null;
            for (int i = 0; i < Abilities.All.Length; i++)
            {
                var def = Abilities.All[i];
                var slot = new Rect(r.x + 6f + i * (Slot + Gap), r.y + 6f, Slot, Slot);
                Bevel(slot);
                var inner = new Rect(slot.x + 2f, slot.y + 2f, slot.width - 4f, slot.height - 4f);
                Fill(inner, Shade(new Color(def.R, def.G, def.B), 0.55f));
                Fill(new Rect(inner.x, inner.y, inner.width, inner.height / 2f), new Color(1f, 1f, 1f, 0.08f));
                string abbr = def.Id == AbilityId.Backhand ? "Bh" : def.Name.Substring(0, 2);
                Shadowed(inner, "<b><size=15>" + abbr + "</size></b>", centered);
                Shadowed(new Rect(slot.x + 3f, slot.y + 1f, 12f, 14f), "<color=#ffffff>" + (i + 1) + "</color>", small);

                // Unusable: no level, or no sword and shield.
                bool hasLevel = p.HasLevelFor(def);
                if (!hasLevel || !armed)
                    Fill(inner, new Color(0f, 0f, 0f, 0.55f));
                if (!hasLevel)
                    Shadowed(new Rect(slot.x, slot.yMax - 15f, slot.width, 14f), "<color=#ff6060>Lv " + def.Level + "</color>", centered);

                // Cooldown: a dark curtain that shrinks as it comes back, and the seconds left.
                float f = p.Cooldowns.Fraction(def.Id, now);
                if (f > 0f)
                {
                    Fill(new Rect(inner.x, inner.y, inner.width, inner.height * f), new Color(0f, 0f, 0f, 0.6f));
                    float left = p.Cooldowns.Remaining(def.Id, now);
                    if (left > Abilities.GlobalCooldownSeconds)
                        Shadowed(inner, "<b>" + Mathf.CeilToInt(left) + "</b>", centered);
                }

                if (slot.Contains(e.mousePosition))
                {
                    tip = def;
                    if (e.type == EventType.MouseDown)
                    {
                        var id = def.Id;
                        if (e.button == 1)
                            OpenMenu(new List<MenuOption>
                            {
                                new MenuOption("Activate", MenuOption.Item(def.Name), () => G.Player.UseAbility(id)),
                                new MenuOption("Examine", MenuOption.Item(def.Name), () => GameLog.Add(def.Description)),
                            }, false);
                        else
                            p.UseAbility(id);
                        e.Use();
                    }
                }
            }

            if (tip != null)
            {
                const float w = 230f, h = 70f;
                var t = new Rect(Mathf.Clamp(e.mousePosition.x - w / 2f, 4f, W - w - 4f), r.y - h - 4f, w, h);
                Fill(t, new Color(0.06f, 0.05f, 0.04f, 0.95f));
                bool ok = p.HasLevelFor(tip);
                Shadowed(new Rect(t.x + 8f, t.y + 3f, w - 16f, 18f), "<b><color=#ffcc66>" + tip.Name + "</color></b>", small);
                GUI.Label(new Rect(t.x + 8f, t.y + 19f, w - 16f, 16f),
                    (ok ? "<color=#a0ffa0>" : "<color=#ff6060>") + "Requires " + SkillDefs.Get(tip.Skill).Name + " " + tip.Level + "</color>   Cooldown " + tip.CooldownSeconds + " s", small);
                GUI.Label(new Rect(t.x + 8f, t.y + 35f, w - 16f, 34f), tip.Description, rich);
            }
        }

        // ---- Hitsplats and health bars ----

        /// Your health (left) and prayer points (right): bars that empty as they're used, with the numbers on them.
        void DrawHealthBar()
        {
            var whole = HealthRect;
            var r = new Rect(whole.x, whole.y, whole.width / 2f - 2f, whole.height);
            DrawPrayerBar(new Rect(whole.x + whole.width / 2f + 2f, whole.y, whole.width / 2f - 2f, whole.height));
            var hp = G.Health;
            Bevel(r);
            // A small heart.
            var red = new Color(0.85f, 0.12f, 0.15f);
            float hx = r.x + 6f, hy = r.y + 5f;
            Fill(new Rect(hx, hy, 5f, 5f), red);
            Fill(new Rect(hx + 6f, hy, 5f, 5f), red);
            Fill(new Rect(hx, hy + 3f, 11f, 5f), red);
            Fill(new Rect(hx + 2f, hy + 8f, 7f, 2f), red);
            Fill(new Rect(hx + 4f, hy + 10f, 3f, 2f), red);
            var bar = new Rect(r.x + 22f, r.y + 4f, r.width - 26f, r.height - 8f);
            Fill(bar, new Color(0.25f, 0.04f, 0.04f));
            Color fill = hp.Fraction > 0.5f ? new Color(0.75f, 0.1f, 0.12f) : hp.Fraction > 0.25f ? new Color(0.85f, 0.4f, 0.08f) : new Color(1f, 0.15f, 0.1f);
            Fill(new Rect(bar.x, bar.y, bar.width * hp.Fraction, bar.height), fill);
            Fill(new Rect(bar.x, bar.y, bar.width * hp.Fraction, 2f), new Color(1f, 1f, 1f, 0.15f));
            Shadowed(bar, "<b>" + hp.Current + " / " + hp.Max + "</b>", centered);
            if (r.Contains(Event.current.mousePosition))
                Shadowed(new Rect(r.x, r.y - 20f, 300f, 18f), "<color=#ffcc66>Hitpoints: eat cooked food to heal. Lost health returns 1 a minute.</color>", small);
        }

        void DrawPrayerBar(Rect r)
        {
            var pr = G.Prayer;
            var e = Event.current;
            Bevel(r);
            DrawPrayerStar(new Rect(r.x + 3f, r.y + 2f, 18f, 18f), pr.AnyActive);
            var bar = new Rect(r.x + 22f, r.y + 4f, r.width - 26f, r.height - 8f);
            Fill(bar, new Color(0.03f, 0.12f, 0.2f));
            Fill(new Rect(bar.x, bar.y, bar.width * pr.Fraction, bar.height), pr.AnyActive ? new Color(0.25f, 0.75f, 0.95f) : new Color(0.15f, 0.5f, 0.75f));
            Fill(new Rect(bar.x, bar.y, bar.width * pr.Fraction, 2f), new Color(1f, 1f, 1f, 0.15f));
            Shadowed(bar, "<b>" + pr.Points + " / " + pr.Max + "</b>", centered);
            if (r.Contains(e.mousePosition))
            {
                Shadowed(new Rect(r.x - 60f, r.y - 20f, 320f, 18f), "<color=#ffcc66>Prayer points: recharge at the altar. Click to turn all prayers off.</color>", small);
                if (e.type == EventType.MouseDown)
                {
                    pr.DeactivateAll();
                    e.Use();
                }
            }
        }

        /// A glowing star: the prayer symbol.
        static void DrawPrayerStar(Rect b, bool lit)
        {
            float cx = b.center.x, cy = b.center.y;
            var c = lit ? new Color(1f, 0.95f, 0.55f) : new Color(0.75f, 0.72f, 0.55f);
            Fill(new Rect(cx - 7f, cy - 1.5f, 14f, 3f), c);
            Fill(new Rect(cx - 1.5f, cy - 7f, 3f, 14f), c);
            FillDiamond(cx, cy, 5f, c);
            Fill(new Rect(cx - 1.5f, cy - 1.5f, 3f, 3f), Color.white);
        }

        // ---- Prayer tab ----

        void DrawPrayers(Rect body)
        {
            var e = Event.current;
            var pr = G.Prayer;
            int level = G.Skills.GetLevel(SkillId.Prayer);
            Shadowed(new Rect(body.x, body.y - 2f, body.width, 18f), "<b>PRAYER</b>", small);
            Shadowed(new Rect(body.x, body.y - 2f, body.width, 18f), "<color=#7fd4ff>" + pr.Points + " / " + pr.Max + "</color>", rightSmall);
            float top = body.y + 20f;
            PrayerDef tip = null;
            for (int i = 0; i < Prayers.All.Length; i++)
            {
                var def = Prayers.All[i];
                var slot = new Rect(body.x + (i % 4) * (Slot + Gap), top + (i / 4) * (Slot + Gap), Slot, Slot);
                bool on = pr.IsActive(def.Id), unlocked = level >= def.Level;
                Bevel(slot);
                if (on)
                    Fill(new Rect(slot.x + 1f, slot.y + 1f, slot.width - 2f, slot.height - 2f), new Color(1f, 0.85f, 0.35f, 0.35f));
                DrawPrayerIcon(new Rect(slot.x + 9f, slot.y + 9f, 22f, 22f), def);
                if (!unlocked)
                {
                    Fill(new Rect(slot.x + 1f, slot.y + 1f, slot.width - 2f, slot.height - 2f), new Color(0.12f, 0.11f, 0.09f, 0.7f));
                    Shadowed(new Rect(slot.x, slot.yMax - 15f, slot.width, 14f), "<color=#ff6060>Lv " + def.Level + "</color>", centered);
                }
                if (slot.Contains(e.mousePosition))
                {
                    tip = def;
                    if (e.type == EventType.MouseDown)
                    {
                        var id = def.Id;
                        if (e.button == 1)
                            OpenMenu(new List<MenuOption>
                            {
                                new MenuOption(on ? "Deactivate" : "Activate", MenuOption.Item(def.Name), () => G.TogglePrayer(id)),
                            }, false);
                        else
                            G.TogglePrayer(id);
                        e.Use();
                    }
                }
            }
            int rows = (Prayers.All.Length + 3) / 4;
            GUI.Label(new Rect(body.x, top + rows * (Slot + Gap) + 4f, body.width, 90f),
                "Click a prayer to turn it on or off. Prayers drain your points; recharge them at the altar by the bank. Bury bones to train Prayer.", rich);
            if (tip != null)
            {
                float seconds = PrayerBook.DrainResistance / (float)tip.DrainEffect * WoodcuttingRules.TickSeconds;
                var t = new Rect(Mathf.Max(4f, PanelRect.x - 236f), Mathf.Clamp(e.mousePosition.y - 30f, 4f, H - 74f), 230f, 70f);
                Fill(t, new Color(0.06f, 0.05f, 0.04f, 0.95f));
                Shadowed(new Rect(t.x + 8f, t.y + 3f, t.width - 16f, 18f), "<b><color=#ffcc66>" + tip.Name + "</color></b>  (level " + tip.Level + ")", small);
                GUI.Label(new Rect(t.x + 8f, t.y + 20f, t.width - 16f, 30f), tip.Description, small);
                GUI.Label(new Rect(t.x + 8f, t.y + 48f, t.width - 16f, 18f), "Drains a point every " + seconds.ToString("0.#") + " s", small);
            }
        }

        /// No art yet: each prayer's icon is its skill's icon (shield, fist, sword), with a mark for the stronger tier.
        static void DrawPrayerIcon(Rect b, PrayerDef def)
        {
            if (def.Group == PrayerGroup.Protection)
            {
                // A blue shield with a sword across it.
                float cx = b.center.x, cy = b.center.y;
                var blue = new Color(0.35f, 0.7f, 1f);
                Fill(new Rect(cx - 8f, cy - 9f, 16f, 11f), blue);
                Fill(new Rect(cx - 6f, cy + 2f, 12f, 4f), blue);
                Fill(new Rect(cx - 4f, cy + 6f, 8f, 3f), blue);
                FillRotated(new Rect(cx - 1f, cy - 10f, 2f, 20f), new Color(0.9f, 0.92f, 0.95f), 45f, b.center);
                return;
            }
            var skill = def.Group == PrayerGroup.Defence ? SkillId.Defence : def.Group == PrayerGroup.Strength ? SkillId.Strength : SkillId.Attack;
            DrawSkillIcon(b, SkillDefs.Get(skill));
            if (def.Boost > 0.05)
                Fill(new Rect(b.xMax - 3f, b.y - 2f, 4f, 4f), new Color(1f, 0.9f, 0.4f));
        }

        void DrawCombatOverlays()
        {
            var cam = G.Rig.Cam;
            // Your own health over your head while hurt (tile mode), with hitsplats when hit.
            var p = G.Player;
            if (G.Prayer.ProtectsFromMelee)
            {
                // The protection prayer's symbol over your head (top of the view in first person).
                Vector3 over = cam.WorldToScreenPoint(p.transform.position + Vector3.up * 2.5f);
                Vector2 at = p.Mode == ViewMode.Tile && over.z > 0f ? new Vector2(over.x / scale, (Screen.height - over.y) / scale) : new Vector2(W / 2f, 40f);
                DrawPrayerIcon(new Rect(at.x - 11f, at.y - 11f, 22f, 22f), Prayers.Get(PrayerId.ProtectFromMelee));
            }
            if (p.Mode == ViewMode.Tile && (!G.Health.IsFull || p.Hitsplats.Count > 0))
            {
                Vector3 head = cam.WorldToScreenPoint(p.transform.position + Vector3.up * 2.15f);
                if (head.z > 0f)
                {
                    float x = head.x / scale, y = (Screen.height - head.y) / scale;
                    var bar = new Rect(x - 20f, y - 6f, 40f, 5f);
                    Fill(bar, new Color(0.8f, 0.1f, 0.1f));
                    Fill(new Rect(bar.x, bar.y, bar.width * G.Health.Fraction, bar.height), new Color(0.1f, 0.85f, 0.15f));
                    for (int i = 0; i < p.Hitsplats.Count; i++)
                    {
                        var h = p.Hitsplats[i];
                        var r = new Rect(x - 12f + i * 8f, y + 14f - i * 6f, 24f, 22f);
                        Fill(r, h.Damage > 0 ? new Color(0.75f, 0.08f, 0.08f, 0.95f) : new Color(0.15f, 0.35f, 0.8f, 0.95f));
                        Shadowed(r, "<b>" + h.Damage + "</b>", centered);
                    }
                }
            }
            foreach (var npc in G.World.Npcs)
            {
                if (Time.time - npc.LastHitTime > 6f || (npc.IsDead && npc.Hitsplats.Count == 0))
                    continue;
                Vector3 head = cam.WorldToScreenPoint(npc.transform.position + Vector3.up * 1.75f);
                if (head.z <= 0f)
                    continue;
                float x = head.x / scale, y = (Screen.height - head.y) / scale;
                if (npc.IsStunned)
                    Shadowed(new Rect(x - 40f, y - 22f, 80f, 16f), "<color=#ffff66>Stunned</color>", centered);
                // Health bar: green for what's left, red for what's lost.
                var bar = new Rect(x - 20f, y - 6f, 40f, 5f);
                Fill(bar, new Color(0.8f, 0.1f, 0.1f));
                Fill(new Rect(bar.x, bar.y, bar.width * npc.Hitpoints / npc.Def.Hitpoints, bar.height), new Color(0.1f, 0.85f, 0.15f));
                Vector3 body = cam.WorldToScreenPoint(npc.transform.position + Vector3.up * 0.8f);
                float bx = body.x / scale, by = (Screen.height - body.y) / scale;
                for (int i = 0; i < npc.Hitsplats.Count; i++)
                {
                    var h = npc.Hitsplats[i];
                    var r = new Rect(bx - 12f + i * 8f, by - 12f - i * 6f, 24f, 22f);
                    Fill(r, h.Damage > 0 ? new Color(0.75f, 0.08f, 0.08f, 0.95f) : new Color(0.15f, 0.35f, 0.8f, 0.95f));
                    Shadowed(r, "<b>" + h.Damage + "</b>", centered);
                }
            }
        }

        // ---- Bank ----

        void DrawBank()
        {
            var r = WindowRect;
            var bank = G.Bank;
            var e = Event.current;
            Fill(r, new Color(0.24f, 0.2f, 0.15f, 0.96f));
            Fill(new Rect(r.x, r.y, r.width, 30f), new Color(0.16f, 0.13f, 0.09f, 1f));
            Shadowed(new Rect(r.x + Pad, r.y + 5f, 260f, 22f), "<b>Bank of Epacenur</b>", title);
            Shadowed(new Rect(r.x + r.width - 140f, r.y + 7f, 100f, 20f), bank.UsedSlots + " / " + Bank.Size, rightSmall);
            if (GUI.Button(new Rect(r.xMax - 30f, r.y + 4f, 24f, 22f), "X"))
                CloseWindow();

            // Items: one slot per kind of item, with the whole stack in it.
            var view = new Rect(r.x + Pad, r.y + 38f, r.width - Pad * 2, r.height - 38f - 84f);
            int rows = Mathf.Max(1, (bank.UsedSlots + BankColumns - 1) / BankColumns);
            var content = new Rect(0f, 0f, view.width - 18f, rows * (Slot + Gap));
            bankScroll = GUI.BeginScrollView(view, bankScroll, content);
            string tooltip = null;
            int withdrawSlot = -1;
            long withdrawAmount = 0;
            for (int i = 0; i < bank.UsedSlots; i++)
            {
                var slot = new Rect((i % BankColumns) * (Slot + Gap), (i / BankColumns) * (Slot + Gap), Slot, Slot);
                var s = bank[i];
                Fill(slot, new Color(0f, 0f, 0f, 0.25f));
                DrawItemIcon(slot, s.Def);
                Shadowed(new Rect(slot.x + 2f, slot.y - 1f, slot.width, 16f), BankCount(s.Count), small);
                if (slot.Contains(e.mousePosition))
                    tooltip = s.Def.Name + " x " + Experience.Format(s.Count);
                if (e.type == EventType.MouseDown && slot.Contains(e.mousePosition))
                {
                    if (e.button == 1)
                        OpenMenu(BankOptions(s), false);
                    else
                    {
                        withdrawSlot = i;  // withdraw after the loop, so the slot list doesn't change mid-draw
                        withdrawAmount = e.shift ? long.MaxValue : bankAmount;
                    }
                    e.Use();
                }
            }
            if (bank.UsedSlots == 0)
                GUI.Label(new Rect(0f, 8f, content.width, 40f), "The bank is empty. Click items in your inventory to deposit them.", rich);
            GUI.EndScrollView();
            if (withdrawSlot >= 0 && G.Bank.Withdraw(G.Inventory, withdrawSlot, withdrawAmount) == 0 && G.Inventory.IsFull)
                GameLog.Add("You don't have enough inventory space.");

            // Amount buttons and deposit-all.
            float y = r.yMax - 76f;
            GUI.Label(new Rect(r.x + Pad, y, 140f, 22f), "Withdraw / deposit:", small);
            for (int i = 0; i < BankAmounts.Length; i++)
            {
                int a = BankAmounts[i];
                if (GUI.Toggle(new Rect(r.x + Pad + 120f + i * 46f, y, 42f, 22f), bankAmount == a, a == int.MaxValue ? "All" : a.ToString(), GUI.skin.button))
                    bankAmount = a;
            }
            if (GUI.Button(new Rect(r.x + Pad, y + 26f, 150f, 22f), "Deposit inventory"))
                bank.DepositAll(G.Inventory);
            if (GUI.Button(new Rect(r.x + Pad, y + 51f, 150f, 22f), "Deposit money pouch"))
                bank.DepositPouch(G.Inventory);
            GUI.Label(new Rect(r.x + Pad + 160f, y + 26f, r.width - Pad * 2 - 160f, 50f),
                "Click: withdraw / deposit that many\nShift-click: all of it. Right-click: options", small);

            if (tooltip != null && new Rect(view.x, view.y, view.width, view.height).Contains(e.mousePosition))
                Shadowed(new Rect(e.mousePosition.x + 14f, e.mousePosition.y - 6f, 260f, 20f), "<color=#ffcc66>" + tooltip + "</color>", small);
        }

        // ---- Shop ----

        void DrawShop()
        {
            var r = WindowRect;
            var shop = ((ShopKeeper)openAt).Shop;
            var e = Event.current;
            Fill(r, new Color(0.24f, 0.2f, 0.15f, 0.96f));
            Fill(new Rect(r.x, r.y, r.width, 30f), new Color(0.16f, 0.13f, 0.09f, 1f));
            Shadowed(new Rect(r.x + Pad, r.y + 5f, 300f, 22f), "<b>" + shop.Name + "</b>", title);
            if (GUI.Button(new Rect(r.xMax - 30f, r.y + 4f, 24f, 22f), "X"))
                CloseWindow();

            // Stock: each item with how many the shop has, and its price under it.
            const float cellH = Slot + 16f;
            var view = new Rect(r.x + Pad, r.y + 38f, r.width - Pad * 2, r.height - 38f - 84f);
            int rows = Mathf.Max(1, (shop.Items.Count + BankColumns - 1) / BankColumns);
            var content = new Rect(0f, 0f, view.width - 18f, rows * (cellH + Gap));
            bankScroll = GUI.BeginScrollView(view, bankScroll, content);
            string tooltip = null;
            int buyIndex = -1, buyAmount = 0;
            for (int i = 0; i < shop.Items.Count; i++)
            {
                var item = shop.Items[i];
                var slot = new Rect((i % BankColumns) * (Slot + Gap), (i / BankColumns) * (cellH + Gap), Slot, Slot);
                int price = Shop.BuyPrice(item.Def);
                Fill(slot, new Color(0f, 0f, 0f, 0.25f));
                DrawItemIcon(slot, item.Def);
                Shadowed(new Rect(slot.x + 2f, slot.y - 1f, slot.width, 16f), (item.Stock > 0 ? "<color=#ffff00>" : "<color=#ff6060>") + item.Stock + "</color>", small);
                Shadowed(new Rect(slot.x - 4f, slot.yMax, slot.width + 8f, 16f), price + " gp", centered);
                if (slot.Contains(e.mousePosition))
                    tooltip = item.Def.Name + ": " + price + " coins (" + item.Stock + " in stock)";
                if (e.type == EventType.MouseDown && slot.Contains(e.mousePosition))
                {
                    if (e.button == 1)
                        OpenMenu(ShopOptions(shop, item), false);
                    else
                    {
                        buyIndex = i;  // buy after the loop, so the stock list doesn't change mid-draw
                        buyAmount = e.shift ? 50 : shopAmount;
                    }
                    e.Use();
                }
            }
            GUI.EndScrollView();
            if (buyIndex >= 0)
            {
                var def = shop.Items[buyIndex].Def;
                if (shop.Buy(G.Inventory, buyIndex, buyAmount, out string problem) < buyAmount && problem != null)
                    GameLog.Add(problem);
            }

            float y = r.yMax - 76f;
            GUI.Label(new Rect(r.x + Pad, y, 140f, 22f), "Buy / sell:", small);
            for (int i = 0; i < ShopAmounts.Length; i++)
            {
                int a = ShopAmounts[i];
                if (GUI.Toggle(new Rect(r.x + Pad + 120f + i * 46f, y, 42f, 22f), shopAmount == a, a.ToString(), GUI.skin.button))
                    shopAmount = a;
            }
            Shadowed(new Rect(r.x + Pad, y + 28f, 160f, 22f), "Your coins: <color=#ffff00>" + Experience.Format(G.Inventory.Count(ItemDefs.Coins.Id)) + "</color>", label);
            GUI.Label(new Rect(r.x + Pad + 160f, y + 26f, r.width - Pad * 2 - 160f, 50f),
                "Click: buy / sell that many. Shift: 50 / all\nRight-click: options. Your items: click to sell", small);

            if (tooltip != null && view.Contains(e.mousePosition))
                Shadowed(new Rect(e.mousePosition.x + 14f, e.mousePosition.y - 6f, 300f, 20f), "<color=#ffcc66>" + tooltip + "</color>", small);
        }

        /// amount 0: just say what the shop would pay.
        void SellToShop(ItemDef def, int amount)
        {
            var shop = ((ShopKeeper)openAt).Shop;
            if (amount == 0 || !Shop.CanSell(def))
            {
                GameLog.Add(Shop.CanSell(def)
                    ? def.Name + ": shop will buy for " + Shop.SellPrice(def) + " coins."
                    : "You can't sell this item to this shop.");
                return;
            }
            if (shop.Sell(G.Inventory, def.Id, amount, out string problem) == 0 && problem != null)
                GameLog.Add(problem);
        }

        /// RuneScape-style stack counts: yellow below 100K, white in K below 10M, green in M above.
        static string BankCount(long n)
        {
            if (n < 100000)
                return "<color=#ffff00>" + n + "</color>";
            if (n < 10000000)
                return "<color=#ffffff>" + (n / 1000) + "K</color>";
            return "<color=#00ff80>" + (n / 1000000) + "M</color>";
        }

        static Color Shade(Color c, float f) => new Color(c.r * f, c.g * f, c.b * f, c.a);

        /// No art yet: items are drawn from coloured rectangles.
        static void DrawItemIcon(Rect slot, ItemDef def)
        {
            var c = new Color(def.R, def.G, def.B);
            if (def == ItemDefs.Logs)
            {
                var end = new Color(0.85f, 0.7f, 0.45f);
                for (int k = 0; k < 3; k++)
                {
                    float y = slot.y + 9f + k * 8f, x = slot.x + 6f + (k == 1 ? 4f : 0f);
                    Fill(new Rect(x, y, 24f, 7f), k == 1 ? Shade(c, 0.85f) : c);
                    Fill(new Rect(x + 24f, y, 4f, 7f), end);
                }
                return;
            }
            float ix = slot.x, iy = slot.y;
            if (def == ItemDefs.Coins)
            {
                var dark = new Color(0.7f, 0.55f, 0.1f);
                for (int k = 0; k < 4; k++)
                {
                    Fill(new Rect(ix + 10f + (k % 2) * 6f, iy + 26f - k * 5f, 16f, 5f), dark);
                    Fill(new Rect(ix + 10f + (k % 2) * 6f, iy + 26f - k * 5f, 16f, 3f), c);
                }
            }
            else if (def == ItemDefs.Tinderbox)
            {
                Fill(new Rect(ix + 9f, iy + 15f, 22f, 14f), c);
                Fill(new Rect(ix + 9f, iy + 13f, 22f, 4f), Shade(c, 0.7f));
                Fill(new Rect(ix + 27f, iy + 8f, 4f, 5f), new Color(1f, 0.55f, 0.1f));
            }
            else if (def == ItemDefs.BronzeSword)
            {
                Fill(new Rect(ix + 18f, iy + 4f, 5f, 22f), c);                          // blade
                Fill(new Rect(ix + 19f, iy + 4f, 2f, 22f), new Color(0.9f, 0.7f, 0.45f));
                Fill(new Rect(ix + 12f, iy + 26f, 17f, 3f), new Color(0.4f, 0.4f, 0.45f)); // guard
                Fill(new Rect(ix + 18f, iy + 29f, 5f, 6f), new Color(0.35f, 0.22f, 0.12f)); // grip
            }
            else if (def == ItemDefs.BronzeHatchet)
            {
                var wood = new Color(0.5f, 0.32f, 0.16f);
                var pivot = new Vector2(slot.x + slot.width / 2f, slot.y + slot.height / 2f);
                FillRotated(new Rect(ix + 18.5f, iy + 7f, 3f, 27f), wood, -35f, pivot);            // handle
                FillRotated(new Rect(ix + 18.5f, iy + 6f, 11f, 4f), c, -35f, pivot);               // head
                FillRotated(new Rect(ix + 18.5f, iy + 10f, 11f, 4f), c, -35f, pivot);
                FillRotated(new Rect(ix + 27f, iy + 5f, 3f, 10f), Shade(c, 1.25f), -35f, pivot);   // edge
            }
            else if (def == ItemDefs.Bones)
            {
                var pivot = new Vector2(slot.x + slot.width / 2f, slot.y + slot.height / 2f);
                FillRotated(new Rect(ix + 17f, iy + 9f, 6f, 22f), c, -35f, pivot);   // shaft
                FillRotated(new Rect(ix + 13f, iy + 6f, 6f, 6f), c, -35f, pivot);    // knobs
                FillRotated(new Rect(ix + 21f, iy + 6f, 6f, 6f), c, -35f, pivot);
                FillRotated(new Rect(ix + 13f, iy + 28f, 6f, 6f), c, -35f, pivot);
                FillRotated(new Rect(ix + 21f, iy + 28f, 6f, 6f), c, -35f, pivot);
            }
            else if (def == ItemDefs.Ashes)
            {
                Fill(new Rect(ix + 9f, iy + 24f, 22f, 7f), c);
                Fill(new Rect(ix + 13f, iy + 19f, 14f, 6f), Shade(c, 1.2f));
                Fill(new Rect(ix + 17f, iy + 15f, 6f, 5f), Shade(c, 1.35f));
            }
            else if (def == ItemDefs.SmallFishingNet)
            {
                var wood = new Color(0.5f, 0.34f, 0.18f);
                Fill(new Rect(ix + 7f, iy + 5f, 18f, 18f), wood);
                Fill(new Rect(ix + 9f, iy + 7f, 14f, 14f), c);
                for (int k = 0; k < 3; k++)
                {
                    Fill(new Rect(ix + 12f + k * 3.5f, iy + 7f, 1f, 14f), Shade(c, 0.6f));
                    Fill(new Rect(ix + 9f, iy + 10f + k * 3.5f, 14f, 1f), Shade(c, 0.6f));
                }
                Fill(new Rect(ix + 23f, iy + 21f, 4f, 13f), wood);
            }
            else if (def == ItemDefs.RawShrimps || def == ItemDefs.RawAnchovies || def == ItemDefs.Shrimps
                || def == ItemDefs.Anchovies || def == ItemDefs.BurntShrimp || def == ItemDefs.BurntFish)
            {
                int n = def == ItemDefs.RawShrimps || def == ItemDefs.Shrimps || def == ItemDefs.BurntShrimp ? 2 : 3;
                for (int k = 0; k < n; k++)
                {
                    float fy = iy + 10f + k * 8f;
                    Fill(new Rect(ix + 8f, fy, 18f, 5f), c);
                    Fill(new Rect(ix + 26f, fy - 1f, 4f, 7f), Shade(c, 0.75f));
                    Fill(new Rect(ix + 10f, fy + 1f, 2f, 2f), new Color(0.1f, 0.1f, 0.1f));
                }
            }
            else if (def == ItemDefs.WoodenShield)
            {
                Fill(new Rect(ix + 8f, iy + 6f, 24f, 28f), new Color(0.45f, 0.45f, 0.5f));  // rim
                Fill(new Rect(ix + 10f, iy + 8f, 20f, 24f), c);
                Fill(new Rect(ix + 16f, iy + 8f, 1f, 24f), Shade(c, 0.7f));
                Fill(new Rect(ix + 23f, iy + 8f, 1f, 24f), Shade(c, 0.7f));
                Fill(new Rect(ix + 17f, iy + 17f, 6f, 6f), new Color(0.6f, 0.6f, 0.65f));  // boss
            }
            else
            {
                Fill(new Rect(ix + 8f, iy + 8f, slot.width - 16f, slot.height - 16f), c);
            }
        }

        // ---- Skills (laid out like RuneScape 3's skills panel) ----

        static readonly Color CellBg = new Color(0.12f, 0.11f, 0.09f, 1f);
        static readonly Color BevelLight = new Color(0.36f, 0.32f, 0.25f, 1f);
        static readonly Color BevelDark = new Color(0.04f, 0.04f, 0.03f, 1f);

        /// A sunken box with a light bottom-right edge and a dark top-left edge.
        static void Bevel(Rect r)
        {
            Fill(r, BevelLight);
            Fill(new Rect(r.x, r.y, r.width - 1f, r.height - 1f), BevelDark);
            Fill(new Rect(r.x + 1f, r.y + 1f, r.width - 2f, r.height - 2f), CellBg);
        }

        void DrawSkills(Rect body)
        {
            const int cols = 3;
            const float gap = 3f, cellH = 30f, footH = 28f;
            float cellW = (body.width - gap * (cols - 1)) / cols;
            var e = Event.current;
            Shadowed(new Rect(body.x, body.y - 2f, body.width, 18f), "<b>SKILLS</b>", small);
            float top = body.y + 18f;
            SkillDef hovered = null;
            Rect hoveredRect = default(Rect);

            // One cell per skill: its icon, then the level twice, stacked (current over base,
            // as in RuneScape; they're the same until there are boosts).
            var defs = SkillDefs.All;
            for (int i = 0; i < defs.Length; i++)
            {
                var def = defs[i];
                var cell = new Rect(body.x + (i % cols) * (cellW + gap), top + (i / cols) * (cellH + gap), cellW, cellH);
                bool over = cell.Contains(e.mousePosition);
                Bevel(cell);
                if (over)
                {
                    Fill(new Rect(cell.x + 1f, cell.y + 1f, cell.width - 2f, cell.height - 2f), new Color(1f, 1f, 1f, 0.08f));
                    hovered = def;
                    hoveredRect = cell;
                }
                DrawSkillIcon(new Rect(cell.x + 3f, cell.y + 4f, 22f, 22f), def);

                int level = G.Skills.GetLevel(def.Id);
                var numbers = new Rect(cell.x + 25f, cell.y + 1f, cell.width - 28f, cell.height - 2f);
                Fill(new Rect(numbers.x + 4f, numbers.y + numbers.height / 2f, numbers.width - 4f, 1f), new Color(1f, 1f, 1f, 0.12f));
                string text = "<color=#e8e2d0>" + level + "</color>";
                Shadowed(new Rect(numbers.x, numbers.y - 1f, numbers.width, 16f), text, rightSmall);
                Shadowed(new Rect(numbers.x, numbers.y + numbers.height / 2f - 1f, numbers.width, 16f), text, rightSmall);
            }

            // Footer: total level, combat level, total xp.
            var foot = new Rect(body.x, body.yMax - footH, body.width, footH);
            float boxW = (foot.width - gap * 2) / 3f;
            BigInteger totalXp = BigInteger.Zero;
            foreach (var def in defs)
                totalXp += G.Skills.GetXp(def.Id);
            string[] values =
            {
                G.Skills.TotalLevel.ToString(),
                CombatRules.CombatLevel(G.Skills).ToString(),
                Experience.FormatShort(totalXp),
            };
            string[] tips = { "Total level", "Combat level", "Total XP: " + Experience.Format(totalXp) };
            string footTip = null;
            for (int i = 0; i < 3; i++)
            {
                var box = new Rect(foot.x + i * (boxW + gap), foot.y, boxW, foot.height);
                Bevel(box);
                DrawFooterIcon(new Rect(box.x + 3f, box.y + 4f, 20f, 20f), i);
                Shadowed(new Rect(box.x + 22f, box.y + 6f, box.width - 25f, 18f), "<color=#e8e2d0>" + values[i] + "</color>", rightSmall);
                if (box.Contains(e.mousePosition))
                    footTip = tips[i];
            }
            if (footTip != null)
                Shadowed(new Rect(e.mousePosition.x - 210f, e.mousePosition.y - 22f, 200f, 20f), "<color=#ffcc66>" + footTip + "</color>", rightSmall);

            if (hovered != null)
                DrawSkillTooltip(hovered, hoveredRect);
        }

        /// The hover box: name, xp, next level, xp left, and a progress bar.
        void DrawSkillTooltip(SkillDef def, Rect cell)
        {
            BigInteger xp = G.Skills.GetXp(def.Id);
            int level = Experience.LevelForXp(xp);
            const float w = 210f, h = 84f;
            // To the left of the panel, level with the cell.
            var r = new Rect(Mathf.Max(4f, PanelRect.x - w - 6f), Mathf.Clamp(cell.y - 10f, 4f, H - h - 4f), w, h);
            Fill(r, new Color(0.06f, 0.05f, 0.04f, 0.95f));
            Fill(new Rect(r.x, r.y, r.width, 1f), BevelLight);
            Fill(new Rect(r.x, r.yMax - 1f, r.width, 1f), BevelLight);
            Fill(new Rect(r.x, r.y, 1f, r.height), BevelLight);
            Fill(new Rect(r.xMax - 1f, r.y, 1f, r.height), BevelLight);
            Shadowed(new Rect(r.x + 8f, r.y + 4f, w, 18f), "<b><color=#ffcc66>" + def.Name + "</color></b>  level " + level + " / " + Experience.MaxLevel, small);
            GUI.Label(new Rect(r.x + 8f, r.y + 22f, w - 16f, 16f), "XP: " + Experience.Format(xp), small);
            if (level < Experience.MaxLevel)
            {
                GUI.Label(new Rect(r.x + 8f, r.y + 37f, w - 16f, 16f), "Next level at: " + Experience.FormatShort(Experience.XpForLevel(level + 1)), small);
                GUI.Label(new Rect(r.x + 8f, r.y + 52f, w - 16f, 16f), "Remaining: " + Experience.FormatShort(Experience.XpToNextLevel(xp)), small);
            }
            else
            {
                GUI.Label(new Rect(r.x + 8f, r.y + 37f, w - 16f, 16f), "Maxed!", small);
            }
            var bar = new Rect(r.x + 8f, r.yMax - 12f, w - 16f, 6f);
            Fill(bar, new Color(0.5f, 0.1f, 0.1f));
            Fill(new Rect(bar.x, bar.y, bar.width * (float)Experience.LevelProgress(xp), bar.height), new Color(0.2f, 0.75f, 0.2f));
        }

        /// Fills a rectangle turned by `angle` degrees around `pivot` (for diagonal blades and handles).
        /// It's drawn as a row of small squares along its length, not by rotating GUI.matrix: Unity's
        /// RotateAroundPivot ignores the HUD's scale, which threw rotated icons off their slots
        /// (above the inventory at 1080p).
        static void FillRotated(Rect r, Color c, float angle, Vector2 pivot)
        {
            float rad = angle * Mathf.Deg2Rad, cos = Mathf.Cos(rad), sin = Mathf.Sin(rad);
            bool tall = r.height >= r.width;
            float thick = tall ? r.width : r.height;
            float half = ((tall ? r.height : r.width) - thick) / 2f;
            float cx = r.x + r.width / 2f, cy = r.y + r.height / 2f;
            // The two ends of the long axis, turned round the pivot (y points down, so +angle is clockwise).
            float ax = tall ? cx : cx - half, ay = tall ? cy - half : cy;
            float bx = tall ? cx : cx + half, by = tall ? cy + half : cy;
            float x0 = pivot.x + (ax - pivot.x) * cos - (ay - pivot.y) * sin, y0 = pivot.y + (ax - pivot.x) * sin + (ay - pivot.y) * cos;
            float x1 = pivot.x + (bx - pivot.x) * cos - (by - pivot.y) * sin, y1 = pivot.y + (bx - pivot.x) * sin + (by - pivot.y) * cos;
            float length = Mathf.Sqrt((x1 - x0) * (x1 - x0) + (y1 - y0) * (y1 - y0));
            int steps = Mathf.Max(1, Mathf.CeilToInt(length / Mathf.Max(0.75f, thick * 0.5f)));
            for (int i = 0; i <= steps; i++)
            {
                float t = (float)i / steps;
                float x = x0 + (x1 - x0) * t, y = y0 + (y1 - y0) * t;
                Fill(new Rect(x - thick / 2f, y - thick / 2f, thick, thick), c);
            }
        }

        /// A filled diamond (a square turned 45 degrees), drawn row by row.
        static void FillDiamond(float cx, float cy, float radius, Color c)
        {
            for (float dy = -radius; dy < radius; dy += 1f)
            {
                float w = (radius - Mathf.Abs(dy + 0.5f)) * 2f;
                Fill(new Rect(cx - w / 2f, cy + dy, w, 1f), c);
            }
        }

        /// No art yet: each skill's icon is drawn from rectangles.
        static void DrawSkillIcon(Rect b, SkillDef def)
        {
            float cx = b.center.x, cy = b.center.y;
            var steel = new Color(0.78f, 0.8f, 0.84f);
            var wood = new Color(0.5f, 0.32f, 0.16f);
            var gold = new Color(0.9f, 0.72f, 0.2f);
            var c = b.center;
            switch (def.Id)
            {
                case SkillId.Attack:  // a sword, point up and to the right
                    FillRotated(new Rect(cx - 1.5f, cy - 10f, 3f, 14f), steel, 45f, c);
                    FillRotated(new Rect(cx - 5f, cy + 4f, 10f, 2f), gold, 45f, c);
                    FillRotated(new Rect(cx - 1f, cy + 6f, 2f, 4f), wood, 45f, c);
                    break;
                case SkillId.Strength:  // a clenched fist
                    var skin = new Color(0.86f, 0.6f, 0.4f);
                    Fill(new Rect(cx - 6f, cy - 5f, 12f, 11f), skin);
                    Fill(new Rect(cx - 9f, cy - 1f, 4f, 6f), skin);
                    for (int k = 0; k < 3; k++)
                        Fill(new Rect(cx - 3f + k * 3f, cy - 5f, 1f, 5f), Shade(skin, 0.7f));
                    Fill(new Rect(cx - 4f, cy + 6f, 8f, 4f), new Color(0.55f, 0.35f, 0.2f));
                    break;
                case SkillId.Defence:  // a kite shield
                    var rim = new Color(0.6f, 0.62f, 0.66f);
                    var face = new Color(0.3f, 0.45f, 0.75f);
                    Fill(new Rect(cx - 8f, cy - 9f, 16f, 11f), rim);
                    Fill(new Rect(cx - 6f, cy + 2f, 12f, 4f), rim);
                    Fill(new Rect(cx - 4f, cy + 6f, 8f, 3f), rim);
                    Fill(new Rect(cx - 2f, cy + 9f, 4f, 2f), rim);
                    Fill(new Rect(cx - 6f, cy - 7f, 12f, 9f), face);
                    Fill(new Rect(cx - 4f, cy + 2f, 8f, 3f), face);
                    Fill(new Rect(cx - 1f, cy - 7f, 2f, 13f), rim);
                    break;
                case SkillId.Hitpoints:  // a heart
                    var red = new Color(0.85f, 0.12f, 0.15f);
                    Fill(new Rect(cx - 8f, cy - 7f, 7f, 6f), red);
                    Fill(new Rect(cx + 1f, cy - 7f, 7f, 6f), red);
                    Fill(new Rect(cx - 8f, cy - 3f, 16f, 4f), red);
                    Fill(new Rect(cx - 6f, cy + 1f, 12f, 3f), red);
                    Fill(new Rect(cx - 4f, cy + 4f, 8f, 3f), red);
                    Fill(new Rect(cx - 2f, cy + 7f, 4f, 2f), red);
                    Fill(new Rect(cx - 6f, cy - 6f, 2f, 2f), new Color(1f, 0.7f, 0.7f));
                    break;
                case SkillId.Woodcutting:  // a hatchet
                    FillRotated(new Rect(cx - 1.25f, cy - 10f, 2.5f, 20f), wood, -40f, c);
                    FillRotated(new Rect(cx - 1.25f, cy - 10f, 8f, 6f), steel, -40f, c);
                    break;
                case SkillId.Firemaking:  // a flame
                    Fill(new Rect(cx - 6f, cy - 1f, 12f, 9f), new Color(0.95f, 0.3f, 0.05f));
                    Fill(new Rect(cx - 4f, cy - 6f, 8f, 6f), new Color(0.95f, 0.3f, 0.05f));
                    Fill(new Rect(cx - 2f, cy - 10f, 4f, 5f), new Color(0.95f, 0.3f, 0.05f));
                    Fill(new Rect(cx - 3f, cy + 1f, 6f, 6f), new Color(1f, 0.75f, 0.15f));
                    Fill(new Rect(cx - 1f, cy - 3f, 3f, 5f), new Color(1f, 0.75f, 0.15f));
                    Fill(new Rect(cx - 7f, cy + 8f, 14f, 2f), wood);
                    break;
                case SkillId.Prayer:  // a star
                    DrawPrayerStar(b, true);
                    break;
                case SkillId.Cooking:  // a chef's hat
                    var white = new Color(0.95f, 0.95f, 0.92f);
                    Fill(new Rect(cx - 8f, cy - 8f, 7f, 7f), white);
                    Fill(new Rect(cx - 3f, cy - 10f, 7f, 7f), white);
                    Fill(new Rect(cx + 2f, cy - 8f, 6f, 7f), white);
                    Fill(new Rect(cx - 6f, cy - 3f, 12f, 8f), white);
                    Fill(new Rect(cx - 6f, cy + 5f, 12f, 3f), new Color(0.75f, 0.75f, 0.72f));
                    break;
                case SkillId.Fishing:  // a fish
                    var fishC = new Color(0.4f, 0.65f, 0.95f);
                    Fill(new Rect(cx - 8f, cy - 3f, 12f, 7f), fishC);
                    Fill(new Rect(cx - 6f, cy - 5f, 8f, 11f), fishC);
                    Fill(new Rect(cx + 4f, cy - 5f, 3f, 11f), Shade(fishC, 0.75f));
                    Fill(new Rect(cx - 6f, cy - 1f, 2f, 2f), new Color(0.05f, 0.05f, 0.1f));
                    break;
                default:
                    Fill(new Rect(cx - 7f, cy - 7f, 14f, 14f), new Color(def.R, def.G, def.B));
                    break;
            }
        }

        static void DrawFooterIcon(Rect b, int which)
        {
            float cx = b.center.x, cy = b.center.y;
            if (which == 0)
            {
                // Total level: three bars.
                Fill(new Rect(cx - 7f, cy + 1f, 4f, 7f), new Color(0.85f, 0.2f, 0.2f));
                Fill(new Rect(cx - 2f, cy - 3f, 4f, 11f), new Color(0.25f, 0.75f, 0.3f));
                Fill(new Rect(cx + 3f, cy - 7f, 4f, 15f), new Color(0.3f, 0.5f, 0.9f));
            }
            else if (which == 1)
            {
                // Combat level: crossed swords.
                var steel = new Color(0.78f, 0.8f, 0.84f);
                FillRotated(new Rect(cx - 1f, cy - 9f, 2f, 18f), steel, 45f, b.center);
                FillRotated(new Rect(cx - 1f, cy - 9f, 2f, 18f), steel, -45f, b.center);
                FillRotated(new Rect(cx - 4f, cy + 4f, 8f, 2f), new Color(0.9f, 0.72f, 0.2f), 45f, b.center);
                FillRotated(new Rect(cx - 4f, cy + 4f, 8f, 2f), new Color(0.9f, 0.72f, 0.2f), -45f, b.center);
            }
            else
            {
                // Total xp: a blue star.
                var blue = new Color(0.35f, 0.6f, 1f);
                Fill(new Rect(cx - 7f, cy - 2f, 14f, 4f), blue);
                Fill(new Rect(cx - 2f, cy - 7f, 4f, 14f), blue);
                FillDiamond(cx, cy, 7f, blue);
                Fill(new Rect(cx - 2f, cy - 2f, 4f, 4f), Color.white);
            }
        }

        // ---- Dev tools ----

        void DrawDevWindow(int id)
        {
            {
                // One skill at a time (< > to pick it), so the window stays short as skills are added.
                devSkill = (devSkill + SkillDefs.All.Length) % SkillDefs.All.Length;
                var def = SkillDefs.All[devSkill];
                int level = G.Skills.GetLevel(def.Id);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("<", GUILayout.Width(26f))) devSkill--;
                GUILayout.Label("<b>" + def.Name + "</b>  level " + level + "  (" + Experience.FormatShort(G.Skills.GetXp(def.Id)) + " xp)", rich);
                if (GUILayout.Button(">", GUILayout.Width(26f))) devSkill++;
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
            }

            GUILayout.Label("<b>Inventory</b>", rich);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Empty it")) { G.Inventory.Clear(); selectedSlot = -1; }
            if (GUILayout.Button("Fill with logs")) G.Inventory.Add(ItemDefs.Logs.Id, Inventory.Size);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Empty bank")) G.Bank.Clear();
            if (GUILayout.Button("+1,000 logs in bank")) G.Bank.Add(ItemDefs.Logs.Id, 1000);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("+1,000 coins")) G.Inventory.Add(ItemDefs.Coins.Id, 1000);
            if (GUILayout.Button("Sword + shield")) { G.Inventory.Add(ItemDefs.BronzeSword.Id); G.Inventory.Add(ItemDefs.WoodenShield.Id); }
            if (GUILayout.Button("Tools")) { G.Inventory.Add(ItemDefs.BronzeHatchet.Id); G.Inventory.Add(ItemDefs.Tinderbox.Id); G.Inventory.Add(ItemDefs.SmallFishingNet.Id); }
            if (GUILayout.Button("Fill belt")) foreach (var t in ToolBelt.Tools) G.ToolBelt.Add(t.Id);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Hurt me 3")) G.HurtPlayer(3);
            if (GUILayout.Button("Heal fully")) G.Health.Set(G.Health.Max);
            if (GUILayout.Button("5 shrimps")) G.Inventory.Add(ItemDefs.Shrimps.Id, 5);
            if (GUILayout.Button("Recharge prayer")) G.Prayer.Recharge();
            GUILayout.EndHorizontal();

            GUILayout.Label("<b>World</b>", rich);
            if (GUILayout.Button("Regrow every tree"))
                foreach (var t in G.World.Trees)
                    if (t.IsStump)
                        t.Regrow();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Go to spawn"))
                G.Player.Teleport(G.World.Spawn);
            foreach (var area in G.World.Areas)
                if (!(area is EmptyPlot) && GUILayout.Button("Go to " + area.Name))
                    G.Player.Teleport(G.World.NearestWalkable(area.TeleportTile));
            GUILayout.EndHorizontal();

            GUILayout.Label("<b>Save</b>", rich);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Save now")) { SaveGame.Save(G); GameLog.Add("Saved."); }
            if (GUILayout.Button("Reset everything"))
            {
                SaveGame.Delete();
                foreach (var def in SkillDefs.All)
                    G.Skills.SetLevel(def.Id, def.StartLevel);
                foreach (EquipSlot slot in System.Enum.GetValues(typeof(EquipSlot)))
                    if (slot != EquipSlot.None)
                        G.Equipment.Set(slot, default(ItemStack));
                G.Inventory.Clear();
                G.Inventory.SetPouch(0);
                G.Inventory.Add(ItemDefs.Coins.Id, 100);
                G.ToolBelt.Clear();
                G.ToolBelt.Add(ItemDefs.BronzeHatchet.Id);
                G.Health.SetMax(G.Skills.GetLevel(SkillId.Hitpoints));
                G.Health.Set(G.Health.Max);
                G.Prayer.DeactivateAll();
                G.Prayer.SetMax(G.Skills.GetLevel(SkillId.Prayer));
                G.Prayer.Recharge();
                G.Bank.Clear();
                G.Player.Teleport(G.World.Spawn);
                GameLog.Add("Progress reset.");
            }
            GUILayout.EndHorizontal();
            GUI.DragWindow();
        }
    }
}
