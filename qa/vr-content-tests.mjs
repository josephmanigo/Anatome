import assert from 'node:assert/strict';
import {readFileSync,writeFileSync} from 'node:fs';
const catalog=JSON.parse(readFileSync(new URL('../unity/ThoraxLab/Assets/ThoraxLab/Resources/Learning/catalog.json',import.meta.url)));
const records=new Map(JSON.parse(readFileSync(new URL('../unity/ThoraxLab/Assets/ThoraxLab/anatomy-catalog.json',import.meta.url))).records.map(r=>[r.id,r]));
let checks=0;const check=(value,message)=>{assert.ok(value,message);checks++;};
check(catalog.version==='2026.10.2','Clinical reasoning has a new immutable curriculum revision');
check(catalog.units.filter(u=>u.isCase).length===6,'Six distinct cases');
const sources=new Set(catalog.sources.map(s=>s.id));
for(const unit of catalog.units){
 for(const source of unit.sourceIds)check(sources.has(source),`${unit.id}: source ${source} resolves`);
 for(const variant of unit.variants){
  check(variant.version===catalog.version,variant.id+' version');
  check(new Set(variant.objectives.map(o=>o.id)).size===variant.objectives.length,variant.id+' unique objectives');
  for(const [i,o] of variant.objectives.entries()){
   check(o.targetId.startsWith('equipment:')||records.has(o.targetId),variant.id+' anatomical reference '+o.targetId);
   if(variant.ordered&&i)check(o.prerequisites.includes(variant.objectives[i-1].id),variant.id+' explicit previous step');
   if(o.action>=4)check(Boolean(o.toolId)||o.action===9,variant.id+' action requires actual scene evidence');
   if(o.action===10){check(o.choices.length===3&&o.choices.filter(c=>c.id===o.toolId).length===1,variant.id+' three meaningful choices and one correct rubric');check(o.choices.every(c=>c.label&&c.rationale),variant.id+' every decision has explanatory feedback');}
   if(o.action===7)check(o.minPathFraction>=.95,variant.id+' trace needs recorded full path');
  }
  if(variant.mode===3){
   check(variant.ordered,variant.id+' dissection is ordered');
   check(variant.objectives.filter(o=>o.action===2).every(o=>o.equipmentState===`chunk-lifted:${o.region}`&&o.minPathFraction>0&&o.toolId),variant.id+' bare cut cannot imply exposure');
  }
  if(variant.difficulty===0&&variant.mode===1)check(variant.ordered&&variant.objectives.length<=3,variant.id+' Easy identification stays small');
  if(variant.mode===2&&variant.difficulty===0)check(variant.objectives.every(o=>o.maxDistance===.12&&o.maxAngle===55),variant.id+' Easy assembly capture');
  if(unit.isCase){
   check(variant.ordered&&variant.objectives[0].action===3,variant.id+' starts with actual disease inspection');
   check(variant.objectives.at(-1).action===8,variant.id+' requires reassessment');
   check(variant.objectives.some(o=>o.targetId==='equipment:chart'),variant.id+' requires patient/plan review');
   check(variant.objectives.filter(o=>o.action===10).length>=2,variant.id+' requires mechanism reasoning and response prediction');
   check(variant.objectives.filter(o=>o.action===7).every(o=>o.targetId.startsWith('equipment:')),variant.id+' access route is a named enlarged model, never an external coronary surface');
  }
 }
}
const one=id=>catalog.units.find(u=>u.id===id).variants[0].objectives;
const heart=catalog.units.find(u=>u.id==='heart-coronaries');
const heartHard=heart.variants.find(v=>v.mode===3&&v.difficulty===2);
const pericardium=heartHard.objectives.findIndex(o=>o.targetId==='REC_Pericardium');
check(pericardium===0,'Heart station begins at pericardium, not shared skin sequence');
check(heartHard.objectives[pericardium].action===2,'Pericardium requires a real chunk');
check(heart.anatomyIds.every(id=>heartHard.objectives.some(o=>o.targetId===id)),'Every coronary and heart target is covered');
check(!heartHard.objectives.some(o=>['FMA50039','FMA50040','FMA9457','FMA8612'].includes(o.targetId)&&o.action===2),'Coronary vessels and heart wall remain intact');
for(const id of ['REC_Right_Phrenic_nerve','REC_Left_Phrenic_nerve','FMA50039','FMA50040'])check(heartHard.protectedIds.includes(id),'Heart dissection preserves '+id);
check(heart.sourceIds.includes('heart-dissection')&&heart.sourceIds.includes('coronary-dissection'),'Heart exposure references university dissection sources');
const position=(id,tool,action)=>one(id).findIndex(o=>o.toolId===tool&&o.action===action);
check(position('pneumothorax','needle',5)<position('pneumothorax','guidewire',7),'Pleural air confirmation precedes wire');
check(position('pneumothorax','scalpel',7)<position('pneumothorax','dilator',7),'Skin opening precedes dilator');
check(position('pneumothorax','suture',7)<position('pneumothorax','dressing',6),'Suture fixation precedes dressing');
check(position('asthma','cap',7)<position('asthma','inhaler',4),'Uncap before spacer connection');
check(one('asthma').filter(o=>o.action===6&&o.toolId==='inhaler').length===1,'One represented puff');
check(position('coronary-disease','guidewire',7)<position('coronary-disease','balloon',6),'Wire before balloon');
check(position('coronary-disease','stent',6)<one('coronary-disease').findIndex(o=>o.targetId==='equipment:catheter-withdrawal'),'Retain stent before withdrawal');
check(position('pleural-effusion','ultrasound',5)<position('pleural-effusion','aspirator',7),'Effusion localization before access');
check(position('aortic-stenosis','valve',6)<one('aortic-stenosis').findIndex(o=>o.targetId==='equipment:valve-withdrawal'),'Replacement before withdrawing delivery system');
check(one('pleural-effusion').some(o=>o.action===10&&o.criticalOnWrong),'Aspiration symptom branch has a critical safety decision');
const report={utc:new Date().toISOString(),version:catalog.version,checks,caseSteps:catalog.units.filter(u=>u.isCase).reduce((n,u)=>n+u.variants[0].objectives.length,0),passed:true};
writeFileSync(new URL('./vr-content-report.json',import.meta.url),JSON.stringify(report,null,2)+'\n');console.log(JSON.stringify(report));
