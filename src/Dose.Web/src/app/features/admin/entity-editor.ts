import { Component, effect, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { errorMessage } from '../../core/format';

export interface Option { id: string; nameAr: string }
export interface Field {
  key: string;
  label: string;
  type: 'text' | 'number' | 'checkbox' | 'select' | 'multi' | 'textarea';
  options?: () => Option[];
  /** For selects: allow "none" (null). */
  nullable?: boolean;
  noneLabel?: string;
  dir?: 'ltr';
  readonly?: boolean;
  hint?: string;
}
export interface EditorConfig<T> {
  fields: Field[];
  load: () => Promise<T[]>;
  save: (row: T) => Promise<unknown>;
  /** New blank row; omit to disable adding. */
  blank?: () => T;
  title: (row: T) => string;
  addLabel?: string;
}

type Row = Record<string, unknown> & { id?: string | null };

/**
 * Generic admin list editor: each record is an editable card with its own Save button.
 * Records are never deleted — they're deactivated (isActive) so history stays intact.
 */
@Component({
  selector: 'dose-entity-editor',
  imports: [FormsModule],
  template: `
    <div class="bar">
      @if (config().blank) {
        <button type="button" class="dose-btn dose-btn--primary" (click)="add()">+ {{ config().addLabel ?? 'إضافة' }}</button>
      }
      <span class="muted small">{{ rows().length }} عنصر</span>
    </div>
    @if (loadError()) { <p class="alert alert--error">{{ loadError() }}</p> }

    @for (row of rows(); track $index) {
      <details class="dose-card item" [open]="!row.id || open() === $index" [class.inactive]="row['isActive'] === false">
        <summary (click)="open.set($index)">
          <strong>{{ row.id ? config().title($any(row)) : 'جديد' }}</strong>
          @if (row['isActive'] === false) { <span class="badge badge--gray">موقوف</span> }
          @if (row['isPublished'] === false) { <span class="badge badge--gray">مش منشور</span> }
        </summary>
        <form class="grid" (ngSubmit)="save(row, $index)">
          @for (f of config().fields; track f.key) {
            @switch (f.type) {
              @case ('checkbox') {
                <label class="check"><input type="checkbox" [name]="f.key" [attr.name]="f.key" [(ngModel)]="row[f.key]" [disabled]="!!f.readonly" /><span>{{ f.label }}</span></label>
              }
              @case ('select') {
                <label class="field"><span>{{ f.label }}</span>
                  <select [name]="f.key" [attr.name]="f.key" [(ngModel)]="row[f.key]" [disabled]="!!f.readonly">
                    @if (f.nullable) { <option [ngValue]="null">{{ f.noneLabel ?? '— الكل —' }}</option> }
                    @for (o of f.options!(); track o.id) { <option [ngValue]="o.id">{{ o.nameAr }}</option> }
                  </select>
                </label>
              }
              @case ('multi') {
                <fieldset class="multi"><legend>{{ f.label }}</legend>
                  @for (o of f.options!(); track o.id) {
                    <label class="check"><input type="checkbox" [checked]="has(row, f.key, o.id)" (change)="toggle(row, f.key, o.id)" />
                      <span>{{ o.nameAr }}</span></label>
                  }
                </fieldset>
              }
              @case ('textarea') {
                <label class="field wide"><span>{{ f.label }}</span><textarea [name]="f.key" [attr.name]="f.key" [(ngModel)]="row[f.key]"></textarea></label>
              }
              @case ('number') {
                <label class="field"><span>{{ f.label }}</span><input type="number" [name]="f.key" [attr.name]="f.key" [(ngModel)]="row[f.key]" dir="ltr" /></label>
              }
              @default {
                <label class="field"><span>{{ f.label }}</span>
                  <input [name]="f.key" [attr.name]="f.key" [(ngModel)]="row[f.key]" [attr.dir]="f.dir ?? null" [readonly]="!!f.readonly" />
                  @if (f.hint) { <span class="hint">{{ f.hint }}</span> }
                </label>
              }
            }
          }
          <div class="actions wide">
            <button class="dose-btn dose-btn--primary" type="submit" [disabled]="busy() === $index">حفظ</button>
            @if (status()[$index]; as s) {
              <span [class]="s.ok ? 'ok' : 'err'" role="status">{{ s.text }}</span>
            }
          </div>
        </form>
      </details>
    }
  `,
  styles: `
    .bar { display: flex; justify-content: space-between; align-items: center; gap: 8px; }
    .item { padding: 0; overflow: hidden; }
    .item.inactive { opacity: .7; }
    summary { display: flex; gap: 8px; align-items: center; padding: 14px 16px; cursor: pointer; list-style: none; }
    summary::-webkit-details-marker { display: none; }
    .grid { display: grid; gap: 12px; padding: 0 16px 16px; }
    @media (min-width: 760px) { .grid { grid-template-columns: 1fr 1fr; } .wide { grid-column: 1 / -1; } }
    .multi { border: 1px solid var(--dose-border); border-radius: var(--dose-radius-sm); padding: 10px; display: flex; flex-wrap: wrap; gap: 6px 16px; grid-column: 1 / -1; }
    .multi legend { font-weight: 600; padding: 0 6px; }
    .actions { display: flex; align-items: center; gap: 12px; }
    .ok { color: var(--dose-success); font-weight: 600; }
    .err { color: #a52714; font-weight: 600; }
  `,
})
export class EntityEditor {
  readonly config = input.required<EditorConfig<any>>();
  protected readonly rows = signal<Row[]>([]);
  protected readonly open = signal<number | null>(null);
  protected readonly busy = signal<number | null>(null);
  protected readonly status = signal<Record<number, { ok: boolean; text: string }>>({});
  protected readonly loadError = signal('');

  constructor() {
    effect(() => { void this.reload(this.config()); });
  }

  async reload(cfg = this.config()) {
    try {
      const data = await cfg.load();
      this.rows.set(data.map((r) => structuredClone(r) as Row));
      this.loadError.set('');
    } catch (e) {
      this.loadError.set(errorMessage(e));
    }
  }

  protected add() {
    const blank = this.config().blank;
    if (blank) this.rows.update((r) => [blank() as Row, ...r]);
    this.status.set({});
  }

  protected has(row: Row, key: string, id: string) { return ((row[key] as string[]) ?? []).includes(id); }
  protected toggle(row: Row, key: string, id: string) {
    const list = new Set((row[key] as string[]) ?? []);
    if (list.has(id)) list.delete(id); else list.add(id);
    row[key] = [...list];
  }

  protected async save(row: Row, index: number) {
    this.busy.set(index);
    try {
      for (const f of this.config().fields)
        if (f.type === 'number') row[f.key] = row[f.key] === '' || row[f.key] == null ? (f.nullable ? null : 0) : Number(row[f.key]);
      const result = (await this.config().save(row)) as { id?: string } | undefined;
      if (!row.id && result?.id) row.id = result.id;
      this.open.set(index);
      this.status.update((s) => ({ ...s, [index]: { ok: true, text: 'اتحفظ ✓' } }));
    } catch (e) {
      this.status.update((s) => ({ ...s, [index]: { ok: false, text: errorMessage(e) } }));
    } finally {
      this.busy.set(null);
    }
  }
}
