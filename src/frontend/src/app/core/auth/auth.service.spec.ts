import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { RouterTestingModule } from '@angular/router/testing';
import { AuthService, AuthResponse, LoginRequest, User } from './auth.service';

function base64UrlEncode(json: string): string {
  return btoa(json).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
}

function makeJwtWithExp(secondsFromNow: number): string {
  const header = base64UrlEncode(JSON.stringify({ alg: 'none', typ: 'JWT' }));
  const payload = base64UrlEncode(JSON.stringify({ exp: Math.floor(Date.now() / 1000) + secondsFromNow }));
  return `${header}.${payload}.signature`;
}

describe('AuthService', () => {
  let service: AuthService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule, RouterTestingModule],
      providers: [AuthService]
    });
    service = TestBed.inject(AuthService);
    httpMock = TestBed.inject(HttpTestingController);
    localStorage.clear();
  });

  afterEach(() => {
    httpMock.verify();
    localStorage.clear();
  });

  it('should return false for isAuthenticated when no token', () => {
    expect(service.isAuthenticated()).toBeFalse();
  });

  it('should return true for isAuthenticated when token not expired', () => {
    const token = makeJwtWithExp(3600); // valid for 1h
    localStorage.setItem('access_token', token);
    expect(service.isAuthenticated()).toBeTrue();
  });

  it('should POST login and store tokens on success', () => {
    const reqBody: LoginRequest = { username: 'alice', password: 'p@ss' };
    const user: User = {
      userId: '00000000-0000-0000-0000-000000000001',
      username: 'alice',
      email: 'alice@example.com',
      roles: ['Client']
    };
    const mock: { success: boolean; data: AuthResponse } = {
      success: true,
      data: {
        accessToken: makeJwtWithExp(600),
        refreshToken: 'r-token',
        expiresIn: 600,
        tokenType: 'Bearer',
        user
      }
    };

    service.login(reqBody.username, reqBody.password).subscribe();

    const req = httpMock.expectOne(r => r.url.endsWith('/api/v1/auth/login'));
    expect(req.request.method).toBe('POST');
    req.flush(mock);

    expect(localStorage.getItem('access_token')).toBeTruthy();
    expect(localStorage.getItem('refresh_token')).toBe('r-token');
  });
});

