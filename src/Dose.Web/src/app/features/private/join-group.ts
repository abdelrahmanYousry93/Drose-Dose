import { Component, effect, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { Api, InviteDetails } from '../../core/api';
import { errorMessage, fmtDate, fmtDuration, fmtTime, myBookings } from '../../core/format';
import { TranslatePipe, translate } from '../../core/i18n';
import { StudentContext } from '../../core/student-context';
import { TeacherCard } from '../../shared/teacher-card';
import { looksLikePhone } from '../../shared/validation';

/** A friend opens the invite link /join/:code, enters their name and WhatsApp, and gets their own booking link. */
@Component({
  selector: 'dose-join-group',
  imports: [FormsModule, RouterLink, TeacherCard, TranslatePipe],
  template: `
    <section class="page">
      @if (loadError()) {
        <div class="dose-card box center">
          <h1 class="page-title">{{ 'invite.notFound' | t }}</h1>
          <p class="muted">{{ loadError() }}</p>
          <a class="dose-btn dose-btn--ghost" routerLink="/">{{ 'booking.home' | t }}</a>
        </div>
      } @else if (!info()) {
        <div class="skeleton"></div>
      } @else {
        @let i = info()!;
        @let s = i.session;
        <h1 class="page-title">{{ 'invite.joinTitle' | t: { name: i.organizerFirstName } }}</h1>
        <div class="dose-card box">
          <h2 dir="auto">{{ s.subjectName }} — {{ s.scopeTitle }}</h2>
          <ul class="info-list">
            <li><span>{{ 'details.when' | t }}</span><span>{{ date(s.startUtc) }} — {{ time(s.startUtc) }}</span></li>
            <li><span>{{ 'details.duration' | t }}</span><span>{{ duration(s.durationMinutes) }}</span></li>
            <li><span>{{ 'common.grade' | t }}</span><span>{{ s.gradeName }} · {{ s.studyTrackName }}</span></li>
            <li><span>{{ 'invite.seatsLeft' | t }}</span><span>{{ i.seatsLeft }}</span></li>
          </ul>
          <dose-teacher-card [teacher]="i.teacher" />
          <p class="alert alert--info small">{{ 'invite.free' | t: { name: i.organizerFirstName } }}</p>
        </div>

        @if (i.canJoin) {
          <form class="dose-card box" (ngSubmit)="join(i)">
            <h2>{{ 'invite.yourDetails' | t }}</h2>
            <label class="field" [class.has-error]="invalid()['name']"><span>{{ 'booking.studentName' | t }} <span class="req">*</span></span>
              <input name="name" [(ngModel)]="name" maxlength="100" autocomplete="name" />
              @if (invalid()['name']; as m) { <span class="field-error">{{ m }}</span> }</label>
            <label class="field" [class.has-error]="invalid()['phone']"><span>{{ 'booking.whatsapp' | t }} <span class="req">*</span></span>
              <input name="phone" [(ngModel)]="phone" inputmode="tel" dir="ltr" placeholder="01xxxxxxxxx" autocomplete="tel" />
              @if (invalid()['phone']; as m) { <span class="field-error">{{ m }}</span> } @else { <span class="hint">{{ 'booking.whatsappHint' | t }}</span> }</label>
            @if (i.requiresGuardianContact) {
              <label class="field" [class.has-error]="invalid()['gname']"><span>{{ 'booking.guardianName' | t }} <span class="req">*</span></span>
                <input name="gname" [(ngModel)]="guardianName" maxlength="100" />
                @if (invalid()['gname']; as m) { <span class="field-error">{{ m }}</span> }</label>
              <label class="field" [class.has-error]="invalid()['gphone']"><span>{{ 'booking.guardianPhone' | t }} <span class="req">*</span></span>
                <input name="gphone" [(ngModel)]="guardianPhone" inputmode="tel" dir="ltr" placeholder="01xxxxxxxxx" />
                @if (invalid()['gphone']; as m) { <span class="field-error">{{ m }}</span> }</label>
            }
            <label class="check" [class.has-error]="invalid()['terms']"><input type="checkbox" name="terms" [(ngModel)]="accept" />
              <span>{{ 'booking.acceptTerms' | t }}</span></label>
            @if (error()) { <p class="alert alert--error" role="alert">{{ error() }}</p> }
            <button class="dose-btn dose-btn--primary btn-block" type="submit" [disabled]="busy()">{{ (busy() ? 'common.saving' : 'invite.join') | t }}</button>
          </form>
        } @else {
          <p class="alert">{{ 'invite.full' | t }}</p>
        }
      }
    </section>
  `,
  styles: `
    .box { padding: 18px; display: grid; gap: 14px; }
    .box h2 { margin: 0; font-size: 1.1rem; color: var(--dose-navy); }
    .center { text-align: center; justify-items: center; }
  `,
})
export class JoinGroup {
  private readonly api = inject(Api);
  private readonly router = inject(Router);
  private readonly context = inject(StudentContext);
  readonly code = input.required<string>();

  protected readonly info = signal<InviteDetails | null>(null);
  protected readonly loadError = signal('');
  protected readonly error = signal('');
  protected readonly busy = signal(false);
  protected readonly invalid = signal<Record<string, string>>({});
  protected name = '';
  protected phone = '';
  protected guardianName = '';
  protected guardianPhone = '';
  protected accept = false;
  protected readonly date = fmtDate;
  protected readonly time = fmtTime;
  protected readonly duration = fmtDuration;

  constructor() {
    effect(() => {
      const code = this.code();
      this.api.invite(code).then((i) => {
        this.info.set(i);
        this.context.setSupport({ step: 'support.step.joining', session: `${i.session.subjectName} — ${i.session.scopeTitle}`,
          sessionTime: `${fmtDate(i.session.startUtc)} ${fmtTime(i.session.startUtc)}` });
      }).catch((e) => this.loadError.set(errorMessage(e)));
    });
  }

  protected async join(i: InviteDetails) {
    const e: Record<string, string> = {};
    if (this.name.trim().length < 2) e['name'] = translate('validation.name');
    if (!looksLikePhone(this.phone)) e['phone'] = translate('validation.phone');
    if (i.requiresGuardianContact && this.guardianName.trim().length < 2) e['gname'] = translate('validation.guardianName');
    if (i.requiresGuardianContact && !looksLikePhone(this.guardianPhone)) e['gphone'] = translate('validation.phone');
    if (!this.accept) e['terms'] = translate('validation.acceptTerms');
    this.invalid.set(e);
    if (Object.keys(e).length) { this.error.set(translate('validation.fixFields')); return; }

    this.busy.set(true);
    this.error.set('');
    try {
      const res = await this.api.joinGroup(this.code(), {
        studentName: this.name.trim(), contactPhone: this.phone.trim(),
        guardianName: this.guardianName.trim() || null, guardianPhone: this.guardianPhone.trim() || null, acceptTerms: this.accept,
      });
      myBookings.add(res.reference, res.accessToken);
      await this.router.navigate(['/b', res.accessToken]);
    } catch (err) {
      this.error.set(errorMessage(err));
    } finally {
      this.busy.set(false);
    }
  }
}
