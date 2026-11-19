export interface Page<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
}
export type OperationalStatus =
  "Operational" | "Partially Operational" | "Not Operational";
export const STATUSES: OperationalStatus[] = [
  "Operational",
  "Partially Operational",
  "Not Operational",
];
export interface VeteranWrite {
  fullName: string;
  mkmvaNumber?: string | null;
  dmvNumber?: string | null;
  contactNumber?: string | null;
  emailAddress?: string | null;
  membershipReference?: string | null;
}
export interface Veteran {
  id: string;
  fullName: string;
  mkmvaNumber: string | null;
  dmvNumber: string | null;
  contactNumber: string | null;
  emailAddress: string | null;
  membershipReference: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
  business: { id: string; businessName: string } | null;
}
export interface BusinessWrite {
  veteranId: string;
  businessName: string;
  cipcRegistrationNumber?: string | null;
  businessAddress?: string | null;
  sector?: string | null;
  businessBackground?: string | null;
  operationalStatus?: OperationalStatus | null;
  numberOfEmployees?: number | null;
  yearsInOperation?: number | null;
  currentBusinessNeeds?: string | null;
  priorityNeed?: string | null;
  fundingRequired?: boolean | null;
  fundingAmount?: number | null;
  additionalComments?: string | null;
  assessmentDate?: string | null;
  assessedBy?: string | null;
}
export interface Business {
  id: string;
  veteranId: string;
  businessName: string;
  cipcRegistrationNumber: string | null;
  businessAddress: string | null;
  sector: string | null;
  businessBackground: string | null;
  operationalStatus: OperationalStatus | null;
  numberOfEmployees: number | null;
  yearsInOperation: number | null;
  currentBusinessNeeds: string | null;
  priorityNeed: string | null;
  fundingRequired: boolean | null;
  fundingAmount: number | null;
  additionalComments: string | null;
  assessmentDate: string | null;
  assessedBy: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
}
export interface Count {
  value: string;
  count: number;
}
export interface Dashboard {
  totalVeterans: number;
  totalBusinesses: number;
  operationalBusinesses: number;
  partiallyOperationalBusinesses: number;
  notOperationalBusinesses: number;
  totalRecordedFundingAmount: number;
  fundingRequiredBusinesses: number;
  businessesBySector: Count[];
  recordedPriorityNeeds: Count[];
}
export interface Query {
  page?: number;
  pageSize?: number;
  search?: string;
  hasBusiness?: boolean;
  sector?: string;
  operationalStatus?: string;
  veteranId?: string;
  isActive?: boolean;
}
export interface Administrator {
  id: string;
  email: string;
  role: string;
  isActive: boolean;
  createdAtUtc: string;
  updatedAtUtc: string;
  lastLoginAtUtc: string | null;
}
export interface Problem {
  status?: number;
  title?: string;
  errors?: Record<string, string[]>;
  traceId?: string;
}
