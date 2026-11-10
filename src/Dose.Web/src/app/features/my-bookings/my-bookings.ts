import { Component, DestroyRef, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Api, BookingStatus, BookingView } from '../../core/api';
import { fmtDate, fmtDateTime, fmtMoney, myBookings, myRequests, SentRequest } from '../../core/format';
import { TranslatePipe } from '../../core/i18n';
import { PublicSettingsService } from '../../core/public-settings.service';
import { StudentContext } from '../../core/student-context';

interface Row { token: string; reference: string; view: BookingView | null; missing: boolean }

/** What the student still has to do for each status (null = nothing). */
const NEXT_STEP: Partial<Record<BookingStatus, string>> = {
  PendingHold: 'my.next.details',
  PendingPayment: 'my.next.pay',
  PaymentUnderReview: 'my.next.review',
  TopUpPending: 'my.next.topUp',
  Confirmed: 'my.next.confirmed',
};
const BADGE: Partial<Record<BookingStatus, string>> = {
  Confirmed: 'badge--green', Completed: 'badge--green',
  PendingHold: 'badge--yellow', PendingPayment: 'badge--yellow', PaymentUnderReview: 'badge--yellow', TopUpPending: 'badge--yellow',
  Cancelled: 'badge--gray', Expired: 'badge--gray', Refunded: 'badge--gray', Transferred: 'badge--gray', NoShow: 'badge--gray',
};

/**
 * "My bookings": every booking and request made or opened on this device, with its live status.
 * There are no student accounts — a booking's private link is its key — so the list lives on this device only;
 * a booking made on another phone shows up here as soon as its link is opened once.
 */
