# Requirements Document

## Intent analysis

| Field | Value |
| --- | --- |
| User request | Execute `PRD.md` with AI-DLC; ask clarifying questions; do not assume |
| Request type | New project (greenfield) |
| Scope estimate | Multiple components (CLI, discovery, replay, policy, HITL, Playwright driver, DemoBank, tests, evidence) |
| Complexity estimate | Complex |
| Requirements depth | Comprehensive |
| Primary reviewer | interface.ai engineering take-home |

---

## Extension configuration

| Extension | Enabled | Decided at |
| --- | --- | --- |
| Security Baseline | Yes | Requirements Analysis |
| Resiliency Baseline | Yes | Requirements Analysis |
| Property-Based Testing | Partial (PBT-02, PBT-03, PBT-07, PBT-08, PBT-09 blocking; other PBT rules advisory) | Requirements Analysis |

---

## Locked decisions (from user answers)

| Topic | Decision |
| --- | --- |
| Language | C# / .NET 8 |
| LLM | Amazon Bedrock, configuration-driven (`LLM_PROVIDER=bedrock`, `BEDROCK_MODEL_ID`, `AWS_REGION=us-east-1`) |
| Computer use | Playwright for .NET; accessibility/semantic observation primary; locator fallbacks; screenshots secondary |
| Target | Local DemoBank in-repo; somewhat hostile (no test IDs, table display, awkward markup, optional iframe, runtime errors, confirmation modal, simulated latency); not every screen made hard |
| Primary flow | Look up member by ID, open record, extract current savings balance |
| Secondary flow (smaller) | Search → open member → start opening sub-account → confirmation/risky step → HITL |
| Invocation | CLI only (`discover`, `replay`, `resume`) |
| Artifacts | JSON files on disk (`artifacts/`, copies under `evidence/`) |
| Risk | Flag and pause; classes READ_ONLY, REVERSIBLE, RISKY, IRREVERSIBLE; RISKY/IRREVERSIBLE not unattended unless policy explicitly approved |
| HITL | Keep headed Playwright session; pause; `InterventionRequest`; operator page; AUTOMATION → HUMAN → Resume → AUTOMATION |
| Stuck | Max steps, timeout, no-progress loop, unknown UI, failed checkpoint after bounded retries, unexpected dialog, risky action needing approval, action that cannot be determined safely. Permanently prohibited action = policy failure, not auto-escalate |
| Evidence | Screenshot + accessibility/DOM/semantic snapshot; structured step logs; Playwright trace where practical |
| Redaction | Allow-listed outputs plus deny-list on logs/metadata. Synthetic demo data: screenshots allowed as evidence. Production financial would need extra visual/retention controls |
| Allowlist | Config file: origins/hosts, path prefixes, action types |
| Architecture | Modular monolith + separate DemoBank. No queues/microservices |
| Stretch | After core only: (1) cross-tenant/canonicalized reuse, (2) agent-facing catalog |
| Tests | Unit + scripted browser replay; optional live Bedrock gated `RUN_LIVE_LLM_TESTS=false` by default |
| Layout | Repo-root `README.md`, `REPORT.md`, `evidence/`; `ComputerUse.sln`; `src/` projects as specified |
| Browser | Headed default; `--headless` opt-in |
| Replay evidence | Both not-found business outcome and injected/simulated hard failure |
| RTO/RPO | N/A local-only; no production SLA; no multi-region DR |
| Change management | N/A take-home exemption |
| CI | Minimal GitHub Actions: restore, build, unit tests, scripted replay; live LLM off |
| Rollback | Git rollback for code **and** versioned capability artifact rollback |
| Run path | Documented primary: local `dotnet run`. Docker Compose optional, not required |
| Topology | Local machine only |
| Incidents | Lightweight **execution incident** JSON (not 24/7 on-call) |
| Operator host | ComputerUse process, Kestrel loopback e.g. `http://127.0.0.1:5200`; DemoBank `http://127.0.0.1:5100` |

---

## Product thesis

The model discovers. The JSON artifact is a versioned, parameterized capability. Deterministic replay (no LLM in the decision loop) is production invocation. Human takes the **same live browser session** when stuck or when policy requires it.

---

## Functional requirements

### FR-1 Goal-driven discovery

- FR-1.1 CLI `discover` accepts natural-language goal + target (DemoBank origin/entry).
- FR-1.2 LLM-driven observe → decide → act via Bedrock until goal met or stop condition.
- FR-1.3 Real UI interaction through `ISurfaceDriver` (Playwright). Observation prefers accessibility/semantic tree.
- FR-1.4 At least one genuine Bedrock discovery run against live DemoBank; evidence under `/evidence/`.

### FR-2 Capability artifact

After successful discovery, emit typed JSON decoupled from the raw model transcript, including:

- Ordered steps/actions
- Control identity (locator strategy + robustness notes)
- Typed inputs (e.g. `memberId: string`)
- Typed outputs and shape (e.g. `balance: decimal`)
- Checkpoint / success condition
- Schema version
- Risk classification metadata per step where applicable

