# Manual testing runbook

Walk through this in order. Use **two terminals**. Keep DemoBank running in Terminal 1. Run CLI commands in Terminal 2.

Do **not** treat live Bedrock discovery as required. Replay, outcomes, and HITL use the committed fixture `artifacts/lookup-savings-balance.json`.

## Members

| Member ID | Expected |
| --- | --- |
| `12345` | Success, savings `1842.50` |
| `00000` | Business outcome `MEMBER_NOT_FOUND` |
| `88888` | Recoverable interruption, then balance `500.00` |

## Windows you will use

| Window | URL / what it is | Use for |
| --- | --- | --- |
| Terminal 1 | DemoBank process | Leave running |
| Your browser (optional) | http://127.0.0.1:5100 | Confirm DemoBank is up |
| Playwright Chromium | Opens automatically | Live DemoBank during replay/HITL. Do not close it. |
| Your browser | http://127.0.0.1:5200 | Operator decisions only. **Scroll is no longer required** — the three buttons are above the screenshot. The screenshot is a picture, not the bank. |

---

## 0. One-time setup

```bash
cd /Users/yashshah/Documents/GitHub/computer-use-capability-engine
dotnet restore
dotnet build
pwsh tests/ComputerUse.Tests/bin/Debug/net8.0/playwright.ps1 install chromium
```

Skip the Playwright line if Chromium is already installed.

---

## 1. Start DemoBank (leave it running)

**Terminal 1:**

```bash
dotnet run --project src/DemoBank
```

**Pass:** http://127.0.0.1:5100 shows the lookup form. If this is down, later commands fail with `ERR_CONNECTION_REFUSED`.

If port `5100` is already in use, stop the old DemoBank process first.

---

## 2. Happy-path replay (no LLM)

**Terminal 2:**

```bash
dotnet run --project src/ComputerUse.Cli -- replay \
  --artifact artifacts/lookup-savings-balance.json \
  --member-id 12345 \
  --url http://127.0.0.1:5100
```

Chromium opens, types `12345`, clicks Lookup, opens the member record.

**Pass:** JSON includes `"kind": "Success"` and `"balance": "1842.50"`. A folder `evidence/replay-<timestamp>/` is created (gitignored).

Add `--headless` if you do not want a visible browser.

---

## 3. Member not found

```bash
dotnet run --project src/ComputerUse.Cli -- replay \
  --member-id 00000 \
  --url http://127.0.0.1:5100
```

**Pass:** `"kind": "BusinessOutcome"`, `"message": "MEMBER_NOT_FOUND"`. Not a crash.

---

## 4. Recoverable interruption

```bash
dotnet run --project src/ComputerUse.Cli -- replay \
  --member-id 88888 \
  --url http://127.0.0.1:5100
```

You should see a service-interruption banner, then Dismiss, then the member page.

**Pass:** `"kind": "Recoverable"`, `"balance": "500.00"`, plus `recoveryEvents`.

---

## 5. Simulated hard failure

```bash
dotnet run --project src/ComputerUse.Cli -- replay \
  --simulate-failure \
  --url http://127.0.0.1:5100
```

**Pass:** `"kind": "HardFailure"`.

---

## 6. Human-in-the-loop (three separate runs)

The committed artifact is already `approved`. You do not need `approve` first.

If a previous `hitl` is still running, stop it (`Ctrl+C`) so port `5200` is free.

You will start `hitl` **three times**. Each run pauses once; you press **one** operator button; the CLI exits; then you start the next run.

### 6a. Start a run

```bash
dotnet run --project src/ComputerUse.Cli -- hitl --url http://127.0.0.1:5100
```

Wait until:

1. Playwright Chromium stops on DemoBank **Confirm** (`Open sub-account` / **Confirm open sub-account**).
2. Terminal prints: open `http://127.0.0.1:5200` and choose Authorize, Completed, or Deny.
3. In your **normal** browser (not that Chromium), open http://127.0.0.1:5200.

You should see **Human intervention**, `Controller: Human`, then the **three buttons**, then a screenshot of DemoBank.

Do not click **Open sub-account** on `:5200` — that is inside the screenshot image.

### 6b. Audit-only click (do this on the first run, before any operator button)

1. In **Playwright Chromium**, click the heading or empty page — **not** **Confirm open sub-account**.
2. Refresh http://127.0.0.1:5200.
3. You should see a live-session list. `Controller` stays `Human`. The CLI is still waiting. Chromium is still on Confirm.

A bank click is logged. It does **not** authorize the irreversible step.

### 6c. Run 1 — Authorize

On http://127.0.0.1:5200 click **Authorize automation to perform this step**.

Watch Chromium: automation clicks **Confirm open sub-account**. Page shows **Sub-account opened for 12345**.

**Pass:** `"kind": "Success"`. Process exits.

### 6d. Run 2 — You complete it, then mark completed

Start `hitl` again (step 6a). Wait for Confirm + `:5200`.

1. In **Chromium**, click **Confirm open sub-account** yourself. Page shows **Sub-account opened for 12345**.
2. On `:5200` click **I completed the step**.

**Pass:** success (automation does not click Confirm again).

If you click **I completed the step** without confirming first, expect `"kind": "InterventionRequired"` and a message that completion could not be verified.

### 6e. Run 3 — Deny

Start `hitl` again. Wait for Confirm + `:5200`.

Do **not** click Confirm in Chromium.

On `:5200` click **Deny / stop**.

**Pass:** `"kind": "InterventionRequired"` and a message like `denied by human`. Chromium stays on Confirm (sub-account not opened). Process exits.

---

## 7. Optional — stability

```bash
dotnet run --project src/ComputerUse.Cli -- stability \
  --runs 5 \
  --member-id 12345 \
  --url http://127.0.0.1:5100
```

**Pass:** `evidence/stability-<timestamp>/report.json` with pass rate `1`. Add `--headed` to watch.

---

## 8. Optional — automated tests

Stop Terminal 1 DemoBank if you hit a port conflict on `:5100`.

```bash
dotnet test
```

No Bedrock required.

---

## 9. Optional — live Bedrock discovery (may fail)

DemoBank must still be running. This does **not** replace the replay fixture unless discovery finishes.

```bash
export BEDROCK_MODEL_ID=amazon.nova-lite-v1:0
export AWS_REGION=us-east-1

dotnet run --project src/ComputerUse.Cli -- discover \
  --goal "Look up member 12345 and return the current savings balance" \
  --url http://127.0.0.1:5100 \
  --member-id 12345
```

**If it finishes:** a **draft** artifact plus `evidence/discovery/<run-id>/` (`discovery.jsonl`, `obs-*.txt`, `artifact.json`, `result.json`). Replay that draft without Bedrock.

**If it does not:** command fails; `artifacts/lookup-savings-balance.json` is left as it was.

`--scripted` only rewrites the fixture with no LLM. It is not proof of live discovery.

---

## Suggested order

**0 → 1 → 2 → 3 → 4 → 5 → 6.** That covers success, business outcome, recovery, hard failure, and HITL without Bedrock.
