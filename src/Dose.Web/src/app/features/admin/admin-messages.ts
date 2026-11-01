import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AdminApi, ComposedMessage, MessageA, MessageStatus, TemplateA } from '../../core/admin-api';
import { errorMessage, fmtDateTime } from '../../core/format';
import { LANG, TranslatePipe, translate as tr } from '../../core/i18n';

/**
 * WhatsApp messages (spec 10). Events (payment approved/rejected, top-up needed, reminders, cancellations…) queue a
 * message from a template in the student's language. In the MVP a team member opens it, sends it from the business
 * WhatsApp and marks it sent — so this page is both the to-do list and the log. Templates are edited at the bottom.
 * The secret booking link is filled in only when a message is opened ("Prepare"), never stored in the log.
 */
@Component({
  selector: 'dose-admin-messages',
  imports: [FormsModule, TranslatePipe],
  template: `
    <section class="page">
      <h1 class="page-title">{{ 'titles.adminMessages' | t }}</h1>
      <p class="muted small">{{ 'messages.lead' | t }}</p>

      <div class="tabs" role="tablist">
        @for (t of tabs; track t.key) {
          <button role="tab" type="button" [attr.aria-selected]="tab() === t.key" (click)="setTab(t.key)">{{ t.label }}</button>
        }
      </div>
      @if (error()) { <p class="alert alert--error" role="alert">{{ error() }}</p> }

      @if (tab() === 'templates') {
        <p class="muted small">{{ 'messages.templatesLead' | t }}</p>
        @for (tp of templates(); track tp.code) {
          <details class="dose-card item tpl" [class.off]="!tp.isActive">
            <summary>
              <strong>{{ lang === 'en' ? tp.titleEn : tp.titleAr }}</strong>
              <code dir="ltr">{{ tp.code }}</code>
              @if (!tp.isActive) { <span class="badge badge--gray">{{ 'messages.off' | t }}</span> }
            </summary>
            <div class="tpl-body">
              <div class="chips">
                <span class="muted small">{{ 'messages.placeholders' | t }}</span>
                @for (p of tp.placeholders; track p) { <code dir="ltr" class="chip">{{ '{' + p + '}' }}</code> }
              </div>
              <div class="two">
                <label class="field"><span>{{ 'common.arabic' | t }}</span>
                  <textarea dir="rtl" lang="ar" [(ngModel)]="tp.bodyAr" [name]="tp.code + '-ar'"></textarea></label>
                <label class="field"><span>{{ 'common.english' | t }}</span>
                  <textarea dir="ltr" lang="en" [(ngModel)]="tp.bodyEn" [name]="tp.code + '-en'"></textarea></label>
              </div>
              <label class="check"><input type="checkbox" [(ngModel)]="tp.isActive" [name]="tp.code + '-on'" />
                <span>{{ 'messages.active' | t }}</span></label>
              <div class="actions">
                <button type="button" class="dose-btn dose-btn--primary small-btn" (click)="saveTemplate(tp)">{{ 'common.save' | t }}</button>
                @if (tplStatus()[tp.code]; as st) { <span [class]="st.ok ? 'ok' : 'err'" role="status">{{ st.text }}</span> }
              </div>
            </div>
          </details>
        }
      } @else {
        @for (m of messages(); track m.id) {
          <article class="dose-card item" [class.due]="m.status === 'Pending' && isDue(m)">
            <div class="row">
              <strong>{{ title(m) }}</strong>
              <span class="badge badge--gray">{{ m.language === 'en' ? 'EN' : 'ع' }}</span>
              @if (m.bookingReference) { <span class="muted small" dir="ltr">{{ m.bookingReference }}</span> }
              @if (m.status === 'Pending' && !isDue(m)) { <span class="badge">{{ 'messages.scheduled' | t: { when: dt(m.dueAtUtc) } }}</span> }
            </div>
            <span class="small">{{ m.recipientName }} · <span dir="ltr">{{ m.recipientPhone }}</span>
              @if (m.guardianPhone) { · {{ 'admin.guardian' | t }} <span dir="ltr">{{ m.guardianPhone }}</span> }</span>
            <p class="preview" [attr.dir]="m.language === 'en' ? 'ltr' : 'rtl'">{{ m.preview }}</p>
            <span class="muted small">
              {{ 'messages.created' | t: { when: dt(m.createdAtUtc) } }}
              @if (m.handledAtUtc) { · {{ (m.status === 'Sent' ? 'messages.sentBy' : 'messages.skippedBy') | t: { when: dt(m.handledAtUtc), who: m.handledBy } }} }
            </span>

            @if (m.status === 'Pending') {
              <div class="actions">
                <button type="button" class="dose-btn dose-btn--primary small-btn" (click)="prepare(m)" [disabled]="busy() === m.id">{{ 'messages.prepare' | t }}</button>
                <button type="button" class="dose-btn dose-btn--ghost small-btn" (click)="mark(m, true)" [disabled]="busy() === m.id">{{ 'messages.markSent' | t }}</button>
                <button type="button" class="dose-btn small-btn" (click)="mark(m, false)" [disabled]="busy() === m.id">{{ 'messages.skip' | t }}</button>
              </div>
            }
            @if (composed()?.id === m.id) {
              @let c = composed()!.msg;
              <div class="link-box">
                <p class="small">{{ 'messages.howTo' | t }}</p>
                <textarea readonly [value]="c.text" [attr.dir]="m.language === 'en' ? 'ltr' : 'rtl'" [attr.aria-label]="'followUps.msgLabel' | t"></textarea>
                <div class="actions">
                  <button type="button" class="dose-btn dose-btn--primary small-btn" (click)="copy(c.text)">{{ (copied() ? 'common.copied' : 'messages.copy') | t }}</button>
                  <a class="dose-btn dose-btn--whatsapp small-btn" [href]="c.chatUrl" target="_blank" rel="noopener">{{ 'messages.openChat' | t }}</a>
                  @if (c.guardianChatUrl) {
                    <a class="dose-btn dose-btn--whatsapp small-btn" [href]="c.guardianChatUrl" target="_blank" rel="noopener">{{ 'messages.openGuardianChat' | t }}</a>
                  }
                  @if (m.status === 'Pending') {
                    <button type="button" class="dose-btn dose-btn--ghost small-btn" (click)="mark(m, true)">{{ 'messages.markSent' | t }}</button>
                  }
                </div>
              </div>
            }
          </article>
        } @empty {
          <p class="muted">{{ 'messages.empty' | t }}</p>
        }
      }
    </section>
  `,
  styles: `
    .tabs { display: flex; flex-wrap: wrap; gap: 6px; }
    .tabs button { min-height: 38px; padding: 0 14px; border: 1px solid var(--dose-border); border-radius: 999px; background: var(--dose-surface); font: 600 0.9rem var(--dose-font); cursor: pointer; }
    .tabs button[aria-selected='true'] { background: var(--dose-primary); border-color: var(--dose-primary); color: #fff; }
    .item { padding: 14px 16px; display: grid; gap: 8px; }
    .item.due { border-color: var(--dose-accent); }
    .row { display: flex; gap: 8px; align-items: center; flex-wrap: wrap; }
    .preview { margin: 0; padding: 10px 12px; background: var(--dose-bg); border-radius: 8px; white-space: pre-line; font-size: 0.92rem; }
    .actions { display: flex; flex-wrap: wrap; gap: 8px; align-items: center; }
    .small-btn { min-height: 38px; padding: 0 12px; font-size: 0.88rem; }
    .link-box { display: grid; gap: 8px; padding: 12px; border-radius: var(--dose-radius-sm); background: var(--dose-success-50); }
    .link-box p { margin: 0; }
    .link-box textarea { min-height: 140px; padding: 8px; border: 1px solid var(--dose-border); border-radius: 8px; font: 0.9rem var(--dose-font); }
    .tpl { padding: 0; overflow: hidden; }
    .tpl.off { opacity: .75; }
    .tpl summary { display: flex; flex-wrap: wrap; gap: 8px; align-items: center; padding: 14px 16px; cursor: pointer; list-style: none; }
    .tpl summary::-webkit-details-marker { display: none; }
    .tpl code { font-size: 0.8rem; color: var(--dose-text-muted); }
    .tpl-body { display: grid; gap: 12px; padding: 0 16px 16px; }
    .chips { display: flex; flex-wrap: wrap; gap: 6px; align-items: center; }
    .chip { padding: 2px 8px; border-radius: 999px; background: var(--dose-primary-50); color: var(--dose-primary); font-size: 0.82rem; }
    .two { display: grid; gap: 12px; }
    .two textarea { min-height: 150px; }
    @media (min-width: 760px) { .two { grid-template-columns: 1fr 1fr; } }
    .ok { color: var(--dose-success); font-weight: 600; }
    .err { color: #a52714; font-weight: 600; }
  `,
})
export class AdminMessages {
  private readonly api = inject(AdminApi);
  protected readonly lang = LANG;
  protected readonly tabs = (['Pending', 'Sent', 'Skipped', 'templates'] as const).map((key) => ({ key, label: tr(`messages.tabs.${key}`) }));
  protected readonly tab = signal<MessageStatus | 'templates'>('Pending');
  protected readonly messages = signal<MessageA[]>([]);
  protected readonly templates = signal<TemplateA[]>([]);
  protected readonly composed = signal<{ id: string; msg: ComposedMessage } | null>(null);
  protected readonly tplStatus = signal<Record<string, { ok: boolean; text: string }>>({});
  protected readonly error = signal('');
  protected readonly busy = signal<string | null>(null);
  protected readonly copied = signal(false);
  protected readonly dt = fmtDateTime;

