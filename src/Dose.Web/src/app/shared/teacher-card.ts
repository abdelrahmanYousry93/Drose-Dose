import { Component, computed, input } from '@angular/core';
import { TeacherPublic } from '../core/api';

@Component({
  selector: 'dose-teacher-card',
  template: `
    @let t = teacher();
    <div class="dose-card teacher">
      @if (t.photoUrl) { <img [src]="t.photoUrl" [alt]="t.displayNameAr" /> }
      @else { <span class="avatar" aria-hidden="true">{{ initials() }}</span> }
      <div class="body">
        <span class="muted small">المدرس</span>
        <strong>{{ t.displayNameAr }}</strong>
        @if (t.yearsOfExperience) { <span class="small">خبرة {{ t.yearsOfExperience }} سنين</span> }
        @if (t.qualificationsAr) { <span class="small">{{ t.qualificationsAr }}</span> }
        @if (t.bioAr) { <p class="muted small">{{ t.bioAr }}</p> }
      </div>
    </div>
  `,
  styles: `
    .teacher { display: flex; gap: 14px; padding: 16px; align-items: flex-start; }
    img, .avatar { width: 64px; height: 64px; border-radius: 50%; flex: none; object-fit: cover; }
    .avatar { display: grid; place-items: center; background: var(--dose-primary-50); color: var(--dose-primary); font-weight: 800; font-size: 1.3rem; }
    .body { display: grid; gap: 2px; }
    strong { font-size: 1.08rem; color: var(--dose-navy); }
    p { margin: 4px 0 0; }
  `,
})
export class TeacherCard {
  readonly teacher = input.required<TeacherPublic>();
  protected readonly initials = computed(() =>
    this.teacher().displayNameAr.replace(/^(أ\.|م\.|د\.)\s*/, '').split(/\s+/).slice(0, 2).map((w) => w[0]).join(' '));
}
