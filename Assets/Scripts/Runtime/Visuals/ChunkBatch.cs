using System.Collections.Generic;
using UnityEngine;

namespace Epacenur.Runtime
{
    /// The trees, rocks and plants a chunk (or region, or the dev plot) built, drawn as one merged mesh with a
    /// sub-mesh per material instead of hundreds of renderers. It's rebuilt when one of them changes (felled,
    /// mined out, grown back); one being worked on (shaking under the axe) is drawn on its own meanwhile.
    public sealed class ChunkBatch : MonoBehaviour
    {
        readonly List<NodeObject> members = new List<NodeObject>();
        MeshFilter filter;
        MeshRenderer meshRenderer;
        Mesh mesh;
        bool dirty;

        /// The batch on a root (made the first time).
        public static ChunkBatch On(Transform root)
        {
            var b = root.GetComponent<ChunkBatch>();
            if (b == null)
            {
                b = root.gameObject.AddComponent<ChunkBatch>();
                var go = new GameObject("Batched");
                go.transform.SetParent(root, false);
                b.filter = go.AddComponent<MeshFilter>();
                b.meshRenderer = go.AddComponent<MeshRenderer>();
            }
            return b;
        }

        public void Add(NodeObject node)
        {
            members.Add(node);
            dirty = true;
        }

        public void Invalidate() => dirty = true;

        void LateUpdate()
        {
            if (dirty)
                Rebuild();
        }

        /// Merges every member that's batchable now; the rest draw themselves.
        public void Rebuild()
        {
            dirty = false;
            var byMaterial = new Dictionary<Material, List<CombineInstance>>();
            var toLocal = transform.worldToLocalMatrix;
            foreach (var node in members)
            {
                if (node == null)
                    continue;
                bool batch = node.Batchable;
                foreach (var r in node.SolidRenderers())
                {
                    r.enabled = !batch;
                    if (!batch)
                        continue;
                    var mf = r.GetComponent<MeshFilter>();
                    if (mf == null || mf.sharedMesh == null)
                        continue;
                    var mats = r.sharedMaterials;
                    var m = toLocal * r.transform.localToWorldMatrix;
                    for (int s = 0; s < mf.sharedMesh.subMeshCount && s < mats.Length; s++)
                    {
                        if (!byMaterial.TryGetValue(mats[s], out var list))
                            byMaterial[mats[s]] = list = new List<CombineInstance>();
                        list.Add(new CombineInstance { mesh = mf.sharedMesh, subMeshIndex = s, transform = m });
                    }
                }
            }
            if (mesh != null)
                Destroy(mesh);
            mesh = null;
            if (byMaterial.Count == 0)
            {
                filter.sharedMesh = null;
                return;
            }
            var parts = new CombineInstance[byMaterial.Count];
            var materials = new Material[byMaterial.Count];
            int i = 0;
            foreach (var kv in byMaterial)
            {
                var one = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                one.CombineMeshes(kv.Value.ToArray(), true, true);
                parts[i] = new CombineInstance { mesh = one, transform = Matrix4x4.identity };
                materials[i] = kv.Key;
                i++;
            }
            mesh = new Mesh { name = "Batched", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.CombineMeshes(parts, false, false);
            mesh.RecalculateBounds();
            foreach (var p in parts)
                Destroy(p.mesh);
            filter.sharedMesh = mesh;
            meshRenderer.sharedMaterials = materials;
        }

        void OnDestroy()
        {
            if (mesh != null)
                Destroy(mesh);
        }
    }
}
