import { ApiError } from './api';
import { LANG, translate } from './i18n';

/** Business time zone for display. Timestamps from the API are UTC. */
export const BUSINESS_TZ = 'Africa/Cairo';
// Latin digits in both languages, matching phone numbers and booking references.
const LOCALE = LANG === 'en' ? 'en-GB' : 'ar-EG-u-nu-latn';

const dateFmt = new Intl.DateTimeFormat(LOCALE, { weekday: 'long', day: 'numeric', month: 'long', timeZone: BUSINESS_TZ });
const timeFmt = new Intl.DateTimeFormat(LOCALE, { hour: 'numeric', minute: '2-digit', hour12: true, timeZone: BUSINESS_TZ });
const moneyFmt = new Intl.NumberFormat(LOCALE, { maximumFractionDigits: 2 });
const numFmt = new Intl.NumberFormat(LOCALE);

export const fmtDate = (utc: string | null | undefined) => (utc ? dateFmt.format(new Date(utc)) : '');
export const fmtTime = (utc: string | null | undefined) => (utc ? timeFmt.format(new Date(utc)) : '');
export const fmtDateTime = (utc: string | null | undefined) => (utc ? `${fmtDate(utc)} — ${fmtTime(utc)}` : '');
export const fmtNumber = (n: number) => numFmt.format(n);
export const fmtMoney = (amount: number, currency = 'EGP') =>
  `${moneyFmt.format(amount)} ${currency === 'EGP' ? translate('common.egp') : currency}`;

export function fmtDuration(minutes: number): string {
  if (minutes === 60) return translate('duration.hour');
  if (minutes === 90) return translate('duration.hourHalf');
  if (minutes === 120) return translate('duration.twoHours');
  if (minutes < 60) return translate('duration.minutes', { n: minutes });
  const h = Math.floor(minutes / 60), m = minutes % 60;
  return m ? translate('duration.hoursMinutes', { h, m }) : translate('duration.hours', { h });
}

/**
 * The API returns a stable `code` plus a `title` already written in the request's language (Accept-Language).
 * We show the server's text; only transport-level failures (offline, 401, 429…) are worded here.
 */
export function errorMessage(e: unknown): string {
  if (e instanceof ApiError) {
    if (e.code.startsWith('http.')) {
      const key = `errors.${e.code.replace('.', '_')}`;
      const local = translate(key);
      if (local !== key) return local;
    }
    return e.message || translate('errors.generic');
  }
  return translate('errors.generic');
}

/** Remembers booking links on this device only (per-viewer convenience; the link itself is the key). */
export const myBookings = {
  add(reference: string, token: string) {
    try {
      const list = myBookings.list().filter((b) => b.token !== token);
      list.unshift({ reference, token });
      localStorage.setItem('dose.bookings', JSON.stringify(list.slice(0, 30)));
    } catch { /* storage unavailable */ }
  },
  remove(token: string) {
    try { localStorage.setItem('dose.bookings', JSON.stringify(myBookings.list().filter((b) => b.token !== token))); } catch { /* storage unavailable */ }
  },
  list(): { reference: string; token: string }[] {
    try {
      const v = JSON.parse(localStorage.getItem('dose.bookings') ?? '[]');
      return Array.isArray(v) ? v.filter((b) => typeof b?.token === 'string' && typeof b?.reference === 'string') : [];
    } catch { return []; }
  },
};

/** Session requests sent from this device (no private link exists for a request; the team answers on WhatsApp). */
export interface SentRequest { reference: string; subject: string; grade: string; sentAtUtc: string }
export const myRequests = {
  add(r: SentRequest) {
    try {
      const list = myRequests.list().filter((x) => x.reference !== r.reference);
      list.unshift(r);
      localStorage.setItem('dose.requests', JSON.stringify(list.slice(0, 30)));
    } catch { /* storage unavailable */ }
  },
  remove(reference: string) {
    try { localStorage.setItem('dose.requests', JSON.stringify(myRequests.list().filter((x) => x.reference !== reference))); } catch { /* storage unavailable */ }
  },
  list(): SentRequest[] {
    try {
      const v = JSON.parse(localStorage.getItem('dose.requests') ?? '[]');
      return Array.isArray(v) ? v.filter((x) => typeof x?.reference === 'string') : [];
    } catch { return []; }
  },
};
