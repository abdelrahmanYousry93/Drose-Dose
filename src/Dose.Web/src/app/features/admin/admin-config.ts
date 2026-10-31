import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import {
  AdminApi, BandA, ChapterA, GradeA, LessonA, LookupA, PaymentMethodA, ServiceTypeA, StageA, TeacherA,
} from '../../core/admin-api';
import { EditorConfig, EntityEditor, Option } from './entity-editor';

const nextSort = (n: number) => (n + 1) * 10;

/** Catalog management: stages, grades, tracks, pricing bands, subjects, chapters & lessons, service types. */
@Component({
  selector: 'dose-admin-catalog',
  imports: [EntityEditor, FormsModule],
  template: `
    <section class="page">
      <h1 class="page-title">الكتالوج الدراسي</h1>
      <div class="tabs" role="tablist">
        @for (t of tabs; track t.key) {
          <button role="tab" type="button" [attr.aria-selected]="tab() === t.key" (click)="tab.set(t.key)">{{ t.label }}</button>
        }
      </div>

      @switch (tab()) {
        @case ('stages') {
          <p class="muted small">"ولي الأمر إجباري" بيتحكم في طلب اسم ورقم ولي الأمر وقت الحجز لكل صفوف المرحلة.</p>
          <dose-entity-editor [config]="stagesCfg" />
        }
        @case ('grades') {
          <p class="muted small">اربط كل صف بالمواد اللي بتتدرّس فيه، وبشريحة التسعير بتاعته.</p>
          <dose-entity-editor [config]="gradesCfg" />
        }
        @case ('tracks') { <dose-entity-editor [config]="tracksCfg" /> }
        @case ('bands') {
          <p class="muted small">شرائح التسعير بتجمّع الصفوف اللي ليها نفس الأسعار. الصف بيتربط بشريحة من صفحة الصفوف.</p>
          <dose-entity-editor [config]="bandsCfg" />
        }
        @case ('subjects') { <dose-entity-editor [config]="subjectsCfg" /> }
        @case ('chapters') {
          <div class="filters">
            <label class="field"><span>الصف</span>
              <select [ngModel]="gradeId()" (ngModelChange)="gradeId.set($event); refreshChapters()">
                <option value="">اختار</option>
                @for (g of gradeOptions(); track g.id) { <option [value]="g.id">{{ g.nameAr }}</option> }
              </select></label>
            <label class="field"><span>المادة</span>
              <select [ngModel]="subjectId()" (ngModelChange)="subjectId.set($event); refreshChapters()">
                <option value="">اختار</option>
                @for (s of subjectOptions(); track s.id) { <option [value]="s.id">{{ s.nameAr }}</option> }
              </select></label>
          </div>
          @if (chaptersCfg(); as cfg) {
            <h2 class="sub">الوحدات</h2>
            <dose-entity-editor [config]="cfg" />
            <label class="field"><span>دروس الوحدة</span>
              <select [ngModel]="chapterId()" (ngModelChange)="chapterId.set($event); refreshLessons()">
                <option value="">اختار وحدة</option>
                @for (c of chapterOptions(); track c.id) { <option [value]="c.id">{{ c.nameAr }}</option> }
              </select></label>
            @if (lessonsCfg(); as lc) { <dose-entity-editor [config]="lc" /> }
          } @else {
            <p class="muted">اختار الصف والمادة علشان تشوف الوحدات والدروس.</p>
          }
        }
        @case ('services') {
          <p class="muted small">أنواع الخدمات التلاتة ثابتة في النظام، وتقدر تعدّل أسماءها ووصفها أو توقف واحدة.</p>
          <dose-entity-editor [config]="servicesCfg" />
        }
      }
    </section>
  `,
  styles: `
    .tabs { display: flex; flex-wrap: wrap; gap: 6px; }
    .tabs button { min-height: 38px; padding: 0 14px; border: 1px solid var(--dose-border); border-radius: 999px; background: var(--dose-surface); font: 600 0.9rem var(--dose-font); cursor: pointer; }
    .tabs button[aria-selected='true'] { background: var(--dose-primary); border-color: var(--dose-primary); color: #fff; }
    .filters { display: grid; gap: 12px; grid-template-columns: 1fr 1fr; }
    .sub { margin: 8px 0 0; font-size: 1.1rem; color: var(--dose-navy); }
  `,
})
export class AdminCatalog {
  private readonly api = inject(AdminApi);
  protected readonly tabs = [
    { key: 'stages', label: 'المراحل' }, { key: 'grades', label: 'الصفوف' }, { key: 'tracks', label: 'المناهج' },
    { key: 'bands', label: 'شرائح التسعير' }, { key: 'subjects', label: 'المواد' }, { key: 'chapters', label: 'الوحدات والدروس' },
    { key: 'services', label: 'أنواع الخدمات' },
  ];
  protected readonly tab = signal('stages');

