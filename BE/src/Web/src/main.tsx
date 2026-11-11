import React, { useRef, useState } from 'react';
import { createRoot } from 'react-dom/client';
import { WorkOrders } from './WorkOrders';
import './style.css';

type Tokens = { accessToken: string; refreshToken: string };
type User = { id: string; roles: string[]; departmentId: string | null };
type Department = { id: string; code: string; name: string; isActive: boolean; rowVersion: string };
type DepartmentPage = { items: Department[]; total: number };
type Asset = { id: string; code: string; name: string; department: string; status: string };
type AssetPage = { items: Asset[]; hasNext: boolean };
type Employee = { id: string; name: string; email: string | null; role: string; isActive: boolean; emailConfirmed: boolean; rowVersion: string };
type EmployeePage = { items: Employee[]; page: number; hasNext: boolean };
type Incident = { id: string; code: string; title: string; status: string; assignedTechnicianId: string | null; rowVersion: string;
  equipmentId: string; equipmentNameSnapshot: string; responseOutcome: string; resolutionOutcome: string };
type Attempt = { id: string; attemptNumber: number; resultSummary: string; resolutionNotes: string; reviewDecision: string; reviewReason: string | null; hasPhoto: boolean };

class ApiError extends Error {
  constructor(readonly status: number) {
    super(({400: 'Kiểm tra dữ liệu đã nhập.', 401: 'Thông tin đăng nhập sai hoặc phiên đã hết hạn.',
      403: 'Bạn không có quyền thực hiện.', 404: 'Không tìm thấy bản ghi.',
      409: 'Mã đã tồn tại hoặc dữ liệu đã thay đổi. Hãy tải lại.',
      503: 'Dịch vụ tạm thời không khả dụng.'} as Record<number, string>)[status] ?? `Yêu cầu thất bại (HTTP ${status}).`);
  }
}

async function request<T>(path: string, options: RequestInit = {}, token?: string): Promise<T> {
  const headers = new Headers(options.headers);
  if (options.body && !(options.body instanceof FormData)) headers.set('Content-Type', 'application/json');
  if (token) headers.set('Authorization', `Bearer ${token}`);
  const response = await fetch('/api/' + path, { ...options, headers, cache: 'no-store' });
  if (!response.ok) throw new ApiError(response.status);
  if (response.status === 204) return undefined as T;
  if (response.headers.get('Content-Type')?.startsWith('image/')) return await response.blob() as T;
  return await response.json();
}

