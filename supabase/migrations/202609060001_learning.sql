-- Thorax Lab owns only thorax_* objects. This migration preserves existing applications.
create table if not exists public.thorax_learning_definitions (
  id text not null, version text not null, definition jsonb not null,
  primary key (id, version)
);
create table if not exists public.thorax_progress_accounts (
  user_id uuid primary key references auth.users(id) on delete cascade,
  generation integer not null default 0 check (generation >= 0),
  orientation_completed boolean not null default false,
  updated_at timestamptz not null default now()
);
create table if not exists public.thorax_attempts (
  user_id uuid not null references auth.users(id) on delete cascade,
  id text not null, generation integer not null, record jsonb not null,
  created_at timestamptz not null default now(), primary key (user_id,id)
);
create table if not exists public.thorax_checkpoints (
  user_id uuid not null references auth.users(id) on delete cascade,
  attempt_id text not null, generation integer not null, record jsonb not null,
  primary key (user_id,attempt_id)
);
create table if not exists public.thorax_settings_profiles (
  user_id uuid not null references auth.users(id) on delete cascade,
  id text not null, platform text not null check (platform in ('Windows','Quest')),
  profile jsonb not null, updated_at timestamptz not null default now(), primary key(user_id,id)
);
alter table public.thorax_learning_definitions enable row level security;
alter table public.thorax_progress_accounts enable row level security;
alter table public.thorax_attempts enable row level security;
alter table public.thorax_checkpoints enable row level security;
alter table public.thorax_settings_profiles enable row level security;
create policy thorax_definitions_read on public.thorax_learning_definitions for select to authenticated using (true);
create policy thorax_accounts_read on public.thorax_progress_accounts for select to authenticated using ((select auth.uid())=user_id);
create policy thorax_attempts_read on public.thorax_attempts for select to authenticated using ((select auth.uid())=user_id);
create policy thorax_checkpoints_read on public.thorax_checkpoints for select to authenticated using ((select auth.uid())=user_id);
create policy thorax_profiles_owner on public.thorax_settings_profiles to authenticated using ((select auth.uid())=user_id) with check ((select auth.uid())=user_id);
grant select on public.thorax_learning_definitions, public.thorax_progress_accounts, public.thorax_attempts, public.thorax_checkpoints to authenticated;
grant select,insert,update,delete on public.thorax_settings_profiles to authenticated;
revoke all on public.thorax_progress_accounts,public.thorax_attempts,public.thorax_checkpoints from anon;

-- Recompute formative scores from bounded interaction evidence and the stored rubric.
-- This does not claim physical anti-cheat: Unity supplies the measured interaction evidence.
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

