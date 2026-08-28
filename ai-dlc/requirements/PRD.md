# Product Requirements Document

> Relocated from the repository root to `ai-dlc/requirements/` for submission layout. Content is unchanged.

**Product:** Computer-Use Automation System  
**Company:** interface.ai — Engineering Team  
**Document type:** Take-home assignment, restated as a product requirements document  
**Format expected:** Design + working implementation + short write-up  
**Distribution:** Public GitHub repository  
**Time box:** No hard deadline; focused effort, not a polished product (see Scope)  
**Source:** Assignment — Computer-Use Automation System (original brief)

---

## 1. Document control

| Field | Value |
| --- | --- |
| Status | Draft converted from assignment brief |
| Audience | Candidate implementer and reviewers |
| Evaluation focus | Design and implementation of a computer-use system under realistic constraints |
| Success bar | Clear thinking, sound trade-offs, and a working core — not feature breadth |

A glossary is in [Section 14](#14-glossary). Difficulty is in the decisions, not the vocabulary.

---

## 2. Executive summary

interface.ai builds AI agents for banks and credit unions. This product is the **backend integration layer that gives those agents hands**: it lets an agent operate an institution’s back-office applications when no API exists.

Preferred integrations go through APIs. Those are **out of scope**. This system exists for the long tail of legacy apps (core banking screens, servicing tools, admin consoles) where the only path in is driving the UI as a human would.

**Product thesis**

1. An LLM (“computer use”) discovers how to accomplish a task the first time.
2. The successful run is turned into a **typed, versioned, parameterized capability artifact**.
3. Production invocation is **deterministic replay** with no model in the decision loop — reliable and cheap.

The agent-facing product decides **what** to do. This system is **how** it reliably and safely does it inside legacy bank software.

---

## 3. Problem statement

### 3.1 Who this is for

| Persona | Role |
| --- | --- |
| Calling AI agent | Invokes a saved capability with typed inputs and consumes typed outputs / outcomes |
| Discovery operator / engineer | Runs a natural-language goal against a target app to record a new capability |
| Human operator (HITL) | Takes control of a **live** session when automation is stuck or a risky step needs a person |
| Reviewer | Reads the artifact contract (what it does, needs, and returns) before it is reused |

### 3.2 Environment that shapes the product

The real applications are US bank and credit union **back-office** systems. Three properties drive requirements:

1. **Stable UIs, real runtime errors.** Enterprise UIs change slowly, so record-once / replay-many is viable. Replay must handle legitimate runtime states: validation errors, “record not found,” permission denials, unexpected confirmation dialogs, session/timeout expiry, transient slowness, and outright app errors. Happy-path-only capabilities are not useful in production.

2. **Heterogeneous, often legacy surfaces.** A given app may be a modern web app, a legacy web app (server-rendered, framesets, nested tables, non-semantic markup, no test IDs), or a native desktop app. Do not assume a clean DOM, stable selectors, or an API. Often the only reliable surface is what a human sees and does.

3. **Multi-tenant at scale.** Hundreds of tenants, ~20 apps each, thousands of app instances. Many tenants run the same vendor product, configured/branded/versioned differently. Automation that works for one tenant should generalize — or degrade gracefully — rather than being rebuilt per tenant.

The take-home asks for a **small but real** end-to-end version of this idea, designed with those realities in mind, implemented against **one** concrete surface.

The brief is **intentionally under-specified**. Where it does not dictate an answer, the implementer must decide and explain why.

---

## 4. Product goals and non-goals

### 4.1 Goals

Build a system that can:

1. Accept a **natural-language goal** for a target application (examples: look up member 12345 and read current savings balance; open a new sub-account and reach confirmation; or, on a public proxy, add an item to cart and reach checkout review).
2. Use an **LLM** to accomplish that goal on a **real** application surface: observe, decide, act. Browser is one case of a general computer-use problem (accessibility tree, screenshot + coordinates, OS-level automation, etc. are in play).
3. Record a successful run as a **structured, reusable artifact**: typed, versioned flow description (steps, how each control is identified, data to extract), **decoupled from the raw model transcript**.
4. **Replay** that artifact deterministically — no LLM in the decision loop — with stable targeting, and report success/failure.
5. **Escalate to a human** when stuck: route an intervention, let them take the **same live session**, then hand control back.
6. Stay inside **safety guardrails**: allowlist of permitted actions; do not leak or persist sensitive/regulated financial data.

**Through-line:** The model discovers. The artifact is a reusable capability. Deterministic replay is how the AI agent invokes it in production.

### 4.2 Non-goals (explicit)

| Out of scope | Notes |
| --- | --- |
| API-first integrations | Preferred in production; not this product |
| Full real-time co-browsing operator console | Minimal real handoff is required; UI may be mocked |
| Implementing multi-tenant or desktop support | Design must not paint into a corner; implementation against one surface |
| Feature breadth, framework name-dropping | Not rewarded |
| Scaling infrastructure (queues, clusters, multi-tenant plumbing) | Designing abstractions that *could* scale is valuable; building that infra is not |
| Access to a real bank system | Do not obtain one; use a proxy target |

---

## 5. User journeys

### 5.1 Happy path (must demonstrate end to end)

```
Goal
  -> LLM-driven discovery run completes the goal
  -> Saved capability artifact
  -> Deterministic replay (params, outputs, error/outcome handling)
  -> Human-escalation path can take over the live session
  -> Evidence for both discovery and replay
```

Text alternative: A goal is submitted; an LLM-driven run completes it; a capability artifact is saved; replay runs with input params, outputs, and error handling; a human can take over the live session; evidence exists for both runs.

### 5.2 Production invocation (conceptual)

Calling agent supplies typed parameters to a saved capability. Replay engine executes without LLM decisions, verifies checkpoint/success, returns outputs or a structured business outcome or failure.

### 5.3 Stuck / risky path

System detects it cannot safely proceed, raises an intervention with context, pauses automation, cedes the live session to a human, records human actions, resumes or completes, preserving evidence.

---

## 6. Functional requirements (must-have)

These are the evaluation requirements. How they are satisfied is the implementer’s choice.

### FR-1 — Goal-driven agent loop

| ID | Requirement |
| --- | --- |
| FR-1.1 | Accept a goal plus a target (app / URL / entry point). |
| FR-1.2 | Run an LLM-driven observe → decide → act loop on a live surface until the goal is met or a stop condition (max steps, timeout, dead-end). |
| FR-1.3 | The agent must actually interact with a real UI (click, type, navigate, read state). Mechanism is choosable (DOM, accessibility tree, screenshot + coordinates, OS automation, etc.). Bias toward approaches that still work when there is **no clean DOM**. |

### FR-2 — Structured artifact (agent-invocable capability)

After a successful run, emit a typed, serializable artifact. It is a **capability with a contract**, not only a step list. At minimum it must express:

- Ordered steps / actions
- How each target element/control is identified (with reasoning about robustness)
- Typed input parameters (e.g. member ID)
- Typed outputs / extracted data and their shape
- A checkpoint or success condition

The artifact must be **versioned and reviewable** so a human and a calling agent understand what it does, what it needs, and what it returns. Schema design is a **focal evaluation point**.

### FR-3 — Deterministic replay (production execution path)

| ID | Requirement |
| --- | --- |
| FR-3.1 | Given a saved artifact and input parameters, replay **without** invoking the LLM for decisions. |
| FR-3.2 | Use stable element/control targeting, verify checkpoint/success, return declared outputs. |
| FR-3.3 | Handle errors and exceptional states explicitly (validation, not-found, permission denial, unexpected dialog, session timeout, slow/failed load). Do not blindly proceed. |

**Result contract must distinguish:**

1. **Expected business outcomes** the caller needs (e.g. “no such member” is a legitimate result, not a crash)
2. **Recoverable conditions** (e.g. dismiss a known interstitial, wait/retry a transient load)
3. **Hard failures** that stop and surface a clear, debuggable error

Report a structured result: success with outputs, a known business outcome, or a failure with enough detail (what step, what was expected, what was observed).

### FR-4 — Safety and policy guardrails

| ID | Requirement |
| --- | --- |
| FR-4.1 | Enforce an explicit, configurable allowlist (e.g. permitted domains/routes and allowed action types). The agent must not act outside it. |
| FR-4.2 | Distinguish safe/reversible actions from risky/irreversible ones; handle the risky class conservatively (block, require confirmation, or flag — implementer’s call, must be justified). |
| FR-4.3 | Never persist secrets or raw sensitive data (credentials, tokens, full PII) into artifacts or logs. Redact appropriately. |

### FR-5 — Evidence / observability

Produce enough evidence to understand and debug a run: a structured log of what the agent did and why, plus at least one richer signal on failure (screenshot, DOM snapshot, trace, or equivalent — implementer’s choice).

### FR-6 — Human-in-the-loop escalation and handoff

When the agent is stuck during discovery, replay hits an unrecoverable condition, or a risky/irreversible step needs a person:

| ID | Requirement |
| --- | --- |
| FR-6.1 | Detect stuck/blocked state and raise an intervention request with context: capability/goal, current step, current state or screenshot, why it stopped. |
| FR-6.2 | Let the human operate the **same live session** (not a fresh one), perform manual steps, then hand control back so the run can resume or complete. Preserve context and evidence; record what the human did. |
| FR-6.3 | Automation must pause, cede control, and resume on the same session. There must be a way to know who is (or should be) in control. |

**Scope note:** Full operator console is out of scope. Minimal but real handoff — pause, expose live session for manual control (even a bare/mock operator surface), signal resume, capture human actions — plus a clear design for the rest. Mock the operator UI if needed; the **handoff mechanism and control-transfer model must be real**.

### FR-7 — Design for heterogeneity and scale (design, not necessarily build)

Implement against one concrete surface. Write-up must address:

- **Surface abstraction:** How artifact schema and replay would extend from the chosen surface (e.g. web) to legacy web and/or desktop. What is the seam between “how we perceive/act on a surface” and “the recorded flow”?
- **Multi-tenant reuse:** How to represent an artifact so it can be reused or safely specialized/overridden across tenants on the same app, rather than re-recorded per tenant. How to detect and manage per-tenant/version drift.

Do not implement multi-tenant or desktop support. Core abstractions must not paint into a corner.

---

## 7. Decisions left to the implementer

The following are **not prescribed**. Choose and defend in the write-up:

- Language, runtime, and frameworks
- LLM provider / model, prompting, and agent-loop structure
- Computer-use technology (Playwright, Puppeteer, Selenium, CUA/agent SDK, screenshot-based control, accessibility APIs, OS automation, etc.)
- Target application (stand-in only). Pick a proxy that exercises interesting problems: non-trivial multi-step flow (search → detail → action, or multi-field form + confirmation). Options: public demo/sandbox, local sample/mock app, intentionally hostile surface (iframes/framesets, table layouts, no test IDs), or a simple desktop app. If using a public site, respect terms and rate limits; never use real credentials or real PII.
- Artifact schema and storage/serialization
- How determinism is achieved on replay (locator strategy, fallbacks, waiting, etc.)
- Architecture and boundaries (single process vs. services, sync vs. queued). Simpler is fine if justified.

### 7.1 Not optional

The **discovery run must be real**: at least one genuine LLM-driven run against a live surface, with evidence in `/evidence/`. That is the heart of the project; a description of it cannot be assessed. Candidate must use their own model API access. A single successful run is not expensive.

Everywhere else, a clean seam is fine. Mock operator console, desktop surface, or anything Section 6 already allows — deliberately, documented. A well-designed seam is preferred over a stalled project.

---

## 8. Non-functional requirements

| Area | Requirement |
| --- | --- |
| Correctness | Agent completes a real goal; artifact replays deterministically and verifies success |
| Robustness | Replay detects and responds to runtime errors and exceptional states; separates business outcomes, recoverable conditions, and hard failures; sound locator, wait, and checkpoint strategy |
| Safety / data | Allowlist enforcement; conservative treatment of risky/irreversible actions; redaction of regulated financial data |
| Observability | Structured logs; richer failure signal |
| Quality | Readable, reasonably typed, tested where it counts, easy to run |
| Scope discipline | Thin-but-real version of **every** core requirement rather than a polished subset |

---

## 9. Scope and quality bar

AI-assisted development is assumed and encouraged. With modern tooling, scaffolding (agent loop, schemas, replay executor, guardrails, logging) comes together fast.

**Required:** a complete end-to-end **vertical slice** that touches **every** core requirement in Section 6 — not one or two of them.

**Judgment over throughput.** Focus: artifact schema, locator/control robustness, error taxonomy, control-transfer model, and how coherently the pieces fit. Be ready to defend every decision.

**Depth, not breadth**

- Go deep on artifact schema, deterministic replay + error handling, and safety/escalation.
- Cut **depth**, not whole capabilities. Minimal, stubbed at a clean seam, or mocked (operator UI, desktop surface) is fine if intentional, documented, and the seam/design are real.
- Say what was cut, why, and what would be built next.

---

## 10. Optional stretch goals

Only after a solid core. Pick **at most one or two**.

| Stretch | Description |
| --- | --- |
| Agent-facing capability interface | Catalog of callable capabilities (tool/function-calling or API) with typed args; show one invocation |
| Code generation | Emit a runnable test or automation snippet (page object, test file) from an artifact |
| Confidence and approval | Score replay reliability; gate unattended replay on draft → approved |
| Assisted fallback | On replay failure, bounded, policy-checked LLM recovery for a **single** step (never open-ended); record as evidence |
| Canonicalization / cross-tenant reuse | Parameterize routes (`/item/12345` → `/item/:id`) and/or apply one “base” artifact to a slightly different variant with per-variant overrides |
| Multi-run stability | Replay N times; report flakiness/stability |

---

## 11. Deliverables

Use these **exact paths and headings**.

### 11.1 Source code in a public git repository

`/README.md` must cover:

- How to set up and run (keys/config; how to run without live services if applicable)
- Demo path: exact command(s) to run the agent on a goal, then replay the resulting artifact

### 11.2 Design write-up

`/REPORT.md` (~1–3 pages), seven headings:

1. **Architecture** — architecture, key decisions, trade-offs
2. **Artifact schema** — schema and why it is shaped that way
3. **Determinism & error handling** — deterministic replay; runtime errors and exceptional states (and, secondarily, UI drift)
4. **Heterogeneity & multi-tenant** — extension to legacy web and desktop; reuse across institutions on the same app (see FR-7)
5. **Escalation & handoff** — detecting “stuck,” live-session takeover, handing control back
6. **Safety** — guardrail model and its limits
7. **Cuts** — what was left out and what would be built next

### 11.3 Evidence

`/evidence/` — saved example artifact plus logs from a discovery run and a replay run. Ideally include one replay that hits an error or exceptional state (bad input, not-found, or injected/simulated failure). Short screen recording is welcome but optional.

---

## 12. Success metrics (evaluation criteria)

Weighed roughly in this order:

1. **System design** — Clear boundaries, sensible data models, good trade-offs, appropriate simplicity. Artifact schema and replay contract are central.
2. **Correctness of the core loop** — Real goal completed; artifact replays deterministically and verifies success.
3. **Robustness and error handling** — Runtime/exceptional state handling; business vs recoverable vs hard failure; locator/wait/checkpoint strategy.
4. **Human-in-the-loop escalation** — Real mechanism: detect stuck, route with context, transfer live session, resume — not a TODO.
5. **Generalization to the real environment** — Credible design for heterogeneous surfaces and cross-tenant reuse without brittle per-tenant rebuilds.
6. **Safety and data handling** — Allowlist, risky actions, redaction.
7. **Code quality** — Readable, typed and tested where it counts, easy to run.
8. **Communication** — Write-up makes reasoning, trade-offs, and cut lines clear.

A small, correct, well-argued system is the goal.

---

## 13. Constraints and ground rules

- AI-assisted development is assumed and encouraged. Own everything submitted; be able to explain and defend any part in detail.
- Do not automate against sites if that would violate terms, harm the service, or require credentials that should not be used. Prefer sandboxes, demo sites, or a local app for anything sensitive.
- Keep secrets out of the repo.
- Time-box yourself. No deadline, but this should not consume a month. If stopping early, document next steps.

### Submission

Push to a public GitHub repo and email the link to **assignments@interface.ai**. Put the repo URL on its own line, use the address you applied with, and do not send a zip.

---

## 14. Glossary

| Term | Meaning |
| --- | --- |
| Computer use | An LLM operating a computer interface the way a person would (read screen/page, click, type) rather than calling an API |
| DOM | Browser’s structured page representation. A “clean DOM” has meaningful elements and stable identifiers; legacy apps often do not |
| Accessibility tree | Parallel representation for screen readers; often more stable than raw markup; available on desktop apps too |
| Locator / selector | How automation identifies a control; choice determines whether replay still works later |
| Test ID | Attribute added so automation can find an element; legacy enterprise apps almost never have them |
| Deterministic replay | Re-run recorded flow the same way every time, with no model deciding; same inputs, same steps, same outputs |
| Checkpoint | Assertion that the expected state was reached, rather than assuming a click worked |
| Business outcome vs. failure | “No such member” is a legitimate answer the caller needs, not a crash. Conflating the two is the most common design mistake |
| Tenant | One customer institution. Hundreds of them, many running the same vendor software configured differently |

---

## 15. Requirement traceability (assignment → PRD)

| Assignment section | PRD section |
| --- | --- |
| 1. Context | 2, 3 |
| 2. The problem | 4.1 |
| 3. Core requirements | 6 |
| 4. Explicitly your call | 7 |
| 5. Scope & expectations | 9 |
| 6. Deliverables | 11 |
| 7. Evaluation criteria | 12 |
| 8. Optional stretch goals | 10 |
| 9. Ground rules | 13 |
| 10. Glossary | 14 |
| 11. Submission | 13 |

---

## 16. Open decisions for implementation (not specified by the brief)

These remain implementer judgment (see Section 7). They are not blocking this PRD; they are the work of the design write-up.

- Target proxy application
- Computer-use observation/action stack
- Artifact schema details
- Replay locator and wait strategy
- HITL control-transfer implementation (minimal real seam)
- Allowlist and risk-class policy details
