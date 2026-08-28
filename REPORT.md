# REPORT

## 1. Architecture

Modular monolith (`ComputerUse.Cli` → Discovery, Replay, Policy, Artifacts, Evidence, HITL) plus a separate DemoBank app. `ISurfaceDriver` lives in Domain (no Playwright types); Playwright is the only implemented driver. Replay depends on that interface, not the adapter assembly. No queues. Operator UI is loopback Kestrel on the CLI process (`:5200`); DemoBank is `:5100`. Trade-off: one process is easy to demo; the driver interface keeps desktop/legacy web from leaking into the artifact schema.

## 2. Artifact schema

Versioned JSON: `id`, `schemaVersion`, `artifactVersion`, `approvalState` (`draft` | `approved`), typed `inputs`/`outputs`, `knownOutcomes` (code + `textContains` / `urlContains`), `recoverableConditions` (dismiss/wait + locators + `maxRetries`), ordered `steps` with `action`, locator **list** (fallback order), `risk`, checkpoints. Parameters use `{{memberId}}` / `{{baseUrl}}`. The schema is not a model transcript. Locators are data, not Playwright objects, so another driver can interpret the same steps. Live discovery writes `draft`; scripted and committed lookup artifacts are `approved`.

## 3. Determinism and error handling

Replay never calls the LLM. It substitutes params, enforces allowlist and approval, then for each step: optional HITL, recover known interstitials, act, classify. Classification order: policy → declared `knownOutcomes` → recoverable dismiss/wait → hard failure. Results: **Success**, **Recoverable** (completed after a declared recovery, outputs populated), **BusinessOutcome** (code from the artifact, e.g. `MEMBER_NOT_FOUND`), **HardFailure**, **PolicyFailure**, **InterventionRequired**. Locator resolution requires a unique match; a lower-tier win is recorded as `degradations[].kind = tier_degraded` with `matchedLocatorIndex`. CSS locators that also have `name` must contain that text. `--simulate-failure` injects a locator error. Drift is measurable, not a second model call.

## 4. Heterogeneity and multi-tenant

Surface-specific details stay in the driver. Artifacts store intent (click named control, type param, extract field). A desktop driver would map the same step types to AX/UI Automation. Tenants: parameterize IDs and `baseUrl`; stretch is a second branded DemoBank with locator overrides—not implemented in the core slice.

## 5. Escalation and handoff

Stuck/risk: max steps, timeout, no-progress, failed checkpoint, prohibited path (policy fail), RISKY/IRREVERSIBLE (HITL). Automation pauses, ownership becomes HUMAN, the **same** Playwright page stays open. The operator page shows reason, step, and screenshot (`GET /screenshot`). Resume is **HTTP 409** until at least one click/type is recorded on the live session (`humanActions` on the result). After resume, if the gated step's locators no longer resolve, the step is treated as human-completed and skipped.

## 6. Safety

Config allowlist (hosts, ports, paths, actions). Risk classes; RISKY/IRREVERSIBLE do not run unattended and require `approved` unless `--allow-draft`. Logs/artifacts redact deny-list tokens; only declared outputs persist. Loopback binds. Synthetic DemoBank data. Limits: no pixel redaction; local HTTP not TLS; AWS creds stay in the environment; operator page has no authentication (loopback only).

## 7. Cuts

No capability catalog, no tenant B, no desktop driver, no co-browse, no production DR, no operator auth. Next: catalog + parameterized cross-tenant overrides after the core demo is solid.
