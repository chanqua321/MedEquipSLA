import { useState, type FormEvent } from 'react';
import { useAuth } from '../../app/AuthContext';
import { isAdmin, isTech } from '../../app/access';
import { navigate } from '../../app/router';
import { incidentApi } from '../../api/incident';
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
  TextArea,
  useToast,
} from '../../components/ui';
import { EntityPicker } from '../../components/EntityPicker';
import { EvidenceForm, PhotoViewer } from '../../components/Evidence';
import { date, shortId } from '../../utils/format';
import { requireUuid, textValue } from '../../utils/validation';
import { finishRequest, pendingRequest, requestKey } from '../../utils/idempotency';
const states = ['Open', 'InProgress', 'Resolved', 'Closed'];
export function Incidents({ id }: { id?: string }) {
  const { user } = useAuth();
  const [search, setSearch] = useState('');
  const [status, setStatus] = useState('');
  const [priority, setPriority] = useState('');
  const [sort, setSort] = useState('newest');
  const [mine, setMine] = useState(false);
  const [page, setPage] = useState(1);
  const [creating, setCreating] = useState(false);
  const state = useData(
    `incidents:${search}:${status}:${priority}:${sort}:${page}:${mine}`,
    (signal) =>
      incidentApi.list(
        {
          search,
          status,
          priority,
          sort,
          page,
          pageSize: 20,
          technicianId: mine ? user!.id : undefined,
        },
        signal,
      ),
    !id,
  );
  if (id) return <IncidentDetail id={id} />;
  return (
    <>
      <PageHeader
        title="Sự cố thiết bị"
        description="Tiếp nhận báo hỏng, phối hợp sửa chữa và theo dõi nghiệm thu theo SLA."
        actions={
          !isTech(user!) && (
            <Button icon="plus" onClick={() => setCreating(true)}>
              Báo sự cố
            </Button>
          )
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
            label="Tìm sự cố"
            type="search"
            maxLength={200}
            placeholder="Mã, tiêu đề hoặc tên máy…"
          />
          <Select
            label="Trạng thái"
            value={status}
            onChange={(v) => {
              setStatus(v);
              setPage(1);
            }}
          >
            <option value="">Tất cả</option>
            {states.map((x) => (
              <option key={x} value={x}>
                {x}
              </option>
            ))}
          </Select>
          <Select
            label="Mức ưu tiên"
            value={priority}
            onChange={(v) => {
              setPriority(v);
              setPage(1);
            }}
          >
            <option value="">Tất cả</option>
            {['Critical', 'High', 'Medium', 'Low'].map((x) => (
              <option key={x}>{x}</option>
            ))}
          </Select>
          <Select
            label="Sắp xếp"
            value={sort}
            onChange={(v) => {
              setSort(v);
              setPage(1);
            }}
          >
            <option value="newest">Mới nhất</option>
            <option value="oldest">Cũ nhất</option>
            <option value="deadline">Hạn xử lý</option>
            <option value="code">Mã hồ sơ</option>
          </Select>
          <Button kind="secondary" type="submit" icon="search">
            Tìm kiếm
          </Button>
        </form>
        {isTech(user!) && (
          <label className="check-label" style={{ marginBottom: 18 }}>
            <input
              type="checkbox"
              checked={mine}
              onChange={(e) => {
                setMine(e.target.checked);
                setPage(1);
              }}
            />
            Chỉ công việc được giao cho tôi
          </label>
        )}
        <DataState state={state}>
          {(result) => (
            <>
              {result.items.length ? (
                <Table
                  headers={[
                    'Sự cố',
                    'Thiết bị',
                    'Ưu tiên',
                    'Trạng thái',
                    'Người được giao',
                    'Ngày tạo',
                  ]}
                >
                  {result.items.map((row) => (
                    <tr key={row.id}>
                      <td>
                        <a className="table-link" href={'#/incidents/' + row.id}>
                          {row.code}
                        </a>
                        <small>{row.title}</small>
                      </td>
                      <td>{row.equipmentName}</td>
                      <td>
                        <Badge value={row.priority} />
                      </td>
                      <td>
                        <Badge value={row.status} />
                      </td>
                      <td>
                        {row.assignedTechnicianId === user!.id
                          ? 'Bạn'
                          : shortId(row.assignedTechnicianId)}
                      </td>
                      <td>{date(row.createdAtUtc)}</td>
                    </tr>
                  ))}
                </Table>
              ) : (
                <Empty
                  title="Chưa có sự cố phù hợp"
                  description="Thay bộ lọc hoặc tạo báo hỏng khi phát hiện máy không hoạt động."
                />
              )}
              <Pagination
                page={page}
                hasNext={page * 20 < (result.total ?? 0)}
                total={result.total}
                onChange={setPage}
              />
            </>
          )}
        </DataState>
      </Panel>
      {creating && (
        <CreateIncident
          onClose={() => setCreating(false)}
          onSaved={(id) => {
            setCreating(false);
            navigate('incidents/' + id);
          }}
        />
      )}
    </>
  );
}
export function CreateIncident({
  onClose,
  onSaved,
  equipmentId = '',
  equipmentName = '',
}: {
  onClose: () => void;
  onSaved: (id: string) => void;
  equipmentId?: string;
  equipmentName?: string;
}) {
  const { user } = useAuth();
  const mutation = useMutation(),
    toast = useToast();
  const [draft] = useState(() => {
    try {
      return JSON.parse(pendingRequest(user!.id, 'incident.create')?.payload ?? 'null') as {
        equipmentId: string;
        title: string;
        description: string;
        priority: string;
        relatedIncidentId: string | null;
      } | null;
    } catch {
      return null;
    }
  });
  const [equipment, setEquipment] = useState(draft?.equipmentId ?? equipmentId);
  const related = useData(
    'related:' + equipment,
    (signal) =>
      incidentApi.list(
        { equipmentId: equipment, status: 'Closed', page: 1, pageSize: 100 },
        signal,
      ),
    Boolean(equipment),
  );
  function submit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    const form = new FormData(e.currentTarget);
    void mutation.run(async () => {
      const input = {
        equipmentId: requireUuid(String(form.get('equipmentId'))),
        title: textValue(form.get('title'), 'Tiêu đề', 200),
        description: textValue(form.get('description'), 'Mô tả', 4000),
        priority: String(form.get('priority')),
        relatedIncidentId: String(form.get('relatedIncidentId') ?? '') || null,
      };
      const key = requestKey(user!.id, 'incident.create', input);
      const result = await incidentApi.create(input, key);
      finishRequest(user!.id, 'incident.create');
      toast('Ticket đã được BE ghi nhận.');
      onSaved(result.id);
    });
  }
  return (
    <Dialog
      title="Báo sự cố thiết bị"
      description="Mô tả hiện tượng để đội kỹ thuật tiếp nhận và xử lý."
      onClose={onClose}
      busy={mutation.busy}
      drawer
    >
      <form className="stack" onSubmit={submit}>
        {draft && (
          <Notice>
            Đang phục hồi nội dung yêu cầu chưa rõ kết quả. Giữ nguyên dữ liệu để retry cùng mã,
            hoặc nhờ Admin kiểm tra đối soát.
          </Notice>
        )}
        <EntityPicker
          kind="equipment"
          name="equipmentId"
          label="Thiết bị"
          defaultValue={equipment}
          defaultLabel={equipmentName || equipment}
          onChange={setEquipment}
        />
        <Field
          name="title"
          label="Tiêu đề sự cố"
          required
          maxLength={200}
          defaultValue={draft?.title}
        />
        <TextArea
          name="description"
          label="Hiện tượng và mô tả"
          maxLength={4000}
          defaultValue={draft?.description}
        />
        <Select label="Mức ưu tiên" name="priority" defaultValue={draft?.priority ?? 'Medium'}>
          {['Critical', 'High', 'Medium', 'Low'].map((x) => (
            <option key={x}>{x}</option>
          ))}
        </Select>
        <Select
          label="Sự cố tái phát liên quan nếu có"
          name="relatedIncidentId"
          defaultValue={draft?.relatedIncidentId ?? ''}
        >
          <option value="">Không liên kết</option>
          {draft?.relatedIncidentId &&
            !related.data?.items.some((x) => x.id === draft.relatedIncidentId) && (
              <option value={draft.relatedIncidentId}>Hồ sơ đã liên kết</option>
            )}
          {related.data?.items.map((x) => (
            <option key={x.id} value={x.id}>
              {x.code} · {x.title}
            </option>
          ))}
        </Select>
        <Notice>
          Yêu cầu được lưu với mã chống trùng. Nếu timeout, không đổi payload hoặc tạo key mới để né
          lỗi.
        </Notice>
        <ErrorMessage error={mutation.error} />
        <FormActions label="Gửi báo hỏng" busy={mutation.busy} onCancel={onClose} />
      </form>
    </Dialog>
  );
}
function IncidentDetail({ id }: { id: string }) {
  const { user } = useAuth();
  const state = useData('incident:' + id, (signal) => incidentApi.detail(id, signal));
  const attempts = useData('incident-attempts:' + id, (signal) => incidentApi.attempts(id, signal));
  const mutation = useMutation(),
    toast = useToast();
  const [assign, setAssign] = useState(false);
  const [submit, setSubmit] = useState(false);
  const [review, setReview] = useState<{ attempt: string; accept: boolean }>();
  const [photo, setPhoto] = useState<string>();
  function reload() {
    state.reload();
    attempts.reload();
  }
  return (
    <>
      <Button kind="ghost" icon="back" onClick={() => navigate('incidents')}>
        Danh sách sự cố
      </Button>
      <DataState state={state}>
        {(detail) => {
          const row = detail.incident;
          const assigned = row.assignedTechnicianId === user!.id;
          return (
            <>
              <PageHeader
                eyebrow={row.code}
                title={row.title}
                description={row.equipmentName}
                actions={<Badge value={row.status} />}
              />
              <ErrorMessage error={mutation.error} />
              <div className="detail-layout">
                <div>
                  <Panel title="Nội dung tiếp nhận">
                    <p style={{ whiteSpace: 'pre-wrap' }}>{detail.description}</p>
                    <KeyValues
                      items={[
                        [
                          'Thiết bị',
                          <a href={'#/equipment/' + row.equipmentId}>{row.equipmentName}</a>,
                        ],
                        ['Mức ưu tiên', row.priority],
                        [
                          'Người được giao',
                          assigned ? 'Bạn' : (row.assignedTechnicianId ?? 'Chưa phân công'),
                        ],
                        ['Ngày tạo', date(row.createdAtUtc)],
                        ['Người báo', detail.reportedByUserId],
                        ['Ngày đóng', date(detail.closedAtUtc)],
                      ]}
                    />
                  </Panel>
                  <Panel
                    title="Biên bản và diễn biến"
                    description="Diễn biến được dựng từ mốc hồ sơ và các attempt mà API cung cấp, không thay Audit Log tổng hợp."
                  >
                    <DataState state={attempts}>
                      {(items) =>
                        items.length ? (
                          <ol className="timeline">
                            {items.map((attempt) => (
                              <li key={attempt.id}>
                                <div className="actions">
                                  <h3>Lần thực hiện {attempt.attemptNumber}</h3>
                                  <Badge value={attempt.reviewDecision} />
                                </div>
                                <small>{date(attempt.completedAtUtc)}</small>
                                <p>{attempt.resolutionNotes}</p>
                                <p>
                                  <strong>Kết quả: </strong>
                                  {attempt.resultSummary}
                                </p>
                                {attempt.reviewReason && (
                                  <p>Lý do trả lại: {attempt.reviewReason}</p>
                                )}
                                <div className="actions">
                                  {attempt.hasPhoto && (
                                    <Button
                                      kind="secondary"
                                      icon="eye"
                                      onClick={() => setPhoto(attempt.id)}
                                    >
                                      Xem ảnh
                                    </Button>
                                  )}
                                  {isAdmin(user!) &&
                                    row.status === 'Resolved' &&
                                    attempt.reviewDecision === 'Pending' && (
                                      <>
                                        <Button
                                          onClick={() => {
                                            mutation.clearError();
                                            setReview({ attempt: attempt.id, accept: true });
                                          }}
                                        >
                                          Nghiệm thu
                                        </Button>
                                        <Button
                                          kind="secondary"
                                          onClick={() => {
                                            mutation.clearError();
                                            setReview({ attempt: attempt.id, accept: false });
                                          }}
                                        >
                                          Trả lại
                                        </Button>
                                      </>
                                    )}
                                </div>
                              </li>
                            ))}
                          </ol>
                        ) : (
                          <Empty
                            title="Chưa có biên bản sửa chữa"
                            description="Kỹ thuật viên được giao bắt đầu công việc rồi nộp kết quả kèm ảnh máy vận hành."
                          />
                        )
                      }
                    </DataState>
                  </Panel>
                </div>
                <div>
                  <Panel title="Thời hạn và SLA">
                    <KeyValues
                      items={[
                        ['Hạn phản hồi', date(detail.responseDeadlineAtUtc)],
                        ['Hạn xử lý', date(detail.resolutionDeadlineAtUtc)],
                        ['Phản hồi', <Badge value={detail.responseOutcome} />],
                        ['Xử lý', <Badge value={detail.resolutionOutcome} />],
                      ]}
                    />
                    {row.status !== 'Closed' && (
                      <Notice>
                        SLA chưa chốt cuối. Resolution dùng thời điểm hoàn tất của attempt được
                        Admin chấp nhận.
                      </Notice>
                    )}
                  </Panel>
                  <Panel title="Thao tác hiện hành">
                    <div className="stack">
                      {isAdmin(user!) && ['Open', 'InProgress'].includes(row.status) && (
                        <Button
                          icon="users"
                          onClick={() => {
                            mutation.clearError();
                            setAssign(true);
                          }}
                        >
                          Phân công kỹ thuật viên
                        </Button>
                      )}
                      {isTech(user!) && assigned && row.status === 'Open' && (
                        <Button
                          disabled={mutation.busy}
                          onClick={() =>
                            void mutation.run(async () => {
                              await incidentApi.start(id, row.rowVersion);
                              reload();
                              toast('Đã bắt đầu xử lý sự cố.');
                            })
                          }
                        >
                          Nhận và bắt đầu sửa
                        </Button>
                      )}
                      {row.status === 'InProgress' &&
                        (isAdmin(user!) || (isTech(user!) && assigned)) && (
                          <Button icon="file" onClick={() => setSubmit(true)}>
                            Nộp biên bản và ảnh
                          </Button>
                        )}
                      {row.status === 'Closed' && (
                        <Notice>
                          Hồ sơ đã đóng. Nếu lỗi tái phát, tạo ticket mới và liên kết sự cố này.
                        </Notice>
                      )}
                      {!isAdmin(user!) && !assigned && row.status !== 'Closed' && (
                        <Notice>
                          Chỉ kỹ thuật viên được phân công mới nhận và thực hiện công việc.
                        </Notice>
                      )}
                      <Button kind="secondary" icon="refresh" onClick={reload}>
                        Tải lại hồ sơ
                      </Button>
                    </div>
                  </Panel>
                </div>
              </div>
              {assign && (
                <Dialog
                  title="Phân công kỹ thuật viên"
                  onClose={() => setAssign(false)}
                  busy={mutation.busy}
                >
                  <form
                    className="stack"
                    onSubmit={(e) => {
                      e.preventDefault();
                      const technician = String(new FormData(e.currentTarget).get('technicianId'));
                      void mutation.run(async () => {
                        await incidentApi.assign(id, requireUuid(technician), row.rowVersion);
                        setAssign(false);
                        reload();
                        toast('Đã phân công.');
                      });
                    }}
                  >
                    <EntityPicker kind="technician" name="technicianId" label="Kỹ thuật viên" />
                    <ErrorMessage error={mutation.error} />
                    <FormActions
                      busy={mutation.busy}
                      label="Phân công"
                      onCancel={() => setAssign(false)}
                    />
                  </form>
                </Dialog>
              )}
              {submit && (
                <EvidenceForm
                  kind="incident"
                  rowVersion={row.rowVersion}
                  onClose={() => setSubmit(false)}
                  onSubmit={async (form) => {
                    await incidentApi.submit(id, form);
                    reload();
                    toast('Đã nộp biên bản. Hồ sơ chờ Admin nghiệm thu.');
                  }}
                />
              )}
              {review && (
                <Confirm
                  title={review.accept ? 'Nghiệm thu kết quả sửa' : 'Trả lại để tiếp tục sửa'}
                  description={
                    review.accept
                      ? 'Xác nhận máy đã vận hành đạt yêu cầu sau khi xem biên bản và ảnh.'
                      : 'Giữ attempt và ảnh cũ, ghi lý do rồi chuyển hồ sơ về đang xử lý.'
                  }
                  reason={!review.accept}
                  busy={mutation.busy}
                  error={mutation.error}
                  onClose={() => setReview(undefined)}
                  onConfirm={(reason) =>
                    void mutation.run(async () => {
                      await incidentApi.review(
                        id,
                        review.attempt,
                        review.accept,
                        row.rowVersion,
                        reason,
                      );
                      setReview(undefined);
                      reload();
                      toast('Đã ghi nhận kết quả nghiệm thu.');
                    })
                  }
                />
              )}
              {photo && (
                <PhotoViewer
                  id={photo}
                  load={() => incidentApi.photo(id, photo)}
                  onClose={() => setPhoto(undefined)}
                />
              )}
            </>
          );
        }}
      </DataState>
    </>
  );
}
