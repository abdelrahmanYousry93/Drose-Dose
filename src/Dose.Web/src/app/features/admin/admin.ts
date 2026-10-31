import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { CanActivateFn, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { Api, PaymentProofAdmin, Setting } from '../../core/api';
import { errorMessage, fmtDateTime, fmtMoney } from '../../core/format';

/** Route guard: admin area requires an authenticated Admin (checked server-side on every call too). */
export const adminGuard: CanActivateFn = async () => {
  const api = inject(Api);
  const router = inject(Router);
  try {
    const me = await api.me();
    return me.roles.includes('Admin') ? true : router.parseUrl('/admin/login');
  } catch {
    return router.parseUrl('/admin/login');
  }
};

@Component({
  selector: 'dose-admin-login',
  imports: [FormsModule],
  template: `
    <section class="page narrow">
      <form class="dose-card box" (ngSubmit)="login()">
        <h1 class="page-title">دخول الإدارة</h1>
        @if (error()) { <p class="alert alert--error">{{ error() }}</p> }
        <label class="field"><span>البريد الإلكتروني</span>
          <input name="email" type="email" [(ngModel)]="email" required dir="ltr" autocomplete="username" /></label>
        <label class="field"><span>كلمة السر</span>
          <input name="password" type="password" [(ngModel)]="password" required dir="ltr" autocomplete="current-password" /></label>
        <button class="dose-btn dose-btn--primary" type="submit" [disabled]="busy()">دخول</button>
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
      await this.router.navigateByUrl('/admin/payments');
    } catch (e) {
      this.error.set(errorMessage(e) === 'لازم تسجّل دخول.' ? 'بيانات الدخول غير صحيحة.' : errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }
}

@Component({
  selector: 'dose-admin-shell',
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  template: `
    <nav class="admin-nav">
      <a routerLink="/admin/payments" routerLinkActive="on">مراجعة المدفوعات</a>
      <a routerLink="/admin/settings" routerLinkActive="on">الإعدادات</a>
      <button type="button" (click)="logout()">خروج</button>
    </nav>
    <router-outlet />
  `,
  styles: `
    .admin-nav { display: flex; gap: 6px; margin-top: 16px; padding: 4px; border-radius: var(--dose-radius-sm); background: var(--dose-navy); }
    .admin-nav a, .admin-nav button { flex: 1; text-align: center; padding: 10px; border-radius: 8px; color: #fff; text-decoration: none; font: 600 0.92rem var(--dose-font); background: none; border: 0; cursor: pointer; }
    .admin-nav a.on { background: var(--dose-primary); }
  `,
})
export class AdminShell {
  private readonly api = inject(Api);
  private readonly router = inject(Router);
  protected async logout() {
    await this.api.logout().catch(() => undefined);
    await this.router.navigateByUrl('/admin/login');
  }
}

@Component({
  selector: 'dose-admin-payments',
  imports: [FormsModule],
  template: `
    <section class="page">
      <div class="row">
        <h1 class="page-title">مراجعة المدفوعات</h1>
        <select [ngModel]="status()" (ngModelChange)="status.set($event); load()" aria-label="الحالة">
          <option value="Submitted">في انتظار المراجعة</option>
          <option value="Approved">مقبولة</option>
          <option value="Rejected">مرفوضة</option>
          <option value="">الكل</option>
        </select>
      </div>
      @if (error()) { <p class="alert alert--error">{{ error() }}</p> }
      @if (proofs().length === 0 && !loading()) { <p class="muted">مفيش إيصالات هنا.</p> }

      @for (p of proofs(); track p.id) {
        <article class="dose-card item" [class.late]="p.status === 'Submitted' && p.ageMinutes > 60">
          <div class="row">
            <strong dir="ltr">{{ p.bookingReference }}</strong>
            <span class="badge" [class.badge--yellow]="p.status === 'Submitted'" [class.badge--green]="p.status === 'Approved'"
                  [class.badge--gray]="p.status === 'Rejected'">{{ statusAr(p.status) }}</span>
          </div>
          <ul class="info-list small">
            <li><span>الطالب</span><span>{{ p.studentName }}</span></li>
            <li><span>واتساب</span><span dir="ltr">{{ p.contactPhone }}</span></li>
            @if (p.guardianPhone) { <li><span>ولي الأمر</span><span dir="ltr">{{ p.guardianPhone }}</span></li> }
            <li><span>الجلسة</span><span>{{ p.sessionTitleAr }} — {{ dt(p.sessionStartUtc) }}</span></li>
            <li><span>المبلغ</span><span>{{ money(p.amount, p.currency) }} · {{ p.methodNameAr }}</span></li>
            @if (p.senderReference) { <li><span>رقم العملية</span><span dir="ltr">{{ p.senderReference }}</span></li> }
            <li><span>من</span><span>{{ p.ageMinutes }} دقيقة</span></li>
          </ul>
          <button type="button" class="dose-btn dose-btn--ghost" (click)="toggleImage(p.id)">
            {{ openImage() === p.id ? 'إخفاء الإيصال' : 'عرض الإيصال' }}
          </button>
          @if (openImage() === p.id) { <img [src]="fileUrl(p.id)" alt="صورة الإيصال" class="proof" /> }

          @if (p.status === 'Submitted') {
            <div class="actions">
              <button type="button" class="dose-btn dose-btn--primary" (click)="approve(p)" [disabled]="busy()">تأكيد الدفع</button>
              <input [(ngModel)]="reasons[p.id]" placeholder="سبب الرفض (هيظهر للطالب)" aria-label="سبب الرفض" />
              <button type="button" class="dose-btn reject" (click)="reject(p)" [disabled]="busy()">رفض</button>
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

  protected statusAr(s: string) {
    return ({ Submitted: 'في انتظار المراجعة', Approved: 'مقبول', Rejected: 'مرفوض' } as Record<string, string>)[s] ?? s;
  }
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
  imports: [FormsModule],
  template: `
    <section class="page">
      <h1 class="page-title">الإعدادات</h1>
      <p class="muted">المدد والمهل دي بتتطبق على الحجوزات الجديدة فوراً.</p>
      @if (error()) { <p class="alert alert--error">{{ error() }}</p> }
      @if (saved()) { <p class="alert alert--ok">اتحفظت الإعدادات.</p> }
      <form class="dose-card box" (ngSubmit)="save()">
        @for (s of items(); track s.key) {
          <label class="field">
            <span>{{ s.descriptionAr ?? s.key }}</span>
            <input type="number" min="1" [name]="s.key" [(ngModel)]="s.value" dir="ltr" />
          </label>
        }
        <button class="dose-btn dose-btn--primary" type="submit" [disabled]="busy()">حفظ</button>
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
