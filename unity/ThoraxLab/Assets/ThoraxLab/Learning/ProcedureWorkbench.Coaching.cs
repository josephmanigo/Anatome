using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

namespace ThoraxLab.Learning
{
    public sealed partial class ProcedureWorkbench
    {
        ProcedureTool completedTool;
        Vector3 completedTip;
        readonly List<TextMeshPro> toolLabels=new();
        bool Guided=>app.Engine.Attempt.phase!=SessionPhase.Assessment;
        string ToolName(string id)=>id=="dressing"&&unit?.disease=="coronary-disease"?"Radial compression band":id=="suture"?"Needle holder":id=="medication"?"Prepared IV medicine":FriendlyTool(id);
        public string StepFeedback
        {
            get
            {
                var objective=app.Engine?.Current;if(objective==null||unit?.isCase!=true)return "";
                if(objective.action==ActionKind.ClinicalDecision)return Reasoning?Reasoning.PlacementFeedback:"Grip a choice and move it to the answer dock.";
                if(objective.action==ActionKind.InspectFinding||objective.action==ActionKind.Select)return "Point at the anatomy on the specimen. Press trigger once to select it.";
                var held=tools.Values.FirstOrDefault(t=>t.Held&&t.activating)??tools.Values.FirstOrDefault(t=>t.Held);
                string needed=Guided?ToolName(objective.toolId):"a tool for your plan";
                if(!held)return objective.targetId.EndsWith("withdrawal",System.StringComparison.Ordinal)?"Hold GRIP on the retained "+needed+" at the model, then move it back along the route.":"Hold GRIP to pick up "+needed+" from the tray.";
                if(held.toolId!=objective.toolId)return Guided?"Holding "+ToolName(held.toolId)+". Return it, then grip "+needed+".":"Holding "+ToolName(held.toolId)+". Match the tool to your plan before using it.";
                if(objective.action==ActionKind.Trace&&routeIndex<route.Count)return ToolName(held.toolId)+" · move the working end through the route in order.\n"+(Guided?"Next point: "+(routeIndex+1)+" of "+route.Count+". ":"")+"Keep holding grip. No trigger needed.";
                float distance=Vector3.Distance(held.Tip,targetPoint),angle=Vector3.Angle(held.transform.forward,approach);
                if(distance>objective.maxDistance)return ToolName(held.toolId)+" · move the working end to "+TargetName(objective.targetId)+".";
                if(angle>objective.maxAngle)return "Working end is at the target. Rotate your wrist to align the tool with the entry direction.";
                if(objective.equipmentState=="inspiration"&&!Inspiration)return "Keep the mouthpiece sealed. Wait for the virtual patient to breathe in.";
                if(consumedGesture)return "Step recorded. Release grip, or move away before the next contact.";
                if(objective.action==ActionKind.ToolActivate&&!held.activating)return ToolName(held.toolId)+" aligned. Hold TRIGGER to operate it.";
                return "Keep steady · "+Mathf.Min(100,Mathf.RoundToInt(contactDuration/Mathf.Max(.01f,objective.minContactSeconds)*100))+"%"+(objective.equipmentState=="inspiration"?" · breathing in":"");
            }
        }
        string TargetName(string id)
        {
            if(!Guided)return "the location required by this step";
            if(app.Structures.TryGetValue(id,out var anatomy))return anatomy.record.name+" on the specimen";
            switch(id){
                case "equipment:chart":return "the patient chart beside the model";
                case "equipment:monitor":return "the monitoring station";
                case "equipment:mouthpiece":return "the manikin's mouth";
                case "equipment:spacer":return "the spacer's rear connection";
                case "equipment:prescribed-care":return "the prepared IV port";
                default:return "the target on the enlarged model";
            }
        }
        void AddToolLabel(ProcedureTool tool)
        {
            var label=WorldLabel(tool.transform,ToolName(tool.toolId),new Vector3(0,.045f,-.06f),.17f);
            label.gameObject.name="Instrument name";toolLabels.Add(label);
            var end=Shape(tool.transform,"Working end marker",PrimitiveType.Sphere,tool.tip.localPosition,Vector3.one*.005f,teal,false);
            end.GetComponent<Renderer>().shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        TextMeshPro WorldLabel(Transform parent,string text,Vector3 position,float width)
        {
            var label=new GameObject(text).AddComponent<TextMeshPro>();label.transform.SetParent(parent,false);label.transform.localPosition=position;
            label.font=LabWidgets.Font;label.text=text;label.fontSize=.14f*app.Settings.Current.textScale;label.color=LabWidgets.Ink;
            label.alignment=TextAlignmentOptions.Center;label.textWrappingMode=TextWrappingModes.Normal;label.richText=false;
            label.rectTransform.sizeDelta=new Vector2(width,.06f);return label;
        }
        void AddAlignmentGuide(Transform parent)
        {
            var line=new GameObject("Tool direction guide").AddComponent<LineRenderer>();line.transform.SetParent(parent,false);
            line.sharedMaterial=Resources.Load<Material>("Learning/GuidanceLine");line.positionCount=2;line.startWidth=.005f;line.endWidth=.001f;
            line.SetPositions(new[]{targetPoint-approach*.065f,targetPoint});
            // A small open ring and a tapered axis show where the working end enters.
            var ring=new GameObject("Working end alignment ring").AddComponent<LineRenderer>();ring.transform.SetParent(parent,false);
            ring.sharedMaterial=line.sharedMaterial;ring.startWidth=ring.endWidth=.0015f;ring.positionCount=25;
            var rotation=Quaternion.LookRotation(approach);for(int i=0;i<25;i++){float a=i*Mathf.PI/12;ring.SetPosition(i,targetPoint+rotation*new Vector3(Mathf.Cos(a)*.014f,Mathf.Sin(a)*.014f,0));}
        }
        void AddRouteLabels()
        {
            for(int i=0;i<route.Count;i++){
                var label=WorldLabel(pathRoot.transform,(i+1).ToString(),Vector3.zero,.035f);
                label.transform.position=route[i]+Vector3.up*.018f;toolLabels.Add(label);
            }
        }
        void LateUpdate()
        {
            if(!app||!Camera.main)return;
            foreach(var label in toolLabels)if(label){label.transform.rotation=Camera.main.transform.rotation;label.fontSize=.14f*app.Settings.Current.textScale;}
        }
        public void ReturnLooseTools()
        {
            foreach(var pair in tools)if(!pair.Value.Held){
                int slot=offeredSlots.TryGetValue(pair.Key,out var value)?value:1;
                pair.Value.transform.SetPositionAndRotation(toolRest+Vector3.right*((slot-1)*.075f),Quaternion.Euler(65,0,0));
                pair.Value.activating=false;
                RestoreWithdrawalTool(pair.Value);
            }
            app.Message("Loose tools returned to the tray. Release a held tool before returning it.");
        }
    }
}
