using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using ThoraxLab.Learning;

static class IdentificationFocusTests
{
    sealed class Anatomy
    {
        public string id { get; set; }
        public string required_group { get; set; }
        public string tissue { get; set; }
        public int layer { get; set; }
        public bool reference_region { get; set; }
    }
    sealed class AnatomyCatalog { public Anatomy[] records { get; set; } }

    public static void Run(LearningCatalog catalog, string anatomyPath, Action<bool, string> check)
    {
        var anatomy=JsonSerializer.Deserialize<AnatomyCatalog>(File.ReadAllText(anatomyPath)).records;
        var byId=anatomy.ToDictionary(r=>r.id);
        bool Visible(IdentificationFocus focus,string id)
        {
            var r=byId[id];return focus.IsVisible(r.id,r.required_group,r.tissue,r.layer,r.reference_region);
        }
        void Target(IdentificationFocus focus,string id)
        {var r=byId[id];focus.SetTarget(r.id,r.tissue,r.layer);}
        IdentificationFocus Focus(string unitId)
        {
            var u=catalog.units.Single(u=>u.id==unitId);
            return new IdentificationFocus(u,u.variants.Where(v=>v.mode==LearningMode.IdentifyEnumerate).SelectMany(v=>v.objectives).Select(o=>o.targetId));
        }
        int variants=0,targets=0;
        foreach(var unit in catalog.units)
        {
            HashSet<string> previous=null;
            foreach(var variant in unit.variants.Where(v=>v.mode==LearningMode.IdentifyEnumerate))
            {
                variants++;
                var focus=new IdentificationFocus(unit,variant.objectives.Select(o=>o.targetId));
                var context=anatomy.Where(r=>focus.Includes(r.id,r.required_group,r.reference_region)).Select(r=>r.id).ToHashSet();
                check(unit.anatomyIds.All(context.Contains),unit.id+": the topic model must include every part needed at later difficulties.");
                if(previous!=null)check(previous.SetEquals(context),unit.id+": the model must not shrink to just the answers at an easier difficulty.");
                previous=context;
                foreach(var objective in variant.objectives)
                {
                    targets++;Target(focus,objective.targetId);
                    check(Visible(focus,objective.targetId),variant.id+": the current target must remain visible: "+objective.targetId);
                    check(!Visible(focus,"FMA7163")&&!Visible(focus,"REC_fat")&&!Visible(focus,"REC_fascia"),variant.id+": the full torso covering must not return.");
                    check(anatomy.Where(r=>r.reference_region).All(r=>!Visible(focus,r.id)),variant.id+": reference regions must not become selectable tissue.");
                }
            }
        }
        check(variants==18&&targets==96,"Focused model coverage must include all 18 identification variants and 96 target steps.");

        var heart=Focus("heart-coronaries");Target(heart,"FMA50039");
        check(new[]{"FMA9457","FMA9531","FMA13884","FMA50039","FMA50040","FMA4706","FMA4707"}.All(id=>Visible(heart,id)),"Coronary identification needs a recognizable heart and companion vessels, not an isolated answer.");
        check(new[]{"MAL_RUL","MAL_LUL","FMA7486","FMA7394","FMA7131","FMA13295","REC_Pericardium","REC_Left_Phrenic_nerve","FMA87217"}.All(id=>!Visible(heart,id)),"Heart identification must exclude lungs, ribs, airways, esophagus, diaphragm, pericardium and unrelated vessels/nerves.");
        Target(heart,"FMA7242");
        check(Visible(heart,"FMA7242")&&Visible(heart,"FMA7243")&&!Visible(heart,"FMA13884")&&!Visible(heart,"FMA3736")&&!Visible(heart,"FMA8612"),"Mitral identification must expose both leaflets without chamber or outflow walls covering them.");
        check(heart.Label.Contains("heart")&&heart.Label.Contains("cutaway"),"Internal heart views must be labelled as cutaways.");
        Target(heart,"FMA50039");
        check(Visible(heart,"FMA13884")&&Visible(heart,"FMA3736")&&!Visible(heart,"MAL_RUL"),"Returning to coronary anatomy must restore heart context without restoring the thorax.");

        var lungs=Focus("pleura-lungs");Target(lungs,"MAL_RUL");
        check(anatomy.Where(r=>r.tissue=="lung").All(r=>Visible(lungs,r.id)),"Lung identification must show all lobes, including non-answer context.");
        check(!Visible(lungs,"REC_Right_Visceral_Pleura")&&!Visible(lungs,"REC_Right_Parietal_Pleura")&&!Visible(lungs,"FMA13884")&&!Visible(lungs,"FMA7486"),"Lung lobes must not be covered by pleura or a full thorax.");
        Target(lungs,"REC_Right_Visceral_Pleura");
        check(Visible(lungs,"REC_Right_Visceral_Pleura")&&!Visible(lungs,"REC_Right_Parietal_Pleura"),"Visceral pleura needs its own exposed surface.");
        Target(lungs,"REC_Right_Parietal_Pleura");
        check(Visible(lungs,"REC_Right_Parietal_Pleura")&&!Visible(lungs,"REC_Right_Visceral_Pleura"),"Changing pleural target must restore the requested surface.");
        Target(lungs,"FMA13295");
        check(Visible(lungs,"FMA13295")&&anatomy.Where(r=>r.tissue=="lung").All(r=>!Visible(lungs,r.id)),"Diaphragm identification must remove the overlying lung lobes.");
        Target(lungs,"MAL_RUL");check(Visible(lungs,"MAL_LLL"),"Returning from diaphragm to lungs must restore the complete lung model.");

        var airway=Focus("airways");Target(airway,"FMA7394");
        check(anatomy.Where(r=>r.tissue=="airway").All(r=>Visible(airway,r.id)),"Airway identification needs the complete branching tree.");
        check(new[]{"MAL_RUL","MAL_LUL","FMA13884","FMA3736","FMA7131","FMA7486"}.All(id=>!Visible(airway,id)),"Airway identification must remove organs and vessels that cover the branches.");
        var vessels=Focus("vessels-valves");Target(vessels,"FMA7252");
        check(new[]{"FMA7252","FMA7253","FMA7254"}.All(id=>Visible(vessels,id))&&!Visible(vessels,"FMA3736")&&!Visible(vessels,"FMA13884"),"All aortic cusps must be exposed in the valve cutaway.");
        var chest=Focus("chest-wall");Target(chest,"FMA7486");
        check(Visible(chest,"FMA7857")&&Visible(chest,"FMA7487")&&!Visible(chest,"FMA13884")&&!Visible(chest,"MAL_RUL"),"Chest-wall identification should show the rib/sternal model without internal organs.");
        var mediastinum=Focus("mediastinum");Target(mediastinum,"FMA7131");
        check(Visible(mediastinum,"REC_ThoracicDuct")&&Visible(mediastinum,"REC_Left_Vagus_nerve")&&!Visible(mediastinum,"FMA7394")&&!Visible(mediastinum,"FMA13884")&&!Visible(mediastinum,"MAL_RUL"),"Mediastinal identification must expose esophagus, nerves and duct without overlying organs.");
        var future=new IdentificationFocus(new LearningUnit{id="future-topic",anatomyIds=new[]{"FMA9457"}},new[]{"FMA50039"});Target(future,"FMA50039");
        check(Visible(future,"FMA50039")&&Visible(future,"FMA9457")&&!Visible(future,"MAL_RUL"),"Unknown topics must retain declared targets and anatomy without falling back to the full thorax.");
    }
}
