using System.Collections.Generic;
using Epacenur.Content;
using Epacenur.Core;
using UnityEngine;

namespace Epacenur.Runtime
{
    /// Chat-box messages.
    public static class GameLog
    {
        public const int Keep = 50;
        public static readonly List<string> Messages = new List<string>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Messages.Clear();

        public static void Clear() => Messages.Clear();

        public static void Add(string message)
        {
            Messages.Add(message);
            if (Messages.Count > Keep)
                Messages.RemoveAt(0);
        }
    }

    /// A play session: one character in one world, started from the main menu (MainMenu). It sets the game
    /// up (there are no scenes or prefabs to wire together), holds the character's skills and inventory, and
    /// runs the game clock: a tick every 0.6 s, as in RuneScape. Logging out takes it all down again.
    public sealed class Game : MonoBehaviour
    {
        public static Game Instance { get; private set; }

        /// Who's playing, and where.
        public CharacterInfo Character { get; private set; }
        public WorldInfo WorldInfo { get; private set; }
        static CharacterInfo startCharacter;
        static WorldInfo startWorld;

        /// Fired every game tick (0.6 s), after the player's activity has moved on.
        public event System.Action Ticked;
        public long TickCount { get; private set; }

        public readonly SkillSet Skills = new SkillSet();
        public readonly Inventory Inventory = new Inventory();
        public readonly Bank Bank = new Bank();
        public readonly Equipment Equipment = new Equipment();
        public readonly ToolBelt ToolBelt = new ToolBelt();
        /// Your hitpoints (the maximum follows the Hitpoints level).
        public Health Health { get; private set; }
        /// Prayer points and active prayers (the maximum follows the Prayer level).
        public PrayerBook Prayer { get; private set; }
        /// The chosen attack style, by its place in the weapon's list (the first is used if a new weapon has fewer).
        public int StyleIndex;

        /// The attack style in use with the wielded weapon (or unarmed).
        public WeaponStyle CurrentStyle
        {
            get
            {
                var kind = ContentDb.Current.WeaponKind(Equipment.WeaponKind) ?? ContentDb.Current.WeaponKind(WeaponKindIds.Unarmed);
                return kind.Styles[StyleIndex >= 0 && StyleIndex < kind.Styles.Length ? StyleIndex : 0];
            }
        }
        public readonly System.Random Rng = new System.Random();
        public World World { get; private set; }
        public PlayerController Player { get; private set; }
        public CameraRig Rig { get; private set; }
        public Hud Hud { get; private set; }

        float nextSave, tickTimer;
        static bool booted;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            booted = false;
            Instance = null;
        }

