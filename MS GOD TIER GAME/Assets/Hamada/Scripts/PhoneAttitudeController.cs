using UnityEngine;
using UnityEngine.InputSystem;

public class PhoneAttitudeController : MonoBehaviour
{
    private Quaternion m_centerAttitude;
    private bool m_initializeFlag = false;

    Vector2 m_normalizedAngles;

    private void Start()
    {
        Input.gyro.enabled = true;
    }

    private void Update()
    {
        CalculateNormalizedAngles();
    }

    //ƒŒƒeƒBƒNƒ‹—p‚É³‹K‰»‚³‚ê‚½ƒXƒ}ƒz‚ÌŠp“x‚ğŒvZ‚·‚é
    private void CalculateNormalizedAngles() 
    {
        // ƒWƒƒƒCƒ‚Ìp¨‚ğæ“¾
        Quaternion attitude = Input.gyro.attitude;

        // Å‰‚Ìp¨‚ğ’†‰›‚Æ‚µ‚Ä“o˜^
        if (!m_initializeFlag)
        {
            m_centerAttitude = attitude;
            m_initializeFlag = true;
            return;
        }

        // Šî€p¨‚©‚ç‚Ç‚ê‚¾‚¯‰ñ“]‚µ‚½‚©
        Quaternion delta = Quaternion.Inverse(m_centerAttitude) * attitude;

        // ‰ñ“]‚ğƒIƒCƒ‰[Šp‚É•ÏŠ·
        Vector3 euler = delta.eulerAngles;

        m_normalizedAngles.y = NormalizeAngle(euler.x);
        m_normalizedAngles.x = NormalizeAngle(euler.z);
    }

    public Vector2 GetNormalizedAngles()
    {
        return m_normalizedAngles;
    }

    //Šî€‚É‚·‚éŠp“x‚©‚çˆø”‚ÌŠp“x‚Ü‚Å‚ÌÅ’ZŠp‚ğ•Ô‚·
    private float NormalizeAngle(float angle)
    {
        return Mathf.DeltaAngle(0.0f, angle);
    }

    // ƒWƒƒƒCƒ‚ÌŠî€p¨‚ğŒ»İ‚Ìp¨‚ÉƒŠƒZƒbƒg‚·‚é
    public void Recenter()
    {
        m_centerAttitude = Input.gyro.attitude;
    }
}
