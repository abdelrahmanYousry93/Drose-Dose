import { Component, WritableSignal, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Api, Lookup, PriceOptions, ServiceType, Stage } from '../../core/api';
import { errorMessage, fmtMoney } from '../../core/format';
import { TranslatePipe, translate } from '../../core/i18n';
import { PublicSettingsService } from '../../core/public-settings.service';

// Values are the stable codes the API accepts; labels are translated.
const DAYS = [['السبت', 'sat'], ['الأحد', 'sun'], ['الاثنين', 'mon'], ['الثلاثاء', 'tue'], ['الأربعاء', 'wed'], ['الخميس', 'thu'], ['الجمعة', 'fri'], ['أي يوم', 'anyDay']]
  .map(([value, key]) => ({ value, label: translate(`request.days.${key}`) }));
const TIMES = [['صباحاً', 'morning'], ['بعد الظهر', 'afternoon'], ['مساءً', 'evening'], ['أي وقت', 'anyTime']]
  .map(([value, key]) => ({ value, label: translate(`request.times.${key}`) }));

/** "No suitable session? Request one" (spec 7, 9.1). Prices shown here are indicative from the pricing plans. */
@Component({
  selector: 'dose-request-session',
  imports: [FormsModule, RouterLink, TranslatePipe],
  template: `
    <section class="page">
      <h1 class="page-title">{{ 'sessions.requestNew' | t }}</h1>
      <p class="muted">{{ 'request.lead' | t }}</p>

      @if (done(); as d) {
        <div class="dose-card box center">
          <span class="ok">✓</span>
          <h2>{{ 'request.doneTitle' | t }}</h2>
          <p>{{ 'request.doneRef' | t }} <strong dir="ltr">{{ d.reference }}</strong>. {{ 'request.doneBody' | t }}</p>
          @if (wa(d.whatsAppMessage); as link) {
            <a class="dose-btn dose-btn--whatsapp" [href]="link" target="_blank" rel="noopener">{{ 'request.sendWhatsapp' | t }}</a>
          }
          <a class="dose-btn dose-btn--ghost" routerLink="/">{{ 'booking.home' | t }}</a>
        </div>
      } @else {
        <form class="dose-card box" (ngSubmit)="submit()">
          <h2>{{ 'request.whatNeed' | t }}</h2>
          <label class="field"><span>{{ 'common.grade' | t }} <span class="req">*</span></span>
            <select name="grade" [(ngModel)]="gradeId" (ngModelChange)="onGrade()">
              <option value="">{{ 'home.chooseGrade' | t }}</option>
              @for (st of stages(); track st.id) {
                <optgroup [label]="st.name">@for (g of st.grades; track g.id) { <option [value]="g.id">{{ g.name }}</option> }</optgroup>
              }
            </select></label>
          <div class="two">
            <label class="field"><span>{{ 'common.subject' | t }} <span class="req">*</span></span>
              <select name="subject" [(ngModel)]="subjectId" (ngModelChange)="loadPrices()" [disabled]="!gradeId">
                <option value="">{{ 'request.choose' | t }}</option>
                @for (s of subjects(); track s.id) { <option [value]="s.id">{{ s.name }}</option> }
              </select></label>
            <label class="field"><span>{{ 'common.track' | t }} <span class="req">*</span></span>
              <select name="track" [(ngModel)]="trackId" (ngModelChange)="loadPrices()">
                @for (t of tracks(); track t.id) { <option [value]="t.id">{{ t.name }}</option> }
              </select></label>
          </div>
          <label class="field"><span>{{ 'request.service' | t }} <span class="req">*</span></span>
            <select name="service" [(ngModel)]="serviceTypeId" (ngModelChange)="loadPrices()">
              @for (s of services(); track s.id) { <option [value]="s.id">{{ s.name }}</option> }
            </select></label>
          <label class="field"><span>{{ 'request.scope' | t }} <span class="req">*</span></span>
            <textarea name="scope" [(ngModel)]="scopeText" maxlength="1000" [placeholder]="'request.scopePlaceholder' | t"></textarea></label>

          <h2>{{ 'request.when' | t }}</h2>
          <fieldset class="chips"><legend>{{ 'request.daysLabel' | t }} <span class="req">*</span></legend>
            @for (d of days; track d.value) {
              <label class="chip" [class.on]="pickedDays().includes(d.value)"><input type="checkbox" [checked]="pickedDays().includes(d.value)" (change)="toggle(pickedDays, d.value)" />{{ d.label }}</label>
            }
          </fieldset>
          <fieldset class="chips"><legend>{{ 'request.timesLabel' | t }} <span class="req">*</span></legend>
            @for (t of times; track t.value) {
              <label class="chip" [class.on]="pickedTimes().includes(t.value)"><input type="checkbox" [checked]="pickedTimes().includes(t.value)" (change)="toggle(pickedTimes, t.value)" />{{ t.label }}</label>
            }
          </fieldset>

          <h2>{{ 'request.modeTitle' | t }}</h2>
          <div class="modes" role="radiogroup">
            <label class="opt" [class.on]="mode === 'Group'"><input type="radio" name="mode" value="Group" [(ngModel)]="mode" />
              <strong>{{ 'request.group' | t }}</strong><span class="muted small">{{ 'request.groupHint' | t }}</span></label>
            <label class="opt" [class.on]="mode === 'Private'"><input type="radio" name="mode" value="Private" [(ngModel)]="mode" />
              <strong>{{ 'session.private' | t }}</strong>
              @if (prices()?.privatePrice; as p) { <span class="price small-price">{{ money(p, prices()!.currency) }}</span> }</label>
          </div>
          @if (mode === 'Group' && prices()?.groupOptions?.length) {
            <span class="label">{{ 'request.groupSize' | t }}</span>
            <div class="modes">
              @for (o of prices()!.groupOptions; track o.id) {
                <label class="opt" [class.on]="tierId === o.id"><input type="radio" name="tier" [value]="o.id" [(ngModel)]="tierId" />
                  <strong>{{ o.name }}</strong><span class="muted small">{{ 'request.studentsRange' | t: { min: o.minParticipants, max: o.maxParticipants } }}</span>
                  <span class="price small-price">{{ money(o.pricePerStudent, prices()!.currency) }} {{ 'session.perStudent' | t }}</span></label>
              }
            </div>
            <p class="muted small">{{ 'request.priceNote' | t }}</p>
          } @else if (subjectId && !prices()) {
            <p class="muted small">{{ 'request.priceLater' | t }}</p>
          }

          <h2>{{ 'request.contact' | t }}</h2>
          <label class="field"><span>{{ 'booking.studentName' | t }} <span class="req">*</span></span><input name="name" [(ngModel)]="studentName" maxlength="100" /></label>
          <label class="field"><span>{{ 'booking.whatsapp' | t }} <span class="req">*</span></span>
            <input name="phone" [(ngModel)]="contactPhone" inputmode="tel" dir="ltr" placeholder="01xxxxxxxxx" /></label>
          <label class="field"><span>{{ 'booking.guardianName' | t }} @if (guardianRequired()) { <span class="req">*</span> } @else { <span class="hint">{{ 'common.optional' | t }}</span> }</span>
            <input name="gname" [(ngModel)]="guardianName" maxlength="100" /></label>
          <label class="field"><span>{{ 'booking.guardianPhone' | t }} @if (guardianRequired()) { <span class="req">*</span> } @else { <span class="hint">{{ 'common.optional' | t }}</span> }</span>
            <input name="gphone" [(ngModel)]="guardianPhone" inputmode="tel" dir="ltr" placeholder="01xxxxxxxxx" /></label>
          <label class="field"><span>{{ 'request.notes' | t }} <span class="hint">{{ 'common.optional' | t }}</span></span><textarea name="notes" [(ngModel)]="notes" maxlength="1000"></textarea></label>

          @if (error()) { <p class="alert alert--error" role="alert">{{ error() }}</p> }
          <button class="dose-btn dose-btn--primary btn-block" type="submit" [disabled]="busy()">{{ (busy() ? 'request.sending' : 'request.send') | t }}</button>
        </form>
      }
    </section>
  `,
  styles: `
    .box { padding: 18px; display: grid; gap: 14px; }
    .box h2 { margin: 6px 0 0; font-size: 1.08rem; color: var(--dose-navy); }
    .two { display: grid; gap: 14px; grid-template-columns: 1fr 1fr; }
    .chips { border: 0; padding: 0; margin: 0; display: flex; flex-wrap: wrap; gap: 8px; }
    .chips legend { font-weight: 600; margin-bottom: 6px; }
    .chip { padding: 8px 14px; border: 1px solid var(--dose-border); border-radius: 999px; cursor: pointer; font-size: 0.92rem; background: var(--dose-surface); }
    .chip input { position: absolute; opacity: 0; width: 1px; height: 1px; }
    .chip.on { background: var(--dose-primary); border-color: var(--dose-primary); color: #fff; }
    .chip:focus-within { outline: 3px solid var(--dose-accent); }
    .modes { display: grid; gap: 8px; grid-template-columns: repeat(auto-fit, minmax(150px, 1fr)); }
    .opt { display: grid; gap: 2px; padding: 12px; border: 2px solid var(--dose-border); border-radius: var(--dose-radius-sm); cursor: pointer; position: relative; }
    .opt input { position: absolute; opacity: 0; }
    .opt.on { border-color: var(--dose-primary); background: var(--dose-primary-50); }
    .opt:focus-within { outline: 3px solid var(--dose-accent); }
    .label { font-weight: 600; }
    .small-price { font-size: 1rem; }
    .center { text-align: center; justify-items: center; }
    .center p { margin: 0; }
    .ok { width: 60px; height: 60px; border-radius: 50%; display: grid; place-items: center; background: var(--dose-success); color: #fff; font-size: 1.8rem; }
  `,
})
export class RequestSession {
  private readonly api = inject(Api);
  private readonly settings = inject(PublicSettingsService);
  protected readonly days = DAYS;
  protected readonly times = TIMES;
  protected readonly money = fmtMoney;

