using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// ゴースト専用の平面反射。メインカメラを水面で上下反転した位置に反射用カメラを置き、ゴーストのレイヤーだけを描く。
/// 床の高さごとに 1 枚の画像を作り、その高さの水たまりがすべて使い回す。見えている水たまりがないときは描かない。
/// ゴーストを本体のカメラに映らないレイヤーに置けば、「水たまりにしか映らないゴースト」になる。
/// </summary>
[DefaultExecutionOrder(10000), DisallowMultipleComponent, AddComponentMenu("hatake/Rain/Ghost Reflection")]
public sealed class GhostReflection : MonoBehaviour
{
    private sealed class PlaneData
    {
        public float m_height;
        public RenderTexture m_texture;
        public int m_requestedFrame;
    }

    [Tooltip("水たまりに映すレイヤー（ゴースト）。")]
    [SerializeField] private LayerMask m_ghostLayers;
    [Range(0.25f, 1), Tooltip("反射の解像度（画面に対する割合）。小さいほど軽い。")]
    [SerializeField] private float m_resolutionScale = 0.5f;
    [Tooltip("反射の元になるカメラ。未指定ならメインカメラ。")]
    [SerializeField] private Camera m_sourceCamera;

    private readonly Dictionary<int, PlaneData> m_planes = new Dictionary<int, PlaneData>();
    private Camera m_camera;

    /// <summary>シーンで有効な GhostReflection（なければ null）。</summary>
    public static GhostReflection Active { get; private set; }

    public LayerMask GhostLayers { get { return m_ghostLayers; } set { m_ghostLayers = value; } }

    private void OnEnable()
    {
        Active = this;
    }

    private void OnDisable()
    {
        if (Active == this)
        {
            Active = null;
        }
        foreach (PlaneData plane in m_planes.Values)
        {
            if (plane.m_texture != null)
            {
                plane.m_texture.Release();
                Destroy(plane.m_texture);
            }
        }
        m_planes.Clear();
        if (m_camera != null)
        {
            Destroy(m_camera.gameObject);
            m_camera = null;
        }
    }

    /// <summary>この高さの水面の、ゴーストの反射画像を頼む（このフレームで描かれる）。</summary>
    public Texture Request(float height)
    {
        int key = Mathf.RoundToInt(height * 100);
        if (!m_planes.TryGetValue(key, out PlaneData plane))
        {
            plane = new PlaneData { m_height = height };
            m_planes.Add(key, plane);
        }
        plane.m_requestedFrame = Time.frameCount;
        EnsureTexture(plane);
        return plane.m_texture;
    }

    private Camera Source()
    {
        return m_sourceCamera != null ? m_sourceCamera : Camera.main;
    }

    // 画面の大きさに合わせて反射画像を用意する
    private void EnsureTexture(PlaneData plane)
    {
        Camera source = Source();
        int width = Mathf.Max(16, Mathf.RoundToInt((source != null ? source.pixelWidth : 1920) * m_resolutionScale));
        int height = Mathf.Max(16, Mathf.RoundToInt((source != null ? source.pixelHeight : 1080) * m_resolutionScale));
        if (plane.m_texture != null && plane.m_texture.width == width && plane.m_texture.height == height)
        {
            return;
        }
        if (plane.m_texture != null)
        {
            plane.m_texture.Release();
            Destroy(plane.m_texture);
        }
        plane.m_texture = new RenderTexture(width, height, 16, RenderTextureFormat.DefaultHDR)
        {
            name = "GhostReflection",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
        plane.m_texture.Create();
    }

    private void LateUpdate()
    {
        Camera source = Source();
        if (source == null || m_ghostLayers.value == 0)
        {
            return;
        }
        foreach (PlaneData plane in m_planes.Values)
        {
            // このフレームか前のフレームで頼まれた高さだけ描く
            if (plane.m_requestedFrame >= Time.frameCount - 1 && plane.m_texture != null)
            {
                Render(source, plane);
            }
        }
    }

    private void Render(Camera source, PlaneData plane)
    {
        EnsureCamera();
        m_camera.CopyFrom(source);
        m_camera.enabled = false;
        m_camera.targetTexture = null;
        m_camera.cameraType = CameraType.Reflection;
        m_camera.cullingMask = m_ghostLayers.value;
        m_camera.clearFlags = CameraClearFlags.SolidColor;
        m_camera.backgroundColor = Color.clear;
        m_camera.useOcclusionCulling = false;

        // 水面で上下反転した位置から見上げる（左右が反転した画像になるので、シェーダー側で左右を戻す）
        Vector3 position = source.transform.position;
        position.y = 2 * plane.m_height - position.y;
        Vector3 forward = source.transform.forward;
        forward.y = -forward.y;
        Vector3 up = source.transform.up;
        up.y = -up.y;
        m_camera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(forward, up));

        var request = new UniversalRenderPipeline.SingleCameraRequest { destination = plane.m_texture };
        if (RenderPipeline.SupportsRenderRequest(m_camera, request))
        {
            RenderPipeline.SubmitRenderRequest(m_camera, request);
        }
    }

    private void EnsureCamera()
    {
        if (m_camera != null)
        {
            return;
        }
        var cameraObject = new GameObject("GhostReflection Camera") { hideFlags = HideFlags.HideAndDontSave };
        cameraObject.transform.SetParent(transform, false);
        m_camera = cameraObject.AddComponent<Camera>();
        m_camera.enabled = false;
        var data = cameraObject.AddComponent<UniversalAdditionalCameraData>();
        data.renderPostProcessing = false;
        data.renderShadows = false;
        data.requiresDepthTexture = false;
        data.requiresColorTexture = false;
    }
}
