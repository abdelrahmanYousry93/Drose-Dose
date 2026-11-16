import { Component, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';
import { errorMessage } from '../../core/format';
import { TranslatePipe } from '../../core/i18n';

interface Pledge {
  id: string; reference: string; organization: string; contactName: string; contactPhone: string; role: string | null;
  interest: string; doses: number; showOnWall: boolean; hidden: boolean; note: string | null; source: string | null; createdAtUtc: string;
}

/** Pledges from the partners page, newest first, with WhatsApp links and a switch to hide one from the counter. */
@Component({
  selector: 'dose-admin-partners',
  imports: [TranslatePipe, RouterLink],
  template: `
    <section class="page">
      <h1 class="page-title">{{ 'titles.adminPartners' | t }}</h1>
      <p class="muted small">{{ 'adminPartners.lead' | t }} <a routerLink="/partners/wall" target="_blank">{{ 'adminPartners.wallLink' | t }}</a></p>
      @if (error()) { <p class="alert alert--error" role="alert">{{ error() }}</p> }
      @if (rows(); as list) {
        @if (!list.length) { <p class="muted">{{ 'adminPartners.empty' | t }}</p> }
        @for (p of list; track p.id) {
          <article class="dose-card row" [class.off]="p.hidden">
            <div class="top">
              <strong>{{ p.organization }}</strong>
              @if (p.doses > 0) { <span class="doses">💊 {{ p.doses }}</span> }
              <span class="tag">{{ ('partners.i.' + p.interest) | t }}</span>
              <span class="muted small" dir="ltr">{{ p.reference }} · {{ when(p.createdAtUtc) }}{{ p.source ? ' · ' + p.source : '' }}</span>
            </div>
            <div>{{ p.contactName }}{{ p.role ? ' — ' + p.role : '' }} · <a [href]="wa(p.contactPhone)" target="_blank" rel="noopener" dir="ltr">{{ p.contactPhone }}</a></div>
            @if (p.note) { <p class="note">{{ p.note }}</p> }
            @if (p.doses > 0) {
              <div class="foot">
                <span class="muted small">{{ 'adminPartners.onWall' | t }}: {{ (p.hidden ? 'adminPartners.hidden' : p.showOnWall ? 'adminPartners.yes' : 'adminPartners.no') | t }}</span>
                <button type="button" class="dose-btn dose-btn--ghost" (click)="toggle(p)">{{ (p.hidden ? 'adminPartners.show' : 'adminPartners.hide') | t }}</button>
              </div>
            }
          </article>
        }
      }
    </section>
  `,
  styles: `
    .row { padding: 14px 16px; display: grid; gap: 6px; }
    .row.off { opacity: .55; }
    .top { display: flex; flex-wrap: wrap; gap: 8px; align-items: center; }
    .doses { font-weight: 800; color: var(--dose-success); }
    .tag { background: var(--dose-primary-50); color: var(--dose-primary); border-radius: 999px; padding: 2px 10px; font-size: .8rem; font-weight: 700; }
    .note { margin: 0; white-space: pre-wrap; color: var(--dose-text-muted); }
    .foot { display: flex; justify-content: space-between; align-items: center; gap: 8px; }
    .small { font-size: .85rem; }
  `,
})
export class AdminPartners {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/admin/partners`;
  protected readonly rows = signal<Pledge[] | null>(null);
  protected readonly error = signal('');

  constructor() { this.load(); }

  private async load() {
    try { this.rows.set(await firstValueFrom(this.http.get<Pledge[]>(this.base))); }
    catch (e) { this.error.set(errorMessage(e)); }
  }

  protected async toggle(p: Pledge) {
    try {
      await firstValueFrom(this.http.post(`${this.base}/${p.id}/hidden`, null, { params: { hidden: String(!p.hidden) } }));
      await this.load();
    } catch (e) { this.error.set(errorMessage(e)); }
  }

  protected wa(phone: string) { return `https://wa.me/${phone.replace(/\D/g, '')}`; }
  protected when(utc: string) {
    return new Date(utc).toLocaleString('ar-EG', { timeZone: 'Africa/Cairo', day: 'numeric', month: 'short', hour: 'numeric', minute: '2-digit' });
  }
}
