using System;
using System.Collections.Generic;
using UnityEngine;

namespace ThoraxLab.Learning
{
    public sealed class ProcedureProtectedRegion : MonoBehaviour { public string anatomyId; }

    // Enlarged teaching anatomy has its own fixed frame. It never derives an access route from a camera ray.
    public sealed class CaseAnatomyWorkbench : MonoBehaviour
    {
        readonly Dictionary<string, Transform> targets = new Dictionary<string, Transform>();
        readonly Dictionary<string, Vector3[]> routes = new Dictionary<string, Vector3[]>();
        readonly List<Material> materials = new List<Material>();
        readonly List<WorkbenchGeometry> geometry = new List<WorkbenchGeometry>();
        WorkbenchGeometry solid;
        Material skin, fat, muscle, bone, artery, nerve, lung, polymer, steel, dark, fluid, teal, tissueLight, vesselLining, screen, vein, manikin, lips, crease;
        Transform field, retainedDevice, cap, seal, collection, fixation;
        string disease;
        public IReadOnlyDictionary<string, Transform> Targets => targets;
        public int VisualTriangleCount { get; private set; }
        public int VisualRendererCount { get; private set; }
        public void Build(string condition)
        {
            disease=condition;
            skin=Mat("Skin cross-section",new Color(.53f,.32f,.235f),0,.28f);fat=Mat("Adipose cross-section",new Color(.77f,.57f,.27f),0,.3f);muscle=Mat("Intercostal muscle",new Color(.39f,.11f,.12f),0,.34f);bone=Mat("Rib cortex",new Color(.72f,.66f,.52f),0,.3f);artery=Mat("Arterial wall",new Color(.44f,.095f,.09f));nerve=Mat("Intercostal nerve",new Color(.80f,.66f,.35f));lung=Mat("Lung tissue",new Color(.46f,.235f,.26f),0,.34f);polymer=Mat("Medical polymer",new Color(.69f,.75f,.73f),0,.32f);steel=Mat("Satin stainless steel",new Color(.44f,.51f,.55f),.8f,.48f);dark=Mat("Graphite equipment housing",new Color(.035f,.052f,.066f),.2f,.28f);fluid=Mat("Pleural fluid",new Color(.59f,.40f,.16f),0,.56f);
            teal=Mat("Sterile teal detail",new Color(.12f,.32f,.31f),.12f,.35f);tissueLight=Mat("Tissue cut detail",new Color(.66f,.39f,.29f),0,.3f);vesselLining=Mat("Vessel inner wall",new Color(.70f,.30f,.26f),0,.38f);screen=Mat("Monitor display",new Color(.008f,.024f,.030f),0,.18f);vein=Mat("Intercostal vein",new Color(.12f,.19f,.29f),0,.36f);
            ApplyTissue(skin,"skin");ApplyTissue(fat,"fat");ApplyTissue(muscle,"muscle");ApplyTissue(lung,"lung");ApplyTissue(bone,"bone");
            manikin=Mat("Warm silicone training manikin",new Color(.72f,.49f,.38f),0,.25f);ApplyTissue(manikin,"skin");manikin.color=new Color(1.24f,1.25f,1.25f);
            lips=Mat("Muted silicone lip detail",new Color(.43f,.22f,.18f),0,.25f);crease=Mat("Soft facial recess",new Color(.20f,.105f,.081f),0,.16f);
            solid=new WorkbenchGeometry();geometry.Add(solid);
            BuildStation();
            if(condition=="pneumothorax"||condition=="pleural-effusion")BuildPleural();
            else if(condition=="coronary-disease"||condition=="aortic-stenosis")BuildVascular(condition=="aortic-stenosis");
            else BuildAirwayCare();
            BuildChart();
            solid.Build(transform,"Merged clinical workstation");
            foreach(var part in geometry){VisualTriangleCount+=part.TriangleCount;VisualRendererCount+=part.RendererCount;}
        }
        Material Mat(string title,Color color,float metallic=0,float smoothness=.4f){var m=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=title,color=color};m.SetFloat("_Metallic",metallic);m.SetFloat("_Smoothness",smoothness);materials.Add(m);return m;}
        static void ApplyTissue(Material material,string tissue)
        {
            var source=Resources.Load<Material>("Learning/Specimen/Tissue_"+tissue);
            if(!source)return;
            material.CopyPropertiesFromMaterial(source);
            // Small station meshes use continuous UVs; retain the existing subtle albedo.
            material.SetFloat("_Smoothness",.25f);material.DisableKeyword("_NORMALMAP");
        }
        Transform Group(string title,Action<WorkbenchGeometry> author)
        {
            var root=new GameObject(title).transform;root.SetParent(transform,false);var parts=new WorkbenchGeometry();geometry.Add(parts);author(parts);parts.Build(root,title);return root;
        }
        void Box(Vector3 p,Vector3 size,Material mat,float radius=.004f)=>solid.Box(p,size,radius,mat);
        void Tube(Vector3 a,Vector3 b,float diameter,Material mat)=>solid.Tube(new[]{a,b},diameter*.5f,mat);
        void Curve(Vector3 a,Vector3 b,Vector3 c,float diameter,Material mat)
        {var path=new Vector3[19];for(int i=0;i<path.Length;i++){float t=(float)i/(path.Length-1);path[i]=(1-t)*(1-t)*a+2*(1-t)*t*b+t*t*c;}solid.Tube(path,diameter*.5f,mat);}
        Transform ProtectedBox(string title,Vector3 p,Vector3 size,Quaternion rotation,string id)
        {var t=new GameObject(title).transform;t.SetParent(transform,false);t.localPosition=p;t.localScale=size;t.localRotation=rotation;Protect(t,id);return t;}
        void BuildStation()
        {
            var horizontal=Quaternion.Euler(90,0,0);
            solid.Box(new Vector3(0,-.196f,.04f),new Vector3(.54f,.38f,.027f),.024f,dark,horizontal);
            solid.Box(new Vector3(0,-.180f,.04f),new Vector3(.526f,.367f,.006f),.022f,steel,horizontal);
            solid.Box(new Vector3(0,-.176f,.04f),new Vector3(.506f,.347f,.006f),.018f,dark,horizontal);
            for(int side=-1;side<=1;side+=2)
            {
                solid.Box(new Vector3(side*.247f,-.170f,.043f),new Vector3(.007f,.272f,.006f),.002f,teal,horizontal);
                for(int end=-1;end<=1;end+=2)solid.Ellipsoid(new Vector3(side*.237f,-.214f,.04f+end*.145f),new Vector3(.027f,.012f,.029f),dark,segments:12,rings:6);
            }
            // Tissue inserts use mounting standards; the manikin has its own fitted plinth.
            if(disease!="asthma"&&disease!="pneumonia")
            {
                Box(new Vector3(-.11f,-.148f,.12f),new Vector3(.013f,.050f,.020f),steel);
                Box(new Vector3(.11f,-.148f,.12f),new Vector3(.013f,.050f,.020f),steel);
            }
            for(int side=-1;side<=1;side+=2)solid.Ring(new Vector3(side*.241f,-.173f,-.097f),Vector3.up,.004f,.001f,steel,12);
        }
        void Anchor(string id,Vector3 p,Vector3 forward)
        {var t=new GameObject(id).transform;t.SetParent(transform,false);t.localPosition=p;t.localRotation=Quaternion.LookRotation(forward,Mathf.Abs(Vector3.Dot(forward.normalized,Vector3.up))>.99f?Vector3.forward:Vector3.up);targets.Add("equipment:"+id,t);}
        void Route(string id,params Vector3[] points){routes["equipment:"+id]=points;}
        void Protect(Transform t,string id)
        {var b=t.gameObject.AddComponent<BoxCollider>();b.size=Vector3.one;var p=t.gameObject.AddComponent<ProcedureProtectedRegion>();p.anatomyId=id;}
        void BuildPleural()
        {
            // Authored, enlarged cutaway. The continuous contour and differentiated cut
            // surfaces show layer order without pretending to be a segmented patient scan.
            for(int side=-1;side<=1;side+=2)
            {
                float x=side*.093f;
                solid.TissueBand(x,-.0445f,-.0355f,skin);
                solid.TissueBand(x,-.035f,-.007f,fat);
                solid.TissueBand(x,-.006f,.010f,muscle);
                solid.TissueBand(x,.022f,.026f,polymer);
                // Lobules are on the adipose cut edge only, not scattered over intact skin.
                for(int row=0;row<15;row++)
                {
                    float y=-.102f+row*.0145f;
                    solid.Ellipsoid(new Vector3(x-side*.041f,y,-.019f),new Vector3(.008f,.012f,.022f),fat,segments:10,rings:6);
                    solid.Tube(new[]{new Vector3(x-side*.038f,y,.006f),new Vector3(x+side*.035f,y+.008f,.008f)},.00065f,tissueLight,6);
                }
            }
            foreach(float y in new[]{.066f,-.064f})
            {
                Curve(new Vector3(-.15f,y,.012f),new Vector3(0,y+.006f,-.003f),new Vector3(.15f,y,.012f),.025f,bone);
                foreach(int side in new[]{-1,1})
                {
                    solid.Ring(new Vector3(side*.15f,y,.012f),Vector3.right,.0118f,.0018f,bone,18);
                    solid.Ellipsoid(new Vector3(side*.15f,y,.012f),new Vector3(.001f,.013f,.013f),tissueLight,segments:12,rings:8);
                }
            }
            Tube(new Vector3(-.15f,.044f,.023f),new Vector3(.15f,.044f,.023f),.0045f,nerve);
            Tube(new Vector3(-.15f,.048f,.023f),new Vector3(.15f,.048f,.023f),.0035f,artery);
            Tube(new Vector3(-.15f,.052f,.023f),new Vector3(.15f,.052f,.023f),.004f,vein);
            // Keep the original conservative protection volumes exactly unchanged.
            ProtectedBox("Protected intercostal bundle",new Vector3(0,.044f,.023f),new Vector3(.011f,.15f,.011f),Quaternion.FromToRotation(Vector3.up,Vector3.right),disease=="pneumothorax"?"REC_Right_IC4_Nerve":"REC_Right_IC6_Nerve");
            ProtectedBox("Lung beyond the pleural space",new Vector3(0,0,.127f),new Vector3(.24f,.24f,.095f),Quaternion.identity,"MAL_RLL");
            ProtectedBox("Diaphragm below the pleural space",new Vector3(0,-.138f,.05f),new Vector3(.29f,.015f,.17f),Quaternion.identity,"FMA13295");
            solid.Ellipsoid(new Vector3(0,0,.127f),new Vector3(.24f,.24f,.095f),lung,segments:32,rings:20);
            Curve(new Vector3(-.087f,.080f,.100f),new Vector3(-.045f,.025f,.077f),new Vector3(.070f,-.057f,.090f),.0022f,muscle);
            solid.Ellipsoid(new Vector3(0,-.138f,.05f),new Vector3(.29f,.015f,.17f),muscle);
            if(disease=="pleural-effusion")
            {
                collection=Group("Pleural fluid pocket",g=>g.Box(Vector3.zero,Vector3.one,.15f,fluid));
                collection.localPosition=new Vector3(0,-.058f,.067f);collection.localScale=new Vector3(.21f,.08f,.018f);
            }
            field=Group("Fenestrated sterile drape",g=>
            {
                foreach(int side in new[]{-1,1})
                {
                    g.Box(new Vector3(side*.17f,0,-.055f),new Vector3(.04f,.25f,.006f),.009f,teal);
                    g.Tube(new[]{new Vector3(side*.181f,-.11f,-.060f),new Vector3(side*.176f,0,-.060f),new Vector3(side*.181f,.11f,-.060f)},.001f,polymer,6);
                }
                g.Box(new Vector3(0,.127f,-.055f),new Vector3(.36f,.018f,.006f),.004f,teal);
            });field.gameObject.SetActive(false);
            Anchor("pleural-site",new Vector3(0,-.014f,-.048f),Vector3.forward);
            Anchor("pleural-end",new Vector3(0,-.014f,.047f),Vector3.forward);
            Anchor("pleural-incision",new Vector3(.012f,-.014f,-.048f),Vector3.right);
            Route("pleural-incision",new Vector3(-.012f,-.014f,-.048f),new Vector3(0,-.014f,-.048f),new Vector3(.012f,-.014f,-.048f));
            Anchor("pleural-fixation",new Vector3(0,-.014f,-.055f),Vector3.down);
            Route("pleural-fixation",new Vector3(-.014f,-.014f,-.055f),new Vector3(0,.005f,-.055f),new Vector3(.014f,-.014f,-.055f),new Vector3(0,-.031f,-.055f),new Vector3(0,-.014f,-.055f));
            Route("pleural-end",new Vector3(0,-.014f,-.092f),new Vector3(0,-.014f,-.04f),new Vector3(0,-.014f,.014f),new Vector3(0,-.014f,.047f));
            Anchor("pleural-withdrawal",new Vector3(0,-.014f,-.092f),Vector3.forward);
            Route("pleural-withdrawal",new Vector3(0,-.014f,.047f),new Vector3(0,-.014f,.014f),new Vector3(0,-.014f,-.04f),new Vector3(0,-.014f,-.092f));
            BuildDrainCollection();
            Anchor("drain-circuit",new Vector3(.22f,-.030f,.09f),Vector3.down);Anchor("aspiration",new Vector3(.22f,-.030f,.09f),Vector3.down);
            retainedDevice=Group("Retained catheter and connected drain",g=>
            {
                g.Tube(new[]{new Vector3(0,-.014f,.047f),new Vector3(0,-.014f,-.085f)},.0045f,polymer,12);
                var hose=new Vector3[25];for(int i=0;i<hose.Length;i++){float t=(float)i/(hose.Length-1);hose[i]=Vector3.Lerp(new Vector3(0,-.014f,-.085f),new Vector3(.22f,-.05f,.09f),t)+Vector3.down*(Mathf.Sin(t*Mathf.PI)*.058f);}
                g.Tube(hose,.003f,polymer,10);
                g.Ring(new Vector3(0,-.014f,-.065f),Vector3.forward,.005f,.001f,teal,14);
            });retainedDevice.gameObject.SetActive(false);
            fixation=Group("Catheter holding suture",g=>g.Tube(new[]{new Vector3(-.014f,-.014f,-.055f),new Vector3(-.004f,-.010f,-.059f),new Vector3(.004f,-.018f,-.059f),new Vector3(.014f,-.014f,-.055f)},.001f,dark,6));fixation.gameObject.SetActive(false);
            BuildMonitor(new Vector3(-.20f,-.10f,.01f));
        }
        void BuildDrainCollection()
        {
            Box(new Vector3(.22f,-.12f,.09f),new Vector3(.075f,.12f,.070f),polymer,.013f);
            Box(new Vector3(.22f,-.175f,.09f),new Vector3(.082f,.013f,.078f),teal,.006f);
            Box(new Vector3(.22f,-.063f,.09f),new Vector3(.074f,.012f,.070f),teal,.006f);
            Box(new Vector3(.22f,-.118f,.053f),new Vector3(.058f,.086f,.003f),dark,.007f);
            Box(new Vector3(.22f,-.149f,.050f),new Vector3(.051f,.025f,.003f),fluid,.004f);
            for(int i=0;i<7;i++)Box(new Vector3(.242f,-.150f+i*.01f,.048f),new Vector3(i%2==0?.013f:.007f,.001f,.001f),polymer,.0003f);
            Tube(new Vector3(.22f,-.06f,.09f),new Vector3(.22f,-.035f,.09f),.012f,polymer);
            solid.Ring(new Vector3(.22f,-.039f,.09f),Vector3.up,.0075f,.0018f,steel,18);
            Tube(new Vector3(.201f,-.060f,.09f),new Vector3(.201f,-.051f,.09f),.008f,dark);
        }
        void BuildVascular(bool valve)
        {
            var points=valve?new[]{new Vector3(-.09f,-.145f,0),new Vector3(-.065f,-.09f,0),new Vector3(-.045f,-.03f,0),new Vector3(-.04f,.06f,0),new Vector3(-.04f,.125f,0),new Vector3(.015f,.16f,0),new Vector3(.06f,.145f,0),new Vector3(.065f,.09f,0)}:
                new[]{new Vector3(-.215f,-.135f,0),new Vector3(-.19f,-.04f,0),new Vector3(-.155f,.035f,0),new Vector3(-.10f,.085f,0),new Vector3(-.055f,.125f,0),new Vector3(.015f,.155f,0),new Vector3(.065f,.10f,0)};
            // This is an enlarged route demonstrator, with an open lumen and cut wall.
            // All authored interaction points still use the original fixed route frame.
            Box(new Vector3(0,0,.034f),new Vector3(.49f,.34f,.008f),dark,.025f);
            if(valve)VesselChannel(points,.017f,true);
            foreach(var p in points)
            {
                solid.Ring(p+Vector3.forward*.022f,Vector3.forward,.010f,.0018f,steel,18);
                Tube(p+Vector3.forward*.022f,p+Vector3.forward*.030f,.008f,steel);
            }
            var routeVisual=new List<Vector3>();foreach(var p in points)routeVisual.Add(p+Vector3.back*.025f);
            string target=valve?"aortic-valve":"coronary-origin";
            Anchor(target,routeVisual[routeVisual.Count-1],(points[points.Length-1]-points[points.Length-2]).normalized);Route(target,routeVisual.ToArray());
            var access=points[0]+Vector3.back*.025f;
            Box(points[0]+Vector3.forward*.017f,new Vector3(.075f,.066f,.015f),skin,.017f);
            solid.Ring(access+Vector3.forward*.008f,Vector3.forward,.008f,.0018f,polymer,20);
            Anchor(valve?"femoral-access":"radial-access",access,Vector3.forward);
            Anchor(valve?"valve-system":"catheter-system",access,Vector3.forward);
            if(!valve)
            {
                var coronary=new[]{routeVisual[routeVisual.Count-1],new Vector3(.105f,.075f,-.025f),new Vector3(.17f,.045f,-.025f)};
                var channel=new List<Vector3>(points){coronary[1]+Vector3.forward*.025f,coronary[2]+Vector3.forward*.025f};
                VesselChannel(channel.ToArray(),.017f,true,points.Length-1);
                Anchor("coronary-lesion",coronary[2],(coronary[2]-coronary[1]).normalized);Route("coronary-lesion",coronary);
                // Withdrawal begins at the treated lesion, not back at the coronary origin.
                routeVisual.Add(coronary[1]);routeVisual.Add(coronary[2]);
                var axis=(coronary[2]-coronary[1]).normalized;
                retainedDevice=Group("Deployed open coronary stent",g=>Scaffold(g,coronary[2]+Vector3.forward*.002f,axis,.01f,.048f,steel));
            }
            else
            {
                var position=routeVisual[routeVisual.Count-1];var axis=(points[points.Length-1]-points[points.Length-2]).normalized;
                solid.Ring(position+Vector3.forward*.017f,axis,.026f,.003f,bone,30);
                retainedDevice=Group("Valve replacement within native ring",g=>
                {
                    Scaffold(g,position,axis,.024f,.023f,steel);
                    var frame=Quaternion.FromToRotation(Vector3.forward,axis);
                    for(int leaflet=0;leaflet<3;leaflet++)
                    {
                        float angle=leaflet*120*Mathf.Deg2Rad;
                        g.Ellipsoid(position+frame*new Vector3(Mathf.Cos(angle)*.013f,Mathf.Sin(angle)*.013f,0),new Vector3(.018f,.012f,.003f),polymer,frame*Quaternion.Euler(0,0,leaflet*120),16,8);
                    }
                });
            }
            retainedDevice.gameObject.SetActive(false);
            routeVisual.Reverse();Anchor(valve?"valve-withdrawal":"catheter-withdrawal",routeVisual[routeVisual.Count-1],(routeVisual[routeVisual.Count-2]-routeVisual[routeVisual.Count-1]).normalized);Route(valve?"valve-withdrawal":"catheter-withdrawal",routeVisual.ToArray());
            seal=Group("Access compression / closure dressing",g=>
            {
                g.Box(points[0]+Vector3.back*.035f,new Vector3(.074f,.06f,.012f),.014f,polymer);
                g.Box(points[0]+Vector3.back*.043f,new Vector3(.044f,.031f,.004f),.006f,teal);
            });seal.gameObject.SetActive(false);
        }
        void VesselChannel(Vector3[] points,float radius,bool narrowAccess=false,int coronaryStart=-1)
        {
            // Shared cross-section rings at each bend keep the lumen continuous. The
            // interaction polyline is unchanged; its visual wall must not look severed.
            var outer=new float[points.Length];
            for(int i=0;i<points.Length;i++)
            {
                outer[i]=narrowAccess?Mathf.Lerp(.012f,radius,Mathf.Min(i/2f,1)):radius;
                if(coronaryStart>=0&&i>coronaryStart)outer[i]=.011f;
            }
            // Round only the visible corners, within 3 mm of the fixed interaction route.
            var contour=new List<Vector3>();var contourOuter=new List<float>();
            for(int i=0;i<points.Length;i++)
            {
                if(i==0||i==points.Length-1){contour.Add(points[i]);contourOuter.Add(outer[i]);continue;}
                float before=Vector3.Distance(points[i-1],points[i]),after=Vector3.Distance(points[i],points[i+1]);
                float reach=Mathf.Min(.008f,Mathf.Min(before,after)*.2f);
                var entry=Vector3.Lerp(points[i],points[i-1],reach/before);var exit=Vector3.Lerp(points[i],points[i+1],reach/after);
                for(int step=0;step<=6;step++)
                {
                    float t=step/6f;
                    contour.Add((1-t)*(1-t)*entry+2*(1-t)*t*points[i]+t*t*exit);
                    contourOuter.Add(Mathf.Lerp(Mathf.Lerp(outer[i],outer[i-1],reach/before),Mathf.Lerp(outer[i],outer[i+1],reach/after),t));
                }
            }
            var contourInner=new float[contour.Count];for(int i=0;i<contour.Count;i++)contourInner[i]=contourOuter[i]-.003f;
            solid.Tube(contour,radius,artery,24,180,radii:contourOuter);
            solid.Tube(contour,radius-.003f,vesselLining,24,180,true,contourInner);
            for(int side=-1;side<=1;side+=2)
            {
                var edge=new Vector3[contour.Count];
                for(int i=0;i<contour.Count;i++)
                {
                    var tangent=(contour[Mathf.Min(i+1,contour.Count-1)]-contour[Mathf.Max(i-1,0)]).normalized;
                    edge[i]=contour[i]+Vector3.Cross(Vector3.forward,tangent).normalized*(contourOuter[i]-.0015f)*side;
                }
                solid.Tube(edge,.0015f,vesselLining,6);
            }
        }
        static void Scaffold(WorkbenchGeometry g,Vector3 center,Vector3 axis,float radius,float length,Material mat)
        {
            var rotation=Quaternion.FromToRotation(Vector3.forward,axis);
            // Open zig-zag wire cage keeps the vessel lumen visible after deployment.
            for(int row=0;row<5;row++)
            {
                var wire=new Vector3[25];
                for(int i=0;i<wire.Length;i++){float a=i*2*Mathf.PI/24;wire[i]=center+rotation*new Vector3(Mathf.Cos(a)*radius,Mathf.Sin(a)*radius,Mathf.Lerp(-length*.5f,length*.5f,row/4f)+(i%2==0?-.0018f:.0018f));}
                g.Tube(wire,.00065f,mat,6);
            }
            for(int rib=0;rib<6;rib++)
            {
                float a=rib*Mathf.PI/3;var edge=rotation*new Vector3(Mathf.Cos(a)*radius,Mathf.Sin(a)*radius,0);
                g.Tube(new[]{center+edge-axis*length*.5f,center+edge+axis*length*.5f},.0005f,mat,5);
            }
        }
        void BuildAirwayCare()
        {
            // Illustrative silicone training bust; no patient anatomy or selection identity.
            solid.AirwayManikin(manikin);
            var horizontal=Quaternion.Euler(90,0,0);
            solid.Box(new Vector3(.10f,-.156f,.047f),new Vector3(.212f,.125f,.024f),.035f,dark,horizontal);
            solid.Box(new Vector3(.10f,-.143f,.047f),new Vector3(.201f,.115f,.004f),.030f,steel,horizontal);
            for(int side=-1;side<=1;side+=2)
            {
                solid.ManikinEar(side,manikin);
                var eyelid=new Vector3[25];
                for(int i=0;i<eyelid.Length;i++)
                {
                    float t=(float)i/(eyelid.Length-1),x=side*Mathf.Lerp(.014f,.046f,t),y=.078f-.0025f*Mathf.Sin(t*Mathf.PI);
                    eyelid[i]=WorkbenchGeometry.FacePoint(x,y)+Vector3.back*.0005f;
                }
                solid.Tube(eyelid,.00032f,crease,6);
                var nostril=WorkbenchGeometry.FacePoint(side*.010f,.042f)+Vector3.back*.0005f;
                solid.Ellipsoid(nostril,new Vector3(.006f,.0028f,.0018f),crease,Quaternion.Euler(-25,0,side*15),16,8);
            }
            solid.ManikinMouth(crease,lips);
            Anchor("mouthpiece",new Vector3(.10f,.012f,-.047f),Vector3.forward);
            if(disease=="asthma")
            {
                // Open inspection cutaway shows a hollow chamber and its valve; opaque
                // shell sections avoid expensive transparent sorting on standalone VR.
                solid.Tube(new[]{new Vector3(-.14f,-.105f,-.025f),new Vector3(-.14f,-.105f,.115f)},.0255f,polymer,22,255);
                solid.Tube(new[]{new Vector3(-.14f,-.105f,-.025f),new Vector3(-.14f,-.105f,.115f)},.0240f,teal,22,255,true);
                foreach(float z in new[]{-.024f,.087f,.112f})solid.Ring(new Vector3(-.14f,-.105f,z),Vector3.forward,.025f,.002f,polymer,30);
                solid.Ring(new Vector3(-.14f,-.105f,.091f),Vector3.forward,.017f,.0014f,teal,24);
                Tube(new Vector3(-.14f,-.105f,.112f),new Vector3(-.14f,-.105f,.132f),.029f,teal);
                Box(new Vector3(-.14f,-.137f,.043f),new Vector3(.070f,.017f,.099f),dark,.007f);
                for(int side=-1;side<=1;side+=2)Box(new Vector3(-.14f+side*.028f,-.118f,.043f),new Vector3(.007f,.035f,.065f),teal,.003f);
                Anchor("spacer",new Vector3(-.14f,-.105f,-.045f),Vector3.forward);
                Anchor("inhaler-cap",new Vector3(-.14f,-.09f,-.15f),Vector3.back);Route("inhaler-cap",new Vector3(-.14f,-.09f,-.065f),new Vector3(-.14f,-.09f,-.11f),new Vector3(-.14f,-.09f,-.15f));
                Anchor("inhaler-prep",new Vector3(-.14f,-.015f,-.08f),Vector3.forward);Route("inhaler-prep",new Vector3(-.14f,-.11f,-.08f),new Vector3(-.14f,-.015f,-.08f),new Vector3(-.14f,-.11f,-.08f),new Vector3(-.14f,-.015f,-.08f));
                cap=Group("Inhaler mouthpiece cap",g=>
                {
                    g.Box(new Vector3(-.14f,-.105f,-.05f),new Vector3(.052f,.039f,.02f),.012f,teal);
                    for(int i=0;i<5;i++)g.Box(new Vector3(-.157f+i*.0085f,-.105f,-.061f),new Vector3(.0012f,.020f,.001f),.0004f,dark);
                });
            }
            else
            {
                Box(new Vector3(-.16f,-.09f,.015f),new Vector3(.11f,.08f,.06f),polymer,.013f);
                Box(new Vector3(-.16f,-.09f,-.017f),new Vector3(.090f,.058f,.005f),dark,.008f);
                solid.Ring(new Vector3(-.16f,-.09f,-.026f),Vector3.forward,.012f,.003f,teal,24);
                solid.Ring(new Vector3(-.16f,-.09f,-.027f),Vector3.forward,.007f,.0013f,steel,20);
                Curve(new Vector3(-.19f,-.100f,.033f),new Vector3(-.12f,-.164f,.02f),new Vector3(-.08f,-.11f,.026f),.005f,polymer);
                Anchor("prescribed-care",new Vector3(-.16f,-.09f,-.027f),Vector3.forward);
                BuildMonitor(new Vector3(-.17f,.04f,.02f));
            }
        }
        void BuildChart()
        {
            var horizontal=Quaternion.Euler(90,0,0);
            solid.Box(new Vector3(0,-.165f,-.15f),new Vector3(.13f,.075f,.008f),.008f,steel,horizontal);
            solid.Box(new Vector3(0,-.1595f,-.15f),new Vector3(.119f,.065f,.003f),.003f,polymer,horizontal);
            solid.Box(new Vector3(0,-.156f,-.118f),new Vector3(.038f,.010f,.005f),.003f,dark,horizontal);
            // Physical document markings are decorative; the readable patient plan lives
            // on the current-step console, not microscopic text etched into equipment.
            solid.Box(new Vector3(-.011f,-.157f,-.130f),new Vector3(.069f,.003f,.001f),.0005f,teal,horizontal);
            for(int row=0;row<3;row++)
            {
                solid.Box(new Vector3(-.046f,-.157f,-.143f-row*.012f),new Vector3(.006f,.006f,.001f),.001f,teal,horizontal);
                solid.Box(new Vector3(.002f,-.157f,-.143f-row*.012f),new Vector3(.073f-row*.012f,.0015f,.001f),.0003f,dark,horizontal);
            }
            Anchor("chart",new Vector3(0,-.151f,-.15f),Vector3.down);
        }
        void BuildMonitor(Vector3 p)
        {
            Box(p,new Vector3(.085f,.091f,.030f),polymer,.010f);
            Box(p+Vector3.back*.016f,new Vector3(.076f,.083f,.003f),dark,.006f);
            Box(p+new Vector3(-.002f,.007f,-.018f),new Vector3(.063f,.052f,.002f),screen,.004f);
            // Static breath-shape icon, with no invented physiological number or live ECG.
            var breath=new Vector3[41];
            for(int i=0;i<breath.Length;i++){float t=(float)i/(breath.Length-1);breath[i]=p+new Vector3(-.030f+t*.055f,.008f+Mathf.Sin(t*Mathf.PI*4)*.010f,-.0193f);}
            solid.Tube(breath,.00065f,teal,5);
            for(int i=0;i<3;i++)solid.Ellipsoid(p+new Vector3(-.021f+i*.020f,-.029f,-.019f),new Vector3(.007f,.007f,.003f),i==2?teal:steel,segments:12,rings:6);
            Box(p+new Vector3(0,-.055f,.006f),new Vector3(.011f,.025f,.015f),steel,.003f);
            solid.Box(p+new Vector3(0,-.067f,.006f),new Vector3(.055f,.033f,.006f),.009f,dark,Quaternion.Euler(90,0,0));
            for(int i=0;i<5;i++)Box(p+new Vector3(.043f,-.015f+i*.007f,.003f),new Vector3(.001f,.002f,.014f),dark,.0003f);
            // A finger on a small rest gives the respiratory-check pointer the
            // anatomical contact described by the case, beside the monitor.
            var finger=p+new Vector3(.071f,-.030f,-.036f);
            solid.Ellipsoid(finger,new Vector3(.028f,.027f,.078f),skin,segments:20,rings:12);
            solid.Ellipsoid(finger+new Vector3(0,.012f,-.021f),new Vector3(.017f,.002f,.020f),tissueLight,segments:14,rings:8);
            Box(finger+new Vector3(0,-.020f,0),new Vector3(.042f,.015f,.081f),dark,.006f);
            Anchor("monitor",finger+Vector3.back*.040f,Vector3.forward);
        }
        public bool TryRoute(string id,List<Vector3> worldPoints)
        {if(!routes.TryGetValue(id,out var local))return false;foreach(var p in local)worldPoints.Add(transform.TransformPoint(p));return true;}
        public void UpdateCompleted(Func<string,bool> completed)
        {
            if(field)field.gameObject.SetActive(completed("swab"));
            if(cap)cap.gameObject.SetActive(!completed("cap"));
            if(retainedDevice)retainedDevice.gameObject.SetActive(completed(disease=="pneumothorax"?"drain":disease=="coronary-disease"?"stent":"valve"));
            if(seal)seal.gameObject.SetActive(completed("dressing"));
            if(fixation)fixation.gameObject.SetActive(completed("suture"));
        }
        public void SetResponse(float response){if(collection)collection.localScale=new Vector3(.21f,.08f*Mathf.Lerp(1,.25f,response),.018f);}
        void OnDestroy(){foreach(var part in geometry)part.Dispose();foreach(var m in materials)if(m)Destroy(m);}
    }
}
