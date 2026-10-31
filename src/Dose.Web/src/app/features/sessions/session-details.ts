import { Component, effect, inject, input, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { Api, SessionDetails } from '../../core/api';
import { errorMessage, fmtDate, fmtDateTime, fmtDuration, fmtMoney, fmtTime, myBookings } from '../../core/format';
import { GroupTermsBox } from '../../shared/group-terms';
import { TeacherCard } from '../../shared/teacher-card';

/** Session details (spec 9.4): teacher identity and all terms are visible before the student commits. */
@Component({
  selector: 'dose-session-details',
  imports: [RouterLink, TeacherCard, GroupTermsBox],
  template: `
    <section class="page">
      <a routerLink="/sessions" class="back">→ رجوع للجلسات</a>
      @if (error()) {
        <p class="alert alert--error">{{ error() }}</p>
      } @else if (!d()) {
        <div class="skeleton"></div>
      } @else {
        @let s = d()!.summary;
        <div>
          @if (s.mode === 'Group') { <span class="badge badge--green">جلسة جماعية مفتوحة</span> }
          @else { <span class="badge">جلسة فردية</span> }
          <h1 class="page-title">{{ s.subjectNameAr }} — {{ s.scopeTitleAr }}</h1>
          <p class="muted">{{ s.gradeNameAr }} · {{ s.studyTrackNameAr }} · {{ s.serviceTypeNameAr }}</p>
        </div>

        <dose-teacher-card [teacher]="d()!.teacher" />

        <div class="dose-card box">
          <h2>معلومات الجلسة</h2>
          <ul class="info-list">
            <li><span>الميعاد</span><span>{{ date(s.startUtc) }} — {{ time(s.startUtc) }}</span></li>
            <li><span>المدة</span><span>{{ duration(s.durationMinutes) }}</span></li>
            @if (s.mode === 'Group') {
              <li><span>المحجوز</span><span>{{ s.confirmedCount }} من {{ s.capacity }} طلاب</span></li>
              <li><span>الأماكن الفاضلة</span><span>{{ s.remainingSeats }}</span></li>
            }
            <li><span>آخر ميعاد للحجز</span><span>{{ dateTime(s.bookingDeadlineUtc) }}</span></li>
            <li><span>السعر للطالب</span><span class="price">{{ money(s.pricePerStudent, s.currency) }}</span></li>
          </ul>
          @if (d()!.scopeDetailsAr) { <p class="muted small">{{ d()!.scopeDetailsAr }}</p> }
        </div>

        @if (d()!.groupTerms; as g) {
          <dose-group-terms [terms]="g" [price]="s.pricePerStudent" [currency]="s.currency" />
        }

        <div class="dose-card box">
          <h2>الدفع والإلغاء</h2>
          <ul class="bullets">
            <li>الدفع بإنستاباي أو فودافون كاش، وبعدين ترفع صورة التحويل.</li>
            <li>الحجز بيتأكد بعد ما نراجع الدفع.</li>
            <li>لو لغيت قبل الجلسة بـ 24 ساعة أو أكتر بترجعلك فلوسك كاملة. ولو إحنا لغينا الجلسة بترجع كاملة دايماً.</li>
            <li>رابط الجلسة بيوصلك على صفحة الحجز الخاصة بيك قبل الميعاد.</li>
          </ul>
          @if (d()!.requiresGuardianContact) {
            <p class="alert alert--info small">للمرحلة دي هنحتاج اسم ورقم واتساب ولي الأمر.</p>
          }
        </div>

        @if (bookError()) { <p class="alert alert--error">{{ bookError() }}</p> }
        @if (d()!.isOpenForBooking) {
          <button class="dose-btn dose-btn--primary btn-block" (click)="book()" [disabled]="booking()">
            {{ booking() ? 'جاري حجز مكانك…' : 'احجز مكانك' }}
          </button>
        } @else {
          <p class="alert">الحجز في الجلسة دي مقفول. <a routerLink="/request-session">اطلب جلسة جديدة</a></p>
        }
      }
    </section>
  `,
  styles: `
    .back { text-decoration: none; font-weight: 600; }
    h1 { margin-top: 8px; }
    p { margin: 4px 0 0; }
    .box { padding: 16px; display: grid; gap: 12px; }
    .box h2 { margin: 0; font-size: 1.08rem; color: var(--dose-navy); }
    .bullets { margin: 0; padding-inline-start: 20px; display: grid; gap: 6px; font-size: 0.94rem; }
  `,
})
export class SessionDetailsPage {
  private readonly api = inject(Api);
  private readonly router = inject(Router);
  readonly id = input.required<string>();

  protected readonly d = signal<SessionDetails | null>(null);
  protected readonly error = signal('');
  protected readonly booking = signal(false);
  protected readonly bookError = signal('');
  protected readonly date = fmtDate;
  protected readonly time = fmtTime;
  protected readonly dateTime = fmtDateTime;
  protected readonly duration = fmtDuration;
  protected readonly money = fmtMoney;

  constructor() {
    effect(() => {
      const id = this.id();
      this.api.session(id).then((d) => this.d.set(d)).catch((e) => this.error.set(errorMessage(e)));
    });
  }

  protected async book() {
    this.booking.set(true);
    this.bookError.set('');
    try {
      const hold = await this.api.hold(this.id());
      myBookings.add(hold.reference, hold.accessToken);
      await this.router.navigate(['/b', hold.accessToken]);
    } catch (e) {
      this.bookError.set(errorMessage(e));
    } finally {
      this.booking.set(false);
    }
  }
}
