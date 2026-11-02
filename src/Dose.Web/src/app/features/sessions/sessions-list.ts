import { Component, computed, effect, inject, input, signal, untracked } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { Api, Lookup, SessionSummary, Stage } from '../../core/api';
import { errorMessage } from '../../core/format';
import { TranslatePipe } from '../../core/i18n';
import { StudentContext } from '../../core/student-context';
import { SessionCard } from './session-card';

/**
 * Available sessions (spec 9.3) and the open-groups page (9.7, via route data fixedMode = 'Group').
 * Lists only the student's grade by default (remembered from the home page), with filters on top.
 */
@Component({
  selector: 'dose-sessions-list',
  imports: [SessionCard, RouterLink, FormsModule, TranslatePipe],
  template: `
    <section class="page">
      <h1 class="page-title">{{ (fixedMode() === 'Group' ? 'sessions.openGroupsTitle' : 'sessions.title') | t }}</h1>
      @if (fixedMode() === 'Group') { <p class="muted">{{ 'sessions.openGroupsLead' | t }}</p> }

      <form class="dose-card filters" [attr.aria-label]="'filters.title' | t">
        <label class="field"><span>{{ 'common.grade' | t }}</span>
          <select name="grade" [ngModel]="gradeId() ?? ''" (ngModelChange)="setFilter({ gradeId: $event || null, subjectId: null })">
            <option value="">{{ 'filters.allGrades' | t }}</option>
            @for (st of stages(); track st.id) {
              <optgroup [label]="st.name">@for (g of st.grades; track g.id) { <option [value]="g.id">{{ g.name }}</option> }</optgroup>
            }
          </select></label>
        <label class="field"><span>{{ 'common.subject' | t }}</span>
          <select name="subject" [ngModel]="subjectId() ?? ''" (ngModelChange)="setFilter({ subjectId: $event || null })" [disabled]="!gradeId()">
            <option value="">{{ 'home.allSubjects' | t }}</option>
            @for (s of subjects(); track s.id) { <option [value]="s.id">{{ s.name }}</option> }
          </select></label>
        <label class="field"><span>{{ 'common.track' | t }}</span>
          <select name="track" [ngModel]="studyTrackId() ?? ''" (ngModelChange)="setFilter({ studyTrackId: $event || null })">
            <option value="">{{ 'home.allTracks' | t }}</option>
            @for (t of tracks(); track t.id) { <option [value]="t.id">{{ t.name }}</option> }
          </select></label>
      </form>

      @if (fixedMode() !== 'Group') {
        <div class="tabs" role="tablist">
          <button role="tab" [attr.aria-selected]="!mode()" (click)="setFilter({ mode: null })">{{ 'sessions.all' | t }}</button>
          <button role="tab" [attr.aria-selected]="mode() === 'Private'" (click)="setFilter({ mode: 'Private' })">{{ 'sessions.private' | t }}</button>
          <button role="tab" [attr.aria-selected]="mode() === 'Group'" (click)="setFilter({ mode: 'Group' })">{{ 'sessions.group' | t }}</button>
        </div>
      }

      @if (loading()) {
        <div class="skeleton"></div><div class="skeleton"></div>
      } @else if (error()) {
        <p class="alert alert--error">{{ error() }}</p>
      } @else if (sessions().length === 0) {
        <div class="dose-card empty">
          <strong>{{ 'sessions.empty' | t }}</strong>
          <p class="muted">{{ 'sessions.emptyHint' | t }}</p>
          <div class="empty-actions">
            <a class="dose-btn dose-btn--primary" routerLink="/private" [queryParams]="carry()">{{ 'home.privateEntry' | t }}</a>
            <a class="dose-btn dose-btn--ghost" routerLink="/request-session" [queryParams]="carry()">{{ 'sessions.requestNew' | t }}</a>
          </div>
        </div>
      } @else {
        <div class="grid">
          @for (s of sessions(); track s.id) { <dose-session-card [session]="s" /> }
        </div>
        <p class="muted small center">{{ 'sessions.notFound' | t }}
          <a routerLink="/private" [queryParams]="carry()">{{ 'home.privateEntry' | t }}</a> ·
          <a routerLink="/request-session" [queryParams]="carry()">{{ 'sessions.requestNew' | t }}</a></p>
      }
    </section>
  `,
  styles: `
    .filters { padding: 14px; display: grid; gap: 10px; grid-template-columns: 1fr 1fr; }
    .filters .field:first-child { grid-column: 1 / -1; }
    @media (min-width: 760px) { .filters { grid-template-columns: 2fr 1fr 1fr; } .filters .field:first-child { grid-column: auto; } }
    .tabs { display: flex; gap: 6px; background: var(--dose-primary-50); padding: 4px; border-radius: var(--dose-radius-sm); }
    .tabs button { flex: 1; min-height: 40px; border: 0; border-radius: 8px; background: transparent; font: 600 0.92rem var(--dose-font); color: var(--dose-text-muted); cursor: pointer; }
    .tabs button[aria-selected='true'] { background: var(--dose-surface); color: var(--dose-primary); box-shadow: var(--dose-shadow); }
    .grid { display: grid; gap: 12px; }
    @media (min-width: 760px) { .grid { grid-template-columns: 1fr 1fr; } }
    .empty { padding: 24px; display: grid; gap: 8px; justify-items: center; text-align: center; }
    .empty p { margin: 0; }
    .empty-actions { display: flex; flex-wrap: wrap; gap: 8px; justify-content: center; }
    .center { text-align: center; }
  `,
})
export class SessionsList {
  private readonly api = inject(Api);
  private readonly router = inject(Router);
  private readonly context = inject(StudentContext);

