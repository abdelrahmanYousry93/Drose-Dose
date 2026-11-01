import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Component, Injectable, effect, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { CanActivateFn, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';
import { cairoToUtc } from '../../core/admin-api';
import { Api, ApiError } from '../../core/api';
import { errorMessage, fmtDate, fmtDateTime, fmtTime } from '../../core/format';

// ---------- API ----------
export interface TeacherSession {
  id: string; titleAr: string; detailsAr: string | null; subjectNameAr: string; gradeNameAr: string; studyTrackNameAr: string;
  startUtc: string; endUtc: string; mode: string; status: string; confirmedCount: number; capacity: number;
  meetingUrl: string | null; canMarkAttendance: boolean; attendanceMarked: number;
}
export interface TeacherStudent { bookingId: string; firstName: string; attendance: 'Attended' | 'Absent' | null }
export interface Availability { id: string; startUtc: string; endUtc: string; note: string | null; status: string; reviewNote: string | null }

@Injectable({ providedIn: 'root' })
export class TeacherApi {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/teacher`;
  private async run<T>(p: Promise<T>): Promise<T> {
    try { return await p; } catch (e) {
      if (e instanceof HttpErrorResponse) {
        const b = e.error as { code?: string; title?: string } | null;
        throw new ApiError(b?.code ?? `http.${e.status}`, b?.title ?? e.message, e.status);
      }
      throw e;
    }
  }
  me() { return this.run(firstValueFrom(this.http.get<{ teacherId: string; displayNameAr: string; email: string }>(`${this.base}/me`))); }
  sessions(past: boolean) { return this.run(firstValueFrom(this.http.get<TeacherSession[]>(`${this.base}/sessions`, { params: past ? { past: 'true' } : {} }))); }
  session(id: string) { return this.run(firstValueFrom(this.http.get<{ session: TeacherSession; students: TeacherStudent[] }>(`${this.base}/sessions/${id}`))); }
  attendance(id: string, items: { bookingId: string; attended: boolean }[]) {
    return this.run(firstValueFrom(this.http.post<void>(`${this.base}/sessions/${id}/attendance`, { items })));
  }
  complete(id: string) { return this.run(firstValueFrom(this.http.post<void>(`${this.base}/sessions/${id}/complete`, {}))); }
  availability() { return this.run(firstValueFrom(this.http.get<Availability[]>(`${this.base}/availability`))); }
  addAvailability(startUtc: string, endUtc: string, note: string | null) {
    return this.run(firstValueFrom(this.http.post<{ id: string }>(`${this.base}/availability`, { startUtc, endUtc, note })));
  }
  withdraw(id: string) { return this.run(firstValueFrom(this.http.post<void>(`${this.base}/availability/${id}/withdraw`, {}))); }
}

export const teacherGuard: CanActivateFn = async () => {
  const api = inject(Api);
  const router = inject(Router);
  try {
    const me = await api.me();
    return me.roles.includes('Teacher') ? true : router.parseUrl('/login');
  } catch {
    return router.parseUrl('/login');
  }
};

const STATUS_AR: Record<string, string> = {
  Published: 'مفتوحة للحجز', TopUpRequired: 'مستنية رد الطلاب', Confirmed: 'مؤكدة', InProgress: 'شغالة', Completed: 'خلصت', Cancelled: 'ملغية',
};
const statusAr = (s: string) => STATUS_AR[s] ?? s;

// ---------- Shell ----------
@Component({
  selector: 'dose-teacher-shell',
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  template: `
    <div class="hello">
      @if (name()) { <span>أهلاً {{ name() }}</span> }
      @if (error()) { <p class="alert alert--error">{{ error() }}</p> }
    </div>
    <nav class="tnav">
      <a routerLink="/teacher/sessions" routerLinkActive="on">جلساتي</a>
      <a routerLink="/teacher/availability" routerLinkActive="on">مواعيدي</a>
      <button type="button" (click)="logout()">خروج</button>
    </nav>
    <router-outlet />
  `,
  styles: `
    .hello { margin-top: 16px; font-weight: 700; color: var(--dose-navy); }
    .tnav { display: flex; gap: 4px; margin-top: 8px; padding: 4px; border-radius: var(--dose-radius-sm); background: var(--dose-navy); }
    .tnav a, .tnav button { flex: 1; text-align: center; padding: 9px; border-radius: 8px; color: #fff; text-decoration: none; font: 600 0.92rem var(--dose-font); background: none; border: 0; cursor: pointer; }
    .tnav a.on { background: var(--dose-primary); }
  `,
})
export class TeacherShell {
  private readonly api = inject(TeacherApi);
  private readonly auth = inject(Api);
  private readonly router = inject(Router);
  protected readonly name = signal('');
  protected readonly error = signal('');
  constructor() { this.api.me().then((m) => this.name.set(m.displayNameAr)).catch((e) => this.error.set(errorMessage(e))); }
  protected async logout() { await this.auth.logout().catch(() => undefined); await this.router.navigateByUrl('/login'); }
}

// ---------- Dashboard: my sessions ----------
@Component({
  selector: 'dose-teacher-sessions',
  imports: [RouterLink],
  template: `
    <section class="page">
      <div class="row">
        <h1 class="page-title">{{ past() ? 'جلسات قديمة' : 'جلساتي الجاية' }}</h1>
        <button type="button" class="dose-btn dose-btn--ghost small-btn" (click)="past.set(!past()); load()">{{ past() ? 'الجاية' : 'القديمة' }}</button>
      </div>
      @if (!past() && sessions().length) {
        <div class="stats">
          <div class="dose-card stat"><span class="num">{{ sessions().length }}</span><span>جلسة</span></div>
          <div class="dose-card stat"><span class="num">{{ students() }}</span><span>طالب مؤكد</span></div>
          <div class="dose-card stat"><span class="num">{{ needsAttendance() }}</span><span>محتاجة تسجيل حضور</span></div>
        </div>
      }
      @if (error()) { <p class="alert alert--error" role="alert">{{ error() }}</p> }
      @for (s of sessions(); track s.id) {
        <a class="dose-card item" [routerLink]="['/teacher/sessions', s.id]" [class.cancelled]="s.status === 'Cancelled'">
          <div class="row"><strong>{{ s.subjectNameAr }} — {{ s.titleAr }}</strong><span class="badge">{{ statusAr(s.status) }}</span></div>
          <span class="muted small">{{ s.gradeNameAr }} · {{ s.studyTrackNameAr }} · {{ s.mode === 'Group' ? 'جماعية' : 'فردية' }}</span>
          <span>{{ date(s.startUtc) }} — {{ time(s.startUtc) }} لـ {{ time(s.endUtc) }}</span>
          <span class="small">{{ s.confirmedCount }} طالب مؤكد @if (s.canMarkAttendance) { · <strong class="hot">سجّل الحضور</strong> }</span>
        </a>
      } @empty { <p class="muted">مفيش جلسات.</p> }
    </section>
  `,
  styles: `
    .row { display: flex; justify-content: space-between; align-items: center; gap: 8px; }
    .stats { display: grid; grid-template-columns: repeat(3, 1fr); gap: 8px; }
    .stat { padding: 12px; display: grid; text-align: center; }
    .num { font-size: 1.6rem; font-weight: 800; color: var(--dose-navy); }
    .item { padding: 14px 16px; display: grid; gap: 4px; text-decoration: none; color: inherit; }
    .item.cancelled { opacity: .6; }
    .hot { color: var(--dose-primary); }
    .small-btn { min-height: 38px; padding: 0 12px; font-size: 0.88rem; }
  `,
})
export class TeacherSessions {
  private readonly api = inject(TeacherApi);
  protected readonly sessions = signal<TeacherSession[]>([]);
  protected readonly past = signal(false);
  protected readonly error = signal('');
  protected readonly date = fmtDate;
  protected readonly time = fmtTime;
  protected readonly statusAr = statusAr;
  protected readonly students = () => this.sessions().filter((s) => s.status !== 'Cancelled').reduce((n, s) => n + s.confirmedCount, 0);
  protected readonly needsAttendance = () => this.sessions().filter((s) => s.canMarkAttendance && s.status !== 'Completed').length;

  constructor() { void this.load(); }
  protected async load() {
    try { this.sessions.set(await this.api.sessions(this.past())); this.error.set(''); } catch (e) { this.error.set(errorMessage(e)); }
  }
}

// ---------- Session: link, students (first names), attendance, complete ----------
@Component({
  selector: 'dose-teacher-session',
  imports: [RouterLink],
  template: `
    <section class="page">
      <a routerLink="/teacher/sessions" class="back">→ جلساتي</a>
      @if (error()) { <p class="alert alert--error" role="alert">{{ error() }}</p> }
      @if (notice()) { <p class="alert alert--ok" role="status">{{ notice() }}</p> }
      @if (s(); as s) {
        <div class="dose-card box">
          <span class="badge">{{ statusAr(s.status) }}</span>
          <h1 class="page-title">{{ s.subjectNameAr }} — {{ s.titleAr }}</h1>
          <span class="muted">{{ s.gradeNameAr }} · {{ s.studyTrackNameAr }}</span>
          <span>{{ dt(s.startUtc) }} لـ {{ time(s.endUtc) }}</span>
          @if (s.detailsAr) { <p class="small">{{ s.detailsAr }}</p> }
          @if (s.meetingUrl) {
            <a class="dose-btn dose-btn--primary" [href]="s.meetingUrl" target="_blank" rel="noopener">افتح Google Meet</a>
          } @else if (s.status !== 'Cancelled') {
            <p class="alert small">لينك الجلسة لسه متضافش. الإدارة هتضيفه قبل الميعاد.</p>
          }
        </div>

        <div class="dose-card box">
          <h2>الطلاب ({{ students().length }})</h2>
          <p class="muted small">بتظهر الأسماء الأولى بس. أي تواصل مع الطلاب بيكون عن طريق الإدارة.</p>
          @for (st of students(); track st.bookingId) {
            <div class="student">
              <span>{{ st.firstName }}</span>
              @if (s.canMarkAttendance && s.status !== 'Completed') {
                <div class="toggle" role="radiogroup" [attr.aria-label]="'حضور ' + st.firstName">
                  <button type="button" [class.on]="marks[st.bookingId] === true" (click)="marks[st.bookingId] = true">حاضر</button>
                  <button type="button" [class.on]="marks[st.bookingId] === false" (click)="marks[st.bookingId] = false">غايب</button>
                </div>
              } @else if (st.attendance) {
                <span class="badge" [class.badge--green]="st.attendance === 'Attended'">{{ st.attendance === 'Attended' ? 'حاضر' : 'غايب' }}</span>
              }
            </div>
          } @empty { <p class="muted small">مفيش طلاب مؤكدين لسه.</p> }

          @if (s.canMarkAttendance && s.status !== 'Completed' && students().length) {
            <div class="actions">
              <button type="button" class="dose-btn dose-btn--ghost" (click)="save()">حفظ الحضور</button>
              <button type="button" class="dose-btn dose-btn--primary" (click)="complete()">إنهاء الجلسة</button>
            </div>
          } @else if (!s.canMarkAttendance && s.status !== 'Completed' && s.status !== 'Cancelled') {
            <p class="muted small">تسجيل الحضور بيتفتح قبل الجلسة بربع ساعة.</p>
          }
        </div>
      }
    </section>
  `,
  styles: `
    .back { text-decoration: none; font-weight: 600; }
    .box { padding: 16px; display: grid; gap: 8px; justify-items: start; }
    .box h2 { margin: 0; font-size: 1.08rem; color: var(--dose-navy); }
    .box p { margin: 0; }
    .student { width: 100%; display: flex; justify-content: space-between; align-items: center; padding: 8px 0; border-bottom: 1px solid var(--dose-border); }
    .toggle { display: flex; gap: 4px; }
    .toggle button { min-height: 36px; padding: 0 14px; border: 1px solid var(--dose-border); border-radius: 8px; background: var(--dose-surface); font: 600 0.88rem var(--dose-font); cursor: pointer; }
    .toggle button.on:first-child { background: var(--dose-success); border-color: var(--dose-success); color: #fff; }
    .toggle button.on:last-child { background: #a52714; border-color: #a52714; color: #fff; }
    .actions { display: flex; gap: 8px; flex-wrap: wrap; margin-top: 8px; }
  `,
})
export class TeacherSessionPage {
  private readonly api = inject(TeacherApi);
  readonly id = input.required<string>();
  protected readonly s = signal<TeacherSession | null>(null);
  protected readonly students = signal<TeacherStudent[]>([]);
  protected readonly error = signal('');
  protected readonly notice = signal('');
  protected marks: Record<string, boolean | undefined> = {};
  protected readonly dt = fmtDateTime;
  protected readonly time = fmtTime;
  protected readonly statusAr = statusAr;

  constructor() { effect(() => { void this.load(this.id()); }); }

  private async load(id = this.id()) {
    try {
      const d = await this.api.session(id);
      this.s.set(d.session);
      this.students.set(d.students);
      this.marks = Object.fromEntries(d.students.map((st) => [st.bookingId, st.attendance === null ? undefined : st.attendance === 'Attended']));
    } catch (e) { this.error.set(errorMessage(e)); }
  }

  private items() {
    return Object.entries(this.marks).filter(([, v]) => v !== undefined).map(([bookingId, attended]) => ({ bookingId, attended: !!attended }));
  }

  protected async save() {
    this.error.set(''); this.notice.set('');
    try { await this.api.attendance(this.id(), this.items()); this.notice.set('اتحفظ الحضور ✓'); await this.load(); }
    catch (e) { this.error.set(errorMessage(e)); }
  }

  protected async complete() {
    if (!confirm('إنهاء الجلسة؟ اتأكد إنك سجلت حضور كل الطلاب.')) return;
    this.error.set(''); this.notice.set('');
    try {
      await this.api.attendance(this.id(), this.items());
      await this.api.complete(this.id());
      this.notice.set('الجلسة اتقفلت ✓ شكراً!');
      await this.load();
    } catch (e) { this.error.set(errorMessage(e)); }
  }
}

// ---------- Availability ----------
const AV_AR: Record<string, string> = { Submitted: 'مستني موافقة', Approved: 'اتوافق عليه', Rejected: 'مرفوض', Withdrawn: 'اتسحب' };

@Component({
  selector: 'dose-teacher-availability',
  imports: [FormsModule],
  template: `
    <section class="page">
      <h1 class="page-title">مواعيدي المتاحة</h1>
      <p class="muted small">ضيف الأوقات اللي تقدر تشرح فيها، والإدارة هتوافق عليها وتبني عليها الجلسات. المواعيد بتوقيت القاهرة.</p>
      @if (error()) { <p class="alert alert--error" role="alert">{{ error() }}</p> }

      <form class="dose-card box" (ngSubmit)="add()">
        <div class="grid">
          <label class="field"><span>اليوم</span><input name="date" type="date" [(ngModel)]="date" /></label>
          <label class="field"><span>من</span><input name="from" type="time" [(ngModel)]="from" /></label>
          <label class="field"><span>لـ</span><input name="to" type="time" [(ngModel)]="to" /></label>
        </div>
        <label class="field"><span>ملاحظة <span class="hint">(اختياري)</span></span><input name="note" [(ngModel)]="note" maxlength="300" /></label>
        <button class="dose-btn dose-btn--primary" type="submit">إضافة الميعاد</button>
      </form>

      @for (a of items(); track a.id) {
        <div class="dose-card item">
          <div><strong>{{ fmtDate(a.startUtc) }}</strong> — {{ fmtTime(a.startUtc) }} لـ {{ fmtTime(a.endUtc) }}
            @if (a.note) { <span class="muted small"> · {{ a.note }}</span> }
            @if (a.reviewNote) { <div class="muted small">ملاحظة الإدارة: {{ a.reviewNote }}</div> }</div>
          <div class="meta">
            <span class="badge" [class.badge--yellow]="a.status === 'Submitted'" [class.badge--green]="a.status === 'Approved'"
                  [class.badge--gray]="a.status === 'Rejected' || a.status === 'Withdrawn'">{{ avAr(a.status) }}</span>
            @if (a.status === 'Submitted' || a.status === 'Approved') {
              <button type="button" class="x" (click)="withdraw(a)">سحب</button>
            }
          </div>
        </div>
      } @empty { <p class="muted">لسه مضفتش مواعيد.</p> }
    </section>
  `,
  styles: `
    .box { padding: 16px; display: grid; gap: 12px; }
    .grid { display: grid; gap: 12px; grid-template-columns: 1.4fr 1fr 1fr; }
    .item { padding: 12px 14px; display: flex; justify-content: space-between; align-items: center; gap: 8px; flex-wrap: wrap; }
    .meta { display: flex; gap: 6px; align-items: center; }
    .x { border: 0; background: #fdecea; color: #a52714; border-radius: 8px; padding: 6px 10px; font: 600 0.82rem var(--dose-font); cursor: pointer; }
  `,
})
export class TeacherAvailability {
  private readonly api = inject(TeacherApi);
  protected readonly items = signal<Availability[]>([]);
  protected readonly error = signal('');
  protected date = new Date(Date.now() + 86400000).toISOString().slice(0, 10);
  protected from = '16:00';
  protected to = '20:00';
  protected note = '';
  protected readonly fmtDate = fmtDate;
  protected readonly fmtTime = fmtTime;
  protected readonly avAr = (s: string) => AV_AR[s] ?? s;

  constructor() { void this.load(); }
  private async load() {
    try { this.items.set(await this.api.availability()); this.error.set(''); } catch (e) { this.error.set(errorMessage(e)); }
  }
  protected async add() {
    try {
      await this.api.addAvailability(cairoToUtc(this.date, this.from), cairoToUtc(this.date, this.to), this.note || null);
      this.note = '';
      await this.load();
    } catch (e) { this.error.set(errorMessage(e)); }
  }
  protected async withdraw(a: Availability) {
    if (!confirm('سحب الميعاد ده؟')) return;
    try { await this.api.withdraw(a.id); await this.load(); } catch (e) { this.error.set(errorMessage(e)); }
  }
}
