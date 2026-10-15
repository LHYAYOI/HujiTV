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

    private void Update()
    {
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
                    virtualCamera.gameObject.SetActive(false);
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

        CinemachineCamera virtualCamera = camera.GetCamera();

        virtualCamera.gameObject.SetActive(true);
        virtualCamera.Priority = 10;

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

    //縦と横の強さを指定できるカメラシェイク
    public void ShakeCameraInVH(float duration, float horizontalIntensity, float verticalIntensity) 
    {
        CinemachineCamera currentVirtualCamera = m_currentCamera.GetCamera();

        if(currentVirtualCamera == null)
        {
            return;
        }

        CameraShake shake = currentVirtualCamera.GetComponent<CameraShake>();

        if (shake == null)
        {
            return;
        }

        shake.VHShake(duration, horizontalIntensity, verticalIntensity);
    }

    //カメラシェイク
    public void ShakeCamera(float duration, float intensity)
    {
        CinemachineCamera currentVirtualCamera = m_currentCamera.GetCamera();

        if (currentVirtualCamera == null)
        {
            return;
        }

        CameraShake shake = currentVirtualCamera.GetComponent<CameraShake>();

        if (shake == null)
        {
            return;
        }

        shake.ShakeCamera(intensity, duration);
    }

    //トロッコのシェイク調整
    public void SetRateOnTrolleyShake(float rate) 
    {
        CinemachineCamera currentVirtualCamera = m_currentCamera.GetCamera();

        if (currentVirtualCamera == null)
        {
            return;
        }

        CameraShake shake = currentVirtualCamera.GetComponent<CameraShake>();

        if (shake == null)
        {
            return;
        }

        shake.SetRateOnTrolleyShake(rate);
    }

    //Z軸を傾ける
    public void TiltCamera(float duration, float tiltDegree)
    {
        CinemachineCamera currentVirtualCamera = m_currentCamera.GetCamera();

        if (currentVirtualCamera == null)
        {
            return;
        }

        CameraTilt tilt = currentVirtualCamera.GetComponent<CameraTilt>();

        if (tilt == null)
        {
            return;
        }

        tilt.Tilt(duration, tiltDegree);
    }

    //カメラのFOVを変更する
    public void ChangeCameraFOV(float duration, float FOV)
    {
        CinemachineCamera currentVirtualCamera = m_currentCamera.GetCamera();

        if (currentVirtualCamera == null)
        {
            return;
        }

        CameraZoom zoom = currentVirtualCamera.GetComponent<CameraZoom>();

        if (zoom == null)
        {
            return;
        }

        zoom.ChangeFOV(duration, FOV);
    }

    public void SetLookTarget(GameObject lookTarget) 
    {
        CinemachineCamera targetCamera = GetCameraByName("BattleCamera").GetCamera();

        if (targetCamera == null) 
        {
            return;
        }

        CameraLookTargetChanger lookTargetChanger = targetCamera.GetComponent<CameraLookTargetChanger>();

        if (lookTargetChanger == null) 
        {
            return;
        }

        lookTargetChanger.SetLookTarget(lookTarget);
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
