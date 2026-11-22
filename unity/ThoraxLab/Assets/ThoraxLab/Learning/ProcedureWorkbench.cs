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
        Material steel, teal, amber, rubber;
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
        public CaseIncisionSurface Incision => caseAnatomy ? caseAnatomy.Incision : null;
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
            steel = InstrumentGeometry.FinishMaterial(InstrumentGeometry.Satin);
            teal = InstrumentGeometry.FinishMaterial(InstrumentGeometry.Teal);
            amber = Material("Learning target",new Color(.92f,.64f,.25f),0,.35f);
            rubber = InstrumentGeometry.FinishMaterial(InstrumentGeometry.Ink);
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
            // Rest positions use the tray origin in table space.
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
            trayGeometry.Build(trayRoot.transform,"Merged recessed instrument tray",steel,teal,rubber);
        }
        ProcedureTool BuildTool(string id)
        {
            var go = new GameObject(FriendlyTool(id)); var tool=go.AddComponent<ProcedureTool>();tool.toolId=id;
            string visualId = id == "dressing" && unit.disease == "coronary-disease" ? "radial-band" : id;
            var visual = InstrumentGeometry.Create(go.transform, visualId);
            var bounds = new Bounds(Vector3.zero, Vector3.one * .02f);
            bounds.Encapsulate(visual.GetComponent<MeshFilter>().sharedMesh.bounds);
            // Grip the mask itself. The open space enclosed by its head strap is not a handle.
            if(id=="oxygen")bounds=new Bounds(new Vector3(0,0,.031f),new Vector3(.105f,.122f,.076f));
            var collider = go.AddComponent<BoxCollider>();
            collider.center = bounds.center;
            collider.size = bounds.size + Vector3.one * .015f;
            Vector3 tip = id switch
            {
                "scalpel" => Vector3.forward * InstrumentGeometry.ScalpelTipZ,
                "suture" => new Vector3(-.006f, .0016f, .1072f),
                "guidewire" => new Vector3(.004f, 0, .140f),
                "needle" or "anaesthetic" => new Vector3(.00085f, 0, .142f),
                "aspirator" => new Vector3(.0028f, 0, .142f),
                "inhaler" => new Vector3(0, -.022f, .07985f),
                "spacer" => Vector3.forward * .1574f,
                "oxygen" => Vector3.forward * InstrumentGeometry.OxygenSealZ,
                "cap" => Vector3.forward * CaseAnatomyWorkbench.CapTipZ,
                "dressing" => Vector3.forward * (visualId=="radial-band"?.008f:.0072f),
                "ultrasound" or "probe" => Vector3.forward * .0789f,
                "swab" => Vector3.forward * .135f,
                "connector" => Vector3.forward * .095f,
                "medication" => Vector3.forward * .081f,
                "balloon" or "stent" or "valve" => Vector3.forward * .141f,
                _ => Vector3.forward * .142f
            };
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
            pathRoot=new GameObject("Ordered instrument trajectory");pathRoot.transform.SetParent(workbench.transform,false);
            if(objective.toolId=="scalpel"&&Incision&&objective.action==ActionKind.Trace){
                marker=new GameObject("Skin insert guide");marker.transform.SetParent(workbench.transform,false);
                foreach(var point in new[]{route[0],route[route.Count-1]})
                    Shape(marker.transform,"Incision margin tick",PrimitiveType.Cube,point+Incision.transform.up*.007f-Incision.transform.forward*.002f,new Vector3(.0013f,.004f,.0013f),teal,true);
            }
            else{
                marker=Shape(workbench.transform,"Procedure alignment target",PrimitiveType.Sphere,targetPoint,Vector3.one*.014f,amber,true);
                AddAlignmentGuide(marker.transform);
                foreach(var point in route)Shape(pathRoot.transform,"Trajectory checkpoint",PrimitiveType.Sphere,point,Vector3.one*.008f,teal,true);
                AddRouteLabels();
            }
            bool guide=app.Engine.Attempt.phase!=SessionPhase.Assessment && app.Variant.difficulty==Difficulty.Easy && app.Settings.Current.hints;
            hints=app.Settings.Current.hints;
            marker.SetActive(guide);pathRoot.SetActive(guide&&objective.action==ActionKind.Trace);
            RefreshNeededTool();
        }
        void RefreshNeededTool()
        {
            var objective=app.Engine?.Current;
            string needed=objective?.toolId;
            bool decision=objective?.action==ActionKind.ClinicalDecision;
            bool independent=app.Variant.difficulty==Difficulty.Hard||app.Engine.Attempt.phase==SessionPhase.Assessment;
            foreach(var pair in tools)
            {
                bool show=(!decision&&(pair.Key==needed||(independent&&offeredSlots.ContainsKey(pair.Key))))||pair.Value.Held;
                if(show&&!pair.Value.gameObject.activeSelf){
                    int slot=independent&&offeredSlots.TryGetValue(pair.Key,out var position)?position:1;
                    pair.Value.transform.position=toolRest+Vector3.right*((slot-1)*.075f);pair.Value.transform.rotation=Quaternion.Euler(65,0,0);
                    PositionRetainedTool(pair.Value);
                }
                if(pair.Value.gameObject.activeSelf!=show)pair.Value.gameObject.SetActive(show);
            }
        }
        void PositionRetainedTool(ProcedureTool tool)
        {
            var objective=app.Engine?.Current;
            if(objective==null||tool.Held||tool.toolId!=objective.toolId)return;
            if(tool.toolId=="cap"&&route.Count>0){
                caseAnatomy.HideFittedCap();
                tool.transform.rotation=Quaternion.LookRotation(approach,Vector3.up);
                tool.transform.position=route[0]-tool.transform.rotation*tool.tip.localPosition;
                return;
            }
            if(!objective.targetId.EndsWith("withdrawal",StringComparison.Ordinal))return;
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
            bool usesTool=(!decision&&!string.IsNullOrEmpty(objective?.toolId))||HeldTool();
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
            if(app?.Engine==null||unit==null||!unit.isCase||!app.CanInteract){contactDuration=0;lastActivation=false;if(Incision&&Incision.Touching)Incision.BreakContact();return;}
            var objective=app.Engine.Current;if(objective==null){AnimateResponse();return;}
            if(objective.id!=objectiveId)SetTarget();
            if(objective.action==ActionKind.ClinicalDecision){AnimateResponse();return;}
            currentTool=HeldTool()??DesktopTool;
            bool needsTrigger=objective.action==ActionKind.ToolActivate;
            bool active=currentTool&&(needsTrigger?currentTool.activating:currentTool.Held);
            // A completed physical gesture cannot complete the next step at the same pose.
            // A new tool, a fresh grip or deliberate movement starts the next contact/route.
            if(!needsTrigger&&consumedGesture&&currentTool&&(currentTool!=completedTool||Vector3.Distance(currentTool.Tip,completedTip)>.025f))consumedGesture=false;
            if(active&&(!lastActivation||sweepTool!=currentTool))
            {
                previousTip=currentTool.Tip;contactDuration=0;
                if(sweepTool!=currentTool&&Incision)Incision.BreakContact();
                sweepTool=currentTool;
            }
            if(!active){consumedGesture=false;contactDuration=0;if(Incision&&Incision.Touching)Incision.BreakContact();}
            lastActivation=active;
            Vector3 sweepStart=previousTip;
            if(currentTool)previousTip=currentTool.Tip;
            AnimateResponse();
            if(!currentTool)return;
            InstrumentStatus=FriendlyTool(currentTool.toolId)+(unit.disease=="asthma"?Inspiration?" · patient breathing in":" · patient breathing out":"");
            if(!active||consumedGesture||objective.action<ActionKind.EquipmentConnect)return;
            bool invasive=currentTool.toolId!="probe"&&currentTool.toolId!="connector"&&currentTool.toolId!="inhaler"&&currentTool.toolId!="spacer"&&currentTool.toolId!="oxygen"&&currentTool.toolId!="medication";
            if(invasive&&CheckProtectedContact(currentTool,sweepStart)){if(Incision)Incision.BreakContact();consumedGesture=true;return;}
            float distance=Vector3.Distance(currentTool.Tip,targetPoint);
            float angle=Vector3.Angle(currentTool.transform.forward,approach);
            bool correctTool=currentTool.toolId==objective.toolId;
            float radius=ContactRadius(objective,currentTool.toolId);
            if(!correctTool){if(currentTool.activating||distance<=radius){contactDuration+=Time.deltaTime;if(contactDuration>=.8f){Submit(objective,currentTool,distance,angle,0);consumedGesture=true;}}else contactDuration=0;return;}
            if(objective.action==ActionKind.Trace&&currentTool.toolId=="scalpel"&&Incision)
            {
                var skin=Incision;
                bool finished=skin.Sample(currentTool.Tip,currentTool.transform.rotation,objective.maxAngle,Time.deltaTime);
                contactDuration=skin.ContactSeconds;
                if(finished&&contactDuration>=objective.minContactSeconds)
                {
                    Submit(objective,currentTool,distance,angle,skin.Progress);
                    consumedGesture=true;
                }
                return;
            }
            if(objective.action==ActionKind.Trace)
            {
                // A stationary tip cannot consume adjacent checkpoints whose capture regions overlap.
                float segment=routeIndex>0&&routeIndex<route.Count?Vector3.Distance(route[routeIndex-1],route[routeIndex]):.05f;
                bool moved=routeIndex==0||Vector3.Distance(currentTool.Tip,previousRouteTip)>=Mathf.Min(.012f,segment*.4f);
                if(routeIndex<route.Count&&moved)
                {
                    var step=route[routeIndex];var delta=currentTool.Tip-sweepStart;
                    var nearest=currentTool.Tip;
                    if(delta.sqrMagnitude>1e-10f&&delta.sqrMagnitude<=.045f*.045f)
                        nearest=sweepStart+delta*Mathf.Clamp01(Vector3.Dot(step-sweepStart,delta)/delta.sqrMagnitude);
                    if(Vector3.Distance(nearest,step)<=Mathf.Min(.014f,objective.maxDistance)){previousRouteTip=currentTool.Tip;routeIndex++;}
                }
                float progress=(float)routeIndex/route.Count;
                InstrumentStatus=FriendlyTool(currentTool.toolId)+" · route "+routeIndex+" / "+route.Count;
                if(!invasive&&CheckProtectedContact(currentTool,sweepStart)){consumedGesture=true;return;}
                if(progress<.95f)return;
            }
            // Do not penalize a learner while waiting for the represented inhalation phase.
            bool phaseReady=objective.equipmentState!="inspiration"||connected&&Inspiration;
            if(distance<=radius&&angle<=objective.maxAngle&&phaseReady)contactDuration+=Mathf.Min(.1f,Time.deltaTime);else contactDuration=0;
            if(contactDuration>=objective.minContactSeconds)
            {
                if(!invasive&&(objective.action==ActionKind.ToolContact||objective.action==ActionKind.ToolActivate)&&CheckProtectedContact(currentTool,sweepStart)){consumedGesture=true;return;}
                Submit(objective,currentTool,distance,angle,objective.action==ActionKind.Trace?(float)routeIndex/route.Count:0);consumedGesture=true;
            }
        }
        ProcedureTool HeldTool()
        {
            ProcedureTool first=null;
            foreach(var tool in tools.Values)if(tool.Held){
                if(tool.activating)return tool;
                if(!first)first=tool;
            }
            return first;
        }
        void Submit(ObjectiveDefinition objective,ProcedureTool tool,float distance,float angle,float pathFraction)
        {
            completedTool=tool;completedTip=tool.Tip;
            string state=objective.action==ActionKind.EquipmentConnect?"connected":objective.equipmentState=="ready"?"ready":objective.equipmentState=="inspiration"?(connected&&Inspiration?"inspiration":"not-ready"):connected?"connected":"disconnected";
            var point=app.Session.specimen.InverseTransformPoint(tool.Tip);var rotation=Quaternion.Inverse(app.Session.specimen.rotation)*tool.transform.rotation;
            app.Emit(new InteractionAction {kind=objective.action,targetId=objective.targetId,toolId=tool.toolId,equipmentState=state,contactVerified=true,distance=distance,angle=angle,contactSeconds=contactDuration,pathFraction=pathFraction,localPosition=new[]{point.x,point.y,point.z},localRotation=new[]{rotation.x,rotation.y,rotation.z,rotation.w}});
        }
        static float ContactRadius(ObjectiveDefinition objective,string toolId)
        {
            if(!objective.targetId.StartsWith("equipment:",StringComparison.Ordinal))return objective.maxDistance;
            float patch=toolId switch
            {
                "oxygen" or "dressing" => .025f,
                "swab" or "spacer" or "ultrasound" => .016f,
                _ => .010f
            };
            return Mathf.Min(objective.maxDistance,patch);
        }
        bool CheckProtectedContact(ProcedureTool tool,Vector3 from)
        {
            var movement=tool.Tip-from;int swept=movement.sqrMagnitude>.0000001f?Physics.SphereCastNonAlloc(from,.003f,movement.normalized,sweptContacts,movement.magnitude,~0,QueryTriggerInteraction.Ignore):0;
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
            completedTool=tool;completedTip=tool.Tip;
            app.Emit(new InteractionAction{kind=ActionKind.ProtectedStructureViolation,targetId=id,toolId=tool.toolId,contactVerified=true});return true;
        }
        public static string FriendlyTool(string id)
        {switch(id){case "probe":return "Assessment pointer";case "anaesthetic":return "Local anaesthetic syringe";case "guidewire":return "Guidewire";case "dilator":return "Short dilator";case "drain":return "Chest drain";case "connector":return "Circuit connector";case "dressing":return "Fixation dressing";case "aspirator":return "Aspiration catheter";case "medication":return "Prescribed medicine";case "oxygen":return "Oxygen mask";case "valve":return "Valve delivery handle";default:return string.IsNullOrEmpty(id)?"":char.ToUpperInvariant(id[0])+id.Substring(1);}}
        void AnimateResponse()
        {
            bool failed=app.Engine.Attempt.criticalFailure;
            if(failed){effect=desiredEffect=0;ResponseText="Recorded preservation failure · stop and reassess.";}
            else{
                effect=Mathf.MoveTowards(effect,desiredEffect,Time.deltaTime*(unit.disease=="pneumonia"?.05f:.2f));
                ResponseText="Enlarged schematic · "+(effect<.01f?"initial disease state":effect<desiredEffect-.01f?unit.disease=="pneumonia"?"days of recovery compressed":"response time compressed":unit.response);
            }
            if(teachingModel)teachingModel.SetResponse(effect,failed||app.Settings.Current.reducedMotion);
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
        void OnDestroy(){Clear();if(amber)Destroy(amber);}
    }
}
