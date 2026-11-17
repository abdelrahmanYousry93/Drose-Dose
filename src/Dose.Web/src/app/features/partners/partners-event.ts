import { Component, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Api } from '../../core/api';
import { errorMessage } from '../../core/format';
import { TranslatePipe, translate } from '../../core/i18n';
import { MarketingTracker } from '../../core/marketing-tracker';
import { PublicSettingsService } from '../../core/public-settings.service';
import { looksLikePhone } from '../../shared/validation';

type Step = 'hook' | 'reveal' | 'how' | 'form' | 'done';
type Way = 'fund' | 'students';

/**
 * What someone sees after scanning a medicine-bottle card at the summit: a few taps, almost no reading.
 * 1. "You're holding a dose of <lesson>" + one question (monthly subscription or one dose?).
 * 2. One-line reveal of what Dose is.  3. Three big choices.  4. A three-field form (+ dose chips).  5. Thanks.
 * The pledge goes through the normal partners API (Telegram alert to the owner).
 */
@Component({
  selector: 'dose-partners-event',
  imports: [FormsModule, RouterLink, TranslatePipe],
  template: `
    <div class="flow">
      <div class="dots"><i [class.on]="n() >= 1"></i><i [class.on]="n() >= 2"></i><i [class.on]="n() >= 3"></i><i [class.on]="n() >= 4"></i></div>

      @switch (step()) {
        @case ('hook') {
          <div class="pill wobble" aria-hidden="true"><i></i><i></i></div>
          <h1>{{ (lesson() ? 'ev.holding' : 'ev.holdingAny') | t: { lesson: lesson() ?? '' } }}</h1>
          <p class="q">{{ 'ev.q' | t }}</p>
          <div class="two">
            <button type="button" class="big" (click)="answer(false)">📅<span>{{ 'ev.month' | t }}</span></button>
            <button type="button" class="big" (click)="answer(true)">💊<span>{{ 'ev.dose' | t }}</span></button>
          </div>
        }
        @case ('reveal') {
          <div class="emoji">{{ right() ? '👏' : '😅' }}</div>
          <p class="react">{{ (right() ? 'ev.right' : 'ev.wrong') | t }}</p>
          <h1 class="brand">{{ 'ev.is' | t }}</h1>
          <p class="one">{{ 'ev.oneLine' | t }}</p>
          <button type="button" class="go" (click)="step.set('how')">{{ 'ev.next' | t }} ←</button>
        }
        @case ('how') {
          <h1>{{ 'ev.howTitle' | t }}</h1>
          <button type="button" class="tile" (click)="pick('fund')"><b>🤝</b><span>{{ 'ev.fund' | t }}</span></button>
          <button type="button" class="tile" (click)="pick('students')"><b>🏫</b><span>{{ 'ev.students' | t }}</span></button>
          @if (settings.whatsAppLink(waText()); as link) {
            <a class="tile wa" [href]="link" target="_blank" rel="noopener"><b>💬</b><span>{{ 'ev.chat' | t }}</span></a>
          }
        }
        @case ('form') {
          <h1>{{ (way() === 'fund' ? 'ev.formFund' : 'ev.formStudents') | t }}</h1>
          @if (way() === 'fund') {
            <div class="chips">
              @for (c of chips; track c) {
                <button type="button" class="chip" [class.on]="doses() === c" (click)="doses.set(c)">💊 {{ c }}</button>
              }
            </div>
            @if (bad()['doses']) { <span class="field-error">{{ bad()['doses'] }}</span> }
          }
          <form class="form" (ngSubmit)="send()">
            <input name="org" [(ngModel)]="org" [placeholder]="'ev.org' | t" autocomplete="organization" maxlength="120" [class.err]="bad()['org']" />
            <input name="name" [(ngModel)]="name" [placeholder]="'ev.name' | t" autocomplete="name" maxlength="100" [class.err]="bad()['name']" />
            <input name="phone" [(ngModel)]="phone" [placeholder]="'ev.phone' | t" dir="ltr" inputmode="tel" autocomplete="tel" maxlength="20" [class.err]="bad()['phone']" />
            @if (error()) { <p class="alert alert--error">{{ error() }}</p> }
            <button class="go" type="submit" [disabled]="busy()">{{ (busy() ? 'partners.sending' : 'ev.send') | t }}</button>
          </form>
          <button type="button" class="back" (click)="step.set('how')">{{ 'ev.back' | t }}</button>
        }
        @case ('done') {
          <div class="rain" aria-hidden="true">@for (i of rain; track i) { <span [style.--i]="i">💊</span> }</div>
          <h1>{{ 'ev.thanks' | t }}</h1>
          @if (sentDoses() > 0) { <p class="one">{{ 'ev.thanksDoses' | t: { n: sentDoses() } }}</p> }
          <p class="one">{{ 'ev.callYou' | t }}</p>
          <a class="go" routerLink="/partners/wall">{{ 'ev.wall' | t }}</a>
        }
      }

      <button type="button" class="more" (click)="full.emit()">{{ 'ev.more' | t }}</button>
    </div>
  `,
  styles: `
    .flow { min-height: calc(100dvh - 150px); display: flex; flex-direction: column; align-items: center; justify-content: center; gap: 18px; text-align: center; padding: 12px 4px 20px; }
    .dots { display: flex; gap: 6px; } .dots i { width: 28px; height: 6px; border-radius: 3px; background: var(--dose-border); } .dots i.on { background: var(--dose-primary); }
    h1 { margin: 0; font-size: clamp(1.35rem, 6vw, 1.7rem); line-height: 1.35; color: var(--dose-navy); max-width: 22ch; }
    .q { margin: 0; font-size: 1.15rem; font-weight: 700; }
    .pill { width: 150px; height: 64px; border-radius: 32px; margin: 18px 0 6px; display: flex; overflow: hidden; box-shadow: 0 16px 40px rgb(11 31 107 / .25); }
    .pill i { flex: 1; } .pill i:first-child { background: var(--dose-primary); } .pill i:last-child { background: var(--dose-accent); }
    .wobble { animation: wob 2.4s ease-in-out infinite; } @keyframes wob { 0%,100% { transform: rotate(-12deg); } 50% { transform: rotate(12deg) translateY(-6px); } }
    .two { display: grid; grid-template-columns: 1fr 1fr; gap: 12px; width: 100%; max-width: 420px; }
    .big { display: grid; gap: 6px; justify-items: center; padding: 18px 10px; border-radius: 20px; border: 2px solid var(--dose-border); background: var(--dose-surface); font: inherit; font-size: 2.2rem; cursor: pointer; }
    .big span { font-size: 1rem; font-weight: 800; color: var(--dose-navy); }
    .big:active, .tile:active, .chip:active { transform: scale(.97); }
    .emoji { font-size: 3.4rem; line-height: 1; animation: pop .5s ease; } @keyframes pop { 0% { transform: scale(.3); } 70% { transform: scale(1.15); } }
    .react { margin: 0; font-weight: 800; color: var(--dose-text-muted); }
    .brand { font-size: 2rem; } .one { margin: 0; font-size: 1.1rem; font-weight: 600; max-width: 30ch; }
    .go { display: inline-block; background: var(--dose-primary); color: #fff; border: 0; border-radius: 999px; padding: 14px 34px; font: inherit; font-weight: 800; font-size: 1.1rem; cursor: pointer; text-decoration: none; }
    .tile { width: 100%; max-width: 420px; display: flex; align-items: center; gap: 14px; padding: 16px 18px; border-radius: 18px; border: 2px solid var(--dose-primary); background: var(--dose-primary-50); font: inherit; font-weight: 800; font-size: 1.1rem; color: var(--dose-navy); cursor: pointer; text-decoration: none; text-align: start; }
    .tile b { font-size: 1.8rem; } .tile.wa { border-color: #25d366; background: #e9fbef; }
    .chips { display: grid; grid-template-columns: repeat(4, 1fr); gap: 8px; width: 100%; max-width: 420px; }
    @media (max-width: 380px) { .chips { grid-template-columns: repeat(2, 1fr); } }
    .chip { border: 2px solid var(--dose-border); background: var(--dose-surface); border-radius: 999px; padding: 10px 16px; font: inherit; font-weight: 800; cursor: pointer; }
    .chip.on { border-color: var(--dose-primary); background: var(--dose-primary); color: #fff; }
    .form { display: grid; gap: 10px; width: 100%; max-width: 420px; }
    .form input { padding: 14px 16px; border-radius: 14px; border: 2px solid var(--dose-border); font: inherit; font-size: 1.05rem; }
    .form input.err { border-color: var(--dose-danger, #e5484d); }
    .back::before, .back::after { content: none !important; }
    .back, .more { background: none; border: 0; font: inherit; color: var(--dose-text-muted); cursor: pointer; text-decoration: underline; }
    .more { margin-top: 8px; font-size: .85rem; }
    .rain { position: relative; height: 90px; width: 100%; max-width: 320px; overflow: hidden; }
    .rain span { position: absolute; top: -30px; left: calc(var(--i) * 9%); font-size: 1.4rem; animation: fall 1.6s ease-in calc(var(--i) * .12s) infinite; }
    @keyframes fall { to { transform: translateY(130px) rotate(200deg); opacity: 0; } }
  `,
})
export class PartnersEvent {
  private readonly api = inject(Api);
  private readonly tracker = inject(MarketingTracker);
  protected readonly settings = inject(PublicSettingsService);
  readonly lesson = input<string | null>(null);
  readonly full = output<void>();

