using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;

namespace LightShaftLab
{
    // One pair per camera. Never share reprojection data between Game and Scene views.
    internal sealed class ShaftHistory
    {
        public readonly RTHandle[] color = new RTHandle[2], depth = new RTHandle[2];
        public int index, frame, hash, width, height;
        public bool valid;
        public Matrix4x4 viewProjection, view, projection;
        public Vector3 position;
        public Quaternion rotation;
        public double time;
        public string reason = "Initial frame";
        public void Ensure(int w, int h)
        {
            if (width == w && height == h && color[0] != null) return;
            Dispose(); width = w; height = h;
            for (int i = 0; i < 2; i++)
            {
                color[i] = RTHandles.Alloc(w, h, colorFormat: GraphicsFormat.R16G16B16A16_SFloat, filterMode: FilterMode.Bilinear, name: "Shaft History Color");
                depth[i] = RTHandles.Alloc(w, h, colorFormat: GraphicsFormat.R32G32_SFloat, filterMode: FilterMode.Point, name: "Shaft History Depth");
            }
            reason = "Resolution changed";
        }
        public void Reset(string why) { valid = false; frame = 0; reason = why; }
        public void Dispose()
        {
            for (int i = 0; i < 2; i++) { color[i]?.Release(); depth[i]?.Release(); color[i] = null; depth[i] = null; }
            valid = false; frame = 0;
        }
    }
}
