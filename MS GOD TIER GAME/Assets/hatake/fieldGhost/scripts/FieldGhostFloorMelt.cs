using System.Collections;
using System.Collections.Generic;
using LightShaftLab;
using TeamToon;
using UnityEngine;

namespace FieldGhost
{
    /// <summary>
    /// 床の冷気にとける。床の霧が濃くなった所からお化けが伸び上がって現れ、道筋をふわふわ進み（通った跡の霧が割れる）、
    /// 最後は床へ押しつぶされるように霧へ溶ける。お化けは custom/hatake/ToonMaster のゴースト設定、
    /// 変形には TeamToonGhostDeformer を使う。霧は LightShaftLab の LocalFogVolume を演出中だけ置く。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Field Ghost/床の冷気にとける")]
    public sealed class FieldGhostFloorMelt : FieldGhostEvent
    {
        [Header("お化け")]
        [Tooltip("動かすお化け（ふだんは隠れている）。この位置が現れる場所（道筋が無いとき）。")]
        public Transform ghost;
        [Tooltip("お化けの部位。空ならお化けの子から自動で集める。")]
        public Renderer[] renderers = new Renderer[0];
        [Tooltip("変形。空ならお化けの子から探す。無くても動く（伸び縮みしないだけ）。")]
        public TeamToonGhostDeformer deformer;

        [Header("道筋")]
        [Tooltip("通る点（順番）。空ならその場で現れて溶ける。最初の点で現れ、最後の点で溶ける。")]
        public Transform[] path = new Transform[0];
        [Tooltip("進む速さ（m/秒）。")] [Min(0.05f)] public float moveSpeed = 1.1f;
        [Tooltip("進む向きへ体を向ける。")] public bool faceMovement = true;
        [Tooltip("モデルの正面のずれ（度）。正面が横を向くモデルの補正。")] public float yawOffset;
        [Tooltip("上下のふわふわ（m）。")] [Min(0)] public float bobAmplitude = 0.05f;
        [Tooltip("上下のふわふわの速さ（Hz）。")] [Min(0)] public float bobFrequency = 0.7f;

        [Header("現れる・溶ける")]
        [Tooltip("現れる時間（秒）。")] [Min(0.05f)] public float riseDuration = 1.4f;
        [Tooltip("溶ける前にその場で待つ時間（秒）。")] [Min(0)] public float lingerTime = 0.6f;
        [Tooltip("溶ける時間（秒）。")] [Min(0.05f)] public float meltDuration = 1.5f;
        [Tooltip("床の下へ沈む深さ（m）。")] [Min(0)] public float sinkDepth = 0.6f;
        [Tooltip("溶けるときのつぶれ具合（縦の倍率。小さいほど平たく広がる）。")] [Range(0.05f, 1)] public float meltSquash = 0.25f;
        [Tooltip("溶け始めのふくらみ（m/秒）。")] [Min(0)] public float meltPuff = 0.6f;

        [Header("霧")]
        [Tooltip("霧の演出を使う（LocalFogVolume を演出中だけ置く）。")]
        public bool useFog = true;
        [Tooltip("床の高さを探すレイヤー。見つからないときはお化けの位置 - floorFallbackDepth。")]
        public LayerMask floorMask = ~0;
        [Tooltip("床が見つからないとき、お化けの位置からどれだけ下を床とみなすか（m）。")] [Min(0)] public float floorFallbackDepth = 0.8f;
        [Tooltip("霧の塊の中心の、床からの高さ（m）。")] [Min(0)] public float fogHeight = 0.15f;
        [Tooltip("現れる所・溶ける所で濃くなる霧の濃さ。")] [Range(0, 0.5f)] public float puffDensity = 0.18f;
        [Tooltip("濃くなる霧の大きさ（m。始め→終わりに広がる）。")] public Vector3 puffStartSize = new Vector3(1.6f, 0.35f, 1.6f);
        public Vector3 puffEndSize = new Vector3(3.2f, 0.6f, 3.2f);
        [Tooltip("濃くなった霧が消えるまで（秒）。")] [Min(0.1f)] public float puffLife = 3.5f;
        [Tooltip("霧の色。")] public Color puffTint = new Color(0.75f, 0.85f, 1f);
        [Tooltip("通った跡で霧を抜く強さ。")] [Range(0, 0.5f)] public float trailStrength = 0.2f;
        [Tooltip("跡の大きさ（m）。")] [Min(0.05f)] public float trailSize = 1.0f;
        [Tooltip("跡を置く間隔（m）。")] [Min(0.05f)] public float trailSpacing = 0.45f;
        [Tooltip("跡が埋まるまで（秒）。")] [Min(0.1f)] public float trailLife = 2.5f;

