using Epacenur.Content;
using Epacenur.Core;
using UnityEngine;

namespace Epacenur.Runtime
{
    /// Building (Construction): placing a base's pieces from the build menu, with a ghost of the piece where it
    /// would go (green, or red while something's wrong), and taking pieces down again. A first foundation can go
    /// anywhere, at any angle (R turns it); everything after snaps to its base's grid.
    public sealed partial class PlayerController
    {
        public const float BuildReach = 12f;  // how far from you a piece can go (map units)

        /// The piece being placed, or null.
        public PieceDef Building { get; private set; }
        /// What's wrong with where it would go now, or null.
        public string BuildProblem { get; private set; }

        int buildFacing;      // furniture: quarter turns
        float buildAngle;     // a new base's angle (degrees)
        Transform ghost;
        string ghostFor;      // the piece and facing the ghost was made for
        bool ghostValid;

        struct Placement
        {
            public bool Valid, NewBase;
            public Structure Base;
            public double X, Z, Floor;  // a new base: where its first cell's centre goes, and its floor
            public int I, J, Side;
        }

        Placement plan;

        static readonly Color GhostGood = new Color(0.35f, 0.9f, 0.4f), GhostBad = new Color(0.95f, 0.3f, 0.25f);

        bool HasHammer => ToolBelt.Find(G.ToolBelt, G.Inventory, "hammer", G.Skills) != null;

        public void StartBuilding(PieceDef piece)
        {
            if (!HasHammer)
            {
                GameLog.Add("You need a hammer to build.");
                return;
            }
            CancelAction();
            Building = piece;
            buildFacing = 0;
        }

        public void StopBuilding()
        {
            Building = null;
            BuildProblem = null;
            if (ghost != null)
                Destroy(ghost.gameObject);
            ghost = null;
            ghostFor = null;
        }

        /// Each frame while building: where the piece would go (from `ray`), its ghost there, R to turn it, and a
        /// click to place it (`cancel` stops).
        void UpdateBuilding(Ray ray, bool overUi, bool click, bool cancel)
        {
            if (cancel || G.World.Map != MapId.Overworld)
            {
                StopBuilding();
                return;
            }
            if (Input.GetKeyDown(KeyCode.R) && !Hud.Typing)
            {
                bool back = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                buildAngle = (buildAngle + (back ? -15f : 15f) + 360f) % 360f;
                buildFacing = (buildFacing + (back ? 3 : 1)) % 4;
            }
            plan = overUi ? default(Placement) : Plan(ray);
            ShowGhost();
            if (click && !overUi)
                Place();
        }

