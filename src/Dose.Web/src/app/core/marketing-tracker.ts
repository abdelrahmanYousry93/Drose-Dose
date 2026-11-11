import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';

type Step = 'visit' | 'whatsapp' | 'hold' | 'request';
const KEY = 'dose.src';
const KEEP_DAYS = 30;

/**
 * Remembers which marketing post a visitor came from (?src=fb-w01-03 on the link) for 30 days on this device and
 * reports the funnel steps — visit, WhatsApp click, booking started, request sent — with the booking's public
 * reference only. No personal data leaves the device through this.
 */
@Injectable({ providedIn: 'root' })
export class MarketingTracker {
  private readonly http = inject(HttpClient);

  /** Call once on start-up: picks up ?src= from the address and reports the visit (once per tab session). */
  start() {
    let src: string | null = null;
    try { src = new URLSearchParams(location.search).get('src'); } catch { /* no window */ }
    if (src && /^[a-z0-9][a-z0-9-]{0,59}$/i.test(src)) {
      src = src.toLowerCase();
      try { localStorage.setItem(KEY, JSON.stringify({ src, at: Date.now() })); } catch { /* storage unavailable */ }
      let seen = false;
      try { seen = sessionStorage.getItem(`${KEY}.visit.${src}`) === '1'; sessionStorage.setItem(`${KEY}.visit.${src}`, '1'); } catch { /* ignore */ }
      if (!seen) this.send(src, 'visit');
    }
    // WhatsApp clicks anywhere on the site.
    document.addEventListener('click', (e) => {
      const a = (e.target as HTMLElement | null)?.closest?.('a[href]') as HTMLAnchorElement | null;
      if (a && /wa\.me|whatsapp\.com/i.test(a.href)) this.track('whatsapp');
    }, { capture: true });
  }

  /** The post this visitor came from, if within 30 days. */
  current(): string | null {
    try {
      const v = JSON.parse(localStorage.getItem(KEY) ?? 'null');
      return v && typeof v.src === 'string' && Date.now() - v.at < KEEP_DAYS * 86_400_000 ? v.src : null;
    } catch { return null; }
  }

  track(step: Step, reference?: string) {
    const src = this.current();
    if (src) this.send(src, step, reference);
  }

  private send(source: string, step: Step, reference?: string) {
    this.http.post('/api/track', { source, step, reference: reference ?? null }).subscribe({ error: () => { /* best effort */ } });
  }
}
