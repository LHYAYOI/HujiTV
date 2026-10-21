using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [SerializeField] private MagicBulletManager m_bulletManager;
    [SerializeField] private MagicDatabase m_magicDatabase;
    [SerializeField] private TargetSelector m_targetSelector;
    [SerializeField] private BookNetworkServer m_bookNetworkServer;

    [SerializeField] private MagicData magicData;

    private void OnEnable()
    {
        m_bookNetworkServer.CastMagicReceived += OnReceiveCastMagic;
    }

    private void OnDisable()
    {
        m_bookNetworkServer.CastMagicReceived -= OnReceiveCastMagic;
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.LeftArrow))
        {
            FireBullet(magicData, MAGIC_CAST_DIRECTION.LEFT);
        }

        if (Input.GetKeyDown(KeyCode.UpArrow))
        {
            FireBullet(magicData, MAGIC_CAST_DIRECTION.CENTER);
        }

        if (Input.GetKeyDown(KeyCode.RightArrow))
        {
            FireBullet(magicData, MAGIC_CAST_DIRECTION.RIGHT);
        }
    }

    //弾の発射イベント
    private void OnReceiveCastMagic(PLAYER_SLOT slot, byte id, MAGIC_CAST_DIRECTION direction)
    {
        MagicData data = m_magicDatabase.Find(id);
        FireBullet(data, direction);
    }

    //弾の発射
    public void FireBullet(MagicData magicData, MAGIC_CAST_DIRECTION direction)
    {
        m_targetSelector.SortTargets();
        GameObject target = m_targetSelector.GetSelectedTarget(direction);

        if (target == null) 
        {
            return;
        }

        //CameraManager.Instance.SetLookTarget(target);

        CameraManager.Instance.ShakeCamera(.5f, 0.5f);

        m_bulletManager.FireBullet(magicData, direction, target);
    }
}
