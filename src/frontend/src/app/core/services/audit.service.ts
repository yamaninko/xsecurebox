import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

@Injectable({ providedIn: 'root' })
export class AuditService {
  private readonly API_URL = `${environment.apiUrl}/v1/audit`;

  constructor(private http: HttpClient) {}

  getTrails(params?: Record<string, string>): Observable<any> {
    let httpParams = new HttpParams();
    Object.entries(params || {}).forEach(([key, value]) => {
      if (value) httpParams = httpParams.set(key, value);
    });
    return this.http.get(`${this.API_URL}/trails`, { params: httpParams });
  }
}
