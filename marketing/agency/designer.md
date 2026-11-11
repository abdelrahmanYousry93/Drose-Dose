# Writing a design spec

`node marketing/studio/render.js marketing/weeks/wNN/spec.json <outDir>` renders every piece of the spec.

```json
{ "pieces": [
  { "file": "w02-carousel-1.png", "size": "post", "bg": "light", "tag": "both",
    "body": "<div class='h1'>ذاكر مع <span class='accent'>صحابك</span><br/>وادفعوا أقل</div><div class='lead muted'>إزاي؟ اسحب ←</div>" },
  { "file": "w02-story-poll.png", "size": "story", "bg": "navy", "logo": "top",
    "body": "<div class='h1' style='text-align:center'>بتذاكر لوحدك<br/>ولا مع صحابك؟</div>", "sticker": "مكان ملصق الاستطلاع" },
  { "file": "w02-reel.mp4", "reel": true, "secondsPerScene": 3, "scenes": [
    { "bg": "navy", "body": "<div class='h1'>…</div>" },
    { "bg": "light", "logo": "center", "body": "<div class='h2'>جرعتك المركزة من الفهم</div>" } ] }
] }
```

Piece fields: `size` post|story|square · `bg` light|blue|navy|yellow · `tag` sec|prep|both|parents (or `tagText`) ·
`logo` top|center|none · `counter` e.g. "2 / 5" (replaces the tag, shown LTR) · `center` true centers the body ·
`sticker` adds the dashed sticker area · `footer` small line at the bottom.

Classes for `body`: `h1` (88px) · `h2` (64px) · `lead` · `small` · `accent` (blue; yellow on dark) · `yellow` · `muted` ·
`num` (round number badge) · `checks` (list of ✓ lines) · `pill` (yellow button) · `box` (white card) · `row` · `cols` · `ltr`.
Override size inline (`style='font-size:120px'`) when a line is short. Arabic math: use Arabic-Indic digits (٢س + ٣ = ١١)
so the equation reads right-to-left correctly.

Always look at every rendered PNG (and one reel frame) before uploading: no clipped text, nothing overlapping the sticker area.
