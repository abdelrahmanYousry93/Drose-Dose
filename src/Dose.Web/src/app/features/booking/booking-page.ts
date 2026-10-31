import { Component, DestroyRef, computed, effect, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Api, BookingView, PaymentMethod } from '../../core/api';
import { errorMessage, fmtDate, fmtDateTime, fmtDuration, fmtMoney, fmtTime } from '../../core/format';
import { PublicSettingsService } from '../../core/public-settings.service';
import { GroupTermsBox } from '../../shared/group-terms';
import { TeacherCard } from '../../shared/teacher-card';

/**
 * The student's private booking page, opened from the secret link /b/:token.
 * One page drives the whole journey by status: details → payment → review → confirmed → join.
 * The meeting URL is never sent in messages; "Join" asks the API for it inside the join window.
 */
@Component({
  selector: 'dose-booking-page',
  imports: [FormsModule, RouterLink, TeacherCard, GroupTermsBox],
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

  protected readonly fmt = { date: fmtDate, time: fmtTime, dateTime: fmtDateTime, duration: fmtDuration, money: fmtMoney };

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
    if (s === 'Confirmed' || s === 'Completed') return 4;
    return 0;
  });

  protected readonly whatsAppLink = computed(() => {
    const v = this.b();
    return v ? this.settings.whatsAppLink(v.whatsAppMessage) : null;
  });

  constructor() {
    const timer = setInterval(() => this.now.set(Date.now()), 1000);
    inject(DestroyRef).onDestroy(() => clearInterval(timer));
    effect(() => { void this.load(this.token()); });
    this.api.paymentMethods().then((m) => {
      this.methods.set(m);
      if (m.length) this.methodId = m[0].id;
    }).catch(() => undefined);
  }

  protected async load(token = this.token()) {
    try {
      this.b.set(await this.api.booking(token));
      this.error.set('');
    } catch (e) {
      this.error.set(errorMessage(e));
    }
  }

  protected async submitDetails() {
    const v = this.b();
    if (!v) return;
    await this.act(() => this.api.submitDetails(this.token(), {
      studentName: this.studentName, contactPhone: this.contactPhone,
      guardianName: this.guardianName || null, guardianPhone: this.guardianPhone || null,
      acceptTerms: this.acceptTerms, acceptTopUpTerms: this.acceptTopUpTerms, expectedPrice: v.price,
    }));
  }

  protected onFile(event: Event) {
    this.file = (event.target as HTMLInputElement).files?.[0] ?? null;
  }

  protected async uploadProof() {
    if (!this.file || !this.methodId) {
      this.actionError.set('اختار طريقة الدفع وارفع صورة التحويل.');
      return;
    }
    await this.act(() => this.api.uploadProof(this.token(), this.methodId, this.file!, this.senderReference));
    this.file = null;
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
