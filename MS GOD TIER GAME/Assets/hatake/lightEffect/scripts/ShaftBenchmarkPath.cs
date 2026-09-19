using UnityEngine;

namespace LightShaftLab
{
    public static class ShaftBenchmarkPath
    {
        // Closed path; integer frame indexing makes repeated runs reproducible.
        public static void Apply(Camera camera, int frame, int frameCount)
        {
            float phase = frame / (float)Mathf.Max(1, frameCount) * Mathf.PI * 2;
            camera.transform.position = new Vector3(Mathf.Sin(phase) * 7, 10 + Mathf.Cos(phase) * 2, -22 + Mathf.Sin(phase * 2) * 2);
            camera.transform.LookAt(new Vector3(0, 2, 8));
        }
    }
}
