import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-alert',
  imports: [CommonModule],
  templateUrl: './alert.component.html',
  styleUrl: './alert.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class AlertComponent {
  readonly message = input.required<string>();
  readonly type = input<'error' | 'info'>('info');
}
