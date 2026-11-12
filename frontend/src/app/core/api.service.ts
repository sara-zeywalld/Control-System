import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import {
  AiSuggestion,
  Catalog,
  ControlDefinition,
  ControlInput,
  ControlView,
  RunResult,
} from './models';

const API = 'http://localhost:5230/api';

/** מחלץ הודעות שגיאה בעברית מתשובת השרת (או הודעה כללית אם השרת לא זמין). */
export function errorMessages(err: unknown): string[] {
  if (err instanceof HttpErrorResponse) {
    const errors = err.error?.errors;
    if (Array.isArray(errors) && errors.length) return errors;
    if (err.status === 0) return ['אין חיבור לשרת. ודאו ששרת ה-API פועל.'];
    if (err.error?.title) return [err.error.title];
  }
  return ['אירעה שגיאה לא צפויה'];
}

@Injectable({ providedIn: 'root' })
export class ApiService {
  private readonly http = inject(HttpClient);

  catalog() { return this.http.get<Catalog>(`${API}/catalog`); }

  listControls() { return this.http.get<ControlView[]>(`${API}/controls`); }
  getControl(id: number) { return this.http.get<ControlView>(`${API}/controls/${id}`); }
  createControl(input: ControlInput) { return this.http.post<ControlView>(`${API}/controls`, input); }
  updateControl(id: number, input: ControlInput) { return this.http.put<ControlView>(`${API}/controls/${id}`, input); }
  deleteControl(id: number) { return this.http.delete<void>(`${API}/controls/${id}`); }
  duplicateControl(id: number) { return this.http.post<ControlView>(`${API}/controls/${id}/duplicate`, {}); }

  runControl(id: number): Observable<RunResult> { return this.http.post<RunResult>(`${API}/controls/${id}/run`, {}); }
  previewControl(def: ControlDefinition): Observable<RunResult> { return this.http.post<RunResult>(`${API}/controls/preview`, def); }

  suggestControl(prompt: string): Observable<AiSuggestion> {
    return this.http.post<AiSuggestion>(`${API}/ai/suggest-control`, { prompt });
  }
  suggestProactive(): Observable<AiSuggestion[]> {
    return this.http.post<AiSuggestion[]>(`${API}/ai/suggest-proactive`, {});
  }
}
