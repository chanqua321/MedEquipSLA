import type { EmployeeInput, MfaProof, MfaConfirmation, RedeemInput } from './requests';
import { http, json, query, type Tokens, type User } from './http';
import type { Employee, Page } from './contracts';
const base = 'identity/api/';
export const identityApi = {
  login: (input: { userName: string; password: string; code?: string; recoveryCode?: string }) =>
    http<Tokens>(base + 'auth/login', { public: true, method: 'POST', body: json(input) }),
  me: (signal?: AbortSignal) => http<User>(base + 'auth/me', { signal }),
  logout: (all = false) => http<void>(base + 'auth/logout' + query({ all }), { method: 'POST' }),
  setup: (input: MfaProof) =>
    http<{
      userId: string;
      setupToken: string;
      secret: string | null;
      authenticatorUri: string | null;
      method: string;
    }>(base + 'auth/mfa/setup', { public: true, method: 'POST', body: json(input) }),
  confirm: (input: MfaConfirmation) =>
    http<{ recoveryCodes: string[] }>(base + 'auth/mfa/confirm', {
      public: true,
      method: 'POST',
      body: json(input),
    }),
  mfaAction: (action: 'email-code' | 'disable', input: MfaProof) =>
    http<void>(base + 'auth/mfa/' + action, { public: true, method: 'POST', body: json(input) }),
  redeem: (kind: 'activate' | 'password-reset/confirm', input: RedeemInput) =>
    http<void>(base + 'auth/' + kind, { public: true, method: 'POST', body: json(input) }),
  reset: (email: string) =>
    http<{ message: string }>(base + 'auth/password-reset/request', {
      public: true,
      method: 'POST',
      body: json({ email }),
    }),
  users: (search = '', page = 1, signal?: AbortSignal) =>
    http<Page<Employee>>(base + 'users' + query({ search, page }), { signal }),
  invite: (input: EmployeeInput) =>
    http<{ userId: string; status: string }>(base + 'users', { method: 'POST', body: json(input) }),
  profile: (id: string, input: EmployeeInput) =>
    http<void>(base + `users/${id}/profile`, { method: 'PUT', body: json(input) }),
  access: (id: string, input: { active: boolean; rowVersion: string }) =>
    http<void>(base + `users/${id}/access`, { method: 'PUT', body: json(input) }),
  resend: (id: string, rowVersion: string) =>
    http<unknown>(base + `users/${id}/activation/resend`, {
      method: 'POST',
      body: json({ rowVersion }),
    }),
  eligible: (id: string) => http<number>(base + `users/technicians/${id}/eligibility`),
  import: (file: File, preview: boolean) => {
    const form = new FormData();
    form.set('file', file);
    return http<{
      preview: boolean;
      rows: { row: number; status: string; userId: string | null }[];
    }>(base + 'users/import' + query({ preview }), { method: 'POST', body: form, timeout: 120000 });
  },
};
