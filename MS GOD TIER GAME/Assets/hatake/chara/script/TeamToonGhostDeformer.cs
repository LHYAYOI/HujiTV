using UnityEngine;

namespace TeamToon
{
    /// <summary>
    /// Procedural deformer layered on top of authored animation: trailing drag, squash &amp; stretch,
    /// a tail wave and breathing. The shader deforms in world space after skinning, so every part of a
    /// ghost (body, hands, eyes) bends together when their materials enable 変形（デフォーマ）.
    /// Call Punch / Squash / Puff from gameplay code to add reactions.
    /// </summary>
    [ExecuteAlways, DisallowMultipleComponent, AddComponentMenu("Team Toon/オバケの変形（デフォーマ）")]
    public sealed class TeamToonGhostDeformer : MonoBehaviour
    {
        [Tooltip("変形させるRenderer。体・手・目など、同じオバケの部位をすべて指定します。各Materialで『変形（デフォーマ）を使用』をONにしてください。")]
        public Renderer[] renderers = new Renderer[0];
        [Tooltip("変形の基準。アニメーションで動く頭や体の骨を指定できます。未指定時はこのオブジェクト。")]
        public Transform anchor;
        [Tooltip("変形が始まる位置（根元、anchorのローカル座標）。ここから先端に向かって効きが強くなります。")]
        public Vector3 localRoot = new Vector3(0, 1, 0);
        [Tooltip("根元→先端（尻尾）の方向（anchorのローカル）。")]
        public Vector3 localTipDirection = Vector3.down;
        [Min(0.01f), Tooltip("根元→先端の長さ（anchorのローカル単位）。")]
        public float length = 1;
        [Tooltip("伸び縮みの中心（anchorのローカル座標）。")]
        public Vector3 localCenter = new Vector3(0, 0.5f, 0);

        [Header("引きずり（先端が動きに遅れる）")]
        [Tooltip("速度1m/sあたりの先端の遅れ（m）。")] public float dragPerSpeed = 0.12f;
        [Tooltip("回転したときの先端の遅れ。")] public float turnDrag = 0.08f;
        [Min(0)] public float maxDrag = 0.6f;
        [Tooltip("戻る速さ（Hz）。小さいほどゆったり。")] [Min(0.1f)] public float dragFrequency = 2.2f;
        [Tooltip("1で揺り戻しなし、小さいほどプルプル揺れます。")] [Range(0.05f, 1)] public float dragDamping = 0.35f;

        [Header("伸び縮み（スクワッシュ＆ストレッチ）")]
        [Tooltip("速度1m/sあたりの伸び率。")] public float stretchPerSpeed = 0.06f;
        [Min(0)] public float maxStretch = 0.35f;
        [Min(0.1f)] public float stretchFrequency = 3.5f;
        [Range(0.05f, 1)] public float stretchDamping = 0.3f;

        [Header("尻尾の揺れ")]
        [Tooltip("揺れる方向（anchorのローカル）。根元→先端の方向とは直交させます。")]
        public Vector3 localWaveDirection = Vector3.right;
        [Tooltip("止まっているときの揺れ幅（m）。")] public float idleWaveAmplitude = 0.03f;
        [Tooltip("速度1m/sあたりに増える揺れ幅（m）。")] public float waveAmplitudePerSpeed = 0.04f;
        [Min(0)] public float maxWaveAmplitude = 0.25f;
        [Tooltip("揺れの速さ（Hz）。")] public float waveFrequency = 1.2f;
        [Tooltip("速度1m/sあたりに増える揺れの速さ（Hz）。")] public float waveFrequencyPerSpeed = 0.5f;

        [Header("ふくらみ（呼吸）")]
        [Tooltip("法線方向のふくらみ（m）。")] public float breathAmplitude = 0.01f;
        public float breathFrequency = 0.5f;
        [Min(0.1f)] public float puffFrequency = 4;
        [Range(0.05f, 1)] public float puffDamping = 0.25f;

        [Header("入力")]
        [Tooltip("OFFにするとexternalVelocityを移動速度として使います（物理やネットワーク同期で動かす場合）。")]
        public bool useTransformVelocity = true;
        public Vector3 externalVelocity;
        [Tooltip("これ以上の1フレーム移動はワープとみなして変形をリセットします（m）。")]
        public float teleportDistance = 5;
        [Tooltip("変形で画面外判定されないよう、Play中に描画範囲を広げる量（m）。")]
        public float boundsPadding = 0.5f;

        /// <summary>Current world-space velocity used by the deformer.</summary>
        public Vector3 Velocity { get; private set; }