function App() {
  const tokens = useRef<Tokens | null>(null);
  const [user, setUser] = useState<User>();
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  const inFlight = useRef(false);
  const [departments, setDepartments] = useState<DepartmentPage>();
  const [search, setSearch] = useState('');
  const [filter, setFilter] = useState('');
  const [sort, setSort] = useState('name');
  const [page, setPage] = useState(1);
  const [edit, setEdit] = useState<Department>();
  const [assets, setAssets] = useState<AssetPage>();
  const [assetSearch, setAssetSearch] = useState('');
  const [assetPage, setAssetPage] = useState(1);
  const [tab, setTab] = useState<'departments' | 'assets' | 'employees' | 'incidents' | 'maintenance'>('assets');
  const [incidents, setIncidents] = useState<Incident[]>([]);
  const incidentRequest = useRef<{ body: string; key: string } | undefined>(undefined);
  const [incident, setIncident] = useState<Incident>();
  const [attempts, setAttempts] = useState<Attempt[]>([]);
  const [policies, setPolicies] = useState<{ priority: string; isEnabled: boolean; rowVersion: string }[]>([]);
  const [photo, setPhoto] = useState('');
  const photoUrl = useRef('');
  function showPhoto(url = '') {
    if (photoUrl.current) URL.revokeObjectURL(photoUrl.current);
    photoUrl.current = url;
    setPhoto(url);
  }
  const [employees, setEmployees] = useState<EmployeePage>();
  const [employeeSearch, setEmployeeSearch] = useState('');

  function clearSession() {
    tokens.current = null;
    setUser(undefined);
    setDepartments(undefined);
    setEmployees(undefined);
    setIncident(undefined);
    setIncidents([]);
    incidentRequest.current = undefined;
    setAttempts([]);
    setPolicies([]);
    showPhoto();
    setAssets(undefined);
    setAssetSearch('');
    setAssetPage(1);
    setEdit(undefined);
  }

  async function run(action: () => Promise<void>) {
    if (inFlight.current) return;
    inFlight.current = true;
    setBusy(true);
    setError('');
    try { await action(); }
    catch (e) { setError(e instanceof Error ? e.message : 'Đã xảy ra lỗi.'); }
    finally { inFlight.current = false; setBusy(false); }
  }

  async function api<T>(path: string, options?: RequestInit): Promise<T> {
    const current = tokens.current;
    if (!current) throw new ApiError(401);
    try { return await request<T>(path, options, current.accessToken); }
    catch (error) {
      if (!(error instanceof ApiError) || error.status !== 401) throw error;
      try {
        const next = await request<Tokens>('identity/api/auth/refresh', {
          method: 'POST', body: JSON.stringify({ refreshToken: current.refreshToken }),
        });
        tokens.current = next;
        return await request<T>(path, options, next.accessToken);
      } catch (retryError) {
        if (retryError instanceof ApiError && retryError.status === 401) clearSession();
        throw retryError;
      }
    }
  }

  async function loadDepartments(nextPage = 1) {
    const query = new URLSearchParams({ search, sort, page: String(nextPage), pageSize: '10' });
    if (filter) query.set('active', filter);
    setDepartments(await api<DepartmentPage>('equipment/api/departments?' + query));
    setPage(nextPage);
  }

  async function loadAssets(page: number, search: string) {
    const query = new URLSearchParams({ search, page: String(page) });
    setAssets(await api<AssetPage>('equipment/api/equipment?' + query));
    setAssetSearch(search);
    setAssetPage(page);
  }

  async function loadEmployees(page = 1) {
    const query = new URLSearchParams({ search: employeeSearch, page: String(page) });
    setEmployees(await api<EmployeePage>('identity/api/users?' + query));
  }

  async function reloadIncident(id: string) {
    const [rows, history] = await Promise.all([
      api<Incident[]>('incident/api/incidents?id=' + encodeURIComponent(id)),
      api<Attempt[]>('incident/api/incidents/' + id + '/attempts'),
    ]);
    setIncident(rows[0]);
    setIncidents(previous => previous.map(row => row.id === id && rows[0] ? rows[0] : row));
    setAttempts(history);
  }

  function login(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = new FormData(event.currentTarget);
    void run(async () => {
      const result = await request<Tokens>('identity/api/auth/login', {method: 'POST',
        body: JSON.stringify({userName: form.get('userName'), password: form.get('password')})});
      const current = await request<User>('identity/api/auth/me', {}, result.accessToken);
      tokens.current = result;
      setUser(current);
      setTab(current.roles.includes('Admin') ? 'departments' : 'assets');
    });
  }

  function saveDepartment(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const element = event.currentTarget;
    const form = new FormData(element);
    void run(async () => {
      await api('equipment/api/departments' + (edit ? '/' + edit.id : ''), {
        method: edit ? 'PUT' : 'POST',
        body: JSON.stringify(edit ? {name: form.get('name'), rowVersion: edit.rowVersion}
          : {code: form.get('code'), name: form.get('name')}),
      });
      setEdit(undefined);
      element.reset();
      await loadDepartments();
    });
  }

  return <main>
    <header><p className="eyebrow">QUẢN LÝ THIẾT BỊ Y TẾ</p><h1>MedEquipSLA</h1></header>
    {error && <p className="error" role="alert">{error}</p>}
    {!user ? <form className="panel login" onSubmit={login}>
      <h2>Đăng nhập</h2>
      <label>Tên đăng nhập<input name="userName" autoComplete="username" required maxLength={256} /></label>
      <label>Mật khẩu<input name="password" type="password" autoComplete="current-password" required maxLength={1024} /></label>
      <button disabled={busy}>{busy ? 'Đang đăng nhập…' : 'Đăng nhập'}</button>
    </form> : <>
      <div className="toolbar panel"><strong>{user.roles.join(', ')}</strong>
        {[false, true].map(all => <button key={String(all)} className="secondary" disabled={busy}
          onClick={() => void run(async () => {
            await api('identity/api/auth/logout?all=' + all, {method: 'POST'});
            clearSession();
          })}>{all ? 'Đăng xuất tất cả' : 'Đăng xuất'}</button>)}
      </div>
      <nav className="toolbar" aria-label="Chức năng">
        {user.roles.includes('Admin') && <button disabled={busy} onClick={() => {
          setTab('departments'); void run(() => loadDepartments());
        }}>Khoa / phòng</button>}
        {user.roles.includes('Admin') && <button disabled={busy} onClick={() => {
          setTab('employees'); void run(() => loadEmployees());
        }}>Nhân viên</button>}
        <button disabled={busy} onClick={() => setTab('assets')}>Tra cứu thiết bị</button>
        {(user.roles.includes('Admin') || user.roles.includes('Technician')) &&
          <button disabled={busy} onClick={() => setTab('maintenance')}>Bảo trì và hiệu chuẩn</button>}
        <button disabled={busy} onClick={() => {
          setTab('incidents'); void run(async () => setIncidents(await api<Incident[]>('incident/api/incidents')));
        }}>Sự cố và ảnh sửa chữa</button>
      </nav>
      {tab === 'departments' && user.roles.includes('Admin') ? <>
        <form className="panel toolbar" onSubmit={e => {e.preventDefault(); void run(() => loadDepartments());}}>
          <label>Tìm khoa<input value={search} maxLength={200} onChange={e => setSearch(e.target.value)} /></label>
          <label>Trạng thái<select value={filter} onChange={e => setFilter(e.target.value)}>
            <option value="">Tất cả</option><option value="true">Đang hoạt động</option><option value="false">Đã ngừng</option>
          </select></label>
          <label>Sắp xếp<select value={sort} onChange={e => setSort(e.target.value)}>
            <option value="name">Tên A–Z</option><option value="-name">Tên Z–A</option>
            <option value="code">Mã tăng dần</option><option value="-code">Mã giảm dần</option>
          </select></label>
          <button disabled={busy}>Tải danh sách</button>
        </form>
        <form className="panel toolbar" key={edit?.id ?? 'new'} onSubmit={saveDepartment}>
          <label>Mã khoa<input name="code" required maxLength={50} defaultValue={edit?.code} readOnly={!!edit}/></label>
          <label>Tên khoa<input name="name" required maxLength={200} defaultValue={edit?.name}/></label>
          <button disabled={busy}>{edit ? 'Lưu thay đổi' : 'Tạo khoa'}</button>
          {edit && <button type="button" className="secondary" disabled={busy} onClick={() => setEdit(undefined)}>Hủy sửa</button>}
        </form>
        {departments && <div className="panel"><div className="table-scroll"><table><thead><tr>
          <th>Mã</th><th>Tên khoa</th><th>Trạng thái</th><th>Thao tác</th>
        </tr></thead><tbody>{departments.items.map(d => <tr key={d.id}>
          <td>{d.code}</td><td>{d.name}</td><td>{d.isActive ? 'Đang hoạt động' : 'Đã ngừng'}</td>
          <td><button className="secondary" disabled={busy} onClick={() => setEdit(d)}>Sửa</button>
          {d.isActive && <button className="secondary" disabled={busy} onClick={() => {
            if (!window.confirm(`Ngừng sử dụng khoa ${d.name}?`)) return;
            void run(async () => {
              await api('equipment/api/departments/' + d.id + '?' + new URLSearchParams({version: d.rowVersion}), {method: 'DELETE'});
              setEdit(undefined);
              await loadDepartments();
            });
          }}>Ngừng sử dụng</button>}</td>
        </tr>)}</tbody></table></div>
        {!departments.items.length && <p>Không có khoa phù hợp.</p>}
        <div className="toolbar"><span>Trang {page} · {departments.total} khoa</span>
          <button className="secondary" disabled={busy || page === 1} onClick={() => void run(() => loadDepartments(page - 1))}>Trước</button>
          <button className="secondary" disabled={busy || page * 10 >= departments.total} onClick={() => void run(() => loadDepartments(page + 1))}>Sau</button>
        </div></div>}
      </> : tab === 'employees' && user.roles.includes('Admin') ? <section className="panel">
        <h2>Quản lý nhân viên</h2>
        <form className="toolbar" onSubmit={event => {
          event.preventDefault(); void run(() => loadEmployees());
        }}>
          <label>Tìm tên hoặc email<input value={employeeSearch} maxLength={200}
            onChange={event => setEmployeeSearch(event.target.value)} /></label>
          <button disabled={busy}>Tìm kiếm</button>
        </form>
        {employees && <>
          <div className="table-scroll"><table><thead><tr>
            <th>Nhân viên</th><th>Vai trò</th><th>Trạng thái</th><th>Thao tác</th>
          </tr></thead><tbody>{employees.items.map(employee => <tr key={employee.id}>
            <td>{employee.name}<br />{employee.email}<br /><small>{employee.id}</small></td>
            <td>{employee.role}</td>
            <td>{employee.isActive ? 'Đang hoạt động' : 'Đã vô hiệu hóa'}</td>
            <td><button type="button" className="secondary" disabled={busy || (!employee.isActive && !employee.emailConfirmed)}
              onClick={() => {
                if (employee.isActive && !window.confirm('Vô hiệu hóa tài khoản và thu hồi các phiên đăng nhập?')) return;
                void run(async () => {
                  await api('identity/api/users/' + employee.id + '/access', {
                    method: 'PUT',
                    body: JSON.stringify({ active: !employee.isActive, rowVersion: employee.rowVersion }),
                  });
                  await loadEmployees(employees.page);
                });
              }}>{employee.isActive ? 'Vô hiệu hóa' : 'Kích hoạt lại'}</button></td>
          </tr>)}</tbody></table></div>
          {!employees.items.length && <p>Không có nhân viên phù hợp.</p>}
          <div className="toolbar"><span>Trang {employees.page}</span>
            <button className="secondary" disabled={busy || employees.page === 1} onClick={() => void run(() => loadEmployees(employees.page - 1))}>Trước</button>
            <button className="secondary" disabled={busy || !employees.hasNext} onClick={() => void run(() => loadEmployees(employees.page + 1))}>Sau</button>
          </div>
        </>}
      </section> : tab === 'maintenance' ? <WorkOrders api={api} run={run} busy={busy} canAssign={user.roles.includes('Admin')} /> : tab === 'incidents' ? <section className="panel">
        <h2>Sự cố và ảnh sửa chữa</h2>
        {user.roles.includes('Admin') && <details className="panel">
          <summary>Cấu hình SLA</summary>
          <form className="toolbar" onSubmit={event => {
            event.preventDefault();
            const fields = new FormData(event.currentTarget);
            const priority = String(fields.get('priority'));
            void run(async () => {
              const existing = policies.find(policy => policy.priority === priority);
              await api('incident/api/sla-policies/' + priority, {
                method: 'PUT', body: JSON.stringify({
                  enabled: fields.get('enabled') === 'on', rowVersion: existing?.rowVersion,
                  responseDurationMinutes: Number(fields.get('response')),
                  resolutionDurationMinutes: Number(fields.get('resolution')),
                }),
              });
              setPolicies(await api<typeof policies>('incident/api/sla-policies'));
            });
          }}>
            <label>Mức ưu tiên<select name="priority" defaultValue="Medium">
              <option value="Low">Thấp</option><option value="Medium">Trung bình</option>
              <option value="High">Cao</option><option value="Critical">Khẩn cấp</option>
            </select></label>
            <label>Phút phản hồi<input name="response" type="number" min="1" /></label>
            <label>Phút xử lý<input name="resolution" type="number" min="1" /></label>
            <label><input name="enabled" type="checkbox" defaultChecked /> Áp dụng</label>
            <button type="button" disabled={busy} onClick={() => void run(async () =>
              setPolicies(await api<typeof policies>('incident/api/sla-policies')))}>Tải cấu hình</button>
            <button disabled={busy}>Lưu SLA</button>
          </form>
          <ul>{policies.map(policy => <li key={policy.priority}>{policy.priority} · {policy.isEnabled ? 'Đang áp dụng' : 'Đã tắt'}</li>)}</ul>
        </details>}
        {(user.roles.includes('Admin') || user.roles.includes('DepartmentStaff')) &&
          <form className="panel" onSubmit={event => {
            event.preventDefault();
            const element = event.currentTarget;
            const form = new FormData(element);
            const body = JSON.stringify({
              equipmentId: form.get('equipmentId'), title: form.get('title'), description: form.get('description'),
              priority: form.get('priority'), relatedIncidentId: form.get('relatedIncidentId') || null,
            });
            void run(async () => {
              if (incidentRequest.current?.body !== body) incidentRequest.current = { body, key: crypto.randomUUID() };
              const created = await api<{ id: string }>('incident/api/incidents', {
                method: 'POST', headers: { 'Idempotency-Key': incidentRequest.current.key }, body,
              });
              incidentRequest.current = undefined;
              element.reset();
              setIncidents(await api<Incident[]>('incident/api/incidents'));
              await reloadIncident(created.id);
            });
          }}>
            <h3>Báo sự cố</h3>
            <div className="toolbar">
              <label>Tìm thiết bị<input name="assetSearch" maxLength={200} defaultValue={assetSearch} /></label>
              <button type="button" disabled={busy} onClick={event => {
                const form = event.currentTarget.form;
                if (form) void run(() => loadAssets(1, String(new FormData(form).get('assetSearch')).trim()));
              }}>Tìm</button>
              {assets && <div className="toolbar">
                <button type="button" disabled={busy || assetPage <= 1}
                  onClick={() => void run(() => loadAssets(assetPage - 1, assetSearch))}>Trước</button>
                <button type="button" disabled={busy || !assets.hasNext}
                  onClick={() => void run(() => loadAssets(assetPage + 1, assetSearch))}>Sau</button>
              </div>}
              <label>Thiết bị<select name="equipmentId" required defaultValue="">
                <option value="">Chọn thiết bị</option>
                {assets?.items.filter(asset => asset.status !== 'Retired').map(asset =>
                  <option key={asset.id} value={asset.id}>{asset.code} · {asset.name}</option>)}
              </select></label>
              <label>Mức ưu tiên<select name="priority" defaultValue="Medium">
                <option value="Low">Thấp</option><option value="Medium">Trung bình</option>
                <option value="High">Cao</option><option value="Critical">Khẩn cấp</option>
              </select></label>
            </div>
            <label>Tiêu đề<input name="title" required maxLength={200} /></label>
            <label>Mô tả<textarea name="description" required maxLength={4000} /></label>
            {user.roles.includes('Admin') && <label>Sự cố liên quan nếu đây là lỗi độc lập
              <select name="relatedIncidentId" defaultValue=""><option value="">Không có</option>
                {incidents.filter(row => row.status !== 'Closed').map(row =>
                  <option key={row.id} value={row.id}>{row.code} · {row.title}</option>)}
              </select></label>}
            <button disabled={busy}>Gửi báo cáo</button>
          </form>}
        <label>Chọn sự cố<select disabled={busy} value={incident?.id ?? ''} onChange={event => {
          const selected = incidents.find(row => row.id === event.target.value);
          setIncident(selected); setAttempts([]); showPhoto();
          if (selected) void run(async () =>
            setAttempts(await api<Attempt[]>('incident/api/incidents/' + selected.id + '/attempts')));
        }}>
          <option value="">Chọn một hồ sơ</option>
          {incidents.map(row => <option key={row.id} value={row.id}>{row.code} · {row.title} · {row.status}</option>)}
        </select></label>
        {!incidents.length && <p>Chưa có hồ sơ sự cố trong phạm vi của bạn.</p>}
        {incident && <p>Thiết bị: {incident.equipmentNameSnapshot}</p>}
        {incident?.status === 'Closed' && <p>
          SLA phản hồi: {incident.responseOutcome === 'Met' ? 'Đạt' : 'Quá hạn'}.
          SLA xử lý: {incident.resolutionOutcome === 'Met' ? 'Đạt' : 'Quá hạn'}.
        </p>}
        {incident && user.roles.includes('Admin') && ['Open', 'InProgress'].includes(incident.status) &&
          <form className="panel toolbar" key={incident.id} onSubmit={event => {
            event.preventDefault();
            const technicianId = String(new FormData(event.currentTarget).get('technician')).trim();
            void run(async () => {
              await api('incident/api/incidents/' + incident.id + '/assignment', {
                method: 'PUT', body: JSON.stringify({ technicianId, rowVersion: incident.rowVersion }),
              });
              await reloadIncident(incident.id);
            });
          }}>
            <label>ID kỹ thuật viên<input name="technician" required defaultValue={incident.assignedTechnicianId ?? ''} /></label>
            <button disabled={busy}>Phân công</button>
          </form>}
        {incident?.status === 'Open' && user.roles.includes('Technician') && incident.assignedTechnicianId === user.id &&
          <button disabled={busy} onClick={() => void run(async () => {
            await api('incident/api/incidents/' + incident.id + '/start', {
              method: 'POST', body: JSON.stringify({ rowVersion: incident.rowVersion }),
            });
            await reloadIncident(incident.id);
          })}>Tiếp nhận sửa chữa</button>}
        {incident?.status === 'InProgress' &&
          (user.roles.includes('Admin') || (user.roles.includes('Technician') && incident.assignedTechnicianId === user.id)) &&
          <form className="panel" onSubmit={event => {
            event.preventDefault();
            const form = new FormData(event.currentTarget);
            form.set('rowVersion', incident.rowVersion);
            void run(async () => {
              const path = 'incident/api/incidents/' + incident.id;
              await api(path + '/attempts', { method: 'POST', body: form });
              await reloadIncident(incident.id);
            });
          }}>
            <h3>Báo sửa hoàn thành</h3>
            <label>Biên bản<textarea name="notes" required maxLength={4000} /></label>
            <label>Kết quả chạy thử<textarea name="result" required maxLength={2000} /></label>
            <label>Ảnh thiết bị đang chạy sau sửa<input name="photo" type="file" required accept="image/jpeg,image/png" /></label>
            <p>JPEG hoặc PNG, tối đa 5 MB.</p>
            <button disabled={busy}>Gửi kết quả</button>
          </form>}
        {incident && attempts.map(attempt => <article key={attempt.id} className="panel">
          <strong>Lần {attempt.attemptNumber} · {attempt.reviewDecision}</strong>
          <p>{attempt.resolutionNotes}</p><p>{attempt.resultSummary}</p>
          {attempt.reviewReason && <p>Lý do trả lại: {attempt.reviewReason}</p>}
          {incident.status === 'Resolved' && user.roles.includes('Admin') && attempt.reviewDecision === 'Pending' &&
            attempt.id === attempts[0]?.id && <form className="toolbar" onSubmit={event => {
              event.preventDefault();
              const reason = new FormData(event.currentTarget).get('reason');
              void run(async () => {
                await api('incident/api/incidents/' + incident.id + '/attempts/' + attempt.id + '/reject', {
                  method: 'POST', body: JSON.stringify({ reason, rowVersion: incident.rowVersion }),
                });
                await reloadIncident(incident.id);
              });
            }}>
              <label>Lý do trả lại<input name="reason" required maxLength={2000} /></label>
              <button disabled={busy}>Yêu cầu sửa lại</button>
            </form>}
          {attempt.hasPhoto && incident.status === 'Resolved' && user.roles.includes('Admin') &&
            attempt.id === attempts[0]?.id && attempt.reviewDecision === 'Pending' &&
            <button disabled={busy} onClick={() => {
              if (!window.confirm('Xác nhận thiết bị đã chạy và đóng sự cố?')) return;
              void run(async () => {
                await api('incident/api/incidents/' + incident.id + '/attempts/' + attempt.id + '/accept', {
                  method: 'POST', body: JSON.stringify({ rowVersion: incident.rowVersion }),
                });
                await reloadIncident(incident.id);
              });
            }}>Nghiệm thu và đóng</button>}
          {attempt.hasPhoto && <button disabled={busy} onClick={() => void run(async () => {
            const blob = await api<Blob>('incident/api/incidents/' + incident.id + '/attempts/' + attempt.id + '/photo');
            showPhoto(URL.createObjectURL(blob));
          })}>Xem ảnh lần {attempt.attemptNumber}</button>}
        </article>)}
        {photo && <img src={photo} alt="Ảnh bằng chứng sau sửa" style={{maxWidth:'100%'}} />}
      </section> : <section className="panel">
        <h2>Tra cứu thiết bị</h2>
        <form className="toolbar" onSubmit={event => {
          event.preventDefault();
          const search = String(new FormData(event.currentTarget).get('search')).trim();
          void run(() => loadAssets(1, search));
        }}>
          <label>Mã hoặc tên thiết bị<input name="search" maxLength={200} defaultValue={assetSearch} /></label>
          <button disabled={busy}>Tra cứu</button>
        </form>
        {assets?.items.length === 0 && <p>Không tìm thấy thiết bị.</p>}
        {assets && assets.items.length > 0 && <>
          <table>
            <thead><tr><th>Mã</th><th>Tên thiết bị</th><th>Khoa</th><th>Trạng thái</th></tr></thead>
            <tbody>{assets.items.map(asset => <tr key={asset.id}>
              <td>{asset.code}</td><td>{asset.name}</td><td>{asset.department}</td><td>{asset.status}</td>
            </tr>)}</tbody>
          </table>
          <div className="toolbar">
            <button disabled={busy || assetPage === 1} onClick={() => void run(() => loadAssets(assetPage - 1, assetSearch))}>Trang trước</button>
            <span>Trang {assetPage}</span>
            <button disabled={busy || !assets.hasNext} onClick={() => void run(() => loadAssets(assetPage + 1, assetSearch))}>Trang sau</button>
          </div>
        </>}
      </section>}
    </>}
    <div role="status" aria-live="polite">{busy ? 'Đang xử lý…' : ''}</div>
  </main>;
}
createRoot(document.getElementById('root')!).render(<React.StrictMode><App /></React.StrictMode>);

