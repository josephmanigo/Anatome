using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ThoraxLab.Learning
{
    /// <summary>
    /// Brand-neutral instrument presentation. Geometry is authored in the existing tool's
    /// local metre coordinates; this class never creates colliders or working-end markers.
    /// Meshes are cached by instrument and merged by finish. The saw blade is a separate
    /// rigid part so it can oscillate about its spindle without moving the grip.
    /// </summary>
    public static class InstrumentGeometry
    {
        public const float ScalpelTipZ=.108f, SawTipZ=.129f, OxygenSealZ=.067f;
        public const string SawBladeName="Oscillating blade";
        const float SawSpindleZ=.082f;
        public const int Steel=0, Satin=1, Ivory=2, Teal=3, Ink=4, Clear=5, Fibre=6;
        static readonly string[] finishNames={"Honed edge","Satin steel","Moulded ivory","Moulded teal","Grip rubber","Clear shell","Absorbent fibre"};
        static readonly Material[] materials=new Material[finishNames.Length];
        static readonly Dictionary<string,(Mesh mesh,int[] finishes)> meshes=new Dictionary<string,(Mesh,int[])>();
        public static readonly string[] SupportedIds={"scalpel","suture","inhaler","spacer","oxygen","cap","dressing","ultrasound","probe","swab","connector","anaesthetic","needle","aspirator","medication","guidewire","dilator","drain","balloon","stent","valve","dissection-scalpel","dissection-saw"};

        public static GameObject Create(Transform parent,string toolId)
        {
            var root=CreatePart(parent,toolId,"Crafted "+toolId+" / visual only");
            if(toolId=="dissection-saw")
                CreatePart(root.transform,SawBladeName,SawBladeName).transform.localPosition=Vector3.forward*SawSpindleZ;
            return root;
        }

        static GameObject CreatePart(Transform parent,string toolId,string name)
        {
            if(!meshes.TryGetValue(toolId,out var part)||!part.mesh)
            {
                var geometry=new Builder();
                switch(toolId)
                {
                    case "scalpel": Scalpel(geometry,false);break;
                    case "dissection-scalpel": Scalpel(geometry,true);break;
                    case "dissection-saw": Saw(geometry);break;
                    case SawBladeName: SawBlade(geometry);break;
                    case "suture": NeedleHolder(geometry);break;
                    case "inhaler": Inhaler(geometry);break;
                    case "spacer": Spacer(geometry);break;
                    case "oxygen": OxygenMask(geometry);break;
                    case "cap": Cap(geometry);break;
                    case "dressing": Dressing(geometry);break;
                    case "radial-band": RadialBand(geometry);break;
                    case "ultrasound": Sensor(geometry,true);break;
                    case "probe": Sensor(geometry,false);break;
                    case "swab": Swab(geometry);break;
                    case "connector": Connector(geometry);break;
                    case "anaesthetic": case "needle": case "aspirator": case "medication": Syringe(geometry,toolId);break;
                    default: Catheter(geometry,toolId);break;
                }
                var mesh=geometry.Finish("Detailed "+toolId,out var finishes);
                part=(mesh,finishes);
                meshes[toolId]=part;
            }
            var go=new GameObject(name);go.transform.SetParent(parent,false);
            go.AddComponent<MeshFilter>().sharedMesh=part.mesh;
            var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterials=Array.ConvertAll(part.finishes,FinishMaterial);
            renderer.shadowCastingMode=ShadowCastingMode.On;renderer.receiveShadows=true;
            return go;
        }

        public static Material FinishMaterial(int index)
        {
            if(!materials[index])materials[index]=Resources.Load<Material>("Learning/InstrumentFinishes/"+finishNames[index]);
            if(!materials[index])throw new InvalidOperationException("Missing instrument finish "+finishNames[index]);
            return materials[index];
        }

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        static void CleanUpOnReload()=>UnityEditor.AssemblyReloadEvents.beforeAssemblyReload+=ResetCache;
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCache()
        {
            foreach(var part in meshes.Values)if(part.mesh)
            {
                if(Application.isPlaying)UnityEngine.Object.Destroy(part.mesh);
                else UnityEngine.Object.DestroyImmediate(part.mesh);
            }
            meshes.Clear();
            Array.Clear(materials,0,materials.Length);
        }

        static void Scalpel(Builder b,bool dissection)
        {
            float start=dissection?-.005f:-.047f;
            Quaternion rotation=dissection?Quaternion.Euler(0,0,90):Quaternion.identity;
            b.Push(Vector3.zero,rotation);
            b.RoundedBody(Satin,new Vector3(0,0,(start+.063f)/2),new Vector3(.012f,.006f,.063f-start),.002f);
            b.Lathe(Steel,new[]{new Vector3(.0048f,.0021f,.058f),new Vector3(.0034f,.0016f,.070f),new Vector3(.003f,.0014f,.076f)},16);
            for(int i=0;i<12;i++)
            {
                float z=Mathf.Lerp(start+.016f,.047f,i/11f);
                foreach(float side in new[]{-1f,1f})
                {
                    var a=new Vector3(-.0041f,side*.00302f,z-.0009f);
                    var c=new Vector3(.0041f,side*.00302f,z+.0009f);
                    b.RoundBar(Ink,a,c,.00013f,5);
                    b.RoundBar(Steel,a+Vector3.forward*.00030f,c+Vector3.forward*.00030f,.00019f,6);
                }
            }
            b.RoundedBody(Steel,new Vector3(0,.00305f,start+.009f),new Vector3(.0065f,.00016f,.007f),.001f);
            // Each row describes the spine, the start of the grind, and the curved edge.
            var rows=new[]{
                new Vector4(.067f,-.0027f,.0017f,.0026f),new Vector4(.073f,-.0031f,.0024f,.0043f),
                new Vector4(.074f,-.0032f,.0027f,.0047f),new Vector4(.082f,-.0033f,.00385f,.00645f),
                new Vector4(.084f,-.0032f,.0040f,.00670f),new Vector4(.091f,-.0028f,.00365f,.00655f),
                new Vector4(.097f,-.0020f,.00265f,.00512f),new Vector4(.103f,-.00095f,.00125f,.00285f),
                new Vector4(.106f,-.00040f,.00046f,.00117f),new Vector4(ScalpelTipZ,0,0,0)};
            Vector3 Surface(Vector4 row,float x)=>new Vector3(x,.00052f*Mathf.Clamp01((ScalpelTipZ-row.x)/.012f),row.x);
            for(int i=0;i<rows.Length-1;i++)
            {
                var a=rows[i];var c=rows[i+1];
                var backA=Surface(a,a.y);var backC=Surface(c,c.y);
                var grindA=Surface(a,a.z);var grindC=Surface(c,c.z);
                var edgeA=new Vector3(a.w,0,a.x);var edgeC=new Vector3(c.w,0,c.x);
                if(i>=1&&i<=3)
                {
                    const float slotCenter=-.0005f;
                    float near=i==1?0:.00078f,far=i==3?0:.00078f;
                    var leftA=Surface(a,slotCenter-near);var leftC=Surface(c,slotCenter-far);
                    var rightA=Surface(a,slotCenter+near);var rightC=Surface(c,slotCenter+far);
                    b.SheetFaces(Satin,backA,backC,leftC,leftA);
                    b.SheetFaces(Satin,rightA,rightC,grindC,grindA);
                    b.ThicknessEdge(Satin,leftC,leftA);b.ThicknessEdge(Satin,rightA,rightC);
                }
                else b.SheetFaces(Satin,backA,backC,grindC,grindA);
                b.SheetFaces(Steel,grindA,grindC,edgeC,edgeA);
                b.ThicknessEdge(Satin,backA,backC);
                if(i==0){b.ThicknessEdge(Satin,grindA,backA);b.ThicknessEdge(Steel,edgeA,grindA);}
            }
            b.Pop();
        }

        static void Saw(Builder b)
        {
            b.RoundedBody(Teal,new Vector3(0,-.008f,.026f),new Vector3(.028f,.034f,.074f),.007f);
            for(int i=0;i<6;i++)b.RoundedBody(Ink,new Vector3(0,-.008f,.005f+i*.007f),new Vector3(.0295f,.0352f,.0026f),.0068f);
            b.RoundedBody(Ink,new Vector3(0,-.008f,-.008f),new Vector3(.0284f,.0345f,.0055f),.0065f);
            b.RoundedBody(Satin,new Vector3(0,0,.070f),new Vector3(.034f,.024f,.035f),.006f);
            b.RoundedBody(Steel,new Vector3(0,0,.082f),new Vector3(.025f,.019f,.008f),.003f);
            b.RoundedBody(Ink,new Vector3(0,.0098f,.043f),new Vector3(.012f,.0038f,.016f),.002f);
            b.RoundedBody(Teal,new Vector3(0,.0118f,.043f),new Vector3(.0085f,.0018f,.011f),.0008f);
            foreach(float side in new[]{-1f,1f})
            {
                var spindle=new Vector3(side*.017f,0,SawSpindleZ);
                b.RoundBar(Ink,spindle,spindle+Vector3.right*side*.0006f,.0058f,20);
                b.RoundBar(Satin,spindle+Vector3.right*side*.0006f,spindle+Vector3.right*side*.0013f,.00465f,20);
                b.RoundBar(Steel,spindle+Vector3.right*side*.0013f,spindle+Vector3.right*side*.00165f,.00265f,14);
                for(int i=0;i<3;i++)
                    b.RoundBar(Ink,new Vector3(side*.017f,-.0025f,.058f+i*.0035f),new Vector3(side*.017f,.003f,.060f+i*.0035f),.0005f,8);
            }
        }

        static void SawBlade(Builder b)
        {
            b.Push(Vector3.back*SawSpindleZ,Quaternion.Euler(0,0,90));
            const float depth=.00145f,slotOffset=.0041f,pitch=.00205f;
            // The twin radiused apertures pass through the plate.
            var rows=new[]{new Vector3(.078f,.0067f,0),new Vector3(.094f,.0089f,0),
                new Vector3(.0950f,.0091f,.0010f),new Vector3(.0962f,.0093f,.00138f),
                new Vector3(.1140f,.0119f,.00138f),new Vector3(.1152f,.0121f,.0010f),
                new Vector3(.1162f,.0122f,0),new Vector3(.121f,pitch*6.5f,0)};
            void Band(Vector3 a,Vector3 c,float leftA,float rightA,float leftC,float rightC)
            {
                b.Polygon(Satin,new[]{new Vector2(leftA,a.x),new Vector2(rightA,a.x),new Vector2(rightC,c.x),new Vector2(leftC,c.x)},depth);
            }
            for(int i=0;i<rows.Length-1;i++)
            {
                var a=rows[i];var c=rows[i+1];
                if(a.z==0&&c.z==0){Band(a,c,-a.y,a.y,-c.y,c.y);continue;}
                Band(a,c,-a.y,-slotOffset-a.z,-c.y,-slotOffset-c.z);
                Band(a,c,-slotOffset+a.z,slotOffset-a.z,-slotOffset+c.z,slotOffset-c.z);
                Band(a,c,slotOffset+a.z,a.y,slotOffset+c.z,c.y);
            }
            float Crown(float x)=>.12595f-.0026f*Mathf.Pow(x/(pitch*6.5f),2);
            for(int tooth=-6;tooth<=6;tooth++)
            {
                float x=tooth*pitch,left=x-pitch*.5f,right=x+pitch*.5f;
                float zl=Crown(left),zr=Crown(right);
                b.Polygon(Satin,new[]{new Vector2(left,.121f),new Vector2(right,.121f),new Vector2(right,zr),new Vector2(left,zl)},depth);
                var a=new Vector3(left,depth*.5f,zl);var c=new Vector3(right,depth*.5f,zr);
                var tip=new Vector3(x,0,SawTipZ+Crown(x)-Crown(0));
                b.SheetFaces(Steel,a,tip,tip,c);
                b.ThicknessEdge(Steel,a,tip);b.ThicknessEdge(Steel,tip,c);
            }
            b.Pop();
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
            // The pneumonia case uses a prepared IV administration port, never a
            // hypodermic needle applied to a prescription chart or to lung tissue.
            if(id=="medication")return;
            bool aspirator=id=="aspirator";
            int shaft=aspirator?Ivory:Steel;
            b.Lathe(shaft,new[]{new Vector3(radius,radius,.080f),new Vector3(radius,radius,.134f)},16,false);
            for(int i=0;i<16;i++)
            {
                Vector3 Rim(int k)
                {
                    float angle=k*Mathf.PI/8;
                    return new Vector3(radius*Mathf.Cos(angle),radius*Mathf.Sin(angle),
                        .142f-(aspirator?.0004f:.0025f)*(1-Mathf.Cos(angle)));
                }
                var a=Rim(i);var c=Rim(i+1);
                var innerA=new Vector3(a.x*.64f,a.y*.64f,a.z);
                var innerC=new Vector3(c.x*.64f,c.y*.64f,c.z);
                b.Face(shaft,new Vector3(a.x,a.y,.134f),new Vector3(c.x,c.y,.134f),c,a);
                b.Face(shaft,a,c,innerC,innerA);
                b.Face(shaft,innerA,innerC,new Vector3(innerC.x,innerC.y,.134f),new Vector3(innerA.x,innerA.y,.134f));
            }
            if(aspirator)for(int i=0;i<3;i++)b.RoundedBody(Ink,new Vector3(0,radius+.00004f,.118f+i*.007f),new Vector3(.0014f,.00008f,.0025f),.0005f);
        }

        static void RadialBand(Builder b)
        {
            b.RoundedBody(Clear,new Vector3(0,0,0),new Vector3(.12f,.035f,.004f),.006f);
            b.RoundedBody(Clear,new Vector3(0,0,.004f),new Vector3(.042f,.031f,.008f),.008f);
            b.RoundedBody(Teal,new Vector3(.047f,0,-.001f),new Vector3(.018f,.038f,.006f),.003f);
            b.RoundBar(Ivory,new Vector3(.009f,.018f,0),new Vector3(.025f,.033f,0),.002f,10);
            b.RoundBar(Teal,new Vector3(.025f,.033f,0),new Vector3(.038f,.033f,0),.004f,12);
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
            // The broad open rim faces the patient along tool-local +Z. The nose clip remains uppermost.
            b.Push(Vector3.forward*(OxygenSealZ+.005f),Quaternion.Euler(0,180,0));
            b.Lathe(Clear,new[]{new Vector3(.043f,.059f,.004f),new Vector3(.046f,.060f,.010f),new Vector3(.041f,.055f,.034f),
                new Vector3(.028f,.041f,.058f),new Vector3(.009f,.013f,.067f),new Vector3(.0078f,.0118f,.067f),
                new Vector3(.0268f,.0398f,.057f),new Vector3(.0398f,.0538f,.033f),new Vector3(.0448f,.0588f,.010f),
                new Vector3(.0418f,.0578f,.004f),new Vector3(.043f,.059f,.004f)},32,false);
            b.Loop(Teal,new Vector3(0,0,.005f),.045f,.059f,.0028f,36,Quaternion.Euler(90,0,0));
            b.RoundedBody(Satin,new Vector3(0,.044f,.050f),new Vector3(.031f,.005f,.004f),.0018f);
            foreach(float sign in new[]{-1f,1f})
            {
                b.RoundedBody(Ivory,new Vector3(sign*.043f,0,.014f),new Vector3(.008f,.016f,.006f),.002f);
                for(int i=0;i<3;i++)b.RoundBar(Ink,new Vector3(sign*.027f,-.012f+i*.007f,.050f),new Vector3(sign*.034f,-.012f+i*.007f,.045f),.0008f,8);
            }
            b.Pop();
            b.Push(Vector3.forward*(OxygenSealZ-.062f),Quaternion.Euler(163,0,0));
            b.Lathe(Teal,new[]{new Vector3(.0085f,.0108f,-.001f),new Vector3(.0065f,.0065f,.004f),
                new Vector3(.0048f,.0048f,.020f),new Vector3(.0035f,.0035f,.020f),new Vector3(.0048f,.0048f,.003f),
                new Vector3(.0074f,.0096f,-.001f),new Vector3(.0085f,.0108f,-.001f)},20,false);
            b.Pop();

            // A flat elastic ribbon passes beside the cheeks and around the back of the training head.
            Vector3[] previous=null;
            for(int i=0;i<=32;i++)
            {
                float angle=Mathf.Lerp(-2.59f,2.59f,i/32f),s=Mathf.Sin(angle),c=Mathf.Cos(angle);
                var center=new Vector3(.081f*s,.001f+.024f*Mathf.InverseLerp(Mathf.Cos(2.59f),1,c),OxygenSealZ+.067f+.090f*c);
                var thickness=new Vector3(s/.081f,0,c/.090f).normalized*.00065f;
                var width=Vector3.up*.0045f;
                var current=new[]{center+thickness-width,center+thickness+width,center-thickness+width,center-thickness-width};
                if(previous!=null)for(int edge=0;edge<4;edge++)
                {
                    int next=(edge+1)%4;b.Face(Ink,previous[edge],current[edge],current[next],previous[next]);
                }
                previous=current;
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
            b.RoundedBody(Fibre,new Vector3(0,0,.0043f),new Vector3(.046f,.032f,.0024f),.004f);
            b.RoundedBody(Fibre,new Vector3(.0003f,0,.006f),new Vector3(.0445f,.031f,.0024f),.004f);
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
            b.Lathe(Fibre,new[]{new Vector3(.005f,.005f,.091f),new Vector3(.013f,.0096f,.098f),
                new Vector3(.0161f,.0118f,.106f),new Vector3(.0165f,.012f,.116f),
                new Vector3(.015f,.0113f,.124f),new Vector3(.0103f,.0088f,.131f),new Vector3(.002f,.002f,.135f)},28);
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
            readonly List<int>[] triangles={new List<int>(),new List<int>(),new List<int>(),new List<int>(),new List<int>(),new List<int>(),new List<int>()};
            Matrix4x4 transform=Matrix4x4.identity;
            public void Push(Vector3 position,Quaternion rotation){transform=Matrix4x4.TRS(position,rotation,Vector3.one);}
            public void Pop(){transform=Matrix4x4.identity;}
            int V(Vector3 value){int i=vertices.Count;vertices.Add(transform.MultiplyPoint3x4(value));return i;}
            void Tri(int mat,int a,int b,int c){triangles[mat].Add(a);triangles[mat].Add(b);triangles[mat].Add(c);}
            void Quad(int mat,int a,int b,int c,int d){Tri(mat,a,b,c);Tri(mat,a,c,d);}
            public void Face(int mat,Vector3 a,Vector3 b,Vector3 c,Vector3 d)=>Quad(mat,V(a),V(b),V(c),V(d));
            static Vector3 Underside(Vector3 p)=>new Vector3(p.x,-p.y,p.z);
            public void SheetFaces(int mat,Vector3 a,Vector3 b,Vector3 c,Vector3 d)
            {
                Face(mat,a,b,c,d);Face(mat,Underside(d),Underside(c),Underside(b),Underside(a));
            }
            public void ThicknessEdge(int mat,Vector3 a,Vector3 b)=>Face(mat,a,Underside(a),Underside(b),b);
            public void Lathe(int mat,IReadOnlyList<Vector3> rings,int sides=20,bool caps=true)
            {
                int first=vertices.Count;
                for(int j=0;j<rings.Count;j++)for(int k=0;k<sides;k++)
                {
                    float angle=k*Mathf.PI*2/sides;
                    V(new Vector3(Mathf.Cos(angle)*rings[j].x,Mathf.Sin(angle)*rings[j].y,rings[j].z));
                }
                for(int j=0;j<rings.Count-1;j++)for(int k=0;k<sides;k++)
                {
                    int a=first+j*sides+k,c=first+j*sides+(k+1)%sides;Quad(mat,a,c,c+sides,a+sides);
                }
                if(caps)
                {
                    int begin=V(new Vector3(0,0,rings[0].z)),end=V(new Vector3(0,0,rings[rings.Count-1].z));
                    int last=first+(rings.Count-1)*sides;
                    for(int k=0;k<sides;k++){int next=(k+1)%sides;Tri(mat,begin,first+next,first+k);Tri(mat,end,last+k,last+next);}
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
                for(int i=0;i<n;i++)
                {
                    var a=outline[i];var c=outline[(i+1)%n];
                    Face(mat,new Vector3(a.x,-depth/2,a.y),new Vector3(a.x,depth/2,a.y),
                        new Vector3(c.x,depth/2,c.y),new Vector3(c.x,-depth/2,c.y));
                }
            }
            public Mesh Finish(string name,out int[] finishes)
            {
                var normalSums=new Vector3[vertices.Count];
                var mapped=new int[vertices.Count*3];
                var source=new List<int>(vertices.Count);
                var positions=new List<Vector3>(vertices.Count);
                var uv=new List<Vector2>(vertices.Count);
                var drawn=new List<int>();
                var groups=new List<List<int>>();
                // Project each face in local metres. Split UV seams while retaining its smoothed source normal.
                int Project(int vertex,int axis)
                {
                    int key=vertex*3+axis;
                    if(mapped[key]!=0)return mapped[key]-1;
                    var p=vertices[vertex];
                    uv.Add(axis==0?new Vector2(p.z,p.y):axis==1?new Vector2(p.z,p.x):new Vector2(p.x,p.y));
                    source.Add(vertex);positions.Add(p);mapped[key]=positions.Count;
                    return positions.Count-1;
                }
                for(int mat=0;mat<triangles.Length;mat++)
                {
                    var faces=triangles[mat];
                    if(faces.Count==0)continue;
                    var indices=new List<int>(faces.Count);
                    for(int i=0;i<faces.Count;i+=3)
                    {
                        int a=faces[i],b=faces[i+1],c=faces[i+2];
                        var normal=Vector3.Cross((vertices[b]-vertices[a])*1000,(vertices[c]-vertices[a])*1000);
                        if(normal.sqrMagnitude<1e-14f)continue;
                        normalSums[a]+=normal;normalSums[b]+=normal;normalSums[c]+=normal;
                        float x=Mathf.Abs(normal.x),y=Mathf.Abs(normal.y),z=Mathf.Abs(normal.z);
                        int axis=x>=y&&x>=z?0:y>=z?1:2;
                        indices.Add(Project(a,axis));indices.Add(Project(b,axis));indices.Add(Project(c,axis));
                    }
                    if(indices.Count>0){drawn.Add(mat);groups.Add(indices);}
                }
                var normals=new List<Vector3>(source.Count);
                foreach(int vertex in source)
                {
                    var normal=normalSums[vertex];
                    normals.Add(normal.sqrMagnitude>1e-16f?normal/normal.magnitude:Vector3.up);
                }
                var mesh=new Mesh{name=name,hideFlags=HideFlags.DontSave,indexFormat=positions.Count>65535?IndexFormat.UInt32:IndexFormat.UInt16};
                mesh.SetVertices(positions);mesh.SetNormals(normals);mesh.SetUVs(0,uv);mesh.subMeshCount=groups.Count;
                for(int i=0;i<groups.Count;i++)mesh.SetTriangles(groups[i],i,false);
                mesh.RecalculateTangents();mesh.RecalculateBounds();mesh.UploadMeshData(false);
                finishes=drawn.ToArray();return mesh;
            }
        }
    }
}
