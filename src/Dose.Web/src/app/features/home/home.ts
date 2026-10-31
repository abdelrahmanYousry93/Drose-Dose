import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { Api, Lookup, Stage } from '../../core/api';
import { errorMessage } from '../../core/format';

/** Home (spec 9.1). All selector options come from the catalog API. */
@Component({
  selector: 'dose-home',
  imports: [RouterLink, FormsModule],
  template: `
    <section class="hero">
      <p class="eyebrow">جرعتك المركزة من الفهم</p>
      <h1>في حتة واقفة معاك؟<br /><span>Dose</span> توصلك للشرح المناسب</h1>
      <p class="lead">اختار الدرس أو الجزء اللي مش فاهمه، واحجز شرح مركّز ليه بس — من غير ما تدفع دروس شهرية.</p>
    </section>

    <form class="dose-card finder" (ngSubmit)="search()" aria-labelledby="finder-title">
      <h2 id="finder-title">إيه اللي واقف معاك؟</h2>
      <p class="muted small">اختار الصف والمادة والمنهج</p>

      @if (loadError()) { <p class="alert alert--error">{{ loadError() }}</p> }

      <label class="field">
        <span>الصف الدراسي</span>
        <select name="grade" [ngModel]="gradeId()" (ngModelChange)="onGrade($event)">
          <option value="">اختار الصف</option>
          @for (stage of stages(); track stage.id) {
            <optgroup [label]="stage.nameAr">
              @for (g of stage.grades; track g.id) { <option [value]="g.id">{{ g.nameAr }}</option> }
            </optgroup>
          }
        </select>
      </label>
      <label class="field">
        <span>المادة</span>
        <select name="subject" [ngModel]="subjectId()" (ngModelChange)="subjectId.set($event)" [disabled]="!gradeId()">
          <option value="">كل المواد</option>
          @for (s of subjects(); track s.id) { <option [value]="s.id">{{ s.nameAr }}</option> }
        </select>
      </label>
      <label class="field">
        <span>المنهج</span>
        <select name="track" [ngModel]="trackId()" (ngModelChange)="trackId.set($event)">
          <option value="">عربي ولغات</option>
          @for (t of tracks(); track t.id) { <option [value]="t.id">{{ t.nameAr }}</option> }
        </select>
      </label>

      <button class="dose-btn dose-btn--primary" type="submit" [disabled]="!canSearch()">ابحث عن جلسة</button>
    </form>

    <section class="entries">
      <a class="dose-card entry" routerLink="/open-groups">
        <strong>جلسات جماعية مفتوحة</strong>
        <span>انضم لجروب بيتكوّن على نفس الدرس بسعر أقل</span>
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
    .finder p { margin: -8px 0 0; }
    .entries { display: grid; gap: 12px; margin: 16px 0 32px; }
    .entry { display: grid; gap: 4px; padding: 18px; text-decoration: none; color: inherit; border-inline-start: 4px solid var(--dose-primary); }
    .entry:nth-child(2) { border-inline-start-color: var(--dose-accent); }
    .entry strong { font-size: 1.05rem; color: var(--dose-navy); }
    .entry span { color: var(--dose-text-muted); font-size: 0.92rem; }
    @media (min-width: 760px) { .entries { grid-template-columns: 1fr 1fr; } }
  `,
})
export class Home {
  private readonly api = inject(Api);
  private readonly router = inject(Router);

  protected readonly stages = signal<Stage[]>([]);
  protected readonly tracks = signal<Lookup[]>([]);
  protected readonly subjects = signal<Lookup[]>([]);
  protected readonly gradeId = signal('');
  protected readonly subjectId = signal('');
  protected readonly trackId = signal('');
  protected readonly loadError = signal('');
  protected readonly canSearch = computed(() => !!this.gradeId());

  constructor() {
    Promise.all([this.api.stages(), this.api.studyTracks()])
      .then(([stages, tracks]) => { this.stages.set(stages); this.tracks.set(tracks); })
      .catch((e) => this.loadError.set(errorMessage(e)));
  }

  protected async onGrade(id: string) {
    this.gradeId.set(id);
    this.subjectId.set('');
    this.subjects.set(id ? await this.api.subjects(id).catch(() => []) : []);
  }

  protected search() {
    if (!this.canSearch()) return;
    this.router.navigate(['/sessions'], {
      queryParams: { gradeId: this.gradeId(), subjectId: this.subjectId() || null, studyTrackId: this.trackId() || null },
    });
  }
}
