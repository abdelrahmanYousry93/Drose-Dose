import { HttpInterceptorFn } from '@angular/common/http';
import { Injectable, Pipe, PipeTransform, inject } from '@angular/core';
import { Title } from '@angular/platform-browser';
import { RouterStateSnapshot, TitleStrategy } from '@angular/router';
import { ar } from '../i18n/ar';
import { en } from '../i18n/en';

export type Lang = 'ar' | 'en';
const STORAGE_KEY = 'dose.lang';

function detect(): Lang {
  try {
    const saved = localStorage.getItem(STORAGE_KEY);
    if (saved === 'ar' || saved === 'en') return saved;
  } catch { /* storage unavailable */ }
  return typeof navigator !== 'undefined' && navigator.language?.toLowerCase().startsWith('en') ? 'en' : 'ar';
}

/**
 * The UI language for this page load. Switching language stores the choice and reloads, so every screen and every
 * API response (Accept-Language) is consistent without each component having to re-fetch.
 */
export const LANG: Lang = detect();
export const IS_RTL = LANG === 'ar';

const dictionaries: Record<Lang, unknown> = { ar, en };

function lookup(dict: unknown, key: string): string | undefined {
  let node: unknown = dict;
  for (const part of key.split('.')) {
    if (node && typeof node === 'object' && part in (node as Record<string, unknown>)) node = (node as Record<string, unknown>)[part];
    else return undefined;
  }
  return typeof node === 'string' ? node : undefined;
}

/** Translates a dotted key, filling {placeholders}. Falls back to Arabic, then to the key itself. */
export function translate(key: string, params?: Record<string, string | number | null | undefined>): string {
  return translateIn(LANG, key, params);
}

/** Same as translate, in a specific language — e.g. a message written in the student's language, not the admin's. */
export function translateIn(lang: Lang, key: string, params?: Record<string, string | number | null | undefined>): string {
  let text = lookup(dictionaries[lang], key) ?? lookup(ar, key) ?? key;
  if (params) for (const [k, v] of Object.entries(params)) text = text.replaceAll(`{${k}}`, String(v ?? ''));
  return text;
}

/** Applies lang/dir to <html> before the app renders. */
export function applyDocumentLanguage() {
  document.documentElement.lang = LANG;
  document.documentElement.dir = IS_RTL ? 'rtl' : 'ltr';
}

@Injectable({ providedIn: 'root' })
export class I18n {
  readonly lang = LANG;
  readonly rtl = IS_RTL;
  readonly t = translate;

  setLang(lang: Lang) {
    if (lang === LANG) return;
    try { localStorage.setItem(STORAGE_KEY, lang); } catch { /* storage unavailable */ }
    location.reload();
  }
}

@Pipe({ name: 't' })
export class TranslatePipe implements PipeTransform {
  transform(key: string, params?: Record<string, string | number | null | undefined>): string {
    return translate(key, params);
  }
}

/** Sends the UI language so the API returns localized names and messages. */
export const languageInterceptor: HttpInterceptorFn = (req, next) =>
  next(req.clone({ setHeaders: { 'Accept-Language': LANG } }));

/** Route titles are translation keys. */
@Injectable({ providedIn: 'root' })
export class TranslatedTitleStrategy extends TitleStrategy {
  private readonly title = inject(Title);
  override updateTitle(state: RouterStateSnapshot) {
    const key = this.buildTitle(state);
    this.title.setTitle(key ? `${translate(key)} — Dose` : translate('brand.pageTitle'));
  }
}
