using UnityEngine;

public class MagicBulletController : MonoBehaviour
{
    [SerializeField] MagicBulletData m_magicBulletData;

    GameObject m_target;

    private void Update()
    {
        MoveProcess();
    }

    public MagicBulletData GetMagicBulletData()
    {
        return m_magicBulletData;
    }

    public void SetTarget(GameObject target)
    {
        m_target = target;
    }

    private void MoveProcess() 
    {
        if (m_target == null) 
        {
            return;
        }

        gameObject.transform.position = Vector3.MoveTowards(gameObject.transform.position, m_target.transform.position, 10 * Time.deltaTime);
    }

    //発射処理

    //発射音再生 

    //---何か当たった時に呼び出してほしい処理---//

    //エフェクト再生

    //ヒット時の音再生
}
