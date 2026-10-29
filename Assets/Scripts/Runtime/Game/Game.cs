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

        public static void Add(string message)
        {
            Messages.Add(message);
            if (Messages.Count > Keep)
                Messages.RemoveAt(0);
        }
    }

    /// Sets the whole game up when Play is pressed (there are no scenes or prefabs to wire together),
    /// holds the player's skills and inventory, and runs the game clock: a tick every 0.6 s, as in RuneScape.
    public sealed class Game : MonoBehaviour
    {
        public static Game Instance { get; private set; }

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
        public CombatStyle Style = CombatStyle.Accurate;
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

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (booted)
                return;
            booted = true;
            var go = new GameObject("Game");
            DontDestroyOnLoad(go);
            go.AddComponent<Game>();
        }

        void Awake()
        {
            Instance = this;
            Application.targetFrameRate = 144;
            Modules.Load();  // every item, skill, monster... (Assets/Scripts/Content)
            Camera cam = SetUpScene();

            World = World.Create();
            Player = PlayerController.Create(World.Spawn);
            Rig = CameraRig.Create(cam, Player);
            Hud = gameObject.AddComponent<Hud>();

            Skills.XpGained += (id, tenths) => Hud.AddXpDrop(id, tenths);
            Skills.LevelledUp += OnLevelUp;
            Health = new Health(Skills.GetLevel(SkillIds.Hitpoints));
            Prayer = new PrayerBook(Skills.GetLevel(SkillIds.Prayer));

            if (SaveGame.Load(this))
            {
                GameLog.Add("Welcome back to Epacenur.");
            }
            else
            {
                Inventory.Add(ItemIds.Coins, 100);  // pocket money for the shop
                GiveStartingTools();
                GameLog.Add("Welcome to Epacenur.");
            }
            GameLog.Add("The bank, general store and chapel are round the town square. V switches to first person.");
            GameLog.Add("F1 opens the dev tools, with a teleport to the dev plot for testing skills.");
            nextSave = Time.time + 15f;
        }

        static Camera SetUpScene()
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
            RenderSettings.fogStartDistance = 45f;
            RenderSettings.fogEndDistance = 110f;
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
            {
                GameLog.Add("Oh dear, you are dead!");
                Health.Set(Health.Max);
                Prayer.DeactivateAll();
                Prayer.Recharge();
                Player.Teleport(World.Spawn);
            }
        }

        void Update()
        {
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

        void OnApplicationQuit() => SaveGame.Save(this);
    }
}
