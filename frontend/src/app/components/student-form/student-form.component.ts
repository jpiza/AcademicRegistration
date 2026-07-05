import { ChangeDetectionStrategy, Component, computed, effect, inject, input, model, output } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { CommonModule } from '@angular/common';

import { Subject } from '../../models/registration.models';

@Component({
  selector: 'app-student-form',
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './student-form.component.html',
  styleUrl: './student-form.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class StudentFormComponent {
  private readonly formBuilder = inject(FormBuilder);

  readonly subjects = input.required<Subject[]>();
  readonly editing = input<boolean>(false);
  readonly saving = input<boolean>(false);
  readonly studentData = input<{ name: string; email: string; documentNumber: string } | null>(null);

  readonly selectedSubjectIds = model<string[]>([]);
  readonly statusMessage = model<string | null>(null);

  readonly form = this.formBuilder.nonNullable.group({
    name: ['', [Validators.required, Validators.minLength(3), Validators.maxLength(120)]],
    email: ['', [Validators.required, Validators.email]],
    documentNumber: ['', [Validators.required, Validators.pattern(/^[A-Za-z0-9-]{5,20}$/)]]
  });

  private readonly formValue = toSignal(this.form.valueChanges, { initialValue: this.form.getRawValue() });

  readonly selectedSubjects = computed(() => {
    const selectedIds = new Set(this.selectedSubjectIds());
    return this.subjects().filter((subject) => selectedIds.has(subject.id));
  });

  readonly totalCredits = computed(() => this.selectedSubjects().reduce((sum, subject) => sum + subject.credits, 0));
  readonly selectedProfessorIds = computed(() => new Set(this.selectedSubjects().map((subject) => subject.professorId)));

  readonly canSubmit = computed(() => {
    this.formValue();
    return this.form.valid && this.selectedSubjectIds().length === 3 && !this.saving();
  });

  constructor() {
    effect(() => {
      const data = this.studentData();
      if (data) {
        this.form.setValue(data);
      }
    });
  }

  isSelected(subjectId: string): boolean {
    return this.selectedSubjectIds().includes(subjectId);
  }

  isBlocked(subject: Subject): boolean {
    return !this.isSelected(subject.id)
      && (this.selectedSubjectIds().length >= 3 || this.selectedProfessorIds().has(subject.professorId));
  }

  toggleSubject(subject: Subject): void {
    const selectedIds = this.selectedSubjectIds();

    if (selectedIds.includes(subject.id)) {
      this.selectedSubjectIds.set(selectedIds.filter((id) => id !== subject.id));
      this.statusMessage.set(null);
      return;
    }

    if (selectedIds.length >= 3) {
      this.statusMessage.set('Solo puedes seleccionar 3 materias.');
      return;
    }

    if (this.selectedProfessorIds().has(subject.professorId)) {
      this.statusMessage.set('No puedes repetir profesor.');
      return;
    }

    this.selectedSubjectIds.set([...selectedIds, subject.id]);
    this.statusMessage.set(null);
  }

  submit(): void {
    this.form.markAllAsTouched();

    if (!this.canSubmit()) {
      this.statusMessage.set('Completa los datos y selecciona 3 materias validas.');
      return;
    }

    const request = {
      ...this.form.getRawValue(),
      subjectIds: this.selectedSubjectIds()
    };

    this.submitForm.emit(request);
  }

  reset(): void {
    this.form.reset();
    this.selectedSubjectIds.set([]);
    this.statusMessage.set(null);
    this.resetForm.emit();
  }

  subjectStateLabel(subject: Subject): string {
    if (this.isSelected(subject.id)) {
      return 'Seleccionada';
    }

    if (this.isBlocked(subject)) {
      return this.selectedProfessorIds().has(subject.professorId) ? 'Profesor ocupado' : 'Cupo completo';
    }

    return 'Disponible';
  }

  readonly submitForm = output<{ name: string; email: string; documentNumber: string; subjectIds: string[] }>();
  readonly resetForm = output<void>();
}