  private readonly stages = signal<Option[]>([]);
  private readonly bands = signal<Option[]>([]);
  protected readonly subjectOptions = signal<Option[]>([]);
  protected readonly gradeOptions = signal<Option[]>([]);
  private readonly trackOptions = signal<Option[]>([]);
  protected readonly chapterOptions = signal<Option[]>([]);
  protected readonly gradeId = signal('');
  protected readonly subjectId = signal('');
  protected readonly chapterId = signal('');
  protected readonly chaptersCfg = signal<EditorConfig<ChapterA> | null>(null);
  protected readonly lessonsCfg = signal<EditorConfig<LessonA> | null>(null);

  constructor() { void this.refreshOptions(); }

  private async refreshOptions() {
    const [st, b, s, g, t] = await Promise.all([this.api.stages(), this.api.bands(), this.api.subjects(), this.api.grades(), this.api.tracks()]);
    this.stages.set(st.map((x) => ({ id: x.id!, nameAr: x.nameAr })));
    this.bands.set(b.map((x) => ({ id: x.id!, nameAr: x.nameAr })));
    this.subjectOptions.set(s.map((x) => ({ id: x.id!, nameAr: x.nameAr })));
    this.gradeOptions.set(g.map((x) => ({ id: x.id!, nameAr: x.nameAr })));
    this.trackOptions.set(t.map((x) => ({ id: x.id!, nameAr: x.nameAr })));
  }

  private lookupCfg(load: () => Promise<LookupA[]>, save: (d: LookupA) => Promise<unknown>, addLabel: string): EditorConfig<LookupA> {
    return {
      load, addLabel, title: (r) => r.nameAr,
      save: async (d) => { const r = await save(d); void this.refreshOptions(); return r; },
      blank: () => ({ id: null, nameAr: '', sortOrder: nextSort(0), isActive: true }),
      fields: [
        { key: 'nameAr', label: 'الاسم', type: 'text' },
        { key: 'sortOrder', label: 'الترتيب', type: 'number' },
        { key: 'isActive', label: 'مفعّل', type: 'checkbox' },
      ],
    };
  }

  protected readonly stagesCfg: EditorConfig<StageA> = {
    load: () => this.api.stages(), title: (r) => r.nameAr, addLabel: 'مرحلة',
    save: async (d) => { const r = await this.api.saveStage(d); void this.refreshOptions(); return r; },
    blank: () => ({ id: null, nameAr: '', requiresGuardianContact: true, sortOrder: 10, isActive: true }),
    fields: [
      { key: 'nameAr', label: 'اسم المرحلة', type: 'text' },
      { key: 'sortOrder', label: 'الترتيب', type: 'number' },
      { key: 'requiresGuardianContact', label: 'رقم ولي الأمر إجباري', type: 'checkbox' },
      { key: 'isActive', label: 'مفعّلة', type: 'checkbox' },
    ],
  };

