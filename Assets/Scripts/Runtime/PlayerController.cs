using System.Collections.Generic;
using Epacenur.Core;
using UnityEngine;

namespace Epacenur.Runtime
{
    public enum ViewMode
    {
        Tile,         // top-down: click tiles to walk, click trees to chop them automatically; WASD also works
        FirstPerson,  // mouse look + WASD; every chop or attack is a click
    }

    /// What the first-person view shows in your hand.
    public enum HeldTool { None, Axe, Sword }

    /// The player: walking (tile paths or WASD), the view toggle, woodcutting, picking things up and fighting.
    public sealed partial class PlayerController : MonoBehaviour
    {
        public const float PathSpeed = 3.4f;        // tiles per second when walking a clicked path
        public const float WasdSpeed = 4.2f;
        public const float FirstPersonReach = 2.3f; // furthest a tree's centre can be to chop it in first person
        public const float EyeHeight = 1.6f;

        public ViewMode Mode { get; private set; } = ViewMode.Tile;
        /// What left-click would do, RuneScape style ("Chop down Tree / 2 more options"), for the HUD.
        public string HoverText { get; private set; } = "";
        /// First person: whether the crosshair is on something close enough to use (a tree, a bank booth, an NPC).
        public bool AimingAtTarget { get; private set; }
        public bool SwingReady => swingCooldown <= 0f;
        public bool IsChopping => chopping;
        public bool IsFighting => fighting;
        /// The monster you're walking to or fighting in tile mode (monsters wait for you rather than meet you).
        public Npc AttackTarget => attackTarget;
        float kneelUntil, buryCooldown;

        /// Kneel for a moment (praying at an altar).
        public void Kneel(float seconds) => kneelUntil = Time.time + seconds;
        public HeldTool FirstPersonTool { get; private set; } = HeldTool.None;
        /// Ability cooldowns, for the action bar.
        public readonly AbilityCooldowns Cooldowns = new AbilityCooldowns();
        /// 1 at the start of a first-person swing, falling to 0.
        public float SwingT { get; private set; }

        Tile CurrentTile => World.TileAt(transform.position);
        Game G => Game.Instance;

        CharacterController cc;
        Transform model, armPivot, armLeft, legLeft, legRight, cape, heldAxe, heldSword, heldShield, heldNet;
        Vector3 lastPosition;
        float walkPhase, speed;
        Renderer[] modelRenderers;
        GameObject hoverMarker, destMarker;

        readonly List<Tile> path = new List<Tile>();
        int pathIndex;
        TreeObject target;      // the tree being walked to or chopped (tile mode)
        Interactable targetObject;  // the bank booth, NPC... being walked to (tile mode)
        GroundItem targetItem;      // the ground item being walked to (tile mode)
        Npc attackTarget;           // the monster being walked to or fought (tile mode)
        Npc hoverNpc;               // the monster under the mouse (tile mode) or crosshair (first person)
        Npc lastTarget;             // the last monster you hit, for abilities when nothing else is targeted
        bool chopping, fighting;

        // A bleed (Dismember): hits still to land on a monster.
        sealed class Bleed { public Npc Npc; public int[] Hits; public int Next; public float NextTime; }
        readonly List<Bleed> bleeds = new List<Bleed>();
        float chopTimer, swingCooldown, fallSpeed, chopAnim, attackTimer, attackAnim, eatCooldown;

        /// Damage you've taken lately, for hitsplats over your head.
        public readonly List<Npc.Hitsplat> Hitsplats = new List<Npc.Hitsplat>();
        public float LastHurtTime { get; private set; } = -100f;

        public static PlayerController Create(Tile spawn)
        {
            var go = new GameObject("Player");
            go.layer = 2;  // Ignore Raycast: mouse clicks go through the player
            go.transform.position = World.TileCenter(spawn);
            var cc = go.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.3f;
            cc.center = new Vector3(0f, 0.9f, 0f);
            cc.stepOffset = 0.3f;
            cc.skinWidth = 0.03f;
            var p = go.AddComponent<PlayerController>();
            p.cc = cc;
            p.BuildModel();
            p.BuildMarkers();
            return p;
        }

