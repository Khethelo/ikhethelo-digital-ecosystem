import { test, expect, Page } from '@playwright/test';
import { mkdir } from 'node:fs/promises';

async function fillApplication(page: Page, name = 'Naledi', email = 'naledi@example.test', phone = '+27841234567') {
  await page.locator('#firstName').fill(name); await page.locator('#lastName').fill('Dlamini');
  await page.locator('#dateOfBirth').fill('1995-06-15'); await page.locator('#phoneNumber').fill(phone);
  await page.locator('#email').fill(email); await page.locator('#termsAccepted').check(); await page.locator('#privacyConsent').check();
}
test('public site, mobile navigation, eligibility and recoverable API errors', async ({ page }) => {
  await mkdir('artifacts', { recursive: true });
  await page.goto('/'); await expect(page.locator('h1')).toContainText('Come through.');
  await expect(page.locator('.hero-emblem')).toBeVisible();
  await page.screenshot({ path: 'artifacts/home-desktop.png', fullPage: true });
  await page.setViewportSize({ width: 390, height: 844 });
  await page.screenshot({ path: 'artifacts/home-mobile.png', fullPage: true });
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  await page.getByRole('button', { name: 'Menu', exact: true }).click();
  await page.locator('#public-navigation').getByRole('link', { name: 'Membership', exact: true }).click();
  await expect(page).toHaveURL(/\/membership$/);
  await page.goto('/join'); await fillApplication(page); await page.locator('#dateOfBirth').fill('2020-01-01');
  let submissions = 0; await page.route('**/api/v1/public/**', async route => { submissions++; await route.fulfill({ status: 409, json: { status: 409 } }); });
  await page.getByRole('button', { name: 'Register for Membership' }).click();
  await expect(page.locator('#dateOfBirth-error')).toBeVisible(); expect(submissions).toBe(0);
  await page.locator('#dateOfBirth').fill('1995-06-15'); await page.getByRole('button', { name: 'Register for Membership' }).click();
  await expect(page.getByText('A registration may already exist', { exact: false })).toBeVisible();
  await page.unroute('**/api/v1/public/**');
  await page.route('**/api/v1/public/**', route => route.fulfill({ status: 422, json: { status: 422, errors: { email: ['Please use another email address.'] } } }));
  await page.getByRole('button', { name: 'Register for Membership' }).click(); await expect(page.getByText('Please use another email address.')).toBeVisible();
  await page.screenshot({ path: 'artifacts/join-mobile.png', fullPage: true });
});