  protected readonly gradesCfg: EditorConfig<GradeA> = {
    load: () => this.api.grades(), title: (r) => r.nameAr, addLabel: 'صف',
    save: async (d) => { const r = await this.api.saveGrade(d); void this.refreshOptions(); return r; },
    blank: () => ({ id: null, nameAr: '', stageId: this.stages()[0]?.id ?? '', pricingBandId: this.bands()[0]?.id ?? '', curriculumSystem: null, sortOrder: 10, isActive: true, subjectIds: [] }),
    fields: [
      { key: 'nameAr', label: 'اسم الصف', type: 'text' },
      { key: 'stageId', label: 'المرحلة', type: 'select', options: () => this.stages() },
      { key: 'pricingBandId', label: 'شريحة التسعير', type: 'select', options: () => this.bands() },
      { key: 'curriculumSystem', label: 'النظام الدراسي (اختياري)', type: 'text', hint: 'مثلاً نظام البكالوريا لتانية وتالتة ثانوي' },
      { key: 'sortOrder', label: 'الترتيب', type: 'number' },
      { key: 'isActive', label: 'مفعّل', type: 'checkbox' },
      { key: 'subjectIds', label: 'المواد في الصف ده', type: 'multi', options: () => this.subjectOptions() },
    ],
  };

  protected readonly tracksCfg = this.lookupCfg(() => this.api.tracks(), (d) => this.api.saveTrack(d), 'منهج');
  protected readonly subjectsCfg = this.lookupCfg(() => this.api.subjects(), (d) => this.api.saveSubject(d), 'مادة');

  protected readonly bandsCfg: EditorConfig<BandA> = {
    load: () => this.api.bands(), title: (r) => r.nameAr, addLabel: 'شريحة',
    save: async (d) => { const r = await this.api.saveBand(d); void this.refreshOptions(); return r; },
    blank: () => ({ id: null, nameAr: '', sortOrder: 10 }),
    fields: [{ key: 'nameAr', label: 'الاسم', type: 'text' }, { key: 'sortOrder', label: 'الترتيب', type: 'number' }],
  };

  protected readonly servicesCfg: EditorConfig<ServiceTypeA> = {
    load: () => this.api.serviceTypes(), title: (r) => r.nameAr, save: (d) => this.api.saveServiceType(d),
    fields: [
      { key: 'code', label: 'الكود', type: 'text', readonly: true, dir: 'ltr' },
      { key: 'nameAr', label: 'الاسم', type: 'text' },
      { key: 'descriptionAr', label: 'الوصف للطالب', type: 'textarea' },
      { key: 'sortOrder', label: 'الترتيب', type: 'number' },
      { key: 'isActive', label: 'مفعّلة', type: 'checkbox' },
    ],
  };

  protected refreshChapters() {
    const gradeId = this.gradeId(), subjectId = this.subjectId();
    this.chapterId.set('');
    this.lessonsCfg.set(null);
    if (!gradeId || !subjectId) { this.chaptersCfg.set(null); return; }
    const load = async () => {
      const list = await this.api.chapters(gradeId, subjectId);
      this.chapterOptions.set(list.map((c) => ({ id: c.id!, nameAr: c.nameAr })));
      return list;
    };
    this.chaptersCfg.set({
      load, title: (r) => r.nameAr, addLabel: 'وحدة',
      save: async (d) => { const r = await this.api.saveChapter(d); await load(); return r; },
      blank: () => ({ id: null, nameAr: '', gradeId, subjectId, studyTrackId: null, sortOrder: 10, isActive: true }),
      fields: [
        { key: 'nameAr', label: 'اسم الوحدة', type: 'text' },
        { key: 'studyTrackId', label: 'المنهج', type: 'select', nullable: true, noneLabel: 'كل المناهج', options: () => this.trackOptions() },
        { key: 'sortOrder', label: 'الترتيب', type: 'number' },
        { key: 'isActive', label: 'مفعّلة', type: 'checkbox' },
      ],
    });
  }

