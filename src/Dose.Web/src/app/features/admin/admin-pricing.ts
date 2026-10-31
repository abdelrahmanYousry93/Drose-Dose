import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AdminApi, PlanA, TierA } from '../../core/admin-api';
import { errorMessage, fmtMoney } from '../../core/format';
import { Option } from './entity-editor';

interface PlanEdit extends PlanA { _private: number | null; _tiers: TierA[]; _msg?: { ok: boolean; text: string } }

/**
 * Pricing plans: who a price applies to (service, mode, band, track, subject, dates) and the prices themselves.
 * Prices of a version already used by a session are locked — saving creates a new version.
 */
@Component({
  selector: 'dose-admin-pricing',
  imports: [FormsModule],
  template: `
    <section class="page">
      <div class="row">
        <h1 class="page-title">الأسعار</h1>
        <button type="button" class="dose-btn dose-btn--primary" (click)="add()">+ خطة تسعير</button>
      </div>
      <p class="muted small">
        كل خطة بتحدد لمين السعر ده: نوع الخدمة، فردي ولا جماعي، وممكن تخصصها لشريحة صفوف أو منهج أو مادة.
        لو أكتر من خطة تنطبق، الأكثر تحديداً هي اللي بتتطبق. الأرقام اللي اسمها SAMPLE تجريبية وغيّرها قبل الإطلاق.
      </p>
      @if (error()) { <p class="alert alert--error">{{ error() }}</p> }

      @for (p of plans(); track $index) {
        <details class="dose-card plan" [open]="!p.id" [class.inactive]="!p.isActive">
          <summary>
            <strong>{{ p.name || 'خطة جديدة' }}</strong>
            <span class="badge">{{ p.mode === 'Group' ? 'جماعي' : 'فردي' }}</span>
            <span class="muted small">{{ name(services(), p.serviceTypeId) }}</span>
            @if (p.currentVersion) { <span class="badge badge--gray">نسخة {{ p.currentVersion.versionNo }}{{ p.currentVersion.isLocked ? ' · مستخدمة' : '' }}</span> }
            @else if (p.id) { <span class="badge badge--yellow">مفيش أسعار لسه</span> }
          </summary>

          <div class="body">
            <h3>لمين الخطة دي؟</h3>
            <div class="grid">
              <label class="field"><span>اسم الخطة</span><input [(ngModel)]="p.name" /></label>
              <label class="field"><span>نوع الخدمة</span>
                <select [(ngModel)]="p.serviceTypeId">@for (o of services(); track o.id) { <option [ngValue]="o.id">{{ o.nameAr }}</option> }</select></label>
              <label class="field"><span>فردي / جماعي</span>
                <select [(ngModel)]="p.mode" [disabled]="!!p.id"><option ngValue="Group">جماعي</option><option ngValue="Private">فردي</option></select></label>
              <label class="field"><span>شريحة الصفوف</span>
                <select [(ngModel)]="p.pricingBandId"><option [ngValue]="null">كل الصفوف</option>
                  @for (o of bands(); track o.id) { <option [ngValue]="o.id">{{ o.nameAr }}</option> }</select></label>
              <label class="field"><span>المنهج</span>
                <select [(ngModel)]="p.studyTrackId"><option [ngValue]="null">كل المناهج</option>
                  @for (o of tracks(); track o.id) { <option [ngValue]="o.id">{{ o.nameAr }}</option> }</select></label>
              <label class="field"><span>المادة</span>
                <select [(ngModel)]="p.subjectId"><option [ngValue]="null">كل المواد</option>
                  @for (o of subjects(); track o.id) { <option [ngValue]="o.id">{{ o.nameAr }}</option> }</select></label>
              <label class="field"><span>يبدأ من</span><input type="date" [ngModel]="p.effectiveFromUtc.slice(0, 10)" (ngModelChange)="p.effectiveFromUtc = $event + 'T00:00:00Z'" /></label>
              <label class="field"><span>ينتهي (اختياري)</span><input type="date" [ngModel]="p.effectiveToUtc?.slice(0, 10)" (ngModelChange)="p.effectiveToUtc = $event ? $event + 'T00:00:00Z' : null" /></label>
              <label class="check"><input type="checkbox" [(ngModel)]="p.isActive" /><span>مفعّلة</span></label>
            </div>
            <button type="button" class="dose-btn dose-btn--ghost" (click)="savePlan(p)">حفظ بيانات الخطة</button>

            @if (p.id) {
              <h3>الأسعار</h3>
              @if (p.currentVersion?.isLocked) {
                <p class="alert alert--info small">الأسعار دي مستخدمة في جلسات. لو غيّرتها هتتعمل نسخة جديدة للجلسات الجديدة بس، والجلسات والحجوزات الحالية هتفضل بسعرها.</p>
              }
              <label class="field narrow">
                <span>{{ p.mode === 'Private' ? 'سعر الجلسة الفردية للطالب' : 'سعر الطالب لو بقى لوحده (اختياري)' }}</span>
                <input type="number" min="1" [(ngModel)]="p._private" dir="ltr" />
                @if (p.mode === 'Group') { <span class="hint">بيتستخدم لحساب أقصى فرق ممكن لو الجروب مكملش.</span> }
              </label>

              @if (p.mode === 'Group') {
                <table class="tiers">
                  <thead><tr><th>اسم الشريحة</th><th>من</th><th>لـ</th><th>السعر للطالب</th><th></th></tr></thead>
                  <tbody>
                    @for (t of p._tiers; track $index) {
                      <tr>
                        <td><input [(ngModel)]="t.nameAr" aria-label="اسم الشريحة" /></td>
                        <td><input type="number" min="1" [(ngModel)]="t.minParticipants" dir="ltr" aria-label="أقل عدد" /></td>
                        <td><input type="number" min="1" [(ngModel)]="t.maxParticipants" dir="ltr" aria-label="أكبر عدد" /></td>
                        <td><input type="number" min="1" [(ngModel)]="t.pricePerStudent" dir="ltr" aria-label="السعر" /></td>
                        <td><button type="button" class="x" (click)="p._tiers.splice($index, 1)" aria-label="حذف الشريحة">✕</button></td>
                      </tr>
                    }
                  </tbody>
                </table>
                <button type="button" class="dose-btn dose-btn--ghost" (click)="addTier(p)">+ شريحة</button>
              }
              <button type="button" class="dose-btn dose-btn--primary" (click)="savePrices(p)">حفظ الأسعار</button>
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
    .tiers td:nth-child(2) input, .tiers td:nth-child(3) input { max-width: 70px; }
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
      this.services.set(s.map((x) => ({ id: x.id, nameAr: x.nameAr })));
      this.bands.set(b.map((x) => ({ id: x.id!, nameAr: x.nameAr })));
      this.tracks.set(t.map((x) => ({ id: x.id!, nameAr: x.nameAr })));
      this.subjects.set(su.map((x) => ({ id: x.id!, nameAr: x.nameAr })));
    }).catch((e) => this.error.set(errorMessage(e)));
  }

  protected name(list: Option[], id: string | null) { return list.find((o) => o.id === id)?.nameAr ?? ''; }

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
      _tiers: [{ id: null, nameAr: 'جروب صغير', minParticipants: 2, maxParticipants: 4, pricePerStudent: 0, sortOrder: 1 }],
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
      p._msg = { ok: true, text: 'اتحفظت بيانات الخطة ✓' + (p.currentVersion ? '' : ' — حط الأسعار تحت.') };
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
      p._msg = { ok: true, text: wasLocked ? `اتعملت نسخة أسعار جديدة (${saved.currentVersion!.versionNo}) ✓` : 'اتحفظت الأسعار ✓' };
    } catch (e) { p._msg = { ok: false, text: errorMessage(e) }; }
    this.plans.update((l) => [...l]);
  }
}
