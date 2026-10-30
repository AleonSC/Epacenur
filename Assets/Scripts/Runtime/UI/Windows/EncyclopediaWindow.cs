using System.Collections.Generic;
using Epacenur.Content;
using Epacenur.Core;
using UnityEngine;

namespace Epacenur.Runtime
{
    /// The encyclopedia (J, or the button left of the minimap): type to search every page for any word; with
    /// nothing typed, browse by category. The page you pick shows on the right, with what you searched for
    /// picked out, and buttons to the pages it mentions. Back goes back.
    public sealed class EncyclopediaWindow : Window
    {
        const float ListW = 280f, RowH = 46f;
        const string SearchField = "EncyclopediaSearch";

        static Encyclopedia book;  // made once, from the content
        static Encyclopedia Book => book ?? (book = Encyclopedia.Build(ContentDb.Current, Guides.All));

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => book = null;

        string query, searched;
        List<EncyclopediaHit> hits = new List<EncyclopediaHit>();
        string category;
        EncyclopediaEntry page;
        readonly Stack<EncyclopediaEntry> back = new Stack<EncyclopediaEntry>();
        Vector2 listScroll, pageScroll;
        bool focus = true;

        /// Opens on a search (or the start page, for null), showing the best match.
        public EncyclopediaWindow(string search = null)
        {
            Anywhere = true;
            query = search ?? "";
            if (query.Length == 0)
                page = Book.Get("guide:Getting started");
            else
            {
                Search();
                if (hits.Count > 0)
                    page = hits[0].Entry;
            }
        }

        public override Rect Place(Hud hud)
        {
            float w = Mathf.Min(hud.W - 40f, 820f), h = Mathf.Min(hud.H - 120f, 560f);
            return new Rect((hud.W - w) / 2f, Mathf.Max(10f, (hud.H - h) / 2f - 30f), w, h);
        }

        void Search()
        {
            searched = query;
            hits = Book.Search(query);
            listScroll = Vector2.zero;
        }

        void Show(EncyclopediaEntry e)
        {
            if (e == null || e == page)
                return;
            if (page != null)
                back.Push(page);
            page = e;
            pageScroll = Vector2.zero;
            Sfx.Play(Sound.Click, 0.4f);
        }

        public override void Draw(Hud hud, Rect r)
        {
            Frame(hud, r, "Encyclopedia");
            Gui.Shadowed(new Rect(r.x + r.width - 260f, r.y + 8f, 220f, 20f), "<color=" + Gui.MutedHex + ">" + Book.Entries.Count + " pages</color>", Gui.RightSmall);

            // The search box: the list follows what's typed.
            var field = new Rect(r.x + Gui.Pad + 60f, r.y + 38f, ListW - 60f, 24f);
            GUI.Label(new Rect(r.x + Gui.Pad, r.y + 41f, 60f, 20f), "Search:", Gui.Small);
            GUI.SetNextControlName(SearchField);
            query = GUI.TextField(field, query ?? "", 40);
            if (focus)
            {
                GUI.FocusControl(SearchField);
                focus = false;
            }
            if (query != searched)
                Search();
            if (back.Count > 0 && GUI.Button(new Rect(field.xMax + 10f, field.y + 1f, 64f, 22f), "< Back"))
            {
                page = back.Pop();
                pageScroll = Vector2.zero;
            }

            var list = new Rect(r.x + Gui.Pad, r.y + 70f, ListW, r.height - 80f);
            var view = new Rect(list.xMax + 10f, r.y + 70f, r.xMax - list.xMax - 10f - Gui.Pad, r.height - 80f);
            Gui.Fill(new Rect(list.xMax + 4f, list.y, 1f, list.height), Gui.TrimDim);
            DrawList(list);
            DrawPage(view);
        }

        // ---- The list: search results, a category's pages, or the categories ----

