//-----------------------------------------------
// TransitionManager.cs
// 制作日：2026/09/10
// 制作者：渡辺開斗
// 概要：シーン遷移管理クラス
//-----------------------------------------------
using System.Collections.Generic;
using UnityEngine;

public class TransitionManager : MonoBehaviour
{
    [Header("シーン遷移演出")]
    [SerializeField]
    private List<SceneTransition> m_transitionPrefabs = new();

    // 指定された種類の遷移演出を取得
    public SceneTransition GetTransition(SceneTransition.TRANSITION_TYPE type)
    {
        foreach (SceneTransition transition in m_transitionPrefabs)
        {
            if (transition == null)
                continue;

            if (transition.Type == type)
            {
                return transition;
            }
        }

        return null;
    }
}
