import { Component, inject } from '@angular/core';
import { I18n, TranslatePipe } from '../core/i18n';

/** Arabic ⇄ English. The label is written in the language you switch to, so it is readable either way. */
@Component({
  selector: 'dose-lang-switch',
  imports: [TranslatePipe],
  template: `
    <button type="button" class="switch" (click)="toggle()" [attr.aria-label]="'nav.language' | t"
            [attr.lang]="i18n.lang === 'ar' ? 'en' : 'ar'">
      <span aria-hidden="true">🌐</span> {{ 'nav.switchTo' | t }}
    </button>
  `,
  styles: `
    .switch {
      display: inline-flex; align-items: center; gap: 6px; min-height: 36px; padding: 0 12px;
      border: 1px solid var(--dose-border); border-radius: 999px; background: var(--dose-surface);
      font: 700 0.9rem var(--dose-font); color: var(--dose-navy); cursor: pointer;
    }
    .switch:hover { border-color: var(--dose-primary); color: var(--dose-primary); }
    .switch:focus-visible { outline: 3px solid var(--dose-accent); outline-offset: 2px; }
  `,
})
export class LangSwitch {
  protected readonly i18n = inject(I18n);
  protected toggle() { this.i18n.setLang(this.i18n.lang === 'ar' ? 'en' : 'ar'); }
}