  protected refreshLessons() {
    const chapterId = this.chapterId();
    if (!chapterId) { this.lessonsCfg.set(null); return; }
    this.lessonsCfg.set({
      load: () => this.api.lessons(chapterId), title: (r) => r.nameAr, addLabel: 'درس',
      save: (d) => this.api.saveLesson(d),
      blank: () => ({ id: null, nameAr: '', chapterId, sortOrder: 10, isActive: true }),
      fields: [
        { key: 'nameAr', label: 'اسم الدرس', type: 'text' },
        { key: 'sortOrder', label: 'الترتيب', type: 'number' },
        { key: 'isActive', label: 'مفعّل', type: 'checkbox' },
      ],
    });
  }
}

@Component({
  selector: 'dose-admin-teachers',
  imports: [EntityEditor],
  template: `
    <section class="page">
      <h1 class="page-title">المدرسين</h1>
      <p class="muted small">الاسم والصورة والمؤهلات بتظهر للطالب قبل ما يحجز. الجلسة مينفعش تتنشر غير لو المدرس منشور.</p>
      <dose-entity-editor [config]="cfg" />
    </section>
  `,
})
export class AdminTeachers {
  private readonly api = inject(AdminApi);
  protected readonly cfg: EditorConfig<TeacherA> = {
    load: () => this.api.teachers(), save: (d) => this.api.saveTeacher(d), title: (r) => r.displayNameAr, addLabel: 'مدرس',
    blank: () => ({ id: null, displayNameAr: '', photoUrl: null, bioAr: null, qualificationsAr: null, yearsOfExperience: null, isPublished: false }),
    fields: [
      { key: 'displayNameAr', label: 'الاسم المعروض', type: 'text', hint: 'مثلاً: أ. محمد صلاح' },
      { key: 'yearsOfExperience', label: 'سنين الخبرة', type: 'number', nullable: true },
      { key: 'photoUrl', label: 'رابط الصورة (https)', type: 'text', dir: 'ltr' },
      { key: 'qualificationsAr', label: 'المؤهلات', type: 'textarea' },
      { key: 'bioAr', label: 'نبذة قصيرة', type: 'textarea' },
      { key: 'isPublished', label: 'منشور (يظهر للطلاب)', type: 'checkbox' },
    ],
  };
}

@Component({
  selector: 'dose-admin-payment-methods',
  imports: [EntityEditor],
  template: `
    <section class="page">
      <h1 class="page-title">طرق الدفع</h1>
      <p class="muted small">رقم أو عنوان الاستلام بيظهر للطالب في خطوة الدفع. اتأكد إنه صح قبل الإطلاق.</p>
      <dose-entity-editor [config]="cfg" />
    </section>
  `,
})
export class AdminPaymentMethods {
  private readonly api = inject(AdminApi);
  protected readonly cfg: EditorConfig<PaymentMethodA> = {
    load: () => this.api.paymentMethods(), save: (d) => this.api.savePaymentMethod(d), title: (r) => `${r.nameAr} — ${r.receivingAccount}`,
    addLabel: 'طريقة دفع',
    blank: () => ({ id: null, code: '', nameAr: '', receivingAccount: '', instructionsAr: null, sortOrder: 10, isActive: true }),
    fields: [
      { key: 'nameAr', label: 'الاسم', type: 'text' },
      { key: 'code', label: 'الكود (إنجليزي)', type: 'text', dir: 'ltr', hint: 'مثلاً instapay' },
      { key: 'receivingAccount', label: 'رقم / عنوان الاستلام', type: 'text', dir: 'ltr' },
      { key: 'sortOrder', label: 'الترتيب', type: 'number' },
      { key: 'instructionsAr', label: 'تعليمات للطالب', type: 'textarea' },
      { key: 'isActive', label: 'مفعّلة', type: 'checkbox' },
    ],
  };
}
