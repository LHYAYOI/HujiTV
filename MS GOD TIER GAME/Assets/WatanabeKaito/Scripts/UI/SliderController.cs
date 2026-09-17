//-----------------------------------------------
// SliderController.cs
// 制作日：2026/09/17
// 制作者：渡辺開斗
// 概要：スライダーの操作管理クラス
//-----------------------------------------------
using UnityEngine;
using UnityEngine.EventSystems;

public class SliderController : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
{
    private bool m_isDraggingFlag = false;

    // クリック・タップした時
    public void OnPointerDown(PointerEventData eventData)
    {
        m_isDraggingFlag = false;
    }

    // ドラッグ中
    public void OnDrag(PointerEventData eventData)
    {
        m_isDraggingFlag = true; // ドラッグされたことを記録
    }

    // 離した時
    public void OnPointerUp(PointerEventData eventData)
    {
        // ドラッグ操作が行われていた場合のみ音を鳴らす
        if (m_isDraggingFlag)
        {
            AudioManager.Instance.PlaySE("SE");

            m_isDraggingFlag = false;
        }

    }
}
