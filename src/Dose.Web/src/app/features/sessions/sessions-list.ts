import { Component, effect, inject, input, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { Api, SessionSummary } from '../../core/api';
import { errorMessage } from '../../core/format';
import { TranslatePipe } from '../../core/i18n';
import { SessionCard } from './session-card';

/** Available sessions (spec 9.3) and the open-groups page (9.7, via route data fixedMode = 'Group'). */
@Component({
  selector: 'dose-sessions-list',
  imports: [SessionCard, RouterLink, TranslatePipe],
  template: `
    <section class="page">
      <h1 class="page-title">{{ (fixedMode() === 'Group' ? 'sessions.openGroupsTitle' : 'sessions.title') | t }}</h1>
      @if (fixedMode() === 'Group') {
        <p class="muted">{{ 'sessions.openGroupsLead' | t }}</p>
      } @else {
        <div class="tabs" role="tablist">
          <button role="tab" [attr.aria-selected]="!mode()" (click)="setMode(null)">{{ 'sessions.all' | t }}</button>
          <button role="tab" [attr.aria-selected]="mode() === 'Private'" (click)="setMode('Private')">{{ 'sessions.private' | t }}</button>
          <button role="tab" [attr.aria-selected]="mode() === 'Group'" (click)="setMode('Group')">{{ 'sessions.group' | t }}</button>
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
          <a class="dose-btn dose-btn--primary" routerLink="/request-session">{{ 'sessions.requestNew' | t }}</a>
        </div>
      } @else {
        <div class="grid">
          @for (s of sessions(); track s.id) { <dose-session-card [session]="s" /> }
        </div>
        <p class="muted small center">{{ 'sessions.notFound' | t }} <a routerLink="/request-session">{{ 'sessions.requestNew' | t }}</a></p>
      }
    </section>
  `,
  styles: `
    .tabs { display: flex; gap: 6px; background: var(--dose-primary-50); padding: 4px; border-radius: var(--dose-radius-sm); }
    .tabs button { flex: 1; min-height: 40px; border: 0; border-radius: 8px; background: transparent; font: 600 0.92rem var(--dose-font); color: var(--dose-text-muted); cursor: pointer; }
    .tabs button[aria-selected='true'] { background: var(--dose-surface); color: var(--dose-primary); box-shadow: var(--dose-shadow); }
    .grid { display: grid; gap: 12px; }
    @media (min-width: 760px) { .grid { grid-template-columns: 1fr 1fr; } }
    .empty { padding: 24px; display: grid; gap: 8px; justify-items: center; text-align: center; }
    .empty p { margin: 0; }
    .center { text-align: center; }
  `,
})
export class SessionsList {
  private readonly api = inject(Api);
  private readonly router = inject(Router);

  readonly gradeId = input<string>();
  readonly subjectId = input<string>();
  readonly studyTrackId = input<string>();
  readonly mode = input<string>();
  readonly fixedMode = input<string>();

  protected readonly sessions = signal<SessionSummary[]>([]);
  protected readonly loading = signal(true);
  protected readonly error = signal('');

  constructor() {
    effect(() => {
      const filter = {
        gradeId: this.gradeId(), subjectId: this.subjectId(), studyTrackId: this.studyTrackId(),
        mode: this.fixedMode() ?? this.mode(),
      };
      this.loading.set(true);
      this.error.set('');
      this.api.sessions(filter)
        .then((s) => this.sessions.set(s))
        .catch((e) => this.error.set(errorMessage(e)))
        .finally(() => this.loading.set(false));
    });
  }

  protected setMode(mode: string | null) {
    this.router.navigate([], { queryParams: { mode }, queryParamsHandling: 'merge' });
  }
}
