using System;
using System.Collections.Generic;
using System.Linq;

namespace ThoraxLab.Learning
{
    public sealed class MuseumEntry
    {
        public string Id { get; }
        public string Name { get; }
        public string System { get; }
        public string Group { get; }
        public string Description { get; }
        public bool IsReference { get; }

        public MuseumEntry(string id, string name, string system, string group, string tissue, bool reference)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(system))
                throw new ArgumentException("Museum entries need an identity, name and system.");
            Id = id; Name = name; System = system; Group = group ?? ""; IsReference = reference;
            Description = MuseumDescriptions.Describe(Group, tissue, name, reference, id);
        }
    }

    // The museum is an ungraded collection, separate from saved assessment modes.
    public sealed class MuseumCollection
    {
        public const int PageSize = 6;
        readonly MuseumEntry[] entries;
        readonly Dictionary<string, MuseumEntry> byId;
        public IReadOnlyList<MuseumEntry> Entries => entries;
        public string[] Systems { get; }

        public MuseumCollection(IEnumerable<MuseumEntry> source)
        {
            entries = source.OrderBy(e => e.System, StringComparer.Ordinal)
                .ThenBy(e => e.Group, StringComparer.Ordinal).ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(e => e.Id, StringComparer.Ordinal).ToArray();
            byId = entries.ToDictionary(e => e.Id, StringComparer.Ordinal);
            Systems = entries.Select(e => e.System).Distinct().ToArray();
        }
        public MuseumEntry Find(string id) => byId.TryGetValue(id, out var entry) ? entry : null;
        public MuseumEntry[] InSystem(string system) => entries.Where(e => e.System == system).ToArray();
        public int PageCount(string system) => (InSystem(system).Length + PageSize - 1) / PageSize;
        public MuseumEntry[] Page(string system, int page)
        {
            int count = PageCount(system);
            return count == 0 ? Array.Empty<MuseumEntry>() : InSystem(system)
                .Skip(Math.Max(0, Math.Min(page, count - 1)) * PageSize).Take(PageSize).ToArray();
        }
        public MuseumEntry[] Search(string query)
        {
            query = (query ?? "").Trim();
            return entries.Where(e => e.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                e.System.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                e.Description.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0).ToArray();
        }
    }

    public static class MuseumDescriptions
    {
        static readonly Dictionary<string, string> descriptions = new Dictionary<string, string>
        {
            ["ribs"] = "Curved bone of the chest wall. The ribs protect the heart and lungs and move during breathing.",
            ["costal_cartilages"] = "Flexible cartilage at the front of a rib. It supports the chest wall while allowing movement during breathing.",
            ["sternum"] = "Part of the breastbone at the front of the chest. It helps support and protect the thoracic organs.",
            ["thoracic_vertebrae"] = "A bone of the thoracic spine. It supports the trunk, protects the spinal cord and articulates with ribs.",
            ["thoracic_discs"] = "A fibrocartilage pad between adjacent thoracic vertebrae. It cushions loads and allows limited spinal movement.",
            ["shoulder_girdle"] = "Part of the shoulder girdle. It supports the shoulder and helps connect the upper limb to the trunk.",
            ["pectoralis_major"] = "Part of the large superficial chest muscle. It helps bring the upper arm toward the body and rotate it inward.",
            ["pectoralis_minor"] = "A small muscle beneath pectoralis major. It draws the shoulder blade forward and downward against the chest wall.",
            ["serratus_anterior"] = "Muscle along the side of the rib cage. It holds the shoulder blade against the chest and helps rotate it upward.",
            ["intercostal_layers"] = "Muscle between the ribs. The intercostal layers support the spaces between ribs and help move the chest wall.",
            ["diaphragm"] = "The main muscle of breathing, separating chest and abdomen. Contraction lowers its dome and draws air into the lungs.",
            ["thoracic_skin"] = "The outer covering of the chest. It forms a protective barrier and contains sensory receptors.",
            ["trachea"] = "The windpipe carries air toward the lungs. Cartilage supports its wall before it divides into the main bronchi.",
            ["main_bronchi"] = "A main airway entering a lung from the trachea. It divides into smaller airways that distribute air within the lung.",
            ["lobar_bronchial_trees"] = "Branching airways supplying a lung lobe. They conduct air to and from progressively smaller airways.",
            ["remaining_airway_branches"] = "Additional branches of the tracheobronchial tree. These air passages continue the route into the lungs.",
            ["ventricular_walls"] = "Muscular walls of the heart's lower chambers. Their contraction pumps blood toward the lungs and the body.",
            ["tricuspid_valve"] = "A leaflet of the valve between the right atrium and right ventricle. Closure helps prevent blood flowing back into the atrium.",
            ["mitral_valve"] = "A leaflet of the valve between the left atrium and left ventricle. Closure helps prevent blood flowing back into the atrium.",
            ["pulmonary_valve"] = "A cusp of the valve at the right ventricular outlet. It helps prevent blood returning from the pulmonary trunk.",
            ["aortic_valve"] = "A cusp of the valve at the left ventricular outlet. It helps prevent blood returning from the aorta.",
            ["papillary_muscles"] = "Muscle inside a ventricle that tensions the chordae tendineae. It helps keep valve leaflets from prolapsing during contraction.",
            ["azygos_system"] = "Part of the venous drainage along the thoracic spine. The azygos system returns blood from the chest wall toward the superior vena cava.",
            ["esophagus"] = "The muscular tube carrying swallowed food and liquid toward the stomach. It descends through the chest behind the trachea.",
            ["lung_lobes"] = "CT lobe from the Mauritanian Anatomy Laboratory. Three lobes form the right lung and two form the left. The lab's airways and vessels come from a different donor.",
            ["pericardium"] = "The sac around the heart. Its serous surfaces slide as the heart beats. This teaching reconstruction simplifies its attachments to the great vessels.",
            ["thoracic_fat"] = "Subcutaneous tissue beneath the skin. Its fat cushions the chest wall, stores energy and helps insulate the body.",
            ["thoracic_fascia"] = "Connective tissue supporting and separating structures of the chest wall. It surrounds muscles and helps organize tissue layers.",
            ["phrenic_nerves"] = "Nerve carrying motor signals to the diaphragm. It also carries sensation from parts of the diaphragm, pleura and pericardium.",
            ["vagus_nerves"] = "Nerve contributing parasympathetic and sensory pathways to thoracic organs, including the heart, lungs and esophagus.",
            ["sympathetic_trunks"] = "A chain of sympathetic ganglia beside the spine. It helps distribute autonomic signals to the chest and other body regions.",
            ["thoracic_duct"] = "The main lymphatic channel in the chest. It returns lymph from much of the body to the venous circulation near the left neck."
        };

        public static string Describe(string group, string tissue, string name, bool reference, string id = null)
        {
            if (reference)
            {
                if (group == "cardiac_chambers")
                    return "Reference outline of a heart chamber's cavity. It represents the space containing blood, not the surrounding muscle wall.";
                if (group == "cardiac_septa")
                    return name.IndexOf("Interatrial", StringComparison.OrdinalIgnoreCase) >= 0 ?
                        "Reference marker for the region separating the right and left atria. The marker is not a separately segmented tissue wall." :
                        "Reference marker for the region separating the right and left ventricles. The marker is not a separately segmented tissue wall.";
                return "Reference marker showing an anatomical region. It is an annotation rather than a separate tissue structure.";
            }
            switch (group)
            {
                case "intercostal_layers" when id == "FMA9761" || id == "FMA9762":
                    return "Transversus thoracis lies on the inner anterior chest wall. Its slips run from the posterior sternum toward the costal cartilages, usually ribs 2 through 6.";
                case "atrial_walls": return name.IndexOf("right", StringComparison.OrdinalIgnoreCase) >= 0 ?
                    "Muscular wall of the right atrium. This chamber receives venous blood from the body and passes it to the right ventricle." :
                    "Muscular wall of the left atrium. This chamber receives oxygenated blood from the lungs and passes it to the left ventricle.";
                case "pleural_layers": return name.IndexOf("visceral", StringComparison.OrdinalIgnoreCase) >= 0 ?
                    "Visceral pleura covers the lung surface and extends into its fissures. This teaching envelope simplifies the fissures and the fold at the lung root." :
                    "Parietal pleura lines the chest wall, diaphragm and mediastinum. This teaching envelope illustrates its position around the lung.";
                case "great_vessels": return tissue == "vein" ?
                    "A major vein connected to the thoracic circulation. It carries blood from the body toward the right side of the heart." :
                    "A major artery connected to the thoracic circulation. It carries blood away from the heart toward the body's tissues.";
                case "pulmonary_vessels": return tissue == "vein" ?
                    "A pulmonary vein carries oxygenated blood from the lungs toward the left atrium." :
                    "A pulmonary artery carries oxygen-poor blood from the right ventricle toward the lungs for gas exchange.";
                case "coronary_vessels": return tissue == "vein" ?
                    "A vein draining blood from the heart muscle into the cardiac venous circulation." :
                    "A coronary artery supplies oxygenated blood to the heart muscle so it can keep contracting.";
                case "intercostal_vessels":
                case "intercostal_neurovascular_bundles": return tissue == "nerve" ?
                    "Nerve running in an intercostal space. It carries sensation from the chest wall and motor signals to chest-wall muscles." : tissue == "vein" ?
                    "A vein draining the chest wall. It returns blood from tissues around the ribs toward the larger thoracic veins." :
                    "An artery supplying the chest wall. It delivers blood to tissues around the ribs and intercostal spaces.";
                default: return descriptions.TryGetValue(group ?? "", out var description) ? description :
                    "An anatomical structure of the thorax, displayed separately so its shape can be examined.";
            }
        }
    }
}
