import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AdminApi, PlanA, TierA } from '../../core/admin-api';
import { errorMessage, fmtMoney } from '../../core/format';
import { TranslatePipe, translate as tr } from '../../core/i18n';
import { Option, optName } from './entity-editor';

interface PlanEdit extends PlanA { _private: number | null; _tiers: TierA[]; _msg?: { ok: boolean; text: string } }

/**
 * Pricing plans: who a price applies to (service, mode, band, track, subject, dates) and the prices themselves.
 * Prices of a version already used by a session are locked — saving creates a new version.
 */
@Component({
  selector: 'dose-admin-pricing',
  imports: [FormsModule, TranslatePipe],
  template: `
    <section class="page">
      <div class="row">
        <h1 class="page-title">{{ 'titles.adminPricing' | t }}</h1>
        <button type="button" class="dose-btn dose-btn--primary" (click)="add()">+ {{ 'pricing.addPlan' | t }}</button>
      </div>
      <p class="muted small">{{ 'pricing.lead' | t }}</p>
      @if (error()) { <p class="alert alert--error">{{ error() }}</p> }

      @for (p of plans(); track $index) {
        <details class="dose-card plan" [open]="!p.id" [class.inactive]="!p.isActive">
          <summary>
            <strong dir="auto">{{ p.name || ('pricing.newPlan' | t) }}</strong>
            <span class="badge">{{ (p.mode === 'Group' ? 'pricing.group' : 'pricing.private') | t }}</span>
            <span class="muted small">{{ name(services(), p.serviceTypeId) }}</span>
            @if (p.currentVersion) { <span class="badge badge--gray">{{ 'pricing.version' | t: { n: p.currentVersion.versionNo } }}{{ p.currentVersion.isLocked ? (' · ' + ('pricing.inUse' | t)) : '' }}</span> }
            @else if (p.id) { <span class="badge badge--yellow">{{ 'pricing.noPrices' | t }}</span> }
          </summary>

          <div class="body">
            <h3>{{ 'pricing.whoFor' | t }}</h3>
            <div class="grid">
              <label class="field"><span>{{ 'pricing.planName' | t }}</span><input [(ngModel)]="p.name" dir="auto" /></label>
              <label class="field"><span>{{ 'pricing.service' | t }}</span>
                <select [(ngModel)]="p.serviceTypeId">@for (o of services(); track o.id) { <option [ngValue]="o.id">{{ optName(o) }}</option> }</select></label>
              <label class="field"><span>{{ 'pricing.mode' | t }}</span>
                <select [(ngModel)]="p.mode" [disabled]="!!p.id"><option ngValue="Group">{{ 'pricing.group' | t }}</option><option ngValue="Private">{{ 'pricing.private' | t }}</option></select></label>
              <label class="field"><span>{{ 'pricing.band' | t }}</span>
                <select [(ngModel)]="p.pricingBandId"><option [ngValue]="null">{{ 'pricing.allGrades' | t }}</option>
                  @for (o of bands(); track o.id) { <option [ngValue]="o.id">{{ optName(o) }}</option> }</select></label>
              <label class="field"><span>{{ 'common.track' | t }}</span>
                <select [(ngModel)]="p.studyTrackId"><option [ngValue]="null">{{ 'f.allTracks' | t }}</option>
                  @for (o of tracks(); track o.id) { <option [ngValue]="o.id">{{ optName(o) }}</option> }</select></label>
              <label class="field"><span>{{ 'common.subject' | t }}</span>
                <select [(ngModel)]="p.subjectId"><option [ngValue]="null">{{ 'home.allSubjects' | t }}</option>
                  @for (o of subjects(); track o.id) { <option [ngValue]="o.id">{{ optName(o) }}</option> }</select></label>
              <label class="field"><span>{{ 'pricing.from' | t }}</span><input type="date" [ngModel]="p.effectiveFromUtc.slice(0, 10)" (ngModelChange)="p.effectiveFromUtc = $event + 'T00:00:00Z'" /></label>
              <label class="field"><span>{{ 'pricing.to' | t }}</span><input type="date" [ngModel]="p.effectiveToUtc?.slice(0, 10)" (ngModelChange)="p.effectiveToUtc = $event ? $event + 'T00:00:00Z' : null" /></label>
              <label class="check"><input type="checkbox" [(ngModel)]="p.isActive" /><span>{{ 'f.activeF' | t }}</span></label>
            </div>
            <button type="button" class="dose-btn dose-btn--ghost" (click)="savePlan(p)">{{ 'pricing.savePlan' | t }}</button>

            @if (p.id) {
              <h3>{{ 'titles.adminPricing' | t }}</h3>
              @if (p.currentVersion?.isLocked) {
                <p class="alert alert--info small">{{ 'pricing.lockedHint' | t }}</p>
              }
              <label class="field narrow">
                <span>{{ (p.mode === 'Private' ? 'pricing.privatePrice' : 'pricing.alonePrice') | t }}</span>
                <input type="number" min="1" [(ngModel)]="p._private" dir="ltr" />
                @if (p.mode === 'Group') { <span class="hint">{{ 'pricing.aloneHint' | t }}</span> }
              </label>

              @if (p.mode === 'Group') {
                <table class="tiers">
                  <thead><tr><th>{{ 'pricing.tierName' | t }}</th><th>{{ 'pricing.tierNameEn' | t }}</th><th>{{ 'pricing.min' | t }}</th><th>{{ 'pricing.max' | t }}</th><th>{{ 'details.price' | t }}</th><th></th></tr></thead>
                  <tbody>
                    @for (t of p._tiers; track $index) {
                      <tr>
                        <td><input [(ngModel)]="t.nameAr" [attr.aria-label]="'pricing.tierName' | t" /></td>
                        <td><input [(ngModel)]="t.nameEn" dir="ltr" [attr.aria-label]="'pricing.tierNameEn' | t" /></td>
                        <td><input type="number" min="1" [(ngModel)]="t.minParticipants" dir="ltr" [attr.aria-label]="'pricing.minA' | t" /></td>
                        <td><input type="number" min="1" [(ngModel)]="t.maxParticipants" dir="ltr" [attr.aria-label]="'pricing.maxA' | t" /></td>
                        <td><input type="number" min="1" [(ngModel)]="t.pricePerStudent" dir="ltr" [attr.aria-label]="'pricing.priceA' | t" /></td>
                        <td><button type="button" class="x" (click)="p._tiers.splice($index, 1)" [attr.aria-label]="'pricing.removeTier' | t">✕</button></td>
                      </tr>
                    }
                  </tbody>
                </table>
                <button type="button" class="dose-btn dose-btn--ghost" (click)="addTier(p)">+ {{ 'pricing.tier' | t }}</button>
              }
              <button type="button" class="dose-btn dose-btn--primary" (click)="savePrices(p)">{{ 'pricing.savePrices' | t }}</button>
            }
            @if (p._msg; as m) { <p [class]="m.ok ? 'alert alert--ok' : 'alert alert--error'">{{ m.text }}</p> }
          </div>
        </details>
      }
    </section>
  `,
  styles: `
    .row { display: flex; justify-content: space-between; align-items: center; gap: 8px; }
    .plan { padding: 0; overflow: hidden; }
    .plan.inactive { opacity: .7; }
    summary { display: flex; flex-wrap: wrap; gap: 8px; align-items: center; padding: 14px 16px; cursor: pointer; list-style: none; }
    summary::-webkit-details-marker { display: none; }
    .body { display: grid; gap: 12px; padding: 0 16px 16px; }
    h3 { margin: 8px 0 0; font-size: 1rem; color: var(--dose-navy); }
    .grid { display: grid; gap: 12px; }
    @media (min-width: 760px) { .grid { grid-template-columns: 1fr 1fr 1fr; } }
    .narrow { max-width: 320px; }
    .tiers { width: 100%; border-collapse: collapse; font-size: 0.9rem; }
    .tiers th { text-align: start; color: var(--dose-text-muted); font-weight: 600; padding: 4px; }
    .tiers td { padding: 4px; }
    .tiers input { width: 100%; min-height: 40px; padding: 0 8px; border: 1px solid var(--dose-border); border-radius: 8px; font: inherit; }
    .tiers td:nth-child(3) input, .tiers td:nth-child(4) input { max-width: 70px; }
    @media (max-width: 759px) { .tiers { display: block; overflow-x: auto; } }
    .x { border: 0; background: #fdecea; color: #a52714; border-radius: 8px; width: 36px; height: 36px; cursor: pointer; }
  `,
})
export class AdminPricing {
  private readonly api = inject(AdminApi);
  protected readonly plans = signal<PlanEdit[]>([]);
  protected readonly services = signal<Option[]>([]);
  protected readonly bands = signal<Option[]>([]);
  protected readonly tracks = signal<Option[]>([]);
  protected readonly subjects = signal<Option[]>([]);
  protected readonly error = signal('');
  protected readonly money = fmtMoney;
  protected readonly count = computed(() => this.plans().length);

