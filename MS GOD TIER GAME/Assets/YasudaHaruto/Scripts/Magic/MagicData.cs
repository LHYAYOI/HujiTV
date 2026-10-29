
using UnityEngine;

[CreateAssetMenu(fileName = "MagicData", menuName = "Book/Magic Data")]
public class MagicData : ScriptableObject
{
    [SerializeField] private byte m_magicId;

    [SerializeField] private string m_displayName;

    [SerializeField] private MAGIC_TYPE m_magicType;

    [SerializeField] private EVENT_MAGIC_TYPE m_eventMagicType = EVENT_MAGIC_TYPE.NONE;

    [SerializeField] private MAGIC_AIM_TYPE m_aimType = MAGIC_AIM_TYPE.THREE_DIRECTIONS;

    [Tooltip("着弾時に表示する固定ルーン画像。未設定の場合はルーン画像の演出を省略します。")]
    [SerializeField] private Sprite m_impactRuneTexture;

    public byte MagicId => m_magicId;
    public string DisplayName => m_displayName;
    public MAGIC_TYPE MagicType => m_magicType;
    public EVENT_MAGIC_TYPE EventMagicType => m_eventMagicType;
    public MAGIC_AIM_TYPE AimType => m_aimType;
    public Sprite ImpactRuneTexture => m_impactRuneTexture;
}