  protected readonly step = signal<Step>('hook');
  protected readonly right = signal(false);
  protected readonly way = signal<Way>('fund');
  protected readonly doses = signal(0);
  protected readonly sentDoses = signal(0);
  protected readonly bad = signal<Record<string, string>>({});
  protected readonly error = signal('');
  protected readonly busy = signal(false);
  protected readonly chips = [10, 25, 50, 100];
  protected readonly rain = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9];
  protected org = '';
  protected name = '';
  protected phone = '';

  protected n() { return { hook: 1, reveal: 2, how: 3, form: 4, done: 4 }[this.step()]; }
  protected answer(dose: boolean) { this.right.set(dose); this.step.set('reveal'); }
  protected pick(w: Way) { this.way.set(w); this.step.set('form'); }
  protected waText() { return translate('event.waText', { lesson: this.lesson() ?? '' }).replace(/\s+\(\)/, ''); }

  protected async send() {
    const req = translate('partners.required');
    const b: Record<string, string> = {};
    if (!this.org.trim()) b['org'] = req;
    if (!this.name.trim()) b['name'] = req;
    if (!looksLikePhone(this.phone)) b['phone'] = translate('partners.badPhone');
    if (this.way() === 'fund' && !this.doses()) b['doses'] = translate('ev.pickDoses');
    this.bad.set(b);
    if (Object.keys(b).length) return;
    this.busy.set(true);
    this.error.set('');
    try {
      const doses = this.way() === 'fund' ? this.doses() : 0;
      const r = await this.api.pledge({
        organization: this.org.trim(), contactName: this.name.trim(), contactPhone: this.phone.trim(), role: null,
        interest: this.way(), doses, showOnWall: doses > 0, note: this.lesson() ? `زجاجة: ${this.lesson()}` : null,
        source: this.tracker.current(),
      });
      this.tracker.track('request', r.reference);
      this.sentDoses.set(doses);
      this.step.set('done');
    } catch (e) {
      this.error.set(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }
}
