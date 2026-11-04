using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace YasudaHaruto
{
    public sealed class BattlePageDrops : MonoBehaviour
    {
        [Serializable]
        private struct Assignment
        {
            public string EnemyId;
            [Range(1, 255)] public int PageId;
        }

        [SerializeField] private Assignment[] m_assignments =
        {
            new Assignment { EnemyId = "MiniGhost_A", PageId = 5 },
            new Assignment { EnemyId = "MiniGhost_B", PageId = 5 },
            new Assignment { EnemyId = "MiniGhost_C", PageId = 5 }
        };
        [SerializeField] private GameObject m_bubblePrefab;
        [SerializeField] private Material m_bubbleMaterial;
        [SerializeField] private Transform m_previewCartTarget;
        [SerializeField] private LayerMask m_groundLayers;

        private sealed class Drop
        {
            public Transform Visual;
            public byte PageId;
            public bool Landed;
        }

        private readonly List<Drop> m_waitingDrops = new();
        private Material m_previewBubble;
        private Material m_previewPaper;

        public bool ReportEnemyDefeated(string enemyId, Vector3 position)
        {
            if (!isActiveAndEnabled || string.IsNullOrEmpty(enemyId)) return false;
            foreach (var assignment in m_assignments)
            {
                if (assignment.EnemyId != enemyId || assignment.PageId < 1 || assignment.PageId > 255)
                    continue;
                Transform visual = CreateVisual(position);
                var drop = new Drop { Visual = visual, PageId = (byte)assignment.PageId };
                m_waitingDrops.Add(drop);
                Vector3 landing = position - Vector3.up * 0.5f;
                if (Physics.Raycast(position, Vector3.down, out RaycastHit hit, 8f,
                    m_groundLayers, QueryTriggerInteraction.Ignore))
                    landing = hit.point + Vector3.up * 0.28f;
                StartCoroutine(Fall(drop, position, landing));
                return true;
            }
            return false;
        }

        public void CollectBattleDrops(Transform cartTarget)
        {
            if (cartTarget == null || m_waitingDrops.Count == 0) return;
            var drops = m_waitingDrops.ToArray();
            m_waitingDrops.Clear();
            StartCoroutine(CollectAfterLanding(drops, cartTarget));
        }

        private IEnumerator CollectAfterLanding(Drop[] drops, Transform cartTarget)
        {
            foreach (Drop drop in drops)
            {
                while (!drop.Landed) yield return null;
            }

            if (cartTarget == null)
            {
                m_waitingDrops.AddRange(drops);
                yield break;
            }

            foreach (Drop drop in drops)
                StartCoroutine(FlyToCart(drop, cartTarget));
        }

        private static IEnumerator Fall(Drop drop, Vector3 from, Vector3 to)
        {
            const float duration = 0.55f;
            for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
            {
                float t = elapsed / duration;
                drop.Visual.position = Vector3.Lerp(from, to, t) + Vector3.up * (Mathf.Sin(t * Mathf.PI) * 0.36f);
                yield return null;
            }
            drop.Visual.position = to;
            drop.Landed = true;
        }

        private static IEnumerator FlyToCart(Drop drop, Transform target)
        {
            const float duration = 0.7f;
            Vector3 from = drop.Visual.position;
            Vector3 destination = target.position;
            for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
            {
                if (target != null) destination = target.position;
                float t = elapsed / duration;
                drop.Visual.position = Vector3.Lerp(from, destination, t) +
                    Vector3.up * (Mathf.Sin(t * Mathf.PI) * 0.8f);
                yield return null;
            }

            BookNetworkServer.Instance?.AcquirePageForBoth(drop.PageId);
            Destroy(drop.Visual.gameObject);
        }

        private Transform CreateVisual(Vector3 position)
        {
            if (m_bubblePrefab != null)
                return Instantiate(m_bubblePrefab, position, Quaternion.identity, transform).transform;

            var root = new GameObject("Page Bubble");
            root.transform.SetParent(transform);
            root.transform.position = position;

            if (m_previewBubble == null && m_bubbleMaterial == null)
                m_previewBubble = CreateMaterial(new Color(0.35f, 0.85f, 1f, 0.3f), true);
            if (m_previewPaper == null)
                m_previewPaper = CreateMaterial(new Color(1f, 0.89f, 0.67f), false);

            AddShape(PrimitiveType.Sphere, root.transform, Vector3.one * 0.52f,
                Quaternion.identity, m_bubbleMaterial != null ? m_bubbleMaterial : m_previewBubble);
            AddShape(PrimitiveType.Cube, root.transform, new Vector3(0.20f, 0.29f, 0.015f),
                Quaternion.Euler(12f, 25f, -18f), m_previewPaper);
            return root.transform;
        }

        private static void AddShape(PrimitiveType shape, Transform parent, Vector3 scale,
            Quaternion rotation, Material material)
        {
            GameObject part = GameObject.CreatePrimitive(shape);
            part.GetComponent<Collider>().enabled = false;
            part.transform.SetParent(parent, false);
            part.transform.localScale = scale;
            part.transform.localRotation = rotation;
            if (material != null) part.GetComponent<Renderer>().sharedMaterial = material;
        }

        private static Material CreateMaterial(Color color, bool transparent)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) return null;
            var material = new Material(shader) { color = color };
            if (transparent)
            {
                material.SetFloat("_Surface", 1);
                material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_ZWrite", 0);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }
            return material;
        }

        [ContextMenu("Preview / Drop Page Bubbles")]
        private void PreviewDrops()
        {
            if (!Application.isPlaying) return;
            for (int i = 0; i < m_assignments.Length; i++)
                ReportEnemyDefeated(m_assignments[i].EnemyId, transform.position + Vector3.right * i * 0.65f);
        }

        [ContextMenu("Preview / Collect Page Bubbles")]
        private void PreviewCollect()
        {
            if (Application.isPlaying) CollectBattleDrops(m_previewCartTarget);
        }

        private void OnDestroy()
        {
            if (m_previewBubble != null) Destroy(m_previewBubble);
            if (m_previewPaper != null) Destroy(m_previewPaper);
        }
    }
}
