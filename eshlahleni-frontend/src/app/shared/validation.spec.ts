import { describe, expect, it } from 'vitest';
import { FormControl } from '@angular/forms';
import { ageInYears, calendarDate, canonicalPhone, isAdult, johannesburgToday, trimmedName } from './validation';

describe('membership eligibility and exact input rules', () => {
  it('uses the South African calendar date across midnight UTC', () => {
    expect(johannesburgToday(new Date('2026-11-05T22:01:00Z'))).toBe('2026-11-06');
  });
  it('admits on the eighteenth birthday, including leap-day anniversaries', () => {
    expect(isAdult('2008-11-06', '2026-11-06')).toBe(true);
    expect(isAdult('2008-11-07', '2026-11-06')).toBe(false);
    expect(isAdult('2008-02-29', '2026-02-28')).toBe(true);
    expect(ageInYears('2008-02-29', '2026-02-28')).toBe(18);
  });
  it('rejects impossible calendar dates and future births', () => {
    expect(calendarDate('2001-02-29')).toBeNull();
    expect(calendarDate('2000-02-29')).not.toBeNull();
    expect(isAdult('2027-01-01', '2026-11-06')).toBe(false);
  });
  it('accepts local and international numbers without whitespace or trailing newline', () => {
    for (const value of ['084351772', '+27 843517720', '+27843517720\n', '+012345678']) expect(canonicalPhone(new FormControl(value))).not.toBeNull();
    expect(canonicalPhone(new FormControl('0843517720'))).toBeNull();
    expect(canonicalPhone(new FormControl('+27843517720'))).toBeNull();
  });
  it('counts Unicode code points and trims names', () => {
    expect(trimmedName(new FormControl('  Jo  '))).toBeNull();
    expect(trimmedName(new FormControl('𐐀'))).not.toBeNull();
    expect(trimmedName(new FormControl('𐐀'.repeat(100)))).toBeNull();
  });
});
