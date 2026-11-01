import { Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { SessionSummary } from '../../core/api';
import { fmtDate, fmtDuration, fmtMoney, fmtTime } from '../../core/format';
import { TranslatePipe } from '../../core/i18n';

/** Session card: learning need, time, price and availability first — not teacher popularity (spec 9.3). */
@Component({
  selector: 'dose-session-card',
  imports: [RouterLink, TranslatePipe],
  template: `
    @let s = session();
    <a class="dose-card card" [routerLink]="['/sessions', s.id]">
      <div class="top">
        @if (s.mode === 'Group') { <span class="badge badge--green">{{ 'session.openGroup' | t }}</span> }
        @else { <span class="badge">{{ 'session.private' | t }}</span> }
        <span class="muted small">{{ s.serviceTypeName }}</span>
      </div>
      <h3 dir="auto">{{ s.subjectName }} — {{ s.scopeTitle }}</h3>
      <p class="muted small">{{ s.gradeName }} · {{ s.studyTrackName }}</p>
      <ul class="meta">
        <li>📅 {{ date(s.startUtc) }} — {{ time(s.startUtc) }}</li>
        <li>⏱️ {{ duration(s.durationMinutes) }}</li>
        @if (s.mode === 'Group') { <li>👥 {{ 'session.seatsLine' | t: { n: s.confirmedCount, cap: s.capacity, left: s.remainingSeats } }}</li> }
        <li>👤 {{ s.teacherName }}</li>
      </ul>
      <div class="bottom">
        <span><span class="price">{{ money(s.pricePerStudent, s.currency) }}</span> <span class="muted small">{{ 'session.perStudent' | t }}</span></span>
        <span class="dose-btn dose-btn--primary go">{{ 'session.details' | t }}</span>
      </div>
    </a>
  `,
  styles: `
    .card { display: grid; gap: 8px; padding: 16px; text-decoration: none; color: inherit; transition: border-color .15s; }
    .card:hover { border-color: var(--dose-primary); }
    .top { display: flex; justify-content: space-between; align-items: center; gap: 8px; }
    h3 { margin: 0; font-size: 1.08rem; color: var(--dose-navy); }
    p { margin: 0; }
    .meta { list-style: none; margin: 0; padding: 0; display: grid; gap: 4px; font-size: 0.92rem; }
    .bottom { display: flex; justify-content: space-between; align-items: center; margin-top: 4px; }
    .go { min-height: 40px; padding: 0 16px; }
    .price + .muted { margin-inline-start: 4px; }
  `,
})
export class SessionCard {
  readonly session = input.required<SessionSummary>();
  protected readonly date = fmtDate;
  protected readonly time = fmtTime;
  protected readonly duration = fmtDuration;
  protected readonly money = fmtMoney;
}
