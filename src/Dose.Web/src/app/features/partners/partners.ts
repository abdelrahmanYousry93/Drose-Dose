import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Api, PledgeWall } from '../../core/api';
import { errorMessage } from '../../core/format';
import { TranslatePipe, translate } from '../../core/i18n';
import { MarketingTracker } from '../../core/marketing-tracker';
import { looksLikePhone } from '../../shared/validation';
import { PublicSettingsService } from '../../core/public-settings.service';
import { PartnersEvent } from './partners-event';

/** Lessons on the event's medicine-bottle cards (QR src = ses26-<slug>). */
const BOTTLES: Record<string, string> = {
  longdiv: 'القسمة المطوّلة', fractions: 'الكسور', density: 'الكثافة', speed: 'السرعة والعجلة', irab: 'الإعراب',
  perfect: 'Present Perfect', ohm: 'قانون أوم', mole: 'المول', percent: 'النسبة المئوية', pythagoras: 'فيثاغورس',
  photosynthesis: 'البناء الضوئي', cell: 'الخلية', kana: 'كان وإنّ', passive: 'Passive Voice', newton: 'قانون نيوتن التاني', ph: 'الأحماض والقواعد',
};

const INTERESTS = ['fund', 'students', 'employees', 'content', 'other'] as const;

/**
 * "اكتب روشتة لطالب" — the partners page (QR target at events). An impact calculator, the live pledge total and a
 * non-binding pledge form that alerts the owner on Telegram. No payment here.
 */
