import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiError } from '../../core/api';
import { errorMessage, fmtDate, fmtDateTime } from '../../core/format';
import { TranslatePipe, translate } from '../../core/i18n';

interface Lookup {
  phone: string; name: string | null;
  bookings: { reference: string; status: string; subject: string; scope: string; startUtc: string; createdAtUtc: string }[];
  requests: { reference: string; status: string; subject: string; grade: string; scope: string; createdAtUtc: string }[];
  links: { createdAtUtc: string; expiresAtUtc: string; uses: number; lastUsedAtUtc: string | null; createdBy: string }[];
}
interface Issued { link: string; message: string; chatUrl: string; expiresAtUtc: string }

/** A student asked on WhatsApp for their bookings: look the number up and send back one private restore link. */
@Component({
  selector: 'dose-admin-students',
  imports: [FormsModule, TranslatePipe],
  template: `
    <section class="page">
      <h1 class="page-title">{{ 'titles.adminStudents' | t }}</h1>
      <p class="muted small">{{ 'adminStudents.lead' | t }}</p>
      <form class="dose-card box row" (ngSubmit)="find()">
        <label class="field grow"><span>{{ 'adminStudents.phone' | t }}</span>
          <input name="phone" [(ngModel)]="phone" dir="ltr" inputmode="tel" autocomplete="off" placeholder="01xxxxxxxxx" /></label>
        <button class="dose-btn dose-btn--primary" type="submit" [disabled]="busy()">{{ 'adminStudents.find' | t }}</button>
      </form>
      @if (error()) { <p class="alert alert--error" role="alert">{{ error() }}</p> }

      @if (data(); as d) {
        <div class="dose-card box">
          <strong dir="ltr" class="phone">{{ d.phone }}</strong>@if (d.name) { <span> — {{ d.name }}</span> }
          @if (!d.bookings.length && !d.requests.length) {
            <p class="muted">{{ 'adminStudents.none' | t }}</p>
          } @else {
            @if (d.bookings.length) {
              <h2>{{ 'adminStudents.bookings' | t }} ({{ d.bookings.length }})</h2>
              <ul class="list">
                @for (b of d.bookings; track b.reference) {
                  <li><span dir="ltr">{{ b.reference }}</span> · {{ b.subject }} — {{ b.scope }} · {{ dateTime(b.startUtc) }} · <b>{{ ('status.' + b.status) | t }}</b></li>
                }
              </ul>
            }
            @if (d.requests.length) {
              <h2>{{ 'adminStudents.requests' | t }} ({{ d.requests.length }})</h2>
              <ul class="list">
                @for (r of d.requests; track r.reference) {
                  <li><span dir="ltr">{{ r.reference }}</span> · {{ r.subject }} — {{ r.grade }} · {{ r.scope }} · {{ date(r.createdAtUtc) }}</li>
                }
              </ul>
            }
            @if (!issued()) {
              <button type="button" class="dose-btn dose-btn--primary" (click)="issue()" [disabled]="busy()">{{ 'adminStudents.issue' | t }}</button>
            }
          }
          @if (issued(); as i) {
            <div class="issued">
              <p class="muted small">{{ 'adminStudents.steps' | t }}</p>
              <pre class="msg">{{ i.message }}</pre>
              <div class="btns">
                <button type="button" class="dose-btn dose-btn--primary" (click)="copy(i.message)">{{ (copied() ? 'adminStudents.copied' : 'adminStudents.copy') | t }}</button>
                <a class="dose-btn dose-btn--whatsapp" [href]="i.chatUrl" target="_blank" rel="noopener">{{ 'adminStudents.openChat' | t }}</a>
              </div>
              <p class="muted small">{{ 'adminStudents.expires' | t: { date: date(i.expiresAtUtc) } }}</p>
            </div>
          }
          @if (d.links.length) {
            <h2>{{ 'adminStudents.links' | t }}</h2>
            <ul class="list muted small">
              @for (l of d.links; track l.createdAtUtc) {
                <li>{{ dateTime(l.createdAtUtc) }} · {{ l.createdBy }} · {{ 'adminStudents.used' | t: { n: l.uses } }}</li>
              }
            </ul>
          }
        </div>
      }
    </section>
  `,
  styles: `
    .box { padding: 16px; display: grid; gap: 10px; }
    .row { grid-template-columns: 1fr auto; align-items: end; }
    .grow { min-width: 0; }
    h2 { margin: 6px 0 0; font-size: 1rem; color: var(--dose-navy); }
    .list { margin: 0; padding-inline-start: 18px; display: grid; gap: 4px; }
    .phone { font-size: 1.1rem; }
    .issued { display: grid; gap: 8px; border-top: 1px dashed var(--dose-border); padding-top: 10px; }
    .msg { white-space: pre-wrap; word-break: break-all; background: var(--dose-bg); border-radius: var(--dose-radius-sm); padding: 12px; margin: 0; font-family: inherit; direction: rtl; }
    .btns { display: flex; flex-wrap: wrap; gap: 8px; }
    .small { font-size: .85rem; }
  `,
})
export class AdminStudents {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/admin/students`;
  protected phone = '';
  protected readonly data = signal<Lookup | null>(null);
  protected readonly issued = signal<Issued | null>(null);
  protected readonly error = signal('');
  protected readonly busy = signal(false);
  protected readonly copied = signal(false);

  protected async find() {
    if (!this.phone.trim()) return;
    await this.run(async () => {
      this.issued.set(null);
      this.data.set(await firstValueFrom(this.http.get<Lookup>(`${this.base}/lookup`, { params: { phone: this.phone.trim() } })));
    });
  }

  protected async issue() {
    const d = this.data();
    if (!d) return;
    await this.run(async () => {
      this.issued.set(await firstValueFrom(this.http.post<Issued>(`${this.base}/access-link`, { phone: d.phone })));
      this.data.set(await firstValueFrom(this.http.get<Lookup>(`${this.base}/lookup`, { params: { phone: d.phone } })));
    });
  }

  protected async copy(text: string) {
    try { await navigator.clipboard.writeText(text); this.copied.set(true); setTimeout(() => this.copied.set(false), 2000); }
    catch { /* the text is selectable too */ }
  }

  private async run(f: () => Promise<void>) {
    this.busy.set(true); this.error.set('');
    try { await f(); }
    catch (e) {
      const body = (e as { error?: { title?: string } })?.error;
      this.error.set(body?.title ?? (e instanceof ApiError ? errorMessage(e) : translate('errors.generic')));
    } finally { this.busy.set(false); }
  }

  protected date(utc: string) { return fmtDate(utc); }
  protected dateTime(utc: string) { return fmtDateTime(utc); }
}