        static readonly int AnchorId = Shader.PropertyToID("_DeformAnchor"), AxisId = Shader.PropertyToID("_DeformAxis"),
            CenterId = Shader.PropertyToID("_DeformCenter"), StretchId = Shader.PropertyToID("_DeformStretch"),
            DragId = Shader.PropertyToID("_DeformDrag"), WaveId = Shader.PropertyToID("_DeformWave");
        MaterialPropertyBlock block;
        Vector3 lastPosition; Quaternion lastRotation; bool hasLast;
        Vector3 drag, dragVelocity;
        float stretch, stretchVelocity; Vector3 stretchDirection = Vector3.up;
        float puff, puffVelocity;
        float waveAmplitude, wavePhase, breathPhase;
        Bounds[] savedBounds;

        Transform Frame => anchor ? anchor : transform;

        /// <summary>Kicks the tip in a world direction (m/s), e.g. when hit or dashing.</summary>
        public void Punch(Vector3 worldImpulse) => dragVelocity += worldImpulse;
        /// <summary>Positive squashes along the motion, negative stretches. Springs back on its own.</summary>
        public void Squash(float amount) => stretchVelocity -= amount * stretchFrequency * Mathf.PI * 2;
        /// <summary>Inflates the whole body for a moment (m/s along the normal).</summary>
        public void Puff(float amount) => puffVelocity += amount;
        /// <summary>Writes the current shape to the renderers without advancing the simulation.</summary>
        public void Refresh() => Apply(0);
        public void ResetState()
        {
            drag = dragVelocity = Vector3.zero; stretch = stretchVelocity = puff = puffVelocity = 0;
            hasLast = false;
        }

        void Reset()
        {
            var found = GetComponentsInChildren<Renderer>();
            renderers = found;
            FitToRenderers();
        }

        [ContextMenu("Rendererの範囲から根元・長さ・中心を設定")]
        public void FitToRenderers()
        {
            if (renderers == null || renderers.Length == 0) return;
            var frame = Frame;
            Vector3 d = localTipDirection.sqrMagnitude > 0.0001f ? localTipDirection.normalized : Vector3.down;
            float min = float.MaxValue, max = float.MinValue; Vector3 lo = Vector3.one * float.MaxValue, hi = -lo;
            foreach (var r in renderers)
            {
                if (!r) continue;
                var b = r.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var local = frame.InverseTransformPoint(corner);
                    lo = Vector3.Min(lo, local); hi = Vector3.Max(hi, local);
                    float projection = Vector3.Dot(local, d);
                    min = Mathf.Min(min, projection); max = Mathf.Max(max, projection);
                }
            }
            if (min > max) return;
            localCenter = (lo + hi) * 0.5f;
            localRoot = localCenter + d * (min - Vector3.Dot(localCenter, d));
            length = Mathf.Max(max - min, 0.01f);
        }

        void OnEnable()
        {
            ResetState();
            ExpandBounds();
            Apply(0);
        }

        void LateUpdate() => Apply(Application.isPlaying ? Time.deltaTime : 0);
        void OnValidate() { if (isActiveAndEnabled) Refresh(); }

        void Apply(float dt)
        {
            var frame = Frame;
            Vector3 tipDirection = localTipDirection.sqrMagnitude > 0.0001f ? localTipDirection.normalized : Vector3.down;
            Vector3 tipVector = frame.TransformVector(tipDirection * length);
            float worldLength = Mathf.Max(tipVector.magnitude, 0.001f);
            Vector3 axis = tipVector / worldLength;

            Simulate(dt, frame, axis, worldLength);

            Vector3 waveDirection = Vector3.ProjectOnPlane(frame.TransformDirection(localWaveDirection), axis);
            waveDirection = waveDirection.sqrMagnitude > 1e-6f ? waveDirection.normalized : Vector3.zero;
            float inflate = breathAmplitude * Mathf.Sin(breathPhase) + puff;

            block ??= new MaterialPropertyBlock();
            if (renderers == null) return;
            foreach (var r in renderers)
            {
                if (!r) continue;
                r.GetPropertyBlock(block);
                block.SetVector(AnchorId, Point(frame.TransformPoint(localRoot), worldLength));
                block.SetVector(AxisId, Point(axis, 0));
                block.SetVector(CenterId, Point(frame.TransformPoint(localCenter), inflate));
                block.SetVector(StretchId, Point(stretchDirection, 1 + stretch));
                block.SetVector(DragId, Point(drag, 0));
                block.SetVector(WaveId, Point(waveDirection * waveAmplitude, wavePhase));
                r.SetPropertyBlock(block);
            }
        }

