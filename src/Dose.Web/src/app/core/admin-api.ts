import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../environments/environment';
import { ApiError } from './api';

// ---------- Models (mirror Dose.Contracts.Admin) ----------
export interface StageA { id: string | null; nameAr: string; requiresGuardianContact: boolean; sortOrder: number; isActive: boolean }
export interface LookupA { id: string | null; nameAr: string; sortOrder: number; isActive: boolean }
export interface BandA { id: string | null; nameAr: string; sortOrder: number }
export interface GradeA {
  id: string | null; nameAr: string; stageId: string; pricingBandId: string; curriculumSystem: string | null;
  sortOrder: number; isActive: boolean; subjectIds: string[];
}
export interface ChapterA { id: string | null; nameAr: string; subjectId: string; gradeId: string; studyTrackId: string | null; sortOrder: number; isActive: boolean }
export interface LessonA { id: string | null; nameAr: string; chapterId: string; sortOrder: number; isActive: boolean }
export interface ServiceTypeA { id: string; code: string; nameAr: string; descriptionAr: string | null; sortOrder: number; isActive: boolean }
export interface TeacherA {
  id: string | null; displayNameAr: string; photoUrl: string | null; bioAr: string | null; qualificationsAr: string | null;
  yearsOfExperience: number | null; isPublished: boolean;
}
export interface PaymentMethodA {
  id: string | null; code: string; nameAr: string; receivingAccount: string; instructionsAr: string | null; sortOrder: number; isActive: boolean;
}
export interface TierA { id: string | null; nameAr: string; minParticipants: number; maxParticipants: number; pricePerStudent: number; sortOrder: number }
export interface VersionA { id: string; versionNo: number; privatePrice: number | null; isLocked: boolean; tiers: TierA[] }
export interface PlanA {
  id: string | null; name: string; serviceTypeId: string; mode: 'Private' | 'Group'; pricingBandId: string | null;
  studyTrackId: string | null; subjectId: string | null; currency: string; policy: string; effectiveFromUtc: string;
  effectiveToUtc: string | null; isActive: boolean; currentVersion: VersionA | null;
}
export interface PricingPreview {
  planId: string; planName: string; versionId: string; versionNo: number; currency: string; privatePrice: number | null; tiers: TierA[];
}
export interface SessionSave {
  serviceTypeId: string; gradeId: string; studyTrackId: string; subjectId: string; chapterId: string | null; lessonId: string | null;
  scopeTitleAr: string; scopeDetailsAr: string | null; teacherId: string; startUtc: string; durationMinutes: number;
  mode: 'Private' | 'Group'; targetTierId: string | null; meetingUrl: string | null;
}
export interface SessionA {
  id: string; status: string; scopeTitleAr: string; scopeDetailsAr: string | null;
  serviceTypeId: string; serviceTypeNameAr: string; gradeId: string; gradeNameAr: string; studyTrackId: string; studyTrackNameAr: string;
  subjectId: string; subjectNameAr: string; chapterId: string | null; lessonId: string | null; teacherId: string; teacherNameAr: string;
  startUtc: string; endUtc: string; durationMinutes: number; mode: 'Private' | 'Group'; capacity: number; minParticipants: number;
  targetTierId: string | null; targetTierNameAr: string | null; pricePerStudent: number; currency: string; planName: string;
  bookingDeadlineUtc: string; tierDecisionAtUtc: string | null; meetingUrl: string | null;
  confirmedCount: number; occupiedCount: number; activeBookingCount: number;
}
export interface SessionBookingA {
  id: string; reference: string; status: string; studentName: string | null; contactPhone: string | null;
  guardianName: string | null; guardianPhone: string | null; price: number; currency: string; createdAtUtc: string; confirmedAtUtc: string | null;
}
export interface FollowUp {
  bookingId: string; kind: 'TopUp' | 'Refund' | 'Transfer'; bookingReference: string; bookingStatus: string;
  studentName: string | null; contactPhone: string | null; guardianPhone: string | null; sessionId: string; sessionTitleAr: string;
  sessionStartUtc: string; paidAmount: number; currency: string; topUpAmount: number | null; respondByUtc: string | null;
  studentChoice: string | null; topUpProofStatus: string | null; reason: string | null; overdue: boolean;
}
export interface SessionRequestA {
  id: string; reference: string; status: string; studentName: string; contactPhone: string; guardianName: string | null;
  guardianPhone: string | null; gradeId: string; gradeNameAr: string; studyTrackId: string; studyTrackNameAr: string;
  subjectId: string; subjectNameAr: string; serviceTypeId: string; serviceTypeNameAr: string; scopeText: string;
  preferredDays: string; preferredTimes: string; mode: 'Private' | 'Group'; preferredTierNameAr: string | null;
  notes: string | null; adminNotes: string | null; fulfilledBySessionId: string | null; createdAtUtc: string;
}
export interface DashSession {
  id: string; titleAr: string; subjectNameAr: string; teacherNameAr: string; startUtc: string; mode: string; status: string;
  confirmedCount: number; capacity: number; minParticipants: number; hasMeetingUrl: boolean; tierDecisionAtUtc: string | null;
}
export interface Dashboard {
  pendingProofs: number; oldestPendingProofMinutes: number | null; openTopUps: number; overdueTopUps: number; pendingRefunds: number;
  pendingTransfers: number; newRequests: number; pendingAvailability: number; upcomingSessions: DashSession[];
  groupsNearDecision: DashSession[]; periodDays: number; periodConfirmedBookings: number; periodCollected: number;
  periodRefunded: number; currency: string;
}
export interface UserA { id: string; email: string; roles: string[]; isDisabled: boolean; teacherId: string | null; teacherNameAr: string | null }
export interface AvailabilityA {
  id: string; teacherId: string; teacherNameAr: string; startUtc: string; endUtc: string; note: string | null; status: string;
  reviewNote: string | null; createdAtUtc: string;
}
export interface ReissueLink { reference: string; accessToken: string; contactPhone: string | null; message: string }

