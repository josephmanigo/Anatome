using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace ThoraxLab.Learning
{
    public sealed partial class GuidedInteractionCoach
    {
        GameObject dissectionGuide;
        LineRenderer startRing, sweepLine, directionArrow;
        TextMeshPro guideLabel;
        Material guideMaterial;
        Material guideBackingMaterial;
        Mesh guideBackingMesh;
        Transform guideBacking;
        readonly Vector3[] guidePoints=new Vector3[7];
        Mesh guideMesh;
        string guideObjective;
        Vector3 guideObserver, guideTargetPosition, guideNormal;
        float nextGuideProbe;
        bool hasCutGuide;
        public string DissectionFeedback {get;private set;}

        void UpdateDissectionGuide()
        {
            var objective=app?.Engine?.Current;
            bool visible=objective?.action==ActionKind.Expose&&app.Variant?.mode==LearningMode.GuidedDissection&&
                app.Engine.Attempt.phase!=SessionPhase.Assessment&&app.Settings.Current.hints&&app.CanInteract&&!app.Session.busy;
            if(!visible){if(dissectionGuide)dissectionGuide.SetActive(false);DissectionFeedback=null;hasCutGuide=false;nextGuideProbe=0;return;}
            if(!app.Structures.TryGetValue(objective.targetId,out var target)||!Camera.main)return;
            EnsureDissectionGuide();
            AnatomyStructure chunk=null;
            foreach(var fragment in app.Session.Fragments)
                if(fragment&&fragment.gameObject.activeInHierarchy&&fragment.cutObjectiveId==objective.id&&fragment.record.id==objective.targetId){chunk=fragment;break;}
            var camera=Camera.main.transform;
            Vector3 start,end,normal;
            if(chunk)
            {
                hasCutGuide=false;start=chunk.Renderer.bounds.center;end=start+Vector3.up*.08f;normal=-camera.forward;
                sweepLine.positionCount=2;sweepLine.SetPosition(0,start);sweepLine.SetPosition(1,end);
                DissectionFeedback=chunk.Grab.isSelected?"Lift the chunk clear, then release grip.":"Grip the marked chunk, lift clear, then release.";
                guideLabel.text=chunk.Grab.isSelected?"2 · Lift, then release":"2 · Grip and lift";
            }
            else
            {
                var mesh=target.Filter.sharedMesh;
                if(guideObjective!=objective.id||guideMesh!=mesh||guideTargetPosition!=target.transform.position||
                    (Time.unscaledTime>=nextGuideProbe&&(!hasCutGuide||(guideObserver-camera.position).sqrMagnitude>.0025f)))
                {
                    hasCutGuide=app.Presentation.TryGetCutGuide(target,objective.region,camera.position,guidePoints,out guideNormal);
                    guideObjective=objective.id;guideMesh=mesh;guideObserver=camera.position;guideTargetPosition=target.transform.position;nextGuideProbe=Time.unscaledTime+.75f;
                }
                if(!hasCutGuide){dissectionGuide.SetActive(false);DissectionFeedback="Move around the specimen to see the named region. The guide needs a clear tissue surface.";return;}
                normal=guideNormal;start=guidePoints[0]+normal*.006f;end=guidePoints[guidePoints.Length-1]+normal*.006f;
                // Leave a small gap after the start ring so its shape stays distinct.
                sweepLine.positionCount=guidePoints.Length-2;
                for(int i=2;i<guidePoints.Length;i++)sweepLine.SetPosition(i-2,guidePoints[i]+normal*.006f);
                DissectionFeedback="Start at the ring. Hold trigger, sweep along the arrow, then release. The guide does not cut for you.";
                guideLabel.text="1 · Hold trigger and sweep";
            }
            dissectionGuide.SetActive(true);
            var across=Vector3.Cross(normal,(end-start).normalized).normalized;
            if(across.sqrMagnitude<.1f)across=camera.right;
            var ringCenter=start+(camera.position-start).normalized*.03f;
            for(int i=0;i<24;i++){float angle=i*Mathf.PI*2/24;startRing.SetPosition(i,ringCenter+(camera.right*Mathf.Cos(angle)+camera.up*Mathf.Sin(angle))*.007f);}
            var back=end-(end-start).normalized*.007f;
            directionArrow.SetPosition(0,back+across*.005f);directionArrow.SetPosition(1,end);directionArrow.SetPosition(2,back-across*.005f);
            guideLabel.transform.position=end+camera.up*.06f;guideLabel.transform.rotation=camera.rotation;
            guideLabel.fontSize=.14f*app.Settings.Current.textScale;
            guideLabel.color=app.Settings.Current.highContrast?Color.white:LabWidgets.Ink;
            guideBacking.localScale=new Vector3(Mathf.Min(.35f,guideLabel.preferredWidth+.02f),Mathf.Max(.035f,guideLabel.preferredHeight+.012f),1);
            guideBackingMaterial.SetColor("_BaseColor",app.Settings.Current.highContrast?Color.black:LabWidgets.Ground);
        }

        void EnsureDissectionGuide()
        {
            if(dissectionGuide)return;
            dissectionGuide=new GameObject("Dissection visual guide - no collision or scoring");
            dissectionGuide.transform.SetParent(transform,false);
            guideMaterial=new Material(Resources.Load<Material>("Learning/GuidanceLine"));
            // A closed billboard line reverses strip winding around its loop.
            guideMaterial.SetFloat("_Cull",0);
            startRing=GuideLine("Start ring",24);startRing.loop=true;
            sweepLine=GuideLine("Sweep or lift route",7);directionArrow=GuideLine("Direction arrow",3);
            guideLabel=new GameObject("Dissection guide instruction").AddComponent<TextMeshPro>();
            guideLabel.transform.SetParent(dissectionGuide.transform,false);guideLabel.font=LabWidgets.Font;
            guideLabel.alignment=TextAlignmentOptions.Center;guideLabel.rectTransform.sizeDelta=new Vector2(.32f,.07f);
            // A solid backing keeps instructions readable over both skin and the room.
            guideBacking=new GameObject("Guide instruction backing").transform;guideBacking.SetParent(guideLabel.transform,false);guideBacking.localPosition=Vector3.forward*.003f;
            guideBackingMesh=new Mesh{name="Guide label backing",vertices=new[]{new Vector3(-.5f,-.5f,0),new Vector3(-.5f,.5f,0),new Vector3(.5f,.5f,0),new Vector3(.5f,-.5f,0)},triangles=new[]{0,1,2,0,2,3}};
            guideBackingMesh.RecalculateBounds();guideBacking.gameObject.AddComponent<MeshFilter>().sharedMesh=guideBackingMesh;
            guideBackingMaterial=new Material(Resources.Load<Material>("Learning/GuidanceLine"));
            var backingRenderer=guideBacking.gameObject.AddComponent<MeshRenderer>();backingRenderer.sharedMaterial=guideBackingMaterial;backingRenderer.shadowCastingMode=ShadowCastingMode.Off;backingRenderer.receiveShadows=false;
        }
        LineRenderer GuideLine(string name,int count)
        {
            var line=new GameObject(name).AddComponent<LineRenderer>();line.transform.SetParent(dissectionGuide.transform,false);
            line.sharedMaterial=guideMaterial;line.useWorldSpace=true;line.positionCount=count;line.startWidth=line.endWidth=.002f;
            line.shadowCastingMode=ShadowCastingMode.Off;line.receiveShadows=false;return line;
        }
        void ClearDissectionGuide()
        {
            if(dissectionGuide){dissectionGuide.SetActive(false);Destroy(dissectionGuide);}if(guideMaterial)Destroy(guideMaterial);
            if(guideBackingMaterial)Destroy(guideBackingMaterial);if(guideBackingMesh)Destroy(guideBackingMesh);
            dissectionGuide=null;guideMaterial=null;guideMesh=null;guideObjective=null;hasCutGuide=false;DissectionFeedback=null;
        }
    }
}