        void Simulate(float dt, Transform frame, Vector3 axis, float worldLength)
        {
            Vector3 position = frame.position; Quaternion rotation = frame.rotation;
            // First frame or a warp: no velocity this frame.
            if (!hasLast || (position - lastPosition).sqrMagnitude > teleportDistance * teleportDistance)
            {
                lastPosition = position; lastRotation = rotation; hasLast = true;
            }
            if (dt <= 0) { if (!Application.isPlaying) waveAmplitude = idleWaveAmplitude; return; }

            Velocity = useTransformVelocity ? (position - lastPosition) / dt : externalVelocity;
            (rotation * Quaternion.Inverse(lastRotation)).ToAngleAxis(out float angle, out Vector3 turnAxis);
            if (angle > 180) angle -= 360;
            Vector3 angularVelocity = float.IsFinite(turnAxis.x) ? turnAxis * (angle * Mathf.Deg2Rad / dt) : Vector3.zero;
            lastPosition = position; lastRotation = rotation;
            float speed = Velocity.magnitude;

            // The tip lags opposite to motion and turning; only the sideways part bends the body.
            Vector3 dragTarget = -Velocity * dragPerSpeed - Vector3.Cross(angularVelocity, axis * worldLength) * turnDrag;
            dragTarget = Vector3.ClampMagnitude(Vector3.ProjectOnPlane(dragTarget, axis), maxDrag);
            if (speed > 0.05f) stretchDirection = Vector3.Slerp(stretchDirection, Velocity / speed, 1 - Mathf.Exp(-10 * dt)).normalized;
            float stretchTarget = Mathf.Min(speed * stretchPerSpeed, maxStretch);

            // Fixed substeps keep the springs stable at low frame rates.
            int steps = Mathf.Clamp(Mathf.CeilToInt(dt * 120), 1, 8);
            float h = dt / steps;
            for (int i = 0; i < steps; i++)
            {
                Spring(ref drag, ref dragVelocity, dragTarget, dragFrequency, dragDamping, h);
                Spring(ref stretch, ref stretchVelocity, stretchTarget, stretchFrequency, stretchDamping, h);
                Spring(ref puff, ref puffVelocity, 0, puffFrequency, puffDamping, h);
            }
            drag = Vector3.ProjectOnPlane(Vector3.ClampMagnitude(drag, maxDrag * 1.5f), axis);
            stretch = Mathf.Clamp(stretch, -0.6f, maxStretch * 1.5f + 0.6f);

            float amplitudeTarget = Mathf.Min(idleWaveAmplitude + speed * waveAmplitudePerSpeed, maxWaveAmplitude);
            waveAmplitude = Mathf.Lerp(waveAmplitude, amplitudeTarget, 1 - Mathf.Exp(-4 * dt));
            wavePhase = Mathf.Repeat(wavePhase + dt * Mathf.PI * 2 * (waveFrequency + speed * waveFrequencyPerSpeed), Mathf.PI * 2);
            breathPhase = Mathf.Repeat(breathPhase + dt * Mathf.PI * 2 * breathFrequency, Mathf.PI * 2);
        }

        static void Spring(ref Vector3 x, ref Vector3 v, Vector3 target, float frequency, float damping, float h)
        {
            float w = frequency * Mathf.PI * 2;
            v += (w * w * (target - x) - 2 * damping * w * v) * h;
            x += v * h;
        }
        static void Spring(ref float x, ref float v, float target, float frequency, float damping, float h)
        {
            float w = frequency * Mathf.PI * 2;
            v += (w * w * (target - x) - 2 * damping * w * v) * h;
            x += v * h;
        }
        static Vector4 Point(Vector3 v, float w) => new Vector4(v.x, v.y, v.z, w);

        // Deformed vertices may leave the authored bounds; widen them in Play only so scenes stay clean.
        void ExpandBounds()
        {
            if (!Application.isPlaying || renderers == null) return;
            savedBounds = new Bounds[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (!r) continue;
                var skinned = r as SkinnedMeshRenderer;
                var b = skinned ? skinned.localBounds : r.localBounds; savedBounds[i] = b;
                Vector3 scale = r.transform.lossyScale;
                float pad = boundsPadding / Mathf.Max(Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y)), Mathf.Max(Mathf.Abs(scale.z), 0.0001f));
                b.Expand(pad * 2);
                if (skinned) skinned.localBounds = b; else r.localBounds = b;
            }
        }

        void OnDisable()
        {
            if (savedBounds != null && renderers != null)
                for (int i = 0; i < renderers.Length && i < savedBounds.Length; i++)
                {
                    if (renderers[i] is SkinnedMeshRenderer skinned) skinned.localBounds = savedBounds[i];
                    else if (renderers[i]) renderers[i].ResetLocalBounds();
                }
            savedBounds = null;
            if (renderers == null) return;
            block ??= new MaterialPropertyBlock();
            foreach (var r in renderers)
            {
                if (!r) continue;
                // Preserve other components' overrides and restore the material's identity values.
                r.GetPropertyBlock(block);
                Material material = r.sharedMaterial;
                if (!TeamToonMaterial.IsSupported(material)) continue;
                foreach (int id in new[] { AnchorId, AxisId, CenterId, StretchId, DragId, WaveId })
                    block.SetVector(id, material.GetVector(id));
                r.SetPropertyBlock(block);
            }
        }
    }
}