        sealed class FogBlob
        {
            public LocalFogVolume volume;
            public float age, life, peak, attack;
            public Vector3 startSize, endSize;
        }

        readonly List<FogBlob> blobs = new List<FogBlob>();
        Vector3 homePosition;
        Quaternion homeRotation;
        bool homeRecorded;
        float floorY;
        Vector3 lastTrailPoint;
        bool hasTrailPoint;

        protected override Vector3 FocusPoint
        {
            get
            {
                if (path != null && path.Length > 0 && path[0])
                {
                    return path[0].position;
                }
                if (homeRecorded)
                {
                    return homePosition;
                }
                return ghost ? ghost.position : transform.position;
            }
        }

        protected override void OnEnable()
        {
            if (ghost && !homeRecorded)
            {
                homePosition = ghost.position;
                homeRotation = ghost.rotation;
                homeRecorded = true;
            }
            CollectParts();
            base.OnEnable();
        }

        void OnDestroy()
        {
            foreach (FogBlob blob in blobs)
            {
                if (blob.volume)
                {
                    Destroy(blob.volume.gameObject);
                }
            }
            blobs.Clear();
        }

        void CollectParts()
        {
            if (!ghost)
            {
                return;
            }
            if (renderers == null || renderers.Length == 0)
            {
                renderers = ghost.GetComponentsInChildren<Renderer>(true);
            }
            if (!deformer)
            {
                deformer = ghost.GetComponentInChildren<TeamToonGhostDeformer>(true);
            }
        }

        protected override void ResetVisual()
        {
            if (!ghost)
            {
                return;
            }
            if (deformer)
            {
                deformer.ClearStretchOverride();
            }
            FieldGhostLook.SetVisibility(renderers, 1);
            if (homeRecorded)
            {
                ghost.SetPositionAndRotation(homePosition, homeRotation);
            }
            HideGhost(ghost);
            if (IsStopping)
            {
                ClearFog();
            }
        }

        // 途中で止めたときに霧の塊が残らないようにする（最後まで終わったときは自然に消えるのを待つ）。
        void ClearFog()
        {
            foreach (FogBlob blob in blobs)
            {
                if (blob.volume)
                {
                    blob.volume.m_density = 0;
                    if (!IsDisabling)
                    {
                        blob.volume.gameObject.SetActive(false);
                    }
                }
            }
        }

        protected override void Update()
        {
            base.Update();
            UpdateFog(Time.deltaTime);
        }

