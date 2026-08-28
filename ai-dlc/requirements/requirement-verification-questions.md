# Requirements Clarification Questions

Please answer every question by putting a letter after `[Answer]:`. If none of the options fit, choose **X** and describe your preference on the same line or the next line.

Do not leave any `[Answer]:` blank. Reply in chat when you are done.

---

## Question: Security Extensions

Should security extension rules be enforced for this project?

A) Yes — enforce all SECURITY rules as blocking constraints (recommended for production-grade applications)

B) No — skip all SECURITY rules (suitable for PoCs, prototypes, and experimental projects)

X) Other (please describe after [Answer]: tag below)

[Answer]:A

---

## Question: Resiliency Extensions

Should the resiliency baseline be applied to this project?

**What this extension is.** Enabling it applies directional, design-time best practices for building resilient systems (fault tolerance, observability, recoverability). It does **not** make the workload production-ready or certify availability/RTO/RPO.

A) Yes — apply the resiliency baseline as directional best practices and design-time guidance

B) No — skip the resiliency baseline (suitable for PoCs, prototypes, and experimental projects)

X) Other (please describe after [Answer]: tag below)

[Answer]:A

---

## Question: Property-Based Testing Extension

Should property-based testing (PBT) rules be enforced for this project?

A) Yes — enforce all PBT rules as blocking constraints (recommended for projects with business logic, data transformations, serialization, or stateful components)

B) Partial — enforce PBT rules only for pure functions and serialization round-trips

C) No — skip all PBT rules (suitable for simple CRUD applications, UI-only projects, or thin integration layers)

X) Other (please describe after [Answer]: tag below)

[Answer]: B

---

## Question 1

What is the primary **implementation language / runtime** for this take-home?

A) Python 3 (async CLI/library)

B) TypeScript / Node.js

C) Go

X) Other (please describe after [Answer]: tag below)

[Answer]: X — C# / .NET 8

---

## Question 2

Which **LLM provider and access** will discovery runs use? (A real LLM-driven run is required.)

A) OpenAI API (you will supply `OPENAI_API_KEY` locally; never commit it)

B) Anthropic API (you will supply `ANTHROPIC_API_KEY` locally; never commit it)

C) Another hosted API you will name after [Answer]: (Google, Azure OpenAI, etc.)

D) I do not have API access yet; pause implementation until I confirm a provider

X) Other (please describe after [Answer]: tag below)

[Answer]: Amazon Bedrock, Prefer configuration driven such as: LLM_PROVIDER=bedrock, BEDROCK_MODEL_ID=<configured model> ,AWS_REGION=us-east-1

---

## Question 3

Which **computer-use / observation-action stack** should the first vertical slice use? (The PRD biases toward something that still works without a clean DOM, but does not pick a stack.)

A) Browser automation with accessibility tree as the primary observation (e.g. Playwright accessibility snapshot), actions via the same browser session

B) Browser automation with DOM locators as primary (Playwright/Puppeteer/Selenium CSS/XPath)

C) Screenshot + coordinate clicking as primary (vision model)

D) OS-level / desktop automation as primary

X) Other (please describe after [Answer]: tag below)

[Answer]: X — Playwright for .NET with accessibility/semantic observation primary, locator fallbacks, and screenshots as secondary evidence/fallback

---

## Question 4

What **proxy target application** should discovery and replay run against? (Do not use a real bank system.)

A) A **local mock back-office app we build** in this repo (search → detail → action or form + confirmation; we can make it “hostile”: tables, no test IDs)

B) An **existing public demo/sandbox site** you will name after [Answer]: (must allow automation; no real credentials/PII)

C) An **intentionally hostile local page** only (iframes/framesets, table layout, no test IDs) without a fuller mock product

D) A **simple desktop app** you will name or we will add locally

X) Other (please describe after [Answer]: tag below)

[Answer]: A

Make it somewhat hostile/legacy-like:

no test IDs;
table-based account display;
awkward markup;
one iframe or nested section if useful;
runtime errors;
confirmation modal;
simulated latency.

Do not intentionally make every screen difficult just for complexity.

---

## Question 5

If the target is a local mock (or you chose a public site), what **demo business goal** should the end-to-end slice prove?

A) Look up a member/account by ID and extract a named field (e.g. savings balance)

B) Multi-step: search → open record → perform an action → reach a confirmation screen

C) Multi-field form submit → confirmation

D) E-commerce proxy: add a specific item to cart → checkout review

X) Other (please describe after [Answer]: tag below)

[Answer]: X

Primary required flow:
- Look up a member by ID, open the member record, and extract the current savings balance.

Secondary scenario for safety/HITL:
- Search member → open member → start opening a sub-account → reach a confirmation/risky step → require human intervention.

Clarification: The first flow proves discovery → artifact → deterministic replay → typed output. The second flow proves risk classification and human handoff. Do not make both equally large
---

