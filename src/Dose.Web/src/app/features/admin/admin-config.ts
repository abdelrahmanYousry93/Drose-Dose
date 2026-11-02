import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import {
  AdminApi, BandA, ChapterA, GradeA, LessonA, LookupA, PaymentMethodA, ServiceTypeA, StageA, TeacherA,
} from '../../core/admin-api';
import { LANG, TranslatePipe, translate as tr } from '../../core/i18n';
import { EditorConfig, EntityEditor, Field, Option, optName } from './entity-editor';

const nextSort = (n: number) => (n + 1) * 10;
const nameEn = (key = 'nameEn'): Field => ({ key, label: tr('f.nameEn'), type: 'text', dir: 'ltr', hint: tr('f.enHint') });

/** Catalog management: stages, grades, tracks, pricing bands, subjects, chapters & lessons, service types. */
@Component({
  selector: 'dose-admin-catalog',
  imports: [EntityEditor, FormsModule, TranslatePipe],
  template: `
    <section class="page">
      <h1 class="page-title">{{ 'catalog.title' | t }}</h1>
      <div class="tabs" role="tablist">
        @for (t of tabs; track t.key) {
          <button role="tab" type="button" [attr.aria-selected]="tab() === t.key" (click)="tab.set(t.key)">{{ t.label }}</button>
        }
      </div>

      @switch (tab()) {
        @case ('stages') {
          <p class="muted small">{{ 'catalog.stagesHint' | t }}</p>
          <dose-entity-editor [config]="stagesCfg" />
        }
        @case ('grades') {
          <p class="muted small">{{ 'catalog.gradesHint' | t }}</p>
          <dose-entity-editor [config]="gradesCfg" />
        }
        @case ('tracks') { <dose-entity-editor [config]="tracksCfg" /> }
        @case ('bands') {
          <p class="muted small">{{ 'catalog.bandsHint' | t }}</p>
          <dose-entity-editor [config]="bandsCfg" />
        }
        @case ('subjects') { <dose-entity-editor [config]="subjectsCfg" /> }
        @case ('chapters') {
          <div class="filters">
            <label class="field"><span>{{ 'common.grade' | t }}</span>
              <select [ngModel]="gradeId()" (ngModelChange)="gradeId.set($event); refreshChapters()">
                <option value="">{{ 'request.choose' | t }}</option>
                @for (g of gradeOptions(); track g.id) { <option [value]="g.id">{{ name(g) }}</option> }
              </select></label>
            <label class="field"><span>{{ 'common.subject' | t }}</span>
              <select [ngModel]="subjectId()" (ngModelChange)="subjectId.set($event); refreshChapters()">
                <option value="">{{ 'request.choose' | t }}</option>
                @for (s of subjectOptions(); track s.id) { <option [value]="s.id">{{ name(s) }}</option> }
              </select></label>
          </div>
          @if (chaptersCfg(); as cfg) {
            <h2 class="sub">{{ 'catalog.chapters' | t }}</h2>
            <dose-entity-editor [config]="cfg" />
            <label class="field"><span>{{ 'catalog.unitLessons' | t }}</span>
              <select [ngModel]="chapterId()" (ngModelChange)="chapterId.set($event); refreshLessons()">
                <option value="">{{ 'catalog.chooseUnit' | t }}</option>
                @for (c of chapterOptions(); track c.id) { <option [value]="c.id">{{ name(c) }}</option> }
              </select></label>
            @if (lessonsCfg(); as lc) { <dose-entity-editor [config]="lc" /> }
          } @else {
            <p class="muted">{{ 'catalog.pickFirst' | t }}</p>
          }
        }
        @case ('services') {
          <p class="muted small">{{ 'catalog.servicesHint' | t }}</p>
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
  protected readonly name = optName;
  protected readonly tabs = ['stages', 'grades', 'tracks', 'bands', 'subjects', 'chapters', 'services']
    .map((key) => ({ key, label: tr(`catalog.tabs.${key}`) }));
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
    this.stages.set(st.map((x) => ({ id: x.id!, nameAr: x.nameAr, nameEn: x.nameEn })));
    this.bands.set(b.map((x) => ({ id: x.id!, nameAr: x.nameAr, nameEn: x.nameEn })));
    this.subjectOptions.set(s.map((x) => ({ id: x.id!, nameAr: x.nameAr, nameEn: x.nameEn })));
    this.gradeOptions.set(g.map((x) => ({ id: x.id!, nameAr: x.nameAr, nameEn: x.nameEn })));
    this.trackOptions.set(t.map((x) => ({ id: x.id!, nameAr: x.nameAr, nameEn: x.nameEn })));
  }

  private lookupCfg(load: () => Promise<LookupA[]>, save: (d: LookupA) => Promise<unknown>, addLabel: string): EditorConfig<LookupA> {
    return {
      load, addLabel, title: (r) => optName(r),
      save: async (d) => { const r = await save(d); void this.refreshOptions(); return r; },
      blank: () => ({ id: null, nameAr: '', sortOrder: nextSort(0), isActive: true }),
      fields: [
        { key: 'nameAr', label: tr('f.name'), type: 'text' },
        nameEn(),
        { key: 'sortOrder', label: tr('f.sort'), type: 'number' },
        { key: 'isActive', label: tr('f.active'), type: 'checkbox' },
      ],
    };
  }

  protected readonly stagesCfg: EditorConfig<StageA> = {
    load: () => this.api.stages(), title: (r) => optName(r), addLabel: tr('add.stage'),
    save: async (d) => { const r = await this.api.saveStage(d); void this.refreshOptions(); return r; },
    blank: () => ({ id: null, nameAr: '', requiresGuardianContact: true, sortOrder: 10, isActive: true }),
    fields: [
      { key: 'nameAr', label: tr('f.stageName'), type: 'text' },
      nameEn(),
      { key: 'sortOrder', label: tr('f.sort'), type: 'number' },
      { key: 'requiresGuardianContact', label: tr('f.guardianRequired'), type: 'checkbox' },
      { key: 'isActive', label: tr('f.activeF'), type: 'checkbox' },
    ],
  };

  protected readonly gradesCfg: EditorConfig<GradeA> = {
    load: () => this.api.grades(), title: (r) => optName(r), addLabel: tr('add.grade'),
    save: async (d) => { const r = await this.api.saveGrade(d); void this.refreshOptions(); return r; },
    blank: () => ({ id: null, nameAr: '', stageId: this.stages()[0]?.id ?? '', pricingBandId: this.bands()[0]?.id ?? '', curriculumSystem: null, sortOrder: 10, isActive: true, subjectIds: [] }),
    fields: [
      { key: 'nameAr', label: tr('f.gradeName'), type: 'text' },
      nameEn(),
      { key: 'stageId', label: tr('f.stage'), type: 'select', options: () => this.stages() },
      { key: 'pricingBandId', label: tr('f.band'), type: 'select', options: () => this.bands() },
      { key: 'curriculumSystem', label: tr('f.curriculum'), type: 'text', hint: tr('f.curriculumHint') },
      { key: 'sortOrder', label: tr('f.sort'), type: 'number' },
      { key: 'isActive', label: tr('f.active'), type: 'checkbox' },
      { key: 'subjectIds', label: tr('f.gradeSubjects'), type: 'multi', options: () => this.subjectOptions() },
    ],
  };

  protected readonly tracksCfg = this.lookupCfg(() => this.api.tracks(), (d) => this.api.saveTrack(d), tr('add.track'));
  protected readonly subjectsCfg = this.lookupCfg(() => this.api.subjects(), (d) => this.api.saveSubject(d), tr('add.subject'));

  protected readonly bandsCfg: EditorConfig<BandA> = {
    load: () => this.api.bands(), title: (r) => optName(r), addLabel: tr('add.band'),
    save: async (d) => { const r = await this.api.saveBand(d); void this.refreshOptions(); return r; },
    blank: () => ({ id: null, nameAr: '', sortOrder: 10 }),
    fields: [{ key: 'nameAr', label: tr('f.name'), type: 'text' }, nameEn(), { key: 'sortOrder', label: tr('f.sort'), type: 'number' }],
  };

  protected readonly servicesCfg: EditorConfig<ServiceTypeA> = {
    load: () => this.api.serviceTypes(), title: (r) => optName(r), save: (d) => this.api.saveServiceType(d),
    fields: [
      { key: 'code', label: tr('f.code'), type: 'text', readonly: true, dir: 'ltr' },
      { key: 'nameAr', label: tr('f.name'), type: 'text' },
      nameEn(),
      { key: 'descriptionAr', label: tr('f.description'), type: 'textarea' },
      { key: 'descriptionEn', label: tr('f.descriptionEn'), type: 'textarea', dir: 'ltr' },
      { key: 'defaultDurationMinutes', label: tr('f.defaultDuration'), type: 'number', hint: tr('f.defaultDurationHint') },
      { key: 'sortOrder', label: tr('f.sort'), type: 'number' },
      { key: 'isActive', label: tr('f.activeF'), type: 'checkbox' },
    ],
  };

  protected refreshChapters() {
    const gradeId = this.gradeId(), subjectId = this.subjectId();
    this.chapterId.set('');
    this.lessonsCfg.set(null);
    if (!gradeId || !subjectId) { this.chaptersCfg.set(null); return; }
    const load = async () => {
      const list = await this.api.chapters(gradeId, subjectId);
      this.chapterOptions.set(list.map((c) => ({ id: c.id!, nameAr: c.nameAr, nameEn: c.nameEn })));
      return list;
    };
    this.chaptersCfg.set({
      load, title: (r) => optName(r), addLabel: tr('add.unit'),
      save: async (d) => { const r = await this.api.saveChapter(d); await load(); return r; },
      blank: () => ({ id: null, nameAr: '', gradeId, subjectId, studyTrackId: null, sortOrder: 10, isActive: true }),
      fields: [
        { key: 'nameAr', label: tr('f.unitName'), type: 'text' },
        nameEn(),
        { key: 'studyTrackId', label: tr('common.track'), type: 'select', nullable: true, noneLabel: tr('f.allTracks'), options: () => this.trackOptions() },
        { key: 'sortOrder', label: tr('f.sort'), type: 'number' },
        { key: 'isActive', label: tr('f.activeF'), type: 'checkbox' },
      ],
    });
  }

  protected refreshLessons() {
    const chapterId = this.chapterId();
    if (!chapterId) { this.lessonsCfg.set(null); return; }
    this.lessonsCfg.set({
      load: () => this.api.lessons(chapterId), title: (r) => optName(r), addLabel: tr('add.lesson'),
      save: (d) => this.api.saveLesson(d),
      blank: () => ({ id: null, nameAr: '', chapterId, sortOrder: 10, isActive: true }),
      fields: [
        { key: 'nameAr', label: tr('f.lessonName'), type: 'text' },
        nameEn(),
        { key: 'sortOrder', label: tr('f.sort'), type: 'number' },
        { key: 'isActive', label: tr('f.active'), type: 'checkbox' },
      ],
    });
  }
}

@Component({
  selector: 'dose-admin-teachers',
  imports: [EntityEditor, TranslatePipe],
  template: `
    <section class="page">
      <h1 class="page-title">{{ 'titles.adminTeachers' | t }}</h1>
      <p class="muted small">{{ 'teachers.hint' | t }}</p>
      <dose-entity-editor [config]="cfg" />
    </section>
  `,
})
export class AdminTeachers {
  private readonly api = inject(AdminApi);
  private readonly subjectOptions = signal<Option[]>([]);
  constructor() { this.api.subjects().then((s) => this.subjectOptions.set(s.map((x) => ({ id: x.id!, nameAr: x.nameAr, nameEn: x.nameEn })))).catch(() => undefined); }
  protected readonly cfg: EditorConfig<TeacherA> = {
    load: () => this.api.teachers(), save: (d) => this.api.saveTeacher(d),
    title: (r) => (LANG === 'en' && r.displayNameEn ? r.displayNameEn : r.displayNameAr), addLabel: tr('add.teacher'),
    blank: () => ({ id: null, displayNameAr: '', photoUrl: null, bioAr: null, qualificationsAr: null, yearsOfExperience: null, isPublished: false, subjectIds: [] }),
    fields: [
      {
        key: 'photo', label: tr('f.photo'), type: 'photo', hint: tr('f.photoHint'),
        photo: {
          preview: (r: TeacherA) => r.photoPreviewUrl ?? r.photoUrl,
          upload: (r: TeacherA, file: File) => this.api.uploadTeacherPhoto(r.id!, file),
          remove: (r: TeacherA) => this.api.removeTeacherPhoto(r.id!),
        },
      },
      { key: 'displayNameAr', label: tr('f.displayName'), type: 'text', hint: tr('f.displayNameHint') },
      { key: 'displayNameEn', label: tr('f.displayNameEn'), type: 'text', dir: 'ltr', hint: tr('f.enHint') },
      { key: 'yearsOfExperience', label: tr('f.years'), type: 'number', nullable: true },
      { key: 'photoUrl', label: tr('f.photoUrl'), type: 'text', dir: 'ltr', hint: tr('f.photoUrlHint') },
      { key: 'qualificationsAr', label: tr('f.qualifications'), type: 'textarea' },
      { key: 'qualificationsEn', label: tr('f.qualificationsEn'), type: 'textarea', dir: 'ltr' },
      { key: 'bioAr', label: tr('f.bio'), type: 'textarea' },
      { key: 'bioEn', label: tr('f.bioEn'), type: 'textarea', dir: 'ltr' },
      { key: 'subjectIds', label: tr('f.teacherSubjects'), type: 'multi', options: () => this.subjectOptions(), hint: tr('f.teacherSubjectsHint') },
      { key: 'isPublished', label: tr('f.published'), type: 'checkbox' },
    ],
  };
}

@Component({
  selector: 'dose-admin-payment-methods',
  imports: [EntityEditor, TranslatePipe],
  template: `
    <section class="page">
      <h1 class="page-title">{{ 'titles.adminPaymentMethods' | t }}</h1>
      <p class="muted small">{{ 'pm.hint' | t }}</p>
      <dose-entity-editor [config]="cfg" />
    </section>
  `,
})
export class AdminPaymentMethods {
  private readonly api = inject(AdminApi);
  protected readonly cfg: EditorConfig<PaymentMethodA> = {
    load: () => this.api.paymentMethods(), save: (d) => this.api.savePaymentMethod(d), title: (r) => `${optName(r)} — ${r.receivingAccount}`,
    addLabel: tr('add.pm'),
    blank: () => ({ id: null, code: '', nameAr: '', receivingAccount: '', instructionsAr: null, sortOrder: 10, isActive: true }),
    fields: [
      { key: 'nameAr', label: tr('f.name'), type: 'text' },
      nameEn(),
      { key: 'code', label: tr('f.pmCode'), type: 'text', dir: 'ltr', hint: tr('f.pmCodeHint') },
      { key: 'receivingAccount', label: tr('f.receiving'), type: 'text', dir: 'ltr' },
      { key: 'sortOrder', label: tr('f.sort'), type: 'number' },
      { key: 'instructionsAr', label: tr('f.instructions'), type: 'textarea' },
      { key: 'instructionsEn', label: tr('f.instructionsEn'), type: 'textarea', dir: 'ltr' },
      { key: 'isActive', label: tr('f.activeF'), type: 'checkbox' },
    ],
  };
}
