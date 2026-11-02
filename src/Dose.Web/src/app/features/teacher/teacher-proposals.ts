import { HttpClient } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';
import { cairoToUtc } from '../../core/admin-api';
import { Api, ApiError, Lookup, ServiceType, Stage } from '../../core/api';
import { errorMessage, fmtDateTime, fmtDuration, fmtMoney } from '../../core/format';
import { TranslatePipe, translate as tr } from '../../core/i18n';

interface Tier { id: string; nameAr: string; nameEn?: string | null; minParticipants: number; maxParticipants: number; pricePerStudent: number }
interface Preview { currency: string; tiers: Tier[] }
interface Proposal { id: string; title: string; subjectName: string; gradeName: string; startUtc: string; durationMinutes: number; capacity: number; state: 'Pending' | 'Approved' | 'Declined'; reviewNote: string | null }

/**
 * A teacher sees demand for a lesson and proposes a group session (date, size). The price comes from Dose's pricing
 * plans; the admin reviews it and publishes it as an open group students can join.
 */
@Component({
  selector: 'dose-teacher-proposals',
  imports: [FormsModule, TranslatePipe],
  template: `
    <section class="page">
      <h1 class="page-title">{{ 'proposals.title' | t }}</h1>
      <p class="muted small">{{ 'proposals.lead' | t }}</p>
      @if (error()) { <p class="alert alert--error" role="alert">{{ error() }}</p> }
      @if (notice()) { <p class="alert alert--success" role="status">{{ notice() }}</p> }

      <form class="dose-card box" (ngSubmit)="submit()">
        <div class="two">
          <label class="field"><span>{{ 'common.grade' | t }} <span class="req">*</span></span>
            <select name="grade" [(ngModel)]="gradeId" (ngModelChange)="onGrade()">
              <option value="">{{ 'home.chooseGrade' | t }}</option>
              @for (st of stages(); track st.id) { <optgroup [label]="st.name">@for (g of st.grades; track g.id) { <option [value]="g.id">{{ g.name }}</option> }</optgroup> }
            </select></label>
          <label class="field"><span>{{ 'common.subject' | t }} <span class="req">*</span></span>
            <select name="subject" [(ngModel)]="subjectId" (ngModelChange)="loadTiers()" [disabled]="!gradeId">
              <option value="">{{ 'request.choose' | t }}</option>
              @for (s of subjects(); track s.id) { <option [value]="s.id">{{ s.name }}</option> }
            </select></label>
          <label class="field"><span>{{ 'common.track' | t }}</span>
            <select name="track" [(ngModel)]="trackId" (ngModelChange)="loadTiers()">@for (t of tracks(); track t.id) { <option [value]="t.id">{{ t.name }}</option> }</select></label>
          <label class="field"><span>{{ 'request.service' | t }}</span>
            <select name="service" [(ngModel)]="serviceTypeId" (ngModelChange)="loadTiers()">@for (s of services(); track s.id) { <option [value]="s.id">{{ s.name }}</option> }</select></label>
        </div>
        <label class="field"><span>{{ 'proposals.titleField' | t }} <span class="req">*</span></span>
          <input name="title" [(ngModel)]="title" maxlength="200" [placeholder]="'private.scopePlaceholder' | t" /></label>
        <label class="field"><span>{{ 'proposals.details' | t }} <span class="hint">{{ 'common.optional' | t }}</span></span>
          <textarea name="details" [(ngModel)]="details" maxlength="2000"></textarea></label>
        <div class="three">
          <label class="field"><span>{{ 'teacher.day' | t }}</span><input name="date" type="date" [(ngModel)]="date" /></label>
          <label class="field"><span>{{ 'requests.offerTime' | t }}</span><input name="time" type="time" step="900" [(ngModel)]="time" /></label>
          <label class="field"><span>{{ 'requests.offerDuration' | t }}</span><input name="dur" type="number" min="15" max="480" step="15" [(ngModel)]="duration" /></label>
        </div>
        @if (tiers().length) {
          <span class="label">{{ 'proposals.size' | t }}</span>
          <div class="tiers">
            @for (t of tiers(); track t.id) {
              <label class="opt" [class.on]="tierId === t.id"><input type="radio" name="tier" [value]="t.id" [(ngModel)]="tierId" />
                <strong>{{ t.nameEn && lang === 'en' ? t.nameEn : t.nameAr }}</strong>
                <span class="muted small">{{ 'request.studentsRange' | t: { min: t.minParticipants, max: t.maxParticipants } }}</span>
                <span class="small">{{ money(t.pricePerStudent, currency()) }} {{ 'session.perStudent' | t }}</span></label>
            }
          </div>
        } @else if (subjectId) { <p class="muted small">{{ 'proposals.noPricing' | t }}</p> }
        <button class="dose-btn dose-btn--primary" type="submit" [disabled]="busy()">{{ 'proposals.send' | t }}</button>
      </form>

      <h2 class="sub">{{ 'proposals.mine' | t }}</h2>
      @for (p of items(); track p.id) {
        <div class="dose-card item">
          <div><strong>{{ p.subjectName }} — {{ p.title }}</strong>
            <div class="muted small">{{ p.gradeName }} · {{ dt(p.startUtc) }} · {{ dur(p.durationMinutes) }} · {{ 'proposals.upTo' | t: { n: p.capacity } }}</div>
            @if (p.reviewNote) { <div class="muted small">{{ 'teacher.adminNote' | t: { note: p.reviewNote } }}</div> }</div>
          <span class="badge" [class.badge--yellow]="p.state === 'Pending'" [class.badge--green]="p.state === 'Approved'" [class.badge--gray]="p.state === 'Declined'">
            {{ 'proposals.state.' + p.state | t }}</span>
        </div>
      } @empty { <p class="muted">{{ 'proposals.none' | t }}</p> }
    </section>
  `,
  styles: `
    .box { padding: 16px; display: grid; gap: 12px; }
    .two { display: grid; gap: 12px; grid-template-columns: 1fr 1fr; }
    .three { display: grid; gap: 12px; grid-template-columns: 1.4fr 1fr 1fr; }
    .label { font-weight: 600; }
    .tiers { display: grid; gap: 8px; grid-template-columns: repeat(auto-fit, minmax(150px, 1fr)); }
    .opt { display: grid; gap: 2px; padding: 10px; border: 2px solid var(--dose-border); border-radius: var(--dose-radius-sm); cursor: pointer; position: relative; }
    .opt input { position: absolute; opacity: 0; }
    .opt.on { border-color: var(--dose-primary); background: var(--dose-primary-50); }
    .sub { margin: 24px 0 8px; font-size: 1.1rem; }
    .item { padding: 12px 14px; display: flex; justify-content: space-between; align-items: center; gap: 8px; flex-wrap: wrap; margin-bottom: 8px; }
  `,
})
export class TeacherProposals {
  private readonly http = inject(HttpClient);
  private readonly api = inject(Api);
  private readonly base = `${environment.apiBaseUrl}/teacher`;
  protected readonly lang = document.documentElement.lang;
  protected readonly stages = signal<Stage[]>([]);
  protected readonly tracks = signal<Lookup[]>([]);
  protected readonly subjects = signal<Lookup[]>([]);
  protected readonly services = signal<ServiceType[]>([]);
  protected readonly tiers = signal<Tier[]>([]);
  protected readonly currency = signal('EGP');
  protected readonly items = signal<Proposal[]>([]);
  protected readonly error = signal('');
  protected readonly notice = signal('');
  protected readonly busy = signal(false);
  protected gradeId = '';
  protected subjectId = '';
  protected trackId = '';
  protected serviceTypeId = '';
  protected tierId = '';
  protected title = '';
  protected details = '';
  protected date = new Date(Date.now() + 3 * 86_400_000).toLocaleDateString('en-CA', { timeZone: 'Africa/Cairo' });
  protected time = '18:00';
  protected duration = 90;
  protected readonly money = fmtMoney;
  protected readonly dt = fmtDateTime;
  protected readonly dur = fmtDuration;

