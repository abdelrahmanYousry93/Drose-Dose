import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { AdminApi, FollowUp, ReissueLink, SessionRequestA } from '../../core/admin-api';
import { errorMessage, fmtDateTime, fmtMoney } from '../../core/format';
import { Lang, TranslatePipe, translate as tr, translateIn } from '../../core/i18n';

const choiceLabel = (c: string) => { const k = `followUps.choice.${c}`; const t = tr(k); return t === k ? c : t; };
/** Amount written for the student's language (independent of the admin's UI language). */
const moneyIn = (lang: Lang, amount: number, currency: string) =>
  `${new Intl.NumberFormat(lang === 'en' ? 'en-GB' : 'ar-EG', { maximumFractionDigits: 2 }).format(amount)} ${currency === 'EGP' ? translateIn(lang, 'common.egp') : currency}`;

/** Students who owe a top-up, refunds to send, and transfers to arrange (spec 6.4, decision D8). */
@Component({
  selector: 'dose-admin-followups',
  imports: [FormsModule, TranslatePipe],
  template: `
    <section class="page">
      <h1 class="page-title">{{ 'titles.adminFollowUps' | t }}</h1>
      <p class="muted small">{{ 'followUps.lead' | t }}</p>
      @if (error()) { <p class="alert alert--error" role="alert">{{ error() }}</p> }

      @for (g of groups(); track g.kind) {
        <h2 class="sub">{{ g.title }} <span class="badge badge--gray">{{ g.items.length }}</span></h2>
        @for (f of g.items; track f.bookingId) {
          <article class="dose-card item" [class.overdue]="f.overdue">
            <div class="row">
              <strong dir="ltr">{{ f.bookingReference }}</strong>
              @if (f.overdue) { <span class="badge badge--yellow">{{ 'followUps.overdue' | t }}</span> }
              @if (f.studentChoice) { <span class="badge">{{ choiceAr(f.studentChoice) }}</span> }
              @if (f.topUpProofStatus === 'Submitted') { <span class="badge badge--green">{{ 'followUps.proofReview' | t }}</span> }
            </div>
            <ul class="info-list small">
              <li><span>{{ 'admin.student' | t }}</span><span>{{ f.studentName }} · <span dir="ltr">{{ f.contactPhone }}</span></span></li>
              @if (f.guardianPhone) { <li><span>{{ 'admin.guardian' | t }}</span><span dir="ltr">{{ f.guardianPhone }}</span></li> }
              <li><span>{{ 'admin.session' | t }}</span><span>{{ f.sessionTitleAr }} — {{ dt(f.sessionStartUtc) }}</span></li>
              <li><span>{{ 'followUps.paid' | t }}</span><span>{{ money(f.paidAmount, f.currency) }}</span></li>
              @if (f.topUpAmount) { <li><span>{{ 'followUps.diff' | t }}</span><span>{{ money(f.topUpAmount, f.currency) }}</span></li> }
              @if (f.respondByUtc) { <li><span>{{ 'followUps.respondBy' | t }}</span><span>{{ dt(f.respondByUtc) }}</span></li> }
              @if (f.reason) { <li><span>{{ 'followUps.reason' | t }}</span><span>{{ f.reason }}</span></li> }
            </ul>
            <input class="note" [(ngModel)]="notes[f.bookingId]" [placeholder]="'followUps.notePh' | t" [attr.aria-label]="'availability.note' | t" />
            <div class="actions">
              <button type="button" class="dose-btn dose-btn--ghost small-btn" (click)="message(f)">{{ 'followUps.message' | t }}</button>
              @switch (f.kind) {
                @case ('TopUp') { <button type="button" class="dose-btn dose-btn--primary small-btn" (click)="act(f, 'waive')">{{ 'followUps.waive' | t }}</button> }
                @case ('Refund') { <button type="button" class="dose-btn dose-btn--primary small-btn" (click)="act(f, 'refunded')">{{ 'followUps.refunded' | t }}</button> }
                @case ('Transfer') { <button type="button" class="dose-btn dose-btn--primary small-btn" (click)="act(f, 'transfer')">{{ 'followUps.transferred' | t }}</button> }
              }
            </div>
            @if (msg()?.bookingId === f.bookingId) {
              <div class="link-box">
                <textarea readonly [value]="msg()!.text" [attr.aria-label]="'followUps.msgLabel' | t"></textarea>
                <div class="actions">
                  <button type="button" class="dose-btn dose-btn--primary small-btn" (click)="copy(msg()!.text)">{{ (copied() ? 'common.copied' : 'common.copy') | t }}</button>
                  @if (f.contactPhone) {
                    <a class="dose-btn dose-btn--whatsapp small-btn" [href]="'https://wa.me/' + digits(f.contactPhone)" target="_blank" rel="noopener">{{ 'followUps.openChat' | t }}</a>
                  }
                </div>
              </div>
            }
          </article>
        } @empty { <p class="muted small">{{ 'followUps.none' | t }}</p> }
      }
    </section>
  `,
  styles: `
    .sub { margin: 10px 0 0; font-size: 1.12rem; color: var(--dose-navy); display: flex; gap: 8px; align-items: center; }
    .row { display: flex; gap: 8px; align-items: center; flex-wrap: wrap; }
    .item { padding: 14px 16px; display: grid; gap: 10px; }
    .item.overdue { border-color: #f0b400; }
    .note { min-height: 40px; padding: 0 10px; border: 1px solid var(--dose-border); border-radius: 8px; font: inherit; }
    .actions { display: flex; flex-wrap: wrap; gap: 8px; }
    .small-btn { min-height: 38px; padding: 0 12px; font-size: 0.88rem; }
    .link-box { display: grid; gap: 8px; padding: 12px; border-radius: var(--dose-radius-sm); background: var(--dose-success-50); }
    .link-box textarea { min-height: 120px; padding: 8px; border: 1px solid var(--dose-border); border-radius: 8px; font: 0.9rem var(--dose-font); }
  `,
})
export class AdminFollowUps {
  private readonly api = inject(AdminApi);
  protected readonly items = signal<FollowUp[]>([]);
  protected readonly error = signal('');
  protected readonly msg = signal<{ bookingId: string; text: string } | null>(null);
  protected readonly copied = signal(false);
  protected notes: Record<string, string> = {};
  protected readonly dt = fmtDateTime;
  protected readonly money = fmtMoney;
  protected readonly choiceAr = choiceLabel;
  protected readonly digits = (p: string) => p.replace(/\D/g, '');
  protected readonly groups = computed(() => [
    { kind: 'TopUp', title: tr('followUps.groupTopUp'), items: this.items().filter((i) => i.kind === 'TopUp') },
    { kind: 'Refund', title: tr('followUps.groupRefund'), items: this.items().filter((i) => i.kind === 'Refund') },
    { kind: 'Transfer', title: tr('followUps.groupTransfer'), items: this.items().filter((i) => i.kind === 'Transfer') },
  ]);

