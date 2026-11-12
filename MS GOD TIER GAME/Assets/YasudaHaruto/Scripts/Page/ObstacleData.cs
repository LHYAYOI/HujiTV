using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "Book/Obstacle Data")]
public sealed class ObstacleData : ScriptableObject
{
    [SerializeField] private Texture2D m_texture;
    [Header("段階ごとの見た目（1〜4番枠）")]
    [SerializeField] private Texture2D[] m_stageTextures = new Texture2D[4];
    [Tooltip("4番枠と同じ進捗で消える追加の飛沫。別の段階にはなりません。")]
    [SerializeField] private ObstacleImagePart[] m_fourthStageParts = new ObstacleImagePart[0];
    [SerializeField, HideInInspector] private ObstacleAlphaMask[] m_hitMasks = new ObstacleAlphaMask[0];
    private Dictionary<Texture2D, ObstacleAlphaMask> m_maskLookup;
    [Tooltip("見開き全体を0〜1とした配置。Y=0は下端。画像はこの矩形へ表示します。")]
    [SerializeField] private Rect[] m_stagePlacements =
    {
        new Rect(0.501f, 0.537f, 0.4493f, 0.3994f),
        new Rect(0.529f, 0.034f, 0.4462f, 0.4049f),
        new Rect(0.108f, 0.708f, 0.4403f, 0.2557f),
        new Rect(0f, 0f, 1f, 1f)
    };
    [SerializeField] private Color m_color = Color.white;
    [InspectorName("消えるまでに必要な擦り距離")]
    [Tooltip("汚れの表示範囲の短辺を1とした合計移動距離。小さいほど早く消えます。2なら4の半分の距離で解除。方向は問いません。")]
    [SerializeField, Min(0.1f)] private float m_requiredDistance = 4f;
    [Tooltip("Movements smaller than this distance from the last anchor are ignored.")]
    [SerializeField, Range(0.001f, 0.02f)] private float m_minimumMovement = 0.003f;
    public Texture2D Texture => m_texture;
    public bool HasStageImages => m_stageTextures != null && m_stageTextures.Length >= 4 &&
        m_stageTextures[0] != null && m_stageTextures[1] != null && m_stageTextures[2] != null && m_stageTextures[3] != null;
    public Texture2D GetTexture(int slot) => m_stageTextures != null && slot >= 0 && slot < m_stageTextures.Length &&
        m_stageTextures[slot] != null ? m_stageTextures[slot] : m_texture;
    public int ExtraPartCount => m_fourthStageParts != null ? m_fourthStageParts.Length : 0;
    public Texture2D GetPartTexture(int slot, int part) => part == 0 ? GetTexture(slot) : m_fourthStageParts[part - 1].Texture;
    public Rect GetPartPlacement(int slot, int part) => part == 0 ? GetSpreadPlacement(slot) : m_fourthStageParts[part - 1].Placement;
    public bool ContainsInk(Texture2D texture, Vector2 uv)
    {
        if (texture == null) return false;
        if (m_maskLookup == null)
        {
            m_maskLookup = new Dictionary<Texture2D, ObstacleAlphaMask>();
            foreach (var mask in m_hitMasks)
                if (mask != null && mask.Texture != null) m_maskLookup[mask.Texture] = mask;
        }
        return m_maskLookup.TryGetValue(texture, out var found) && found.Contains(uv);
    }
    private void OnEnable() => m_maskLookup = null;
#if UNITY_EDITOR
    public bool SetHitMasks(ObstacleAlphaMask[] masks)
    {
        bool same = m_hitMasks != null && m_hitMasks.Length == masks.Length;
        for (int i = 0; same && i < masks.Length; i++) same &= masks[i].Matches(m_hitMasks[i]);
        if (same) return false;
        m_hitMasks = masks;
        m_maskLookup = null;
        return true;
    }
#endif
    public Rect GetSpreadPlacement(int slot) => m_stagePlacements != null && slot >= 0 && slot < m_stagePlacements.Length
        ? m_stagePlacements[slot] : new Rect(0, 0, 1, 1);
    public Color Color => m_color;
    public float RequiredDistance => Mathf.Max(0.1f, m_requiredDistance);
    public float MinimumMovement => Mathf.Clamp(m_minimumMovement, 0.001f, 0.02f);
}
