using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ThoraxLab.Editor
{
    // Builds ordinary editable meshes, materials and prefabs. No authoring work runs on the headset.
    public static class MuseumGalleryAssets
    {
        const string Art = "Assets/ThoraxLab/Museum/";
        const string Prefabs = "Assets/ThoraxLab/Resources/Learning/Museum/";
        enum Finish { Stone, Plaster, Oak, Teal, Bronze, Lens, Contact }
        static readonly Vector3 X = Vector3.right, Y = Vector3.up, Z = Vector3.forward;
        static Vector3 P(float x, float y, float z) => new Vector3(x,y,z);

        [MenuItem("Anatome/Art/Rebuild museum architecture")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Rebuild in Edit mode.");
            if (!AssetDatabase.IsValidFolder(Prefabs.TrimEnd('/')))
                AssetDatabase.CreateFolder("Assets/ThoraxLab/Resources/Learning", "Museum");
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(Art + "MuseumFinish.shader");
            if (!shader || ShaderUtil.ShaderHasError(shader)) throw new InvalidOperationException("Museum finish shader failed to import.");
            var stone = Texture("Honed terrazzo");
            var plaster = Texture("Mineral plaster");
            var oak = Texture("Quarter sawn oak");
            var finishes = new[] {
                Material("Honed terrazzo", shader, stone, "FFFFFF", .13f),
                Material("Lime plaster", shader, plaster, "FFFFFF", .025f),
                Material("Oiled oak", shader, oak, "FFFFFF", .09f),
                Material("Patinated green", shader, plaster, "386164", .09f),
                Material("Satin bronze", shader, null, "B29669", .22f),
                Material("Opal diffuser", shader, null, "FFF0D5", 0),
                Material("Plinth contact", shader, null, "FFFFFF", 0)
            };
            var contact = finishes[(int)Finish.Contact];
            contact.SetFloat("_SrcBlend", (float)BlendMode.DstColor);
            contact.SetFloat("_DstBlend", (float)BlendMode.Zero);
            contact.SetFloat("_ZWrite", 0);
            contact.SetOverrideTag("RenderType", "Transparent");
            contact.renderQueue = (int)RenderQueue.Transparent - 10;
            BuildShell().Save("Gallery", finishes);
            BuildPlinth().Save("Plinth", finishes);
            AssetDatabase.SaveAssets();
        }

        static Texture2D Texture(string name)
        {
            var path = Art + name + ".png";
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (!importer) throw new InvalidOperationException("Missing museum map: " + path);
            importer.sRGBTexture = true; importer.mipmapEnabled = true; importer.isReadable = false;
            importer.alphaSource = TextureImporterAlphaSource.FromInput; importer.alphaIsTransparency = false;
            importer.wrapMode = TextureWrapMode.Repeat; importer.filterMode = FilterMode.Trilinear; importer.anisoLevel = 4;
            var android = importer.GetPlatformTextureSettings("Android");
            android.overridden = true; android.maxTextureSize = 512; android.format = TextureImporterFormat.ASTC_6x6;
            importer.SetPlatformTextureSettings(android); importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static Material Material(string name, Shader shader, Texture2D map, string html, float sheen)
        {
            string path = Art + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
            ColorUtility.TryParseHtmlString("#" + html, out var color);
            material.shader = shader; material.SetColor("_BaseColor", color);
            material.SetTexture("_BaseMap", map); material.SetFloat("_Sheen", sheen);
            material.enableInstancing = true; EditorUtility.SetDirty(material);
            return material;
        }

        static Geometry BuildShell()
        {
            var g = new Geometry(false);
            g.Grid(P(-3.94f,0,-2.44f), X*7.88f, Z*8.88f, Y, Finish.Stone, 20, 22);
            g.Grid(P(-3.94f,3.94f,-2.44f), X*7.88f, Z*8.88f, -Y, Finish.Plaster, 12, 14);
            Wall(g, P(-3.94f,0,-2.44f), Z, X, 8.88f);
            Wall(g, P(3.94f,0,-2.44f), Z, -X, 8.88f);
            Wall(g, P(-3.94f,0,6.44f), X, -Z, 7.88f);
            Wall(g, P(-3.94f,0,-2.44f), X, Z, 7.88f);
            foreach (float side in new[]{-1f,1f})
            {
                var inward = X * -side;
                foreach(float z in new[]{-.88f,2.0f,4.88f})
                {
                    var origin=P(side*3.91f,0,z);
                    Arch(g,origin,Z,inward,.98f,1.99f,.94f,true);
                }
                g.Box(P(side*1.72f,3.79f,2),P(.25f,.18f,8.68f),Finish.Oak);
                g.Box(P(side*1.72f,3.682f,2),P(.132f,.040f,8.46f),Finish.Bronze);
                g.Box(P(side*1.72f,3.656f,2),P(.082f,.016f,8.34f),Finish.Lens);
                g.Grid(P(side*3.32f,.004f,-1.91f), X*.018f, Z*7.84f, Y, Finish.Bronze);
                for (int i=0;i<9;i++)
                    g.Box(P(side*(2.49f+i*.141f),1.89f,6.315f),P(.076f,2.97f,.13f),Finish.Oak);
            }
            foreach(float z in new[]{-1.94f,5.92f})
                g.Grid(P(-3.32f,.004f,z),X*6.65f,Z*.018f,Y,Finish.Bronze);
            foreach(float z in new[]{-1.72f,.24f,2.2f,4.16f,6.12f})
                g.Box(P(0,3.86f,z),P(7.85f,.15f,.105f),Finish.Plaster);
            Arch(g,P(0,0,6.35f),X,-Z,2.36f,2.30f,1.17f,false);
            g.Box(P(0,.58f,6.23f),P(3.93f,.033f,.07f),Finish.Bronze);
            return g;
        }

        static void Wall(Geometry g, Vector3 origin, Vector3 along, Vector3 inward, float width)
        {
            g.Grid(origin,along*width,Y*3.94f,inward,Finish.Plaster,16,8);
            var center = origin + along*width*.5f;
            g.Box(center + Y*.14f + inward*.025f, along*width + Y*.28f + inward*.065f,Finish.Teal);
            g.Box(center + Y*.285f + inward*.046f, along*width + Y*.024f + inward*.048f,Finish.Bronze);
            g.Box(center + Y*3.60f + inward*.045f, along*width + Y*.14f + inward*.14f,Finish.Oak);
            g.Box(center + Y*3.68f + inward*.085f, along*width + Y*.02f + inward*.04f,Finish.Lens);
        }

        static void Arch(Geometry g, Vector3 origin, Vector3 side, Vector3 inward, float radius, float spring, float rise, bool slats)
        {
            const float bottom=.36f, border=.052f;
            var mid = origin + Y*spring;
            g.Grid(origin-side*radius+Y*bottom,side*(2*radius),Y*(spring-bottom),inward,Finish.Teal,8,4);
            int segments = slats ? 24 : 48;
            for(int i=0;i<segments;i++)
            {
                float a=Mathf.PI*i/segments, b=Mathf.PI*(i+1)/segments;
                var rimA = mid+side*(radius*Mathf.Cos(a))+Y*(rise*Mathf.Sin(a));
                var rimB = mid+side*(radius*Mathf.Cos(b))+Y*(rise*Mathf.Sin(b));
                var innerA = rimA-side*(border*Mathf.Cos(a))-Y*(border*Mathf.Sin(a));
                var innerB = rimB-side*(border*Mathf.Cos(b))-Y*(border*Mathf.Sin(b));
                g.Triangle(mid,rimA,rimB,inward,Finish.Teal);
                g.Quad(rimA+inward*.103f,rimB+inward*.103f,innerB+inward*.103f,innerA+inward*.103f,inward,Finish.Bronze);
                var towardCenter=mid-(innerA+innerB)*.5f;
                g.Quad(innerA,innerB,innerB+inward*.103f,innerA+inward*.103f,towardCenter,Finish.Oak);
                g.Quad(rimA,rimB,rimB+inward*.103f,rimA+inward*.103f,-towardCenter,Finish.Bronze);
            }
            foreach(float sign in new[]{-1f,1f})
                g.Box(origin+side*(sign*(radius-border/2))+Y*((spring+bottom)/2)+inward*.051f,side*border+Y*(spring-bottom)+inward*.104f,Finish.Bronze);
            if(!slats) return;
            for(int i=-7;i<=7;i++)
            {
                float x=i*.117f;
                float height=spring+rise*Mathf.Sqrt(1-Mathf.Pow((Mathf.Abs(x)+.029f)/(radius-border),2))-bottom-.11f;
                g.Box(origin+side*x+Y*(bottom+height/2)+inward*.034f,side*.059f+Y*height+inward*.066f,Finish.Oak);
            }
        }

        static Geometry BuildPlinth()
        {
            var g=new Geometry(true);
            float[] levels={0,.035f,.08f,.125f,.14f,.158f,.85f,.872f,.895f,.955f,.976f,1};
            float[] widths={.324f,.35f,.35f,.383f,.383f,.359f,.375f,.391f,.39f,.414f,.414f,.398f};
            Finish[] sides={Finish.Teal,Finish.Teal,Finish.Teal,Finish.Bronze,Finish.Teal,Finish.Oak,Finish.Oak,Finish.Bronze,Finish.Teal,Finish.Bronze,Finish.Teal};
            Vector3[] previous=null;
            for(int j=0;j<levels.Length;j++)
            {
                var ring=new List<Vector3>();
                for(int corner=0;corner<4;corner++)
                {
                    float mid=(corner*90+45)*Mathf.Deg2Rad;
                    float cx=Mathf.Sign(Mathf.Cos(mid))*(widths[j]-.078f);
                    float cz=Mathf.Sign(Mathf.Sin(mid))*(widths[j]-.083f-.078f);
                    for(int k=0;k<=4;k++)
                    {
                        float a=(corner*90+k*22.5f)*Mathf.Deg2Rad;
                        ring.Add(P(cx+.078f*Mathf.Cos(a),levels[j],cz+.078f*Mathf.Sin(a)));
                    }
                }
                var current=ring.ToArray();
                for(int i=0;i<current.Length;i++)
                {
                    int next=(i+1)%current.Length;
                    if(previous!=null) g.Quad(previous[i],previous[next],current[next],current[i],Vector3.ProjectOnPlane(current[i]+current[next],Y),sides[j-1]);
                    if(j==levels.Length-1) g.Triangle(Y,current[i],current[next],Y,Finish.Teal);
                }
                previous=current;
            }
            // A small multiplicative contact skirt leaves the real floor grain visible beneath occupied stands.
            float[] apronWidths={.318f,.36f,.415f,.49f,.57f};
            for(int j=0;j<apronWidths.Length;j++)
            {
                var loop=new Vector3[32];
                for(int i=0;i<loop.Length;i++)
                {
                    float a=2*Mathf.PI*i/loop.Length;
                    float cos=Mathf.Cos(a), sin=Mathf.Sin(a), w=apronWidths[j];
                    loop[i]=P(w*Mathf.Sign(cos)*Mathf.Pow(Mathf.Abs(cos),.47f),.003f,(w-.073f)*Mathf.Sign(sin)*Mathf.Pow(Mathf.Abs(sin),.47f));
                }
                if(j>0) for(int i=0;i<loop.Length;i++)
                {
                    int next=(i+1)%loop.Length;
                    g.Quad(previous[i],previous[next],loop[next],loop[i],Y,Finish.Contact);
                }
                previous=loop;
            }
            return g;
        }

        sealed class Geometry
        {
            readonly bool stand;
            Vector3 timberAxis;
            static readonly Color enamelTint = new Color32(47, 82, 81, 255);
            static readonly Color bronzeTint = new Color32(178, 150, 105, 255);
            readonly List<Vector3> vertices=new List<Vector3>(), normals=new List<Vector3>();
            readonly List<Color32> colors=new List<Color32>();
            readonly List<Vector2> uv=new List<Vector2>();
            readonly List<int>[] indices=Enum.GetValues(typeof(Finish)).Cast<Finish>().Select(f=>new List<int>()).ToArray();
            internal Geometry(bool stand) { this.stand=stand; }
            // Vertex tint keeps all opaque parts of a stand in the same oak material submission.
            List<int> Indices(Finish f) => indices[(int)(stand && f != Finish.Contact ? Finish.Oak : f)];

            internal void Grid(Vector3 origin, Vector3 across, Vector3 up, Vector3 normal, Finish f, int columns=1, int rows=1)
            {
                var dx=across/columns; var dy=up/rows;
                for(int x=0;x<columns;x++) for(int y=0;y<rows;y++)
                {
                    var a=origin+dx*x+dy*y;
                    Quad(a,a+dx,a+dx+dy,a+dy,normal,f);
                }
            }
            internal void Box(Vector3 center, Vector3 size, Finish f)
            {
                var dx=X*Mathf.Abs(size.x); var dy=Y*Mathf.Abs(size.y); var dz=Z*Mathf.Abs(size.z);
                timberAxis=f==Finish.Oak && Mathf.Max(dx.x,dz.z)>dy.y*2 ? (dx.x>dz.z?X:Z) : Vector3.zero;
                var a=center-(dx+dy+dz)*.5f;
                Grid(a,dx,dy,-Z,f); Grid(a+dz,dx,dy,Z,f);
                Grid(a,dz,dy,-X,f); Grid(a+dx,dz,dy,X,f);
                Grid(a,dx,dz,-Y,f); Grid(a+dy,dx,dz,Y,f);
                timberAxis=Vector3.zero;
            }
            internal void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 facing, Finish f)
            {
                var n=Vector3.Cross(b-a,c-a).normalized;
                bool flip=Vector3.Dot(n,facing)<0; if(flip)n=-n;
                int i=vertices.Count;
                Vertex(a,n,f); Vertex(b,n,f); Vertex(c,n,f); Vertex(d,n,f);
                var list=Indices(f);
                list.Add(i);list.Add(i+(flip?2:1));list.Add(i+(flip?1:2));
                list.Add(i);list.Add(i+(flip?3:2));list.Add(i+(flip?2:3));
            }
            internal void Triangle(Vector3 a, Vector3 b, Vector3 c, Vector3 facing, Finish f)
            {
                var n=Vector3.Cross(b-a,c-a).normalized; bool flip=Vector3.Dot(n,facing)<0; if(flip)n=-n;
                int i=vertices.Count;Vertex(a,n,f);Vertex(b,n,f);Vertex(c,n,f);
                var list=Indices(f);list.Add(i);list.Add(i+(flip?2:1));list.Add(i+(flip?1:2));
            }

            void Vertex(Vector3 p, Vector3 n, Finish f)
            {
                vertices.Add(p); normals.Add(n);
                bool flat=Mathf.Abs(n.y)>.70f;
                var coords=flat ? new Vector2(p.x,p.z) : new Vector2(Mathf.Abs(n.x)>Mathf.Abs(n.z)?p.z:p.x,p.y);
                if(timberAxis!=Vector3.zero)
                    coords=new Vector2(flat?(timberAxis==X?p.z:p.x):p.y,Vector3.Dot(p,timberAxis));
                uv.Add(coords/(f==Finish.Oak?.79f:1.05f));
                if(f==Finish.Contact)
                {
                    float distance=Mathf.Pow(Mathf.Pow(Mathf.Abs(p.x),4.25f)+Mathf.Pow(Mathf.Abs(p.z)*1.21f,4.25f),1/4.25f);
                    float shadeAtFoot=Mathf.Lerp(.34f,1,Mathf.SmoothStep(0,1,Mathf.InverseLerp(.31f,.54f,distance)));
                    colors.Add(new Color(shadeAtFoot,shadeAtFoot,shadeAtFoot,1));return;
                }
                // Interior diffuse is authored in room space, independent of the learner's entry heading.
                float shade;
                if(stand) shade=.42f+.18f*Mathf.Clamp01(p.y*3)+.14f*Mathf.Max(0,n.y)+.11f*Mathf.Abs(n.x)+.06f*Mathf.Max(0,-n.z);
                else if(n.y>.7f) {
                    float edge=Mathf.Min(3.94f-Mathf.Abs(p.x),Mathf.Min(p.z+2.44f,6.44f-p.z));
                    shade=.47f+.21f*Mathf.Clamp01(edge)+.12f*Mathf.Exp(-.5f*Mathf.Pow(Mathf.Abs(p.x)-1.72f,2));
                } else {
                    shade=.46f+.17f*Mathf.Clamp01(p.y/3.4f)+.1f*Mathf.Abs(n.x)+.13f*Mathf.Max(0,n.z);
                    if(Mathf.Abs(p.x)>3.6f) {
                        float bay=.5f+.5f*Mathf.Cos((p.z-2)*Mathf.PI/1.44f);
                        shade+=.17f*bay*Mathf.Exp(-Mathf.Pow((p.y-2.65f)*.85f,2))-.05f*(1-bay);
                    }
                }
                Color lighting = f==Finish.Lens ? Color.white : new Color(shade*1.04f,shade,shade*.935f,1);
                if (stand && (f==Finish.Teal || f==Finish.Bronze))
                {
                    var tint=f==Finish.Teal ? enamelTint : bronzeTint;
                    lighting *= QualitySettings.activeColorSpace==ColorSpace.Linear ? tint.linear : tint;
                    lighting.a=0;
                }
                colors.Add(lighting);
            }

            internal void Save(string name, Material[] palette)
            {
                string meshPath=Art+name+" mesh.asset";
                var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                if(!mesh){mesh=new Mesh { name=name+" mesh" };AssetDatabase.CreateAsset(mesh,meshPath);}
                mesh.Clear();mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetColors(colors);mesh.SetUVs(0,uv);
                var used=Enumerable.Range(0,indices.Length).Where(i=>indices[i].Count!=0).ToArray();
                mesh.subMeshCount=used.Length;
                for(int i=0;i<used.Length;i++)mesh.SetTriangles(indices[used[i]],i,false);
                mesh.RecalculateBounds(); EditorUtility.SetDirty(mesh);
                var root=new GameObject(name);
                try {
                    root.AddComponent<MeshFilter>().sharedMesh=mesh;
                    var renderer=root.AddComponent<MeshRenderer>();renderer.sharedMaterials=used.Select(i=>palette[i]).ToArray();
                    renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
                    renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
                    if(stand) Collider(root,P(0,.5f,0),P(.8f,1,.65f));
                    else {
                        Collider(root,P(0,-.06f,2),P(8,.12f,9)); Collider(root,P(0,4,2),P(8,.12f,9));
                        foreach(float x in new[]{-4f,4f}) Collider(root,P(x,2,2),P(.12f,4,9));
                        foreach(float z in new[]{-2.5f,6.5f}) Collider(root,P(0,2,z),P(8,4,.12f));
                        foreach(var position in new[]{P(-1.05f,1.88f,.30f),P(.85f,2.75f,3.75f)}) {
                            var light=new GameObject("Exhibit wash").AddComponent<Light>();
                            light.transform.SetParent(root.transform,false);light.transform.localPosition=position;
                            light.type=LightType.Point;light.color=new Color(1,.98f,.95f);light.intensity=3.6f;light.range=5.6f;light.shadows=LightShadows.None;
                        }
                    }
                    PrefabUtility.SaveAsPrefabAsset(root,Prefabs+name+".prefab");
                } finally { UnityEngine.Object.DestroyImmediate(root); }
            }
            static void Collider(GameObject root, Vector3 center, Vector3 size)
            {
                var collider=root.AddComponent<BoxCollider>(); collider.center=center; collider.size=size;
            }
        }
    }
}
