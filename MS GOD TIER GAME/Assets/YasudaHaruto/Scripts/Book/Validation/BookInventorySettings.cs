using UnityEngine;

namespace YasudaHaruto.BookValidation
{
    [CreateAssetMenu(menuName = "YasudaHaruto/Book Inventory Settings")]
    public sealed class BookInventorySettings : ScriptableObject
    {
        [SerializeField, Min(1)] private int m_maxGamePageCount = 8;
        [SerializeField, Min(1)] private int m_maxPendingRequests = 16;
        public int MaxGamePageCount => Mathf.Max(1, m_maxGamePageCount);
        public int MaxPendingRequests => Mathf.Max(1, m_maxPendingRequests);
    }
}