        void DrawList(Rect r)
        {
            var e = Event.current;
            bool searching = query.Trim().Length > 0;
            int count = searching ? hits.Count : category != null ? Book.InCategory(category).Count + 1 : Encyclopedia.Categories.Length;
            var content = new Rect(0f, 0f, r.width - 14f, Mathf.Max(r.height - 2f, count * (RowH + 3f) + (searching && hits.Count == 0 ? 40f : 0f)));
            listScroll = GUI.BeginScrollView(r, listScroll, content);
            EncyclopediaEntry pick = null;
            if (searching)
            {
                if (hits.Count == 0)
                    GUI.Label(new Rect(4f, 4f, content.width - 8f, 40f), "<color=" + Gui.MutedHex + ">Nothing mentions \"" + query.Trim() + "\".</color>", Gui.Rich);
                for (int i = 0; i < hits.Count; i++)
                {
                    var row = new Rect(0f, i * (RowH + 3f), content.width, RowH);
                    if (Row(row, hits[i].Entry, hits[i].Snippet, e))
                        pick = hits[i].Entry;
                }
            }
            else if (category != null)
            {
                var pages = Book.InCategory(category);
                if (Row(new Rect(0f, 0f, content.width, RowH), null, "All categories", e))
                {
                    category = null;
                    listScroll = Vector2.zero;
                }
                for (int i = 0; i < pages.Count; i++)
                    if (Row(new Rect(0f, (i + 1) * (RowH + 3f), content.width, RowH), pages[i], null, e))
                        pick = pages[i];
            }
            else
            {
                for (int i = 0; i < Encyclopedia.Categories.Length; i++)
                {
                    string c = Encyclopedia.Categories[i];
                    var row = new Rect(0f, i * (RowH + 3f), content.width, RowH);
                    Gui.Bevel(row);
                    if (row.Contains(e.mousePosition))
                        Gui.Highlight(row, false);
                    Gui.Shadowed(new Rect(row.x + 10f, row.y + 6f, row.width - 20f, 20f), "<b>" + Plural(c) + "</b>", Gui.Label);
                    GUI.Label(new Rect(row.x + 10f, row.y + 24f, row.width - 20f, 18f), "<color=" + Gui.MutedHex + ">" + Book.InCategory(c).Count + " pages</color>", Gui.Small);
                    if (e.type == EventType.MouseDown && row.Contains(e.mousePosition))
                    {
                        category = c;
                        listScroll = Vector2.zero;
                        Sfx.Play(Sound.Click, 0.4f);
                        e.Use();
                    }
                }
            }
            GUI.EndScrollView();
            if (pick != null)
                Show(pick);
        }

        static string Plural(string c) => c == "Building" ? "Building pieces" : c == "Gathering" ? "Things to gather" : c + "s";

        /// A page in the list (its icon, title, category and a line); null page: a plain button. True when clicked.
        bool Row(Rect row, EncyclopediaEntry entry, string line, Event e)
        {
            Gui.Bevel(row);
            if (row.Contains(e.mousePosition) || (entry != null && entry == page))
                Gui.Highlight(row, entry != null && entry == page);
            float x = row.x + 8f;
            if (entry?.Icon != null)
            {
                Gui.DrawIcon(new Rect(row.x + 4f, row.y + 4f, 38f, 38f), entry.Icon);
                x = row.x + 46f;
            }
            if (entry != null)
            {
                Gui.Shadowed(new Rect(x, row.y + 3f, row.xMax - x - 4f, 20f), "<b>" + entry.Title + "</b>", Gui.Label);
                Gui.Shadowed(new Rect(x, row.y + 4f, row.xMax - x - 6f, 20f), "<color=" + Gui.MutedHex + ">" + entry.Category + "</color>", Gui.RightSmall);
            }
            else
                Gui.Shadowed(new Rect(x, row.y + 12f, row.xMax - x - 4f, 20f), "<b>< " + line + "</b>", Gui.Label);
            if (entry != null && line != null)
                GUI.Label(new Rect(x, row.y + 23f, row.xMax - x - 4f, 20f), "<color=" + Gui.MutedHex + ">" + Mark(Clip(line, 60)) + "</color>", Gui.Small);
            if (e.type == EventType.MouseDown && row.Contains(e.mousePosition))
            {
                e.Use();
                return true;
            }
            return false;
        }

