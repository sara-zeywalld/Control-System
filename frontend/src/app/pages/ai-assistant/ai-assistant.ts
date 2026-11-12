import { Component, inject, signal } from '@angular/core';
import { JsonPipe } from '@angular/common';
import { Router } from '@angular/router';
import { ApiService, errorMessages } from '../../core/api.service';
import { DraftStore } from '../../core/draft.store';
import { AiSuggestion } from '../../core/models';
import { Alert } from '../../shared/alert/alert';

const EXAMPLES = [
  'מצא עובדים פעילים שגילם נמוך מ-18 ומשכורתם גבוהה מ-15,000 ש"ח',
  'עובדים שתאריך סיום העסקתם מוקדם מתאריך תחילת העסקתם',
  'עובדים פעילים המשויכים למחלקה שאינה פעילה',
  'עובדים שחסר להם אימייל',
];

@Component({
  selector: 'app-ai-assistant',
  imports: [Alert, JsonPipe],
  templateUrl: './ai-assistant.html',
  styleUrl: './ai-assistant.css',
})
export class AiAssistant {
  private readonly api = inject(ApiService);
  private readonly router = inject(Router);
  private readonly drafts = inject(DraftStore);

  protected readonly examples = EXAMPLES;
  protected readonly prompt = signal('');
  protected readonly suggestion = signal<AiSuggestion | null>(null);
  protected readonly proactive = signal<AiSuggestion[]>([]);
  protected readonly busy = signal(false);
  protected readonly errors = signal<string[]>([]);
  protected readonly showJson = signal(false);

  protected suggest() {
    if (!this.prompt().trim()) return;
    this.busy.set(true);
    this.errors.set([]);
    this.api.suggestControl(this.prompt()).subscribe({
      next: (s) => { this.suggestion.set(s); this.busy.set(false); },
      error: (e) => { this.errors.set(errorMessages(e)); this.busy.set(false); },
    });
  }

  protected loadProactive() {
    this.busy.set(true);
    this.errors.set([]);
    this.api.suggestProactive().subscribe({
      next: (list) => { this.proactive.set(list); this.busy.set(false); },
      error: (e) => { this.errors.set(errorMessages(e)); this.busy.set(false); },
    });
  }

  protected select(s: AiSuggestion) { this.suggestion.set(s); }
  protected cancel() { this.suggestion.set(null); }

  protected approve(s: AiSuggestion, thenRun: boolean) {
    this.busy.set(true);
    this.api.createControl({ name: s.name, description: s.description, definition: s.definition, source: 'ai' }).subscribe({
      next: (c) => this.router.navigate(thenRun ? ['/controls', c.id, 'results'] : ['/']),
      error: (e) => { this.errors.set(errorMessages(e)); this.busy.set(false); },
    });
  }

  protected edit(s: AiSuggestion) {
    this.drafts.draft.set({ name: s.name, description: s.description, definition: s.definition, source: 'ai' });
    this.router.navigate(['/controls/new']);
  }
}
