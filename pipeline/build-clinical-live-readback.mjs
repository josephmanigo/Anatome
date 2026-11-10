import {readFileSync, writeFileSync} from 'node:fs';
const root = new URL('../', import.meta.url);
const catalog = JSON.parse(readFileSync(new URL('unity/ThoraxLab/Assets/ThoraxLab/Resources/Learning/catalog.json', root)));
const variant = catalog.units.find(u => u.id === 'pleural-effusion').variants[0];
const actions = variant.objectives.map((o, i) => ({id:'live-readback-'+i, kind:o.action, targetId:o.targetId, toolId:o.toolId,
  equipmentState:o.equipmentState, contactVerified:true, distance:0, angle:0, contactSeconds:o.minContactSeconds, pathFraction:o.minPathFraction}));
const index = variant.objectives.findIndex(o => o.criticalOnWrong);
const unsafe = actions.slice(); unsafe.splice(index, 0, {...actions[index],id:'unsafe-readback',toolId:'continue'});
const unverified = actions.slice(); unverified.splice(index, 0, {...actions[index],id:'unverified-readback',toolId:'continue',contactVerified:false});
const input = evidence => JSON.stringify({phase:2,assisted:false,passed:true,score:100,actions:evidence});
const query = `-- Read-only current-content and stored-rubric grading checks. No learner records are created.
with rubric as (select definition from public.thorax_learning_definitions where id='${variant.id}' and version='${catalog.version}'),
unsafe as (select public.thorax_evaluate_attempt($fixture$${input(unsafe)}$fixture$::jsonb,definition) as r from rubric),
unverified as (select public.thorax_evaluate_attempt($fixture$${input(unverified)}$fixture$::jsonb,definition) as r from rubric)
select jsonb_build_object(
  'contentVersion','${catalog.version}',
  'versions',(select jsonb_agg(v order by version) from (select version,count(*) as rubrics from public.thorax_learning_definitions group by version) v),
  'clinicalDecisions',(select count(*) from public.thorax_learning_definitions d cross join lateral jsonb_array_elements(d.definition->'objectives') o where d.version='${catalog.version}' and o->>'action'='10'),
  'criticalDecisions',(select count(*) from public.thorax_learning_definitions d cross join lateral jsonb_array_elements(d.definition->'objectives') o where d.version='${catalog.version}' and o->>'criticalOnWrong'='true'),
  'unsafeGrade',(select jsonb_build_object('passed',r->'passed','criticalFailure',r->'criticalFailure','score',r->'score','mistakes',r->'mistakes') from unsafe),
  'unsafeFailsAfterCorrection',(select r->>'passed'='false' and r->>'criticalFailure'='true' and (r->>'score')::numeric=95 from unsafe),
  'unverifiedCannotCreateCriticalFailure',(select r->>'passed'='true' and r->>'criticalFailure'='false' from unverified),
  'rlsTables',(select count(*) from pg_class where relname in ('thorax_learning_definitions','thorax_progress_accounts','thorax_attempts','thorax_checkpoints','thorax_settings_profiles') and relrowsecurity),
  'anonymousSyncDenied',not has_function_privilege('anon','public.thorax_sync_progress(integer,boolean,jsonb,jsonb)','execute'),
  'authenticatedSyncAllowed',has_function_privilege('authenticated','public.thorax_sync_progress(integer,boolean,jsonb,jsonb)','execute'),
  'anonymousEvaluatorDenied',not has_function_privilege('anon','public.thorax_evaluate_attempt(jsonb,jsonb)','execute')
) as verification;
`;
writeFileSync(new URL('qa/clinical-learning/live-readback.sql', root), query);
console.log('Read-only live grading query written.');
