# AI-Assisted Development Lifecycle

This directory contains supplementary engineering artifacts produced
during the AI-assisted development lifecycle for this project.

The implementation itself lives in `/src`, automated verification in
`/tests`, and reproducible runtime evidence in `/evidence`.

These artifacts capture the progression from:

requirements
→ design decisions
→ implementation planning
→ validation

AI assistance was used as an engineering accelerator. Architectural
decisions, implementation choices, and submitted code were reviewed and
remain the responsibility of the author.

| Path | What you will find |
| --- | --- |
| `requirements/` | Product brief (`PRD.md`), locked requirements, and clarification Q&A |
| `design/` | Pointers to the assignment-required design write-up (`/REPORT.md`) |
| `decisions/` | Locked product/architecture decisions from requirements analysis |

Formal per-unit implementation plans and validation packs were not produced as separate documents; `src/`, `tests/`, and `evidence/` are the implementation and validation record.

## Tooling vs reviewer-facing docs

This folder is **reviewer-facing**. The AI-DLC *tooling* still expects its own paths and is left in place so the workflow is not broken:

| Path | Why it stays at repo root |
| --- | --- |
| `.cursor/rules/` | Cursor rule that loads the workflow |
| `.aidlc-rule-details/` | Stage procedures the agent must load |
| `aidlc-rules/` | Packaged AWS AI-DLC rule copies |
| `aidlc-docs/` | State, audit log, and stage outputs the workflow writes |

Do not treat those directories as runtime architecture. They are process metadata.
