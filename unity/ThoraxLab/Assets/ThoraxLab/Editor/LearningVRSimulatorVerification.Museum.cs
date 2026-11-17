#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using TMPro;
using ThoraxLab.Learning;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;

namespace ThoraxLab.Editor
{
    public sealed partial class LearningVRSimulatorVerificationRunner
    {
        IEnumerable<int> VerifyThoraxMuseum()
        {
            var settings=app.Settings.Draft();var savedLeft=leftState;var savedRight=rightState;
            try
            {
                app.EnterPreview();app.Settings.Current.leftHanded=false;app.Settings.Current.toggleGrip=false;
                SetController(ref leftState,leftState.devicePosition,leftState.deviceRotation,true,false,false);
                SetController(ref rightState,rightState.devicePosition,rightState.deviceRotation,true,false,false);
                leftState.primary2DAxis=rightState.primary2DAxis=Vector2.zero;
                leftState=leftState.WithButton(ControllerButton.PrimaryButton,false).WithButton(ControllerButton.MenuButton,false);
                rightState=rightState.WithButton(ControllerButton.PrimaryButton,false).WithButton(ControllerButton.MenuButton,false);
                QueueAll();yield return 5;
                var rig=Camera.main.transform.parent;var labPosition=rig.position;var labRotation=rig.rotation;
                app.StartMuseum();yield return 8;
                var museum=app.Museum;
                Require("museum/entry",app.MuseumActive&&app.CanInteract&&!app.OrientationActive&&!app.Progress.orientationCompleted&&app.Engine==null&&!app.Sandbox,
                    "The museum opens from guest access without orientation, assessment or dissection permissions.");
                Require("museum/collection",museum.Collection.Entries.Count==app.Session.originals.Count&&museum.Collection.Entries.Count==249&&museum.Collection.Systems.Length==8,
                    "All 249 catalog entries, including reference annotations, belong to one of eight galleries.");
                Require("museum/space",Vector3.Distance(rig.position,labPosition)>10&&Physics.Raycast(Camera.main.transform.position,Vector3.down,out var floor,3)&&floor.normal.y>.8f,
                    "The learner enters a separate gallery with a real floor for walking and teleportation.");
                var originals=app.Session.originals.ToDictionary(s=>s.record.id);
                var meshes=originals.ToDictionary(p=>p.Key,p=>p.Value.Filter.sharedMesh);
                var positions=originals.ToDictionary(p=>p.Key,p=>p.Value.transform.position);
                var scales=originals.ToDictionary(p=>p.Key,p=>p.Value.transform.localScale);
                string progress=JsonUtility.ToJson(app.Progress);
                var visited=new HashSet<string>();
                foreach(string system in museum.Collection.Systems)
                {
                    museum.ShowSystem(system);
                    for(int page=0;page<museum.Collection.PageCount(system);page++)
                    {
                        if(page>0)museum.TurnPage(1);
                        app.UI.ShowWorkspace();yield return 4;Canvas.ForceUpdateCanvases();
                        Require("museum/display/"+system+"/"+page,museum.Exhibits.Count>0&&museum.Exhibits.Count<=6&&museum.Exhibits.All(e=>e.Entry.System==system),
                            "Each display groups at most six separate models from the selected body system.");
                        foreach(var exhibit in museum.Exhibits)
                        {
                            visited.Add(exhibit.Entry.Id);
                            var labels=exhibit.Label.GetComponentsInChildren<TMP_Text>();
                            Require("museum/label/"+exhibit.Entry.Id,labels.Any(t=>t.text==exhibit.Entry.Name)&&labels.Any(t=>t.text==exhibit.Entry.Description)&&labels.All(t=>!t.isTextOverflowing),
                                "Each physical exhibit has its structure name and brief description, without an ID in its label.");
                            Require("museum/model/"+exhibit.Entry.Id,exhibit.GetComponent<MeshFilter>().sharedMesh==meshes[exhibit.Entry.Id]&&exhibit.Grab&&exhibit.GetComponent<MeshCollider>()&&exhibit.GetComponent<AnatomyStructure>()==null,
                                "The museum uses the catalog geometry in an independent grabbable display, never an assessment target.");
                        }
                    }
                }
                Require("museum/reachable",visited.SetEquals(originals.Keys),"Every catalog model is reachable through gallery pagination.");
                museum.FocusEntry("FMA7857");app.UI.ShowWorkspace();yield return 5;
                var selected=museum.Selected;var home=selected.transform.position;
                var mesh=selected.GetComponent<MeshFilter>().sharedMesh;var indices=mesh.GetTriangles(0);var vertices=mesh.vertices;
                var a=selected.transform.TransformPoint(vertices[indices[0]]);var b=selected.transform.TransformPoint(vertices[indices[1]]);var c=selected.transform.TransformPoint(vertices[indices[2]]);
                var normal=Vector3.Cross(b-a,c-a).normalized;var surface=(a+b+c)/3;
                var rotation=Quaternion.LookRotation(-normal,Mathf.Abs(normal.y)>.95f?Vector3.forward:Vector3.up);
                museum.Select(museum.Exhibits.First(e=>e!=selected));
                SetHandWorld(surface+normal*.04f,rotation);yield return 6;
                Trigger(true);yield return 3;Trigger(false);yield return 4;
                Require("museum/select",museum.Selected==selected,"The normal controller trigger selects the museum exhibit.");
                Grip(true);yield return 10;
                Require("museum/grab",hand.interactablesSelected.Any(x=>ReferenceEquals(x,selected.Grab)),"Normal XRI grip picks up a museum model.");
                SetHandWorld(surface+normal*.04f+Vector3.up*.12f,rotation);yield return 8;
                Require("museum/lift",Vector3.Distance(selected.transform.position,home)>.06f,"The held exhibit follows the tracked controller.");
                Grip(false);yield return 8;
                Require("museum/release",Vector3.Distance(selected.transform.position,home)<.005f,"Releasing grip returns the model to its stand.");
                app.Inspector.Open();yield return 8;
                Require("museum/zoom_source",app.Inspector.IsOpen&&app.Inspector.Visual&&app.Inspector.Visual.GetComponentsInChildren<MeshFilter>().Single().sharedMesh==mesh,
                    "A museum inspection enlarges the selected exhibit rather than the main thorax.");
                app.Inspector.ZoomIn();
                Require("museum/zoom",app.Inspector.Magnification>1&&!app.CanInteract,"Zoom enlarges the selected 3D model while activity interactions pause.");
                app.Inspector.ReturnToActivity();yield return 4;
                Require("museum/zoom_return",app.MuseumActive&&app.CanInteract&&museum.Selected==selected,"Closing inspection restores the same museum display and selection.");
                app.UI.TogglePauseMenu();yield return 4;
                Require("museum/pause",app.MuseumActive&&!app.CanInteract&&app.UI.ScreenName=="Menu","Menu pauses gallery interaction without leaving the museum.");
                app.UI.TogglePauseMenu();yield return 4;
                Require("museum/resume",app.MuseumActive&&app.CanInteract,"The menu shortcut returns to the museum workspace.");
                Require("museum/no_source_mutation",originals.All(p=>p.Value.Filter.sharedMesh==meshes[p.Key]&&Vector3.Distance(p.Value.transform.position,positions[p.Key])<.00001f&&p.Value.transform.localScale==scales[p.Key]),
                    "Museum movement, gripping and zoom do not change the source specimen.");
                Require("museum/no_credit",JsonUtility.ToJson(app.Progress)==progress&&!app.CanUseCuttingTool&&app.Engine==null,
                    "Museum exploration cannot add assessment actions, unlock levels or activate cutting tools.");
                app.UI.ShowHub();yield return 6;
                Require("museum/exit",!app.MuseumActive&&!app.Inspector.IsOpen&&museum.Exhibits.Count==0&&Vector3.Distance(rig.position,labPosition)<.001f&&Quaternion.Angle(rig.rotation,labRotation)<.1f,
                    "Leaving removes the gallery and restores the saved lab position and heading.");
            }
            finally
            {
                app.Inspector.Close();app.Interrupt();app.Session.ResetSpecimen();app.Presentation.ShowIntact();
                app.Settings.Apply(settings);leftState=savedLeft;rightState=savedRight;QueueAll();app.UI.ShowSignIn();
            }
            yield return 4;
        }
    }
}
#endif