  constructor() { void this.load(); }

  private async load() {
    try { this.items.set(await this.api.followUps()); this.error.set(''); } catch (e) { this.error.set(errorMessage(e)); }
  }

  protected async act(f: FollowUp, action: 'waive' | 'refunded' | 'transfer') {
    const note = this.notes[f.bookingId] ?? '';
    if (!confirm(tr(`followUps.confirm.${action}`))) return;
    try {
      if (action === 'waive') await this.api.waive(f.bookingId, note);
      if (action === 'refunded') await this.api.markRefunded(f.bookingId, note);
      if (action === 'transfer') await this.api.markTransferDone(f.bookingId, note);
      await this.load();
    } catch (e) { this.error.set(errorMessage(e)); }
  }

  /** Builds the WhatsApp message; top-up messages include a fresh secret link so the student can choose. */
  protected async message(f: FollowUp) {
    try {
      // Written in the language the student booked in.
      const lang: Lang = f.language === 'en' ? 'en' : 'ar';
      const p = {
        name: f.studentName ?? '', ref: f.bookingReference,
        title: lang === 'en' && f.sessionTitleEn ? f.sessionTitleEn : f.sessionTitleAr,
        amount: moneyIn(lang, f.topUpAmount ?? 0, f.currency), paid: moneyIn(lang, f.paidAmount, f.currency), url: '',
      };
      if (f.kind === 'TopUp') {
        if (!confirm(tr('followUps.reissueConfirm'))) return;
        const link: ReissueLink = await this.api.reissueLink(f.bookingId);
        p.url = `${window.location.origin}/b/${link.accessToken}`;
      }
      const text = translateIn(lang, `followUps.msg.${f.kind}`, p);
      this.msg.set({ bookingId: f.bookingId, text });
    } catch (e) { this.error.set(errorMessage(e)); }
  }

  protected async copy(text: string) {
    try { await navigator.clipboard.writeText(text); this.copied.set(true); setTimeout(() => this.copied.set(false), 2000); } catch { /* ignore */ }
  }
}

const reqStatus = (s: string) => { const k = `requests.st.${s}`; const t = tr(k); return t === k ? s : t; };

