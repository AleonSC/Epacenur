using System.Collections.Generic;
using Epacenur.Core;
using UnityEngine;

namespace Epacenur.Runtime
{
    /// The player's body: built from primitives, with worn items on it and the weapon or tool in hand,
    /// and its animations (walking, swinging, kneeling).
    public sealed partial class PlayerController
    {
        Transform model, armPivot, armLeft, legLeft, legRight, cape, hand, shieldArm;
        readonly Dictionary<string, Transform> heldModels = new Dictionary<string, Transform>();
        readonly List<GameObject> wornParts = new List<GameObject>();
        Vector3 lastPosition;
        float walkPhase, speed, chopAnim, attackAnim, kneelUntil;
        float runBlend;  // 0 walking .. 1 running (eases between them)

        /// Kneel for a moment (praying at an altar, burying bones).
        public void Kneel(float seconds) => kneelUntil = Time.time + seconds;

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

            // Where weapons and tools go (Bone.Hand) and shields (Bone.ShieldArm).
            hand = Holder("Hand", armPivot, new Vector3(0f, -0.6f, 0.05f));
            shieldArm = Holder("Shield arm", armLeft, new Vector3(-0.1f, -0.4f, 0.05f));
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

        Transform BoneTransform(Bone bone)
        {
            switch (bone)
            {
                case Bone.LegLeft: return legLeft;
                case Bone.LegRight: return legRight;
                case Bone.ArmLeft: return armLeft;
                case Bone.ArmRight: return armPivot;
                case Bone.Hand: return hand;
                case Bone.ShieldArm: return shieldArm;
                default: return model;
            }
        }

        /// Rebuilds what shows on the body from the worn equipment: armour parts, and the shield on the arm.
        void RebuildWorn()
        {
            foreach (var go in wornParts)
                Destroy(go);
            wornParts.Clear();
            foreach (EquipSlot slot in System.Enum.GetValues(typeof(EquipSlot)))
            {
                if (slot == EquipSlot.None || !G.Equipment.IsWearing(slot))
                    continue;
                var def = G.Equipment[slot].Def;
                foreach (var part in def.Worn)
                    wornParts.Add(ModelBuilder.Build(part.Model, BoneTransform(part.Bone), def.Name).gameObject);
                if (slot == EquipSlot.Shield && def.HeldModel != null)
                    wornParts.Add(ModelBuilder.Build(def.HeldModel, shieldArm, def.Name).gameObject);
            }
            ApplyShadowMode(model);
        }

        /// The weapon or tool in your hand: the activity's tool while you use one, otherwise what you wield.
        void UpdateHeldItems()
        {
            ItemDef shown = Activity?.Tool;
            if (shown == null && G.Equipment.IsWearing(EquipSlot.Weapon))
                shown = G.Equipment[EquipSlot.Weapon].Def;
            if (shown != null && shown.HeldModel != null && !heldModels.ContainsKey(shown.Id))
            {
                heldModels[shown.Id] = ModelBuilder.Build(shown.HeldModel, hand, shown.Name);
                ApplyShadowMode(hand);
            }
            foreach (var pair in heldModels)
                pair.Value.gameObject.SetActive(shown != null && pair.Key == shown.Id);
        }

        /// Your own body would block the view in first person; it still casts a shadow.
        void ApplyShadowMode(Transform root)
        {
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                r.shadowCastingMode = Mode == ViewMode.FirstPerson
                    ? UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly
                    : UnityEngine.Rendering.ShadowCastingMode.On;
        }

