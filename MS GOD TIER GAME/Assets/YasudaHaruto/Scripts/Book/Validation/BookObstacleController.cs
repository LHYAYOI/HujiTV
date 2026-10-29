using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace YasudaHaruto.BookValidation
{
public sealed class BookObstacleController : MonoBehaviour
{
    private readonly Queue<ObstacleData> m_pending = new Queue<ObstacleData>();
    private readonly Dictionary<byte, ObstacleData> m_database = new Dictionary<byte, ObstacleData>();
    private readonly ObstaclePatchGraphic[] m_graphics = new ObstaclePatchGraphic[4];
    private readonly Rect[] m_areas = new Rect[4];
    private BookGameController m_game;
    private BookGameDragInput m_input;
    private YasudaBookViewController m_book;
    private Canvas m_canvas;
    private PageInstance m_page;
    private ObstacleInstance m_pressed;
    private int m_slot = -1;
    private uint m_lastEvent;
    private bool m_hasEvent;
    private bool m_connected = true;
    private Rect m_rightArea;
    public bool BlocksActions => m_game != null && m_game.CurrentPage != null && m_game.CurrentPage.Obstacles.BlocksActions;
    private bool Ready => m_game != null && m_game.CanRemoveObstacles;

    public void Initialize(BookGameController game, BookGameDragInput input, ObstacleData[] definitions, Rect rightArea)
    {
        m_game = game;
        m_input = input;
        m_book = game.BookView;
        float x = Mathf.Clamp(rightArea.x, 0f, 0.95f), y = Mathf.Clamp(rightArea.y, 0f, 0.95f);
        m_rightArea = new Rect(x, y, Mathf.Clamp(rightArea.width, 0.05f, 1f - x), Mathf.Clamp(rightArea.height, 0.05f, 1f - y));
        if (definitions == null || definitions.Length == 0) definitions = Resources.LoadAll<ObstacleData>("BookObstacles");
        foreach (var data in definitions)
        {
            if (data == null || data.ObstacleId == 0) continue;
            if (m_database.ContainsKey(data.ObstacleId)) { Debug.LogError("Duplicate obstacle ID: " + data.ObstacleId, this); continue; }
            if (data.RemovalType == OBSTACLE_REMOVAL_TYPE.SCRATCH && data.Texture != null && !data.Texture.isReadable)
            { Debug.LogError("Scratch texture requires Read/Write Enabled: " + data.name, this); continue; }
            m_database.Add(data.ObstacleId, data);
        }
        m_input.SetObstacleController(this);
        var root = new GameObject("Page Obstacles", typeof(RectTransform), typeof(Canvas));
        root.transform.SetParent(transform, false);
        m_canvas = root.GetComponent<Canvas>();
        m_canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        m_canvas.sortingOrder = 50;
        for (int i = 0; i < 4; i++)
        {
            var go = new GameObject("Obstacle Slot " + (i + 1), typeof(RectTransform), typeof(CanvasRenderer), typeof(ObstaclePatchGraphic));
            go.transform.SetParent(root.transform, false);
            m_graphics[i] = go.GetComponent<ObstaclePatchGraphic>();
            m_graphics[i].raycastTarget = false;
            go.SetActive(false);
        }
    }

    public void Receive(uint eventId, byte typeId)
    {
        // IDs are monotonically increasing within one server connection. No replay after reconnect.
        if (m_hasEvent && unchecked((int)(eventId - m_lastEvent)) <= 0) return;
        m_hasEvent = true;
        m_lastEvent = eventId;
        if (!m_database.TryGetValue(typeId, out var data)) { Debug.LogWarning("Unknown obstacle ID: " + typeId, this); return; }
        if (m_book == null || m_game == null) return;
        if (m_book.IsBusy || m_book.FlipView.IsTurning)
        {
            if (m_pending.Count < 32) m_pending.Enqueue(data);
            else Debug.LogWarning("Obstacle queue full; request discarded.", this);
            return;
        }
        // Fixed pages, closed covers and inventory selection do not acquire deferred attacks.
        if (!Ready || m_game.CurrentPage == null) return;
        Apply(data);
    }

    private void Apply(ObstacleData data)
    {
        var page = m_game.CurrentPage;
        if (page == null) return;
        page.Obstacles.Apply(data);
        if (m_pressed != null && !ReferenceEquals(page.Obstacles[m_slot], m_pressed)) CancelPointer();
        SynchronizeInput();
    }

    public void SetConnected(bool connected)
    {
        m_connected = connected;
        m_hasEvent = false;
        m_pending.Clear();
        CancelPointer();
        SynchronizeInput();
    }

    private void SynchronizeInput()
    {
        bool blocked = BlocksActions;
        m_input?.SetRuneInputAvailable(m_connected && !blocked);
    }

    private void LateUpdate()
    {
        if (m_game == null || m_canvas == null) return;
        if (!ReferenceEquals(m_page, m_game.CurrentPage))
        {
            CancelPointer();
            m_page = m_game.CurrentPage;
            SynchronizeInput();
        }
        if (Ready)
            while (m_pending.Count > 0) Apply(m_pending.Dequeue());
        bool visible = Ready && m_game.CurrentPage != null;
        Rect? bookRect = visible ? m_input.GetBookScreenRect() : null;
        for (int i = 0; i < 4; i++)
        {
            var instance = visible ? m_game.CurrentPage.Obstacles[i] : null;
            var graphic = m_graphics[i];
            graphic.gameObject.SetActive(instance != null && bookRect.HasValue);
            if (!graphic.gameObject.activeSelf) continue;
            Rect book = bookRect.Value;
            Rect local;
            if (i == 3) local = new Rect(0.04f, 0.08f, 0.42f, 0.84f);
            else
            {
                // Three non-overlapping horizontal bands inside the configurable right-page area.
                float height = m_rightArea.height / 3f;
                float seed = (instance.Sequence * 0.618034f) % 1f;
                local = new Rect(0.5f + (m_rightArea.x + m_rightArea.width * 0.1f * seed) * 0.5f,
                    m_rightArea.y + height * i + height * 0.05f, m_rightArea.width * 0.45f, height * 0.9f);
            }
            Rect screen = new Rect(book.x + local.x * book.width, book.y + local.y * book.height,
                local.width * book.width, local.height * book.height);
            m_areas[i] = screen;
            graphic.rectTransform.anchorMin = graphic.rectTransform.anchorMax = Vector2.zero;
            graphic.rectTransform.pivot = Vector2.zero;
            graphic.rectTransform.anchoredPosition = screen.position;
            graphic.rectTransform.sizeDelta = screen.size;
            graphic.Bind(instance);
        }
        if (!visible) CancelPointer();
    }

    public bool BeginPointer(Vector2 screen)
    {
        if (!Ready || m_game.CurrentPage == null) return false;
        CancelPointer();
        for (int i = 3; i >= 0; i--)
        {
            var entry = m_game.CurrentPage.Obstacles[i];
            if (entry == null || !m_graphics[i].gameObject.activeInHierarchy || !m_areas[i].Contains(screen)) continue;
            m_slot = i;
            m_pressed = entry;
            entry.Interaction.Begin(Normalize(screen));
            FinishIfComplete();
            return true;
        }
        return false;
    }
    private Vector2 Normalize(Vector2 screen) => new Vector2((screen.x - m_areas[m_slot].x) / m_areas[m_slot].width,
        (screen.y - m_areas[m_slot].y) / m_areas[m_slot].height);
    public void MovePointer(Vector2 screen)
    {
        if (m_pressed == null || !Ready || m_game.CurrentPage == null ||
            !ReferenceEquals(m_game.CurrentPage.Obstacles[m_slot], m_pressed)) { CancelPointer(); return; }
        Vector2 p = Normalize(screen);
        // Exiting the patch breaks the stroke, so crossing elsewhere does not erase it.
        if (p.x < 0 || p.x > 1 || p.y < 0 || p.y > 1) { CancelPointer(); return; }
        m_pressed.Interaction.Move(p);
        FinishIfComplete();
    }
    private void FinishIfComplete()
    {
        if (m_pressed == null || !m_pressed.Interaction.Complete) return;
        m_game.CurrentPage.Obstacles.Clear(m_slot, m_pressed);
        CancelPointer();
        SynchronizeInput();
    }
    public void CancelPointer() { m_pressed?.Interaction.Cancel(); m_pressed = null; m_slot = -1; }
    private void OnApplicationFocus(bool focused) { if (!focused) { CancelPointer(); m_input?.CancelObstaclePointer(); } }
    private void OnDisable() { CancelPointer(); if (m_canvas != null) m_canvas.gameObject.SetActive(false); }
    private void OnEnable() { if (m_canvas != null) m_canvas.gameObject.SetActive(true); }
}
}