        static string Clip(string s, int n) => s.Length <= n ? s : s.Substring(0, n) + "...";

        // ---- The page ----

        void DrawPage(Rect r)
        {
            if (page == null)
                return;
            var e = Event.current;
            float w = r.width - 16f;
            // Work out the height first: the lines wrap.
            float h = 52f;
            foreach (var line in page.Lines)
                h += LineHeight(line, w);
            h += page.Links.Count > 0 ? 34f + Mathf.Ceil(page.Links.Count / 3f) * 26f : 0f;
            var content = new Rect(0f, 0f, w, Mathf.Max(r.height - 2f, h));
            pageScroll = GUI.BeginScrollView(r, pageScroll, content);
            float x = 0f;
            if (page.Icon != null)
            {
                Gui.Bevel(new Rect(0f, 0f, 44f, 44f));
                Gui.DrawIcon(new Rect(2f, 2f, 40f, 40f), page.Icon);
                x = 52f;
            }
            Gui.Shadowed(new Rect(x, 0f, w - x, 26f), "<size=18><b><color=" + Gui.GoldHex + ">" + page.Title + "</color></b></size>", Gui.Label);
            GUI.Label(new Rect(x, 25f, w - x, 18f), "<color=" + Gui.MutedHex + ">" + page.Category + "</color>", Gui.Small);
            float y = 52f;
            foreach (var line in page.Lines)
            {
                float lh = LineHeight(line, w);
                if (line.StartsWith("## "))
                {
                    Gui.Shadowed(new Rect(0f, y + 6f, w, 20f), "<b><color=" + Gui.GoldHex + ">" + line.Substring(3) + "</color></b>", Gui.Label);
                    Gui.Fill(new Rect(0f, y + 25f, w, 1f), Gui.TrimDim);
                }
                else
                    GUI.Label(new Rect(0f, y, w, lh), Mark(line), Gui.Rich);
                y += lh;
            }
            if (page.Links.Count > 0)
            {
                y += 8f;
                Gui.Shadowed(new Rect(0f, y, w, 20f), "<b><color=" + Gui.GoldHex + ">See also</color></b>", Gui.Label);
                y += 24f;
                float bw = (w - 8f) / 3f;
                EncyclopediaEntry go = null;
                for (int i = 0; i < page.Links.Count; i++)
                {
                    var target = Book.Get(page.Links[i]);
                    if (target == null)
                        continue;
                    if (GUI.Button(new Rect((i % 3) * (bw + 4f), y + (i / 3) * 26f, bw, 23f), Clip(target.Title, 24)))
                        go = target;
                }
                if (go != null)
                    Show(go);
            }
            GUI.EndScrollView();
        }

        static float LineHeight(string line, float w) => line.StartsWith("## ")
            ? 30f
            : Gui.Rich.CalcHeight(new GUIContent(line), w) + 4f;

        /// Picks out what was searched for (each word), in gold.
        string Mark(string text)
        {
            var words = (searched ?? "").Trim().ToLowerInvariant().Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0)
                return text;
            var lower = text.ToLowerInvariant();
            var marked = new System.Text.StringBuilder();
            int i = 0;
            while (i < text.Length)
            {
                int best = -1, len = 0;
                foreach (var w in words)
                {
                    int at = lower.IndexOf(w, i, System.StringComparison.Ordinal);
                    if (at >= 0 && (best < 0 || at < best))
                    {
                        best = at;
                        len = w.Length;
                    }
                }
                if (best < 0)
                {
                    marked.Append(text, i, text.Length - i);
                    break;
                }
                marked.Append(text, i, best - i).Append("<color=" + Gui.GoldHex + "><b>").Append(text, best, len).Append("</b></color>");
                i = best + len;
            }
            return marked.ToString();
        }
    }
}
