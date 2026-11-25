using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace YasudaHaruto
{
    public sealed class RoutePageRewards : MonoBehaviour
    {
        [SerializeField] private Transform m_cartTarget;
        [SerializeField] private Transform m_routePage;
        [SerializeField] private PageData m_routePageData;
        [SerializeField] private float m_pickupDistance = 2f;
        [SerializeField] private GameObject m_pageModel;
        [SerializeField] private AnimationClip m_openClip;
        [SerializeField] private Material m_pageMaterial;
        [SerializeField] private float m_pageScale = 1f;
        [SerializeField] private bool m_localPreviewOnly;
        [SerializeField] private bool m_faceCamera;
        [SerializeField] private Transform m_proximityTarget;
        [SerializeField] private float m_enemyFallDuration;

        public event Action<byte> PageArrived;

        private readonly HashSet<byte> m_started = new();
        private readonly Queue<byte> m_arrived = new();
        private readonly List<PlayableGraph> m_openGraphs = new();
        private GameObject m_routeVisual;
        private Vector3 m_previousCartPosition;

        private void Start()
        {
            m_previousCartPosition = ProximityPosition;
            m_routeVisual = CreateVisual(m_routePage.position, m_routePage.rotation);
        }

        private void LateUpdate()
        {
            Vector3 current = ProximityPosition;
            if (m_routeVisual != null && IsWithinPickupDistance(m_previousCartPosition, current))
            {
                byte pageId = m_routePageData.PageId;
                if (m_started.Add(pageId))
                    StartCoroutine(FlyToCart(pageId, m_routeVisual));
                else
                    Destroy(m_routeVisual);
                m_routeVisual = null;
            }
            m_previousCartPosition = current;

            if (m_localPreviewOnly)
                return;

            var server = BookNetworkServer.Instance;
            if (server == null || !server.IsListening)
                return;

            if (!server.IsGameStarted)
                server.BeginGame();
            if (!server.IsGameStarted)
                return;

            while (m_arrived.Count > 0)
                server.AcquirePageForBoth(m_arrived.Dequeue());
        }

        public void Drop(PageData page, Vector3 position)
        {
            Vector3 landing = position - Vector3.up * 0.38f;
            Drop(page, position, landing, m_enemyFallDuration);
        }

        private Vector3 ProximityPosition => (m_proximityTarget != null ? m_proximityTarget : m_cartTarget).position;

        public void Drop(PageData page, Vector3 position, Vector3 landingPosition, float landingDuration)
        {
            if (page == null || page.PageId == 0 || !m_started.Add(page.PageId))
                return;

            var visual = CreateVisual(position, Quaternion.identity);
            StartCoroutine(FlyToCart(page.PageId, visual, landingPosition, landingDuration));
        }

        private bool IsWithinPickupDistance(Vector3 previous, Vector3 current)
        {
            Vector3 travel = current - previous;
            float along = travel.sqrMagnitude > 0f
                ? Mathf.Clamp01(Vector3.Dot(m_routePage.position - previous, travel) / travel.sqrMagnitude)
                : 0f;
            return (previous + travel * along - m_routePage.position).sqrMagnitude <=
                m_pickupDistance * m_pickupDistance;
        }

        private GameObject CreateVisual(Vector3 position, Quaternion rotation)
        {
            var root = new GameObject("Page Pickup");
            root.transform.SetParent(transform);
            root.transform.SetPositionAndRotation(position, rotation);

            var model = Instantiate(m_pageModel, root.transform);
            model.transform.localScale *= m_pageScale;
            m_openClip.SampleAnimation(model, 0f);
            if (m_pageMaterial != null)
            {
                foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>())
                    renderer.sharedMaterial = m_pageMaterial;
            }
            return root;
        }

        private IEnumerator FlyToCart(byte pageId, GameObject visual, Vector3 landingPosition = default, float landingDuration = 0f)
        {
            Vector3 spawnPosition = visual.transform.position;
            for (float time = 0f; time < landingDuration; time += Time.deltaTime)
            {
                float t = time / landingDuration;
                visual.transform.position = Vector3.Lerp(spawnPosition, landingPosition, t * t);
                yield return null;
            }
            if (landingDuration > 0f)
            {
                visual.transform.position = landingPosition;
                yield return new WaitForSeconds(0.45f);
            }

            const float waveDuration = 0.5f;
            const float waveHeight = 0.28f;
            Animator animator = visual.GetComponentInChildren<Animator>();
            if (animator == null)
                animator = visual.transform.GetChild(0).gameObject.AddComponent<Animator>();
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            var graph = PlayableGraph.Create("Page Open");
            m_openGraphs.Add(graph);
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            var clip = AnimationClipPlayable.Create(graph, m_openClip);
            clip.SetSpeed(0d);
            AnimationPlayableOutput.Create(graph, "Page", animator).SetSourcePlayable(clip);
            graph.Play();

            Vector3 origin = visual.transform.position;
            var camera = m_faceCamera ? Camera.main : null;
            float openingDuration = Mathf.Max(0.1f, m_openClip.length);
            float duration = openingDuration + waveDuration;
            try
            {
                for (float time = 0f; time < duration; time += Time.deltaTime)
                {
                    clip.SetTime(Mathf.Min(time, Mathf.Max(0f, m_openClip.length - 0.0001f)));
                    graph.Evaluate(0f);

                    Vector3 target = m_cartTarget.position;
                    float wave = Mathf.Clamp01((time - openingDuration) / waveDuration);
                    Vector3 position = Vector3.Lerp(origin, target, time / duration)
                        + Vector3.up * (Mathf.Sin(wave * 2f * Mathf.PI) * waveHeight);
                    Vector3 lookAt = camera != null ? camera.transform.position : target;
                    Vector3 heading = Vector3.ProjectOnPlane(lookAt - position, Vector3.up);
                    if (heading.sqrMagnitude > 0.001f)
                        visual.transform.rotation = Quaternion.LookRotation(heading) * Quaternion.Euler(65f, 0f, 0f);
                    visual.transform.position = position;
                    yield return null;
                }
                visual.transform.position = m_cartTarget.position;
                if (!m_localPreviewOnly)
                    m_arrived.Enqueue(pageId);
                PageArrived?.Invoke(pageId);
            }
            finally
            {
                m_openGraphs.Remove(graph);
                if (graph.IsValid())
                    graph.Destroy();
                Destroy(visual);
            }
        }

        private void OnDestroy()
        {
            foreach (var graph in m_openGraphs)
                if (graph.IsValid())
                    graph.Destroy();
            m_openGraphs.Clear();
        }
    }
}
