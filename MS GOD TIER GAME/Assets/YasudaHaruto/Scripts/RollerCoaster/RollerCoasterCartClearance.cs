using UnityEngine;
namespace YasudaHaruto.RollerCoaster
{
    // 各車両の全Meshを包含するローカルOBB。視点ではなく車体全体を判定する。
    /// <summary>車体の向きと寸法を考慮して、遭遇時の車体間の余裕を計算する。</summary>
    public static class RollerCoasterCartClearance
    {

        public static Bounds Measure(Transform root)
        {
            bool found = false;
            var result = new Bounds();
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null)
                    continue;
                var b = filter.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var p = root.InverseTransformPoint(filter.transform.TransformPoint(corner));
                    if (!found)
                    {
                        result = new Bounds(p, Vector3.zero);
                        found = true;
                    }
                    else
                        result.Encapsulate(p);
                }
            }
            return found ? result : new Bounds(new Vector3(0, 1, 0), new Vector3(2, 2, 2.5f));
        }

        public static float Gap(Bounds a, Vector3 ap, Quaternion ar, Bounds b, Vector3 bp, Quaternion br)
        {
            Vector3 delta = bp + br * b.center - ap - ar * a.center;
            Vector3 ax = ar * Vector3.right, ay = ar * Vector3.up, az = ar * Vector3.forward;
            Vector3 bx = br * Vector3.right, by = br * Vector3.up, bz = br * Vector3.forward;
            float gap = float.NegativeInfinity;
            void Test(Vector3 axis)
            {
                if (axis.sqrMagnitude < 1e-8f)
                    return;
                axis.Normalize();
                float ra = Mathf.Abs(Vector3.Dot(axis, ax)) * a.extents.x + Mathf.Abs(Vector3.Dot(axis, ay)) * a.extents.y + Mathf.Abs(Vector3.Dot(axis, az)) * a.extents.z;
                float rb = Mathf.Abs(Vector3.Dot(axis, bx)) * b.extents.x + Mathf.Abs(Vector3.Dot(axis, by)) * b.extents.y + Mathf.Abs(Vector3.Dot(axis, bz)) * b.extents.z;
                gap = Mathf.Max(gap, Mathf.Abs(Vector3.Dot(delta, axis)) - ra - rb);
            }
            Test(ax);
            Test(ay);
            Test(az);
            Test(bx);
            Test(by);
            Test(bz);
            Test(Vector3.Cross(ax, bx));
            Test(Vector3.Cross(ax, by));
            Test(Vector3.Cross(ax, bz));
            Test(Vector3.Cross(ay, bx));
            Test(Vector3.Cross(ay, by));
            Test(Vector3.Cross(ay, bz));
            Test(Vector3.Cross(az, bx));
            Test(Vector3.Cross(az, by));
            Test(Vector3.Cross(az, bz));
            return gap;
        }
    }
}
