using System.Collections.Generic;
using UnityEngine;

namespace YasudaHaruto.BookValidation
{
    // Network IDs are resolved here; inventory rules remain in BookGameController.
    public sealed class BookPageRequestQueue
    {
        private readonly Queue<(PageData Data, bool Present)> m_requests = new();
        private readonly HashSet<byte> m_pendingIds = new();
        private readonly PageDatabase m_database;
        private readonly BookGameController m_game;

        public BookPageRequestQueue(PageDatabase database, BookGameController game)
        {
            m_database = database;
            m_game = game;
        }

        public void Receive(byte id) => ReceiveAcquired(id, true);

        public void ReceiveAcquired(byte id, bool present)
        {
            if (m_database == null) { Debug.LogWarning("Page database is not configured."); return; }
            PageData data = m_database.GetPage(id);
            if (!BookInventory.CanAdd(data)) { Debug.LogWarning("Unsupported page request: " + id); return; }
            if (m_pendingIds.Add(id)) m_requests.Enqueue((data, present));
        }

        public void Tick()
        {
            if (m_requests.Count == 0 || m_game == null || m_game.IsInputBlocked) return;
            var request = m_requests.Peek();
            // Keep the request until the captured inventory has committed.
            if (m_game.RequestAdd(request.Data, request.Present) == PAGE_ADD_RESULT.INVALID)
            {
                m_requests.Dequeue();
                m_pendingIds.Remove(request.Data.PageId);
            }
        }

        public void Clear()
        {
            m_requests.Clear();
            m_pendingIds.Clear();
        }
    }
}
