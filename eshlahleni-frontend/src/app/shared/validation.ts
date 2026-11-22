import { AbstractControl, ValidationErrors, ValidatorFn } from '@angular/forms';

export function johannesburgToday(now = new Date()): string {
  const parts = new Intl.DateTimeFormat('en-CA', { timeZone: 'Africa/Johannesburg', year: 'numeric', month: '2-digit', day: '2-digit' }).formatToParts(now);
  const part = (type: string) => parts.find(value => value.type === type)!.value;
  return `${part('year')}-${part('month')}-${part('day')}`;
}
export function calendarDate(value: string): { year: number; month: number; day: number } | null {
  if (value.length !== 10 || !/^\d{4}-\d{2}-\d{2}$/.test(value)) return null;
  const [year, month, day] = value.split('-').map(Number);
  if (year < 1 || year > 9999) return null;
  const date = new Date(0); date.setUTCFullYear(year, month - 1, day); date.setUTCHours(0, 0, 0, 0);
  return date.getUTCFullYear() === year && date.getUTCMonth() === month - 1 && date.getUTCDate() === day ? { year, month, day } : null;
}
function anniversary(birth: { year: number; month: number; day: number }, year: number): string {
  const leap = year % 4 === 0 && (year % 100 !== 0 || year % 400 === 0);
  const day = birth.month === 2 && birth.day === 29 && !leap ? 28 : birth.day;
  return `${String(year).padStart(4, '0')}-${String(birth.month).padStart(2, '0')}-${String(day).padStart(2, '0')}`;
}
export function isAdult(value: string, today = johannesburgToday()): boolean {
  const birth = calendarDate(value);
  return !!birth && value <= today && anniversary(birth, birth.year + 18) <= today;
}
export function ageInYears(value: string, today = johannesburgToday()): number | null {
  const birth = calendarDate(value); const current = calendarDate(today);
  if (!birth || !current || value > today) return null;
  const years = current.year - birth.year;
  return years - (anniversary(birth, current.year) > today ? 1 : 0);
}
export const trimmedName: ValidatorFn = (control: AbstractControl): ValidationErrors | null => {
  const length = Array.from(String(control.value ?? '').trim()).length;
  return length >= 2 && length <= 100 ? null : { name: true };
};
export const emailAddress: ValidatorFn = (control: AbstractControl): ValidationErrors | null => {
  const value = String(control.value ?? '').trim();
  return value.length > 0 && Array.from(value).length <= 320 && Array.from(value.toUpperCase()).length <= 320 && /^[^\s@<>]+@[^\s@<>]+$/.test(value)
    ? null : { email: true };
};
export const canonicalPhone: ValidatorFn = (control: AbstractControl): ValidationErrors | null => {
  const value = String(control.value ?? '');
  return /^(?:0[1-9][0-9]{8}|\+[1-9][0-9]{7,14})$/.exec(value)?.[0] === value ? null : { phone: true };
};
export const codepointMaxLength = (maximum: number): ValidatorFn => control =>
  Array.from(String(control.value ?? '')).length <= maximum ? null : { length: true };
export const adultDate: ValidatorFn = (control: AbstractControl): ValidationErrors | null => {
  const value = String(control.value ?? '');
  if (!calendarDate(value) || value > johannesburgToday()) return { date: true };
  return isAdult(value) ? null : { adult: true };
};
