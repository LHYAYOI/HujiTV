using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class ResolutionController : MonoBehaviour
{
    [SerializeField] private TMP_Dropdown resolutionDropdown;

    private List<Resolution> filteredResolutions = new List<Resolution>();

    private void Start()
    {
        // ディスプレイがサポートしているすべての解像度を取得
        Resolution[] resolutions = Screen.resolutions;

        resolutionDropdown.ClearOptions();
        List<string> options = new List<string>();

        int currentResolutionIndex = 0;

        for (int i = 0; i < resolutions.Length; i++)
        {
            // 同じ解像度（リフレッシュレート違い）の重複を排除して文字列を作成
            string option = resolutions[i].width + " x " + resolutions[i].height;

            if (!options.Contains(option))
            {
                options.Add(option);
                filteredResolutions.Add(resolutions[i]);

                // 現在の画面解像度と一致するインデックスを判定
                if (resolutions[i].width == Screen.width &&
                    resolutions[i].height == Screen.height)
                {
                    currentResolutionIndex = filteredResolutions.Count - 1;
                }
            }
        }

        // ドロップダウンに選択肢を追加し、現在の解像度を選択状態にする
        resolutionDropdown.AddOptions(options);
        resolutionDropdown.value = currentResolutionIndex;
        resolutionDropdown.RefreshShownValue();

        // 値が変更されたときのイベント登録
        resolutionDropdown.onValueChanged.AddListener(SetResolution);
    }

    public void SetResolution(int index)
    {
        Resolution resolution = filteredResolutions[index];
        // 現在の画面モード（フルスクリーン / ウィンドウ）を維持したまま解像度を変更
        Screen.SetResolution(resolution.width, resolution.height, Screen.fullScreenMode);
    }

    private void OnDestroy()
    {
        if (resolutionDropdown != null)
        {
            resolutionDropdown.onValueChanged.RemoveListener(SetResolution);
        }
    }
}