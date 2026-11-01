import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { Api, Lookup, Stage } from '../../core/api';
import { errorMessage } from '../../core/format';
import { TranslatePipe } from '../../core/i18n';

/** Home (spec 9.1). All selector options come from the catalog API. */
@Component({
  selector: 'dose-home',
  imports: [RouterLink, FormsModule, TranslatePipe],
  template: `
    <section class="hero">
      <p class="eyebrow">{{ 'home.eyebrow' | t }}</p>
      <h1>{{ 'home.title1' | t }}<br /><span>Dose</span> {{ 'home.title2' | t }}</h1>
      <p class="lead">{{ 'home.lead' | t }}</p>
    </section>

    <form class="dose-card finder" (ngSubmit)="search()" aria-labelledby="finder-title">
      <h2 id="finder-title">{{ 'home.finderTitle' | t }}</h2>
      <p class="muted small">{{ 'home.finderHint' | t }}</p>

      @if (loadError()) { <p class="alert alert--error">{{ loadError() }}</p> }

      <label class="field">
        <span>{{ 'common.grade' | t }}</span>
        <select name="grade" [ngModel]="gradeId()" (ngModelChange)="onGrade($event)">
          <option value="">{{ 'home.chooseGrade' | t }}</option>
          @for (stage of stages(); track stage.id) {
            <optgroup [label]="stage.name">
              @for (g of stage.grades; track g.id) { <option [value]="g.id">{{ g.name }}</option> }
            </optgroup>
          }
        </select>
      </label>
      <label class="field">
        <span>{{ 'common.subject' | t }}</span>
        <select name="subject" [ngModel]="subjectId()" (ngModelChange)="subjectId.set($event)" [disabled]="!gradeId()">
          <option value="">{{ 'home.allSubjects' | t }}</option>
          @for (s of subjects(); track s.id) { <option [value]="s.id">{{ s.name }}</option> }
        </select>
      </label>
      <label class="field">
        <span>{{ 'common.track' | t }}</span>
        <select name="track" [ngModel]="trackId()" (ngModelChange)="trackId.set($event)">
          <option value="">{{ 'home.allTracks' | t }}</option>
          @for (t of tracks(); track t.id) { <option [value]="t.id">{{ t.name }}</option> }
        </select>
      </label>

      <button class="dose-btn dose-btn--primary" type="submit" [disabled]="!canSearch()">{{ 'home.search' | t }}</button>
    </form>

    <section class="entries">
      <a class="dose-card entry" routerLink="/open-groups">
        <strong>{{ 'home.openGroups' | t }}</strong>
        <span>{{ 'home.openGroupsHint' | t }}</span>
      </a>
      <a class="dose-card entry" routerLink="/request-session">
        <strong>{{ 'home.noTime' | t }}</strong>
        <span>{{ 'home.noTimeHint' | t }}</span>
      </a>
    </section>
  `,
  styles: `
    :host { display: block; }
    .hero { padding: 28px 0 20px; }
    .eyebrow { margin: 0 0 6px; color: var(--dose-primary); font-weight: 700; }
    h1 { margin: 0; font-size: clamp(1.6rem, 6vw, 2.4rem); line-height: 1.35; color: var(--dose-navy); }
    h1 span { color: var(--dose-primary); }
    .lead { margin: 12px 0 0; color: var(--dose-text-muted); max-width: 36rem; }
    .finder { padding: 20px; display: grid; gap: 14px; }
    .finder h2 { margin: 0; font-size: 1.3rem; }
    .finder p { margin: -8px 0 0; }
    .entries { display: grid; gap: 12px; margin: 16px 0 32px; }
    .entry { display: grid; gap: 4px; padding: 18px; text-decoration: none; color: inherit; border-inline-start: 4px solid var(--dose-primary); }
    .entry:nth-child(2) { border-inline-start-color: var(--dose-accent); }
    .entry strong { font-size: 1.05rem; color: var(--dose-navy); }
    .entry span { color: var(--dose-text-muted); font-size: 0.92rem; }
    @media (min-width: 760px) { .entries { grid-template-columns: 1fr 1fr; } }
  `,
})
export class Home {
  private readonly api = inject(Api);
  private readonly router = inject(Router);

  protected readonly stages = signal<Stage[]>([]);
  protected readonly tracks = signal<Lookup[]>([]);
  protected readonly subjects = signal<Lookup[]>([]);
  protected readonly gradeId = signal('');
  protected readonly subjectId = signal('');
  protected readonly trackId = signal('');
  protected readonly loadError = signal('');
  protected readonly canSearch = computed(() => !!this.gradeId());

  constructor() {
    Promise.all([this.api.stages(), this.api.studyTracks()])
      .then(([stages, tracks]) => { this.stages.set(stages); this.tracks.set(tracks); })
      .catch((e) => this.loadError.set(errorMessage(e)));
  }

  protected async onGrade(id: string) {
    this.gradeId.set(id);
    this.subjectId.set('');
    this.subjects.set(id ? await this.api.subjects(id).catch(() => []) : []);
  }

  protected search() {
    if (!this.canSearch()) return;
    this.router.navigate(['/sessions'], {
      queryParams: { gradeId: this.gradeId(), subjectId: this.subjectId() || null, studyTrackId: this.trackId() || null },
    });
  }
}
