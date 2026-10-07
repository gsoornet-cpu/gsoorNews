# Phase 2 Decision-Readiness — Atmaen (اطمّن) Case Core + Diaspora Map

**Status: all 6 decisions below are now APPROVED by the developer.** Each
section is marked with its approved outcome; the original options/trade-off
analysis is kept below the approval line as a record of the reasoning, not as
still-open content. Decisions 4 (retention/anonymization) and 6 (demographic
data source) were approved as *mechanisms* with their final values/dataset
still explicitly pending legal review / data-sourcing discovery — see those
sections for the exact boundary. This document no longer speaks for itself
as the source of truth for what's approved: where it and a later, more
specific record (e.g. `docs/project-progress.html`) disagree on
implementation status, the more specific/more recent record wins.

This document expands the 6 decisions recorded in `docs/project-progress.html`
(Phase 2 → Slice 10 detail block). For each: what exactly must be decided, why
the architecture depends on it, the realistic options, their trade-offs, what
stays blocked without it, what can safely be built ahead of it, and one
proposed engineering default — offered for approval, not assumed.

Spec references are to the technical/editorial spec document (اطمّن =
"Atmaen"/"reassure"). §10 = Atmaen's 8-step flow and 8 case-status labels,
§13 = anti-enumeration/anti-impersonation/anti-scraping (spec: "mandatory
before launch"), §14 = contributor/correspondent response network, §22–23 =
security and data classification, §09 = Diaspora Map.

---

## 1. OTP/SMS provider — ✅ APPROVED

**Approved decision:** Twilio Verify (V2). Account and costs owned by the
project. Implemented behind the provider-agnostic `IOtpService` seam in
**Slice 12** (`docs/project-progress.html`) — status there is IMPLEMENTED,
AWAITING VERIFICATION (developer build/test + a real-Twilio-credentials
check are still needed; this sandbox has no NuGet or live network access to
confirm either).

Original analysis kept below for record — no longer an open decision.

**What this was:** which SMS/OTP delivery provider and account to use for
verifying an Atmaen applicant's identity (spec §10 step 5), and who owns the
account/credentials/cost.

**Why the architecture depends on it:** step 5 of the Atmaen flow is
explicitly the applicant-identity check that the rest of the flow (§13 —
"anti-impersonation: OTP + rate limiting + suspicious-pattern detection") is
built around. The interface Jusoor's Application layer would call
(`IOtpService` or equivalent) is provider-agnostic by construction, but the
concrete adapter, the delivery guarantees, and the cost model differ enough
between providers that building the adapter first means guessing at an
interface shape that may not fit the real provider's API (delivery
confirmation, retry semantics, per-country coverage).

**Options:**
- **Twilio Verify** — mature, global coverage, per-verification pricing,
  well-documented .NET SDK.
- **Azure Communication Services SMS** — already in the spec's own tech list
  (§26, alerting channel), same Azure billing as the rest of the stack, more
  limited country coverage than Twilio for some diaspora destinations.
- **A regional/local provider** (e.g. an Egypt- or MENA-focused SMS gateway)
  — potentially cheaper and better deliverability to Egyptian numbers
  specifically, but adds a second vendor relationship and likely weaker
  coverage for applicants calling from far-flung diaspora countries.

**Trade-offs:**
| | Coverage | Cost | Integration effort | Vendor lock-in |
|---|---|---|---|---|
| Twilio Verify | Best global | Higher per-message | Low (mature SDK) | Low (standard REST) |
| Azure Communication Services | Good, Azure-region-dependent | Bundled Azure billing | Low (same cloud) | Medium (Azure-specific) |
| Regional provider | Best for Egypt, weaker elsewhere | Lowest for target country | Higher (custom integration) | High |

**What stays blocked without it:** the entire Atmaen submission flow (steps
1–8) — step 5 gates step 6 (`Case` creation), so nothing past "describe the
concern" can be built end-to-end without *some* provider chosen, even a
placeholder.

**What can safely be built first:** the provider-agnostic pieces of steps
1–4 — country/region/city selection (already possible on Slice 10's
`Region`/`Neighborhood`), the free-text concern field, and the "subject
name only mandatory, phone/details optional" applicant-info step — behind an
`IOtpService` interface with no concrete implementation wired up yet, the
same seam pattern this project already uses for `IGeographicExtractor`
(Slice 6). This still risks the interface needing to change once a real
provider is chosen, so it's a genuine trade-off, not free work.