  constructor() {
    void this.load();
    Promise.all([this.api.serviceTypes(), this.api.bands(), this.api.tracks(), this.api.subjects()]).then(([s, b, t, su]) => {
      this.services.set(s.map((x) => ({ id: x.id, nameAr: x.nameAr, nameEn: x.nameEn })));
      this.bands.set(b.map((x) => ({ id: x.id!, nameAr: x.nameAr, nameEn: x.nameEn })));
      this.tracks.set(t.map((x) => ({ id: x.id!, nameAr: x.nameAr, nameEn: x.nameEn })));
      this.subjects.set(su.map((x) => ({ id: x.id!, nameAr: x.nameAr, nameEn: x.nameEn })));
    }).catch((e) => this.error.set(errorMessage(e)));
  }

  protected readonly optName = optName;
  protected name(list: Option[], id: string | null) { const o = list.find((x) => x.id === id); return o ? optName(o) : ''; }

  private toEdit(p: PlanA): PlanEdit {
    return { ...p, _private: p.currentVersion?.privatePrice ?? null, _tiers: (p.currentVersion?.tiers ?? []).map((t) => ({ ...t })) };
  }

  private async load() {
    try { this.plans.set((await this.api.plans()).map((p) => this.toEdit(p))); } catch (e) { this.error.set(errorMessage(e)); }
  }