  constructor() { void this.load(); }

  protected setTab(t: MessageStatus | 'templates') {
    this.tab.set(t);
    this.composed.set(null);
    void this.load();
  }

  protected title(m: MessageA) { const k = `messages.title.${m.templateCode}`; const t = tr(k); return t === k ? m.templateTitle : t; }

  protected isDue(m: MessageA) { return new Date(m.dueAtUtc).getTime() <= Date.now(); }

  private async load() {
    this.error.set('');
    try {
      const t = this.tab();
      if (t === 'templates') this.templates.set((await this.api.templates()).map((x) => ({ ...x })));
      else this.messages.set(await this.api.messages(t));
    } catch (e) { this.error.set(errorMessage(e)); }
  }

  protected async prepare(m: MessageA) {
    this.busy.set(m.id);
    try { this.composed.set({ id: m.id, msg: await this.api.composeMessage(m.id) }); }
    catch (e) { this.error.set(errorMessage(e)); }
    finally { this.busy.set(null); }
  }

  protected async mark(m: MessageA, sent: boolean) {
    this.busy.set(m.id);
    try {
      if (sent) await this.api.markMessageSent(m.id); else await this.api.skipMessage(m.id);
      this.composed.set(null);
      await this.load();
    } catch (e) { this.error.set(errorMessage(e)); }
    finally { this.busy.set(null); }
  }

  protected async saveTemplate(tp: TemplateA) {
    try {
      await this.api.saveTemplate(tp.code, tp.bodyAr, tp.bodyEn, tp.isActive);
      this.tplStatus.update((s) => ({ ...s, [tp.code]: { ok: true, text: tr('common.saved') } }));
    } catch (e) {
      this.tplStatus.update((s) => ({ ...s, [tp.code]: { ok: false, text: errorMessage(e) } }));
    }
  }

  protected async copy(text: string) {
    try { await navigator.clipboard.writeText(text); this.copied.set(true); setTimeout(() => this.copied.set(false), 2000); } catch { /* ignore */ }
  }
}
