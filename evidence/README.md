# Evidence

Sanitized captures from representative DemoBank runs. Nothing here is fabricated; folders keep original timestamps and filenames.

| Folder | Contents |
| --- | --- |
| `discovery/` | Walkthrough screenshots (`01-*.png` …). Timestamped live Bedrock folders are gitignored. |
| `replay-success/` | Successful balance replay walkthrough PNG/JSON |
| `replay-business-outcome/` | Member-not-found runs, plus simulated hard-failure capture |
| `replay-recoverable/` | Transient interruption (member 88888) dismissed then completed |
| `stability/` | Multi-run pass-rate report |
| `handoff/` | Sub-account confirm and operator HITL screenshots |

## Live discovery (`discover` without `--scripted`)

Genuine LLM discovery writes:

`evidence/discovery/<run-id>/`

Typical files:

- `discovery.jsonl` — one sanitized event per observation / model decision / action / checkpoint / extract
- `obs-00.txt`, `obs-01.txt`, … — redacted observations (page, visible text, semantic controls)
- `artifact.json` — draft capability recorded from successful actions
- `result.json` — discovery summary
- `failure.png` — only if the run failed

A genuine discovery run should be enough to trace:

observation → model decision → action → checkpoint/extract → generated artifact

`--scripted` does **not** produce this trail; it only writes the fixture artifact.

Successful live Bedrock discovery writes `evidence/discovery/<run-id>/` (gitignored) and overwrites `artifacts/lookup-savings-balance.json` as a **draft**. Replay of that draft does not need Bedrock. Timestamped folders are gitignored; `git add -f` a sanitized `evidence/discovery/<run-id>/` if you want the trace in git.

Replay/stability/HITL CLI runs still write timestamped directories under `evidence/` (for example `evidence/replay-…`). Those folders are gitignored.

Capability JSON used as **input** to replay remains at `/artifacts/lookup-savings-balance.json` until you replace it with a reviewed discovery draft.
