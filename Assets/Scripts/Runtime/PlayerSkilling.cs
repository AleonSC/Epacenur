using Epacenur.Core;
using UnityEngine;

namespace Epacenur.Runtime
{
    /// Firemaking, fishing and cooking (the rest of the player is in PlayerController.cs).
    public sealed partial class PlayerController
    {
        GroundItem lightTarget;       // logs on the ground being lit, or walked to (tile mode)
        bool lighting, walkingToLight;
        float lightTimer;

        FishingSpot fishTarget;
        Tile fishSpotTile;
        bool fishing;
        float fishTimer;

        Fire cookFire;
        int pendingCook;   // the raw item to cook first, chosen from the inventory
        bool cooking;
        float cookTimer;

        public bool IsCooking => cooking;
        public bool IsLighting => lighting;
        public bool IsFishing => fishing;
        public bool HasTinderbox => G.HasTool(ItemDefs.Tinderbox.Id);

        void CancelSkilling()
        {
            lightTarget = null;
            lighting = walkingToLight = false;
            fishTarget = null;
            fishing = false;
            cookFire = null;
            cooking = false;
            pendingCook = 0;
        }

        void UpdateSkilling(float dt)
        {
            if (walkingToLight && pathIndex >= path.Count)
            {
                walkingToLight = false;
                if (lightTarget != null && CurrentTile == lightTarget.Tile)
                    BeginLighting(lightTarget);
                else if (lightTarget != null)
                    GameLog.Add("I can't reach that!");
            }
            if (lighting)
                UpdateLighting(dt);
            if (fishing)
                UpdateFishing(dt);
            if (cooking)
                UpdateCooking(dt);
        }

        // ---- Firemaking ----

        /// "Light" on logs in the inventory: they go down on your tile and you light them there.
        public void LightFromInventory(int slot)
        {
            var stack = G.Inventory[slot];
            var burn = stack.IsEmpty ? null : FiremakingRules.For(stack.ItemId);
            if (burn == null || !CanLight(burn, CurrentTile))
                return;
            CancelAction();
            G.Inventory.Set(slot, default(ItemStack));
            var logs = G.World.SpawnGroundItem(stack.ItemId, 1, CurrentTile);
            BeginLighting(logs);
        }

        /// "Light" on logs lying on the ground: walk onto their tile (tile mode) and light them.
        public void LightGroundItem(GroundItem item)
        {
            if (item == null)
                return;
            var burn = FiremakingRules.For(item.ItemId);
            if (burn == null || !CanLight(burn, item.Tile))
                return;
            CancelAction();
            if (Mode == ViewMode.FirstPerson || CurrentTile == item.Tile)
            {
                BeginLighting(item);
                return;
            }
            lightTarget = item;
            walkingToLight = true;
            SetPath(Pathfinder.PathTo(G.World.Grid, StartTile(), item.Tile));
        }

        bool CanLight(LogBurnDef burn, Tile tile)
        {
            if (!HasTinderbox)
            {
                GameLog.Add("You need a tinderbox to light a fire.");
                return false;
            }
            if (G.Skills.GetLevel(SkillId.Firemaking) < burn.Level)
            {
                GameLog.Add("You need a Firemaking level of " + burn.Level + " to burn these logs.");
                return false;
            }
            foreach (var f in G.World.Fires)
                if (f.Tile == tile)
                {
                    GameLog.Add("You can't light a fire here.");
                    return false;
                }
            return true;
        }

        void BeginLighting(GroundItem logs)
        {
            lightTarget = logs;
            lighting = true;
            lightTimer = (FiremakingRules.AttemptTicks - 1) * WoodcuttingRules.TickSeconds;  // first try one tick in
            GameLog.Add("You attempt to light the logs.");
        }

