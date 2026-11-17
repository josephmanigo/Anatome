using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace ThoraxLab.Learning
{
    public sealed class ProcedureTool : MonoBehaviour
    {
        public string toolId;
        public Transform tip;
        public XRGrabInteractable grab;
        public bool desktopHeld, activating;
        public bool Held => desktopHeld || (grab && grab.isSelected);
        public Vector3 Tip => tip ? tip.position : transform.position;
    }

    public sealed partial class ProcedureWorkbench : MonoBehaviour
    {
        LabRuntime app;
        GameObject workbench, marker, pathRoot, mechanismRoot, stationRoot, trayRoot;
        DiseaseTeachingModel teachingModel;
        CaseAnatomyWorkbench caseAnatomy;
        WorkbenchGeometry trayGeometry;
        readonly Dictionary<string, ProcedureTool> tools = new Dictionary<string, ProcedureTool>();
        readonly Dictionary<string,int> offeredSlots = new Dictionary<string,int>();
        readonly Dictionary<string, Transform> equipment = new Dictionary<string, Transform>();
        readonly Dictionary<string, Vector3> healthyScales = new Dictionary<string, Vector3>();
        readonly Collider[] contacts = new Collider[32];
        readonly RaycastHit[] sweptContacts=new RaycastHit[32];
        Vector3 previousTip, previousRouteTip;
        ProcedureTool sweepTool;
        readonly List<Vector3> route = new List<Vector3>();
        Material steel, teal, white, amber, rubber;
        string objectiveId;
        Vector3 targetPoint, approach, toolRest;
        float contactDuration, effect, desiredEffect;
        float tableHeight;
        bool hints;
        int routeIndex;
        bool connected, consumedGesture, lastActivation;
        ProcedureTool currentTool;
        LearningUnit unit;
        public ClinicalReasoningWorkbench Reasoning { get; private set; }
        public ProcedureTool DesktopTool { get; set; }
        public string InstrumentStatus { get; private set; } = "Select an instrument from the tray.";
        public string ResponseText { get; private set; } = "";
        public Vector3 TargetPoint => targetPoint;
        public Vector3 Approach => approach;
        public IReadOnlyList<Vector3> TrainingRoute => route;
        public IEnumerable<Renderer> VisibleRenderers => workbench?workbench.GetComponentsInChildren<Renderer>():Array.Empty<Renderer>();
        // Include the models used by the current case step, without its tray,
        // instruments, labels, dock or answer guidance.
        public IEnumerable<Renderer> InspectionRenderers
        {
            get
            {
                if(stationRoot&&stationRoot.activeInHierarchy)
                    foreach(var renderer in stationRoot.GetComponentsInChildren<MeshRenderer>())yield return renderer;
                if(mechanismRoot&&mechanismRoot.activeInHierarchy)
                    foreach(var renderer in mechanismRoot.GetComponentsInChildren<MeshRenderer>())yield return renderer;
                if(Reasoning&&Reasoning.gameObject.activeInHierarchy)
                    foreach(var model in Reasoning.GetComponentsInChildren<DiseaseTeachingModel>())
                        foreach(var renderer in model.GetComponentsInChildren<MeshRenderer>())yield return renderer;
            }
        }
        public IReadOnlyDictionary<string, ProcedureTool> Tools => tools;
        public bool Inspiration => Mathf.Sin(Time.time * 1.15f) > 0;

        public void Initialize(LabRuntime runtime)
        {
            app = runtime;
            steel = Material("Surgical stainless", new Color(.62f,.69f,.73f), .78f, .58f);
            teal = Material("Instrument teal", new Color(.10f,.48f,.46f), .12f,.38f);
            white = Material("Medical polymer", new Color(.9f,.92f,.86f),0,.28f);
            amber = Material("Learning target",new Color(.92f,.64f,.25f),0,.35f);
            rubber = Material("Instrument grip",new Color(.045f,.065f,.08f),0,.2f);
        }
        static Material Material(string name, Color color, float metallic, float smoothness)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name, color = color };
            m.SetFloat("_Metallic",metallic);m.SetFloat("_Smoothness",smoothness);return m;
        }
        public void Configure(LearningUnit value, ScenarioVariant variant)
        {
            Clear(); unit = value;
            if (!value.isCase) return;
            workbench = new GameObject("Procedure equipment");
            tableHeight=app.Session.specimen.position.y;
            Vector3 center = app.Session.specimen.position;
            var bounds = app.Structures[value.anatomyIds[0]].Renderer.bounds;
            center = new Vector3(0, bounds.center.y + .02f, bounds.center.z);
            // Fixed table-space working area. This stays separate from the left console
            // and never follows head movement while an instrument route is in progress.
            Vector3 tableRight=new Vector3(.42f,0,.91f).normalized;
            Vector3 tableNear=new Vector3(.91f,0,-.42f).normalized;
            Vector3 stationPosition=center+tableRight*.50f+Vector3.up*.14f;
            toolRest=stationPosition+tableNear*.21f-Vector3.up*.12f;
            BuildInstrumentTray(toolRest-Vector3.up*.06f);
            foreach (string id in variant.objectives.Where(o=>o.action!=ActionKind.ClinicalDecision).Select(o=>o.toolId).Where(t=>!string.IsNullOrEmpty(t)).Distinct())
            {
                var tool = BuildTool(id); tool.transform.SetParent(workbench.transform, true);
                tool.transform.position=toolRest;
                tool.transform.rotation=Quaternion.Euler(65,0,0); tools.Add(id,tool);
            }
            stationRoot=new GameObject("Enlarged case anatomy / access model");stationRoot.transform.SetParent(workbench.transform,false);stationRoot.transform.position=stationPosition;
            caseAnatomy=stationRoot.AddComponent<CaseAnatomyWorkbench>();caseAnatomy.Build(unit.disease);
            var reasoningRoot=new GameObject("Clinical reasoning choices");reasoningRoot.transform.SetParent(workbench.transform,false);Reasoning=reasoningRoot.AddComponent<ClinicalReasoningWorkbench>();reasoningRoot.SetActive(false);
            foreach(var pair in caseAnatomy.Targets)equipment.Add(pair.Key,pair.Value);
            BuildMechanism(center+tableRight*.58f-tableNear*.34f+Vector3.up*.28f);
            foreach(var id in value.anatomyIds) if(app.Structures.TryGetValue(id,out var s))healthyScales[id]=s.transform.localScale;
            ObjectiveCompleted();
            RefreshPresentation();
        }
        void BuildInstrumentTray(Vector3 position)
        {
            // Visual-only insert with the original tray origin and outer dimensions.
            // Tool rest positions, grab colliders, working ends and route rules are unchanged.
            trayRoot=new GameObject("Current instrument tray");
            trayRoot.transform.SetParent(workbench.transform,false);trayRoot.transform.position=position;
            trayGeometry=new WorkbenchGeometry();var horizontal=Quaternion.Euler(90,0,0);
            trayGeometry.Box(new Vector3(0,-.0055f,0),new Vector3(.22f,.24f,.014f),.016f,steel,horizontal);
            trayGeometry.Box(new Vector3(0,.002f,0),new Vector3(.198f,.218f,.003f),.010f,rubber,horizontal);
            foreach(int side in new[]{-1,1})
            {
                trayGeometry.Box(new Vector3(side*.105f,.007f,0),new Vector3(.010f,.213f,.011f),.003f,steel,horizontal);
                trayGeometry.Box(new Vector3(0,.007f,side*.115f),new Vector3(.196f,.010f,.011f),.003f,steel,horizontal);
                trayGeometry.Box(new Vector3(0,.0055f,side*.047f),new Vector3(.066f,.011f,.004f),.004f,rubber,horizontal);
                foreach(int end in new[]{-1,1})trayGeometry.Box(new Vector3(side*.083f,.0045f,end*.093f),new Vector3(.018f,.003f,.0015f),.0007f,teal,horizontal);
            }
            trayGeometry.Build(trayRoot.transform,"Merged recessed instrument tray");
        }
        ProcedureTool BuildTool(string id)
        {
            var go = new GameObject(FriendlyTool(id)); var tool=go.AddComponent<ProcedureTool>();tool.toolId=id;
            GameObject Part(string title,PrimitiveType type,Vector3 position,Vector3 scale,Material mat)=>Shape(go.transform,title,type,position,scale,mat,false);
            GameObject Shaft(string title,float z,float diameter,float length,Material mat){var p=Part(title,PrimitiveType.Cylinder,new Vector3(0,0,z),new Vector3(diameter,length*.5f,diameter),mat);p.transform.localRotation=Quaternion.Euler(90,0,0);return p;}
            Vector3 tip=new Vector3(0,0,.142f);
            if(id=="inhaler")
            {
                Part("L-shaped actuator",PrimitiveType.Cube,new Vector3(0,0,.02f),new Vector3(.041f,.071f,.038f),teal);
                Part("Pressurised canister",PrimitiveType.Cylinder,new Vector3(0,.032f,.021f),new Vector3(.032f,.040f,.032f),steel);
                Part("Canister press surface",PrimitiveType.Cylinder,new Vector3(0,.074f,.021f),new Vector3(.034f,.003f,.034f),white);
                Part("Open mouthpiece",PrimitiveType.Cube,new Vector3(0,-.022f,.056f),new Vector3(.034f,.024f,.047f),teal);
                Part("Mouthpiece aperture",PrimitiveType.Cube,new Vector3(0,-.022f,.081f),new Vector3(.024f,.015f,.002f),rubber);tip=new Vector3(0,-.022f,.084f);
            }
            else if(id=="spacer")
            {
                Shaft("Valved holding chamber",.048f,.055f,.14f,white);Shaft("Rear inhaler gasket",-.025f,.058f,.015f,teal);Shaft("One-way valve housing",.118f,.043f,.011f,teal);
                Part("Mouthpiece",PrimitiveType.Cube,new Vector3(0,0,.139f),new Vector3(.036f,.022f,.033f),white);tip=new Vector3(0,0,.159f);
            }
            else if(id=="oxygen")
            {
                Part("Contoured oxygen mask",PrimitiveType.Sphere,new Vector3(0,0,.033f),new Vector3(.092f,.125f,.06f),white);
                Part("Nose bridge",PrimitiveType.Cube,new Vector3(0,.044f,.052f),new Vector3(.031f,.012f,.013f),steel);
                Part("Elastic strap",PrimitiveType.Cube,new Vector3(0,0,-.004f),new Vector3(.142f,.009f,.005f),teal);
                Shaft("Oxygen inlet",-.022f,.018f,.042f,teal);tip=new Vector3(0,0,.067f);
            }
            else if(id=="cap"||id=="dressing")
            {
                Part(id=="cap"?"Removable inhaler cap":"Sterile fixation pad",PrimitiveType.Cube,Vector3.zero,id=="cap"?new Vector3(.037f,.025f,.027f):new Vector3(.085f,.064f,.009f),id=="cap"?teal:white);
                if(id=="dressing")for(int i=0;i<7;i++)Part("Woven pad detail",PrimitiveType.Cube,new Vector3(-.03f+i*.01f,0,.005f),new Vector3(.001f,.047f,.001f),teal);
                tip=new Vector3(0,0,id=="cap"?.016f:.008f);
            }
            else if(id=="ultrasound"||id=="probe")
            {
                Part("Ergonomic sensor body",PrimitiveType.Capsule,Vector3.zero,new Vector3(.025f,.043f,.023f),white).transform.localRotation=Quaternion.Euler(90,0,0);
                Part(id=="ultrasound"?"Curved ultrasound face":"Assessment sensor face",PrimitiveType.Cube,new Vector3(0,0,.063f),new Vector3(id=="ultrasound"?.057f:.03f,.020f,.027f),teal);
                Shaft("Cable strain relief",-.052f,.012f,.035f,rubber);tip=new Vector3(0,0,.079f);
            }
            else if(id=="swab")
            {
                Shaft("Applicator handle",.027f,.008f,.135f,white);Part("Antiseptic foam tip",PrimitiveType.Sphere,new Vector3(0,0,.112f),new Vector3(.033f,.024f,.042f),amber);tip=new Vector3(0,0,.135f);
            }
            else if(id=="scalpel"||id=="suture")
            {
                Part(id=="scalpel"?"Flat scalpel handle":"Needle holder",PrimitiveType.Cube,new Vector3(0,0,.008f),new Vector3(.012f,.006f,.11f),steel);
                Part(id=="scalpel"?"Short incision blade":"Fine suture needle",PrimitiveType.Cube,new Vector3(0,0,.085f),new Vector3(id=="scalpel"?.014f:.002f,.002f,.042f),steel);
                for(int i=0;i<5;i++)Part("Grip ridge",PrimitiveType.Cube,new Vector3(0,.004f,-.025f+i*.011f),new Vector3(.014f,.002f,.003f),rubber);tip=new Vector3(0,0,.108f);
            }
            else if(id=="connector")
            {
                Shaft("Luer connector",.042f,.021f,.10f,white);Shaft("Locking collar",.052f,.031f,.016f,teal);
                Part("Tap control",PrimitiveType.Cube,new Vector3(.02f,.012f,.02f),new Vector3(.064f,.013f,.013f),teal);tip=new Vector3(0,0,.095f);
            }
            else if(id=="anaesthetic"||id=="needle"||id=="aspirator"||id=="medication")
            {
                Shaft("Syringe barrel",.025f,.026f,.077f,white);Shaft("Plunger rod",-.029f,.010f,.04f,steel);
                Part("Thumb flange",PrimitiveType.Cube,new Vector3(0,0,-.054f),new Vector3(.040f,.014f,.006f),teal);
                Part("Finger flange",PrimitiveType.Cube,new Vector3(0,0,-.009f),new Vector3(.047f,.012f,.005f),white);
                for(int i=0;i<6;i++)Part("Graduation",PrimitiveType.Cube,new Vector3(0,.0135f,.004f+i*.009f),new Vector3(i%2==0?.015f:.009f,.001f,.0015f),rubber);
                Shaft(id=="aspirator"?"Aspiration catheter":"Needle / prepared administration port",.100f,id=="aspirator"?.006f:.002f,.07f,steel);
            }
            else
            {
                Shaft(id=="valve"?"Valve deployment control":"Catheter grip",-.006f,id=="valve"?.035f:.019f,.085f,rubber);
                Shaft("Delivery shaft",.070f,id=="guidewire"?.0018f:id=="drain"?.008f:.005f,.14f,id=="drain"?white:steel);
                Shaft("Control collar",.016f,.027f,.013f,teal);
                if(id=="drain")for(int i=0;i<4;i++)Part("Catheter fenestration",PrimitiveType.Sphere,new Vector3(0,.004f,.093f+i*.01f),new Vector3(.003f,.0015f,.005f),rubber);
                if(id=="dilator")Part("Short tapered dilator",PrimitiveType.Capsule,new Vector3(0,0,.080f),new Vector3(.012f,.034f,.012f),white).transform.localRotation=Quaternion.Euler(90,0,0);
                if(id=="balloon")Part("Deflated balloon",PrimitiveType.Capsule,new Vector3(0,0,.112f),new Vector3(.010f,.022f,.010f),white).transform.localRotation=Quaternion.Euler(90,0,0);
                if(id=="stent"||id=="valve")for(int i=0;i<7;i++)Shaft("Crimped scaffold ring",.09f+i*.006f,id=="valve"?.022f:.011f,.0015f,steel);
                if(id=="guidewire")Part("Soft wire tip",PrimitiveType.Sphere,new Vector3(.002f,0,.139f),Vector3.one*.003f,white);
            }
            var collider=go.AddComponent<BoxCollider>();var renderers=go.GetComponentsInChildren<Renderer>();var bounds=new Bounds(Vector3.zero,Vector3.one*.02f);foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);collider.center=bounds.center;collider.size=bounds.size+Vector3.one*.015f;
            // Keep the validated grab volume, origin and working endpoint, then replace only display geometry.
            foreach(Transform oldVisual in go.transform){oldVisual.gameObject.SetActive(false);Destroy(oldVisual.gameObject);}
            string visual=id=="dressing"&&unit.disease=="coronary-disease"?"radial-band":id;
            InstrumentGeometry.Create(go.transform,visual);
            if(id=="medication")tip=new Vector3(0,0,.081f);
            var bodyRigid=go.AddComponent<Rigidbody>();bodyRigid.isKinematic=true;bodyRigid.useGravity=false;
            tool.grab=go.AddComponent<XRGrabInteractable>();tool.grab.movementType=XRBaseInteractable.MovementType.Instantaneous;tool.grab.throwOnDetach=false;
            tool.grab.useDynamicAttach=true;tool.grab.matchAttachPosition=true;tool.grab.matchAttachRotation=true;tool.grab.snapToColliderVolume=false;
            tool.grab.unparentTransformOnGrab=false;
            tool.tip=new GameObject("Working end").transform;tool.tip.SetParent(go.transform,false);tool.tip.localPosition=tip;
            AddToolLabel(tool);
            return tool;
        }
        static GameObject Shape(Transform parent,string name,PrimitiveType type,Vector3 position,Vector3 scale,Material material,bool world)
        {
            var go=GameObject.CreatePrimitive(type);go.name=name;go.transform.SetParent(parent,false);go.transform.localScale=scale;
            if(world)go.transform.position=position;else go.transform.localPosition=position;
            go.GetComponent<Renderer>().sharedMaterial=material;
            var col=go.GetComponent<Collider>();if(col)Destroy(col);return go;
        }
        void BuildMechanism(Vector3 position)
        {
            mechanismRoot=new GameObject("Enlarged mechanism diagram");mechanismRoot.transform.SetParent(workbench.transform,false);mechanismRoot.transform.position=position;
            // Explicit schematic geometry, separate from the registered anatomical specimen.
            teachingModel=mechanismRoot.AddComponent<DiseaseTeachingModel>();teachingModel.Build(unit.disease);
        }
        public void ObjectiveCompleted()
        {
            contactDuration=0;routeIndex=0;consumedGesture=true;
            if(app.Engine==null)return;
            connected=app.Engine.Attempt.objectives.Any(result=>result.completed&&app.Variant.objectives.Any(o=>o.id==result.objectiveId&&o.action==ActionKind.EquipmentConnect));
            bool Done(string tool,ActionKind? kind=null)=>app.Variant.objectives.Any(o=>o.toolId==tool&&(!kind.HasValue||o.action==kind.Value)&&app.Engine.IsComplete(o.id));
            // Preparation is not treatment. The disease response follows the condition's actual intervention.
            desiredEffect=unit.disease=="pneumothorax"?Done("drain")&&connected?1:0:
                unit.disease=="pleural-effusion"?Done("aspirator",ActionKind.ToolActivate)?1:0:
                unit.disease=="pneumonia"?Done("medication",ActionKind.ToolActivate)?1:0:
                unit.disease=="asthma"?app.Variant.objectives.Any(o=>o.equipmentState=="inspiration"&&app.Engine.IsComplete(o.id))?1:0:
                unit.disease=="coronary-disease"?Done("stent",ActionKind.ToolActivate)?1:Done("balloon",ActionKind.ToolActivate)?.45f:0:
                Done("valve",ActionKind.ToolActivate)?1:0;
            if(caseAnatomy)caseAnatomy.UpdateCompleted(tool=>Done(tool,tool=="valve"||tool=="stent"?ActionKind.ToolActivate:(ActionKind?)null));
            SetTarget();
        }
        public void RestoreCheckpoint(){ObjectiveCompleted();effect=desiredEffect;}
        void SetTarget()
        {
            var objective=app.Engine?.Current;objectiveId=objective?.id;
            offeredSlots.Clear();
            if(objective!=null){var offered=tools.Keys.Where(t=>t!=objective.toolId).Take(2).Append(objective.toolId).Where(t=>t!=null).ToArray();for(int i=0;i<offered.Length;i++)offeredSlots[offered[i]]=(i+app.Engine.Attempt.id[0])%offered.Length;}
            foreach(var tool in tools.Values)if(!tool.Held)tool.gameObject.SetActive(false);
            if(marker)Destroy(marker);if(pathRoot)Destroy(pathRoot);marker=null;pathRoot=null;route.Clear();
            toolLabels.RemoveAll(label=>!label);
            if(objective==null||unit==null||!unit.isCase)return;
            bool decision=objective.action==ActionKind.ClinicalDecision;
            if(Reasoning){Reasoning.gameObject.SetActive(decision);if(decision){Reasoning.Configure(app,objective,stationRoot.transform.position+new Vector3(.91f,0,-.42f).normalized*.28f);return;}}
            if(equipment.TryGetValue(objective.targetId,out var socket)){targetPoint=socket.position;approach=socket.forward;}
            else if(app.Structures.TryGetValue(objective.targetId,out var s))
            {
                var direction=app.Session.specimen.forward;Vector3 from=s.Renderer.bounds.center-direction*(s.Renderer.bounds.extents.magnitude+.25f);var ray=new Ray(from,direction);
                var collider=s.GetComponent<Collider>();
                if(collider&&collider.Raycast(ray,out var hit,10)){targetPoint=hit.point;approach=-hit.normal;}else{targetPoint=s.Renderer.bounds.center;approach=(targetPoint-from).normalized;}
            }
            else return;
            if(!caseAnatomy||!caseAnatomy.TryRoute(objective.targetId,route))
            {route.Add(targetPoint-approach*.10f);route.Add(targetPoint-approach*.05f);route.Add(targetPoint);}
            marker=Shape(workbench.transform,"Procedure alignment target",PrimitiveType.Sphere,targetPoint,Vector3.one*.014f,amber,true);
            AddAlignmentGuide(marker.transform);
            pathRoot=new GameObject("Ordered instrument trajectory");pathRoot.transform.SetParent(workbench.transform,false);
            foreach(var point in route)Shape(pathRoot.transform,"Trajectory checkpoint",PrimitiveType.Sphere,point,Vector3.one*.008f,teal,true);
            AddRouteLabels();
            bool guide=app.Engine.Attempt.phase!=SessionPhase.Assessment && app.Variant.difficulty==Difficulty.Easy && app.Settings.Current.hints;
            hints=app.Settings.Current.hints;
            marker.SetActive(guide);pathRoot.SetActive(guide&&objective.action==ActionKind.Trace);
            RefreshNeededTool();
        }
        void RefreshNeededTool()
        {
            string needed=app.Engine?.Current?.toolId;
            bool independent=app.Variant.difficulty==Difficulty.Hard||app.Engine.Attempt.phase==SessionPhase.Assessment;
            foreach(var pair in tools)
            {
                bool decision=app.Engine.Current?.action==ActionKind.ClinicalDecision;
                bool show=(!decision&&(pair.Key==needed||(independent&&offeredSlots.ContainsKey(pair.Key))))||pair.Value.Held;
                if(show&&!pair.Value.gameObject.activeSelf){
                    int slot=independent&&offeredSlots.TryGetValue(pair.Key,out var position)?position:1;
                    pair.Value.transform.position=toolRest+Vector3.right*((slot-1)*.075f);pair.Value.transform.rotation=Quaternion.Euler(65,0,0);
                    RestoreWithdrawalTool(pair.Value);
                }
                if(pair.Value.gameObject.activeSelf!=show)pair.Value.gameObject.SetActive(show);
            }
        }
        void RestoreWithdrawalTool(ProcedureTool tool)
        {
            var objective=app.Engine?.Current;
            if(objective==null||tool.Held||tool.toolId!=objective.toolId||!objective.targetId.EndsWith("withdrawal",StringComparison.Ordinal))return;
            var prior=app.Engine.Attempt.actions.LastOrDefault(a=>a.toolId==tool.toolId&&a.localPosition.Length==3&&a.localRotation.Length==4&&app.Engine.Attempt.objectives.Any(o=>o.completed&&o.actionId==a.id));
            if(prior==null)return;
            // Evidence stores the working end, not the instrument's transform origin.
            var tip=app.Session.specimen.TransformPoint(new Vector3(prior.localPosition[0],prior.localPosition[1],prior.localPosition[2]));
            tool.transform.rotation=app.Session.specimen.rotation*new Quaternion(prior.localRotation[0],prior.localRotation[1],prior.localRotation[2],prior.localRotation[3]);
            tool.transform.position+=tip-tool.Tip;
        }
        void RefreshPresentation()
        {
            if(!workbench)return;
            var shift=Vector3.up*(app.Session.specimen.position.y-tableHeight);
            if(shift.sqrMagnitude>0)
            {
                tableHeight=app.Session.specimen.position.y;workbench.transform.position+=shift;
                toolRest+=shift;targetPoint+=shift;previousTip+=shift;previousRouteTip+=shift;completedTip+=shift;
                for(int i=0;i<route.Count;i++)route[i]+=shift;
                foreach(var line in workbench.GetComponentsInChildren<LineRenderer>(true))if(line.useWorldSpace)
                    for(int i=0;i<line.positionCount;i++)line.SetPosition(i,line.GetPosition(i)+shift);
            }
            bool visible=app&&app.UI&&app.UI.WorkspaceVisible&&unit!=null&&unit.isCase;
            if(workbench.activeSelf!=visible)workbench.SetActive(visible);
            if(!visible)return;
            var objective=app.Engine?.Current;
            if(hints!=app.Settings.Current.hints)
            {
                hints=app.Settings.Current.hints;
                bool guide=hints&&app.Engine.Attempt.phase!=SessionPhase.Assessment&&app.Variant.difficulty==Difficulty.Easy;
                if(marker)marker.SetActive(guide);if(pathRoot)pathRoot.SetActive(guide&&objective?.action==ActionKind.Trace);
            }
            bool decision=objective?.action==ActionKind.ClinicalDecision;
            bool usesStation=objective!=null&&!decision&&objective.targetId.StartsWith("equipment:",StringComparison.Ordinal);
            if(stationRoot&&stationRoot.activeSelf!=usesStation)stationRoot.SetActive(usesStation);
            bool usesTool=(!decision&&!string.IsNullOrEmpty(objective?.toolId))||tools.Values.Any(t=>t.Held);
            if(trayRoot&&!trayRoot.activeSelf)trayRoot.SetActive(true);
            bool demonstration=app.Demonstrator&&app.Demonstrator.Playing;
            bool showsResponse=objective?.action==ActionKind.Reassess||demonstration;
            if(mechanismRoot&&mechanismRoot.activeSelf!=showsResponse)mechanismRoot.SetActive(showsResponse);
            RefreshNeededTool();
            // Anatomy selection has its own highlight; instrument guides belong to tool steps.
            if(!usesTool){if(marker)marker.SetActive(false);if(pathRoot)pathRoot.SetActive(false);}
        }
        public void ShowHint(){if(app.Engine?.Current?.action==ActionKind.ClinicalDecision){Reasoning.ShowHint();return;}if(marker)marker.SetActive(true);if(pathRoot)pathRoot.SetActive(app.Engine?.Current?.action==ActionKind.Trace);}
        bool ClearApproach(Vector3 target,Vector3 direction)
        {
            for(int step=0;step<=12;step++)
            {
                var point=target-direction*(step*.01f);int count=Physics.OverlapSphereNonAlloc(point,.003f,contacts);
                for(int i=0;i<count;i++){var s=contacts[i].GetComponentInParent<AnatomyStructure>();if(s&&unit.protectedIds.Contains(s.record.id))return false;}
            }
            return true;
        }
        void Update()
        {
            RefreshPresentation();
            if(app?.Engine==null||unit==null||!unit.isCase||!app.CanInteract){contactDuration=0;lastActivation=false;return;}
            var objective=app.Engine.Current;if(objective==null){AnimateResponse();return;}
            if(objective.id!=objectiveId)SetTarget();
            if(objective.action==ActionKind.ClinicalDecision){AnimateResponse();return;}
            RefreshNeededTool();
            currentTool=tools.Values.FirstOrDefault(t=>t.Held&&t.activating)??tools.Values.FirstOrDefault(t=>t.Held)??DesktopTool;
            bool needsTrigger=objective.action==ActionKind.ToolActivate;
            bool active=currentTool&&(needsTrigger?currentTool.activating:currentTool.Held);
            // A completed physical gesture cannot complete the next step at the same pose.
            // A new tool, a fresh grip or deliberate movement starts the next contact/route.
            if(!needsTrigger&&consumedGesture&&currentTool&&(currentTool!=completedTool||Vector3.Distance(currentTool.Tip,completedTip)>.025f))consumedGesture=false;
            if(active&&(!lastActivation||sweepTool!=currentTool)){previousTip=currentTool.Tip;sweepTool=currentTool;}
            if(!active){consumedGesture=false;if(lastActivation)contactDuration=0;}lastActivation=active;
            AnimateResponse();
            if(!currentTool)return;
            InstrumentStatus=FriendlyTool(currentTool.toolId)+(unit.disease=="asthma"?Inspiration?" · patient breathing in":" · patient breathing out":"");
            if(!active||consumedGesture||objective.action<ActionKind.EquipmentConnect)return;
            if(currentTool.toolId!="probe"&&currentTool.toolId!="connector"&&currentTool.toolId!="inhaler"&&currentTool.toolId!="spacer"&&currentTool.toolId!="oxygen"&&currentTool.toolId!="medication"&&CheckProtectedContact(currentTool)){consumedGesture=true;return;}
            float distance=Vector3.Distance(currentTool.Tip,targetPoint);
            float angle=Vector3.Angle(currentTool.transform.forward,approach);
            bool correctTool=currentTool.toolId==objective.toolId;
            if(!correctTool){if(currentTool.activating||distance<=objective.maxDistance){contactDuration+=Time.deltaTime;if(contactDuration>=.8f){Submit(objective,currentTool,distance,angle,0);consumedGesture=true;}}else contactDuration=0;return;}
            if(objective.action==ActionKind.Trace)
            {
                // A stationary tip cannot consume adjacent checkpoints whose capture regions overlap.
                float segment=routeIndex>0&&routeIndex<route.Count?Vector3.Distance(route[routeIndex-1],route[routeIndex]):.05f;
                bool moved=routeIndex==0||Vector3.Distance(currentTool.Tip,previousRouteTip)>=Mathf.Min(.012f,segment*.4f);
                if(routeIndex<route.Count&&moved&&Vector3.Distance(currentTool.Tip,route[routeIndex])<=Mathf.Min(.025f,objective.maxDistance)){previousRouteTip=currentTool.Tip;routeIndex++;}
                float progress=(float)routeIndex/route.Count;
                InstrumentStatus=FriendlyTool(currentTool.toolId)+" · route "+routeIndex+" / "+route.Count;
                if(CheckProtectedContact(currentTool)){consumedGesture=true;return;}
                if(progress<.95f)return;
            }
            // Do not penalize a learner while waiting for the represented inhalation phase.
            bool phaseReady=objective.equipmentState!="inspiration"||connected&&Inspiration;
            if(distance<=objective.maxDistance&&angle<=objective.maxAngle&&phaseReady)contactDuration+=Time.deltaTime;else contactDuration=0;
            if(contactDuration>=objective.minContactSeconds)
            {
                if((objective.action==ActionKind.ToolContact||objective.action==ActionKind.ToolActivate)&&CheckProtectedContact(currentTool)){consumedGesture=true;return;}
                Submit(objective,currentTool,distance,angle,objective.action==ActionKind.Trace?(float)routeIndex/route.Count:0);consumedGesture=true;
            }
        }
        void Submit(ObjectiveDefinition objective,ProcedureTool tool,float distance,float angle,float pathFraction)
        {
            completedTool=tool;completedTip=tool.Tip;
            string state=objective.action==ActionKind.EquipmentConnect?"connected":objective.equipmentState=="ready"?"ready":objective.equipmentState=="inspiration"?(connected&&Inspiration?"inspiration":"not-ready"):connected?"connected":"disconnected";
            var point=app.Session.specimen.InverseTransformPoint(tool.Tip);var rotation=Quaternion.Inverse(app.Session.specimen.rotation)*tool.transform.rotation;
            app.Emit(new InteractionAction {kind=objective.action,targetId=objective.targetId,toolId=tool.toolId,equipmentState=state,contactVerified=true,distance=distance,angle=angle,contactSeconds=contactDuration,pathFraction=pathFraction,localPosition=new[]{point.x,point.y,point.z},localRotation=new[]{rotation.x,rotation.y,rotation.z,rotation.w}});
        }
        bool CheckProtectedContact(ProcedureTool tool)
        {
            var movement=tool.Tip-previousTip;int swept=movement.sqrMagnitude>.0000001f?Physics.SphereCastNonAlloc(previousTip,.003f,movement.normalized,sweptContacts,movement.magnitude,~0,QueryTriggerInteraction.Ignore):0;previousTip=tool.Tip;
            for(int i=0;i<swept;i++)if(RecordProtected(sweptContacts[i].collider,tool))return true;
            int count=Physics.OverlapSphereNonAlloc(tool.Tip,.003f,contacts);
            for(int i=0;i<count;i++)
            {
                if(RecordProtected(contacts[i],tool))return true;
            }
            return false;
        }
        bool RecordProtected(Collider collider,ProcedureTool tool)
        {
            var original=collider.GetComponentInParent<AnatomyStructure>();var supplementary=collider.GetComponentInParent<ProcedureProtectedRegion>();
            string id=original?original.record.id:supplementary?supplementary.anatomyId:null;
            if(string.IsNullOrEmpty(id)||!unit.protectedIds.Contains(id))return false;
            app.Emit(new InteractionAction{kind=ActionKind.ProtectedStructureViolation,targetId=id,toolId=tool.toolId,contactVerified=true});return true;
        }
        public static string FriendlyTool(string id)
        {switch(id){case "probe":return "Assessment pointer";case "anaesthetic":return "Local anaesthetic syringe";case "guidewire":return "Guidewire";case "dilator":return "Short dilator";case "drain":return "Chest drain";case "connector":return "Circuit connector";case "dressing":return "Fixation dressing";case "aspirator":return "Aspiration catheter";case "medication":return "Prescribed medicine";case "oxygen":return "Oxygen mask";case "valve":return "Valve delivery handle";default:return string.IsNullOrEmpty(id)?"":char.ToUpperInvariant(id[0])+id.Substring(1);}}
        void AnimateResponse()
        {
            if(app.Engine.Attempt.criticalFailure){desiredEffect=0;ResponseText="Recorded preservation failure · stop and reassess.";if(teachingModel)teachingModel.SetResponse(0,true);return;}
            effect=Mathf.MoveTowards(effect,desiredEffect,Time.deltaTime*(unit.disease=="pneumonia"?.05f:.2f));
            ResponseText="Enlarged schematic · "+(effect<.01f?"initial disease state":effect<desiredEffect-.01f?unit.disease=="pneumonia"?"days of recovery compressed":"response time compressed":unit.response);
            if(teachingModel)teachingModel.SetResponse(effect,app.Settings.Current.reducedMotion);
            if(caseAnatomy)caseAnatomy.SetResponse(effect);
            if(unit.disease=="pneumothorax")foreach(var pair in healthyScales)if(pair.Key.StartsWith("MAL_R")&&app.Structures.TryGetValue(pair.Key,out var lung))
                lung.transform.localScale=Vector3.Scale(pair.Value,new Vector3(Mathf.Lerp(.7f,1f,effect),Mathf.Lerp(.65f,1f,effect),Mathf.Lerp(.8f,1f,effect)));
        }
        public void Clear()
        {
            foreach(var pair in healthyScales)if(app&&app.Structures.TryGetValue(pair.Key,out var s)&&s)s.transform.localScale=pair.Value;
            trayGeometry?.Dispose();trayGeometry=null;
            healthyScales.Clear();if(workbench){workbench.SetActive(false);Destroy(workbench);}workbench=null;Reasoning=null;stationRoot=null;trayRoot=null;tools.Clear();equipment.Clear();route.Clear();toolLabels.Clear();unit=null;DesktopTool=null;currentTool=completedTool=null;marker=null;pathRoot=null;mechanismRoot=null;effect=desiredEffect=0;connected=false;ResponseText="";objectiveId=null;
        }
        void OnDestroy(){Clear();foreach(var material in new[]{steel,teal,white,amber,rubber})if(material)Destroy(material);}
    }
}
