//-----------------------------------------------
// PopUpEnemy.cs
// 制作日：2026/09/30
// 制作者：渡辺開斗
// 概要：出現・隠れるエネミーのクラス
//-----------------------------------------------
using System.Collections;
using UnityEngine;

public class PopUpEnemy : EnemyBase
{
    [Header("出現している時間")]
    [SerializeField] protected float m_activeTime = 4.0f;

    [Header("隠れている時間")]
    [SerializeField] protected float m_hideTime = 2.0f;

    [Header("レンダラー")]
    [SerializeField] protected Renderer[] m_renderers;

    [Header("当たり判定")]
    [SerializeField] protected Collider[] m_colliders;

    protected bool m_isVisible = false; // 出現状態かどうかのフラグ

    protected override void Awake()
    {
        base.Awake();
    }

    public override void Initialize()
    {
        SetVisible(false);
        StartCoroutine(PopRoutine());
    }

    // 出現・隠れるをループするコルーチン
    protected virtual IEnumerator PopRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(m_hideTime);

            SetVisible(true);
            OnAppear(); // 出現

            yield return new WaitForSeconds(m_activeTime);

            SetVisible(false);
            OnHide();   // 隠れる
        }
    }

    // 表示状態の切り替え処理
    protected virtual void SetVisible(bool isVisible)
    {
        m_isVisible = isVisible;

        foreach (var r in m_renderers)
        {
            if (r != null) r.enabled = isVisible;
        }
        foreach (var c in m_colliders)
        {
            if (c != null) c.enabled = isVisible;
        }
    }

    // 出現した瞬間に呼ばれる（子クラスで拡張用）
    protected virtual void OnAppear(){}

    /// 隠れた瞬間に呼ばれる（子クラスで拡張用）
    protected virtual void OnHide(){}
}
