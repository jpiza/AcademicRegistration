import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { CommonModule } from '@angular/common';

import { StudentSummary } from '../../models/registration.models';

@Component({
  selector: 'app-student-list',
  imports: [CommonModule],
  templateUrl: './student-list.component.html',
  styleUrl: './student-list.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class StudentListComponent {
  readonly students = input.required<StudentSummary[]>();
  readonly loading = input.required<boolean>();
  readonly activeStudentId = input<string | null>(null);

  readonly viewClassmates = output<string>();
  readonly editStudent = output<StudentSummary>();
  readonly deleteStudent = output<StudentSummary>();
}
