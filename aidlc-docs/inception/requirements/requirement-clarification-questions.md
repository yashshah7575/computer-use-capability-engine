# Requirements Clarification Questions (Resiliency decisions)

Your previous answers are recorded and internally consistent (C#/.NET 8, Bedrock, Playwright a11y-first, local DemoBank, CLI, HITL operator page, etc.).

Security, resiliency, and partial PBT extensions are **enabled**. The resiliency baseline requires these decisions **before** `requirements.md` can be finalized. I will **not** invent RTO/RPO, CI/CD, or DR.

Please fill every `[Answer]:`. Reply in chat when done.

---

## Question: RTO/RPO Goals and Disaster Recovery Strategy

What are your Recovery Time Objective (RTO) and Recovery Point Objective (RPO) goals? These determine DR strategy if any.

A) RPO/RTO: Hours — Backup and Restore. Lowest cost. Redeploy and restore from backups on failure.

B) RPO/RTO: tens of minutes — Pilot Light. Data live, services idle until failover.

C) RPO/RTO: Minutes — Warm Standby. Reduced-capacity standby scaled up on failover.

D) RPO/RTO: Near real-time — Multi-site Active/Active.

E) N/A — This take-home is **local-only** (laptop DemoBank + CLI). No production SLA, no cross-region DR. Document that explicitly; do not design AWS multi-region failover.

X) Other (please describe after [Answer]: tag below)

[Answer]: E

---

## Question: Change Management Process

How should production changes for this workload be governed?

A) Use an existing organizational change management process — name the tool after [Answer]: (e.g., ServiceNow, Jira Change).

B) No formal process exists yet — propose a lightweight process (change record + approval + rollback note).

C) N/A — take-home / local demo is exempt from formal production change management. Document the exemption.

X) Other (please describe after [Answer]: tag below)

[Answer]: C

---

## Question: CI/CD and Deployment Tooling

How should builds and tests run?

A) Use an existing org pipeline — name it after [Answer]: (GitHub Actions, Azure DevOps, etc.).

B) Add a **minimal GitHub Actions** workflow in this repo: restore, build, unit tests, scripted replay; live Bedrock tests off by default.

C) No CI in-repo — document local `dotnet test` only.

X) Other (please describe after [Answer]: tag below)

[Answer]: B

---

## Question: Rollback Mechanism

If a “release” of this take-home needed rollback, what is acceptable?

A) Git revert / previous commit of the repo (no production deploy).

B) Versioned capability artifacts on disk; pin replay to a previous artifact version.

C) N/A — no deployed production service; rollback is git history only.

X) Other (please describe after [Answer]: tag below)

[Answer]: X — Use both Git rollback for application code and versioned capability rollback for automation artifacts.

---

## Question: Deployment Style

How will reviewers run the system?

A) Clone, `dotnet run` locally (DemoBank + CLI). No cloud deploy.

B) Docker Compose locally (optional containers) plus `dotnet run` documented as primary.

C) Deploy to a cloud environment you will name after [Answer]:.

X) Other (please describe after [Answer]: tag below)

[Answer]: B
Primary documented path should remain simple local .NET execution:

dotnet run --project src/DemoBank
dotnet run --project src/ComputerUse.Cli -- discover ...
dotnet run --project src/ComputerUse.Cli -- replay ...

---

## Question: Regional Topology

A) Single-region cloud multi-AZ — you will name the provider/region after [Answer]:.

B) Multi-region active-passive.

C) Multi-region active-active.

D) N/A — no cloud topology; local machine only.

X) Other (please describe after [Answer]: tag below)

[Answer]: D

---

## Question: Incident Response Process

A) Use an existing org incident process — name it after [Answer]:.

B) Propose a lightweight incident note (who, what, evidence path, resume/abort).

C) N/A — take-home; incidents are local failures handled via logs/evidence and HITL, no on-call process.

X) Other (please describe after [Answer]: tag below)

[Answer]: B
For meaningful execution failures/interventions capture:
Run ID
Timestamp
Capability / goal
Execution mode: discovery | replay
Current step
Classification
Expected state
Observed state
Controller: automation | human
Evidence paths
Action: retry | resume | abort
Final outcome

For example:
{
  "runId": "run-20260827-001",
  "capability": "lookup-savings-balance",
  "step": "search-member",
  "classification": "checkpoint_failure",
  "controller": "automation",
  "expected": "Member Details",
  "observed": "Application Error",
  "evidence": {
    "screenshot": "evidence/run-20260827-001/failure.png"
  },
  "resolution": "abort"
}
Clarification: this is execution incident evidence, not a fabricated 24/7 incident-management/on-call process.
---

## Question: DemoBank operator page binding

The HITL operator page is a **minimal local web page**. DemoBank is a **separate ASP.NET Core app**. How should the operator page be hosted?

A) Serve the operator page from the **ComputerUse.Cli** process (embedded Kestrel or similar) on a loopback port, separate from DemoBank.

B) Serve the operator page from **DemoBank** (same host as the mock bank UI).

C) Static HTML file opened locally; Cli watches a resume signal file.

X) Other (please describe after [Answer]: tag below)

[Answer]: A
Serve the HITL operator page from the ComputerUse automation process, using a minimal embedded ASP.NET Core/Kestrel host bound only to loopback, for example:

http://127.0.0.1:5200

Keep it separate from:

DemoBank
http://127.0.0.1:5100

Architecture:
              Automation System
        ┌──────────────────────────┐
        │ Discovery / Replay       │
        │                          │
        │ Session Controller       │
        │                          │
        │ HITL Operator Page       │
        │ localhost:5200           │
        └────────────┬─────────────┘
                     │
               Playwright
                     │
                     ▼
        ┌──────────────────────────┐
        │ DemoBank                 │
        │ localhost:5100           │
        └──────────────────────────┘
