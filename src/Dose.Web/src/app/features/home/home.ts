import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';

/**
 * Home (spec 9.1). Education selectors are disabled until the catalog API
 * (Phase 1) is available — options will be loaded from the API, never hard-coded.
 */
@Component({
  selector: 'dose-home',
  imports: [RouterLink],
  template: `
    <section class="hero">
      <p class="eyebrow">جرعتك المركزة من الفهم</p>
      <h1>في حتة واقفة معاك؟<br /><span>Dose</span> توصلك للشرح المناسب</h1>
      <p class="lead">
        اختار الدرس أو الجزء اللي مش فاهمه، واحجز شرح مركّز ليه بس — من غير ما تدفع دروس شهرية.
      </p>
    </section>

    <section class="dose-card finder" aria-labelledby="finder-title">
      <h2 id="finder-title">إيه اللي واقف معاك؟</h2>
      <p class="hint">اختار المرحلة والمادة والمنهج</p>

      <label>
        <span>المرحلة / الصف</span>
        <select disabled><option>اختار الصف</option></select>
      </label>
      <label>
        <span>المادة</span>
        <select disabled><option>اختار المادة</option></select>
      </label>
      <label>
        <span>المنهج</span>
        <select disabled><option>عربي / لغات</option></select>
      </label>

      <button class="dose-btn dose-btn--primary" type="button" disabled>ابحث عن جلسة</button>
      <p class="dev-note">قيد التطوير: الاختيارات هتتحمّل من الكتالوج في المرحلة الأولى.</p>
    </section>

    <section class="entries">
      <a class="dose-card entry" routerLink="/open-groups">
        <strong>جلسات جماعية مفتوحة</strong>
        <span>انضم لجروب بدأ يتكوّن على نفس الدرس</span>
      </a>
      <a class="dose-card entry" routerLink="/request-session">
        <strong>مفيش ميعاد مناسب؟</strong>
        <span>اطلب جلسة جديدة وإحنا ننظّمها لك</span>
      </a>
    </section>
  `,
  styles: `
    :host { display: block; }
    .hero { padding: 28px 0 20px; }
    .eyebrow { margin: 0 0 6px; color: var(--dose-primary); font-weight: 700; }
    h1 { margin: 0; font-size: clamp(1.6rem, 6vw, 2.4rem); line-height: 1.35; color: var(--dose-navy); }
    h1 span { color: var(--dose-primary); }
    .lead { margin: 12px 0 0; color: var(--dose-text-muted); max-width: 36rem; }

    .finder { padding: 20px; display: grid; gap: 14px; }
    .finder h2 { margin: 0; font-size: 1.3rem; }
    .hint { margin: -10px 0 0; color: var(--dose-text-muted); font-size: 0.9rem; }
    label { display: grid; gap: 6px; font-weight: 600; font-size: 0.95rem; }
    select {
      min-height: 48px; padding: 0 12px; border: 1px solid var(--dose-border);
      border-radius: var(--dose-radius-sm); background: var(--dose-bg); font: inherit; color: var(--dose-text-muted);
    }
    .dev-note { margin: 0; font-size: 0.8rem; color: var(--dose-text-muted); text-align: center; }

    .entries { display: grid; gap: 12px; margin: 16px 0 32px; }
    .entry { display: grid; gap: 4px; padding: 18px; text-decoration: none; color: inherit; border-inline-start: 4px solid var(--dose-primary); }
    .entry:nth-child(2) { border-inline-start-color: var(--dose-accent); }
    .entry strong { font-size: 1.05rem; color: var(--dose-navy); }
    .entry span { color: var(--dose-text-muted); font-size: 0.92rem; }

    @media (min-width: 760px) {
      .entries { grid-template-columns: 1fr 1fr; }
    }
  `,
})
export class Home {}
