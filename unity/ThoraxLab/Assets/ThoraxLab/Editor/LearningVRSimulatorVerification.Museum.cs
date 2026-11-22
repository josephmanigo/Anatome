#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using TMPro;
using ThoraxLab.Learning;
using UnityEngine;
using UnityEngine.UI;
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
                Require("museum/voice",app.Narrator.IsPlaying&&app.Narrator.Caption.Length>0,"Entering the gallery automatically starts its spoken controls.");
                Require("museum/entry",app.MuseumActive&&app.CanInteract&&!app.OrientationActive&&!app.Progress.orientationCompleted&&app.Engine==null&&!app.Sandbox,
                    "The museum opens from guest access without orientation, assessment or dissection permissions.");
                Require("museum/collection",museum.Collection.Entries.Count==app.Session.originals.Count&&museum.Collection.Entries.Count==249&&museum.Collection.Systems.Length==8,
                    "All 249 catalog entries, including reference annotations, belong to one of eight galleries.");
                Require("museum/space",Vector3.Distance(rig.position,labPosition)>10&&Physics.Raycast(Camera.main.transform.position,Vector3.down,out var floor,3)&&floor.normal.y>.8f,
                    "The learner enters a separate gallery with a real floor for walking and teleportation.");
                var gallery=museum.Exhibits[0].transform.parent.parent;
                var shell=gallery.GetComponent<MeshRenderer>();
                var directory=gallery.GetComponentInChildren<TextMeshPro>();
                Require("museum/finishes",shell && shell.sharedMaterials.All(m=>m && m.shader.isSupported && m.shader.name=="ThoraxLab/Museum finish")
                    && shell.sharedMaterials.Select(m=>m.mainTexture).OfType<Texture2D>().Distinct().Count(t=>t.mipmapCount>1)==3,
                    "The gallery uses the supported architectural shader and three distinct mipmapped finish maps.");
                Require("museum/ceiling",Physics.Raycast(Camera.main.transform.position,Vector3.up,out var ceiling,4) && ceiling.collider.transform==gallery,
                    "The room has an enclosed ceiling above the entry position.");
                var originals=app.Session.originals.ToDictionary(s=>s.record.id);
                var meshes=originals.ToDictionary(p=>p.Key,p=>p.Value.Filter.sharedMesh);
                var atlasMeshes=Resources.LoadAll<Mesh>("Learning/AtlasLungDetail").ToDictionary(m=>m.name);
                Require("museum/atlas-inventory",atlasMeshes.Keys.ToHashSet().SetEquals(originals.Values.Where(s=>s.record.required_group=="lung_lobes").Select(s=>s.record.id)),
                    "Each of the five catalog lobes has a corresponding atlas mesh.");
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
                        directory.ForceMeshUpdate();
                        Require("museum/directory/"+system+"/"+page,directory.text.Contains(system) && directory.text.Contains($"Display {page+1} of {museum.Collection.PageCount(system)}")
                            && directory.textBounds.size.x<=directory.rectTransform.rect.width,
                            "The rear sign identifies this gallery and page within its mounted width.");
                        Require("museum/display/"+system+"/"+page,museum.Exhibits.Count>0&&museum.Exhibits.Count<=6&&museum.Exhibits.All(e=>e.Entry.System==system),
                            "Each display groups at most six separate models from the selected body system.");
                        foreach(var exhibit in museum.Exhibits)
                        {
                            visited.Add(exhibit.Entry.Id);
                            Require("museum/label/"+exhibit.Entry.Id,MuseumCopyFits(exhibit.Label,exhibit.Entry)
                                && exhibit.Label.GetComponentsInChildren<Graphic>(true).All(g=>!g.raycastTarget),
                                "The separate name and description fit their plaque and leave the controller ray free.");
                            var displayMesh=exhibit==museum.Selected && exhibit.Entry.Group=="lung_lobes" ? atlasMeshes[exhibit.Entry.Id] : meshes[exhibit.Entry.Id];
                            Require("museum/model/"+exhibit.Entry.Id,exhibit.GetComponent<MeshFilter>().sharedMesh==displayMesh && exhibit.Grab
                                && exhibit.Renderer.sharedMaterials.SequenceEqual(originals[exhibit.Entry.Id].Renderer.sharedMaterials)
                                && exhibit.GetComponent<MeshCollider>().sharedMesh==meshes[exhibit.Entry.Id] && exhibit.GetComponent<AnatomyStructure>()==null,
                                "The exhibit uses the corresponding lab mesh and surface materials and retains its working grab collider.");
                            float radius=meshes[exhibit.Entry.Id].bounds.extents.magnitude*exhibit.transform.lossyScale.x;
                            Require("museum/display-size/"+exhibit.Entry.Id,Mathf.Abs(radius-.24f)<.001f,
                                "The source mesh has the same display radius as other exhibits, including small imported structures.");
                        }
                    }
                }
                Require("museum/reachable",visited.SetEquals(originals.Keys),"Every catalog model is reachable through gallery pagination.");
                foreach(var lobe in originals.Values.Where(s=>s.record.required_group=="lung_lobes"))
                {
                    museum.FocusEntry(lobe.record.id);
                    var selectedLobe=museum.Selected;
                    if (lobe.record.id == "MAL_LUL")
                    {
                        app.UI.ShowWorkspace();
                        yield return 4;
                        CaptureExhibitDescriptions(selectedLobe.Label);
                    }
                    var detailed=selectedLobe.GetComponent<MeshFilter>().sharedMesh;
                    var working=meshes[lobe.record.id];
                    Require("museum/atlas-detail/"+lobe.record.id,detailed==atlasMeshes[lobe.record.id]
                        && detailed.GetIndexCount(0)>working.GetIndexCount(0)*2 && selectedLobe.GetComponent<MeshCollider>().sharedMesh==working
                        && Vector3.Distance(detailed.bounds.center,working.bounds.center)<working.bounds.extents.magnitude*.025f
                        && Vector3.Distance(detailed.bounds.size,working.bounds.size)<working.bounds.size.magnitude*.025f,
                        "Selection displays the corresponding full-resolution atlas lobe at its source pivot and keeps the working collider.");
                    museum.Select(museum.Exhibits.First(e=>e!=selectedLobe));
                    Require("museum/atlas-release/"+lobe.record.id,selectedLobe.GetComponent<MeshFilter>().sharedMesh==working,
                        "Changing selection releases the additional lung geometry.");
                }
                var valves=originals.Values.Where(s=>s.record.AppearanceTissue=="valve").ToArray();
                Require("museum/valve-tissue",valves.Length==11 && valves.All(s=>s.Renderer.sharedMaterial.name.EndsWith("Tissue_valve")
                    && s.interiorMaterial.name.EndsWith("Interior_valve") && s.Renderer.sharedMaterial.shader.name=="ThoraxLab/Organ Surface")
                    && originals["FMA13884"].Renderer.sharedMaterial.name.EndsWith("Tissue_cardiac"),
                    "Eleven native valve parts use distinct connective tissue on their sides and cuts; myocardium retains muscle.");
                foreach(var tissue in new[]{"lung","valve","cardiac","artery","vein","esophagus","membrane","airway"})
                {
                    var structures=originals.Values.Where(s=>!s.record.reference_region && s.record.AppearanceTissue==tissue).ToArray();
                    Require("museum/organ-surfaces/"+tissue,structures.Length>0 && structures.All(s=>
                        s.Renderer.sharedMaterial.shader.name=="ThoraxLab/Organ Surface" && s.interiorMaterial.shader==s.Renderer.sharedMaterial.shader
                        && s.Renderer.sharedMaterial.mainTexture!=s.interiorMaterial.mainTexture
                        && s.interiorMaterial.mainTextureScale.x>s.Renderer.sharedMaterial.mainTextureScale.x),
                        "Intact and exposed organ tissue use separate mapped surfaces at their assigned scales.");
                }
                foreach (var tissue in new[] { "skin", "fat", "fascia", "muscle" })
                {
                    var parts = originals.Values.Where(s => !s.record.reference_region && s.record.AppearanceTissue == tissue).ToArray();
                    var outer = Resources.Load<Material>("Learning/Specimen/Tissue_" + tissue);
                    var inner = Resources.Load<Material>("Learning/Specimen/Interior_" + tissue);
                    Require("museum/thoracic-tissue/" + tissue, parts.Length > 0 && outer && inner
                        && outer.shader.name == "ThoraxLab/Organ Surface" && inner.shader == outer.shader
                        && outer.mainTexture && inner.mainTexture && outer.mainTexture != inner.mainTexture
                        && parts.All(s => s.Renderer.sharedMaterial.mainTexture == outer.mainTexture
                            && s.interiorMaterial.mainTexture == inner.mainTexture),
                        "The installed outer and cut materials reach all " + parts.Length + " corresponding source structures.");
                }
                var headBefore=headState;
                var longestName=museum.Collection.Entries.OrderByDescending(e=>e.Name.Length).First();
                foreach(int frames in GuideClick("Choose a gallery / find a part"))yield return frames;
                var search=app.UI.Root.GetComponentInChildren<TMP_InputField>();
                search.text=longestName.Name;yield return 4;
                foreach(int frames in GuideClick(longestName.Name+" · "+longestName.System))yield return frames;
                var plaqueExhibit=museum.Selected;
                Require("museum/search-reader",plaqueExhibit && plaqueExhibit.Entry==longestName && MuseumCopyFits(MuseumReader(),longestName)
                    && museum.Exhibits.All(e=>e.Label.GetComponentsInChildren<LabIcon>().Any(i=>i.glyph==LabGlyph.Check)==(e==plaqueExhibit)),
                    "Selecting the longest name through search opens the matching reader and marks exactly one physical plaque.");
                var displaySettings=app.Settings.Draft();
                var largeText=app.Settings.Draft();largeText.textScale=1.5f;largeText.highContrast=true;app.Settings.Apply(largeText);
                app.UI.ShowWorkspace();yield return 5;Canvas.ForceUpdateCanvases();
                var reader=MuseumReader();
                Require("museum/reader-large-text",MuseumCopyFits(reader,longestName) && reader.GetComponent<Image>().color==Color.black
                    && reader.GetComponentsInChildren<TMP_Text>().All(t=>t.color==Color.white) && reader.GetComponentInParent<ScrollRect>(),
                    "At 150% text, the long selected name and full description reflow on the high-contrast scrolling panel.");
                var seatedEye=gallery.TransformPoint(new Vector3(0,1.1f,0));
                SetHead(rig.InverseTransformPoint(seatedEye),headBefore.deviceRotation,true);QueueAll();yield return 5;
                museum.FocusEntry(longestName.Id);yield return 5;
                plaqueExhibit=museum.Selected;
                Require("museum/seated-plaque",Mathf.Abs(plaqueExhibit.StandTop.y-.7f)<.001f,"Plaque clearance is tested on the lowest supported seated stand.");
                var cornerPoints=new Vector3[4];
                foreach (var offset in new[]{Vector3.forward*.75f,Vector3.back*2.5f,Vector3.left*1.1f,Vector3.right*1.1f,(Vector3.right+Vector3.forward).normalized*1.1f})
                {
                    var direction=offset.normalized;
                    Vector3 eye=gallery.TransformPoint(plaqueExhibit.StandTop+offset+Vector3.up*.52f);
                    SetHead(rig.InverseTransformPoint(eye),headBefore.deviceRotation,true); QueueAll();yield return 5;
                    Canvas.ForceUpdateCanvases();plaqueExhibit.Label.GetWorldCorners(cornerPoints);
                    float edge=.414f*Mathf.Abs(direction.x)+.332f*Mathf.Abs(direction.z);
                    Require("museum/plaque-clearance/"+direction,cornerPoints.All(c=>Vector3.Dot(gallery.InverseTransformPoint(c)-plaqueExhibit.StandTop,direction)>=edge)
                        && cornerPoints.All(c=>gallery.InverseTransformPoint(c).y>.02f) && plaqueExhibit.Label.GetComponent<Image>().color==Color.black
                        && plaqueExhibit.Label.GetComponentsInChildren<TMP_Text>().All(t=>t.color==Color.white) && MuseumCopyFits(plaqueExhibit.Label,longestName),
                        "The large-text plaque clears the stand and floor with both fields readable from this approach.");
                }
                headState=headBefore; QueueAll();app.Settings.Apply(displaySettings);yield return 5;
                museum.FocusEntry("FMA7857");app.UI.ShowWorkspace();yield return 5;
                var selected=museum.Selected;var home=selected.transform.position;
                var mesh=selected.GetComponent<MeshFilter>().sharedMesh;var indices=mesh.GetTriangles(0);var vertices=mesh.vertices;
                var a=selected.transform.TransformPoint(vertices[indices[0]]);var b=selected.transform.TransformPoint(vertices[indices[1]]);var c=selected.transform.TransformPoint(vertices[indices[2]]);
                var normal=Vector3.Cross(b-a,c-a).normalized;var surface=(a+b+c)/3;
                var rotation=Quaternion.LookRotation(-normal,Mathf.Abs(normal.y)>.95f?Vector3.forward:Vector3.up);
                museum.Select(museum.Exhibits.First(e=>e!=selected));
                SetHandWorld(surface+normal*.04f,rotation);yield return 6;
                Trigger(true);yield return 3;Trigger(false);yield return 4;
                Require("museum/select",museum.Selected==selected && MuseumCopyFits(MuseumReader(),selected.Entry)
                    && museum.Exhibits.All(e=>e.Label.GetComponentsInChildren<LabIcon>().Any(i=>i.glyph==LabGlyph.Check)==(e==selected)),
                    "A controller trigger moves both the plaque check and the reader to the selected exhibit.");
                Grip(true);yield return 10;
                Require("museum/grab",hand.interactablesSelected.Any(x=>ReferenceEquals(x,selected.Grab)),"Normal XRI grip picks up a museum model.");
                SetHandWorld(surface+normal*.04f+Vector3.up*.12f,rotation);yield return 8;
                Require("museum/lift",Vector3.Distance(selected.transform.position,home)>.06f,"The held exhibit follows the tracked controller.");
                Grip(false);yield return 8;
                Require("museum/release",Vector3.Distance(selected.transform.position,home)<.005f,"Releasing grip returns the model to its stand.");
                app.Inspector.Open();yield return 8;
                var initialDistance=Vector3.Distance(Camera.main.transform.position,selected.Renderer.bounds.center);
                app.Inspector.ZoomIn();
                VerifySingleSolidModel("museum/single_solid_zoom",selected.Renderer);
                Require("museum/zoom",app.Inspector.IsOpen&&app.CanInteract&&selected.gameObject.activeInHierarchy&&Mathf.Abs(initialDistance/app.Inspector.Magnification-Vector3.Distance(Camera.main.transform.position,selected.Renderer.bounds.center))<.002f,
                    "Zoom focuses on the selected exhibit while its real collider stays available in the gallery.");
                SetHandWorld(surface+normal*.04f,rotation);yield return 4;Grip(true);yield return 8;
                Require("museum/zoom_grip",selected.Grab.isSelected&&app.Inspector.IsOpen,"The exhibit can be picked up in the enlarged gallery.");
                Grip(false);yield return 5;
                app.Inspector.Close();yield return 4;
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

        RectTransform MuseumReader() => app.UI.Root.GetComponentsInChildren<RectTransform>().FirstOrDefault(r=>r.name=="Exhibit reading panel");

        void CaptureExhibitDescriptions(RectTransform plaque)
        {
            var camera = Camera.main;
            var original = new Pose(camera.transform.position, camera.transform.rotation);
            float field = camera.fieldOfView, aspect = camera.aspect;
            var eye = camera.stereoTargetEye;
            var canvases = app.UI.Root.GetComponentsInChildren<Canvas>(true);
            var enabled = canvases.Select(c => c.enabled).ToArray();
            string folder = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../../../qa/case-instruments"));
            void Draw(Vector3 from, Vector3 target, float angle, string file)
            {
                camera.transform.SetPositionAndRotation(from, Quaternion.LookRotation(target - from, Vector3.up));
                camera.fieldOfView = angle;
                CaptureWorldInterface(System.IO.Path.Combine(folder, file), 1200, 800);
            }
            try
            {
                camera.stereoTargetEye = StereoTargetEyeMask.None;
                camera.aspect = 1.5f;
                foreach (var canvas in canvases) canvas.enabled = false;
                var plate = plaque.TransformPoint(plaque.rect.center);
                Draw(plate - plaque.forward * 1.18f + Vector3.up * .10f, plate + Vector3.up * .12f, 47, "after-plaque.png");
                for (int i = 0; i < canvases.Length; i++) canvases[i].enabled = enabled[i];
                var center = app.UI.Root.TransformPoint(app.UI.Root.rect.center);
                Draw(center - app.UI.Root.forward * 1.26f, center, 48, "after-museum-panel.png");
            }
            finally
            {
                for (int i = 0; i < canvases.Length; i++) canvases[i].enabled = enabled[i];
                camera.transform.SetPositionAndRotation(original.position, original.rotation);
                camera.stereoTargetEye = eye;
                camera.fieldOfView = field;
                camera.aspect = aspect;
            }
        }

        static bool MuseumCopyFits(RectTransform parent,MuseumEntry entry)
        {
            if(!parent)return false;
            var texts=parent.GetComponentsInChildren<TMP_Text>();
            var title=texts.FirstOrDefault(t=>t.text==entry.Name);
            var body=texts.FirstOrDefault(t=>t.text==entry.Description);
            if(!title || !body || title.fontSize<=body.fontSize || !title.fontStyle.HasFlag(FontStyles.Bold))return false;
            foreach(var text in new[]{title,body})
            {
                text.ForceMeshUpdate();
                var bounds=text.textBounds.size;
                var rect=text.rectTransform.rect;
                if(text.isTextOverflowing || bounds.x<=0 || bounds.y<=0 || bounds.x>rect.width+1 || bounds.y>rect.height+1)return false;
            }
            float titleBottom=parent.InverseTransformPoint(title.rectTransform.TransformPoint(0,title.rectTransform.rect.yMin,0)).y;
            float bodyTop=parent.InverseTransformPoint(body.rectTransform.TransformPoint(0,body.rectTransform.rect.yMax,0)).y;
            return titleBottom>=bodyTop;
        }
    }
}
#endif
