using System.Collections.Generic;
using Epacenur.Core;
using UnityEngine;

namespace Epacenur.Runtime
{
    /// Tile mode: a RuneScape-style camera orbiting the player (arrow keys or middle-drag to rotate,
    /// scroll to zoom). First person: the camera sits at eye height and the mouse looks around.
    public sealed class CameraRig : MonoBehaviour
    {
        public Camera Cam { get; private set; }
        public ViewMode Mode { get; private set; } = ViewMode.Tile;
        /// Direction the camera faces on the ground, in degrees (0 = north, +z). WASD moves relative to it.
        public float Yaw => Mode == ViewMode.Tile ? orbitYaw : lookYaw;

        public static float MouseSensitivity = 2.2f;

        PlayerController player;
        float orbitYaw = 0f, orbitPitch = 52f, distance = 13f;
        float lookYaw, lookPitch;
        Vector3 focus;
        // What you hold in first person (your weapon, or the tool for what you aim at), built on first use.
        readonly Dictionary<string, Transform> fpHeld = new Dictionary<string, Transform>();

        public static CameraRig Create(Camera cam, PlayerController player)
        {
            var rig = cam.gameObject.AddComponent<CameraRig>();
            rig.Cam = cam;
            rig.player = player;
            rig.focus = player.transform.position;

            rig.ApplyLens();
            return rig;
        }

        public void SetMode(ViewMode mode)
        {
            Mode = mode;
            if (mode == ViewMode.FirstPerson)
            {
                lookYaw = orbitYaw;
                lookPitch = 8f;
                LockCursor(true);
            }
            else
            {
                orbitYaw = lookYaw;
                LockCursor(false);
            }
            if (mode == ViewMode.Tile)
                ShowHeld(null);
            ApplyLens();
        }

        /// The world's origin moved (World): move with it.
        public void ShiftBy(Vector3 delta)
        {
            focus += delta;
            transform.position += delta;
        }

        public void LockCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        void ApplyLens()
        {
            Cam.fieldOfView = Mode == ViewMode.FirstPerson ? 72f : 50f;
            Cam.nearClipPlane = Mode == ViewMode.FirstPerson ? 0.05f : 0.3f;
            Cam.farClipPlane = 82f;  // the fog hides everything past 78
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (Mode == ViewMode.Tile)
                UpdateOrbit(dt);
            else
                UpdateFirstPerson();
        }

        void UpdateOrbit(float dt)
        {
            if (Input.GetKey(KeyCode.LeftArrow)) orbitYaw += 110f * dt;
            if (Input.GetKey(KeyCode.RightArrow)) orbitYaw -= 110f * dt;
            if (Input.GetKey(KeyCode.UpArrow)) orbitPitch += 60f * dt;
            if (Input.GetKey(KeyCode.DownArrow)) orbitPitch -= 60f * dt;
            if (Input.GetMouseButton(2))
            {
                orbitYaw += Input.GetAxisRaw("Mouse X") * MouseSensitivity * 2f;
                orbitPitch -= Input.GetAxisRaw("Mouse Y") * MouseSensitivity * 2f;
            }
            if (!Game.Instance.Hud.IsMouseOverUi())
                distance *= 1f - Input.mouseScrollDelta.y * 0.1f;
            orbitPitch = Mathf.Clamp(orbitPitch, 20f, 85f);
            distance = Mathf.Clamp(distance, 4f, 30f);

            focus = Vector3.Lerp(focus, player.transform.position, 1f - Mathf.Exp(-12f * dt));
            var rot = Quaternion.Euler(orbitPitch, orbitYaw, 0f);
            Vector3 lookAt = focus + Vector3.up * 1f;
            transform.SetPositionAndRotation(lookAt - rot * Vector3.forward * distance, rot);
        }

        void UpdateFirstPerson()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
                LockCursor(false);
            if (Cursor.lockState == CursorLockMode.Locked)
            {
                lookYaw += Input.GetAxisRaw("Mouse X") * MouseSensitivity;
                lookPitch -= Input.GetAxisRaw("Mouse Y") * MouseSensitivity;
                lookPitch = Mathf.Clamp(lookPitch, -85f, 85f);
            }
            focus = player.transform.position;
            transform.SetPositionAndRotation(focus + Vector3.up * PlayerController.EyeHeight, Quaternion.Euler(lookPitch, lookYaw, 0f));

            // Chop or slash: a quick downward strike, then back.
            var item = player.FirstPersonItem;
            var held = ShowHeld(item);
            if (held == null)
                return;
            bool weapon = item.Slot == EquipSlot.Weapon;
            float s = player.SwingT;
            float strike = s > 0.6f ? Mathf.InverseLerp(1f, 0.6f, s) : Mathf.InverseLerp(0f, 0.6f, s);
            held.localRotation = Quaternion.Euler(strike * 75f, 0f, strike * (weapon ? -35f : -10f));
        }

        /// Shows `item` in your hand at the bottom right of the view (null: empty-handed). Returns its holder.
        Transform ShowHeld(ItemDef item)
        {
            Transform shown = null;
            if (item != null && item.HeldModel != null && !fpHeld.TryGetValue(item.Id, out shown))
            {
                // Weapons are held a little higher and closer than tools.
                bool weapon = item.Slot == EquipSlot.Weapon;
                shown = new GameObject("First person " + item.Name).transform;
                shown.SetParent(Cam.transform, false);
                shown.localPosition = weapon ? new Vector3(0.32f, -0.3f, 0.5f) : new Vector3(0.32f, -0.32f, 0.55f);
                var model = ModelBuilder.Build(item.HeldModel, shown);
                model.localPosition = new Vector3(0f, 0f, weapon ? -0.1f : -0.2f);
                model.localRotation = weapon ? Quaternion.Euler(-55f, -12f, 0f) : Quaternion.Euler(-60f, -10f, 0f);
                foreach (var r in shown.GetComponentsInChildren<Renderer>())
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                fpHeld[item.Id] = shown;
            }
            foreach (var pair in fpHeld)
                pair.Value.gameObject.SetActive(pair.Value == shown);
            return shown;
        }
    }
}