        /// Walk and run cycles (legs, arms and cape follow your speed), idle breathing, and kneeling.
        /// Running (Shift) is its own gait: a forward lean, high knees, arms pumping and a bouncier step.
        void AnimateBody(float dt)
        {
            Vector3 moved = transform.position - lastPosition;
            moved.y = 0f;
            lastPosition = transform.position;
            float measured = dt > 0f ? moved.magnitude / dt : 0f;
            speed = Mathf.Lerp(speed, Mathf.Min(measured, 6f), 1f - Mathf.Exp(-12f * dt));
            float stride = Mathf.Clamp01(speed / 2f);
            runBlend = Mathf.MoveTowards(runBlend, IsRunning && stride > 0.3f ? 1f : 0f, dt * 5f);
            float run = runBlend;
            // A step every half cycle: quicker steps when running, not just longer ones.
            walkPhase += dt * Mathf.Max(speed, 0.01f) * Mathf.Lerp(3.4f, 2.9f, run);
            float s = Mathf.Sin(walkPhase);
            float swing = s * Mathf.Lerp(34f, 52f, run) * stride;

            float kneel = (Activity != null && Activity.Kneels) || Time.time < kneelUntil ? 1f : 0f;
            // Running lifts the knee on the forward swing (the leg swings further forward than back).
            float lift = run * stride * 18f;
            // (Negative angles swing a limb forward: the left leg is forward while s < 0, the right while s > 0.)
            float leftLeg = swing - Mathf.Max(0f, -s) * lift - kneel * 70f;
            float rightLeg = -swing - Mathf.Max(0f, s) * lift + kneel * 10f;
            legLeft.localRotation = Quaternion.Slerp(legLeft.localRotation, Quaternion.Euler(leftLeg, 0f, 0f), 1f - Mathf.Exp(-20f * dt));
            legRight.localRotation = Quaternion.Slerp(legRight.localRotation, Quaternion.Euler(rightLeg, 0f, 0f), 1f - Mathf.Exp(-20f * dt));
            // The left arm swings against the left leg; running, it pumps, bent forward.
            float leftArm = G.Equipment.IsWearing(EquipSlot.Shield) && (fighting || attackAnim > 0f) ? -50f
                : -swing * Mathf.Lerp(0.8f, 1.1f, run) - run * stride * 25f;
            armLeft.localRotation = Quaternion.Slerp(armLeft.localRotation, Quaternion.Euler(leftArm, 0f, -4f - run * 6f), 1f - Mathf.Exp(-15f * dt));

            float bob = stride > 0.05f ? Mathf.Abs(s) * Mathf.Lerp(0.04f, 0.09f, run) * stride : Mathf.Sin(Time.time * 2f) * 0.008f;
            model.localPosition = new Vector3(0f, bob - kneel * 0.22f, 0f);
            model.localRotation = Quaternion.Euler(kneel * 12f + run * stride * 14f, 0f, 0f);  // lean into a run

            float flutter = Mathf.Sin(Time.time * 7f) * 4f * stride + Mathf.Sin(Time.time * 1.7f) * 1.5f;
            cape.localRotation = Quaternion.Slerp(cape.localRotation, Quaternion.Euler(6f + stride * 32f + flutter, 0f, 0f), 1f - Mathf.Exp(-8f * dt));
        }

        void AnimateArm(float dt)
        {
            attackAnim = Mathf.Max(0f, attackAnim - dt);
            var pose = Activity != null ? Activity.Pose : ArmPose.None;
            bool swinging = pose == ArmPose.Swing || pose == ArmPose.Hammer;
            if (swinging)
                chopAnim += dt;
            else
                chopAnim = 0f;
            float angle;
            if (pose == ArmPose.Hammer)
            {
                // Quick hammer blows on the anvil.
                float t = (chopAnim % 0.6f) / 0.6f;
                angle = t < 0.6f ? Mathf.Lerp(-40f, -110f, t / 0.6f) : Mathf.Lerp(-110f, -40f, (t - 0.6f) / 0.4f);
            }
            else if (swinging)
            {
                // Two swings per 4-tick attempt: raise slowly, strike fast.
                const float cycle = 4 * GameTime.TickSeconds / 2f;
                float t = (chopAnim % cycle) / cycle;
                angle = t < 0.7f ? Mathf.Lerp(0f, -150f, t / 0.7f) : Mathf.Lerp(-150f, -40f, (t - 0.7f) / 0.3f);
            }
            else if (attackAnim > 0f)
            {
                // A quick slash: up, then down and forward.
                float t = 1f - attackAnim / 0.4f;
                angle = t < 0.35f ? Mathf.Lerp(-15f, -140f, t / 0.35f) : Mathf.Lerp(-140f, -60f, (t - 0.35f) / 0.65f);
            }
            else if (pose == ArmPose.HoldOut)
                angle = -75f + Mathf.Sin(Time.time * 3f) * 8f;              // holding the food over the fire
            else if (pose == ArmPose.NetOut)
                angle = -70f + Mathf.Sin(Time.time * 1.5f) * 6f;            // net held out over the water
            else if (pose == ArmPose.Strike)
                angle = -55f + Mathf.Abs(Mathf.Sin(Time.time * 9f)) * 20f;  // striking the tinderbox
            else if (fighting)
                angle = -60f;
            else
            {
                // Swings against the right leg; running, it pumps harder from a bent, forward position.
                float stride = Mathf.Clamp01(speed / 2f);
                angle = Mathf.Lerp(-15f, -40f, runBlend * stride) + Mathf.Sin(walkPhase) * Mathf.Lerp(30f * Mathf.Clamp01(speed / 3f), 50f * stride, runBlend);
            }
            armPivot.localRotation = Quaternion.Slerp(armPivot.localRotation, Quaternion.Euler(angle, 0f, 0f), 1f - Mathf.Exp(-25f * dt));
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
    }
}
