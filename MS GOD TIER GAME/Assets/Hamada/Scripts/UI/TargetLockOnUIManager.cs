using System.Collections.Generic;
using UnityEngine;

public class TargetLockOnUIManager : MonoBehaviour
{

    [Header("参照")]
    [SerializeField] private TargetSelector m_targetSelector;

    [Header("方向別UIマッピング")]
    [SerializeField] private List<LockOnTargetController> m_lockOnUIList = new List<LockOnTargetController>();

    private void Start()
    {
        //初期化
        foreach (LockOnTargetController ui in m_lockOnUIList)
        {
            if (ui != null)
            {
                ui.Init();
                ui.gameObject.SetActive(false);
            }
        }
    }

    private void Update()
    {
        if (m_targetSelector == null)
        {
            return;
        }

       //選定されてるターゲット取得
        List<TargetableObject> activeTargets = GetActiveTargetsFromSelector();

        // すでに割り当てられているUIのチェック（範囲外、またはフラグfalseになったものの解除）
        foreach (LockOnTargetController ui in m_lockOnUIList)
        {
            if (!ui.gameObject.activeSelf) 
            {
                continue;
            }

            TargetableObject assignedTarget = ui.GetTarget();

            // ターゲット自体が消滅、または TargetSelector の有効リストから外れた（範囲外）
            // TargetableObject の狙えるフラグが false になった
            bool shouldRelease = assignedTarget == null || !assignedTarget.IsTargetable() || !activeTargets.Contains(assignedTarget);

            if (shouldRelease)
            {
                ui.CancelTargetUI();
            }
        }

        // 新たに範囲内に入ってきたターゲットの割り当て
        foreach (TargetableObject target in activeTargets)
        {
            if (target == null) continue;

            // すでにこのターゲットを担当しているUIがすでにあるか確認
            bool isAlreadyAssigned = false;

            foreach (LockOnTargetController ui in m_lockOnUIList)
            {
                if (ui.gameObject.activeSelf && ui.GetTarget() == target)
                {
                    isAlreadyAssigned = true;
                    break;
                }
            }

            if (isAlreadyAssigned)
            {
                continue;
            }

            // 割り当ててないUIを割り当てる
            LockOnTargetController freeUI = GetFreeUI();

            if (freeUI != null)
            {
                freeUI.gameObject.SetActive(true);
                freeUI.SetTarget(target);
                freeUI.AppearTargetUI();
            }
        }
    }

    // 空いている（使われていない）UIをプールから1つ見つける
    private LockOnTargetController GetFreeUI()
    {
        foreach (LockOnTargetController ui in m_lockOnUIList)
        {
            if (!ui.gameObject.activeSelf)
            {
                return ui;
            }
        }

        return null;
    }

    // TargetSelector から現在狙うべき有効なターゲットのリストを取得するヘルパー
    private List<TargetableObject> GetActiveTargetsFromSelector()
    {
        List<TargetableObject> targets = new List<TargetableObject>();

        // TargetSelector が持つ方向別配列（m_targetArrayなど）から有効なものを集める
        foreach (MAGIC_CAST_DIRECTION dir in System.Enum.GetValues(typeof(MAGIC_CAST_DIRECTION)))
        {
            GameObject obj = m_targetSelector.GetSelectedTarget(dir);

            if (obj != null)
            {
                TargetableObject targetable = obj.GetComponent<TargetableObject>();

                if (targetable != null && !targets.Contains(targetable))
                {
                    targets.Add(targetable);
                }
            }
        }

        return targets;
    }

    public void PlaySelectedEffect(MAGIC_CAST_DIRECTION direction)
    {
        GameObject target = m_targetSelector.GetSelectedTarget(direction);

        if (target == null) 
        {
            return;
        }

        TargetableObject targetable = target.GetComponent<TargetableObject>();

        if (targetable == null) 
        {
            return;
        }

        foreach (LockOnTargetController ui in m_lockOnUIList) 
        {
            if (ui == null) 
            {
                continue;
            }

            if (!ui.gameObject.activeSelf) 
            {
                continue;
            }

            if (ui.GetTarget() == targetable) 
            {
                ui.SelectedTargetUI();
                break;
            }
        }
    }
}
