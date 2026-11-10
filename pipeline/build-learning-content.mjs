import fs from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
import {dissectionPlan} from './learning-dissection.mjs';
import {casePathways} from './learning-case-pathways.mjs';
import {reasoningSteps} from './learning-case-reasoning.mjs';

const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'..');
const destination=path.join(root,'unity/ThoraxLab/Assets/ThoraxLab/Resources/Learning');
fs.mkdirSync(destination,{recursive:true});
const anatomy=JSON.parse(fs.readFileSync(path.join(root,'unity/ThoraxLab/Assets/ThoraxLab/anatomy-catalog.json'),'utf8')).records;
const names=new Map(anatomy.map(r=>[r.id,r.name]));
const records=new Map(anatomy.map(r=>[r.id,r]));
const version='2026.10.2';
const sources=[
 ['bodyparts','BodyParts3D anatomy source','https://dbarchive.biosciencedbc.jp/en/bodyparts3d/desc.html'],
 ['bts','British Thoracic Society: pleural procedures','https://www.brit-thoracic.org.uk/clinical-resources/clinical-statements/pleural-procedures/'],
 ['asthma','NHLBI: asthma treatment and action plan','https://www.nhlbi.nih.gov/health/asthma/treatment-action-plan'],
 ['pneumonia','NHLBI: pneumonia treatment','https://www.nhlbi.nih.gov/health/pneumonia/treatment'],
 ['coronary','ESC: chronic coronary syndromes','https://www.escardio.org/guidelines/clinical-practice-guidelines/all-esc-practice-guidelines/chronic-coronary-syndromes/'],
 ['valves','ESC/EACTS: valvular heart disease','https://www.escardio.org/guidelines/clinical-practice-guidelines/all-esc-practice-guidelines/valvular-heart-disease/'],
 ['tavr','NHLBI: transcatheter aortic valve replacement','https://www.nhlbi.nih.gov/health/tavr'],
 ['pleural','NHLBI: pleural disorders treatment','https://www.nhlbi.nih.gov/health/pleural-disorders/treatment'],
 ['bts-safety','BTS: pleural procedure safety checks','https://www.brit-thoracic.org.uk/clinical-resources/interventional-procedures/national-safety-standards-for-invasive-procedures-bronchoscopy-and-pleural-procedures/'],
 ['bts-detail','BTS clinical statement: pleural procedure techniques','https://www.brit-thoracic.org.uk/document-library/clinical-statements/pleural-procedures/bts-clinical-statement-on-pleural-procedures/'],
 ['spacer','University Hospitals Sussex: metered dose inhaler with AeroChamber','https://www.uhsussex.nhs.uk/resources/inhaler-technique-metered-dose-inhalers-with-aerochamber-spacer/'],
 ['pci-process','NHS: what happens during coronary angioplasty','https://www.nhs.uk/tests-and-treatments/coronary-angioplasty/what-happens/'],
 ['pci-aftercare','Gloucestershire Hospitals: coronary angioplasty','https://www.gloshospitals.nhs.uk/your-visit/patient-information-leaflets/coronary-angioplasty-cgh/'],
 ['pneumonia-care','NICE NG250: pneumonia diagnosis and management','https://www.nice.org.uk/guidance/NG250/chapter/recommendations'],
 ['tavi-process','Guys and St Thomas: TAVI procedure','https://www.guysandstthomas.nhs.uk/health-information/tavi-aortic-stenosis/tavi-procedure'],
 ['tavi-assessment','Royal Brompton and Harefield: TAVI assessment and monitoring','https://www.rbht.nhs.uk/our-services/transcatheter-aortic-valve-implantation-tavi-for-aortic-stenosis'],
 ['arterial-routes','OpenStax Anatomy and Physiology: systemic arterial pathways','https://openstax.org/books/anatomy-and-physiology/pages/20-5-circulatory-pathways'],
 ['heart-dissection','Texas Tech University Health Sciences Center: thoracic wall, pleura and pericardium dissection','https://anatomy.ttuhscep.edu/schemes/thorax_wall.html'],
 ['coronary-dissection','Texas Tech University Health Sciences Center: heart dissection','https://anatomy.ttuhscep.edu/cardiovascular_system/heart.html'],
 ['mechanisms-asthma','NHLBI: asthma mechanisms','https://www.nhlbi.nih.gov/health/asthma'],
 ['mechanisms-pneumonia','NHLBI: pneumonia mechanisms','https://www.nhlbi.nih.gov/health/pneumonia'],
 ['mechanisms-pleural','NHLBI: pleural compartments','https://www.nhlbi.nih.gov/health/pleural-disorders']
].map(([id,title,url])=>({id,title,url,accessed:id.startsWith('mechanisms-')?'2026-10-04':'2026-09-06',usage:'Original educational summaries; linked reference, no reproduced figures or guideline tables.'}));

