#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using ThoraxLab.Learning;
using UnityEngine;

namespace ThoraxLab.Editor
{
    public sealed partial class LearningVRSimulatorVerificationRunner
    {
        IEnumerable<int> VerifyIdentificationFocus()
        {
            bool Visible(string id)=>app.Structures[id].gameObject.activeInHierarchy;
            void Prepare(string unitId,string targetId)
            {
                var unit=app.Catalog.units.Single(u=>u.id==unitId);
                var variant=unit.variants.First(v=>v.mode==LearningMode.IdentifyEnumerate);
                app.Presentation.Prepare(variant.mode,variant.objectives.Select(o=>o.targetId),unit.protectedIds,unit);
                app.Presentation.PrepareTeachingWindow(app.Structures[targetId]);
            }
            string progress=JsonUtility.ToJson(app.Progress);
            int actions=app.Engine?.Attempt.actions.Count??0;
            var meshes=app.Structures.ToDictionary(p=>p.Key,p=>p.Value.Filter.sharedMesh);
            try
            {
                Prepare("heart-coronaries","FMA50039");yield return 3;
                Require("identify_focus/heart_context",new[]{"FMA9457","FMA9531","FMA13884","FMA50039","FMA50040","FMA4706"}.All(Visible),"The heart walls and companion coronary vessels form the visible model.");
                Require("identify_focus/thorax_hidden",new[]{"FMA7163","FMA7486","MAL_RUL","MAL_LUL","FMA7394","FMA7131","REC_Pericardium"}.All(id=>!Visible(id)),"Unrelated thorax anatomy and its colliders are inactive during heart identification.");
                app.Presentation.PrepareTeachingWindow(app.Structures["FMA7242"]);yield return 3;
                Require("identify_focus/valve_cutaway",Visible("FMA7242")&&Visible("FMA7243")&&!Visible("FMA13884")&&!Visible("FMA3736")&&!Visible("FMA8612"),"Chamber and outflow walls cannot cover the mitral leaflets.");
                app.Presentation.PrepareTeachingWindow(app.Structures["FMA50039"]);yield return 3;
                Require("identify_focus/heart_restored",Visible("FMA13884")&&!Visible("MAL_RUL"),"Changing steps restores heart context without restoring the thorax.");
                Prepare("pleura-lungs","MAL_RUL");yield return 3;
                Require("identify_focus/unit_switch",Visible("MAL_RUL")&&Visible("MAL_LLL")&&!Visible("FMA13884")&&!Visible("REC_Right_Visceral_Pleura"),"Switching units replaces the heart with an uncovered lung model.");
                Prepare("airways","FMA7394");yield return 3;
                Require("identify_focus/airway_tree",Visible("FMA7394")&&Visible("FMA31748")&&!Visible("MAL_RUL")&&!Visible("FMA3736"),"The branching airway is exposed without lungs or overlying vessels.");
                Require("identify_focus/no_mesh_changes",app.Structures.All(p=>p.Value.Filter.sharedMesh==meshes[p.Key]),"Focused views keep the original anatomy meshes.");
                Require("identify_focus/no_credit",JsonUtility.ToJson(app.Progress)==progress&&(app.Engine?.Attempt.actions.Count??0)==actions,"Preparing views cannot record selections, complete orientation or award mastery.");
                var chest=app.Catalog.units.Single(u=>u.id=="chest-wall");
                app.Presentation.Prepare(LearningMode.GuidedDissection,chest.anatomyIds,chest.protectedIds,chest);yield return 3;
                Require("identify_focus/dissection_unaffected",Visible("FMA7163")&&Visible("MAL_RUL")&&Visible("FMA13884"),"Changing modes discards the identification mask and restores dissection anatomy.");
            }
            finally{if(app.Engine!=null)app.RestoreActivity();else app.Presentation.ShowIntact();}
            yield return 3;
            if(app.Engine==null)Require("identify_focus/intact_restored",Visible("FMA7163")&&Visible("MAL_RUL"),"Leaving identification restores the intact specimen.");
        }
    }
}
#endif
