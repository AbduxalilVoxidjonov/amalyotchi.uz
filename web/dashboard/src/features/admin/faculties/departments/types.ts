/** `GET /faculties/{id}/departments` qatori (hierarchy-contract.md). */
export interface DepartmentRow {
  id: string;
  name: string;
  code: string;
  isActive: boolean;
  directions: number;
  groups: number;
  students: number;
}

/** `GET/POST/PUT/PATCH /departments/{id}` javobi (breadcrumb: `facultyName`). */
export interface DepartmentDto {
  id: string;
  facultyId: string;
  facultyName: string;
  name: string;
  code: string;
  isActive: boolean;
}

/** `POST/PUT /departments` body'si. */
export interface DepartmentInput {
  name: string;
  code: string;
}
