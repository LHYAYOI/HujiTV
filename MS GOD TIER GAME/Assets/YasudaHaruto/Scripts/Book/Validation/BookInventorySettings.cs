using UnityEngine;

namespace YasudaHaruto.BookValidation
{
    /// <summary>本に保持できるゲームページ数などの所持設定。</summary>
    [CreateAssetMenu(menuName = "YasudaHaruto/Book Inventory Settings")]
    public sealed class BookInventorySettings : ScriptableObject
    {
        [SerializeField, Min(1)] private int m_maxGamePageCount = 8;

        public int MaxGamePageCount => Mathf.Max(1, m_maxGamePageCount);
    }
}