        /// Where the piece would go, and what (if anything) is wrong with it there.
        Placement Plan(Ray ray)
        {
            var p = new Placement();
            BuildProblem = "Point at where it should go.";
            if (!Physics.Raycast(ray, out var hit, 200f))
                return p;
            double x = hit.point.x + World.Origin.X, z = hit.point.z + World.Origin.Z;
            var bases = G.World.Bases;
            var def = Building;
            switch (def.Kind)
            {
                case PieceKind.Foundation:
                {
                    var s = bases.Near(x, z, 0.75);
                    if (s == null)
                    {
                        // A new base, wherever you point.
                        p.NewBase = true;
                        p.X = x;
                        p.Z = z;
                        BuildProblem = bases.NewBaseProblem(x, z, buildAngle, out p.Floor);
                        break;
                    }
                    s.ToLocal(x, z, out double fi, out double fj);
                    int i = Mathf.RoundToInt((float)fi), j = Mathf.RoundToInt((float)fj);
                    if (s.HasFoundation(i, j))
                    {
                        // Pointing at a floor: the next cell on from it, towards its nearest edge.
                        double oi = fi - i, oj = fj - j;
                        if (System.Math.Abs(oi) >= System.Math.Abs(oj)) i += oi > 0 ? 1 : -1;
                        else j += oj > 0 ? 1 : -1;
                    }
                    p.Base = s;
                    p.I = i;
                    p.J = j;
                    BuildProblem = bases.FoundationProblem(s, i, j);
                    break;
                }
                case PieceKind.Wall:
                case PieceKind.Doorway:
                case PieceKind.Window:
                {
                    var s = bases.Near(x, z, 0.6);
                    if (s == null)
                    {
                        BuildProblem = "That goes along the edge of a foundation.";
                        return p;
                    }
                    s.ToLocal(x, z, out double fi, out double fj);
                    int ri = Mathf.RoundToInt((float)fi), rj = Mathf.RoundToInt((float)fj);
                    double oi = fi - ri, oj = fj - rj;
                    p.Base = s;
                    if (System.Math.Abs(oi) > System.Math.Abs(oj))
                    {
                        p.Side = 1;
                        p.I = oi > 0 ? ri : ri - 1;
                        p.J = rj;
                    }
                    else
                    {
                        p.Side = 0;
                        p.I = ri;
                        p.J = oj > 0 ? rj : rj - 1;
                    }
                    BuildProblem = BaseSet.EdgeProblem(s, p.I, p.J, p.Side);
                    break;
                }
                default:
                {
                    var s = bases.Near(x, z, 0.05);
                    if (s == null)
                    {
                        BuildProblem = def.Kind == PieceKind.Roof ? "A roof goes over a foundation." : "That goes on a foundation.";
                        return p;
                    }
                    s.ToLocal(x, z, out double fi, out double fj);
                    p.Base = s;
                    p.I = Mathf.RoundToInt((float)fi);
                    p.J = Mathf.RoundToInt((float)fj);
                    BuildProblem = def.Kind == PieceKind.Roof ? BaseSet.RoofProblem(s, p.I, p.J) : BaseSet.FurnitureProblem(s, p.I, p.J);
                    if (BuildProblem == null && def.Kind == PieceKind.Furniture && CurrentTile == BaseSet.CellTile(s, p.I, p.J))
                        BuildProblem = "You're standing in the way.";
                    break;
                }
            }
            if (BuildProblem == null)
            {
                Target(p, out double tx, out double tz, out _, out _);
                double dx = tx - (transform.position.x + World.Origin.X), dz = tz - (transform.position.z + World.Origin.Z);
                string missing = BaseSet.Missing(def, G.Inventory);
                if (dx * dx + dz * dz > BuildReach * BuildReach)
                    BuildProblem = "That's too far away.";
                else if (G.Skills.GetLevel(ConstructionModule.Skill) < def.Level)
                    BuildProblem = "You need a Construction level of " + def.Level + " to build that.";
                else if (missing != null)
                    BuildProblem = "You need " + missing + ".";
                else if (!HasHammer)
                    BuildProblem = "You need a hammer to build.";
            }
            p.Valid = BuildProblem == null;
            return p;
        }

        /// Where a placement is (map space: the cell's centre, its floor) and which way it's turned.
        void Target(Placement p, out double x, out double z, out double y, out float angle)
        {
            if (p.NewBase)
            {
                x = p.X;
                z = p.Z;
                y = p.Floor;
                angle = buildAngle;
                return;
            }
            p.Base.ToMap(p.I, p.J, out x, out z);
            y = p.Base.Floor;
            angle = (float)p.Base.Angle + (Building.Kind == PieceKind.Furniture ? buildFacing * 90f
                : Building.Kind == PieceKind.Wall || Building.Kind == PieceKind.Doorway || Building.Kind == PieceKind.Window ? p.Side * 90f : 0f);
        }