  protected add() {
    const today = new Date().toISOString().slice(0, 10);
    this.plans.update((list) => [{
      id: null, name: '', serviceTypeId: this.services()[0]?.id ?? '', mode: 'Group', pricingBandId: null, studyTrackId: null,
      subjectId: null, currency: 'EGP', policy: 'TargetTierWithTopUp', effectiveFromUtc: today + 'T00:00:00Z', effectiveToUtc: null,
      isActive: true, currentVersion: null, _private: null,
      _tiers: [{ id: null, nameAr: 'جروب صغير', nameEn: 'Small group', minParticipants: 2, maxParticipants: 4, pricePerStudent: 0, sortOrder: 1 }],
    }, ...list]);
  }

  protected addTier(p: PlanEdit) {
    const last = p._tiers[p._tiers.length - 1];
    const min = last ? Number(last.maxParticipants) + 1 : 2;
    p._tiers.push({ id: null, nameAr: '', minParticipants: min, maxParticipants: min + 4, pricePerStudent: 0, sortOrder: p._tiers.length + 1 });
  }

  protected async savePlan(p: PlanEdit) {
    try {
      const { id } = await this.api.savePlan(p);
      p.id = id;
      p._msg = { ok: true, text: tr('pricing.planSaved') + (p.currentVersion ? '' : tr('pricing.addPricesBelow')) };
    } catch (e) { p._msg = { ok: false, text: errorMessage(e) }; }
    this.plans.update((l) => [...l]);
  }

  protected async savePrices(p: PlanEdit) {
    try {
      const tiers = p.mode === 'Group'
        ? p._tiers.map((t, i) => ({ ...t, minParticipants: +t.minParticipants, maxParticipants: +t.maxParticipants, pricePerStudent: +t.pricePerStudent, sortOrder: i + 1 }))
        : [];
      const priv = p._private === null || (p._private as unknown) === '' ? null : Number(p._private);
      const saved = await this.api.savePrices(p.id!, priv, tiers);
      const wasLocked = p.currentVersion?.isLocked;
      Object.assign(p, this.toEdit(saved));
      p._msg = { ok: true, text: wasLocked ? tr('pricing.newVersion', { n: saved.currentVersion!.versionNo }) : tr('pricing.pricesSaved') };
    } catch (e) { p._msg = { ok: false, text: errorMessage(e) }; }
    this.plans.update((l) => [...l]);
  }
}
