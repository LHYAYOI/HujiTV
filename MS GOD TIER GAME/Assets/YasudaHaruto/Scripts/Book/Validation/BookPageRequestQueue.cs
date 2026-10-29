using System.Collections.Generic;
using UnityEngine;

namespace YasudaHaruto.BookValidation
{
    // Network IDs are resolved here; inventory rules remain in BookGameController.
    public sealed class BookPageRequestQueue
    {
        private readonly Queue<PageData> m_requests = new Queue<PageData>();
        private readonly PageDatabase m_database;
        private readonly BookGameController m_game;

        public BookPageRequestQueue(PageDatabase database, BookGameController game)
        {
            m_database = database;
            m_game = game;
        }

        public void Receive(byte id)
        {
            if (m_database == null) { Debug.LogWarning("Page database is not configured."); return; }
            PageData data = m_database.GetPage(id);
            if (!BookInventory.CanAdd(data)) { Debug.LogWarning("Unsupported page request: " + id); return; }
            if (m_requests.Count >= m_game.MaxPendingRequests)
            { Debug.LogWarning("Page request queue is full; incoming request rejected."); return; }
            m_requests.Enqueue(data);
        }

        public void Tick()
        {
            if (m_requests.Count == 0 || m_game == null || m_game.IsInputBlocked) return;
            var result = m_game.RequestAdd(m_requests.Peek());
            if (result != PAGE_ADD_RESULT.BUSY) m_requests.Dequeue();
        }

        public void Clear() => m_requests.Clear();
    }
}
