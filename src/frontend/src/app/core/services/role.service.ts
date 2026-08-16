import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

@Injectable({ providedIn: 'root' })
export class RoleService {
  private readonly API_URL = `${environment.apiUrl}/v1/roles`;

  constructor(private http: HttpClient) {}

  getRoles(): Observable<any> {
    return this.http.get(this.API_URL);
  }

  create(body: unknown): Observable<any> {
    return this.http.post(this.API_URL, body);
  }

  delete(id: string): Observable<any> {
    return this.http.delete(`${this.API_URL}/${id}`);
  }
}
