import { Injectable, computed, signal } from '@angular/core';
import { translate } from './i18n';

/** What the student picked on the home page; remembered on this device so lists and forms don't ask again. */
export interface StudentProfile { gradeId: string; gradeName: string; subjectId: string; subjectName: string; studyTrackId: string; studyTrackName: string }

/** Where the student is right now — sent with "Talk to us on WhatsApp" so the team can help with the exact step. */
export interface SupportContext {
  /** Translation key of the step, e.g. 'support.step.payment'. */
  step?: string;
  session?: string | null;
  sessionTime?: string | null;
  reference?: string | null;
  studentName?: string | null;
}

const KEY = 'dose.profile';

function load(): StudentProfile | null {
  try {
    const p = JSON.parse(localStorage.getItem(KEY) ?? 'null') as StudentProfile | null;
    return p?.gradeId ? p : null;
  } catch { return null; }
}

@Injectable({ providedIn: 'root' })
export class StudentContext {
  readonly profile = signal<StudentProfile | null>(load());
  readonly support = signal<SupportContext>({});

  saveProfile(p: StudentProfile) {
    this.profile.set(p);
    try { localStorage.setItem(KEY, JSON.stringify(p)); } catch { /* storage unavailable */ }
  }

  clearProfile() {
    this.profile.set(null);
    try { localStorage.removeItem(KEY); } catch { /* storage unavailable */ }
  }

  /** Pages call this on open; a page that doesn't set a step falls back to a plain greeting. */
  setSupport(ctx: SupportContext) { this.support.set(ctx); }

  /** The prefilled WhatsApp text: who/what/where. No phone numbers or private links are ever included. */
  readonly whatsAppText = computed(() => {
    const p = this.profile();
    const s = this.support();
    const lines = [translate('support.hello')];
    if (s.studentName) lines.push(translate('support.name', { name: s.studentName.trim().split(/\s+/)[0] }));
    if (p) {
      lines.push(translate('support.grade', { grade: p.gradeName + (p.studyTrackName ? ` — ${p.studyTrackName}` : '') }));
      if (p.subjectName) lines.push(translate('support.subject', { subject: p.subjectName }));
    }
    if (s.session) lines.push(translate('support.session', { session: s.session + (s.sessionTime ? ` (${s.sessionTime})` : '') }));
    if (s.reference) lines.push(translate('support.reference', { ref: s.reference }));
    if (s.step) lines.push(translate('support.stepLine', { step: translate(s.step) }));
    lines.push(translate('support.question'));
    return lines.join('\n');
  });
}
