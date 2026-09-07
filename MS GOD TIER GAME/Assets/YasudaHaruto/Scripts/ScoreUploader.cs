using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

public class ScoreUploader : MonoBehaviour
{
    [SerializeField]
    private string supabaseUrl = "https://mlzzduyeeptfmmaflgkj.supabase.co";

    [SerializeField]
    private string publishableKey = "sb_publishable_Hev5d_eUSXb0Kjd-wX-Stg_aYnB7Oq9";

    [System.Serializable]
    private class ScoreData
    {
        public string playerName;
        public long score;
    }

    private void Start()
    {
        // Example usage
        SubmitScore("Player2", 2000);
    }

    public void SubmitScore(string playerName, long score)
    {
        StartCoroutine(SubmitScoreCoroutine(playerName, score));
    }

    private IEnumerator SubmitScoreCoroutine(string name, long score)
    {
        ScoreData scoreData = new ScoreData
        {
            playerName = name,
            score = score
        };

        string json = JsonUtility.ToJson(scoreData);

        string url = $"{supabaseUrl}/rest/v1/scores";

        using UnityWebRequest request = new UnityWebRequest(
            url,
            UnityWebRequest.kHttpVerbPOST
        );

        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);

        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();

        request.SetRequestHeader("Content-Type", "application/json; charset=utf-8");
        request.SetRequestHeader("apikey", publishableKey);
        request.SetRequestHeader("Prefer", "return=minimal");

        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            Debug.Log($"送信JSON: {json}");
            Debug.Log("スコア送信成功");
        }
        else
        {
            Debug.LogError(
                $"スコア送信失敗: {request.responseCode}\n{request.error}\n{request.downloadHandler.text}"
            );
        }
    }
}