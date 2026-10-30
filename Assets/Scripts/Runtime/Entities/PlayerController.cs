using System.Collections.Generic;
using Epacenur.Content;
using Epacenur.Core;
using UnityEngine;

namespace Epacenur.Runtime
{
    public enum ViewMode
    {
        Tile,         // top-down: click tiles to walk, click trees to chop them automatically; WASD also works
        FirstPerson,  // mouse look + WASD; every chop or attack is a click
    }

    /// The player: walking (tile paths or WASD), the view toggle, the right-click options for the world,
    /// and running activities (gathering, making, lighting fires). Fighting is in PlayerCombat.cs, the
    /// body and its animations in PlayerModel.cs.
    public sealed partial class PlayerController : MonoBehaviour
    {
        // Tiles per second. Walking is about RuneScape's pace; hold Shift to run.
        public const float WalkPathSpeed = 2f, RunPathSpeed = 4.4f;    // clicked paths (tile mode)
        public const float WalkWasdSpeed = 2.5f, RunWasdSpeed = 5.4f;  // WASD (both views)
        public const float FirstPersonReach = 2.3f; // furthest a thing's centre can be to use it in first person
        public const float EyeHeight = 1.6f;

        /// Holding Shift, with run energy left.
        public bool IsRunning => (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) && G.Energy.CanRun;
        float PathSpeed => IsRunning ? RunPathSpeed : WalkPathSpeed;
        float WasdSpeed => IsRunning ? RunWasdSpeed : WalkWasdSpeed;

        public ViewMode Mode { get; private set; } = ViewMode.Tile;
        /// What left-click would do, RuneScape style ("Chop down Tree / 2 more options"), for the HUD.
        public string HoverText { get; private set; } = "";
        /// First person: whether the crosshair is on something close enough to use (a tree, a bank booth, an NPC).
        public bool AimingAtTarget { get; private set; }
        public bool SwingReady => swingCooldown <= 0f;
        /// First person: what's in your hand (the tool for what you aim at, else your weapon), or null.
        public ItemDef FirstPersonItem { get; private set; }
        /// 1 at the start of a first-person swing, falling to 0.
        public float SwingT { get; private set; }
        /// What you're busy doing (chopping, smelting...), or null.
        public Activity Activity { get; private set; }

        public Tile CurrentTile => World.TileAt(transform.position);
        Game G => Game.Instance;

        CharacterController cc;
        GameObject hoverMarker, destMarker;

        readonly List<Tile> path = new List<Tile>();
        int pathIndex;
        Interactable targetObject;  // the tree, bank booth, NPC... being walked to (tile mode)
        GroundItem targetItem;      // the ground item being walked to (tile mode)
        bool lightOnArrival;        // ...to light it rather than take it
        System.Action<Interactable> onArrive;  // what to do on reaching targetObject (null: use it)
        float swingCooldown, fallSpeed, eatCooldown, buryCooldown;

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
            cc.slopeLimit = 75f;  // the hills are walkable everywhere (the tile grid decides where you can go)
            var p = go.AddComponent<PlayerController>();
            p.cc = cc;
            p.BuildModel();
            p.BuildMarkers();
            return p;
        }

