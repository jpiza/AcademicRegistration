export interface Subject {
  id: string;
  code: string;
  name: string;
  credits: number;
  professorId: string;
  professorName: string;
}

export interface StudentSubject {
  subjectId: string;
  code: string;
  name: string;
  credits: number;
  professorId: string;
  professorName: string;
}

export interface StudentSummary {
  id: string;
  name: string;
  email: string;
  documentNumber: string;
  totalCredits: number;
  subjects: StudentSubject[];
}

export interface StudentDetails extends StudentSummary {
  createdAtUtc: string;
  updatedAtUtc: string | null;
}

export interface ClassmatesBySubject {
  subjectId: string;
  subjectName: string;
  professorName: string;
  classmateNames: string[];
}

export interface SaveStudentRequest {
  name: string;
  email: string;
  documentNumber: string;
  subjectIds: string[];
}

export interface UpdateStudentRequest extends SaveStudentRequest {
  studentId: string;
}
