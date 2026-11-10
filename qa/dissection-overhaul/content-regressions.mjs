import fs from 'node:fs';
const catalog=JSON.parse(fs.readFileSync('unity/ThoraxLab/Assets/ThoraxLab/Resources/Learning/catalog.json','utf8'));
const checks=[];
const check=(id,passed,detail)=>checks.push({id,passed,detail});
const units=catalog.units.filter(u=>!u.isCase);
const records=new Map(JSON.parse(fs.readFileSync('unity/ThoraxLab/Assets/ThoraxLab/anatomy-catalog.json','utf8')).records.map(r=>[r.id,r]));
for(const unit of units){
 const variants=unit.variants.filter(v=>v.mode===3);
 const targets=variants.map(v=>v.objectives.map(o=>o.targetId));
 check(unit.id+'/full-topic-at-every-level',targets.every(ids=>unit.anatomyIds.every(id=>ids.includes(id))),targets);
 check(unit.id+'/same-coverage-at-every-level',targets.every(ids=>JSON.stringify(ids)===JSON.stringify(targets[0])),targets.map(ids=>ids.length));
 for(const v of variants){
  const cuts=v.objectives.filter(o=>o.action===2);
  check(v.id+'/actual-chunk-evidence',cuts.length>0&&cuts.every(o=>o.minPathFraction>0&&o.equipmentState===`chunk-lifted:${o.region}`),'Bare incision or whole-object movement must not complete a cut.');
  check(v.id+'/correct-tool',cuts.every(o=>o.toolId===(records.get(o.targetId).tissue==='bone'?'dissection-saw':'dissection-scalpel')),'Every bone objective requires the saw.');
  check(v.id+'/preserve-protected-anatomy',cuts.every(o=>!v.protectedIds.includes(o.targetId)),'Preserved nerves and coronary vessels must not be removal targets.');
  if(unit.id==='chest-wall'){
   check(v.id+'/both-fourth-ribs',['FMA7957','FMA8148'].every(id=>cuts.some(o=>o.targetId===id)),'Right and left fourth ribs are required.');
   check(v.id+'/bilateral-layers',['FMA7163','REC_fat','REC_fascia','FMA9756','FMA9757','FMA9758'].every(id=>['left','right'].every(region=>cuts.some(o=>o.targetId===id&&o.region===region&&o.essential))),'One side alone cannot finish the chest wall.');
  }
 }
 check(unit.id+'/stricter-removal',variants[0].objectives.every((o,i)=>o.action!==2||o.minPathFraction<variants[1].objectives[i].minPathFraction&&variants[1].objectives[i].minPathFraction<variants[2].objectives[i].minPathFraction),'Harder levels require more measured removal from the same anatomy.');
}
check('topics-have-distinct-dissection',new Set(units.map(u=>JSON.stringify(u.variants.find(v=>v.mode===3&&v.difficulty===0).objectives.map(o=>o.targetId)))).size===units.length,'Each topic must teach its own anatomy from Easy onward.');
const report={utc:new Date().toISOString(),passed:checks.every(c=>c.passed),checks};
fs.writeFileSync('qa/dissection-overhaul/content-regressions.json',JSON.stringify(report,null,2)+'\n');
console.log(JSON.stringify({passed:report.passed,checks:checks.length,failed:checks.filter(c=>!c.passed).map(c=>c.id)},null,2));
if(!report.passed)process.exitCode=1;
