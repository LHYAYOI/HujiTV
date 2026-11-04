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
                if (!pages.Exists(page => page.Data.PageId == data.PageId))
                    pages.Add(m_factory.Create(data));
            }
            pages.Sort((left, right) => left.Data.PageId.CompareTo(right.Data.PageId));
            Commit(pages);
        }

        public static bool HasVisuals(PageData data) => data != null &&
            data.LeftVisualData != null && data.RightVisualData != null;

        public static bool CanAdd(PageData data) => HasVisuals(data) && data.PageId != 0;

        public PageInstance GetPageAtSpread(int spread) =>
            spread > 0 && spread <= m_pages.Count ? m_pages[spread - 1] : null;

        public bool Contains(byte pageId) => m_pages.Exists(page => page.Data.PageId == pageId);

        public List<PageInstance> Add(PageData data, out PageInstance added)
        {
            added = m_factory.Create(data);
            var candidate = new List<PageInstance>(m_pages);
            int index = candidate.FindIndex(page => page.Data.PageId > data.PageId);
            candidate.Insert(index < 0 ? candidate.Count : index, added);
            return candidate;
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
            if (candidate == null) throw new ArgumentException("Invalid inventory candidate.");
            m_pages = new List<PageInstance>(candidate);
            Pages = m_pages.AsReadOnly();
        }
    }
}
