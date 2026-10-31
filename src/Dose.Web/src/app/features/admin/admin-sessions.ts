import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import {
  AdminApi, ChapterA, GradeA, LessonA, PricingPreview, ReissueLink, SessionA, SessionBookingA, SessionSave, cairoToUtc, utcToCairo,
} from '../../core/admin-api';
import { errorMessage, fmtDateTime, fmtMoney } from '../../core/format';
import { Option } from './entity-editor';

interface Form {
  serviceTypeId: string; gradeId: string; studyTrackId: string; subjectId: string; chapterId: string | null; lessonId: string | null;
  scopeTitleAr: string; scopeDetailsAr: string; teacherId: string; date: string; time: string; durationMinutes: number;
  mode: 'Private' | 'Group'; targetTierId: string | null; meetingUrl: string;
}

const STATUS_AR: Record<string, string> = {
  Draft: 'مسودة', Published: 'منشورة', TopUpRequired: 'محتاجة حسم الفرق', Confirmed: 'مؤكدة', InProgress: 'شغالة',
  Completed: 'خلصت', Cancelled: 'ملغية',
  PendingHold: 'حجز مؤقت', PendingPayment: 'منتظر الدفع', PaymentUnderReview: 'الدفع تحت المراجعة', TopUpPending: 'منتظر قرار الفرق',
  Expired: 'انتهت المهلة', RefundPending: 'محتاج استرداد', Refunded: 'اترد', Transferred: 'اتنقل', NoShow: 'محضرش',
};

@Component({
  selector: 'dose-admin-sessions',
  imports: [FormsModule],
  templateUrl: './admin-sessions.html',
  styleUrl: './admin-sessions.scss',
})
export class AdminSessions {
  private readonly api = inject(AdminApi);
  protected readonly statusAr = (s: string) => STATUS_AR[s] ?? s;
  protected readonly dt = fmtDateTime;
  protected readonly money = fmtMoney;

  protected readonly sessions = signal<SessionA[]>([]);
  protected readonly filter = signal('');
  protected readonly includePast = signal(false);
  protected readonly error = signal('');
  protected readonly notice = signal('');

  // Lookups
  protected readonly services = signal<Option[]>([]);
  protected readonly grades = signal<GradeA[]>([]);
  protected readonly tracks = signal<Option[]>([]);
  protected readonly subjects = signal<Option[]>([]);
  protected readonly teachers = signal<(Option & { published: boolean })[]>([]);
  protected readonly chapters = signal<ChapterA[]>([]);
  protected readonly lessons = signal<LessonA[]>([]);

  // Editor
  protected readonly editingId = signal<string | null>(null);
  protected readonly form = signal<Form | null>(null);
  protected readonly preview = signal<PricingPreview | null>(null);
  protected readonly previewError = signal('');
  protected readonly formError = signal('');
  protected readonly lockedPricing = signal(false);
  protected readonly busy = signal(false);
  protected readonly gradeSubjects = computed(() => {
    const f = this.form();
    const g = this.grades().find((x) => x.id === f?.gradeId);
    return this.subjects().filter((s) => g?.subjectIds.includes(s.id));
  });

  // Bookings panel
  protected readonly bookingsFor = signal<SessionA | null>(null);
  protected readonly bookings = signal<SessionBookingA[]>([]);
  protected readonly link = signal<(ReissueLink & { url: string; full: string }) | null>(null);
  protected readonly copied = signal(false);

  constructor() {
    void this.load();
    Promise.all([this.api.serviceTypes(), this.api.grades(), this.api.tracks(), this.api.subjects(), this.api.teachers()])
      .then(([s, g, t, su, te]) => {
        this.services.set(s.filter((x) => x.isActive).map((x) => ({ id: x.id, nameAr: x.nameAr })));
        this.grades.set(g.filter((x) => x.isActive));
        this.tracks.set(t.filter((x) => x.isActive).map((x) => ({ id: x.id!, nameAr: x.nameAr })));
        this.subjects.set(su.map((x) => ({ id: x.id!, nameAr: x.nameAr })));
        this.teachers.set(te.map((x) => ({ id: x.id!, nameAr: x.displayNameAr, published: x.isPublished })));
      })
      .catch((e) => this.error.set(errorMessage(e)));
  }

  protected async load() {
    try {
      this.sessions.set(await this.api.sessions(this.filter() || null, this.includePast()));
      this.error.set('');
    } catch (e) { this.error.set(errorMessage(e)); }
  }

  // ---------- Create / edit ----------
  protected newSession() {
    const d = new Date(Date.now() + 3 * 86400000);
    const local = utcToCairo(d.toISOString());
    this.editingId.set(null);
    this.lockedPricing.set(false);
    this.form.set({
      serviceTypeId: this.services()[0]?.id ?? '', gradeId: '', studyTrackId: this.tracks()[0]?.id ?? '', subjectId: '',
      chapterId: null, lessonId: null, scopeTitleAr: '', scopeDetailsAr: '', teacherId: this.teachers()[0]?.id ?? '',
      date: local.date, time: '18:00', durationMinutes: 60, mode: 'Group', targetTierId: null, meetingUrl: '',
    });
    this.preview.set(null);
    this.formError.set('');
  }

