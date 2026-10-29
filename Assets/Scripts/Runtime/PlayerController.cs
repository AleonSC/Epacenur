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
    public enum HeldTool { Axe, Sword }

    /// The player: walking (tile paths or WASD), the view toggle, woodcutting, picking things up and fighting.
    public sealed class PlayerController : MonoBehaviour
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
        public HeldTool FirstPersonTool { get; private set; } = HeldTool.Axe;
        /// 1 at the start of a first-person swing, falling to 0.
        public float SwingT { get; private set; }

        Tile CurrentTile => World.TileAt(transform.position);
        Game G => Game.Instance;

        CharacterController cc;
        Transform model, armPivot, heldAxe, heldSword, heldShield;
        Renderer[] modelRenderers;
        GameObject hoverMarker, destMarker;

        readonly List<Tile> path = new List<Tile>();
        int pathIndex;
        TreeObject target;      // the tree being walked to or chopped (tile mode)
        Interactable targetObject;  // the bank booth, NPC... being walked to (tile mode)
        GroundItem targetItem;      // the ground item being walked to (tile mode)
        Npc attackTarget;           // the monster being walked to or fought (tile mode)
        bool chopping, fighting;
        float chopTimer, swingCooldown, fallSpeed, chopAnim, attackTimer, attackAnim;

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
            Visuals.Prim(PrimitiveType.Capsule, model, new Vector3(0f, 0.95f, 0f), new Vector3(0.55f, 0.6f, 0.4f), new Color(0.2f, 0.36f, 0.7f));
            Visuals.Prim(PrimitiveType.Cube, model, new Vector3(0f, 0.32f, 0f), new Vector3(0.42f, 0.5f, 0.28f), new Color(0.3f, 0.25f, 0.2f));
            Visuals.Prim(PrimitiveType.Sphere, model, new Vector3(0f, 1.62f, 0f), new Vector3(0.36f, 0.38f, 0.36f), skin);
            Visuals.Prim(PrimitiveType.Cube, model, new Vector3(0f, 1.66f, 0.17f), new Vector3(0.2f, 0.06f, 0.04f), new Color(0.15f, 0.15f, 0.2f));
            Visuals.Prim(PrimitiveType.Capsule, model, new Vector3(-0.33f, 1.1f, 0f), new Vector3(0.14f, 0.3f, 0.14f), skin);

            // Right arm with an axe, swung from the shoulder.
            armPivot = new GameObject("Arm").transform;
            armPivot.SetParent(model, false);
            armPivot.localPosition = new Vector3(0.33f, 1.35f, 0f);
            Visuals.Prim(PrimitiveType.Capsule, armPivot, new Vector3(0f, -0.28f, 0f), new Vector3(0.14f, 0.3f, 0.14f), skin);
            heldAxe = new GameObject("Axe").transform;
            heldAxe.SetParent(armPivot, false);
            heldAxe.localPosition = new Vector3(0f, -0.55f, 0.1f);
            BuildAxe(heldAxe);
            heldSword = new GameObject("Sword").transform;
            heldSword.SetParent(armPivot, false);
            heldSword.localPosition = new Vector3(0f, -0.55f, 0.02f);
            ItemModels.BuildSword(heldSword, ItemDefs.BronzeSword);
            // Shield strapped to the left arm.
            heldShield = new GameObject("Shield").transform;
            heldShield.SetParent(model, false);
            heldShield.localPosition = new Vector3(-0.44f, 1.05f, 0.05f);
            ItemModels.BuildShield(heldShield, ItemDefs.WoodenShield);
            modelRenderers = model.GetComponentsInChildren<Renderer>(true);
        }

        /// Show the sword and shield you're wearing; the axe comes out to chop.
        void UpdateHeldItems()
        {
            bool sword = G.Equipment.IsWearing(EquipSlot.Weapon);
            heldSword.gameObject.SetActive(sword && !chopping);
            heldAxe.gameObject.SetActive(chopping || !sword);
            heldShield.gameObject.SetActive(G.Equipment.IsWearing(EquipSlot.Shield));
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
            cc.enabled = true;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            swingCooldown -= dt;
            SwingT = Mathf.Max(0f, SwingT - dt / 0.35f);

            if (Input.GetKeyDown(KeyCode.V))
                SetMode(Mode == ViewMode.Tile ? ViewMode.FirstPerson : ViewMode.Tile);

            Vector3 move = Mode == ViewMode.Tile ? UpdateTileMode(dt) : UpdateFirstPerson(dt);

            if (cc.isGrounded)
                fallSpeed = -1f;
            else
                fallSpeed -= 20f * dt;
            cc.Move(move + Vector3.up * fallSpeed * dt);

            UpdateHeldItems();
            AnimateArm(dt);
            destMarker.SetActive(Mode == ViewMode.Tile && pathIndex < path.Count);
        }

        public void SetMode(ViewMode mode)
        {
            if (mode == Mode)
                return;
            CancelAction();
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
            var options = overUi ? new List<MenuOption>() : WorldOptions(G.Rig.Cam.ScreenPointToRay(Input.mousePosition), false, out _, out hoverTile);
            SetHoverText(options);
            hoverMarker.SetActive(hoverTile.HasValue && !overUi);
            if (hoverTile.HasValue)
                hoverMarker.transform.position = World.TileCenter(hoverTile.Value);

            Vector3 wasd = ReadWasd();
            if (wasd != Vector3.zero)
            {
                // WASD takes over: drop the path and stop chopping.
                CancelAction();
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

        // Paths start from the tile you're on; after WASD you may be off its centre.
        Tile StartTile()
        {
            Tile t = CurrentTile;
            return G.World.Grid.IsWalkable(t) ? t : G.World.NearestWalkable(t);
        }

        void SetPath(List<Tile> p)
        {
            path.Clear();
            pathIndex = 0;
            Vector3 flat = transform.position;
            flat.y = 0f;
            Tile start = StartTile();
            if ((flat - World.TileCenter(start)).sqrMagnitude > 0.01f)
                path.Add(start);  // step back onto the grid first
            path.AddRange(p);
            if (path.Count > 0)
                destMarker.transform.position = World.TileCenter(path[path.Count - 1]) + Vector3.up * 0.02f;
        }

        Vector3 FollowPath(float dt)
        {
            Vector3 to = World.TileCenter(path[pathIndex]) - transform.position;
            to.y = 0f;
            float step = PathSpeed * dt;
            if (to.magnitude <= step)
            {
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
            FirstPersonTool = (aimed is TreeObject) || !G.Equipment.IsWearing(EquipSlot.Weapon) ? HeldTool.Axe : HeldTool.Sword;

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

        bool CanChop(TreeObject tree)
        {
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
            attackAnim = 0.4f;
            var eq = G.Equipment;
            var style = G.Style;
            double chance = CombatRules.HitChance(G.Skills.GetLevel(SkillId.Attack), eq.AttackBonus, style, npc.Def.DefenceLevel, npc.Def.DefenceBonus);
            int maxHit = CombatRules.MaxHit(G.Skills.GetLevel(SkillId.Strength), eq.StrengthBonus, style);
            int damage = Mathf.Min(CombatRules.RollDamage(G.Rng, chance, maxHit), npc.Hitpoints);
            bool killed = npc.TakeHit(damage);
            CombatRules.AwardXp(G.Skills, style, damage);
            if (killed)
                GameLog.Add("You defeat the " + npc.Def.Name.ToLowerInvariant() + ".");
            return killed;
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
            else
            {
                angle = fighting ? -60f : -15f;
            }
            armPivot.localRotation = Quaternion.Slerp(armPivot.localRotation, Quaternion.Euler(angle, 0f, 0f), 1f - Mathf.Exp(-25f * dt));
        }
    }
}
