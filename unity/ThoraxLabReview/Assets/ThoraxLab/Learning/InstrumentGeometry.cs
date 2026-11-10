using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ThoraxLab.Learning
{
    /// <summary>
    /// Brand-neutral instrument presentation. Geometry is authored in the existing tool's
    /// local metre coordinates; this class never creates colliders or working-end markers.
    /// Meshes are cached by instrument and detail is merged by surface, not one renderer
    /// per graduation, tooth or grip groove. The six shared finishes serve the whole kit.
    /// </summary>
    public static class InstrumentGeometry
    {
        const int Steel=0, Satin=1, Ivory=2, Teal=3, Ink=4, Clear=5;
        static readonly Dictionary<string,Mesh> meshes=new Dictionary<string,Mesh>();
        static Material[] materials;
        public static readonly string[] SupportedIds={"scalpel","suture","inhaler","spacer","oxygen","cap","dressing","ultrasound","probe","swab","connector","anaesthetic","needle","aspirator","medication","guidewire","dilator","drain","balloon","stent","valve","dissection-scalpel","dissection-saw"};

        public static GameObject Create(Transform parent,string toolId)
        {
            if(!meshes.TryGetValue(toolId,out var mesh))
            {
                var geometry=new Builder();
                switch(toolId)
                {
                    case "scalpel": Scalpel(geometry,false);break;
                    case "dissection-scalpel": Scalpel(geometry,true);break;
                    case "dissection-saw": Saw(geometry);break;
                    case "suture": NeedleHolder(geometry);break;
                    case "inhaler": Inhaler(geometry);break;
                    case "spacer": Spacer(geometry);break;
                    case "oxygen": OxygenMask(geometry);break;
                    case "cap": Cap(geometry);break;
                    case "dressing": Dressing(geometry);break;
                    case "ultrasound": Sensor(geometry,true);break;
                    case "probe": Sensor(geometry,false);break;
                    case "swab": Swab(geometry);break;
                    case "connector": Connector(geometry);break;
                    case "anaesthetic": case "needle": case "aspirator": case "medication": Syringe(geometry,toolId);break;
                    default: Catheter(geometry,toolId);break;
                }
                mesh=geometry.Finish("Detailed "+toolId);meshes.Add(toolId,mesh);
            }
            var go=new GameObject("Crafted "+toolId+" / visual only");go.transform.SetParent(parent,false);
            go.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterials=GetMaterials();
            renderer.shadowCastingMode=ShadowCastingMode.On;renderer.receiveShadows=true;
            return go;
        }

        static Material[] GetMaterials()
        {
            if(materials!=null)return materials;
            Material Finish(string name,Color color,float metal,float smooth)
            {
                var material=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name};
                material.SetColor("_BaseColor",color);material.SetFloat("_Metallic",metal);material.SetFloat("_Smoothness",smooth);
                material.enableInstancing=true;return material;
            }
            materials=new[]{
                Finish("Instrument / polished cutting steel",new Color(.76f,.81f,.83f),.74f,.72f),
                Finish("Instrument / satin stainless",new Color(.57f,.64f,.66f),.60f,.43f),
                Finish("Instrument / warm medical polymer",new Color(.86f,.89f,.86f),.02f,.34f),
                Finish("Instrument / deep teal polymer",new Color(.075f,.31f,.32f),.06f,.38f),
                Finish("Instrument / graphite elastomer",new Color(.028f,.044f,.049f),.02f,.24f),
                Finish("Instrument / translucent polymer",new Color(.68f,.88f,.89f,.34f),.02f,.70f)
            };
            // The referenced socket asset ensures this URP transparent variant is included
            // in release builds. No refraction, screen-copy pass or transparent shadow map.
            var transparent=Resources.Load<Material>("Learning/GuidanceSocket");
            if(transparent)
            {
                if(Application.isPlaying)UnityEngine.Object.Destroy(materials[Clear]);else UnityEngine.Object.DestroyImmediate(materials[Clear]);
                materials[Clear]=new Material(transparent){name="Instrument / translucent polymer"};
                materials[Clear].SetColor("_BaseColor",new Color(.68f,.88f,.89f,.34f));materials[Clear].SetFloat("_Smoothness",.70f);
                materials[Clear].SetFloat("_Metallic",.02f);materials[Clear].enableInstancing=true;
            }
            return materials;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCache()
        {
            foreach(var mesh in meshes.Values)if(mesh)UnityEngine.Object.Destroy(mesh);meshes.Clear();
            if(materials!=null)foreach(var material in materials)if(material)UnityEngine.Object.Destroy(material);materials=null;
        }

        static void Scalpel(Builder b,bool dissection)
        {
            // A flattened, waisted handle with a distinct tang and curved cutting edge.
            float start=dissection?-.005f:-.047f;
            Quaternion rotation=dissection?Quaternion.Euler(0,0,90):Quaternion.identity;
            b.Push(Vector3.zero,rotation);
            b.RoundedBody(Satin,new Vector3(0,0,(start+.063f)/2),new Vector3(.012f,.006f,.063f-start),.002f);
            b.Lathe(Steel,new[]{new Vector3(.0048f,.0021f,.058f),new Vector3(.0034f,.0016f,.070f),new Vector3(.003f,.0014f,.076f)},16);
            for(int i=0;i<12;i++)
            {
                float z=Mathf.Lerp(start+.012f,.045f,i/11f);
                b.RoundBar(Ink,new Vector3(-.0045f,.00305f,z),new Vector3(.0045f,.00305f,z),.00022f,5);
                b.RoundBar(Ink,new Vector3(-.0045f,-.00305f,z),new Vector3(.0045f,-.00305f,z),.00022f,5);
            }
            b.Polygon(Steel,new[]{new Vector2(-.003f,.067f),new Vector2(.004f,.069f),new Vector2(.0064f,.083f),new Vector2(.0052f,.094f),new Vector2(.0018f,.103f),new Vector2(0,.108f),new Vector2(-.003f,.083f)},.00070f);
            b.Polygon(Satin,new[]{new Vector2(-.0024f,.068f),new Vector2(.003f,.070f),new Vector2(.0045f,.082f),new Vector2(.0032f,.092f),new Vector2(-.0024f,.082f)},.00074f);
            b.RoundedBody(Ink,new Vector3(0,.00041f,.076f),new Vector3(.0018f,.00010f,.011f),.0006f);
            // Recessed end panel and fine ruler ticks are decorative, without dose values.
            b.RoundedBody(Steel,new Vector3(0,.00305f,start+.009f),new Vector3(.0065f,.00016f,.007f),.001f);
            b.Pop();
        }

        static void Saw(Builder b)
        {
            b.RoundedBody(Ink,new Vector3(0,-.008f,.026f),new Vector3(.027f,.034f,.074f),.007f);
            b.RoundedBody(Teal,new Vector3(0,-.008f,.025f),new Vector3(.028f,.029f,.054f),.006f);
            for(int i=0;i<7;i++)b.RoundedBody(Ink,new Vector3(0,-.008f,.006f+i*.007f),new Vector3(.0286f,.030f,.0018f),.0058f);
            b.RoundedBody(Satin,new Vector3(0,0,.070f),new Vector3(.034f,.024f,.035f),.006f);
            b.RoundedBody(Steel,new Vector3(0,0,.082f),new Vector3(.025f,.019f,.008f),.003f);
            b.RoundedBody(Ink,new Vector3(0,.016f,.041f),new Vector3(.013f,.003f,.020f),.003f);
            b.Push(Vector3.zero,Quaternion.Euler(0,0,90));
            b.Polygon(Steel,new[]{new Vector2(-.008f,.081f),new Vector2(.008f,.081f),new Vector2(.0132f,.125f),new Vector2(-.0132f,.125f)},.0016f);
            for(int i=0;i<13;i++)
            {
                float x=-.013f+i*.002f;
                b.Polygon(Steel,new[]{new Vector2(x,.1248f),new Vector2(x+.001f,.129f),new Vector2(x+.0019f,.1248f)},.0016f);
            }
            for(int i=0;i<3;i++)b.RoundedBody(Ink,new Vector3(0,.00086f,.091f+i*.006f),new Vector3(.008f,.00012f,.0018f),.0005f);
            b.Pop();
            b.RoundBar(Steel,new Vector3(-.0172f,0,.068f),new Vector3(-.0169f,0,.068f),.0026f,12);
            b.RoundBar(Ink,new Vector3(-.01735f,-.0017f,.068f),new Vector3(-.01735f,.0017f,.068f),.00026f,5);
        }

        static void NeedleHolder(Builder b)
        {
            // Ring handles, box joint, fine opposed jaws and a curved needle are separate
            // recognizable forms, merged into the common mesh rather than cube stand-ins.
            b.Loop(Satin,new Vector3(-.010f,0,-.032f),.007f,.012f,.0017f,24);
            b.Loop(Satin,new Vector3(.010f,0,-.032f),.007f,.012f,.0017f,24);
            b.RoundBar(Satin,new Vector3(-.005f,0,-.022f),new Vector3(.002f,0,.054f),.0021f,10);
            b.RoundBar(Satin,new Vector3(.005f,0,-.022f),new Vector3(-.002f,0,.054f),.0021f,10);
            b.RoundedBody(Satin,new Vector3(0,0,.056f),new Vector3(.009f,.005f,.016f),.0018f);
            b.RoundBar(Steel,new Vector3(0,-.003f,.056f),new Vector3(0,.003f,.056f),.0021f,12);
            b.Polygon(Steel,new[]{new Vector2(-.0038f,.063f),new Vector2(-.0005f,.063f),new Vector2(-.0005f,.101f),new Vector2(-.002f,.096f)},.0026f);
            b.Polygon(Steel,new[]{new Vector2(.0005f,.063f),new Vector2(.0038f,.063f),new Vector2(.002f,.096f),new Vector2(.0005f,.101f)},.0026f);
            var needle=new List<Vector3>();for(int i=0;i<=16;i++){float t=Mathf.Lerp(-.7f,2.1f,i/16f);needle.Add(new Vector3(.006f*Mathf.Cos(t)-.003f,.0016f,.102f+.006f*Mathf.Sin(t)));}
            b.Tube(Steel,needle,.00040f,7);
            b.Tube(Ink,new[]{needle[0],new Vector3(.008f,.002f,.09f),new Vector3(.012f,.001f,.066f),new Vector3(.009f,0,.053f)},.00018f,5);
            for(int i=0;i<5;i++)b.RoundBar(Ink,new Vector3(-.004f,.0027f,-.009f+i*.004f),new Vector3(.004f,.0027f,-.009f+i*.004f),.00018f,5);
        }

        static void Syringe(Builder b,string id)
        {
            b.Lathe(Clear,new[]{new Vector3(.0124f,.0124f,-.013f),new Vector3(.013f,.013f,-.011f),new Vector3(.013f,.013f,.058f),new Vector3(.0115f,.0115f,.063f),new Vector3(.0048f,.0048f,.067f),new Vector3(.0038f,.0038f,.067f),new Vector3(.0106f,.0106f,.061f),new Vector3(.0116f,.0116f,.057f),new Vector3(.0116f,.0116f,-.013f)},24,false);
            b.RoundedBody(Ivory,new Vector3(0,0,-.010f),new Vector3(.047f,.012f,.005f),.0024f);
            b.RoundedBody(Ivory,new Vector3(0,0,-.032f),new Vector3(.004f,.014f,.039f),.0008f);
            b.RoundedBody(Ivory,new Vector3(0,0,-.032f),new Vector3(.014f,.004f,.039f),.0008f);
            b.RoundedBody(Teal,new Vector3(0,0,-.053f),new Vector3(.040f,.014f,.006f),.0027f);
            b.Lathe(Ink,Profile(-.003f,.006f,.0109f,.0109f),24);
            b.Lathe(Ivory,Profile(.061f,.073f,.0058f,.0045f),20);
            b.Lathe(Teal,Profile(.068f,.081f,.0051f,.003f),20);
            b.Ring(Teal,.071f,.0066f,.005f,.003f,24);
            for(int i=0;i<11;i++)
            {
                float z=.004f+i*.0045f;float width=i%5==0?.013f:i%2==0?.009f:.005f;
                b.RoundBar(Ink,new Vector3(-width/2,.01304f,z),new Vector3(width/2,.01304f,z),.00018f,5);
            }
            b.RoundBar(Ink,new Vector3(-.0085f,.010f,.007f),new Vector3(-.0085f,.010f,.048f),.00016f,5);
            float radius=id=="aspirator"?.0028f:.00085f;
            b.Lathe(id=="aspirator"?Ivory:Steel,new[]{new Vector3(radius,radius,.080f),new Vector3(radius,radius,.138f),new Vector3(radius*.3f,radius*.3f,.142f)},12);
            if(id=="aspirator")for(int i=0;i<3;i++)b.RoundedBody(Ink,new Vector3(0,radius+.00004f,.118f+i*.007f),new Vector3(.0014f,.00008f,.0025f),.0005f);
        }

        static void Inhaler(Builder b)
        {
            b.RoundedBody(Teal,new Vector3(0,0,.020f),new Vector3(.041f,.071f,.038f),.007f);
            b.RoundedBody(Ivory,new Vector3(0,-.001f,-.0001f),new Vector3(.024f,.035f,.0008f),.003f);
            b.Push(new Vector3(0,.022f,.021f),Quaternion.Euler(-90,0,0));
            b.Lathe(Satin,new[]{new Vector3(.0145f,.0145f,0),new Vector3(.016f,.016f,.003f),new Vector3(.016f,.016f,.045f),new Vector3(.0154f,.0154f,.052f)},24);
            b.Lathe(Steel,Profile(.048f,.053f,.016f,.016f),24);
            b.Lathe(Ivory,Profile(.052f,.055f,.0142f,.0134f),24);b.Pop();
            b.RoundedBody(Teal,new Vector3(0,-.022f,.056f),new Vector3(.034f,.024f,.046f),.0045f);
            b.RoundedBody(Ink,new Vector3(0,-.022f,.0795f),new Vector3(.025f,.016f,.0007f),.0038f);
            b.RoundedBody(Ivory,new Vector3(0,-.029f,.0798f),new Vector3(.021f,.001f,.0015f),.0004f);
            for(int i=0;i<4;i++)
            {
                b.RoundBar(Ink,new Vector3(-.0206f,-.013f+i*.009f,.009f),new Vector3(-.0206f,-.013f+i*.009f,.030f),.00032f,6);
                b.RoundBar(Ink,new Vector3(.0206f,-.013f+i*.009f,.009f),new Vector3(.0206f,-.013f+i*.009f,.030f),.00032f,6);
            }
            b.RoundedBody(Satin,new Vector3(0,.008f,-.0007f),new Vector3(.012f,.001f,.001f),.0004f);
        }

        static void Spacer(Builder b)
        {
            b.Lathe(Clear,new[]{new Vector3(.024f,.024f,-.019f),new Vector3(.0275f,.0275f,-.011f),new Vector3(.026f,.026f,.092f),new Vector3(.020f,.020f,.114f),new Vector3(.0188f,.0188f,.114f),new Vector3(.0247f,.0247f,.091f),new Vector3(.0262f,.0262f,-.011f),new Vector3(.0227f,.0227f,-.019f)},28,false);
            b.Ring(Teal,-.026f,.029f,.017f,.013f,28);
            b.Ring(Ink,-.0328f,.019f,.012f,.003f,24);
            b.Ring(Teal,.117f,.0215f,.015f,.010f,28);
            b.Ring(Ivory,.126f,.017f,.012f,.004f,24);
            b.RoundedBody(Ivory,new Vector3(0,0,.140f),new Vector3(.036f,.022f,.032f),.005f);
            b.RoundedBody(Ink,new Vector3(0,0,.1565f),new Vector3(.027f,.014f,.0006f),.003f);
            b.RoundedBody(Teal,new Vector3(0,0,.157f),new Vector3(.0008f,.012f,.0008f),.0003f);
            b.Lathe(Ivory,Profile(.108f,.109f,.014f,.014f),20);
            for(int i=0;i<6;i++)b.RoundBar(Teal,new Vector3(-.010f+i*.004f,-.011f,.110f),new Vector3(-.010f+i*.004f,.011f,.110f),.00020f,5);
            for(int i=0;i<3;i++)b.Ring(Ivory,-.020f+i*.004f,.0285f,.0272f,.001f,28);
        }

        static void OxygenMask(Builder b)
        {
            // Contoured shell with a soft seal; open recess is visible through the polymer.
            b.Lathe(Clear,new[]{new Vector3(.043f,.059f,.004f),new Vector3(.046f,.060f,.010f),new Vector3(.041f,.055f,.034f),new Vector3(.028f,.041f,.058f),new Vector3(.009f,.013f,.067f)},32);
            b.Loop(Teal,new Vector3(0,0,.005f),.045f,.059f,.0028f,36,Quaternion.Euler(90,0,0));
            b.RoundedBody(Satin,new Vector3(0,.044f,.050f),new Vector3(.031f,.005f,.004f),.0018f);
            b.RoundedBody(Teal,new Vector3(0,0,-.004f),new Vector3(.142f,.009f,.0035f),.0017f);
            b.Push(new Vector3(0,-.013f,.052f),Quaternion.Euler(35,0,0));b.Lathe(Teal,Profile(-.046f,-.015f,.009f,.008f),18);b.Pop();
            foreach(float sign in new[]{-1f,1f})
            {
                b.RoundedBody(Ivory,new Vector3(sign*.043f,0,.014f),new Vector3(.008f,.016f,.006f),.002f);
                for(int i=0;i<3;i++)b.RoundBar(Ink,new Vector3(sign*.027f,-.012f+i*.007f,.050f),new Vector3(sign*.034f,-.012f+i*.007f,.045f),.0008f,8);
            }
        }

        static void Cap(Builder b)
        {
            b.RoundedBody(Teal,Vector3.zero,new Vector3(.037f,.025f,.027f),.005f);
            b.RoundedBody(Ink,new Vector3(0,0,-.0137f),new Vector3(.028f,.017f,.0008f),.003f);
            b.RoundedBody(Ivory,new Vector3(0,0,.0138f),new Vector3(.014f,.005f,.0006f),.001f);
            for(int i=0;i<4;i++)b.RoundBar(Ink,new Vector3(-.014f+i*.009f,-.0126f,-.006f),new Vector3(-.014f+i*.009f,-.0126f,.006f),.00022f,5);
        }
        static void Dressing(Builder b)
        {
            b.RoundedBody(Ivory,Vector3.zero,new Vector3(.085f,.064f,.003f),.009f);
            b.RoundedBody(Clear,new Vector3(0,0,.002f),new Vector3(.070f,.049f,.0015f),.007f);
            b.RoundedBody(Ivory,new Vector3(0,0,.005f),new Vector3(.045f,.031f,.004f),.005f);
            for(int i=0;i<15;i++)b.RoundBar(Satin,new Vector3(-.019f,-.012f+i*.0017f,.0071f),new Vector3(.019f,-.012f+i*.0017f,.0071f),.00006f,4);
            for(int i=0;i<20;i++)b.RoundBar(Satin,new Vector3(-.019f+i*.002f,-.011f,.00715f),new Vector3(-.019f+i*.002f,.011f,.00715f),.00005f,4);
            b.RoundedBody(Teal,new Vector3(-.035f,0,.0017f),new Vector3(.003f,.024f,.0005f),.001f);
        }

        static void Sensor(Builder b,bool ultrasound)
        {
            b.Lathe(Ivory,new[]{new Vector3(.008f,.008f,-.043f),new Vector3(.012f,.011f,-.029f),new Vector3(.014f,.0115f,.011f),new Vector3(.012f,.010f,.040f)},24);
            b.RoundedBody(Teal,new Vector3(0,.0113f,.004f),new Vector3(.013f,.002f,.040f),.004f);
            for(int i=0;i<4;i++)b.RoundBar(Ink,new Vector3(-.005f,.0125f,-.006f+i*.006f),new Vector3(.005f,.0125f,-.006f+i*.006f),.00024f,6);
            b.RoundedBody(Ivory,new Vector3(0,0,.056f),new Vector3(ultrasound?.056f:.029f,.021f,.028f),.007f);
            b.RoundedBody(Teal,new Vector3(0,0,.073f),new Vector3(ultrasound?.049f:.025f,.016f,.010f),.004f);
            b.RoundedBody(Ink,new Vector3(0,0,.0785f),new Vector3(ultrasound?.045f:.022f,.012f,.0008f),.003f);
            b.Lathe(Ink,Profile(-.069f,-.037f,.0055f,.007f),16);
            for(int i=0;i<5;i++)b.Ring(Ink,-.063f+i*.0048f,.0065f,.005f,.002f,16);
            b.Tube(Ink,new[]{new Vector3(0,0,-.070f),new Vector3(-.008f,0,-.077f),new Vector3(-.018f,0,-.071f)},.0028f,10);
        }
        static void Swab(Builder b)
        {
            b.Lathe(Ivory,Profile(-.0405f,.098f,.004f,.0035f),16);
            b.Lathe(Teal,Profile(-.025f,.015f,.0044f,.0044f),16);
            for(int i=0;i<6;i++)b.Ring(Ink,-.019f+i*.005f,.0045f,.004f,.0005f,16);
            b.Lathe(Ivory,new[]{new Vector3(.005f,.005f,.091f),new Vector3(.0145f,.0105f,.101f),new Vector3(.0165f,.012f,.117f),new Vector3(.012f,.010f,.129f),new Vector3(.002f,.002f,.135f)},24);
            for(int i=0;i<5;i++)b.Ring(Satin,.103f+i*.0045f,.0154f,.0149f,.00015f,20,.71f);
        }
        static void Connector(Builder b)
        {
            b.Lathe(Ivory,new[]{new Vector3(.008f,.008f,-.008f),new Vector3(.0105f,.0105f,.005f),new Vector3(.0105f,.0105f,.077f),new Vector3(.005f,.005f,.095f)},20);
            b.Ring(Teal,.052f,.0155f,.0105f,.016f,24);
            for(int i=0;i<10;i++)
            {
                float a=i*Mathf.PI/5;Vector3 o=new Vector3(Mathf.Cos(a)*.0155f,Mathf.Sin(a)*.0155f,.052f);
                b.RoundBar(Ink,o-Vector3.forward*.0055f,o+Vector3.forward*.0055f,.00035f,6);
            }
            b.RoundBar(Ivory,new Vector3(0,0,.022f),new Vector3(.030f,0,.022f),.007f,16);
            b.RoundedBody(Teal,new Vector3(.020f,.012f,.022f),new Vector3(.064f,.010f,.012f),.003f);
            b.RoundBar(Teal,new Vector3(.020f,0,.022f),new Vector3(.020f,.012f,.022f),.006f,16);
            b.RoundedBody(Ivory,new Vector3(.020f,.0172f,.022f),new Vector3(.023f,.0008f,.003f),.0008f);
        }

        static void Catheter(Builder b,string id)
        {
            bool valve=id=="valve", wire=id=="guidewire", drain=id=="drain";
            float grip=valve?.0175f:.0095f;
            b.Lathe(Ink,new[]{new Vector3(grip*.72f,grip*.72f,-.0485f),new Vector3(grip,grip,-.042f),new Vector3(grip,grip,.029f),new Vector3(grip*.75f,grip*.75f,.0365f)},24);
            for(int i=0;i<8;i++)b.Ring(Teal,-.035f+i*.007f,grip+.0003f,grip-.0004f,.0015f,24);
            b.Lathe(Satin,Profile(.026f,.039f,grip*.78f,.004f),24);
            b.Ring(Teal,.016f,valve?.019f:.0135f,grip,.011f,24);
            float shaft=wire?.0009f:drain?.004f:.0025f;
            b.Lathe(drain?Ivory:Steel,Profile(.035f,.141f,shaft,shaft*.84f),wire?10:16);
            if(drain)
            {
                b.Lathe(Ivory,new[]{new Vector3(.0038f,.0038f,.137f),new Vector3(.0031f,.0031f,.140f),new Vector3(.0015f,.0015f,.142f)},16);
                for(int i=0;i<4;i++)b.RoundedBody(Ink,new Vector3(0,.0039f,.095f+i*.010f),new Vector3(.0025f,.0002f,.004f),.0008f);
                b.RoundBar(Teal,new Vector3(-.0028f,.0028f,.039f),new Vector3(-.0028f,.0028f,.139f),.00018f,5);
            }
            if(id=="dilator")b.Lathe(Ivory,new[]{new Vector3(.0058f,.0058f,.047f),new Vector3(.006f,.006f,.070f),new Vector3(.0042f,.0042f,.105f),new Vector3(.0025f,.0025f,.128f),new Vector3(.0018f,.0018f,.142f)},20);
            if(id=="balloon"||id=="stent"||valve)
            {
                float r=valve?.010f:id=="stent"?.0049f:.0047f;
                b.Lathe(Ivory,new[]{new Vector3(.0024f,.0024f,.083f),new Vector3(r,r,.092f),new Vector3(r,r,.122f),new Vector3(.0024f,.0024f,.134f)},20);
                b.Ring(Satin,.091f,r+.0003f,r-.0004f,.0016f,20);b.Ring(Satin,.124f,r+.0003f,r-.0004f,.0016f,20);
                if(id=="stent"||valve)Scaffold(b,r+.00025f,.093f,.123f,valve?10:8);
                else for(int i=0;i<5;i++){float a=i*Mathf.PI*2/5;b.RoundBar(Clear,new Vector3(Mathf.Cos(a)*r,Mathf.Sin(a)*r,.094f),new Vector3(Mathf.Cos(a)*r,Mathf.Sin(a)*r,.122f),.00024f,5);}
            }
            if(wire)b.Tube(Steel,new[]{new Vector3(0,0,.136f),new Vector3(.0015f,0,.141f),new Vector3(.003f,0,.142f),new Vector3(.004f,0,.140f)},.00075f,8);
            if(valve)
            {
                b.Ring(Satin,-.015f,.019f,.016f,.018f,28);
                for(int i=0;i<12;i++){float a=i*Mathf.PI/6;b.RoundBar(Ink,new Vector3(Mathf.Cos(a)*.0191f,Mathf.Sin(a)*.0191f,-.022f),new Vector3(Mathf.Cos(a)*.0191f,Mathf.Sin(a)*.0191f,-.008f),.00045f,6);}
                b.RoundedBody(Teal,new Vector3(0,.017f,.014f),new Vector3(.012f,.005f,.020f),.003f);
            }
        }

        static void Scaffold(Builder b,float radius,float start,float end,int around)
        {
            const int rows=5;
            for(int row=0;row<rows;row++)
            {
                var crown=new List<Vector3>();
                for(int k=0;k<=around*2;k++)
                {
                    float a=k*Mathf.PI/around;float z=Mathf.Lerp(start,end,(row+(k%2)*.7f)/rows);
                    crown.Add(new Vector3(Mathf.Cos(a)*radius,Mathf.Sin(a)*radius,z));
                }
                b.Tube(Steel,crown,.00022f,5);
            }
            for(int i=0;i<around;i+=2){float a=i*Mathf.PI*2/around;b.RoundBar(Steel,new Vector3(Mathf.Cos(a)*radius,Mathf.Sin(a)*radius,start),new Vector3(Mathf.Cos(a)*radius,Mathf.Sin(a)*radius,end-.002f),.00019f,5);}
        }

        static Vector3[] Profile(float start,float end,float a,float c)
        {
            float bevel=Mathf.Min((end-start)*.20f,.0012f);
            return new[]{new Vector3(a*.92f,a*.92f,start),new Vector3(a,a,start+bevel),new Vector3(c,c,end-bevel),new Vector3(c*.92f,c*.92f,end)};
        }

        sealed class Builder
        {
            readonly List<Vector3> vertices=new List<Vector3>();
            readonly List<Vector2> uv=new List<Vector2>();
            readonly List<int>[] triangles={new List<int>(),new List<int>(),new List<int>(),new List<int>(),new List<int>(),new List<int>()};
            Matrix4x4 transform=Matrix4x4.identity;
            public void Push(Vector3 position,Quaternion rotation){transform=Matrix4x4.TRS(position,rotation,Vector3.one);}
            public void Pop(){transform=Matrix4x4.identity;}
            int V(Vector3 value,Vector2 tex=default){int i=vertices.Count;vertices.Add(transform.MultiplyPoint3x4(value));uv.Add(tex);return i;}
            void Tri(int mat,int a,int b,int c){triangles[mat].Add(a);triangles[mat].Add(b);triangles[mat].Add(c);}
            void Quad(int mat,int a,int b,int c,int d){Tri(mat,a,b,c);Tri(mat,a,c,d);}
            public void Lathe(int mat,IReadOnlyList<Vector3> rings,int sides=20,bool caps=true)
            {
                int first=vertices.Count;
                for(int j=0;j<rings.Count;j++)for(int k=0;k<=sides;k++)
                {
                    float angle=k*Mathf.PI*2/sides;
                    V(new Vector3(Mathf.Cos(angle)*rings[j].x,Mathf.Sin(angle)*rings[j].y,rings[j].z),new Vector2(k/(float)sides,j/(float)(rings.Count-1)));
                }
                for(int j=0;j<rings.Count-1;j++)for(int k=0;k<sides;k++)
                {
                    int a=first+j*(sides+1)+k,b=a+sides+1;Quad(mat,a,a+1,b+1,b);
                }
                if(caps)
                {
                    int begin=V(new Vector3(0,0,rings[0].z)),end=V(new Vector3(0,0,rings[rings.Count-1].z));
                    int last=first+(rings.Count-1)*(sides+1);
                    for(int k=0;k<sides;k++){Tri(mat,begin,first+k+1,first+k);Tri(mat,end,last+k,last+k+1);}
                }
            }
            public void Ring(int mat,float z,float outer,float inner,float depth,int sides=20,float yScale=1)
            {
                Lathe(mat,new[]{new Vector3(outer,outer*yScale,z-depth/2),new Vector3(outer,outer*yScale,z+depth/2),new Vector3(inner,inner*yScale,z+depth/2),new Vector3(inner,inner*yScale,z-depth/2),new Vector3(outer,outer*yScale,z-depth/2)},sides,false);
            }
            public void RoundedBody(int mat,Vector3 center,Vector3 size,float radius)
            {
                float bevel=Mathf.Min(radius,Mathf.Min(size.x,Mathf.Min(size.y,size.z))*.35f);
                float halfX=size.x/2,halfY=size.y/2,halfZ=size.z/2;
                radius=Mathf.Min(radius,Mathf.Min(halfX,halfY));
                var cross=new List<Vector2>();
                for(int corner=0;corner<4;corner++)for(int k=0;k<=3;k++)
                {
                    float a=(corner*90+k*30)*Mathf.Deg2Rad;
                    float cx=(corner==0||corner==3)?halfX-radius:-halfX+radius;
                    float cy=corner<2?halfY-radius:-halfY+radius;
                    var point=new Vector2(cx+Mathf.Cos(a)*radius,cy+Mathf.Sin(a)*radius);
                    if(cross.Count==0||(point-cross[cross.Count-1]).sqrMagnitude>1e-16f)cross.Add(point);
                }
                if(cross.Count>1&&(cross[0]-cross[cross.Count-1]).sqrMagnitude<1e-16f)cross.RemoveAt(cross.Count-1);
                int sides=cross.Count,first=vertices.Count;
                float[] zs={-halfZ,-halfZ+bevel,halfZ-bevel,halfZ};
                for(int j=0;j<4;j++)foreach(var point in cross)
                {
                    float inset=(j==0||j==3)?bevel*.35f:0;
                    V(center+new Vector3(point.x*(1-inset/halfX),point.y*(1-inset/halfY),zs[j]));
                }
                for(int j=0;j<3;j++)for(int k=0;k<sides;k++){int a=first+j*sides+k,c=first+j*sides+(k+1)%sides;Quad(mat,a,c,c+sides,a+sides);}
                int start=V(center-Vector3.forward*halfZ),end=V(center+Vector3.forward*halfZ);
                for(int k=0;k<sides;k++){Tri(mat,start,first+(k+1)%sides,first+k);Tri(mat,end,first+3*sides+k,first+3*sides+(k+1)%sides);}
            }
            public void RoundBar(int mat,Vector3 a,Vector3 c,float radius,int sides=8){Tube(mat,new[]{a,c},radius,sides);}
            public void Tube(int mat,IReadOnlyList<Vector3> points,float radius,int sides=8)
            {
                int first=vertices.Count;
                for(int j=0;j<points.Count;j++)
                {
                    Vector3 direction=(points[Mathf.Min(j+1,points.Count-1)]-points[Mathf.Max(j-1,0)]).normalized;
                    if(direction.sqrMagnitude<.5f)direction=Vector3.forward;
                    Vector3 across=Vector3.Cross(direction,Mathf.Abs(Vector3.Dot(direction,Vector3.up))>.9f?Vector3.right:Vector3.up).normalized;
                    Vector3 second=Vector3.Cross(direction,across);
                    for(int k=0;k<sides;k++){float a=k*Mathf.PI*2/sides;V(points[j]+(across*Mathf.Cos(a)+second*Mathf.Sin(a))*radius);}
                }
                for(int j=0;j<points.Count-1;j++)for(int k=0;k<sides;k++){int a=first+j*sides+k,c=first+j*sides+(k+1)%sides;Quad(mat,a,c,c+sides,a+sides);}
                int begin=V(points[0]),end=V(points[points.Count-1]),last=first+(points.Count-1)*sides;
                for(int k=0;k<sides;k++){Tri(mat,begin,first+(k+1)%sides,first+k);Tri(mat,end,last+k,last+(k+1)%sides);}
            }
            public void Loop(int mat,Vector3 center,float x,float z,float radius,int sides,Quaternion? rotation=null)
            {
                var points=new List<Vector3>();for(int i=0;i<=sides;i++){float a=i*Mathf.PI*2/sides;points.Add(center+(rotation??Quaternion.identity)*new Vector3(Mathf.Cos(a)*x,0,Mathf.Sin(a)*z));}Tube(mat,points,radius,8);
            }
            public void Polygon(int mat,Vector2[] outline,float depth)
            {
                // Convex outlines with counter-clockwise x/z coordinates. Each polygon is
                // duplicated on both sides, then given a real edge thickness.
                int first=vertices.Count;float area=0;for(int i=0;i<outline.Length;i++){var a=outline[i];var c=outline[(i+1)%outline.Length];area+=a.x*c.y-c.x*a.y;}
                if(area<0)Array.Reverse(outline);
                foreach(var p in outline)V(new Vector3(p.x,-depth/2,p.y));foreach(var p in outline)V(new Vector3(p.x,depth/2,p.y));
                int n=outline.Length;
                for(int i=1;i<n-1;i++){Tri(mat,first,first+i,first+i+1);Tri(mat,first+n,first+n+i+1,first+n+i);}
                for(int i=0;i<n;i++)Quad(mat,first+i,first+n+i,first+n+(i+1)%n,first+(i+1)%n);
            }
            public Mesh Finish(string name)
            {
                var mesh=new Mesh{name=name,indexFormat=vertices.Count>65535?IndexFormat.UInt32:IndexFormat.UInt16};
                mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.subMeshCount=triangles.Length;
                for(int i=0;i<triangles.Length;i++)mesh.SetTriangles(triangles[i],i,false);
                mesh.RecalculateNormals();mesh.RecalculateBounds();mesh.UploadMeshData(false);return mesh;
            }
        }
    }
}