## Question 6

How should operators **start discovery** and **start replay** for the required demo path?

A) CLI only (`discover` and `replay` commands documented in README)

B) HTTP API only (e.g. POST endpoints)

C) CLI as the demo path, plus a thin HTTP API for the same operations

X) Other (please describe after [Answer]: tag below)

[Answer]: A

---

## Question 7

How should **capability artifacts** be stored?

A) JSON files on disk under a repo path (e.g. `artifacts/` or `evidence/`)

B) YAML files on disk

C) SQLite (or similar) local database

X) Other (please describe after [Answer]: tag below)

[Answer]: A

---

## Question 8

For **risky / irreversible actions** (PRD FR-4.2: block, confirm, or flag — your call), what policy should this slice enforce?

A) **Block** by default; only allowlisted reversible actions run unattended

B) **Flag and pause** for human confirmation / HITL before the risky step

C) **Allow** but record a high-severity audit event (no pause)

X) Other (please describe after [Answer]: tag below)

[Answer]: B
Flag and pause for human confirmation.

Risk classification:

READ_ONLY
REVERSIBLE
RISKY
IRREVERSIBLE

RISKY and IRREVERSIBLE should not execute unattended unless explicitly approved by policy

---

## Question 9

What is the **minimum real HITL handoff** you want implemented (operator UI may still be mock/bare)?

A) Pause automation, print/write an intervention payload, operator uses the **same already-open browser/session** (headed) to act, then a CLI `resume` command; record operator actions if the tool can observe them

B) Pause automation, expose a **minimal local web page** that attaches to the live session (or shows screenshot + “I took over / resume”), then resume

C) Automated test-double: a script injects “human” actions on the same session after pause (to prove control transfer without a person sitting there)

X) Other (please describe after [Answer]: tag below)

[Answer]: X 
Keep the Playwright browser headed and alive. On intervention:

pause automation;
preserve the existing browser/context/session;
create an InterventionRequest;
expose a minimal local operator page showing reason, step, screenshot, and current controller;
switch ownership from AUTOMATION to HUMAN;
human operates the already-open browser directly;
human presses Resume;
capture a fresh observation;
ownership returns to AUTOMATION;
execution resumes.

This proves a real same-session handoff without building a full co-browsing platform.

---

## Question 10

When should the system treat a run as **stuck** and escalate (you may pick the closest match; describe extra conditions under X)?

A) Max steps, timeout, repeated identical observations, or policy block — whichever hits first

B) Only hard locator/checkpoint failure on replay, plus max steps/timeout on discovery

C) Only when a classified unrecoverable UI state is detected (dialog/error page), not on timeout alone

X) Other (please describe after [Answer]: tag below)

[Answer]: X
Escalate or stop on:

maximum steps;
overall timeout;
repeated identical observations / no-progress loop;
unknown UI state;
failed checkpoint after bounded retries;
unexpected dialog;
policy decision requiring human approval;
an action that cannot safely be determined.

Clarification: A permanently prohibited action should produce a policy failure rather than automatically escalating. A risky-but-approvable action should escalate to a human.
---

## Question 11

What **richer failure evidence** is required besides a structured log (PRD FR-5)?

A) Screenshot on failure

B) Accessibility tree or DOM snapshot on failure

C) Screenshot **and** accessibility/DOM snapshot on failure

X) Other (please describe after [Answer]: tag below)

[Answer]: C
Capture both:

screenshot;
accessibility/DOM/semantic snapshot.

Also retain structured step logs and Playwright trace where practical.
---

## Question 12

How should **secrets and PII redaction** work in artifacts and logs?

A) Deny-list: redact fields matching configured names/patterns (password, ssn, account_number, etc.) plus never write env API keys

B) Allow-list: only named output fields from the artifact contract may be persisted; everything else from the page is dropped

C) Both: allow-listed outputs plus deny-list redaction on logs/screenshots metadata (not pixel redaction unless you specify under X)

X) Other (please describe after [Answer]: tag below)

[Answer]: C

Clarification: The local demo uses synthetic data, so screenshots can be retained as assignment evidence. In a real financial environment, screenshots would need additional retention controls and visual/pixel redaction

---

## Question 13

What **allowlist** shape should guardrails use in this slice?

A) Config file: permitted origins/hosts, permitted path prefixes, permitted action types (click, type, navigate, read)

B) Hard-coded allowlist in code for the chosen proxy target only

C) Config file plus a per-run extra allowlist passed on the CLI

X) Other (please describe after [Answer]: tag below)

[Answer]: A

---

## Question 14

What **architecture** should the first slice use?

A) Single process / library + CLI (simplest; justified for take-home)

B) Two processes: agent/replay worker and a small API process

C) Multiple services with a queue between discovery and replay

X) Other (please describe after [Answer]: tag below)

[Answer]: X
Use a modular monolith for the automation system plus a separate local DemoBank target application.

