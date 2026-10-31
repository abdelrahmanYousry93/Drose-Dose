# Dose دروس — Brand & UI identity

Source: `docs/assets/ux-identity-reference.png`. That image is a **visual identity
reference only**. Product behavior always follows `docs/Dose_MVP_Specification.md`;
where screens in the image differ from the spec, the spec wins.

## Name & tagline

- Wordmark: **Dose** (Latin, with the "o" in the accent color) + **دروس** (Arabic).
- Tagline: جرعتك المركزة من الفهم
- Hero line: في حتة واقفة معاك؟ Dose توصلك للشرح المناسب
- Sign-off: معاك في كل خطوة

## Colors (CSS tokens in `src/Dose.Web/src/styles.scss`)

| Token | Value | Use |
|---|---|---|
| `--dose-primary` | `#0061FA` | Primary buttons, links, active states |
| `--dose-navy` | `#0B1F6B` | Headings, wordmark Arabic text |
| `--dose-accent` | `#FFC30A` | Highlights, focus ring, capsule half |
| `--dose-success` | `#07B24C` | Confirmed / "open group" badges |
| `--dose-whatsapp` | `#128C4A` | WhatsApp actions only |
| `--dose-bg` | `#F5F8FF` | Page background |
| `--dose-border` | `#DBE5F7` | Card and input borders |

## Typography

- **Cairo** (self-hosted via `@fontsource/cairo`, weights 400/600/700/800). No third-party font requests at runtime.

## UI language

- Arabic RTL first, Egyptian colloquial tone, short sentences.
- Rounded cards (16px), soft blue shadows, large tap targets (min 48px).
- Mobile-first; layouts must work at 360px width.

## Logo

The current logo in the app is a simple placeholder (capsule mark + wordmark)
drawn in code. Replace with the final logo files (SVG preferred) in
`src/Dose.Web/public/brand/` when available.