        void BuildModel()
        {
            model = new GameObject("Model").transform;
            model.SetParent(transform, false);
            var skin = new Color(0.93f, 0.76f, 0.6f);
            var hair = new Color(0.33f, 0.2f, 0.1f);
            var tunic = new Color(0.17f, 0.3f, 0.62f);
            var trim = new Color(0.88f, 0.72f, 0.3f);
            var trousers = new Color(0.26f, 0.23f, 0.2f);
            var leather = new Color(0.3f, 0.18f, 0.08f);
            var boots = new Color(0.2f, 0.13f, 0.08f);
            var steel = new Color(0.66f, 0.68f, 0.73f);
            var dark = new Color(0.08f, 0.06f, 0.06f);

            // Legs, each swinging from the hip: thigh, shin and a boot with a turned-down cuff.
            legLeft = Limb("Left leg", new Vector3(-0.12f, 0.86f, 0f));
            legRight = Limb("Right leg", new Vector3(0.12f, 0.86f, 0f));
            foreach (var leg in new[] { legLeft, legRight })
            {
                Visuals.Prim(PrimitiveType.Capsule, leg, new Vector3(0f, -0.22f, 0f), new Vector3(0.18f, 0.24f, 0.18f), trousers);
                Visuals.Prim(PrimitiveType.Capsule, leg, new Vector3(0f, -0.54f, 0f), new Vector3(0.15f, 0.22f, 0.15f), trousers);
                Visuals.Prim(PrimitiveType.Cube, leg, new Vector3(0f, -0.79f, 0.04f), new Vector3(0.16f, 0.13f, 0.28f), boots);
                Visuals.Prim(PrimitiveType.Cylinder, leg, new Vector3(0f, -0.68f, 0f), new Vector3(0.19f, 0.04f, 0.19f), leather);
            }

            // Body: hips, tunic with a flared skirt and gold trim, belt and buckle.
            Visuals.Prim(PrimitiveType.Cube, model, new Vector3(0f, 0.9f, 0f), new Vector3(0.4f, 0.16f, 0.24f), trousers);
            Visuals.Prim(PrimitiveType.Cylinder, model, new Vector3(0f, 0.93f, 0f), new Vector3(0.5f, 0.09f, 0.34f), tunic);
            Visuals.Prim(PrimitiveType.Cylinder, model, new Vector3(0f, 0.85f, 0f), new Vector3(0.51f, 0.012f, 0.35f), trim);
            Visuals.Prim(PrimitiveType.Capsule, model, new Vector3(0f, 1.2f, 0f), new Vector3(0.5f, 0.38f, 0.32f), tunic);
            Visuals.Prim(PrimitiveType.Cube, model, new Vector3(0f, 1.24f, 0.155f), new Vector3(0.05f, 0.36f, 0.02f), trim);
            Visuals.Prim(PrimitiveType.Cube, model, new Vector3(0f, 1.42f, 0.14f), new Vector3(0.24f, 0.04f, 0.04f), trim);
            Visuals.Prim(PrimitiveType.Cylinder, model, new Vector3(0f, 1.01f, 0f), new Vector3(0.45f, 0.03f, 0.31f), leather);
            Visuals.Prim(PrimitiveType.Cube, model, new Vector3(0f, 1.01f, 0.16f), new Vector3(0.09f, 0.07f, 0.02f), trim);
            Visuals.Prim(PrimitiveType.Cube, model, new Vector3(0.14f, 0.97f, 0.14f), new Vector3(0.08f, 0.1f, 0.05f), leather); // pouch

            // Shoulder pads.
            Visuals.Prim(PrimitiveType.Sphere, model, new Vector3(-0.27f, 1.43f, 0f), new Vector3(0.22f, 0.14f, 0.24f), steel);
            Visuals.Prim(PrimitiveType.Sphere, model, new Vector3(0.27f, 1.43f, 0f), new Vector3(0.22f, 0.14f, 0.24f), steel);

            // Head: neck, face, ears, hair with a fringe, eyes, brows, nose and mouth.
            Visuals.Prim(PrimitiveType.Cylinder, model, new Vector3(0f, 1.51f, 0f), new Vector3(0.12f, 0.05f, 0.12f), skin);
            Visuals.Prim(PrimitiveType.Sphere, model, new Vector3(0f, 1.69f, 0f), new Vector3(0.32f, 0.36f, 0.32f), skin);
            Visuals.Prim(PrimitiveType.Sphere, model, new Vector3(-0.16f, 1.68f, 0f), new Vector3(0.05f, 0.09f, 0.06f), skin);
            Visuals.Prim(PrimitiveType.Sphere, model, new Vector3(0.16f, 1.68f, 0f), new Vector3(0.05f, 0.09f, 0.06f), skin);
            Visuals.Prim(PrimitiveType.Sphere, model, new Vector3(0f, 1.77f, -0.03f), new Vector3(0.35f, 0.28f, 0.35f), hair);
            Visuals.Prim(PrimitiveType.Cube, model, new Vector3(0f, 1.82f, 0.12f), new Vector3(0.27f, 0.07f, 0.08f), hair);
            Visuals.Prim(PrimitiveType.Sphere, model, new Vector3(0f, 1.63f, -0.12f), new Vector3(0.3f, 0.26f, 0.12f), hair);
            Visuals.Prim(PrimitiveType.Sphere, model, new Vector3(-0.065f, 1.7f, 0.145f), Vector3.one * 0.045f, dark);
            Visuals.Prim(PrimitiveType.Sphere, model, new Vector3(0.065f, 1.7f, 0.145f), Vector3.one * 0.045f, dark);
            Visuals.Prim(PrimitiveType.Cube, model, new Vector3(-0.065f, 1.745f, 0.15f), new Vector3(0.07f, 0.015f, 0.02f), hair);
            Visuals.Prim(PrimitiveType.Cube, model, new Vector3(0.065f, 1.745f, 0.15f), new Vector3(0.07f, 0.015f, 0.02f), hair);
            Visuals.Prim(PrimitiveType.Cube, model, new Vector3(0f, 1.66f, 0.165f), new Vector3(0.04f, 0.06f, 0.05f), skin * 0.92f);
            Visuals.Prim(PrimitiveType.Cube, model, new Vector3(0f, 1.6f, 0.15f), new Vector3(0.08f, 0.013f, 0.02f), new Color(0.55f, 0.2f, 0.2f));

            // Arms from the shoulders: sleeve, forearm, hand. The right one holds tools, the left the shield.
            armPivot = Limb("Right arm", new Vector3(0.33f, 1.38f, 0f));
            armLeft = Limb("Left arm", new Vector3(-0.33f, 1.38f, 0f));
            foreach (var arm in new[] { armPivot, armLeft })
            {
                Visuals.Prim(PrimitiveType.Capsule, arm, new Vector3(0f, -0.16f, 0f), new Vector3(0.14f, 0.18f, 0.14f), tunic);
                Visuals.Prim(PrimitiveType.Cylinder, arm, new Vector3(0f, -0.3f, 0f), new Vector3(0.15f, 0.015f, 0.15f), trim);
                Visuals.Prim(PrimitiveType.Capsule, arm, new Vector3(0f, -0.43f, 0f), new Vector3(0.12f, 0.15f, 0.12f), skin);
                Visuals.Prim(PrimitiveType.Sphere, arm, new Vector3(0f, -0.58f, 0.01f), Vector3.one * 0.11f, skin);
            }

            // A red cape with a gold hem, hanging from the shoulders; it flies back as you move.
            cape = Limb("Cape", new Vector3(0f, 1.46f, -0.17f));
            Visuals.Prim(PrimitiveType.Cube, cape, new Vector3(0f, -0.47f, 0f), new Vector3(0.5f, 0.94f, 0.03f), new Color(0.62f, 0.08f, 0.1f));
            Visuals.Prim(PrimitiveType.Cube, cape, new Vector3(0f, -0.93f, 0f), new Vector3(0.52f, 0.05f, 0.035f), trim);
            Visuals.Prim(PrimitiveType.Cube, cape, new Vector3(0f, 0f, 0.02f), new Vector3(0.46f, 0.05f, 0.05f), trim);

            heldAxe = Holder("Axe", armPivot, new Vector3(0f, -0.6f, 0.08f));
            BuildAxe(heldAxe);
            heldSword = Holder("Sword", armPivot, new Vector3(0f, -0.6f, 0.02f));
            ItemModels.BuildSword(heldSword, ItemDefs.BronzeSword);
            heldNet = Holder("Net", armPivot, new Vector3(0f, -0.6f, 0.02f));
            ItemModels.BuildNet(heldNet);
            heldShield = Holder("Shield", armLeft, new Vector3(-0.1f, -0.4f, 0.05f));
            ItemModels.BuildShield(heldShield, ItemDefs.WoodenShield);
            modelRenderers = model.GetComponentsInChildren<Renderer>(true);
        }

