//-----------------------------------------------
// SceneController.cs
// 制作日：2026/09/10
// 制作者：渡辺開斗
// 概要：シーン管理クラス
//-----------------------------------------------
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneController : MonoBehaviour
{
    public static SceneController Instance { get; private set; }

    public enum SCENE_TYPE
    {
        TITLE,
        GAME,
        RESULT,
        TUTORIAL
    }

    [Header("遷移管理")]
    [SerializeField] private TransitionManager m_transitionManager;

    private bool m_changingSceneFlag = false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void InitializeAutoLoad()
    {
        // インスタンスがまだ存在しない場合のみ生成
        if (Instance == null)
        {
            // Resourcesフォルダ内にあるSceneControllerプレハブを読み込む
            GameObject prefab = Resources.Load<GameObject>("SceneController");
            if (prefab != null)
            {
                Instantiate(prefab);
            }
        }
    }

    private void Awake()
    {
        // シングルトン
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // シーンをまたいでも維持
        DontDestroyOnLoad(gameObject);
    }

    //private void Start()
    //{
    //    // 最初のシーンをフェードイン
    //    SceneTransition transition = m_transitionManager.GetTransition(SceneTransition.TRANSITION_TYPE.FADE);

    //    if (transition != null)
    //    {
    //        StartCoroutine(transition.PlayIn());
    //    }
    //}

    // シーン遷移
    public void ChangeScene(SCENE_TYPE sceneType,SceneTransition.TRANSITION_TYPE transitionType = SceneTransition.TRANSITION_TYPE.FADE)
    {
        // 遷移中なら無視
        if (m_changingSceneFlag)
            return;

        StartCoroutine(ChangeSceneRoutine(sceneType,transitionType));
    }

    // シーン遷移処理
    private IEnumerator ChangeSceneRoutine(SCENE_TYPE sceneType, SceneTransition.TRANSITION_TYPE transitionType)
    {
        m_changingSceneFlag = true;

        // 遷移演出を取得
        SceneTransition transition = m_transitionManager.GetTransition(transitionType);

        if (transition == null)
        {
            m_changingSceneFlag = false;
            yield break;
        }

        // 現在のシーンを覆う演出
        yield return transition.PlayOut();

        // シーン変更
        SceneManager.LoadScene(GetSceneName(sceneType));

        // 新しいシーンで演出を解除
        yield return transition.PlayIn();

        m_changingSceneFlag = false;
    }

    // enumからシーン名に変換
    private string GetSceneName(SCENE_TYPE sceneType)
    {
        return sceneType switch
        {
            SCENE_TYPE.TITLE => "Title",
            SCENE_TYPE.GAME => "Game",
            SCENE_TYPE.RESULT => "Result",
            SCENE_TYPE.TUTORIAL => "Tutorial",

        };
    }
}
