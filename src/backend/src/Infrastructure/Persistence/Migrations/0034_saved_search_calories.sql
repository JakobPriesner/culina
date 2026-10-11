alter table saved_searches add column max_kcal integer
    check (max_kcal > 0 and max_kcal <= 100000);
