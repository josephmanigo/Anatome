-- Preserve historical attempts; mastery and resume contracts belong to their authored curriculum version.
-- This migration creates no tables and retains every existing account-isolation policy.
-- Reassert RLS explicitly so applying it also protects a pre-existing misconfigured deployment.
alter table public.thorax_learning_definitions enable row level security;
alter table public.thorax_progress_accounts enable row level security;
alter table public.thorax_attempts enable row level security;
alter table public.thorax_checkpoints enable row level security;
alter table public.thorax_settings_profiles enable row level security;
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
   if exists(select 1 from generate_series(0,(definition->>'difficulty')::integer-1) as required(level) where not exists(
    select 1 from public.thorax_attempts a where a.user_id=uid and a.generation=account.generation
     and a.record->>'unitId'=definition->>'unitId' and a.record->>'mode'=definition->>'mode'
     and a.record->>'contentVersion'=definition->>'version' and (a.record->>'difficulty')::integer=required.level
     and a.record->>'phase'='2' and a.record->>'status'='1' and a.record->>'passed'='true'
     and not coalesce((a.record->>'assisted')::boolean,false) and not coalesce((a.record->>'criticalFailure')::boolean,false)
     and (a.record->>'score')::numeric>=80)) then raise exception 'Current curriculum Easy and prior difficulty mastery required'; end if;
   checked=public.thorax_evaluate_attempt(item,definition);
   insert into public.thorax_attempts(user_id,id,generation,record) values(uid,item->>'id',account.generation,checked) on conflict do nothing;
  end loop;
  for item in select value from jsonb_array_elements(incoming_checkpoints) loop
   if (item->>'resetGeneration')::integer is distinct from account.generation or (item->'attempt'->>'resetGeneration')::integer is distinct from account.generation or coalesce(item->>'attemptId','')='' or length(item->>'attemptId')>64 or (item->'attempt'->>'userId') is distinct from uid::text or (item->'attempt'->>'id') is distinct from item->>'attemptId' or (item->'attempt'->>'status')::integer is distinct from 0 then raise exception 'Invalid checkpoint'; end if;
   if exists(select 1 from public.thorax_attempts where user_id=uid and id=item->>'attemptId') then continue; end if;
   select d.definition into definition from public.thorax_learning_definitions d where d.id=item->'attempt'->>'scenarioId' and d.version=item->'attempt'->>'contentVersion';
   if definition is null or definition->>'unitId' is distinct from item->'attempt'->>'unitId' or definition->>'mode' is distinct from item->'attempt'->>'mode' or definition->>'difficulty' is distinct from item->'attempt'->>'difficulty' then raise exception 'Unknown checkpoint content contract'; end if;
   if exists(select 1 from generate_series(0,(definition->>'difficulty')::integer-1) as required(level) where not exists(
    select 1 from public.thorax_attempts a where a.user_id=uid and a.generation=account.generation
     and a.record->>'unitId'=definition->>'unitId' and a.record->>'mode'=definition->>'mode'
     and a.record->>'contentVersion'=definition->>'version' and (a.record->>'difficulty')::integer=required.level
     and a.record->>'phase'='2' and a.record->>'status'='1' and a.record->>'passed'='true'
     and not coalesce((a.record->>'assisted')::boolean,false) and not coalesce((a.record->>'criticalFailure')::boolean,false)
     and (a.record->>'score')::numeric>=80)) then raise exception 'Current curriculum Easy and prior difficulty mastery required'; end if;
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