  protected readonly stages = signal<Stage[]>([]);
  protected readonly tracks = signal<Lookup[]>([]);
  protected readonly services = signal<ServiceType[]>([]);
  protected readonly subjects = signal<Lookup[]>([]);
  protected readonly prices = signal<PriceOptions | null>(null);
  protected readonly pickedDays = signal<string[]>([]);
  protected readonly pickedTimes = signal<string[]>([]);
  protected readonly error = signal('');
  protected readonly busy = signal(false);
  protected readonly done = signal<{ reference: string; whatsAppMessage: string } | null>(null);
  private readonly gradeSig = signal('');
  protected readonly guardianRequired = computed(() =>
    this.stages().some((s) => s.requiresGuardianContact && s.grades.some((g) => g.id === this.gradeSig())));

  protected gradeId = '';
  protected subjectId = '';
  protected trackId = '';
  protected serviceTypeId = '';
  protected scopeText = '';
  protected mode: 'Group' | 'Private' = 'Group';
  protected tierId: string | null = null;
  protected studentName = '';
  protected contactPhone = '';
  protected guardianName = '';
  protected guardianPhone = '';
  protected notes = '';

  constructor() {
    Promise.all([this.api.stages(), this.api.studyTracks(), this.api.services()]).then(([st, t, sv]) => {
      this.stages.set(st);
      this.tracks.set(t);
      this.services.set(sv);
      this.trackId = t[0]?.id ?? '';
      this.serviceTypeId = sv[0]?.id ?? '';
    }).catch((e) => this.error.set(errorMessage(e)));
  }

