# Dose Agency

Dose's marketing is run by an AI agency: a team of roles that plans, writes, designs, edits and reports every week.
The owner only reads the report, approves or comments on each piece, and posts (until Meta publishing is connected).

| Where | What |
|---|---|
| **Agency HQ** (published page, link in `agency/hq.md`) | Weekly plan, content waiting for approval, report and numbers, owner's tasks, requests to the agency |
| `agency/brand.md` | Who we talk to, voice, visual identity, hard rules |
| `agency/roles.md` | The nine roles and what each one delivers |
| `agency/weekly-run.md` | The exact procedure the weekly run follows (the scheduled task reads this) |
| `agency/designer.md` | How to write a design spec for the renderer |
| `studio/render.js` | Design engine: spec JSON → PNG posts/stories and MP4 reels in the brand |
| `weeks/wNN/` | Each week's spec, captions and notes (rendered files are uploaded to HQ, not committed) |

## Cycle

1. **Thursday – production run** (scheduled): read last week's numbers, the owner's requests and comments → report → plan → write → design → upload everything to HQ with status *Waiting for approval*.
2. **Owner** (Thu–Fri): approve each piece or write what to change; enter the week's numbers.
3. **Saturday – revisions run** (scheduled): redo every piece marked *Needs changes*, answer requests.
4. **Owner**: schedule the approved pieces in Meta Business Suite and mark them *Published*.

Phase 2 (later): Meta Graph API for auto-publishing, insights and comment replies. Phase 3: ads + WhatsApp Cloud API.
