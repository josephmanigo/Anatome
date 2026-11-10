using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ThoraxLab.Learning
{
    // Contact evidence is independent of the selected mesh's boolean cut result.
    // A rejected cut or Undo must not erase an activated blade crossing a nerve.
    public sealed class DissectionContactMonitor
    {
        readonly Collider[] overlaps = new Collider[64];
        readonly RaycastHit[] sweep = new RaycastHit[64];
        readonly HashSet<string> recorded = new HashSet<string>();
        string attemptId;
        bool hasPrevious;
        Vector3 previous;
        public bool StrokeBlocked { get; private set; }
        public void EndStroke() { hasPrevious = false; StrokeBlocked=false; }

        public void Sample(LabRuntime app, Vector3 center, Quaternion rotation, bool saw)
        {
            if (!app || !app.CanUseCuttingTool || app.Engine == null || app.Variant?.mode != LearningMode.GuidedDissection || app.Session.busy)
            { EndStroke(); return; }
            if (attemptId != app.Engine.Attempt.id) { attemptId = app.Engine.Attempt.id; recorded.Clear(); EndStroke(); }
            if(StrokeBlocked)return;
            var half = saw ? new Vector3(.0015f,.015f,.025f) : new Vector3(.0007f,.003f,.018f);
            int count = Physics.OverlapBoxNonAlloc(center,half,overlaps,rotation,~0,QueryTriggerInteraction.Ignore);
            if(count==overlaps.Length){Block(app);return;}
            for (int i=0;i<count;i++) Record(app,overlaps[i],center,saw);
            if (hasPrevious)
            {
                Vector3 delta=center-previous;
                if (delta.sqrMagnitude>.00000001f)
                {
                    count=Physics.BoxCastNonAlloc(previous,half,delta.normalized,sweep,rotation,delta.magnitude,~0,QueryTriggerInteraction.Ignore);
                    if(count==sweep.Length){Block(app);return;}
                    for(int i=0;i<count;i++)Record(app,sweep[i].collider,sweep[i].point,saw);
                }
            }
            previous=center;hasPrevious=true;
        }
        void Block(LabRuntime app)
        {
            StrokeBlocked=true;
            app.Message("This stroke crossed too much overlapping geometry to check safely. Release the trigger and make a shorter stroke.");
        }
        void Record(LabRuntime app,Collider collider,Vector3 contact,bool saw)
        {
            var structure=collider ? collider.GetComponentInParent<AnatomyStructure>() : null;
            if(!structure || !app.Unit.protectedIds.Contains(structure.record.id) || !recorded.Add(structure.record.id))return;
            // The action log also deduplicates a contact made by the other hand or after resume.
            if(app.Engine.Attempt.actions.Any(a=>a.kind==ActionKind.ProtectedStructureViolation&&a.targetId==structure.record.id))return;
            var local=structure.transform.InverseTransformPoint(contact);
            app.Emit(new InteractionAction {kind=ActionKind.ProtectedStructureViolation,targetId=structure.record.id,
                toolId=saw?"dissection-saw":"dissection-scalpel",contactVerified=true,localPosition=new[]{local.x,local.y,local.z}});
        }
    }
}
