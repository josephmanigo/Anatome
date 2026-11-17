using System;
using System.Collections.Generic;
using System.Linq;

namespace ThoraxLab.Learning
{
    // Identify & Enumerate uses a complete topic model, including related parts
    // that are not answers in the current difficulty. Covers can be opened for
    // internal anatomy without changing meshes, scoring or saved evidence.
    public sealed class IdentificationFocus
    {
        readonly HashSet<string> members;
        readonly HashSet<string> groups;
        readonly string subject;
        string targetId, targetTissue;
        int targetLayer;

        public IdentificationFocus(LearningUnit unit, IEnumerable<string> objectiveTargets)
        {
            members = new HashSet<string>((unit?.anatomyIds ?? Array.Empty<string>())
                .Concat(objectiveTargets ?? Array.Empty<string>()));
            string[] context;
            switch (unit?.id)
            {
                case "chest-wall":
                    subject = "chest-wall model";
                    context = new[] { "sternum", "ribs", "costal_cartilages" }; break;
                case "pleura-lungs":
                    subject = "lung model";
                    context = new[] { "lung_lobes", "pleural_layers", "diaphragm" }; break;
                case "airways":
                    subject = "airway model";
                    context = new[] { "trachea", "main_bronchi", "lobar_bronchial_trees", "remaining_airway_branches" }; break;
                case "heart-coronaries":
                case "vessels-valves":
                    subject = unit.id == "heart-coronaries" ? "heart model" : "heart and great vessels";
                    context = new[] { "atrial_walls", "ventricular_walls", "coronary_vessels",
                        "mitral_valve", "tricuspid_valve", "aortic_valve", "pulmonary_valve", "papillary_muscles" }; break;
                case "mediastinum":
                    subject = "mediastinal model";
                    context = new[] { "esophagus", "phrenic_nerves", "vagus_nerves", "sympathetic_trunks", "thoracic_duct" }; break;
                default:
                    subject = "activity anatomy";
                    context = Array.Empty<string>(); break;
            }
            groups = new HashSet<string>(context);
        }

        public bool Includes(string id, string group, bool referenceRegion) =>
            !referenceRegion && (members.Contains(id) || groups.Contains(group));

        public void SetTarget(string id, string tissue, int layer)
        { targetId = id; targetTissue = tissue; targetLayer = layer; }

        bool ValveView => targetTissue == "cardiac" && targetLayer >= 6;
        public string Label => "Focused " + (targetId == "FMA13295" ? "diaphragm model" : subject) +
            (ValveView ? " · internal cutaway" : targetTissue == "membrane" ? " · pleural surface exposed" : "");

        public bool IsVisible(string id, string group, string tissue, int layer, bool referenceRegion)
        {
            if (!Includes(id, group, referenceRegion)) return false;
            if (id == targetId) return true;
            // Pleura and pericardium must not cover the structures being identified.
            if (tissue == "membrane") return false;
            if (ValveView && ((tissue == "cardiac" && layer == 5) ||
                group == "great_vessels" || group == "pulmonary_vessels")) return false;
            if (targetId == "FMA13295" && tissue == "lung") return false;
            if (targetId == "FMA7131" && tissue == "airway") return false;
            return true;
        }
    }
}
