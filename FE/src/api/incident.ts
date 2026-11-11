import type { IncidentInput, PolicyInput } from './requests';
import { http, json, query } from './http';
import type { IncidentAttempt, IncidentDetail, IncidentRow, Page, Policy } from './contracts';
const base = 'incident/api/';
export const incidentApi = {
  list: async (values: Record<string, string | number | undefined> = {}, signal?: AbortSignal) => {
    const result = await http<Page<IncidentRow>>(base + 'incidents/search' + query(values), {
      signal,
    });
    return {
      ...result,
      hasNext: (result.page ?? 1) * (result.pageSize ?? 20) < (result.total ?? 0),
    };
  },
  detail: (id: string, signal?: AbortSignal) =>
    http<IncidentDetail>(base + `incidents/${id}`, { signal }),
  create: (input: IncidentInput, key: string) =>
    http<{ id: string }>(base + 'incidents', {
      method: 'POST',
      body: json(input),
      headers: { 'Idempotency-Key': key },
    }),
  assign: (id: string, technicianId: string, rowVersion: string) =>
    http<void>(base + `incidents/${id}/assignment`, {
      method: 'PUT',
      body: json({ technicianId, rowVersion }),
    }),
  start: (id: string, rowVersion: string) =>
    http<void>(base + `incidents/${id}/start`, { method: 'POST', body: json({ rowVersion }) }),
  attempts: (id: string, signal?: AbortSignal) =>
    http<IncidentAttempt[]>(base + `incidents/${id}/attempts`, { signal }),
  submit: (id: string, form: FormData) =>
    http<unknown>(base + `incidents/${id}/attempts`, { method: 'POST', body: form }),
  review: (id: string, attempt: string, accept: boolean, rowVersion: string, reason?: string) =>
    http<void>(base + `incidents/${id}/attempts/${attempt}/${accept ? 'accept' : 'reject'}`, {
      method: 'POST',
      body: json({ rowVersion, reason }),
    }),
  photo: (id: string, attempt: string) =>
    http<Blob>(base + `incidents/${id}/attempts/${attempt}/photo`, { response: 'blob' }),
  policies: (signal?: AbortSignal) => http<Policy[]>(base + 'sla-policies', { signal }),
  savePolicy: (priority: string, input: PolicyInput) =>
    http<void>(base + `sla-policies/${priority}`, { method: 'PUT', body: json(input) }),
};
