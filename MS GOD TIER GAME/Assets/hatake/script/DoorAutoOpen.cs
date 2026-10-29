using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 対象（トロッコのカメラなど）が近づいたら、ドアを蝶番まわりに回して自動で開ける。
/// 蝶番の位置はメッシュの外形から求めるので、モデルのピボットが端になくても正しく回る。
/// 開くときに DoorSmoke（床を這う煙）へ開き具合を渡せる。
/// DoorFogProfile を差すと、Play 時にドアの足元へ床霧と煙パーティクルを自動で作り、開いた向きへ流れ出させる。
/// </summary>
[DisallowMultipleComponent, AddComponentMenu("hatake/Door Auto Open")]
public sealed class DoorAutoOpen : MonoBehaviour
{
    public enum HINGE_SIDE
    {
        // ピボットに近い方の縦の辺を蝶番にする
        AUTO = 0,
        // ピボットそのものを蝶番にする
        PIVOT = 1,
        // メッシュのローカル X が小さい側の辺
        MIN_X = 2,
        // メッシュのローカル X が大きい側の辺
        MAX_X = 3
    }

    public enum SWING_DIRECTION
    {
        // 対象から遠ざかる向きに開く（トロッコにぶつからない）
        AWAY_FROM_TARGET = 0,
        // 対象の方へ開く
        TOWARD_TARGET = 1,
        // 蝶番の軸まわりに正の向き
        POSITIVE = 2,
        // 蝶番の軸まわりに負の向き
        NEGATIVE = 3
    }

    [Header("ドア")]
    [Tooltip("回すドアのメッシュ。未指定なら子の最初の MeshRenderer。")]
    [SerializeField] private Transform m_door;
    [Tooltip("蝶番にする辺。")]
    [SerializeField] private HINGE_SIDE m_hingeSide = HINGE_SIDE.AUTO;
    [Tooltip("開く向き。")]
    [SerializeField] private SWING_DIRECTION m_swingDirection = SWING_DIRECTION.AWAY_FROM_TARGET;
    [Range(0, 180), Tooltip("開く角度（度）。")]
    [SerializeField] private float m_openAngle = 100;

    [Header("近づいたら開く")]
    [Tooltip("近づくと開ける対象。未指定ならメインカメラ。")]
    [SerializeField] private Transform m_target;
    [Min(0), Tooltip("この距離（m、水平）まで近づいたら開く。")]
    [SerializeField] private float m_openDistance = 6;
    [Tooltip("離れたら閉じる。")]
    [SerializeField] private bool m_autoCloseFlag;
    [Min(0), Tooltip("この距離より離れたら閉じる（Auto Close のとき）。開く距離より大きくする。")]
    [SerializeField] private float m_closeDistance = 10;

    [Header("動き")]
    [Min(0.01f), Tooltip("開ききるまでの秒数。")]
    [SerializeField] private float m_openSeconds = 1.4f;
    [Min(0.01f), Tooltip("閉じきるまでの秒数。")]
    [SerializeField] private float m_closeSeconds = 0.6f;
    [Tooltip("開き方（横 = 時間 0〜1、縦 = 開き具合 0〜1）。最初に少し引っかかってから開く。")]
    [SerializeField] private AnimationCurve m_openCurve = new AnimationCurve(
        new Keyframe(0, 0, 0, 0),
        new Keyframe(0.12f, 0.06f, 0.2f, 0.2f),
        new Keyframe(0.22f, 0.07f, 0.4f, 0.4f),
        new Keyframe(1, 1, 0, 0));

    [Header("ドアが開いたときの霧")]
    [Tooltip("霧の見た目のアセット。差すと Play 時にドアの足元へ床霧と煙を作る。下の Smoke を指定した場合は、その見た目を上書きする。")]
    [SerializeField] private DoorFogProfile m_fogProfile;
    [Tooltip("霧の出どころを、ドアの面から流れ出す向きへずらす距離（m）。")]
    [SerializeField] private float m_fogForwardOffset = 0.1f;
    [Tooltip("霧の出どころを、ドアの下の床に合わせる（下向きのレイで床を探す）。")]
    [SerializeField] private bool m_snapToFloorFlag = true;

    [Header("連動")]
    [Tooltip("床を這う煙。開き始めで Open、閉じ始めで Close、開き具合も渡す。プロファイルから自動で作る場合は空のままでよい。")]
    [SerializeField] private LightShaftLab.DoorSmoke m_smoke;
    [SerializeField] private UnityEvent m_onOpen = new UnityEvent();
    [SerializeField] private UnityEvent m_onClose = new UnityEvent();

