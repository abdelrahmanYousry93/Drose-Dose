import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Component, Injectable, effect, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { CanActivateFn, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';
import { cairoToUtc } from '../../core/admin-api';
import { Api, ApiError } from '../../core/api';
import { errorMessage, fmtDate, fmtDateTime, fmtTime } from '../../core/format';
import { TranslatePipe, translate as tr } from '../../core/i18n';

// ---------- API ----------
export interface TeacherSession {
  id: string; title: string; details: string | null; subjectName: string; gradeName: string; studyTrackName: string;
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
  me() { return this.run(firstValueFrom(this.http.get<{ teacherId: string; displayName: string; email: string }>(`${this.base}/me`))); }
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

const label = (prefix: string) => (s: string) => { const k = `${prefix}.${s}`; const t = tr(k); return t === k ? s : t; };
const statusAr = label('teacher.st');

// ---------- Shell ----------
@Component({
  selector: 'dose-teacher-shell',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, TranslatePipe],
  template: `
    <div class="hello">
      @if (name()) { <span>{{ 'teacher.hello' | t: { name: name() } }}</span> }
      @if (error()) { <p class="alert alert--error">{{ error() }}</p> }
    </div>
    <nav class="tnav">
      <a routerLink="/teacher/sessions" routerLinkActive="on">{{ 'titles.teacherSessions' | t }}</a>
      <a routerLink="/teacher/availability" routerLinkActive="on">{{ 'titles.teacherAvailability' | t }}</a>
      <a routerLink="/teacher/proposals" routerLinkActive="on">{{ 'proposals.nav' | t }}</a>
      <button type="button" (click)="logout()">{{ 'adminNav.logout' | t }}</button>
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
  constructor() { this.api.me().then((m) => this.name.set(m.displayName)).catch((e) => this.error.set(errorMessage(e))); }
  protected async logout() { await this.auth.logout().catch(() => undefined); await this.router.navigateByUrl('/login'); }
}

// ---------- Dashboard: my sessions ----------
@Component({
  selector: 'dose-teacher-sessions',
  imports: [RouterLink, TranslatePipe],
  template: `
    <section class="page">
      <div class="row">
        <h1 class="page-title">{{ (past() ? 'teacher.past' : 'teacher.upcoming') | t }}</h1>
        <button type="button" class="dose-btn dose-btn--ghost small-btn" (click)="past.set(!past()); load()">{{ (past() ? 'teacher.showUpcoming' : 'teacher.showPast') | t }}</button>
      </div>
      @if (!past() && sessions().length) {
        <div class="stats">
          <div class="dose-card stat"><span class="num">{{ sessions().length }}</span><span>{{ 'teacher.statSessions' | t }}</span></div>
          <div class="dose-card stat"><span class="num">{{ students() }}</span><span>{{ 'teacher.statStudents' | t }}</span></div>
          <div class="dose-card stat"><span class="num">{{ needsAttendance() }}</span><span>{{ 'teacher.statAttendance' | t }}</span></div>
        </div>
      }
      @if (error()) { <p class="alert alert--error" role="alert">{{ error() }}</p> }
      @for (s of sessions(); track s.id) {
        <a class="dose-card item" [routerLink]="['/teacher/sessions', s.id]" [class.cancelled]="s.status === 'Cancelled'">
          <div class="row"><strong>{{ s.subjectName }} — {{ s.title }}</strong><span class="badge">{{ statusAr(s.status) }}</span></div>
          <span class="muted small">{{ s.gradeName }} · {{ s.studyTrackName }} · {{ (s.mode === 'Group' ? 'booking.group' : 'booking.private') | t }}</span>
          <span>{{ date(s.startUtc) }} — {{ 'availability.range' | t: { from: time(s.startUtc), to: time(s.endUtc) } }}</span>
          <span class="small">{{ 'teacher.confirmedN' | t: { n: s.confirmedCount } }} @if (s.canMarkAttendance) { · <strong class="hot">{{ 'teacher.markAttendance' | t }}</strong> }</span>
        </a>
      } @empty { <p class="muted">{{ 'sessionsAdmin.empty' | t }}</p> }
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
  imports: [RouterLink, TranslatePipe],
  template: `
    <section class="page">
      <a routerLink="/teacher/sessions" class="back">{{ 'titles.teacherSessions' | t }}</a>
      @if (error()) { <p class="alert alert--error" role="alert">{{ error() }}</p> }
      @if (notice()) { <p class="alert alert--ok" role="status">{{ notice() }}</p> }
      @if (s(); as s) {
        <div class="dose-card box">
          <span class="badge">{{ statusAr(s.status) }}</span>
          <h1 class="page-title" dir="auto">{{ s.subjectName }} — {{ s.title }}</h1>
          <span class="muted">{{ s.gradeName }} · {{ s.studyTrackName }}</span>
          <span>{{ dt(s.startUtc) }} – {{ time(s.endUtc) }}</span>
          @if (s.details) { <p class="small">{{ s.details }}</p> }
          @if (s.meetingUrl) {
            <a class="dose-btn dose-btn--primary" [href]="s.meetingUrl" target="_blank" rel="noopener">{{ 'teacher.openMeet' | t }}</a>
          } @else if (s.status !== 'Cancelled') {
            <p class="alert small">{{ 'teacher.noLink' | t }}</p>
          }
        </div>

        <div class="dose-card box">
          <h2>{{ 'teacher.students' | t: { n: students().length } }}</h2>
          <p class="muted small">{{ 'teacher.studentsHint' | t }}</p>
          @for (st of students(); track st.bookingId) {
            <div class="student">
              <span>{{ st.firstName }}</span>
              @if (s.canMarkAttendance && s.status !== 'Completed') {
                <div class="toggle" role="radiogroup" [attr.aria-label]="'teacher.attendanceOf' | t: { name: st.firstName }">
                  <button type="button" [class.on]="marks[st.bookingId] === true" (click)="marks[st.bookingId] = true">{{ 'teacher.present' | t }}</button>
                  <button type="button" [class.on]="marks[st.bookingId] === false" (click)="marks[st.bookingId] = false">{{ 'teacher.absent' | t }}</button>
                </div>
              } @else if (st.attendance) {
                <span class="badge" [class.badge--green]="st.attendance === 'Attended'">{{ (st.attendance === 'Attended' ? 'teacher.present' : 'teacher.absent') | t }}</span>
              }
            </div>
          } @empty { <p class="muted small">{{ 'teacher.noStudents' | t }}</p> }

          @if (s.canMarkAttendance && s.status !== 'Completed' && students().length) {
            <div class="actions">
              <button type="button" class="dose-btn dose-btn--ghost" (click)="save()">{{ 'teacher.saveAttendance' | t }}</button>
              <button type="button" class="dose-btn dose-btn--primary" (click)="complete()">{{ 'teacher.complete' | t }}</button>
            </div>
          } @else if (!s.canMarkAttendance && s.status !== 'Completed' && s.status !== 'Cancelled') {
            <p class="muted small">{{ 'teacher.attendanceOpens' | t }}</p>
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
    try { await this.api.attendance(this.id(), this.items()); this.notice.set(tr('teacher.attendanceSaved')); await this.load(); }
    catch (e) { this.error.set(errorMessage(e)); }
  }

  protected async complete() {
    if (!confirm(tr('teacher.completeConfirm'))) return;
    this.error.set(''); this.notice.set('');
    try {
      await this.api.attendance(this.id(), this.items());
      await this.api.complete(this.id());
      this.notice.set(tr('teacher.completed'));
      await this.load();
    } catch (e) { this.error.set(errorMessage(e)); }
  }
}

// ---------- Availability ----------
const avAr = label('teacher.av');

@Component({
  selector: 'dose-teacher-availability',
  imports: [FormsModule, TranslatePipe],
  template: `
    <section class="page">
      <h1 class="page-title">{{ 'teacher.avTitle' | t }}</h1>
      <p class="muted small">{{ 'teacher.avLead' | t }}</p>
      @if (error()) { <p class="alert alert--error" role="alert">{{ error() }}</p> }

      <form class="dose-card box" (ngSubmit)="add()">
        <div class="grid">
          <label class="field"><span>{{ 'teacher.day' | t }}</span><input name="date" type="date" [(ngModel)]="date" /></label>
          <label class="field"><span>{{ 'pricing.min' | t }}</span><input name="from" type="time" [(ngModel)]="from" /></label>
          <label class="field"><span>{{ 'pricing.max' | t }}</span><input name="to" type="time" [(ngModel)]="to" /></label>
        </div>
        <label class="field"><span>{{ 'availability.note' | t }} <span class="hint">{{ 'common.optional' | t }}</span></span><input name="note" [(ngModel)]="note" maxlength="300" /></label>
        <button class="dose-btn dose-btn--primary" type="submit">{{ 'teacher.addSlot' | t }}</button>
      </form>

      @for (a of items(); track a.id) {
        <div class="dose-card item">
          <div><strong>{{ fmtDate(a.startUtc) }}</strong> — {{ 'availability.range' | t: { from: fmtTime(a.startUtc), to: fmtTime(a.endUtc) } }}
            @if (a.note) { <span class="muted small"> · {{ a.note }}</span> }
            @if (a.reviewNote) { <div class="muted small">{{ 'teacher.adminNote' | t: { note: a.reviewNote } }}</div> }</div>
          <div class="meta">
            <span class="badge" [class.badge--yellow]="a.status === 'Submitted'" [class.badge--green]="a.status === 'Approved'"
                  [class.badge--gray]="a.status === 'Rejected' || a.status === 'Withdrawn'">{{ avAr(a.status) }}</span>
            @if (a.status === 'Submitted' || a.status === 'Approved') {
              <button type="button" class="x" (click)="withdraw(a)">{{ 'teacher.withdraw' | t }}</button>
            }
          </div>
        </div>
      } @empty { <p class="muted">{{ 'teacher.noSlots' | t }}</p> }
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
  protected readonly avAr = avAr;

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
    if (!confirm(tr('teacher.withdrawConfirm'))) return;
    try { await this.api.withdraw(a.id); await this.load(); } catch (e) { this.error.set(errorMessage(e)); }
  }
}