@Component({
  selector: 'dose-my-bookings',
  imports: [RouterLink, TranslatePipe],
  template: `
    <section class="page">
      <h1>{{ 'my.title' | t }}</h1>
      <p class="muted">{{ 'my.lead' | t }}</p>

      @if (loading()) {
        <p class="muted">{{ 'common.loading' | t }}</p>
      } @else if (!rows().length && !requests().length) {
        <div class="dose-card empty">
          <p><strong>{{ 'my.empty' | t }}</strong></p>
          <p class="muted">{{ 'my.emptyBody' | t }}</p>
          <a class="dose-btn dose-btn--primary" routerLink="/sessions">{{ 'my.browse' | t }}</a>
        </div>
      }

      @if (rows().length) {
        <h2>{{ 'my.bookings' | t }}</h2>
        <div class="list">
          @for (r of rows(); track r.token) {
            <article class="dose-card item">
              @if (r.view; as v) {
                <div class="top">
                  <strong class="title">{{ v.session.subjectName }} — {{ v.session.scopeTitle }}</strong>
                  <span class="badge {{ badge(v.status) }}">{{ 'status.' + v.status | t }}</span>
                </div>
                <ul class="info-list">
                  <li><span>{{ 'common.grade' | t }}</span><span>{{ v.session.gradeName }}</span></li>
                  <li><span>{{ 'my.when' | t }}</span><span>{{ dateTime(v.session.startUtc) }}</span></li>
                  <li><span>{{ 'my.price' | t }}</span><span>{{ money(v.price, v.currency) }}</span></li>
                  <li><span>{{ 'my.reference' | t }}</span><span dir="ltr">{{ v.reference }}</span></li>
                </ul>
                @if (next(v.status); as n) { <p class="next">{{ n | t }}</p> }
                <a class="dose-btn dose-btn--primary btn-block" [routerLink]="['/b', r.token]">{{ 'my.open' | t }}</a>
              } @else {
                <div class="top">
                  <strong class="title" dir="ltr">{{ r.reference }}</strong>
                  <span class="badge badge--gray">{{ 'my.unavailable' | t }}</span>
                </div>
                <p class="muted small">{{ 'my.unavailableBody' | t }}</p>
                <button type="button" class="dose-btn dose-btn--ghost" (click)="removeBooking(r.token)">{{ 'my.remove' | t }}</button>
              }
            </article>
          }
        </div>
      }

      @if (requests().length) {
        <h2>{{ 'my.requests' | t }}</h2>
        <div class="list">
          @for (q of requests(); track q.reference) {
            <article class="dose-card item">
              <div class="top">
                <strong class="title">{{ q.subject || ('my.request' | t) }}</strong>
                <span class="badge badge--yellow">{{ 'my.requestSent' | t }}</span>
              </div>
              <ul class="info-list">
                @if (q.grade) { <li><span>{{ 'common.grade' | t }}</span><span>{{ q.grade }}</span></li> }
                <li><span>{{ 'my.sentOn' | t }}</span><span>{{ date(q.sentAtUtc) }}</span></li>
                <li><span>{{ 'my.reference' | t }}</span><span dir="ltr">{{ q.reference }}</span></li>
              </ul>
              <p class="next">{{ 'my.requestNext' | t }}</p>
              <button type="button" class="dose-btn dose-btn--ghost" (click)="removeRequest(q.reference)">{{ 'my.remove' | t }}</button>
            </article>
          }
        </div>
      }

      <div class="dose-card help">
        <p><strong>{{ 'my.missingTitle' | t }}</strong></p>
        <p class="muted small">{{ 'my.missingBody' | t }}</p>
        @if (settings.whatsAppLink(); as link) {
          <a class="dose-btn dose-btn--whatsapp" [href]="link" target="_blank" rel="noopener">{{ 'nav.whatsapp' | t }}</a>
        }
      </div>
    </section>
  `,
  styles: `
    .page { max-width: 720px; margin: 0 auto; padding: 24px 16px 40px; display: grid; gap: 14px; }
    h1 { margin: 0; color: var(--dose-navy); }
    h2 { margin: 10px 0 0; font-size: 1.1rem; color: var(--dose-navy); }
    .page > p { margin: 0; }
    .list { display: grid; gap: 12px; }
    .item { padding: 16px; display: grid; gap: 12px; }
    .top { display: flex; gap: 10px; align-items: flex-start; justify-content: space-between; }
    .title { color: var(--dose-navy); }
    .badge { flex: none; }
    .next { margin: 0; padding: 10px 12px; border-radius: var(--dose-radius-sm); background: var(--dose-primary-50); color: var(--dose-navy); font-weight: 600; font-size: 0.92rem; }
    .empty, .help { padding: 20px; display: grid; gap: 8px; justify-items: start; }
    .empty p, .help p { margin: 0; }
  `,
})
export class MyBookings {
  private readonly api = inject(Api);
  protected readonly settings = inject(PublicSettingsService);
  protected readonly rows = signal<Row[]>([]);
  protected readonly requests = signal<SentRequest[]>(myRequests.list());
  protected readonly loading = signal(true);

  constructor() {
    const context = inject(StudentContext);
    context.setSupport({ step: 'support.step.myBookings' });
    inject(DestroyRef).onDestroy(() => context.setSupport({}));
    void this.load();
  }

  private async load() {
    const saved = myBookings.list();
    const rows = await Promise.all(saved.map(async (b): Promise<Row> => {
      try { return { ...b, view: await this.api.booking(b.token), missing: false }; }
      catch { return { ...b, view: null, missing: true }; }
    }));
    // Upcoming first, then the most recent past ones.
    const now = Date.now();
    const time = (r: Row) => (r.view ? new Date(r.view.session.startUtc).getTime() : 0);
    rows.sort((a, b) => {
      const fa = time(a) >= now, fb = time(b) >= now;
      if (fa !== fb) return fa ? -1 : 1;
      return fa ? time(a) - time(b) : time(b) - time(a);
    });
    this.rows.set(rows);
    this.loading.set(false);
  }

  protected badge(s: BookingStatus) { return BADGE[s] ?? ''; }
  protected next(s: BookingStatus) { return NEXT_STEP[s] ?? null; }
  protected dateTime(utc: string) { return fmtDateTime(utc); }
  protected date(utc: string) { return fmtDate(utc); }
  protected money(v: number, c: string) { return fmtMoney(v, c); }

  protected removeBooking(token: string) {
    myBookings.remove(token);
    this.rows.update((r) => r.filter((x) => x.token !== token));
  }

  protected removeRequest(reference: string) {
    myRequests.remove(reference);
    this.requests.set(myRequests.list());
  }
}
