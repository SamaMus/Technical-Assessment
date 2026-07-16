# EnergyMarket

This is a direct transcription of the solution built in a separate chat
(pasted to me in two parts: an initial draft, then a follow-up that fixed
two violations found in review). Nothing here is new architecture from me —
I only fixed a handful of Razor `@` symbols that were stripped by markdown
rendering when the code was pasted (e.g. `AppAssembly="typeof(Program)..."`
had lost its `@` prefix), since those are not valid Razor syntax as pasted
and the file wouldn't render otherwise.

## What this is

A .NET 10 solution that imports the Spanish Day-Ahead electricity spot price
(published by OMIE's public `MARGINALPDBC` file, no auth required), persists
it in SQLite, exposes it via a REST API, and displays it in a Blazor Server
frontend that only talks to the REST API (no direct DB/Domain/Infrastructure
reference from Web).

```
EnergyMarket/
├── EnergyMarket.sln
└── src/
    ├── EnergyMarket.Domain/          entities, value objects, validator,
    │                                  DayAheadPriceImportService (orchestration)
    ├── EnergyMarket.Infrastructure/   EF Core (SQLite), OMIE file client + parser
    ├── EnergyMarket.Api/              REST API + Quartz.NET scheduled job
    └── EnergyMarket.Web/              Blazor Server, HttpClient-only consumer
```

## Verification checklist applied (from the source chat's own review pass)

| # | Requirement | Status |
|---|---|---|
| 1 | Quartz.NET for scheduling (not `BackgroundService`/`PeriodicTimer`) | Fixed — `EnergyMarket.Api/Scheduling/ImportDayAheadPricesJob.cs` + `QuartzSchedulingExtensions.cs`, registered via `AddImportScheduling()` in `Program.cs`. |
| 2 | `MARGINALPDBC` prices use `.` as decimal separator, not `,` | Fixed — `MarginalPdbcParser` uses `CultureInfo.InvariantCulture` with explicit `NumberStyles.AllowDecimalPoint \| NumberStyles.AllowLeadingSign`, not `es-ES`. |
| 3 | `net10.0` everywhere, current (post-.NET 8) Blazor Web App template shape | Already correct in the source. |
| 4 | `Web` has zero project references to Domain/Infrastructure/Api | Already correct — `EnergyMarket.Web.csproj` has no `ProjectReference`, only a resilience package. |
| 5 | Proportionate project count (no separate Worker project) | Already correct — Quartz hosted inside `Api`. |

## Known gaps and uncertainty flags, stated in the source chat and preserved here

These are exactly the caveats given when this code was written — I have not
been able to compile it (no .NET SDK or outbound network in this sandbox),
so treat `dotnet build` as the actual first verification step, not a
formality:

- **The OMIE `MARGINALPDBC` parsing logic is the one piece to trust least
  until verified.** It's written defensively (skips unparseable lines,
  logs what it skips, rather than throwing) but was not checked against a
  real downloaded file byte-for-byte in the original session. Download one
  real `marginalpdbc_YYYYMMDD.1` file and confirm a sample price value
  against the parsed `decimal` before relying on it.
- **Quartz.NET DI registration syntax** (`AddQuartz`, `AddJob`, `AddTrigger`,
  `AddQuartzHostedService`) is stable across Quartz 3.x historically, but was
  not compiled against `net10.0` specifically in the original session. If
  `ImportDayAheadPricesJob`'s constructor injection of the `Scoped`
  `DayAheadPriceImportService` fails to resolve at runtime, swap to the
  commented `IServiceScopeFactory` fallback in the same file.
- **Package version numbers** (e.g. `10.0.0` for the EF Core/Http packages)
  are placeholders reflecting "match the net10.0 major version" — if these
  don't resolve on NuGet, run `dotnet add package <name>` without a version
  pin and let NuGet resolve the latest compatible release.
- **No test projects** were included in this pass, to keep the deliverable
  focused on "builds and runs."
- **No chart** — the Prices page shows a data table only, per the original
  scope-control decision; a chart can be layered on later without touching
  the architecture.
- **appsettings ports**: `EnergyMarket.Web/appsettings.json` points
  `Api:BaseUrl` at `https://localhost:5443/`. I added `launchSettings.json`
  to both projects so `dotnet run` binds to that port by default — check the
  actual bound URL printed by `dotnet run --project src/EnergyMarket.Api`
  and update `Web`'s `appsettings.json` if it differs on your machine.

## Running it

```bash
dotnet restore
dotnet build

dotnet run --project src/EnergyMarket.Api     # note the actual bound URL
dotnet run --project src/EnergyMarket.Web     # update its appsettings.json Api:BaseUrl if needed
```

Seed data immediately (don't wait for the hourly Quartz trigger):
```
POST https://localhost:5443/api/imports?from=2026-07-15&to=2026-07-16&force=false
```

Then open the Web app and search using Price Zone `1` (Spain) or `2` (Portugal).
