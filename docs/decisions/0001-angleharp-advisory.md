# ADR 0001 — Suppress the AngleSharp advisory instead of forcing an upgrade

Date: 2026-09-06
Status: accepted

## Context

`dotnet build` reports NU1902 for AngleSharp 0.17.0 (GHSA-pgww-w46g-26qg / CVE-2026-54570,
mXSS via the annotation-xml HTML integration point). The package is not referenced directly:
it is a transitive dependency of PnP.Core 1.17.0, the latest stable release at the time of writing.
The advisory is fixed in AngleSharp 1.5.0 — a major version change.

## Decision

Suppress the advisory at repository level via `NuGetAuditSuppress` in `Directory.Build.props`
and keep the AngleSharp version resolved by PnP.Core.

## Rationale

The vulnerability affects HTML sanitisation performed with AngleSharp parsing. Registro does not
sanitise untrusted HTML; AngleSharp is used only internally by PnP.Core. Pinning AngleSharp 1.5.0
directly would override the version PnP.Core is built and tested against, trading a non-applicable
advisory for an untested binary compatibility risk.

## Consequences

- The build stays warning-free and the suppression is documented and traceable.
- Revisit when PnP.Core publishes a release depending on AngleSharp >= 1.5.0, then remove
  the suppression and this ADR entry.
- Advisories affecting code paths Registro actually uses are fixed by upgrading, not suppressed —
  as done for System.Security.Cryptography.Xml (10.0.11, then 10.0.14 together with
  System.Security.Cryptography.Pkcs 10.0.14 on 2026-11-09).
