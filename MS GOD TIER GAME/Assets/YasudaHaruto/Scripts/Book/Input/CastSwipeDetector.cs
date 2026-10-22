//-----------------------------------------------
// CastSwipeDetector.cs
// 制作日：2026/10/11
// 制作者：安田晴人
// 概要： 魔法の詠唱のスワイプ入力を検出するクラス
//-----------------------------------------------
using UnityEngine;

public enum MAGIC_CAST_DIRECTION : byte
{
    LEFT = 0,
    CENTER = 1,
    RIGHT = 2,
    NONE = 255
}


public class CastSwipeDetector : MonoBehaviour
{
    [SerializeField] private RectTransform m_displayArea;

    [Header("キャスト設定")]
    [SerializeField, Range(0.01f, 1f)] private float m_minSwipeDistance = 0.15f;
    [SerializeField, Range(0f, 45f)] private float m_centerAngle = 20f;
    [SerializeField, Range(45f, 90f)] private float m_maxAngle = 65f;

    // スワイプの開始位置と終了位置から、魔法の詠唱の方向を評価する
    public float GuideSideAngle => (m_centerAngle + m_maxAngle) * 0.5f;

    public bool TryEvaluate(Vector2 startScreenPosition, Vector2 endScreenPosition, out MAGIC_CAST_DIRECTION direction)
    {
        // 初期化
        direction = default;

        if (m_displayArea == null)
        {// DisplayAreaが設定されていない場合は評価できない
            return false;
        }

        // DisplayAreaのCanvasを取得
        Canvas canvas = m_displayArea.GetComponentInParent<Canvas>();

        // CanvasのRenderModeがScreenSpaceOverlay以外の場合は、カメラを取得
        Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(m_displayArea, startScreenPosition, camera, out Vector2 start))
        {// スタート位置がDisplayArea内にない場合は評価できない
            return false;
        }

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(m_displayArea, endScreenPosition, camera, out Vector2 end))
        {// エンド位置がDisplayArea内にない場合は評価できない
            return false;
        }

        // DisplayAreaの高さを取得
        float displayHeight = m_displayArea.rect.height;
        if (displayHeight <= 0f)
        {// DisplayAreaの高さが0以下の場合は評価できない
            return false;
        }

        // スワイプの距離と方向を計算
        Vector2 delta = end - start;
        if (delta.magnitude / displayHeight < m_minSwipeDistance)
        {// スワイプの距離が最小距離未満の場合は評価できない
            return false;
        }
        if (delta.y <= 0f)
        {// スワイプの方向が下方向の場合は評価できない
            return false;
        }

        // スワイプの角度を計算
        float angle = Mathf.Atan2(delta.x, delta.y) * Mathf.Rad2Deg;
        if (Mathf.Abs(angle) > m_maxAngle)
        {// スワイプの角度が最大角度を超える場合は評価できない
            return false;
        }

        // スワイプの方向を判定
        direction = angle < -m_centerAngle ? MAGIC_CAST_DIRECTION.LEFT : angle > m_centerAngle ? MAGIC_CAST_DIRECTION.RIGHT : MAGIC_CAST_DIRECTION.CENTER;

        // 評価成功
        return true;
    }
}
