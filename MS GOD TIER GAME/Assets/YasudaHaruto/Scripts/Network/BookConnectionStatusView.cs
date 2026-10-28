using UnityEngine;
using UnityEngine.UI;

// Created by the bootstrap; requires no scene references and never captures input.
public sealed class BookConnectionStatusView : MonoBehaviour
{
    private BookNetworkClient m_client;
    private GameObject m_panel;
    private Text m_label;
    private Font m_font;

    public void Initialize(BookNetworkClient client)
    {
        m_client = client;
        m_panel = new GameObject("Connection Status", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        m_panel.transform.SetParent(transform, false);
        Canvas canvas = m_panel.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;
        CanvasScaler scaler = m_panel.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.matchWidthOrHeight = 0.5f;

        var banner = new GameObject("Banner", typeof(RectTransform), typeof(Image));
        banner.transform.SetParent(m_panel.transform, false);
        RectTransform rect = (RectTransform)banner.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0, -16);
        rect.sizeDelta = new Vector2(540, 58);
        Image background = banner.GetComponent<Image>();
        background.color = new Color(0.08f, 0.08f, 0.1f, 0.9f);
        background.raycastTarget = false;

        var label = new GameObject("Label", typeof(RectTransform), typeof(Text));
        label.transform.SetParent(banner.transform, false);
        RectTransform labelRect = (RectTransform)label.transform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
        m_font = Font.CreateDynamicFontFromOSFont(new[] { "Yu Gothic", "Meiryo", "Noto Sans CJK JP", "sans-serif", "Arial" }, 24);
        m_label = label.GetComponent<Text>();
        m_label.font = m_font;
        m_label.fontSize = 24;
        m_label.alignment = TextAnchor.MiddleCenter;
        m_label.color = Color.white;
        m_label.raycastTarget = false;
        Update();
    }

    private void Update()
    {
        if (m_panel == null) return;
        bool disconnected = m_client != null && m_client.ConnectionState != BOOK_CONNECTION_STATE.CONNECTED;
        m_panel.SetActive(disconnected);
        if (disconnected)
            m_label.text = m_client.IsReconnecting ? "再接続中… ページめくりのみ操作できます" : "未接続：ページめくりのみ操作できます";
    }

    private void OnDestroy()
    {
        if (m_panel != null) Destroy(m_panel);
        if (m_font != null) Destroy(m_font);
    }
}