@Component({
  selector: 'dose-partners',
  imports: [FormsModule, RouterLink, TranslatePipe, PartnersEvent],
  template: `
    <section class="page">
      @if (event() && !showFull()) {
        <dose-partners-event [lesson]="bottle()" (full)="showFull.set(true)" />
      } @else {
      <div class="hero">
        <div class="bottle" aria-hidden="true">
          <div class="cap"></div><div class="neck"></div>
          <div class="body">
            <div class="label">
              <strong class="brand">{{ 'partners.bName' | t }}</strong>
              <span>{{ 'partners.bFor' | t }}</span>
              <span>{{ 'partners.bDose' | t }}</span>
              <span class="side">{{ 'partners.bSide' | t }}</span>
              <em>{{ 'partners.bNo' | t }}</em>
            </div>
          </div>
        </div>
        <div class="hero-text">
          <h1 class="page-title">{{ 'partners.heroTitle' | t }}</h1>
          <p class="muted">{{ 'partners.heroBody' | t }}</p>
          @if (wall(); as w) {
            <a class="live" routerLink="/partners/wall">
              @if (w.totalDoses > 0) {
                <span class="dot"></span>{{ 'partners.liveTitle' | t }}: {{ 'partners.liveBody' | t: { doses: num(w.totalDoses), orgs: w.organizations } }}
              } @else { {{ 'partners.liveEmpty' | t }} }
              <span class="go">{{ 'partners.seeWall' | t }} ←</span>
            </a>
          }
        </div>
      </div>

      <div class="dose-card box">
        <h2>{{ 'partners.calcTitle' | t }}</h2>
        <div class="grid4">
          <label class="field"><span>{{ 'partners.budget' | t }}</span><input type="number" inputmode="numeric" min="0" step="500" name="budget" [ngModel]="budget()" (ngModelChange)="budget.set(+$event || 0)" /></label>
          <label class="field"><span>{{ 'partners.dosePrice' | t }}</span><input type="number" inputmode="numeric" min="1" name="price" [ngModel]="price()" (ngModelChange)="price.set(+$event || 0)" /></label>
          <label class="field"><span>{{ 'partners.monthly' | t }}</span><input type="number" inputmode="numeric" min="1" name="monthly" [ngModel]="monthly()" (ngModelChange)="monthly.set(+$event || 0)" /></label>
          <label class="field"><span>{{ 'partners.months' | t }}</span><input type="number" inputmode="numeric" min="1" max="12" name="months" [ngModel]="months()" (ngModelChange)="months.set(+$event || 0)" /></label>
        </div>
        <p class="muted small">{{ 'partners.calcNote' | t }}</p>
        <div class="compare">
          <div class="old"><span class="k">{{ 'partners.oldWay' | t }}</span><strong>{{ num(oldStudents()) }}</strong><span>{{ 'partners.oldWayV' | t: { n: num(oldStudents()) } }}</span></div>
          <div class="new"><span class="k">{{ 'partners.doseWay' | t }}</span><strong>{{ num(doses()) }}</strong><span>{{ 'partners.doseWayV' | t: { n: num(doses()) } }}</span></div>
        </div>
        @if (factor() > 1) { <p class="factor">{{ 'partners.times' | t: { x: factor() } }}</p> }
        @if (doses() > 0) {
          <button type="button" class="dose-btn dose-btn--primary" (click)="pledgeThese()">{{ 'partners.pledgeThese' | t: { n: num(doses()) } }}</button>
        }
      </div>

      <div class="dose-card box" id="pledge">
        @if (done(); as d) {
          <div class="center">
            <span class="ok">✓</span>
            <h2>{{ 'partners.doneTitle' | t }}</h2>
            <p>{{ 'partners.doneBody' | t: { ref: d.reference } }}</p>
            @if (d.doses > 0) { <p class="strong">{{ 'partners.doneDoses' | t: { n: num(d.doses) } }}</p> }
            <a class="dose-btn dose-btn--primary" routerLink="/partners/wall">{{ 'partners.seeWall' | t }}</a>
            <button type="button" class="link-btn" (click)="done.set(null)">{{ 'partners.another' | t }}</button>
          </div>
        } @else {
          <form (ngSubmit)="submit()" class="form">
            <h2>{{ 'partners.formTitle' | t }}</h2>
            <p class="muted small">{{ 'partners.formLead' | t }}</p>
            <fieldset class="ways"><legend>{{ 'partners.interest' | t }} <span class="req">*</span></legend>
              @for (i of interests; track i) {
                <label class="opt" [class.on]="interest() === i"><input type="radio" name="interest" [checked]="interest() === i" (change)="interest.set(i)" />
                  <strong>{{ ('partners.i.' + i) | t }}</strong><span class="muted small">{{ ('partners.i.' + i + 'Hint') | t }}</span></label>
              }
            </fieldset>
            @if (interest() === 'fund') {
              <label class="field" [class.has-error]="bad()['doses']"><span>{{ 'partners.doses' | t }} <span class="req">*</span></span>
                <input type="number" inputmode="numeric" min="1" name="pdoses" [(ngModel)]="pledgeDoses" />
                @if (bad()['doses']; as m) { <span class="field-error">{{ m }}</span> }</label>
              <label class="check"><input type="checkbox" name="wall" [(ngModel)]="showOnWall" /> {{ 'partners.showOnWall' | t }}</label>
            }
            <div class="two">
              <label class="field" [class.has-error]="bad()['org']"><span>{{ 'partners.org' | t }} <span class="req">*</span></span>
                <input name="org" [(ngModel)]="organization" maxlength="120" autocomplete="organization" />
                @if (bad()['org']; as m) { <span class="field-error">{{ m }}</span> }</label>
              <label class="field"><span>{{ 'partners.role' | t }} <span class="hint">{{ 'common.optional' | t }}</span></span>
                <input name="role" [(ngModel)]="role" maxlength="100" autocomplete="organization-title" /></label>
              <label class="field" [class.has-error]="bad()['name']"><span>{{ 'partners.name' | t }} <span class="req">*</span></span>
                <input name="name" [(ngModel)]="contactName" maxlength="100" autocomplete="name" />
                @if (bad()['name']; as m) { <span class="field-error">{{ m }}</span> }</label>
              <label class="field" [class.has-error]="bad()['phone']"><span>{{ 'partners.phone' | t }} <span class="req">*</span></span>
                <input name="phone" [(ngModel)]="phone" dir="ltr" inputmode="tel" autocomplete="tel" maxlength="20" />
                @if (bad()['phone']; as m) { <span class="field-error">{{ m }}</span> }</label>
            </div>
            <label class="field"><span>{{ 'partners.note' | t }} <span class="hint">{{ 'common.optional' | t }}</span></span>
              <textarea name="note" [(ngModel)]="note" maxlength="1000"></textarea></label>
            @if (error()) { <p class="alert alert--error" role="alert">{{ error() }}</p> }
            <button class="dose-btn dose-btn--primary btn-block" type="submit" [disabled]="busy()">
              {{ (busy() ? 'partners.sending' : interest() === 'fund' ? 'partners.send' : 'partners.sendOther') | t }}</button>
          </form>
        }
      </div>
      }
    </section>
  `,
  styles: `
    .event { padding: 22px 18px; display: grid; gap: 12px; border: 2px solid var(--dose-accent); }
    .event .hello { justify-self: start; background: var(--dose-accent); color: var(--dose-navy); font-weight: 800; border-radius: 999px; padding: 2px 12px; font-size: .85rem; }
    .event h1 { margin: 0; font-size: 1.5rem; line-height: 1.35; color: var(--dose-navy); }
    .what { margin: 0; padding: 0; list-style: none; display: grid; gap: 6px; font-weight: 600; }
    .acts { display: grid; gap: 10px; }
    .act { display: grid; gap: 2px; text-align: start; padding: 14px 16px; border-radius: var(--dose-radius-sm); border: 2px solid var(--dose-primary); background: var(--dose-primary-50); color: var(--dose-navy); font: inherit; cursor: pointer; text-decoration: none; }
    .act b { font-size: 1.05rem; } .act span { font-size: .85rem; color: var(--dose-text-muted); }
    .act.wa { border-color: #25d366; background: #e9fbef; }

    .hero { display: grid; grid-template-columns: auto 1fr; gap: 24px; align-items: center; }
    @media (max-width: 640px) { .hero { grid-template-columns: 1fr; justify-items: center; text-align: center; } }
    .hero-text { display: grid; gap: 10px; }
    .bottle { width: 190px; display: grid; justify-items: center; filter: drop-shadow(0 10px 18px rgb(16 47 120 / .18)); }
    .cap { width: 92px; height: 34px; border-radius: 8px 8px 4px 4px; background: repeating-linear-gradient(90deg, #0b1f6b 0 6px, #14308f 6px 9px); }
    .neck { width: 72px; height: 14px; background: #8a4b12; }
    .body { width: 190px; min-height: 240px; border-radius: 28px 28px 22px 22px; background: linear-gradient(90deg, #7a3e0c, #b8621a 40%, #8a4b12); padding: 26px 12px; display: grid; align-items: center; }
    .label { background: #fff; border-radius: 10px; padding: 12px 10px; display: grid; gap: 4px; font-size: .72rem; line-height: 1.5; color: var(--dose-text); border-top: 6px solid var(--dose-primary); border-bottom: 6px solid var(--dose-accent); text-align: start; }
    .brand { font-size: 1.1rem; color: var(--dose-navy); }
    .side { color: var(--dose-success); font-weight: 700; }
    .label em { font-style: normal; font-weight: 800; color: var(--dose-primary); }
    .live { display: flex; flex-wrap: wrap; gap: 8px; align-items: center; padding: 10px 14px; border-radius: var(--dose-radius-sm); background: var(--dose-success-50); color: var(--dose-text); text-decoration: none; font-weight: 700; }
    .live .go { color: var(--dose-primary); margin-inline-start: auto; }
    .dot { width: 10px; height: 10px; border-radius: 50%; background: var(--dose-success); animation: pulse 1.4s infinite; }
    @keyframes pulse { 50% { opacity: .3; } }
    .box { padding: 20px; display: grid; gap: 14px; }
    h2 { margin: 0; font-size: 1.15rem; color: var(--dose-navy); }
    .grid4 { display: grid; grid-template-columns: repeat(auto-fit, minmax(150px, 1fr)); gap: 12px; }
    .compare { display: grid; grid-template-columns: 1fr 1fr; gap: 12px; }
    @media (max-width: 520px) { .compare { grid-template-columns: 1fr; } }
    .compare > div { border-radius: var(--dose-radius-sm); padding: 14px; display: grid; gap: 4px; }
    .old { background: #f3f4f7; color: var(--dose-text-muted); }
    .new { background: var(--dose-primary-50); border: 2px solid var(--dose-primary); }
    .compare strong { font-size: 2rem; line-height: 1.1; color: var(--dose-navy); }
    .old strong { color: var(--dose-text-muted); }
    .k { font-weight: 800; font-size: .85rem; }
    .factor { margin: 0; font-weight: 800; color: var(--dose-success); font-size: 1.1rem; }
    .form { display: grid; gap: 14px; }
    .ways { border: 0; padding: 0; margin: 0; display: grid; gap: 8px; }
    .ways legend { font-weight: 600; margin-bottom: 6px; }
    .opt { display: grid; gap: 2px; padding: 10px 12px; border: 1.5px solid var(--dose-border); border-radius: var(--dose-radius-sm); cursor: pointer; position: relative; padding-inline-start: 36px; }
    .opt input { position: absolute; inset-inline-start: 12px; top: 13px; }
    .opt.on { border-color: var(--dose-primary); background: var(--dose-primary-50); }
    .two { display: grid; grid-template-columns: 1fr 1fr; gap: 12px; }
    @media (max-width: 520px) { .two { grid-template-columns: 1fr; } }
    .check { display: flex; gap: 8px; align-items: center; font-weight: 600; }
    .center { display: grid; gap: 10px; justify-items: center; text-align: center; }
    .ok { width: 52px; height: 52px; border-radius: 50%; background: var(--dose-success-50); color: var(--dose-success); display: grid; place-items: center; font-size: 1.6rem; font-weight: 800; }
    .strong { font-weight: 800; color: var(--dose-success); }
    .small { font-size: .85rem; } p { margin: 0; }
  `,
})
export class Partners {
  private readonly api = inject(Api);
  private readonly tracker = inject(MarketingTracker);
  protected readonly settings = inject(PublicSettingsService);
  /** Visitors who scanned an event card (src ses26 / ses26-<lesson>) get a short "who we are + what next" block. */
  private readonly src = this.tracker.current() ?? '';
  protected readonly event = signal(this.src.startsWith('ses26'));
  protected readonly showFull = signal(false);
  protected readonly bottle = signal<string | null>(BOTTLES[this.src.replace(/^ses26-?/, '')] ?? null);
  protected waText() {
    return translate('event.waText', { lesson: this.bottle() ?? '' }).replace(/\s+\(\)/, '');
  }
  protected choose(i: 'fund' | 'students') {
    this.interest.set(i);
    document.getElementById('pledge')?.scrollIntoView({ behavior: 'smooth', block: 'start' });
  }
  protected readonly interests = INTERESTS;

