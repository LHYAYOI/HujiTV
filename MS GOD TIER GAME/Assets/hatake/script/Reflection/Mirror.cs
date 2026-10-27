using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 板ポリの鏡。鏡の面で反転した位置に反射用カメラを置いて部屋を描き、その画像を鏡に貼る。
/// 鏡の奥（壁の向こう）は斜めの近クリップ面で切るので、壁があっても映る。
/// 見えていないとき、遠いとき、鏡の裏側から見ているときは描かず、反射プローブの景色で代わりに映す。
/// Culling Mask にメインカメラが映さないレイヤーを入れると「鏡にだけ映るゴースト」になる。
/// </summary>
[RequireComponent(typeof(MeshRenderer)), DisallowMultipleComponent, DefaultExecutionOrder(10000), AddComponentMenu("hatake/Reflection/Mirror")]
public sealed class Mirror : MonoBehaviour
{
    [Tooltip("鏡に映すレイヤー。メインカメラに映らないレイヤー（ゴースト）を足すと、鏡にだけ映る。")]
    [SerializeField] private LayerMask m_cullingMask = ~0;
    [Range(0.25f, 1), Tooltip("反射の解像度（画面に対する割合）。小さいほど軽い。")]
    [SerializeField] private float m_resolutionScale = 0.5f;
    [Min(1), Tooltip("この距離（m）より遠いときは描かず、反射プローブで代わりに映す。")]
    [SerializeField] private float m_maxDistance = 20;
    [Min(0), Tooltip("鏡の面から少し手前で切る距離（m）。鏡に壁の端が映り込むときに増やす。")]
    [SerializeField] private float m_clipOffset = 0.02f;
    [Tooltip("鏡の中にも距離フォグをかける。")]
    [SerializeField] private bool m_distanceFogFlag = true;
    [Tooltip("反射の元になるカメラ。未指定ならメインカメラ。")]
    [SerializeField] private Camera m_sourceCamera;

    private static readonly int g_textureId = Shader.PropertyToID("_MirrorTex");
    private static readonly int g_enabledId = Shader.PropertyToID("_MirrorEnabled");

    private MeshRenderer m_renderer;
    private MaterialPropertyBlock m_block;
    private Camera m_camera;
    private RenderTexture m_texture;

    /// <summary>鏡の表の向き（Unity の Quad は -Z が表）。</summary>
    public Vector3 Normal { get { return transform.TransformDirection(Vector3.back).normalized; } }

    private void OnEnable()
    {
        m_renderer = GetComponent<MeshRenderer>();
    }

    private void OnDisable()
    {
        if (m_texture != null)
        {
            m_texture.Release();
            Destroy(m_texture);
            m_texture = null;
        }
        if (m_camera != null)
        {
            Destroy(m_camera.gameObject);
            m_camera = null;
        }
    }

    private void LateUpdate()
    {
        Camera source = m_sourceCamera != null ? m_sourceCamera : Camera.main;
        bool drawFlag = source != null && m_renderer.isVisible &&
            Vector3.Distance(source.transform.position, transform.position) <= m_maxDistance &&
            Vector3.Dot(source.transform.position - transform.position, Normal) > 0;
        if (drawFlag)
        {
            EnsureTexture(source);
            Render(source);
        }
        if (m_block == null)
        {
            m_block = new MaterialPropertyBlock();
        }
        m_renderer.GetPropertyBlock(m_block);
        m_block.SetTexture(g_textureId, drawFlag && m_texture != null ? m_texture : Texture2D.blackTexture);
        m_block.SetFloat(g_enabledId, drawFlag ? 1 : 0);
        m_renderer.SetPropertyBlock(m_block);
    }

    private void Render(Camera source)
    {
        EnsureCamera();
        m_camera.ResetProjectionMatrix();
        m_camera.CopyFrom(source);
        m_camera.enabled = false;
        m_camera.targetTexture = null;
        m_camera.cameraType = CameraType.Reflection;
        m_camera.cullingMask = m_cullingMask.value;
        m_camera.useOcclusionCulling = false;

        // 鏡の面で反転した位置と向き（左右が反転した画像になるので、シェーダー側で左右を戻す）
        Vector3 normal = Normal;
        Vector3 origin = transform.position;
        Vector3 position = Reflect(source.transform.position - origin, normal) + origin;
        Vector3 forward = Reflect(source.transform.forward, normal);
        Vector3 up = Reflect(source.transform.up, normal);
        m_camera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(forward, up));

        // 鏡の面より奥（反射カメラと鏡の間にある壁など）を切る
        Matrix4x4 view = m_camera.worldToCameraMatrix;
        Vector3 planePoint = view.MultiplyPoint(origin + normal * m_clipOffset);
        Vector3 planeNormal = view.MultiplyVector(normal).normalized;
        m_camera.projectionMatrix = m_camera.CalculateObliqueMatrix(new Vector4(planeNormal.x, planeNormal.y, planeNormal.z, -Vector3.Dot(planePoint, planeNormal)));

        var request = new UniversalRenderPipeline.SingleCameraRequest { destination = m_texture };
        if (RenderPipeline.SupportsRenderRequest(m_camera, request))
        {
            RenderPipeline.SubmitRenderRequest(m_camera, request);
        }
    }

    private static Vector3 Reflect(Vector3 vector, Vector3 normal)
    {
        return vector - 2 * Vector3.Dot(vector, normal) * normal;
    }

    private void EnsureTexture(Camera source)
    {
        int width = Mathf.Max(16, Mathf.RoundToInt(source.pixelWidth * m_resolutionScale));
        int height = Mathf.Max(16, Mathf.RoundToInt(source.pixelHeight * m_resolutionScale));
        if (m_texture != null && m_texture.width == width && m_texture.height == height)
        {
            return;
        }
        if (m_texture != null)
        {
            m_texture.Release();
            Destroy(m_texture);
        }
        m_texture = new RenderTexture(width, height, 24, RenderTextureFormat.DefaultHDR)
        {
            name = "Mirror",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
        m_texture.Create();
    }

    private void EnsureCamera()
    {
        if (m_camera != null)
        {
            return;
        }
        var cameraObject = new GameObject("Mirror Camera") { hideFlags = HideFlags.HideAndDontSave };
        cameraObject.transform.SetParent(transform, false);
        m_camera = cameraObject.AddComponent<Camera>();
        m_camera.enabled = false;
        var data = cameraObject.AddComponent<UniversalAdditionalCameraData>();
        data.renderPostProcessing = false;
        data.renderShadows = true;
        data.requiresDepthTexture = m_distanceFogFlag;
        data.requiresColorTexture = false;
        if (m_distanceFogFlag)
        {
            cameraObject.AddComponent<LightShaftLab.DistanceFogCameraOptIn>();
        }
    }

    private void OnDrawGizmosSelected()
    {
        // 鏡の表の向き
        Gizmos.color = new Color(0.6f, 0.9f, 1f, 0.9f);
        Gizmos.DrawLine(transform.position, transform.position + Normal * 0.5f);
        Gizmos.DrawWireSphere(transform.position + Normal * 0.5f, 0.03f);
    }
}
