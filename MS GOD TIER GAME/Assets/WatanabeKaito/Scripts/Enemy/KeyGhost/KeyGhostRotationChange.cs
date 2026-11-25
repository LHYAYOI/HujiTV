using UnityEngine;
using UnityEngine.InputSystem;

public class KeyGhostRotationChange : MonoBehaviour
{
    private void Update()
    {
        if(Keyboard.current.spaceKey.wasPressedThisFrame)
        {
                       HarfRotateY();
        }
    }


    // YŽ²‚ð”¼‰ñ“]‚³‚¹‚é
    public void HarfRotateY()
    {
        Quaternion rotation = transform.rotation;

        rotation *= Quaternion.Euler(0, 180, 0);

        transform.rotation = rotation;
    }
}
