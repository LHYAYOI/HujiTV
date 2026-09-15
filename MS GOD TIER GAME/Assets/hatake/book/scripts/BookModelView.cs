using System.Collections;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

public sealed class BookModelView : MonoBehaviour
{
    [SerializeField] private Animator m_animator;
    [SerializeField] private AnimationClip m_pageClip;
    [SerializeField] private Renderer m_bookRenderer;
    [SerializeField] private SkinnedMeshRenderer m_pageRenderer;
    [SerializeField] private Camera m_modelCamera;
    [SerializeField] private RenderTexture m_modelTexture;
    [SerializeField, Min(0.1f)] private float m_turnDuration = 0.65f;
    [Tooltip("通常はON。読み込んだClipが左から右ならOFFにします。")]
    [SerializeField] private bool m_forwardFlag = true;

    private PlayableGraph m_graph;
    private AnimationClipPlayable m_clipPlayable;
    private MaterialPropertyBlock m_bookProperties;
    private MaterialPropertyBlock m_pageProperties;

    public RenderTexture OutputTexture => m_modelTexture;

    public bool Initialize()
    {
        if (m_graph.IsValid()) return true;
        if (m_animator == null || m_pageClip == null ||
            m_bookRenderer == null || m_pageRenderer == null ||
            m_modelCamera == null || m_modelTexture == null)
        {
            Debug.LogError("BookModelViewの参照が不足しています。", this);
            return false;
        }
        if (m_pageClip.legacy || m_pageClip.length <= 0f)
        {
            Debug.LogError("長さのあるGeneric AnimationClipを指定してください。", this);
            return false;
        }
        m_animator.applyRootMotion = false;
        m_animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        m_graph = PlayableGraph.Create("BookPageAnimation");
        m_graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
        m_clipPlayable = AnimationClipPlayable.Create(m_graph, m_pageClip);
        m_clipPlayable.SetApplyFootIK(false);
        m_clipPlayable.SetSpeed(0);
        AnimationPlayableOutput output = AnimationPlayableOutput.Create(
            m_graph, "BookPage", m_animator);
        output.SetSourcePlayable(m_clipPlayable);
        m_graph.Play();
        m_bookProperties = new MaterialPropertyBlock();
        m_pageProperties = new MaterialPropertyBlock();
        m_pageRenderer.updateWhenOffscreen = true;
        m_pageRenderer.enabled = false;
        m_modelCamera.targetTexture = m_modelTexture;
        m_modelCamera.enabled = false;
        return true;
    }

    public void ShowSpread(Texture texture)
    {
        SetBookTextures(texture, texture);
        m_pageRenderer.enabled = false;
        m_modelCamera.enabled = true;
    }

    public IEnumerator PlayTurn(Texture currentTexture, Texture nextTexture, bool advanceFlag)
    {
        // Static left/right surfaces beneath the moving sheet.
        SetBookTextures(
            advanceFlag ? currentTexture : nextTexture,
            advanceFlag ? nextTexture : currentTexture);

        // Front is the physical right-hand face at the start of the source clip.
        m_pageProperties.SetTexture("_FrontTex", advanceFlag ? currentTexture : nextTexture);
        m_pageProperties.SetTexture("_BackTex", advanceFlag ? nextTexture : currentTexture);
        m_pageRenderer.SetPropertyBlock(m_pageProperties);

        bool playForwardFlag = advanceFlag == m_forwardFlag;
        SamplePose(playForwardFlag ? 0f : 1f);
        m_pageRenderer.enabled = true;
        m_modelCamera.enabled = true;

        float elapsedTime = 0f;
        float duration = Mathf.Max(0.1f, m_turnDuration);
        while (elapsedTime < duration)
        {
            float progress = Mathf.Clamp01(elapsedTime / duration);
            SamplePose(playForwardFlag ? progress : 1f - progress);
            yield return null;
            elapsedTime += Time.unscaledDeltaTime;
        }
        SamplePose(playForwardFlag ? 1f : 0f);
        ShowSpread(nextTexture);
    }

    public void StopDisplay()
    {
        if (m_pageRenderer != null) m_pageRenderer.enabled = false;
        if (m_modelCamera != null) m_modelCamera.enabled = false;
    }

    private void SamplePose(float progress)
    {
        double time = progress * Mathf.Max(0f, m_pageClip.length - 0.0001f);
        m_clipPlayable.SetTime(time);
        m_clipPlayable.SetDone(false);
        m_graph.Evaluate(0f);
    }

    private void SetBookTextures(Texture leftTexture, Texture rightTexture)
    {
        m_bookProperties.SetTexture("_LeftTex", leftTexture);
        m_bookProperties.SetTexture("_RightTex", rightTexture);
        m_bookRenderer.SetPropertyBlock(m_bookProperties);
    }

    private void OnDisable()
    {
        StopDisplay();
    }

    private void OnDestroy()
    {
        if (m_graph.IsValid()) m_graph.Destroy();
    }
}
