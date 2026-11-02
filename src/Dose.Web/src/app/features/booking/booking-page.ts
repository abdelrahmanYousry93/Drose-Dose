import { Component, DestroyRef, computed, effect, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Api, BookingView, PaymentMethod, PromoPreview } from '../../core/api';
import { errorMessage, fmtDate, fmtDateTime, fmtDuration, fmtMoney, fmtTime } from '../../core/format';
import { TranslatePipe, translate } from '../../core/i18n';
import { PublicSettingsService } from '../../core/public-settings.service';
import { StudentContext } from '../../core/student-context';
import { looksLikePhone } from '../../shared/validation';
import { escapeHtml } from '../../shared/group-terms';
import { GroupTermsBox } from '../../shared/group-terms';
import { TeacherCard } from '../../shared/teacher-card';

/**
 * The student's private booking page, opened from the secret link /b/:token.
 * One page drives the whole journey by status: details → payment → review → confirmed → join.
 * The meeting URL is never sent in messages; "Join" asks the API for it inside the join window.
 */
@Component({
  selector: 'dose-booking-page',
  imports: [FormsModule, RouterLink, TeacherCard, GroupTermsBox, TranslatePipe],
  templateUrl: './booking-page.html',
  styleUrl: './booking-page.scss',
})
export class BookingPage {
  private readonly api = inject(Api);
  protected readonly settings = inject(PublicSettingsService);
  readonly token = input.required<string>();

  protected readonly b = signal<BookingView | null>(null);
  protected readonly error = signal('');
  protected readonly actionError = signal('');
  protected readonly busy = signal(false);
  protected readonly now = signal(Date.now());
  protected readonly methods = signal<PaymentMethod[]>([]);
  protected readonly copied = signal(false);
  protected readonly inviteCopied = signal(false);
  private readonly context = inject(StudentContext);

  // Promo code (details step)
  protected promoCode = '';
  protected readonly promo = signal<PromoPreview | null>(null);
  protected readonly promoError = signal('');
  protected readonly promoBusy = signal(false);
  /** Fields with a problem, shown in red next to the field. */
  protected readonly invalid = signal<Record<string, string>>({});

  // Details form
  protected studentName = '';
  protected contactPhone = '';
  protected guardianName = '';
  protected guardianPhone = '';
  protected acceptTerms = false;
  protected acceptTopUpTerms = false;

  // Payment form
  protected methodId = '';
  protected senderReference = '';
  protected file: File | null = null;
  protected topUpChoice: '' | 'PayDifference' | 'Transfer' | 'Refund' = '';
  protected acceptNoRefund = false;

  protected readonly fmt = { date: fmtDate, time: fmtTime, dateTime: fmtDateTime, duration: fmtDuration, money: fmtMoney };

  protected bold(v: string) { return `<strong dir="auto">${escapeHtml(v)}</strong>`; }

  protected readonly deadline = computed(() => {
    const v = this.b();
    const at = v?.status === 'PendingHold' ? v.holdExpiresAtUtc : v?.status === 'PendingPayment' ? v.paymentDueAtUtc : null;
    if (!at) return null;
    const ms = new Date(at).getTime() - this.now();
    if (ms <= 0) return '00:00';
    const m = Math.floor(ms / 60000), s = Math.floor((ms % 60000) / 1000);
    return `${String(m).padStart(2, '0')}:${String(s).padStart(2, '0')}`;
  });

  protected readonly step = computed(() => {
    const s = this.b()?.status;
    if (s === 'PendingHold') return 1;
    if (s === 'PendingPayment') return 2;
    if (s === 'PaymentUnderReview') return 3;
    if (s === 'Confirmed' || s === 'Completed' || s === 'TopUpPending') return 4;
    return 0;
  });

  protected readonly whatsAppLink = computed(() => (this.b() ? this.settings.whatsAppLink() : null));

  /** Amount due now: after a previewed promo during the details step. */
  protected readonly amountDue = computed(() => {
    const v = this.b();
    return v?.status === 'PendingHold' && this.promo() ? this.promo()!.netPrice : (v?.price ?? 0);
  });

  /** Invite link friends open to join a private group. */
  protected inviteUrl(code: string) { return `${location.origin}/join/${code}`; }
  protected inviteWhatsApp(v: BookingView) {
    const i = v.invite!;
    const text = translate('invite.shareText', { session: `${v.session.subjectName} — ${v.session.scopeTitle}`,
      when: `${fmtDate(v.session.startUtc)} ${fmtTime(v.session.startUtc)}`, code: i.code, link: this.inviteUrl(i.code) });
    return `https://wa.me/?text=${encodeURIComponent(text)}`;
  }

  constructor() {
    const timer = setInterval(() => this.now.set(Date.now()), 1000);
    inject(DestroyRef).onDestroy(() => clearInterval(timer));
    effect(() => { void this.load(this.token()); });
    // Tell the WhatsApp button where the student is, so the team can help with the exact step.
    effect(() => {
      const v = this.b();
      if (!v) return;
      const steps: Record<string, string> = {
        PendingHold: 'support.step.details', PendingPayment: 'support.step.payment', PaymentUnderReview: 'support.step.review',
        Confirmed: 'support.step.confirmed', TopUpPending: 'support.step.topUp', RefundPending: 'support.step.refund',
        Expired: 'support.step.expired', Cancelled: 'support.step.cancelled',
      };
      this.context.setSupport({
        step: steps[v.status] ?? 'support.step.booking', session: `${v.session.subjectName} — ${v.session.scopeTitle}`,
        sessionTime: `${fmtDate(v.session.startUtc)} ${fmtTime(v.session.startUtc)}`, reference: v.reference, studentName: v.studentName,
      });
    });
    inject(DestroyRef).onDestroy(() => this.context.setSupport({}));
    this.api.paymentMethods().then((m) => {
      this.methods.set(m);
      if (m.length) this.methodId = m[0].id;
    }).catch(() => undefined);
  }

