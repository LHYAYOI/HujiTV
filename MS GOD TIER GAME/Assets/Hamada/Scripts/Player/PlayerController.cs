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

        if (Input.GetKeyDown(KeyCode.K)) 
        {
            TakeDamage(1);
        }
    }

    //ダメージ処理
    public void TakeDamage(byte dirtAmount) 
    {
        //汚れの付着

        BookNetworkServer server = BookNetworkServer.Instance;

        if (server != null) 
        {
            server.SendObstacle(PLAYER_SLOT.PLAYER1, dirtAmount);
            server.SendObstacle(PLAYER_SLOT.PLAYER2, dirtAmount);
        }

        //ダメージエフェクト

        //シェイク
        CameraManager.Instance.ShakeCamera(.5f, 10);

        //音再生
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
        GameObject target = m_targetSelector.GetSelectedTarget(direction);

        if (target == null) 
        {
            return;
        }

        CameraManager.Instance.ShakeCameraInVHF(.5f, 1.0f, 0.0f, 0.0f);

        m_bulletManager.FireBullet(magicData, direction, target);
    }
}
