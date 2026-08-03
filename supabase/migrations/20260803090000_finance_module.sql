create table public.finance_accounts (
    id uuid primary key default gen_random_uuid(),
    user_id uuid not null default auth.uid() references auth.users (id) on delete cascade,
    name text not null,
    account_type text not null default 'cash',
    currency char(3) not null default 'RUB',
    opening_balance_minor bigint not null default 0,
    archived_at timestamptz,
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now(),
    constraint finance_accounts_name_length check (char_length(name) between 1 and 120),
    constraint finance_accounts_type_allowed check (account_type in ('cash', 'bank', 'card', 'savings', 'wallet', 'other')),
    constraint finance_accounts_currency_allowed check (currency ~ '^[A-Z]{3}$'),
    constraint finance_accounts_user_name_key unique (user_id, name),
    constraint finance_accounts_id_user_key unique (id, user_id)
);

create table public.finance_categories (
    id uuid primary key default gen_random_uuid(),
    user_id uuid references auth.users (id) on delete cascade,
    slug text not null,
    name_ru text not null,
    name_en text not null,
    category_type text not null,
    parent_id uuid references public.finance_categories (id) on delete restrict,
    icon text,
    is_system boolean not null default false,
    is_archived boolean not null default false,
    created_at timestamptz not null default now(),
    constraint finance_categories_slug_length check (slug ~ '^[a-z0-9]+([.-][a-z0-9]+)*$'),
    constraint finance_categories_type_allowed check (category_type in ('income', 'expense', 'transfer')),
    constraint finance_categories_system_owner check (is_system = (user_id is null)),
    constraint finance_categories_name_ru_length check (char_length(name_ru) between 1 and 120),
    constraint finance_categories_name_en_length check (char_length(name_en) between 1 and 120),
    constraint finance_categories_user_slug_key unique (user_id, slug)
);

create unique index finance_categories_system_slug_key
    on public.finance_categories (slug)
    where user_id is null;

create table public.finance_transactions (
    id uuid primary key default gen_random_uuid(),
    user_id uuid not null default auth.uid() references auth.users (id) on delete cascade,
    account_id uuid not null,
    transaction_type text not null,
    title text not null,
    occurred_on date not null,
    currency char(3) not null,
    total_minor bigint not null,
    source text not null default 'manual',
    import_fingerprint text,
    notes text,
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now(),
    constraint finance_transactions_account_owner_fk
        foreign key (account_id, user_id)
        references public.finance_accounts (id, user_id)
        on delete restrict,
    constraint finance_transactions_type_allowed check (transaction_type in ('income', 'expense', 'transfer')),
    constraint finance_transactions_title_length check (char_length(title) between 1 and 240),
    constraint finance_transactions_currency_allowed check (currency ~ '^[A-Z]{3}$'),
    constraint finance_transactions_total_positive check (total_minor > 0),
    constraint finance_transactions_source_allowed check (source in ('manual', 'ocr', 'ai', 'import')),
    constraint finance_transactions_import_fingerprint_length check (
        import_fingerprint is null or char_length(import_fingerprint) between 16 and 128
    ),
    constraint finance_transactions_user_id_key unique (id, user_id)
);

create unique index finance_transactions_import_fingerprint_key
    on public.finance_transactions (user_id, import_fingerprint)
    where import_fingerprint is not null;

create table public.finance_transaction_items (
    id uuid primary key default gen_random_uuid(),
    user_id uuid not null default auth.uid() references auth.users (id) on delete cascade,
    transaction_id uuid not null,
    name text not null,
    quantity numeric(18, 6) not null default 1,
    unit_price_minor bigint not null,
    line_total_minor bigint not null,
    category_id uuid not null references public.finance_categories (id) on delete restrict,
    sort_order integer not null default 0,
    constraint finance_items_transaction_owner_fk
        foreign key (transaction_id, user_id)
        references public.finance_transactions (id, user_id)
        on delete cascade,
    constraint finance_items_name_length check (char_length(name) between 1 and 240),
    constraint finance_items_quantity_positive check (quantity > 0),
    constraint finance_items_unit_price_nonnegative check (unit_price_minor >= 0),
    constraint finance_items_total_nonnegative check (line_total_minor >= 0),
    constraint finance_items_sort_order_nonnegative check (sort_order >= 0)
);

