//-----------------------------------------------
// BookRaycastTest.cs
// 制作日：2026/10/10
// 制作者：安田晴人
// 概要： 本のメッシュに対してレイキャストを行い、ページ情報を取得するテストスクリプト
//-----------------------------------------------
using UnityEngine;

public class BookRaycastTest : MonoBehaviour
{
    [Header("Display")]
    [SerializeField] private RectTransform m_displayImage;
    [SerializeField] private Camera m_modelCamera;

    [Header("Book Mesh")]
    [SerializeField] private MeshCollider m_bookCollider;
    [SerializeField] private Renderer m_bookRenderer;

    private static readonly int LEFT_UV_ID = Shader.PropertyToID("_LeftUv");
    private static readonly int RIGHT_UV_ID = Shader.PropertyToID("_RightUv");

    private void Update()
    {
        if (!Input.GetMouseButtonDown(0)) return;
        if (m_displayImage == null || m_modelCamera == null ||
            m_bookCollider == null || m_bookRenderer == null) return;

        if (!TryGetDisplayUV(Input.mousePosition, out Vector2 displayUV)) return;

        Ray ray = m_modelCamera.ViewportPointToRay(displayUV);

        if (!m_bookCollider.Raycast(ray, out RaycastHit hit, 100f))
        {
            Debug.Log("Book Raycast : Miss");
            return;
        }

        Material material = m_bookRenderer.sharedMaterial;
        if (material == null) return;

        Vector2 meshUV = hit.textureCoord;
        Vector4 leftRegion = material.GetVector(LEFT_UV_ID);
        Vector4 rightRegion = material.GetVector(RIGHT_UV_ID);

        string page = "Other";
        Vector2 pageUV = default;

        if (IsInsideRegion(meshUV, leftRegion))
        {
            page = "LEFT";
            pageUV = ConvertToPageUV(meshUV, leftRegion, true);
        }
        else if (IsInsideRegion(meshUV, rightRegion))
        {
            page = "RIGHT";
            pageUV = ConvertToPageUV(meshUV, rightRegion, false);
        }

        Debug.Log($"Book Raycast : Page={page}, MeshUV={meshUV.ToString("F4")}, PageUV={pageUV.ToString("F4")}, Triangle={hit.triangleIndex}");

        Debug.Log(
    $"Page={page}, " +
    $"DisplayUV={displayUV.ToString("F4")}, " +
    $"MeshUV={meshUV.ToString("F4")}, " +
    $"HitPoint={hit.point.ToString("F3")}, " +
    $"Triangle={hit.triangleIndex}");
    }

    private bool TryGetDisplayUV(Vector2 screenPosition, out Vector2 uv)
    {
        uv = default;

        Canvas canvas = m_displayImage.GetComponentInParent<Canvas>();
        Camera uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera : null;

        if (!RectTransformUtility.RectangleContainsScreenPoint(
            m_displayImage, screenPosition, uiCamera)) return false;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
            m_displayImage, screenPosition, uiCamera, out Vector2 local)) return false;

        Rect rect = m_displayImage.rect;
        if (rect.width <= 0f || rect.height <= 0f) return false;

        uv = new Vector2(
            (local.x - rect.xMin) / rect.width,
            (local.y - rect.yMin) / rect.height);

        return true;
    }

    private static bool IsInsideRegion(Vector2 uv, Vector4 region)
    {
        return uv.x >= region.x && uv.x <= region.x + region.z &&
               uv.y >= region.y && uv.y <= region.y + region.w;
    }

    private static Vector2 ConvertToPageUV(Vector2 uv, Vector4 region, bool flip)
    {
        Vector2 local = new Vector2(
            (uv.x - region.x) / region.z,
            (uv.y - region.y) / region.w);

        return flip ? Vector2.one - local : local;
    }
}
