import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../environments/environment';

// ---------- Models (mirror Dose.Contracts) ----------
export interface Grade { id: string; nameAr: string; stageId: string; stageNameAr: string; requiresGuardianContact: boolean }
export interface Stage { id: string; nameAr: string; requiresGuardianContact: boolean; grades: Grade[] }
export interface Lookup { id: string; nameAr: string }
export interface ServiceType { id: string; code: string; nameAr: string; descriptionAr: string | null }

export interface TeacherPublic {
  id: string; displayNameAr: string; photoUrl: string | null; bioAr: string | null;
  qualificationsAr: string | null; yearsOfExperience: number | null;
}
export interface GroupTerms {
  targetTierNameAr: string; targetMin: number; targetMax: number;
  worstCasePrice: number; maxExtraAmount: number; tierDecisionAtUtc: string | null;
}
export interface SessionSummary {
  id: string; scopeTitleAr: string; subjectNameAr: string; gradeNameAr: string; studyTrackNameAr: string;
  serviceTypeNameAr: string; mode: 'Private' | 'Group'; startUtc: string; endUtc: string; durationMinutes: number;
  teacherNameAr: string; teacherPhotoUrl: string | null; pricePerStudent: number; currency: string;
  capacity: number; confirmedCount: number; remainingSeats: number; bookingDeadlineUtc: string;
}
export interface SessionDetails {
  summary: SessionSummary; scopeDetailsAr: string | null; minParticipants: number; teacher: TeacherPublic;
  groupTerms: GroupTerms | null; requiresGuardianContact: boolean; isOpenForBooking: boolean;
}
export interface HoldResponse {
  bookingId: string; reference: string; accessToken: string; holdExpiresAtUtc: string;
  price: number; worstCasePrice: number | null; currency: string;
}
export interface DetailsRequest {
  studentName: string; contactPhone: string; guardianName: string | null; guardianPhone: string | null;
  acceptTerms: boolean; acceptTopUpTerms: boolean; expectedPrice: number;
}
export interface PaymentMethod { id: string; code: string; nameAr: string; receivingAccount: string; instructionsAr: string | null }
export type BookingStatus =
  | 'PendingHold' | 'PendingPayment' | 'PaymentUnderReview' | 'Confirmed' | 'TopUpPending'
  | 'Cancelled' | 'Expired' | 'RefundPending' | 'Refunded' | 'Transferred' | 'Completed' | 'NoShow';
export interface BookingView {
  reference: string; status: BookingStatus; session: SessionSummary; teacher: TeacherPublic; groupTerms: GroupTerms | null;
  requiresGuardianContact: boolean; price: number; currency: string; holdExpiresAtUtc: string | null;
  paymentDueAtUtc: string | null; studentName: string | null; latestProofStatus: string | null;
  latestProofRejectionReason: string | null; canJoinNow: boolean; joinOpensAtUtc: string | null; whatsAppMessage: string;
  topUp: TopUpView | null; fullRefundUntilUtc: string; canCancel: boolean;
}
export interface TopUpView {
  amount: number; currency: string; respondByUtc: string | null; reason: string | null;
  choice: 'PayDifference' | 'Transfer' | 'Refund' | null; proofStatus: string | null; proofRejectionReason: string | null;
}
export interface GroupOption { id: string; nameAr: string; minParticipants: number; maxParticipants: number; pricePerStudent: number }
export interface PriceOptions { currency: string; privatePrice: number | null; groupOptions: GroupOption[] }
export interface SessionRequestBody {
  studentName: string; contactPhone: string; guardianName: string | null; guardianPhone: string | null;
  gradeId: string; studyTrackId: string; subjectId: string; serviceTypeId: string; scopeText: string;
  preferredDays: string[]; preferredTimes: string[]; mode: 'Private' | 'Group'; preferredTierId: string | null; notes: string | null;
}
export interface PaymentProofAdmin {
  id: string; bookingReference: string; bookingStatus: string; studentName: string; contactPhone: string;
  guardianPhone: string | null; sessionTitleAr: string; sessionStartUtc: string; methodNameAr: string; amount: number;
  currency: string; senderReference: string | null; channel: string; status: string; submittedAtUtc: string; ageMinutes: number;
  isTopUp: boolean;
}
export interface Setting { key: string; value: string; descriptionAr: string | null }

/** Error carrying the API's stable error code (see DomainExceptionHandler). */
export class ApiError extends Error {
  constructor(public readonly code: string, message: string, public readonly status: number) { super(message); }
}

