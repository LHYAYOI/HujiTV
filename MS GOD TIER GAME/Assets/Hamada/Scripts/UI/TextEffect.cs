using TMPro;
using UnityEngine;

[CreateAssetMenu(fileName = "TextEffect", menuName = "Scriptable Objects/TextEffect")]
public abstract class TextEffect : ScriptableObject
{
    public abstract void Apply(TMP_TextInfo textInfo, int characterIndex, int localIndex, float elapsedTime);
    public abstract float GetDuration(int characterCount);
}