  protected async edit(s: SessionA) {
    const local = utcToCairo(s.startUtc);
    this.editingId.set(s.id);
    this.lockedPricing.set(s.activeBookingCount > 0);
    this.form.set({
      serviceTypeId: s.serviceTypeId, gradeId: s.gradeId, studyTrackId: s.studyTrackId, subjectId: s.subjectId,
      chapterId: s.chapterId, lessonId: s.lessonId, scopeTitleAr: s.scopeTitleAr, scopeDetailsAr: s.scopeDetailsAr ?? '',
      teacherId: s.teacherId, date: local.date, time: local.time, durationMinutes: s.durationMinutes, mode: s.mode,
      targetTierId: s.targetTierId, meetingUrl: s.meetingUrl ?? '',
    });
    this.formError.set('');
    await this.loadChapters();
    await this.loadLessons();
    await this.refreshPreview();
    window.scrollTo({ top: 0, behavior: 'smooth' });
  }

  protected async onScopeChange(resetChapter = false) {
    const f = this.form()!;
    if (resetChapter) { f.chapterId = null; f.lessonId = null; this.lessons.set([]); }
    if (!this.gradeSubjects().some((s) => s.id === f.subjectId)) f.subjectId = '';
    this.form.set({ ...f });
    await this.loadChapters();
    await this.refreshPreview();
  }

  protected async loadChapters() {
    const f = this.form();
    this.chapters.set(f?.gradeId && f.subjectId
      ? (await this.api.chapters(f.gradeId, f.subjectId).catch(() => [])).filter((c) => c.isActive && (!c.studyTrackId || c.studyTrackId === f.studyTrackId))
      : []);
  }

  protected async loadLessons() {
    const f = this.form();
    this.lessons.set(f?.chapterId ? (await this.api.lessons(f.chapterId).catch(() => [])).filter((l) => l.isActive) : []);
  }

  protected async onChapter() {
    this.form()!.lessonId = null;
    await this.loadLessons();
  }

  protected async refreshPreview() {
    const f = this.form();
    this.preview.set(null);
    this.previewError.set('');
    if (!f?.serviceTypeId || !f.gradeId || !f.studyTrackId || !f.subjectId) return;
    try {
      const p = await this.api.preview({ serviceTypeId: f.serviceTypeId, gradeId: f.gradeId, studyTrackId: f.studyTrackId, subjectId: f.subjectId, mode: f.mode });
      this.preview.set(p);
      if (f.mode === 'Group' && !p.tiers.some((t) => t.id === f.targetTierId)) f.targetTierId = p.tiers[0]?.id ?? null;
    } catch (e) {
      this.previewError.set(errorMessage(e));
    }
  }

  protected async save(publish: boolean) {
    const f = this.form()!;
    const body: SessionSave = {
      serviceTypeId: f.serviceTypeId, gradeId: f.gradeId, studyTrackId: f.studyTrackId, subjectId: f.subjectId,
      chapterId: f.chapterId, lessonId: f.lessonId, scopeTitleAr: f.scopeTitleAr, scopeDetailsAr: f.scopeDetailsAr || null,
      teacherId: f.teacherId, startUtc: cairoToUtc(f.date, f.time), durationMinutes: Number(f.durationMinutes),
      mode: f.mode, targetTierId: f.mode === 'Group' ? f.targetTierId : null, meetingUrl: f.meetingUrl || null,
    };
    this.busy.set(true);
    this.formError.set('');
    try {
      let id = this.editingId();
      if (id) await this.api.updateSession(id, body);
      else id = (await this.api.createSession(body)).id;
      if (publish) await this.api.publish(id);
      this.notice.set(publish ? 'الجلسة اتحفظت واتنشرت للطلاب ✓' : 'الجلسة اتحفظت ✓');
      this.form.set(null);
      await this.load();
    } catch (e) {
      this.formError.set(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }

  // ---------- Actions ----------
  protected async publish(s: SessionA) {
    try { await this.api.publish(s.id); this.notice.set('اتنشرت ✓'); await this.load(); } catch (e) { this.error.set(errorMessage(e)); }
  }

  protected async cancel(s: SessionA) {
    const reason = prompt(`سبب إلغاء "${s.scopeTitleAr}"؟ (الحجوزات المدفوعة هتتحول لـ "محتاج استرداد")`);
    if (!reason) return;
    try {
      const r = await this.api.cancel(s.id, reason);
      this.notice.set(r.refundsPending ? `الجلسة اتلغت. فيه ${r.refundsPending} حجز محتاج استرداد — كلّمهم على واتساب.` : 'الجلسة اتلغت.');
      await this.load();
    } catch (e) { this.error.set(errorMessage(e)); }
  }

  protected async showBookings(s: SessionA) {
    this.bookingsFor.set(s);
    this.link.set(null);
    try { this.bookings.set(await this.api.sessionBookings(s.id)); } catch (e) { this.error.set(errorMessage(e)); }
  }

  protected async sendLink(b: SessionBookingA) {
    if (!confirm(`هيتعمل لينك جديد لحجز ${b.reference} واللينك القديم هيبطل. تكمّل؟`)) return;
    try {
      const r = await this.api.reissueLink(b.id);
      const url = `${window.location.origin}/b/${r.accessToken}`;
      this.link.set({ ...r, url, full: `${r.message}\n${url}` });
    } catch (e) { this.error.set(errorMessage(e)); }
  }

  protected chatUrl(phone: string | null) { return phone ? `https://wa.me/${phone.replace(/\D/g, '')}` : null; }

  protected async copy(text: string) {
    try { await navigator.clipboard.writeText(text); this.copied.set(true); setTimeout(() => this.copied.set(false), 2000); } catch { /* ignore */ }
  }
}
