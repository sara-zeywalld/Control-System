import { Component, computed, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { Router, RouterLink } from '@angular/router';
import { ApiService, errorMessages } from '../../core/api.service';
import { ControlView } from '../../core/models';
import { Alert } from '../../shared/alert/alert';

@Component({
  selector: 'app-controls-list',
  imports: [RouterLink, DatePipe, Alert],
  templateUrl: './controls-list.html',
  styleUrl: './controls-list.css',
})
export class ControlsList {
  private readonly api = inject(ApiService);
  private readonly router = inject(Router);

  protected readonly controls = signal<ControlView[]>([]);
  protected readonly loading = signal(true);
  protected readonly errors = signal<string[]>([]);
  protected readonly search = signal('');

  protected readonly filtered = computed(() => {
    const q = this.search().trim().toLowerCase();
    return this.controls().filter((c) =>
      !q || [c.name, c.description, c.rootEntityName, c.summary].some((s) => s?.toLowerCase().includes(q)));
  });

  constructor() { this.reload(); }

  private reload() {
    this.loading.set(true);
    this.api.listControls().subscribe({
      next: (list) => { this.controls.set(list); this.loading.set(false); },
      error: (e) => { this.errors.set(errorMessages(e)); this.loading.set(false); },
    });
  }

  protected run(c: ControlView) { this.router.navigate(['/controls', c.id, 'results']); }

  protected duplicate(c: ControlView) {
    this.api.duplicateControl(c.id).subscribe({
      next: () => this.reload(),
      error: (e) => this.errors.set(errorMessages(e)),
    });
  }

  protected remove(c: ControlView) {
    if (!confirm(`למחוק את הבקרה "${c.name}"?`)) return;
    this.api.deleteControl(c.id).subscribe({
      next: () => this.reload(),
      error: (e) => this.errors.set(errorMessages(e)),
    });
  }
}