create table public.finance_attachments (
    id uuid primary key default gen_random_uuid(),
    user_id uuid not null default auth.uid() references auth.users (id) on delete cascade,
    transaction_id uuid,
    storage_path text not null,
    original_name text not null,
    mime_type text not null,
    byte_size bigint not null,
    sha256 text not null,
    ocr_text text,
    created_at timestamptz not null default now(),
    constraint finance_attachments_transaction_owner_fk
        foreign key (transaction_id, user_id)
        references public.finance_transactions (id, user_id)
        on delete set null,
    constraint finance_attachments_size_allowed check (byte_size between 1 and 20971520),
    constraint finance_attachments_sha256_length check (sha256 ~ '^[a-f0-9]{64}$'),
    constraint finance_attachments_path_owned check (storage_path like user_id::text || '/%')
);

create index finance_transactions_user_date_idx
    on public.finance_transactions (user_id, occurred_on desc, created_at desc);
create index finance_transaction_items_transaction_idx
    on public.finance_transaction_items (transaction_id, sort_order);
create index finance_categories_user_parent_idx
    on public.finance_categories (user_id, parent_id, slug);

create or replace function public.finance_set_updated_at()
returns trigger
language plpgsql
set search_path = pg_catalog
as $$
begin
    new.updated_at = now();
    return new;
end;
$$;

revoke all on function public.finance_set_updated_at() from public, anon, authenticated;

create trigger finance_accounts_set_updated_at
before update on public.finance_accounts
for each row execute function public.finance_set_updated_at();

create trigger finance_transactions_set_updated_at
before update on public.finance_transactions
for each row execute function public.finance_set_updated_at();

alter table public.finance_accounts enable row level security;
alter table public.finance_categories enable row level security;
alter table public.finance_transactions enable row level security;
alter table public.finance_transaction_items enable row level security;
alter table public.finance_attachments enable row level security;

create policy finance_accounts_select_own on public.finance_accounts
for select to authenticated using ((select auth.uid()) = user_id);
create policy finance_accounts_insert_own on public.finance_accounts
for insert to authenticated with check ((select auth.uid()) = user_id);
create policy finance_accounts_update_own on public.finance_accounts
for update to authenticated using ((select auth.uid()) = user_id)
with check ((select auth.uid()) = user_id);

create policy finance_categories_select_visible on public.finance_categories
for select to authenticated using (user_id is null or (select auth.uid()) = user_id);
create policy finance_categories_insert_own on public.finance_categories
for insert to authenticated with check (
    (select auth.uid()) = user_id and is_system = false
);
create policy finance_categories_update_own on public.finance_categories
for update to authenticated using ((select auth.uid()) = user_id and is_system = false)
with check ((select auth.uid()) = user_id and is_system = false);
create policy finance_categories_delete_own on public.finance_categories
for delete to authenticated using ((select auth.uid()) = user_id and is_system = false);

create policy finance_transactions_select_own on public.finance_transactions
for select to authenticated using ((select auth.uid()) = user_id);
create policy finance_transactions_update_own on public.finance_transactions
for update to authenticated using ((select auth.uid()) = user_id)
with check ((select auth.uid()) = user_id);
create policy finance_transactions_delete_own on public.finance_transactions
for delete to authenticated using ((select auth.uid()) = user_id);

create policy finance_items_select_own on public.finance_transaction_items
for select to authenticated using ((select auth.uid()) = user_id);
create policy finance_items_update_own on public.finance_transaction_items
for update to authenticated using ((select auth.uid()) = user_id)
with check ((select auth.uid()) = user_id);
create policy finance_items_delete_own on public.finance_transaction_items
for delete to authenticated using ((select auth.uid()) = user_id);