  protected readonly budget = signal(10000);
  protected readonly price = signal(120);
  protected readonly monthly = signal(400);
  protected readonly months = signal(9);
  protected readonly doses = computed(() => this.price() > 0 ? Math.floor(this.budget() / this.price()) : 0);
  protected readonly oldStudents = computed(() => {
    const year = this.monthly() * this.months();
    return year > 0 ? Math.floor(this.budget() / year) : 0;
  });
  protected readonly factor = computed(() => {
    const o = this.oldStudents();
    return o > 0 ? Math.round(this.doses() / o) : 0;
  });

  protected readonly wall = signal<PledgeWall | null>(null);
  protected readonly interest = signal<(typeof INTERESTS)[number]>('fund');
  protected organization = '';
  protected contactName = '';
  protected role = '';
  protected phone = '';
  protected note = '';
  protected pledgeDoses: number | null = null;
  protected showOnWall = true;
  protected readonly bad = signal<Record<string, string>>({});
  protected readonly error = signal('');
  protected readonly busy = signal(false);
  protected readonly done = signal<{ reference: string; doses: number } | null>(null);

  constructor() {
    this.api.pledgeWall().then((w) => this.wall.set(w)).catch(() => null);
  }

  protected num(n: number) { return n.toLocaleString(document.documentElement.lang === 'en' ? 'en' : 'ar-EG'); }