    // ドアの親空間での、閉じた姿勢と蝶番
    private Vector3 m_closedLocalPosition;
    private Quaternion m_closedLocalRotation;
    private Vector3 m_hingeLocal;
    private Vector3 m_axisLocal;
    private Vector3 m_freeEdgeLocal;
    private bool m_initializedFlag;

    // 自動で作った霧
    private bool m_builtFogFlag;
    private Material m_runtimeMaterial;
    private Vector3 m_fogOriginWorld;

    private float m_progress;
    private float m_direction;
    private bool m_openFlag;
    private bool m_triggeredFlag;

    public bool IsOpen { get { return m_openFlag; } }
    public float Progress { get { return m_progress; } }

    private void Awake()
    {
        Initialize();
    }

    private void OnEnable()
    {
        if (m_fogProfile != null)
        {
            m_fogProfile.Changed += ApplyFogProfile;
        }
    }

    private void OnDisable()
    {
        if (m_fogProfile != null)
        {
            m_fogProfile.Changed -= ApplyFogProfile;
        }
    }

    private void OnDestroy()
    {
        if (m_runtimeMaterial != null)
        {
            Destroy(m_runtimeMaterial);
        }
    }

    // 閉じた姿勢と蝶番の位置を記録する
    private void Initialize()
    {
        if (m_door == null)
        {
            MeshRenderer meshRenderer = GetComponentInChildren<MeshRenderer>();
            m_door = meshRenderer != null ? meshRenderer.transform : transform;
        }
        m_closedLocalPosition = m_door.localPosition;
        m_closedLocalRotation = m_door.localRotation;
        ComputeHinge(out Vector3 hingeWorld, out Vector3 axisWorld, out Vector3 freeEdgeWorld);
        Transform parent = m_door.parent;
        m_hingeLocal = parent != null ? parent.InverseTransformPoint(hingeWorld) : hingeWorld;
        m_axisLocal = (parent != null ? parent.InverseTransformDirection(axisWorld) : axisWorld).normalized;
        m_freeEdgeLocal = parent != null ? parent.InverseTransformPoint(freeEdgeWorld) : freeEdgeWorld;
        m_direction = 1;
        m_initializedFlag = true;

        // 霧のプロファイルがあれば、煙一式を作るか、指定された煙に見た目を当てる
        if (m_fogProfile != null)
        {
            if (m_smoke == null)
            {
                BuildFog();
            }
            ApplyFogProfile();
        }
    }

