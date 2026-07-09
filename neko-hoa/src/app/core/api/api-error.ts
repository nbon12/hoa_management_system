/**
 * Normalized business error (015 US6, FR-020): the backend's uniform `{ code, message }` envelope
 * (015 US2, contracts/error-envelope.md) surfaced as a typed error by the error interceptor, so
 * components never re-parse `HttpErrorResponse` shapes by hand.
 */
export class ApiError extends Error {
  constructor(
    /** Stable contract code, e.g. `INVALID_CREDENTIALS`, `MISSING_CLAIM`, or `HTTP_<status>` when the body carried no envelope. */
    public readonly code: string,
    message: string,
    public readonly status: number,
  ) {
    super(message);
    this.name = 'ApiError';
  }
}

/** User-presentable message for any thrown value, preferring the contract envelope's message. */
export function apiErrorMessage(err: unknown, fallback: string): string {
  return err instanceof ApiError && err.message ? err.message : fallback;
}
