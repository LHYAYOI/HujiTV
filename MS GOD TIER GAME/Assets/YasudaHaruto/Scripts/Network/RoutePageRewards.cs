using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace YasudaHaruto
{
    [DefaultExecutionOrder(1000)]
    public sealed class RoutePageRewards : MonoBehaviour
    {
        private static readonly int s_waveOriginId = Shader.PropertyToID("_PaperWaveOrigin");
        private static readonly int s_waveDirectionId = Shader.PropertyToID("_PaperWaveDirection");
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
        [SerializeField, Min(0.01f)] private float m_openPlaybackSpeed = 1f;
        [SerializeField, Min(0f)] private float m_minimumFlightDuration;
        [SerializeField, Min(0f)] private float m_cameraArrivalDistance;
        [SerializeField] private bool m_faceCameraAfterOpening;
        [SerializeField, Min(0f)] private float m_waveHeight = 0.28f;
        [SerializeField, Min(0f)] private float m_arrivalHoldDuration;
        [SerializeField] private bool m_useHorizontalSuction;
        [SerializeField] private Camera m_pickupCamera;
        [SerializeField, Range(0f, 1f)] private float m_suctionViewportHeight = 0.30f;
        [SerializeField, Min(0f)] private float m_extraDropDistance;
        [SerializeField, Range(0f, 1f)] private float m_minimumPaperViewportHeight;

        public event Action<byte> PageArrived;

        private readonly HashSet<byte> m_started = new();
        private readonly Queue<byte> m_arrived = new();
        private readonly List<PlayableGraph> m_openGraphs = new();
        private GameObject m_routeVisual;
        private Vector3 m_previousCartPosition;
        private event Action PickupVisualUpdate;

        private void Start()
        {
            m_previousCartPosition = ProximityPosition;
            m_routeVisual = CreateVisual(m_routePage.position, m_routePage.rotation);
        }

        private void LateUpdate()
        {
            PickupVisualUpdate?.Invoke();
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
            Vector3 landing = position - Vector3.up * (0.38f + m_extraDropDistance);
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
            const float uprightTurnDuration = 0.75f;
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

            float playbackSpeed = Mathf.Max(0.01f, m_openPlaybackSpeed);
            float openingDuration = Mathf.Max(0.1f, m_openClip.length / playbackSpeed);
            float finishDuration = m_faceCameraAfterOpening ? uprightTurnDuration + 0.15f : waveDuration;
            float duration = Mathf.Max(m_minimumFlightDuration, openingDuration + finishDuration);
            float waveStart = m_faceCameraAfterOpening ? openingDuration : duration - waveDuration;
            try
            {
                if (m_useHorizontalSuction)
                    yield return OpenAndAbsorb(visual.transform, clip, graph, playbackSpeed, openingDuration);
                else
                    yield return FlyWithUprightFinish(visual, clip, graph, playbackSpeed, openingDuration, duration, waveStart, uprightTurnDuration);

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

        private IEnumerator FlyWithUprightFinish(GameObject visual, AnimationClipPlayable clip, PlayableGraph graph,
            float playbackSpeed, float openingDuration, float duration, float waveStart, float uprightTurnDuration)
        {
            Vector3 origin = visual.transform.position;
            for (float elapsed = 0f; elapsed < duration + m_arrivalHoldDuration; elapsed += Time.deltaTime)
            {
                // Hold the final pose at the camera-relative destination until acquisition.
                float time = Mathf.Min(elapsed, duration);
                clip.SetTime(Mathf.Min(time * playbackSpeed, Mathf.Max(0f, m_openClip.length - 0.0001f)));
                graph.Evaluate(0f);

                Camera camera = m_faceCamera || m_faceCameraAfterOpening || m_cameraArrivalDistance > 0f
                    ? Camera.main : null;
                Vector3 target = GetArrivalPosition(camera);
                float wave = Mathf.Clamp01((time - waveStart) / (duration - waveStart));
                Vector3 position = Vector3.Lerp(origin, target, time / duration)
                    + Vector3.up * (Mathf.Sin(wave * 2f * Mathf.PI) * m_waveHeight);
                Vector3 lookAt = m_faceCamera && !m_faceCameraAfterOpening && camera != null
                    ? camera.transform.position : target;
                Vector3 heading = Vector3.ProjectOnPlane(lookAt - position, Vector3.up);
                Quaternion rotation = visual.transform.rotation;
                if (heading.sqrMagnitude > 0.001f)
                    rotation = Quaternion.LookRotation(heading) * Quaternion.Euler(65f, 0f, 0f);
                if (m_faceCameraAfterOpening && camera != null)
                {
                    Vector3 towardCamera = camera.transform.position - position;
                    Vector3 flatHeading = Vector3.ProjectOnPlane(towardCamera, Vector3.up);
                    if (flatHeading.sqrMagnitude <= 0.001f)
                        flatHeading = Vector3.ProjectOnPlane(-camera.transform.forward, Vector3.up);
                    if (flatHeading.sqrMagnitude > 0.001f)
                    {
                        float flutter = Mathf.Sin(wave * 2f * Mathf.PI) * 4f;
                        rotation = Quaternion.LookRotation(flatHeading) * Quaternion.Euler(flutter, 0f, 0f);
                    }

                    if (towardCamera.sqrMagnitude > 0.001f)
                    {
                        Quaternion faceCamera = Quaternion.LookRotation(towardCamera, camera.transform.up)
                            * Quaternion.Euler(90f, 0f, 0f);
                        float turn = Mathf.SmoothStep(0f, 1f,
                            Mathf.InverseLerp(duration - uprightTurnDuration, duration, time));
                        rotation = Quaternion.Slerp(rotation, faceCamera, turn);
                    }
                }
                visual.transform.rotation = rotation;
                visual.transform.position = position;
                yield return null;
            }
            visual.transform.position = GetArrivalPosition(m_cameraArrivalDistance > 0f ? Camera.main : null);
        }

        private IEnumerator OpenAndAbsorb(Transform page, AnimationClipPlayable clip, PlayableGraph graph,
            float playbackSpeed, float openingDuration)
        {
            const float suctionDuration = 0.9f;
            const float approachOverlap = 0.25f;
            float approachDuration = Mathf.Max(1.6f, m_minimumFlightDuration - suctionDuration);
            float duration = approachDuration + suctionDuration;
            float suctionTravelDuration = suctionDuration + approachOverlap;
            Vector3 origin = page.position;
            page.rotation = Quaternion.Euler(0f, page.eulerAngles.y, 0f);

            float openEnd = Mathf.Max(0f, m_openClip.length - 0.0001f);
            for (float time = 0f; time < openingDuration; time += Time.deltaTime)
            {
                clip.SetTime(Mathf.Min(time * playbackSpeed, openEnd));
                graph.Evaluate(0f);
                yield return null;
            }
            clip.SetTime(openEnd);
            graph.Evaluate(0f);

            Action updateVisual = null;
            try
            {
                Camera camera = m_pickupCamera != null && m_pickupCamera.isActiveAndEnabled
                    ? m_pickupCamera : Camera.main;
                Vector3 start = Vector3.zero;
                Vector3 cameraLocalStart = Vector3.zero;
                float arrivalDepth = camera != null ? Mathf.Max(m_cameraArrivalDistance, camera.nearClipPlane + 0.1f) : 0f;
                float nearDepth = Mathf.Max(0.9f, arrivalDepth);

                var skin = page.GetComponentInChildren<SkinnedMeshRenderer>();
                var wave = new MaterialPropertyBlock();
                if (skin != null)
                    skin.GetPropertyBlock(wave);
                Bounds paperBounds = default;
                bool keepAboveWagon = m_minimumPaperViewportHeight > 0f && skin != null;
                var wagon = keepAboveWagon && m_proximityTarget != null
                    ? m_proximityTarget.Find("wagon") : null;
                var wagonRenderer = wagon != null ? wagon.GetComponentInChildren<Renderer>() : null;
                if (skin != null && camera != null)
                {
                    // Measure the open pose without changing the displayed renderer.
                    var sampledPaper = new Mesh();
                    skin.BakeMesh(sampledPaper);
                    paperBounds = sampledPaper.bounds;
                    Destroy(sampledPaper);
                }

                float elapsed = 0f;
                bool started = false;
                bool finished = false;
                // Use the camera pose after the wagon and Cinemachine finish updating for this frame.
                updateVisual = () =>
                {
                    if (finished)
                        return;
                    if (!started)
                    {
                        started = true;
                        if (camera != null)
                        {
                            start = camera.WorldToViewportPoint(origin);
                            cameraLocalStart = camera.transform.InverseTransformPoint(origin);
                            if (start.z > arrivalDepth)
                                nearDepth = Mathf.Min(nearDepth, start.z);
                        }
                    }
                    float time = elapsed;
                    float approach = Mathf.SmoothStep(0f, 1f, time / approachDuration);
                    float suctionTime = Mathf.Clamp01((time - approachDuration + approachOverlap) / suctionTravelDuration);
                    float suction = suctionTime * suctionTime;
                    Vector3 arrival = GetArrivalPosition(camera, m_suctionViewportHeight);
                    if (camera != null)
                    {
                        Vector3 near = new Vector3(0.5f, 0.46f, nearDepth);
                        Vector3 nearPosition = camera.ViewportToWorldPoint(near);
                        Vector3 flightPosition = start.z > 0f
                            ? camera.ViewportToWorldPoint(Vector3.Lerp(start, near, approach))
                            : Vector3.Lerp(camera.transform.TransformPoint(cameraLocalStart), nearPosition, approach);
                        flightPosition = Vector3.Lerp(flightPosition, arrival, suction);
                        float passTime = Mathf.Max(0f, time - duration);
                        // Carry the incoming velocity past the old arrival point, then clear the lens.
                        flightPosition += (arrival - nearPosition) * (2f * passTime / suctionTravelDuration)
                            - camera.transform.forward * (2f * passTime * passTime);
                        Vector3 carriedOrigin = camera.transform.TransformPoint(cameraLocalStart);
                        float follow = Mathf.SmoothStep(0f, 1f, time / (approachDuration * 0.75f));
                        // Fade camera motion in separately from the page's forward travel.
                        page.position = flightPosition + (origin - carriedOrigin) * (1f - follow);
                    }
                    else
                    {
                        page.position = Vector3.Lerp(origin, arrival, Mathf.Lerp(approach * 0.85f, 1f, suction));
                    }

                    float amplitude = m_waveHeight * Mathf.SmoothStep(0f, 1f, time / 0.35f) * (1f - suction);
                    if (keepAboveWagon && camera != null)
                        KeepPaperAboveWagon(page, skin.transform, paperBounds, wagonRenderer, camera, amplitude);

                    if (skin != null)
                    {
                        Vector3 position = page.position;
                        Vector3 forward = page.forward;
                        wave.SetVector(s_waveOriginId, new Vector4(position.x, position.y, position.z, amplitude));
                        wave.SetVector(s_waveDirectionId, new Vector4(forward.x, forward.y, forward.z, time * 8f));
                        skin.SetPropertyBlock(wave);
                    }
                    finished = time >= duration && (camera == null ||
                        IsPaperBehindCamera(page, skin, paperBounds, camera));
                    elapsed += Time.deltaTime;
                };
                PickupVisualUpdate += updateVisual;
                while (!finished)
                    yield return null;
            }
            finally
            {
                PickupVisualUpdate -= updateVisual;
            }
        }

        private void KeepPaperAboveWagon(Transform page, Transform paper, Bounds localBounds, Renderer wagon,
            Camera camera, float waveHeight)
        {
            float depth = camera.WorldToViewportPoint(page.position).z;
            float nearWagon = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(2.3f, 1.1f, depth));
            if (nearWagon <= 0f)
                return;

            float worldBottom = float.PositiveInfinity;
            float screenLift = 0f;
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 extent = localBounds.extents;
                Vector3 point = paper.TransformPoint(localBounds.center + new Vector3(
                    (corner & 1) == 0 ? -extent.x : extent.x,
                    (corner & 2) == 0 ? -extent.y : extent.y,
                    (corner & 4) == 0 ? -extent.z : extent.z)) - Vector3.up * waveHeight;
                worldBottom = Mathf.Min(worldBottom, point.y);
                Vector3 view = camera.WorldToViewportPoint(point);
                if (view.z > camera.nearClipPlane && view.y < m_minimumPaperViewportHeight)
                {
                    Vector3 raised = camera.ViewportToWorldPoint(
                        new Vector3(view.x, m_minimumPaperViewportHeight, view.z));
                    screenLift = Mathf.Max(screenLift, Vector3.Dot(raised - point, camera.transform.up));
                }
            }

            if (wagon != null)
            {
                float worldLift = Mathf.Max(0f, wagon.bounds.max.y + 0.025f - worldBottom);
                page.position += Vector3.up * (worldLift * nearWagon);
            }
            float screenWeight = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.10f, 0.40f, depth));
            page.position += camera.transform.up * (screenLift * nearWagon * screenWeight);
        }

        private static bool IsPaperBehindCamera(Transform page, SkinnedMeshRenderer skin, Bounds bounds, Camera camera)
        {
            if (skin == null)
                return camera.WorldToViewportPoint(page.position).z < -0.65f;

            Transform paper = skin.transform;
            Vector3 forward = camera.transform.forward;
            float furthestDepth = Vector3.Dot(paper.TransformPoint(bounds.center) - camera.transform.position, forward)
                + Mathf.Abs(Vector3.Dot(paper.TransformVector(Vector3.right * bounds.extents.x), forward))
                + Mathf.Abs(Vector3.Dot(paper.TransformVector(Vector3.up * bounds.extents.y), forward))
                + Mathf.Abs(Vector3.Dot(paper.TransformVector(Vector3.forward * bounds.extents.z), forward));
            return furthestDepth < -0.02f;
        }

        private Vector3 GetArrivalPosition(Camera camera, float viewportHeight = 0.43f)
        {
            if (m_cameraArrivalDistance <= 0f || camera == null)
                return m_cartTarget.position;

            float distance = Mathf.Max(m_cameraArrivalDistance, camera.nearClipPlane + 0.1f);
            return camera.ViewportToWorldPoint(new Vector3(0.5f, viewportHeight, distance));
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
