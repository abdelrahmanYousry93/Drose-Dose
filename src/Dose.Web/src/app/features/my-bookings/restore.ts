import { Component, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Api } from '../../core/api';
import { myBookings, myRequests } from '../../core/format';
import { TranslatePipe } from '../../core/i18n';
import { PublicSettingsService } from '../../core/public-settings.service';
import { translate } from '../../core/i18n';

/**
 * /me/<token> — the private "my bookings" link the team sends back on WhatsApp. Saves every booking link and request
 * of that number on this device, so they show up in "My bookings" here from now on.
 */
@Component({
  selector: 'dose-restore',
  imports: [RouterLink, TranslatePipe],
  template: `
    <section class="page">
      <div class="dose-card box">
        @if (state() === 'loading') {
          <p class="muted">{{ 'restore.loading' | t }}</p>
        } @else if (state() === 'done') {
          <span class="ok">✓</span>
          <h1>{{ 'restore.doneTitle' | t }}</h1>
          <p>{{ 'restore.doneBody' | t: { b: counts().b, r: counts().r } }}</p>
          <a class="dose-btn dose-btn--primary" routerLink="/my-bookings">{{ 'restore.open' | t }}</a>
        } @else {
          <h1>{{ 'restore.badTitle' | t }}</h1>
          <p class="muted">{{ 'restore.badBody' | t }}</p>
          @if (settings.whatsAppLink(askText); as link) {
            <a class="dose-btn dose-btn--whatsapp" [href]="link" target="_blank" rel="noopener">{{ 'my.askLink' | t }}</a>
          }
        }
      </div>
    </section>
  `,
  styles: `
    .page { max-width: 560px; margin: 0 auto; padding: 32px 16px; }
    .box { padding: 28px 20px; display: grid; gap: 12px; justify-items: center; text-align: center; }
    h1 { margin: 0; font-size: 1.3rem; color: var(--dose-navy); }
    p { margin: 0; }
    .ok { width: 56px; height: 56px; border-radius: 50%; background: var(--dose-success-50); color: var(--dose-success); display: grid; place-items: center; font-size: 1.7rem; font-weight: 800; }
  `,
})
export class Restore {
  private readonly api = inject(Api);
  protected readonly settings = inject(PublicSettingsService);
  protected readonly askText = translate('my.askLinkMsg');
  protected readonly state = signal<'loading' | 'done' | 'bad'>('loading');
  protected readonly counts = signal({ b: 0, r: 0 });

  constructor() {
    const token = inject(ActivatedRoute).snapshot.paramMap.get('token') ?? '';
    // Keep the private token out of the address bar (and of screenshots / shared screens) once it is read.
    try { history.replaceState(history.state, '', '/me'); } catch { /* ignore */ }
    this.api.restore(token).then((r) => {
      for (const b of [...r.bookings].reverse()) myBookings.add(b.reference, b.token);
      for (const q of [...r.requests].reverse()) myRequests.add({ reference: q.reference, subject: q.subject, grade: q.grade, sentAtUtc: q.sentAtUtc });
      this.counts.set({ b: r.bookings.length, r: r.requests.length });
      this.state.set('done');
    }).catch(() => this.state.set('bad'));
  }
}