const units=[];
function unit(id,title,summary,lesson,system,ids,protectedIds=[]){
 if(id==='heart-coronaries')protectedIds=[...new Set([...protectedIds,'REC_Right_Phrenic_nerve','FMA50039','FMA50040'])];
 for(const key of [...ids,...protectedIds])if(!names.has(key))throw Error(`Unknown anatomy ${key} in ${id}`);
 const value={id,title,summary,lesson,system,isCase:false,narration:id,sourceIds:id==='heart-coronaries'?['bodyparts','heart-dissection','coronary-dissection']:['bodyparts'],anatomyIds:ids,protectedIds,variants:[]};
 for(let mode=1;mode<=3;mode++)for(let difficulty=0;difficulty<3;difficulty++){
  const plan=mode===3?dissectionPlan(id,ids):ids.slice(0,[3,5,ids.length][difficulty]).map(targetId=>({targetId,region:'all',action:mode===1?0:1}));
  for(const step of plan)if(!names.has(step.targetId))throw Error('Unknown dissection target '+step.targetId);
  const ordered=difficulty===0||mode===3;
  const objectiveId=(step,i)=>`${id}-${mode}-${difficulty}-${i}-${step.targetId.toLowerCase()}-${step.region}`;
  const objectives=plan.map((step,i)=>{
   const {targetId,region,action}=step,opening=action===2,bone=records.get(targetId).tissue==='bone';
   const location=region==='all'?'':` on the specimen's ${region} side`;
   const title=opening?`Remove a section of ${names.get(targetId)}${location}`:`${action===1?'Place':'Identify and preserve'} ${names.get(targetId)}`;
   const instruction=opening?`${bone?'Bone saw':'Scalpel'} is equipped automatically. Point at ${names.get(targetId)}${location}, hold trigger and sweep a short, continuous cut. Release trigger, grip the separated chunk, lift it clear and release. Continue until this region's removal meter is full. The remaining tissue stays anchored.`:action===1?`Grip ${names.get(targetId)}, match its ghost outline and release.`:`Point at ${names.get(targetId)} and press trigger. Examine its relationships and preserve it intact.`;
   return {id:objectiveId(step,i),title,instruction,explanation:opening?`A tissue section from ${names.get(targetId)}${location} was cut and lifted. The next prepared station does not imply that all remaining tissue was removed.`:`${names.get(targetId)} examined.`,targetId,action,region,narration:`anatomy-${id}-${mode}-${i}-${region}`,toolId:opening?bone?'dissection-saw':'dissection-scalpel':'',equipmentState:opening?`chunk-lifted:${region}`:'',essential:true,weight:1,maxDistance:mode===2?[.12,.065,.04][difficulty]:[.075,.04,.02][difficulty],maxAngle:mode===2?[55,35,20][difficulty]:[35,20,10][difficulty],minContactSeconds:0,minPathFraction:opening?[.003,.005,.008][difficulty]:0,prerequisites:ordered&&i?[objectiveId(plan[i-1],i-1)]:[]};
  });
  value.variants.push({id:`${id}-${mode}-${difficulty}`,unitId:id,version,mode,difficulty,ordered,briefing:mode===3?`${title}: complete every anatomical station. ${['Highlighted targets and coached steps.','Fewer cues and a larger required opening.','Independent work with the strictest removal requirement.'][difficulty]} Exterior coverings are prepared for this topic; only your own cut-and-lift actions earn credit.`:summary,debrief:'Review missed structures and repeat the activity. Prepared views and chunk extraction are teaching aids, not clinical procedures.',objectives});
 }
 units.push(value);
}
unit('chest-wall','Chest wall','Build a map of ribs, sternum and the protective thoracic cage.','The thoracic wall supports breathing and protects the organs beneath it. Begin at the manubrium, follow the body of the sternum and find its xiphoid process. Compare the first ribs with the lower ribs. Costal cartilage connects the anterior rib ends to the sternum in different patterns. Intercostal nerves and vessels need special attention when opening the chest wall. The removable model is a teaching aid; it does not represent a clinical incision plan.','Skeletal',['FMA7486','FMA7487','FMA7488','FMA7857','FMA7987','FMA7957','FMA8066','FMA7875'],['REC_Right_IC4_Nerve']);
unit('pleura-lungs','Pleura & lungs','Explore lung lobes, pleural layers and the diaphragm.','The right lung has three lobes and the left lung has two. Visceral pleura covers the lung; parietal pleura lines the thoracic cavity. The pleural space is normally a potential space between those surfaces. The diaphragm forms the floor of the thorax. Keep anatomical left and right anchored to the specimen, not your screen.','Respiratory',['MAL_RUL','MAL_RML','MAL_RLL','MAL_LUL','MAL_LLL','REC_Right_Visceral_Pleura','REC_Right_Parietal_Pleura','FMA13295'],['REC_Right_Phrenic_nerve']);
unit('airways','Airways','Follow the trachea into the main bronchi and lobar branches.','Air passes from the trachea into the right and left main bronchi and then into branching airways. Trace each route in three dimensions. Airway narrowing can limit airflow without a large visible change to the outside of a lung. Close-up teaching views enlarge small structures and should not be mistaken for their life-size dimensions.','Respiratory',['FMA7394','FMA68418','FMA7396','FMA31744','FMA31741','FMA31747','FMA31746','FMA31748'],['FMA7131']);
unit('heart-coronaries','Heart & coronary anatomy','Connect the heart wall, coronary vessels and neighboring great vessels.','The coronary arteries supply the heart muscle. The left and right coronary arteries arise near the root of the aorta. Coronary veins return blood through routes including the coronary sinus. Distinguish a vessel carrying blood to the myocardium from a cardiac chamber containing blood. Cavity reference regions in this collection represent spaces, not pieces of myocardium.','Cardiovascular',['FMA50039','FMA50040','FMA4706','FMA3736','FMA7242','FMA7243','FMA8612','FMA50873'],['REC_Left_Phrenic_nerve']);
unit('vessels-valves','Great vessels & valves','Compare inflow, outflow and valve relationships.','The ascending aorta continues into the aortic arch and descending thoracic aorta. The pulmonary trunk divides toward the lungs. Pulmonary veins return toward the left atrium. Valve leaflets open and close in response to pressure differences. Their arrangement matters as much as their names. Separate components are enlarged or isolated only for teaching.','Cardiovascular',['FMA3736','FMA3768','FMA8612','FMA50872','FMA50873','FMA7252','FMA7253','FMA7254'],['FMA50040']);
unit('mediastinum','Mediastinal relationships','Find the esophagus, nerves and thoracic duct among the thoracic organs.','The mediastinum contains closely related organs, vessels, nerves and lymphatic structures. Follow the esophagus and compare its position with the trachea. Distinguish the phrenic nerves from the vagus nerves. Trace the thoracic duct through its regional course. Some fine structures in this collection are documented reconstructions; use the source panel to distinguish them.','Nervous',['FMA7131','REC_Right_Phrenic_nerve','REC_Left_Phrenic_nerve','REC_Right_Vagus_nerve','REC_Left_Vagus_nerve','REC_ThoracicDuct','FMA87217','FMA7394'],['FMA50040']);

