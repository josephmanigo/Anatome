using UnityEngine;

namespace ThoraxLab.Learning
{
    // The contact plane, drawn slit and scored path share the trainer's local coordinates.
    public sealed class CaseIncisionSurface : MonoBehaviour
    {
        const int Segments = 32, Rows = 6;
        readonly Vector3[] vertices = new Vector3[(Segments + 3) * Rows];
        Mesh mesh;
        float length;
        CaseIncisionStroke stroke;
        public float Progress => stroke.Progress;
        public float ContactSeconds => stroke.ContactSeconds;
        public bool Touching => stroke.Touching;
        public string Feedback => stroke.Feedback;
        public Quaternion WorkingRotation => Quaternion.LookRotation(transform.right, -transform.up);

        public void Build(Vector3 start, Vector3 end, Material skin, Material cut)
        {
            length = Vector3.Distance(start, end);
            stroke = new CaseIncisionStroke(length);
            transform.localPosition = (start + end) * .5f;
            transform.localRotation = Quaternion.FromToRotation(Vector3.right, end - start);
            mesh = new Mesh { name = "Opening in the skin training insert" };
            mesh.MarkDynamic();
            var surface = new System.Collections.Generic.List<int>();
            var interior = new System.Collections.Generic.List<int>();
            for (int col = 0; col < Segments + 2; col++)
            for (int row = 0; row < Rows - 1; row++)
            {
                int i = col * Rows + row;
                var triangles = row == 0 || row == Rows - 2 ? surface : interior;
                triangles.Add(i); triangles.Add(i + 1); triangles.Add(i + Rows);
                triangles.Add(i + Rows); triangles.Add(i + 1); triangles.Add(i + Rows + 1);
            }
            WriteSurface(0);
            var uv = new Vector2[vertices.Length]; var normals = new Vector3[uv.Length]; var tangents = new Vector4[uv.Length];
            for (int i = 0; i < uv.Length; i++)
            {
                int row = i % Rows;
                // The top charts use metres. Both exposed walls start at the same
                // superficial border (V=0) and reach the deep dermal mix at V=1.
                float v = row == 2 || row == 3 ? 1f : vertices[i].y * 16;
                uv[i] = new Vector2(vertices[i].x * 16, v);
                normals[i] = i % Rows == 2 ? new Vector3(0,.8f,-.6f) : i % Rows == 3 ? new Vector3(0,-.8f,-.6f) : Vector3.back;
                tangents[i] = new Vector4(1,0,0,row == 3 ? 1 : -1);
            }
            mesh.uv = uv; mesh.normals = normals; mesh.tangents = tangents;
            mesh.subMeshCount = 2;
            mesh.SetTriangles(surface, 0); mesh.SetTriangles(interior, 1);
            gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = new[] { skin, cut };
            // The thin overlay self-shadows into triangular bands at mobile shadow-map resolution.
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        void WriteSurface(float fraction)
        {
            ShapeInsert(vertices,length,fraction);
            mesh.vertices = vertices;
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(.112f, .04f, .010f));
        }

        static void ShapeInsert(Vector3[] points, float length, float fraction)
        {
            float half = length * .5f;
            for (int col = 0; col < Segments + 3; col++)
            {
                float t = (col - 1f) / Segments;
                float x = col == 0 ? -.055f : col == Segments + 2 ? .055f : Mathf.Lerp(-half, half, t);
                float gap = t > 0 && t < fraction ? .00215f * Mathf.Sqrt(fraction) * Mathf.Sin(Mathf.PI * t / fraction) : 0;
                int j = col * Rows;
                points[j] = new Vector3(x, -.019f, .001f);
                points[j + 1] = new Vector3(x, -gap, 0);
                points[j + 2] = new Vector3(x, -gap * .45f, .0035f);
                points[j + 3] = new Vector3(x, gap * .45f, .0035f);
                points[j + 4] = new Vector3(x, gap, 0);
                points[j + 5] = new Vector3(x, .019f, .001f);
            }
        }

        public void BreakContact() => stroke.BreakContact();

        public void Restore(bool completed)
        {
            float previous = Progress;
            stroke.Restore(completed);
            if (Progress != previous) WriteSurface(Progress);
        }

        public bool Sample(Vector3 tip, Quaternion blade, float maxAngle, float seconds)
        {
            Vector3 p = transform.InverseTransformPoint(tip);
            float previous = Progress;
            bool finished = stroke.Sample(p.x + length * .5f, p.y, p.z,
                Vector3.Angle(blade * Vector3.forward, transform.right) <= maxAngle,
                Vector3.Dot(blade * Vector3.right, transform.forward) >= .25f, seconds);
            if (Progress != previous) WriteSurface(Progress);
            return finished;
        }

        void OnDestroy() { WorkbenchGeometry.Release(mesh); }
    }
}
