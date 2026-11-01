import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AdminApi, AvailabilityA, TeacherA, UserA } from '../../core/admin-api';
import { errorMessage, fmtDate, fmtDateTime, fmtTime } from '../../core/format';

/** Admin and teacher accounts. Students never have accounts. */
@Component({
  selector: 'dose-admin-users',
  imports: [FormsModule],
  template: `
    <section class="page">
      <h1 class="page-title">المستخدمين</h1>
      <p class="muted small">حسابات فريق Dose: الأدمن بيدير كل حاجة، والمدرس بيشوف جلساته بس. كل حساب مدرس لازم يتربط بملف مدرس.</p>
      @if (error()) { <p class="alert alert--error" role="alert">{{ error() }}</p> }
      @if (notice()) { <p class="alert alert--ok" role="status">{{ notice() }}</p> }

      <form class="dose-card box" (ngSubmit)="create()">
        <h2>حساب جديد</h2>
        <div class="grid">
          <label class="field"><span>الإيميل</span><input name="email" type="email" [(ngModel)]="email" dir="ltr" autocomplete="off" /></label>
          <label class="field"><span>كلمة سر مؤقتة</span><input name="password" type="text" [(ngModel)]="password" dir="ltr" autocomplete="new-password" />
            <span class="hint">10 حروف على الأقل. ابعتها للشخص وخليه يغيّرها.</span></label>
          <label class="field"><span>الدور</span>
            <select name="role" [(ngModel)]="role"><option value="Teacher">مدرس</option><option value="Admin">أدمن</option></select></label>
          @if (role === 'Teacher') {
            <label class="field"><span>ملف المدرس</span>
              <select name="teacher" [(ngModel)]="teacherId">
                <option [ngValue]="null">اختار</option>
                @for (t of freeTeachers(); track t.id) { <option [ngValue]="t.id">{{ t.displayNameAr }}</option> }
              </select></label>
          }
        </div>
        <button class="dose-btn dose-btn--primary" type="submit">إنشاء الحساب</button>
      </form>

      @for (u of users(); track u.id) {
        <article class="dose-card item" [class.off]="u.isDisabled">
          <div class="row">
            <strong dir="ltr">{{ u.email }}</strong>
            @for (r of u.roles; track r) { <span class="badge">{{ r === 'Admin' ? 'أدمن' : 'مدرس' }}</span> }
            @if (u.isDisabled) { <span class="badge badge--gray">موقوف</span> }
          </div>
          @if (u.roles.includes('Teacher')) {
            <label class="inline">ملف المدرس:
              <select [ngModel]="u.teacherId" (ngModelChange)="link(u, $event)" aria-label="ملف المدرس">
                <option [ngValue]="null">— مش مربوط —</option>
                @for (t of teachers(); track t.id) { <option [ngValue]="t.id">{{ t.displayNameAr }}</option> }
              </select>
            </label>
          }
          <div class="actions">
            <button type="button" class="dose-btn dose-btn--ghost small-btn" (click)="reset(u)">كلمة سر جديدة</button>
            <button type="button" class="dose-btn small-btn" [class.danger]="!u.isDisabled" (click)="toggle(u)">{{ u.isDisabled ? 'تفعيل' : 'إيقاف' }}</button>
          </div>
        </article>
      }
    </section>
  `,
  styles: `
    .box { padding: 16px; display: grid; gap: 12px; }
    .box h2 { margin: 0; font-size: 1.08rem; color: var(--dose-navy); }
    .grid { display: grid; gap: 12px; }
    @media (min-width: 760px) { .grid { grid-template-columns: 1fr 1fr; } }
    .item { padding: 14px 16px; display: grid; gap: 10px; }
    .item.off { opacity: .65; }
    .row { display: flex; gap: 8px; align-items: center; flex-wrap: wrap; }
    .inline { display: flex; gap: 8px; align-items: center; font-size: 0.92rem; }
    .inline select { min-height: 38px; padding: 0 8px; border: 1px solid var(--dose-border); border-radius: 8px; font: inherit; }
    .actions { display: flex; gap: 8px; }
    .small-btn { min-height: 38px; padding: 0 12px; font-size: 0.88rem; }
    .danger { background: #fdecea; color: #a52714; }
  `,
})
export class AdminUsers {
  private readonly api = inject(AdminApi);
  protected readonly users = signal<UserA[]>([]);
  protected readonly teachers = signal<TeacherA[]>([]);
  protected readonly error = signal('');
  protected readonly notice = signal('');
  protected email = '';
  protected password = '';
  protected role = 'Teacher';
  protected teacherId: string | null = null;

  constructor() { void this.load(); }

  protected freeTeachers() {
    const linked = new Set(this.users().map((u) => u.teacherId).filter(Boolean));
    return this.teachers().filter((t) => !linked.has(t.id));
  }

  private async load() {
    try {
      const [u, t] = await Promise.all([this.api.users(), this.api.teachers()]);
      this.users.set(u);
      this.teachers.set(t);
    } catch (e) { this.error.set(errorMessage(e)); }
  }

