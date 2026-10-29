using System;
using Epacenur.Core;
using UnityEngine;

namespace Epacenur.Runtime
{
    /// The main menu: pick a character (or make one) and a world (or make one, from a seed), then Play.
    /// A character keeps their skills, items and bank in every world; each world remembers where they stood.
    public sealed class MainMenu : MonoBehaviour
    {
        static MainMenu instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => instance = null;

        public static void Show()
        {
            if (instance == null)
                instance = new GameObject("Main menu").AddComponent<MainMenu>();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        CharacterInfo character;
        WorldInfo world;
        string newCharacter = "", newWorld = "", newSeed = "", message;
        object confirmDelete;  // the character or world awaiting a second click on Delete
        Vector2 characterScroll, worldScroll;
        float scale = 1f;

        float W => Screen.width / scale;
        float H => Screen.height / scale;

        void Start()
        {
            Profiles.GetLast(out character, out world);
            if (character == null && Profiles.Characters.Count > 0)
                character = Profiles.Characters[0];
            if (world == null)
                world = Profiles.Worlds[0];
            newSeed = RandomSeed();
        }

        static string RandomSeed() => UnityEngine.Random.Range(1, 1000000).ToString();

        void Update()
        {
            scale = Mathf.Clamp(Screen.height / 760f, 0.75f, 3f);
            if (Input.GetKeyDown(KeyCode.Return) && character != null && world != null)
                Play();
        }

        void OnGUI()
        {
            Gui.MakeStyles();
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            Gui.Fill(new Rect(0f, 0f, W, H), new Color(0.13f, 0.11f, 0.08f, 1f));
            // A strip of grass and sky for a backdrop.
            Gui.Fill(new Rect(0f, 0f, W, H * 0.3f), new Color(0.38f, 0.55f, 0.72f, 1f));
            Gui.Fill(new Rect(0f, H * 0.3f, W, 6f), new Color(0.3f, 0.48f, 0.22f, 1f));

            var title = new Rect(0f, 30f, W, 60f);
            Gui.Shadowed(title, "<size=46><b><color=#ffd966>Epacenur</color></b></size>", Gui.Centered);

            float panelW = Mathf.Min(380f, (W - 60f) / 2f), panelH = Mathf.Min(430f, H - 260f);
            float left = W / 2f - panelW - 10f, top = H * 0.3f + 30f;
            DrawCharacters(new Rect(left, top, panelW, panelH));
            DrawWorlds(new Rect(W / 2f + 10f, top, panelW, panelH));

            var play = new Rect(W / 2f - 110f, top + panelH + 20f, 220f, 44f);
            GUI.enabled = character != null && world != null;
            if (GUI.Button(play, "<size=20><b>Play</b></size>"))
                Play();
            GUI.enabled = true;
            string who = character == null ? "Make a character to play." : world == null ? "Pick a world." : character.name + " in " + world.name;
            Gui.Shadowed(new Rect(0f, play.yMax + 6f, W, 20f), who, Gui.Centered);
            if (message != null)
                Gui.Shadowed(new Rect(0f, play.yMax + 26f, W, 20f), "<color=#ff8060>" + message + "</color>", Gui.Centered);
        }

        static void Panel(Rect r, string heading)
        {
            Gui.Fill(r, new Color(0.24f, 0.2f, 0.15f, 0.96f));
            Gui.Fill(new Rect(r.x, r.y, r.width, 30f), new Color(0.16f, 0.13f, 0.09f, 1f));
            Gui.Shadowed(new Rect(r.x + 10f, r.y + 5f, r.width, 22f), "<b>" + heading + "</b>", Gui.Title);
        }

        /// A row in a list: selected rows are highlighted. Returns true when clicked.
        static bool Row(Rect r, bool selected, string text, string detail)
        {
            Gui.Bevel(r);
            if (selected)
                Gui.Fill(new Rect(r.x + 1f, r.y + 1f, r.width - 2f, r.height - 2f), new Color(1f, 0.85f, 0.35f, 0.25f));
            Gui.Shadowed(new Rect(r.x + 8f, r.y + 3f, r.width - 16f, 20f), "<b>" + text + "</b>", Gui.Label);
            GUI.Label(new Rect(r.x + 8f, r.y + 21f, r.width - 16f, 18f), detail, Gui.Small);
            var e = Event.current;
            if (e.type == EventType.MouseDown && r.Contains(e.mousePosition))
            {
                e.Use();
                return true;
            }
            return false;
        }

        static string When(long ticks)
        {
            if (ticks == 0)
                return "never played";
            var span = DateTime.UtcNow - new DateTime(ticks, DateTimeKind.Utc);
            if (span.TotalMinutes < 1) return "played just now";
            if (span.TotalHours < 1) return "played " + (int)span.TotalMinutes + " min ago";
            if (span.TotalDays < 1) return "played " + (int)span.TotalHours + " h ago";
            return "played " + (int)span.TotalDays + " days ago";
        }

        void DrawCharacters(Rect r)
        {
            Panel(r, "Characters");
            var list = Profiles.Characters;
            var view = new Rect(r.x + 10f, r.y + 38f, r.width - 20f, r.height - 38f - 92f);
            var content = new Rect(0f, 0f, view.width - 18f, Mathf.Max(view.height - 4f, list.Count * 48f));
            characterScroll = GUI.BeginScrollView(view, characterScroll, content);
            for (int i = 0; i < list.Count; i++)
            {
                var c = list[i];
                string detail = "Total level " + (c.totalLevel > 0 ? c.totalLevel.ToString() : "-") + "   Combat " + c.combatLevel + "   " + When(c.lastPlayed);
                if (Row(new Rect(0f, i * 48f, content.width, 44f), c == character, c.name, detail))
                {
                    character = c;
                    confirmDelete = null;
                }
            }
            if (list.Count == 0)
                GUI.Label(new Rect(4f, 4f, content.width - 8f, 60f), "No characters yet. Give your first one a name below.", Gui.Rich);
            GUI.EndScrollView();

            float y = r.yMax - 84f;
            GUI.Label(new Rect(r.x + 10f, y, 120f, 22f), "New character:", Gui.Small);
            newCharacter = GUI.TextField(new Rect(r.x + 110f, y, r.width - 200f, 22f), newCharacter, 12);
            if (GUI.Button(new Rect(r.xMax - 84f, y, 74f, 22f), "Create"))
            {
                message = Profiles.NameProblem(newCharacter, true);
                if (message == null)
                {
                    character = Profiles.CreateCharacter(newCharacter);
                    newCharacter = "";
                }
            }
            DeleteButton(new Rect(r.x + 10f, y + 32f, r.width - 20f, 22f), character, character == null ? null : character.name,
                () => { Profiles.DeleteCharacter(character); character = Profiles.Characters.Count > 0 ? Profiles.Characters[0] : null; });
        }

        void DrawWorlds(Rect r)
        {
            Panel(r, "Worlds");
            var list = Profiles.Worlds;
            var view = new Rect(r.x + 10f, r.y + 38f, r.width - 20f, r.height - 38f - 92f);
            var content = new Rect(0f, 0f, view.width - 18f, Mathf.Max(view.height - 4f, list.Count * 48f));
            worldScroll = GUI.BeginScrollView(view, worldScroll, content);
            for (int i = 0; i < list.Count; i++)
            {
                var w = list[i];
                if (Row(new Rect(0f, i * 48f, content.width, 44f), w == world, w.name, "Seed " + w.seed + "   " + When(w.lastPlayed)))
                {
                    world = w;
                    confirmDelete = null;
                }
            }
            GUI.EndScrollView();

            float y = r.yMax - 84f;
            GUI.Label(new Rect(r.x + 10f, y, 60f, 22f), "Name:", Gui.Small);
            newWorld = GUI.TextField(new Rect(r.x + 54f, y, r.width - 220f, 22f), newWorld, 12);
            GUI.Label(new Rect(r.xMax - 160f, y, 40f, 22f), "Seed:", Gui.Small);
            newSeed = GUI.TextField(new Rect(r.xMax - 124f, y, 60f, 22f), newSeed, 9);
            if (GUI.Button(new Rect(r.xMax - 60f, y, 50f, 22f), "Dice"))
                newSeed = RandomSeed();
            y += 30f;
            if (GUI.Button(new Rect(r.x + 10f, y, (r.width - 26f) / 2f, 22f), "Create world"))
            {
                message = Profiles.NameProblem(newWorld, false);
                if (message == null && !int.TryParse(newSeed, out _))
                    message = "The seed has to be a whole number.";
                if (message == null)
                {
                    world = Profiles.CreateWorld(newWorld, int.Parse(newSeed));
                    newWorld = "";
                    newSeed = RandomSeed();
                }
            }
            DeleteButton(new Rect(r.x + 16f + (r.width - 26f) / 2f, y, (r.width - 26f) / 2f, 22f), world,
                world == null ? null : world.name, () =>
                {
                    if (Profiles.Worlds.Count <= 1)
                    {
                        message = "There has to be at least one world.";
                        return;
                    }
                    Profiles.DeleteWorld(world);
                    world = Profiles.Worlds[0];
                });
        }

        /// Delete takes two clicks: the first asks to be sure.
        void DeleteButton(Rect r, object what, string name, Action delete)
        {
            GUI.enabled = what != null;
            bool sure = what != null && confirmDelete == what;
            if (GUI.Button(r, sure ? "Really delete " + name + "? Click again" : "Delete" + (name == null ? "" : " " + name)))
            {
                if (sure)
                {
                    delete();
                    confirmDelete = null;
                    message = null;
                }
                else
                {
                    confirmDelete = what;
                }
            }
            GUI.enabled = true;
        }

        void Play()
        {
            Profiles.SetLast(character, world);
            Destroy(gameObject);
            instance = null;
            Game.Begin(character, world);
        }
    }
}