  protected async load(token = this.token()) {
    try {
      const v = await this.api.booking(token);
      // A booking the team prepared for you already has your name and number.
      if (v.status === 'PendingHold' && !this.studentName && v.studentName) this.studentName = v.studentName;
      if (v.status === 'PendingHold' && !this.contactPhone && v.prefillPhone) this.contactPhone = v.prefillPhone.replace(/^\+20/, '0');
      this.b.set(v);
      this.error.set('');
    } catch (e) {
      this.error.set(errorMessage(e));
    }
  }

  protected async applyPromo() {
    this.promoError.set('');
    const code = this.promoCode.trim();
    if (!code) { this.promo.set(null); return; }
    this.promoBusy.set(true);
    try {
      this.promo.set(await this.api.previewPromo(this.token(), code, this.contactPhone.trim() || null));
    } catch (e) {
      this.promo.set(null);
      this.promoError.set(errorMessage(e));
    } finally {
      this.promoBusy.set(false);
    }
  }

  protected removePromo() { this.promo.set(null); this.promoCode = ''; this.promoError.set(''); }

  /** Checks the form before sending, so the student sees exactly which field needs attention. */
  private validateDetails(v: BookingView): boolean {
    const e: Record<string, string> = {};
    if (this.studentName.trim().length < 2) e['studentName'] = translate('validation.name');
    if (!looksLikePhone(this.contactPhone)) e['contactPhone'] = translate('validation.phone');
    if (v.requiresGuardianContact && this.guardianName.trim().length < 2) e['guardianName'] = translate('validation.guardianName');
    if ((v.requiresGuardianContact || this.guardianPhone.trim()) && !looksLikePhone(this.guardianPhone)) e['guardianPhone'] = translate('validation.phone');
    if (v.groupTerms && !v.isClosed && !this.acceptTopUpTerms) e['acceptTopUpTerms'] = translate('validation.acceptGroup');
    if (!this.acceptTerms) e['acceptTerms'] = translate('validation.acceptTerms');
    if (this.promoCode.trim() && this.promo()?.code !== this.promoCode.trim().toUpperCase()) e['promo'] = translate('validation.applyPromo');
    this.invalid.set(e);
    if (Object.keys(e).length) this.actionError.set(translate('validation.fixFields'));
    return Object.keys(e).length === 0;
  }

  protected async submitDetails() {
    const v = this.b();
    if (!v || !this.validateDetails(v)) return;
    await this.act(() => this.api.submitDetails(this.token(), {
      studentName: this.studentName.trim(), contactPhone: this.contactPhone.trim(),
      guardianName: this.guardianName.trim() || null, guardianPhone: this.guardianPhone.trim() || null,
      acceptTerms: this.acceptTerms, acceptTopUpTerms: this.acceptTopUpTerms, expectedPrice: this.amountDue(),
      promoCode: this.promo()?.code ?? null,
    }));
  }

  protected async copyInvite(v: BookingView) {
    try {
      await navigator.clipboard.writeText(this.inviteUrl(v.invite!.code));
      this.inviteCopied.set(true);
      setTimeout(() => this.inviteCopied.set(false), 2000);
    } catch { /* clipboard unavailable */ }
  }

  protected onFile(event: Event) {
    this.file = (event.target as HTMLInputElement).files?.[0] ?? null;
  }

  protected async uploadProof() {
    if (!this.file || !this.methodId) {
      this.actionError.set(translate('booking.needFile'));
      return;
    }
    await this.act(() => this.api.uploadProof(this.token(), this.methodId, this.file!, this.senderReference));
    this.file = null;
  }

  protected isPaid(v: BookingView) {
    return !v.invitedBy && (v.status === 'PaymentUnderReview' || v.status === 'Confirmed' || v.status === 'TopUpPending');
  }

  protected fullRefundAvailable(v: BookingView) {
    return this.now() <= new Date(v.fullRefundUntilUtc).getTime();
  }

  protected async chooseTopUp() {
    if (!this.topUpChoice) return;
    await this.act(() => this.api.topUpChoice(this.token(), this.topUpChoice));
  }

  protected async uploadTopUp() {
    if (!this.file || !this.methodId) {
      this.actionError.set(translate('booking.needFile'));
      return;
    }
    await this.act(() => this.api.uploadTopUpProof(this.token(), this.methodId, this.file!, ''));
    this.file = null;
  }

  protected async cancelBooking() {
    if (!confirm(translate('booking.confirmCancel'))) return;
    await this.act(() => this.api.cancelBooking(this.token(), this.acceptNoRefund));
  }

  protected async joinSession() {
    this.actionError.set('');
    try {
      const { meetingUrl } = await this.api.join(this.token());
      window.location.href = meetingUrl;
    } catch (e) {
      this.actionError.set(errorMessage(e));
    }
  }

  protected async copyLink() {
    try {
      await navigator.clipboard.writeText(window.location.href);
      this.copied.set(true);
      setTimeout(() => this.copied.set(false), 2000);
    } catch { /* clipboard unavailable */ }
  }

  private async act(fn: () => Promise<unknown>) {
    this.busy.set(true);
    this.actionError.set('');
    try {
      await fn();
    } catch (e) {
      this.actionError.set(errorMessage(e));
    } finally {
      await this.load();
      this.busy.set(false);
    }
  }
}
