# Evidence

Sanitized captures from representative DemoBank runs. Nothing here is fabricated; folders keep original timestamps and filenames.

| Folder | Contents |
| --- | --- |
| `discovery/` | Walkthrough screenshots of the DemoBank surface (not a live Bedrock transcript) |
| `replay-success/` | Successful balance replay (`result.json` runs + walkthrough PNG/JSON) |
| `replay-business-outcome/` | Member-not-found runs, plus simulated hard-failure capture |
| `replay-recoverable/` | Transient interruption (member 88888) dismissed then completed |
| `stability/` | Multi-run pass-rate report |
| `handoff/` | Sub-account confirm and operator HITL screenshots |

## Live discovery (`discover` without `--scripted`)

Genuine LLM discovery writes:

`evidence/discovery/<run-id>/`

Typical files:

- `discovery.jsonl` — one sanitized event per observation / model decision / action
- `obs-00.txt`, `obs-01.txt`, … — redacted surface observations
- `artifact.json` — draft capability recorded from successful actions
- `result.json` — discovery summary
- `failure.png` — only if the run failed

`--scripted` does **not** produce this trail; it only writes the fixture artifact.

This repository does **not** currently commit a genuine Bedrock discovery directory. After you run live `discover` locally, inspect `evidence/discovery/<run-id>/`, confirm it is sanitized (no credentials, tokens, or unrestricted transcripts), then commit **that** directory if you want reviewers to see a real discovery trace.

Replay/stability/HITL CLI runs still write timestamped directories under `evidence/` (for example `evidence/replay-…`). `evidence/runs/` is gitignored for local clutter.

Capability JSON used as **input** to replay remains at `/artifacts/lookup-savings-balance.json` until you replace it with a reviewed discovery draft.
