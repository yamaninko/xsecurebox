import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { KeyService } from './key.service';

describe('KeyService', () => {
  let service: KeyService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
      providers: [KeyService]
    });
    service = TestBed.inject(KeyService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('should call GET /api/v1/keys with params', () => {
    service.getKeys({ page: 2, pageSize: 50, status: 'Active' }).subscribe();
    const req = httpMock.expectOne(r => r.url.includes('/api/v1/keys'));
    expect(req.request.method).toBe('GET');
    expect(req.request.params.get('page')).toBe('2');
    expect(req.request.params.get('pageSize')).toBe('50');
    expect(req.request.params.get('status')).toBe('Active');
    req.flush({ success: true, data: [] });
  });
});

