using System.Collections;
using TeamToon;
using UnityEngine;

namespace FieldGhost
{
    /// <summary>
    /// 壁をすり抜ける。このオブジェクトを壁の表面に置き、青の矢印（+Z）を部屋側へ向ける。
    /// 「のぞく」: 壁から上半身だけ出てこちらを見て、ねじれながら引っ込む。
    /// 「通り抜ける」: 壁から出て部屋を横切り、向かいの壁（exitWall）へ入っていく。
    /// お化けは壁の面で切られ（壁の中の部分は描かない）、切り口がほのかに光る。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Field Ghost/壁をすり抜ける")]
    public sealed class FieldGhostWallPeek : FieldGhostEvent
    {
        public enum Mode
        {
            [InspectorName("のぞいて引っ込む")] Peek,
            [InspectorName("部屋を通り抜ける")] PassThrough,
        }

        [Header("お化け")]
        [Tooltip("動かすお化け（ふだんは隠れている）。")]
        public Transform ghost;
        [Tooltip("お化けの部位。空ならお化けの子から自動で集める。")]
        public Renderer[] renderers = new Renderer[0];
        [Tooltip("変形。空ならお化けの子から探す。")]
        public TeamToonGhostDeformer deformer;
        [Tooltip("モデルの正面のずれ（度）。")] public float yawOffset;

        [Header("動き")]
        public Mode mode = Mode.Peek;
        [Tooltip("始めに壁の奥へ隠れている深さ（m）。お化けの大きさより深く。")] [Min(0)] public float hiddenDepth = 0.8f;
        [Tooltip("のぞくとき、壁から出る量（m）。")] [Min(0)] public float peekDepth = 0.45f;
        [Tooltip("出てくる時間（秒）。")] [Min(0.05f)] public float peekDuration = 0.9f;
        [Tooltip("出たまま見ている時間（秒）。")] [Min(0)] public float holdDuration = 1.4f;
        [Tooltip("首をかしげる角度（度）。")] [Range(0, 45)] public float tiltAngle = 14;
        [Tooltip("こちらへ顔を向ける最大の角度（度）。")] [Range(0, 90)] public float maxLookAngle = 35;
        [Tooltip("引っ込む時間（秒）。短いほど『サッ』と。")] [Min(0.05f)] public float retractDuration = 0.35f;
        [Tooltip("引っ込むときのねじれ（度）。")] [Range(0, 360)] public float twistAngle = 70;
        [Tooltip("引っ込むときに横へはじく強さ（m/秒）。")] [Min(0)] public float retractPunch = 1.2f;

        [Header("通り抜ける")]
        [Tooltip("入っていく壁。壁の表面に置き、+Z を部屋側へ向ける。")]
        public Transform exitWall;
        [Tooltip("横切る速さ（m/秒）。")] [Min(0.05f)] public float passSpeed = 1.3f;

        [Header("切り口")]
        [Tooltip("切り口の発光色（HDR）。")]
        [ColorUsage(false, true)] public Color sliceGlowColor = new Color(0.55f, 0.75f, 1f, 1f);
        [Tooltip("切り口の発光の幅（m）。")] [Range(0.001f, 0.5f)] public float sliceGlowWidth = 0.06f;

        Vector3 homePosition;
        Quaternion homeRotation;
        bool homeRecorded;

        protected override Vector3 FocusPoint => transform.position;

        protected override void OnEnable()
        {
            if (ghost && !homeRecorded)
            {
                homePosition = ghost.position;
                homeRotation = ghost.rotation;
                homeRecorded = true;
            }
            if (ghost && (renderers == null || renderers.Length == 0))
            {
                renderers = ghost.GetComponentsInChildren<Renderer>(true);
            }
            if (ghost && !deformer)
            {
                deformer = ghost.GetComponentInChildren<TeamToonGhostDeformer>(true);
            }
            base.OnEnable();
        }

        protected override void ResetVisual()
        {
            if (!ghost)
            {
                return;
            }
            FieldGhostLook.ClearSlice(renderers);
            FieldGhostLook.SetVisibility(renderers, 1);
            if (homeRecorded)
            {
                ghost.SetPositionAndRotation(homePosition, homeRotation);
            }
            HideGhost(ghost);
        }

        protected override IEnumerator Sequence()
        {
            if (!ghost)
            {
                Debug.LogWarning("FieldGhostWallPeek: お化け（ghost）が未指定です。", this);
                yield break;
            }
            string unsupported = FieldGhostLook.FindUnsupported(renderers);
            if (unsupported != null)
            {
                Debug.LogWarning($"FieldGhostWallPeek: {unsupported} のマテリアルがゴースト用（custom/hatake/ToonMaster）ではないため、切り口が効きません。", this);
            }
            if (mode == Mode.PassThrough && exitWall)
            {
                yield return PassThrough();
            }
            else
            {
                yield return Peek();
            }
            FieldGhostLook.SetVisibility(renderers, 0);
            ghost.gameObject.SetActive(false);
        }

