//-----------------------------------------------
// SceneTransition.cs
// 制作日：2026/09/10
// 制作者：渡辺開斗
// 概要：シーン遷移の抽象クラス
//-----------------------------------------------
using System.Collections;
using UnityEngine;

public abstract class SceneTransition : MonoBehaviour
{
    public enum TRANSITION_TYPE  // 遷移の種類
    {
        FADE,
    }

    public abstract TRANSITION_TYPE Type { get; }    // 遷移の種類を取得

    public abstract IEnumerator PlayIn();   // シーン遷移の開始時の演出を行うコルーチン
    public abstract IEnumerator PlayOut();  // シーン遷移の終了時の演出を行うコルーチン
}
