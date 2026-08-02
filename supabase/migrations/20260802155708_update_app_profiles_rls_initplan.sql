-- Cache the Auth0 JWT lookup once per query instead of re-evaluating it for
-- every candidate row in the RLS policy.
drop policy if exists app_profiles_select_own on public.app_profiles;
drop policy if exists app_profiles_insert_own on public.app_profiles;
drop policy if exists app_profiles_update_own on public.app_profiles;

create policy app_profiles_select_own
on public.app_profiles
for select
to authenticated
using (((select auth.jwt()) ->> 'sub') = auth0_subject);

create policy app_profiles_insert_own
on public.app_profiles
for insert
to authenticated
with check (((select auth.jwt()) ->> 'sub') = auth0_subject);

create policy app_profiles_update_own
on public.app_profiles
for update
to authenticated
using (((select auth.jwt()) ->> 'sub') = auth0_subject)
with check (((select auth.jwt()) ->> 'sub') = auth0_subject);