        Transform Limb(string name, Vector3 pivot)
        {
            var t = new GameObject(name).transform;
            t.SetParent(model, false);
            t.localPosition = pivot;
            return t;
        }

        static Transform Holder(string name, Transform parent, Vector3 at)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = at;
            return t;
        }

        /// Show the sword and shield you're wearing; the axe comes out to chop, the net to fish.
        void UpdateHeldItems()
        {
            bool sword = G.Equipment.IsWearing(EquipSlot.Weapon);
            heldNet.gameObject.SetActive(fishing);
            heldSword.gameObject.SetActive(sword && !chopping && !fishing);
            heldAxe.gameObject.SetActive(chopping);  // only out while chopping
            heldShield.gameObject.SetActive(G.Equipment.IsWearing(EquipSlot.Shield));
        }

        /// Walk cycle (legs, left arm and cape follow your speed), idle breathing, and kneeling to light fires.
        void AnimateBody(float dt)
        {
            Vector3 moved = transform.position - lastPosition;
            moved.y = 0f;
            lastPosition = transform.position;
            float measured = dt > 0f ? moved.magnitude / dt : 0f;
            speed = Mathf.Lerp(speed, Mathf.Min(measured, 6f), 1f - Mathf.Exp(-12f * dt));
            float stride = Mathf.Clamp01(speed / 3f);
            walkPhase += dt * speed * 3.4f;
            float swing = Mathf.Sin(walkPhase) * 38f * stride;

            float kneel = lighting || Time.time < kneelUntil ? 1f : 0f;
            legLeft.localRotation = Quaternion.Slerp(legLeft.localRotation, Quaternion.Euler(swing - kneel * 70f, 0f, 0f), 1f - Mathf.Exp(-20f * dt));
            legRight.localRotation = Quaternion.Slerp(legRight.localRotation, Quaternion.Euler(-swing + kneel * 10f, 0f, 0f), 1f - Mathf.Exp(-20f * dt));
            float leftArm = G.Equipment.IsWearing(EquipSlot.Shield) && (fighting || attackAnim > 0f) ? -50f : -swing * 0.8f;
            armLeft.localRotation = Quaternion.Slerp(armLeft.localRotation, Quaternion.Euler(leftArm, 0f, -4f), 1f - Mathf.Exp(-15f * dt));

            float bob = stride > 0.05f ? Mathf.Abs(Mathf.Sin(walkPhase)) * 0.04f * stride : Mathf.Sin(Time.time * 2f) * 0.008f;
            model.localPosition = new Vector3(0f, bob - kneel * 0.22f, 0f);
            model.localRotation = Quaternion.Euler(kneel * 12f, 0f, 0f);

            float flutter = Mathf.Sin(Time.time * 7f) * 4f * stride + Mathf.Sin(Time.time * 1.7f) * 1.5f;
            cape.localRotation = Quaternion.Slerp(cape.localRotation, Quaternion.Euler(6f + stride * 32f + flutter, 0f, 0f), 1f - Mathf.Exp(-8f * dt));
        }

        /// A small axe pointing forward along +z (shared with the first-person view).
        public static void BuildAxe(Transform parent)
        {
            Visuals.Prim(PrimitiveType.Cube, parent, new Vector3(0f, 0f, 0.25f), new Vector3(0.05f, 0.05f, 0.6f), new Color(0.45f, 0.3f, 0.16f));
            Visuals.Prim(PrimitiveType.Cube, parent, new Vector3(0f, 0.08f, 0.5f), new Vector3(0.03f, 0.2f, 0.14f), new Color(0.6f, 0.62f, 0.66f));
        }

        void BuildMarkers()
        {
            // Hovered tile: a thin yellow outline. Destination: a red square, like RuneScape's flag.
            hoverMarker = new GameObject("Hover tile");
            var c = new Color(1f, 0.95f, 0.4f);
            const float t = 0.05f, y = 0.03f;
            Visuals.Prim(PrimitiveType.Cube, hoverMarker.transform, new Vector3(0f, y, 0.5f), new Vector3(1f, 0.01f, t), c);
            Visuals.Prim(PrimitiveType.Cube, hoverMarker.transform, new Vector3(0f, y, -0.5f), new Vector3(1f, 0.01f, t), c);
            Visuals.Prim(PrimitiveType.Cube, hoverMarker.transform, new Vector3(0.5f, y, 0f), new Vector3(t, 0.01f, 1f), c);
            Visuals.Prim(PrimitiveType.Cube, hoverMarker.transform, new Vector3(-0.5f, y, 0f), new Vector3(t, 0.01f, 1f), c);
            destMarker = Visuals.Prim(PrimitiveType.Cube, null, Vector3.zero, new Vector3(0.35f, 0.02f, 0.35f), new Color(0.9f, 0.2f, 0.15f));
            destMarker.name = "Destination";
            hoverMarker.SetActive(false);
            destMarker.SetActive(false);
        }

        public void Teleport(Tile tile)
        {
            CancelAction();
            cc.enabled = false;  // a CharacterController overrides position changes while enabled
            transform.position = World.TileCenter(tile);
            lastPosition = transform.position;
            stepTarget = null;
            cc.enabled = true;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            swingCooldown -= dt;
            eatCooldown -= dt;
            buryCooldown -= dt;
            Hitsplats.RemoveAll(h => Time.time - h.Time > Npc.HitsplatSeconds);
            SwingT = Mathf.Max(0f, SwingT - dt / 0.35f);

            if (Input.GetKeyDown(KeyCode.V))
                SetMode(Mode == ViewMode.Tile ? ViewMode.FirstPerson : ViewMode.Tile);

            for (int i = 0; i < Abilities.All.Length; i++)
                if (Input.GetKeyDown(KeyCode.Alpha1 + i))
                    UseAbility(Abilities.All[i].Id);
            UpdateBleeds();

            Vector3 move = Mode == ViewMode.Tile ? UpdateTileMode(dt) : UpdateFirstPerson(dt);

            if (cc.isGrounded)
                fallSpeed = -1f;
            else
                fallSpeed -= 20f * dt;
            cc.Move(move + Vector3.up * fallSpeed * dt);

            UpdateSkilling(dt);
            UpdateHeldItems();
            AnimateArm(dt);
            AnimateBody(dt);
            destMarker.SetActive(Mode == ViewMode.Tile && pathIndex < path.Count);
        }

