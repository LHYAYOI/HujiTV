using System.Collections.Generic;
using UnityEngine;

namespace LightShaftLab
{
    [AddComponentMenu(""), ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(Collider))]
    public sealed class FogInteractor : MonoBehaviour
    {
        [Min(0)] public float m_strength = 1;
        [Min(.01f), Tooltip("物体の外側に流れを注入する幅（m）。")]
        public float m_influence = .6f;
        [Min(.01f)] public float m_maxSpeed = 15;
        [Min(.1f)] public float m_teleportDistance = 4;
        public static readonly List<FogInteractor> g_active = new List<FogInteractor>();
        Collider m_collider;
        Rigidbody m_body;
        Vector3 m_previousCenter;
        Quaternion m_previousRotation;
        bool m_initializedFlag;
        public Vector3 Velocity { get; private set; }
        public Vector3 AngularVelocity { get; private set; }
        public Vector3 SweepStart { get; private set; }
        public Vector3 A { get; private set; }
        public Vector3 B { get; private set; }
        public float Radius { get; private set; }
        public Bounds Bounds { get; private set; }
        public bool IsBox => m_collider is BoxCollider;
        public Matrix4x4 BoxTransform { get; private set; }
        public float BoxMinimumSide { get; private set; }
        void OnEnable()
        {
            m_collider = GetComponent<Collider>(); m_body = GetComponentInParent<Rigidbody>();
            m_initializedFlag = false; if (!g_active.Contains(this)) g_active.Add(this);
        }
        void OnDisable() { g_active.Remove(this); m_initializedFlag = false; }
        // 現在のコライダー形状と速度・角速度を取得する。大きく瞬間移動した場合は速度を 0 にして掃引を行わない。
        public void Capture(float deltaTime)
        {
            if (!m_collider) m_collider = GetComponent<Collider>();
            if (!m_collider || !m_collider.enabled) { Bounds = new Bounds(transform.position, Vector3.zero); m_initializedFlag = false; return; }
            var bounds = m_collider.bounds; var center = bounds.center;
            bool jumpedFlag = !m_initializedFlag || Vector3.Distance(center, m_previousCenter) > m_teleportDistance;
            SweepStart = jumpedFlag ? center : m_previousCenter;
            bool usePhysicsVelocityFlag = m_body && !m_body.isKinematic;
            Velocity = jumpedFlag ? Vector3.zero : usePhysicsVelocityFlag ? m_body.GetPointVelocity(center) : (center - m_previousCenter) / Mathf.Max(deltaTime, .0001f);
            Velocity = Vector3.ClampMagnitude(Velocity, m_maxSpeed);
            AngularVelocity = Vector3.zero;
            if (!jumpedFlag)
            {
                if (usePhysicsVelocityFlag) AngularVelocity = m_body.angularVelocity;
                else
                {
                    var rotationDelta = transform.rotation * Quaternion.Inverse(m_previousRotation);
                    rotationDelta.ToAngleAxis(out float angle, out Vector3 axis);
                    if (angle > 180) angle -= 360;
                    if (Mathf.Abs(angle) > .0001f && !float.IsNaN(axis.x)) AngularVelocity = axis * (angle * Mathf.Deg2Rad / Mathf.Max(deltaTime, .0001f));
                }
            }
            A = B = center; Radius = bounds.extents.magnitude;
            var scale = transform.lossyScale;
            scale = new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
            if (m_collider is SphereCollider sphere)
            {
                A = B = transform.TransformPoint(sphere.center); Radius = sphere.radius * Mathf.Max(scale.x, Mathf.Max(scale.y, scale.z));
            }
            else if (m_collider is CapsuleCollider capsule)
            {
                Vector3 axis = capsule.direction == 0 ? Vector3.right : capsule.direction == 1 ? Vector3.up : Vector3.forward;
                float axisScale = scale[capsule.direction];
                float radialScale = capsule.direction == 0 ? Mathf.Max(scale.y, scale.z) : capsule.direction == 1 ? Mathf.Max(scale.x, scale.z) : Mathf.Max(scale.x, scale.y);
                Radius = capsule.radius * radialScale;
                var offset = transform.TransformDirection(axis) * Mathf.Max(0, capsule.height * axisScale * .5f - Radius);
                var capsuleCenter = transform.TransformPoint(capsule.center); A = capsuleCenter - offset; B = capsuleCenter + offset;
            }
            else if (m_collider is BoxCollider box)
            {
                BoxTransform = Matrix4x4.TRS(box.center, Quaternion.identity, box.size).inverse * transform.worldToLocalMatrix;
                BoxMinimumSide = Mathf.Min(box.size.x * scale.x, Mathf.Min(box.size.y * scale.y, box.size.z * scale.z));
            }
            m_previousCenter = center; m_previousRotation = transform.rotation; m_initializedFlag = true;
            Bounds = bounds; var swept = Bounds; swept.Encapsulate(SweepStart); swept.Expand((Radius + m_influence) * 2); Bounds = swept;
        }
        public bool IsAvailable => isActiveAndEnabled && m_collider && m_collider.enabled && m_strength > 0;
    }
}
