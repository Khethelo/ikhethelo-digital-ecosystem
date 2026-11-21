import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { finalize, Observable } from 'rxjs';
import { Administrator, AdministratorsService } from '../../core/administrators.service';
import { AuthService } from '../../core/auth.service';
import { ApiIssue, apiIssue } from '../../core/api-errors';
import { adminPassword } from '../../shared/admin-password';
import { ApiError } from '../../shared/ui';

@Component({ imports: [ReactiveFormsModule, ApiError], styles: [`
  .user-actions { display: flex; flex-wrap: wrap; gap: 8px; }
  .user-actions button {
    display: inline-flex; align-items: center; justify-content: center;
    min-height: 38px; padding: 8px 12px; border: 1px solid #cbd0c1;
    border-radius: 2px; background: #fff; color: #171c17;
    font-size: .7rem; font-weight: 700; line-height: 1.2; white-space: nowrap;
  }
  .user-actions button:not(:disabled):hover { background: #f2f4ed; border-color: #728060; }
  .user-actions button:focus-visible { outline: 2px solid #536d3f; outline-offset: 2px; }
  .user-actions .deactivate { color: #8c3732; border-color: #d4b3ab; }
  .user-actions .deactivate:not(:disabled):hover { background: #faf0ed; border-color: #d4b3ab; }
  .user-actions .activate { background: #e9eedf; border-color: #9aa98a; color: #314528; }
  .user-actions button:disabled { opacity: .5; cursor: not-allowed; }
`], template: `
  <div class="admin-page-heading admin-settings-heading"><div><span class="section-tag">USER MANAGEMENT</span><h1>ADMINISTRATORS.</h1></div><button class="button" (click)="open('create')">Add administrator</button></div>
  <app-api-error [issue]="issue()" /> @if (message()) { <p class="alert alert--success" role="status">{{ message() }}</p> }
  @if (mode()) { <section class="panel admin-form-panel"><h2>{{ mode() === 'create' ? 'Add administrator' : mode() === 'edit' ? 'Edit administrator' : 'Reset password' }}</h2>
    <form class="admin-form" [formGroup]="form" (ngSubmit)="save()">
      @if (mode() !== 'reset') { <div class="admin-form-field"><label for="userEmail">Email</label><input id="userEmail" type="email" formControlName="email" /></div>
        <div class="admin-form-field"><label for="userRole">Role</label><select id="userRole" formControlName="role"><option value="TenantStaff">Admin</option><option value="TenantAdmin">Organisation Administrator</option></select></div> }
      @if (mode() !== 'edit') { <div class="admin-form-field"><label for="userPassword">{{ mode() === 'create' ? 'Initial password' : 'New password' }}</label><input id="userPassword" type="password" autocomplete="new-password" formControlName="password" aria-describedby="userPasswordGuidance" />
        <p id="userPasswordGuidance" class="admin-form-hint">{{ guidance() }}</p>@if (issue()?.fields?.['password']; as errors) { <p class="field-error" role="alert">{{ errors[0] }}</p> }</div><div class="admin-form-field"><label for="userConfirm">Confirm password</label><input id="userConfirm" type="password" autocomplete="new-password" formControlName="confirmPassword" />
        @if (form.controls.confirmPassword.touched && form.controls.confirmPassword.value !== form.controls.password.value) { <p class="field-error" role="alert">Passwords do not match.</p> }</div> }
      <div class="admin-form-actions"><button class="button" type="submit" [disabled]="saving()">Save</button><button class="admin-form-cancel" type="button" (click)="close()" [disabled]="saving()">Cancel</button></div>
    </form></section> }
  <section class="table-panel"><table class="responsive-table"><thead><tr><th>Email</th><th>Role</th><th>Status</th><th>Actions</th></tr></thead><tbody>
    @for (user of users(); track user.id) { <tr><td>{{ user.email }}</td><td>{{ user.role === 'TenantAdmin' ? 'Organisation Administrator' : 'Admin' }}</td><td>{{ user.isActive ? 'Active' : 'Inactive' }}</td><td><div class="user-actions">
      <button type="button" (click)="open('edit', user)" [attr.aria-label]="'Edit ' + user.email">Edit</button>
      <button type="button" (click)="open('reset', user)" [disabled]="user.id === auth.actorId()" [attr.aria-label]="'Reset password for ' + user.email">Reset password</button>
      <button type="button" [class.deactivate]="user.isActive" [class.activate]="!user.isActive" (click)="changeStatus(user)" [disabled]="user.id === auth.actorId() || saving()" [attr.aria-label]="(user.isActive ? 'Deactivate ' : 'Activate ') + user.email">{{ user.isActive ? 'Deactivate' : 'Activate' }}</button>
    </div></td></tr> } @empty { <tr><td colspan="4">No administrators to display.</td></tr> }
  </tbody></table>@if (page() > 1) { <button class="text-button" (click)="load(page() - 1)">Previous</button> } @if (page() * 100 < total()) { <button class="text-button" (click)="load(page() + 1)">Next</button> }</section>` })
