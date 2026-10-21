using System;
using UnityEngine;
using UnityEngine.UI;

namespace YasudaHaruto.BookValidation
{
    // Presentation only. Inventory decisions belong to BookGameController.
    public sealed class BookPageChoiceRow : MonoBehaviour
    {
        [SerializeField] private Text m_label;
        [SerializeField] private Button m_button;
        [SerializeField] private Image m_background;
        [SerializeField] private Color m_normalColor = new Color(0.94f, 0.95f, 0.97f);
        [SerializeField] private Color m_selectedColor = new Color(0.72f, 0.85f, 1f);
        public PageInstance Page { get; private set; }

        public void Bind(PageInstance page, int index, Font font, Action<PageInstance> selected)
        {
            Page = page;
            m_label.font = font;
            m_label.text = $"{index + 1}.  {page.Data.DisplayName}  ・  {BookPageReplacementPanel.TypeLabel(page.Data.PageType)}";
            m_button.onClick.RemoveAllListeners();
            m_button.onClick.AddListener(() => selected(Page));
            SetState(false, true);
        }

        public void SetState(bool selected, bool interactable)
        {
            m_background.color = selected ? m_selectedColor : m_normalColor;
            m_button.interactable = interactable;
        }
    }
}