  private async run(fn: () => Promise<unknown>, ok: string) {
    this.error.set('');
    this.notice.set('');
    try { await fn(); this.notice.set(ok); await this.load(); } catch (e) { this.error.set(errorMessage(e)); }
  }

  protected create() {
    return this.run(async () => {
      await this.api.createUser(this.email, this.password, this.role, this.role === 'Teacher' ? this.teacherId : null);
      this.email = ''; this.password = ''; this.teacherId = null;
    }, 'اتعمل الحساب ✓ — ابعت الإيميل وكلمة السر للشخص.');
  }

  protected reset(u: UserA) {
    const pass = prompt(`كلمة سر جديدة لـ ${u.email} (10 حروف على الأقل):`);
    if (pass) void this.run(() => this.api.resetPassword(u.id, pass), 'اتغيرت كلمة السر ✓');
  }

  protected toggle(u: UserA) {
    if (!u.isDisabled && !confirm(`إيقاف ${u.email}؟ مش هيقدر يدخل تاني لحد ما تفعّله.`)) return;
    void this.run(() => this.api.setDisabled(u.id, !u.isDisabled), u.isDisabled ? 'اتفعّل ✓' : 'اتوقف ✓');
  }

  protected link(u: UserA, teacherId: string | null) {
    void this.run(() => this.api.linkTeacher(u.id, teacherId), 'اتربط ملف المدرس ✓');
  }
}

const AV_AR: Record<string, string> = { Submitted: 'مستني موافقة', Approved: 'متوافق عليه', Rejected: 'مرفوض', Withdrawn: 'المدرس سحبه' };

/** Teachers submit availability; admins approve or reject (decision D11). */
@Component({
  selector: 'dose-admin-availability',
  imports: [FormsModule],
  template: `
    <section class="page">
      <div class="row">
        <h1 class="page-title">مواعيد المدرسين</h1>
        <select [ngModel]="status()" (ngModelChange)="status.set($event); load()" aria-label="الحالة">
          <option value="Submitted">مستنية موافقة</option><option value="Approved">متوافق عليها</option>
          <option value="Rejected">مرفوضة</option><option value="">الكل</option>
        </select>
      </div>
      <p class="muted small">المواعيد المتوافق عليها هي اللي تبني عليها الجلسات الجديدة لكل مدرس.</p>
      @if (error()) { <p class="alert alert--error" role="alert">{{ error() }}</p> }
      @for (a of items(); track a.id) {
        <article class="dose-card item">
          <div class="row"><strong>{{ a.teacherNameAr }}</strong><span class="badge" [class.badge--yellow]="a.status === 'Submitted'"
            [class.badge--green]="a.status === 'Approved'">{{ statusAr(a.status) }}</span></div>
          <span>{{ date(a.startUtc) }} — من {{ time(a.startUtc) }} لـ {{ time(a.endUtc) }}</span>
          @if (a.note) { <span class="muted small">{{ a.note }}</span> }
          @if (a.status === 'Submitted') {
            <div class="actions">
              <input [(ngModel)]="notes[a.id]" placeholder="ملاحظة للمدرس (اختياري)" aria-label="ملاحظة" />
              <button type="button" class="dose-btn dose-btn--primary small-btn" (click)="review(a, true)">موافقة</button>
              <button type="button" class="dose-btn small-btn danger" (click)="review(a, false)">رفض</button>
            </div>
          } @else if (a.reviewNote) { <span class="muted small">ملاحظة: {{ a.reviewNote }}</span> }
        </article>
      } @empty { <p class="muted">مفيش مواعيد.</p> }
    </section>
  `,
  styles: `
    .row { display: flex; justify-content: space-between; align-items: center; gap: 8px; flex-wrap: wrap; }
    select, input { min-height: 40px; padding: 0 10px; border: 1px solid var(--dose-border); border-radius: 8px; font: inherit; }
    .item { padding: 14px 16px; display: grid; gap: 6px; }
    .actions { display: flex; gap: 8px; flex-wrap: wrap; }
    .actions input { flex: 1; min-width: 160px; }
    .small-btn { min-height: 38px; padding: 0 12px; font-size: 0.88rem; }
    .danger { background: #fdecea; color: #a52714; }
  `,
})
export class AdminAvailability {
  private readonly api = inject(AdminApi);
  protected readonly items = signal<AvailabilityA[]>([]);
  protected readonly status = signal('Submitted');
  protected readonly error = signal('');
  protected notes: Record<string, string> = {};
  protected readonly date = fmtDate;
  protected readonly time = fmtTime;
  protected readonly dt = fmtDateTime;
  protected readonly statusAr = (s: string) => AV_AR[s] ?? s;

  constructor() { void this.load(); }

  protected async load() {
    try { this.items.set(await this.api.availability(this.status() || null)); this.error.set(''); } catch (e) { this.error.set(errorMessage(e)); }
  }

  protected async review(a: AvailabilityA, approve: boolean) {
    try { await this.api.reviewAvailability(a.id, approve, this.notes[a.id] || null); await this.load(); } catch (e) { this.error.set(errorMessage(e)); }
  }
}
