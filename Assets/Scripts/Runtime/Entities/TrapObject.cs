using Epacenur.Content;
using Epacenur.Core;
using UnityEngine;

namespace Epacenur.Runtime
{
    /// A hunter's trap on the ground: laid and waiting, then sprung (something caught, or nothing). Check it to take
    /// the catch and the trap back; a trap left too long falls apart.
    public sealed class TrapObject : Interactable
    {
        public ItemDef Trap { get; private set; }
        public CreatureDef Caught { get; private set; }
        public bool Sprung { get; private set; }

        float springAt, collapseAt;
        Transform shown;

        public override string Action => Sprung ? "Check" : "Dismantle";
        public override string DisplayName => Sprung && Caught != null ? Trap.Name + " (" + Caught.Name.ToLowerInvariant() + ")" : Trap.Name;
        public override string Examine => !Sprung ? "It's waiting for something to come along." : Caught != null
            ? "There's " + Making.Article(Caught.Name.ToLowerInvariant()) + " in it!" : "It's been sprung, but there's nothing in it.";

        public static TrapObject Create(Tile tile, ItemDef trap, Transform parent)
        {
            var go = new GameObject(trap.Name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = World.Local(tile);
            var t = go.AddComponent<TrapObject>();
            t.Tile = tile;
            t.Trap = trap;
            t.springAt = Time.time + Random.Range(Hunter.MinWait, Hunter.MaxWait);
            t.collapseAt = float.MaxValue;
            t.Show(trap.TrapKind == "box" ? OpenBox : Snare);
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = new Vector3(0f, 0.2f, 0f);
            box.size = new Vector3(0.6f, 0.4f, 0.7f);
            return t;
        }

        void Show(Model m)
        {
            if (shown != null)
                Destroy(shown.gameObject);
            shown = ModelBuilder.Build(m, transform);
        }

        void Update()
        {
            var g = Game.Instance;
            if (!Sprung && Time.time >= springAt)
            {
                Sprung = true;
                collapseAt = Time.time + Hunter.Collapse;
                var biome = g.World.Map == MapId.Overworld ? g.World.Overworld.BiomeAt(Tile) : Biome.Meadow;
                Caught = Hunter.Catch(ContentDb.Current, Trap.TrapKind, biome, g.Skills.GetLevel(Hunter.Skill), g.Rng);
                if (Caught != null)
                    Show(CaughtModel(Trap.TrapKind, Caught));
                else
                    Show(Trap.TrapKind == "box" ? ShutBox : SprungSnare);
            }
            else if (Sprung && Time.time >= collapseAt)
            {
                GameLog.Add("Your " + Trap.Name.ToLowerInvariant() + " has fallen apart.");
                g.World.Traps.Remove(this);
                Destroy(gameObject);
            }
        }

        static readonly System.Collections.Generic.Dictionary<string, Model> caughtModels = new System.Collections.Generic.Dictionary<string, Model>();

        /// A trap with its catch: a bird sitting in the snare, or the box shut (one model each, shared).
        static Model CaughtModel(string kind, CreatureDef c)
        {
            if (caughtModels.TryGetValue(c.Id, out var m))
                return m;
            m = new Model().Include(kind == "box" ? HunterModule.BoxModel(true) : HunterModule.SnareModel());
            if (kind != "box")
                m.Include(c.Model);
            return caughtModels[c.Id] = m;
        }

        static readonly Model ShutBox = HunterModule.BoxModel(true), OpenBox = HunterModule.BoxModel(false), Snare = HunterModule.SnareModel();

        static readonly Model SprungSnare = new Model().Cylinder(0f, 0.03f, 0f, 0.03f, 0.25f, 0.03f, new Rgb(0.55f, 0.4f, 0.24f)).Turn(90f, 0f, 0f);

        /// Check (or dismantle): the catch, its xp, and the trap back in the inventory.
        public override void Interact()
        {
            var g = Game.Instance;
            if (!g.Inventory.HasRoomFor(Trap.Id))
            {
                GameLog.Add("You don't have room for the trap.");
                return;
            }
            g.Player.Kneel(0.6f);
            if (Caught != null)
            {
                for (int i = 0; i < Caught.Loot.Count; i++)
                {
                    var l = Caught.Loot[i];
                    int n = l.Count + (i == 0 && Caught.ExtraLootMax > 0 ? g.Rng.Next(Caught.ExtraLootMax + 1) : 0);
                    if (g.Inventory.Add(l.ItemId, n) < n)
                        g.World.SpawnGroundItem(l.ItemId, n, g.Player.CurrentTile);
                }
                g.Skills.AddXp(Hunter.Skill, Caught.Xp);
                GameLog.Add("You've caught " + Making.Article(Caught.Name.ToLowerInvariant()) + "!");
            }
            else if (Sprung)
                GameLog.Add("Nothing's been caught.");
            else
                GameLog.Add("You dismantle the trap.");
            g.Inventory.Add(Trap.Id);
            g.World.Traps.Remove(this);
            Destroy(gameObject);
        }
    }
}
