using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ThoraxLab.Learning
{
    // Decorative teaching models only. Geometry is assembled once, merged by material,
    // and has no colliders, interaction identities, update loop or clinical authority.
    internal sealed class WorkbenchGeometry : IDisposable
    {
        sealed class Surface
        {
            public readonly List<Vector3> vertices = new List<Vector3>();
            public readonly List<Vector3> normals = new List<Vector3>();
            public readonly List<Vector2> uv = new List<Vector2>();
            public readonly List<int> triangles = new List<int>();
        }
        readonly Dictionary<Material, Surface> surfaces = new Dictionary<Material, Surface>();
        readonly List<Mesh> meshes = new List<Mesh>();
        public int TriangleCount { get; private set; }
        public int RendererCount => meshes.Count;
        Surface For(Material material)
        {
            if (!surfaces.TryGetValue(material, out var surface)) { surface = new Surface(); surfaces.Add(material, surface); }
            return surface;
        }
        static void Quad(Surface surface, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal)
        {
            int at = surface.vertices.Count;
            surface.vertices.Add(a); surface.vertices.Add(b); surface.vertices.Add(c); surface.vertices.Add(d);
            for (int i = 0; i < 4; i++) surface.normals.Add(normal);
            surface.uv.Add(Vector2.zero); surface.uv.Add(Vector2.right); surface.uv.Add(Vector2.one); surface.uv.Add(Vector2.up);
            surface.triangles.Add(at); surface.triangles.Add(at + 1); surface.triangles.Add(at + 2);
            surface.triangles.Add(at); surface.triangles.Add(at + 2); surface.triangles.Add(at + 3);
        }
        public void Box(Vector3 center, Vector3 size, float radius, Material material, Quaternion rotation = default)
        {
            if (rotation == default) rotation = Quaternion.identity;
            // Rounded XY profile, shallow bevel on both faces. The corner radius belongs
            // to manufactured equipment; organic layers use their own contours below.
            var surface = For(material);
            radius = Mathf.Clamp(radius, .0001f, Mathf.Min(size.x, size.y) * .45f);
            float bevel = Mathf.Min(radius * .35f, size.z * .22f);
            const int count = 24;
            var loops = new Vector3[4][];
            for (int ring = 0; ring < 4; ring++)
            {
                loops[ring] = new Vector3[count];
                float inset = ring == 0 || ring == 3 ? bevel : 0;
                float z = ring == 0 ? -size.z / 2 : ring == 1 ? -size.z / 2 + bevel : ring == 2 ? size.z / 2 - bevel : size.z / 2;
                for (int i = 0; i < count; i++)
                {
                    int corner = i / 6;
                    float angle = (corner * 90 + (i % 6) * 18) * Mathf.Deg2Rad;
                    var sign = new Vector2(corner == 0 || corner == 3 ? 1 : -1, corner < 2 ? 1 : -1);
                    var point = new Vector3(sign.x * (size.x / 2 - radius) + Mathf.Cos(angle) * (radius - inset), sign.y * (size.y / 2 - radius) + Mathf.Sin(angle) * (radius - inset), z);
                    loops[ring][i] = center + rotation * point;
                }
            }
            for (int ring = 0; ring < 3; ring++) for (int i = 0; i < count; i++)
            {
                int next = (i + 1) % count;
                var a = loops[ring][i]; var b = loops[ring][next]; var c = loops[ring + 1][next]; var d = loops[ring + 1][i];
                Quad(surface, a, b, c, d, Vector3.Cross(b - a, d - a).normalized);
            }
            for (int i = 0; i < count; i++)
            {
                int next = (i + 1) % count;
                Quad(surface, center + rotation * new Vector3(0, 0, -size.z / 2), loops[0][next], loops[0][i], loops[0][i], rotation * Vector3.back);
                Quad(surface, center + rotation * new Vector3(0, 0, size.z / 2), loops[3][i], loops[3][next], loops[3][next], rotation * Vector3.forward);
            }
        }
        public void Ellipsoid(Vector3 center, Vector3 size, Material material, Quaternion rotation = default, int segments = 20, int rings = 12)
        {
            if (rotation == default) rotation = Quaternion.identity;
            var surface = For(material); int start = surface.vertices.Count;
            for (int row = 0; row <= rings; row++) for (int col = 0; col <= segments; col++)
            {
                float v = (float)row / rings; float u = (float)col / segments;
                float latitude = v * Mathf.PI, longitude = u * 2 * Mathf.PI;
                var p = new Vector3(Mathf.Sin(latitude) * Mathf.Cos(longitude), Mathf.Cos(latitude), Mathf.Sin(latitude) * Mathf.Sin(longitude));
                surface.vertices.Add(center + rotation * Vector3.Scale(p, size * .5f));
                surface.normals.Add(rotation * new Vector3(p.x / size.x, p.y / size.y, p.z / size.z).normalized); surface.uv.Add(new Vector2(u, v));
            }
            for (int row = 0; row < rings; row++) for (int col = 0; col < segments; col++)
            {
                int a = start + row * (segments + 1) + col; int b = a + segments + 1;
                surface.triangles.Add(a); surface.triangles.Add(a + 1); surface.triangles.Add(b);
                surface.triangles.Add(a + 1); surface.triangles.Add(b + 1); surface.triangles.Add(b);
            }
        }
        public void Tube(IReadOnlyList<Vector3> points, float radius, Material material, int sides = 10, float arc = 360, bool inner = false, IReadOnlyList<float> radii = null)
        {
            if (points.Count < 2) return;
            var surface = For(material); int start = surface.vertices.Count;
            for (int i = 0; i < points.Count; i++)
            {
                var tangent = (points[Mathf.Min(i + 1, points.Count - 1)] - points[Mathf.Max(i - 1, 0)]).normalized;
                // The camera-facing cut is along -Z on a station, so a half tube is an
                // honest open lumen rather than an opaque rod with a "channel" label.
                var side = Vector3.Cross(Vector3.forward, tangent).normalized;
                if (side.sqrMagnitude < .01f) side = Vector3.right;
                var outward = Vector3.Cross(tangent, side).normalized;
                for (int j = 0; j <= sides; j++)
                {
                    float a = (float)j / sides * arc * Mathf.Deg2Rad;
                    var normal = side * Mathf.Cos(a) + outward * Mathf.Sin(a);
                    surface.vertices.Add(points[i] + normal * (radii == null ? radius : radii[i])); surface.normals.Add(inner ? -normal : normal); surface.uv.Add(new Vector2((float)j / sides, (float)i / (points.Count - 1)));
                }
            }
            for (int i = 0; i < points.Count - 1; i++) for (int j = 0; j < sides; j++)
            {
                int a = start + i * (sides + 1) + j; int b = a + sides + 1;
                surface.triangles.Add(a); surface.triangles.Add(inner ? b : a + 1); surface.triangles.Add(inner ? a + 1 : b);
                surface.triangles.Add(a + 1); surface.triangles.Add(inner ? b : b + 1); surface.triangles.Add(inner ? b + 1 : b);
            }
        }
        public void Ring(Vector3 center, Vector3 normal, float radius, float wire, Material material, int segments = 28)
        {
            var rotation = Quaternion.FromToRotation(Vector3.forward, normal);
            var path = new Vector3[segments + 1];
            for (int i = 0; i <= segments; i++) { float a = i * 2 * Mathf.PI / segments; path[i] = center + rotation * new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0) * radius; }
            Tube(path, wire, material, 6);
        }
        public void TissueBand(float centerX, float front, float back, Material material, float width = .085f)
        {
            var surface = For(material);
            // Irregular cut edges are continuous, not disconnected rectangular slices.
            const int count = 18;
            var leftFront = new Vector3[count + 1]; var rightFront = new Vector3[count + 1];
            var leftBack = new Vector3[count + 1]; var rightBack = new Vector3[count + 1];
            for (int i = 0; i <= count; i++)
            {
                float y = -.11f + .22f * i / count;
                float curve = .005f * Mathf.Cos(y * 13) + .0012f * Mathf.Sin(y * 83 + front * 55);
                float edge = .002f * Mathf.Sin(y * 39) + .001f * Mathf.Cos(y * 101);
                leftFront[i] = new Vector3(centerX - width / 2 + edge, y, front + curve);
                rightFront[i] = new Vector3(centerX + width / 2 + edge, y, front + curve);
                leftBack[i] = new Vector3(leftFront[i].x, y, back + curve); rightBack[i] = new Vector3(rightFront[i].x, y, back + curve);
            }
            for (int i = 0; i < count; i++)
            {
                Quad(surface, leftFront[i], leftFront[i + 1], rightFront[i + 1], rightFront[i], Vector3.back);
                Quad(surface, leftBack[i], rightBack[i], rightBack[i + 1], leftBack[i + 1], Vector3.forward);
                Quad(surface, leftFront[i], leftBack[i], leftBack[i + 1], leftFront[i + 1], Vector3.left);
                Quad(surface, rightFront[i], rightFront[i + 1], rightBack[i + 1], rightBack[i], Vector3.right);
            }
            Quad(surface, leftFront[0], rightFront[0], rightBack[0], leftBack[0], Vector3.down);
            Quad(surface, leftFront[count], leftBack[count], rightBack[count], rightFront[count], Vector3.up);
        }
        public void Build(Transform parent, string title)
        {
            foreach (var pair in surfaces)
            {
                var source = pair.Value;
                var mesh = new Mesh { name = title + " / " + pair.Key.name, indexFormat = source.vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
                mesh.SetVertices(source.vertices); mesh.SetNormals(source.normals); mesh.SetUVs(0, source.uv); mesh.SetTriangles(source.triangles, 0); mesh.RecalculateBounds();
                mesh.UploadMeshData(true); meshes.Add(mesh); TriangleCount += source.triangles.Count / 3;
                var go = new GameObject(mesh.name); go.transform.SetParent(parent, false); go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterial = pair.Key; renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = true;
            }
            surfaces.Clear();
        }
        public void Dispose() { foreach (var mesh in meshes) if (mesh) UnityEngine.Object.Destroy(mesh); meshes.Clear(); }
    }
}