Artifact must be reviewable by a human and a calling agent.

### FR-3 Deterministic replay

- FR-3.1 CLI `replay` takes saved artifact + input parameters; **no LLM decisions**.
- FR-3.2 Stable targeting with documented fallback order; verify checkpoint; return declared outputs.
- FR-3.3 Result contract:
  - `Success` with outputs
  - `BusinessOutcome` (e.g. member not found) — not a crash
  - `Recoverable` (retry/wait/dismiss known interstitial)
  - `HardFailure` (stop; step, expected, observed, evidence paths)
  - `PolicyFailure` (permanently prohibited)
  - `InterventionRequired` (HITL)

Must demonstrate: (a) unknown member → business outcome; (b) simulated/injected failure → hard failure with evidence.

### FR-4 Safety and policy

- FR-4.1 Configurable allowlist: hosts, path prefixes, action types. Agent must not act outside it.
- FR-4.2 Risk classes: READ_ONLY, REVERSIBLE, RISKY, IRREVERSIBLE. RISKY/IRREVERSIBLE pause for human unless policy explicitly allows.
- FR-4.3 Never persist secrets, AWS credentials, tokens, or raw PII into artifacts or logs. Demo uses synthetic data.

### FR-5 Evidence and observability

- Structured logs: timestamp, correlation/run ID, level, message; no secrets/PII (deny-list + allow-listed outputs).
- On failure: screenshot + accessibility/DOM/semantic snapshot.
- Playwright trace where practical.
- Execution incident record on meaningful failures/interventions (fields: runId, timestamp, capability/goal, mode, step, classification, expected, observed, controller, evidence paths, action retry/resume/abort, final outcome).

### FR-6 HITL

- Detect stuck per locked list; raise `InterventionRequest`.
- Pause automation; preserve Playwright browser/context/session.
- Operator page on ComputerUse loopback (e.g. port 5200): reason, step, screenshot, current controller.
- Ownership AUTOMATION | HUMAN; human uses **already-open** headed browser; Resume captures fresh observation and returns control to automation.
- Record what the human did to the extent the driver can observe.
- Permanently prohibited → policy failure, not HITL.

### FR-7 DemoBank

Separate ASP.NET Core app (e.g. port 5100) implementing:

- Member lookup by ID, member detail with savings balance (table-based, no test IDs)
- Secondary smaller flow: open sub-account reaching confirmation/risky step
- Runtime errors, confirmation modal, simulated latency
- One iframe or nested section if useful
- Synthetic members including a known not-found ID

### FR-8 Surface abstraction (implement web; design for more)

`ISurfaceDriver` is the seam between perceive/act and the recorded flow. Write-up must explain extension to legacy web and desktop without implementing those drivers.

### FR-9 Multi-tenant (design now; implement only as stretch after core)

Core artifacts should be parameterizable (e.g. `memberId`) so they are not hard-coded to one member. Stretch after core: Tenant A vs Tenant B variants + catalog `lookup-savings-balance`.

### FR-10 CLI and packaging

Commands documented in root `README.md`:

- Start DemoBank
- `discover`
- `replay`
- `resume` (HITL)
- `--headless`
- How to run without Bedrock (replay/tests)
- Config for Bedrock (env vars; never commit secrets)

Root `REPORT.md` with the seven assignment headings.

### FR-11 Stretch (explicitly after core)

Do not start until discovery, replay, failure handling, HITL, safety, evidence, README, and REPORT are complete.

1. Canonicalization / DemoBank Tenant A and B
2. Agent-facing catalog invoke-by-name with typed args

---

## Non-functional requirements

| ID | Requirement |
| --- | --- |
| NFR-1 | Correctness: real discovery goal; replay verifies checkpoint |
| NFR-2 | Robustness: locator wait/fallback; bounded retries; taxonomy as FR-3.3 |
| NFR-3 | Safety: allowlist; fail closed on policy/unknown; redaction |
| NFR-4 | Observability: FR-5 |
| NFR-5 | Quality: typed C#; tests per Q16; easy `dotnet` run |
| NFR-6 | Local-only: bind DemoBank and operator page to loopback |
| NFR-7 | CI: GitHub Actions build + unit + scripted replay; `RUN_LIVE_LLM_TESTS=false` default |
| NFR-8 | Rollback: git for code; version field on artifacts for capability rollback |
| NFR-9 | Optional Docker Compose; primary docs remain `dotnet run` |
| NFR-10 | PBT partial: JSON artifact round-trip; policy/redaction/result invariants with generated inputs (FsCheck or equivalent) |
| NFR-11 | Supply chain: lock files (`packages.lock.json` or Directory.Packages.props + restore lock); pin GitHub Actions versions |
| NFR-12 | HTTP security headers on HTML endpoints (DemoBank and operator page) per SECURITY-04 |
| NFR-13 | Input validation on CLI args, JSON artifacts, and operator-page posts |
| NFR-14 | Fail closed: errors do not bypass policy; `using`/dispose Playwright; generic user-facing errors |
| NFR-15 | No production availability SLA; no multi-region DR (RESILIENCY-02 E) |