create policy finance_attachments_select_own on public.finance_attachments
for select to authenticated using ((select auth.uid()) = user_id);
create policy finance_attachments_insert_own on public.finance_attachments
for insert to authenticated with check ((select auth.uid()) = user_id);
create policy finance_attachments_delete_own on public.finance_attachments
for delete to authenticated using ((select auth.uid()) = user_id);

create or replace function public.finance_create_transaction(payload jsonb)
returns jsonb
language plpgsql
security invoker
set search_path = public, pg_catalog
as $$
declare
    current_user_id uuid := (select auth.uid());
    account_uuid uuid := (payload ->> 'account_id')::uuid;
    transaction_uuid uuid;
    transaction_kind text := payload ->> 'type';
    transaction_total bigint := (payload ->> 'total_minor')::bigint;
    transaction_currency text := upper(payload ->> 'currency');
    fingerprint text := nullif(payload ->> 'import_fingerprint', '');
    item jsonb;
    category_uuid uuid;
    item_total bigint := 0;
    item_index integer := 0;
begin
    if current_user_id is null then
        raise exception 'Authentication is required.' using errcode = '42501';
    end if;
    if transaction_kind not in ('income', 'expense', 'transfer') then
        raise exception 'Transaction type is invalid.' using errcode = '22023';
    end if;
    if nullif(trim(payload ->> 'title'), '') is null then
        raise exception 'Transaction title is required.' using errcode = '22023';
    end if;
    if transaction_total is null or transaction_total <= 0 then
        raise exception 'Transaction total must be positive.' using errcode = '22023';
    end if;
    if transaction_currency is null or transaction_currency !~ '^[A-Z]{3}$' then
        raise exception 'Currency must be an ISO 4217 code.' using errcode = '22023';
    end if;
    if not exists (
        select 1 from public.finance_accounts
        where id = account_uuid and user_id = current_user_id
    ) then
        raise exception 'Account does not belong to the current user.' using errcode = '42501';
    end if;
    if fingerprint is not null then
        select id into transaction_uuid
        from public.finance_transactions
        where user_id = current_user_id and import_fingerprint = fingerprint;
        if transaction_uuid is not null then
            return jsonb_build_object(
                'duplicate', true,
                'transaction_id', transaction_uuid
            );
        end if;
    end if;

    if jsonb_typeof(payload -> 'items') <> 'array' or jsonb_array_length(payload -> 'items') = 0 then
        raise exception 'At least one transaction item is required.' using errcode = '22023';
    end if;

    for item in select value from jsonb_array_elements(payload -> 'items') loop
        if nullif(item ->> 'name', '') is null then
            raise exception 'Transaction item name is required.' using errcode = '22023';
        end if;
        if (item ->> 'quantity')::numeric <= 0 or (item ->> 'line_total_minor')::bigint < 0 then
            raise exception 'Transaction item amount is invalid.' using errcode = '22023';
        end if;

        select id into category_uuid
        from public.finance_categories
        where slug = item ->> 'category_slug'
          and category_type = transaction_kind
          and (user_id is null or user_id = current_user_id)
        order by (user_id is null), created_at
        limit 1;
        if category_uuid is null then
            raise exception 'Category is not available for this transaction type.' using errcode = '22023';
        end if;
        item_total := item_total + (item ->> 'line_total_minor')::bigint;
    end loop;

    if item_total <> transaction_total then
        raise exception 'Item totals must equal the transaction total.' using errcode = '22023';
    end if;

    insert into public.finance_transactions (
        user_id, account_id, transaction_type, title, occurred_on,
        currency, total_minor, source, import_fingerprint, notes
    ) values (
        current_user_id,
        account_uuid,
        transaction_kind,
        left(coalesce(nullif(payload ->> 'title', ''), 'Без названия'), 240),
        (payload ->> 'occurred_on')::date,
        transaction_currency,
        transaction_total,
        coalesce(nullif(payload ->> 'source', ''), 'manual'),
        fingerprint,
        payload ->> 'notes'
    ) returning id into transaction_uuid;

    for item in select value from jsonb_array_elements(payload -> 'items') loop
        select id into category_uuid
        from public.finance_categories
        where slug = item ->> 'category_slug'
          and category_type = transaction_kind
          and (user_id is null or user_id = current_user_id)
        order by (user_id is null), created_at
        limit 1;
        insert into public.finance_transaction_items (
            user_id, transaction_id, name, quantity, unit_price_minor,
            line_total_minor, category_id, sort_order
        ) values (
            current_user_id,
            transaction_uuid,
            left(item ->> 'name', 240),
            (item ->> 'quantity')::numeric,
            (item ->> 'unit_price_minor')::bigint,
            (item ->> 'line_total_minor')::bigint,
            category_uuid,
            item_index
        );
        item_index := item_index + 1;
    end loop;

    return jsonb_build_object(
        'duplicate', false,
        'transaction_id', transaction_uuid,
        'total_minor', transaction_total
    );
