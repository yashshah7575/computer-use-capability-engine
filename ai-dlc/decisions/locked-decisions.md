# Locked decisions

Captured during requirements analysis. Source: [requirements.md](../requirements/requirements.md).

| Topic | Decision |
| --- | --- |
| Language | C# / .NET 8 |
| LLM | Amazon Bedrock, configuration-driven (`LLM_PROVIDER=bedrock`, `BEDROCK_MODEL_ID`, `AWS_REGION=us-east-1`) |
| Computer use | Playwright for .NET; accessibility/semantic observation primary; locator fallbacks; screenshots secondary |
| Target | Local DemoBank in-repo; somewhat hostile (no test IDs, table display, awkward markup, optional iframe, confirmation modal) |
| Primary flow | Look up member by ID, open record, extract current savings balance |
| Secondary flow | Search → open member → start opening sub-account → confirmation/risky step → HITL |
| Invocation | CLI (`discover`, `replay`, `hitl`) |
| Artifacts | JSON files on disk (`artifacts/`, copies under `evidence/`) |
| Risk | READ_ONLY, REVERSIBLE, RISKY, IRREVERSIBLE; RISKY/IRREVERSIBLE not unattended |
| HITL | Same headed Playwright session; operator page; AUTOMATION → HUMAN → Resume → AUTOMATION |
| Architecture | Modular monolith + separate DemoBank. No queues/microservices |
| Tests | Unit + scripted browser replay; live Bedrock gated off by default |
| Layout | Repo-root `README.md`, `REPORT.md`, `evidence/` |
| Browser | Headed default; `--headless` opt-in |
