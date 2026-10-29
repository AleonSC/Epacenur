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
        Transform fpAxe, fpSword;

        public static CameraRig Create(Camera cam, PlayerController player)
        {
            var rig = cam.gameObject.AddComponent<CameraRig>();
            rig.Cam = cam;
            rig.player = player;
            rig.focus = player.transform.position;

            // The axe you see in first person, held at the bottom right of the view.
            var holder = new GameObject("First person axe").transform;
            holder.SetParent(cam.transform, false);
            holder.localPosition = new Vector3(0.32f, -0.32f, 0.55f);
            var axe = new GameObject("Axe").transform;
            axe.SetParent(holder, false);
            axe.localPosition = new Vector3(0f, 0f, -0.2f);
            axe.localRotation = Quaternion.Euler(-60f, -10f, 0f);
            PlayerController.BuildAxe(axe);
            foreach (var r in holder.GetComponentsInChildren<Renderer>())
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rig.fpAxe = holder;
            holder.gameObject.SetActive(false);

            // The sword, when you're wielding one.
            var swordHolder = new GameObject("First person sword").transform;
            swordHolder.SetParent(cam.transform, false);
            swordHolder.localPosition = new Vector3(0.32f, -0.3f, 0.5f);
            var sword = new GameObject("Sword").transform;
            sword.SetParent(swordHolder, false);
            sword.localPosition = new Vector3(0f, 0f, -0.1f);
            sword.localRotation = Quaternion.Euler(-55f, -12f, 0f);
            ItemModels.BuildSword(sword, Core.ItemDefs.BronzeSword);
            foreach (var r in swordHolder.GetComponentsInChildren<Renderer>())
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rig.fpSword = swordHolder;
            swordHolder.gameObject.SetActive(false);

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
            {
                fpAxe.gameObject.SetActive(false);
                fpSword.gameObject.SetActive(false);
            }
            ApplyLens();
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
            Cam.farClipPlane = 115f;  // the fog hides everything past 110
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
            var tool = player.FirstPersonTool;
            bool sword = tool == HeldTool.Sword;
            fpAxe.gameObject.SetActive(tool == HeldTool.Axe);
            fpSword.gameObject.SetActive(sword);
            if (tool == HeldTool.None)
                return;
            float s = player.SwingT;
            float strike = s > 0.6f ? Mathf.InverseLerp(1f, 0.6f, s) : Mathf.InverseLerp(0f, 0.6f, s);
            (sword ? fpSword : fpAxe).localRotation = Quaternion.Euler(strike * 75f, 0f, strike * (sword ? -35f : -10f));
        }
    }
}
