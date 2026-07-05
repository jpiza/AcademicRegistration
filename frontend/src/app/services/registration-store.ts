import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { computed, inject, Injectable, signal } from '@angular/core';
import { firstValueFrom, forkJoin } from 'rxjs';

import {
  ClassmatesBySubject,
  SaveStudentRequest,
  StudentSummary,
  Subject
} from '../models/registration.models';

@Injectable({ providedIn: 'root' })
export class RegistrationStore {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = '/api';

  private readonly subjectsState = signal<Subject[]>([]);
  private readonly studentsState = signal<StudentSummary[]>([]);
  private readonly classmatesState = signal<ClassmatesBySubject[]>([]);
  private readonly activeStudentIdState = signal<string | null>(null);
  private readonly loadingState = signal(false);
  private readonly savingState = signal(false);
  private readonly errorState = signal<string | null>(null);

  readonly subjects = this.subjectsState.asReadonly();
  readonly students = this.studentsState.asReadonly();
  readonly classmates = this.classmatesState.asReadonly();
  readonly loading = this.loadingState.asReadonly();
  readonly saving = this.savingState.asReadonly();
  readonly error = this.errorState.asReadonly();

  readonly professorCount = computed(() => new Set(this.subjects().map((subject) => subject.professorId)).size);
  readonly registrationCount = computed(() => this.students().reduce((sum, student) => sum + student.subjects.length, 0));

  async loadInitial(): Promise<void> {
    this.loadingState.set(true);
    this.errorState.set(null);

    try {
      const [subjects, students] = await firstValueFrom(
        forkJoin([
          this.http.get<Subject[]>(`${this.apiUrl}/subjects`),
          this.http.get<StudentSummary[]>(`${this.apiUrl}/students`)
        ])
      );

      this.subjectsState.set(subjects);
      this.studentsState.set(students);
    } catch (error) {
      this.errorState.set(this.describeError(error));
    } finally {
      this.loadingState.set(false);
    }
  }

  async refreshStudents(): Promise<void> {
    this.errorState.set(null);

    try {
      const students = await firstValueFrom(this.http.get<StudentSummary[]>(`${this.apiUrl}/students`));
      this.studentsState.set(students);
    } catch (error) {
      this.errorState.set(this.describeError(error));
    }
  }

  async createStudent(request: SaveStudentRequest): Promise<string | null> {
    this.savingState.set(true);
    this.errorState.set(null);

    try {
      const response = await firstValueFrom(this.http.post<{ id: string }>(`${this.apiUrl}/students`, request));
      await this.refreshStudents();
      return response.id;
    } catch (error) {
      this.errorState.set(this.describeError(error));
      return null;
    } finally {
      this.savingState.set(false);
    }
  }

  async updateStudent(studentId: string, request: SaveStudentRequest): Promise<boolean> {
    this.savingState.set(true);
    this.errorState.set(null);

    try {
      await firstValueFrom(this.http.put<void>(`${this.apiUrl}/students/${studentId}`, request));
      await this.refreshStudents();
      
      // Recargar compañeros si el estudiante actualizado es el activo
      if (this.activeStudentIdState() === studentId) {
        await this.loadClassmates(studentId);
      }
      
      return true;
    } catch (error) {
      this.errorState.set(this.describeError(error));
      return false;
    } finally {
      this.savingState.set(false);
    }
  }

  async deleteStudent(studentId: string): Promise<void> {
    this.savingState.set(true);
    this.errorState.set(null);

    try {
      await firstValueFrom(this.http.delete<void>(`${this.apiUrl}/students/${studentId}`));
      this.studentsState.update((students) => students.filter((student) => student.id !== studentId));
      this.classmatesState.set([]);
    } catch (error) {
      this.errorState.set(this.describeError(error));
    } finally {
      this.savingState.set(false);
    }
  }

  async loadClassmates(studentId: string): Promise<void> {
    this.errorState.set(null);

    try {
      const classmates = await firstValueFrom(
        this.http.get<ClassmatesBySubject[]>(`${this.apiUrl}/students/${studentId}/classmates`)
      );
      this.classmatesState.set(classmates);
    } catch (error) {
      this.classmatesState.set([]);
      this.errorState.set(this.describeError(error));
    }
  }

  clearClassmates(): void {
    this.classmatesState.set([]);
  }

  setActiveStudentId(studentId: string | null): void {
    this.activeStudentIdState.set(studentId);
  }

  private describeError(error: unknown): string {
    if (error instanceof HttpErrorResponse) {
      if (typeof error.error?.title === 'string') {
        return error.error.title;
      }

      if (error.status === 0) {
        return 'No se pudo conectar con el API.';
      }

      return `Error ${error.status}: ${error.statusText}`;
    }

    return 'Ocurrio un error inesperado.';
  }
}
