export interface LoginRequest {
  email: string;
  password: string;
}

export interface LoginResponse {
  accessToken: string;
  tokenType: 'Bearer';
  expiresIn: number;
}

export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  instance?: string;
  traceId?: string;
}

export type LoginResult =
  | { kind: 'success' | 'cancelled' | 'validation' | 'credentials' | 'network' | 'unexpected' }
  | { kind: 'rate-limited'; retryAt: number | null };
