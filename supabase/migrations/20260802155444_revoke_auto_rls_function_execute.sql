-- Supabase's automatic-RLS helper is used by the database event trigger, not by
-- client requests. Keep it unavailable through the public Data API roles.
revoke execute on function public.rls_auto_enable() from public, anon, authenticated;
