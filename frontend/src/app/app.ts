import { ChangeDetectionStrategy, Component, computed, effect, inject, signal } from '@angular/core';

import { StudentSummary } from './models/registration.models';
import { RegistrationStore } from './services/registration-store';
import { SaveStudentRequest } from './models/registration.models';

import { HeaderComponent } from './components/header/header.component';
import { MetricsComponent } from './components/metrics/metrics.component';
import { AlertComponent } from './components/alert/alert.component';
import { StudentFormComponent } from './components/student-form/student-form.component';
import { StudentListComponent } from './components/student-list/student-list.component';
import { ClassmatesComponent } from './components/classmates/classmates.component';

@Component({
  selector: 'app-root',
  imports: [
    HeaderComponent,
    MetricsComponent,
    AlertComponent,
    StudentFormComponent,
    StudentListComponent,
    ClassmatesComponent
  ],
  templateUrl: './app.html',
  styleUrl: './app.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class AppComponent {
  readonly store = inject(RegistrationStore);

  readonly editingStudentId = signal<string | null>(null);
  readonly activeStudentId = signal<string | null>(null);
  readonly selectedSubjectIds = signal<string[]>([]);
  readonly statusMessage = signal<string | null>(null);

  readonly editingStudentData = computed(() => {
    const editingId = this.editingStudentId();
    if (!editingId) return null;
    const student = this.store.students().find((s) => s.id === editingId);
    if (!student) return null;
    return {
      name: student.name,
      email: student.email,
      documentNumber: student.documentNumber
    };
  });

  constructor() {
    void this.store.loadInitial();

    effect(() => {
      const studentId = this.activeStudentId();
      this.store.setActiveStudentId(studentId);

      if (studentId) {
        void this.store.loadClassmates(studentId);
      } else {
        this.store.clearClassmates();
      }
    });
  }

  async handleFormSubmit(request: SaveStudentRequest): Promise<void> {
    const editingId = this.editingStudentId();
    const saved = editingId
      ? await this.store.updateStudent(editingId, request)
      : await this.store.createStudent(request);

    if (saved) {
      this.resetForm();
      this.activeStudentId.set(typeof saved === 'string' ? saved : editingId);
      this.statusMessage.set(editingId ? 'Registro actualizado.' : 'Registro creado.');
    }
  }

  editStudent(student: StudentSummary): void {
    this.editingStudentId.set(student.id);
    this.selectedSubjectIds.set(student.subjects.map((subject) => subject.subjectId));
    this.activeStudentId.set(student.id);
    this.statusMessage.set('Editando registro.');
  }

  async deleteStudent(student: StudentSummary): Promise<void> {
    await this.store.deleteStudent(student.id);

    if (this.activeStudentId() === student.id) {
      this.activeStudentId.set(null);
    }

    if (this.editingStudentId() === student.id) {
      this.resetForm();
    }
  }

  viewClassmates(studentId: string): void {
    this.activeStudentId.set(studentId);
  }

  resetForm(): void {
    this.editingStudentId.set(null);
    this.selectedSubjectIds.set([]);
    this.statusMessage.set(null);
  }

}
