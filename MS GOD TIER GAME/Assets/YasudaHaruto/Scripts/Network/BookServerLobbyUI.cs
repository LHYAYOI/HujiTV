using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.InputSystem.UI;

// Scene-local UI; the separately rooted server and flow survive its destruction.
public sealed class BookServerLobbyUI : MonoBehaviour
{
    [SerializeField] private BookNetworkServer m_server;
    [SerializeField] private BookServerGameFlow m_gameFlow;
    private Text m_player1;
    private Text m_player2;
    private Text m_status;
    private Button m_startButton;
    private Font m_font;

    private void Start()
    {
        if (m_server == null) m_server = BookNetworkServer.Instance;
        if (m_gameFlow == null && m_server != null) m_gameFlow = m_server.GetComponent<BookServerGameFlow>();
        m_font = Font.CreateDynamicFontFromOSFont(new[] { "Yu Gothic", "Meiryo", "Arial" }, 24);
        var canvasObject = new GameObject("Server Lobby Canvas", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.matchWidthOrHeight = 0.5f;
        var background = new GameObject("Background", typeof(RectTransform), typeof(Image));
        background.transform.SetParent(canvasObject.transform, false);
        var backgroundRect = (RectTransform)background.transform;
        backgroundRect.anchorMin = Vector2.zero;
        backgroundRect.anchorMax = Vector2.one;
        backgroundRect.offsetMin = backgroundRect.offsetMax = Vector2.zero;
        background.GetComponent<Image>().color = new Color(0.055f, 0.07f, 0.11f);
        CreateLabel(canvasObject.transform, "本型コントローラー 接続確認", 210, 36);
        m_player1 = CreateLabel(canvasObject.transform, "", 90, 30);
        m_player2 = CreateLabel(canvasObject.transform, "", 25, 30);
        m_status = CreateLabel(canvasObject.transform, "", -65, 22);
        var buttonObject = new GameObject("Start Game", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(canvasObject.transform, false);
        var rect = (RectTransform)buttonObject.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(360, 76);
        rect.anchoredPosition = new Vector2(0, -180);
        buttonObject.GetComponent<Image>().color = new Color(0.16f, 0.47f, 0.62f);
        m_startButton = buttonObject.GetComponent<Button>();
        m_startButton.targetGraphic = buttonObject.GetComponent<Image>();
        m_startButton.onClick.AddListener(OnStartPressed);
        var label = CreateLabel(buttonObject.transform, "ゲームを開始", 0, 28);
        ((RectTransform)label.transform).sizeDelta = new Vector2(340, 70);
        if (FindFirstObjectByType<EventSystem>() == null)
        {
            var events = new GameObject("Lobby EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            events.transform.SetParent(transform, false);
        }
    }

    private Text CreateLabel(Transform parent, string value, float y, int size)
    {
        var labelObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
        labelObject.transform.SetParent(parent, false);
        var rect = (RectTransform)labelObject.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(1100, 72);
        rect.anchoredPosition = new Vector2(0, y);
        var label = labelObject.GetComponent<Text>();
        label.font = m_font;
        label.fontSize = size;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = Color.white;
        label.raycastTarget = false;
        label.text = value;
        return label;
    }

    private void Update()
    {
        if (m_startButton == null) return;
        UpdatePlayer(m_player1, PLAYER_SLOT.PLAYER1, "Player 1");
        UpdatePlayer(m_player2, PLAYER_SLOT.PLAYER2, "Player 2");
        m_startButton.interactable = m_gameFlow != null && m_gameFlow.CanStart;
        m_status.text = m_server == null || !m_server.IsListening ? "Serverを起動できません。Consoleを確認してください。" :
            !string.IsNullOrEmpty(m_gameFlow?.Status) ? m_gameFlow.Status :
            "タブレットを接続してください。1台の登録完了で開始できます。";
    }

    private void UpdatePlayer(Text label, PLAYER_SLOT slot, string name)
    {
        bool connected = m_server != null && m_server.IsPlayerConnected(slot);
        label.text = name + (connected ? "  接続済み" : "  未接続");
        label.color = connected ? new Color(0.4f, 0.95f, 0.65f) : new Color(0.65f, 0.68f, 0.75f);
    }

    private void OnStartPressed() { if (m_gameFlow != null) m_gameFlow.StartGame(); }
    private void OnDestroy() { if (m_font != null) Destroy(m_font); }
}