        void Start()
        {
            G.Equipment.Changed += RebuildWorn;
            RebuildWorn();
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

        void OnDestroy()
        {
            Destroy(hoverMarker);
            Destroy(destMarker);
            StopBuilding();  // the ghost of a piece being placed
        }

        /// The world's origin moved (World): move with it, so you stay on the same tile.
        public void ShiftBy(Vector3 delta)
        {
            cc.enabled = false;
            transform.position += delta;
            cc.enabled = true;
            lastPosition += delta;
            destMarker.transform.position += delta;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            swingCooldown -= dt;
            eatCooldown -= dt;
            buryCooldown -= dt;
            Hitsplats.RemoveAll(h => Time.time - h.Time > Npc.HitsplatSeconds);
            SwingT = Mathf.Max(0f, SwingT - dt / 0.35f);
            if (UpdateCrossing(dt))
            {
                G.Energy.Rest(dt, G.Skills.GetLevel(Epacenur.Content.AgilityModule.Skill));
                UpdateHeldItems();
                AnimateArm(dt);
                AnimateBody(dt);
                return;
            }

            if (Input.GetKeyDown(KeyCode.V) && !Hud.Typing)
                SetMode(Mode == ViewMode.Tile ? ViewMode.FirstPerson : ViewMode.Tile);

            for (int i = 0; i < Abilities.All.Length; i++)
                if (Input.GetKeyDown(KeyCode.Alpha1 + i) && !Hud.Typing)
                    UseAbility(Abilities.All[i].Id);
            UpdateBleeds();
            UpdateMagic(dt);

            directMove = false;
            Vector3 move = IsStunned ? Vector3.zero : Mode == ViewMode.Tile ? UpdateTileMode(dt) : UpdateFirstPerson(dt);
            UpdateEnergy(move, dt);

            if (directMove)
            {
                // A path step into, out of or through a base: straight there (the path already keeps to its
                // doorways), not pushed about by its floors and walls.
                cc.enabled = false;
                transform.position += move;
                cc.enabled = true;
                fallSpeed = 0f;
            }
            else
            {
                if (cc.isGrounded)
                    fallSpeed = -6f;  // pressed to the ground, so walking down a hill doesn't bounce
                else
                    fallSpeed -= 20f * dt;
                cc.Move(move + Vector3.up * fallSpeed * dt);
            }

            if (Activity != null && !Activity.StillValid(this))
                Activity = null;
            if (Activity != null && Activity.Target != null)
                Face(Activity.Target.position - transform.position, dt);
            UpdateHeldItems();
            AnimateArm(dt);
            AnimateBody(dt);
            destMarker.SetActive(Mode == ViewMode.Tile && pathIndex < path.Count);
        }

        /// A game tick (Game's clock): activities move on.
        public void OnTick()
        {
            if (Activity != null && !Activity.Tick(this))
                Activity = null;
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
            ApplyShadowMode(model);
            GameLog.Add(mode == ViewMode.FirstPerson
                ? "First person: click a tree to chop it, a goblin to attack it. Esc frees the mouse, V goes back."
                : "Tile mode: click a tile to walk, a tree to chop it, a goblin to attack it.");
        }

        /// WASD relative to where the camera faces, length 0..1.
        Vector3 ReadWasd()
        {
            if (Hud.Typing)
                return Vector3.zero;
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
            if (Building != null)
            {
                // Placing a piece: clicks place it, so they don't walk; WASD still does.
                hoverMarker.SetActive(false);
                HoverText = "";
                UpdateBuilding(G.Rig.Cam.ScreenPointToRay(Input.mousePosition), overUi, Input.GetMouseButtonDown(0), !overUi && Input.GetMouseButtonDown(1));
                Vector3 keys = ReadWasd();
                if (keys == Vector3.zero)
                    return Vector3.zero;
                Face(keys, dt);
                return keys * WasdSpeed * dt;
            }
            Tile? hoverTile = null;
            Component hovered = null;
            var options = overUi ? new List<MenuOption>() : WorldOptions(G.Rig.Cam.ScreenPointToRay(Input.mousePosition), false, out hovered, out hoverTile);
            hoverNpc = hovered as Npc;
            SetHoverText(options);
            hoverMarker.SetActive(hoverTile.HasValue && !overUi);
            if (hoverTile.HasValue)
            {
                var ht = hoverTile.Value;
                hoverMarker.transform.position = World.TileCenter(ht);
                if (G.World.Bases.TryCell(ht, out var hb, out _, out _))
                {
                    // A base's cell: square with the base.
                    hoverMarker.transform.position += Vector3.up * 0.01f;
                    hoverMarker.transform.rotation = Quaternion.Euler(0f, (float)hb.Angle, 0f);
                }
                else
                {
                    // Lie along the slope of the hill.
                    float sx = (World.Height(new Tile(ht.X + 1, ht.Z)) - World.Height(new Tile(ht.X - 1, ht.Z))) / 2f;
                    float sz = (World.Height(new Tile(ht.X, ht.Z + 1)) - World.Height(new Tile(ht.X, ht.Z - 1))) / 2f;
                    hoverMarker.transform.rotation = Quaternion.Euler(-Mathf.Atan(sz) * Mathf.Rad2Deg, 0f, Mathf.Atan(sx) * Mathf.Rad2Deg);
                }
            }

            Vector3 wasd = ReadWasd();
            if (wasd != Vector3.zero)
            {
                // WASD takes over: drop the path and whatever you were doing.
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

            if (pickTarget != null)
                return UpdatePickpocket(dt);
            if (attackTarget != null)
                return UpdateAttack(dt);
            if (pathIndex < path.Count)
                return FollowPath(dt);
            if (targetObject != null)
                ArriveAtObject();
            if (targetItem != null)
                ArriveAtItem();
            return Vector3.zero;
        }

        void SetHoverText(List<MenuOption> options)
        {
            if (IsUsing && (options.Count == 0 || options[0].Action != "Use"))
                HoverText = "Use " + MenuOption.Item(UsingDef.Name) + " ->";
            else if (options.Count == 0)
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
            var hits = Physics.RaycastAll(ray, firstPerson ? Mathf.Max(FirstPersonReach, AttackRange + 0.5f) + 1.5f : 500f);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var hit in hits)
            {
                Component thing = hit.collider.GetComponentInParent<Interactable>();
                if (thing == null) thing = hit.collider.GetComponentInParent<Npc>();
                if (thing == null) thing = hit.collider.GetComponentInParent<GroundItem>();
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
            if (IsUsing)
            {
                AddUseOptions(thing, firstPerson, options, examines);
                return;
            }
            if (thing is Interactable obj)
            {
                string name = MenuOption.Thing(obj.DisplayName);
                if (obj.HasAction)
                    options.Add(new MenuOption(obj.Action, name, () => { if (firstPerson) obj.Interact(); else WalkToObject(obj); }));
                obj.AddOptions(options);
                var part = obj.GetComponent<BasePart>();
                if (part != null && HasHammer)
                    examines.Add(new MenuOption("Remove", name, () => RemovePiece(part)));
                examines.Add(new MenuOption("Examine", name, () => GameLog.Add(obj.Examine)));
                LookUp(obj.DisplayName, name, examines);
            }
            else if (thing is Npc npc)
            {
                if (npc.IsDead)
                    return;
                string name = MenuOption.Npc(npc.HoverName);
                if (npc.Def.Pickpocket != null)
                    options.Add(new MenuOption("Pickpocket", name, () => StartPickpocket(npc, firstPerson)));
                options.Add(new MenuOption("Attack", name, () => { if (firstPerson) FirstPersonAttack(npc); else StartAttack(npc); }));
                examines.Add(new MenuOption("Examine", MenuOption.Npc(npc.Def.Name), () => GameLog.Add(npc.Def.Examine)));
                LookUp(npc.Def.Name, MenuOption.Npc(npc.Def.Name), examines);
            }
            else if (thing is GroundItem item)
            {
                string name = MenuOption.Item(item.Label);
                options.Add(new MenuOption("Take", name, () => { if (firstPerson) PickUp(item); else WalkToItem(item, false); }));
                if (ContentDb.Current.Burnable(item.ItemId) != null && ToolBelt.Find(G.ToolBelt, G.Inventory, "tinderbox", G.Skills) != null)
                    options.Add(new MenuOption(Verbs.Light, name, () => LightGroundItem(item)));
                examines.Add(new MenuOption("Examine", MenuOption.Item(item.Def.Name), () => GameLog.Add(item.Def.Examine)));
                LookUp(item.Def.Name, MenuOption.Item(item.Def.Name), examines);
            }
        }

        /// "Look up": opens the encyclopedia on the thing's name.
        void LookUp(string title, string shown, List<MenuOption> options) => options.Add(new MenuOption("Look up", shown, () => G.Hud.OpenEncyclopedia(title)));

        static Tile TileOf(Component thing)
        {
            if (thing is Interactable obj) return obj.Tile;
            if (thing is Npc npc) return npc.Tile;
            return ((GroundItem)thing).Tile;
        }

        /// First person: close enough to use, or with a bow, to shoot at a monster.
        bool InReach(Component thing) => FlatDistance(thing) <= (thing is Npc && AttacksFromRange ? AttackRange + 0.5f : FirstPersonReach);

        public float FlatDistance(Component c)
        {
            Vector3 d = c.transform.position - transform.position;
            d.y = 0f;
            return d.magnitude;
        }

        /// Walks to a tile (the minimap).
        public void WalkToTile(Tile tile) => WalkTo(tile);

        void WalkTo(Tile tile)
        {
            CancelAction();
            SetPath(Pathfinder.PathTo(G.World.Grid, StartTile(), tile));
        }

        /// Walks next to something, then uses it (or does `arrive` instead, e.g. use an item on it).
        void WalkToObject(Interactable thing, System.Action<Interactable> arrive = null)
        {
            CancelAction();
            var approach = thing.ApproachTile;
            var p = approach.HasValue ? Pathfinder.PathTo(G.World.Grid, StartTile(), approach.Value) : Pathfinder.PathToInteract(G.World.Grid, StartTile(), thing.Tile);
            if (p != null && approach.HasValue && (p.Count > 0 ? p[p.Count - 1] : StartTile()) != approach.Value)
                p = null;  // couldn't get right there
            if (p == null)
            {
                GameLog.Add("I can't reach that!");
                return;
            }
            targetObject = thing;
            onArrive = arrive;
            SetPath(p);
        }

        void ArriveAtObject()
        {
            var thing = targetObject;
            var arrive = onArrive;
            targetObject = null;
            onArrive = null;
            if (thing == null)
                return;  // gone (a fire burned out)
            var approach = thing.ApproachTile;
            if (approach.HasValue ? CurrentTile != approach.Value : !CurrentTile.IsCardinalNeighbour(thing.Tile))
            {
                WalkToObject(thing, arrive);  // pushed off course; try again
                return;
            }
            var look = World.TileCenter(thing.Tile) - World.TileCenter(CurrentTile);
            look.y = 0f;
            if (look.sqrMagnitude > 1e-4f)
                transform.rotation = Quaternion.LookRotation(look);
            if (arrive != null)
                arrive(thing);
            else
                thing.Interact();
        }

        void WalkToItem(GroundItem item, bool light)
        {
            CancelAction();
            if (item == null)
                return;  // gone since the menu opened
            targetItem = item;
            lightOnArrival = light;
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
            if (lightOnArrival)
                Activity = new LightActivity(item);
            else
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
            Sfx.Play(Sound.Pickup, 0.7f);
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
        bool directMove;   // this frame's move is a path step in a base: no collisions or gravity

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
            directMove = BaseSet.IsCell(path[pathIndex]) || BaseSet.IsCell(CurrentTile);
            Vector3 full = World.TileCenter(path[pathIndex]) - transform.position;
            Vector3 to = full;
            to.y = 0f;
            float step = PathSpeed * dt;
            // Up and down the hills with the ground (gravity alone would bounce you down a slope).
            float climb = to.magnitude > 1e-4f ? full.y * Mathf.Min(1f, step / to.magnitude) : 0f;
            if (to.magnitude <= step)
            {
                stepTarget = null;
                pathIndex++;
                if (pathIndex >= path.Count)
                {
                    path.Clear();
                    pathIndex = 0;
                }
                return to + Vector3.up * climb;
            }
            Face(to, dt);
            return to.normalized * step + Vector3.up * climb;
        }

        void CancelAction()
        {
            path.Clear();
            pathIndex = 0;
            targetObject = null;
            targetItem = null;
            attackTarget = null;
            pickTarget = null;
            fighting = false;
            onArrive = null;
            Activity = null;
            CancelUse();
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
            if (Building != null)
            {
                // Placing a piece where the crosshair points.
                HoverText = "";
                AimingAtTarget = false;
                bool down = Input.GetMouseButtonDown(0) && !G.Hud.MenuOpen;
                if (down && free && !G.Hud.IsMouseOverUi())
                    G.Rig.LockCursor(true);  // this click only grabs the mouse
                UpdateBuilding(new Ray(cam.position, cam.forward), free, down && !free, !free && Input.GetMouseButtonDown(1));
                return ReadWasd() * WasdSpeed * dt;
            }
            Component aimed = null;
            var options = free || G.Hud.MenuOpen ? new List<MenuOption>() : WorldOptions(new Ray(cam.position, cam.forward), true, out aimed, out _);
            SetHoverText(options);
            // A stump only has Examine: clicking it should still swing, so it doesn't count as a target.
            bool hasAction = options.Count > 0 && options[0].Action != "Examine";
            AimingAtTarget = hasAction;
            hoverNpc = aimed as Npc;
            // The tool comes out for what you aim at (a hatchet for a tree); otherwise your weapon, if any.
            var node = aimed as NodeObject;
            ItemDef tool = node != null && node.Def.ToolType != null && node.Def.Look != NodeLook.FishingSpot
                ? ToolBelt.Find(G.ToolBelt, G.Inventory, node.Def.ToolType, G.Skills) : null;
            FirstPersonItem = tool ?? (G.Equipment.IsWearing(EquipSlot.Weapon) ? G.Equipment[EquipSlot.Weapon].Def : null);

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
                    swingCooldown = 2 * GameTime.TickSeconds;
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

        // ---- Gathering, making, firemaking ----

        /// Using a node (tree, rock, fishing spot): in tile mode you keep at it (you're already next to it);
        /// in first person each click is one attempt.
        public void Gather(NodeObject node)
        {
            string problem = GatherActivity.Problem(node);
            if (problem != null)
            {
                GameLog.Add(problem);
                return;
            }
            if (Mode == ViewMode.FirstPerson)
            {
                if (swingCooldown > 0f)
                    return;
                swingCooldown = node.Def.FirstPersonTicks * GameTime.TickSeconds;
                SwingT = 1f;
                GatherActivity.Attempt(node);
                return;
            }
            Activity = new GatherActivity(node);
            if (node.Def.StartMessage != null)
                GameLog.Add(node.Def.StartMessage);
        }

        /// Starts making `count` of a recipe at a station (int.MaxValue: as many as you can). Walking away stops it.
        public void StartMaking(StationObject station, RecipeDef recipe, int count)
        {
            var a = MakeActivity.Start(station, recipe, count);
            if (a == null)
                return;
            CancelAction();
            Activity = a;
        }

        /// "Cook" on raw food in the inventory: use the nearest fire (walking to it in tile mode).
        public void CookFromInventory(string rawItemId)
        {
            Fire nearest = null;
            float best = float.MaxValue;
            foreach (var f in G.World.Fires)
            {
                float d = FlatDistance(f);
                if (d < best)
                {
                    best = d;
                    nearest = f;
                }
            }
            float range = Mode == ViewMode.FirstPerson ? FirstPersonReach : 15f;
            if (nearest == null || best > range)
            {
                GameLog.Add(Mode == ViewMode.FirstPerson && nearest != null
                    ? "You need to stand closer to the fire."
                    : "You need a fire to cook on. Light one with logs and a tinderbox.");
                return;
            }
            if (Mode == ViewMode.Tile && !CurrentTile.IsCardinalNeighbour(nearest.Tile))
                WalkToObject(nearest, t => UseOnObject(t, rawItemId));  // cooks when you get there
            else
                UseOnObject(nearest, rawItemId);
        }

        /// "Light" on logs in the inventory: they go down on your tile and you light them there.
        public void LightFromInventory(int slot)
        {
            var stack = G.Inventory[slot];
            var burn = stack.IsEmpty ? null : ContentDb.Current.Burnable(stack.ItemId);
            if (burn == null || !CanLight(burn, CurrentTile))
                return;
            CancelAction();
            G.Inventory.Set(slot, default(ItemStack));
            var logs = G.World.SpawnGroundItem(stack.ItemId, 1, CurrentTile);
            Activity = new LightActivity(logs);
        }

        /// "Light" on logs lying on the ground: walk onto their tile (tile mode) and light them.
        public void LightGroundItem(GroundItem item)
        {
            if (item == null)
                return;
            var burn = ContentDb.Current.Burnable(item.ItemId);
            if (burn == null || !CanLight(burn, item.Tile))
                return;
            CancelAction();
            if (Mode == ViewMode.FirstPerson || CurrentTile == item.Tile)
                Activity = new LightActivity(item);
            else
                WalkToItem(item, true);
        }

        bool CanLight(BurnableDef burn, Tile tile)
        {
            string problem = G.World.Bases.TryCell(tile, out var b, out int bi, out int bj) && b.HasFoundation(bi, bj)
                ? "You can't light a fire on a floor." : LightActivity.Problem(burn, tile);
            if (problem != null)
                GameLog.Add(problem);
            return problem == null;
        }

        /// After lighting a fire: step off it, west first as in RuneScape (tile mode; in first person
        /// you just walk off).
        public void StepOffFire(Tile tile)
        {
            if (Mode != ViewMode.Tile || CurrentTile != tile)
                return;
            foreach (var d in new[] { new Tile(-1, 0), new Tile(1, 0), new Tile(0, -1), new Tile(0, 1) })
            {
                var next = new Tile(tile.X + d.X, tile.Z + d.Z);
                if (G.World.Grid.IsWalkable(next))
                {
                    path.Clear();
                    pathIndex = 0;
                    path.Add(next);
                    return;
                }
            }
        }

        // ---- Eating, burying, dropping ----

        /// Eats the food in an inventory slot: one bite every 3 ticks, which also holds back your next attack.
        public void Eat(int slot)
        {
            if (eatCooldown > 0f)
                return;
            var stack = G.Inventory[slot];
            var food = Foods.Eat(G.Inventory, slot, G.Health, out int healed);
            if (food == null)
                return;
            float delay = Foods.EatTicks * GameTime.TickSeconds;
            eatCooldown = delay;
            nextAttackAt = Mathf.Max(nextAttackAt, Time.time) + delay;
            swingCooldown = Mathf.Max(swingCooldown, delay);
            GameLog.Add("You eat the " + stack.Def.Name.ToLowerInvariant() + "." + (healed > 0 ? " It heals some health." : ""));
            Sfx.Play(Sound.Eat);
        }

        /// Buries bones from an inventory slot for Prayer xp (one every 2 ticks).
        public void Bury(int slot)
        {
            var stack = G.Inventory[slot];
            if (stack.IsEmpty || stack.Def.BuryXp <= 0 || buryCooldown > 0f)
                return;
            buryCooldown = Prayers.BuryTicks * GameTime.TickSeconds;
            G.Inventory.RemoveAt(slot);
            G.Skills.AddXp(SkillIds.Prayer, stack.Def.BuryXp);
            Kneel(0.6f);
            Sfx.Play(Sound.Bury);
            GameLog.Add("You dig a hole in the ground... You bury the bones.");
        }

        /// Drops an inventory slot's items on the ground under you.
        public void Drop(int slot)
        {
            var stack = G.Inventory[slot];
            if (stack.IsEmpty)
                return;
            G.Inventory.RemoveAt(slot);
            G.World.SpawnGroundItem(stack.ItemId, stack.Count, CurrentTile);
            Sfx.Play(Sound.Drop, 0.7f);
        }
    }
}