test('real registration, membership activation, member edits and session guard', async ({ page, request }) => {
  test.skip(!process.env['ESHL_E2E_EMAIL'], 'Run npm run verify:live with the disposable database harness.');
  const suffix = Date.now().toString(); const email = `approved-${suffix}@example.test`;
  await page.goto('/join'); await fillApplication(page, 'Naledi', email);
  await page.getByRole('button', { name: 'Register for Membership' }).click();
  const reference = page.getByTestId('application-reference'); await expect(reference).toBeVisible(); const applicationId = (await reference.textContent())!.trim();
  await expect(page.getByText('Awaiting In-Person Completion', { exact: false }).first()).toBeVisible();
  await expect(page.getByRole('link', { name: /Chat on WhatsApp/ })).toHaveAttribute('href', 'https://wa.me/27837265910');
  await expect(page.locator('.receipt-card')).toContainText('32 Albany Grove');
  await expect(page.locator('.receipt-card')).toContainText('R100');
  await page.goto('/admin/members'); await expect(page).toHaveURL(/admin\/login/);
  await page.locator('#adminEmail').fill(process.env['ESHL_E2E_EMAIL']!); await page.locator('#adminPassword').fill(process.env['ESHL_E2E_PASSWORD']!);
  await page.getByRole('button', { name: 'Sign in', exact: false }).click();
  await expect(page).toHaveURL(/admin\/members/);
  // Separate authenticated assertion client avoids racing Chrome's navigation response-body lifetime.
  const verificationLogin = await request.post('/api/v1/auth/login', { data: { email: process.env['ESHL_E2E_EMAIL'], password: process.env['ESHL_E2E_PASSWORD'] } });
  expect(verificationLogin.status()).toBe(200); const token = (await verificationLogin.json()).accessToken;
  await page.locator('.admin-nav').getByRole('link', { name: 'Registrations' }).click();
  await page.locator('#applicationSearch').fill(email); await page.getByRole('button', { name: 'Search', exact: false }).click();
  await page.getByRole('link', { name: 'View application for Naledi Dlamini' }).click();
  await expect(page).toHaveURL(new RegExp(applicationId));
  await expect(page.locator('#reviewNotes')).toBeVisible();
  await page.screenshot({ path: 'artifacts/admin-application-desktop.png', fullPage: true });
  await page.getByRole('button', { name: 'Activate Membership' }).click();
  await page.getByRole('dialog').getByRole('button', { name: 'Cancel', exact: true }).click();
  await expect(page.getByRole('button', { name: 'Activate Membership' })).toBeEnabled();
  await page.locator('#reviewNotes').fill('Verified with the applicant during frontend acceptance testing.');
  await page.getByRole('button', { name: 'Activate Membership' }).click(); await page.getByRole('dialog').getByRole('button', { name: 'Activate Membership' }).click();
  await expect(page.getByText('Membership activated. Member', { exact: false })).toBeVisible();
  await page.getByRole('link', { name: /^View member ESC/ }).click();
  await expect(page.getByTestId('member-number')).toHaveText(/^ESC/);
  await page.locator('.admin-nav').getByRole('link', { name: 'Members', exact: true }).click();
  await page.locator('#memberSearch').fill(email); await page.getByRole('button', { name: 'Search', exact: false }).click();
  await expect(page.locator('tbody')).toContainText('ESC-');
  await page.getByRole('link', { name: 'View member Naledi Dlamini' }).click();
  await page.getByRole('button', { name: 'Edit profile' }).click(); await page.locator('#memberFirstName').fill('Naledi Updated'); await page.getByRole('button', { name: 'Save profile' }).click();
  await expect(page.getByText('Member profile updated.', { exact: true })).toBeVisible();
  await page.locator('#detailMemberStatus').selectOption('Suspended'); await page.getByRole('button', { name: 'Update status' }).click(); await page.getByRole('button', { name: 'Confirm status change' }).click();
  await expect(page.getByText('Membership status updated to Suspended.')).toBeVisible();
  await page.setViewportSize({ width: 390, height: 844 }); await page.locator('#main-content').focus(); await page.screenshot({ path: 'artifacts/admin-member-mobile.png', fullPage: true });
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  await page.setViewportSize({ width: 1440, height: 1000 });
  const headers = { Authorization: `Bearer ${token}` };
  const completed = await request.get(`/api/v1/admin/tenants/ESHL/membership-applications/${applicationId}`, { headers });
  expect((await completed.json()).status).toBe('Completed');
  const repeated = await request.post(`/api/v1/admin/tenants/ESHL/membership-applications/${applicationId}/activate`, { headers, data: {} });
  expect(repeated.status()).toBe(409);
  expect(await page.evaluate(() => localStorage.length + sessionStorage.length)).toBe(0);
  await page.reload(); await expect(page).toHaveURL(/\/admin\/login/);
});

test('administrator pagination, forbidden/missing records and expired sessions', async ({ page }) => {
  let failure = 0; const pages: string[] = [];
  const applicant = { applicationId: '11111111-1111-4111-8111-111111111111', firstName: 'Test', lastName: 'Applicant', email: 'test@example.test', phoneNumber: '+27841234567', dateOfBirth: '1990-01-01', termsAccepted: true, privacyConsent: true, status: 'AwaitingInPersonCompletion', submittedAtUtc: '2026-11-06T10:00:00Z' };
  await page.route('**/api/v1/**', route => {
    const url = new URL(route.request().url());
    if (url.pathname.endsWith('/auth/login')) return route.fulfill({ json: { accessToken: 'temporary-browser-test-token', tokenType: 'Bearer', expiresIn: 900 } });
    if (failure) return route.fulfill({ status: failure, json: { status: failure } });
    pages.push(url.searchParams.get('page') ?? '1');
    return route.fulfill({ json: { items: url.pathname.endsWith('/members') ? [] : [applicant], totalCount: url.pathname.endsWith('/members') ? 0 : 45, page: Number(url.searchParams.get('page') ?? '1'), pageSize: 20 } });
  });
  await page.goto('/admin/login'); await page.locator('#adminEmail').fill('admin@example.test'); await page.locator('#adminPassword').fill('test-password'); await page.getByRole('button', { name: 'Sign in', exact: false }).click();
  await expect(page).toHaveURL(/\/admin$/); await page.locator('.admin-nav').getByRole('link', { name: 'Registrations' }).click();
  await page.getByRole('button', { name: 'Next page' }).click(); await expect(page).toHaveURL(/page=2/); expect(pages).toContain('2');
  failure = 403; await page.locator('#applicationStatus').selectOption('Completed'); await page.getByRole('button', { name: 'Search', exact: false }).click();
  await expect(page.getByText('Your account does not have permission', { exact: false })).toBeVisible();
  failure = 404; await page.getByRole('button', { name: 'Try again', exact: false }).click(); await expect(page.getByText('This record could not be found.', { exact: false })).toBeVisible();
  failure = 401; await page.getByRole('button', { name: 'Try again', exact: false }).click(); await expect(page).toHaveURL(/\/admin\/login/); await expect(page.getByText('Your session has ended.', { exact: false })).toBeVisible();
});