export class UsersPage {
  private readonly api = inject(AdministratorsService); readonly auth = inject(AuthService);
  readonly guidance = signal('Password requirements are checked by the service.'); readonly users = signal<Administrator[]>([]); readonly page = signal(1); readonly total = signal(0);
  readonly issue = signal<ApiIssue | null>(null); readonly message = signal(''); readonly saving = signal(false);
  readonly mode = signal<'create' | 'edit' | 'reset' | null>(null); readonly selected = signal<Administrator | null>(null);
  readonly form = inject(FormBuilder).nonNullable.group({ email: ['', [Validators.required, Validators.email]], role: ['TenantStaff', Validators.required], password: ['', adminPassword], confirmPassword: ['', Validators.required] });
  constructor() { this.load(); this.api.passwordPolicy().subscribe({ next: policy => this.guidance.set(policy.guidance), error: () => {} }); }
  load(page = this.page()): void { this.api.list(page).subscribe({ next: result => { this.users.set(result.items); this.page.set(result.page); this.total.set(result.totalCount); }, error: error => this.issue.set(apiIssue(error)) }); }
  open(mode: 'create' | 'edit' | 'reset', user: Administrator | null = null): void {
    this.mode.set(mode); this.selected.set(user); this.issue.set(null); this.message.set('');
    this.form.reset({ email: user?.email ?? '', role: user?.role ?? 'TenantStaff', password: '', confirmPassword: '' });
    this.form.controls.email.setValidators(mode === 'reset' ? [] : [Validators.required, Validators.email]);
    this.form.controls.password.setValidators(mode === 'edit' ? [] : adminPassword);
    this.form.controls.confirmPassword.setValidators(mode === 'edit' ? [] : [Validators.required]);
    for (const control of Object.values(this.form.controls)) control.updateValueAndValidity();
  }
  close(): void { this.form.reset(); this.mode.set(null); }
  save(): void {
    this.form.markAllAsTouched(); const mode = this.mode(); const user = this.selected(); const value = this.form.getRawValue();
    if (!mode || this.form.invalid || this.saving() || (mode !== 'edit' && value.password !== value.confirmPassword)) return;
    if (mode === 'reset' && !confirm(`Reset the password for ${user!.email}?`)) return;
    if (mode === 'edit' && user!.role !== value.role && !confirm('Change this administrator’s role? Existing sessions will be invalidated.')) return;
    if (mode === 'create' && value.role === 'TenantAdmin' && !confirm('Grant Organisation Administrator access?')) return;
    this.saving.set(true); this.issue.set(null);
    const request: Observable<Administrator | void> = mode === 'create' ? this.api.create(value.email, value.password, value.role) : mode === 'edit' ? this.api.update(user!.id, value.email, value.role) : this.api.reset(user!.id, value.password);
    request.pipe(finalize(() => this.saving.set(false))).subscribe({ next: () => { this.close(); if (mode === 'edit' && user?.id === this.auth.actorId()) { this.auth.logout(); return; } this.message.set('Administrator updated.'); this.load(); }, error: error => { this.issue.set(apiIssue(error)); this.form.controls.password.reset(); this.form.controls.confirmPassword.reset(); } });
  }
  changeStatus(user: Administrator): void {
    if (!confirm(`${user.isActive ? 'Deactivate' : 'Activate'} ${user.email}?`)) return;
    this.saving.set(true); this.api.status(user.id, !user.isActive).pipe(finalize(() => this.saving.set(false))).subscribe({ next: () => { this.message.set('Administrator status updated.'); this.load(); }, error: error => this.issue.set(apiIssue(error)) });
  }
}
