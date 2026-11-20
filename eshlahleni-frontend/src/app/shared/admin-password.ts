import { Validators } from '@angular/forms';

// The shared API supplies and validates the policy; never trim passwords.
export const adminPassword = [Validators.required];
