using System;
using System.Collections.Generic;

namespace YasudaHaruto.BookValidation
{
    // Owns game spreads. The renderer receives only a projection of this inventory.
    public sealed class BookInventory
    {
        private readonly BookPageData m_firstFixedPage;
        private readonly BookPageData m_lastFixedPage;
        private readonly PageFactory m_factory;
        private List<PageInstance> m_pages;

        public IReadOnlyList<PageInstance> Pages { get; private set; }
        public int MaxPageCount { get; }

        public BookInventory(PageFactory factory, IReadOnlyList<PageData> initialPages,
            BookPageData firstFixedPage, BookPageData lastFixedPage, int maxPageCount)
        {
            if (factory == null || initialPages == null || firstFixedPage == null ||
                lastFixedPage == null || maxPageCount < 1 || initialPages.Count > maxPageCount)
                throw new ArgumentException("Invalid book inventory configuration.");

            m_factory = factory;
            m_firstFixedPage = firstFixedPage;
            m_lastFixedPage = lastFixedPage;
            MaxPageCount = maxPageCount;
            var pages = new List<PageInstance>(initialPages.Count);
            foreach (PageData data in initialPages)
            {
                if (!HasVisuals(data)) throw new ArgumentException("Initial page has no left/right visual.");
                pages.Add(m_factory.Create(data));
            }
            Commit(pages);
        }

        public static bool HasVisuals(PageData data) => data != null &&
            data.LeftVisualData != null && data.RightVisualData != null;

        public static bool CanAdd(PageData data) => HasVisuals(data) &&
            (data.InsertType == PAGE_INSERT_TYPE.END || data.InsertType == PAGE_INSERT_TYPE.AFTER_CURRENT);

        public PageInstance GetPageAtSpread(int spread) =>
            spread > 0 && spread <= m_pages.Count ? m_pages[spread - 1] : null;

        public List<PageInstance> Add(PageData data, PageInstance anchor, bool atFront)
        {
            if (!CanAdd(data) || m_pages.Count >= MaxPageCount) return null;
            var candidate = new List<PageInstance>(m_pages);
            candidate.Insert(InsertionIndex(candidate, data, anchor, atFront), m_factory.Create(data));
            return candidate;
        }

        public List<PageInstance> Replace(PageData data, PageInstance selected, PageInstance anchor,
            bool atFront, out PageInstance visible)
        {
            visible = anchor;
            int removed = m_pages.IndexOf(selected);
            if (!CanAdd(data) || removed < 0) return null;
            var candidate = new List<PageInstance>(m_pages);
            candidate.RemoveAt(removed);
            var added = m_factory.Create(data);
            int at = ReferenceEquals(selected, anchor) && data.InsertType == PAGE_INSERT_TYPE.AFTER_CURRENT
                ? removed : InsertionIndex(candidate, data, anchor, atFront);
            candidate.Insert(at, added);
            if (ReferenceEquals(selected, anchor)) visible = added;
            return candidate;
        }

        public List<PageInstance> Remove(PageInstance selected, PageInstance current, out PageInstance visible)
        {
            visible = current;
            int at = m_pages.IndexOf(selected);
            if (at < 0) return null;
            var candidate = new List<PageInstance>(m_pages);
            candidate.RemoveAt(at);
            if (ReferenceEquals(current, selected))
                visible = candidate.Count == 0 ? null : candidate[Math.Min(at, candidate.Count - 1)];
            return candidate;
        }

        private static int InsertionIndex(List<PageInstance> pages, PageData data, PageInstance anchor, bool atFront)
        {
            if (data.InsertType == PAGE_INSERT_TYPE.END) return pages.Count;
            if (anchor != null)
            {
                int index = pages.IndexOf(anchor);
                if (index < 0) throw new InvalidOperationException("Insertion anchor is no longer owned.");
                return index + 1;
            }
            return atFront ? 0 : pages.Count;
        }

        public List<BookPageData> BuildVisualPages(IReadOnlyList<PageInstance> pages)
        {
            var visuals = new List<BookPageData> { null, m_firstFixedPage };
            foreach (var page in pages)
            {
                if (page == null || !HasVisuals(page.Data)) throw new ArgumentException("Invalid game spread.");
                visuals.Add(page.Data.LeftVisualData);
                visuals.Add(page.Data.RightVisualData);
            }
            visuals.Add(m_lastFixedPage);
            return visuals;
        }

        // Commit only after the view has accepted and captured the complete candidate.
        public void Commit(List<PageInstance> candidate)
        {
            if (candidate == null || candidate.Count > MaxPageCount) throw new ArgumentException("Invalid inventory candidate.");
            m_pages = new List<PageInstance>(candidate);
            Pages = m_pages.AsReadOnly();
        }
    }
}
