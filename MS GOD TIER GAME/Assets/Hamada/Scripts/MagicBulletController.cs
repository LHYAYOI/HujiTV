using UnityEngine;

public class MagicBulletController : MonoBehaviour
{
    [SerializeField] AttackData m_attackData;

    GameObject m_target;

    public AttackData GetAttackData()
    {
        return m_attackData;
    }

    public void SetTarget(GameObject target)
    {
        m_target = target;
    }

    //発射処理

    //発射音再生 

    //---何か当たった時に呼び出してほしい処理---//

    //エフェクト再生

    //ヒット時の音再生
}
