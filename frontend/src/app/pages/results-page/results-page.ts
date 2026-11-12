import { Component, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { ApiService, errorMessages } from '../../core/api.service';
import { ControlView, RunResult } from '../../core/models';
import { Alert } from '../../shared/alert/alert';
import { ResultsTable } from '../../shared/results-table/results-table';

@Component({
  selector: 'app-results-page',
  imports: [RouterLink, DatePipe, Alert, ResultsTable],
  templateUrl: './results-page.html',
  styleUrl: './results-page.css',
})
export class ResultsPage {
  private readonly api = inject(ApiService);
  private readonly id = Number(inject(ActivatedRoute).snapshot.paramMap.get('id'));

  protected readonly control = signal<ControlView | null>(null);
  protected readonly result = signal<RunResult | null>(null);
  protected readonly running = signal(false);
  protected readonly errors = signal<string[]>([]);
  protected readonly showSql = signal(false);

  constructor() {
    this.api.getControl(this.id).subscribe({
      next: (c) => this.control.set(c),
      error: (e) => this.errors.set(errorMessages(e)),
    });
    this.run();
  }

  protected run() {
    this.running.set(true);
    this.errors.set([]);
    this.api.runControl(this.id).subscribe({
      next: (r) => { this.result.set(r); this.running.set(false); },
      error: (e) => { this.errors.set(errorMessages(e)); this.result.set(null); this.running.set(false); },
    });
  }
}
