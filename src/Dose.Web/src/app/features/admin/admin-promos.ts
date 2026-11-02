import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AdminApi, GradeA, LookupA, PromoA, ServiceTypeA, SessionA, cairoToUtc } from '../../core/admin-api';
import { errorMessage, fmtDate, fmtMoney } from '../../core/format';
import { TranslatePipe } from '../../core/i18n';
import { optName } from './entity-editor';

interface LessonOpt { id: string; name: string }

/**
 * Promo codes: percentage or fixed amount, optionally limited to a grade, subject, service type, lesson or one
 * session, with dates and use limits (total, per WhatsApp number, per session). The student types the code while
 * booking; uses come back automatically when a booking expires or is cancelled before payment.
 */
@Component({
  selector: 'dose-admin-promos',
  imports: [FormsModule, TranslatePipe],
  template: `
    <section class="page">
      <h1 class="page-title">{{ 'titles.adminPromos' | t }}</h1>
      <p class="muted small">{{ 'promoAdmin.lead' | t }}</p>
      @if (error()) { <p class="alert alert--error" role="alert">{{ error() }}</p> }

      @if (form(); as f) {
        <form class="dose-card box" (ngSubmit)="save(f)">
          <h2>{{ (f.id ? 'promoAdmin.edit' : 'promoAdmin.new') | t }}</h2>
          <div class="two">
            <label class="field"><span>{{ 'promoAdmin.code' | t }} <span class="req">*</span></span>
              <input name="code" [(ngModel)]="f.code" dir="ltr" maxlength="30" class="upper" /><span class="hint">{{ 'promoAdmin.codeHint' | t }}</span></label>
            <label class="field"><span>{{ 'promoAdmin.description' | t }}</span>
              <input name="desc" [(ngModel)]="f.description" maxlength="300" /></label>
          </div>
          <div class="two">
            <label class="field"><span>{{ 'promoAdmin.kind' | t }}</span>
              <select name="kind" [(ngModel)]="f.kind">
                <option value="Percent">{{ 'promoAdmin.percent' | t }}</option>
                <option value="Fixed">{{ 'promoAdmin.fixed' | t }}</option>
              </select></label>
            <label class="field"><span>{{ (f.kind === 'Percent' ? 'promoAdmin.valuePercent' : 'promoAdmin.valueAmount') | t }} <span class="req">*</span></span>
              <input name="value" type="number" min="1" [max]="f.kind === 'Percent' ? 100 : 100000" [(ngModel)]="f.value" /></label>
          </div>

          <fieldset class="scope"><legend>{{ 'promoAdmin.scope' | t }}</legend>
            <p class="muted small">{{ 'promoAdmin.scopeHint' | t }}</p>
            <div class="two">
              <label class="field"><span>{{ 'common.grade' | t }}</span>
                <select name="grade" [(ngModel)]="f.gradeId" (ngModelChange)="loadLessons(f)"><option [ngValue]="null">{{ 'promoAdmin.any' | t }}</option>
                  @for (g of grades(); track g.id) { <option [ngValue]="g.id">{{ name(g) }}</option> }</select></label>
              <label class="field"><span>{{ 'common.subject' | t }}</span>
                <select name="subject" [(ngModel)]="f.subjectId" (ngModelChange)="loadLessons(f)"><option [ngValue]="null">{{ 'promoAdmin.any' | t }}</option>
                  @for (s of subjects(); track s.id) { <option [ngValue]="s.id">{{ name(s) }}</option> }</select></label>
              <label class="field"><span>{{ 'promoAdmin.service' | t }}</span>
                <select name="service" [(ngModel)]="f.serviceTypeId"><option [ngValue]="null">{{ 'promoAdmin.any' | t }}</option>
                  @for (s of services(); track s.id) { <option [ngValue]="s.id">{{ name(s) }}</option> }</select></label>
              <label class="field"><span>{{ 'promoAdmin.lesson' | t }}</span>
                <select name="lesson" [(ngModel)]="f.lessonId" [disabled]="!lessons().length"><option [ngValue]="null">{{ 'promoAdmin.any' | t }}</option>
                  @for (l of lessons(); track l.id) { <option [ngValue]="l.id">{{ l.name }}</option> }</select>
                @if (!lessons().length) { <span class="hint">{{ 'promoAdmin.lessonHint' | t }}</span> }</label>
            </div>
            <label class="field"><span>{{ 'promoAdmin.session' | t }}</span>
              <select name="session" [(ngModel)]="f.sessionId"><option [ngValue]="null">{{ 'promoAdmin.any' | t }}</option>
                @for (s of sessions(); track s.id) { <option [ngValue]="s.id">{{ s.subjectNameAr }} — {{ s.scopeTitleAr }} ({{ date(s.startUtc) }})</option> }</select></label>
          </fieldset>

          <div class="two">
            <label class="field"><span>{{ 'promoAdmin.from' | t }}</span><input name="from" type="date" [(ngModel)]="fromDate" /></label>
            <label class="field"><span>{{ 'promoAdmin.to' | t }}</span><input name="to" type="date" [(ngModel)]="toDate" /></label>
          </div>
          <div class="three">
            <label class="field"><span>{{ 'promoAdmin.maxTotal' | t }}</span><input name="mt" type="number" min="1" [(ngModel)]="f.maxTotalUses" /></label>
            <label class="field"><span>{{ 'promoAdmin.maxPhone' | t }}</span><input name="mp" type="number" min="1" [(ngModel)]="f.maxUsesPerPhone" /></label>
            <label class="field"><span>{{ 'promoAdmin.maxSession' | t }}</span><input name="ms" type="number" min="1" [(ngModel)]="f.maxUsesPerSession" /></label>
          </div>
          <p class="muted small">{{ 'promoAdmin.limitsHint' | t }}</p>
          <label class="check"><input type="checkbox" name="active" [(ngModel)]="f.isActive" /><span>{{ 'promoAdmin.active' | t }}</span></label>
          <div class="actions">
            <button class="dose-btn dose-btn--primary" type="submit" [disabled]="busy()">{{ 'common.save' | t }}</button>
            <button class="dose-btn dose-btn--ghost" type="button" (click)="form.set(null)">{{ 'common.cancel' | t }}</button>
          </div>
        </form>
      } @else {
        <button class="dose-btn dose-btn--primary" type="button" (click)="edit(null)">+ {{ 'promoAdmin.new' | t }}</button>
      }

      @for (p of promos(); track p.id) {
        <article class="dose-card item" [class.off]="!p.isActive">
          <div class="row">
            <strong class="code" dir="ltr">{{ p.code }}</strong>
            <span class="badge" [class.badge--green]="p.isActive" [class.badge--gray]="!p.isActive">{{ (p.isActive ? 'promoAdmin.on' : 'promoAdmin.off') | t }}</span>
            <span class="value">{{ p.kind === 'Percent' ? p.value + '%' : money(p.value) }}</span>
          </div>
          @if (p.description) { <span class="muted small" dir="auto">{{ p.description }}</span> }
          <span class="small">{{ p.scopeLabel ?? ('promoAdmin.everything' | t) }}</span>
          <span class="small muted">
            {{ 'promoAdmin.usage' | t: { n: p.uses ?? 0, max: p.maxTotalUses ?? '∞', total: money(p.totalDiscount ?? 0) } }}
            @if (p.validToUtc) { · {{ 'promoAdmin.until' | t: { d: date(p.validToUtc) } }} }
          </span>
          <div class="actions">
            <button class="dose-btn dose-btn--ghost small-btn" type="button" (click)="edit(p)">{{ 'common.edit' | t }}</button>
            <button class="dose-btn small-btn" type="button" (click)="toggle(p)">{{ (p.isActive ? 'promoAdmin.disable' : 'promoAdmin.enable') | t }}</button>
          </div>
        </article>
      } @empty {
        <p class="muted">{{ 'promoAdmin.empty' | t }}</p>
      }
    </section>
  `,
  styles: `
    .box { padding: 18px; display: grid; gap: 12px; }
    .box h2 { margin: 0; font-size: 1.1rem; }
    .two { display: grid; gap: 12px; }
    .three { display: grid; gap: 12px; }
    @media (min-width: 700px) { .two { grid-template-columns: 1fr 1fr; } .three { grid-template-columns: 1fr 1fr 1fr; } }
    .scope { border: 1px dashed var(--dose-border); border-radius: var(--dose-radius-sm); padding: 12px; display: grid; gap: 10px; margin: 0; }
    .scope legend { font-weight: 700; padding: 0 6px; }
    .scope p { margin: 0; }
    .upper { text-transform: uppercase; }
    .item { padding: 14px 16px; display: grid; gap: 6px; }
    .item.off { opacity: .7; }
    .row { display: flex; flex-wrap: wrap; gap: 8px; align-items: center; }
    .code { font-size: 1.1rem; letter-spacing: .06em; }
    .value { margin-inline-start: auto; font-weight: 800; color: var(--dose-primary); }
    .actions { display: flex; flex-wrap: wrap; gap: 8px; }
    .small-btn { min-height: 36px; padding: 0 12px; font-size: .88rem; }
  `,
})
export class AdminPromos {
  private readonly api = inject(AdminApi);
  protected readonly promos = signal<PromoA[]>([]);
  protected readonly grades = signal<GradeA[]>([]);
  protected readonly subjects = signal<LookupA[]>([]);
  protected readonly services = signal<ServiceTypeA[]>([]);
  protected readonly sessions = signal<SessionA[]>([]);
  protected readonly lessons = signal<LessonOpt[]>([]);
  protected readonly form = signal<PromoA | null>(null);
  protected readonly error = signal('');
  protected readonly busy = signal(false);
  protected fromDate = '';
  protected toDate = '';
  protected readonly date = fmtDate;
  protected readonly name = optName;
  protected money(v: number) { return fmtMoney(v, 'EGP'); }