**Outcome:** the developer chose Twilio Verify directly rather than the
Azure Communication Services default proposed above — the safer choice on
global diaspora-number coverage, which the developer's decision explicitly
prioritized over single-cloud-vendor consolidation.

---

## 2. Field-level encryption and key management — ✅ APPROVED

**Approved decision:** EF Core value converters + Azure Key Vault + envelope encryption, exactly as proposed below — application-layer encryption, keys never stored alongside the encrypted data, designed for rotation without wholesale re-encryption where technically possible. Postgres-native encryption and `DataProtection` are explicitly excluded as the primary mechanism, per the developer's decision. Implemented in **Slice 13** (`docs/project-progress.html`) — status there is IMPLEMENTED, AWAITING VERIFICATION (a developer `dotnet build`/`dotnet test` run, plus a real Key Vault check of the Encrypt/Decrypt round-trip, are still needed).

Original analysis kept below for record.

**What this was:** which encryption library/approach for `Case` fields
(spec §10 step 6, §22: "Field-level Encryption" specifically for Atmaen, a
higher security tier than the rest of the platform), which key store, and the
key-rotation policy.

**Why the architecture depends on it:** this determines the actual shape of
the `Case` entity and its EF configuration — whether encryption happens at
the application layer (encrypt-before-save, decrypt-after-load, via an EF
value converter or a dedicated encryption service) or at the database layer
(e.g. Postgres `pgcrypto` or column-level TDE), and whether key material is
per-tenant, per-environment, or per-record. Building `Case` before this is
decided risks a migration that has to be reversed once the real encryption
approach is chosen — the same "wrong migration corrupts the schema" concern
already applied to every prior slice's EF migrations in this project.

**Options:**
- **Application-layer encryption via EF Core value converters + Azure Key
  Vault-backed keys** (spec already names Key Vault for secrets management,
  §22) — encrypt/decrypt happens in .NET code using a key fetched from Key
  Vault; keys never touch the database.
- **`Microsoft.AspNetCore.DataProtection`** — built into ASP.NET Core,
  supports key rotation and Key Vault as a key ring storage provider, but is
  designed for protecting short-lived tokens/cookies more than long-lived
  structured PII; using it for `Case` fields is workable but not its primary
  design intent.
- **Postgres native (`pgcrypto` / column encryption)** — keeps encryption in
  the database, simpler queries, but key material either lives in
  Postgres config (weaker separation from the data it protects) or requires
  passing keys per-query from the app anyway, which mostly cancels the
  simplicity benefit.

**Trade-offs:**
| | Key separation from data | Query-ability of encrypted fields | Rotation support | Fits existing stack |
|---|---|---|---|---|
| EF value converters + Key Vault | Strong | None (opaque ciphertext, no WHERE on encrypted cols) | Manual but controllable | High — Key Vault already named in spec |
| DataProtection API | Strong | None | Built-in | Medium — not its primary use case |
| Postgres-native | Weak-to-medium | Possible with pgcrypto functions, at a performance cost | Harder to do cleanly | Low — no other part of this stack encrypts at the DB layer |

**What stays blocked without it:** `Case` entity creation and its EF
configuration/migration (step 6), everything downstream of it (assignment,
status tracking, SignalR live status) — the entity's shape depends directly
on this choice.

**What can safely be built first:** nothing entity-specific; this is the one
decision in the list with the least safe-to-build-ahead surface, because the
`Case` entity's column types and EF configuration are the direct output of
this decision.

**Outcome:** approved as proposed, with two explicit exclusions added by the developer: Postgres-native encryption must not be the primary mechanism, and `DataProtection` must not be the primary long-lived-PII mechanism either (both were already the two options recommended against above, now made a hard rule rather than a preference).

---

## 3. Trusted response network — ✅ APPROVED

**Approved decision:** the minimal CrisisEditor-queue placeholder, exactly as proposed below, with two hard constraints the developer added: a CrisisEditor may act only on Cases assigned specifically to them (not the whole queue at large), and the full §14 network/algorithm must not be built now or invented as part of this simplification. Not yet implemented — queued as step 3, after Case encryption.

Original analysis kept below for record.