        /// The ghost of the piece where it would go: green if it can, red if not; hidden when it has nowhere to go.
        void ShowGhost()
        {
            if (!plan.NewBase && plan.Base == null)
            {
                if (ghost != null)
                    ghost.gameObject.SetActive(false);
                return;
            }
            string key = Building.Id + (Building.Kind == PieceKind.Furniture ? "/" + buildFacing : "");
            if (ghost == null || ghostFor != key)
            {
                if (ghost != null)
                    Destroy(ghost.gameObject);
                ghost = MakeGhost(Building);
                ghostFor = key;
                ghostValid = !plan.Valid;  // forces the colour below
            }
            ghost.gameObject.SetActive(true);
            Target(plan, out double x, out double z, out double y, out float angle);
            ghost.position = new Vector3((float)x - World.Origin.X, (float)y, (float)z - World.Origin.Z);
            ghost.rotation = Quaternion.Euler(0f, angle, 0f);
            if (ghostValid != plan.Valid)
            {
                ghostValid = plan.Valid;
                var mat = Visuals.Mat(plan.Valid ? GhostGood : GhostBad);
                foreach (var r in ghost.GetComponentsInChildren<Renderer>())
                {
                    r.sharedMaterial = mat;
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
            }
        }

        /// The piece's shapes with no colliders, out of the way of clicks (Ignore Raycast), its origin at the
        /// cell's centre on the floor.
        static Transform MakeGhost(PieceDef def)
        {
            var root = new GameObject("Ghost " + def.Name).transform;
            switch (def.Kind)
            {
                case PieceKind.Foundation:
                    BaseObject.Foundation(root, Vector3.zero, def, GhostGood, false);
                    break;
                case PieceKind.Roof:
                    BaseObject.Roof(root, Vector3.zero, def, GhostGood, false, true, true, true, true);
                    break;
                case PieceKind.Furniture:
                    var model = def.Use == FurnitureUse.Station ? ContentDb.Current.Station(def.Station).Model : def.Model;
                    ModelBuilder.Build(model, root, "Model");
                    break;
                default:
                    BaseObject.Edge(root, def, GhostGood, false);
                    break;
            }
            foreach (var t in root.GetComponentsInChildren<Transform>())
                t.gameObject.layer = 2;
            return root;
        }

        /// Puts the piece where the plan says: pays for it, gives the xp, and builds it.
        void Place()
        {
            if (!plan.Valid)
            {
                if (BuildProblem != null)
                    GameLog.Add(BuildProblem);
                Sfx.Play(Sound.Error, 0.6f);
                return;
            }
            var def = Building;
            var s = plan.Base;
            int i = plan.I, j = plan.J;
            if (plan.NewBase)
            {
                s = G.World.Bases.Create(plan.X, plan.Z, buildAngle, plan.Floor);
                i = j = 0;
            }
            long cell = Structure.Key(i, j);
            switch (def.Kind)
            {
                case PieceKind.Foundation: s.Foundations[cell] = def.Id; break;
                case PieceKind.Roof: s.Roofs[cell] = def.Id; break;
                case PieceKind.Furniture:
                    s.Furniture[cell] = new Furnishing { Piece = def.Id, Facing = buildFacing,
                        Store = def.Use == FurnitureUse.Chest ? new Bank(ConstructionModule.ChestSlots) : null };
                    break;
                default: s.Edges[Structure.EdgeKey(i, j, plan.Side)] = def.Id; break;
            }
            foreach (var input in def.Inputs)
                G.Inventory.Remove(input.ItemId, input.Count);
            G.Skills.AddXp(ConstructionModule.Skill, def.Xp);
            SwingT = 1f;
            Sfx.Play(Sound.Build);
            G.World.BaseChanged(s);
            // Standing where a floor now is: step up onto it.
            if (def.Kind == PieceKind.Foundation)
                foreach (var w in BaseSet.CoveredTiles(s, i, j))
                    if (w == CurrentTile)
                    {
                        Teleport(BaseSet.CellTile(s, i, j));
                        break;
                    }
        }

        /// "Remove" on a piece (with a hammer): takes it down and gives back half of what it cost.
        void RemovePiece(BasePart part)
        {
            if (part == null || part.Base == null)
                return;
            if (!HasHammer)
            {
                GameLog.Add("You need a hammer to take that down.");
                return;
            }
            var at = part.GetComponent<Interactable>();
            var off = at != null ? World.TileCenter(at.Tile) - transform.position : Vector3.zero;
            off.y = 0f;
            if (off.magnitude > BuildReach)
            {
                GameLog.Add("That's too far away.");
                return;
            }
            var s = part.Base;
            string problem = null;
            if (part.Kind == PieceKind.Foundation)
            {
                Structure.Unkey(part.Key, out int i, out int j);
                problem = BaseSet.RemoveFoundationProblem(s, i, j);
            }
            else if (part.Kind == PieceKind.Furniture && s.Furniture.TryGetValue(part.Key, out var f) && f.Store != null && f.Store.UsedSlots > 0)
                problem = "Empty the chest first.";
            if (problem != null)
            {
                GameLog.Add(problem);
                return;
            }
            switch (part.Kind)
            {
                case PieceKind.Foundation: s.Foundations.Remove(part.Key); break;
                case PieceKind.Roof: s.Roofs.Remove(part.Key); break;
                case PieceKind.Furniture: s.Furniture.Remove(part.Key); break;
                default: s.Edges.Remove(part.Key); break;
            }
            foreach (var input in part.Def.Inputs)
            {
                int back = input.Count / 2;
                int kept = back > 0 ? G.Inventory.Add(input.ItemId, back) : 0;
                if (back > kept)
                    G.World.SpawnGroundItem(input.ItemId, back - kept, CurrentTile);
            }
            SwingT = 1f;
            Sfx.Play(Sound.Remove);
            GameLog.Add("You take down the " + part.Def.Name.ToLowerInvariant() + ".");
            G.World.BaseChanged(s);
        }
    }
}
