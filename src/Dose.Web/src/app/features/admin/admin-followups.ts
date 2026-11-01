import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { AdminApi, FollowUp, ReissueLink, SessionRequestA } from '../../core/admin-api';
import { errorMessage, fmtDateTime, fmtMoney } from '../../core/format';

const CHOICE_AR: Record<string, string> = { PayDifference: 'هيدفع الفرق', Transfer: 'عايز ينقل', Refund: 'عايز يسترد' };

/** Students who owe a top-up, refunds to send, and transfers to arrange (spec 6.4, decision D8). */
@Component({
  selector: 'dose-admin-followups',
  imports: [FormsModule],
  template: `
    <section class="page">
      <h1 class="page-title">المتابعات</h1>
      <p class="muted small">
        الجروبات اللي مكملتش بتظهر هنا بعد ميعاد الحسم: كل طالب بيختار من صفحة حجزه. الفلوس بتتحوّل برّه المنصة
        (إنستاباي / فودافون كاش)، وإنت بتسجّل اللي حصل هنا. إيصالات الفرق بتظهر في صفحة المدفوعات.
      </p>
      @if (error()) { <p class="alert alert--error" role="alert">{{ error() }}</p> }

      @for (g of groups(); track g.kind) {
        <h2 class="sub">{{ g.title }} <span class="badge badge--gray">{{ g.items.length }}</span></h2>
        @for (f of g.items; track f.bookingId) {
          <article class="dose-card item" [class.overdue]="f.overdue">
            <div class="row">
              <strong dir="ltr">{{ f.bookingReference }}</strong>
              @if (f.overdue) { <span class="badge badge--yellow">عدّى ميعاد الرد</span> }
              @if (f.studentChoice) { <span class="badge">{{ choiceAr(f.studentChoice) }}</span> }
              @if (f.topUpProofStatus === 'Submitted') { <span class="badge badge--green">إيصال الفرق تحت المراجعة</span> }
            </div>
            <ul class="info-list small">
              <li><span>الطالب</span><span>{{ f.studentName }} · <span dir="ltr">{{ f.contactPhone }}</span></span></li>
              @if (f.guardianPhone) { <li><span>ولي الأمر</span><span dir="ltr">{{ f.guardianPhone }}</span></li> }
              <li><span>الجلسة</span><span>{{ f.sessionTitleAr }} — {{ dt(f.sessionStartUtc) }}</span></li>
              <li><span>دفع</span><span>{{ money(f.paidAmount, f.currency) }}</span></li>
              @if (f.topUpAmount) { <li><span>الفرق المطلوب</span><span>{{ money(f.topUpAmount, f.currency) }}</span></li> }
              @if (f.respondByUtc) { <li><span>آخر ميعاد للرد</span><span>{{ dt(f.respondByUtc) }}</span></li> }
              @if (f.reason) { <li><span>السبب</span><span>{{ f.reason }}</span></li> }
            </ul>
            <input class="note" [(ngModel)]="notes[f.bookingId]" placeholder="ملاحظة (اختياري)" aria-label="ملاحظة" />
            <div class="actions">
              <button type="button" class="dose-btn dose-btn--ghost small-btn" (click)="message(f)">رسالة واتساب للطالب</button>
              @switch (f.kind) {
                @case ('TopUp') { <button type="button" class="dose-btn dose-btn--primary small-btn" (click)="act(f, 'waive')">تنازل عن الفرق</button> }
                @case ('Refund') { <button type="button" class="dose-btn dose-btn--primary small-btn" (click)="act(f, 'refunded')">تم تحويل الاسترداد</button> }
                @case ('Transfer') { <button type="button" class="dose-btn dose-btn--primary small-btn" (click)="act(f, 'transfer')">تم النقل</button> }
              }
            </div>
            @if (msg()?.bookingId === f.bookingId) {
              <div class="link-box">
                <textarea readonly [value]="msg()!.text" aria-label="الرسالة"></textarea>
                <div class="actions">
                  <button type="button" class="dose-btn dose-btn--primary small-btn" (click)="copy(msg()!.text)">{{ copied() ? 'اتنسخت ✓' : 'انسخ' }}</button>
                  @if (f.contactPhone) {
                    <a class="dose-btn dose-btn--whatsapp small-btn" [href]="'https://wa.me/' + digits(f.contactPhone)" target="_blank" rel="noopener">افتح الشات</a>
                  }
                </div>
              </div>
            }
          </article>
        } @empty { <p class="muted small">مفيش.</p> }
      }
    </section>
  `,
  styles: `
    .sub { margin: 10px 0 0; font-size: 1.12rem; color: var(--dose-navy); display: flex; gap: 8px; align-items: center; }
    .row { display: flex; gap: 8px; align-items: center; flex-wrap: wrap; }
    .item { padding: 14px 16px; display: grid; gap: 10px; }
    .item.overdue { border-color: #f0b400; }
    .note { min-height: 40px; padding: 0 10px; border: 1px solid var(--dose-border); border-radius: 8px; font: inherit; }
    .actions { display: flex; flex-wrap: wrap; gap: 8px; }
    .small-btn { min-height: 38px; padding: 0 12px; font-size: 0.88rem; }
    .link-box { display: grid; gap: 8px; padding: 12px; border-radius: var(--dose-radius-sm); background: var(--dose-success-50); }
    .link-box textarea { min-height: 120px; padding: 8px; border: 1px solid var(--dose-border); border-radius: 8px; font: 0.9rem var(--dose-font); }
  `,
})
export class AdminFollowUps {
  private readonly api = inject(AdminApi);
  protected readonly items = signal<FollowUp[]>([]);
  protected readonly error = signal('');
  protected readonly msg = signal<{ bookingId: string; text: string } | null>(null);
  protected readonly copied = signal(false);
  protected notes: Record<string, string> = {};
  protected readonly dt = fmtDateTime;
  protected readonly money = fmtMoney;
  protected readonly choiceAr = (c: string) => CHOICE_AR[c] ?? c;
  protected readonly digits = (p: string) => p.replace(/\D/g, '');
  protected readonly groups = computed(() => [
    { kind: 'TopUp', title: 'جروبات مكملتش — مستني رد أو فرق', items: this.items().filter((i) => i.kind === 'TopUp') },
    { kind: 'Refund', title: 'استرداد مطلوب', items: this.items().filter((i) => i.kind === 'Refund') },
    { kind: 'Transfer', title: 'نقل لميعاد تاني', items: this.items().filter((i) => i.kind === 'Transfer') },
  ]);