@Injectable({ providedIn: 'root' })
export class AdminApi {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/admin`;

  private async run<T>(p: Promise<T>): Promise<T> {
    try { return await p; } catch (e) {
      if (e instanceof HttpErrorResponse) {
        const b = e.error as { code?: string; title?: string } | null;
        throw new ApiError(b?.code ?? `http.${e.status}`, b?.title ?? e.message, e.status);
      }
      throw e;
    }
  }
  get<T>(url: string, params: Record<string, string | null | undefined> = {}) {
    let p = new HttpParams();
    for (const [k, v] of Object.entries(params)) if (v) p = p.set(k, v);
    return this.run(firstValueFrom(this.http.get<T>(this.base + url, { params: p })));
  }
  post<T>(url: string, body: unknown) { return this.run(firstValueFrom(this.http.post<T>(this.base + url, body))); }
  put<T>(url: string, body: unknown) { return this.run(firstValueFrom(this.http.put<T>(this.base + url, body))); }

  // Catalog
  stages() { return this.get<StageA[]>('/catalog/stages'); }
  saveStage(d: StageA) { return this.post<{ id: string }>('/catalog/stages', d); }
  tracks() { return this.get<LookupA[]>('/catalog/study-tracks'); }
  saveTrack(d: LookupA) { return this.post<{ id: string }>('/catalog/study-tracks', d); }
  bands() { return this.get<BandA[]>('/catalog/pricing-bands'); }
  saveBand(d: BandA) { return this.post<{ id: string }>('/catalog/pricing-bands', d); }
  subjects() { return this.get<LookupA[]>('/catalog/subjects'); }
  saveSubject(d: LookupA) { return this.post<{ id: string }>('/catalog/subjects', d); }
  grades() { return this.get<GradeA[]>('/catalog/grades'); }
  saveGrade(d: GradeA) { return this.post<{ id: string }>('/catalog/grades', d); }
  chapters(gradeId?: string | null, subjectId?: string | null) { return this.get<ChapterA[]>('/catalog/chapters', { gradeId, subjectId }); }
  saveChapter(d: ChapterA) { return this.post<{ id: string }>('/catalog/chapters', d); }
  lessons(chapterId: string) { return this.get<LessonA[]>('/catalog/lessons', { chapterId }); }
  saveLesson(d: LessonA) { return this.post<{ id: string }>('/catalog/lessons', d); }
  serviceTypes() { return this.get<ServiceTypeA[]>('/catalog/service-types'); }
  saveServiceType(d: ServiceTypeA) { return this.post<{ id: string }>('/catalog/service-types', d); }

  // Teachers & payment methods
  teachers() { return this.get<TeacherA[]>('/teachers'); }
  saveTeacher(d: TeacherA) { return this.post<{ id: string }>('/teachers', d); }
  paymentMethods() { return this.get<PaymentMethodA[]>('/payment-methods'); }
  savePaymentMethod(d: PaymentMethodA) { return this.post<{ id: string }>('/payment-methods', d); }

  // Pricing
  plans() { return this.get<PlanA[]>('/pricing/plans'); }
  savePlan(d: PlanA) { return this.post<{ id: string }>('/pricing/plans', d); }
  savePrices(planId: string, privatePrice: number | null, tiers: TierA[]) {
    return this.post<PlanA>(`/pricing/plans/${planId}/prices`, { privatePrice, tiers });
  }

  // Sessions
  sessions(status: string | null, includePast: boolean) {
    return this.get<SessionA[]>('/sessions', { status, includePast: includePast ? 'true' : null });
  }
  preview(d: { serviceTypeId: string; gradeId: string; studyTrackId: string; subjectId: string; mode: string }) {
    return this.post<PricingPreview>('/sessions/pricing-preview', d);
  }
  createSession(d: SessionSave) { return this.post<{ id: string }>('/sessions', d); }
  updateSession(id: string, d: SessionSave) { return this.put<void>(`/sessions/${id}`, d); }
  publish(id: string) { return this.post<void>(`/sessions/${id}/publish`, {}); }
  cancel(id: string, reason: string) { return this.post<{ refundsPending: number }>(`/sessions/${id}/cancel`, { reason }); }
  sessionBookings(id: string) { return this.get<SessionBookingA[]>(`/sessions/${id}/bookings`); }
  reissueLink(bookingId: string) { return this.post<ReissueLink>(`/bookings/${bookingId}/reissue-link`, {}); }
  decide(sessionId: string) { return this.post<void>(`/sessions/${sessionId}/decide`, {}); }

  // Dashboard, users, availability
  dashboard(days = 30) { return this.get<Dashboard>('/dashboard', { days: String(days) }); }
  users() { return this.get<UserA[]>('/users'); }
  createUser(email: string, password: string, role: string, teacherId: string | null) {
    return this.post<{ id: string }>('/users', { email, password, role, teacherId });
  }
  resetPassword(id: string, password: string) { return this.post<void>(`/users/${id}/password`, { password }); }
  setDisabled(id: string, disabled: boolean) { return this.post<void>(`/users/${id}/disabled`, { disabled }); }
  linkTeacher(id: string, teacherId: string | null) { return this.post<void>(`/users/${id}/teacher`, { teacherId }); }
  availability(status: string | null) { return this.get<AvailabilityA[]>('/availability', { status }); }
  reviewAvailability(id: string, approve: boolean, note: string | null) { return this.post<void>(`/availability/${id}/review`, { approve, note }); }

  // Follow-ups & requests
  followUps() { return this.get<FollowUp[]>('/follow-ups'); }
  waive(bookingId: string, note: string) { return this.post<void>(`/follow-ups/${bookingId}/waive`, { note }); }
  markRefunded(bookingId: string, note: string) { return this.post<void>(`/follow-ups/${bookingId}/refunded`, { note }); }
  markTransferDone(bookingId: string, note: string) { return this.post<void>(`/follow-ups/${bookingId}/transfer-done`, { note }); }
  requests(status: string | null) { return this.get<SessionRequestA[]>('/session-requests', { status }); }
  updateRequest(id: string, status: string, adminNotes: string | null, fulfilledBySessionId: string | null = null) {
    return this.post<void>(`/session-requests/${id}/status`, { status, adminNotes, fulfilledBySessionId });
  }
}

/** Converts a Cairo local date ("2026-11-05") + time ("18:00") to a UTC ISO string, DST-aware. */
export function cairoToUtc(date: string, time: string): string {
  const [y, mo, d] = date.split('-').map(Number);
  const [h, mi] = time.split(':').map(Number);
  const guess = Date.UTC(y, mo - 1, d, h, mi);
  const offset = (ms: number) => {
    const parts = new Intl.DateTimeFormat('en-US', {
      timeZone: 'Africa/Cairo', hourCycle: 'h23', year: 'numeric', month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit',
    }).formatToParts(new Date(ms));
    const g = (t: string) => Number(parts.find((p) => p.type === t)!.value);
    return Date.UTC(g('year'), g('month') - 1, g('day'), g('hour'), g('minute')) - ms;
  };
  let utc = guess - offset(guess);
  utc = guess - offset(utc);
  return new Date(utc).toISOString();
}

/** UTC ISO → Cairo local { date: "yyyy-mm-dd", time: "HH:mm" } for form inputs. */
export function utcToCairo(iso: string): { date: string; time: string } {
  const parts = new Intl.DateTimeFormat('en-CA', {
    timeZone: 'Africa/Cairo', hourCycle: 'h23', year: 'numeric', month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit',
  }).formatToParts(new Date(iso));
  const g = (t: string) => parts.find((p) => p.type === t)!.value;
  return { date: `${g('year')}-${g('month')}-${g('day')}`, time: `${g('hour')}:${g('minute')}` };
}