    // ドアの足元の中央（閉じた姿勢）。床に合わせる設定なら下の床に置く
    private Vector3 ComputeFogOrigin()
    {
        Vector3 origin = m_door.position;
        MeshFilter meshFilter = m_door.GetComponent<MeshFilter>();
        if (meshFilter != null && meshFilter.sharedMesh != null)
        {
            Bounds bounds = meshFilter.sharedMesh.bounds;
            origin = m_door.TransformPoint(new Vector3(bounds.center.x, bounds.min.y, bounds.center.z));
        }
        if (m_snapToFloorFlag && Physics.Raycast(origin + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, 3, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            origin = hit.point;
        }
        return origin;
    }

    // 床霧と煙パーティクルを、このドアの子として作る
    private void BuildFog()
    {
        m_fogOriginWorld = ComputeFogOrigin();
        var root = new GameObject("DoorFog (auto)");
        root.transform.SetParent(transform, false);
        root.transform.position = m_fogOriginWorld;

        var fogObject = new GameObject("GroundFog");
        fogObject.transform.SetParent(root.transform, false);
        var fog = fogObject.AddComponent<LightShaftLab.LocalFogVolume>();
        fog.m_shape = LightShaftLab.LocalFogVolume.SHAPE.GROUND;
        fog.m_spreadOrigin = root.transform;

        var puffObject = new GameObject("Puffs");
        puffObject.transform.SetParent(root.transform, false);
        var puffs = puffObject.AddComponent<ParticleSystem>();
        puffs.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        m_smoke = root.AddComponent<LightShaftLab.DoorSmoke>();
        m_smoke.m_groundFog = fog;
        m_smoke.m_puffs = puffs;
        m_builtFogFlag = true;
        // 開くまでは何も見えないようにする
        ApplyFogProfile();
        m_smoke.ResetToClosed();
    }

    // 作った霧を、ドアの面から対象の側へ流れ出す向きに置く
    private void OrientFog(Vector3 targetWorld)
    {
        if (!m_builtFogFlag || m_smoke == null)
        {
            return;
        }
        Transform parent = m_door.parent;
        Vector3 axis = parent != null ? parent.TransformDirection(m_axisLocal) : m_axisLocal;
        Vector3 hinge = HingeWorld();
        Vector3 freeEdge = parent != null ? parent.TransformPoint(m_freeEdgeLocal) : m_freeEdgeLocal;
        Vector3 normal = Vector3.Cross(axis, freeEdge - hinge);
        normal.y = 0;
        if (normal.sqrMagnitude < 0.0001f)
        {
            normal = transform.forward;
            normal.y = 0;
        }
        normal.Normalize();
        Vector3 toTarget = targetWorld - m_fogOriginWorld;
        if (Vector3.Dot(normal, toTarget) < 0)
        {
            normal = -normal;
        }
        Transform root = m_smoke.transform;
        root.SetPositionAndRotation(m_fogOriginWorld + normal * m_fogForwardOffset, Quaternion.LookRotation(normal, Vector3.up));
    }

    // プロファイルの見た目を、煙一式へ書き込む（Play 中にプロファイルを変えたときも呼ばれる）
    private void ApplyFogProfile()
    {
        DoorFogProfile profile = m_fogProfile;
        if (profile == null || m_smoke == null)
        {
            return;
        }
        m_smoke.m_fogDensity = profile.m_groundFogFlag ? profile.m_fogDensity : 0;
        m_smoke.m_maxRadius = profile.m_maxRadius;
        m_smoke.m_spreadSeconds = profile.m_spreadSeconds;
        m_smoke.m_fadeInSeconds = profile.m_fadeInSeconds;
        m_smoke.m_fadeOutSeconds = profile.m_fadeOutSeconds;
        m_smoke.m_burstRate = profile.m_particlesFlag ? profile.m_burstRate : 0;
        m_smoke.m_sustainRatio = profile.m_sustainRatio;
        m_smoke.m_burstSeconds = profile.m_burstSeconds;

        LightShaftLab.LocalFogVolume fog = m_smoke.m_groundFog;
        if (fog != null)
        {
            fog.m_shape = LightShaftLab.LocalFogVolume.SHAPE.GROUND;
            fog.m_tint = profile.m_fogTint;
            fog.m_edgeFade = profile.m_edgeFade;
            fog.m_priority = profile.m_priority;
            fog.m_sunBoost = 0;
            fog.m_groundFill = profile.m_fill;
            fog.m_groundSoftness = profile.m_softness;
            fog.m_groundNoiseScale = profile.m_noiseScale;
            fog.m_groundNoiseAmount = profile.m_noiseAmount;
            fog.m_spreadEdge = profile.m_spreadEdge;
            fog.m_groundFlow = m_smoke.transform.forward * profile.m_flowSpeed;
            if (m_builtFogFlag)
            {
                // 範囲は出どころから流れ出す向きへ伸ばす
                fog.transform.localPosition = new Vector3(0, profile.m_areaHeight * 0.5f, profile.m_areaLength * 0.5f);
                fog.transform.localRotation = Quaternion.identity;
                fog.transform.localScale = new Vector3(profile.m_areaWidth, profile.m_areaHeight, profile.m_areaLength);
            }
        }

        ParticleSystem puffs = m_smoke.m_puffs;
        if (puffs != null)
        {
            LightShaftLab.DoorSmokeParticles.Configure(puffs, ParticleMaterial(profile), profile.m_particles, 0);
        }
    }

    private Material ParticleMaterial(DoorFogProfile profile)
    {
        if (profile.m_particleMaterial != null)
        {
            return profile.m_particleMaterial;
        }
        if (m_runtimeMaterial == null)
        {
            Shader shader = Shader.Find(LightShaftLab.DoorSmokeParticles.k_shaderName);
            if (shader == null)
            {
                Debug.LogWarning("煙のシェーダーが見つかりません。DoorFogProfile にマテリアルを設定してください。", this);
                return null;
            }
            m_runtimeMaterial = new Material(shader) { name = "SmokePuff (runtime)" };
        }
        return m_runtimeMaterial;
    }

    // メッシュの外形から、蝶番の点・軸と、反対側（開く側）の辺の点をワールドで求める
    private void ComputeHinge(out Vector3 hingeWorld, out Vector3 axisWorld, out Vector3 freeEdgeWorld)
    {
        Bounds bounds = new Bounds(Vector3.zero, Vector3.zero);
        MeshFilter meshFilter = m_door.GetComponent<MeshFilter>();
        if (meshFilter != null && meshFilter.sharedMesh != null)
        {
            bounds = meshFilter.sharedMesh.bounds;
        }
        float minX = bounds.min.x;
        float maxX = bounds.max.x;
        bool useMinFlag;
        switch (m_hingeSide)
        {
            case HINGE_SIDE.MIN_X:
                useMinFlag = true;
                break;
            case HINGE_SIDE.MAX_X:
                useMinFlag = false;
                break;
            default:
                useMinFlag = Mathf.Abs(minX) <= Mathf.Abs(maxX);
                break;
        }
        float hingeX = m_hingeSide == HINGE_SIDE.PIVOT ? 0 : (useMinFlag ? minX : maxX);
        float freeX = useMinFlag ? maxX : minX;
        Vector3 hingeLocal = new Vector3(hingeX, bounds.center.y, bounds.center.z);
        Vector3 freeLocal = new Vector3(freeX, bounds.center.y, bounds.center.z);
        hingeWorld = m_door.TransformPoint(hingeLocal);
        freeEdgeWorld = m_door.TransformPoint(freeLocal);
        axisWorld = m_door.TransformDirection(Vector3.up);
    }

    private void Update()
    {
        if (!m_initializedFlag)
        {
            return;
        }
        UpdateTrigger();
        UpdateMotion(Time.deltaTime);
    }

    // 対象との距離で開閉を決める
    private void UpdateTrigger()
    {
        Transform target = m_target;
        if (target == null && Camera.main != null)
        {
            target = Camera.main.transform;
        }
        if (target == null)
        {
            return;
        }
        Vector3 hingeWorld = HingeWorld();
        Vector3 offset = target.position - hingeWorld;
        offset.y = 0;
        float distance = offset.magnitude;
        if (!m_openFlag && !m_triggeredFlag && distance <= m_openDistance)
        {
            Open(target.position);
        }
        else if (m_openFlag && m_autoCloseFlag && distance >= m_closeDistance)
        {
            Close();
        }
    }

    // 開き具合を進め、蝶番まわりにドアを回す
    private void UpdateMotion(float deltaTime)
    {
        float goal = m_openFlag ? 1 : 0;
        float seconds = m_openFlag ? m_openSeconds : m_closeSeconds;
        if (Mathf.Approximately(m_progress, goal))
        {
            return;
        }
        m_progress = Mathf.MoveTowards(m_progress, goal, deltaTime / seconds);
        float amount = m_openFlag ? m_openCurve.Evaluate(m_progress) : Mathf.SmoothStep(0, 1, m_progress);
        ApplyAngle(amount * m_openAngle * m_direction);
        if (m_smoke != null)
        {
            m_smoke.SetDoorOpenAmount(amount);
        }
    }

    private void ApplyAngle(float angle)
    {
        Quaternion rotation = Quaternion.AngleAxis(angle, m_axisLocal);
        m_door.localPosition = m_hingeLocal + rotation * (m_closedLocalPosition - m_hingeLocal);
        m_door.localRotation = rotation * m_closedLocalRotation;
    }

    /// <summary>ドアを開ける。開く向きの判定には現在の対象の位置を使う。</summary>
    [ContextMenu("Open")]
    public void Open()
    {
        Transform target = m_target != null ? m_target : (Camera.main != null ? Camera.main.transform : null);
        Open(target != null ? target.position : transform.position + transform.forward);
    }

    /// <summary>ドアを閉める。</summary>
    [ContextMenu("Close")]
    public void Close()
    {
        if (!m_openFlag)
        {
            return;
        }
        m_openFlag = false;
        // 閉じたら、もう一度近づいたときに開けられるようにする
        m_triggeredFlag = false;
        if (m_smoke != null)
        {
            m_smoke.Close();
        }
        m_onClose.Invoke();
    }

    private void Open(Vector3 targetWorld)
    {
        if (m_openFlag)
        {
            return;
        }
        if (!m_initializedFlag)
        {
            Initialize();
        }
        // 閉じきっているときだけ開く向きを決め直す
        if (m_progress <= 0)
        {
            m_direction = DecideDirection(targetWorld);
        }
        m_openFlag = true;
        m_triggeredFlag = true;
        if (m_builtFogFlag && m_progress <= 0)
        {
            OrientFog(targetWorld);
            ApplyFogProfile();
        }
        if (m_smoke != null)
        {
            m_smoke.SetDoorOpenAmount(0);
            m_smoke.Open();
        }
        m_onOpen.Invoke();
    }

    // 開く向き（+1 か -1）を決める
    private float DecideDirection(Vector3 targetWorld)
    {
        switch (m_swingDirection)
        {
            case SWING_DIRECTION.POSITIVE:
                return 1;
            case SWING_DIRECTION.NEGATIVE:
                return -1;
            default:
                break;
        }
        // 開く側の辺が、どちらに回すと対象から遠ざかるかで決める
        Transform parent = m_door.parent;
        Vector3 targetLocal = parent != null ? parent.InverseTransformPoint(targetWorld) : targetWorld;
        Vector3 edge = m_freeEdgeLocal - m_hingeLocal;
        Vector3 toTarget = targetLocal - m_hingeLocal;
        Vector3 positive = Quaternion.AngleAxis(30, m_axisLocal) * edge;
        Vector3 negative = Quaternion.AngleAxis(-30, m_axisLocal) * edge;
        bool positiveAwayFlag = Vector3.Dot(positive, toTarget) < Vector3.Dot(negative, toTarget);
        bool awayFlag = m_swingDirection == SWING_DIRECTION.AWAY_FROM_TARGET;
        return positiveAwayFlag == awayFlag ? 1 : -1;
    }

    private Vector3 HingeWorld()
    {
        Transform parent = m_door.parent;
        return parent != null ? parent.TransformPoint(m_hingeLocal) : m_hingeLocal;
    }

    private void OnDrawGizmosSelected()
    {
        Transform door = m_door;
        if (door == null)
        {
            MeshRenderer meshRenderer = GetComponentInChildren<MeshRenderer>();
            door = meshRenderer != null ? meshRenderer.transform : null;
        }
        if (door == null)
        {
            return;
        }
        // 実行中でないときは今の姿勢から蝶番を求めて表示する
        Transform saved = m_door;
        m_door = door;
        ComputeHinge(out Vector3 hinge, out Vector3 axis, out Vector3 freeEdge);
        m_door = saved;
        if (Application.isPlaying && m_initializedFlag)
        {
            hinge = HingeWorld();
        }

        // 蝶番の軸（黄）と、開く側の辺（シアン）
        float height = 1.5f;
        MeshFilter meshFilter = door.GetComponent<MeshFilter>();
        if (meshFilter != null && meshFilter.sharedMesh != null)
        {
            height = meshFilter.sharedMesh.bounds.extents.y * door.lossyScale.y;
        }
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(hinge - axis * height, hinge + axis * height);
        Gizmos.DrawSphere(hinge, 0.06f);
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(hinge, freeEdge);

        // 霧の範囲（白）。向きはメインカメラのいる側
        if (m_fogProfile != null && m_smoke == null)
        {
            DrawFogArea(door, hinge, axis, freeEdge);
        }

        // 開く距離（緑）と閉じる距離（赤）
        DrawCircle(hinge, m_openDistance, new Color(0.3f, 1, 0.3f));
        if (m_autoCloseFlag)
        {
            DrawCircle(hinge, m_closeDistance, new Color(1, 0.4f, 0.3f));
        }
    }

    private void DrawFogArea(Transform door, Vector3 hinge, Vector3 axis, Vector3 freeEdge)
    {
        Vector3 origin = door.position;
        MeshFilter meshFilter = door.GetComponent<MeshFilter>();
        if (meshFilter != null && meshFilter.sharedMesh != null)
        {
            Bounds bounds = meshFilter.sharedMesh.bounds;
            origin = door.TransformPoint(new Vector3(bounds.center.x, bounds.min.y, bounds.center.z));
        }
        Vector3 normal = Vector3.Cross(axis, freeEdge - hinge);
        normal.y = 0;
        if (normal.sqrMagnitude < 0.0001f)
        {
            return;
        }
        normal.Normalize();
        Transform viewer = m_target != null ? m_target : (Camera.main != null ? Camera.main.transform : null);
        if (viewer != null && Vector3.Dot(normal, viewer.position - origin) < 0)
        {
            normal = -normal;
        }
        Vector3 size = new Vector3(m_fogProfile.m_areaWidth, m_fogProfile.m_areaHeight, m_fogProfile.m_areaLength);
        Vector3 center = origin + normal * m_fogForwardOffset + Vector3.up * size.y * 0.5f + normal * size.z * 0.5f;
        Gizmos.color = new Color(0.85f, 0.9f, 1f, 0.8f);
        Gizmos.matrix = Matrix4x4.TRS(center, Quaternion.LookRotation(normal, Vector3.up), Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, size);
        Gizmos.matrix = Matrix4x4.identity;
    }

    private static void DrawCircle(Vector3 center, float radius, Color color)
    {
        Gizmos.color = color;
        Vector3 previous = center + new Vector3(radius, 0, 0);
        for (int i = 1; i <= 48; i++)
        {
            float angle = i / 48f * Mathf.PI * 2;
            Vector3 next = center + new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
            Gizmos.DrawLine(previous, next);
            previous = next;
        }
    }
}