        protected override IEnumerator Sequence()
        {
            if (!ghost)
            {
                Debug.LogWarning("FieldGhostFloorMelt: お化け（ghost）が未指定です。", this);
                yield break;
            }
            string unsupported = FieldGhostLook.FindUnsupported(renderers);
            if (unsupported != null)
            {
                Debug.LogWarning($"FieldGhostFloorMelt: {unsupported} のマテリアルがゴースト用（custom/hatake/ToonMaster）ではないため、薄れる演出が効きません。", this);
            }

            List<Vector3> points = PathPoints();
            Vector3 start = points[0];
            floorY = FindFloor(start);
            hasTrailPoint = false;
            Quaternion facing = homeRotation;

            // 1. 霧が濃くなり、そこから伸び上がって現れる。
            ghost.SetPositionAndRotation(start - Vector3.up * sinkDepth, facing);
            FieldGhostLook.SetVisibility(renderers, 0);
            ghost.gameObject.SetActive(true);
            SpawnPuff(start);
            for (float time = 0; time < riseDuration; time += Time.deltaTime)
            {
                float t = time / riseDuration;
                float up = EaseOut(t);
                ghost.position = start - Vector3.up * (sinkDepth * (1 - up));
                FieldGhostLook.SetVisibility(renderers, Smooth(t * 1.4f));
                // 平たい霧から縦に伸びて元の形へ。
                SetSquash(Mathf.Lerp(meltSquash, 1.15f, up), 1 - Smooth((t - 0.6f) / 0.4f));
                yield return null;
            }
            SetSquash(1, 0);
            FieldGhostLook.SetVisibility(renderers, 1);

            // 2. 道筋を進む。通った跡の霧が割れる。
            float bobTime = 0;
            Vector3 lastPathPosition = start;
            for (int i = 0; i + 1 < points.Count; i++)
            {
                float length = Vector3.Distance(points[i], points[i + 1]);
                float duration = Mathf.Max(length / moveSpeed, 0.05f);
                for (float time = 0; time < duration; time += Time.deltaTime)
                {
                    float t = time / duration;
                    Vector3 position = CatmullRom(points, i, t);
                    bobTime += Time.deltaTime;
                    Vector3 bob = Vector3.up * (Mathf.Sin(bobTime * bobFrequency * Mathf.PI * 2) * bobAmplitude);
                    Vector3 delta = position - lastPathPosition;
                    lastPathPosition = position;
                    if (faceMovement && new Vector3(delta.x, 0, delta.z).sqrMagnitude > 1e-6f)
                    {
                        Quaternion target = Quaternion.LookRotation(new Vector3(delta.x, 0, delta.z)) * Quaternion.Euler(0, yawOffset, 0);
                        facing = Quaternion.Slerp(ghost.rotation, target, 1 - Mathf.Exp(-5 * Time.deltaTime));
                    }
                    ghost.SetPositionAndRotation(position + bob, facing);
                    LeaveTrail(ghost.position);
                    yield return null;
                }
            }

            // 3. その場で少し待ってから、床へつぶれながら霧に溶ける。
            for (float time = 0; time < lingerTime; time += Time.deltaTime)
            {
                yield return null;
            }
            Vector3 meltStart = ghost.position;
            if (deformer)
            {
                deformer.Puff(meltPuff);
            }
            SpawnPuff(meltStart);
            for (float time = 0; time < meltDuration; time += Time.deltaTime)
            {
                float t = time / meltDuration;
                ghost.position = meltStart - Vector3.up * (sinkDepth * EaseIn(t));
                SetSquash(Mathf.Lerp(1, meltSquash, EaseOut(t)), Smooth(t * 2));
                FieldGhostLook.SetVisibility(renderers, 1 - EaseIn(t));
                yield return null;
            }
            FieldGhostLook.SetVisibility(renderers, 0);
            ghost.gameObject.SetActive(false);
        }

        void SetSquash(float factor, float weight)
        {
            if (deformer)
            {
                deformer.SetStretchOverride(Vector3.up, factor, weight);
            }
        }

        List<Vector3> PathPoints()
        {
            var points = new List<Vector3>();
            if (path != null)
            {
                foreach (Transform point in path)
                {
                    if (point)
                    {
                        points.Add(point.position);
                    }
                }
            }
            if (points.Count == 0)
            {
                points.Add(homeRecorded ? homePosition : ghost.position);
            }
            return points;
        }

        // 点 i と i+1 の間をなめらかに結ぶ（端は同じ点を繰り返す）。
        static Vector3 CatmullRom(List<Vector3> points, int i, float t)
        {
            Vector3 p0 = points[Mathf.Max(i - 1, 0)];
            Vector3 p1 = points[i];
            Vector3 p2 = points[i + 1];
            Vector3 p3 = points[Mathf.Min(i + 2, points.Count - 1)];
            float t2 = t * t;
            float t3 = t2 * t;
            return 0.5f * (2 * p1 + (p2 - p0) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t2 + (3 * p1 - p0 - 3 * p2 + p3) * t3);
        }

        float FindFloor(Vector3 from)
        {
            if (Physics.Raycast(from + Vector3.up * 0.2f, Vector3.down, out RaycastHit hit, 10, floorMask, QueryTriggerInteraction.Ignore))
            {
                return hit.point.y;
            }
            return from.y - floorFallbackDepth;
        }

        // ---- 霧 ----

        void SpawnPuff(Vector3 at)
        {
            if (!useFog || puffDensity <= 0)
            {
                return;
            }
            FogBlob blob = Take(false);
            blob.volume.transform.position = new Vector3(at.x, floorY + fogHeight, at.z);
            blob.life = puffLife;
            blob.peak = puffDensity;
            blob.attack = 0.3f;
            blob.startSize = puffStartSize;
            blob.endSize = puffEndSize;
            blob.volume.m_tint = puffTint;
            Apply(blob);
        }

