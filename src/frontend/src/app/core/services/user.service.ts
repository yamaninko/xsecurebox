import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

@Injectable({ providedIn: 'root' })
export class UserService {
  private readonly API_URL = `${environment.apiUrl}/v1/users`;

  constructor(private http: HttpClient) {}

  getUsers(): Observable<any> {
    return this.http.get(this.API_URL);
  }

  create(body: unknown): Observable<any> {
    return this.http.post(this.API_URL, body);
  }

  update(id: string, body: unknown): Observable<any> {
    return this.http.put(`${this.API_URL}/${id}`, body);
  }

  delete(id: string): Observable<any> {
    return this.http.delete(`${this.API_URL}/${id}`);
  }

  assignRole(userId: string, roleId: string): Observable<any> {
    return this.http.post(`${this.API_URL}/${userId}/roles/${roleId}`, {});
  }
}
