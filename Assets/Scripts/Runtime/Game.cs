using System.Collections.Generic;
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

    /// Sets the whole game up when Play is pressed (there are no scenes or prefabs to wire together)
    /// and holds the player's skills and inventory.
    public sealed class Game : MonoBehaviour
    {
        public static Game Instance { get; private set; }

        public readonly SkillSet Skills = new SkillSet();
        public readonly Inventory Inventory = new Inventory();
        public readonly Bank Bank = new Bank();
        public readonly Equipment Equipment = new Equipment();
        public readonly ToolBelt ToolBelt = new ToolBelt();
        /// Your hitpoints (the maximum follows the Hitpoints level).
        public Health Health { get; private set; }
        /// Whether a tool is to hand: on the tool belt or in the backpack.
        public bool HasTool(int itemId) => ToolBelt.HasTool(ToolBelt, Inventory, itemId);
        public CombatStyle Style = CombatStyle.Accurate;
        public readonly System.Random Rng = new System.Random();
        public World World { get; private set; }
        public PlayerController Player { get; private set; }
        public CameraRig Rig { get; private set; }
        public Hud Hud { get; private set; }

        float nextSave;
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
            Camera cam = SetUpScene();

            World = World.Create();
            Player = PlayerController.Create(World.Spawn);
            Rig = CameraRig.Create(cam, Player);
            Hud = gameObject.AddComponent<Hud>();

            Skills.XpGained += (id, tenths) => Hud.AddXpDrop(id, tenths);
            Skills.LevelledUp += OnLevelUp;
            Health = new Health(Skills.GetLevel(SkillId.Hitpoints));

            if (SaveGame.Load(this))
            {
                GameLog.Add("Welcome back to the Epacenur dev area.");
            }
            else
            {
                Inventory.Add(ItemDefs.Coins.Id, 100);  // pocket money for the shop
                ToolBelt.Add(ItemDefs.BronzeHatchet.Id); // so you can chop straight away
                GameLog.Add("Welcome to the Epacenur dev area.");
            }
            GameLog.Add("Click a tree to chop it. V switches to first person, F1 opens the dev tools.");
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

        void OnLevelUp(SkillId id, int level)
        {
            string name = SkillDefs.Get(id).Name;
            GameLog.Add("Congratulations, you just advanced a " + name + " level. Your " + name + " level is now " + level + ".");
            Hud.ShowLevelUp(id, level);
        }

        float regenTimer;

        /// Hurts the player; at 0 you die and wake up at spawn with full health.
        public void HurtPlayer(int damage)
        {
            Health.Damage(damage);
            Player.ShowDamage(damage);
            if (Health.IsDead)
            {
                GameLog.Add("Oh dear, you are dead!");
                Health.Set(Health.Max);
                Player.Teleport(World.Spawn);
            }
        }

        void Update()
        {
            // Max health follows the Hitpoints level; lost health comes back a point a minute.
            Health.SetMax(Skills.GetLevel(SkillId.Hitpoints));
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

        void OnApplicationQuit() => SaveGame.Save(this);
    }
}
