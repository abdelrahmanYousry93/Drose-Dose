import { Component, input } from '@angular/core';
import { GroupTerms } from '../core/api';
import { fmtDateTime, fmtMoney } from '../core/format';
import { TranslatePipe } from '../core/i18n';

/** Mandatory disclosure for the target-tier-with-top-up model (spec 6.4). */
@Component({
  selector: 'dose-group-terms',
  imports: [TranslatePipe],
  template: `
    @let g = terms();
    <div class="dose-card terms">
      <h2>{{ 'groupTerms.title' | t }}</h2>
      <p [innerHTML]="'groupTerms.youBook' | t: { tier: bold(g.targetTierName), price: bold(money(price(), currency())), min: g.targetMin, max: g.targetMax }"></p>
      @if (g.tierDecisionAtUtc) {
        <p [innerHTML]="'groupTerms.decideOn' | t: { when: bold(dateTime(g.tierDecisionAtUtc)) }"></p>
      }
      @if (g.maxExtraAmount > 0) {
        <div class="alert">
          <span [innerHTML]="'groupTerms.ifShort' | t"></span>
          <ul>
            <li [innerHTML]="'groupTerms.payDiff' | t: { max: bold(money(g.maxExtraAmount, currency())) }"></li>
            <li>{{ 'groupTerms.orMove' | t }}</li>
            <li>{{ 'groupTerms.orRefund' | t }}</li>
          </ul>
          {{ 'groupTerms.noCharge' | t }}
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
  /** Values are escaped before being wrapped, so server text can't inject markup. */
  protected bold(v: string) { return `<strong>${escapeHtml(v)}</strong>`; }
}

export function escapeHtml(v: string) {
  return v.replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[c]!);
}
