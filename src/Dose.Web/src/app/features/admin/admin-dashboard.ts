import { Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AdminApi, Dashboard } from '../../core/admin-api';
import { errorMessage, fmtDateTime, fmtMoney } from '../../core/format';
import { TranslatePipe, translate as tr } from '../../core/i18n';

/** Admin home (spec 9.8): what needs attention, what's coming up, and numbers for the period. */
@Component({
  selector: 'dose-admin-dashboard',
  imports: [RouterLink, TranslatePipe],
  template: `
    <section class="page">
      <div class="row">
        <h1 class="page-title">{{ 'titles.adminDashboard' | t }}</h1>
        <button type="button" class="dose-btn dose-btn--ghost small-btn" (click)="load()">{{ 'common.refresh' | t }}</button>
      </div>
      @if (error()) { <p class="alert alert--error" role="alert">{{ error() }}</p> }
      @if (d(); as d) {
        <h2 class="sub">{{ 'dash.attention' | t }}</h2>
        <div class="tiles">
          <a class="dose-card tile" routerLink="/admin/payments" [class.hot]="d.pendingProofs > 0">
            <span class="num">{{ d.pendingProofs }}</span><span>{{ 'dash.proofs' | t }}</span>
            @if (d.oldestPendingProofMinutes !== null) { <span class="muted small">{{ 'dash.oldest' | t: { age: age(d.oldestPendingProofMinutes) } }}</span> }
          </a>
          <a class="dose-card tile" routerLink="/admin/follow-ups" [class.hot]="d.overdueTopUps > 0">
            <span class="num">{{ d.openTopUps }}</span><span>{{ 'dash.topUps' | t }}</span>
            @if (d.overdueTopUps) { <span class="small warn">{{ 'dash.overdue' | t: { n: d.overdueTopUps } }}</span> }
          </a>
          <a class="dose-card tile" routerLink="/admin/follow-ups" [class.hot]="d.pendingRefunds > 0">
            <span class="num">{{ d.pendingRefunds }}</span><span>{{ 'dash.refunds' | t }}</span>
            @if (d.pendingTransfers) { <span class="muted small">{{ 'dash.transfers' | t: { n: d.pendingTransfers } }}</span> }
          </a>
          <a class="dose-card tile" routerLink="/admin/requests" [class.hot]="d.newRequests > 0">
            <span class="num">{{ d.newRequests }}</span><span>{{ 'dash.requests' | t }}</span>
          </a>
          <a class="dose-card tile" routerLink="/admin/availability" [class.hot]="d.pendingAvailability > 0">
            <span class="num">{{ d.pendingAvailability }}</span><span>{{ 'dash.availability' | t }}</span>
          </a>
          <a class="dose-card tile" routerLink="/admin/messages" [class.hot]="d.pendingMessages > 0">
            <span class="num">{{ d.pendingMessages }}</span><span>{{ 'dash.messages' | t }}</span>
          </a>
        </div>

        <h2 class="sub">{{ 'dash.upcoming' | t }}</h2>
        @for (s of d.upcomingSessions; track s.id) {
          <div class="dose-card line">
            <div><strong>{{ s.subjectNameAr }} — {{ s.titleAr }}</strong>
              <span class="muted small">{{ dt(s.startUtc) }} · {{ s.teacherNameAr }}</span></div>
            <div class="meta">
              <span class="badge" [class.badge--green]="s.confirmedCount >= s.minParticipants">{{ s.confirmedCount }}/{{ s.capacity }}</span>
              @if (!s.hasMeetingUrl) { <span class="badge badge--yellow">{{ 'dash.noMeet' | t }}</span> }
            </div>
          </div>
        } @empty { <p class="muted small">{{ 'dash.noUpcoming' | t }}</p> }

        <h2 class="sub">{{ 'dash.nearDecision' | t }}</h2>
        @for (s of d.groupsNearDecision; track s.id) {
          <div class="dose-card line">
            <div><strong>{{ s.subjectNameAr }} — {{ s.titleAr }}</strong>
              <span class="muted small">{{ 'dash.decisionLine' | t: { d: dt(s.tierDecisionAtUtc), s: dt(s.startUtc) } }}</span></div>
            <div class="meta">
              <span class="badge" [class.badge--green]="s.confirmedCount >= s.minParticipants" [class.badge--yellow]="s.confirmedCount < s.minParticipants">
                {{ 'dash.ofNeeded' | t: { n: s.confirmedCount, min: s.minParticipants } }}</span>
            </div>
          </div>
        } @empty { <p class="muted small">{{ 'dash.noNear' | t }}</p> }

        <h2 class="sub">{{ 'dash.period' | t: { n: d.periodDays } }}</h2>
        <div class="tiles">
          <div class="dose-card tile"><span class="num">{{ d.periodConfirmedBookings }}</span><span>{{ 'dash.confirmed' | t }}</span></div>
          <div class="dose-card tile"><span class="num money">{{ money(d.periodCollected, d.currency) }}</span><span>{{ 'dash.collected' | t }}</span></div>
          <div class="dose-card tile"><span class="num money">{{ money(d.periodRefunded, d.currency) }}</span><span>{{ 'dash.refunded' | t }}</span></div>
        </div>
      } @else if (!error()) {
        <div class="skeleton"></div>
      }
    </section>
  `,
  styles: `
    .row { display: flex; justify-content: space-between; align-items: center; }
    .sub { margin: 10px 0 0; font-size: 1.1rem; color: var(--dose-navy); }
    .tiles { display: grid; gap: 10px; grid-template-columns: repeat(auto-fit, minmax(170px, 1fr)); }
    .tile { padding: 14px; display: grid; gap: 2px; text-decoration: none; color: inherit; }
    .tile.hot { border-color: var(--dose-accent); box-shadow: 0 0 0 2px #fff1c2 inset; }
    .num { font-size: 1.8rem; font-weight: 800; color: var(--dose-navy); line-height: 1.2; }
    .num.money { font-size: 1.3rem; }
    .warn { color: #8a6400; font-weight: 600; }
    .line { padding: 12px 14px; display: flex; justify-content: space-between; align-items: center; gap: 10px; flex-wrap: wrap; }
    .line > div:first-child { display: grid; }
    .meta { display: flex; gap: 6px; }
    .small-btn { min-height: 38px; padding: 0 12px; font-size: 0.88rem; }
  `,
})
export class AdminDashboard {
  private readonly api = inject(AdminApi);
  protected readonly d = signal<Dashboard | null>(null);
  protected readonly error = signal('');
  protected readonly dt = fmtDateTime;
  protected readonly money = fmtMoney;

  constructor() { void this.load(); }

  protected async load() {
    try { this.d.set(await this.api.dashboard(30)); this.error.set(''); } catch (e) { this.error.set(errorMessage(e)); }
  }

  protected age(min: number) {
    if (min < 60) return tr('duration.minutes', { n: Math.round(min) });
    if (min < 1440) return tr('dash.hours', { n: Math.round(min / 60) });
    return tr('dash.days', { n: Math.round(min / 1440) });
  }
}
