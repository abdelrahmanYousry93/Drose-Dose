import { Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { SessionSummary } from '../../core/api';
import { fmtDate, fmtDuration, fmtMoney, fmtTime } from '../../core/format';

/** Session card: learning need, time, price and availability first — not teacher popularity (spec 9.3). */
@Component({
  selector: 'dose-session-card',
  imports: [RouterLink],
  template: `
    @let s = session();
    <a class="dose-card card" [routerLink]="['/sessions', s.id]">
      <div class="top">
        @if (s.mode === 'Group') { <span class="badge badge--green">جلسة جماعية مفتوحة</span> }
        @else { <span class="badge">جلسة فردية</span> }
        <span class="muted small">{{ s.serviceTypeNameAr }}</span>
      </div>
      <h3>{{ s.subjectNameAr }} — {{ s.scopeTitleAr }}</h3>
      <p class="muted small">{{ s.gradeNameAr }} · {{ s.studyTrackNameAr }}</p>
      <ul class="meta">
        <li>📅 {{ date(s.startUtc) }} — {{ time(s.startUtc) }}</li>
        <li>⏱️ {{ duration(s.durationMinutes) }}</li>
        @if (s.mode === 'Group') { <li>👥 {{ s.confirmedCount }} من {{ s.capacity }} طلاب · فاضل {{ s.remainingSeats }}</li> }
        <li>👤 {{ s.teacherNameAr }}</li>
      </ul>
      <div class="bottom">
        <span><span class="price">{{ money(s.pricePerStudent, s.currency) }}</span> <span class="muted small">للطالب</span></span>
        <span class="dose-btn dose-btn--primary go">التفاصيل</span>
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
  `,
})
export class SessionCard {
  readonly session = input.required<SessionSummary>();
  protected readonly date = fmtDate;
  protected readonly time = fmtTime;
  protected readonly duration = fmtDuration;
  protected readonly money = fmtMoney;
}
