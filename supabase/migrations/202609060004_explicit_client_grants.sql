-- Supabase projects may grant new objects directly to anon/authenticated by default.
-- RLS does not govern TRUNCATE, so reset every client table privilege explicitly.
revoke all on public.thorax_learning_definitions, public.thorax_progress_accounts,
  public.thorax_attempts, public.thorax_checkpoints, public.thorax_settings_profiles
  from public, anon, authenticated;
grant select on public.thorax_learning_definitions, public.thorax_progress_accounts,
  public.thorax_attempts, public.thorax_checkpoints, public.thorax_settings_profiles
  to authenticated;
revoke all on function public.thorax_evaluate_attempt(jsonb,jsonb) from public, anon, authenticated;
revoke all on function public.thorax_sync_progress(integer,boolean,jsonb,jsonb) from public, anon, authenticated;
revoke all on function public.thorax_reset_progress() from public, anon, authenticated;
revoke all on function public.thorax_sync_settings(jsonb,text) from public, anon, authenticated;
grant execute on function public.thorax_sync_progress(integer,boolean,jsonb,jsonb),
  public.thorax_reset_progress(), public.thorax_sync_settings(jsonb,text) to authenticated;
