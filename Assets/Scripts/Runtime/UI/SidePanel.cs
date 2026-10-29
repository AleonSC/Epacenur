using UnityEngine;

namespace Epacenur.Runtime
{
    /// One tab of the side panel (inventory, equipment, skills...).
    public abstract class PanelTab
    {
        protected static Game G => Game.Instance;
        public abstract string Name { get; }
        public abstract void Draw(Hud hud, Rect body);
    }

    /// The tabbed panel at the bottom right. To add a tab: subclass PanelTab and add it to Tabs.
    public sealed class SidePanel
    {
        const float TabH = 26f, PouchH = InventoryTab.PouchHeight;
        public const float Width = Gui.Pad * 2 + Gui.Slot * 4 + Gui.Gap * 3;
        public const float Height = Gui.Pad * 2 + TabH + 6f + Gui.Slot * 7 + Gui.Gap * 6 + Gui.Gap + PouchH;

        public readonly InventoryTab Inventory = new InventoryTab();
        public readonly PanelTab[] Tabs;
        int current;

        public SidePanel()
        {
            Tabs = new PanelTab[] { Inventory, new EquipmentTab(), new ToolBeltTab(), new PrayerTab(), new SkillsTab() };
        }

        public void ShowInventory()
        {
            current = 0;
            Inventory.ClearSelection();
        }

        public void Draw(Hud hud, Rect r)
        {
            Gui.Fill(r, new Color(0.24f, 0.2f, 0.15f, 0.92f));
            float tabW = (r.width - Gui.Pad * 2 - 3f * (Tabs.Length - 1)) / Tabs.Length;
            for (int i = 0; i < Tabs.Length; i++)
                if (GUI.Toggle(new Rect(r.x + Gui.Pad + i * (tabW + 3f), r.y + Gui.Pad, tabW, TabH), current == i, Tabs[i].Name, Gui.TabStyle))
                    current = i;
            var body = new Rect(r.x + Gui.Pad, r.y + Gui.Pad + TabH + 6f, r.width - Gui.Pad * 2, r.height - Gui.Pad * 2 - TabH - 6f);
            Tabs[current].Draw(hud, body);
        }
    }
}
