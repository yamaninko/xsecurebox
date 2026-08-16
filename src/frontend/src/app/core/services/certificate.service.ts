import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

@Injectable({ providedIn: 'root' })
export class CertificateService {
  private readonly API_URL = `${environment.apiUrl}/v1/certificates`;

  constructor(private http: HttpClient) {}

  getCertificates(params?: Record<string, string>): Observable<any> {
    let httpParams = new HttpParams();
    Object.entries(params || {}).forEach(([key, value]) => {
      if (value) httpParams = httpParams.set(key, value);
    });
    return this.http.get(this.API_URL, { params: httpParams });
  }

  upload(body: unknown): Observable<any> {
    return this.http.post(this.API_URL, body);
  }

  revoke(id: string, reason: string): Observable<any> {
    return this.http.post(`${this.API_URL}/${id}/revoke`, { reason });
  }

  delete(id: string): Observable<any> {
    return this.http.delete(`${this.API_URL}/${id}`);
  }
}
