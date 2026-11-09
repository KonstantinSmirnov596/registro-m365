# Registro — document registration on Microsoft 365

[![build](https://github.com/KonstantinSmirnov596/registro-m365/actions/workflows/build.yml/badge.svg)](https://github.com/KonstantinSmirnov596/registro-m365/actions/workflows/build.yml)
&nbsp;·&nbsp; [Versión en español](README.es.md)

**Registro** is a learning and portfolio project: the core of a records office (*mesa de entradas*) —
registration of inbound, outbound and internal documents, approval, retention — built on
**SharePoint Online, Power Automate, Power Apps, Microsoft Purview and Azure Functions (C#)**.

The rule of the project is *configuration first, code only where the platform has no answer*.
Everything that Microsoft 365 provides out of the box is configured in the tenant;
code is written for the one thing it cannot do reliably — issuing registration numbers.

> Status: work in progress (November 2026). What is listed below as *built* exists and runs in a
> Microsoft 365 trial tenant; flow and site definitions are not exported to this repository yet.

## What is built

| Area | Implementation | ECM concept |
|---|---|---|
| Information architecture | Communication site *Gestión Documental*; term store group `Registro` with term sets *Tipo documental*, *Serie documental*, *Dependencia*; 10 site columns; content type hierarchy `Documento registrado` → `Documento de entrada` / `de salida` / `interno`; libraries `Entrada`, `Salida`, `Interno` | document types, records series (file plan), organisational units, metadata schema |
| Registration log | SharePoint list `Registro`: number, flow, date, counterparty, text copies of the managed metadata, link and ID of the file, status | registration journal |
| Registration | Automated cloud flow **Registrar entrada**: when a file in `Entrada` has its card filled in → journal entry → number → file properties `Número de registro`, `Fecha de registro`, `Estado = Registrado` | registration of inbound correspondence |
| Approval | Instant cloud flow **Solicitar aprobación** (run from the file menu) with Approvals: `En aprobación` → `Aprobado` / `Rechazado` | approval workflow |
| Mobile register | Canvas app **Registro móvil** generated from the `Registro` list, with a status filter (delegable `Filter` on a Choice column) | register lookup on the go |
| Retention | Purview retention label *Correspondencia — 5 años*, published to the site | retention period by records series |
| Numbering | **Azure Function `Numerador`** (this repository) | atomic registration numbers |
| Provisioning (demo) | PnP PowerShell scripts that create a demo site, content type, library and register a document — `scripts/powershell/demo` | — |

## Numerador — why there is code

Registration numbers look like `E-2026-000001`: flow prefix (E inbound, S outbound, I internal),
year, six-digit sequence. They must be unique, never reused and restart every year per register.

The first version of the flow derived the number from the SharePoint list item ID — atomic, but shared
by all three registers, never restarting in January and leaving gaps on deletion. Computing "max + 1"
inside the flow is not an option either: two flows running at the same moment issue the same number.

`Numerador` keeps one counter per register and year in **Azure Table Storage** and increments it with
**optimistic concurrency**: read the counter with its ETag → write `value + 1` only if the ETag is
unchanged (otherwise Table Storage answers 412) → on conflict re-read and retry with random back-off.
No locks, so a failed caller can never block the register. Details and alternatives:
[ADR 0002](docs/decisions/0002-numerador-optimistic-concurrency.md).

```
POST /api/numerador/{flujo}[?anio=2026]      flujo = E | S | I | entrada | salida | interno

200 { "numero": "E-2026-000001", "flujo": "E", "anio": 2026, "secuencia": 1 }
400 unknown flow or invalid year        503 counter still contended after all retries
```

The year defaults to the organisation's calendar (`Numerador:TimeZone`, default `America/Montevideo`),
so a document registered on 31 December at 22:00 local time gets that year's number.

| Code | What it does |
|---|---|
| `src/Registro.Core/Numeracion` | algorithm (`Numerador`), number format (`NumeroRegistro`), storage contract (`ICounterStore`) — no Azure dependency |
| `src/Registro.Functions/Numeracion` | HTTP function and `TableCounterStore` on Azure Table Storage |
| `tests/Registro.Core.Tests` | format/parsing, per-register and per-year counters, **300 parallel callers → unique contiguous numbers**, give-up path |

### Run it locally

Requirements: .NET SDK 10, Azure Functions Core Tools 4, Azurite (Visual Studio starts it automatically
for Functions projects; otherwise `npm install -g azurite` and `azurite-table`).

```powershell
dotnet test --solution Registro.slnx          # unit tests
cd src/Registro.Functions
func start --port 7104                         # http://localhost:7104/api/numerador/{flujo}
Invoke-RestMethod -Method Post http://localhost:7104/api/numerador/E
```

### Calling it from Power Automate

In `Registrar entrada`, the *Compose* step that formatted the list item ID is replaced by an **HTTP**
action (`POST https://<function-app>.azurewebsites.net/api/numerador/E`, header `x-functions-key`),
and `body('HTTP')?['numero']` goes to the journal and to the file. The HTTP connector is premium.
This wiring is the next step; the function is not deployed to Azure yet.

## Repository layout

```
src/Registro.Core            domain logic (.NET 10)
src/Registro.Functions       Azure Functions, isolated worker (.NET 10, Azure.Functions.Sdk)
tests/Registro.Core.Tests    xUnit v3 on Microsoft.Testing.Platform
scripts/powershell/demo      PnP PowerShell: demo site, content type, document registration
docs/decisions               architecture decision records
sharepoint/ power-automate/ purview/   exports of tenant configuration (pending)
```

## Roadmap

- Deploy `Numerador` (Flex Consumption) and call it from `Registrar entrada`.
- Export site schema (PnP provisioning template) and flows (solution) into the repository.
- Harden the trigger condition of `Registrar entrada` (a card with empty metadata must not pass).
- Keep the journal status in sync with the file status; notify the requester after approval.
- Auto-apply retention labels; outbound (`Salida`) and internal (`Interno`) registration flows.
- Copilot Studio agent that answers questions about the registration procedure.

## Author

Konstantin Smirnov — ECM / document management developer moving from 1C:Document Management
to Microsoft 365. Montevideo, Uruguay.
