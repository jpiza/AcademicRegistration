import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { CommonModule } from '@angular/common';

import { ClassmatesBySubject, StudentSummary } from '../../models/registration.models';

@Component({
  selector: 'app-classmates',
  imports: [CommonModule],
  templateUrl: './classmates.component.html',
  styleUrl: './classmates.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ClassmatesComponent {
  readonly classmates = input.required<ClassmatesBySubject[]>();
  readonly students = input.required<StudentSummary[]>();
  readonly activeStudentId = input<string | null>(null);

  readonly activeStudent = computed(() => {
    const activeId = this.activeStudentId();
    return this.students().find((student) => student.id === activeId) ?? null;
  });
}