  protected pledgeThese() {
    this.interest.set('fund');
    this.pledgeDoses = this.doses();
    document.getElementById('pledge')?.scrollIntoView({ behavior: 'smooth', block: 'start' });
  }

  protected async submit() {
    const req = translate('partners.required');
    const b: Record<string, string> = {};
    if (!this.organization.trim()) b['org'] = req;
    if (!this.contactName.trim()) b['name'] = req;
    if (!this.phone.trim()) b['phone'] = req; else if (!looksLikePhone(this.phone)) b['phone'] = translate('partners.badPhone');
    const fund = this.interest() === 'fund';
    if (fund && !(Number(this.pledgeDoses) > 0)) b['doses'] = translate('partners.badDoses');
    this.bad.set(b);
    if (Object.keys(b).length) return;
    this.busy.set(true);
    this.error.set('');
    try {
      const doses = fund ? Math.floor(Number(this.pledgeDoses)) : 0;
      const r = await this.api.pledge({
        organization: this.organization.trim(), contactName: this.contactName.trim(), contactPhone: this.phone.trim(),
        role: this.role.trim() || null, interest: this.interest(), doses, showOnWall: fund && this.showOnWall,
        note: this.note.trim() || null, source: this.tracker.current(),
      });
      this.tracker.track('request', r.reference);
      this.done.set({ reference: r.reference, doses });
      this.organization = this.contactName = this.role = this.phone = this.note = '';
      this.pledgeDoses = null;
      this.api.pledgeWall().then((w) => this.wall.set(w)).catch(() => null);
    } catch (e) {
      this.error.set(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }
}
