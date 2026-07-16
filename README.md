# EnergyMarket

Imports Spanish Day-Ahead electricity auction prices from OMIE's
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


# Research Findings
## 1. What is the Spanish Spot Power Exchange, and what's its role?

Think of electricity like a perishable good that can't be stored cheaply at scale: it has to be produced at almost the exact moment it's consumed. Someone has to match, every hour, "how much will be generated" with "how much will be consumed" — and set a price for it.

That matching happens in a **power exchange** — the Spanish/Iberian one is called **OMIE** (Operador del Mercado Ibérico de Energía). OMIE is the "Nominated Electricity Market Operator" (NEMO) for the Iberian Peninsula, and it manages day-ahead and intraday markets for electricity in Spain and Portugal. These markets enable price training in a way that is competitive, public, and transparent for all agents.

Concretely: generators (nuclear plants, wind farms, gas plants, solar parks) submit sell offers, and retailers/large consumers submit buy offers. OMIE runs an auction, produces a **single clearing price per hour** (or, since a recent change, per 15-minute block), and that price becomes "the" wholesale price of electricity for that period. The day-ahead market is the main trading platform for electricity in the Spanish peninsula (excluding the islands, Ceuta, and Melilla). Participation in that market is obligatory for all available generation units not under bilateral contracts. It is managed by OMIE, the designated electricity market operator.

This price then feeds into everything downstream: retailer pricing, dynamic/indexed consumer tariffs (PVPC), renewable plant revenue, battery arbitrage strategies, and financial hedging products.

## 2. What is the Day-Ahead auction, and how does it work?

**Simple version:** Once a day, every generator says "I'll sell X MWh at Y €/MWh for hour H tomorrow," and every buyer says "I need X MWh and I'll pay up to Y €/MWh for hour H tomorrow." OMIE stacks all the sell offers from cheapest to most expensive (the "merit order") and stacks buy offers from most willing-to-pay downward. Where the two curves cross is the clearing price for that hour.

**Technical version:**

- The day-ahead market, also called single day-ahead coupling (SDAC), aims to carry out electrical energy transactions by submitting selling and takeover bids for electrical energy on behalf of the market agents for the twenty-four hours of the following day. This market, coupled with Europe since 2014, is one of the crucial pieces in achieving the objective of the European Internal Energy Market. Every day of the year at 12:00 CET is the day-ahead market session where prices and electrical energies are set for all across Europe for the twenty-four hours of the next day.
- OMIE's day-ahead market operates on a well-known principle. Sellers submit supply bids consisting of price-quantity pairs for each generating unit and each hour of the following day. Buyers submit demand bids. The EUPHEMIA algorithm sorts all supply bids from cheapest to most expensive, finds the intersection with demand, and sets a single clearing price equal to the last accepted bid. Every accepted seller receives this price, regardless of their original bid. This is **marginal pricing** — the last (most expensive) unit needed to meet demand sets the price for everyone, including much cheaper units like nuclear or wind.
- Because Spain and Portugal are electrically interconnected with the rest of Europe, OMIE's auction doesn't run in isolation — it's coupled with all other European day-ahead markets via the pan-European EUPHEMIA algorithm. Their buying and selling bids are accepted based on their economic merit and depending on the available capacity for interconnection between price zones. If, at a certain time of day, the capacity for interconnection between two zones is sufficient to allow the flow of electricity resulting from negotiation, the price of electricity at that time will be the same in both zones. If, on the other hand, interconnection at that time is maxed out, at that moment the algorithm for setting prices results in a different price in each zone. The mechanism described for setting electricity prices is called market coupling.
- The market result isn't final the instant the auction clears — physics has a say too. Once these results are obtained, they are sent to the System Operator for validation with perspective on their technical viability... ensures that the market results can be technically accommodated on the transportation network... results from the day-ahead market may be altered slightly as a result of the analysis of technical limitations done by the System Operator, giving rise to a viable daily program. In Spain that System Operator is **Red Eléctrica de España (REE)**.
- There's also a price ceiling: The reference harmonised maximum clearing price for SDAC shall be +4000 EUR/MWh. And notably, prices can go **negative** — meaning generators effectively pay to keep running, which happens on very windy/sunny days with oversupply.
- After the day-ahead result, agents can still adjust via **intraday markets** (auctions + a continuous market) closer to real time, since forecasts (wind, solar, demand) get more accurate as delivery approaches.

