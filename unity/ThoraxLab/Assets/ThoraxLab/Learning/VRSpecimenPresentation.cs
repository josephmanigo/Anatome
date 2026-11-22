using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace ThoraxLab.Learning
{
    public enum SpecimenLayer { Skin, Subcutaneous, Muscles, ChestWall, Organs }

    /// <summary>
    /// Presents the existing, attributed specimen. Layer controls alter visibility only;
    /// assessment credit always comes from the interaction/evaluation pipeline.
    /// No source mesh, anatomy transform, collider, or landmark is reconstructed here.
    /// </summary>
    public sealed class VRSpecimenPresentation : MonoBehaviour
    {
        public const float ExaminationIntensity = .52f;
        public const string ReconstructionNote = "Skin wall, subcutaneous tissue and fascia include authored teaching thickness. Surface detail is illustrative; source geometry and attribution are retained.";
        public SpecimenLayer CurrentLayer { get; private set; }
        public int VisibleStructureCount => session == null ? 0 : session.originals.Count(s => s && s.gameObject.activeInHierarchy && !s.record.reference_region);
        public string LayerLabel => LayerName(CurrentLayer);
        public string CurrentWindowLabel { get; private set; }
        public string LastSurfaceCheck { get; private set; }

        DissectionSession session;
        readonly Dictionary<Renderer, Material[]> originalMaterials = new Dictionary<Renderer, Material[]>();
        readonly Dictionary<AnatomyStructure, Material> originalInteriors = new Dictionary<AnatomyStructure, Material>();
        readonly Dictionary<string, Material> presentationMaterials = new Dictionary<string, Material>();
        readonly List<GameObject> fixtures = new List<GameObject>();
        readonly List<UnityEngine.Object> generated = new List<UnityEngine.Object>();
        readonly RaycastHit[] surfaceHits = new RaycastHit[64];
        readonly Dictionary<Mesh, Vector3[]> surfaceSamples = new Dictionary<Mesh, Vector3[]>();
        readonly HashSet<string> preparedContext = new HashSet<string>();
        LearningMode preparedMode;
        IdentificationFocus identificationFocus;
        bool initialized;

        public void Initialize(DissectionSession dissection)
        {
            if (initialized) return;
            session = dissection;
            if (!session || session.originals.Count == 0) throw new InvalidOperationException("The attributed specimen must be loaded before presentation.");
            initialized = true;
            foreach (var structure in session.originals.Where(s => s && !s.record.reference_region))
            {
                originalInteriors[structure] = structure.interiorMaterial;
                foreach (var renderer in structure.GetComponentsInChildren<Renderer>(true)) originalMaterials[renderer] = renderer.sharedMaterials;
            }
            RefreshMaterials();
            ConfigureLighting();
            CreateExaminationDrape();
            ShowIntact();
        }

        public static string LayerName(SpecimenLayer layer) => layer switch
        {
            SpecimenLayer.Skin => "Skin · intact thorax",
            SpecimenLayer.Subcutaneous => "Subcutaneous tissue and fascia",
            SpecimenLayer.Muscles => "Pectoral and intercostal muscles",
            SpecimenLayer.ChestWall => "Ribs, sternum and thoracic cavity",
            _ => "Lungs, heart and deep relationships"
        };

        public void ShowIntact() => ShowLayer(SpecimenLayer.Skin);

        /// <summary>Contextual display; call after restoring the original specimen and before staging movable assembly pieces.</summary>
        public void Prepare(LearningMode mode, IEnumerable<string> targetIds, IEnumerable<string> protectedIds, LearningUnit unit = null)
        {
            if (!session) return;
            RefreshMaterials();
            identificationFocus = mode == LearningMode.IdentifyEnumerate ? new IdentificationFocus(unit, targetIds) : null;
            var required = new HashSet<string>((targetIds ?? Array.Empty<string>()).Concat(protectedIds ?? Array.Empty<string>()));
            var targets = session.originals.Where(s => s && required.Contains(s.record.id)).ToArray();
            int firstLayer = targets.Length == 0 ? 0 : targets.Min(s => s.record.layer);
            CurrentLayer = (SpecimenLayer)Mathf.Clamp(firstLayer, 0, 4);
            foreach (var structure in session.originals.Where(s => s))
            {
                var record = structure.record;
                bool visible;
                if (record.reference_region) visible = false;
                else if (identificationFocus != null) visible = identificationFocus.Includes(record.id, record.required_group, record.reference_region);
                else if (mode == LearningMode.GuidedDissection) visible = true;
                else if (mode == LearningMode.AssembleAnatomy)
                    visible = required.Contains(record.id) || (record.tissue == "bone" && record.layer >= firstLayer);
                else if (mode == LearningMode.AnatomyCases)
                    visible = required.Contains(record.id) || (record.layer >= Math.Max(3, firstLayer) && record.tissue != "membrane");
                else visible = required.Contains(record.id) || (record.layer >= firstLayer && record.tissue != "membrane");
                structure.gameObject.SetActive(visible);
            }
            preparedMode=mode;preparedContext.Clear();CurrentWindowLabel=identificationFocus?.Label;
            foreach(var structure in session.originals.Where(s=>s&&s.gameObject.activeSelf))preparedContext.Add(structure.record.id);
        }

        public void PrepareDissectionWindow(AnatomyStructure target,IEnumerable<string> protectedIds)
        {
            if(!target||!session)return;
            var protectedSet=new HashSet<string>(protectedIds);
            // Each station exposes its actual target, with deeper landmarks for context.
            // Covers are a prepared teaching view and never receive cut/removal credit.
            foreach(var structure in session.originals.Where(s=>s)){
                var r=structure.record;
                bool visible=!r.reference_region&&(r.id==target.record.id||protectedSet.Contains(r.id));
                if(target.record.layer<=3&&r.layer>target.record.layer&&r.tissue!="membrane")visible=!r.reference_region;
                structure.gameObject.SetActive(visible);
            }
            CurrentWindowLabel="Prepared station · "+target.record.name+" · only cut chunks earn removal credit";
            Physics.SyncTransforms();
        }

        /// <summary>Explicit prepared teaching views. This never claims a learner made an incision or grants an objective.</summary>
        public void PrepareTeachingWindow(AnatomyStructure target)
        {
            if(!target||!session||(preparedMode!=LearningMode.IdentifyEnumerate&&preparedMode!=LearningMode.AnatomyCases&&preparedMode!=LearningMode.GuidedDissection))return;
            if(preparedMode==LearningMode.IdentifyEnumerate&&identificationFocus!=null)
            {
                identificationFocus.SetTarget(target.record.id,target.record.tissue,target.record.layer);
                foreach(var structure in session.originals.Where(s=>s))
                {
                    var record=structure.record;
                    structure.gameObject.SetActive(identificationFocus.IsVisible(record.id,record.required_group,record.tissue,record.layer,record.reference_region));
                }
                CurrentWindowLabel=identificationFocus.Label;
                Physics.SyncTransforms();return;
            }
            string id=target.record.id,tissue=target.record.tissue;
            bool pleura=tissue=="membrane"&&id.Contains("Pleura"),lung=tissue=="lung",airway=tissue=="airway";
            bool valve=tissue=="cardiac"&&target.record.layer>=6;
            bool coronary=id=="FMA50039"||id=="FMA50040"||id=="FMA4706";
            bool mediastinal=tissue=="nerve"||tissue=="duct"||id=="FMA7131";
            bool diaphragm=id=="FMA13295";
            bool vessel=(tissue=="artery"||tissue=="vein")&&!coronary;
            bool heartWall=tissue=="cardiac"&&target.record.layer==5;
            bool prepared=heartWall||pleura||lung||airway||valve||coronary||mediastinal||diaphragm||vessel;
            if(!prepared){
                foreach(var structure in session.originals.Where(s=>s))structure.gameObject.SetActive(!structure.record.reference_region&&preparedContext.Contains(structure.record.id)&&structure.record.layer>=target.record.layer&&structure.record.tissue!="membrane");
                target.gameObject.SetActive(true);CurrentWindowLabel="Prepared chest-wall view";Physics.SyncTransforms();return;
            }
            foreach(var structure in session.originals.Where(s=>s&&!s.record.reference_region))
            {
                var r=structure.record;bool visible=preparedContext.Contains(r.id);
                // Exterior anatomy is removed as an explicitly prepared view, not as a simulated operation.
                if(r.layer<=3||r.tissue=="membrane")visible=false;
                if((heartWall||airway||valve||coronary||mediastinal||diaphragm||vessel)&&r.tissue=="lung")visible=false;
                if((airway||valve||mediastinal)&&r.tissue=="cardiac"&&r.layer==5)visible=false;
                if(airway&&(r.tissue=="artery"||r.tissue=="vein"))visible=false;
                if(valve&&(r.id=="FMA3736"||r.id=="FMA8612"))visible=false;
                if(mediastinal&&(r.id=="FMA3736"||r.id=="FMA8612"||r.id=="FMA50872"||r.id=="FMA50873"))visible=false;
                if(id=="FMA7131"&&r.tissue=="airway")visible=false;
                // Only one pleural surface is assessed in its own prepared layer view.
                if(pleura&&r.id==id)visible=true;
                if(r.id==id)visible=true;
                structure.gameObject.SetActive(visible);
            }
            CurrentWindowLabel=pleura?id.Contains("Parietal")?"Prepared pleural view · outer parietal surface":"Prepared pleural view · parietal layer removed":
                lung?"Prepared lung view · chest wall and pleural cover removed":
                airway?"Prepared airway view · lungs and overlying vessels removed":
                valve?"Prepared valve view · chamber and outflow walls opened":
                coronary?"Prepared coronary view · lungs and outer layers removed":
                mediastinal?"Prepared mediastinal view · overlying organs removed":
                diaphragm?"Prepared diaphragm view · lungs removed":"Prepared vessel view · lungs and outer layers removed";
            Physics.SyncTransforms();
        }

        /// <summary>Exploration cutaway only. Runtime must keep answer-revealing view changes out of assessments.</summary>
        public void ShowLayer(SpecimenLayer layer)
        {
            if (!session || session.busy) return;
            CurrentLayer = layer;
            identificationFocus=null;
            CurrentWindowLabel=null;
            RefreshMaterials();
            foreach (var structure in session.originals.Where(s => s))
            {
                bool visible = !structure.record.reference_region && structure.record.layer >= (int)layer;
                if (layer >= SpecimenLayer.ChestWall && structure.record.tissue == "membrane") visible = false;
                structure.gameObject.SetActive(visible);
            }
        }

        /// <summary>
        /// Called after a guided exposure is accepted, including checkpoint restore.
        /// Retire that layer and its fragments so completed body-shaped shells do not
        /// accumulate beside the specimen. This never grants assessment credit.
        /// </summary>
        public void RevealBelow(AnatomyStructure openedStructure)
        {
            if (!session || !openedStructure) return;
            foreach (var structure in session.originals.Where(s => s && !s.record.reference_region))
                if (structure.record.layer > openedStructure.record.layer) structure.gameObject.SetActive(true);
            session.RetireCompletedLayer(openedStructure);
            RefreshMaterials();
        }

        public void RestoreOriginalVisibility() => ShowIntact();

        /// <summary>
        /// A geometric access check for the runtime evaluator: at least one sampled real
        /// surface must be reachable without another active anatomy collider in front.
        /// Passing this check alone never proves a dissection objective is complete.
        /// </summary>
        public bool TryGetExposedSurface(AnatomyStructure target, Vector3 observer, out Vector3 surface)
        {
            surface = default;
            if (!target || !target.gameObject.activeInHierarchy) { LastSurfaceCheck="Target is not displayed."; return false; }
            var collider = target.GetComponent<MeshCollider>();
            if (!collider || !collider.enabled) { LastSurfaceCheck="Target has no active surface collider."; return false; }
            var bounds = collider.bounds;
            var samples=CachedSurfaceSamples(collider.sharedMesh);
            int targetHits=0,blockedHits=0,saturatedQueries=0;
            string closestBlocker=null;float closestGap=float.PositiveInfinity;
            for (int sample = 0; sample < 7+samples.Length; sample++)
            {
                var point = bounds.center;
                if(sample>=7)point=collider.transform.TransformPoint(samples[sample-7]);
                else if (sample > 0) { int axis = (sample - 1) / 2; point[axis] += bounds.extents[axis] * ((sample % 2 == 1) ? .75f : -.75f); }
                Vector3 delta = point - observer;
                if (delta.sqrMagnitude < .000001f) continue;
                var ray = new Ray(observer, delta.normalized);
                if (!collider.Raycast(ray, out var targetHit, delta.magnitude + bounds.size.magnitude)) continue;
                targetHits++;
                int count = Physics.RaycastNonAlloc(ray, surfaceHits, targetHit.distance + .002f, ~0, QueryTriggerInteraction.Ignore);
                // Saturation is ambiguous: never treat a truncated query as clear access.
                if (count == surfaceHits.Length) { saturatedQueries++; continue; }
                bool blocked = false;
                for (int hitIndex = 0; hitIndex < count; hitIndex++)
                {
                    var hit = surfaceHits[hitIndex];
                    var anatomy = hit.collider.GetComponentInParent<AnatomyStructure>();
                    if (anatomy && anatomy != target && !anatomy.record.reference_region && hit.distance < targetHit.distance - .001f)
                    {
                        blocked = true;
                        float gap=targetHit.distance-hit.distance;
                        if(gap<closestGap){closestGap=gap;closestBlocker=anatomy.record.id;}
                    }
                }
                if (!blocked) { surface = targetHit.point; LastSurfaceCheck=$"Exposed real surface · {target.record.id} · sample {sample+1}"; return true; }
                blockedHits++;
            }
            LastSurfaceCheck=$"{target.record.id}: {targetHits} surface rays, {blockedHits} blocked, {saturatedQueries} saturated; nearest covering anatomy {closestBlocker??"none"}.";
            return false;
        }

        public bool TryGetCutGuide(AnatomyStructure target, string region, Vector3 observer, Vector3[] points, out Vector3 normal)
        {
            normal=Vector3.up;
            if(!target||!target.gameObject.activeInHierarchy||!target.record.cutting_ready)return false;
            var collider=target.GetComponent<MeshCollider>();
            if(!collider||!collider.enabled)return false;
            float margin=.025f/Mathf.Max(.001f,Mathf.Abs(target.transform.lossyScale.x));
            bool OnSide(Vector3 world){float x=target.transform.InverseTransformPoint(world).x;return region=="left"?x<=target.SourceBounds.center.x-margin:region!="right"||x>=target.SourceBounds.center.x+margin;}
            bool Surface(Vector3 point,out RaycastHit hit){var ray=new Ray(observer,(point-observer).normalized);return Physics.Raycast(ray,out hit,Vector3.Distance(point,observer)+.05f,~0,QueryTriggerInteraction.Ignore)&&hit.collider==collider&&OnSide(hit.point);}
            // Bounded source-mesh samples; never draw a route through a covering layer.
            int routeTrials=0;
            foreach(var local in CachedSurfaceSamples(collider.sharedMesh))
            {
                var point=target.transform.TransformPoint(local);
                if(!OnSide(point)||!Surface(point,out var start))continue;
                var towardObserver=(observer-start.point).normalized;
                if(Vector3.Dot(start.normal,towardObserver)<.6f)continue;
                if(routeTrials++==8)break;
                // A lateral stroke remains readable instead of collapsing along the viewing ray.
                var tangent=Vector3.Cross(start.normal,towardObserver).normalized;
                if(tangent.sqrMagnitude<.1f)tangent=Vector3.ProjectOnPlane(Vector3.right,start.normal).normalized;
                for(int direction=0;direction<4;direction++)
                {
                    var along=Quaternion.AngleAxis(direction*90,start.normal)*tangent;bool clear=true;
                    for(int i=0;i<points.Length;i++)
                    {
                        if(!Surface(start.point+along*(i*.005f),out var hit)||(i>0&&Vector3.Distance(hit.point,points[i-1])>.01f)){clear=false;break;}
                        points[i]=hit.point;
                    }
                    if(clear){normal=start.normal;return true;}
                }
            }
            return false;
        }

        Vector3[] CachedSurfaceSamples(Mesh mesh)
        {
            if(!mesh||!mesh.isReadable)return Array.Empty<Vector3>();
            if(surfaceSamples.TryGetValue(mesh,out var cached))return cached;
            // Narrow coronary branches and nerves frequently contain no surface on an
            // AABB centre ray. Sample their actual triangles without weakening occlusion.
            // Bound both per-query work and retained caches as cut meshes are replaced.
            if(surfaceSamples.Count>=128)surfaceSamples.Clear();
            var vertices=mesh.vertices;var indices=mesh.triangles;
            int triangleCount=indices.Length/3,count=Math.Min(256,triangleCount);
            var samples=new Vector3[count];
            for(int i=0;i<count;i++)
            {
                int offset=(int)(((long)i*triangleCount+triangleCount/2)/count)*3;
                samples[i]=(vertices[indices[offset]]+vertices[indices[offset+1]]+vertices[indices[offset+2]])/3f;
            }
            surfaceSamples[mesh]=samples;return samples;
        }

        public void RefreshMaterials()
        {
            if (!session) return;
            foreach (var structure in session.originals.Where(s => s && !s.record.reference_region))
            {
                var outer = MaterialFor(structure.record.AppearanceTissue, false);
                var inner = MaterialFor(structure.record.AppearanceTissue, true);
                // Bone caps retain the existing depth-derived cortical/cancellous shader.
                if (inner && structure.record.tissue != "bone") structure.interiorMaterial = inner;
                if (!outer) continue;
                structure.SetRestingMaterials(outer, structure.interiorMaterial);
                foreach (var renderer in structure.GetComponentsInChildren<Renderer>(true))
                {
                    var materials = renderer.sharedMaterials;
                    if (materials.Length == 0) continue;
                    materials[0] = outer;
                    if (materials.Length > 1 && inner && structure.record.tissue != "bone") materials[1] = inner;
                    renderer.sharedMaterials = materials;
                }
            }
        }

        Material MaterialFor(string tissue, bool interior)
        {
            string key = (interior ? "Interior_" : "Tissue_") + tissue;
            if (presentationMaterials.TryGetValue(key, out var material)) return material;
            var source = Resources.Load<Material>("Learning/Specimen/" + key);
            if (!source) { presentationMaterials[key] = null; return null; }
            material = new Material(source) { name = "VR presentation " + key, enableInstancing = true };
            generated.Add(material); presentationMaterials[key] = material;
            return material;
        }

        Bounds SpecimenBounds()
        {
            var skin = session.originals.FirstOrDefault(s => s && s.record.id == "FMA7163");
            if (skin) return skin.Renderer.bounds;
            var renderers = session.originals.Where(s => s && !s.record.reference_region).Select(s => s.Renderer).ToArray();
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }

        void ConfigureLighting()
        {
            var bounds = SpecimenBounds();
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.19f, .21f, .23f);
            RenderSettings.ambientEquatorColor = new Color(.105f, .12f, .14f);
            RenderSettings.ambientGroundColor = new Color(.045f, .05f, .055f);

            var key = GameObject.Find("Specimen examination light")?.GetComponent<Light>();
            if (key)
            {
                key.transform.position = bounds.center + new Vector3(.28f, .8f, -.2f);
                key.intensity = ExaminationIntensity; key.color = new Color(1f, .99f, .98f); key.range = 2.2f;
                key.shadows = LightShadows.None;
            }
            AddLight("Neutral specimen fill", bounds.center + new Vector3(-.48f, .48f, .12f), new Color(.94f, .97f, 1f), .18f);
            AddLight("Specimen boundary light", bounds.center + new Vector3(.1f, .48f, .48f), new Color(1f, .98f, .95f), .13f);
        }

        void AddLight(string name, Vector3 position, Color color, float intensity)
        {
            var go = new GameObject(name); go.transform.SetParent(session.specimen, true); go.transform.position = position;
            var light = go.AddComponent<Light>(); light.type = LightType.Point; light.color = color; light.intensity = intensity;
            light.range = 1.6f; light.shadows = LightShadows.None; light.renderMode = LightRenderMode.ForceVertex;
            fixtures.Add(go);
        }

        void CreateExaminationDrape()
        {
            var bounds = SpecimenBounds();
            var go = new GameObject("Reusable examination drape — equipment, not anatomy");
            go.transform.SetParent(session.specimen, true);
            float surface = bounds.min.y;
            var tabletop = session.TabletopCollider;
            if (tabletop && tabletop.Raycast(new Ray(bounds.center + Vector3.up, Vector3.down), out var hit, 2f))
                surface = hit.point.y;
            go.transform.position = new Vector3(bounds.center.x, surface + .0025f, bounds.center.z);
            var mesh = new Mesh { name = "Drape surrounding original specimen boundary" };
            const int segments = 48;
            const int rings = 4;
            int center = segments * rings;
            var vertices = new Vector3[center + 1]; var uv = new Vector2[vertices.Length]; var indices = new int[segments * ((rings - 1) * 6 + 3)];
            float innerX = bounds.extents.x + .012f, innerZ = bounds.extents.z + .018f;
            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2 / segments;
                float x = Mathf.Cos(angle), z = Mathf.Sin(angle);
                var inner = new Vector2(x * innerX, z * innerZ);
                var outer = new Vector2(.405f * Mathf.Sign(x) * Mathf.Pow(Mathf.Abs(x), .38f),
                    .54f * Mathf.Sign(z) * Mathf.Pow(Mathf.Abs(z), .38f));
                int cap = segments * (rings - 1) * 6 + i * 3;
                indices[cap] = center; indices[cap + 1] = ((i + 1) % segments) * rings; indices[cap + 2] = i * rings;
                for (int ring = 0; ring < rings; ring++)
                {
                    float t = ring == 0 ? 0 : ring == 1 ? .08f : ring == 2 ? .94f : 1;
                    var point = Vector2.Lerp(inner, outer, t);
                    float fold = .0011f * Mathf.Sin(7 * angle + 2 * t);
                    int a = i * rings + ring;
                    vertices[a] = new Vector3(point.x, fold + (ring == 1 || ring == 2 ? .0016f : 0), point.y);
                    uv[a] = point;
                    if (ring == rings - 1) continue;
                    int b = ((i + 1) % segments) * rings + ring, offset = (i * (rings - 1) + ring) * 6;
                    indices[offset] = a; indices[offset + 1] = b; indices[offset + 2] = a + 1;
                    indices[offset + 3] = a + 1; indices[offset + 4] = b; indices[offset + 5] = b + 1;
                }
            }
            mesh.vertices = vertices; mesh.uv = uv; mesh.triangles = indices; mesh.RecalculateNormals(); mesh.RecalculateBounds();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>(); renderer.shadowCastingMode = ShadowCastingMode.Off;
            var linen = session.table && session.table.parent ? session.table.parent.Find("Lab_Blue")?.GetComponent<Renderer>() : null;
            if (linen) renderer.sharedMaterial = linen.sharedMaterial;
            else
            {
                var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Examination linen", color = new Color(.18f, .42f, .41f) };
                material.SetFloat("_Smoothness", .12f); renderer.sharedMaterial = material; generated.Add(material);
            }
            generated.Add(mesh); fixtures.Add(go);
        }

        void OnDestroy()
        {
            foreach (var entry in originalMaterials) if (entry.Key) entry.Key.sharedMaterials = entry.Value;
            foreach (var entry in originalInteriors)
            {
                if (!entry.Key) continue;
                entry.Key.interiorMaterial = entry.Value;
                if (originalMaterials.TryGetValue(entry.Key.Renderer, out var resting) && resting.Length > 0)
                    entry.Key.SetRestingMaterials(resting[0], resting.Length > 1 ? resting[1] : null);
            }
            foreach (var fixture in fixtures) if (fixture) Destroy(fixture);
            foreach (var asset in generated) if (asset) Destroy(asset);
        }
    }
}