        void UpdateLighting(float dt)
        {
            var logs = lightTarget;
            if (logs == null)
            {
                lighting = false;  // someone took them, or they vanished
                return;
            }
            Face(logs.transform.position - transform.position, dt);
            lightTimer += dt;
            float interval = FiremakingRules.AttemptTicks * WoodcuttingRules.TickSeconds;
            if (lightTimer < interval)
                return;
            lightTimer -= interval;
            var burn = FiremakingRules.For(logs.ItemId);
            if (G.Rng.NextDouble() >= FiremakingRules.LightChance(burn, G.Skills.GetLevel(SkillId.Firemaking)))
                return;

            Tile tile = logs.Tile;
            G.World.RemoveGroundItem(logs);
            G.World.AddFire(tile, Random.Range(burn.MinBurnSeconds, burn.MaxBurnSeconds));
            G.Skills.AddXp(SkillId.Firemaking, burn.Xp);
            GameLog.Add("The fire catches and the logs begin to burn.");
            lighting = false;
            lightTarget = null;
            // Step off the fire, west first as in RuneScape (tile mode; in first person you just walk off).
            if (Mode == ViewMode.Tile && CurrentTile == tile)
                foreach (var d in new[] { new Tile(-1, 0), new Tile(1, 0), new Tile(0, -1), new Tile(0, 1) })
                {
                    var next = new Tile(tile.X + d.X, tile.Z + d.Z);
                    if (G.World.Grid.IsWalkable(next))
                    {
                        path.Clear();
                        pathIndex = 0;
                        path.Add(next);
                        break;
                    }
                }
        }

        // ---- Cooking ----

        /// "Cook" on raw food in the inventory: use the nearest fire (walking to it in tile mode).
        public void CookFromInventory(int rawItemId)
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
            {
                WalkToObject(nearest);   // cooks when you get there (Fire.Interact)
                pendingCook = rawItemId;
                return;
            }
            pendingCook = rawItemId;
            CookOn(nearest);
        }

        /// Cooking on a fire: in tile mode everything raw you carry, one every 4 ticks; in first
        /// person one item per click.
        public void CookOn(Fire fire)
        {
            if (FindRawFood() < 0)
            {
                GameLog.Add("You have nothing to cook.");
                pendingCook = 0;
                return;
            }
            if (Mode == ViewMode.FirstPerson)
            {
                if (swingCooldown > 0f)
                    return;
                swingCooldown = CookingRules.FirstPersonCookTicks * WoodcuttingRules.TickSeconds;
                SwingT = 1f;
                CookOne();
                return;
            }
            int keep = pendingCook;
            CancelSkilling();
            pendingCook = keep;
            cookFire = fire;
            cooking = true;
            cookTimer = (CookingRules.AttemptTicks - 1) * WoodcuttingRules.TickSeconds;  // first one a tick in
        }

        void UpdateCooking(float dt)
        {
            var fire = cookFire;
            if (fire == null || !CurrentTile.IsCardinalNeighbour(fire.Tile))
            {
                if (fire == null)
                    GameLog.Add("The fire has gone out.");
                cooking = false;
                cookFire = null;
                return;
            }
            Face(fire.transform.position - transform.position, dt);
            cookTimer += dt;
            float interval = CookingRules.AttemptTicks * WoodcuttingRules.TickSeconds;
            if (cookTimer < interval)
                return;
            cookTimer -= interval;
            if (!CookOne())
            {
                cooking = false;
                cookFire = null;
            }
        }

        /// The inventory slot of the next raw item to cook (the chosen kind first), or -1.
        int FindRawFood()
        {
            int any = -1;
            for (int i = 0; i < Inventory.Size; i++)
            {
                var s = G.Inventory[i];
                if (s.IsEmpty || CookingRules.For(s.ItemId) == null)
                    continue;
                if (s.ItemId == pendingCook)
                    return i;
                if (any < 0)
                    any = i;
            }
            return any;
        }