  readonly gradeId = input<string>();
  readonly subjectId = input<string>();
  readonly studyTrackId = input<string>();
  readonly mode = input<string>();
  readonly fixedMode = input<string>();
  /** Set when the student explicitly picked "All grades" (so the remembered grade isn't re-applied). */
  readonly all = input<string>();

  protected readonly sessions = signal<SessionSummary[]>([]);
  protected readonly stages = signal<Stage[]>([]);
  protected readonly tracks = signal<Lookup[]>([]);
  protected readonly subjects = signal<Lookup[]>([]);
  protected readonly loading = signal(true);
  protected readonly error = signal('');

  /** Filters carried to the request / private-booking forms so the student doesn't type them again. */
  protected readonly carry = computed(() => ({
    gradeId: this.gradeId() ?? null, subjectId: this.subjectId() ?? null, studyTrackId: this.studyTrackId() ?? null,
  }));

  constructor() {
    effect(() => this.context.setSupport({ step: this.fixedMode() === 'Group' ? 'support.step.openGroups' : 'support.step.browsing' }));
    Promise.all([this.api.stages(), this.api.studyTracks()])
      .then(([st, t]) => { this.stages.set(st); this.tracks.set(t); }).catch(() => undefined);

    // First visit without filters: use the grade the student picked on the home page.
    effect(() => {
      const p = this.context.profile();
      if (!this.gradeId() && !this.all() && p) {
        untracked(() => this.router.navigate([], {
          queryParams: { gradeId: p.gradeId, subjectId: p.subjectId || null, studyTrackId: p.studyTrackId || null },
          queryParamsHandling: 'merge', replaceUrl: true,
        }));
      }
    });
    effect(() => {
      const g = this.gradeId();
      untracked(() => {
        if (g) this.api.subjects(g).then((s) => this.subjects.set(s)).catch(() => this.subjects.set([]));
        else this.subjects.set([]);
      });
    });
    effect(() => {
      if (!this.gradeId() && !this.all() && this.context.profile()) return; // redirecting to the remembered grade
      const filter = {
        gradeId: this.gradeId(), subjectId: this.subjectId(), studyTrackId: this.studyTrackId(),
        mode: this.fixedMode() ?? this.mode(),
      };
      untracked(() => {
        this.loading.set(true);
        this.error.set('');
        this.api.sessions(filter)
          .then((s) => this.sessions.set(s))
          .catch((e) => this.error.set(errorMessage(e)))
          .finally(() => this.loading.set(false));
      });
    });
  }

  protected setFilter(change: { gradeId?: string | null; subjectId?: string | null; studyTrackId?: string | null; mode?: string | null }) {
    const next = { ...change, all: 'gradeId' in change && !change.gradeId ? '1' : null };
    this.router.navigate([], { queryParams: next, queryParamsHandling: 'merge' }).then(() => this.remember());
  }

  /** Keeps the remembered profile in step with what the student filters on. */
  private remember() {
    const g = this.gradeId();
    if (!g) return;
    const grade = this.stages().flatMap((s) => s.grades).find((x) => x.id === g);
    this.context.saveProfile({
      gradeId: g, gradeName: grade?.name ?? '',
      subjectId: this.subjectId() ?? '', subjectName: this.subjects().find((s) => s.id === this.subjectId())?.name ?? '',
      studyTrackId: this.studyTrackId() ?? '', studyTrackName: this.tracks().find((t) => t.id === this.studyTrackId())?.name ?? '',
    });
  }
}
