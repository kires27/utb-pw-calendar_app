
**Obecné podmínky pro projekt:**

Návrh webové aplikace projektu (funkční požadavky, návrh entit/diagramu tříd/databáze). Můžete vytvořit dokument ve Wordu (nemusíte dělat diagramy v EA).
Webová aplikace typu ASP.NET Core MVC (verze 9.x nebo vyšší)
Aplikace může být vytvořena kompletně v této technologii, nebo lze využít jen pro back-end, např. ve formě Web API.
Jedinou povolenou alternativou je vytvoření backendu v Javě (framework Spring MVC) nebo Python (Django MVC), anebo PHP (Laravel -> MVC!!!) - doporučeno jen pokud už s tímto máte zkušenosti!
Projekt i tak musí splňovat podmínky využití MVC, vícevrstvé architektury, objektového programování. Ve webovém projektu se také nesmí objevit ani řádek SQL kódu, aby byl oddělen kód webové stránky a databáze (výjimkou je volání SQL procedur - ujistěte se ale, že víte, co znamená "SQL procedura" a "volání SQL procedury"!)
Pokud tyto podmínky nebudou splněny, tak projekt neprojde 7. týden kontrolou a ani obhajobou!
Vytvoření vícevrstvé aplikace -> minimálně musí být obsaženy vrstvy Presentation, Application, Infrastructure a Domain.
Všechna funkcionalita musí být vytvořena a používána pomocí služeb (services).
V Presentation vrstvě se nesmí objevit přímé použití Infrastructure vrstvy (kromě konfigurace).
V Controllerech nesmí být kód logicky spadající do nižších vrstev.
Vytvoření databáze technikou Code-First (s migracemi) a její napojení a použití pomocí EntityFrameworkCore (nebo obdobného frameworku).
Projekt musí obsahovat několik entit, popř. ViewModelů/DTO (minimálně 5 - nepočítají se entity z Entity Framework Core a ani z Identity, ani ErrorViewModel). Musí být definováno aspoň jedno propojení entit skrz cizí klíč.
Vytvoření Area "Admin", ve které budou uloženy Controllery a View pro správu všech položek v databázi adminem (admin bude moct spravovat i data uživatele, ale nebude jim smět měnit hash hesla).
Ve správě položek musí být implementována i editace položek.
Bude vyřešena hlavně serverová validace (ale nejlépe i ta klientská).
včetně vytvoření jednoho vlastního validačního atributu (tzn. takového, který jste vytvořili sami!).
Bude umožněna registrace a přihlášení uživatele pomocí Identity nebo ekvivalentního frameworku -> celkem aspoň 2 role (admin, zákazník/klient, popř. manager/redaktor apod.).
Admin má veškerá práva a může spravovat vše.
Funkce Managera je taková, že spravuje systém v oblastech, kde interaguje se zákazníky/klienty (např. v případě e-shopu jsou to produkty, objednávky, faktury apod.), ale nemůže měnit kritické vlastnosti systému (uživatele, role, přístupová práva, nastavení webové aplikace apod.).
Zákazník/klient využívá systém pro účely, ke kterým je určen (např. v e-shopu nakupuje produkty, vytváří pro sebe objednávky, prohlíží si jen své vlastní objednávky, generuje své faktury apod.).
Prvky na stránce by měly být responzivní (pomocí Bootstrap nebo jiné technologie, anebo vlastním řešením). Pro front-end je možné použít jakýkoliv framework (např. Vue, React, Angular, SvelteKit apod.)

https://www.tutorialsteacher.com/core/aspnet-core-application-project-structure
https://www.tutorialsteacher.com/core/aspnet-core-wwwroot
https://www.tutorialsteacher.com/core/aspnet-core-program


# Startup

Everything runs in Docker — no dotnet on the host needed.

## one-time setup
```bash
sudo pacman -S docker docker-compose
sudo systemctl enable --now docker
sudo usermod -aG docker $USER   # log out/in once, then docker works without sudo
```

## run
```bash
docker compose up -d            # db + migrations + web (hot reload enabled)
```

website url
`http://localhost:5292/`

seed data (users, roles, events, notifications) is applied automatically
by the `SeedDataInDbContext` migration on first start — logins below

## stop
```bash
docker compose down             # stop everything, data kept
```
> `docker compose down -v` also deletes the database volume — avoid

## code changes
Source is bind-mounted into the container with `dotnet watch` — edit normally,
changes apply on save, no rebuild needed.

# project dependencies

dotnet, MySQL and dotnet-ef all run inside containers (see `docker-compose.yml`):
- `web` / `migrator` — `mcr.microsoft.com/dotnet/sdk:9.0`, source mounted at `/src`
- `db` — `mysql:8.0.29`, data in named volume `pw_calendar_db`

NuGet packages cache in `.docker/nuget/` (host dir, gitignored).

*build / shell inside container*
```bash
docker compose run --rm web bash -c "dotnet build CalendarApp.Web"
docker compose run --rm web bash      # interactive shell with dotnet + dotnet-ef
```

## EF Migration

Migrations run in the container (`migrator` service applies pending migrations
on every `docker compose up`). For creating new migrations:

*create new migration*
```bash
docker compose run --rm web bash -c "dotnet tool restore && \
  dotnet tool run dotnet-ef migrations add <Name> \
  --project ./CalendarApp.Infrastructure \
  --startup-project ./CalendarApp.Web"
```

*apply migrations (also runs automatically on `up`)*
```bash
docker compose run --rm migrator
```

*check applied migrations*
```bash
docker compose run --rm web bash -c "dotnet tool restore && \
  dotnet tool run dotnet-ef migrations list \
  --project ./CalendarApp.Infrastructure \
  --startup-project ./CalendarApp.Web"
```

### seed users

seeded via EF Core `HasData` in `CalendarDbContext` (migration
`SeedDataInDbContext`), applied by the migrator on `docker compose up`:

*username*	*email*					        *password*		*role*
admin		    admin@calendarapp.com	  Admin123!			Admin
alexjohnson	alex.johnson@email.com	User123!			User
sarahchen	  sarah.chen@email.com	  User123!			User
mikedavis	  mike.davis@email.com	  User123!			User

> log in with the **username** (not the email)

> existing databases: the seed uses fixed IDs (roles 1-2, users -1..-4).
> if the db already has those users/roles, the migration fails on the
> unique index — delete the volume (`docker compose down -v`) or remove
> the conflicting rows first

## docker

```bash
docker compose up -d                 # start (db + migrate + web)
docker compose down                  # stop, keep data
docker compose ps                    # running containers
docker compose logs web -f           # follow app logs
docker compose logs db -f            # follow MySQL logs
docker compose run --rm web bash     # shell inside web container
docker compose exec db mysql -u root -p   # MySQL client in db container
```

MySQL data lives in named volume `pw_calendar_db` (persists across `down`).

## database

browse from host — port `3306` is published

VS Code extension: *Database Client* (`cweijan.vscode-mysql-client2`)

*connection*
- host: `localhost`
- port: `3306`
- user: `root`
- password: `root`
- database: `calendar_db`
