# EnergyMarket

Imports Spanish/Portuguese Day-Ahead electricity auction prices from OMIE's
public `MARGINALPDBC` file, stores them, and exposes them via a REST API and
a Blazor Server dashboard.

## Solution structure

```
EnergyMarket.sln
└── src/
    ├── EnergyMarket.Domain          Entities, value objects, validation, import orchestration
    ├── EnergyMarket.Infrastructure  EF Core (SQLite) + OMIE file client
    ├── EnergyMarket.Api             REST API + Quartz.NET scheduled import job
    └── EnergyMarket.Web             Blazor Server dashboard (HTTP client of the API only)
```

Four projects, no separate Worker process. Scheduling runs **inside** `EnergyMarket.Api`
via Quartz.NET. `EnergyMarket.Web` has **no project reference** to `Domain`, `Infrastructure`,
or `Api` — it talks to the API exclusively over HTTP using its own local DTOs.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- No database server required — data is stored in a local SQLite file
  (`energymarket.db`), created automatically on first run.
- No API key or registration required — OMIE's public file endpoint needs no authentication.

Verify your SDK version:

```bash
dotnet --version   # should report a 10.x.x version
```

## Getting the code building

```bash
git clone <this-repo>
cd EnergyMarket
dotnet restore
dotnet build
```

If `dotnet build` fails on a specific package version (e.g. `Quartz.Extensions.Hosting`,
`Microsoft.EntityFrameworkCore.Sqlite`, or `Microsoft.Extensions.Http.Resilience`), the
version pins in the `.csproj` files were written before this exact package set was
verified against `net10.0` in a live restore. Fix by re-adding the package without a
version pin and letting NuGet resolve the latest compatible release:

```bash
dotnet add src/EnergyMarket.Infrastructure package Microsoft.EntityFrameworkCore.Sqlite
dotnet add src/EnergyMarket.Api package Quartz.Extensions.Hosting
dotnet add src/EnergyMarket.Web package Microsoft.Extensions.Http.Resilience
```

## Running the API

```bash
dotnet run --project src/EnergyMarket.Api
```

On first launch:
- The SQLite schema is created automatically (`Database.EnsureCreated()` — see
  **Known limitations** below for why this isn't a proper migration).
- A Quartz.NET job runs immediately, then hourly, importing the last two days
  (Madrid calendar time) of Day-Ahead prices from OMIE.
- Swagger UI is available at `https://localhost:{port}/swagger` in the
  Development environment — check your terminal output for the actual port.

**Note the port printed in the console** — you'll need it for the next step.

### Triggering an import manually

Don't want to wait for the scheduled job? Trigger one directly:

```bash
curl -X POST "https://localhost:{port}/api/imports?from=2026-07-15&to=2026-07-16&force=false"
```

### Querying prices

```bash
# JSON
curl "https://localhost:{port}/api/prices?priceZoneId=1&from=2026-07-15&to=2026-07-15"

# CSV
curl -H "Accept: text/plain" "https://localhost:{port}/api/prices?priceZoneId=1&from=2026-07-15&to=2026-07-15"
```

`priceZoneId`: `1` = Spain, `2` = Portugal (these are our own convention — OMIE's
file has no zone identifier, just two fixed price columns per row).

## Running the Web dashboard

The Web project needs to know where the API is running. Before starting it,
update `src/EnergyMarket.Web/appsettings.json` (or `appsettings.Development.json`)
with the actual port the API printed on startup:

```json
{
  "Api": { "BaseUrl": "https://localhost:5001/" }
}
```

Then run it in a second terminal (with the API still running):

```bash
dotnet run --project src/EnergyMarket.Web
```

Open the printed URL in a browser, pick a price zone and date range, and click Search.

## Configuration reference

| Setting | Project | Purpose | Default |
|---|---|---|---|
| `ConnectionStrings:EnergyMarket` | Api | SQLite connection string | `Data Source=energymarket.db` |
| `Omie:BaseUrl` | Api (bound into Infrastructure) | OMIE file-download base URL | `https://www.omie.es/en/file-download` |
| `Omie:TimeoutSeconds` | Api | HTTP timeout for OMIE requests | `30` |
| `Api:BaseUrl` | Web | Where Web sends its HTTP requests | *(must be set manually — see above)* |

No secrets are required anywhere in this solution — OMIE's public file endpoint
and the local SQLite file need no credentials.

## Scheduling

Handled entirely by Quartz.NET, registered in `EnergyMarket.Api/Scheduling/QuartzSchedulingExtensions.cs`:

- One job (`ImportDayAheadPricesJob`), one trigger, firing immediately on startup
  and then every hour.
- Each run imports "yesterday and today" (Madrid calendar dates) and skips a
  date/zone combination that's already fully imported, unless triggered with `force=true`
  via the manual `/api/imports` endpoint.

## Known limitations (deliberate, not oversights)

- **`Database.EnsureCreated()` instead of EF Core migrations.** This gets the
  app running immediately with zero setup steps, but it means there's no
  migration history. Before any real deployment, replace this with
  `dotnet ef migrations add InitialCreate` (run from the solution root,
  `--project src/EnergyMarket.Infrastructure --startup-project src/EnergyMarket.Api`)
  and call `Database.Migrate()` instead.
- **`MARGINALPDBC` parsing is defensive but not exhaustively verified against every
  historical file variant.** The parser skips and logs (at Debug level) any line
  it can't parse rather than throwing, so a malformed or unexpected row won't
  crash an import — but if an entire day imports as empty, check the application
  logs first; the raw file format may differ from what's assumed in
  `MarginalPdbcParser.cs`.
- **No automated tests included in this version** — the previous design pass
  covered unit/integration tests for each layer (Domain validation rules,
  repository upsert/idempotency behavior, OMIE parsing edge cases, API status
  codes); they were trimmed here to prioritize a buildable, runnable solution
  first. Re-adding them is a natural next step.
- **No chart/visualization on the dashboard** — the Prices page shows a plain
  data table only; charting was deliberately left out to keep the Blazor
  project's dependency footprint minimal (see `EnergyMarket.Web.csproj`).

## Architecture at a glance

- **Domain** — no dependencies on EF Core, HTTP, or any external package.
  Contains `DayAheadPrice`/`PriceZone` entities, `MarketPeriod`/`DateRange`
  value objects, validation rules, and `DayAheadPriceImportService`, which
  orchestrates the whole import workflow against interfaces only.
- **Infrastructure** — implements those interfaces: `OmieMarginalPricesClient`
  (HTTP + file parsing, isolated from Domain) and `DayAheadPriceRepository`
  (EF Core + SQLite, isolated from Domain).
- **Api** — the only project that talks to both Domain and Infrastructure. Hosts
  the REST endpoints and the Quartz.NET scheduled job. Converts UTC to CET at
  the response boundary using `TimeZoneInfo` (DST-safe, no manual hour math).
- **Web** — a pure HTTP client of the Api project. No compile-time visibility
  into Domain, Infrastructure, or Api's internals — only a typed `HttpClient`
  and its own local response DTOs.
