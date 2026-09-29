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
            SceneController.Instance.ChangeScene(SceneController.SCENE_TYPE.TITLE, SceneTransition.TRANSITION_TYPE.IRIS);
        }
    }

    public void SETest()
    {
        AudioManager.Instance.PlaySE("SE");
    }
}
