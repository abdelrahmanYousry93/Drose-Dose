import { Component, input } from '@angular/core';

/**
 * TEMPORARY Dose logo (until the final brand logo is delivered):
 * a "dose" capsule wearing a graduation cap + bilingual wordmark.
 * Standalone files: public/brand/dose-logo.svg and public/brand/dose-mark.svg.
 */
@Component({
  selector: 'dose-logo',
  template: `
    <span class="logo" [class.logo--lg]="size() === 'lg'" aria-label="Dose دروس">
      <svg class="mark" viewBox="0 0 64 64" aria-hidden="true">
        <g transform="rotate(-38 32 38)">
          <rect x="10" y="28" width="44" height="20" rx="10" fill="#0061FA" />
          <path d="M32 28h12a10 10 0 0 1 0 20H32z" fill="#FFC30A" />
          <rect x="15" y="32" width="10" height="4" rx="2" fill="#fff" opacity=".55" />
        </g>
        <path d="M14 15 33 7l19 8-19 8z" fill="#0B1F6B" />
        <path d="M22 18.5v6c0 2.5 5 4.5 11 4.5s11-2 11-4.5v-6l-11 4.6z" fill="#0B1F6B" />
        <path d="M50 16v8.5" stroke="#0B1F6B" stroke-width="2" stroke-linecap="round" />
        <circle cx="50" cy="26" r="2.2" fill="#FFC30A" />
      </svg>
      <span class="words">
        <span class="latin">D<span class="o">o</span>se</span>
        <span class="arabic">دروس</span>
      </span>
    </span>
  `,
  styles: `
    .logo { display: inline-flex; align-items: center; gap: 6px; direction: ltr; }
    .mark { width: 40px; height: 40px; flex: none; }
    .words { display: inline-flex; align-items: baseline; gap: 6px; line-height: 1; }
    .latin { font: 800 1.5rem var(--dose-font); color: var(--dose-primary); letter-spacing: -0.5px; }
    .latin .o { color: var(--dose-accent); }
    .arabic { font: 800 1.1rem var(--dose-font); color: var(--dose-navy); }
    .logo--lg .mark { width: 72px; height: 72px; }
    .logo--lg .latin { font-size: 2.6rem; }
    .logo--lg .arabic { font-size: 1.8rem; }
  `,
})
export class Logo {
  readonly size = input<'md' | 'lg'>('md');
}
