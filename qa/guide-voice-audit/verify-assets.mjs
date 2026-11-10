import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import {createHash} from 'node:crypto';
const root=path.resolve(import.meta.dirname,'../..');
const read=p=>JSON.parse(fs.readFileSync(path.join(root,p),'utf8').replace(/^\uFEFF/,''));
const checks=[];const check=(id,value)=>{checks.push({id,passed:!!value});};
const records=read('exports/anatomy-catalog.json').records;
const unity=read('unity/ThoraxLab/Assets/ThoraxLab/anatomy-catalog.json').records;
const selected=read('source/thorax-selection.json').records;
const groups=Object.fromEntries([...new Set(records.map(r=>r.required_group))].sort().map(g=>[g,records.filter(r=>r.required_group===g).length]));
check('unique_catalog_ids',new Set(records.map(r=>r.id)).size===249);
for(const r of selected)check('selected_source_present/'+r.id,records.some(u=>u.id===r.id));
for(const r of records){
 const u=unity.find(x=>x.id===r.id);
 check('shipping_record/'+r.id,u&&['id','name','source_kind','required_group','cutting_ready','reference_region'].every(k=>u[k]===r[k]));
 check('provenance_record/'+r.id,r.source_record&&fs.existsSync(path.join(root,r.source_record)));
 check('source_identity/'+r.id,r.source_kind==='authored_reconstruction'||r.mesh_ids?.length>0);
 if(r.reference_region)check('reference_not_cuttable/'+r.id,!r.cutting_ready);
}
for(const [group,count] of Object.entries({ribs:24,costal_cartilages:24,sternum:3,thoracic_vertebrae:12,thoracic_discs:11,lung_lobes:5,main_bronchi:2,pleural_layers:4,cardiac_chambers:4,cardiac_septa:2,aortic_valve:3,pulmonary_valve:3,mitral_valve:2,tricuspid_valve:3,phrenic_nerves:2,vagus_nerves:2,sympathetic_trunks:2,intercostal_neurovascular_bundles:66,thoracic_skin:1,thoracic_fat:1,thoracic_fascia:1,diaphragm:1,thoracic_duct:1,pericardium:1,esophagus:1}))check('required_group/'+group,groups[group]===count);
const manifest=read('pipeline/learning-narration.json');const guide=read('unity/ThoraxLab/Assets/ThoraxLab/Resources/Learning/how-to-play.json');
check('female_voice',manifest.voice==='Microsoft Zira Desktop'&&manifest.gender==='Female');
check('ten_guide_steps',guide.pages.length===10);
const clips=[];const voiceRoot=path.join(root,'unity/ThoraxLab/Assets/ThoraxLab/Resources/Learning/Voice');
for(const clip of manifest.clips){
 const file=path.join(voiceRoot,clip.id+'.wav');const b=fs.readFileSync(file);
 check('wav_signature/'+clip.id,b.toString('ascii',0,4)==='RIFF'&&b.toString('ascii',8,12)==='WAVE');
 let format,data;
 for(let offset=12;offset+8<=b.length;){const type=b.toString('ascii',offset,offset+4),n=b.readUInt32LE(offset+4);if(type==='fmt ')format=b.subarray(offset+8,offset+8+n);if(type==='data')data=b.subarray(offset+8,offset+8+n);offset+=8+n+(n%2);}
 const seconds=data?.length/format?.readUInt32LE(8);
 check('nonempty_audio/'+clip.id,seconds>.3&&format.readUInt16LE(0)===1&&format.readUInt16LE(14)===16&&data.some(x=>x!==0));
 const hash=createHash('sha256').update(manifest.voice+'\n'+manifest.rate+'\n'+clip.text).digest('hex');
 check('female_cache_identity/'+clip.id,fs.readFileSync(file+'.sha256','utf8').trim()===hash);
 clips.push({id:clip.id,seconds,bytes:b.length,sha256:createHash('sha256').update(b).digest('hex')});
}
for(const p of guide.pages)check('guide_voice_matches_text/'+p.id,manifest.clips.some(c=>c.id===p.id&&c.text===p.title+'. '+p.text));
check('no_unmanifested_narration',fs.readdirSync(voiceRoot).filter(p=>p.endsWith('.wav')).length===manifest.clips.length);
const report={utc:new Date().toISOString(),passed:checks.every(c=>c.passed),checks,anatomy:{total:records.length,tissues:records.filter(r=>!r.reference_region).length,references:records.filter(r=>r.reference_region).length,selectedSources:selected.length,groups,scope:'Gross thorax; source-backed and documented reconstructions. Septa are region annotations, not separate segmented tissue. No whole-body or microscopic-completeness claim.'},voice:{name:manifest.voice,gender:manifest.gender,rate:manifest.rate,clips},guidePages:guide.pages.length};
fs.writeFileSync(path.join(import.meta.dirname,'assets-report.json'),JSON.stringify(report,null,2)+'\n');
console.log(JSON.stringify({passed:report.passed,checks:checks.length,failures:checks.filter(c=>!c.passed),tissues:report.anatomy.tissues,references:report.anatomy.references,clips:clips.length,seconds:clips.reduce((n,c)=>n+c.seconds,0)}));
assert.ok(report.passed,'Asset audit failed; see assets-report.json');
