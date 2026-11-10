import { Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { NavigationEnd, Router } from '@angular/router';
import { SwUpdate } from '@angular/service-worker';
import { filter } from 'rxjs';
import { TranslatePipe } from '../core/i18n';

/** The browser's install event (Chrome/Android); not in the TS DOM typings. */
interface InstallPromptEvent extends Event {
  prompt(): Promise<void>;
  userChoice: Promise<{ outcome: 'accepted' | 'dismissed' }>;
}

const DISMISS_KEY = 'dose.installDismissedAt';
const DISMISS_DAYS = 30;

function recentlyDismissed(): boolean {
  try {
    const at = Number(localStorage.getItem(DISMISS_KEY));
    return !!at && Date.now() - at < DISMISS_DAYS * 86_400_000;
  } catch { return false; }
}

function isStandalone(): boolean {
  return window.matchMedia?.('(display-mode: standalone)').matches || (navigator as { standalone?: boolean }).standalone === true;
}

/** Facebook / Instagram / Messenger / TikTok in-app browsers: they cannot install or add to the home screen. */
function isInAppBrowser(): boolean {
  return /FBAN|FBAV|FB_IAB|Instagram|Messenger|musical_ly|BytedanceWebview|TikTok/i.test(navigator.userAgent);
}

function isIos(): boolean {
  return /iphone|ipad|ipod/i.test(navigator.userAgent) || (navigator.platform === 'MacIntel' && navigator.maxTouchPoints > 1);
}

/**
 * Which hint to show when the browser has no install button of its own (iPhone never has one):
 * Safari → the Share-menu steps; other iPhone browsers → "open in Safari"; Facebook/Instagram → "open in browser".
 */
function manualHintKey(): string | null {
  if (isInAppBrowser()) return 'pwa.inAppHint';
  if (!isIos()) return null;
  return /crios|fxios|edgios|opios/i.test(navigator.userAgent) ? 'pwa.iosHintOther' : 'pwa.iosHint';
}

/**
 * PWA helpers shown under the header:
 * - offline notice (the app keeps showing the last data it saved),
 * - "new version" prompt when the service worker has downloaded an update,
 * - "add Dose to your home screen" (Android install prompt, or the Share-menu hint on iPhone). Students only —
 *   not on admin/teacher pages — and it stays hidden for 30 days after "Not now".
 */
@Component({
  selector: 'dose-pwa-banners',
  imports: [TranslatePipe],
  template: `
    @if (!online()) {
      <div class="strip strip--offline" role="status">
        <span aria-hidden="true">📡</span><span>{{ 'pwa.offline' | t }}</span>
      </div>
    }
    @if (updateReady()) {
      <div class="strip strip--update" role="status">
        <span>{{ 'pwa.updateReady' | t }}</span>
        <button type="button" class="dose-btn dose-btn--primary btn-sm" (click)="reload()">{{ 'pwa.updateNow' | t }}</button>
      </div>
    }
    @if (showInstall()) {
      <aside class="install" [attr.aria-label]="'pwa.installTitle' | t">
        <img src="icons/icon-192.png" alt="" width="40" height="40" />
        <div class="install__text">
          <strong>{{ 'pwa.installTitle' | t }}</strong>
          <span class="muted small">{{ (iosHint() ?? 'pwa.installBody') | t }}</span>
        </div>
        <div class="install__actions">
          @if (!iosHint()) {
            <button type="button" class="dose-btn dose-btn--primary btn-sm" (click)="install()">{{ 'pwa.install' | t }}</button>
          }
          <button type="button" class="dose-btn dose-btn--ghost btn-sm" (click)="dismiss()">{{ 'pwa.notNow' | t }}</button>
        </div>
      </aside>
    }
    @if (manualSteps()) {
      <aside class="install install--manual" role="status">
        <div class="install__text">
          <strong>{{ 'pwa.manualTitle' | t }}</strong>
          <span class="small">{{ 'pwa.manualSteps' | t }}</span>
        </div>
        <div class="install__actions">
          <button type="button" class="dose-btn dose-btn--ghost btn-sm" (click)="manualSteps.set(false)">{{ 'pwa.ok' | t }}</button>
        </div>
      </aside>
    }
  `,
  styles: `
    :host { display: block; width: 100%; max-width: 960px; margin: 0 auto; padding: 0 16px; }
    .strip { display: flex; flex-wrap: wrap; gap: 10px; align-items: center; justify-content: space-between; margin-top: 10px;
      padding: 10px 14px; border-radius: var(--dose-radius-sm); font-weight: 600; font-size: 0.92rem; }
    .strip--offline { background: #fff4d6; color: #6b4e00; justify-content: flex-start; flex-wrap: nowrap; align-items: flex-start; }
    .strip--update { background: var(--dose-primary-50); color: var(--dose-navy); }
    .install { display: flex; flex-wrap: wrap; gap: 12px; align-items: center; margin-top: 10px; padding: 12px 14px;
      border: 1px solid var(--dose-border); border-radius: var(--dose-radius-sm); background: var(--dose-surface); }
    .install img { border-radius: 10px; flex: none; }
    .install--manual { background: var(--dose-primary-50); border-color: #cfe0ff; }
    .install__text { display: grid; gap: 2px; flex: 1 1 200px; }
    .install__actions { display: flex; gap: 8px; }
    .btn-sm { min-height: 36px; padding: 0 14px; font-size: 0.88rem; }
  `,
})
export class PwaBanners {
  private readonly router = inject(Router);
  private readonly sw = inject(SwUpdate);

  protected readonly online = signal(navigator.onLine);
  protected readonly updateReady = signal(false);
  private readonly deferred = signal<InstallPromptEvent | null>(null);
  private readonly dismissed = signal(recentlyDismissed());
  private readonly staffPage = signal(false);
  /** Translation key of the manual "add to home screen" hint, when the browser has no install prompt (iPhone, in-app). */
  protected readonly iosHint = signal<string | null>(null);
  /** Shown when Android accepted the install but it never completed (e.g. "Couldn't install" on some phones). */
  protected readonly manualSteps = signal(false);
  private installTimer: ReturnType<typeof setTimeout> | undefined;

  protected readonly showInstall = computed(() =>
    !this.dismissed() && !this.staffPage() && !this.updateReady() && (this.deferred() !== null || !!this.iosHint()));

  constructor() {
    const destroy = inject(DestroyRef);
    const on = () => this.online.set(true);
    const off = () => this.online.set(false);
    const beforeInstall = (e: Event) => { e.preventDefault(); this.deferred.set(e as InstallPromptEvent); };
    const installed = () => {
      this.deferred.set(null); this.iosHint.set(null); this.manualSteps.set(false); clearTimeout(this.installTimer);
    };
    window.addEventListener('online', on);
    window.addEventListener('offline', off);
    window.addEventListener('beforeinstallprompt', beforeInstall);
    window.addEventListener('appinstalled', installed);
    destroy.onDestroy(() => {
      window.removeEventListener('online', on);
      window.removeEventListener('offline', off);
      window.removeEventListener('beforeinstallprompt', beforeInstall);
      window.removeEventListener('appinstalled', installed);
    });

    this.iosHint.set(isStandalone() ? null : manualHintKey());
    destroy.onDestroy(() => clearTimeout(this.installTimer));

    const sub = this.router.events.pipe(filter((e) => e instanceof NavigationEnd)).subscribe((e) =>
      this.staffPage.set(/^\/(admin|teacher)(\/|$|\?)/.test((e as NavigationEnd).urlAfterRedirects)));
    destroy.onDestroy(() => sub.unsubscribe());

    if (this.sw.isEnabled) {
      const up = this.sw.versionUpdates.subscribe((evt) => { if (evt.type === 'VERSION_READY') this.updateReady.set(true); });
      // A broken cached version cannot recover by itself: load the fresh one from the network.
      const broken = this.sw.unrecoverable.subscribe(() => location.reload());
      destroy.onDestroy(() => { up.unsubscribe(); broken.unsubscribe(); });
      // Long-lived installed apps check for a new version every 6 hours.
      const timer = setInterval(() => this.sw.checkForUpdate().catch(() => undefined), 6 * 3_600_000);
      destroy.onDestroy(() => clearInterval(timer));
    }
  }

  protected reload() { location.reload(); }

  protected async install() {
    const e = this.deferred();
    if (!e) return;
    this.deferred.set(null);
    await e.prompt();
    const { outcome } = await e.userChoice;
    if (outcome === 'dismissed') { this.dismiss(); return; }
    // Accepted: Chrome normally fires "appinstalled" within seconds. Some phones fail silently or say
    // "Couldn't install" (missing home-screen-shortcut permission, old Play services): then explain the manual way.
    this.installTimer = setTimeout(() => this.manualSteps.set(true), 20_000);
  }

  protected dismiss() {
    this.dismissed.set(true);
    try { localStorage.setItem(DISMISS_KEY, String(Date.now())); } catch { /* storage unavailable */ }
  }
}