function disease(id,title,summary,lesson,anatomyIds,protectedIds,pathway,response,sourceIds,list){
 list=reasoningSteps(id,list);
 sourceIds=[...sourceIds,...(id==='asthma'?['mechanisms-asthma']:id==='pneumonia'?['mechanisms-pneumonia']:id==='pneumothorax'||id==='pleural-effusion'?['mechanisms-pleural']:[])];
 const value={id,title,summary,lesson,narration:id,system:'Clinical anatomy',disease:id,isCase:true,anatomyIds,protectedIds,pathway,response,sourceIds,limitations:'Fictional, fixed-indication learning scenario. Responses are illustrative and time-compressed. The model does not establish clinical competence or predict a real patient outcome.',variants:[]};
 for(const key of [...anatomyIds,...protectedIds])if(!names.has(key))throw Error(`Unknown anatomy ${key}`);
 for(let difficulty=0;difficulty<3;difficulty++){
  const identity=o=>`${o.action}-${o.targetId.replaceAll(':','-').toLowerCase()}-${o.toolId||'hand'}`;
  const objectiveIds=list.map((o,i)=>`${id}-0-${difficulty}-${identity(o)}-${list.slice(0,i).filter(previous=>identity(previous)===identity(o)).length}`);
  const objectives=list.map((o,i)=>({...o,id:objectiveIds[i],essential:true,weight:o.action>=4?2:1,maxDistance:o.action===10?.09:[.075,.05,.03][difficulty],maxAngle:o.action===10?180:[55,35,22][difficulty],minContactSeconds:o.action>=4&&o.action!==10?[.6,.9,1.2][difficulty]:0,minPathFraction:o.action===7?.95:0,prerequisites:i?[objectiveIds[i-1]]:[],narration:`${id}-step-${i}`}));
  value.variants.push({id:`${id}-0-${difficulty}`,unitId:id,version,mode:0,difficulty,ordered:true,briefing:`${summary} ${['One coached step and one needed instrument are shown at a time.','Use the findings to plan the sequence; most placement cues are removed.','Work independently, identify unsafe actions and reassess the response.'][difficulty]}`,debrief:`${response} ${value.limitations}`,objectives});
 }
 units.push(value);
}
for (const c of casePathways) disease(c.id,c.title,c.summary,c.lesson,c.anatomyIds,c.protectedIds,c.pathway,c.response,c.sourceIds,c.objectives);