end;
$$;

grant select, insert, update, delete on
    public.finance_accounts,
    public.finance_categories,
    public.finance_transactions,
    public.finance_transaction_items,
    public.finance_attachments
to authenticated;
grant execute on function public.finance_create_transaction(jsonb) to authenticated;

insert into public.finance_categories (slug, name_ru, name_en, category_type, is_system)
values
    ('housing', 'Жильё', 'Housing', 'expense', true),
    ('housing.rent', 'Аренда', 'Rent', 'expense', true),
    ('housing.mortgage', 'Ипотека', 'Mortgage', 'expense', true),
    ('housing.repairs', 'Ремонт и обслуживание', 'Repairs and maintenance', 'expense', true),
    ('housing.furniture', 'Мебель и предметы интерьера', 'Furniture and decor', 'expense', true),
    ('utilities', 'Коммунальные услуги', 'Utilities', 'expense', true),
    ('utilities.electricity', 'Электричество', 'Electricity', 'expense', true),
    ('utilities.water', 'Вода и отопление', 'Water and heating', 'expense', true),
    ('utilities.internet', 'Интернет', 'Internet', 'expense', true),
    ('utilities.phone', 'Мобильная связь', 'Mobile phone', 'expense', true),
    ('food', 'Еда', 'Food', 'expense', true),
    ('food.groceries', 'Продукты', 'Groceries', 'expense', true),
    ('food.dairy', 'Молочные продукты', 'Dairy', 'expense', true),
    ('food.meat', 'Мясо и рыба', 'Meat and fish', 'expense', true),
    ('food.cafe', 'Кафе и рестораны', 'Cafes and restaurants', 'expense', true),
    ('food.delivery', 'Доставка еды', 'Food delivery', 'expense', true),
    ('transport', 'Транспорт', 'Transport', 'expense', true),
    ('transport.public', 'Общественный транспорт', 'Public transport', 'expense', true),
    ('transport.taxi', 'Такси и каршеринг', 'Taxi and car sharing', 'expense', true),
    ('transport.fuel', 'Топливо', 'Fuel', 'expense', true),
    ('transport.service', 'Обслуживание автомобиля', 'Car maintenance', 'expense', true),
    ('transport.insurance', 'Страховка автомобиля', 'Car insurance', 'expense', true),
    ('health', 'Здоровье', 'Health', 'expense', true),
    ('health.doctors', 'Врачи', 'Doctors', 'expense', true),
    ('health.medicine', 'Лекарства', 'Medicine', 'expense', true),
    ('health.dentist', 'Стоматология', 'Dentist', 'expense', true),
    ('health.fitness', 'Спорт и фитнес', 'Fitness', 'expense', true),
    ('clothing', 'Одежда и обувь', 'Clothing and shoes', 'expense', true),
    ('beauty', 'Красота и уход', 'Beauty and care', 'expense', true),
    ('education', 'Образование', 'Education', 'expense', true),
    ('education.courses', 'Курсы', 'Courses', 'expense', true),
    ('education.books', 'Книги', 'Books', 'expense', true),
    ('subscriptions', 'Подписки', 'Subscriptions', 'expense', true),
    ('subscriptions.software', 'Программы и сервисы', 'Software and services', 'expense', true),
    ('subscriptions.media', 'Кино и музыка', 'Media', 'expense', true),
    ('entertainment', 'Развлечения', 'Entertainment', 'expense', true),
    ('entertainment.hobbies', 'Хобби', 'Hobbies', 'expense', true),
    ('entertainment.events', 'События и концерты', 'Events and concerts', 'expense', true),
    ('travel', 'Путешествия', 'Travel', 'expense', true),
    ('travel.flights', 'Билеты', 'Flights', 'expense', true),
    ('travel.hotels', 'Отели', 'Hotels', 'expense', true),
    ('children', 'Дети', 'Children', 'expense', true),
    ('children.education', 'Образование детей', 'Children education', 'expense', true),
    ('children.clothes', 'Одежда детей', 'Children clothing', 'expense', true),
    ('pets', 'Животные', 'Pets', 'expense', true),
    ('pets.food', 'Корм и уход за животными', 'Pet food and care', 'expense', true),
    ('gifts', 'Подарки', 'Gifts', 'expense', true),
    ('taxes', 'Налоги', 'Taxes', 'expense', true),
    ('fees', 'Комиссии и банковские услуги', 'Fees and banking', 'expense', true),
    ('charity', 'Благотворительность', 'Charity', 'expense', true),
    ('other.expense', 'Прочие расходы', 'Other expenses', 'expense', true),
    ('income', 'Доходы', 'Income', 'income', true),
    ('income.salary', 'Зарплата', 'Salary', 'income', true),
    ('income.bonus', 'Премия', 'Bonus', 'income', true),
    ('income.freelance', 'Фриланс', 'Freelance', 'income', true),
    ('income.business', 'Бизнес', 'Business', 'income', true),
    ('income.interest', 'Проценты', 'Interest', 'income', true),
    ('income.dividends', 'Дивиденды', 'Dividends', 'income', true),
    ('income.refund', 'Возврат средств', 'Refund', 'income', true),
    ('income.gift', 'Подарок', 'Gift', 'income', true),
    ('income.other', 'Прочие доходы', 'Other income', 'income', true)
