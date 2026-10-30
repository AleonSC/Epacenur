using System.Collections.Generic;
using System.Numerics;
using Epacenur.Core;
using UnityEngine;
using Matrix4x4 = UnityEngine.Matrix4x4;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;

namespace Epacenur.Runtime
{
    /// Everything on screen. The HUD is laid out here and drawn by its parts: the side panel and its tabs
    /// (UI/Tabs), the bank, shop and making windows (UI/Windows), the chat box, action bar, status bars and
    /// overlays (UI/Overlays.cs), and the dev tools. The window open at a station closes when you walk away.
    public sealed class Hud : MonoBehaviour
    {
        float scale = 1f;
        public float W => Screen.width / scale;
        public float H => Screen.height / scale;
        public float Scale => scale;

        public readonly SidePanel Panel = new SidePanel();
        readonly Overlays overlays = new Overlays();
        readonly ActionBar actionBar = new ActionBar();
        readonly StatusBars statusBars = new StatusBars();
        readonly DevTools devTools = new DevTools();
        readonly Minimap minimap = new Minimap();
        readonly WorldMap worldMap = new WorldMap();
        readonly OptionMenu menu = new OptionMenu();
        bool showHelp = true;
        /// The chat box (C) and the side panel with the inventory (B) can be put away.
        public bool ChatOpen = true, PanelOpen = true;

        /// The window open at a station or shop keeper, or null.
        public Window Window { get; private set; }

        public Rect PanelRect => new Rect(W - SidePanel.Width - 10f, H - SidePanel.Height - 10f, SidePanel.Width, SidePanel.Height);
        /// Bottom left, as wide as fits beside the action bar.
        public Rect ChatRect => new Rect(10f, H - 160f, Mathf.Clamp((W - BarWidth) / 2f - 20f, 200f, 520f), 150f);
        static float BarWidth => 12f + Abilities.All.Length * (Gui.Slot + Gui.Gap) - Gui.Gap;
        /// The abilities, in the middle at the bottom, with your health and prayer over them.
        public Rect BarRect => new Rect((W - BarWidth) / 2f, H - 62f, BarWidth, 52f);
        public Rect StatusRect => new Rect(BarRect.x, BarRect.y - 24f, BarRect.width, 22f);
        /// The buttons that put away and bring back the chat box and the side panel.
        Rect ChatToggleRect => ChatOpen ? new Rect(10f, ChatRect.y - 24f, 64f, 22f) : new Rect(10f, H - 32f, 64f, 22f);
        Rect PanelToggleRect => PanelOpen ? new Rect(W - 100f, PanelRect.y - 26f, 90f, 22f) : new Rect(W - 100f, H - 32f, 90f, 22f);
        public Rect WindowRect
        {
            get
            {
                float w = Gui.Pad * 2 + 8 * (Gui.Slot + Gui.Gap) - Gui.Gap + 18f, h = 400f;
                return new Rect(Mathf.Max(10f, (W - SidePanel.Width - 20f - w) / 2f), Mathf.Max(60f, (H - h) / 2f - 50f), w, h);
            }
        }

        public bool ShowHelp => showHelp;
        public bool MenuOpen => menu.IsOpen || pendingMenu != null;
        Vector2 MouseGui => new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y) / scale;