        void LeaveTrail(Vector3 at)
        {
            if (!useFog || trailStrength <= 0)
            {
                return;
            }
            Vector3 floorPoint = new Vector3(at.x, floorY + fogHeight, at.z);
            if (hasTrailPoint && (floorPoint - lastTrailPoint).sqrMagnitude < trailSpacing * trailSpacing)
            {
                return;
            }
            hasTrailPoint = true;
            lastTrailPoint = floorPoint;
            FogBlob blob = Take(true);
            blob.volume.transform.position = floorPoint;
            blob.life = trailLife;
            blob.peak = trailStrength;
            blob.attack = 0.08f;
            // 跡は少し広がりながら埋まる。
            blob.startSize = new Vector3(trailSize, trailSize * 0.6f, trailSize);
            blob.endSize = blob.startSize * 1.4f;
            Apply(blob);
        }

        // 使っていない霧の塊を取り出す（無ければ作る）。LocalFogVolume は同時に 16 個までしか描かれないので、跡は使い回す。
        FogBlob Take(bool subtract)
        {
            FogBlob found = null;
            foreach (FogBlob blob in blobs)
            {
                if (!blob.volume.gameObject.activeSelf && blob.volume.m_subtractFlag == subtract)
                {
                    found = blob;
                    break;
                }
            }
            if (found == null)
            {
                const int maxTrail = 6;
                int trailCount = 0;
                FogBlob oldest = null;
                foreach (FogBlob blob in blobs)
                {
                    if (blob.volume.m_subtractFlag == subtract)
                    {
                        trailCount++;
                        if (oldest == null || blob.age / blob.life > oldest.age / oldest.life)
                        {
                            oldest = blob;
                        }
                    }
                }
                if (subtract && trailCount >= maxTrail && oldest != null)
                {
                    found = oldest;
                }
                else
                {
                    var go = new GameObject(subtract ? "FieldGhost Fog Trail" : "FieldGhost Fog Puff");
                    go.transform.SetParent(transform, true);
                    go.SetActive(false);
                    var volume = go.AddComponent<LocalFogVolume>();
                    volume.m_shape = LocalFogVolume.SHAPE.SPHERE;
                    volume.m_subtractFlag = subtract;
                    volume.m_edgeFade = 1;
                    // 霧は全体で 16 個までしか描かれないので、跡は先に落とされるよう優先度を下げる。
                    volume.m_priority = subtract ? 0.5f : 1;
                    volume.m_density = 0;
                    found = new FogBlob { volume = volume };
                    blobs.Add(found);
                }
            }
            found.age = 0;
            found.volume.gameObject.SetActive(true);
            return found;
        }

        void UpdateFog(float dt)
        {
            foreach (FogBlob blob in blobs)
            {
                if (!blob.volume || !blob.volume.gameObject.activeSelf)
                {
                    continue;
                }
                blob.age += dt;
                if (blob.age >= blob.life)
                {
                    blob.volume.m_density = 0;
                    blob.volume.gameObject.SetActive(false);
                    continue;
                }
                Apply(blob);
            }
        }

        void Apply(FogBlob blob)
        {
            float t = Mathf.Clamp01(blob.age / blob.life);
            // すっと立ち上がり、ゆっくり消える。
            float envelope = t < blob.attack ? Smooth(t / blob.attack) : 1 - Smooth((t - blob.attack) / (1 - blob.attack));
            blob.volume.m_density = Mathf.Clamp(blob.peak * envelope, 0, 0.5f);
            Vector3 size = Vector3.Lerp(blob.startSize, blob.endSize, EaseOut(t));
            Vector3 parentScale = transform.lossyScale;
            blob.volume.transform.localScale = new Vector3(
                size.x / Mathf.Max(Mathf.Abs(parentScale.x), 1e-4f),
                size.y / Mathf.Max(Mathf.Abs(parentScale.y), 1e-4f),
                size.z / Mathf.Max(Mathf.Abs(parentScale.z), 1e-4f));
        }

        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();
            if (path == null)
            {
                return;
            }
            Gizmos.color = new Color(0.7f, 0.85f, 1f, 0.9f);
            for (int i = 0; i + 1 < path.Length; i++)
            {
                if (path[i] && path[i + 1])
                {
                    Gizmos.DrawLine(path[i].position, path[i + 1].position);
                }
            }
        }
    }
}
