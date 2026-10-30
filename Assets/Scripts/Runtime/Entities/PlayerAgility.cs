using Epacenur.Content;
using Epacenur.Core;
using UnityEngine;

namespace Epacenur.Runtime
{
    /// Crossing agility obstacles, run energy, and being stunned (caught picking a pocket).
    public sealed partial class PlayerController
    {
        sealed class Crossing
        {
            public ObstacleObject Obstacle;
            public Vector3 From, To;
            public float T, Duration;
            public bool Falls;
        }

        Crossing crossing;
        float stunnedUntil;
        bool toldOutOfEnergy;

        /// Part way across an obstacle (nothing else can be done until you're over).
        public bool IsCrossing => crossing != null;
        public bool IsStunned => Time.time < stunnedUntil;

        /// Clicking an obstacle: from its start tile you cross (in tile mode you walk there first).
        public void Cross(ObstacleObject o)
        {
            if (o == null || crossing != null || IsStunned)
                return;
            int level = G.Skills.GetLevel(AgilityModule.Skill);
            if (level < o.Def.Level)
            {
                GameLog.Add("You need an Agility level of " + o.Def.Level + " to attempt this obstacle.");
                return;
            }
            var startAt = World.TileCenter(o.Placement.Start);
            var off = transform.position - startAt;
            off.y = 0f;
            if (off.magnitude > (Mode == ViewMode.FirstPerson ? 1.6f : 0.2f))
            {
                if (Mode == ViewMode.Tile)
                    WalkToObject(o);
                else
                    GameLog.Add("You need to stand at the start of the " + o.Def.Name.ToLowerInvariant() + ".");
                return;
            }
            CancelAction();
            int tiles = o.Placement.Covered.Count + 1;
            crossing = new Crossing
            {
                Obstacle = o, From = startAt, To = World.TileCenter(o.Placement.End),
                Duration = tiles * (o.Def.Look == ObstacleLook.Pipe ? 0.75f : 0.55f) + 0.4f,
                Falls = G.Rng.NextDouble() >= Agility.SuccessChance(o.Def, level),
            };
            cc.enabled = false;  // carried across by hand
        }

        /// Moves you along an obstacle. Returns true while crossing.
        bool UpdateCrossing(float dt)
        {
            var c = crossing;
            if (c == null)
                return false;
            if (c.Obstacle == null)
            {
                EndCrossing(c.From);  // it was unloaded under you
                return false;
            }
            c.T += dt;
            float f = Mathf.Clamp01(c.T / c.Duration);
            var def = c.Obstacle.Def;
            if (c.Falls && f >= 0.5f)
            {
                // Off you go, back to the start, a few hitpoints down.
                EndCrossing(c.From);
                GameLog.Add(def.Fail);
                int damage = 1 + G.Rng.Next(def.FailDamage);
                G.HurtPlayer(damage);
                G.Laps.Crossed(null, -1);
                return true;
            }
            var flat = Vector3.Lerp(c.From, c.To, f);
            float y = c.Obstacle.Lift * Mathf.Clamp01(Mathf.Min(f, 1f - f) * 8f);
            if (def.Look == ObstacleLook.Wall)
                y = Mathf.Sin(f * Mathf.PI) * 1.2f;
            else if (def.Look == ObstacleLook.Stones)
                y = Mathf.Abs(Mathf.Sin(f * Mathf.PI * (c.Obstacle.Placement.Covered.Count + 1))) * 0.35f;
            else if (def.Look == ObstacleLook.Pipe)
                y = -Mathf.Sin(f * Mathf.PI) * 0.6f;  // crouched low through it
            transform.position = flat + Vector3.up * y;
            var dir = c.To - c.From;
            dir.y = 0f;
            transform.rotation = Quaternion.LookRotation(dir);
            if (f < 1f)
                return true;
            EndCrossing(c.To);
            GameLog.Add(def.Success);
            G.Skills.AddXp(AgilityModule.Skill, def.Xp);
            if (G.Laps.Crossed(c.Obstacle.Course.Id, c.Obstacle.Placement.Index))
            {
                G.Skills.AddXp(AgilityModule.Skill, c.Obstacle.Course.LapXp);
                GameLog.Add("You complete a lap of the " + c.Obstacle.Course.Name.ToLowerInvariant() + "! (" + c.Obstacle.Course.LapXp + " bonus xp)");
            }
            return true;
        }

        void EndCrossing(Vector3 at)
        {
            crossing = null;
            transform.position = at;
            lastPosition = at;
            cc.enabled = true;
        }

        /// Caught: hit and stunned, unable to do anything for a few seconds.
        public void Stun(float seconds)
        {
            CancelAction();
            stunnedUntil = Time.time + seconds;
        }

        /// Running uses run energy; walking or standing still brings it back (faster with Agility).
        void UpdateEnergy(Vector3 move, float dt)
        {
            move.y = 0f;
            bool running = IsRunning && move.sqrMagnitude > 1e-8f;
            if (running)
            {
                G.Energy.Run(dt);
                if (!G.Energy.CanRun && !toldOutOfEnergy)
                {
                    GameLog.Add("You're out of run energy. Walk for a while to get it back.");
                    toldOutOfEnergy = true;
                }
            }
            else
            {
                G.Energy.Rest(dt, G.Skills.GetLevel(AgilityModule.Skill));
                if (G.Energy.Value > 5)
                    toldOutOfEnergy = false;
            }
        }
    }
}
