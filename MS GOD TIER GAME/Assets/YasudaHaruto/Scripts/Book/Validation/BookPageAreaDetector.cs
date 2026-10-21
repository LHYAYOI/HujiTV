//-----------------------------------------------
// BookPageAreaDetector.cs
// 制作日：2026/10/10
// 制作者：安田晴人
// 概要： 本のページの各エリアを検出するスクリプト
//-----------------------------------------------
using UnityEngine;

namespace YasudaHaruto.BookValidation
{
public enum BOOK_AREA_TYPE
{
    NONE,
    RUNE,
    PREVIOUS_PAGE,
    NEXT_PAGE,
    BOOKMARK
}

public class BookPageAreaDetector : MonoBehaviour
{
    [Header("Coordinate Mapper")]
    [SerializeField] private BookFlipCoordinateMapper m_mapper;

    [Header("Left Page")]
    [SerializeField] private RectTransform m_runeArea;
    [SerializeField] private RectTransform m_previousPageArea;

    [Header("Right Page")]
    [SerializeField] private RectTransform m_nextPageArea;

    [Header("Bookmark")]
    [SerializeField] private RectTransform m_bookmarkArea;

    // 画面座標から本のページのエリアを検出する
    public bool TryDetect(Vector2 screenPosition, out BOOK_AREA_TYPE area, out Vector2 normalizedPosition, bool includeRune = true)
    {
        // 初期化
        area = BOOK_AREA_TYPE.NONE;
        normalizedPosition = default;

        if (m_mapper == null || !m_mapper.TryGetCapturePosition(screenPosition, out BOOK_PAGE_SIDE side, out Ray ray))
        {// 本のページ情報を取得できない場合は無効
            return false;
        }

        //if (TryHitArea(ray, m_bookmarkArea, out normalizedPosition))
        //{// ブックマークエリアが優先される
        //    area = BOOK_AREA_TYPE.BOOKMARK;
        //    return true;
        //}

        if (side == BOOK_PAGE_SIDE.LEFT)
        {// 左ページのエリアを検出
            if (includeRune && TryHitArea(ray, m_runeArea, out normalizedPosition))
            {
                area = BOOK_AREA_TYPE.RUNE;
                return true;
            }

            if (TryHitArea(ray, m_previousPageArea, out normalizedPosition))
            {// 前のページエリアが優先される
                area = BOOK_AREA_TYPE.PREVIOUS_PAGE;
                return true;
            }
        }
        else if (side == BOOK_PAGE_SIDE.RIGHT)
        {// 右ページのエリアを検出
            if (TryHitArea(ray, m_nextPageArea, out normalizedPosition))
            {// 次のページエリアが優先される
                area = BOOK_AREA_TYPE.NEXT_PAGE;
                return true;
            }
        }

        return false;
    }

    // 指定されたレイが指定されたエリアにヒットするかどうかを判定し、ヒットした場合は正規化された座標を返す
    private static bool TryHitArea(Ray ray, RectTransform area, out Vector2 normalizedPosition)
    {
        // 初期化
        normalizedPosition = default;

        if (area == null || !area.gameObject.activeInHierarchy)
        {// エリアが無効な場合は無効
            return false;
        }

        // エリアの平面を作成し、レイとの交差判定を行う
        Plane plane = new Plane(area.forward, area.position);

        if (!plane.Raycast(ray, out float distance))
        {// レイがエリアの平面に交差しない場合は無効
            return false;
        }

        // レイとエリアの平面の交点を取得し、ローカル座標に変換する
        Vector3 worldPoint = ray.GetPoint(distance);
        Vector3 localPoint = area.InverseTransformPoint(worldPoint);
        Rect rect = area.rect;

        if (rect.width <= 0f || rect.height <= 0f)
        {// エリアのサイズが無効な場合は無効
            return false;
        }

        // ローカル座標を正規化された座標に変換する
        normalizedPosition = new Vector2((localPoint.x - rect.xMin) / rect.width, (localPoint.y - rect.yMin) / rect.height);

        // エリアの矩形内に交点が含まれるかどうかを判定する
        return rect.Contains(new Vector2(localPoint.x, localPoint.y));
    }


    // ルーンエリアの正規化された座標を取得する
    public bool TryGetRunePosition(Vector2 screenPosition, out Vector2 normalizedPosition)
    {
        // 初期化
        normalizedPosition = default;

        if (m_runeArea == null || m_mapper == null)
        {// ルーンエリアまたはマッパーが無効な場合は無効
            return false;
        }

        if (!m_mapper.TryGetCapturePosition(screenPosition, out BOOK_PAGE_SIDE side, out Ray ray))
        {// マッパーから本のページ情報を取得できない場合は無効
            return false;
        }

        if (side != BOOK_PAGE_SIDE.LEFT)
        {// 左ページ以外の場合は無効
            return false;
        }

        return TryGetAreaPosition(ray, m_runeArea, out normalizedPosition);
    }

    // 指定されたレイが指定されたエリアにヒットするかどうかを判定し、ヒットした場合は正規化された座標を返す
    private static bool TryGetAreaPosition(Ray ray, RectTransform area, out Vector2 normalizedPosition)
    {
        // 初期化
        normalizedPosition = default;

        if (area == null || !area.gameObject.activeInHierarchy)
        {// エリアが無効な場合は無効
            return false;
        }

        // エリアの平面を作成し、レイとの交差判定を行う
        Plane plane = new Plane(area.forward, area.position);

        if (!plane.Raycast(ray, out float distance))
        {// レイがエリアの平面に交差しない場合は無効
            return false;
        }

        // レイとエリアの平面の交点を取得し、ローカル座標に変換する
        Vector3 localPoint = area.InverseTransformPoint(ray.GetPoint(distance));
        Rect rect = area.rect;

        if (rect.width <= 0f || rect.height <= 0f)
        {// エリアのサイズが無効な場合は無効
            return false;
        }

        // ローカル座標を正規化された座標に変換する
        normalizedPosition = new Vector2((localPoint.x - rect.xMin) / rect.width, (localPoint.y - rect.yMin) / rect.height);

        return true;
    }

}

}
