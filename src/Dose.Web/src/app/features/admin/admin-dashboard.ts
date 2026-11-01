import { Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AdminApi, Dashboard } from '../../core/admin-api';
import { errorMessage, fmtDateTime, fmtMoney } from '../../core/format';

/** Admin home (spec 9.8): what needs attention, what's coming up, and numbers for the period. */
@Component({
  selector: 'dose-admin-dashboard',
  imports: [RouterLink],
  template: `
    <section class="page">
      <div class="row">
        <h1 class="page-title">الرئيسية</h1>
        <button type="button" class="dose-btn dose-btn--ghost small-btn" (click)="load()">تحديث</button>
      </div>
      @if (error()) { <p class="alert alert--error" role="alert">{{ error() }}</p> }
      @if (d(); as d) {
        <h2 class="sub">محتاج انتباهك</h2>
        <div class="tiles">
          <a class="dose-card tile" routerLink="/admin/payments" [class.hot]="d.pendingProofs > 0">
            <span class="num">{{ d.pendingProofs }}</span><span>إيصال مستني مراجعة</span>
            @if (d.oldestPendingProofMinutes !== null) { <span class="muted small">أقدمهم من {{ age(d.oldestPendingProofMinutes) }}</span> }
          </a>
          <a class="dose-card tile" routerLink="/admin/follow-ups" [class.hot]="d.overdueTopUps > 0">
            <span class="num">{{ d.openTopUps }}</span><span>فرق جروب مفتوح</span>
            @if (d.overdueTopUps) { <span class="small warn">{{ d.overdueTopUps }} عدّى ميعاد ردهم</span> }
          </a>
          <a class="dose-card tile" routerLink="/admin/follow-ups" [class.hot]="d.pendingRefunds > 0">
            <span class="num">{{ d.pendingRefunds }}</span><span>استرداد مطلوب</span>
            @if (d.pendingTransfers) { <span class="muted small">+ {{ d.pendingTransfers }} نقل</span> }
          </a>
          <a class="dose-card tile" routerLink="/admin/requests" [class.hot]="d.newRequests > 0">
            <span class="num">{{ d.newRequests }}</span><span>طلب جلسة جديد</span>
          </a>
          <a class="dose-card tile" routerLink="/admin/availability" [class.hot]="d.pendingAvailability > 0">
            <span class="num">{{ d.pendingAvailability }}</span><span>ميعاد مدرس مستني موافقة</span>
          </a>
        </div>

        <h2 class="sub">جلسات خلال 48 ساعة</h2>
        @for (s of d.upcomingSessions; track s.id) {
          <div class="dose-card line">
            <div><strong>{{ s.subjectNameAr }} — {{ s.titleAr }}</strong>
              <span class="muted small">{{ dt(s.startUtc) }} · {{ s.teacherNameAr }}</span></div>
            <div class="meta">
              <span class="badge" [class.badge--green]="s.confirmedCount >= s.minParticipants">{{ s.confirmedCount }}/{{ s.capacity }}</span>
              @if (!s.hasMeetingUrl) { <span class="badge badge--yellow">مفيش لينك Meet</span> }
            </div>
          </div>
        } @empty { <p class="muted small">مفيش جلسات خلال اليومين الجايين.</p> }

        <h2 class="sub">جروبات قرّب ميعاد حسمها</h2>
        @for (s of d.groupsNearDecision; track s.id) {
          <div class="dose-card line">
            <div><strong>{{ s.subjectNameAr }} — {{ s.titleAr }}</strong>
              <span class="muted small">الحسم {{ dt(s.tierDecisionAtUtc) }} · الجلسة {{ dt(s.startUtc) }}</span></div>
            <div class="meta">
              <span class="badge" [class.badge--green]="s.confirmedCount >= s.minParticipants" [class.badge--yellow]="s.confirmedCount < s.minParticipants">
                {{ s.confirmedCount }} من {{ s.minParticipants }} مطلوبين</span>
            </div>
          </div>
        } @empty { <p class="muted small">مفيش جروبات قرّب حسمها.</p> }

        <h2 class="sub">آخر {{ d.periodDays }} يوم</h2>
        <div class="tiles">
          <div class="dose-card tile"><span class="num">{{ d.periodConfirmedBookings }}</span><span>حجز مؤكد</span></div>
          <div class="dose-card tile"><span class="num money">{{ money(d.periodCollected, d.currency) }}</span><span>تحصيل مؤكد (شامل الفروق)</span></div>
          <div class="dose-card tile"><span class="num money">{{ money(d.periodRefunded, d.currency) }}</span><span>استردادات اتحوّلت</span></div>
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
    if (min < 60) return `${min} دقيقة`;
    if (min < 1440) return `${Math.round(min / 60)} ساعة`;
    return `${Math.round(min / 1440)} يوم`;
  }
}
