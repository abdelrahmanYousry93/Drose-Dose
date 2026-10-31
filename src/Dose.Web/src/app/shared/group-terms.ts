import { Component, input } from '@angular/core';
import { GroupTerms } from '../core/api';
import { fmtDateTime, fmtMoney } from '../core/format';

/** Mandatory disclosure for the target-tier-with-top-up model (spec 6.4). */
@Component({
  selector: 'dose-group-terms',
  template: `
    @let g = terms();
    <div class="dose-card terms">
      <h2>إزاي سعر الجروب بيشتغل؟</h2>
      <p>
        إنت بتحجز في <strong>{{ g.targetTierNameAr }}</strong> وبتدفع
        <strong>{{ money(price(), currency()) }}</strong> بس — سعر الجروب لما يكمل من {{ g.targetMin }} لـ {{ g.targetMax }} طلاب.
      </p>
      @if (g.tierDecisionAtUtc) {
        <p>هنحسم عدد الجروب يوم <strong>{{ dateTime(g.tierDecisionAtUtc) }}</strong>.</p>
      }
      @if (g.maxExtraAmount > 0) {
        <div class="alert">
          لو العدد مكملش لحد الميعاد ده، هنكلمك على واتساب <strong>قبل الجلسة</strong> وتختار:
          <ul>
            <li>تدفع الفرق (أقصى فرق ممكن: <strong>{{ money(g.maxExtraAmount, currency()) }}</strong>)</li>
            <li>أو تنقل لميعاد تاني</li>
            <li>أو تسترد فلوسك كاملة</li>
          </ul>
          ومفيش أي مبلغ هيتخصم منك من غير موافقتك.
        </div>
      }
    </div>
  `,
  styles: `
    .terms { padding: 16px; display: grid; gap: 10px; border-inline-start: 4px solid var(--dose-accent); }
    h2 { margin: 0; font-size: 1.08rem; color: var(--dose-navy); }
    p { margin: 0; }
    ul { margin: 6px 0; padding-inline-start: 20px; }
  `,
})
export class GroupTermsBox {
  readonly terms = input.required<GroupTerms>();
  readonly price = input.required<number>();
  readonly currency = input('EGP');
  protected readonly money = fmtMoney;
  protected readonly dateTime = fmtDateTime;
}
