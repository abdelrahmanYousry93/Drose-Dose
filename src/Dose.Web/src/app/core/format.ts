import { ApiError } from './api';

/** Business time zone for display. Timestamps from the API are UTC. */
export const BUSINESS_TZ = 'Africa/Cairo';

const dateFmt = new Intl.DateTimeFormat('ar-EG', { weekday: 'long', day: 'numeric', month: 'long', timeZone: BUSINESS_TZ });
const timeFmt = new Intl.DateTimeFormat('ar-EG', { hour: 'numeric', minute: '2-digit', timeZone: BUSINESS_TZ });
const moneyFmt = new Intl.NumberFormat('ar-EG', { maximumFractionDigits: 2 });

export const fmtDate = (utc: string | null | undefined) => (utc ? dateFmt.format(new Date(utc)) : '');
export const fmtTime = (utc: string | null | undefined) => (utc ? timeFmt.format(new Date(utc)) : '');
export const fmtDateTime = (utc: string | null | undefined) => (utc ? `${fmtDate(utc)} — ${fmtTime(utc)}` : '');
export const fmtMoney = (amount: number, currency = 'EGP') =>
  `${moneyFmt.format(amount)} ${currency === 'EGP' ? 'ج.م' : currency}`;

export function fmtDuration(minutes: number): string {
  if (minutes === 60) return 'ساعة';
  if (minutes === 90) return 'ساعة ونص';
  if (minutes === 120) return 'ساعتين';
  if (minutes < 60) return `${minutes} دقيقة`;
  const h = Math.floor(minutes / 60), m = minutes % 60;
  return m ? `${h} ساعة و${m} دقيقة` : `${h} ساعات`;
}

const messages: Record<string, string> = {
  'booking.session_full': 'للأسف الأماكن خلصت في الجلسة دي.',
  'booking.session_closed': 'الحجز في الجلسة دي اتقفل.',
  'booking.session_not_found': 'الجلسة دي مش موجودة.',
  'booking.duplicate': 'الرقم ده عنده حجز فعلاً في نفس الجلسة.',
  'booking.not_found': 'الحجز مش موجود. اتأكد إنك فاتح اللينك الصح.',
  'booking.expired': 'مدة حجز المقعد خلصت. ابدأ الحجز من جديد.',
  'booking.invalid_state': 'حالة الحجز اتغيرت. حدّث الصفحة.',
  'booking.guardian_required': 'اسم ورقم واتساب ولي الأمر مطلوبين للمرحلة دي.',
  'booking.terms_required': 'لازم توافق على الشروط علشان تكمّل.',
  'booking.price_changed': 'السعر اتغيّر. راجع السعر الجديد ووافق تاني.',
  'booking.invalid_phone': 'رقم الواتساب مش صحيح. اكتبه بالشكل ده: 01xxxxxxxxx',
  'booking.invalid_name': 'اكتب اسم الطالب (حرفين على الأقل).',
  'booking.invalid_file': 'ارفع صورة بصيغة JPG أو PNG أو WebP وحجمها مناسب.',
  'booking.join_not_available': 'رابط الجلسة لسه مش متاح.',
  'payment.reason_required': 'اكتب سبب الرفض للطالب.',
  'http.401': 'لازم تسجّل دخول.',
  'http.403': 'مش مسموح لك بالصفحة دي.',
  'http.429': 'محاولات كتير. استنى دقيقة وجرّب تاني.',
  'http.0': 'مفيش اتصال بالسيرفر. اتأكد من الإنترنت.',
};

export function errorMessage(e: unknown): string {
  if (e instanceof ApiError) return messages[e.code] ?? e.message ?? 'حصل خطأ. جرّب تاني.';
  return 'حصل خطأ. جرّب تاني.';
}

/** Remembers booking links on this device only (per-viewer convenience; the link itself is the key). */
export const myBookings = {
  add(reference: string, token: string) {
    try {
      const list = myBookings.list().filter((b) => b.token !== token);
      list.unshift({ reference, token });
      localStorage.setItem('dose.bookings', JSON.stringify(list.slice(0, 10)));
    } catch { /* storage unavailable */ }
  },
  list(): { reference: string; token: string }[] {
    try { return JSON.parse(localStorage.getItem('dose.bookings') ?? '[]'); } catch { return []; }
  },
};
