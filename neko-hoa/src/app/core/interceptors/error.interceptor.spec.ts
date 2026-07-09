import { TestBed } from '@angular/core/testing';
import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { errorInterceptor } from './error.interceptor';
import { ApiError } from '../api/api-error';

describe('errorInterceptor', () => {
  let http: HttpClient;
  let controller: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([errorInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    http = TestBed.inject(HttpClient);
    controller = TestBed.inject(HttpTestingController);
  });

  afterEach(() => controller.verify());

  it('normalizes the backend { code, message } envelope into a typed ApiError', async () => {
    const pending = http.get('/api/v1/property').toPromise();
    controller.expectOne('/api/v1/property').flush(
      { code: 'PROPERTY_ACCESS_DENIED', message: 'You are not linked to the requested property.' },
      { status: 403, statusText: 'Forbidden' });

    try {
      await pending;
      fail('expected ApiError');
    } catch (err) {
      expect(err).toBeInstanceOf(ApiError);
      const apiError = err as ApiError;
      expect(apiError.code).toBe('PROPERTY_ACCESS_DENIED');
      expect(apiError.message).toBe('You are not linked to the requested property.');
      expect(apiError.status).toBe(403);
    }
  });

  it('falls back to HTTP_<status> and a generic message when the body has no envelope', async () => {
    const pending = http.get('/api/v1/anything').toPromise();
    controller.expectOne('/api/v1/anything').flush('boom', { status: 502, statusText: 'Bad Gateway' });

    try {
      await pending;
      fail('expected ApiError');
    } catch (err) {
      const apiError = err as ApiError;
      expect(apiError.code).toBe('HTTP_502');
      expect(apiError.status).toBe(502);
      expect(apiError.message.length).toBeGreaterThan(0);
    }
  });

  it('passes successful responses through untouched', async () => {
    const pending = http.get<{ ok: boolean }>('/api/v1/health').toPromise();
    controller.expectOne('/api/v1/health').flush({ ok: true });

    expect(await pending).toEqual({ ok: true });
  });
});
