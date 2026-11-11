using UnityEngine;
using UnityEngine.Rendering;

namespace YasudaHaruto.BookValidation
{
    // The book caches its composite independently of the UI capture texture.
    // Notify it after a changed rune graphic has actually been captured.
    public sealed class BookRuneCaptureRefresh : MonoBehaviour
    {
        private RunePageView m_view;
        private YasudaBookViewController m_book;
        private RuneLineGraphic[] m_graphics;
        private bool m_capturePending;

        public void Configure(RunePageView view, YasudaBookViewController book)
        {
            UnbindGraphics();
            m_view = view;
            m_book = book;
            if (isActiveAndEnabled) BindGraphics();
        }

        private void OnEnable()
        {
            RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
            Camera.onPostRender += HandleCameraRendered;
            BindGraphics();
        }

        private void OnDisable()
        {
            RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
            Camera.onPostRender -= HandleCameraRendered;
            UnbindGraphics();
        }

        private void BindGraphics()
        {
            if (m_view == null || m_book == null || m_graphics != null) return;
            m_graphics = m_view.GetComponentsInChildren<RuneLineGraphic>(true);
            foreach (RuneLineGraphic graphic in m_graphics)
            {
                graphic.RegisterDirtyVerticesCallback(OnGraphicChanged);
                graphic.RegisterDirtyMaterialCallback(OnGraphicChanged);
            }
            m_capturePending = true;
        }

        private void UnbindGraphics()
        {
            if (m_graphics != null)
            {
                foreach (RuneLineGraphic graphic in m_graphics)
                {
                    if (graphic == null) continue;
                    graphic.UnregisterDirtyVerticesCallback(OnGraphicChanged);
                    graphic.UnregisterDirtyMaterialCallback(OnGraphicChanged);
                }
            }
            m_graphics = null;
            m_capturePending = false;
        }

        private void OnGraphicChanged() => m_capturePending = true;

        private void OnEndCameraRendering(ScriptableRenderContext context, Camera camera)
            => HandleCameraRendered(camera);

        private void HandleCameraRendered(Camera camera)
        {
            if (!m_capturePending || m_book == null || !m_book.IsReady || m_book.IsClosed ||
                camera == null || camera.targetTexture == null ||
                camera.targetTexture != m_book.CurrentTexture) return;

            m_book.FlipView.MarkDirty();
            m_capturePending = false;
        }
    }
}