  protected wa(message: string) { return this.settings.whatsAppLink(message); }

  protected toggle(list: WritableSignal<string[]>, value: string) {
    list.update((l) => (l.includes(value) ? l.filter((x) => x !== value) : [...l, value]));
  }

  protected async onGrade() {
    this.gradeSig.set(this.gradeId);
    this.subjectId = '';
    this.prices.set(null);
    this.subjects.set(this.gradeId ? await this.api.subjects(this.gradeId).catch(() => []) : []);
  }

  protected async loadPrices() {
    this.prices.set(null);
    if (!this.gradeId || !this.subjectId || !this.trackId || !this.serviceTypeId) return;
    const p = await this.api.priceOptions({
      serviceTypeId: this.serviceTypeId, gradeId: this.gradeId, studyTrackId: this.trackId, subjectId: this.subjectId,
    }).catch(() => null);
    this.prices.set(p);
    this.tierId = p?.groupOptions[0]?.id ?? null;
  }

  protected async submit() {
    this.busy.set(true);
    this.error.set('');
    try {
      const res = await this.api.createRequest({
        studentName: this.studentName, contactPhone: this.contactPhone,
        guardianName: this.guardianName || null, guardianPhone: this.guardianPhone || null,
        gradeId: this.gradeId, studyTrackId: this.trackId, subjectId: this.subjectId, serviceTypeId: this.serviceTypeId,
        scopeText: this.scopeText, preferredDays: this.pickedDays(), preferredTimes: this.pickedTimes(),
        mode: this.mode, preferredTierId: this.mode === 'Group' ? this.tierId : null, notes: this.notes || null,
      });
      this.done.set(res);
      window.scrollTo({ top: 0 });
    } catch (e) {
      this.error.set(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }
}
