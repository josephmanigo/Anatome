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
        // One continuous molded surface joins the shoulders, neck, jaw and face.
        static readonly Vector4[] headProfile = {
            new(-.141f,.097f,.046f,.045f), new(-.125f,.094f,.047f,.048f),
            new(-.104f,.062f,.037f,.044f), new(-.080f,.034f,.030f,.035f),
            new(-.051f,.031f,.032f,.037f), new(-.034f,.038f,.052f,.039f),
            new(-.018f,.049f,.069f,.046f), new(.005f,.060f,.067f,.055f),
            new(.037f,.069f,.061f,.063f), new(.070f,.072f,.059f,.069f),
            new(.100f,.071f,.060f,.067f), new(.128f,.059f,.053f,.055f),
            new(.149f,.038f,.035f,.036f), new(.160f,.001f,.001f,.001f)
        };
        static float Feature(float x, float y, float cx, float cy, float sx, float sy)
            => Mathf.Exp(-((x-cx)*(x-cx)/(sx*sx)+(y-cy)*(y-cy)/(sy*sy)));
        internal static Vector3 ManikinPoint(float y, float angle)
        {
            int at=0; while(at<headProfile.Length-2 && y>headProfile[at+1].x)at++;
            var a=headProfile[at];var b=headProfile[at+1];float t=Mathf.Clamp01((y-a.x)/(b.x-a.x));
            var previous=headProfile[Mathf.Max(0,at-1)];var next=headProfile[Mathf.Min(headProfile.Length-1,at+2)];
            var m0=(b-previous)/(b.x-previous.x)*(b.x-a.x);var m1=(next-a)/(next.x-a.x)*(b.x-a.x);
            var p=(2*t*t*t-3*t*t+1)*a+(t*t*t-2*t*t+t)*m0+(-2*t*t*t+3*t*t)*b+(t*t*t-t*t)*m1;
            if(at==headProfile.Length-2)
            {
                float dome=Mathf.Sqrt(Mathf.Max(.00001f,1-t));p.y=a.y*dome;p.z=a.z*dome;p.w=a.w*dome;
            }
            float x=Mathf.Sin(angle)*p.y, facing=Mathf.Cos(angle);
            float z=.043f+facing*(facing<0?p.z:p.w);
            if(facing<0)
            {
                float detail=.012f*Feature(x,y,0,.069f,.011f,.030f)+.021f*Feature(x,y,0,.047f,.015f,.013f);
                detail+=.007f*Feature(x,y,0,.018f,.024f,.018f)+.004f*Feature(x,y,0,-.017f,.025f,.014f);
                for(int side=-1;side<=1;side+=2)
                {
                    detail+=.006f*Feature(x,y,side*.038f,.044f,.024f,.022f);
                    detail+=.0025f*Feature(x,y,side*.030f,.091f,.024f,.009f);
                    detail-=.0035f*Feature(x,y,side*.029f,.077f,.020f,.011f);
                    detail+=.005f*Feature(x,y,side*.012f,.044f,.009f,.007f);
                }
                z-=detail*Mathf.Pow(-facing,4);
            }
            return new Vector3(.10f+x,y,z);
        }
        internal static Vector3 FacePoint(float x,float y)
        {
            float width=ManikinPoint(y,Mathf.PI*.5f).x-.10f;
            return ManikinPoint(y,Mathf.PI-Mathf.Asin(Mathf.Clamp(x/width,-1,1)));
        }
        public void AirwayManikin(Material material)
        {
            var surface=For(material);int start=surface.vertices.Count;
            const int rows=100,columns=96;
            for(int row=0;row<=rows;row++)for(int col=0;col<=columns;col++)
            {
                float y=Mathf.Lerp(-.141f,.160f,Mathf.Sin(row*Mathf.PI/(rows*2))),angle=col*Mathf.PI*2/columns;
                var p=ManikinPoint(y,angle);
                var dy=ManikinPoint(Mathf.Min(.160f,y+.0001f),angle)-ManikinPoint(Mathf.Max(-.141f,y-.0001f),angle);
                var da=ManikinPoint(y,angle+.001f)-ManikinPoint(y,angle-.001f);
                // Keep finite differences above Unity's normalization epsilon at tabletop scale.
                surface.vertices.Add(p);surface.normals.Add(Vector3.Cross((da*10000).normalized,(dy*10000).normalized).normalized);surface.uv.Add(new Vector2((float)col/columns,(float)row/rows));
            }
            for(int row=0;row<rows;row++)for(int col=0;col<columns;col++)
            {
                int a=start+row*(columns+1)+col,b=a+columns+1;
                surface.triangles.Add(a);surface.triangles.Add(a+1);surface.triangles.Add(b);
                surface.triangles.Add(a+1);surface.triangles.Add(b+1);surface.triangles.Add(b);
            }
        }
        public void ManikinMouth(Material recess,Material lip)
        {
            var surface=For(recess);var upper=new Vector3[33];var lower=new Vector3[33];
            for(int i=0;i<upper.Length;i++)
            {
                float t=(float)i/(upper.Length-1),x=Mathf.Lerp(-.020f,.020f,t),height=.0025f*Mathf.Sin(t*Mathf.PI);
                upper[i]=FacePoint(x,.012f+height)+Vector3.back*.001f;
                lower[i]=FacePoint(x,.012f-height)+Vector3.back*.001f;
                if(i>0)Quad(surface,lower[i-1],upper[i-1],upper[i],lower[i],Vector3.back);
            }
            Tube(upper,.0009f,lip,8);Tube(lower,.0012f,lip,8);
        }
        public void ManikinEar(int side,Material material)
        {
            var surface=For(material);int start=surface.vertices.Count;
            const int rings=16,segments=48;
            Vector3 Point(float r,float angle)
            {
                float rim=Mathf.Exp(-Mathf.Pow((r-.78f)/.16f,2));
                float fold=.002f*Mathf.Sin(angle*2)*Mathf.Sin(r*Mathf.PI);
                return new Vector3(.10f+side*(.068f+.009f*(1-r*r)+.004f*rim+fold),.059f+.022f*r*Mathf.Sin(angle),.041f+.014f*r*Mathf.Cos(angle));
            }
            for(int row=0;row<=rings;row++)for(int col=0;col<=segments;col++)
            {
                float r=Mathf.Max(.0001f,(float)row/rings),a=col*Mathf.PI*2/segments;
                var dr=Point(r+.001f,a)-Point(r-.001f,a);var da=Point(r,a+.001f)-Point(r,a-.001f);
                surface.vertices.Add(Point(r,a));surface.normals.Add(row==0?Vector3.right*side:Vector3.Cross((dr*10000).normalized,(da*10000).normalized).normalized*-side);
                surface.uv.Add(new Vector2(.5f+r*Mathf.Cos(a)*.5f,.5f+r*Mathf.Sin(a)*.5f));
            }
            for(int row=0;row<rings;row++)for(int col=0;col<segments;col++)
            {
                int a=start+row*(segments+1)+col,b=a+segments+1;
                surface.triangles.Add(a);surface.triangles.Add(side>0?a+1:b);surface.triangles.Add(side>0?b:a+1);
                surface.triangles.Add(a+1);surface.triangles.Add(side>0?b+1:b);surface.triangles.Add(side>0?b:b+1);
            }
        }
        public void TissueBand(float centerX, float front, float back, Material material, float width = .085f, Material cutFace = null)
        {
            var surface = For(material);
            var section = cutFace ? For(cutFace) : surface;
            // A gently convex skin envelope with rounded corners, shared by every layer.
            const int count = 32;
            var leftFront = new Vector3[count + 1]; var rightFront = new Vector3[count + 1];
            var leftBack = new Vector3[count + 1]; var rightBack = new Vector3[count + 1];
            for (int i = 0; i <= count; i++)
            {
                float y = .11f * Mathf.Sin(((float)i / count - .5f) * Mathf.PI);
                float curve = .005f * Mathf.Cos(y * 13);
                float corner=Mathf.Max(0,Mathf.Abs(y)-.099f);
                float inset=.011f-Mathf.Sqrt(Mathf.Max(0,.011f*.011f-corner*corner));
                leftFront[i] = new Vector3(centerX - width / 2 + inset, y, front + curve);
                rightFront[i] = new Vector3(centerX + width / 2 - inset, y, front + curve);
                leftBack[i] = new Vector3(leftFront[i].x, y, back + curve); rightBack[i] = new Vector3(rightFront[i].x, y, back + curve);
            }
            const int columns=8;
            for(int face=0;face<2;face++)
            {
                var side = face == 0 ? surface : section;
                int start=side.vertices.Count;
                for(int row=0;row<=count;row++)for(int col=0;col<=columns;col++)
                {
                    float u=(float)col/columns;
                    var point=Vector3.Lerp(face==0?leftFront[row]:leftBack[row],face==0?rightFront[row]:rightBack[row],u);
                    point.z-=.0035f*Mathf.Sin(u*Mathf.PI);
                    var normal=new Vector3(-.0035f*Mathf.PI*Mathf.Cos(u*Mathf.PI)/(rightFront[row].x-leftFront[row].x),-.065f*Mathf.Sin(point.y*13),-1).normalized;
                    side.vertices.Add(point);side.normals.Add(face==0?normal:-normal);side.uv.Add(new Vector2(u,(float)row/count));
                }
                for(int row=0;row<count;row++)for(int col=0;col<columns;col++)
                {
                    int a=start+row*(columns+1)+col,b=a+columns+1;
                    side.triangles.Add(a);side.triangles.Add(face==0?b:a+1);side.triangles.Add(face==0?a+1:b);
                    side.triangles.Add(a+1);side.triangles.Add(face==0?b:b+1);side.triangles.Add(face==0?b+1:b);
                }
            }
            for (int i = 0; i < count; i++)
            {
                Quad(section, leftFront[i], leftBack[i], leftBack[i + 1], leftFront[i + 1], Vector3.left);
                Quad(section, rightFront[i], rightFront[i + 1], rightBack[i + 1], rightBack[i], Vector3.right);
            }
            for(int col=0;col<columns;col++)for(int end=0;end<=1;end++)
            {
                int row=end*count;float u=(float)col/columns,v=(float)(col+1)/columns;
                Vector3 Edge(Vector3 a,Vector3 b,float t)=>Vector3.Lerp(a,b,t)-Vector3.forward*(.0035f*Mathf.Sin(t*Mathf.PI));
                var a=Edge(leftFront[row],rightFront[row],u);var b=Edge(leftFront[row],rightFront[row],v);
                var c=Edge(leftBack[row],rightBack[row],v);var d=Edge(leftBack[row],rightBack[row],u);
                if(end==0)Quad(section,a,b,c,d,Vector3.down);else Quad(section,a,d,c,b,Vector3.up);
            }
        }
        public void Build(Transform parent, string title, params Material[] metreMapped)
        {
            foreach (var pair in surfaces)
            {
                var source = pair.Value;
                var mesh = new Mesh { name = title + " / " + pair.Key.name, indexFormat = source.vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
                if (Array.IndexOf(metreMapped, pair.Key) >= 0)
                    for (int i = 0; i < source.vertices.Count; i++)
                    {
                        var p = source.vertices[i]; var n = source.normals[i];
                        if (Mathf.Abs(n.z) >= Mathf.Max(Mathf.Abs(n.x), Mathf.Abs(n.y))) source.uv[i] = new Vector2(p.x, p.y);
                        else if (Mathf.Abs(n.y) >= Mathf.Abs(n.x)) source.uv[i] = new Vector2(p.x, p.z);
                        else source.uv[i] = new Vector2(p.z, p.y);
                    }
                mesh.SetVertices(source.vertices); mesh.SetNormals(source.normals); mesh.SetUVs(0, source.uv); mesh.SetTriangles(source.triangles, 0); mesh.RecalculateBounds();
                if (pair.Key.IsKeywordEnabled("_NORMALMAP")) mesh.RecalculateTangents();
                mesh.UploadMeshData(true); meshes.Add(mesh); TriangleCount += source.triangles.Count / 3;
                var go = new GameObject(mesh.name); go.transform.SetParent(parent, false); go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterial = pair.Key; renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = true;
            }
            surfaces.Clear();
        }
        internal static void Release(UnityEngine.Object generated)
        {
            if(!generated)return;
            if(Application.isPlaying)UnityEngine.Object.Destroy(generated);else UnityEngine.Object.DestroyImmediate(generated);
        }
        public void Dispose() { foreach (var mesh in meshes) Release(mesh); meshes.Clear(); }
    }
}
