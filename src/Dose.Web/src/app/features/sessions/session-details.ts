import { Component, computed, effect, inject, input, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { Api, SessionDetails } from '../../core/api';
import { MarketingTracker } from '../../core/marketing-tracker';
import { errorMessage, fmtDate, fmtDateTime, fmtDuration, fmtMoney, fmtTime, myBookings } from '../../core/format';
import { TranslatePipe } from '../../core/i18n';
import { PublicSettingsService } from '../../core/public-settings.service';
import { StudentContext } from '../../core/student-context';
import { GroupTermsBox } from '../../shared/group-terms';
import { TeacherCard } from '../../shared/teacher-card';

/** Session details (spec 9.4): teacher identity and all terms are visible before the student commits. */
@Component({
  selector: 'dose-session-details',
  imports: [RouterLink, TeacherCard, GroupTermsBox, TranslatePipe],
  template: `
    <section class="page">
      <a routerLink="/sessions" class="back">{{ 'details.back' | t }}</a>
      @if (error()) {
        <p class="alert alert--error">{{ error() }}</p>
      } @else if (!d()) {
        <div class="skeleton"></div>
      } @else {
        @let s = d()!.summary;
        <div>
          @if (s.mode === 'Group') { <span class="badge badge--green">{{ 'session.openGroup' | t }}</span> }
          @else { <span class="badge">{{ 'session.private' | t }}</span> }
          <h1 class="page-title" dir="auto">{{ s.subjectName }} — {{ s.scopeTitle }}</h1>
          <p class="muted">{{ s.gradeName }} · {{ s.studyTrackName }} · {{ s.serviceTypeName }}</p>
        </div>

        <dose-teacher-card [teacher]="d()!.teacher" />

        <div class="dose-card box">
          <h2>{{ 'details.info' | t }}</h2>
          <ul class="info-list">
            <li><span>{{ 'details.when' | t }}</span><span>{{ date(s.startUtc) }} — {{ time(s.startUtc) }}</span></li>
            <li><span>{{ 'details.duration' | t }}</span><span>{{ duration(s.durationMinutes) }}</span></li>
            @if (s.mode === 'Group') {
              <li><span>{{ 'details.booked' | t }}</span><span>{{ 'details.bookedOf' | t: { n: s.confirmedCount, cap: s.capacity } }}</span></li>
              <li><span>{{ 'details.seatsLeft' | t }}</span><span>{{ s.remainingSeats }}</span></li>
            }
            <li><span>{{ 'details.deadline' | t }}</span><span>{{ dateTime(s.bookingDeadlineUtc) }}</span></li>
            <li><span>{{ 'details.price' | t }}</span><span class="price">{{ money(s.pricePerStudent, s.currency) }}</span></li>
          </ul>
          @if (d()!.scopeDetails) { <p class="muted small" dir="auto">{{ d()!.scopeDetails }}</p> }
        </div>

        @if (d()!.groupTerms; as g) {
          <dose-group-terms [terms]="g" [price]="s.pricePerStudent" [currency]="s.currency" />
        }

        <div class="dose-card box">
          <h2>{{ 'details.payCancel' | t }}</h2>
          <ul class="bullets">
            <li>{{ 'details.pay1' | t }}</li>
            <li>{{ 'details.pay2' | t }}</li>
            <li>{{ 'details.pay3' | t: { h: refundHours() } }}</li>
            <li>{{ 'details.pay4' | t }}</li>
          </ul>
          @if (d()!.requiresGuardianContact) {
            <p class="alert alert--info small">{{ 'details.guardianNeeded' | t }}</p>
          }
        </div>

        @if (bookError()) { <p class="alert alert--error">{{ bookError() }}</p> }
        @if (d()!.isOpenForBooking) {
          <button class="dose-btn dose-btn--primary btn-block" (click)="book()" [disabled]="booking()">
            {{ (booking() ? 'details.booking' : 'details.book') | t }}
          </button>
        } @else {
          <p class="alert">{{ 'details.closed' | t }} <a routerLink="/request-session">{{ 'sessions.requestNew' | t }}</a></p>
        }
      }
    </section>
  `,
  styles: `
    .back { text-decoration: none; font-weight: 600; }
    h1 { margin-top: 8px; }
    p { margin: 4px 0 0; }
    .box { padding: 16px; display: grid; gap: 12px; }
    .box h2 { margin: 0; font-size: 1.08rem; color: var(--dose-navy); }
    .bullets { margin: 0; padding-inline-start: 20px; display: grid; gap: 6px; font-size: 0.94rem; }
  `,
})
export class SessionDetailsPage {
  private readonly api = inject(Api);
  private readonly tracker = inject(MarketingTracker);
  private readonly router = inject(Router);
  private readonly publicSettings = inject(PublicSettingsService);
  private readonly context = inject(StudentContext);
  protected readonly refundHours = computed(() => this.publicSettings.settings()?.fullRefundCancellationHours ?? 24);
  readonly id = input.required<string>();

  protected readonly d = signal<SessionDetails | null>(null);
  protected readonly error = signal('');
  protected readonly booking = signal(false);
  protected readonly bookError = signal('');
  protected readonly date = fmtDate;
  protected readonly time = fmtTime;
  protected readonly dateTime = fmtDateTime;
  protected readonly duration = fmtDuration;
  protected readonly money = fmtMoney;

  constructor() {
    effect(() => {
      const id = this.id();
      this.api.session(id).then((d) => {
        this.d.set(d);
        this.context.setSupport({ step: 'support.step.viewing', session: `${d.summary.subjectName} — ${d.summary.scopeTitle}`,
          sessionTime: `${fmtDate(d.summary.startUtc)} ${fmtTime(d.summary.startUtc)}` });
      }).catch((e) => this.error.set(errorMessage(e)));
    });
  }

  protected async book() {
    this.booking.set(true);
    this.bookError.set('');
    try {
      const hold = await this.api.hold(this.id());
      myBookings.add(hold.reference, hold.accessToken);
      this.tracker.track('hold', hold.reference);
      await this.router.navigate(['/b', hold.accessToken]);
    } catch (e) {
      this.bookError.set(errorMessage(e));
    } finally {
      this.booking.set(false);
    }
  }
}
