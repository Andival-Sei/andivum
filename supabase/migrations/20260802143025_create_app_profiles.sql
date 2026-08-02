create table public.app_profiles (
    id uuid primary key default gen_random_uuid(),
    auth0_subject text not null default (auth.jwt() ->> 'sub') unique,
    email text,
    display_name text,
    locale text not null default 'ru-RU',
    theme text not null default 'system',
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now(),
    constraint app_profiles_auth0_subject_length
        check (char_length(auth0_subject) between 1 and 255),
    constraint app_profiles_locale_allowed
        check (locale in ('en-US', 'ru-RU')),
    constraint app_profiles_theme_allowed
        check (theme in ('system', 'light', 'dark'))
);

comment on table public.app_profiles is
    'Application profile keyed by immutable Auth0 subject; not an auth credential store.';
comment on column public.app_profiles.auth0_subject is
    'Immutable Auth0 JWT sub. Never use email as the authorization key.';

create or replace function public.app_profiles_set_updated_at()
returns trigger
language plpgsql
set search_path = pg_catalog
as $$
begin
    new.updated_at = now();
    return new;
end;
$$;

revoke all on function public.app_profiles_set_updated_at() from public, anon, authenticated;

create trigger app_profiles_set_updated_at
before update on public.app_profiles
for each row
execute function public.app_profiles_set_updated_at();

grant select, insert, update on table public.app_profiles to authenticated;

alter table public.app_profiles enable row level security;

create policy app_profiles_select_own
on public.app_profiles
for select
to authenticated
using ((select auth.jwt() ->> 'sub') = auth0_subject);

create policy app_profiles_insert_own
on public.app_profiles
for insert
to authenticated
with check ((select auth.jwt() ->> 'sub') = auth0_subject);

create policy app_profiles_update_own
on public.app_profiles
for update
to authenticated
using ((select auth.jwt() ->> 'sub') = auth0_subject)
with check ((select auth.jwt() ->> 'sub') = auth0_subject);