on conflict do nothing;

update public.finance_categories child
set parent_id = parent.id
from public.finance_categories parent
where child.parent_id is null
  and child.slug like parent.slug || '.%'
  and parent.slug not like '%.%'
  and child.slug <> parent.slug;

insert into storage.buckets (id, name, public, file_size_limit, allowed_mime_types)
values (
    'finance-receipts',
    'finance-receipts',
    false,
    20971520,
    array['image/jpeg', 'image/png', 'image/heic', 'image/webp', 'application/pdf',
          'message/rfc822', 'text/plain', 'text/csv', 'application/vnd.ms-outlook']
)
on conflict (id) do update set
    public = excluded.public,
    file_size_limit = excluded.file_size_limit,
    allowed_mime_types = excluded.allowed_mime_types;

create policy finance_receipts_select_own on storage.objects
for select to authenticated using (
    bucket_id = 'finance-receipts'
    and (storage.foldername(name))[1] = (select auth.uid())::text
);
create policy finance_receipts_insert_own on storage.objects
for insert to authenticated with check (
    bucket_id = 'finance-receipts'
    and (storage.foldername(name))[1] = (select auth.uid())::text
);
create policy finance_receipts_delete_own on storage.objects
for delete to authenticated using (
    bucket_id = 'finance-receipts'
    and (storage.foldername(name))[1] = (select auth.uid())::text
);