**What this was:** who actually receives and acts on an assigned Atmaen
case (spec §10 step 7 — "assign the case to the response network, distributed
by geographic coverage, per §14"), and what the minimum viable version of
that assignment mechanism is for this phase.

**Why the architecture depends on it:** Slice 10 already traced this as a
real cross-phase dependency, not a guess: §14's Contributor/correspondent
distribution algorithm — Local Correspondent / Regional Coordinator roles —
is the same system Slice 7 identified as Phase-3-adjacent and currently
deferred. Building the full §14 network now would mean building Phase 3
scope early; but Atmaen's own step 7 cannot function with zero response
network at all.

**Options:**
- **Build the full §14 Contributor/correspondent system now**, ahead of
  Phase 3 — matches the spec exactly, but pulls Phase-3-adjacent scope
  forward, which the standing instruction explicitly cautions against
  ("Timing test... not 'was it explicitly in this slice's brief'" cuts both
  ways: pulling scope forward too early is its own architecture risk).
  Also this system doesn't have its own decision-readiness done yet
  (per-role permissions, escalation rules) — building it now means making
  those decisions unreviewed, as a side effect of unblocking Atmaen.
  Suggested to avoid unless there is a real timeline reason.
  Alternative: build this in the next Phase 3 slice, not before.
- **A minimal, Phase-2-scoped placeholder assignment** — e.g. cases route to
  a small, manually maintained list of `CrisisEditor`-role users (already a
  seeded newsroom role, §25) rather than a geographically-distributed
  correspondent network, with a documented note that this is a temporary
  simplification until §14 exists. Matches this project's own precedent
  (Slice 9's Human Review Queue used the already-seeded role taxonomy
  instead of inventing a new one).
- **No automatic assignment at all in this phase** — cases are created and
  visible in a queue, and a human with the `CrisisEditor` role picks them up
  manually; "assignment" becomes a manual claim action rather than an
  algorithm. Simplest, but arguably under-delivers step 7 as specified
  ("توزيع حسب التغطية الجغرافية" — distribution by geographic coverage).

**Trade-offs:**
| | Matches spec §14 | Pulls Phase 3 forward | New unreviewed decisions required | Effort |
|---|---|---|---|---|
| Full §14 now | Exact | Yes | Yes (role permissions, escalation) | High |
| Minimal placeholder (CrisisEditor queue) | Partial (no real geo-distribution) | No | No | Low-medium |
| Manual claim, no algorithm | Weakest | No | No | Lowest |

**What stays blocked without it:** step 7 (case assignment) and step 8 (live
status via SignalR, since status changes need an assignee to report them) —
the whole back half of the Atmaen flow.

**What can safely be built first:** steps 1–6 (submission through case
creation) do not require this decision — they only need §1 and §2's outputs.
A `Case` can exist in an "unassigned" state without the assignment mechanism
being finalized yet.

**Outcome:** approved as proposed, with the assignment boundary sharpened: "CrisisEditor queue" means each CrisisEditor sees and can act on only the Cases assigned to them, not an unassigned shared pool any CrisisEditor can pick from — see the Phase 3 Role×Action decisions for the exact authorization rule this implies for both viewing and status-updating a Case.

---

## 4. Retention and anonymization rules — ✅ APPROVED AS A MECHANISM; VALUES STILL PENDING LEGAL REVIEW

**Approved decision:** build the configurable retention/anonymization *framework* now — policy-driven, varying by Case outcome/status, Hangfire-ready, with the PII fields it transforms/removes clearly identified while preserving legally-required audit information. The **final retention periods/legal rules remain explicitly NOT approved** and must not be hard-coded, presented to users as approved policy, or invented. Not yet implemented — queued as step 4.

Original analysis kept below for record.

**What this was:** the actual time period after which a `Case` is
archived/anonymized (§23 says this happens "after a defined period" without
naming it), and the applicable legal/compliance requirements for retaining
this class of personal-safety data (which jurisdiction's rules apply —
Egypt's, the country the diaspora user resides in, or both).

**Why the architecture depends on it:** this determines whether retention is
enforced by a scheduled Hangfire job (this project already has the
infrastructure for recurring jobs — `SourceIngestionSweepJob` is the existing
pattern) versus a manual archival action, what "anonymized" means at the
column level (nulling specific fields vs. a separate anonymization
transform), and what audit trail must survive anonymization for compliance
purposes even after the PII itself is gone.

**Options:**
- **A fixed retention period picked by the team** (e.g. "resolved cases are
  anonymized after 90 days") — simplest to implement, but is a real
  product/legal decision this document should not make unilaterally.
- **A retention period tied to case outcome** — e.g. shorter for
  successfully-resolved cases, longer for unresolved/high-severity ones,
  matching typical incident-response retention patterns — more nuanced, more
  implementation surface (multiple timers/policies instead of one).
- **Deferred: no automatic anonymization in this phase**, cases retained
  indefinitely until a policy is set, with manual deletion available on
  request — lowest implementation risk, but leaves the platform without the
  privacy-by-design guarantee §03 states as a binding principle
  ("لا قاعدة بيانات عامة تكشف أماكن أشخاص" and privacy-by-design generally).

**Trade-offs:**
| | Matches §23's intent | Legal risk if wrong | Implementation complexity |
|---|---|---|---|
| Fixed period | Partial — needs the actual number | Depends entirely on what number is chosen | Low |
| Outcome-tied period | Closer | Lower, if tuned to real risk profile | Medium |
| No automation yet | Does not satisfy §23 | Highest (no retention limit at all) | Lowest |

**What stays blocked without it:** any scheduled anonymization job, and any
UI copy that tells users their data will be retained for X period (§03's
"privacy by design, not bolted on" principle implies this should be
disclosed, which requires a real number).

**What can safely be built first:** the `Case` entity's audit log capture
(§22: "full audit log for every Case access") does not depend on the
retention number — it can be built to the append-only pattern this project
already uses (`RelevanceResult`, `HumanReviewDecision`), independent of when
retention eventually fires.

**Outcome:** confirmed — no default was given, and none should be invented when this is implemented. The mechanism (configuration seam, per-outcome policy support, Hangfire-ready enforcement point) is approved for construction now; the values that seam will hold are a separate, still-pending decision. The implementation must make it impossible to mistake "the framework exists" for "a retention policy has been approved."

---

## 5. k-anonymity threshold — ✅ APPROVED

**Approved decision:** k = 25, exactly the proposed default below, as a single centralized configuration value applied consistently at the aggregation/display boundary — never hard-coded per-query, never worked around by exposing individual-level data when an aggregate is suppressed, and easy to raise later if a future privacy review requires it. Not yet implemented — queued as step 5.

Original analysis kept below for record.

**What this was:** the real minimum group size below which aggregated
data (both Atmaen-internal aggregates and the public Diaspora Map, §09) is
suppressed from display. §23 gives "e.g. 20 users" only as an illustrative
example, not a set value.

**Why the architecture depends on it:** this is a single configuration value
(a `MinAggregationGroupSize` or similarly named setting) but it gates a real
security property — §13 explicitly requires this exact mechanism to prevent
scraping/enumeration ("no public page lists located users; all map data is
aggregated with a k-anonymity minimum — see §23"). The threshold itself
doesn't require new architecture to store, but every query that produces
aggregated output must apply it consistently, so the number should be fixed
before those queries are written, not patched in after the fact.

**Options:**
- **Adopt the spec's own example, 20** — simplest, spec-anchored, but "e.g."
  language suggests it wasn't meant as a final number.
- **A larger, more conservative threshold** (e.g. 50–100) — stronger privacy
  guarantee, especially relevant for smaller diaspora cities where a lower
  threshold could still make individuals identifiable by elimination, at the
  cost of more geographic areas having no visible aggregate data at all
  (less useful map coverage).
- **A threshold that scales with local population density** (smaller
  absolute number in a large city, larger in a small town) — best
  anti-enumeration property, but is meaningfully more implementation work
  and needs its own defined formula (another decision, not fewer).

**Trade-offs:**
| | Privacy strength | Map usefulness/coverage | Implementation effort |
|---|---|---|---|
| Fixed at 20 | Baseline (spec's own example) | Best coverage | Lowest |
| Fixed, higher (50–100) | Stronger | Reduced coverage in smaller diaspora hubs | Lowest (same mechanism, different constant) |
| Density-scaled | Strongest | Best of both, if tuned well | Highest — needs its own design |

**What stays blocked without it:** the Diaspora Map's aggregation queries and
any Atmaen-internal aggregate view (e.g. "how many active cases in this
region" if such a view is ever built) — both are explicitly required by §13
to apply this suppression, so neither can ship without the number.

**What can safely be built first:** the underlying per-city/region counts
that would feed an aggregation view can be computed and tested without
deciding the display threshold — the threshold is a display-time filter, not
a data-modeling decision. This is a narrow safe-to-build seam, not a reason
to start the map UI.

**Outcome:** approved as proposed (fixed at 25, not density-scaled) — simplicity was preferred over the stronger but more complex density-scaled option for now.

---

## 6. Source of official demographic data — ✅ APPROVED AS A MECHANISM; DATASET STILL PENDING

**Approved decision:** curated/estimated demographic data with mandatory, explicit provenance — a source citation and source/publication date on every value, UI disclosure that the data is curated/estimated and *not* official government data, kept structurally distinct from the platform's own aggregated signal, and upgradable to an official dataset later without a map redesign. No numbers may be invented where no source exists. Not yet implemented — queued as step 6.

Original analysis kept below for record.

**What this was:** what dataset provides "known official demographic
data" (§09's own phrase) for Egyptians abroad per country/region/city, in
what format, and how it is kept current — this is one of the Diaspora Map's
two required data sources (the other being the platform's own aggregated
user signal, which Slice 11 now makes collectible).

**Why the architecture depends on it:** §09 mandates the UI never present
official demographic data interchangeably with the platform's own aggregated
signal — meaning the data model needs a distinct, clearly-labeled source for
each, not just one aggregate number. Until a real source is chosen, there is
nothing to build a distinct field/table for; building a generic "demographic
data" table with fabricated numbers would violate this project's own
"schema only, no fabricated production data" rule already applied to
Country/City seed data at Slice 6 and Region/Neighborhood at Slice 10.

**Options:**
- **Egyptian government census/CAPMAS data** (Central Agency for Public
  Mobilization and Statistics) — most authoritative if a usable, current,
  machine-readable diaspora breakdown exists; needs verification that such a
  dataset is actually published and accessible in a usable format — not
  confirmed in this document.
- **Egyptian Ministry of Foreign Affairs / embassy consular registration
  data** — potentially more current for diaspora-specific counts (embassies
  track registered nationals), but access would likely require a formal data
  -sharing arrangement, not a public download.
  their own diaspora communities.
- **Manually curated, cited estimates** (e.g. compiled from published
  reports, updated periodically by an editorial team member) — most
  achievable without external partnerships, but weakest on the "official"
  framing §09 requires, and needs an update-cadence commitment or it goes
  stale.

**Trade-offs:**
| | Authoritativeness | Access feasibility | Currency/update burden |
|---|---|---|---|
| CAPMAS/government census | Highest, if accessible | Unconfirmed — needs verification | Low, if published periodically |
| Embassy/consular data | High | Likely needs formal partnership | Depends on partnership terms |
| Manually curated | Lowest of the three | Highest (no partnership needed) | Highest (needs an owner and a cadence) |

**What stays blocked without it:** the Diaspora Map cannot be built at all
without this — it's a named required input, not an optional enhancement, and
§09's "never present interchangeably" rule means the schema itself needs to
know which source each number came from.

**What can safely be built first:** nothing map-specific. The platform's own
aggregated-signal side of the map depends on decision 5 (k-anonymity
threshold) rather than this one, so that half could theoretically proceed
once #5 is resolved — but shipping only half of a two-source map without the
official-data half, when §09 requires both, would mean either an incomplete
feature or a scope decision (ship signal-only first?) that itself needs your
sign-off, not an engineering assumption.

**Outcome:** approved to proceed with the manually-curated route now (not blocked on a CAPMAS/embassy partnership), on the strict provenance terms above. Confirming whether a CAPMAS/embassy dataset is realistically obtainable remains open as a longer-term upgrade path, not a blocker for shipping the curated version.

---

## Summary table

| # | Decision | Fully blocks | Partially unblockable now | Proposed default given |
|---|---|---|---|---|
| 1 | OTP/SMS provider | Steps 5–8 of Atmaen | Steps 1–4 behind an interface seam | ✅ Approved: Twilio Verify (Slice 12, awaiting verification) |
| 2 | Field-level encryption & keys | `Case` entity itself | Nothing entity-specific | ✅ Approved: EF converters + Key Vault, envelope encryption |
| 3 | Trusted response network | Steps 7–8 | Steps 1–6 | ✅ Approved: CrisisEditor queue, assigned-only, temporary |
| 4 | Retention/anonymization | Final retention values only — mechanism is unblocked | Audit-log capture itself | ✅ Mechanism approved; values pending legal review |
| 5 | k-anonymity threshold | All aggregation display (Map + internal) | Underlying counts (not the display filter) | ✅ Approved: k = 25 |
| 6 | Official demographic data source | Diaspora Map entirely | Nothing map-specific | ✅ Curated-with-provenance approved; official dataset still an open upgrade path |

All 6 decisions above are approved (two — retention/anonymization and demographic data — as mechanisms, with final values/dataset still pending legal review / discovery, as marked). Implementation proceeds one slice at a time in the approved execution order; `docs/project-progress.html` is the live record of what has actually been built and verified so far (Slice 12 / OTP only, as of this update).