## 3. Why does the Day-Ahead auction exist?

It exists to solve a coordination problem that no single company could solve alone:

- **No physical storage at scale** → supply must equal demand essentially in real time, every hour of every day.
- **Thousands of independent generators and buyers** → without a centralized mechanism, matching them bilaterally for every hour would be chaotic and inefficient.
- **Economic efficiency** → the merit-order/marginal-pricing mechanism means the cheapest available generation is used first, which (in theory) minimizes total system cost. The day-ahead market's outcome dictates the electricity generation schedule for the following day, optimising for economic efficiency—in other words, the lowest-cost generation sources win the auction.
- **Give the grid operator lead time** → producing a schedule a day ahead lets REE check technical feasibility (grid congestion, security of supply) *before* it becomes an emergency, then use faster mechanisms (intraday, balancing, reserves) to fine-tune.
- **European integration** → coupling day-ahead markets across borders (via EUPHEMIA) lets cheap power flow to where it's needed across Europe, using scarce interconnection capacity as efficiently as possible.

In short: the day-ahead auction exists because "the day before" is a sweet spot — accurate enough forecasts of demand/renewables to plan a sensible schedule, but early enough to let the grid operator and market fix problems before physical delivery.

## 4. Where and by whom are the results published?

Two organizations, two roles — this matters a lot for a software engineer picking a data source:

- **OMIE** (the market operator) publishes market-side results: day-ahead prices, matched volumes, aggregate supply/demand curves, cross-border capacities, bid details. Spanish daily basic matching process program... Aggregate supply and demand curves of Day-ahead market... Header of bids for Day-ahead Market... Files published in the last 10 days. These are published as downloadable files (CSV-like formats) via OMIE's "file access" area (omie.es), plus dashboards for daily/historical results.
- **REE / ESIOS** (Red Eléctrica's transparency platform, esios.ree.es) republishes and integrates this data alongside grid-operator information — generation schedules, technical constraint adjustments, curtailment, settlement files (I90), and consumer-facing indicators like PVPC (the regulated indexed tariff). Communicate with Iberian Market Operator, Spanish Pole (OMIE), to exchange the Day Ahead Market matching results and the successive Intraday Market results... E·sios Public Website https://www.esios.ree.es where the non confidential information, result of the SO market operations, or other information of public interest, related with the electricity markets is published by REE.
- ESIOS exposes this via a **REST API** (`api.esios.ree.es`) with indicator IDs — e.g., indicator 600 is commonly used for the Spanish day-ahead price, indicator 1001 for another price series — accessible via a free personal token. API e·sios Documentation · Personal token request · Archive · Getting a list of archives · Getting a list of archives by date · Getting a list of archives by date and filter by taxonomy terms · Getting a list of archives by start_date, end_date and date_type datos · Getting a list of archives by start_date, end_date and date_type publicacion · Getting a specific Archive · Getting json data for calculations by id_archive, start_date, end_date
- At the pan-European level, the same kind of coupled market results also flow into **ENTSO-E's Transparency Platform**, useful if you need a standardized cross-country view rather than Spain-specific detail.

## 5. Who uses this data, and why?

- **Retailers/utilities** — to price consumer contracts, especially indexed tariffs (PVPC) that track the wholesale price directly.
- **Generators (especially renewables without a PPA)** — to understand expected revenue; since solar/wind bid near zero, they get whatever the marginal (often gas-set) price is that hour — a dynamic explored in the sources as the "solar cannibalization effect," where solar plants offer at 0 €/MWh (because the sun is free), but receive the marginal price of the system, generally determined by gas plants... solar plants usually receive less due to a phenomenon called solar cannibalization effect.
- **Battery storage (BESS) operators and traders** — to arbitrage: charge when the price is low (or negative), discharge when it's high.
- **Large industrial consumers** — to shift consumption to cheaper hours.
- **Analysts, researchers, and fintech/energy-tech companies** — to build price forecasting models, curtailment analytics, dashboards, and trading algorithms.
- **Grid/system planners and regulators** — to study market efficiency, congestion, and the balance between renewable curtailment and price signals. Economic curtailment can also be understood as a form of oversupply. When more renewable generation is offered to the market than demand can absorb, even at very low or negative prices, the excess is simply not matched.

---
# Link to the AI Chat: https://claude.ai/share/bbbaf6d7-58d9-4d2a-9472-43f22c8ae65d
