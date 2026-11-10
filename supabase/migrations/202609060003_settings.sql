-- Named settings remain platform-specific. Cross-platform preference copying is explicit in the client.
revoke insert,update,delete on public.thorax_settings_profiles from authenticated;
create or replace function public.thorax_sync_settings(incoming_profile jsonb default null, delete_id text default null)
returns jsonb language plpgsql security definer set search_path=public,pg_temp as $$
declare uid uuid=auth.uid(); profile_id text;
begin
 if uid is null then raise exception 'Authentication required' using errcode='28000'; end if;
 perform pg_advisory_xact_lock(hashtextextended(uid::text,0));
 if incoming_profile is not null then
  profile_id=incoming_profile->>'id';
  if coalesce(profile_id,'')='' or length(profile_id)>64 or octet_length(incoming_profile::text)>16384 or coalesce(incoming_profile->>'platform','') not in ('Windows','Quest') or coalesce((incoming_profile->>'schemaVersion')::integer,0)<>1 or length(coalesce(incoming_profile->>'name','')) not between 1 and 48 then raise exception 'Invalid settings profile'; end if;
  if not exists(select 1 from public.thorax_settings_profiles where user_id=uid and id=profile_id) and (select count(*) from public.thorax_settings_profiles where user_id=uid)>=32 then raise exception 'Profile limit reached'; end if;
  insert into public.thorax_settings_profiles(user_id,id,platform,profile) values(uid,profile_id,incoming_profile->>'platform',incoming_profile)
  on conflict(user_id,id) do update set profile=excluded.profile,platform=excluded.platform,updated_at=now();
 end if;
 if delete_id is not null then delete from public.thorax_settings_profiles where user_id=uid and id=delete_id; end if;
 return jsonb_build_object('profiles',coalesce((select jsonb_agg(profile) from public.thorax_settings_profiles where user_id=uid),'[]'));
end $$;
revoke all on function public.thorax_sync_settings(jsonb,text) from public;
grant execute on function public.thorax_sync_settings(jsonb,text) to authenticated;