for(const unit of units)for(const variant of unit.variants)variant.protectedIds=unit.protectedIds;
const catalog={schemaVersion:1,version,units,sources};
fs.writeFileSync(path.join(destination,'catalog.json'),JSON.stringify(catalog,null,2)+'\n');
const speechById=new Map();
function say(id,text){if(speechById.has(id)&&speechById.get(id)!==text)throw Error(`Conflicting narration ${id}`);speechById.set(id,text);}
for(const value of units){
 say(value.narration,value.lesson);
 for(const variant of value.variants){
  say(`intro-${variant.id}`,variant.briefing);
  for(const o of variant.objectives)say(o.narration,o.title+'. '+o.instruction);
 }
}
const guide=JSON.parse(fs.readFileSync(path.join(destination,'how-to-play.json'),'utf8'));
for(const page of guide.pages)say(page.id,page.title+'. '+page.text);
const speech=[...speechById].map(([id,text])=>({id,text}));
fs.writeFileSync(path.join(root,'pipeline/learning-narration.json'),JSON.stringify({voice:'Microsoft Zira Desktop',gender:'Female',rate:-1,style:'Calm nurse-style educational guide; synthetic narration',clips:speech},null,2)+'\n');
const seed=units.flatMap(u=>u.variants).map(v=>`INSERT INTO public.thorax_learning_definitions (id, version, definition) VALUES ('${v.id}', '${version}', '${JSON.stringify(v).replaceAll("'","''")}'::jsonb) ON CONFLICT (id,version) DO UPDATE SET definition=excluded.definition;`).join('\n');
fs.mkdirSync(path.join(root,'supabase/migrations'),{recursive:true});
fs.writeFileSync(path.join(root,'supabase/migrations/202610070001_dissection_content.sql'),seed+'\n');
console.log(`Authored ${units.length} units, ${units.flatMap(u=>u.variants).length} variants and ${speech.length} narration clips. All anatomical references resolve.`);
