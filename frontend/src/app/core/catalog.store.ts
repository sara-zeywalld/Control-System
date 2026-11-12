import { Injectable, computed, inject, signal } from '@angular/core';
import { ApiService, errorMessages } from './api.service';
import { Catalog, CatalogEntity, CatalogField, FieldRef, FieldType } from './models';

/** אפשרות בחירת שדה בעורך: תווית עסקית + נתיב הקשרים. */
export interface FieldOption {
  id: string;
  label: string;
  group: string;
  ref: FieldRef;
  type: FieldType;
}

export const MAX_RELATION_DEPTH = 2;

export const fieldId = (r: FieldRef) => [...r.path, r.field].join('/');

/** קטלוג הנתונים העסקיים - נטען פעם אחת ומשותף לכל המסכים. */
@Injectable({ providedIn: 'root' })
export class CatalogStore {
  private readonly api = inject(ApiService);

  readonly catalog = signal<Catalog | null>(null);
  readonly error = signal<string | null>(null);
  readonly entities = computed(() => this.catalog()?.entities.filter((e) => e.isQueryable) ?? []);

  load() {
    if (this.catalog()) return;
    this.api.catalog().subscribe({
      next: (c) => this.catalog.set(c),
      error: (e) => this.error.set(errorMessages(e).join(' ')),
    });
  }

  entity(key: string): CatalogEntity | undefined {
    return this.catalog()?.entities.find((e) => e.key === key);
  }

  field(rootKey: string, ref: FieldRef): CatalogField | undefined {
    let entity = this.entity(rootKey);
    for (const hop of ref.path) {
      const rel = entity?.relations.find((r) => r.key === hop);
      entity = rel ? this.entity(rel.targetEntity) : undefined;
    }
    return entity?.fields.find((f) => f.key === ref.field);
  }

  /** כל השדות שניתן לבחור: של הישות עצמה ושל ישויות קשורות (עד MAX_RELATION_DEPTH קשרים ברצף). */
  fieldOptions(rootKey: string): FieldOption[] {
    const root = this.entity(rootKey);
    if (!root) return [];
    const options: FieldOption[] = root.fields.map((f) => ({
      id: f.key, label: f.displayName, group: root.displayName, ref: { path: [], field: f.key }, type: f.type,
    }));
    this.collectRelated(root, [], [], options);
    return options;
  }

  private collectRelated(entity: CatalogEntity, path: string[], names: string[], out: FieldOption[]) {
    if (path.length >= MAX_RELATION_DEPTH) return;
    for (const rel of entity.relations) {
      // לא חוזרים על אותו קשר ברצף (למשל מנהל של מנהל)
      if (path[path.length - 1] === rel.key) continue;
      const target = this.entity(rel.targetEntity);
      if (!target) continue;
      const relPath = [...path, rel.key];
      const relNames = [...names, rel.displayName];
      for (const f of target.fields) {
        out.push({
          id: [...relPath, f.key].join('/'),
          label: [...relNames, f.displayName].join(' › '),
          group: `לפי ${relNames.join(' › ')}`,
          ref: { path: relPath, field: f.key },
          type: f.type,
        });
      }
      this.collectRelated(target, relPath, relNames, out);
    }
  }
}