        void Update()
        {
            if (G == null)
                return;
            scale = Mathf.Clamp(Screen.height / 760f, 0.75f, 3f);
            UpdateZone();
            if (Input.GetKeyDown(KeyCode.F1))
                devTools.Toggle();
            if (Input.GetKeyDown(KeyCode.H))
                showHelp = !showHelp;
            if (Window != null)
            {
                // Like RuneScape, the bank or shop closes when you walk away.
                if (Input.GetKeyDown(KeyCode.Escape) || (!Window.Anywhere && (Window.At == null || G.Player.FlatDistance(Window.At) > 2.2f)))
                    CloseWindow();
            }
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                G.Player.CancelUse();
                worldMap.Close();
            }
            if (Input.GetKeyDown(KeyCode.M) && GUIUtility.keyboardControl == 0)
                worldMap.Toggle();
            if (Input.GetKeyDown(KeyCode.C) && GUIUtility.keyboardControl == 0)
                ChatOpen = !ChatOpen;
            if (Input.GetKeyDown(KeyCode.B) && GUIUtility.keyboardControl == 0)
                TogglePanel();
        }

        Game G => Game.Instance;

        public void OpenBank(Interactable at) => OpenWindow(new BankWindow(), at);
        public void OpenShop(ShopKeeper keeper) => OpenWindow(new ShopWindow(keeper.Shop), keeper);
        /// The make window at a station; `prefer`: an item used on it, to show only what's made with that.
        public void OpenMaking(StationObject station, string prefer) => OpenWindow(new MakeWindow(station, prefer), station);

        /// The make window for things made in your hands from the inventory (fletching).
        public void OpenMakingInHand(List<RecipeDef> recipes) => OpenWindow(new MakeWindow(null, null, recipes), null);

        /// Puts the side panel away or brings it back.
        public void TogglePanel()
        {
            PanelOpen = !PanelOpen;
            if (!PanelOpen)
                G.Player.CancelUse();  // a picked item or spell can't be aimed at a hidden inventory
        }

        public void OpenWindow(Window window, Interactable at)
        {
            window.At = at;
            Window = window;
            PanelOpen = true;  // the bank, shop and make windows work with your inventory
            Panel.ShowInventory();
            G.Rig.LockCursor(false);  // first person: free the mouse to use the window
        }

        public void CloseWindow() => Window = null;

        /// Opens the right-click menu at the mouse (or mid-screen in first person, where relock is true).
        /// (It opens on the next OnGUI: laying it out needs Unity's GUI, which only works there, and the
        /// world's right-click comes from the player's Update.)
        public void OpenMenu(List<MenuOption> options, bool relock)
        {
            pendingMenu = options;
            pendingAt = relock ? new Vector2(W / 2f, H / 2f) : MouseGui;
            pendingRelock = relock;
        }

        List<MenuOption> pendingMenu;
        Vector2 pendingAt;
        bool pendingRelock;

        /// Whether the mouse is over a panel (so clicks there don't walk or chop).
        public bool IsMouseOverUi()
        {
            if (Cursor.lockState == CursorLockMode.Locked)
                return false;
            var m = MouseGui;
            return (menu.IsOpen && menu.Rect.Contains(m)) || (PanelOpen && PanelRect.Contains(m)) || (ChatOpen && ChatRect.Contains(m))
                || ChatToggleRect.Contains(m) || PanelToggleRect.Contains(m) || BarRect.Contains(m)
                || StatusRect.Contains(m) || LogOutRect.Contains(m) || MapButtonRect.Contains(m) || Minimap.RectFor(this).Contains(m)
                || (worldMap.IsOpen && worldMap.RectFor(this).Contains(m)) || devTools.Contains(m) || (Window != null && WindowRect.Contains(m));
        }

        Rect LogOutRect => new Rect(W - 90f, Minimap.RectFor(this).yMax + 4f, 80f, 24f);
        Rect MapButtonRect => new Rect(W - 180f, Minimap.RectFor(this).yMax + 4f, 86f, 24f);

        /// Under the minimap: how long your gravestone has left.
        void DrawGraveTimer()
        {
            var g = G.Grave;
            if (g == null)
                return;
            string text = g.world == G.WorldInfo.id ? "Gravestone: <color=#ffffff>" + Gravestone.Time(g) + "</color>" : "Gravestone in another world";
            Gui.Shadowed(new Rect(W - 300f, LogOutRect.yMax + 24f, 290f, 20f), "<color=" + Gui.MutedHex + ">" + text + "</color>", Gui.RightSmall);
        }

        // ---- Safety and the wilderness ----

        Tile zoneTile = new Tile(int.MinValue, 0);
        int zoneDanger = -1;
        Biome zoneBiome;
        string zoneTown;

        /// "Safe" in green, or the wilderness level, yellow turning red the deeper it is.
        public static string DangerText(int danger)
        {
            if (danger == 0)
                return "<color=#8fe08a>Safe</color>";
            float t = Mathf.Min(1f, danger / 40f);
            var c = new Color32(255, (byte)(220 - 170 * t), (byte)(90 - 50 * t), 255);
            return "<color=#" + c.r.ToString("x2") + c.g.ToString("x2") + c.b.ToString("x2") + ">Wilderness level " + danger + "</color>";
        }

        /// Works out where you are when you move to a new tile, and says so in the chat when you leave the safety
        /// of a town for the wilderness, go deep into it, or come back.
        void UpdateZone()
        {
            if (G.World.Map != MapId.Overworld)
            {
                zoneDanger = -1;
                return;
            }
            var here = G.Player.CurrentTile;
            if (here == zoneTile)
                return;
            zoneTile = here;
            var ow = G.World.Overworld;
            int danger = ow.Danger(here);
            zoneBiome = ow.BiomeAt(here);
            var town = ow.NearestSettlement(here);
            zoneTown = town != null ? OverworldBuilder.SettlementName(ow, town) : null;
            if (zoneDanger >= 0)
            {
                if (zoneDanger == 0 && danger > 0)
                    GameLog.Add("<color=#ffb070>You leave the safety of " + zoneTown + " for the wilderness. Monsters grow fiercer the further you go.</color>");
                else if (zoneDanger > 0 && danger == 0)
                    GameLog.Add("<color=#8fe08a>You reach the safety of " + zoneTown + ".</color>");
                else if (zoneDanger < Overworld.DeepWilderness && danger >= Overworld.DeepWilderness)
                    GameLog.Add("<color=#ff6b5e>You enter the deep wilderness. The best resources are here, and the fiercest monsters.</color>");
            }
            zoneDanger = danger;
        }

        /// Under the minimap: safe (near which town) or the wilderness level, and the biome.
        void DrawZone()
        {
            if (zoneDanger < 0)
                return;
            string where = zoneDanger == 0 && zoneTown != null ? DangerText(0) + " <color=" + Gui.MutedHex + ">- " + zoneTown + "</color>" : DangerText(zoneDanger);
            Gui.Shadowed(new Rect(W - 300f, LogOutRect.yMax + 4f, 290f, 20f), "<b>" + where + "</b>  <color=" + Gui.MutedHex + ">" + zoneBiome + "</color>", Gui.RightSmall);
        }
        bool confirmLogOut;

        /// Top right: save and go back to the main menu (two clicks).
        void DrawLogOut()
        {
            var r = LogOutRect;
            if (GUI.Button(r, confirmLogOut ? "Sure?" : "Log out"))
            {
                if (confirmLogOut)
                    G.LogOut();
                else
                    confirmLogOut = true;
            }
            if (confirmLogOut && !r.Contains(Event.current.mousePosition) && Event.current.type == EventType.Repaint)
                confirmLogOut = false;
        }

        public void AddXpDrop(string skill, BigInteger tenths) => overlays.AddXpDrop(skill, tenths);
        public void ShowLevelUp(string skill, int level) => overlays.ShowLevelUp(skill, level);

        void OnGUI()
        {
            if (G == null)
                return;  // logged out this frame
            Gui.Begin();
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            if (pendingMenu != null)
            {
                menu.Open(pendingMenu, pendingAt, new Vector2(W, H), pendingRelock);
                pendingMenu = null;
            }
            menu.HandleInput();  // before anything else, so a click on the menu goes nowhere else
            overlays.DrawWorld(this);
            overlays.DrawScreen(this);
            actionBar.Draw(this);
            statusBars.Draw(this);
            if (PanelOpen)
                Panel.Draw(this, PanelRect);
            if (GUI.Button(ChatToggleRect, ChatOpen ? "Hide chat" : "Chat"))
                ChatOpen = !ChatOpen;
            if (GUI.Button(PanelToggleRect, PanelOpen ? "Hide panel" : "Inventory"))
                TogglePanel();
            Window?.Draw(this, WindowRect);
            if (G.Player.Mode == ViewMode.FirstPerson)
                overlays.DrawCrosshair(this);
            minimap.Draw(this);
            DrawLogOut();
            DrawZone();
            DrawGraveTimer();
            if (GUI.Button(MapButtonRect, "World map"))
                worldMap.Toggle();
            worldMap.Draw(this);
            devTools.Draw(this);
            menu.Draw();
        }
    }

    /// A window opened at something in the world (bank booth, shop keeper, anvil). It changes what
    /// clicking your inventory does while it's open (deposit, sell).
    public abstract class Window
    {
        protected static Game G => Game.Instance;
        public Interactable At;
        /// Not tied to a place: stays open wherever you are (making things in your hands).
        public bool Anywhere;
        protected Vector2 scroll;

        public abstract void Draw(Hud hud, Rect r);

        /// A left-click on an inventory item while the window is open; true if the window handled it.
        public virtual bool InventoryClick(Hud hud, int slot, bool shift) => false;

        /// The right-click options for an inventory item while the window is open, or null for the usual ones.
        public virtual List<MenuOption> InventoryOptions(Hud hud, int slot) => null;

        /// Clicking the money pouch (true if handled), and extra pouch options.
        public virtual bool PouchClick() => false;
        public virtual void PouchOptions(List<MenuOption> options) { }

        /// The window's background and title bar, with a close button.
        protected static void Frame(Hud hud, Rect r, string title)
        {
            if (Gui.TitledPanel(r, title, true))
                hud.CloseWindow();
        }

        /// A divider across the window over its controls at the bottom (at `y`).
        protected static void Divider(Rect r, float y) => Gui.Fill(new Rect(r.x + Gui.Pad, y, r.width - Gui.Pad * 2, 1f), Gui.TrimDim);
    }
}
