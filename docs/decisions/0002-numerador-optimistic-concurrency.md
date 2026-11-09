# ADR 0002 — Registration numbers from an Azure Function with optimistic concurrency

Date: 2026-11-09
Status: accepted

## Context

A registry office (*mesa de entradas*) needs registration numbers that are unique, never reused,
gap-free and restart every year per register: `E-2026-000001` for inbound, `S-…` for outbound,
`I-…` for internal documents.

The first version of the Power Automate flow `Registrar entrada` built the number from the ID of the
new item in the SharePoint list `Registro`. The ID is atomic, but it is shared by all three registers,
never restarts in January and leaves a hole whenever an item is deleted. Reading "max + 1" from the
list inside the flow is worse: two flows running at the same moment read the same maximum and issue
the same number.

## Decision

Issue numbers from an Azure Function (`POST /api/numerador/{flujo}`, .NET 10 isolated worker)
that keeps one counter per register and year in Azure Table Storage and increments it with
optimistic concurrency:

1. read the counter and its ETag;
2. write `value + 1` with `If-Match: ETag` — Table Storage rejects the write with 412 if anyone
   changed the counter in between;
3. on 412 re-read and retry with randomised back-off; the very first number of a year is an insert
   that fails with 409 if another caller created it first.

The algorithm lives in `Registro.Core` behind `ICounterStore`, so it is unit-tested without Azure;
`TableCounterStore` in `Registro.Functions` is the only storage-specific code.

## Alternatives considered

- **List item ID** (status quo) — atomic, but not per register/year and not gap-free.
- **Pessimistic lock** (blob lease, SQL `UPDATE … OUTPUT`) — correct, but a crashed caller holds the
  lock until it expires, and SQL adds a paid resource for one integer.
- **Dataverse autonumber column** — built-in, but the prefix/year reset needs custom logic anyway and
  the documents live in SharePoint, not Dataverse.

## Consequences

- Concurrency is covered by a test with 300 parallel callers (unique, contiguous numbers) and was
  checked against the Azurite emulator.
- A number is consumed when it is issued: if the flow fails after calling the function, that number
  stays unused. This matches registry practice (an issued number is never reassigned); the gap is
  visible in the register.
- Calling the function from Power Automate uses the HTTP connector, which is a premium connector.