  constructor() { void this.load(); }

  private async load() {
    try { this.items.set(await this.api.followUps()); this.error.set(''); } catch (e) { this.error.set(errorMessage(e)); }
  }

  protected async act(f: FollowUp, action: 'waive' | 'refunded' | 'transfer') {
    const note = this.notes[f.bookingId] ?? '';
    const q = { waive: 'تتنازل عن الفرق وتأكد حجز الطالب؟', refunded: 'اتأكدت إن الفلوس اتحوّلت للطالب؟', transfer: 'الطالب اتنقل لجلسة تانية؟' }[action];
    if (!confirm(q)) return;
    try {
      if (action === 'waive') await this.api.waive(f.bookingId, note);
      if (action === 'refunded') await this.api.markRefunded(f.bookingId, note);
      if (action === 'transfer') await this.api.markTransferDone(f.bookingId, note);
      await this.load();
    } catch (e) { this.error.set(errorMessage(e)); }
  }

  /** Builds the WhatsApp message; top-up messages include a fresh secret link so the student can choose. */
  protected async message(f: FollowUp) {
    try {
      let text: string;
      if (f.kind === 'TopUp') {
        if (!confirm('هيتعمل لينك جديد لصفحة حجز الطالب (القديم هيبطل). تكمّل؟')) return;
        const link: ReissueLink = await this.api.reissueLink(f.bookingId);
        const url = `${window.location.origin}/b/${link.accessToken}`;
        text = `أهلاً ${f.studentName ?? ''}، جروب "${f.sessionTitleAr}" مكملش العدد المطلوب.\n` +
          `علشان الجلسة تتم في ميعادها المطلوب فرق ${this.money(f.topUpAmount ?? 0, f.currency)}، أو تقدر تنقل لميعاد تاني أو تسترد فلوسك كاملة.\n` +
          `اختار من صفحة حجزك:\n${url}`;
      } else if (f.kind === 'Refund') {
        text = `أهلاً ${f.studentName ?? ''}، بخصوص حجز ${f.bookingReference}: هنرجّعلك ${this.money(f.paidAmount, f.currency)}. ` +
          `ابعتلنا الرقم اللي تحب نحوّل عليه لو مختلف عن اللي حوّلت منه.`;
      } else {
        text = `أهلاً ${f.studentName ?? ''}، بخصوص حجز ${f.bookingReference}: إيه المواعيد اللي تناسبك علشان ننقلك لجلسة تانية لنفس الجزء؟`;
      }
      this.msg.set({ bookingId: f.bookingId, text });
    } catch (e) { this.error.set(errorMessage(e)); }
  }

  protected async copy(text: string) {
    try { await navigator.clipboard.writeText(text); this.copied.set(true); setTimeout(() => this.copied.set(false), 2000); } catch { /* ignore */ }
  }
}

const REQ_STATUS_AR: Record<string, string> = { New: 'جديد', Contacted: 'اتكلمنا معاه', Fulfilled: 'اتعملت جلسة', Closed: 'مقفول' };

