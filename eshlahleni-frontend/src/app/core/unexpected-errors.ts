import { ErrorHandler, Injectable, isDevMode, signal } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class UnexpectedErrors {
  readonly message = signal('');
}

@Injectable()
export class FriendlyErrorHandler implements ErrorHandler {
  constructor(private readonly errors: UnexpectedErrors) {}
  handleError(error: unknown): void {
    if (isDevMode()) console.error('Unexpected application error', error);
    this.errors.message.set('Something unexpected happened. Please refresh the page and try again.');
  }
}