        /// Cooks (or burns) one raw item in its own slot. Returns whether there's more to cook.
        bool CookOne()
        {
            int slot = FindRawFood();
            if (slot < 0)
                return false;
            var food = CookingRules.For(G.Inventory[slot].ItemId);
            if (G.Skills.GetLevel(SkillId.Cooking) < food.Level)
            {
                GameLog.Add("You need a Cooking level of " + food.Level + " to cook this.");
                return false;
            }
            string name = ItemDefs.Get(food.CookedItemId).Name.ToLowerInvariant();
            if (G.Rng.NextDouble() < CookingRules.SuccessChance(food, G.Skills.GetLevel(SkillId.Cooking)))
            {
                G.Inventory.Set(slot, new ItemStack { ItemId = food.CookedItemId, Count = 1 });
                G.Skills.AddXp(SkillId.Cooking, food.Xp);
                GameLog.Add("You successfully cook some " + name + ".");
            }
            else
            {
                G.Inventory.Set(slot, new ItemStack { ItemId = food.BurntItemId, Count = 1 });
                GameLog.Add("You accidentally burn the " + name + ".");
            }
            return FindRawFood() >= 0;
        }

        // ---- Fishing ----

        /// Fishing a spot: in tile mode you keep fishing (you're already next to it); in first person
        /// each click is one cast.
        public void FishAt(FishingSpot spot)
        {
            if (!CanFish(spot))
                return;
            if (Mode == ViewMode.FirstPerson)
            {
                if (swingCooldown > 0f)
                    return;
                swingCooldown = FishingRules.FirstPersonCastTicks * WoodcuttingRules.TickSeconds;
                SwingT = 1f;
                CatchAttempt(spot);
                return;
            }
            fishTarget = spot;
            fishSpotTile = spot.Tile;
            fishing = true;
            fishTimer = (FishingRules.AttemptTicks - 1) * WoodcuttingRules.TickSeconds;
            GameLog.Add("You cast out your net...");
        }

        bool CanFish(FishingSpot spot)
        {
            if (!G.HasTool(spot.Def.ToolItemId))
            {
                GameLog.Add("You need a " + ItemDefs.Get(spot.Def.ToolItemId).Name.ToLowerInvariant() + " to fish here.");
                return false;
            }
            int min = FishingRules.MinLevel(spot.Def);
            if (G.Skills.GetLevel(SkillId.Fishing) < min)
            {
                GameLog.Add("You need a Fishing level of " + min + " to fish here.");
                return false;
            }
            if (G.Inventory.IsFull)
            {
                GameLog.Add("You can't carry any more fish.");
                return false;
            }
            return true;
        }

        void UpdateFishing(float dt)
        {
            var spot = fishTarget;
            if (spot == null || spot.Tile != fishSpotTile || !CurrentTile.IsCardinalNeighbour(spot.Tile))
            {
                if (spot != null && spot.Tile != fishSpotTile)
                    GameLog.Add("The fishing spot has moved.");
                fishing = false;
                fishTarget = null;
                return;
            }
            Face(spot.transform.position - transform.position, dt);
            fishTimer += dt;
            float interval = FishingRules.AttemptTicks * WoodcuttingRules.TickSeconds;
            if (fishTimer < interval)
                return;
            fishTimer -= interval;
            if (!CatchAttempt(spot))
            {
                fishing = false;
                fishTarget = null;
            }
        }

        /// One attempt. Returns whether fishing can carry on (false once the inventory is full).
        bool CatchAttempt(FishingSpot spot)
        {
            if (G.Inventory.IsFull)
            {
                GameLog.Add("You can't carry any more fish.");
                return false;
            }
            var fish = FishingRules.Roll(spot.Def, G.Skills.GetLevel(SkillId.Fishing), G.Rng);
            if (fish == null)
                return true;
            G.Inventory.Add(fish.ItemId);
            G.Skills.AddXp(SkillId.Fishing, fish.Xp);
            GameLog.Add("You catch some " + ItemDefs.Get(fish.ItemId).Name.Substring(4) + ".");  // "Raw shrimps" -> "shrimps"
            if (G.Inventory.IsFull)
            {
                GameLog.Add("You can't carry any more fish.");
                return false;
            }
            return true;
        }
    }
}
