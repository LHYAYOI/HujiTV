using TMPro;
using UnityEngine;

public class ScreenModeController : MonoBehaviour
{
    [SerializeField] private TMP_Dropdown dropdown;

    private void Start()
    {
        // 起動時の画面モードを取得してドロップダウンの初期値を設定する
        // フルスクリーンの場合は0、ウィンドウモードの場合は1
        dropdown.value = Screen.fullScreen ? 0 : 1;

        // ドロップダウンの値が変更されたときに実行されるメソッドを登録
        dropdown.onValueChanged.AddListener(OnChangeScreenMode);
    }

    public void OnChangeScreenMode(int index)
    {
        switch (index)
        {
            case 0:
                // フルスクリーンモード
                Screen.fullScreenMode = FullScreenMode.FullScreenWindow;
                break;
            case 1:
                // ウィンドウモード
                Screen.fullScreenMode = FullScreenMode.Windowed;
                break;
        }
    }

    private void OnDestroy()
    {
        // オブジェクト破棄時にイベントリスナーを解除（メモリリーク防止）
        if (dropdown != null)
        {
            dropdown.onValueChanged.RemoveListener(OnChangeScreenMode);
        }
    }
}