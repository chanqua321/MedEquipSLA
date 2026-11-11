import { AssetImage } from '../../components/AssetImage';
import { useEffect, useState, type FormEvent } from 'react';
import { useAuth } from '../../app/AuthContext';
import { identityApi } from '../../api/identity';
import { useMutation } from '../../hooks/useMutation';
import {
  Button,
  ErrorMessage,
  Field,
  FormActions,
  Notice,
  Select,
  useToast,
} from '../../components/ui';
import { Icon } from '../../components/Icon';
import { navigate } from '../../app/router';
import { password } from '../../utils/validation';
import art from '../../assets/illustrations/operations.svg';
type Mode = 'login' | 'forgot' | 'activate' | 'reset' | 'mfa';
function ticket() {
  const params = new URLSearchParams(window.location.search);
  const value = {
    userId: params.get('userId') ?? '',
    ticketId: params.get('ticketId') ?? '',
    token: params.get('token') ?? '',
  };
  return value;
}
export function AuthPage() {
  const auth = useAuth(),
    toast = useToast(),
    mutation = useMutation();
  const [mode, setMode] = useState<Mode>(() =>
    window.location.pathname.endsWith('/activate')
      ? 'activate'
      : window.location.pathname.endsWith('/reset-password')
        ? 'reset'
        : 'login',
  );
  const [link] = useState(ticket);
  useEffect(() => {
    if (link.token)
      window.history.replaceState(null, '', window.location.pathname + window.location.hash);
  }, [link.token]);
  const [recovery, setRecovery] = useState(false);
  const [message, setMessage] = useState('');
  function switchMode(value: Mode) {
    setMode(value);
    setMessage('');
    mutation.clearError();
  }
  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = new FormData(event.currentTarget);
    await mutation.run(async () => {
      if (mode === 'login') {
        await auth.signIn({
          userName: String(form.get('userName')).trim(),
          password: String(form.get('password')),
          code: recovery ? undefined : String(form.get('code') ?? '') || undefined,
          recoveryCode: recovery ? String(form.get('recoveryCode')) : undefined,
        });
        navigate('dashboard');
      } else if (mode === 'forgot') {
        const result = await identityApi.reset(String(form.get('email')).trim());
        setMessage(result.message);
      } else if (mode === 'activate' || mode === 'reset') {
        const newPassword = password(
          String(form.get('password')),
          String(form.get('confirmation')),
        );
        await identityApi.redeem(mode === 'activate' ? 'activate' : 'password-reset/confirm', {
          userId: form.get('userId'),
          ticketId: form.get('ticketId'),
          token: form.get('token'),
          password: newPassword,
        });
        window.history.replaceState(null, '', '/' + window.location.hash);
        switchMode('login');
        toast(
          mode === 'activate'
            ? 'Tài khoản đã được kích hoạt. Bạn có thể đăng nhập.'
            : 'Mật khẩu đã được đặt lại. Các phiên cũ đã bị thu hồi.',
        );
      }
    });
  }
  const titles = {
    login: 'Chào mừng trở lại',
    forgot: 'Khôi phục mật khẩu',
    activate: 'Kích hoạt tài khoản',
    reset: 'Đặt lại mật khẩu',
    mfa: 'Bảo vệ tài khoản',
  };
  return (
    <div className="auth-layout">
      <main className="auth-form-side">
        <a className="brand" href="#/dashboard">
          <span className="brand-mark">
            <Icon name="activity" size={25} />
          </span>
          <span>
            MedEquip<span className="brand-light">SLA</span>
          </span>
        </a>
        <div className="auth-content">
          <div className="eyebrow">NỀN TẢNG ĐIỀU HÀNH THIẾT BỊ Y TẾ</div>
          <h1>{titles[mode]}</h1>
          <p className="muted">
            {mode === 'login'
              ? 'Đăng nhập để theo dõi thiết bị và công việc của bạn.'
              : mode === 'mfa'
                ? 'Thiết lập hoặc xác minh lớp bảo vệ thứ hai.'
                : 'Sử dụng thông tin từ email hướng dẫn của hệ thống.'}
          </p>
          {mode === 'mfa' ? (
            <MfaForm />
          ) : (
            <form onSubmit={submit} className="stack" key={mode}>
              {mode === 'login' && (
                <>
                  <Field
                    name="userName"
                    label="Tên đăng nhập"
                    autoComplete="username"
                    required
                    maxLength={256}
                  />
                  <Field
                    name="password"
                    label="Mật khẩu"
                    type="password"
                    autoComplete="current-password"
                    required
                    maxLength={1024}
                  />
                  <div className="auth-inline">
                    <label className="check-label">
                      <input
                        type="checkbox"
                        checked={recovery}
                        onChange={(e) => setRecovery(e.target.checked)}
                      />
                      Dùng mã khôi phục
                    </label>
                    <button
                      type="button"
                      className="text-button"
                      onClick={() => switchMode('forgot')}
                    >
                      Quên mật khẩu?
                    </button>
                  </div>
                  {recovery ? (
                    <Field
                      name="recoveryCode"
                      label="Mã khôi phục"
                      autoComplete="one-time-code"
                      required
                      maxLength={30}
                    />
                  ) : (
                    <Field
                      name="code"
                      label="Mã xác thực MFA"
                      inputMode="numeric"
                      pattern="[0-9]{6}"
                      maxLength={6}
                      autoComplete="one-time-code"
                      hint="Bắt buộc với Admin và tài khoản đã bật MFA. Bỏ trống nếu chưa bật."
                    />
                  )}
                </>
              )}
              {mode === 'forgot' && (
                <Field
                  label="Email tài khoản"
                  name="email"
                  type="email"
                  autoComplete="email"
                  required
                  maxLength={256}
                />
              )}
              {(mode === 'activate' || mode === 'reset') && (
                <>
                  <Field label="Mã tài khoản" name="userId" defaultValue={link.userId} required />
                  <Field
                    label="Mã lời mời hoặc yêu cầu"
                    name="ticketId"
                    defaultValue={link.ticketId}
                    required
                  />
                  <Field
                    label="Mã xác nhận từ email"
                    name="token"
                    type="password"
                    defaultValue={link.token}
                    minLength={64}
                    maxLength={64}
                    required
                    autoComplete="off"
                  />
                  <Field
                    label="Mật khẩu mới"
                    name="password"
                    type="password"
                    required
                    minLength={12}
                    maxLength={1024}
                    autoComplete="new-password"
                    hint="Ít nhất 12 ký tự, có chữ hoa, chữ thường, số và ký tự đặc biệt; không dùng mật khẩu phổ biến."
                  />
                  <Field
                    label="Nhập lại mật khẩu"
                    name="confirmation"
                    type="password"
                    required
                    minLength={12}
                    autoComplete="new-password"
                  />
                </>
              )}
              <ErrorMessage error={mutation.error} />
              {message && (
                <p role="status" className="success-message">
                  {message}
                </p>
              )}
              <FormActions
                busy={mutation.busy}
                label={
                  mode === 'login' ? 'Đăng nhập' : mode === 'forgot' ? 'Gửi hướng dẫn' : 'Xác nhận'
                }
              />
            </form>
          )}
          <div className="auth-links">
            {mode !== 'login' && (
              <button className="text-button" onClick={() => switchMode('login')}>
                Trở lại đăng nhập
              </button>
            )}
            {mode === 'login' && (
              <>
                <button className="text-button" onClick={() => switchMode('mfa')}>
                  Thiết lập MFA lần đầu
                </button>
                <button className="text-button" onClick={() => switchMode('activate')}>
                  Kích hoạt từ lời mời
                </button>
              </>
            )}
          </div>
          <p className="fine-print">
            <Icon name="lock" size={14} /> Phiên được giữ trong bộ nhớ của tab. Tải lại trang cần
            đăng nhập lại.
          </p>
        </div>
        <p className="auth-footer">MedEquipSLA · Kết nối vận hành, nâng chuẩn chất lượng.</p>
      </main>
      <aside className="auth-visual">
        <div className="auth-visual-copy">
          <span className="visual-label">
            <i /> MEDICAL OPERATIONS PLATFORM
          </span>
          <h2>
            Chăm sóc thiết bị.
            <br />
            An tâm vận hành.
          </h2>
          <p>Một nơi để quản lý tài sản, phối hợp sửa chữa và theo dõi chất lượng bảo trì.</p>
        </div>
        <AssetImage
          src={art}
          alt="Minh họa kỹ thuật viên kiểm tra thiết bị theo dõi y tế"
          fetchPriority="high"
        />
        <div className="visual-features">
          <span>
            <Icon name="shield" />
            Quyền theo vai trò
          </span>
          <span>
            <Icon name="clipboard" />
            Lịch sử có minh chứng
          </span>
        </div>
      </aside>
    </div>
  );
}
export function MfaForm({ allowDisable = false }: { allowDisable?: boolean }) {
  const mutation = useMutation(),
    toast = useToast();
  const [setup, setSetup] = useState<Awaited<ReturnType<typeof identityApi.setup>>>();
  const [codes, setCodes] = useState<string[]>();
  const [action, setAction] = useState('setup');
  if (codes)
    return (
      <section className="stack">
        <h2>Lưu mã khôi phục</h2>
        <Notice>Chỉ hiển thị một lần. Không chia sẻ mã hoặc chụp màn hình khi trình chiếu.</Notice>
        <ul className="recovery-codes">
          {codes.map((code) => (
            <li key={code}>
              <code>{code}</code>
            </li>
          ))}
        </ul>
        <Button
          onClick={() => {
            navigator.clipboard
              .writeText(codes.join('\n'))
              .then(() => toast('Đã sao chép mã khôi phục.'))
              .catch(() => toast('Không có quyền clipboard. Hãy lưu mã thủ công.'));
          }}
        >
          Sao chép mã
        </Button>
        <p className="muted">
          Quay lại đăng nhập và dùng mã TOTP mới. Tải lại trang sẽ xóa các mã đang hiển thị.
        </p>
      </section>
    );
  return (
    <form
      className="stack"
      onSubmit={(e) => {
        e.preventDefault();
        const form = new FormData(e.currentTarget);
        void mutation.run(async () => {
          if (setup) {
            const result = await identityApi.confirm({
              userId: setup.userId,
              setupToken: setup.setupToken,
              code: form.get('code'),
            });
            setCodes(result.recoveryCodes);
            setSetup(undefined);
          } else {
            const input = {
              userName: String(form.get('userName')).trim(),
              password: form.get('password'),
              method: form.get('method'),
              code: String(form.get('code') ?? '') || undefined,
              recoveryCode: String(form.get('recoveryCode') ?? '') || undefined,
            };
            if (action === 'setup') setSetup(await identityApi.setup(input));
            else {
              await identityApi.mfaAction(action === 'email' ? 'email-code' : 'disable', input);
              toast(
                action === 'email'
                  ? 'Nếu thông tin hợp lệ, hệ thống sẽ gửi mã đến email.'
                  : 'Đã tắt MFA của tài khoản nhân viên.',
              );
            }
          }
        });
      }}
    >
      {setup ? (
        <>
          <Notice>
            {setup.method === 'Totp'
              ? 'Nhập secret vào ứng dụng Authenticator, sau đó điền mã 6 số.'
              : 'Kiểm tra email để lấy mã xác nhận.'}
          </Notice>
          {setup.secret && (
            <div className="secret-output">
              <strong>Secret TOTP</strong>
              <code>{setup.secret}</code>
            </div>
          )}
          {setup.authenticatorUri && (
            <a className="text-button" href={setup.authenticatorUri}>
              Mở ứng dụng Authenticator
            </a>
          )}
          <Field
            label="Mã xác nhận"
            name="code"
            required
            pattern="[0-9]{6}"
            maxLength={6}
            inputMode="numeric"
            autoComplete="one-time-code"
          />
        </>
      ) : (
        <>
          <Select label="Thao tác" value={action} onChange={setAction}>
            <option value="setup">Thiết lập MFA</option>
            <option value="email">Gửi mã đăng nhập qua email</option>
            {allowDisable && <option value="disable">Tắt MFA nhân viên</option>}
          </Select>
          <Field
            name="userName"
            label="Tên đăng nhập"
            required
            autoComplete="username"
            maxLength={256}
          />
          <Field
            name="password"
            label="Mật khẩu hiện tại"
            type="password"
            required
            autoComplete="current-password"
            maxLength={1024}
          />
          {action === 'setup' && (
            <Select
              label="Phương thức"
              name="method"
              defaultValue="Totp"
              hint="Admin bắt buộc TOTP. Email chỉ dành cho nhân viên."
            >
              <option value="Totp">Ứng dụng Authenticator</option>
              <option value="Email">Mã email</option>
            </Select>
          )}
          {action !== 'email' && (
            <>
              <Field
                name="code"
                label="Mã MFA hiện tại nếu đã bật"
                maxLength={6}
                inputMode="numeric"
                pattern="[0-9]{6}"
                autoComplete="one-time-code"
              />
              <Field
                name="recoveryCode"
                label="Hoặc mã khôi phục hiện tại"
                maxLength={30}
                autoComplete="off"
              />
            </>
          )}
        </>
      )}
      <ErrorMessage error={mutation.error} />
      <FormActions
        busy={mutation.busy}
        label={setup ? 'Xác nhận và tạo mã khôi phục' : 'Tiếp tục'}
      />
    </form>
  );
}
