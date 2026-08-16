import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface ApiClient {
  clientId: string;
  clientName: string;
  description?: string;
  clientIdString: string;
  scopes: string[];
  rateLimitPerMinute: number;
  rateLimitPerHour: number;
  isActive: boolean;
  lastUsedAt?: Date;
  totalRequests: number;
  createdByUsername: string;
  createdAt: Date;
  revokedAt?: Date;
}

export interface CreateApiClientRequest {
  clientName: string;
  description?: string;
  scopes: string[];
  rateLimitPerMinute: number;
  rateLimitPerHour: number;
}

export interface UpdateApiClientRequest {
  clientName?: string;
  description?: string;
  scopes?: string[];
  rateLimitPerMinute?: number;
  rateLimitPerHour?: number;
  isActive?: boolean;
}

export interface ApiClientDetailResponse {
  client: {
    clientId: string;
    clientName: string;
    description?: string;
    clientIdString: string;
    apiKey: string;
    scopes: string[];
    rateLimitPerMinute: number;
    rateLimitPerHour: number;
    isActive: boolean;
    lastUsedAt?: Date;
    totalRequests: number;
    createdByUsername: string;
    createdAt: Date;
  };
  clientSecret: string;
  message: string;
}

export interface RegenerateSecretResponse {
  clientId: string;
  clientSecret: string;
  message: string;
}

export interface RegenerateApiKeyResponse {
  apiKey: string;
  message: string;
}

export interface TokenTestRequest {
  clientId: string;
  clientSecret: string;
  scope?: string;
}

export interface TokenTestResponse {
  access_token: string;
  token_type: string;
  expires_in: number;
  scope: string;
}

@Injectable({
  providedIn: 'root'
})
export class ApiClientService {
  private readonly API_URL = `${environment.apiUrl}/v1/clients`;
  private readonly TOKEN_URL = `${environment.apiUrl}/v1/oauth/token`;

  constructor(private http: HttpClient) {}

  getAllClients(): Observable<any> {
    return this.http.get(`${this.API_URL}`);
  }

  getClientById(clientId: string): Observable<any> {
    return this.http.get(`${this.API_URL}/${clientId}`);
  }

  createClient(request: CreateApiClientRequest): Observable<any> {
    return this.http.post(`${this.API_URL}`, request);
  }

  updateClient(clientId: string, request: UpdateApiClientRequest): Observable<any> {
    return this.http.put(`${this.API_URL}/${clientId}`, request);
  }

  revokeClient(clientId: string, reason: string): Observable<any> {
    return this.http.post(`${this.API_URL}/${clientId}/revoke`, { reason });
  }

  deleteClient(clientId: string): Observable<any> {
    return this.http.delete(`${this.API_URL}/${clientId}`);
  }

  regenerateSecret(clientId: string): Observable<any> {
    return this.http.post(`${this.API_URL}/${clientId}/regenerate-secret`, {});
  }

  regenerateApiKey(clientId: string): Observable<any> {
    return this.http.post(`${this.API_URL}/${clientId}/regenerate-apikey`, {});
  }

  getStats(): Observable<any> {
    return this.http.get(`${this.API_URL}/stats`);
  }

  testToken(request: TokenTestRequest): Observable<any> {
    const body = new URLSearchParams();
    body.set('grant_type', 'client_credentials');
    body.set('client_id', request.clientId);
    body.set('client_secret', request.clientSecret);
    if (request.scope) {
      body.set('scope', request.scope);
    }

    return this.http.post(this.TOKEN_URL, body.toString(), {
      headers: {
        'Content-Type': 'application/x-www-form-urlencoded'
      }
    });
  }
}