        /// When Play is pressed in Unity: the content is loaded and the main menu opens.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (booted)
                return;
            booted = true;
            Application.targetFrameRate = 144;
            Modules.Load();  // every item, skill, monster... (Assets/Scripts/Content)
            SetUpScene();
            MainMenu.Show();
        }

        /// Starts playing a character in a world.
        public static void Begin(CharacterInfo character, WorldInfo world)
        {
            startCharacter = character;
            startWorld = world;
            new GameObject("Game").AddComponent<Game>();
        }

        void Awake()
        {
            Instance = this;
            Character = startCharacter;
            WorldInfo = startWorld;
            Camera cam = SetUpScene();

            World = World.Create(WorldInfo.seed, MapId.Overworld, SaveGame.LoadPiles(WorldInfo));
            Player = PlayerController.Create(World.Spawn);
            Rig = CameraRig.Create(cam, Player);
            Hud = gameObject.AddComponent<Hud>();

            Skills.XpGained += (id, tenths) => Hud.AddXpDrop(id, tenths);
            Skills.LevelledUp += OnLevelUp;
            Health = new Health(Skills.GetLevel(SkillIds.Hitpoints));
            Prayer = new PrayerBook(Skills.GetLevel(SkillIds.Prayer));

            GameLog.Clear();
            if (SaveGame.Load(this))
            {
                GameLog.Add("Welcome back to " + WorldInfo.name + ", " + Character.name + ".");
            }
            else
            {
                Inventory.Add(ItemIds.Coins, 100);  // pocket money for the shop
                GiveStartingTools();
                GameLog.Add("Welcome to " + WorldInfo.name + ", " + Character.name + ".");
            }
            GameLog.Add("The bank, general store and chapel are round the town square. V switches to first person.");
            GameLog.Add("F1 opens the dev tools, with a teleport to the dev plot for testing skills.");
            nextSave = Time.time + 15f;
        }

        /// Saves and takes the session down, back to the main menu.
        public void LogOut()
        {
            SaveGame.Save(this);
            loggedOut = true;
            Destroy(World.gameObject);
            Destroy(Player.gameObject);
            Destroy(Rig);
            Destroy(gameObject);
            Instance = null;
            MainMenu.Show();
        }

        bool loggedOut;

        /// The camera, the sun and the sky, shared by the menu and the game.
        public static Camera SetUpScene()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = camGo.AddComponent<Camera>();
            }
            if (FindFirstObjectByType<AudioListener>() == null)
                cam.gameObject.AddComponent<AudioListener>();
            var sky = new Color(0.62f, 0.78f, 0.92f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = sky;

            Light sun = null;
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional)
                    sun = l;
            if (sun == null)
            {
                sun = new GameObject("Sun").AddComponent<Light>();
                sun.type = LightType.Directional;
            }
            sun.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
            sun.intensity = 1.1f;
            sun.color = new Color(1f, 0.96f, 0.88f);
            sun.shadows = LightShadows.Soft;

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.5f, 0.53f, 0.58f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = sky;
            RenderSettings.fogStartDistance = 36f;
            RenderSettings.fogEndDistance = 78f;  // short of where the loaded chunks end (at least 80 tiles out)
            return cam;
        }

        void OnLevelUp(string id, int level)
        {
            string name = ContentDb.Current.Skill(id).Name;
            GameLog.Add("Congratulations, you just advanced a " + name + " level. Your " + name + " level is now " + level + ".");
            Hud.ShowLevelUp(id, level);
        }

        float regenTimer;

        /// A new character's tool belt: hatchet, pickaxe and hammer, so you can chop, mine and smith straight away.
        public static readonly string[] StartingTools = { WoodcuttingModule.BronzeHatchet, MiningModule.BronzePickaxe, SmithingModule.Hammer };

        public void GiveStartingTools()
        {
            foreach (var id in StartingTools)
                ToolBelt.Add(id);
        }

        /// Turns a prayer on or off from the Prayer tab.
        public void TogglePrayer(PrayerId id)
        {
            string problem = Prayer.Toggle(id, Skills.GetLevel(SkillIds.Prayer));
            if (problem != null)
                GameLog.Add(problem);
        }

        /// Hurts the player; at 0 you die and wake up at spawn with full health.
        public void HurtPlayer(int damage)
        {
            Health.Damage(damage);
            Player.ShowDamage(damage);
            if (Health.IsDead)
                Die();
        }

        /// Your gravestone, if you have one (in this world or another), or null.
        public Grave Grave { get; set; }

        /// Dying: you keep your 3 most valuable items; the rest go into a gravestone where you fell, and you
        /// wake up in the nearest town (or at the dev plot's start) with full health.
        void Die()
        {
            var tile = Player.CurrentTile;
            var map = World.Map;
            var kept = new List<ItemStack>();
            var lost = new List<ItemStack>();
            Death.Split(Inventory, Equipment, Death.KeptItems, kept, lost);
            foreach (var k in kept)
                Inventory.Add(k.ItemId, k.Count);
            GameLog.Add("Oh dear, you are dead!");
            if (lost.Count > 0)
            {
                CollapseGrave();  // an older gravestone gives way to the new one
                Grave = new Grave { world = WorldInfo.id, map = (int)map, x = tile.X, z = tile.Z, secondsLeft = Death.GraveSeconds };
                foreach (var l in lost)
                {
                    Grave.items.Add(l.ItemId);
                    Grave.counts.Add(l.Count);
                }
                GameLog.Add("You kept your " + Death.KeptItems + " most valuable items. The rest are in a gravestone where you died: get back to it within "
                    + Gravestone.Time(Grave) + ".");
            }
            Health.Set(Health.Max);
            Prayer.DeactivateAll();
            Prayer.Recharge();
            if (map == MapId.DevPlot)
                World.Travel(MapId.DevPlot, World.DevSpawn);
            else
                World.Travel(MapId.Overworld, World.NearestTown(tile));
        }

        /// "Reclaim items" at your gravestone: as much as fits goes back into your inventory.
        public void ReclaimGrave()
        {
            var g = Grave;
            if (g == null)
                return;
            int taken = 0;
            for (int i = g.items.Count - 1; i >= 0; i--)
            {
                int n = Inventory.Add(g.items[i], g.counts[i]);
                taken += n;
                g.counts[i] -= n;
                if (g.counts[i] <= 0)
                {
                    g.items.RemoveAt(i);
                    g.counts.RemoveAt(i);
                }
            }
            if (g.IsEmpty)
            {
                Grave = null;
                GameLog.Add("You take back all your items. The gravestone crumbles away.");
            }
            else
            {
                GameLog.Add(taken > 0 ? "You take back what you can carry; the rest waits in the gravestone." : "You don't have enough inventory space.");
            }
        }

        /// The gravestone's time is up (or you died again): its items are left on the ground where it stood.
        void CollapseGrave()
        {
            var g = Grave;
            if (g == null)
                return;
            Grave = null;
            if (g.world != WorldInfo.id)
                return;  // in another world: those items are lost
            for (int i = 0; i < g.items.Count; i++)
                World.PlacePile(g.items[i], g.counts[i], (MapId)g.map, g.Tile);
        }

        void Update()
        {
            // Your gravestone's time only runs while you're in its world.
            if (Grave != null && Grave.world == WorldInfo.id)
            {
                Grave.secondsLeft -= Time.deltaTime;
                if (Grave.secondsLeft <= 0f)
                {
                    CollapseGrave();
                    GameLog.Add("Your gravestone has collapsed. Your items are lying on the ground where you died.");
                }
            }
            Prayer.SetMax(Skills.GetLevel(SkillIds.Prayer));
            tickTimer += Time.deltaTime;
            while (tickTimer >= GameTime.TickSeconds)
            {
                tickTimer -= GameTime.TickSeconds;
                Tick();
            }

            // Max health follows the Hitpoints level; lost health comes back a point a minute.
            Health.SetMax(Skills.GetLevel(SkillIds.Hitpoints));
            if (Health.IsFull)
            {
                regenTimer = 0f;
            }
            else
            {
                regenTimer += Time.deltaTime;
                if (regenTimer >= Core.Health.RegenSeconds)
                {
                    regenTimer -= Core.Health.RegenSeconds;
                    Health.Heal(1);
                }
            }

            if (Time.time >= nextSave)
            {
                nextSave = Time.time + 15f;
                SaveGame.Save(this);
            }
        }

        /// One game tick: the player's activity moves on, and prayers drain.
        void Tick()
        {
            TickCount++;
            Player.OnTick();
            if (Prayer.Tick())
                GameLog.Add("You have run out of Prayer points; you can recharge at an altar.");
            Ticked?.Invoke();
        }

        void OnApplicationQuit()
        {
            if (!loggedOut)
                SaveGame.Save(this);
        }
    }
}
