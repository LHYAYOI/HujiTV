using UnityEngine;

/// <summary>
/// 着弾を待たずに発動する魔法の基底クラス。
/// 生成されたインスタンスの終了・破棄は派生クラスで管理する。
/// </summary>
public abstract class InstantMagicBase : MonoBehaviour
{
    public abstract void Execute();
}
