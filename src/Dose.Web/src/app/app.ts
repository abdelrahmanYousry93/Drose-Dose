import { Component, inject } from '@angular/core';
import { RouterLink, RouterOutlet } from '@angular/router';
import { PublicSettingsService } from './core/public-settings.service';
import { Logo } from './shared/logo';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, Logo],
  templateUrl: './app.html',
  styles: `
    :host { display: flex; flex-direction: column; min-height: 100dvh; }
    .bar {
      position: sticky; top: 0; z-index: 10; background: rgb(255 255 255 / 0.92);
      backdrop-filter: blur(8px); border-bottom: 1px solid var(--dose-border);
    }
    .bar__inner, main, .foot__inner { width: 100%; max-width: 960px; margin: 0 auto; padding: 0 16px; }
    .bar__inner { display: flex; align-items: center; justify-content: space-between; min-height: 64px; }
    .home-link { text-decoration: none; }
    nav { display: none; gap: 18px; }
    nav a { text-decoration: none; color: var(--dose-text); font-weight: 600; }
    main { flex: 1; }
    .foot { border-top: 1px solid var(--dose-border); background: var(--dose-surface); }
    .foot__inner { display: flex; flex-wrap: wrap; gap: 12px; align-items: center; justify-content: space-between; padding-block: 16px; }
    .foot p { margin: 0; color: var(--dose-text-muted); font-size: 0.9rem; }
    @media (min-width: 760px) { nav { display: flex; } }
  `,
})
export class App {
  protected readonly publicSettings = inject(PublicSettingsService);
}
