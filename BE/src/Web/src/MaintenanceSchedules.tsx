import { useState } from 'react';

type Schedule = { id: string; code: string; equipment: string; department: string; workType: string;
  isActive: boolean; intervalDays: number; reminderLeadMinutes: number; completionWindowMinutes: number; nextOccurrenceAtUtc: string };
type Occurrence = { id: string; scheduledOccurrenceAtUtc: string; dueAtUtc: string; state: string; workOrderId: string | null };
type Page<T> = { items: T[]; hasNext: boolean };
type Props = { api: <T>(path: string) => Promise<T>; run: (action: () => Promise<void>) => Promise<void>;
  busy: boolean; openOrder: (id: string) => void };
const path = 'maintenance/api/maintenance-schedules';
const time = new Intl.DateTimeFormat('vi-VN', { dateStyle: 'short', timeStyle: 'short', timeZone: 'Asia/Ho_Chi_Minh' });
const occurrenceStates: Record<string, string> = { Planned: 'Đã lập kỳ', WorkOrderCreated: 'Đã sinh lệnh', Cancelled: 'Đã hủy' };

export function MaintenanceSchedules({ api, run, busy, openOrder }: Props) {
  const [schedules, setSchedules] = useState<Page<Schedule>>();
  const [query, setQuery] = useState({ search: '', active: '', page: 1 });
  const [history, setHistory] = useState<{ schedule: Schedule; result: Page<Occurrence>; page: number }>();

  async function loadSchedules(next: typeof query) {
    const params = new URLSearchParams({ search: next.search, page: String(next.page) });
    if (next.active) params.set('active', next.active);
    setSchedules(await api<Page<Schedule>>(path + '?' + params));
    setQuery(next);
    setHistory(undefined);
  }

  async function loadHistory(schedule: Schedule, page: number) {
    const result = await api<Page<Occurrence>>(path + '/' + schedule.id + '/occurrences?page=' + page);
    setHistory({ schedule, result, page });
  }

  return <details className="panel">
    <summary>Lịch định kỳ và các kỳ đã lập</summary>
    <form className="toolbar" onSubmit={event => {
      event.preventDefault();
      const data = new FormData(event.currentTarget);
      void run(() => loadSchedules({ search: String(data.get('search')).trim(), active: String(data.get('active')), page: 1 }));
    }}>
      <label>Mã lịch hoặc tên thiết bị<input name="search" maxLength={200} /></label>
      <label>Trạng thái lịch<select name="active">
        <option value="">Tất cả</option><option value="true">Đang áp dụng</option><option value="false">Ngừng áp dụng</option>
      </select></label>
      <button disabled={busy}>Tìm lịch</button>
    </form>
    {schedules?.items.length === 0 && <p>Không tìm thấy lịch.</p>}
    {schedules && schedules.items.length > 0 && <>
      <ul>{schedules.items.map(schedule => <li key={schedule.id}>
        <button disabled={busy} onClick={() => void run(() => loadHistory(schedule, 1))}>{schedule.code} · {schedule.equipment}</button>
        <p>{schedule.department} · {schedule.workType === 'Calibration' ? 'Hiệu chuẩn' : 'Bảo trì'} · {schedule.isActive ? 'Đang áp dụng' : 'Ngừng áp dụng'}</p>
        <p>Chu kỳ {schedule.intervalDays} ngày · Nhắc trước {schedule.reminderLeadMinutes} phút · Hạn sau kỳ {schedule.completionWindowMinutes} phút</p>
        <p>Kỳ tiếp theo cần lập {time.format(new Date(schedule.nextOccurrenceAtUtc))}</p>
      </li>)}</ul>
      <div className="toolbar">
        <button disabled={busy || query.page <= 1} onClick={() => void run(() => loadSchedules({ ...query, page: query.page - 1 }))}>Trang trước</button>
        <span>Trang {query.page}</span>
        <button disabled={busy || !schedules.hasNext} onClick={() => void run(() => loadSchedules({ ...query, page: query.page + 1 }))}>Trang sau</button>
      </div>
    </>}
    {history && <section className="panel">
      <h3>Các kỳ của {history.schedule.code}</h3>
      {history.result.items.length === 0 && <p>Chưa có kỳ nào.</p>}
      <ul>{history.result.items.map(row => <li key={row.id}>
        {time.format(new Date(row.scheduledOccurrenceAtUtc))} · Hạn {time.format(new Date(row.dueAtUtc))}
        {' · '}{occurrenceStates[row.state] ?? row.state}{' '}
        {row.workOrderId && <button disabled={busy} onClick={() => openOrder(row.workOrderId!)}>Xem lệnh liên kết</button>}
      </li>)}</ul>
      <div className="toolbar">
        <button disabled={busy || history.page <= 1} onClick={() => void run(() => loadHistory(history.schedule, history.page - 1))}>Kỳ trước</button>
        <span>Trang {history.page}</span>
        <button disabled={busy || !history.result.hasNext} onClick={() => void run(() => loadHistory(history.schedule, history.page + 1))}>Kỳ sau</button>
      </div>
    </section>}
  </details>;
}