@Injectable({ providedIn: 'root' })
export class Api {
  private readonly http = inject(HttpClient);
  private readonly base = environment.apiBaseUrl;

  private async run<T>(req: Promise<T>): Promise<T> {
    try {
      return await req;
    } catch (e) {
      if (e instanceof HttpErrorResponse) {
        const body = e.error as { code?: string; title?: string } | null;
        throw new ApiError(body?.code ?? `http.${e.status}`, body?.title ?? e.message, e.status);
      }
      throw e;
    }
  }
  private get<T>(url: string, params?: Record<string, string | null | undefined>) {
    let p = new HttpParams();
    for (const [k, v] of Object.entries(params ?? {})) if (v) p = p.set(k, v);
    return this.run(firstValueFrom(this.http.get<T>(this.base + url, { params: p })));
  }
  private post<T>(url: string, body: unknown) {
    return this.run(firstValueFrom(this.http.post<T>(this.base + url, body)));
  }

  // Catalog
  stages() { return this.get<Stage[]>('/catalog/stages'); }
  studyTracks() { return this.get<Lookup[]>('/catalog/study-tracks'); }
  subjects(gradeId?: string | null) { return this.get<Lookup[]>('/catalog/subjects', { gradeId }); }
  services() { return this.get<ServiceType[]>('/services'); }
  paymentMethods() { return this.get<PaymentMethod[]>('/payment-methods'); }

  // Sessions
  sessions(f: { gradeId?: string | null; subjectId?: string | null; studyTrackId?: string | null; mode?: string | null }) {
    return this.get<SessionSummary[]>('/sessions', f);
  }
  session(id: string) { return this.get<SessionDetails>(`/sessions/${id}`); }

  // Booking (secret-link token)
  hold(sessionId: string) { return this.post<HoldResponse>('/bookings/holds', { sessionId }); }
  booking(token: string) { return this.get<BookingView>(`/b/${encodeURIComponent(token)}`); }
  submitDetails(token: string, req: DetailsRequest) { return this.post<void>(`/b/${encodeURIComponent(token)}/details`, req); }
  uploadProof(token: string, methodId: string, file: File, senderReference: string) {
    const form = new FormData();
    form.append('paymentMethodId', methodId);
    form.append('screenshot', file);
    if (senderReference) form.append('senderReference', senderReference);
    return this.post<void>(`/b/${encodeURIComponent(token)}/payment-proofs`, form);
  }
  join(token: string) { return this.get<{ meetingUrl: string }>(`/b/${encodeURIComponent(token)}/join`); }
  topUpChoice(token: string, choice: string) { return this.post<void>(`/b/${encodeURIComponent(token)}/top-up-choice`, { choice }); }
  uploadTopUpProof(token: string, methodId: string, file: File, senderReference: string) {
    const form = new FormData();
    form.append('paymentMethodId', methodId);
    form.append('screenshot', file);
    if (senderReference) form.append('senderReference', senderReference);
    return this.post<void>(`/b/${encodeURIComponent(token)}/top-up-proofs`, form);
  }
  cancelBooking(token: string, acceptNoRefund: boolean) { return this.post<void>(`/b/${encodeURIComponent(token)}/cancel`, { acceptNoRefund }); }
  priceOptions(q: { serviceTypeId: string; gradeId: string; studyTrackId: string; subjectId: string }) {
    return this.get<PriceOptions | null>('/price-options', q);
  }
  createRequest(body: SessionRequestBody) { return this.post<{ reference: string; whatsAppMessage: string }>('/session-requests', body); }

  // Auth & admin
  login(email: string, password: string) { return this.post<void>('/auth/login', { email, password }); }
  logout() { return this.post<void>('/auth/logout', {}); }
  me() { return this.get<{ email: string; roles: string[] }>('/auth/me'); }
  proofs(status: string | null) { return this.get<PaymentProofAdmin[]>('/admin/payment-proofs', { status }); }
  proofFileUrl(id: string) { return `${this.base}/admin/payment-proofs/${id}/file`; }
  approve(id: string) { return this.post<void>(`/admin/payment-proofs/${id}/approve`, {}); }
  reject(id: string, reason: string) { return this.post<void>(`/admin/payment-proofs/${id}/reject`, { reason }); }
  settings() { return this.get<Setting[]>('/admin/settings'); }
  saveSettings(settings: Setting[]) {
    return this.run(firstValueFrom(this.http.put<void>(`${this.base}/admin/settings`, { settings })));
  }
}
