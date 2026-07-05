import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-metrics',
  imports: [CommonModule],
  templateUrl: './metrics.component.html',
  styleUrl: './metrics.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class MetricsComponent {
  readonly studentCount = input.required<number>();
  readonly subjectCount = input.required<number>();
  readonly professorCount = input.required<number>();
  readonly registrationCount = input.required<number>();
}
