/** `GET /departments/{id}/directions` qatori (hierarchy-contract.md). */
export interface DirectionRow {
  id: string;
  name: string;
  code: string;
  isActive: boolean;
  groups: number;
  students: number;
}

/** `GET/POST/PUT/PATCH /directions/{id}` javobi (breadcrumb: `departmentName`/`facultyName`). */
export interface DirectionDto {
  id: string;
  departmentId: string;
  departmentName: string;
  facultyId: string;
  facultyName: string;
  name: string;
  code: string;
  isActive: boolean;
}

/** `POST/PUT /directions` body'si. */
export interface DirectionInput {
  name: string;
  code: string;
}