        IEnumerator Peek()
        {
            Vector3 wall = transform.position;
            Vector3 forward = transform.forward;
            Quaternion facing = Quaternion.LookRotation(forward, Vector3.up) * Quaternion.Euler(0, yawOffset, 0);
            Vector3 hidden = wall - forward * hiddenDepth;
            Vector3 outside = wall + forward * peekDepth;

            ghost.SetPositionAndRotation(hidden, facing);
            FieldGhostLook.SetVisibility(renderers, 0);
            ghost.gameObject.SetActive(true);
            FieldGhostLook.SetSlice(renderers, wall, forward, sliceGlowColor, sliceGlowWidth);

            // 1. ぬっと出てくる。
            for (float time = 0; time < peekDuration; time += Time.deltaTime)
            {
                float t = time / peekDuration;
                ghost.position = Vector3.LerpUnclamped(hidden, outside, EaseOut(t));
                FieldGhostLook.SetVisibility(renderers, Smooth(t * 3));
                yield return null;
            }
            FieldGhostLook.SetVisibility(renderers, 1);

            // 2. こちらを見て、首をかしげる。
            Quaternion look = facing;
            for (float time = 0; time < holdDuration; time += Time.deltaTime)
            {
                float t = time / Mathf.Max(holdDuration, 0.001f);
                Quaternion toward = LookTowardViewer(facing, ghost.position);
                look = Quaternion.Slerp(look, toward, 1 - Mathf.Exp(-4 * Time.deltaTime));
                float tilt = Mathf.Sin(Smooth(t) * Mathf.PI) * tiltAngle;
                ghost.rotation = look * Quaternion.Euler(0, 0, tilt);
                yield return null;
            }

            // 3. ねじれながらサッと引っ込む。
            if (deformer)
            {
                deformer.Punch(transform.right * retractPunch);
            }
            Quaternion retractStart = ghost.rotation;
            for (float time = 0; time < retractDuration; time += Time.deltaTime)
            {
                float t = time / retractDuration;
                ghost.position = Vector3.LerpUnclamped(outside, hidden, EaseIn(t));
                ghost.rotation = Quaternion.AngleAxis(twistAngle * EaseIn(t), forward) * retractStart;
                FieldGhostLook.SetVisibility(renderers, 1 - Smooth((t - 0.4f) / 0.6f));
                yield return null;
            }
        }

        IEnumerator PassThrough()
        {
            Vector3 wallA = transform.position;
            Vector3 normalA = transform.forward;
            Vector3 wallB = exitWall.position;
            Vector3 normalB = exitWall.forward;
            Vector3 from = wallA - normalA * hiddenDepth;
            Vector3 to = wallB - normalB * hiddenDepth;
            Vector3 direction = to - from;
            Vector3 flat = new Vector3(direction.x, 0, direction.z);
            Quaternion facing = (flat.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(flat) : transform.rotation) * Quaternion.Euler(0, yawOffset, 0);

            ghost.SetPositionAndRotation(from, facing);
            FieldGhostLook.SetVisibility(renderers, 1);
            ghost.gameObject.SetActive(true);

            float duration = Mathf.Max(direction.magnitude / passSpeed, 0.05f);
            for (float time = 0; time < duration; time += Time.deltaTime)
            {
                float t = time / duration;
                // 速さは一定、出入りの所だけ少しゆっくり。
                Vector3 position = Vector3.LerpUnclamped(from, to, Mathf.Lerp(t, Smooth(t), 0.35f));
                position += Vector3.up * (Mathf.Sin(time * Mathf.PI * 1.4f) * 0.04f);
                ghost.position = position;
                // 近い方の壁で切る。
                bool nearA = (position - wallA).sqrMagnitude <= (position - wallB).sqrMagnitude;
                FieldGhostLook.SetSlice(renderers, nearA ? wallA : wallB, nearA ? normalA : normalB, sliceGlowColor, sliceGlowWidth);
                yield return null;
            }
        }

        // 見ている人（Main Camera）の方へ、正面から maxLookAngle までだけ顔を向ける。
        Quaternion LookTowardViewer(Quaternion facing, Vector3 from)
        {
            Camera main = Camera.main;
            if (!main || maxLookAngle <= 0)
            {
                return facing;
            }
            Vector3 toViewer = main.transform.position - from;
            if (toViewer.sqrMagnitude < 1e-6f)
            {
                return facing;
            }
            Quaternion full = Quaternion.LookRotation(toViewer.normalized, Vector3.up) * Quaternion.Euler(0, yawOffset, 0);
            return Quaternion.RotateTowards(facing, full, maxLookAngle);
        }

        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();
            Gizmos.color = new Color(0.55f, 0.75f, 1f, 0.9f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(1.2f, 1.6f, 0));
            Gizmos.DrawLine(Vector3.zero, Vector3.forward * 0.5f);
            if (exitWall)
            {
                Gizmos.matrix = Matrix4x4.identity;
                Gizmos.DrawLine(transform.position, exitWall.position);
            }
        }
    }
}