---

## Logical architecture (text)

```
ComputerUse.Cli
  -> Discovery, Replay, Policy, Artifacts, Evidence, HITL
  -> ISurfaceDriver
       -> Playwright driver
HITL operator page: 127.0.0.1:5200 (same process as Cli)
Playwright drives DemoBank: 127.0.0.1:5100
```

ASCII (equal-width lines):

```
+------------------------------+
| ComputerUse.Cli              |
| Discovery / Replay / Policy  |
| HITL page 127.0.0.1:5200     |
+--------------+---------------+
               |
               v
+--------------+---------------+
| Playwright ISurfaceDriver    |
+--------------+---------------+
               |
               v
+--------------+---------------+
| DemoBank 127.0.0.1:5100      |
+------------------------------+
```

---

## Proposed solution layout (user-specified)

```
/
README.md, REPORT.md, PRD.md, ComputerUse.sln
artifacts/, evidence/, aidlc-docs/
src/ComputerUse.Domain/
src/ComputerUse.Agent/
src/ComputerUse.Replay/
src/ComputerUse.Surfaces.Playwright/
src/ComputerUse.Handoff/
src/ComputerUse.Cli/
src/DemoBank/
tests/
```

---

## Evaluation priority (reviewer)

1. Artifact/schema design  
2. Deterministic replay  
3. Error taxonomy  
4. HITL control transfer  
5. Safety  
6. Surface abstraction  
7. Multi-tenant design (write-up; stretch later)  
8. Code polish  

---

## Out of scope

- Real bank systems, real PII, committed secrets
- API-first core-banking integration
- Full co-browsing operator console
- Implementing desktop driver or production multi-tenant infra
- Queues, clusters, microservices
- Production SLA / multi-region DR
- Stretch work before core deliverables

---

## Extension compliance (this stage)

### Security

| Rule | Status | Rationale |
| --- | --- | --- |
| SECURITY-01 | N/A | No cloud data store; JSON on local disk; synthetic data only; secrets never persisted |
| SECURITY-02 | N/A | No load balancer, API gateway, or CDN |
| SECURITY-03 | Compliant in requirements | Structured logs + redaction required (FR-5, NFR-4) |
| SECURITY-04 | Compliant in requirements | Headers required on DemoBank and operator HTML (NFR-12) |
| SECURITY-05 | Compliant in requirements | Validate CLI, artifacts, operator posts (NFR-13) |
| SECURITY-06 | N/A | No IAM policies shipped; Bedrock uses caller’s AWS credentials outside repo |
| SECURITY-07 | Compliant in requirements | Loopback bind only (NFR-6) |
| SECURITY-08 | N/A | No multi-user auth; local loopback demo only |
| SECURITY-09 | Compliant in requirements | No default creds; generic errors (NFR-14) |
| SECURITY-10 | Compliant in requirements | Lock files + pinned Actions (NFR-11) |
| SECURITY-11 | Compliant in requirements | Isolated Policy module; misuse: off-allowlist, irreversible without HITL |
| SECURITY-12 | N/A | No end-user password auth; Bedrock/AWS creds via env/user secrets only |
| SECURITY-13 | Compliant in requirements | Schema-validate artifact JSON; no unsafe typed deserialization of untrusted types |
| SECURITY-14 | N/A (cloud 90-day) | Local incident JSON + evidence; not a SIEM |
| SECURITY-15 | Compliant in requirements | Fail closed; dispose sessions (NFR-14) |

### Resiliency

| Rule | Status | Rationale |
| --- | --- | --- |
| RESILIENCY-01 | Compliant | Critical: Replay + Policy + DemoBank for demo; Medium: Discovery (LLM); Low: optional Docker |
| RESILIENCY-02 | N/A | User E: no SLA/DR |
| RESILIENCY-03 | N/A | Take-home change-management exemption |
| RESILIENCY-04 | Compliant | GitHub Actions + git/artifact rollback; local deploy style |
| RESILIENCY-08 | N/A | Local topology |
| Others | Deferred | Apply in NFR/design/code where they concern retries, timeouts, checkpoints already in FR-3 |

### PBT (partial)

| Rule | Status | Rationale |
| --- | --- | --- |
| PBT-02, PBT-03, PBT-07, PBT-08, PBT-09 | Required at code/test | Artifact JSON round-trip; invariants on policy/redaction/taxonomy; generators for those types |
| Other PBT rules | Advisory | Partial mode |

---

## Traceability

PRD FR-1..FR-7 and assignment §3 map to FR-1..FR-10 here. User Q1–Q20 and resiliency clarification answers are recorded in `requirement-verification-questions.md` and `requirement-clarification-questions.md`.
