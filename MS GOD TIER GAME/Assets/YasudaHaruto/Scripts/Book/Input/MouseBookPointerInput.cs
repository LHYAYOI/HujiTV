//-----------------------------------------------
// MouseBookPointerInput.cs
// 制作日：2026/09/20
// 制作者：安田晴人
// 概要： マウス入力を本のポインター入力として扱うスクリプト
//-----------------------------------------------
using UnityEngine;


public class MouseBookPointerInput : MonoBehaviour, IBookPointerInput
{
    [SerializeField] private RectTransform m_inputArea;

    private bool m_isTracking;

    public bool TryGetPointerDown(out BookPointerData data)
    {
        // 初期化
        data = default;

        if (!Input.GetMouseButtonDown(0) || !IsInsideInputArea(Input.mousePosition))
        {// マウスの左ボタンが押されていない、または入力エリア外の場合は処理を終了
            return false;
        }

        m_isTracking = true;
        data = CreatePointerData();
        return true;
    }

    public bool TryGetPointer(out BookPointerData data)
    {
        // 初期化
        data = default;

        if (!m_isTracking || !Input.GetMouseButton(0))
        {// トラッキング中でない、またはマウスの左ボタンが押されていない場合は処理を終了
            return false;
        }

        data = CreatePointerData();
        return true;
    }

    public bool TryGetPointerUp(out BookPointerData data)
    {
        // 初期化
        data = default;

        if (!m_isTracking || !Input.GetMouseButtonUp(0))
        {// トラッキング中でない、またはマウスの左ボタンが離されていない場合は処理を終了
            return false;
        }

        data = CreatePointerData();
        m_isTracking = false;
        return true;
    }

    // マウスのスクリーン座標を正規化された座標に変換し、BookPointerDataを作成する
    private BookPointerData CreatePointerData()
    {
        // マウスのスクリーン座標を取得
        Vector2 screenPosition = Input.mousePosition;

        // スクリーン座標を正規化された座標に変換
        Vector2 normalizedPosition = ScreenToNormalized(screenPosition);

        // BookPointerDataを作成して返す
        return new BookPointerData(normalizedPosition, screenPosition);
    }

    // 入力エリア内にマウスがあるかどうかを判定する
    private bool IsInsideInputArea(Vector2 screenPosition)
    {
        if (m_inputArea == null)
        {// 入力エリアが設定されていない場合は無効
            return false;
        }

        // 入力エリアの親Canvasを取得し、必要に応じてカメラを取得する
        Canvas canvas = m_inputArea.GetComponentInParent<Canvas>();
        Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;

        // 入力エリア内にマウスがあるかどうかを判定して返す
        return RectTransformUtility.RectangleContainsScreenPoint(m_inputArea, screenPosition, camera);
    }

    // スクリーン座標を正規化された座標に変換する
    private Vector2 ScreenToNormalized(Vector2 screenPosition)
    {
        if (m_inputArea == null)
        {// 入力エリアが設定されていない場合は無効
            return default;
        }

        // 入力エリアの親Canvasを取得し、必要に応じてカメラを取得する
        Canvas canvas = m_inputArea.GetComponentInParent<Canvas>();
        Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(m_inputArea, screenPosition, camera, out Vector2 localPosition))
        {// スクリーン座標をローカル座標に変換できない場合は無効
            return default;
        }

        Rect rect = m_inputArea.rect;

        if (rect.width <= 0f || rect.height <= 0f)
        {// 入力エリアのサイズが無効な場合は無効
            return default;
        }

        // ローカル座標を正規化された座標に変換して返す
        return new Vector2((localPosition.x - rect.xMin) / rect.width, (localPosition.y - rect.yMin) / rect.height);
    }

    private void OnDisable()
    {
        m_isTracking = false;
    }
}