/** Student session requests (spec 7). Admin contacts the student and creates a session from the request. */
@Component({
  selector: 'dose-admin-requests',
  imports: [FormsModule],
  template: `
    <section class="page">
      <div class="row">
        <h1 class="page-title">طلبات الجلسات</h1>
        <select [ngModel]="status()" (ngModelChange)="status.set($event); load()" aria-label="الحالة">
          <option value="New">جديدة</option><option value="Contacted">اتكلمنا معاهم</option>
          <option value="Fulfilled">اتعملت جلسة</option><option value="Closed">مقفولة</option><option value="">الكل</option>
        </select>
      </div>
      <p class="muted small">الطلبات المتشابهة (نفس الصف والمادة والجزء) ممكن تتجمع في جلسة جماعية واحدة.</p>
      @if (error()) { <p class="alert alert--error" role="alert">{{ error() }}</p> }

      @for (r of items(); track r.id) {
        <article class="dose-card item">
          <div class="row">
            <strong>{{ r.subjectNameAr }} — {{ r.gradeNameAr }}</strong>
            <span class="badge" [class.badge--yellow]="r.status === 'New'" [class.badge--green]="r.status === 'Fulfilled'">{{ statusAr(r.status) }}</span>
          </div>
          <p>{{ r.scopeText }}</p>
          <ul class="info-list small">
            <li><span>الرقم</span><span dir="ltr">{{ r.reference }}</span></li>
            <li><span>النوع</span><span>{{ r.serviceTypeNameAr }} · {{ r.studyTrackNameAr }} · {{ r.mode === 'Group' ? 'جماعي' + (r.preferredTierNameAr ? ' (' + r.preferredTierNameAr + ')' : '') : 'فردي' }}</span></li>
            <li><span>المواعيد</span><span>{{ r.preferredDays }} — {{ r.preferredTimes }}</span></li>
            <li><span>الطالب</span><span>{{ r.studentName }} · <span dir="ltr">{{ r.contactPhone }}</span></span></li>
            @if (r.guardianPhone) { <li><span>ولي الأمر</span><span>{{ r.guardianName }} · <span dir="ltr">{{ r.guardianPhone }}</span></span></li> }
            @if (r.notes) { <li><span>ملاحظات</span><span>{{ r.notes }}</span></li> }
            <li><span>اتبعت</span><span>{{ dt(r.createdAtUtc) }}</span></li>
          </ul>
          <input class="note" [(ngModel)]="r.adminNotes" placeholder="ملاحظاتك (داخلية)" aria-label="ملاحظات داخلية" />
          <div class="actions">
            <a class="dose-btn dose-btn--whatsapp small-btn" [href]="'https://wa.me/' + digits(r.contactPhone)" target="_blank" rel="noopener">كلّم الطالب</a>
            <button type="button" class="dose-btn dose-btn--primary small-btn" (click)="createSession(r)">اعمل جلسة من الطلب</button>
            @if (r.status === 'New') { <button type="button" class="dose-btn dose-btn--ghost small-btn" (click)="set(r, 'Contacted')">اتكلمنا معاه</button> }
            @if (r.status !== 'Closed') { <button type="button" class="dose-btn small-btn" (click)="set(r, 'Closed')">اقفل الطلب</button> }
          </div>
        </article>
      } @empty { <p class="muted">مفيش طلبات.</p> }
    </section>
  `,
  styles: `
    .row { display: flex; justify-content: space-between; align-items: center; gap: 8px; flex-wrap: wrap; }
    select, .note { min-height: 40px; padding: 0 10px; border: 1px solid var(--dose-border); border-radius: 8px; font: inherit; }
    .item { padding: 14px 16px; display: grid; gap: 10px; }
    .item p { margin: 0; }
    .actions { display: flex; flex-wrap: wrap; gap: 8px; }
    .small-btn { min-height: 38px; padding: 0 12px; font-size: 0.88rem; }
  `,
})
export class AdminRequests {
  private readonly api = inject(AdminApi);
  private readonly router = inject(Router);
  protected readonly items = signal<SessionRequestA[]>([]);
  protected readonly status = signal('New');
  protected readonly error = signal('');
  protected readonly dt = fmtDateTime;
  protected readonly statusAr = (s: string) => REQ_STATUS_AR[s] ?? s;
  protected readonly digits = (p: string) => p.replace(/\D/g, '');

  constructor() { void this.load(); }

  protected async load() {
    try { this.items.set(await this.api.requests(this.status() || null)); this.error.set(''); } catch (e) { this.error.set(errorMessage(e)); }
  }

  protected async set(r: SessionRequestA, status: string) {
    try { await this.api.updateRequest(r.id, status, r.adminNotes); await this.load(); } catch (e) { this.error.set(errorMessage(e)); }
  }

  protected createSession(r: SessionRequestA) {
    void this.router.navigate(['/admin/sessions'], { state: { fromRequest: r } });
  }
}
