import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AdminApi, AvailabilityA, TeacherA, UserA } from '../../core/admin-api';
import { errorMessage, fmtDate, fmtDateTime, fmtTime } from '../../core/format';
import { TranslatePipe, translate as tr } from '../../core/i18n';
import { optName } from './entity-editor';

/** Admin and teacher accounts. Students never have accounts. */
@Component({
  selector: 'dose-admin-users',
  imports: [FormsModule, TranslatePipe],
  template: `
    <section class="page">
      <h1 class="page-title">{{ 'titles.adminUsers' | t }}</h1>
      <p class="muted small">{{ 'users.lead' | t }}</p>
      @if (error()) { <p class="alert alert--error" role="alert">{{ error() }}</p> }
      @if (notice()) { <p class="alert alert--ok" role="status">{{ notice() }}</p> }

      <form class="dose-card box" (ngSubmit)="create()">
        <h2>{{ 'users.new' | t }}</h2>
        <div class="grid">
          <label class="field"><span>{{ 'login.email' | t }}</span><input name="email" type="email" [(ngModel)]="email" dir="ltr" autocomplete="off" /></label>
          <label class="field"><span>{{ 'users.tempPassword' | t }}</span><input name="password" type="text" [(ngModel)]="password" dir="ltr" autocomplete="new-password" />
            <span class="hint">{{ 'users.passwordHint' | t }}</span></label>
          <label class="field"><span>{{ 'users.role' | t }}</span>
            <select name="role" [(ngModel)]="role"><option value="Teacher">{{ 'users.roleTeacher' | t }}</option><option value="Admin">{{ 'users.roleAdmin' | t }}</option></select></label>
          @if (role === 'Teacher') {
            <label class="field"><span>{{ 'users.teacherProfile' | t }}</span>
              <select name="teacher" [(ngModel)]="teacherId">
                <option [ngValue]="null">{{ 'request.choose' | t }}</option>
                @for (t of freeTeachers(); track t.id) { <option [ngValue]="t.id">{{ optName({ nameAr: t.displayNameAr, nameEn: t.displayNameEn }) }}</option> }
              </select></label>
          }
        </div>
        <button class="dose-btn dose-btn--primary" type="submit">{{ 'users.create' | t }}</button>
      </form>

      @for (u of users(); track u.id) {
        <article class="dose-card item" [class.off]="u.isDisabled">
          <div class="row">
            <strong dir="ltr">{{ u.email }}</strong>
            @for (r of u.roles; track r) { <span class="badge">{{ (r === 'Admin' ? 'users.roleAdmin' : 'users.roleTeacher') | t }}</span> }
            @if (u.isDisabled) { <span class="badge badge--gray">{{ 'editor.inactive' | t }}</span> }
          </div>
          @if (u.roles.includes('Teacher')) {
            <label class="inline">{{ 'users.teacherProfile' | t }}:
              <select [ngModel]="u.teacherId" (ngModelChange)="link(u, $event)" [attr.aria-label]="'users.teacherProfile' | t">
                <option [ngValue]="null">{{ 'users.notLinked' | t }}</option>
                @for (t of teachers(); track t.id) { <option [ngValue]="t.id">{{ optName({ nameAr: t.displayNameAr, nameEn: t.displayNameEn }) }}</option> }
              </select>
            </label>
          }
          <div class="actions">
            <button type="button" class="dose-btn dose-btn--ghost small-btn" (click)="reset(u)">{{ 'users.newPassword' | t }}</button>
            <button type="button" class="dose-btn small-btn" [class.danger]="!u.isDisabled" (click)="toggle(u)">{{ (u.isDisabled ? 'users.enable' : 'users.disable') | t }}</button>
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
  protected readonly optName = optName;
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
    }, tr('users.created'));
  }

  protected reset(u: UserA) {
    const pass = prompt(tr('users.resetPrompt', { email: u.email }));
    if (pass) void this.run(() => this.api.resetPassword(u.id, pass), tr('users.passwordChanged'));
  }

  protected toggle(u: UserA) {
    if (!u.isDisabled && !confirm(tr('users.disableConfirm', { email: u.email }))) return;
    void this.run(() => this.api.setDisabled(u.id, !u.isDisabled), tr(u.isDisabled ? 'users.enabled' : 'users.disabled'));
  }

  protected link(u: UserA, teacherId: string | null) {
    void this.run(() => this.api.linkTeacher(u.id, teacherId), tr('users.linked'));
  }
}

const avStatus = (s: string) => { const k = `availability.st.${s}`; const t = tr(k); return t === k ? s : t; };

/** Teachers submit availability; admins approve or reject (decision D11). */
@Component({
  selector: 'dose-admin-availability',
  imports: [FormsModule, TranslatePipe],
  template: `
    <section class="page">
      <div class="row">
        <h1 class="page-title">{{ 'titles.adminAvailability' | t }}</h1>
        <select [ngModel]="status()" (ngModelChange)="status.set($event); load()" [attr.aria-label]="'payments.status' | t">
          <option value="Submitted">{{ 'availability.st.Submitted' | t }}</option><option value="Approved">{{ 'availability.st.Approved' | t }}</option>
          <option value="Rejected">{{ 'availability.st.Rejected' | t }}</option><option value="">{{ 'sessions.all' | t }}</option>
        </select>
      </div>
      <p class="muted small">{{ 'availability.lead' | t }}</p>
      @if (error()) { <p class="alert alert--error" role="alert">{{ error() }}</p> }
      @for (a of items(); track a.id) {
        <article class="dose-card item">
          <div class="row"><strong>{{ a.teacherNameAr }}</strong><span class="badge" [class.badge--yellow]="a.status === 'Submitted'"
            [class.badge--green]="a.status === 'Approved'">{{ statusAr(a.status) }}</span></div>
          <span>{{ date(a.startUtc) }} — {{ 'availability.range' | t: { from: time(a.startUtc), to: time(a.endUtc) } }}</span>
          @if (a.note) { <span class="muted small">{{ a.note }}</span> }
          @if (a.status === 'Submitted') {
            <div class="actions">
              <input [(ngModel)]="notes[a.id]" [placeholder]="'availability.notePh' | t" [attr.aria-label]="'availability.note' | t" />
              <button type="button" class="dose-btn dose-btn--primary small-btn" (click)="review(a, true)">{{ 'availability.approve' | t }}</button>
              <button type="button" class="dose-btn small-btn danger" (click)="review(a, false)">{{ 'payments.reject' | t }}</button>
            </div>
          } @else if (a.reviewNote) { <span class="muted small">{{ 'availability.note' | t }}: {{ a.reviewNote }}</span> }
        </article>
      } @empty { <p class="muted">{{ 'availability.empty' | t }}</p> }
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
  protected readonly statusAr = avStatus;

  constructor() { void this.load(); }

  protected async load() {
    try { this.items.set(await this.api.availability(this.status() || null)); this.error.set(''); } catch (e) { this.error.set(errorMessage(e)); }
  }

  protected async review(a: AvailabilityA, approve: boolean) {
    try { await this.api.reviewAvailability(a.id, approve, this.notes[a.id] || null); await this.load(); } catch (e) { this.error.set(errorMessage(e)); }
  }
}
