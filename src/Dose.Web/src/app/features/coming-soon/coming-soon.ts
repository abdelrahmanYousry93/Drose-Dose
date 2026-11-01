import { Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslatePipe, translate } from '../../core/i18n';

/** Placeholder for screens scheduled in later phases. Title comes from route data. */
@Component({
  selector: 'dose-coming-soon',
  imports: [RouterLink, TranslatePipe],
  template: `
    <section class="dose-card box">
      <h1>{{ heading() }}</h1>
      <p>{{ 'comingSoon.body' | t }}</p>
      <a class="dose-btn dose-btn--ghost" routerLink="/">{{ 'common.backHome' | t }}</a>
    </section>
  `,
  styles: `
    .box { margin: 32px 0; padding: 28px 20px; text-align: center; display: grid; gap: 12px; justify-items: center; }
    h1 { margin: 0; font-size: 1.4rem; color: var(--dose-navy); }
    p { margin: 0; color: var(--dose-text-muted); }
  `,
})
export class ComingSoon {
  readonly heading = input(translate('comingSoon.title'));
}
