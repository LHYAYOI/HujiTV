using System.Collections.Generic;
using UnityEngine;

public class TargetLockOnUIManager : MonoBehaviour
{

    [System.Serializable]
    public struct DirectionUIMapping
    {
        public MAGIC_CAST_DIRECTION direction;
        public LockOnTargetController uiController; // あらかじめ配置したUIコントローラー
    }

    [Header("参照")]
    [SerializeField] private TargetSelector m_targetSelector;

    [Header("方向別UIマッピング")]
    [SerializeField] private List<DirectionUIMapping> m_directionUIMappings = new List<DirectionUIMapping>();

    // 前フレームで各方向にアサインされていたターゲット（GameObject）を保持
    private Dictionary<MAGIC_CAST_DIRECTION, GameObject> m_previousTargets = new Dictionary<MAGIC_CAST_DIRECTION, GameObject>();

    private void Start()
    {
        if (m_targetSelector == null)
        {
            m_targetSelector = TargetSelector.Instance;
        }

        //初期化
        foreach (var mapping in m_directionUIMappings)
        {
            if (mapping.uiController != null)
            {
                mapping.uiController.Init();
            }
            m_previousTargets[mapping.direction] = null;
        }
    }

    private void Update()
    {
        if (m_targetSelector == null) return;

        foreach (var mapping in m_directionUIMappings)
        {
            MAGIC_CAST_DIRECTION dir = mapping.direction;
            LockOnTargetController uiController = mapping.uiController;

            if (uiController == null) 
            {
                continue;
            }

            //TargetSelectorから現在のターゲットを取得
            GameObject currentTargetObj = m_targetSelector.GetSelectedTarget(dir);
            TargetableObject currentTargetable = null;

            if (currentTargetObj != null)
            {
                currentTargetable = currentTargetObj.GetComponent<TargetableObject>();

                //m_isTargetableFlag（IsTargetable）が false になっていたらターゲットなしとみなす
                if (currentTargetable != null && !currentTargetable.IsTargetable())
                {
                    currentTargetObj = null;
                    currentTargetable = null;
                }
            }

            GameObject previousTargetObj = m_previousTargets[dir];

            if (uiController.GetTarget() == currentTargetable) 
            {
                continue;
            }

            //状態の変化を検知して制御
            if (previousTargetObj == null && currentTargetObj != null)
            {
                //範囲内に入ってきたまたは新たにターゲットされた
                uiController.SetTarget(currentTargetable);
                uiController.AppearTargetUI(); // 出現アニメーション再生
            }
            else if (previousTargetObj != null && currentTargetObj == null)
            {
                //範囲外に出た、またはターゲットフラグが false になった
                uiController.SetTarget(null);
                uiController.CancelTargetUI(); // 解除・フェードアウトアニメーション再生
            }
            else if (previousTargetObj != null && currentTargetObj != null && previousTargetObj != currentTargetObj)
            {
                //別のターゲットに切り替わった場合
                uiController.SetTarget(currentTargetable);
                uiController.AppearTargetUI();
            }

            //前フレームの情報を更新
            m_previousTargets[dir] = currentTargetObj;
        }
    }

    public void PlaySelectedEffect(MAGIC_CAST_DIRECTION direction)
    {
        foreach (var mapping in m_directionUIMappings)
        {
            if (mapping.direction == direction && mapping.uiController != null)
            {
                mapping.uiController.SelectedTargetUI(); // 決定アニメーション再生
                break;
            }
        }
    }
}
