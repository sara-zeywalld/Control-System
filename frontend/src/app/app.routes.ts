import { Routes } from '@angular/router';
import { ControlsList } from './pages/controls-list/controls-list';
import { ControlEditor } from './pages/control-editor/control-editor';
import { ResultsPage } from './pages/results-page/results-page';
import { AiAssistant } from './pages/ai-assistant/ai-assistant';

export const routes: Routes = [
  { path: '', component: ControlsList, title: 'הבקרות שלי' },
  { path: 'controls/new', component: ControlEditor, title: 'בקרה חדשה' },
  { path: 'controls/:id', component: ControlEditor, title: 'עריכת בקרה' },
  { path: 'controls/:id/results', component: ResultsPage, title: 'תוצאות בקרה' },
  { path: 'ai', component: AiAssistant, title: 'יצירת בקרה בשפה חופשית' },
  { path: '**', redirectTo: '' },
];
