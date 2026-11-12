import { Component, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { ApiService, errorMessages } from '../../core/api.service';
import { CatalogStore } from '../../core/catalog.store';
import { DraftStore } from '../../core/draft.store';
import { ControlDefinition, RunResult, emptyDefinition } from '../../core/models';
import { Alert } from '../../shared/alert/alert';
import { ConditionGroup } from '../../shared/condition-group/condition-group';
import { ResultsTable } from '../../shared/results-table/results-table';

@Component({
  selector: 'app-control-editor',
  imports: [RouterLink, Alert, ConditionGroup, ResultsTable],
  templateUrl: './control-editor.html',
  styleUrl: './control-editor.css',
})
export class ControlEditor {
  private readonly api = inject(ApiService);
  private readonly router = inject(Router);
  protected readonly store = inject(CatalogStore);
  private readonly id = Number(inject(ActivatedRoute).snapshot.paramMap.get('id')) || null;

  protected readonly name = signal('');
  protected readonly description = signal('');
  protected readonly definition = signal<ControlDefinition | null>(null);
  protected readonly source = signal<'manual' | 'ai'>('manual');
  protected readonly errors = signal<string[]>([]);
  protected readonly preview = signal<RunResult | null>(null);
  protected readonly busy = signal(false);

  protected readonly isEdit = this.id !== null;
  protected readonly entityName = computed(() => {
    const d = this.definition();
    return d ? this.store.entity(d.rootEntity)?.displayName ?? '' : '';
  });

  constructor() {
    const draft = inject(DraftStore).take();
    if (draft) {
      this.name.set(draft.name);
      this.description.set(draft.description ?? '');
      this.definition.set(draft.definition);
      this.source.set(draft.source);
    } else if (this.id) {
      this.api.getControl(this.id).subscribe({
        next: (c) => {
          this.name.set(c.name);
          this.description.set(c.description ?? '');
          this.definition.set(c.definition);
          this.source.set(c.source);
        },
        error: (e) => this.errors.set(errorMessages(e)),
      });
    }
  }

  protected chooseEntity(key: string) {
    this.definition.set(emptyDefinition(key));
    this.preview.set(null);
  }

  protected touched() {
    this.preview.set(null);
    this.definition.update((d) => (d ? { ...d } : d));
  }

  protected runPreview() {
    const def = this.definition();
    if (!def) return;
    this.busy.set(true);
    this.errors.set([]);
    this.api.previewControl(def).subscribe({
      next: (r) => { this.preview.set(r); this.busy.set(false); },
      error: (e) => { this.errors.set(errorMessages(e)); this.busy.set(false); },
    });
  }

  protected save(thenRun: boolean) {
    const def = this.definition();
    if (!def) return;
    const input = { name: this.name(), description: this.description() || undefined, definition: def, source: this.source() };
    this.busy.set(true);
    this.errors.set([]);
    const call = this.id ? this.api.updateControl(this.id, input) : this.api.createControl(input);
    call.subscribe({
      next: (c) => this.router.navigate(thenRun ? ['/controls', c.id, 'results'] : ['/']),
      error: (e) => { this.errors.set(errorMessages(e)); this.busy.set(false); },
    });
  }
}
