using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace YasudaHaruto.BookValidation
{
public sealed class BookObstacleController : MonoBehaviour
{
    private readonly Queue<byte> m_pending = new Queue<byte>();
    private ObstacleData m_settings;
    private readonly ObstaclePatchGraphic[] m_graphics = new ObstaclePatchGraphic[4];
    private readonly Rect[] m_areas = new Rect[4];
    private struct HitArea { public Rect Screen, UV; public Texture2D Texture; }
    private readonly List<HitArea>[] m_hitAreas = { new List<HitArea>(), new List<HitArea>(), new List<HitArea>(), new List<HitArea>() };
    private Vector2 m_pointerScreen;
    private BookGameController m_game;
    private BookGameDragInput m_input;
    private YasudaBookViewController m_book;
    private Camera m_mainCamera;
    private readonly List<PageSurface> m_surfaces = new List<PageSurface>();
    private readonly HashSet<Camera> m_pendingCaptures = new HashSet<Camera>();

    private sealed class PageSurface
    {
        public BookPageView View;
        public RectTransform Root;
        public Camera Camera;
        public bool Right;
        public bool Live;
        public PageInstance Page;
        public ObstaclePatchGraphic[] Graphics;
    }
    private PageInstance m_page;
    private ObstacleInstance m_pressed;
    private bool m_pointerInside;
    private int m_slot = -1;
    private uint m_lastEvent;
    private bool m_hasEvent;
    private bool m_connected = true;
    private Rect m_rightArea;
    public bool BlocksActions => m_game != null && m_game.CurrentPage != null && m_game.CurrentPage.Obstacles.BlocksActions;
    public ObstacleData Settings => m_settings;
    private bool Ready => m_game != null && m_game.CanRemoveObstacles;

    public void Initialize(BookGameController game, BookGameDragInput input, ObstacleData settings, Rect rightArea)
    {
        m_game = game;
        m_input = input;
        m_book = game.BookView;
        float x = Mathf.Clamp(rightArea.x, 0f, 0.95f), y = Mathf.Clamp(rightArea.y, 0f, 0.95f);
        m_rightArea = new Rect(x, y, Mathf.Clamp(rightArea.width, 0.05f, 1f - x), Mathf.Clamp(rightArea.height, 0.05f, 1f - y));
        m_settings = settings != null ? settings : Resources.Load<ObstacleData>("BookObstacles/Rub");
        if (m_settings == null) Debug.LogError("Dirt settings are not configured.", this);
        m_input.SetObstacleController(this);
        m_book.SetPageDecoration(BindPages);
    }

    private void BindPages(int spread, BookPageView left, BookPageView right, Camera camera)
    {
        if (m_mainCamera == null) m_mainCamera = camera;
        bool live = camera != m_mainCamera;
        PageInstance page = m_game.GetPageForCapture(spread, live);
        BindSurface(left, camera, false, live, page);
        BindSurface(right, camera, true, live, page);
    }

    private void BindSurface(BookPageView view, Camera camera, bool right, bool live, PageInstance page)
    {
        if (view == null || camera == null) return;
        var surface = m_surfaces.Find(item => item.View == view);
        if (surface == null)
        {
            surface = new PageSurface { View = view, Camera = camera, Right = right, Live = live };
            m_surfaces.Add(surface);
            var root = new GameObject("Page Stains", typeof(RectTransform));
            root.transform.SetParent(view.transform, false);
            root.layer = view.gameObject.layer;
            surface.Root = (RectTransform)root.transform;
            surface.Root.anchorMin = Vector2.zero;
            surface.Root.anchorMax = Vector2.one;
            surface.Root.offsetMin = surface.Root.offsetMax = Vector2.zero;
            surface.Graphics = new ObstaclePatchGraphic[4 + (m_settings != null ? m_settings.ExtraPartCount : 0)];
            for (int index = 0; index < surface.Graphics.Length; index++)
            {
                int slot = index < 4 ? index : 3;
                var go = new GameObject("Page Stain " + (slot + 1), typeof(RectTransform),
                    typeof(CanvasRenderer), typeof(ObstaclePatchGraphic));
                go.transform.SetParent(surface.Root, false);
                go.layer = view.gameObject.layer;
                var graphic = go.GetComponent<ObstaclePatchGraphic>();
                graphic.raycastTarget = false;
                surface.Graphics[index] = graphic;
                if (index < 4 && !live && right == (m_settings != null && m_settings.HasStageImages ? slot < 2 : slot < 3))
                    m_graphics[slot] = graphic;
            }
        }
        surface.Page = page;
        RefreshSurface(surface);
        m_pendingCaptures.Add(camera);
    }

    private Rect GetPlacement(int slot, int part = 0)
    {
        if (m_settings != null && m_settings.HasStageImages) return m_settings.GetPartPlacement(slot, part);
        if (slot == 3) return new Rect(0.015f, 0.04f, 0.47f, 0.92f);
        Rect placement = slot == 0 ? new Rect(0.52f, 0.58f, 0.48f, 0.42f)
            : slot == 1 ? new Rect(0f, 0.29f, 0.48f, 0.42f)
            : new Rect(0.52f, 0f, 0.48f, 0.42f);
        return new Rect(0.5f + (m_rightArea.x + m_rightArea.width * placement.x) * 0.5f,
            m_rightArea.y + m_rightArea.height * placement.y,
            m_rightArea.width * placement.width * 0.5f, m_rightArea.height * placement.height);
    }

    // Split an image at the gutter, retaining its original UVs on each captured page.
    private bool GetPageFragment(int slot, int part, bool right, out Rect placement, out Rect uv)
    {
        Rect full = GetPlacement(slot, part);
        float pageMin = right ? 0.5f : 0f;
        float left = Mathf.Max(full.xMin, pageMin), bottom = Mathf.Max(full.yMin, 0f);
        float rightEdge = Mathf.Min(full.xMax, pageMin + 0.5f), top = Mathf.Min(full.yMax, 1f);
        placement = uv = default;
        if (rightEdge <= left || top <= bottom || full.width <= 0f || full.height <= 0f) return false;
        placement = new Rect((left - pageMin) * 2f, bottom, (rightEdge - left) * 2f, top - bottom);
        uv = new Rect((left - full.xMin) / full.width, (bottom - full.yMin) / full.height,
            (rightEdge - left) / full.width, (top - bottom) / full.height);
        return true;
    }

    private void RefreshSurface(PageSurface surface)
    {
        if (surface.View == null) return;
        if (surface.Root.GetSiblingIndex() != surface.View.transform.childCount - 1)
            surface.Root.SetAsLastSibling();
        Rect pageRect = surface.Root.rect;
        for (int index = 0; index < surface.Graphics.Length; index++)
        {
            int slot = index < 4 ? index : 3;
            int part = index < 4 ? 0 : index - 3;
            var graphic = surface.Graphics[index];
            if (graphic == null) continue;
            var instance = surface.Page?.Obstacles[slot];
            bool fragment = GetPageFragment(slot, part, surface.Right, out Rect placement, out Rect uv);
            bool active = instance != null && fragment;
            bool changed = graphic.gameObject.activeSelf != active;
            graphic.gameObject.SetActive(active);
            if (!fragment) { if (changed) m_pendingCaptures.Add(surface.Camera); continue; }
            Vector2 size = new Vector2(pageRect.width * placement.width, pageRect.height * placement.height);
            Vector2 center = pageRect.min + new Vector2(
                pageRect.width * placement.center.x, pageRect.height * placement.center.y);
            Texture2D texture = instance != null ? m_settings.GetPartTexture(slot, part) : null;
            if (texture != null && !m_settings.HasStageImages)
            {
                float aspect = (float)texture.width / texture.height;
                if (size.x / size.y > aspect) size.x = size.y * aspect;
                else size.y = size.x / aspect;
            }
            RectTransform rect = graphic.rectTransform;
            Vector2 position = center - size * 0.5f - pageRect.min;
            changed |= rect.anchoredPosition != position || rect.sizeDelta != size;
            rect.anchorMin = rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            changed |= graphic.Bind(instance, uv, texture);
            if (changed) m_pendingCaptures.Add(surface.Camera);
        }
    }

    private void RefreshSurfaces()
    {
        foreach (var surface in m_surfaces) RefreshSurface(surface);
    }

    private void OnEndCameraRendering(ScriptableRenderContext context, Camera camera) => OnCameraRendered(camera);
    private void OnCameraRendered(Camera camera)
    {
        if (camera == null || m_book == null || !m_pendingCaptures.Remove(camera)) return;
        // Invalidate the composite only after the changed page pixels have reached their texture.
        m_book.FlipView.MarkDirty();
    }
    public void Receive(uint eventId, byte slotCount)
    {
        if (slotCount < 1 || slotCount > PageObstacleState.SLOT_COUNT || m_settings == null) return;
        // IDs are monotonically increasing within one server connection. No replay after reconnect.
        if (m_hasEvent && unchecked((int)(eventId - m_lastEvent)) <= 0) return;
        m_hasEvent = true;
        m_lastEvent = eventId;
        if (m_book == null || m_game == null) return;
        if (m_book.IsBusy || m_book.FlipView.IsTurning)
        {
            if (m_pending.Count < 32) m_pending.Enqueue(slotCount);
            else Debug.LogWarning("Obstacle queue full; request discarded.", this);
            return;
        }
        // Fixed pages, closed covers and inventory selection do not acquire deferred attacks.
        if (!Ready || m_game.CurrentPage == null) return;
        Apply(slotCount);
    }

    private void Apply(byte slotCount)
    {
        var page = m_game.CurrentPage;
        if (page == null) return;
        page.Obstacles.Apply(m_settings, slotCount);
        RefreshSurfaces();
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
        if (m_game == null || m_book == null) return;
        if (!ReferenceEquals(m_page, m_game.CurrentPage))
        {
            CancelPointer();
            m_page = m_game.CurrentPage;
            SynchronizeInput();
        }
        if (Ready)
            while (m_pending.Count > 0) Apply(m_pending.Dequeue());
        RefreshSurfaces();
        if (!Ready) { CancelPointer(); return; }
        for (int slot = 0; slot < 4; slot++) { m_areas[slot] = default; m_hitAreas[slot].Clear(); }
        foreach (var surface in m_surfaces)
        {
            if (surface.Live) continue;
            for (int index = 0; index < surface.Graphics.Length; index++)
            {
                int slot = index < 4 ? index : 3;
                var graphic = surface.Graphics[index];
                if (graphic == null || !graphic.gameObject.activeInHierarchy ||
                    !m_input.TryGetObstacleScreenRect(graphic.rectTransform, surface.Right, out Rect rect)) continue;
                var areas = m_hitAreas[slot];
                Rect combined = m_areas[slot];
                m_areas[slot] = areas.Count == 0 ? rect : Rect.MinMaxRect(Mathf.Min(combined.xMin, rect.xMin),
                    Mathf.Min(combined.yMin, rect.yMin), Mathf.Max(combined.xMax, rect.xMax), Mathf.Max(combined.yMax, rect.yMax));
                areas.Add(new HitArea { Screen = rect, UV = graphic.UVRect, Texture = graphic.mainTexture as Texture2D });
            }
        }
    }

    private bool IsInkAt(int slot, Vector2 screen)
    {
        foreach (var area in m_hitAreas[slot])
        {
            if (!area.Screen.Contains(screen) || area.Screen.width <= 0 || area.Screen.height <= 0) continue;
            Vector2 local = (screen - area.Screen.min) / area.Screen.size;
            Vector2 uv = area.UV.min + Vector2.Scale(local, area.UV.size);
            if (m_settings.ContainsInk(area.Texture, uv)) return true;
        }
        return false;
    }
    public bool BeginPointer(Vector2 screen)
    {
        if (!Ready || m_game.CurrentPage == null) return false;
        CancelPointer();
        for (int i = 3; i >= 0; i--)
        {
            var entry = m_game.CurrentPage.Obstacles[i];
            if (entry == null || !IsInkAt(i, screen)) continue;
            m_slot = i;
            m_pressed = entry;
            m_pointerInside = true;
            m_pointerScreen = screen;
            entry.Begin(Normalize(screen));
            FinishIfComplete();
            return true;
        }
        return false;
    }
    // Use one physical scale for both axes, so vertical and horizontal rubbing clean equally.
    private Vector2 Normalize(Vector2 screen) => (screen - m_areas[m_slot].position) /
        Mathf.Max(1f, Mathf.Min(m_areas[m_slot].width, m_areas[m_slot].height));
    public void MovePointer(Vector2 screen)
    {
        if (m_pressed == null || !Ready || m_game.CurrentPage == null ||
            !ReferenceEquals(m_game.CurrentPage.Obstacles[m_slot], m_pressed)) { CancelPointer(); return; }
        Vector2 previousScreen = m_pointerScreen;
        m_pointerScreen = screen;
        // Transparent areas act like leaving the patch: retain ownership, suspend cleaning.
        if (!IsInkAt(m_slot, screen))
        {
            m_pressed.Cancel();
            m_pointerInside = false;
            return;
        }
        if (!m_pointerInside)
        {
            m_pressed.Begin(Normalize(screen));
            m_pointerInside = true;
            return;
        }
        // Sample the swept path as well: opaque endpoints cannot count a transparent gap.
        int steps = Mathf.Clamp(Mathf.CeilToInt(Vector2.Distance(previousScreen, screen)), 1, 8192);
        for (int i = 1; i <= steps; i++)
        {
            Vector2 sample = Vector2.Lerp(previousScreen, screen, (float)i / steps);
            if (!IsInkAt(m_slot, sample))
            {
                m_pressed.Cancel();
                m_pointerInside = false;
            }
            else if (!m_pointerInside)
            {
                m_pressed.Begin(Normalize(sample));
                m_pointerInside = true;
            }
            else m_pressed.Move(Normalize(sample));
            FinishIfComplete();
            if (m_pressed == null) return;
        }
    }
    private void FinishIfComplete()
    {
        if (m_pressed == null || !m_pressed.Complete) return;
        m_game.CurrentPage.Obstacles.Clear(m_slot, m_pressed);
        RefreshSurfaces();
        CancelPointer();
        SynchronizeInput();
    }
    public void CancelPointer() { m_pressed?.Cancel(); m_pressed = null; m_slot = -1; m_pointerInside = false; }
    private void OnApplicationFocus(bool focused) { if (!focused) { CancelPointer(); m_input?.CancelObstaclePointer(); } }
    private void OnEnable()
    {
        RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
        Camera.onPostRender += OnCameraRendered;
        if (m_book != null) m_book.SetPageDecoration(BindPages);
    }
    private void OnDisable()
    {
        CancelPointer();
        RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
        Camera.onPostRender -= OnCameraRendered;
        foreach (var surface in m_surfaces)
            foreach (var graphic in surface.Graphics)
                if (graphic != null) graphic.gameObject.SetActive(false);
        if (m_book != null) m_book.SetPageDecoration(null);
    }
    private void OnDestroy()
    {
        foreach (var surface in m_surfaces)
            if (surface.Root != null) Destroy(surface.Root.gameObject);
    }
}
}
