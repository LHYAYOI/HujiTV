//
//カメラマネージャー
//濱田ルイス
//

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Cinemachine;

//シーンに使用するカメラの構造体
[Serializable]
public struct CAMERA_DATA
{
    [SerializeField] CinemachineCamera m_camera;
    [SerializeField] string m_name;

    public CinemachineCamera GetCamera() => m_camera;
    public string GetCameraName() => m_name;
}

public class CameraManager : MonoBehaviour
{
    [SerializeField] CinemachineBrain m_brain;
    [SerializeField] List<CAMERA_DATA> m_cameraList;

    CAMERA_DATA m_currentCamera;

    public static CameraManager Instance { get; private set; }

    private void Awake()
    {
        Instance = this;
    }

    public void SwitchCamera(string cameraName)
    {
        foreach (CAMERA_DATA camera in m_cameraList)
        {
            if (camera.GetCameraName() != cameraName) 
            {
                CinemachineCamera virtualCamera = camera.GetCamera();

                if (virtualCamera != null) 
                {
                    virtualCamera.Priority = 0;
                }

                continue;
            }

            //現在のカメラを切り替え
            ChangeCurrentCamera(camera);
        }
    }

    private void ChangeCurrentCamera(CAMERA_DATA camera)
    {
        CAMERA_DATA previousCamera = m_currentCamera;

        camera.GetCamera().Priority = 10;

        m_currentCamera = camera;
    }

    public IEnumerator ChangeCameraRoutine(string cameraName)
    {
        //カメラ切り替え呼び出し
        SwitchCamera(cameraName);

        float waitTime = 0.1f;

        //内部でのカメラの優先度切り替えが終わるまで待つ
        while (!IsCameraBlending() && waitTime > 0)
        {
            waitTime -= Time.deltaTime;

            yield return null;
        }

        //カメラの切り替えが終わるまで待つ
        while (IsCameraBlending())
        {
            yield return null;
        }
    }

    public void ShakeCamera(float intensity, float duration)
    {
        CinemachineCamera currentVirtualCamera = m_currentCamera.GetCamera();

        CameraShake shake = currentVirtualCamera.GetComponent<CameraShake>();

        if (shake == null)
        {
            return;
        }

        shake.ShakeCamera(intensity, duration);
    }

    public CAMERA_DATA GetCameraByName(string cameraName)
    {
        foreach (CAMERA_DATA camera in m_cameraList)
        {
            if (camera.GetCameraName() == cameraName)
            {
                return camera;
            }
        }

        return default(CAMERA_DATA);
    }

    public CAMERA_DATA GetCurrentCamera() => m_currentCamera;

    //ブレンド中かどうか
    public bool IsCameraBlending() => m_brain.IsBlending;
}
