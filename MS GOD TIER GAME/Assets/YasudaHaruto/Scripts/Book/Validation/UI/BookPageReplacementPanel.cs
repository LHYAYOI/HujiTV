using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace YasudaHaruto.BookValidation
{
    public sealed class BookPageReplacementPanel : MonoBehaviour
    {
        [SerializeField] private BookGameController m_controller;
        [SerializeField] private GameObject m_panel;
        [SerializeField] private Text m_incomingLabel;
        [SerializeField] private Text m_selectionLabel;
        [SerializeField] private RectTransform m_rowsParent;
        [SerializeField] private BookPageChoiceRow m_rowTemplate;
        [SerializeField] private Button m_confirmButton;
        [SerializeField] private Button m_declineButton;
        [SerializeField] private ScrollRect m_scroll;
        [Tooltip("Assign a project font here for the target platform. Empty uses an OS font for this prototype.")]
        [SerializeField] private Font m_font;
        private Font m_runtimeFont;
        private readonly List<BookPageChoiceRow> m_rows = new List<BookPageChoiceRow>();
        private PageInstance m_selected;
        private int m_selectedIndex = -1;
        public bool IsVisible => m_panel.activeSelf;
        public bool CanConfirm => m_confirmButton.interactable;
        public PageInstance SelectedPage => m_selected;

        private void OnEnable()
        {
            if (m_controller == null) return;
            if (m_font == null)
                m_runtimeFont = Font.CreateDynamicFontFromOSFont(new[] { "Yu Gothic", "Meiryo", "Noto Sans CJK JP", "Arial" }, 24);
            Font font = m_font != null ? m_font : m_runtimeFont;
            foreach (var label in m_panel.GetComponentsInChildren<Text>(true)) label.font = font;
            m_controller.ReplacementRequested += Show;
            m_controller.ReplacementDismissed += Hide;
            m_controller.InventoryEditFinished += OnEditFinished;
            m_confirmButton.onClick.AddListener(Confirm);
            m_declineButton.onClick.AddListener(Decline);
            if (m_controller.PendingPage != null) Show();
            else m_panel.SetActive(false);
        }

        private void OnDisable()
        {
            if (m_controller != null)
            {
                m_controller.ReplacementRequested -= Show;
                m_controller.ReplacementDismissed -= Hide;
                m_controller.InventoryEditFinished -= OnEditFinished;
            }
            m_confirmButton.onClick.RemoveListener(Confirm);
            m_declineButton.onClick.RemoveListener(Decline);
            if (m_runtimeFont != null) Destroy(m_runtimeFont);
            m_runtimeFont = null;
        }

        private void Show()
        {
            if (m_controller.PendingPage == null) { m_panel.SetActive(false); return; }
            foreach (var row in m_rows)
            {
                row.gameObject.SetActive(false);
                Destroy(row.gameObject);
            }
            m_rows.Clear();
            m_selected = null;
            m_selectedIndex = -1;
            m_panel.SetActive(true);
            var incoming = m_controller.PendingPage;
            m_incomingLabel.text = $"入手ページ：{incoming.DisplayName}（{TypeLabel(incoming.PageType)}）\n所持 {m_controller.Pages.Count} / {m_controller.MaxPageCount}　手放すページを選択（上下にスクロール）";
            Font font = m_font != null ? m_font : m_runtimeFont;
            for (int i = 0; i < m_controller.Pages.Count; i++)
            {
                var row = Instantiate(m_rowTemplate, m_rowsParent);
                row.gameObject.SetActive(true);
                row.Bind(m_controller.Pages[i], i, font, SelectPage);
                m_rows.Add(row);
            }
            m_scroll.verticalNormalizedPosition = 1f;
            RefreshState();
        }

        public void SelectPage(PageInstance page)
        {
            if (m_controller.IsEditing || m_controller.PendingPage == null) return;
            bool found = false;
            m_selectedIndex = -1;
            for (int i = 0; i < m_controller.Pages.Count; i++)
                if (ReferenceEquals(m_controller.Pages[i], page)) { found = true; m_selectedIndex = i; break; }
            m_selected = found ? page : null;
            RefreshState();
        }

        public void Confirm()
        {
            if (m_selected == null || m_controller.IsEditing || m_controller.PendingPage == null) return;
            if (!m_controller.ReplacePending(m_selected))
                m_selectionLabel.text = "まだ入れ替えできません。少し待ってからもう一度押してください";
            else RefreshState();
        }

        public void Decline()
        {
            if (m_controller.DeclinePending())
            {
                m_selected = null;
                m_panel.SetActive(false);
            }
        }

        private void Hide()
        {
            m_selected = null;
            m_panel.SetActive(false);
        }

        private void OnEditFinished(bool success)
        {
            if (m_controller.PendingPage == null) { m_panel.SetActive(false); m_selected = null; }
            else Show();
        }

        private void RefreshState()
        {
            bool busy = m_controller.IsEditing;
            m_confirmButton.interactable = !busy && m_selected != null;
            m_declineButton.interactable = !busy;
            m_selectionLabel.text = busy ? "入れ替え中…" : m_selected == null
                ? "選択中：なし" : $"手放すページ：{m_selectedIndex + 1}. {m_selected.Data.DisplayName}";
            foreach (var row in m_rows) row.SetState(ReferenceEquals(row.Page, m_selected), !busy);
        }

        public static string TypeLabel(PAGE_TYPE type) => type switch
        {
            PAGE_TYPE.MAGIC => "魔法", PAGE_TYPE.GUIDE => "ガイド", PAGE_TYPE.OBSTACLE => "お邪魔", _ => "ページ"
        };
    }
}
