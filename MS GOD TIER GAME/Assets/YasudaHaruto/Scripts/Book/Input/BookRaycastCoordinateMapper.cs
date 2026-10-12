//-----------------------------------------------
// BookRaycastCoordinateMapper.cs
// 制作日：2026/10/10
// 制作者：安田晴人
// 概要： 本のメッシュに対してレイキャストを行い、ページ情報を取得するスクリプト
//-----------------------------------------------
using UnityEngine;

public enum BOOK_PAGE_SIDE
{
    NONE,
    LEFT,
    RIGHT
}

public class BookRaycastCoordinateMapper : MonoBehaviour
{
    [Header("Display")]
    [SerializeField] private RectTransform m_displayImage;
    [SerializeField] private Camera m_modelCamera;
    [SerializeField] private Camera m_captureCamera;

    [Header("Book Mesh")]
    [SerializeField] private MeshCollider m_bookCollider;
    [SerializeField] private Renderer m_bookRenderer;

    private static readonly int LEFT_UV_ID = Shader.PropertyToID("_LeftUv");
    private static readonly int RIGHT_UV_ID = Shader.PropertyToID("_RightUv");
    private static readonly int LEFT_CROP_ID = Shader.PropertyToID("_LeftCrop");
    private static readonly int RIGHT_CROP_ID = Shader.PropertyToID("_RightCrop");

    // 画面座標から本のページ情報を取得する
    public bool TryGetCapturePosition(Vector2 screenPosition, out BOOK_PAGE_SIDE side, out Ray captureRay)
    {
        // 初期化
        side = BOOK_PAGE_SIDE.NONE;
        captureRay = default;

        if (m_displayImage == null || m_modelCamera == null || m_captureCamera == null || m_bookCollider == null || m_bookRenderer == null)
        {// nullチェック
            return false;
        }

        // UIのCanvasとカメラを取得
        Canvas canvas = m_displayImage.GetComponentInParent<Canvas>();
        Camera uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;

        if (!RectTransformUtility.RectangleContainsScreenPoint(m_displayImage, screenPosition, uiCamera))
        {// 画面座標が表示領域内にない場合は無効
            return false;
        }

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(m_displayImage, screenPosition, uiCamera, out Vector2 local))
        {// 画面座標をローカル座標に変換できない場合は無効
            return false;
        }

        

        // 表示領域のサイズを取得
        Rect rect = m_displayImage.rect;

        if (rect.width <= 0f || rect.height <= 0f)
        {// 表示領域のサイズが無効な場合は無効
            return false;
        }

        // ローカル座標をUV座標に変換
        Vector2 displayUV = new((local.x - rect.xMin) / rect.width, (local.y - rect.yMin) / rect.height);


        // UV座標から本のメッシュにレイキャスト
        Ray modelRay = m_modelCamera.ViewportPointToRay(displayUV);

        if (!m_bookCollider.Raycast(modelRay, out RaycastHit hit, 100f))
        {// 本のメッシュにヒットしない場合は無効
            return false;
        }

        // 本のマテリアルからUV座標を取得
        Material material = m_bookRenderer.sharedMaterial;

        if (material == null)
        {// マテリアルが無効な場合は無効
            return false;
        }

        // ヒットしたUV座標を取得
        Vector2 meshUV = hit.textureCoord;
        Vector4 region;
        Vector4 crop;
        bool flip;

        // 左右のUV領域を取得
        Vector4 leftRegion = material.GetVector(LEFT_UV_ID);
        Vector4 rightRegion = material.GetVector(RIGHT_UV_ID);

        if (ContainsUV(meshUV, leftRegion))
        {// 左ページの場合
            side = BOOK_PAGE_SIDE.LEFT;
            region = leftRegion;
            crop = material.GetVector(LEFT_CROP_ID);
            flip = true;
        }
        else if (ContainsUV(meshUV, rightRegion))
        {// 右ページの場合
            side = BOOK_PAGE_SIDE.RIGHT;
            region = rightRegion;
            crop = material.GetVector(RIGHT_CROP_ID);
            flip = false;
        }
        else
        {// 左右のページに含まれない場合は無効
            return false;
        }

        if (region.z <= 0f || region.w <= 0f)
        {// UV領域のサイズが無効な場合は無効
            return false;
        }

        // メッシュUV座標をページUV座標に変換
        Vector2 pageUV = new((meshUV.x - region.x) / region.z, (meshUV.y - region.y) / region.w);

        if (flip)
        {// 左ページの場合はUV座標を反転
            pageUV = Vector2.one - pageUV;
        }

        // ページUV座標をキャプチャカメラのUV座標に変換
        Vector2 captureUV = new(crop.x + pageUV.x * crop.z, crop.y + pageUV.y * crop.w);

        // キャプチャカメラのUV座標からレイを取得
        captureRay = m_captureCamera.ViewportPointToRay(captureUV);

        return true;
    }

    // UV座標が指定された領域に含まれるかどうかを判定する
    private static bool ContainsUV(Vector2 uv, Vector4 region)
    {
        return uv.x >= region.x && uv.x <= region.x + region.z && uv.y >= region.y && uv.y <= region.y + region.w;
    }
}
