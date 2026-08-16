import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

@Injectable({ providedIn: 'root' })
export class ChainService {
  private readonly API_URL = `${environment.apiUrl}/v1/chain`;

  constructor(private http: HttpClient) {}

  getDashboard(): Observable<any> {
    return this.http.get(this.API_URL);
  }

  updateSettings(body: unknown): Observable<any> {
    return this.http.put(`${this.API_URL}/settings`, body);
  }

  redeploy(systemName?: string): Observable<any> {
    return this.http.post(`${this.API_URL}/redeploy`, { systemName });
  }

  scale(nodeCount: number): Observable<any> {
    return this.http.post(`${this.API_URL}/cluster`, { nodeCount });
  }
}
