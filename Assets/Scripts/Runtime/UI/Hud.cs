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

        /// The window open at a station or shop keeper, or null.
        public Window Window { get; private set; }

        public Rect PanelRect => new Rect(W - SidePanel.Width - 10f, H - SidePanel.Height - 10f, SidePanel.Width, SidePanel.Height);
        public Rect ChatRect => new Rect(10f, H - 160f, Mathf.Clamp(W - SidePanel.Width - 40f, 200f, 520f), 150f);
        public Rect BarRect => new Rect(10f, ChatRect.y - 58f, 12f + Abilities.All.Length * (Gui.Slot + Gui.Gap) - Gui.Gap, 52f);
        public Rect StatusRect => new Rect(BarRect.x, BarRect.y - 24f, BarRect.width, 22f);
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
        }

        Game G => Game.Instance;

        public void OpenBank(Interactable at) => OpenWindow(new BankWindow(), at);
        public void OpenShop(ShopKeeper keeper) => OpenWindow(new ShopWindow(keeper.Shop), keeper);
        /// The make window at a station; `prefer`: an item used on it, to show only what's made with that.
        public void OpenMaking(StationObject station, string prefer) => OpenWindow(new MakeWindow(station, prefer), station);

        /// The make window for things made in your hands from the inventory (fletching).
        public void OpenMakingInHand(List<RecipeDef> recipes) => OpenWindow(new MakeWindow(null, null, recipes), null);

        public void OpenWindow(Window window, Interactable at)
        {
            window.At = at;
            Window = window;
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
            return (menu.IsOpen && menu.Rect.Contains(m)) || PanelRect.Contains(m) || ChatRect.Contains(m) || BarRect.Contains(m)
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
            Gui.Shadowed(new Rect(W - 300f, LogOutRect.yMax + 4f, 290f, 20f), "<color=#c0c0c8>" + text + "</color>", Gui.RightSmall);
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
            Gui.MakeStyles();
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
            Panel.Draw(this, PanelRect);
            Window?.Draw(this, WindowRect);
            if (G.Player.Mode == ViewMode.FirstPerson)
                overlays.DrawCrosshair(this);
            minimap.Draw(this);
            DrawLogOut();
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
            Gui.Fill(r, new Color(0.24f, 0.2f, 0.15f, 0.96f));
            Gui.Fill(new Rect(r.x, r.y, r.width, 30f), new Color(0.16f, 0.13f, 0.09f, 1f));
            Gui.Shadowed(new Rect(r.x + Gui.Pad, r.y + 5f, 300f, 22f), "<b>" + title + "</b>", Gui.Title);
            if (GUI.Button(new Rect(r.xMax - 30f, r.y + 4f, 24f, 22f), "X"))
                hud.CloseWindow();
        }
    }
}
