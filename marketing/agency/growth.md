# Growth track — getting the first followers (started 14 Nov 2026)

Runs **in parallel** with the weekly content plan (weekly-run.md). The content plan explains Dose and drives bookings;
the growth track only aims at **new followers** on the Facebook Page «دروس Dose», Instagram `drwsdose` and TikTok.

## Hard rules
- The owner will **not** invite people from his personal profile (privacy). Never ask him to share from his own account.
- No bought followers/likes, no follow-for-follow groups, no spam messages, no fake accounts or fake testimonials.
- Contests: entry by **commenting only** (Facebook rules forbid "share to enter" / "tag friends to enter"). Prizes are
  free sessions — draft the contest for approval, never announce one the owner hasn't approved.
- Calls to action are "تابع Dose" / "احفظ" / "ابعته لصاحبك" — not "احجز" while the site has no bookable sessions.
- No money is spent without the owner's explicit approval of each boost (budget: HQ task g3).

## Daily — growth reel (scheduled run)
One 12–15 s faceless reel per day at **6 pm Egypt time** (the feed post stays at 8 pm), produced **2 days ahead** so the
owner has time to approve:
- Formats that rotate: POV / relatable school moment · one study trick in 3 steps · "قول كده بدل كده" · myth vs fact ·
  exam-season tip (first-term exams are in January — lean into revision content from December).
- First 2 seconds: the hook in big type. Last scene: logo + follow CTA. 5 scenes × ~2.7 s.
- Spec in `marketing/growth/spec-gNN.json` (one file per batch), render with
  `node marketing/studio/render.js marketing/growth/spec-gNN.json <outdir> src/Dose.Web/public/m/gNN`, look at a frame
  sheet of every reel, upload the MP4 to HQ (asset), and add an HQ item: week = the week containing the date,
  id `<week>-g<N>`, `track: "growth"`, `format: "ريل"`, `status: "review"`, caption ending with hashtags.
- Once approved, the daily publisher sync queues it as `kind: "video"` with media `/m/gNN/<file>.mp4`, channels
  facebook + instagram. TikTok stays manual (owner task g4).

## Weekly (Thursday production run adds these)
- **Study sheet**: one free one-page summary or "ليلة الامتحان" checklist for a hard lesson (album or link post).
- **Group pack**: 3 helpful answers/posts the owner can post *as the Page* in student groups (no ads, no links in the
  first line).
- **Boost proposal**: the best growth reel of the week by saves + shares, with a suggested daily budget — only as a
  proposal in the plan, never spent automatically.

## Monthly
- Contest draft: "اكتب الدرس اللي واقف معاك — 3 ياخدوا جلسة شرح ببلاش" (comment to enter), only once the site has
  sessions; owner approves the prize and dates.

## Measure (analyst)
Followers per channel (owner enters them in HQ metrics on Thursday until the API gives them), reel likes/comments/shares
from `/api/marketing/report`, and site visits from `?src=` links. Target for the first month: a few hundred to ~1,000
real followers; judge formats by saves and shares, not likes.
