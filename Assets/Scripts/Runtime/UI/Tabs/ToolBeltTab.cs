using System.Collections.Generic;
using Epacenur.Core;
using UnityEngine;

namespace Epacenur.Runtime
{
    /// Every tool the belt can hold: the ones on it in full colour, the rest faded.
    public sealed class ToolBeltTab : PanelTab
    {
        public override string Name => "Tools";

        public override void Draw(Hud hud, Rect body)
        {
            var e = Event.current;
            Gui.Heading(body, "TOOL BELT");
            float top = body.y + 20f;
            string tooltip = null;
            int i = 0;
            foreach (var def in ContentDb.Current.Tools)
            {
                bool has = G.ToolBelt.Has(def.Id);
                var slot = new Rect(body.x + (i % 4) * (Gui.Slot + Gui.Gap), top + (i / 4) * (Gui.Slot + Gui.Gap), Gui.Slot, Gui.Slot);
                i++;
                Gui.Bevel(slot);
                Gui.DrawItemIcon(slot, def);
                if (!has)
                    Gui.Fill(new Rect(slot.x + 1f, slot.y + 1f, slot.width - 2f, slot.height - 2f), Gui.Locked);
                if (!slot.Contains(e.mousePosition))
                    continue;
                Gui.Highlight(slot, false);
                tooltip = def.Name + (has ? " (on your belt)" : ": not on your belt yet");
                if (e.type == EventType.MouseDown)
                {
                    var d = def;
                    if (e.button == 1)
                        hud.OpenMenu(new List<MenuOption> { new MenuOption("Examine", MenuOption.Item(d.Name), () => GameLog.Add(d.Examine)) }, false);
                    e.Use();
                }
            }
            int rows = (i + 3) / 4;
            GUI.Label(new Rect(body.x, top + rows * (Gui.Slot + Gui.Gap) + 4f, body.width, 80f),
                "<color=" + Gui.MutedHex + ">" + (G.ToolBelt.Owned.GetEnumerator().MoveNext()
                    ? "Tools here are used automatically, as if they were in your backpack. They stay on the belt for good."
                    : "Right-click a tool in your backpack and choose Add to tool belt. It's then used from here and frees the slot.") + "</color>",
                Gui.Rich);
            if (tooltip != null)
                Gui.Tooltip(new Rect(e.mousePosition.x - 210f, e.mousePosition.y + 16f, 210f, 20f), tooltip, Gui.RightSmall);
        }
    }
}
