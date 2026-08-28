# Evidence

Sanitized captures from representative DemoBank runs. Nothing here is fabricated; folders keep original timestamps and filenames.

| Folder | Contents |
| --- | --- |
| `discovery/` | DemoBank surface screenshots (home, search results, member record) |
| `replay-success/` | Successful balance replay (`result.json` runs + walkthrough PNG/JSON) |
| `replay-business-outcome/` | Member-not-found runs, plus simulated hard-failure capture |
| `handoff/` | Sub-account confirm and operator HITL screenshots |

New CLI executions still write timestamped directories directly under `evidence/` (for example `evidence/replay-…`, `evidence/discovery-…`). `evidence/runs/` is gitignored for local clutter.

Capability JSON used as **input** to replay remains at `/artifacts/lookup-savings-balance.json`.
