import { Injectable, signal } from '@angular/core';
import { ControlDefinition } from './models';

export interface ControlDraft {
  name: string;
  description?: string;
  definition: ControlDefinition;
  source: 'manual' | 'ai';
}

/** העברת טיוטת בקרה בין מסכים (למשל מהצעת AI אל העורך הידני). */
@Injectable({ providedIn: 'root' })
export class DraftStore {
  readonly draft = signal<ControlDraft | null>(null);

  take(): ControlDraft | null {
    const d = this.draft();
    this.draft.set(null);
    return d;
  }
}
