import { Component, computed, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { Api, ApiError, Lookup, ServiceType, Slot, Slots, Stage } from '../../core/api';
import { errorMessage, fmtDate, fmtDuration, fmtMoney, fmtTime, myBookings } from '../../core/format';
import { TranslatePipe, translate } from '../../core/i18n';
import { StudentContext } from '../../core/student-context';

interface Day { key: string; label: string; slots: Slot[] }

/**
 * Book a private session (just me) or a private group (me and my friends): pick the size, see the teachers' free
 * times and the price, and book. The organizer pays for every seat at the group price and gets an invite code for
 * friends. If no time suits, the student sends a request instead (prefilled with the same choices).
 */
@Component({
  selector: 'dose-private-booking',
  imports: [FormsModule, RouterLink, TranslatePipe],
  template: `
    <section class="page">
      <h1 class="page-title">{{ 'private.title' | t }}</h1>
      <p class="muted">{{ 'private.lead' | t }}</p>

      <div class="dose-card box">
        <h2>{{ 'private.what' | t }}</h2>
        @if (editingWhat()) {
          <label class="field" [class.has-error]="invalid()['grade']"><span>{{ 'common.grade' | t }} <span class="req">*</span></span>
            <select name="grade" [ngModel]="gradeId()" (ngModelChange)="onGrade($event)">
              <option value="">{{ 'home.chooseGrade' | t }}</option>
              @for (st of stages(); track st.id) {
                <optgroup [label]="st.name">@for (g of st.grades; track g.id) { <option [value]="g.id">{{ g.name }}</option> }</optgroup>
              }
            </select>
            @if (invalid()['grade']; as m) { <span class="field-error">{{ m }}</span> }</label>
          <div class="two">
            <label class="field" [class.has-error]="invalid()['subject']"><span>{{ 'common.subject' | t }} <span class="req">*</span></span>
              <select name="subject" [ngModel]="subjectId()" (ngModelChange)="subjectId.set($event); reset()" [disabled]="!gradeId()">
                <option value="">{{ 'request.choose' | t }}</option>
                @for (s of subjects(); track s.id) { <option [value]="s.id">{{ s.name }}</option> }
              </select>
              @if (invalid()['subject']; as m) { <span class="field-error">{{ m }}</span> }</label>
            <label class="field"><span>{{ 'common.track' | t }} <span class="req">*</span></span>
              <select name="track" [ngModel]="trackId()" (ngModelChange)="trackId.set($event); reset()">
                @for (t of tracks(); track t.id) { <option [value]="t.id">{{ t.name }}</option> }
              </select></label>
          </div>
        } @else {
          <div class="summary">
            <span>{{ gradeName() }} · {{ subjectName() }} · {{ trackName() }}</span>
            <button type="button" class="link-btn" (click)="editingWhat.set(true)">{{ 'common.change' | t }}</button>
          </div>
        }
        <div class="services" role="radiogroup" [attr.aria-label]="'request.service' | t">
          @for (s of services(); track s.id) {
            <label class="opt" [class.on]="serviceTypeId() === s.id">
              <input type="radio" name="service" [value]="s.id" [ngModel]="serviceTypeId()" (ngModelChange)="serviceTypeId.set($event); reset()" />
              <strong>{{ s.name }}</strong>@if (s.description) { <span class="muted small">{{ s.description }}</span> }</label>
          }
        </div>
        <label class="field"><span>{{ 'private.scope' | t }} <span class="hint">{{ 'common.optional' | t }}</span></span>
          <input name="scope" [(ngModel)]="scope" maxlength="200" [placeholder]="'private.scopePlaceholder' | t" /></label>
      </div>

      <div class="dose-card box">
        <h2>{{ 'private.who' | t }}</h2>
        <div class="services" role="radiogroup">
          <label class="opt" [class.on]="size() === 1"><input type="radio" name="who" [checked]="size() === 1" (change)="setSize(1)" />
            <strong>{{ 'private.alone' | t }}</strong><span class="muted small">{{ 'private.aloneHint' | t }}</span></label>
          <label class="opt" [class.on]="size() > 1"><input type="radio" name="who" [checked]="size() > 1" (change)="setSize(Math.max(2, groupSize))" />
            <strong>{{ 'private.friends' | t }}</strong><span class="muted small">{{ 'private.friendsHint' | t }}</span></label>
        </div>
        @if (size() > 1) {
          <div class="stepper" role="group" [attr.aria-label]="'private.howMany' | t">
            <span>{{ 'private.howMany' | t }}</span>
            <button type="button" (click)="setSize(size() - 1)" [disabled]="size() <= 2" [attr.aria-label]="'private.less' | t">−</button>
            <strong aria-live="polite">{{ size() }}</strong>
            <button type="button" (click)="setSize(size() + 1)" [disabled]="size() >= maxSize()" [attr.aria-label]="'private.more' | t">+</button>
          </div>
          <p class="muted small">{{ 'private.friendsNote' | t }}</p>
        }
        <button class="dose-btn dose-btn--primary btn-block" type="button" (click)="loadSlots()" [disabled]="loading()">
          {{ (loading() ? 'common.loading' : 'private.showTimes') | t }}</button>
      </div>

      @if (noPrice()) {
        <div class="alert alert--info no-price" role="status">
          <strong>{{ 'private.noPriceTitle' | t }}</strong>
          <span>{{ 'private.noPriceBody' | t }}</span>
          <a class="dose-btn dose-btn--primary btn-block" routerLink="/request-session" [queryParams]="requestParams()">{{ 'private.requestPrice' | t }}</a>
        </div>
      } @else if (error()) { <p class="alert alert--error" role="alert">{{ error() }}</p> }

      @if (slots(); as sl) {
        <div class="dose-card box">
          <div class="price-box">
            <span>{{ (sl.size === 1 ? 'private.priceAlone' : 'private.priceGroup') | t: { n: sl.size, each: money(sl.pricePerStudent, sl.currency) } }}</span>
            <strong class="price">{{ money(sl.total, sl.currency) }}</strong>
            <span class="muted small">{{ 'private.duration' | t: { d: duration(sl.durationMinutes) } }}</span>
          </div>
          @if (days().length) {
            <h2>{{ 'private.pickTime' | t }}</h2>
            @for (d of days(); track d.key) {
              <div class="day">
                <h3>{{ d.label }}</h3>
                <div class="times">
                  @for (s of d.slots; track s.teacherId + s.startUtc) {
                    <button type="button" class="time" [class.on]="picked() === s" (click)="picked.set(s)">
                      <strong>{{ time(s.startUtc) }}</strong><span class="small">{{ s.teacherName }}</span></button>
                  }
                </div>
              </div>
            }
            @if (picked(); as p) {
              <p class="alert alert--info">{{ 'private.picked' | t: { when: date(p.startUtc) + ' ' + time(p.startUtc), teacher: p.teacherName } }}</p>
              <button class="dose-btn dose-btn--primary btn-block" type="button" (click)="book()" [disabled]="booking()">
                {{ (booking() ? 'common.saving' : 'private.book') | t }}</button>
            }
          } @else {
            <p class="muted">{{ 'private.noTimes' | t }}</p>
          }
          <p class="small center">{{ 'private.noneSuits' | t }}
            <a routerLink="/request-session" [queryParams]="requestParams()">{{ 'private.sendRequest' | t }}</a></p>
        </div>
      }
    </section>
  `,
  styles: `
    .box { padding: 18px; display: grid; gap: 14px; }
    .no-price { display: grid; gap: 10px; }
    .box h2 { margin: 0; font-size: 1.08rem; color: var(--dose-navy); }
    .two { display: grid; gap: 14px; grid-template-columns: 1fr 1fr; }
    .summary { display: flex; flex-wrap: wrap; gap: 8px; align-items: center; justify-content: space-between; padding: 10px 12px; border-radius: var(--dose-radius-sm); background: var(--dose-primary-50); font-weight: 600; }
    .link-btn { border: 0; background: none; color: var(--dose-primary); text-decoration: underline; cursor: pointer; font: inherit; }
    .services { display: grid; gap: 8px; grid-template-columns: repeat(auto-fit, minmax(160px, 1fr)); }
    .opt { display: grid; gap: 2px; padding: 12px; border: 2px solid var(--dose-border); border-radius: var(--dose-radius-sm); cursor: pointer; position: relative; }
    .opt input { position: absolute; opacity: 0; }
    .opt.on { border-color: var(--dose-primary); background: var(--dose-primary-50); }
    .opt:focus-within { outline: 3px solid var(--dose-accent); }
    .stepper { display: flex; align-items: center; gap: 12px; font-weight: 600; }
    .stepper button { width: 40px; height: 40px; border-radius: 50%; border: 1px solid var(--dose-border); background: var(--dose-surface); font-size: 1.3rem; cursor: pointer; }
    .stepper strong { min-width: 2ch; text-align: center; font-size: 1.3rem; }
    .price-box { display: grid; gap: 2px; justify-items: center; text-align: center; padding: 12px; border-radius: var(--dose-radius-sm); background: var(--dose-bg); }
    .day h3 { margin: 6px 0; font-size: 0.98rem; }
    .times { display: flex; flex-wrap: wrap; gap: 8px; }
    .time { display: grid; gap: 2px; min-width: 104px; padding: 8px 12px; border: 1px solid var(--dose-border); border-radius: 10px; background: var(--dose-surface); cursor: pointer; font: inherit; text-align: center; }
    .time.on { border-color: var(--dose-primary); background: var(--dose-primary); color: #fff; }
    .center { text-align: center; }
  `,
})
export class PrivateBooking {
  private readonly api = inject(Api);
  private readonly router = inject(Router);
  private readonly context = inject(StudentContext);
  protected readonly Math = Math;

  // Prefill from the sessions list / home choice.
  readonly gradeIdParam = input<string | undefined>(undefined, { alias: 'gradeId' });
  readonly subjectIdParam = input<string | undefined>(undefined, { alias: 'subjectId' });
  readonly studyTrackIdParam = input<string | undefined>(undefined, { alias: 'studyTrackId' });

  protected readonly stages = signal<Stage[]>([]);
  protected readonly tracks = signal<Lookup[]>([]);
  protected readonly subjects = signal<Lookup[]>([]);
  protected readonly services = signal<ServiceType[]>([]);
  protected readonly gradeId = signal('');
  protected readonly subjectId = signal('');
  protected readonly trackId = signal('');
  protected readonly serviceTypeId = signal('');
  protected readonly size = signal(1);
  protected readonly maxSize = signal(15);
  protected groupSize = 3;
  protected scope = '';
  protected readonly editingWhat = signal(true);
  protected readonly slots = signal<Slots | null>(null);
  protected readonly picked = signal<Slot | null>(null);
  protected readonly loading = signal(false);
  protected readonly booking = signal(false);
  protected readonly error = signal('');
  /** No price set for this subject yet: offer to send a request instead of a dead end. */
  protected readonly noPrice = signal(false);
  protected readonly invalid = signal<Record<string, string>>({});

  protected readonly money = fmtMoney;
  protected readonly duration = fmtDuration;
  protected readonly time = fmtTime;
  protected readonly date = fmtDate;

  protected readonly gradeName = computed(() => this.stages().flatMap((s) => s.grades).find((g) => g.id === this.gradeId())?.name ?? '');
  protected readonly subjectName = computed(() => this.subjects().find((s) => s.id === this.subjectId())?.name ?? '');
  protected readonly trackName = computed(() => this.tracks().find((t) => t.id === this.trackId())?.name ?? '');

  protected readonly days = computed<Day[]>(() => {
    const map = new Map<string, Day>();
    for (const s of this.slots()?.slots ?? []) {
      const key = new Date(s.startUtc).toLocaleDateString('en-CA', { timeZone: 'Africa/Cairo' });
      if (!map.has(key)) map.set(key, { key, label: fmtDate(s.startUtc), slots: [] });
      map.get(key)!.slots.push(s);
    }
    return [...map.values()];
  });

  protected readonly requestParams = computed(() => ({
    gradeId: this.gradeId() || null, subjectId: this.subjectId() || null, studyTrackId: this.trackId() || null,
    serviceTypeId: this.serviceTypeId() || null, mode: this.size() === 1 ? 'Private' : 'Group', groupSize: this.size() > 1 ? this.size() : null,
  }));

  constructor() {
    this.context.setSupport({ step: 'support.step.private' });
    Promise.all([this.api.stages(), this.api.studyTracks(), this.api.services()]).then(async ([st, t, sv]) => {
      this.stages.set(st);
      this.tracks.set(t);
      this.services.set(sv);
      this.serviceTypeId.set(sv[0]?.id ?? '');
      const p = this.context.profile();
      const grade = this.gradeIdParam() || p?.gradeId || '';
      if (grade && st.some((s) => s.grades.some((g) => g.id === grade))) {
        await this.onGrade(grade);
        const subject = this.subjectIdParam() || p?.subjectId || '';
        if (this.subjects().some((s) => s.id === subject)) this.subjectId.set(subject);
      }
      const track = this.studyTrackIdParam() || p?.studyTrackId || '';
      this.trackId.set(t.some((x) => x.id === track) ? track : (t[0]?.id ?? ''));
      // Everything already known: show a one-line summary instead of asking again.
      if (this.gradeId() && this.subjectId() && this.trackId()) this.editingWhat.set(false);
    }).catch((e) => this.error.set(errorMessage(e)));
  }

  protected async onGrade(id: string) {
    this.gradeId.set(id);
    this.subjectId.set('');
    this.reset();
    this.subjects.set(id ? await this.api.subjects(id).catch(() => []) : []);
  }

  protected reset() { this.slots.set(null); this.picked.set(null); this.error.set(''); this.noPrice.set(false); }

  protected setSize(n: number) {
    const v = Math.max(1, Math.min(n, this.maxSize()));
    if (v > 1) this.groupSize = v;
    this.size.set(v);
    this.reset();
  }

  protected async loadSlots() {
    const e: Record<string, string> = {};
    if (!this.gradeId()) e['grade'] = translate('validation.grade');
    if (!this.subjectId()) e['subject'] = translate('validation.subject');
    this.invalid.set(e);
    if (Object.keys(e).length) { this.editingWhat.set(true); this.error.set(translate('validation.fixFields')); return; }

    this.loading.set(true);
    this.reset();
    try {
      const s = await this.api.privateSlots({
        gradeId: this.gradeId(), subjectId: this.subjectId(), studyTrackId: this.trackId(), serviceTypeId: this.serviceTypeId(), size: String(this.size()),
      });
      this.maxSize.set(Math.max(2, s.maxSize));
      this.slots.set(s);
      this.context.saveProfile({
        gradeId: this.gradeId(), gradeName: this.gradeName(), subjectId: this.subjectId(), subjectName: this.subjectName(),
        studyTrackId: this.trackId(), studyTrackName: this.trackName(),
      });
    } catch (err) {
      if (err instanceof ApiError && err.code === 'pricing.not_configured') this.noPrice.set(true);
      else this.error.set(errorMessage(err));
    } finally {
      this.loading.set(false);
    }
  }

  protected async book() {
    const p = this.picked();
    if (!p) return;
    this.booking.set(true);
    this.error.set('');
    try {
      const hold = await this.api.createPrivateBooking({
        gradeId: this.gradeId(), subjectId: this.subjectId(), studyTrackId: this.trackId(), serviceTypeId: this.serviceTypeId(),
        size: this.size(), teacherId: p.teacherId, startUtc: p.startUtc, scopeText: this.scope.trim() || null,
      });
      myBookings.add(hold.reference, hold.accessToken);
      await this.router.navigate(['/b', hold.accessToken]);
    } catch (e) {
      this.error.set(errorMessage(e));
      await this.loadSlots();
    } finally {
      this.booking.set(false);
    }
  }

}