create or replace function public.thorax_sync_progress(expected_generation integer, orientation_completed boolean, incoming_attempts jsonb default '[]', incoming_checkpoints jsonb default '[]')
returns jsonb language plpgsql security definer set search_path=public,pg_temp as $$
declare uid uuid=auth.uid(); account public.thorax_progress_accounts; item jsonb; definition jsonb; checked jsonb;
begin
 if uid is null then raise exception 'Authentication required' using errcode='28000'; end if;
 if expected_generation is null or orientation_completed is null or jsonb_typeof(incoming_attempts) is distinct from 'array' or jsonb_array_length(incoming_attempts)>200 or jsonb_typeof(incoming_checkpoints) is distinct from 'array' or jsonb_array_length(incoming_checkpoints)>200 then raise exception 'Invalid sync batch'; end if;
 insert into public.thorax_progress_accounts(user_id) values(uid) on conflict do nothing;
 select * into account from public.thorax_progress_accounts where user_id=uid for update;
 if expected_generation>account.generation then raise exception 'Invalid reset generation'; end if;
 if expected_generation=account.generation then
  update public.thorax_progress_accounts set orientation_completed=thorax_progress_accounts.orientation_completed or thorax_sync_progress.orientation_completed,updated_at=now() where user_id=uid;
  for item in select value from jsonb_array_elements(incoming_attempts) loop
   if item->>'userId' is distinct from uid::text or (item->>'resetGeneration')::integer is distinct from account.generation or coalesce(item->>'id','')='' or length(item->>'id')>64 or (item->>'status')::integer is distinct from 1 or coalesce((item->>'phase')::integer,-1) not between 0 and 2 then raise exception 'Invalid attempt identity'; end if;
   if exists(select 1 from public.thorax_attempts where user_id=uid and id=item->>'id') then continue; end if;
   select d.definition into definition from public.thorax_learning_definitions d where d.id=item->>'scenarioId' and d.version=item->>'contentVersion';
   if definition is null or definition->>'unitId' is distinct from item->>'unitId' or definition->>'mode' is distinct from item->>'mode' or definition->>'difficulty' is distinct from item->>'difficulty' then raise exception 'Unknown content contract'; end if;
   if (definition->>'difficulty')::integer>0 and not exists(select 1 from public.thorax_attempts a where a.user_id=uid and a.generation=account.generation and a.record->>'unitId'=item->>'unitId' and a.record->>'mode'=item->>'mode' and (a.record->>'difficulty')::integer=(definition->>'difficulty')::integer-1 and a.record->>'phase'='2' and a.record->>'passed'='true') then raise exception 'Previous difficulty mastery required'; end if;
   checked=public.thorax_evaluate_attempt(item,definition);
   insert into public.thorax_attempts(user_id,id,generation,record) values(uid,item->>'id',account.generation,checked) on conflict do nothing;
  end loop;
  for item in select value from jsonb_array_elements(incoming_checkpoints) loop
   if (item->>'resetGeneration')::integer is distinct from account.generation or (item->'attempt'->>'resetGeneration')::integer is distinct from account.generation or coalesce(item->>'attemptId','')='' or length(item->>'attemptId')>64 or (item->'attempt'->>'userId') is distinct from uid::text or (item->'attempt'->>'id') is distinct from item->>'attemptId' or (item->'attempt'->>'status')::integer is distinct from 0 then raise exception 'Invalid checkpoint'; end if;
   if exists(select 1 from public.thorax_attempts where user_id=uid and id=item->>'attemptId') then continue; end if;
   select d.definition into definition from public.thorax_learning_definitions d where d.id=item->'attempt'->>'scenarioId' and d.version=item->'attempt'->>'contentVersion';
   if definition is null then raise exception 'Unknown checkpoint content'; end if;
   item=jsonb_set(item,'{attempt}',public.thorax_evaluate_attempt(item->'attempt',definition));
   insert into public.thorax_checkpoints(user_id,attempt_id,generation,record) values(uid,item->>'attemptId',account.generation,item)
   on conflict(user_id,attempt_id) do update set record=excluded.record where excluded.record->>'savedUtc'>thorax_checkpoints.record->>'savedUtc';
  end loop;
  delete from public.thorax_checkpoints c where c.user_id=uid and exists(select 1 from public.thorax_attempts a where a.user_id=uid and a.id=c.attempt_id);
 end if;
 return jsonb_build_object('generation',account.generation,'orientationCompleted',(select a.orientation_completed from public.thorax_progress_accounts a where a.user_id=uid),
 'attempts',coalesce((select jsonb_agg(record) from public.thorax_attempts where user_id=uid and generation=account.generation),'[]'),
 'checkpoints',coalesce((select jsonb_agg(record) from public.thorax_checkpoints where user_id=uid and generation=account.generation),'[]'),'syncedUtc',now());
end $$;
revoke all on function public.thorax_sync_progress(integer,boolean,jsonb,jsonb) from public;
grant execute on function public.thorax_sync_progress(integer,boolean,jsonb,jsonb) to authenticated;

create or replace function public.thorax_reset_progress()
returns jsonb language plpgsql security definer set search_path=public,pg_temp as $$
declare uid uuid=auth.uid(); next_generation integer;
begin
 if uid is null then raise exception 'Authentication required' using errcode='28000'; end if;
 insert into public.thorax_progress_accounts(user_id) values(uid) on conflict do nothing;
 update public.thorax_progress_accounts set generation=generation+1,orientation_completed=false,updated_at=now() where user_id=uid returning generation into next_generation;
 delete from public.thorax_attempts where user_id=uid;
 delete from public.thorax_checkpoints where user_id=uid;
 return jsonb_build_object('generation',next_generation);
end $$;
revoke all on function public.thorax_reset_progress() from public;
grant execute on function public.thorax_reset_progress() to authenticated;
