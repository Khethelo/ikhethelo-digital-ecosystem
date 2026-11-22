import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { of, throwError } from 'rxjs';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { AdminMembersService } from '../../core/membership.services';
import { routes } from '../../app.routes';
import { RegisterMemberPage } from './register-member';

describe('walk-in registration', () => {
  const register = vi.fn();
  beforeEach(() => { register.mockReset(); TestBed.configureTestingModule({ imports: [RegisterMemberPage], providers: [provideRouter([]), { provide: AdminMembersService, useValue: { register } }] }); });
  function page() { return TestBed.createComponent(RegisterMemberPage).componentInstance; }
  function fill(p: RegisterMemberPage) { p.form.setValue({ firstName: 'Jane', lastName: 'Doe', dateOfBirth: '1990-01-01', phoneNumber: '+27821234567', email: 'jane@example.test', termsAccepted: true, privacyConsent: true }); }
  it('orders the registration route before the member detail route', () => {
    const children = routes.find(route => route.path === 'admin')!.children!;
    expect(children.findIndex(route => route.path === 'members/new')).toBeLessThan(children.findIndex(route => route.path === 'members/:memberId'));
  });
  it('rejects missing consent and underage dates without submitting', () => {
    const p = page(); fill(p); p.form.controls.privacyConsent.setValue(false); p.submit(); expect(register).not.toHaveBeenCalled();
    p.form.controls.privacyConsent.setValue(true); p.form.controls.dateOfBirth.setValue('2020-01-01'); p.submit(); expect(register).not.toHaveBeenCalled();
  });
  it.each([true, false])('keeps the member number and email result (%s)', sent => {
    const p = page(); fill(p); register.mockReturnValue(of({ member: { memberNumber: 'ESC-00123' }, confirmationEmailSent: sent }));
    p.submit(); expect(register).toHaveBeenCalledOnce(); expect(p.result()?.member.memberNumber).toBe('ESC-00123'); expect(p.result()?.confirmationEmailSent).toBe(sent);
    p.another(); expect(p.result()).toBeNull();
  });
  it('shows a safe duplicate conflict', () => {
    const p = page(); fill(p); const id = '7f111111-1111-4111-8111-111111111111'; register.mockReturnValue(throwError(() => new HttpErrorResponse({ status: 409, error: { title: 'A pending membership application already exists for this email address or phone number. Review the existing application instead.', existingApplicationId: id } })));
    p.submit(); expect(p.issue()?.message).toContain('pending membership application'); expect(p.issue()?.existingApplicationId).toBe(id); expect(p.form.value.email).toBe('jane@example.test'); expect(p.result()).toBeNull();
  });
  it('preserves the form and offers the existing active member after a conflict', () => {
    const p = page(); fill(p); const id = '7f222222-2222-4222-8222-222222222222';
    register.mockReturnValue(throwError(() => new HttpErrorResponse({ status: 409, error: { title: 'An active member already exists with this email address or phone number.', existingMemberId: id } })));
    p.submit(); expect(p.issue()?.existingMemberId).toBe(id); expect(p.issue()?.message).toContain('active member already exists');
    expect(p.form.value.email).toBe('jane@example.test'); expect(p.saving()).toBe(false); expect(p.result()).toBeNull();
  });
});
