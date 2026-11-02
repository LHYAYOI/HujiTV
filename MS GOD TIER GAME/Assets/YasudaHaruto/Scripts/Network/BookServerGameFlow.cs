using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(BookNetworkServer))]
public sealed class BookServerGameFlow : MonoBehaviour
{
    [SerializeField] private string m_gameScenePath = "Assets/YasudaHaruto/Scenes/BookServerGame.unity";
    private BookNetworkServer m_server;
    public bool IsStarting { get; private set; }
    public string Status { get; private set; }
    public bool CanStart => !IsStarting && m_server != null && m_server.IsListening &&
        !m_server.IsGameStarted && (m_server.IsPlayerConnected(PLAYER_SLOT.PLAYER1) ||
                                  m_server.IsPlayerConnected(PLAYER_SLOT.PLAYER2));

    private void Awake() { m_server = GetComponent<BookNetworkServer>(); }

    public void StartGame()
    {
        if (!CanStart) return;
        if (!CanLoadGameScene())
        {
            Status = "Add BookServerGame to the PC Build Profile scene list.";
            return;
        }
        IsStarting = true;
        Status = "Loading game...";
        StartCoroutine(LoadGame());
    }

    private bool CanLoadGameScene()
    {
        if (Application.CanStreamedLevelBeLoaded(m_gameScenePath)) return true;
#if UNITY_EDITOR
        return UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.SceneAsset>(m_gameScenePath) != null;
#else
        return false;
#endif
    }

    private IEnumerator LoadGame()
    {
        AsyncOperation operation = null;
        try
        {
#if UNITY_EDITOR
            if (!Application.CanStreamedLevelBeLoaded(m_gameScenePath))
                operation = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                    m_gameScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            else
#endif
                operation = SceneManager.LoadSceneAsync(m_gameScenePath, LoadSceneMode.Single);
        }
        catch (System.Exception e) { Status = "Game scene load failed: " + e.Message; }
        if (operation == null) { IsStarting = false; yield break; }
        yield return operation;
        // Allow scene Start methods to finish before enabling tablet gameplay.
        yield return null;
        m_server.BeginGame();
        IsStarting = false;
        Status = "Game running";
    }
}
