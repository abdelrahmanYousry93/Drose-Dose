import { Component, computed, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';
import { errorMessage, fmtMoney } from '../../core/format';
import { TranslatePipe } from '../../core/i18n';

interface Overall { visitors: number; bookingsStarted: number; detailsEntered: number; proofsSent: number; paid: number; requests: number; revenue: number }
interface SourceRow { source: string; visits: number; whatsApp: number; holds: number; requests: number; paid: number }
interface PromoRow { code: string; bookings: number; paid: number }
interface DayRow { day: string; started: number; paid: number }
interface EventRow { source: string; scans: number; answeredDose: number; answeredMonth: number; reachedChoices: number; pickedFund: number; pickedStudents: number; openedWhatsApp: number; called: number; sent: number; dosesPledged: number }
interface FunnelReport { overall: Overall; sources: SourceRow[]; promos: PromoRow[]; days: DayRow[] }
type Engagement = Record<string, Record<string, number | null>>;

/** The sales funnel at a glance: steps with conversion between them, sources, promo codes, post engagement, days. */
@Component({
  selector: 'dose-admin-funnel',
  imports: [TranslatePipe],
  template: `
    <section class="page">
      <h1 class="page-title">{{ 'titles.adminFunnel' | t }}</h1>
      <p class="muted small">{{ 'funnel.lead' | t }}</p>
      <div class="periods" role="group" [attr.aria-label]="'funnel.period' | t">
        @for (d of periods; track d) {
          <button type="button" [class.on]="days() === d" [attr.aria-pressed]="days() === d" (click)="load(d)">
            @if (loading() && days() === d) { <span class="spin" aria-hidden="true"></span> }
            {{ ('funnel.d' + d) | t }}
          </button>
        }
      </div>
      @if (error()) { <p class="alert alert--error" role="alert">{{ error() }}</p> }

      @if (loading() && !data()) {
        <div class="loading-note" role="status"><span class="spin"></span> {{ 'common.loading' | t }}</div>
        <div class="skeleton"></div><div class="skeleton"></div><div class="skeleton"></div>
      }
      @if (data(); as r) {
        <div class="report" [class.stale]="loading()" [attr.aria-busy]="loading()">
        @if (loading()) { <div class="loading-note sticky" role="status"><span class="spin"></span> {{ 'common.loading' | t }}</div> }
        <h2>{{ 'funnel.steps' | t }}</h2>
        <div class="steps">
          @for (s of steps(); track s.key; let i = $index) {
            <div class="step dose-card" [class.drop]="s.key === worstKey()">
              <div class="bar" [style.width.%]="s.width"></div>
              <div class="row">
                <span class="label">{{ ('funnel.' + s.key) | t }}</span>
                <span class="num">{{ s.value }}</span>
              </div>
              @if (i > 0 && s.rate !== null) { <span class="rate">{{ s.rate }}% {{ 'funnel.ofPrev' | t }}</span> }
              @if (s.key === worstKey()) { <span class="flag">⚠️ {{ 'funnel.drop' | t }}</span> }
            </div>
          }
        </div>
        <div class="kpis">
          <div class="dose-card kpi"><span>{{ 'funnel.revenue' | t }}</span><b>{{ money(r.overall.revenue) }}</b></div>
          <div class="dose-card kpi"><span>{{ 'funnel.requests' | t }}</span><b>{{ r.overall.requests }}</b></div>
        </div>

        <h2>{{ 'funnel.sources' | t }}</h2>
        <p class="muted small">{{ 'funnel.srcHelp' | t }}</p>
        @if (r.sources.length) {
          <div class="table-wrap"><table>
            <thead><tr><th>{{ 'funnel.source' | t }}</th><th>{{ 'funnel.visits' | t }}</th><th>{{ 'funnel.whatsapp' | t }}</th><th>{{ 'funnel.holds' | t }}</th><th>{{ 'funnel.reqs' | t }}</th><th>{{ 'funnel.paidCol' | t }}</th></tr></thead>
            <tbody>
              @for (s of r.sources; track s.source) {
                <tr><td dir="ltr">{{ s.source }}</td><td>{{ s.visits }}</td><td>{{ s.whatsApp }}</td><td>{{ s.holds }}</td><td>{{ s.requests }}</td><td><b>{{ s.paid }}</b></td></tr>
              }
            </tbody>
          </table></div>
        } @else { <p class="muted">{{ 'funnel.noSources' | t }}</p> }

        <h2>{{ 'funnel.promos' | t }}</h2>
        @if (r.promos.length) {
          <div class="table-wrap"><table>
            <thead><tr><th>{{ 'funnel.code' | t }}</th><th>{{ 'funnel.used' | t }}</th><th>{{ 'funnel.paidCol' | t }}</th></tr></thead>
            <tbody>@for (p of r.promos; track p.code) { <tr><td dir="ltr">{{ p.code }}</td><td>{{ p.bookings }}</td><td><b>{{ p.paid }}</b></td></tr> }</tbody>
          </table></div>
        } @else { <p class="muted">{{ 'funnel.noPromos' | t }}</p> }

        @if (engLoading()) {
          <h2>{{ 'funnel.posts' | t }}</h2>
          <div class="loading-note" role="status"><span class="spin"></span> {{ 'common.loading' | t }}</div>
          <div class="skeleton short"></div>
        } @else if (posts().length) {
          <h2>{{ 'funnel.posts' | t }}</h2>
          <div class="table-wrap"><table>
            <thead><tr><th>{{ 'funnel.post' | t }}</th><th>{{ 'funnel.likes' | t }}</th><th>{{ 'funnel.comments' | t }}</th><th>{{ 'funnel.shares' | t }}</th><th>{{ 'funnel.reach' | t }}</th></tr></thead>
            <tbody>
              @for (p of posts(); track p.key) {
                <tr><td dir="ltr">{{ p.key }}</td><td>{{ p.likes ?? '—' }}</td><td>{{ p.comments ?? '—' }}</td><td>{{ p.shares ?? '—' }}</td><td>{{ p.reach ?? '—' }}</td></tr>
              }
            </tbody>
          </table></div>
        }

        @if (r.days.length) {
          <h2>{{ 'funnel.days' | t }}</h2>
          <div class="table-wrap"><table>
            <thead><tr><th>{{ 'funnel.day' | t }}</th><th>{{ 'funnel.holds' | t }}</th><th>{{ 'funnel.paidCol' | t }}</th></tr></thead>
            <tbody>@for (d of r.days; track d.day) { <tr><td dir="ltr">{{ d.day }}</td><td>{{ d.started }}</td><td><b>{{ d.paid }}</b></td></tr> }</tbody>
          </table></div>
        }
        </div>
      }
      @if (eventLoading()) {
        <div class="dose-card box"><h2>{{ 'funnel.event' | t }}</h2><div class="skeleton short"></div></div>
      } @else if (event().length) {
        <div class="dose-card box">
          <h2>{{ 'funnel.event' | t }}</h2>
          <p class="muted small">{{ 'funnel.eventLead' | t }}</p>
          <div class="table-wrap"><table class="ev">
            <thead><tr><th>{{ 'funnel.card' | t }}</th><th>{{ 'funnel.scans' | t }}</th><th>💊</th><th>📅</th><th>{{ 'funnel.choices' | t }}</th><th>🤝</th><th>🏫</th><th>💬</th><th>📞</th><th>{{ 'funnel.sent' | t }}</th><th>{{ 'funnel.doses' | t }}</th></tr></thead>
            <tbody>
              @for (r of event(); track r.source) {
                <tr><td dir="ltr">{{ r.source }}</td><td><b>{{ r.scans }}</b></td><td>{{ r.answeredDose }}</td><td>{{ r.answeredMonth }}</td><td>{{ r.reachedChoices }}</td><td>{{ r.pickedFund }}</td><td>{{ r.pickedStudents }}</td><td>{{ r.openedWhatsApp }}</td><td>{{ r.called }}</td><td><b>{{ r.sent }}</b></td><td>{{ r.dosesPledged }}</td></tr>
              }
              <tr class="tot"><td>{{ 'funnel.total' | t }}</td><td>{{ sum('scans') }}</td><td>{{ sum('answeredDose') }}</td><td>{{ sum('answeredMonth') }}</td><td>{{ sum('reachedChoices') }}</td><td>{{ sum('pickedFund') }}</td><td>{{ sum('pickedStudents') }}</td><td>{{ sum('openedWhatsApp') }}</td><td>{{ sum('called') }}</td><td>{{ sum('sent') }}</td><td>{{ sum('dosesPledged') }}</td></tr>
            </tbody>
          </table></div>
        </div>
      }
    </section>
  `,
  styles: `
    .report { position: relative; transition: opacity .15s; }
    .report.stale > :not(.loading-note) { opacity: .3; pointer-events: none; }
    .loading-note { display: flex; align-items: center; gap: 8px; font-weight: 700; color: var(--dose-navy); margin: 12px 0; }
    .loading-note.sticky { position: sticky; top: 8px; z-index: 2; background: #fff; border-radius: 999px; padding: 8px 16px; width: fit-content; box-shadow: 0 4px 16px rgb(11 31 107 / .15); }
    .spin { width: 16px; height: 16px; border-radius: 50%; border: 2.5px solid currentColor; border-inline-end-color: transparent; display: inline-block; animation: spin .7s linear infinite; vertical-align: -3px; }
    @keyframes spin { to { transform: rotate(360deg); } }
    @media (prefers-reduced-motion: reduce) { .spin { animation-duration: 2s; } }
    .skeleton { margin-bottom: 8px; } .skeleton.short { height: 60px; }
    .table-wrap { overflow-x: auto; }
    table.ev { border-collapse: collapse; width: 100%; font-size: .85rem; white-space: nowrap; }
    table.ev th, table.ev td { padding: 6px 8px; border-bottom: 1px solid var(--dose-border); text-align: center; }
    table.ev tr.tot td { font-weight: 800; border-top: 2px solid var(--dose-navy); }

    .periods { display: flex; flex-wrap: wrap; gap: 6px; margin: 12px 0 4px; }
    .periods button { padding: 8px 14px; border-radius: 999px; border: 1px solid var(--dose-border, #d0d5dd); background: #fff; font: 600 0.92rem var(--dose-font); cursor: pointer; }
    .periods button.on { background: var(--dose-navy); color: #fff; border-color: var(--dose-navy); }
    h2 { margin: 24px 0 10px; font-size: 1.15rem; }
    .steps { display: grid; gap: 8px; }
    .step { position: relative; overflow: hidden; padding: 12px 16px; }
    .step .bar { position: absolute; inset-block: 0; inset-inline-start: 0; background: color-mix(in srgb, var(--dose-primary) 14%, transparent); }
    .step .row { position: relative; display: flex; justify-content: space-between; align-items: baseline; gap: 12px; }
    .step .label { font-weight: 700; }
    .step .num { font-weight: 800; font-size: 1.4rem; color: var(--dose-navy); }
    .step .rate, .step .flag { position: relative; font-size: 0.85rem; color: var(--dose-text-muted); margin-inline-end: 10px; }
    .step.drop { outline: 2px solid #f79009; }
    .step .flag { color: #b54708; font-weight: 700; }
    .kpis { display: grid; grid-template-columns: repeat(auto-fit, minmax(180px, 1fr)); gap: 8px; margin-top: 8px; }
    .kpi { padding: 12px 16px; display: flex; flex-direction: column; gap: 4px; }
    .kpi b { font-size: 1.3rem; color: var(--dose-navy); }
    .table-wrap { overflow-x: auto; }
    table { width: 100%; border-collapse: collapse; background: #fff; border-radius: 12px; overflow: hidden; }
    th, td { padding: 8px 10px; text-align: start; border-bottom: 1px solid #eaecf0; font-size: 0.92rem; white-space: nowrap; }
    th { background: #f2f4f7; font-weight: 700; }
  `,
})
export class AdminFunnel {
  private readonly http = inject(HttpClient);
  protected readonly periods = [1, 7, 30, 90];
  protected readonly days = signal(30);
  protected readonly data = signal<FunnelReport | null>(null);
  protected readonly error = signal('');
  protected readonly loading = signal(false);
  protected readonly engagement = signal<Engagement>({});
  protected readonly engLoading = signal(true);
  protected readonly eventLoading = signal(true);
  private seq = 0;

  protected readonly steps = computed(() => {
    const o = this.data()?.overall;
    if (!o) return [];
    const raw: [string, number][] = [['visitors', o.visitors], ['started', o.bookingsStarted], ['details', o.detailsEntered], ['proofs', o.proofsSent], ['paid', o.paid]];
    const max = Math.max(1, ...raw.map(([, v]) => v));
    return raw.map(([key, value], i) => ({
      key, value, width: (value / max) * 100,
      rate: i > 0 && raw[i - 1][1] > 0 ? Math.round((value / raw[i - 1][1]) * 100) : null,
    }));
  });

  /** The step (after the first) that keeps the smallest share of the one before it. */
  protected readonly worstKey = computed(() => {
    const s = this.steps().filter((x, i) => i > 1 && x.rate !== null);
    return s.length ? s.reduce((a, b) => ((b.rate ?? 100) < (a.rate ?? 100) ? b : a)).key : '';
  });

  protected readonly posts = computed(() => Object.entries(this.engagement()).map(([key, v]) => ({
    key: key.replace('|', ' · '),
    likes: (v['likes'] ?? v['reactions']) as number | null, comments: v['comments'] as number | null,
    shares: v['shares'] as number | null, reach: v['reach'] as number | null,
  })));

  protected readonly event = signal<EventRow[]>([]);
  protected sum(k: keyof EventRow) { return this.event().reduce((a, r) => a + (r[k] as number), 0); }

  constructor() {
    void this.load(30);
    firstValueFrom(this.http.get<EventRow[]>(`${environment.apiBaseUrl}/admin/funnel/event?prefix=ses26`))
      .then((r) => this.event.set(r)).catch(() => undefined).finally(() => this.eventLoading.set(false));
    // Engagement is read live from Facebook/Instagram (slow on a cold cache): it fills in on its own.
    firstValueFrom(this.http.get<Engagement>(`${environment.apiBaseUrl}/admin/funnel/engagement`))
      .then((r) => this.engagement.set(r ?? {})).catch(() => undefined).finally(() => this.engLoading.set(false));
  }

  protected money = (n: number) => fmtMoney(n);

  protected async load(days: number) {
    // Only the latest click wins: a slower answer for an older period never replaces the current one.
    const mine = ++this.seq;
    this.days.set(days);
    this.error.set('');
    this.loading.set(true);
    try {
      const r = await firstValueFrom(this.http.get<FunnelReport>(`${environment.apiBaseUrl}/admin/funnel?days=${days}`));
      if (mine === this.seq) this.data.set(r);
    } catch (e) {
      if (mine === this.seq) { this.data.set(null); this.error.set(errorMessage(e)); }
    } finally {
      if (mine === this.seq) this.loading.set(false);
    }
  }
}
