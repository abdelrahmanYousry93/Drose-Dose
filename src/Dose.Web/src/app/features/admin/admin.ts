import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { CanActivateFn, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { Api, ApiError, PaymentProofAdmin, Setting } from '../../core/api';
import { errorMessage, fmtDateTime, fmtMoney } from '../../core/format';
import { TranslatePipe, translate } from '../../core/i18n';

/** Route guard: admin area requires an authenticated Admin (checked server-side on every call too). */
export const adminGuard: CanActivateFn = async () => {
  const api = inject(Api);
  const router = inject(Router);
  try {
    const me = await api.me();
    return me.roles.includes('Admin') ? true : router.parseUrl('/login');
  } catch {
    return router.parseUrl('/login');
  }
};

@Component({
  selector: 'dose-admin-login',
  imports: [FormsModule, TranslatePipe],
  template: `
    <section class="page narrow">
      <form class="dose-card box" (ngSubmit)="login()">
        <h1 class="page-title">{{ 'titles.login' | t }}</h1>
        <p class="muted small">{{ 'login.hint' | t }}</p>
        @if (error()) { <p class="alert alert--error">{{ error() }}</p> }
        <label class="field"><span>{{ 'login.email' | t }}</span>
          <input name="email" type="email" [(ngModel)]="email" required dir="ltr" autocomplete="username" /></label>
        <label class="field"><span>{{ 'login.password' | t }}</span>
          <input name="password" type="password" [(ngModel)]="password" required dir="ltr" autocomplete="current-password" /></label>
        <button class="dose-btn dose-btn--primary" type="submit" [disabled]="busy()">{{ 'login.submit' | t }}</button>
      </form>
    </section>
  `,
  styles: `.narrow { max-width: 420px; margin: 0 auto; } .box { padding: 20px; display: grid; gap: 14px; }`,
})
export class AdminLogin {
  private readonly api = inject(Api);
  private readonly router = inject(Router);
  protected email = '';
  protected password = '';
  protected readonly error = signal('');
  protected readonly busy = signal(false);

  protected async login() {
    this.busy.set(true);
    this.error.set('');
    try {
      await this.api.login(this.email, this.password);
      const me = await this.api.me();
      await this.router.navigateByUrl(me.roles.includes('Admin') ? '/admin' : me.roles.includes('Teacher') ? '/teacher' : '/');
    } catch (e) {
      this.error.set(e instanceof ApiError && e.status === 401 ? translate('login.invalid') : errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }
}

@Component({
  selector: 'dose-admin-shell',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, TranslatePipe],
  template: `
    <nav class="admin-nav">
      <a routerLink="/admin/dashboard" routerLinkActive="on">{{ 'adminNav.dashboard' | t }}</a>
      <a routerLink="/admin/payments" routerLinkActive="on">{{ 'adminNav.payments' | t }}</a>
      <a routerLink="/admin/follow-ups" routerLinkActive="on">{{ 'adminNav.followUps' | t }}</a>
      <a routerLink="/admin/messages" routerLinkActive="on">{{ 'adminNav.messages' | t }}</a>
      <a routerLink="/admin/requests" routerLinkActive="on">{{ 'adminNav.requests' | t }}</a>
      <a routerLink="/admin/sessions" routerLinkActive="on">{{ 'adminNav.sessions' | t }}</a>
      <a routerLink="/admin/pricing" routerLinkActive="on">{{ 'adminNav.pricing' | t }}</a>
      <a routerLink="/admin/teachers" routerLinkActive="on">{{ 'adminNav.teachers' | t }}</a>
      <a routerLink="/admin/catalog" routerLinkActive="on">{{ 'adminNav.catalog' | t }}</a>
      <a routerLink="/admin/payment-methods" routerLinkActive="on">{{ 'adminNav.paymentMethods' | t }}</a>
      <a routerLink="/admin/availability" routerLinkActive="on">{{ 'adminNav.availability' | t }}</a>
      <a routerLink="/admin/users" routerLinkActive="on">{{ 'adminNav.users' | t }}</a>
      <a routerLink="/admin/settings" routerLinkActive="on">{{ 'adminNav.settings' | t }}</a>
      <button type="button" (click)="logout()">{{ 'adminNav.logout' | t }}</button>
    </nav>
    <router-outlet />
  `,
  styles: `
    .admin-nav { display: flex; flex-wrap: wrap; gap: 4px; margin-top: 16px; padding: 4px; border-radius: var(--dose-radius-sm); background: var(--dose-navy); }
    .admin-nav a, .admin-nav button { flex: 1 0 auto; text-align: center; padding: 8px 10px; border-radius: 8px; color: #fff; text-decoration: none; font: 600 0.92rem var(--dose-font); background: none; border: 0; cursor: pointer; }
    .admin-nav a.on { background: var(--dose-primary); }
  `,
})
export class AdminShell {
  private readonly api = inject(Api);
  private readonly router = inject(Router);
  protected async logout() {
    await this.api.logout().catch(() => undefined);
    await this.router.navigateByUrl('/login');
  }
}

@Component({
  selector: 'dose-admin-payments',
  imports: [FormsModule, TranslatePipe],
  template: `
    <section class="page">
      <div class="row">
        <h1 class="page-title">{{ 'titles.adminPayments' | t }}</h1>
        <select [ngModel]="status()" (ngModelChange)="status.set($event); load()" [attr.aria-label]="'payments.status' | t">
          <option value="Submitted">{{ 'payments.st.Submitted' | t }}</option>
          <option value="Approved">{{ 'payments.filterApproved' | t }}</option>
          <option value="Rejected">{{ 'payments.filterRejected' | t }}</option>
          <option value="">{{ 'sessions.all' | t }}</option>
        </select>
      </div>
      @if (error()) { <p class="alert alert--error">{{ error() }}</p> }
      @if (proofs().length === 0 && !loading()) { <p class="muted">{{ 'payments.empty' | t }}</p> }

      @for (p of proofs(); track p.id) {
        <article class="dose-card item" [class.late]="p.status === 'Submitted' && p.ageMinutes > 60">
          <div class="row">
            <strong dir="ltr">{{ p.bookingReference }} @if (p.isTopUp) { <span class="badge">{{ 'payments.topUp' | t }}</span> }</strong>
            <span class="badge" [class.badge--yellow]="p.status === 'Submitted'" [class.badge--green]="p.status === 'Approved'"
                  [class.badge--gray]="p.status === 'Rejected'">{{ ('payments.st.' + p.status) | t }}</span>
          </div>
          <ul class="info-list small">
            <li><span>{{ 'admin.student' | t }}</span><span>{{ p.studentName }}</span></li>
            <li><span>{{ 'admin.whatsapp' | t }}</span><span dir="ltr">{{ p.contactPhone }}</span></li>
            @if (p.guardianPhone) { <li><span>{{ 'admin.guardian' | t }}</span><span dir="ltr">{{ p.guardianPhone }}</span></li> }
            <li><span>{{ 'admin.session' | t }}</span><span>{{ p.sessionTitleAr }} — {{ dt(p.sessionStartUtc) }}</span></li>
            <li><span>{{ 'booking.amount' | t }}</span><span>{{ money(p.amount, p.currency) }} · {{ p.methodNameAr }}</span></li>
            @if (p.senderReference) { <li><span>{{ 'payments.senderRef' | t }}</span><span dir="ltr">{{ p.senderReference }}</span></li> }
            <li><span>{{ 'payments.age' | t }}</span><span>{{ 'duration.minutes' | t: { n: round(p.ageMinutes) } }}</span></li>
          </ul>
          <button type="button" class="dose-btn dose-btn--ghost" (click)="toggleImage(p.id)">
            {{ (openImage() === p.id ? 'payments.hide' : 'payments.show') | t }}
          </button>
          @if (openImage() === p.id) { <img [src]="fileUrl(p.id)" [alt]="'payments.image' | t" class="proof" /> }

          @if (p.status === 'Submitted') {
            <div class="actions">
              <button type="button" class="dose-btn dose-btn--primary" (click)="approve(p)" [disabled]="busy()">{{ 'payments.approve' | t }}</button>
              <input [(ngModel)]="reasons[p.id]" [placeholder]="'payments.reasonPh' | t" [attr.aria-label]="'payments.reason' | t" />
              <button type="button" class="dose-btn reject" (click)="reject(p)" [disabled]="busy()">{{ 'payments.reject' | t }}</button>
            </div>
          }
        </article>
      }
    </section>
  `,
  styles: `
    .row { display: flex; justify-content: space-between; align-items: center; gap: 8px; flex-wrap: wrap; }
    select, .actions input { min-height: 42px; padding: 0 10px; border: 1px solid var(--dose-border); border-radius: 8px; font: inherit; }
    .item { padding: 16px; display: grid; gap: 10px; }
    .item.late { border-color: #f0b400; }
    .proof { max-width: 100%; max-height: 520px; border-radius: 8px; border: 1px solid var(--dose-border); justify-self: center; }
    .actions { display: grid; gap: 8px; }
    .reject { background: #fdecea; color: #a52714; }
    @media (min-width: 760px) { .actions { grid-template-columns: auto 1fr auto; } }
  `,
})
export class AdminPayments {
  private readonly api = inject(Api);
  protected readonly proofs = signal<PaymentProofAdmin[]>([]);
  protected readonly status = signal('Submitted');
  protected readonly error = signal('');
  protected readonly loading = signal(true);
  protected readonly busy = signal(false);
  protected readonly openImage = signal<string | null>(null);
  protected reasons: Record<string, string> = {};
  protected readonly dt = fmtDateTime;
  protected readonly money = fmtMoney;

  constructor() { void this.load(); }

  protected round(n: number) { return Math.round(n); }
  protected fileUrl(id: string) { return this.api.proofFileUrl(id); }
  protected toggleImage(id: string) { this.openImage.set(this.openImage() === id ? null : id); }

  protected async load() {
    this.loading.set(true);
    try {
      this.proofs.set(await this.api.proofs(this.status() || null));
      this.error.set('');
    } catch (e) {
      this.error.set(errorMessage(e));
    } finally {
      this.loading.set(false);
    }
  }

  protected approve(p: PaymentProofAdmin) { return this.act(() => this.api.approve(p.id)); }
  protected reject(p: PaymentProofAdmin) { return this.act(() => this.api.reject(p.id, this.reasons[p.id] ?? '')); }

  private async act(fn: () => Promise<unknown>) {
    this.busy.set(true);
    try {
      await fn();
      await this.load();
    } catch (e) {
      this.error.set(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }
}

@Component({
  selector: 'dose-admin-settings',
  imports: [FormsModule, TranslatePipe],
  template: `
    <section class="page">
      <h1 class="page-title">{{ 'titles.adminSettings' | t }}</h1>
      <p class="muted">{{ 'settings.lead' | t }}</p>
      @if (error()) { <p class="alert alert--error">{{ error() }}</p> }
      @if (saved()) { <p class="alert alert--ok">{{ 'settings.saved' | t }}</p> }
      <form class="dose-card box" (ngSubmit)="save()">
        @for (s of items(); track s.key) {
          <label class="field">
            <span>{{ settingLabel(s) }}</span>
            <input type="number" min="1" [name]="s.key" [(ngModel)]="s.value" dir="ltr" />
          </label>
        }
        <button class="dose-btn dose-btn--primary" type="submit" [disabled]="busy()">{{ 'common.save' | t }}</button>
      </form>
    </section>
  `,
  styles: `.box { padding: 16px; display: grid; gap: 14px; }`,
})
export class AdminSettings {
  private readonly api = inject(Api);
  protected readonly items = signal<Setting[]>([]);
  protected readonly error = signal('');
  protected readonly saved = signal(false);
  protected readonly busy = signal(false);

  constructor() { void this.load(); }

  protected settingLabel(s: Setting) {
    const key = `settings.keys.${s.key.replace(/\./g, '_')}`;
    const t = translate(key);
    return t !== key ? t : (s.descriptionAr ?? s.key);
  }

  private async load() {
    try {
      const s = await this.api.settings();
      this.items.set(s.map((x) => ({ ...x })));
    } catch (e) {
      this.error.set(errorMessage(e));
    }
  }

  protected async save() {
    this.busy.set(true);
    this.error.set('');
    this.saved.set(false);
    try {
      await this.api.saveSettings(this.items().map((s) => ({ ...s, value: String(s.value) })));
      this.saved.set(true);
      await this.load();
    } catch (e) {
      this.error.set(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }
}
