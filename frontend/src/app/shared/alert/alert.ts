import { Component, input } from '@angular/core';

@Component({
  selector: 'app-alert',
  templateUrl: './alert.html',
  styleUrl: './alert.css',
})
export class Alert {
  readonly messages = input.required<string[]>();
  readonly kind = input<'error' | 'warning' | 'info'>('error');
  readonly title = input<string>('');
}
