using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace ThoraxLab.Learning
{
    public static class LearningReadinessRegression
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        [Serializable] sealed class Check { public string id; public bool passed; }
        [Serializable] sealed class Report { public string utc; public bool passed; public List<Check> checks=new List<Check>(); }
        public static IEnumerator Run(LabRuntime app,List<string> errors,string output)
        {
            var report=new Report();
            void Check(string id,bool passed){report.checks.Add(new Check{id=id,passed=passed});if(!passed)errors.Add("Readiness regression: "+id);}
            string profileRead=JsonUtility.ToJson(AccountService.ProfileSyncPayload(null,null));
            string profileDelete=JsonUtility.ToJson(AccountService.ProfileSyncPayload(null,"qa-profile"));
            var requestedProfile=new SettingsProfile{name="Explicitly saved profile"};
            string profileSave=JsonUtility.ToJson(AccountService.ProfileSyncPayload(requestedProfile,null));
            Check("profile_read_omits_optional_profile",!profileRead.Contains("incoming_profile"));
            Check("profile_delete_does_not_create_default",!profileDelete.Contains("incoming_profile")&&profileDelete.Contains("qa-profile"));
            Check("profile_save_preserves_explicit_identity",profileSave.Contains(requestedProfile.id)&&profileSave.Contains("Explicitly saved profile"));
            string folder=Path.Combine(output,"settings-regression-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
            string path=Path.Combine(folder,"settings.save");
            File.WriteAllText(path,"damaged primary");File.WriteAllText(path+".bak","damaged backup");
            try
            {
                string profileId;
                using(var recovered=new SettingsService(folder))
                {
                    Check("corrupt_settings_boot_with_defaults",recovered.Current!=null&&!string.IsNullOrEmpty(recovered.RecoveryNotice));
                    Check("corrupt_settings_originals_preserved",Directory.GetFiles(folder,"*.corrupt-*").Length==2&&File.ReadAllText(path)=="damaged primary");
                    var draft=recovered.Draft();draft.name="Recovered readable profile";draft.brightness=1.4f;draft.shadows=true;profileId=draft.id;
                    recovered.Apply(draft);
                }
                File.WriteAllText(path,"interrupted primary after recovery");
                using(var restarted=new SettingsService(folder))
                {
                    Check("first_recovery_save_rebuilds_valid_backup",restarted.Current.id==profileId&&restarted.Current.name=="Recovered readable profile");
                    app.Presentation.Initialize(app.Session);restarted.ApplySceneSettings();
                    var light=GameObject.Find("Specimen examination light").GetComponent<Light>();
                    Check("saved_lighting_matches_profile",Mathf.Abs(light.intensity-VRSpecimenPresentation.ExaminationIntensity*1.4f)<.001f&&light.shadows==LightShadows.Soft);
                }
                string failureFolder=Path.Combine(folder,"save-failure");Directory.CreateDirectory(failureFolder);
                using(var service=new SettingsService(failureFolder))
                {
                    string before=JsonUtility.ToJson(service.Collection);var draft=service.Draft();draft.name="Must not become current";
                    Directory.CreateDirectory(Path.Combine(failureFolder,"settings.save"));
                    bool failed=false;try{service.Apply(draft);}catch(IOException){failed=true;}catch(UnauthorizedAccessException){failed=true;}
                    Check("failed_apply_preserves_current_profile",failed&&JsonUtility.ToJson(service.Collection)==before);
                    service.RevertDisplay();Check("cancel_after_failed_apply_keeps_old_profile",service.Current.name!="Must not become current");
                }
            }
            catch(Exception ex){errors.Add("Settings recovery regression: "+ex.Message);}
            finally{app.Settings.RevertDisplay();}
            app.SendMessage("OnApplicationFocus",true);
            var unit=app.Catalog.units.First(u=>u.id=="chest-wall");
            var assembly=unit.variants.First(v=>v.mode==LearningMode.AssembleAnatomy&&v.difficulty==Difficulty.Easy);
            app.StartActivity(assembly,SessionPhase.Assessment);yield return null;
            var objective=app.Engine.Current;var piece=app.Structures[objective.targetId];
            piece.RestorePose();piece.transform.position+=Vector3.right*(objective.maxDistance+.03f);Physics.SyncTransforms();piece.TrySnap();
            Check("incorrect_assembly_release_recorded",app.Engine.Attempt.mistakes==1&&app.Engine.Attempt.actions.Count==1&&!app.Engine.IsComplete(objective.id));
            piece.RestorePose();Physics.SyncTransforms();piece.TrySnap();
            Check("corrected_assembly_retains_error",app.Engine.IsComplete(objective.id)&&app.Engine.Attempt.mistakes==1);
            app.RestoreActivity();Check("restore_does_not_erase_assembly_error",app.Engine.Attempt.mistakes==1);
            app.StartActivity(assembly,SessionPhase.Practice);yield return null;
            piece=app.Structures[app.Engine.Current.targetId];piece.TrySnap();
            Check("practice_assembly_remains_forgiving",app.Engine.Attempt.actions.Count==0&&app.Engine.Attempt.mistakes==0);

            var enumerate=unit.variants.First(v=>v.mode==LearningMode.IdentifyEnumerate&&v.difficulty==Difficulty.Easy);
            app.StartActivity(enumerate,SessionPhase.Practice);yield return null;
            objective=app.Engine.Current;piece=app.Structures[objective.targetId];
            if(LearningSceneActionFixture.FindObserver(app,piece,out var point))app.Select(piece,point);
            Check("enumeration_places_surface_marker",app.Markers.Count==1&&app.Engine.Attempt.actions.LastOrDefault()?.localPosition.Length==3);
            app.Select(piece);Check("repeated_selection_does_not_duplicate_marker",app.Markers.Count==1);
            app.RestoreActivity();Check("markers_rebuilt_from_safe_checkpoint",app.Markers.Count==1);
            yield return app.GetComponent<LearningRuntimeVerification>().Capture("vr-enumeration-markers",true);
            var dissection=unit.variants.First(v=>v.mode==LearningMode.GuidedDissection&&v.difficulty==Difficulty.Easy);
            app.StartActivity(dissection,SessionPhase.Assessment);yield return null;
            var nerve=app.Structures[unit.protectedIds[0]];
            // Select the protected mesh to verify monitoring is not coupled to CanCut.
            app.Session.Select(nerve);Physics.SyncTransforms();
            point=nerve.transform.TransformPoint(nerve.Filter.sharedMesh.vertices[0]);
            var monitor=new DissectionContactMonitor();app.Paused=true;monitor.Sample(app,point,Quaternion.identity,false);
            Check("paused_blade_contact_does_not_score",!app.Engine.Attempt.criticalFailure);
            app.Paused=false;monitor.Sample(app,point,Quaternion.identity,false);
            Check("protected_mesh_contact_recorded_without_cut_permission",!app.CanCut&&app.Engine.Attempt.criticalFailure&&app.Engine.Attempt.actions.Any(a=>a.kind==ActionKind.ProtectedStructureViolation&&a.targetId==nerve.record.id));
            int count=app.Engine.Attempt.actions.Count;monitor.Sample(app,point,Quaternion.identity,false);monitor.EndStroke();monitor.Sample(app,point,Quaternion.identity,false);
            Check("repeated_protected_contact_is_bounded",app.Engine.Attempt.actions.Count==count);
            app.Session.Undo();app.RestoreActivity();Check("undo_and_restore_preserve_protection_failure",app.Engine.Attempt.criticalFailure);
            var congestion=new GameObject("QA contact saturation fixture");
            point=new Vector3(10,10,10);
            for(int i=0;i<70;i++){var item=new GameObject("Contact fixture "+i);item.transform.SetParent(congestion.transform);item.transform.position=point;item.AddComponent<BoxCollider>().size=Vector3.one*.01f;}
            Physics.SyncTransforms();monitor.EndStroke();monitor.Sample(app,point,Quaternion.identity,false);
            Check("saturated_contact_query_blocks_stroke",monitor.StrokeBlocked);
            UnityEngine.Object.Destroy(congestion);monitor.EndStroke();
            Check("releasing_blocked_stroke_allows_retry",!monitor.StrokeBlocked);
            app.Interrupt();app.Session.ResetSpecimen();app.Presentation.ShowIntact();app.UI.ShowHub();
            report.utc=DateTime.UtcNow.ToString("o");report.passed=report.checks.Count>=15&&report.checks.All(c=>c.passed);
            File.WriteAllText(Path.Combine(output,"readiness-regressions.json"),JsonUtility.ToJson(report,true));
        }
#endif
    }
}