  constructor() {
    void this.load();
    Promise.all([this.api.grades(), this.api.subjects(), this.api.serviceTypes(), this.api.sessions('Published', false)])
      .then(([g, s, sv, ss]) => { this.grades.set(g); this.subjects.set(s); this.services.set(sv); this.sessions.set(ss); })
      .catch(() => undefined);
  }

  private async load() {
    try { this.promos.set(await this.api.promos()); } catch (e) { this.error.set(errorMessage(e)); }
  }

  protected edit(p: PromoA | null) {
    this.error.set('');
    const f: PromoA = p ? { ...p } : {
      id: null, code: '', description: null, kind: 'Percent', value: 10, gradeId: null, subjectId: null, serviceTypeId: null,
      lessonId: null, sessionId: null, validFromUtc: null, validToUtc: null, maxTotalUses: null, maxUsesPerPhone: 1,
      maxUsesPerSession: null, isActive: true,
    };
    this.fromDate = f.validFromUtc ? cairoDate(f.validFromUtc) : '';
    this.toDate = f.validToUtc ? cairoDate(f.validToUtc) : '';
    this.form.set(f);
    void this.loadLessons(f);
    window.scrollTo({ top: 0 });
  }

  protected async loadLessons(f: PromoA) {
    this.lessons.set([]);
    if (!f.gradeId || !f.subjectId) { f.lessonId = null; return; }
    try {
      const chapters = await this.api.chapters(f.gradeId, f.subjectId);
      const all: LessonOpt[] = [];
      for (const c of chapters) for (const l of await this.api.lessons(c.id!)) all.push({ id: l.id!, name: `${optName(c)} — ${optName(l)}` });
      this.lessons.set(all);
      if (f.lessonId && !all.some((l) => l.id === f.lessonId)) f.lessonId = null;
    } catch { /* lessons are optional */ }
  }

  protected async save(f: PromoA) {
    this.busy.set(true);
    this.error.set('');
    try {
      const empty = (n: number | null) => (n === null || (n as unknown) === '' || Number.isNaN(n) ? null : Number(n));
      await this.api.savePromo({
        ...f, code: f.code.trim().toUpperCase(), value: Number(f.value),
        validFromUtc: this.fromDate ? cairoToUtc(this.fromDate, '00:00') : null,
        validToUtc: this.toDate ? cairoToUtc(this.toDate, '23:59') : null,
        maxTotalUses: empty(f.maxTotalUses), maxUsesPerPhone: empty(f.maxUsesPerPhone), maxUsesPerSession: empty(f.maxUsesPerSession),
      });
      this.form.set(null);
      await this.load();
    } catch (e) {
      this.error.set(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }

  protected async toggle(p: PromoA) {
    try { await this.api.savePromo({ ...p, isActive: !p.isActive }); await this.load(); }
    catch (e) { this.error.set(errorMessage(e)); }
  }
}

function cairoDate(utc: string) {
  return new Date(utc).toLocaleDateString('en-CA', { timeZone: 'Africa/Cairo' });
}

