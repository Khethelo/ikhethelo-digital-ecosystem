import {
  AbstractControl,
  FormGroup,
  ValidationErrors,
  ValidatorFn,
  Validators,
} from "@angular/forms";
import { HttpErrorResponse } from "@angular/common/http";
import { Problem } from "./models";
export const requiredText: ValidatorFn = (control) =>
  typeof control.value === "string" && control.value.trim()
    ? null
    : { required: true };
export function maxText(max: number): ValidatorFn {
  return (control) =>
    typeof control.value === "string" && [...control.value].length > max
      ? { maxlength: { requiredLength: max } }
      : null;
}
export const registration: ValidatorFn = (control) =>
  control.get("mkmvaNumber")?.value?.trim() ||
  control.get("dmvNumber")?.value?.trim()
    ? null
    : { registration: true };
export function numeric(max: number, places: number, min = 0): ValidatorFn {
  return (control) => {
    const value = control.value;
    if (value === "" || value === null || value === undefined) return null;
    if (
      typeof value !== "number" ||
      !Number.isFinite(value) ||
      value < min ||
      value > max
    )
      return { range: true };
    const fraction = String(value).split(".")[1]?.length ?? 0;
    return fraction > places || /e-/i.test(String(value))
      ? { precision: { places } }
      : null;
  };
}
export const calendarDate: ValidatorFn = (control) => {
  const value = control.value;
  if (!value) return null;
  if (!/^\d{4}-\d{2}-\d{2}$/.test(value)) return { date: true };
  const [year, month, day] = value.split("-").map(Number);
  const date = new Date(0);
  date.setUTCFullYear(year, month - 1, day);
  date.setUTCHours(0, 0, 0, 0);
  return year >= 1 &&
    year <= 9999 &&
    date.getUTCFullYear() === year &&
    date.getUTCMonth() === month - 1 &&
    date.getUTCDate() === day
    ? null
    : { date: true };
};
export function errorText(control: AbstractControl | null): string {
  const errors = control?.errors;
  if (!errors || !control?.touched) return "";
  if (errors["server"]) return errors["server"];
  if (errors["required"]) return "This field is required.";
  if (errors["maxlength"])
    return `Use at most ${errors["maxlength"].requiredLength} characters.`;
  if (errors["minlength"])
    return `Use at least ${errors["minlength"].requiredLength} characters.`;
  if (errors["email"]) return "Enter a valid email address.";
  if (errors["range"]) return "Enter a number within the allowed range.";
  if (errors["precision"])
    return `Use at most ${errors["precision"].places} decimal places.`;
  if (errors["date"]) return "Enter a valid calendar date.";
  return "Check this value.";
}
export interface Issue {
  message: string;
  traceId?: string;
  details: string[];
  status: number;
}
export function issueOf(
  error: unknown,
  conflict = "This conflicts with an existing record. Check the details and try again.",
): Issue {
  const response = error instanceof HttpErrorResponse ? error : null;
  const body = response?.error as Problem | null;
  const status = response?.status ?? 0;
  const message =
    status === 409
      ? conflict
      : status === 403
        ? "This account cannot access this area. Contact your NTM administrator."
        : status === 404
          ? "This record could not be found in your tenant."
          : status === 401
            ? "Sign in with an active administrator account."
            : status === 413
              ? "This record is too large. Shorten the entered descriptions before saving."
              : status === 429
                ? "Too many attempts. Wait a moment and try again."
                : status === 422 || status === 400
                  ? "Check the highlighted fields and try again."
                  : "The service could not complete this request. Try again.";
  return {
    message,
    status,
    traceId: body?.traceId,
    details: body?.errors ? Object.values(body.errors).flat() : [],
  };
}
export function applyErrors(form: FormGroup, error: unknown) {
  const errors =
    error instanceof HttpErrorResponse
      ? (error.error as Problem)?.errors
      : null;
  for (const [name, messages] of Object.entries(errors ?? {})) {
    const field = form.get(name);
    if (field) {
      field.setErrors({ ...field.errors, server: messages.join(" ") });
      field.markAsTouched();
    }
  }
}
export const adminPassword = [
  Validators.required,
  (control: AbstractControl) => [...(control.value ?? "")].length >= 8 ? null : { passwordLength: true },
  (control: AbstractControl) => /[0-9]/.test(control.value ?? "") ? null : { passwordDigit: true },
  (control: AbstractControl) => /[^\p{L}\p{N}\p{White_Space}]/u.test(control.value ?? "") ? null : { passwordSpecial: true },
  maxText(128),
];
