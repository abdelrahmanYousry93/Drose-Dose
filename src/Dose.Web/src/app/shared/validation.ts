/** Loose client-side phone check (the server normalizes and validates Egyptian / international numbers). */
export function looksLikePhone(value: string | null | undefined): boolean {
  const digits = (value ?? '').replace(/[\s\-()]/g, '');
  return /^\+?\d{10,15}$/.test(digits);
}
