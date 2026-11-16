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
interface FunnelReport { overall: Overall; sources: SourceRow[]; promos: PromoRow[]; days: DayRow[]; engagement: Record<string, Record<string, number | null>> }

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
          <button type="button" [class.on]="days() === d" (click)="load(d)">{{ ('funnel.d' + d) | t }}</button>
        }
      </div>
      @if (error()) { <p class="alert alert--error" role="alert">{{ error() }}</p> }

      @if (data(); as r) {
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

        @if (posts().length) {
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
      }
    </section>
  `,
  styles: `
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

  protected readonly posts = computed(() => Object.entries(this.data()?.engagement ?? {}).map(([key, v]) => ({
    key: key.replace('|', ' · '),
    likes: (v['likes'] ?? v['reactions']) as number | null, comments: v['comments'] as number | null,
    shares: v['shares'] as number | null, reach: v['reach'] as number | null,
  })));

  constructor() { void this.load(30); }

  protected money = (n: number) => fmtMoney(n);

  protected async load(days: number) {
    this.days.set(days);
    this.error.set('');
    try {
      this.data.set(await firstValueFrom(this.http.get<FunnelReport>(`${environment.apiBaseUrl}/admin/funnel?days=${days}`)));
    } catch (e) {
      this.error.set(errorMessage(e));
    }
  }
}
