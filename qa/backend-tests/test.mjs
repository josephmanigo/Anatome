import {PGlite} from '@electric-sql/pglite';
import {readFileSync,writeFileSync} from 'node:fs';
import assert from 'node:assert/strict';
import {randomUUID} from 'node:crypto';
const db=new PGlite();let checks=0;
const check=(value,message)=>{assert.ok(value,message);checks++;};
await db.exec(`create role anon; create role authenticated; create schema auth; create table auth.users(id uuid primary key); create function auth.uid() returns uuid language sql stable as $$select nullif(current_setting('request.jwt.claim.sub',true),'')::uuid$$; grant usage on schema auth,public to authenticated; grant execute on function auth.uid() to authenticated;`);
await db.exec('alter default privileges in schema public grant all on tables to anon, authenticated; alter default privileges in schema public grant all on functions to anon, authenticated;');
for(const file of ['202609060001_learning.sql','202609060002_learning_content.sql','202609060003_settings.sql','202609060004_explicit_client_grants.sql','202609060005_vr_learning_content.sql','202609060006_content_version_mastery.sql','202609060007_heart_dissection_content.sql','202610040001_clinical_reasoning_content.sql','202610040002_clinical_decision_safety.sql','202610070001_dissection_content.sql'])await db.exec(readFileSync(new URL('../../supabase/migrations/'+file,import.meta.url),'utf8'));
for(const table of ['thorax_learning_definitions','thorax_progress_accounts','thorax_attempts','thorax_checkpoints','thorax_settings_profiles']){
 const acl=(await db.query(`select has_table_privilege('authenticated',$1,'truncate') as truncate, has_table_privilege('authenticated',$1,'insert') as insert, has_table_privilege('anon',$1,'select') as anonymous`,[table])).rows[0];
 check(!acl.truncate&&!acl.insert&&!acl.anonymous,table+' explicit client grants');
}
for(const signature of ['thorax_sync_progress(integer,boolean,jsonb,jsonb)','thorax_reset_progress()','thorax_sync_settings(jsonb,text)'])check(!(await db.query("select has_function_privilege('anon',$1,'execute') as allowed",[signature])).rows[0].allowed,signature+' denies anon');
const catalog=JSON.parse(readFileSync(new URL('../../unity/ThoraxLab/Assets/ThoraxLab/Resources/Learning/catalog.json',import.meta.url)));
const userA=randomUUID(),userB=randomUUID();
await db.query('insert into auth.users values ($1),($2)',[userA,userB]);
const login=async id=>{await db.exec('reset role');await db.query("select set_config('request.jwt.claim.sub',$1,false)",[id]);await db.exec('set role authenticated');};
const sync=async(attempts=[],generation=0,checkpoints=[]) => (await db.query('select public.thorax_sync_progress($1,true,$2::jsonb,$3::jsonb) as result',[generation,JSON.stringify(attempts),JSON.stringify(checkpoints)])).rows[0].result;
function attempt(v,userId=userA){return {id:randomUUID(),schemaVersion:1,userId,resetGeneration:0,status:1,phase:2,mode:v.mode,difficulty:v.difficulty,unitId:v.unitId,scenarioId:v.id,contentVersion:v.version,startedUtc:new Date().toISOString(),completedUtc:new Date().toISOString(),passed:true,score:100,assisted:false,actions:v.objectives.map((o,i)=>({id:randomUUID(),sequence:i,kind:o.action,targetId:o.targetId,toolId:o.toolId,equipmentState:o.equipmentState,contactVerified:true,distance:0,angle:0,contactSeconds:o.minContactSeconds,pathFraction:o.minPathFraction}))};}
// A real historical rubric can still synchronize, but cannot grant mastery in the replacement curriculum.
const migratingUser=randomUUID();await db.query('insert into auth.users values ($1)',[migratingUser]);
const newCases=catalog.units.find(u=>u.id==='pneumothorax').variants;
const oldEasy=(await db.query("select definition from thorax_learning_definitions where id='pneumothorax-0-0' and version='2026.09.1'")).rows[0].definition;
const oldMedium=(await db.query("select definition from thorax_learning_definitions where id='pneumothorax-0-1' and version='2026.09.1'")).rows[0].definition;
await login(migratingUser);await sync([attempt(oldEasy,migratingUser),attempt(oldMedium,migratingUser)]);
let versionRejected=false;try{await sync([attempt(newCases[1],migratingUser)]);}catch{versionRejected=true;}check(versionRejected,'v1 Easy cannot unlock current Medium');
const previousEasy=(await db.query("select definition from thorax_learning_definitions where id='pneumothorax-0-0' and version='2026.09.2'")).rows[0].definition;
await sync([attempt(previousEasy,migratingUser)]);
versionRejected=false;try{await sync([attempt(newCases[1],migratingUser)]);}catch{versionRejected=true;}check(versionRejected,'published v2 Easy cannot unlock revised v3 Medium');
await sync([attempt(newCases[0],migratingUser)]);
versionRejected=false;try{await sync([attempt(newCases[2],migratingUser)]);}catch{versionRejected=true;}check(versionRejected,'v1 Medium cannot complete current Hard prerequisite');
const hardCheckpoint=attempt(newCases[2],migratingUser);hardCheckpoint.status=0;hardCheckpoint.actions=[];
versionRejected=false;try{await sync([],0,[{attemptId:hardCheckpoint.id,resetGeneration:0,savedUtc:new Date().toISOString(),attempt:hardCheckpoint}]);}catch{versionRejected=true;}check(versionRejected,'checkpoint cannot bypass current-version prerequisites');
const migrated=await sync([attempt(newCases[1],migratingUser),attempt(newCases[2],migratingUser)]);
check(migrated.attempts.filter(a=>a.contentVersion==='2026.09.1').length===2,'historical results remain available');
check(migrated.attempts.filter(a=>a.contentVersion==='2026.09.2').length===1,'published v2 results remain available after v3 migration');
check(migrated.attempts.filter(a=>a.contentVersion===catalog.version&&a.passed).length===3,'current Easy Medium Hard mastery sequence passes');
await login(userA);
for(const variant of catalog.units.flatMap(u=>u.variants)){
 const record=attempt(variant);const result=await sync([record]);const evaluated=result.attempts.find(a=>a.id===record.id);check(evaluated.passed&&evaluated.score===100,variant.id+' passes');
 await sync([record]);check((await db.query('select count(*)::int as n from thorax_attempts where id=$1',[record.id])).rows[0].n===1,'idempotence');
 const incomplete=attempt(variant);incomplete.actions.pop();const failure=(await sync([incomplete])).attempts.find(a=>a.id===incomplete.id);check(!failure.passed,'essential objective cannot be forged');
}
for(const v of catalog.units.find(u=>u.id==='chest-wall').variants.filter(v=>v.mode===3)){
 const leftOnly=attempt(v);for(const action of leftOnly.actions)if(action.equipmentState==='chunk-lifted:right')action.equipmentState='chunk-lifted:left';
 check(!(await sync([leftOnly])).attempts.find(a=>a.id===leftOnly.id).passed,'left-side cuts cannot complete bilateral chest wall '+v.id);
 const tiny=attempt(v);for(const action of tiny.actions)if(action.kind===2)action.pathFraction=.00000001;
 check(!(await sync([tiny])).attempts.find(a=>a.id===tiny.id).passed,'tiny chips cannot complete regional removal '+v.id);
 const noSaw=attempt(v);for(const action of noSaw.actions)if(action.toolId==='dissection-saw')action.toolId='dissection-scalpel';
 check(!(await sync([noSaw])).attempts.find(a=>a.id===noSaw.id).passed,'bone step requires saw '+v.id);
}
const variant=catalog.units.find(u=>u.isCase).variants[0];
const reversed=attempt(variant);reversed.actions.reverse();check(!(await sync([reversed])).attempts.find(a=>a.id===reversed.id).passed,'ordered actions');
const critical=attempt(variant);critical.actions.push({id:randomUUID(),kind:9,targetId:variant.protectedIds[0],contactVerified:true,distance:0,angle:0,contactSeconds:0,pathFraction:0});check(!(await sync([critical])).attempts.find(a=>a.id===critical.id).passed,'preservation error');
const safetyVariant=catalog.units.find(u=>u.id==='pleural-effusion').variants[0];
const safetyIndex=safetyVariant.objectives.findIndex(o=>o.criticalOnWrong);
const unsafeDecision=attempt(safetyVariant);unsafeDecision.actions.splice(safetyIndex,0,{...unsafeDecision.actions[safetyIndex],id:randomUUID(),toolId:'continue'});
const unsafeResult=(await sync([unsafeDecision])).attempts.find(a=>a.id===unsafeDecision.id);
check(!unsafeResult.passed&&unsafeResult.criticalFailure&&unsafeResult.score===95,'server retains unsafe decision despite a later correct choice and forged passed=true');
const unverifiedDecision=attempt(safetyVariant);unverifiedDecision.actions.splice(safetyIndex,0,{...unverifiedDecision.actions[safetyIndex],id:randomUUID(),toolId:'continue',contactVerified:false});
const unverifiedResult=(await sync([unverifiedDecision])).attempts.find(a=>a.id===unverifiedDecision.id);
check(!unverifiedResult.criticalFailure&&unverifiedResult.passed,'unverified decision cannot create critical safety evidence');
const reasoningRetry=attempt(variant);const reasoningIndex=variant.objectives.findIndex(o=>o.action===10);reasoningRetry.actions.splice(reasoningIndex,0,{...reasoningRetry.actions[reasoningIndex],id:randomUUID(),toolId:variant.objectives[reasoningIndex].choices.find(c=>c.id!==variant.objectives[reasoningIndex].toolId).id});
const reasoningResult=(await sync([reasoningRetry])).attempts.find(a=>a.id===reasoningRetry.id);
check(reasoningResult.passed&&!reasoningResult.criticalFailure&&reasoningResult.score===95,'ordinary reasoning feedback/retry agrees with client scoring');
let rejected=false;try{await sync([attempt(variant,userB)]);}catch{rejected=true;}check(rejected,'foreign attempt rejected');
for(const key of ['userId','unitId','status','phase','difficulty','resetGeneration']){const invalid=attempt(variant);delete invalid[key];rejected=false;try{await sync([invalid]);}catch{rejected=true;}check(rejected,'missing '+key+' rejected');}
const nan=attempt(variant);nan.actions[0].contactSeconds='NaN';check(!(await sync([nan])).attempts.find(a=>a.id===nan.id).passed,'non-finite evidence rejected');
const resumed=attempt(variant);resumed.status=0;resumed.actions=resumed.actions.slice(0,2);const checkpoint={attemptId:resumed.id,scenarioId:variant.id,phase:2,resetGeneration:0,savedUtc:new Date().toISOString(),attempt:resumed};
let snapshot=(await sync([],0,[checkpoint])).checkpoints.find(c=>c.attemptId===resumed.id);check(snapshot.attempt.actions.length===2&&!snapshot.attempt.passed,'safe checkpoint preserves prior evidence');
const profile={id:randomUUID(),schemaVersion:1,platform:'Windows',name:'Study'};await db.query('select thorax_sync_settings($1::jsonb,null)',[JSON.stringify(profile)]);
check((await db.query('select thorax_sync_settings(null,null) as r')).rows[0].r.profiles.length===1,'profile stored');
await login(userB);check((await sync()).attempts.length===0,'RPC isolation');check((await db.query('select * from thorax_attempts')).rows.length===0,'table RLS isolation');
check((await db.query('select thorax_sync_settings(null,null) as r')).rows[0].r.profiles.length===0,'profile account isolation');
rejected=false;try{await sync([attempt(catalog.units.find(u=>u.isCase).variants[1],userB)]);}catch{rejected=true;}check(rejected,'server rejects difficulty bypass');
rejected=false;try{await db.query('insert into thorax_attempts(user_id,id,generation,record) values ($1,$2,0,$3)',[userB,randomUUID(),'{}']);}catch{rejected=true;}check(rejected,'no direct score writes');
await login(userA);const reset=(await db.query('select thorax_reset_progress() as r')).rows[0].r;check(reset.generation===1,'reset increments');
const stale=await sync([attempt(variant)],0);check(stale.generation===1&&stale.attempts.length===0&&!stale.orientationCompleted,'stale device cannot resurrect progress');
await db.exec('reset role');await db.query("select set_config('request.jwt.claim.sub','',false)");await db.exec('set role authenticated');rejected=false;try{await sync();}catch{rejected=true;}check(rejected,'missing identity rejected');
writeFileSync(new URL('../learning-backend-report.json',import.meta.url),JSON.stringify({checks,variants:72,result:'pass',engine:'PGlite PostgreSQL',liveSupabase:false,utc:new Date().toISOString()},null,2));
console.log('PASS '+checks+' PostgreSQL checks: all 72 rubrics, idempotence, essential objectives, order, RLS and stale reset generation.');await db.close();