  constructor() {
    Promise.all([this.api.stages(), this.api.studyTracks(), this.api.services()]).then(([st, t, sv]) => {
      this.stages.set(st); this.tracks.set(t); this.services.set(sv);
      this.trackId = t[0]?.id ?? ''; this.serviceTypeId = sv[0]?.id ?? '';
    }).catch((e) => this.error.set(errorMessage(e)));
    void this.load();
  }

  private async call<T>(p: Promise<T>) {
    try { return await p; } catch (e) {
      const err = e as { error?: { code?: string; title?: string }; status?: number; message?: string };
      throw new ApiError(err.error?.code ?? `http.${err.status}`, err.error?.title ?? err.message ?? '', err.status ?? 0);
    }
  }

  private async load() {
    try { this.items.set(await this.call(firstValueFrom(this.http.get<Proposal[]>(`${this.base}/proposals`)))); }
    catch (e) { this.error.set(errorMessage(e)); }
  }

  protected async onGrade() {
    this.subjectId = '';
    this.tiers.set([]);
    this.subjects.set(this.gradeId ? await this.api.subjects(this.gradeId).catch(() => []) : []);
  }

  protected async loadTiers() {
    this.tiers.set([]);
    this.tierId = '';
    if (!this.gradeId || !this.subjectId || !this.trackId || !this.serviceTypeId) return;
    try {
      const p = await this.call(firstValueFrom(this.http.get<Preview>(`${this.base}/pricing-preview`, {
        params: { serviceTypeId: this.serviceTypeId, gradeId: this.gradeId, studyTrackId: this.trackId, subjectId: this.subjectId },
      })));
      this.tiers.set(p.tiers);
      this.currency.set(p.currency);
      this.tierId = p.tiers[p.tiers.length - 1]?.id ?? '';
    } catch { /* no group pricing for this combination */ }
  }

  protected async submit() {
    this.error.set('');
    this.notice.set('');
    if (!this.gradeId || !this.subjectId || !this.title.trim() || !this.tierId || !this.date || !this.time) {
      this.error.set(tr('proposals.missing'));
      return;
    }
    this.busy.set(true);
    try {
      await this.call(firstValueFrom(this.http.post(`${this.base}/proposals`, {
        serviceTypeId: this.serviceTypeId, gradeId: this.gradeId, studyTrackId: this.trackId, subjectId: this.subjectId,
        title: this.title.trim(), details: this.details.trim() || null, startUtc: cairoToUtc(this.date, this.time),
        durationMinutes: Number(this.duration), targetTierId: this.tierId,
      })));
      this.notice.set(tr('proposals.sent'));
      this.title = '';
      this.details = '';
      await this.load();
    } catch (e) {
      this.error.set(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }
}