No queues, microservices, distributed workers, or unnecessary infrastructure.

Logical architecture:
ComputerUse.Cli
      │
      ├── Discovery
      ├── Replay
      ├── Policy
      ├── Artifacts
      ├── Evidence
      ├── HITL
      │
      └── ISurfaceDriver
              │
              ▼
        Playwright Driver

DemoBank
  separate local ASP.NET Core app

This keeps deployment simple while preserving clean architectural boundaries.
---

## Question 15

Which **stretch goals** (at most one or two) should we include **after** the core vertical slice, if any?

A) None — core only

B) Agent-facing capability catalog (invoke by name with typed args) only

C) Canonicalization / parameterized routes and/or a second slightly different “tenant variant” with overrides

D) Confidence/approval (draft → approved) only

E) Bounded single-step LLM recovery on replay failure only

F) Two of: catalog **and** parameterized/cross-variant reuse

X) Other (please describe after [Answer]: tag below)

[Answer]: F
After the core is fully working:

Cross-tenant/canonicalized artifact reuse — higher priority.
Agent-facing capability catalog — second priority.

For example:
lookup-savings-balance
Inputs:
  memberId: string

Outputs:
  balance: decimal

And optionally demonstrate:
DemoBank Tenant A
DemoBank Tenant B

with slightly different branding/markup but the same base capability plus an override.

Clarification: Do not work on stretch goals until discovery, replay, failure handling, HITL, safety, evidence, README, and REPORT are complete.
---

## Question 16

What **test depth** do you want in-repo (assignment: tested where it counts)?

A) Unit tests for artifact schema, result taxonomy, allowlist, and replay error classification; few or no live UI tests in CI

B) Unit tests plus one scripted replay against the local mock (no live LLM in CI)

C) Unit tests, scripted replay, and an optional live LLM test gated by env var (off in default CI)

X) Other (please describe after [Answer]: tag below)

[Answer]: C
Include:

unit tests;
scripted deterministic browser replay test;
optional real Bedrock discovery test gated by environment variable.

Default CI must not require Bedrock credentials.
Example:
RUN_LIVE_LLM_TESTS=false

High-value unit-test areas:

artifact validation;
JSON round-trip;
parameter substitution;
result taxonomy;
policy decisions;
PII redaction;
locator fallback order;
retry classification;
checkpoint validation.
---

## Question 17

Where should **application code** live relative to this repo’s existing `PRD.md` and `aidlc-docs/`?

A) Workspace root as the Python/TS package (`src/`, `README.md`, `REPORT.md`, `evidence/` at repo root per assignment)

B) A subdirectory such as `app/` with `README.md` and `REPORT.md` still at repo root (assignment paths)

X) Other (please describe after [Answer]: tag below)

[Answer]: X
Keep assignment deliverables at the repository root and use a normal .NET solution structure.

/
├── README.md
├── REPORT.md
├── PRD.md
├── ComputerUse.sln
├── artifacts/
├── evidence/
├── aidlc-docs/
├── src/
│   ├── ComputerUse.Domain/
│   ├── ComputerUse.Agent/
│   ├── ComputerUse.Replay/
│   ├── ComputerUse.Surfaces.Playwright/
│   ├── ComputerUse.Handoff/
│   ├── ComputerUse.Cli/
│   └── DemoBank/
└── tests/

Do not place the implementation under an unnecessary app/ wrapper.
---

## Question 18

Do you want a **headed** browser for the recorded demo (human-visible) or **headless** by default?

A) Headed by default for discover/replay; headless opt-in via flag

B) Headless by default; headed opt-in via flag

X) Other (please describe after [Answer]: tag below)

[Answer]: A
Headed by default for the take-home demo.

Support:
--headless

as an optional flag.

This makes discovery and human handoff visible to reviewers.
---

## Question 19

For the **replay error-path evidence** (assignment: ideally one replay that hits not-found / bad input / simulated failure), which scenario must we implement?

A) Replay with a member/ID that does not exist → structured **business outcome**, not a crash

B) Injected/simulated load or locator failure → structured **hard failure** with step/expected/observed

C) Both A and B

X) Other (please describe after [Answer]: tag below)

[Answer]: C

---

## Question 20

Who is the **primary reviewer** this implementation should optimize for?

A) interface.ai engineering take-home (public GitHub, REPORT.md, evidence/) — optimize for their evaluation criteria in the PRD

B) Internal learning / portfolio only — same deliverables but you will not submit to interface.ai

X) Other (please describe after [Answer]: tag below)

[Answer]: A

Optimize specifically for interface.ai engineering reviewers and their stated evaluation criteria.

The priority should be:
1. Artifact/schema design
2. Deterministic replay
3. Error taxonomy
4. HITL control transfer
5. Safety
6. Surface abstraction
7. Multi-tenant design
8. Code polish