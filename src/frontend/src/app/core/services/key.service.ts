import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface KeyDto {
  keyId: string;
  name: string;
  description?: string;
  keyType: string;
  status: string;
  version: number;
  expiresAt?: string;
  certificateName: string;
  ownerUsername: string;
  createdAt: string;
  lastAccessedAt?: string;
  accessCount: number;
}

export interface CreateKeyRequest {
  name: string;
  description?: string;
  keyType: string;
  value: string;
  certificateId: string;
  expiresAt?: string;
  ownerUserId?: string;
}

export interface RetrieveKeyResponse {
  keyId: string;
  name: string;
  value: string;
  expiresAt?: string;
  retrievedAt: string;
}

@Injectable({
  providedIn: 'root'
})
export class KeyService {
  private readonly API_URL = `${environment.apiUrl}/v1/keys`;

  constructor(private http: HttpClient) {}

  getKeys(params?: any): Observable<any> {
    let httpParams = new HttpParams();
    if (params) {
      Object.keys(params).forEach(key => {
        if (params[key] !== null && params[key] !== undefined) {
          httpParams = httpParams.set(key, params[key].toString());
        }
      });
    }
    return this.http.get<any>(this.API_URL, { params: httpParams });
  }

  getKeyById(keyId: string): Observable<any> {
    return this.http.get<any>(`${this.API_URL}/${keyId}`);
  }

  createKey(request: CreateKeyRequest): Observable<any> {
    return this.http.post<any>(this.API_URL, request);
  }

  retrieveKey(keyId: string, reason?: string): Observable<any> {
    return this.http.post<any>(`${this.API_URL}/${keyId}/retrieve`, { reason });
  }

  updateKey(keyId: string, request: any): Observable<any> {
    return this.http.put<any>(`${this.API_URL}/${keyId}`, request);
  }

  rotateKey(keyId: string, newValue: string, reason?: string): Observable<any> {
    return this.http.post<any>(`${this.API_URL}/${keyId}/rotate`, { newValue, reason });
  }

  revokeKey(keyId: string, reason: string): Observable<any> {
    return this.http.post<any>(`${this.API_URL}/${keyId}/revoke`, { reason });
  }

  deleteKey(keyId: string): Observable<any> {
    return this.http.delete<any>(`${this.API_URL}/${keyId}`);
  }
}

