import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { NavigationEnd, Router } from '@angular/router';
import { filter } from 'rxjs';
import { environment } from '../../environments/environment';

type Step = 'visit' | 'whatsapp' | 'hold' | 'request' | 'paid';
/** Summit scan-flow steps: counted in our own report only, never sent to ad pixels. */
export type EventStep = 'ev-dose' | 'ev-month' | 'ev-how' | 'ev-fund' | 'ev-students' | 'ev-chat';
const KEY = 'dose.src';
const KEEP_DAYS = 30;

/** Standard ad-platform events for each funnel step (Meta name / TikTok name). */
const PIXEL_EVENTS: Record<Exclude<Step, 'visit'>, [meta: string, tiktok: string]> = {
  whatsapp: ['Contact', 'Contact'],
  request: ['Lead', 'SubmitForm'],
  hold: ['InitiateCheckout', 'InitiateCheckout'],
  paid: ['Purchase', 'CompletePayment'],
};

type Fn = ((...args: unknown[]) => void) & Record<string, unknown>;
declare global { interface Window { fbq?: Fn; _fbq?: Fn; ttq?: Fn & { methods?: string[] }; TiktokAnalyticsObject?: string } }

/**
 * Two things, no personal data in either:
 * 1. Which marketing post a visitor came from (?src=fb-w01-03, kept 30 days on this device) — reported to our own
 *    funnel report with the booking's public reference only.
 * 2. The Meta / TikTok pixels (ids from /api/public-settings): page views and funnel events (contact, lead, booking
 *    started, payment sent with its amount) so the ad platforms can learn who books. No names, phones or references
 *    are sent to them.
 */
@Injectable({ providedIn: 'root' })
export class MarketingTracker {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private meta = false;
  private tiktok = false;

  /** Call once on start-up. */
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

    this.http.get<{ metaPixelId?: string; tikTokPixelId?: string }>(`${environment.apiBaseUrl}/public-settings`).subscribe({
      next: (s) => {
        if (location.pathname.startsWith('/admin') || location.pathname.startsWith('/teacher')) return; // staff pages
        if (s.metaPixelId && /^\d{6,20}$/.test(s.metaPixelId)) this.loadMeta(s.metaPixelId);
        if (s.tikTokPixelId && /^[A-Z0-9]{6,30}$/i.test(s.tikTokPixelId)) this.loadTikTok(s.tikTokPixelId);
        // The first page view is sent on load; later in-app navigations are page views too.
        let initial: string | null = location.pathname + location.search; // already counted by the loaders
        this.router.events.pipe(filter((e) => e instanceof NavigationEnd)).subscribe((e) => {
          const url = (e as NavigationEnd).urlAfterRedirects;
          if (url === initial) { initial = null; return; }
          initial = null;
          if ((e as NavigationEnd).urlAfterRedirects.startsWith('/admin')) return;
          if (this.meta) window.fbq?.('track', 'PageView');
          if (this.tiktok) window.ttq?.['page'] && (window.ttq['page'] as Fn)();
        });
      },
      error: () => { /* pixels are optional */ },
    });
  }

  /** The post this visitor came from, if within 30 days. */
  current(): string | null {
    try {
      const v = JSON.parse(localStorage.getItem(KEY) ?? 'null');
      return v && typeof v.src === 'string' && Date.now() - v.at < KEEP_DAYS * 86_400_000 ? v.src : null;
    } catch { return null; }
  }

  /** A funnel step. `value` is the amount in EGP for payments. */
  track(step: Step, reference?: string, value?: number) {
    const src = this.current();
    if (src && step !== 'paid') this.send(src, step, reference);
    if (step === 'visit') return;
    const [metaEvent, tiktokEvent] = PIXEL_EVENTS[step];
    const params = value != null ? { value, currency: 'EGP' } : {};
    try {
      if (this.meta) window.fbq?.('track', metaEvent, params);
      if (this.tiktok) (window.ttq?.['track'] as Fn | undefined)?.(tiktokEvent, params);
    } catch { /* never break the page */ }
  }

  /** A step in the summit scan flow (only when the visitor came from an event card). */
  trackEvent(step: EventStep) {
    const src = this.current();
    if (src) this.send(src, step);
  }

  private send(source: string, step: Step | EventStep, reference?: string) {
    this.http.post('/api/track', { source, step, reference: reference ?? null }).subscribe({ error: () => { /* best effort */ } });
  }

  /** Meta's standard loader, written without an inline script (the CSP allows only same-origin + connect.facebook.net). */
  private loadMeta(id: string) {
    if (!window.fbq) {
      const q: unknown[] = [];
      const fbq = function (...args: unknown[]) {
        const f = fbq as unknown as Fn;
        if (typeof f['callMethod'] === 'function') (f['callMethod'] as (...a: unknown[]) => void)(...args);
        else q.push(args);
      } as unknown as Fn;
      Object.assign(fbq, { push: fbq, loaded: true, version: '2.0', queue: q });
      window.fbq = window._fbq = fbq;
      const s = document.createElement('script');
      s.async = true;
      s.src = 'https://connect.facebook.net/en_US/fbevents.js';
      document.head.appendChild(s);
    }
    window.fbq('init', id);
    window.fbq('track', 'PageView');
    this.meta = true;
  }

  /** TikTok's official base code, as functions instead of an inline script (the CSP allows analytics.tiktok.com). */
  private loadTikTok(id: string) {
    const w = window as unknown as Record<string, any>; // eslint-disable-line @typescript-eslint/no-explicit-any
    w['TiktokAnalyticsObject'] = 'ttq';
    const ttq = (w['ttq'] = w['ttq'] || []);
    ttq.methods = ['page', 'track', 'identify', 'instances', 'debug', 'on', 'off', 'once', 'ready', 'alias', 'group',
      'enableCookie', 'disableCookie', 'holdConsent', 'revokeConsent', 'grantConsent'];
    ttq.setAndDefer = (t: any, e: string) => { t[e] = (...args: unknown[]) => { t.push([e, ...args]); }; }; // eslint-disable-line @typescript-eslint/no-explicit-any
    for (const m of ttq.methods) ttq.setAndDefer(ttq, m);
    ttq.instance = (t: string) => { const e = ttq._i[t] || []; for (const m of ttq.methods) ttq.setAndDefer(e, m); return e; };
    ttq.load = (e: string, n?: Record<string, unknown>) => {
      const r = 'https://analytics.tiktok.com/i18n/pixel/events.js';
      ttq._i = ttq._i || {}; ttq._i[e] = []; ttq._i[e]._u = r;
      ttq._t = ttq._t || {}; ttq._t[e] = +new Date();
      ttq._o = ttq._o || {}; ttq._o[e] = n || {};
      const s = document.createElement('script');
      s.type = 'text/javascript'; s.async = true; s.src = `${r}?sdkid=${encodeURIComponent(e)}&lib=ttq`;
      document.head.appendChild(s);
    };
    ttq.load(id);
    ttq.page();
    this.tiktok = true;
  }
}