/** Student session requests (spec 7). Admin contacts the student and creates a session from the request. */
@Component({
  selector: 'dose-admin-requests',
  imports: [FormsModule, TranslatePipe],
  template: `
    <section class="page">
      <div class="row">
        <h1 class="page-title">{{ 'titles.adminRequests' | t }}</h1>
        <select [ngModel]="status()" (ngModelChange)="status.set($event); load()" [attr.aria-label]="'payments.status' | t">
          <option value="New">{{ 'requests.st.New' | t }}</option><option value="Contacted">{{ 'requests.st.Contacted' | t }}</option>
          <option value="Fulfilled">{{ 'requests.st.Fulfilled' | t }}</option><option value="Closed">{{ 'requests.st.Closed' | t }}</option><option value="">{{ 'sessions.all' | t }}</option>
        </select>
      </div>
      <p class="muted small">{{ 'requests.lead' | t }}</p>
      @if (error()) { <p class="alert alert--error" role="alert">{{ error() }}</p> }

      @for (r of items(); track r.id) {
        <article class="dose-card item">
          <div class="row">
            <strong>{{ r.subjectNameAr }} — {{ r.gradeNameAr }}</strong>
            <span class="badge" [class.badge--yellow]="r.status === 'New'" [class.badge--green]="r.status === 'Fulfilled'">{{ statusAr(r.status) }}</span>
          </div>
          <p>{{ r.scopeText }}</p>
          <ul class="info-list small">
            <li><span>{{ 'requests.ref' | t }}</span><span dir="ltr">{{ r.reference }}</span></li>
            <li><span>{{ 'booking.type' | t }}</span><span>{{ r.serviceTypeNameAr }} · {{ r.studyTrackNameAr }} · {{ r.mode === 'Group' ? ('pricing.group' | t) + (r.preferredTierNameAr ? ' (' + r.preferredTierNameAr + ')' : '') : ('pricing.private' | t) }}</span></li>
            <li><span>{{ 'requests.times' | t }}</span><span>{{ r.preferredDays }} — {{ r.preferredTimes }}</span></li>
            <li><span>{{ 'admin.student' | t }}</span><span>{{ r.studentName }} · <span dir="ltr">{{ r.contactPhone }}</span></span></li>
            @if (r.guardianPhone) { <li><span>{{ 'admin.guardian' | t }}</span><span>{{ r.guardianName }} · <span dir="ltr">{{ r.guardianPhone }}</span></span></li> }
            @if (r.notes) { <li><span>{{ 'request.notes' | t }}</span><span>{{ r.notes }}</span></li> }
            <li><span>{{ 'requests.sent' | t }}</span><span>{{ dt(r.createdAtUtc) }}</span></li>
          </ul>
          <input class="note" [(ngModel)]="r.adminNotes" [placeholder]="'requests.notesPh' | t" [attr.aria-label]="'requests.notesPh' | t" />
          <div class="actions">
            <a class="dose-btn dose-btn--whatsapp small-btn" [href]="'https://wa.me/' + digits(r.contactPhone)" target="_blank" rel="noopener">{{ 'requests.contact' | t }}</a>
            <button type="button" class="dose-btn dose-btn--primary small-btn" (click)="createSession(r)">{{ 'requests.createSession' | t }}</button>
            @if (r.status === 'New') { <button type="button" class="dose-btn dose-btn--ghost small-btn" (click)="set(r, 'Contacted')">{{ 'requests.markContacted' | t }}</button> }
            @if (r.status !== 'Closed') { <button type="button" class="dose-btn small-btn" (click)="set(r, 'Closed')">{{ 'requests.close' | t }}</button> }
          </div>
        </article>
      } @empty { <p class="muted">{{ 'requests.empty' | t }}</p> }
    </section>
  `,
  styles: `
    .row { display: flex; justify-content: space-between; align-items: center; gap: 8px; flex-wrap: wrap; }
    select, .note { min-height: 40px; padding: 0 10px; border: 1px solid var(--dose-border); border-radius: 8px; font: inherit; }
    .item { padding: 14px 16px; display: grid; gap: 10px; }
    .item p { margin: 0; }
    .actions { display: flex; flex-wrap: wrap; gap: 8px; }
    .small-btn { min-height: 38px; padding: 0 12px; font-size: 0.88rem; }
  `,
})
export class AdminRequests {
  private readonly api = inject(AdminApi);
  private readonly router = inject(Router);
  protected readonly items = signal<SessionRequestA[]>([]);
  protected readonly status = signal('New');
  protected readonly error = signal('');
  protected readonly dt = fmtDateTime;
  protected readonly statusAr = reqStatus;
  protected readonly digits = (p: string) => p.replace(/\D/g, '');

  constructor() { void this.load(); }

  protected async load() {
    try { this.items.set(await this.api.requests(this.status() || null)); this.error.set(''); } catch (e) { this.error.set(errorMessage(e)); }
  }

  protected async set(r: SessionRequestA, status: string) {
    try { await this.api.updateRequest(r.id, status, r.adminNotes); await this.load(); } catch (e) { this.error.set(errorMessage(e)); }
  }

  protected createSession(r: SessionRequestA) {
    void this.router.navigate(['/admin/sessions'], { state: { fromRequest: r } });
  }
}
