# REPORT

## 1. Architecture

Modular monolith (`ComputerUse.Cli` → Discovery, Replay, Policy, Artifacts, Evidence, HITL) plus a separate DemoBank app. `ISurfaceDriver` is the perceive/act seam; Playwright is the only implemented driver. No queues. Operator UI is loopback Kestrel on the CLI process (`:5200`); DemoBank is `:5100`. Trade-off: one process is easy to demo; the driver interface keeps desktop/legacy web from leaking into the artifact schema.

## 2. Artifact schema

Versioned JSON: `id`, `schemaVersion`, `artifactVersion`, typed `inputs`/`outputs`, ordered `steps` with `action`, locator **list** (fallback order), `risk`, checkpoints. Parameters use `{{memberId}}` / `{{baseUrl}}`. The schema is not a model transcript. Locators are data, not Playwright objects, so another driver can interpret the same steps.

## 3. Determinism and error handling

Replay never calls the LLM. It substitutes params, enforces allowlist, runs locators in order with waits, then checkpoints. Results: **Success** (allow-listed outputs), **BusinessOutcome** (e.g. record not found), **HardFailure** (step / expected / observed + screenshot/snapshot), **PolicyFailure**, **InterventionRequired**. `--simulate-failure` injects a locator error. Secondary: UI drift is handled by locator fallbacks, not by the model.

## 4. Heterogeneity and multi-tenant

Surface-specific details stay in the driver. Artifacts store intent (click named control, type param, extract field). A desktop driver would map the same step types to AX/UI Automation. Tenants: parameterize IDs and `baseUrl`; stretch is a second branded DemoBank with locator overrides—not implemented in the core slice.

## 5. Escalation and handoff

Stuck/risk: max steps, timeout, no-progress, failed checkpoint, prohibited path (policy fail), RISKY/IRREVERSIBLE (HITL). Automation pauses, ownership becomes HUMAN, the **same** Playwright page stays open, operator page shows reason/step/screenshot, Resume returns AUTOMATION and replay continues.

## 6. Safety

Config allowlist (hosts, ports, paths, actions). Risk classes; RISKY/IRREVERSIBLE do not run unattended. Logs/artifacts redact deny-list tokens; only declared outputs persist. Loopback binds. Synthetic DemoBank data. Limits: no pixel redaction; local HTTP not TLS; AWS creds stay in the environment.

## 7. Cuts

No capability catalog, no tenant B, no desktop driver, no co-browse, no production DR. Next: catalog + parameterized cross-tenant overrides after the core demo is solid.
