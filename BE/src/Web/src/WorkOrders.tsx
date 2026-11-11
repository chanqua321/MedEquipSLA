import { useState } from 'react';
import { MaintenanceSchedules } from './MaintenanceSchedules';

type Row = { id: string; code: string; equipment: string; department: string; workType: string;
  status: string; scheduledForUtc: string; dueAtUtc: string; isBlocking: boolean; assignedTechnicianId: string | null; rowVersion: string };
type Page = { items: Row[]; hasNext: boolean };
type Attempt = { attemptNumber: number; resultStatus: string; notes: string; completedAtUtc: string;
  reviewDecision: string; reviewReason: string | null };
type Props = { api: <T>(path: string, options?: RequestInit) => Promise<T>;
  run: (action: () => Promise<void>) => Promise<void>; busy: boolean; canAssign: boolean };

const statuses = { Scheduled: 'Đã lên lịch', InProgress: 'Đang thực hiện', Completed: 'Chờ nghiệm thu',
  Closed: 'Đã nghiệm thu', Cancelled: 'Đã hủy' };
const clock = new Intl.DateTimeFormat('vi-VN', {
  dateStyle: 'short', timeStyle: 'short', timeZone: 'Asia/Ho_Chi_Minh',
});
const path = 'maintenance/api/work-orders';

export function WorkOrders({ api, run, busy, canAssign }: Props) {
  const [result, setResult] = useState<Page>();
  const [page, setPage] = useState(1);
  const [filters, setFilters] = useState({ search: '', status: '' });
  const [history, setHistory] = useState<{ order: Row; attempts: Attempt[] }>();

  async function load(next: number, filter: typeof filters) {
    const query = new URLSearchParams({ search: filter.search, page: String(next) });
    if (filter.status) query.set('status', filter.status);
    setResult(await api<Page>(path + '?' + query));
    setFilters(filter);
    setPage(next);
    setHistory(undefined);
  }

  async function openOrder(id: string) {
    const [order, attempts] = await Promise.all([
      api<Row>(path + '/' + id), api<Attempt[]>(path + '/' + id + '/attempts'),
    ]);
    setHistory({ order, attempts });
  }

  return <section className="panel">
    <h2>Lệnh bảo trì và hiệu chuẩn</h2>
    {canAssign && <MaintenanceSchedules api={api} run={run} busy={busy} openOrder={id => void run(() => openOrder(id))} />}
    <form className="toolbar" onSubmit={event => {
      event.preventDefault();
      const data = new FormData(event.currentTarget);
      void run(() => load(1, { search: String(data.get('search')).trim(), status: String(data.get('status')) }));
    }}>
      <label>Mã lệnh hoặc tên thiết bị<input name="search" maxLength={200} /></label>
      <label>Trạng thái<select name="status">
        <option value="">Tất cả</option>
        {Object.entries(statuses).map(([value, label]) => <option key={value} value={value}>{label}</option>)}
      </select></label>
      <button disabled={busy}>Tìm lệnh</button>
    </form>
    {result?.items.length === 0 && <p>Không có lệnh phù hợp.</p>}
    {result && result.items.length > 0 && <>
      <ul>{result.items.map(order => <li key={order.id}>
        <button disabled={busy} onClick={() => void run(() => openOrder(order.id))}>{order.code} · {order.equipment}</button>
        <p>{order.department} · {order.workType === 'Calibration' ? 'Hiệu chuẩn' : 'Bảo trì'} · {statuses[order.status as keyof typeof statuses]}</p>
        <p>Lịch {clock.format(new Date(order.scheduledForUtc))} · Hạn {clock.format(new Date(order.dueAtUtc))}
          {order.isBlocking && ['InProgress', 'Completed'].includes(order.status) && ' · Đang chặn sử dụng thiết bị'}</p>
      </li>)}</ul>
      <div className="toolbar">
        <button disabled={busy || page === 1} onClick={() => void run(() => load(page - 1, filters))}>Trước</button>
        <span>Trang {page}</span>
        <button disabled={busy || !result.hasNext} onClick={() => void run(() => load(page + 1, filters))}>Sau</button>
      </div>
    </>}
    {history && <section className="panel">
      <h3>Lịch sử {history.order.code}</h3>
      {canAssign && ['Scheduled', 'InProgress'].includes(history.order.status) && <form className="toolbar"
        key={history.order.id} onSubmit={event => {
          event.preventDefault();
          const technicianId = String(new FormData(event.currentTarget).get('technicianId')).trim();
          void run(async () => {
            await api(path + '/' + history.order.id + '/assignment', {
              method: 'PUT', body: JSON.stringify({ technicianId, rowVersion: history.order.rowVersion }),
            });
            await load(page, filters);
          });
        }}>
        <label>ID kỹ thuật viên<input name="technicianId" required maxLength={36}
          defaultValue={history.order.assignedTechnicianId ?? ''} /></label>
        <button disabled={busy}>Phân công</button>
      </form>}
      {history.attempts.length === 0 && <p>Chưa có kết quả thực hiện.</p>}
      {history.attempts.map(attempt => <article key={attempt.attemptNumber}>
        <strong>Lần {attempt.attemptNumber} · {attempt.resultStatus === 'Passed' ? 'Đạt' : 'Không đạt'}</strong>
        <p>{clock.format(new Date(attempt.completedAtUtc))} · {attempt.reviewDecision}</p>
        <p>{attempt.notes}</p>
        {attempt.reviewReason && <p>Lý do trả lại: {attempt.reviewReason}</p>}
      </article>)}
    </section>}
  </section>;
}
