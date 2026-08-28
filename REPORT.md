# REPORT

## 1. Architecture

Modular monolith plus a separate DemoBank app. `ComputerUse.Cli` is the composition root (wires `ILanguageModel`, `IReplayEngine`, `ISurfaceDriver`, policy, artifacts, evidence, HITL). Discovery: the Playwright surface exposes a compact semantic observation (page URL/title, visible text, interactive controls with role/name/label/placeholder) rather than a raw DOM; the model chooses actions from that observation; a recorder keeps only successful actions and emits a **draft** `CapabilityArtifact`. Replay never calls the model. `ISurfaceDriver` lives in Domain with no Playwright types; Playwright is the only implemented web driver. Protocol and demo literals are centralized in Domain `Constants`. Unit tests stub the surface with `FakeSurfaceDriver` and the model with predetermined JSON; integration tests drive Chromium against DemoBank. No queues. Operator UI is loopback Kestrel on the CLI process (`:5200`); DemoBank is `:5100`. Trade-off: one process is easy to demo; the driver interface keeps desktop/legacy web from leaking into the artifact schema. Generic seams: driver, typed artifact, deterministic replay, locator alternatives, policy, result taxonomy, HITL ownership, evidence. Demo-specific: `memberId`/`baseUrl` parameterization, required `balance` extract, DemoBank known outcomes/recoveries, current CSS quality.

## 2. Artifact schema

Versioned JSON: `id`, `schemaVersion`, `artifactVersion`, `approvalState` (`draft` | `approved`), typed `inputs`/`outputs`, `knownOutcomes`, `recoverableConditions`, ordered `steps` with `action`, locator **list** (role/name, label, placeholder, text, then CSS fallback), `risk`, checkpoints. Parameters use `{{memberId}}` / `{{baseUrl}}` (supported names are validated; this slice does not infer arbitrary semantics). Dynamic business data (for example an extracted balance) must not become a reusable locator; those text locators are dropped in favor of a stable semantic/CSS alternative. The schema is not a model transcript. Locators are data, not Playwright objects. **Recorded from discovery:** flow, locators, parameters, extracts, checkpoints. **Environment/policy (not LLM-discovered):** DemoBank `MEMBER_NOT_FOUND` and `TRANSIENT_INTERRUPTION`. Live discovery writes `draft`. `--scripted` / `ScriptedLookup()` is a deterministic fixture for tests and offline demos, not proof of LLM discovery. No successful live Bedrock artifact is committed; replay uses the scripted fixture.

## 3. Determinism & error handling

Replay never calls the LLM. It substitutes params, enforces allowlist and approval, then for each step: optional explicit HITL, recover known interstitials, act, classify. Classification order: policy → declared `knownOutcomes` → recoverable dismiss/wait → hard failure. Results: **Success**, **Recoverable**, **BusinessOutcome**, **HardFailure**, **PolicyFailure**, **InterventionRequired**. Locator resolution requires a unique match; a lower-tier win is `degradations[].kind = tier_degraded`. `--simulate-failure` injects a locator error. A discovery artifact is emitted only after this DemoBank slice's required inputs, `balance` extract, and checkpoint are present; `finish` without them fails clearly. Drift is measurable, not a second model call.

## 4. Heterogeneity & multi-tenant

Surface-specific details stay in the driver. Artifacts store intent (click named control, type param, extract field). A desktop driver is not implemented; the seam is there so one could map the same step types later. Tenants: parameterize IDs and `baseUrl`; a second branded DemoBank is not in this slice.

## 5. Escalation & handoff

Stuck/risk: max steps, no-progress, failed checkpoint, prohibited path, RISKY/IRREVERSIBLE. Automation pauses on the **same** Playwright session; ownership becomes HUMAN. The operator page shows reason, step, screenshot, and captured session actions. The human must choose **Authorize automation**, **I completed the step**, or **Deny / stop**. An unrelated click/type is audit only. Authorize returns control to automation and executes the recorded step. Completed-by-human stays human-owned until replay verifies completion (target gone or following checkpoint already true); only then does automation resume and skip the risky click. Unverified completion stays `InterventionRequired`. Deny does not execute the risky action.

## 6. Safety

Config allowlist (hosts, ports, paths, actions) on discovery and replay. Model-supplied URLs/actions cannot bypass `PolicyEngine`. Risk classes are assigned by the application (model-supplied risk is ignored). RISKY/IRREVERSIBLE do not run unattended and require `approved` unless `--allow-draft`. Logs/evidence redact deny-list tokens; extracted customer values are not stored on the artifact. Loopback binds. Synthetic DemoBank data. Limits: no pixel redaction; local HTTP not TLS; AWS creds stay in the environment; operator page has no authentication (loopback only); completion verification is locator/checkpoint based, not a general workflow engine.

## 7. Cuts

No capability catalog, no tenant B, no desktop driver, no co-browse, no production DR, no operator auth. Semantic parameter/output requirements (`memberId`, `balance`) are tailored to the DemoBank lookup vertical slice; arbitrary natural-language goal-to-contract inference is not implemented. Desktop perception is represented by `ISurfaceDriver`, not implemented. Next: catalog + parameterized cross-tenant overrides after the core demo is solid.