        public void SetMode(ViewMode mode)
        {
            if (mode == Mode)
                return;
            CancelAction();
            stepTarget = null;
            Mode = mode;
            G.Rig.SetMode(mode);
            hoverMarker.SetActive(false);
            // Your own body would block the view in first person; it still casts a shadow.
            foreach (var r in modelRenderers)
                r.shadowCastingMode = mode == ViewMode.FirstPerson
                    ? UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly
                    : UnityEngine.Rendering.ShadowCastingMode.On;
            GameLog.Add(mode == ViewMode.FirstPerson
                ? "First person: click a tree to chop it, a goblin to attack it. Esc frees the mouse, V goes back."
                : "Tile mode: click a tile to walk, a tree to chop it, a goblin to attack it.");
        }

        /// WASD relative to where the camera faces, length 0..1.
        Vector3 ReadWasd()
        {
            float x = 0f, z = 0f;
            if (Input.GetKey(KeyCode.W)) z += 1f;
            if (Input.GetKey(KeyCode.S)) z -= 1f;
            if (Input.GetKey(KeyCode.D)) x += 1f;
            if (Input.GetKey(KeyCode.A)) x -= 1f;
            if (x == 0f && z == 0f)
                return Vector3.zero;
            float yaw = G.Rig.Yaw * Mathf.Deg2Rad;
            var forward = new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw));
            var right = new Vector3(forward.z, 0f, -forward.x);
            return Vector3.ClampMagnitude(forward * z + right * x, 1f);
        }

        // ---- Tile mode ----

        Vector3 UpdateTileMode(float dt)
        {
            bool overUi = G.Hud.IsMouseOverUi() || G.Hud.MenuOpen;
            Tile? hoverTile = null;
            Component hovered = null;
            var options = overUi ? new List<MenuOption>() : WorldOptions(G.Rig.Cam.ScreenPointToRay(Input.mousePosition), false, out hovered, out hoverTile);
            hoverNpc = hovered as Npc;
            SetHoverText(options);
            hoverMarker.SetActive(hoverTile.HasValue && !overUi);
            if (hoverTile.HasValue)
                hoverMarker.transform.position = World.TileCenter(hoverTile.Value);

            Vector3 wasd = ReadWasd();
            if (wasd != Vector3.zero)
            {
                // WASD takes over: drop the path and stop chopping.
                CancelAction();
                stepTarget = null;
                Face(wasd, dt);
                return wasd * WasdSpeed * dt;
            }

            // Left-click does the top option; right-click lists them all.
            if (!overUi && Input.GetMouseButtonDown(0) && options.Count > 0)
                options[0].Run();
            else if (!overUi && Input.GetMouseButtonDown(1))
                G.Hud.OpenMenu(options, false);

            if (attackTarget != null)
                return UpdateAttack(dt);
            if (pathIndex < path.Count)
                return FollowPath(dt);
            if (targetObject != null)
                ArriveAtObject();
            if (targetItem != null)
                ArriveAtItem();
            if (target != null && !chopping)
                ArriveAtTree();
            if (chopping)
                UpdateAutoChop(dt);
            return Vector3.zero;
        }

        void SetHoverText(List<MenuOption> options)
        {
            if (options.Count == 0)
                HoverText = "";
            else
                HoverText = options[0].Text + (options.Count > 1 ? " <color=#ffffff>/ " + (options.Count - 1) + " more option" + (options.Count > 2 ? "s" : "") + "</color>" : "");
        }

        /// Every option for what a ray passes through, in RuneScape's order: each thing's main action
        /// (nearest first), "Take" for items on the tile, "Walk here" (tile mode), then "Examine" for each.
        /// The first option is what left-click does. First person only lists things within reach.
        List<MenuOption> WorldOptions(Ray ray, bool firstPerson, out Component first, out Tile? tile)
        {
            var options = new List<MenuOption>();
            var examines = new List<MenuOption>();
            var seen = new List<Component>();
            first = null;
            tile = null;
            var hits = Physics.RaycastAll(ray, firstPerson ? FirstPersonReach + 1.5f : 500f);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var hit in hits)
            {
                Component thing = hit.collider.GetComponentInParent<Interactable>();
                if (thing == null) thing = hit.collider.GetComponentInParent<Npc>();
                if (thing == null) thing = hit.collider.GetComponentInParent<GroundItem>();
                if (thing == null) thing = hit.collider.GetComponentInParent<TreeObject>();
                if (thing == null)
                {
                    // The ground (or a wall): the tile you'd walk to. Nothing behind it counts.
                    tile = World.TileAt(hit.point);
                    break;
                }
                if (seen.Contains(thing) || (firstPerson && !InReach(thing)))
                    continue;
                seen.Add(thing);
                AddOptions(thing, firstPerson, options, examines);
                if (first == null && !(thing is Npc n && n.IsDead))
                    first = thing;
                if (!tile.HasValue)
                    tile = TileOf(thing);
            }

            // Items lying on the tile, even ones the ray missed.
            if (!firstPerson && tile.HasValue)
                foreach (var item in G.World.GroundItems)
                    if (item.Tile == tile.Value && !seen.Contains(item))
                    {
                        seen.Add(item);
                        AddOptions(item, false, options, examines);
                    }

            if (!firstPerson && tile.HasValue)
            {
                Tile walkTo = tile.Value;
                options.Add(new MenuOption("Walk here", "", () => WalkTo(walkTo)));
            }
            options.AddRange(examines);
            return options;
        }

        void AddOptions(Component thing, bool firstPerson, List<MenuOption> options, List<MenuOption> examines)
        {
            if (thing is Interactable obj)
            {
                string name = MenuOption.Thing(obj.DisplayName);
                options.Add(new MenuOption(obj.Action, name, () => { if (firstPerson) obj.Interact(); else WalkToObject(obj); }));
                examines.Add(new MenuOption("Examine", name, () => GameLog.Add(obj.Examine)));
            }
            else if (thing is Npc npc)
            {
                if (npc.IsDead)
                    return;
                string name = MenuOption.Npc(npc.HoverName);
                options.Add(new MenuOption("Attack", name, () => { if (firstPerson) FirstPersonAttack(npc); else StartAttack(npc); }));
                examines.Add(new MenuOption("Examine", MenuOption.Npc(npc.Def.Name), () => GameLog.Add(npc.Def.Examine)));
            }
            else if (thing is GroundItem item)
            {
                string name = MenuOption.Item(item.Label);
                options.Add(new MenuOption("Take", name, () => { if (firstPerson) PickUp(item); else WalkToItem(item); }));
                if (FiremakingRules.For(item.ItemId) != null && HasTinderbox)
                    options.Add(new MenuOption("Light", name, () => LightGroundItem(item)));
                examines.Add(new MenuOption("Examine", MenuOption.Item(item.Def.Name), () => GameLog.Add(item.Def.Examine)));
            }
            else if (thing is TreeObject tree)
            {
                if (tree.IsStump)
                {
                    examines.Add(new MenuOption("Examine", MenuOption.Thing("Tree stump"), () => GameLog.Add(tree.Def.StumpExamine)));
                    return;
                }
                string name = MenuOption.Thing(tree.Def.Name);
                options.Add(new MenuOption("Chop down", name, () => { if (firstPerson) FirstPersonChop(tree); else WalkToTree(tree); }));
                examines.Add(new MenuOption("Examine", name, () => GameLog.Add(tree.Def.Examine)));
            }
        }

        static Tile TileOf(Component thing)
        {
            if (thing is Interactable obj) return obj.Tile;
            if (thing is Npc npc) return npc.Tile;
            if (thing is GroundItem item) return item.Tile;
            return ((TreeObject)thing).Tile;
        }

        bool InReach(Component thing)
        {
            Vector3 d = thing.transform.position - transform.position;
            d.y = 0f;
            return d.magnitude <= FirstPersonReach;
        }

        void WalkTo(Tile tile)
        {
            CancelAction();
            SetPath(Pathfinder.PathTo(G.World.Grid, StartTile(), tile));
        }

        void WalkToTree(TreeObject tree)
        {
            CancelAction();
            var p = Pathfinder.PathToInteract(G.World.Grid, StartTile(), tree.Tile);
            if (p == null)
            {
                GameLog.Add("I can't reach that!");
                return;
            }
            target = tree;
            SetPath(p);
        }

        void WalkToObject(Interactable thing)
        {
            CancelAction();
            var p = Pathfinder.PathToInteract(G.World.Grid, StartTile(), thing.Tile);
            if (p == null)
            {
                GameLog.Add("I can't reach that!");
                return;
            }
            targetObject = thing;
            SetPath(p);
        }

        void ArriveAtObject()
        {
            var thing = targetObject;
            targetObject = null;
            if (!CurrentTile.IsCardinalNeighbour(thing.Tile))
            {
                WalkToObject(thing);  // pushed off course; try again
                return;
            }
            transform.rotation = Quaternion.LookRotation(World.TileCenter(thing.Tile) - World.TileCenter(CurrentTile));
            thing.Interact();
        }

        void WalkToItem(GroundItem item)
        {
            CancelAction();
            if (item == null)
                return;  // gone since the menu opened
            targetItem = item;
            SetPath(Pathfinder.PathTo(G.World.Grid, StartTile(), item.Tile));
        }

        void ArriveAtItem()
        {
            var item = targetItem;
            targetItem = null;
            if (item == null)
                return;  // someone else took it, or it vanished
            if (CurrentTile != item.Tile)
            {
                GameLog.Add("I can't reach that!");
                return;
            }
            PickUp(item);
        }

        /// Loot from a kill: straight into your inventory (coins into the money pouch). Anything that
        /// doesn't fit falls on the ground where the monster died.
        public void ReceiveLoot(List<ItemStack> loot, Tile dropTile)
        {
            var got = new List<string>();
            bool spilled = false;
            foreach (var stack in loot)
            {
                var def = ItemDefs.Get(stack.ItemId);
                int added = G.Inventory.Add(stack.ItemId, stack.Count);
                if (added > 0)
                    got.Add(def.Stackable || added > 1 ? Core.Experience.Format(added) + " " + def.Name.ToLowerInvariant() : def.Name.ToLowerInvariant());
                if (added < stack.Count)
                {
                    G.World.SpawnGroundItem(stack.ItemId, stack.Count - added, dropTile);
                    spilled = true;
                }
            }
            if (got.Count > 0)
                GameLog.Add("You get: " + string.Join(", ", got) + ".");
            if (spilled)
                GameLog.Add("Your inventory is full, so the rest falls to the ground.");
        }

        void PickUp(GroundItem item)
        {
            if (item == null)
                return;
            if (!G.Inventory.HasRoomFor(item.ItemId))
            {
                GameLog.Add("You don't have enough inventory space to hold that item.");
                return;
            }
            G.Inventory.Add(item.ItemId, item.Count);
            G.World.RemoveGroundItem(item);
        }

        /// Where a new path starts: the tile you're stepping into if you're walking (so clicking somewhere
        /// new mid-step carries on forward instead of turning back to the tile you just left), otherwise
        /// the tile you're on.
        Tile StartTile()
        {
            if (stepTarget.HasValue && G.World.Grid.IsWalkable(stepTarget.Value))
                return stepTarget.Value;
            Tile t = CurrentTile;
            return G.World.Grid.IsWalkable(t) ? t : G.World.NearestWalkable(t);
        }

        Tile? stepTarget;  // the tile you're walking into (tile mode paths), until you reach its centre

        void SetPath(List<Tile> p)
        {
            path.Clear();
            pathIndex = 0;
            Vector3 flat = transform.position;
            flat.y = 0f;
            Tile start = StartTile();
            if ((flat - World.TileCenter(start)).sqrMagnitude > 0.01f)
                path.Add(start);  // finish the step you're on (or step back onto the grid after WASD)
            path.AddRange(p);
            if (path.Count > 0)
                destMarker.transform.position = World.TileCenter(path[path.Count - 1]) + Vector3.up * 0.02f;
        }

        Vector3 FollowPath(float dt)
        {
            stepTarget = path[pathIndex];
            Vector3 to = World.TileCenter(path[pathIndex]) - transform.position;
            to.y = 0f;
            float step = PathSpeed * dt;
            if (to.magnitude <= step)
            {
                stepTarget = null;
                pathIndex++;
                if (pathIndex >= path.Count)
                {
                    path.Clear();
                    pathIndex = 0;
                }
                return to;
            }
            Face(to, dt);
            return to.normalized * step;
        }

        void ArriveAtTree()
        {
            if (target.IsStump)
            {
                target = null;
                return;
            }
            if (!CurrentTile.IsCardinalNeighbour(target.Tile))
            {
                WalkToTree(target);  // pushed off course; try again
                return;
            }
            if (!CanChop(target))
            {
                target = null;
                return;
            }
            chopping = true;
            chopTimer = (WoodcuttingRules.AutoChopTicks - 1) * WoodcuttingRules.TickSeconds;  // first roll one tick in
            GameLog.Add("You swing your axe at the tree.");
        }

        void UpdateAutoChop(float dt)
        {
            if (target == null || target.IsStump)
            {
                CancelAction();
                return;
            }
            Face(World.TileCenter(target.Tile) - transform.position, dt);
            chopTimer += dt;
            float interval = WoodcuttingRules.AutoChopTicks * WoodcuttingRules.TickSeconds;
            if (chopTimer >= interval)
            {
                chopTimer -= interval;
                if (!Chop(target))
                    CancelAction();
            }
        }

        void CancelAction()
        {
            path.Clear();
            pathIndex = 0;
            target = null;
            targetObject = null;
            targetItem = null;
            attackTarget = null;
            chopping = false;
            fighting = false;
            CancelSkilling();
        }

        void Face(Vector3 dir, float dt)
        {
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-6f)
                return;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), 1f - Mathf.Exp(-14f * dt));
        }

        // ---- First person ----

        Vector3 UpdateFirstPerson(float dt)
        {
            transform.rotation = Quaternion.Euler(0f, G.Rig.Yaw, 0f);
            var cam = G.Rig.Cam.transform;
            bool free = Cursor.lockState != CursorLockMode.Locked;
            Component aimed = null;
            var options = free || G.Hud.MenuOpen ? new List<MenuOption>() : WorldOptions(new Ray(cam.position, cam.forward), true, out aimed, out _);
            SetHoverText(options);
            // A stump only has Examine: clicking it should still swing, so it doesn't count as a target.
            bool hasAction = options.Count > 0 && options[0].Action != "Examine";
            AimingAtTarget = hasAction;
            hoverNpc = aimed as Npc;
            // The axe only comes out for a tree; otherwise your sword if you wield one, else empty hands.
            FirstPersonTool = aimed is TreeObject ? HeldTool.Axe
                : G.Equipment.IsWearing(EquipSlot.Weapon) ? HeldTool.Sword : HeldTool.None;

            if (Input.GetMouseButtonDown(0) && !G.Hud.MenuOpen)
            {
                if (free)
                {
                    if (!G.Hud.IsMouseOverUi())
                        G.Rig.LockCursor(true);  // this click only grabs the mouse
                }
                else if (hasAction)
                {
                    options[0].Run();
                }
                else if (swingCooldown <= 0f)
                {
                    swingCooldown = WoodcuttingRules.FirstPersonSwingTicks * WoodcuttingRules.TickSeconds;
                    SwingT = 1f;  // a swing at thin air
                }
            }
            else if (Input.GetMouseButtonDown(1) && !free && options.Count > 0)
            {
                // The menu needs the mouse: free it, and grab it again once something is chosen.
                G.Rig.LockCursor(false);
                G.Hud.OpenMenu(options, true);
            }
            return ReadWasd() * WasdSpeed * dt;
        }

        /// First person: one chop per click, no faster than a swing.
        void FirstPersonChop(TreeObject tree)
        {
            if (swingCooldown > 0f)
                return;
            swingCooldown = WoodcuttingRules.FirstPersonSwingTicks * WoodcuttingRules.TickSeconds;
            SwingT = 1f;
            if (CanChop(tree))
                Chop(tree);
        }

        /// First person: one attack per click, no faster than the weapon's speed.
        void FirstPersonAttack(Npc npc)
        {
            if (swingCooldown > 0f || npc.IsDead || !CanFight())
                return;
            swingCooldown = G.Equipment.AttackTicks * WoodcuttingRules.TickSeconds;
            SwingT = 1f;
            npc.Engage(transform.position);
            Attack(npc);
        }

        // ---- Woodcutting (both modes) ----

        bool HasHatchet => System.Array.Exists(WoodcuttingRules.Hatchets, h => G.HasTool(h.Id));

        bool CanChop(TreeObject tree)
        {
            if (!HasHatchet)
            {
                GameLog.Add("You need a hatchet to chop down this tree.");
                return false;
            }
            int level = G.Skills.GetLevel(SkillId.Woodcutting);
            if (level < tree.Def.LevelRequired)
            {
                GameLog.Add("You need a Woodcutting level of " + tree.Def.LevelRequired + " to chop down this tree.");
                return false;
            }
            if (!G.Inventory.HasRoomFor(tree.Def.LogItemId))
            {
                GameLog.Add("Your inventory is too full to hold any more logs.");
                return false;
            }
            return true;
        }

        /// One chop at a tree: maybe a log, xp and one of the tree's lives.
        /// Returns whether chopping can go on (false when the tree fell or the inventory filled up).
        bool Chop(TreeObject tree)
        {
            if (tree.IsStump)
                return false;
            tree.Shake();
            int level = G.Skills.GetLevel(SkillId.Woodcutting);
            if (G.Rng.NextDouble() >= WoodcuttingRules.SuccessChance(tree.Def, level))
                return true;  // no log this time

            G.Inventory.Add(tree.Def.LogItemId);
            G.Skills.AddXp(SkillId.Woodcutting, tree.Def.Xp);
            GameLog.Add("You get some logs.");
            if (tree.TakeLife())
                return false;
            if (!G.Inventory.HasRoomFor(tree.Def.LogItemId))
            {
                GameLog.Add("Your inventory is too full to hold any more logs.");
                return false;
            }
            return true;
        }

        // ---- Combat (both modes) ----

        /// For now you need both a sword and a shield worn to fight.
        bool CanFight()
        {
            if (!G.Equipment.IsWearing(EquipSlot.Weapon) || !G.Equipment.IsWearing(EquipSlot.Shield))
            {
                GameLog.Add("You need to wield a sword and shield to fight.");
                return false;
            }
            return true;
        }

        void StartAttack(Npc npc)
        {
            CancelAction();
            if (!CanFight())
                return;
            attackTarget = npc;
            npc.Engage(transform.position);
        }

        /// Tile mode: walk up to the monster, then hit it every few ticks until it dies.
        Vector3 UpdateAttack(float dt)
        {
            var npc = attackTarget;
            if (npc == null || npc.IsDead)
            {
                CancelAction();
                return Vector3.zero;
            }
            Vector3 flat = transform.position - World.TileCenter(CurrentTile);
            flat.y = 0f;
            bool inPlace = CurrentTile.IsCardinalNeighbour(npc.Tile) && flat.sqrMagnitude < 0.01f;
            if (!inPlace)
            {
                fighting = false;
                if (pathIndex >= path.Count)
                {
                    var p = Pathfinder.PathToInteract(G.World.Grid, StartTile(), npc.Tile);
                    if (p == null)
                    {
                        GameLog.Add("I can't reach that!");
                        CancelAction();
                        return Vector3.zero;
                    }
                    SetPath(p);
                    if (path.Count == 0)
                        return Vector3.zero;
                }
                return FollowPath(dt);
            }

            npc.Engage(transform.position);
            Face(npc.transform.position - transform.position, dt);
            if (!fighting)
            {
                fighting = true;
                attackTimer = 0f;  // the first hit lands as soon as you arrive
            }
            attackTimer -= dt;
            if (attackTimer <= 0f)
            {
                attackTimer = G.Equipment.AttackTicks * WoodcuttingRules.TickSeconds;
                if (Attack(npc))
                    CancelAction();
            }
            return Vector3.zero;
        }

        /// One melee attack. Returns true when it killed the monster.
        bool Attack(Npc npc)
        {
            lastTarget = npc;
            attackAnim = 0.4f;
            var eq = G.Equipment;
            var style = G.Style;
            double chance = CombatRules.HitChance(G.Skills.GetLevel(SkillId.Attack), eq.AttackBonus, style, npc.Def.DefenceLevel, npc.Def.DefenceBonus, G.Prayer.AttackMultiplier);
            int maxHit = CombatRules.MaxHit(G.Skills.GetLevel(SkillId.Strength), eq.StrengthBonus, style, G.Prayer.StrengthMultiplier);
            int damage = Mathf.Min(CombatRules.RollDamage(G.Rng, chance, maxHit), npc.Hitpoints);
            bool killed = npc.TakeHit(damage);
            CombatRules.AwardXp(G.Skills, style, damage);
            if (killed)
                GameLog.Add("You defeat the " + npc.Def.Name.ToLowerInvariant() + ".");
            return killed;
        }

        // ---- Eating ----

        /// Eats the food in an inventory slot: one bite every 3 ticks, which also holds back your next attack.
        public void Eat(int slot)
        {
            if (eatCooldown > 0f)
                return;
            var stack = G.Inventory[slot];
            var food = Foods.Eat(G.Inventory, slot, G.Health, out int healed);
            if (food == null)
                return;
            float delay = Foods.EatTicks * WoodcuttingRules.TickSeconds;
            eatCooldown = delay;
            attackTimer += delay;
            swingCooldown = Mathf.Max(swingCooldown, delay);
            GameLog.Add("You eat the " + stack.Def.Name.ToLowerInvariant() + "." + (healed > 0 ? " It heals some health." : ""));
        }

        /// Buries bones from an inventory slot for Prayer xp (one every 2 ticks).
        public void Bury(int slot)
        {
            var stack = G.Inventory[slot];
            double? xp = stack.IsEmpty ? null : Prayers.BuryXp(stack.ItemId);
            if (xp == null || buryCooldown > 0f)
                return;
            buryCooldown = Prayers.BuryTicks * WoodcuttingRules.TickSeconds;
            G.Inventory.RemoveAt(slot);
            G.Skills.AddXp(SkillId.Prayer, xp.Value);
            Kneel(0.6f);
            GameLog.Add("You dig a hole in the ground... You bury the bones.");
        }

        /// A monster swings at you: Protect from Melee blocks it; otherwise it rolls against your Defence.
        /// If you're standing idle (tile mode, sword and shield worn) you fight back, like RuneScape's auto retaliate.
        public void TakeMonsterHit(Npc npc)
        {
            int damage = 0;
            if (!G.Prayer.ProtectsFromMelee)
            {
                double chance = CombatRules.MonsterHitChance(npc.Def, G.Skills.GetLevel(SkillId.Defence), G.Equipment.DefenceBonus, G.Style, G.Prayer.DefenceMultiplier);
                damage = CombatRules.RollDamage(G.Rng, chance, npc.Def.MaxHit);
            }
            G.HurtPlayer(damage);

            bool idle = attackTarget == null && !chopping && !fishing && !cooking && !lighting && pathIndex >= path.Count;
            bool armed = G.Equipment.IsWearing(EquipSlot.Weapon) && G.Equipment.IsWearing(EquipSlot.Shield);
            if (Mode == ViewMode.Tile && idle && armed && !npc.IsDead)
            {
                attackTarget = npc;
                npc.Engage(transform.position);
            }
        }

        public void ShowDamage(int damage)
        {
            LastHurtTime = Time.time;
            Hitsplats.Add(new Npc.Hitsplat { Damage = damage, Time = Time.time });
        }

        // ---- Abilities (action bar, keys 1-9) ----

        /// Whether the ability's level requirement is met (the bar greys out the ones that aren't).
        public bool HasLevelFor(AbilityDef def) => G.Skills.GetLevel(def.Skill) >= def.Level;

        /// Uses an ability on your target: the monster you're fighting, else the one under the mouse
        /// or crosshair, else the last one you hit.
        public void UseAbility(AbilityId id)
        {
            var def = Abilities.Get(id);
            if (!HasLevelFor(def))
            {
                GameLog.Add("You need " + SkillDefs.Get(def.Skill).Name + " level " + def.Level + " to use " + def.Name + ".");
                return;
            }
            if (!Cooldowns.IsReady(id, Time.time) || !CanFight())
                return;

            Npc target = AbilityTarget(def.Effect == AbilityEffect.Charge);
            if (target == null)
            {
                GameLog.Add("You need a target to use " + def.Name + ".");
                return;
            }
            float reach = Mode == ViewMode.FirstPerson ? FirstPersonReach : 1.6f;
            if (FlatDistance(target) > reach)
            {
                if (def.Effect != AbilityEffect.Charge || FlatDistance(target) > Abilities.ChargeRange + 0.5f)
                {
                    GameLog.Add("Your target is too far away.");
                    return;
                }
                // Barge: rush to the target.
                var p = Pathfinder.PathToInteract(G.World.Grid, StartTile(), target.Tile);
                if (p == null)
                {
                    GameLog.Add("I can't reach that!");
                    return;
                }
                if (p.Count > 0)
                    Teleport(p[p.Count - 1]);
                Vector3 look = target.transform.position - transform.position;
                look.y = 0f;
                if (look.sqrMagnitude > 0.01f)
                    transform.rotation = Quaternion.LookRotation(look);
            }

            Cooldowns.Use(id, Time.time);
            attackAnim = 0.4f;
            SwingT = 1f;
            target.Engage(transform.position);
            lastTarget = target;

            int damage = RollAbility(def, target);
            if (def.Effect == AbilityEffect.Bleed)
            {
                var hits = Abilities.SplitBleed(damage);
                bleeds.Add(new Bleed { Npc = target, Hits = hits, Next = 1, NextTime = Time.time + Abilities.BleedInterval });
                AbilityHit(target, hits[0]);
            }
            else
            {
                if (def.Effect == AbilityEffect.Stun && damage > 0)
                    target.Stun(Abilities.StunSeconds);
                if (def.Effect == AbilityEffect.Area)
                    foreach (var other in G.World.Npcs)
                        if (other != target && !other.IsDead && (other.transform.position - target.transform.position).magnitude <= 1.6f)
                        {
                            other.Engage(transform.position);
                            AbilityHit(other, RollAbility(def, other));
                        }
                AbilityHit(target, damage);
            }

            // Keep fighting it afterwards; the next auto-attack waits a full weapon swing.
            float swing = G.Equipment.AttackTicks * WoodcuttingRules.TickSeconds;
            if (Mode == ViewMode.Tile && !target.IsDead)
            {
                if (attackTarget != target)
                {
                    CancelAction();
                    attackTarget = target;
                }
                fighting = FlatDistance(target) <= 1.6f;
                attackTimer = swing;
            }
            else
            {
                swingCooldown = Mathf.Max(swingCooldown, swing);
            }
        }

        Npc AbilityTarget(bool longRange)
        {
            if (attackTarget != null && !attackTarget.IsDead)
                return attackTarget;
            if (hoverNpc != null && !hoverNpc.IsDead)
                return hoverNpc;
            if (longRange && Mode == ViewMode.FirstPerson)
            {
                // Barge in first person: whatever is under the crosshair, however far.
                var cam = G.Rig.Cam.transform;
                if (Physics.Raycast(cam.position, cam.forward, out RaycastHit hit, Abilities.ChargeRange + 2f))
                {
                    var npc = hit.collider.GetComponentInParent<Npc>();
                    if (npc != null && !npc.IsDead)
                        return npc;
                }
            }
            return lastTarget != null && !lastTarget.IsDead ? lastTarget : null;
        }

        int RollAbility(AbilityDef def, Npc npc)
        {
            var eq = G.Equipment;
            double chance = CombatRules.HitChance(G.Skills.GetLevel(SkillId.Attack), eq.AttackBonus, G.Style, npc.Def.DefenceLevel, npc.Def.DefenceBonus, G.Prayer.AttackMultiplier);
            int maxHit = CombatRules.MaxHit(G.Skills.GetLevel(SkillId.Strength), eq.StrengthBonus, G.Style, G.Prayer.StrengthMultiplier);
            return Abilities.RollDamage(G.Rng, def, chance, maxHit, (double)npc.Hitpoints / npc.Def.Hitpoints);
        }

        void AbilityHit(Npc npc, int damage)
        {
            if (npc.IsDead)
                return;
            damage = Mathf.Min(damage, npc.Hitpoints);
            bool killed = npc.TakeHit(damage);
            CombatRules.AwardXp(G.Skills, G.Style, damage);
            if (killed)
            {
                GameLog.Add("You defeat the " + npc.Def.Name.ToLowerInvariant() + ".");
                if (npc == attackTarget)
                    CancelAction();
            }
        }

        void UpdateBleeds()
        {
            for (int i = bleeds.Count - 1; i >= 0; i--)
            {
                var b = bleeds[i];
                if (b.Npc == null || b.Npc.IsDead)
                {
                    bleeds.RemoveAt(i);
                    continue;
                }
                if (Time.time < b.NextTime)
                    continue;
                AbilityHit(b.Npc, b.Hits[b.Next]);
                b.Next++;
                b.NextTime += Abilities.BleedInterval;
                if (b.Next >= b.Hits.Length)
                    bleeds.RemoveAt(i);
            }
        }

        float FlatDistance(Component c)
        {
            Vector3 d = c.transform.position - transform.position;
            d.y = 0f;
            return d.magnitude;
        }

        void AnimateArm(float dt)
        {
            attackAnim = Mathf.Max(0f, attackAnim - dt);
            if (chopping)
                chopAnim += dt;
            else
                chopAnim = 0f;
            float angle;
            if (chopping)
            {
                // Two swings per automatic chop: raise slowly, strike fast.
                float cycle = WoodcuttingRules.AutoChopTicks * WoodcuttingRules.TickSeconds / 2f;
                float t = (chopAnim % cycle) / cycle;
                angle = t < 0.7f ? Mathf.Lerp(0f, -150f, t / 0.7f) : Mathf.Lerp(-150f, -40f, (t - 0.7f) / 0.3f);
            }
            else if (attackAnim > 0f)
            {
                // A quick slash: up, then down and forward.
                float t = 1f - attackAnim / 0.4f;
                angle = t < 0.35f ? Mathf.Lerp(-15f, -140f, t / 0.35f) : Mathf.Lerp(-140f, -60f, (t - 0.35f) / 0.65f);
            }
            else if (cooking)
            {
                angle = -75f + Mathf.Sin(Time.time * 3f) * 8f;  // holding the food over the fire
            }
            else if (fishing)
            {
                angle = -70f + Mathf.Sin(Time.time * 1.5f) * 6f;  // net held out over the water
            }
            else if (lighting)
            {
                angle = -55f + Mathf.Abs(Mathf.Sin(Time.time * 9f)) * 20f;  // striking the tinderbox
            }
            else
            {
                angle = fighting ? -60f : -15f + Mathf.Sin(walkPhase) * 30f * Mathf.Clamp01(speed / 3f);
            }
            armPivot.localRotation = Quaternion.Slerp(armPivot.localRotation, Quaternion.Euler(angle, 0f, 0f), 1f - Mathf.Exp(-25f * dt));
        }
    }
}
