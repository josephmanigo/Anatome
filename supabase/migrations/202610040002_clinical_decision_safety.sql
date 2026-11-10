-- Recompute critical clinical decision errors using the stored rubric; preserve historical content and RLS.
create or replace function public.thorax_evaluate_attempt(input jsonb, rubric jsonb)
returns jsonb language plpgsql set search_path=public,pg_temp as $$
declare event jsonb; objective jsonb; next_objective jsonb; candidate jsonb; done jsonb='{}'; seen jsonb='{}';
  mistakes integer=0; critical boolean=false; weight_total numeric=0; weight_done numeric=0;
  score numeric; essentials boolean=true; matched boolean; completed jsonb='[]'; event_id text;
begin
 if jsonb_typeof(input->'actions') is distinct from 'array' or jsonb_array_length(input->'actions')>4096 then raise exception 'Invalid event evidence'; end if;
 if jsonb_array_length(rubric->'objectives')=0 then raise exception 'Empty rubric'; end if;
 for event in select value from jsonb_array_elements(input->'actions') loop
  event_id=event->>'id';
  if event_id is null or event_id='' or seen ? event_id then continue; end if;
  seen=seen||jsonb_build_object(event_id,true);
  if exists(select 1 from jsonb_each_text(event) e where e.key in ('distance','angle','contactSeconds','pathFraction') and e.value in ('NaN','Infinity','-Infinity')) then continue; end if;
  if coalesce((event->>'distance')::numeric,-1)<0 or coalesce((event->>'angle')::numeric,-1)<0 or coalesce((event->>'contactSeconds')::numeric,-1)<0 or coalesce((event->>'pathFraction')::numeric,-1) not between 0 and 1 then continue; end if;
  if (event->>'kind')::integer=9 then
   if coalesce((event->>'contactVerified')::boolean,false) and (jsonb_array_length(coalesce(rubric->'protectedIds','[]'))=0 or (rubric->'protectedIds') ? (event->>'targetId')) then critical=true; mistakes=mistakes+1; end if;
   continue;
  end if;
  candidate=null;
  select value into next_objective from jsonb_array_elements(rubric->'objectives') where not done ? (value->>'id') limit 1;
  for objective in select value from jsonb_array_elements(rubric->'objectives') loop
   if done ? (objective->>'id') then continue; end if;
   if (objective->>'action')::integer=(event->>'kind')::integer and objective->>'targetId'=event->>'targetId'
     and (not coalesce((rubric->>'ordered')::boolean,false) or objective->>'id'=next_objective->>'id')
     and not exists(select 1 from jsonb_array_elements_text(coalesce(objective->'prerequisites','[]')) p where not done ? p.value)
   then candidate=objective; exit; end if;
  end loop;
  if candidate is null then
   if not exists(select 1 from jsonb_array_elements(rubric->'objectives') o where done ? (o.value->>'id') and o.value->>'targetId'=event->>'targetId' and o.value->>'action'=event->>'kind') then mistakes=mistakes+1; end if;
   continue;
  end if;
  matched=coalesce((event->>'contactVerified')::boolean,false)
   and (coalesce(candidate->>'toolId','')='' or candidate->>'toolId'=event->>'toolId')
   and (coalesce(candidate->>'equipmentState','')='' or candidate->>'equipmentState'=event->>'equipmentState')
   and (event->>'distance')::numeric <= (candidate->>'maxDistance')::numeric
   and (event->>'angle')::numeric <= (candidate->>'maxAngle')::numeric
   and (event->>'contactSeconds')::numeric >= coalesce((candidate->>'minContactSeconds')::numeric,0)
   and (event->>'pathFraction')::numeric >= coalesce((candidate->>'minPathFraction')::numeric,0);
  if not matched and (candidate->>'action')::integer=10 and coalesce((candidate->>'criticalOnWrong')::boolean,false)
    and coalesce((event->>'contactVerified')::boolean,false) and (event->>'distance')::numeric <= (candidate->>'maxDistance')::numeric
    and event->>'equipmentState'=candidate->>'equipmentState' and event->>'toolId'<>candidate->>'toolId'
    and exists(select 1 from jsonb_array_elements(coalesce(candidate->'choices','[]')) c where c.value->>'id'=event->>'toolId')
  then critical=true; end if;
  if matched then done=done||jsonb_build_object(candidate->>'id',event_id); else mistakes=mistakes+1; end if;
 end loop;
 for objective in select value from jsonb_array_elements(rubric->'objectives') loop
  weight_total=weight_total+greatest(.01,(objective->>'weight')::numeric);
  if done ? (objective->>'id') then weight_done=weight_done+greatest(.01,(objective->>'weight')::numeric);
  elsif coalesce((objective->>'essential')::boolean,true) then essentials=false; end if;
  completed=completed||jsonb_build_array(jsonb_build_object('objectiveId',objective->>'id','actionId',done->> (objective->>'id'),'completed',done ? (objective->>'id')));
 end loop;
 score=greatest(0,100*weight_done/weight_total-least(50,mistakes*5));
 return input||jsonb_build_object('score',score,'passed',score>=80 and essentials and not critical and not coalesce((input->>'assisted')::boolean,false),'criticalFailure',critical,'mistakes',mistakes,'objectives',completed,'synced',true);
end $$;
revoke all on function public.thorax_evaluate_attempt(jsonb,jsonb) from public;


