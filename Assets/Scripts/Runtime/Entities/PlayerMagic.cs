using Epacenur.Core;
using UnityEngine;

namespace Epacenur.Runtime
{
    /// Casting from the Magic tab: picking a combat spell to autocast, teleporting, and alchemy (pick the
    /// spell, then the item in the inventory).
    public sealed partial class PlayerController
    {
        /// An alchemy spell waiting for you to pick an item in the inventory, or null.
        public SpellDef SelectedSpell { get; private set; }
        float castCooldown;
        const int AlchTicks = 5;

        /// Can't home teleport for this long after being hit.
        const float CombatSeconds = 10f;

        /// A click on a spell in the Magic tab.
        public void CastSpell(SpellDef spell)
        {
            CancelUse();
            if (G.Skills.GetLevel(SkillIds.Magic) < spell.Level)
            {
                GameLog.Add("You need a Magic level of " + spell.Level + " to cast " + spell.Name + ".");
                return;
            }
            switch (spell.Kind)
            {
                case SpellKind.Combat:
                    G.Autocast = G.Autocast == spell ? null : spell;
                    GameLog.Add(G.Autocast != null ? "You will now cast " + spell.Name + " when you attack." : "Autocast off: you'll fight with your weapon.");
                    break;
                case SpellKind.HomeTeleport:
                    if (Time.time - LastHurtTime < CombatSeconds)
                    {
                        GameLog.Add("You can't use that in combat.");
                        return;
                    }
                    CancelAction();
                    Activity = new TeleportActivity(spell, transform.position);
                    GameLog.Add("You begin to channel the spell. Don't move...");
                    break;
                case SpellKind.TownTeleport:
                    string problem = Magic.Problem(spell, G.Skills, G.Inventory, G.Equipment);
                    if (problem != null)
                    {
                        GameLog.Add(problem);
                        return;
                    }
                    Magic.UseRunes(spell, G.Inventory, G.Equipment);
                    G.Skills.AddXp(SkillIds.Magic, spell.Xp);
                    G.World.SurfacePlace(CurrentTile, out MapId from, out Tile at);
                    TeleportTo(spell, from == MapId.Overworld ? G.World.NearestTown(at) : G.World.MainSpawn);
                    break;
                case SpellKind.Alchemy:
                    G.Hud.Panel.ShowInventory();  // (this puts down anything picked, so first)
                    SelectedSpell = spell;
                    break;
            }
        }

        /// The picked alchemy spell on an inventory item.
        public void CastOnSlot(int slot)
        {
            var spell = SelectedSpell;
            SelectedSpell = null;
            if (spell == null || castCooldown > 0f)
                return;
            string name = G.Inventory[slot].IsEmpty ? null : G.Inventory[slot].Def.Name;
            int coins = Magic.Alch(spell, G.Skills, G.Inventory, G.Equipment, slot, out string problem);
            if (coins < 0)
            {
                GameLog.Add(problem);
                return;
            }
            castCooldown = AlchTicks * GameTime.TickSeconds;
            attackAnim = 0.4f;
            Projectile.Spell(transform.position + Vector3.up * 1.2f + transform.forward * 0.4f, transform, 2.2f, spell.Colour);
            Sfx.Play(Sound.Spell, 0.8f);
            GameLog.Add("The " + name.ToLowerInvariant() + " turns into " + coins + " coin" + (coins == 1 ? "" : "s") + ".");
            G.Hud.Panel.ShowMagic();
        }

        void UpdateMagic(float dt)
        {
            if (castCooldown > 0f)
                castCooldown -= dt;
        }

        /// Off you go (from the dev plot too: back to the overworld).
        void TeleportTo(SpellDef spell, Tile tile)
        {
            Sfx.Play(Sound.Teleport);
            G.World.Travel(MapId.Overworld, tile);
            GameLog.Add("You teleport to " + (spell.Kind == SpellKind.HomeTeleport ? "Epacenur" : "the town") + ".");
        }

        /// Home Teleport's long cast: stand still until it's done.
        sealed class TeleportActivity : Activity
        {
            readonly SpellDef spell;
            readonly Vector3 start;

            public TeleportActivity(SpellDef spell, Vector3 start) : base(spell.CastTicks)
            {
                this.spell = spell;
                this.start = start;
            }

            public override string Status => "Casting " + spell.Name + "...";
            public override Transform Target => null;
            public override ArmPose Pose => ArmPose.HoldOut;
            protected override int Interval => spell.CastTicks;

            public override bool StillValid(PlayerController p)
            {
                var d = p.transform.position - start;
                d.y = 0f;
                if (d.sqrMagnitude < 0.04f && Time.time - p.LastHurtTime >= 0.5f)
                    return true;
                GameLog.Add("Your teleport is interrupted.");
                return false;
            }

            protected override bool Step(PlayerController p)
            {
                p.TeleportTo(spell, G.World.MainSpawn);
                return false;
            }
        }
    }
}
