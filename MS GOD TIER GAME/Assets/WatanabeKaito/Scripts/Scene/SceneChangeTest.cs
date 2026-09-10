using UnityEngine;

public class SceneChangeTest : MonoBehaviour
{

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            SceneController.Instance.ChangeScene(SceneController.SCENE_TYPE.GAME, SceneTransition.TRANSITION_TYPE.FADE);
        }
    }
}
