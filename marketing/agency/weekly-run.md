# Weekly run — the procedure

The scheduled runs follow this file exactly. HQ page: **https://claude.ai/artifact/D4MiHRbYzKiGysQX5PmDcr**
(read/write its database with the `ArtifactData` tool, upload files with the `Artifact` tool: `action: "publish"`,
`url` = HQ, `asset: true`, `file_paths` = up to 25 files → each returns a `/_blob/<id>` url).

## HQ database
| Collection / doc | Fields |
|---|---|
| `weeks/wNN` | `n`, `dates` ("21 – 27 نوفمبر"), `theme`, `goal`, `summary` (one line for the top of HQ), `plan` (markdown), `report` (markdown), `status` planning\|in_review\|live\|done, `current` (true on exactly one week), `producedAt` |
| `items/wNN-MM` | `week`, `order`, `day`, `date`, `time`, `platforms[]`, `format`, `audience` sec\|prep\|both\|parents, `title`, `brief`, `caption`, `howTo`, `assets[{url,name,kind:image\|video}]`, `role`, `status` review\|changes\|approved\|published, `feedback` (owner), `agencyNote` (agency reply), `updatedAt` |
| `metrics/wNN` | `reach`, `profileVisits`, `linkClicks`, `waChats`, `requests`, `bookings`, `paid`, `adSpend`, `askedLessons`, `notes` — entered by the owner |
| `tasks/<id>` | `title`, `detail`, `owner` you\|agency, `done`, `order`, `week` |
| `briefs/<id>` | `text`, `status` new\|done, `reply`, `createdAt` — requests from the owner |
| `log/<id>` | `role` (see roles.md ids), `text`, `at` ISO — one line per role per run |
Always pass `if_version` from your last read when updating an existing doc.

## Thursday — production run (produces next week)
1. **Read**: `weeks`, last week's `items`, `metrics/<this week>`, open `briefs`, `tasks`; `brand.md`, `roles.md`, the plan
   doc (Claude Docs 1ab5e093-3a4a-4e7a-9bb2-50b570abdb65) for the month arc; the live site's public catalog
   (`GET https://lionfish-app-n6c49.ondigitalocean.app/api/catalog/stages`, `/api/sessions`) to know what is bookable.
2. **Analyst** → `weeks/<this week>.report` (markdown: numbers table, what worked, what didn't, 3 changes for next week).
   If no metrics were entered, say so plainly and base the report on approvals/feedback only. Never invent numbers.
3. **Strategist** → create `weeks/<next>` (status planning, `current` stays on this week until Saturday) with goal, theme, plan.
   Follow the month arc but adapt to the report, the most-asked lessons and the briefs.
4. **Copywriter + Designer + Video** → write `marketing/weeks/<next>/spec.json` (see designer.md), render with
   `node marketing/studio/render.js marketing/weeks/<next>/spec.json /tmp/<next>`; look at every PNG and one reel frame;
   fix and re-render anything clipped or off-brand.
5. **QA** → check every piece against brand.md hard rules and what is bookable. Anything that depends on an owner task
   (e.g. promo codes not created yet) gets that dependency in its `brief`.
6. Upload all files to HQ assets, write the `items/<next>-MM` docs (status `review`), set the week status `in_review`.
7. **Community** → add a "ردود الأسبوع" section to the plan (likely comments/DMs + replies).
8. **Ads** → if a piece from this week is approved/published and did well, add a boost proposal to the plan (budget from
   the plan doc, owner approves; never spend).
9. **Account** → close briefs you handled (`status` done + `reply`), add/refresh owner tasks for next week, write one `log`
   line per role, commit `marketing/weeks/<next>/` (spec + captions) to the repo on main.

## Saturday — revisions run
1. Every item with `status: changes`: read `feedback`, change the spec/caption, re-render, re-upload, update the item
   (`status` review, `agencyNote` = what changed in one line, keep `feedback` for history).
2. Answer new `briefs` (if a brief asks for a new piece, add it to the next week's items).
3. Move `current: true` to the week that starts today (if it has items) and set the previous week `done`.
4. One `log` line per role that did something. If nothing to do, write one `account` log line saying so.

## Never
- Publish to social networks, spend money, or message students (Phase 1 has no such access anyway).
- Delete the owner's feedback, metrics or briefs.
- Invent metrics, testimonials, teacher names or availability.
