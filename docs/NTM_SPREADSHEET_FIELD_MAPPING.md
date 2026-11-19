# NTM spreadsheet field mapping

Structural mapping verified on 12 November 2026 against `InnoBiz_DUT_MK_Veterans_Needs_Assessment (1).xlsx`, supplied from Downloads. SHA-256: `1C0600A961D3FDFF836800FC779071CDF22CACC9A652864A6B3094E8DAB2D727`. The workbook and its personal data have not been copied into the repository.

Source `Needs Assessment!A11:T11` contains the exact 20 headings below. Records occupy rows 12–114 (103 rows). Rows 117–120 contain summary formulas, not records. `Instructions` documents field meanings; it does not override the requested admin-only scope. Spreadsheet formulas/instructions were not executed.

`V` means NtmVeteran, `B` means NtmBusiness. UUIDs, tenant ownership and UTC timestamps are server-generated technical fields. No source data has been imported.

| Column | Exact source heading | Source type / intended meaning | Destination | Required / validation | Transformation / preservation |
| --- | --- | --- | --- | --- | --- |
| A | No. | Numeric sequential tracker | B.SourceRecordNumber: nullable integer | Positive if supplied | Retain number; not a PK, unique key or identity resolver |
| B | Date of Assessment | Text dates | B.AssessmentDate: nullable DateOnly | Valid date if supplied | Explicit `yyyy-MM-dd` and observed `yyyy.MM.dd` formats; no locale guessing or timezone shift |
| C | Business Owner Name & Surname | Text | V.FullName: varchar(200) | Required, nonblank | Trim; do not guess name parts |
| D | MKMVA / Veteran Membership No. (if applicable) | Combined text references and generic DMV markers | V.MembershipReference: nullable varchar(100); separately verified V.MkmvaNumber / V.DmvNumber | Source reference optional/nonunique; API requires an actual MKMVA or DMV number | Retain original reference verbatim. Do not classify unlabeled digits or use generic DMV markers as unique numbers. Preserve leading zeros/case; typed registrations have tenant-scoped normalized uniqueness |
| E | Contact Number | Text, including non-phone content | V.ContactNumber: nullable varchar(50) | Length only | Retain text/leading zeros; trim outer whitespace, no numeric or E.164 coercion |
| F | Email Address | Text; 12 cells lack email shape | V.EmailAddress: nullable varchar(320) | Length only for recorded source | Retain original content, trim outer whitespace; no authentication or automatic email uses this field. Corrections are explicit |
| G | Name of Business | Text | B.BusinessName: varchar(200) | Required for a business, nonblank | Trim; a veteran may exist without a business |
| H | Business Registration Number (CIPC) | Text | B.CipcRegistrationNumber: nullable varchar(100) | Length only | Preserve text and leading zeros; no invented uniqueness rule |
| I | Business Location (Address / Area) | Text | B.BusinessAddress: nullable text | Optional | Preserve full address/area; no inferred geography |
| J | Type of Business / Sector | Text, including compound descriptions | B.Sector: nullable varchar(200) | Optional, max 200 | Preserve whole value; no invented category splits or cell shifts |
| K | Short Background of Business (what it does) | Descriptive text | B.BusinessBackground: nullable text | Optional | Preserve description |
| L | Operational Status | Intended three-state text; actual typos/numbers/descriptions | B.OperationalStatusSource: nullable text; B.OperationalStatus: nullable varchar(30) | Typed values: Operational, Partially Operational, Not Operational | Raw text verbatim; typed status only from unambiguous declared value or explicit correction. Never infer a status from `4`, `Operationl` or `Micro business venture` |
| M | Number of Employees | All populated cells are text: counts and qualifiers | B.NumberOfEmployeesSource: nullable text; B.NumberOfEmployees: nullable integer | Typed count nonnegative; Instructions includes owner | Raw text verbatim. Only wholly numeric unambiguous counts become typed values; retain `3 part time`, `5 Part time`, `7 Part time`, `Operational` without coercion |
| N | Years in Operation | Text full-year counts, descriptive content and blanks | B.YearsInOperationSource: nullable text; B.YearsInOperation: nullable numeric(6,1) | Typed 0–99999.9; max one decimal place | Raw text verbatim; do not convert `Needs compliance, equipment and training` to zero or infer a start date |
| O | Current Business Need(s) | Free text | B.CurrentBusinessNeeds: nullable text | Optional | Preserve descriptions; no separate boolean equipment/training/marketing/monitoring fields |
| P | Priority Need (select one) | Text | B.PriorityNeed: nullable text | Optional | Preserve whole description; no invented taxonomy |
| Q | Funding Required? | Intended Yes/No; also descriptions and a number | B.FundingRequiredSource: nullable text; B.FundingRequired: nullable boolean | True/false/unknown | Raw text verbatim. Only explicit Yes/No maps to typed bool. `Funding`, `400000`, `financial management and bookkeeping` remain unresolved/null; do not shift them to another column |
| R | Estimated Funding Amount Needed (R) | Numeric, Rand display format, includes zero | B.FundingAmount: nullable numeric(15,2) | 0–9999999999999.99; max two decimal places | Currency is South African Rand. Preserve zero versus missing/null; no silent rounding, scaling or derivation from Q |
| S | Additional Comments / Notes | Text, one blank | B.AdditionalComments: nullable text | Optional | Preserve description |
| T | Assessed By (InnoBiz DUT Officer) | Text, only three populated cells | B.AssessedBy: nullable varchar(200) | Optional | Preserve text; no inferred account FK or filling missing names |

## Data-quality checkpoint

Headings and intended representations are verified; the populated workbook is **not clean import-ready data**. It contains generic membership references, non-email values, qualified employee counts, status typos and apparently misplaced content. Typed status/funding aggregates exclude unresolved raw values. Application services never parse raw strings into typed values automatically.

Generic references include `DMV`, `DMV Database`, `DMV Reg`, `DMV Registered`, `on the DMV Database`. These do not satisfy the requirement for an actual MKMVA/DMV number. Affected records need source-owner identity reconciliation before creation. Repeated veteran references must also be reconciled against the confirmed one-business rule; never fabricate identities or auto-merge records to force an import.

Future import requires preview/error reporting, explicit correction of ambiguous values, identity/duplicate checks and source preservation. No import/export is approved or implemented here. Summary formulas/cached totals are not dashboard fixtures; dashboard values come from saved authorized-tenant records.

## SQL reconciliation

The supplied SQL is retained at `database/ntm-greenpulse-supplied-reference.sql`, SHA-256 `5B74B966FA36593DA471F4B349C9E87D306CB9BE9E51545154D7C5CDC715E8F9`. It was read, not executed. Use EF migrations for deployment; do not execute both equivalent reference SQL and migrations.

The model preserves the SQL's tenant-safe FK, one-business uniqueness, typed nullable fields and checks. It aligns status varchar(30), years numeric(6,1), funding numeric(15,2), and name/assessment-date indexes. Workbook evidence requires the added email/reference/source-text/record-number fields. Existing generated normalized registration columns enforce equivalent case-insensitive uniqueness to the SQL expression indexes; existing constraint names remain stable.

Timestamp triggers are intentionally omitted to follow the current application's TimeProvider/explicit-timestamp convention and atomic audits. Direct operational SQL must explicitly update timestamps. No new authentication/audit tables are introduced. All migration application remains a deployment action; verification uses only an isolated test cluster and fixture-owned random databases.
