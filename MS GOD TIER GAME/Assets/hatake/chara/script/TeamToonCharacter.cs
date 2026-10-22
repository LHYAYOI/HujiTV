using UnityEngine;

namespace TeamToon
{
    [ExecuteAlways, DisallowMultipleComponent, AddComponentMenu("Team Toon/顔の向きとキャラクター識別")]
    public sealed class TeamToonCharacter : MonoBehaviour
    {
        [Tooltip("顔の向きを取得する頭のTransform。未指定時はこのオブジェクト。")]
        public Transform head;
        [Tooltip("顔・髪・眉・瞳のRendererを指定します。")]
        public Renderer[] renderers = new Renderer[0];
        [Range(1, 255), Tooltip("前髪越し表示を使うキャラごとに別の番号を指定します。")]
        public int characterId = 1;
        public Vector3 localForward = Vector3.forward;
        public Vector3 localRight = Vector3.right;
        MaterialPropertyBlock block;
        void OnEnable() => Apply();
        void LateUpdate() => Apply();
        void OnValidate() => Apply();
        public void Apply()
        {
            var frame = head ? head : transform;
            Vector3 f = frame.TransformDirection(localForward.sqrMagnitude > 0.0001f ? localForward.normalized : Vector3.forward);
            Vector3 r = frame.TransformDirection(localRight.sqrMagnitude > 0.0001f ? localRight.normalized : Vector3.right);
            block ??= new MaterialPropertyBlock();
            if (renderers == null) return;
            foreach (var renderer in renderers)
            {
                if (!renderer) continue;
                renderer.GetPropertyBlock(block);
                block.SetVector("_FaceForward", f);
                block.SetVector("_FaceRight", r);
                block.SetFloat("_CharacterId", Mathf.Clamp(characterId, 1, 255));
                renderer.SetPropertyBlock(block);
            }
        }
        void OnDisable()
        {
            if (renderers == null) return;
            block ??= new MaterialPropertyBlock();
            foreach (var renderer in renderers)
            {
                if (!renderer) continue;
                // Preserve other components' overrides and restore the material's defaults.
                renderer.GetPropertyBlock(block);
                Material material = renderer.sharedMaterial;
                if (!TeamToonMaterial.IsSupported(material)) continue;
                block.SetVector("_FaceForward", material.GetVector("_FaceForward"));
                block.SetVector("_FaceRight", material.GetVector("_FaceRight"));
                block.SetFloat("_CharacterId", material.GetFloat("_CharacterId"));
                renderer.SetPropertyBlock(block);
            }
        }
    }
}
