import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ApiClient } from './api-client';
import { environment } from '../../../environments/environment';

describe('ApiClient', () => {
  let api: ApiClient;
  let controller: HttpTestingController;
  const base = environment.apiBaseUrl;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    api = TestBed.inject(ApiClient);
    controller = TestBed.inject(HttpTestingController);
  });

  afterEach(() => controller.verify());

  it('owns the base address — contract-relative paths resolve against environment.apiBaseUrl', async () => {
    const pending = api.get<{ ok: boolean }>('/payments/options');
    const req = controller.expectOne(`${base}/payments/options`);
    expect(req.request.method).toBe('GET');
    req.flush({ ok: true });
    expect(await pending).toEqual({ ok: true });
  });

  it('posts bodies and returns the typed result', async () => {
    const pending = api.post<{ id: string }>('/payments/intent', { amount: 100 });
    const req = controller.expectOne(`${base}/payments/intent`);
    expect(req.request.body).toEqual({ amount: 100 });
    req.flush({ id: 'pi_1' });
    expect(await pending).toEqual({ id: 'pi_1' });
  });

  it('exposes the full response for endpoints using 204-as-none', async () => {
    const pending = api.getResponse<unknown>('/payments/recurring');
    controller.expectOne(`${base}/payments/recurring`).flush(null, { status: 204, statusText: 'No Content' });
    expect((await pending).status).toBe(204);
  });
});
