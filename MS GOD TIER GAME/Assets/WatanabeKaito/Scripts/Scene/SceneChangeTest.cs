using UnityEngine;

public class SceneChangeTest : MonoBehaviour
{

    private void Start()
    {
        AudioManager.Instance.PlayBGM("Title_BGM");
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            AudioManager.Instance.PlaySE("SE");
        }
    }

    public void SETest()
    {
        AudioManager.Instance.PlaySE("SE");
    }
}
