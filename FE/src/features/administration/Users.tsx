import { useState, type FormEvent } from 'react';
import { identityApi } from '../../api/identity';
import type { Employee } from '../../api/contracts';
import { useData } from '../../hooks/useData';
import { useMutation } from '../../hooks/useMutation';
import {
  Badge,
  Button,
  Confirm,
  DataState,
  Dialog,
  Empty,
  ErrorMessage,
  Field,
  FormActions,
  KeyValues,
  Notice,
  PageHeader,
  Pagination,
  Panel,
  Select,
  Table,
  useToast,
} from '../../components/ui';
import { EntityPicker } from '../../components/EntityPicker';
import { textValue } from '../../utils/validation';
export function Users() {
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);
  const [editing, setEditing] = useState<Employee | null | undefined>();
  const [role, setRole] = useState('DepartmentStaff');
  const [access, setAccess] = useState<Employee>();
  const [importing, setImporting] = useState(false);
  const mutation = useMutation(),
    toast = useToast();
  const state = useData(`users:${search}:${page}`, (signal) =>
    identityApi.users(search, page, signal),
  );
  function edit(row: Employee | null) {
    mutation.clearError();
    setRole(row?.role === 'Technician' ? 'Technician' : 'DepartmentStaff');
    setEditing(row);
  }
  function save(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    const data = new FormData(e.currentTarget);
    void mutation.run(async () => {
      const fullName = textValue(data.get('fullName'), 'Họ tên', 200);
      const departmentId = String(data.get('departmentId') ?? '') || null;
      if (editing)
        await identityApi.profile(editing.id, {
          fullName,
          role,
          departmentId,
          rowVersion: editing.rowVersion,
        });
      else
        await identityApi.invite({
          email: textValue(data.get('email'), 'Email', 256),
          fullName,
          role,
          departmentId,
        });
      setEditing(undefined);
      state.reload();
      toast(
        editing
          ? 'Đã cập nhật nhân viên. Quyền và phiên cũ được BE xử lý.'
          : 'Đã tiếp nhận lời mời. Nhân viên cần kích hoạt từ email.',
      );
    });
  }
  return (
    <>
      <PageHeader
        title="Nhân sự và phân quyền"
        description="Mời nhân viên, phân khoa và kiểm soát quyền truy cập theo vai trò."
        actions={
          <>
            <Button kind="secondary" icon="file" onClick={() => setImporting(true)}>
              Nhập Excel
            </Button>
            <Button icon="plus" onClick={() => edit(null)}>
              Mời nhân viên
            </Button>
          </>
        }
      />
      <Panel>
        <form
          className="filter-bar"
          onSubmit={(e) => {
            e.preventDefault();
            setSearch(String(new FormData(e.currentTarget).get('search')).trim());
            setPage(1);
          }}
        >
          <Field
            name="search"
            type="search"
            label="Tìm nhân viên"
            placeholder="Tên hoặc email…"
            maxLength={200}
          />
          <Button type="submit" kind="secondary" icon="search">
            Tìm kiếm
          </Button>
        </form>
        <DataState state={state}>
          {(result) => (
            <>
              {result.items.length ? (
                <Table headers={['Nhân viên', 'Vai trò', 'Tài khoản', 'Thao tác']}>
                  {result.items.map((row) => (
                    <tr key={row.id}>
                      <td>
                        <strong>{row.name}</strong>
                        <small>{row.email}</small>
                      </td>
                      <td>
                        {row.role === 'Admin'
                          ? 'Quản trị viên'
                          : row.role === 'Technician'
                            ? 'Kỹ thuật viên'
                            : 'Nhân viên khoa'}
                      </td>
                      <td>
                        <Badge
                          value={
                            row.isInvitationSuspended
                              ? 'Inactive'
                              : !row.emailConfirmed
                                ? 'PendingActivation'
                                : row.isActive
                                  ? 'Active'
                                  : 'Inactive'
                          }
                        />
                      </td>
                      <td>
                        <div className="row-actions">
                          <Button kind="ghost" onClick={() => edit(row)}>
                            Chi tiết
                          </Button>
                          {row.role !== 'Admin' && (
                            <Button
                              kind="ghost"
                              onClick={() => {
                                mutation.clearError();
                                setAccess(row);
                              }}
                            >
                              {!row.emailConfirmed
                                ? row.isInvitationSuspended
                                  ? 'Gỡ đình chỉ'
                                  : 'Đình chỉ lời mời'
                                : row.isActive
                                  ? 'Khóa'
                                  : 'Mở khóa'}
                            </Button>
                          )}
                        </div>
                      </td>
                    </tr>
                  ))}
                </Table>
              ) : (
                <Empty title="Chưa có nhân viên phù hợp" />
              )}
              <Pagination page={page} hasNext={result.hasNext} onChange={setPage} />
            </>
          )}
        </DataState>
      </Panel>
      {editing !== undefined && (
        <Dialog
          title={editing ? 'Chi tiết nhân viên' : 'Mời nhân viên mới'}
          onClose={() => setEditing(undefined)}
          busy={mutation.busy}
          drawer
        >
          <form className="stack" onSubmit={save}>
            {editing && (
              <KeyValues
                items={[
                  ['Mã nhân viên', editing.id],
                  ['Email', editing.email],
                  ['Kích hoạt', editing.emailConfirmed ? 'Đã kích hoạt' : 'Chưa kích hoạt'],
                ]}
              />
            )}
            <Field
              label="Họ và tên"
              name="fullName"
              required
              maxLength={200}
              defaultValue={editing?.name}
              readOnly={editing?.role === 'Admin'}
            />
            {!editing && <Field label="Email" name="email" type="email" required maxLength={256} />}{' '}
            {editing?.role === 'Admin' ? (
              <Notice>
                Admin không được thay đổi thành vai trò khác hoặc tạo thêm Admin qua chức năng quản
                lý nhân viên.
              </Notice>
            ) : (
              <>
                <Select label="Vai trò" value={role} onChange={setRole}>
                  <option value="DepartmentStaff">Nhân viên khoa</option>
                  <option value="Technician">Kỹ thuật viên</option>
                </Select>
                <EntityPicker
                  key={editing?.id ?? 'new'}
                  kind="department"
                  name="departmentId"
                  label="Khoa phòng"
                  defaultValue={editing?.departmentId ?? ''}
                  required={role === 'DepartmentStaff'}
                />
                <Notice>
                  Đổi quyền hoặc khoa sẽ thu hồi các phiên và gỡ phân công không còn phù hợp theo
                  nghiệp vụ BE.
                </Notice>
                {editing && !editing.emailConfirmed && (
                  <Button
                    kind="secondary"
                    disabled={mutation.busy || editing.isInvitationSuspended}
                    onClick={() =>
                      void mutation.run(async () => {
                        await identityApi.resend(editing.id, editing.rowVersion);
                        setEditing(undefined);
                        state.reload();
                        toast('Đã tiếp nhận gửi lại lời mời. Link cũ không còn hiệu lực.');
                      })
                    }
                  >
                    Gửi lại lời mời kích hoạt
                  </Button>
                )}
                <ErrorMessage error={mutation.error} />
                <FormActions busy={mutation.busy} onCancel={() => setEditing(undefined)} />
              </>
            )}
          </form>
        </Dialog>
      )}
      {access && (
        <Confirm
          title={
            access.emailConfirmed
              ? access.isActive
                ? 'Khóa tài khoản'
                : 'Mở khóa tài khoản'
              : access.isInvitationSuspended
                ? 'Gỡ đình chỉ lời mời'
                : 'Đình chỉ lời mời'
          }
          description={`${access.name}: thay đổi này có thể thu hồi phiên và phân công đang mở. Gỡ đình chỉ lời mời không kích hoạt tài khoản; cần gửi lại link mới.`}
          busy={mutation.busy}
          error={mutation.error}
          onClose={() => setAccess(undefined)}
          onConfirm={() =>
            void mutation.run(async () => {
              await identityApi.access(access.id, {
                active: access.emailConfirmed ? !access.isActive : access.isInvitationSuspended,
                rowVersion: access.rowVersion,
              });
              setAccess(undefined);
              state.reload();
              toast('Đã cập nhật quyền truy cập.');
            })
          }
        />
      )}
      {importing && (
        <ImportUsers
          onClose={() => {
            setImporting(false);
            state.reload();
          }}
        />
      )}
    </>
  );
}
function ImportUsers({ onClose }: { onClose: () => void }) {
  const [file, setFile] = useState<File>();
  const [result, setResult] = useState<Awaited<ReturnType<typeof identityApi.import>>>();
  const mutation = useMutation(),
    toast = useToast();
  function send(preview: boolean) {
    void mutation.run(async () => {
      if (!file || !file.name.toLowerCase().endsWith('.xlsx') || file.size > 2097152)
        throw new Error('Chọn workbook .xlsx tối đa 2 MB.');
      const r = await identityApi.import(file, preview);
      setResult(r);
      if (!preview)
        toast('Đã xử lý import. Kiểm tra kết quả từng dòng, có thể thành công một phần.');
    });
  }
  return (
    <Dialog
      title="Nhập nhân viên từ Excel"
      description="Xem trước dữ liệu trước khi gửi lời mời."
      onClose={onClose}
      drawer
      busy={mutation.busy}
    >
      <div className="stack">
        <Notice>
          Cột A đến D: Email, FullName, Role, DepartmentId. Vai trò Technician hoặc DepartmentStaff.
          Tối đa 1000 dòng, không dùng công thức. Nhân viên khoa bắt buộc có DepartmentId hợp lệ.
        </Notice>
        <Field
          label="File nhân viên"
          type="file"
          accept=".xlsx"
          onChange={(e) => {
            setFile(e.target.files?.[0]);
            setResult(undefined);
          }}
        />
        <div className="actions">
          <Button kind="secondary" disabled={mutation.busy || !file} onClick={() => send(true)}>
            Xem trước
          </Button>
          <Button disabled={mutation.busy || !file || !result?.preview} onClick={() => send(false)}>
            Gửi lời mời các dòng hợp lệ
          </Button>
        </div>
        <ErrorMessage error={mutation.error} />
        {result && (
          <>
            <h3>{result.preview ? 'Kết quả xem trước' : 'Kết quả xử lý'}</h3>
            <Table headers={['Dòng', 'Kết quả', 'Tài khoản']}>
              {result.rows.map((row) => (
                <tr key={row.row}>
                  <td>{row.row}</td>
                  <td>{row.status}</td>
                  <td>{row.userId ?? '—'}</td>
                </tr>
              ))}
            </Table>
          </>
        )}
      </div>
    </Dialog>
  );
}
