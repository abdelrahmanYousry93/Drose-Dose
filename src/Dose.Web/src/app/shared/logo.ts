import { Component, input } from '@angular/core';

/** Dose wordmark: capsule mark + "Dose" (Latin) + "دروس" (Arabic). */
@Component({
  selector: 'dose-logo',
  template: `
    <span class="logo" [class.logo--lg]="size() === 'lg'" aria-label="Dose دروس">
      <svg class="mark" viewBox="0 0 48 48" aria-hidden="true">
        <g transform="rotate(-35 24 24)">
          <rect x="6" y="15" width="36" height="18" rx="9" fill="#0061FA" />
          <path d="M24 15h9a9 9 0 0 1 0 18h-9z" fill="#FFC30A" />
        </g>
      </svg>
      <span class="words">
        <span class="latin">D<span class="o">o</span>se</span>
        <span class="arabic">دروس</span>
      </span>
    </span>
  `,
  styles: `
    .logo { display: inline-flex; align-items: center; gap: 8px; direction: ltr; }
    .mark { width: 36px; height: 36px; }
    .words { display: flex; flex-direction: column; line-height: 1; }
    .latin { font: 800 1.45rem var(--dose-font); color: var(--dose-primary); letter-spacing: -0.5px; }
    .latin .o { color: var(--dose-accent); }
    .arabic { font: 700 0.85rem var(--dose-font); color: var(--dose-navy); align-self: flex-end; }
    .logo--lg .mark { width: 64px; height: 64px; }
    .logo--lg .latin { font-size: 2.6rem; }
    .logo--lg .arabic { font-size: 1.4rem; }
  `,
})
export class Logo {
  readonly size = input<'md' | 'lg'>('md');
}
