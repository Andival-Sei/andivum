-- The current cloud project has no app_profiles rows. Do not silently guess a
-- mapping from legacy Auth0 identities: a Supabase Auth user must explicitly
-- own the new profile.
do $$
begin
    if exists (select 1 from public.app_profiles limit 1) then
        raise exception
            'Cannot migrate app_profiles automatically: legacy rows still exist';
    end if;
end;
$$;

drop policy if exists app_profiles_select_own on public.app_profiles;
drop policy if exists app_profiles_insert_own on public.app_profiles;
drop policy if exists app_profiles_update_own on public.app_profiles;

alter table public.app_profiles
    add column user_id uuid;

alter table public.app_profiles
    drop constraint if exists app_profiles_auth0_subject_length;

alter table public.app_profiles
    drop constraint if exists app_profiles_auth0_subject_key;

alter table public.app_profiles
    drop column if exists auth0_subject;

alter table public.app_profiles
    alter column user_id set default auth.uid(),
    alter column user_id set not null;

alter table public.app_profiles
    add constraint app_profiles_user_id_fkey
        foreign key (user_id) references auth.users (id) on delete cascade,
    add constraint app_profiles_user_id_key unique (user_id);

comment on table public.app_profiles is
    'Application profile keyed by Supabase Auth user id; not an auth credential store.';
comment on column public.app_profiles.user_id is
    'Immutable Supabase Auth user id. Never use email as the authorization key.';

create policy app_profiles_select_own
on public.app_profiles
for select
to authenticated
using ((select auth.uid()) = user_id);

create policy app_profiles_insert_own
on public.app_profiles
for insert
to authenticated
with check ((select auth.uid()) = user_id);

create policy app_profiles_update_own
on public.app_profiles
for update
to authenticated
using ((select auth.uid()) = user_id)
with check ((select auth.uid()) = user_id);
