using Epacenur.Content;
using Epacenur.Core;
using UnityEngine;

namespace Epacenur.Runtime
{
    /// Picking pockets: walk up to someone and keep at it until you're caught (hit and stunned) or full.
    public sealed partial class PlayerController
    {
        Npc pickTarget;
        float nextPickAt;

        /// The person whose pocket you're picking (or walking up to), or null.
        public Npc PickTarget => pickTarget;

        public void StartPickpocket(Npc npc, bool firstPerson)
        {
            if (npc == null || npc.IsDead || npc.Def.Pickpocket == null || IsStunned)
                return;
            if (firstPerson)
            {
                if (FlatDistance(npc) > FirstPersonReach)
                    GameLog.Add("You need to get closer.");
                else if (Time.time >= nextPickAt)
                    AttemptPick(npc);
                return;
            }
            if (npc == pickTarget)
                return;
            CancelAction();
            pickTarget = npc;
        }

        /// Tile mode: walk next to them, then try every couple of ticks.
        Vector3 UpdatePickpocket(float dt)
        {
            var npc = pickTarget;
            if (npc == null || npc.IsDead)
            {
                CancelAction();
                return Vector3.zero;
            }
            Vector3 flat = transform.position - World.TileCenter(CurrentTile);
            flat.y = 0f;
            bool inPlace = flat.sqrMagnitude < 0.01f && CurrentTile.IsCardinalNeighbour(npc.Tile);
            if (!inPlace)
            {
                if (pathIndex >= path.Count || !path[path.Count - 1].IsCardinalNeighbour(npc.Tile))
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
            npc.Hold(2f);  // they stand still while you're at it
            Face(npc.transform.position - transform.position, dt);
            if (Time.time >= nextPickAt && !AttemptPick(npc))
                CancelAction();
            return Vector3.zero;
        }

        /// One try. Returns whether to keep going.
        bool AttemptPick(Npc npc)
        {
            var pp = npc.Def.Pickpocket;
            int level = G.Level(ThievingModule.Skill);
            string who = "the " + npc.Def.Name.ToLowerInvariant();
            if (level < pp.Level)
            {
                GameLog.Add("You need a Thieving level of " + pp.Level + " to pick " + who + "'s pocket.");
                return false;
            }
            if (G.Inventory.IsFull)
            {
                GameLog.Add("You don't have enough inventory space.");
                return false;
            }
            nextPickAt = Time.time + Thieving.PickTicks * GameTime.TickSeconds;
            SwingT = 1f;
            if (G.Rng.NextDouble() < Thieving.Chance(pp, level))
            {
                if (pp.Loot.Roll(G.Rng, out string item, out int count) && item != null)
                    G.Inventory.Add(item, count);
                G.Skills.AddXp(ThievingModule.Skill, pp.Xp);
                Sfx.Play(Sound.Coins, 0.7f);
                GameLog.Add("You pick " + who + "'s pocket.");
                return true;
            }
            GameLog.Add("You fail to pick " + who + "'s pocket. \"What do you think you're doing?\"");
            npc.Catch(transform.position);
            npc.Hold(Thieving.StunSeconds);
            G.HurtPlayer(1 + G.Rng.Next(pp.StunDamage));
            Stun(Thieving.StunSeconds);
            GameLog.Add("You've been stunned!");
            return false;
        }
    }
}
