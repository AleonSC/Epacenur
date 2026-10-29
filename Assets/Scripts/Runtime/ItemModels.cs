using Epacenur.Core;
using UnityEngine;

namespace Epacenur.Runtime
{
    /// 3D models for items: lying on the ground, and the sword and shield held by the player.
    public static class ItemModels
    {
        /// An item lying on the ground, about half a tile across, resting on y = 0.
        public static void BuildGround(ItemDef def, Transform parent)
        {
            var c = new Color(def.R, def.G, def.B);
            if (def == ItemDefs.Logs)
            {
                var end = new Color(0.85f, 0.7f, 0.45f);
                for (int i = 0; i < 3; i++)
                {
                    var log = Visuals.Prim(PrimitiveType.Cylinder, parent, new Vector3(-0.08f + i * 0.08f, 0.06f + (i == 1 ? 0.08f : 0f), i * 0.04f),
                        new Vector3(0.1f, 0.2f, 0.1f), i == 1 ? c * 0.85f : c);
                    log.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                    Visuals.Prim(PrimitiveType.Cylinder, log.transform, new Vector3(0f, 1f, 0f), new Vector3(0.9f, 0.02f, 0.9f), end);
                }
            }
            else if (def == ItemDefs.Coins)
            {
                for (int i = 0; i < 4; i++)
                    Visuals.Prim(PrimitiveType.Cylinder, parent, new Vector3((i % 2) * 0.05f, 0.01f + i * 0.022f, (i / 2) * 0.03f),
                        new Vector3(0.14f, 0.01f, 0.14f), c);
            }
            else if (def == ItemDefs.Tinderbox)
            {
                Visuals.Prim(PrimitiveType.Cube, parent, new Vector3(0f, 0.04f, 0f), new Vector3(0.18f, 0.08f, 0.12f), c);
                Visuals.Prim(PrimitiveType.Cube, parent, new Vector3(0f, 0.085f, 0f), new Vector3(0.19f, 0.015f, 0.13f), c * 0.7f);
            }
            else if (def == ItemDefs.BronzeSword)
            {
                var sword = new GameObject("Sword").transform;
                sword.SetParent(parent, false);
                sword.localPosition = new Vector3(0f, 0.03f, -0.25f);
                sword.localRotation = Quaternion.Euler(0f, 0f, 90f);
                BuildSword(sword, def);
            }
            else if (def == ItemDefs.WoodenShield)
            {
                var shield = new GameObject("Shield").transform;
                shield.SetParent(parent, false);
                shield.localPosition = new Vector3(0f, 0.03f, 0f);
                shield.localRotation = Quaternion.Euler(0f, 0f, 90f);
                BuildShield(shield, def);
            }
            else
            {
                Visuals.Prim(PrimitiveType.Cube, parent, new Vector3(0f, 0.1f, 0f), new Vector3(0.2f, 0.2f, 0.2f), c);
            }
        }

        /// A sword pointing along +z from the grip at the origin.
        public static void BuildSword(Transform parent, ItemDef def)
        {
            var blade = new Color(def.R, def.G, def.B);
            Visuals.Prim(PrimitiveType.Cube, parent, new Vector3(0f, 0f, 0.05f), new Vector3(0.04f, 0.04f, 0.14f), new Color(0.35f, 0.22f, 0.12f));
            Visuals.Prim(PrimitiveType.Cube, parent, new Vector3(0f, 0f, 0.13f), new Vector3(0.2f, 0.04f, 0.04f), new Color(0.45f, 0.45f, 0.5f));
            Visuals.Prim(PrimitiveType.Cube, parent, new Vector3(0f, 0f, 0.43f), new Vector3(0.06f, 0.015f, 0.56f), blade);
        }

        /// A round shield facing -x (outwards from the left arm), centred on the origin.
        public static void BuildShield(Transform parent, ItemDef def)
        {
            var wood = new Color(def.R, def.G, def.B);
            var rim = Visuals.Prim(PrimitiveType.Cylinder, parent, Vector3.zero, new Vector3(0.5f, 0.02f, 0.5f), new Color(0.45f, 0.45f, 0.5f));
            rim.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            var face = Visuals.Prim(PrimitiveType.Cylinder, parent, new Vector3(-0.012f, 0f, 0f), new Vector3(0.44f, 0.02f, 0.44f), wood);
            face.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            var boss = Visuals.Prim(PrimitiveType.Sphere, parent, new Vector3(-0.03f, 0f, 0f), new Vector3(0.06f, 0.1f, 0.1f), new Color(0.6f, 0.6f, 0.65f));
            boss.name = "Boss";
        }
    }
}
