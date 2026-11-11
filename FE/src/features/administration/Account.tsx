import { useState } from 'react';
import { useAuth } from '../../app/AuthContext';
import { isAdmin, roleLabel } from '../../app/access';
import { identityApi } from '../../api/identity';
import { useMutation } from '../../hooks/useMutation';
import {
  Button,
  ErrorMessage,
  Field,
  FormActions,
  KeyValues,
  Notice,
  PageHeader,
  Panel,
  useToast,
} from '../../components/ui';
import { MfaForm } from '../auth/AuthPage';
export function Account() {
  const { user, signOut } = useAuth();
  const mutation = useMutation(),
    toast = useToast();
  const [security, setSecurity] = useState(false);
  return (
    <>
      <PageHeader
        title="Tài khoản của tôi"
        description="Thông tin quyền truy cập, bảo mật và quản lý phiên hiện hành."
      />
      <div className="grid-2">
        <Panel title="Thông tin phiên">
          <KeyValues
            items={[
              ['Mã tài khoản', user!.id],
              ['Vai trò', roleLabel(user!)],
              ['Khoa phòng', user!.departmentId ?? 'Không gắn khoa'],
              ['Phiên bản quyền', user!.authorizationVersion],
              ['Mã phiên', user!.sessionId],
            ]}
          />
          <Notice>
            BE chưa có API sửa hồ sơ cá nhân. Liên hệ Admin để cập nhật họ tên, vai trò hoặc khoa.
          </Notice>
          <div className="actions">
            <Button
              kind="secondary"
              disabled={mutation.busy}
              onClick={() => void mutation.run(() => signOut())}
            >
              Đăng xuất phiên này
            </Button>
            <Button
              kind="secondary"
              disabled={mutation.busy}
              onClick={() => void mutation.run(() => signOut(true))}
            >
              Đăng xuất tất cả
            </Button>
          </div>
        </Panel>
        <Panel title="Bảo mật tài khoản">
          {isAdmin(user!) ? (
            <Notice>
              Admin bắt buộc TOTP. Khôi phục Admin và tắt MFA không nằm trong quy trình đã duyệt.
            </Notice>
          ) : (
            <form
              className="stack"
              onSubmit={(e) => {
                e.preventDefault();
                const email = String(new FormData(e.currentTarget).get('email'));
                void mutation.run(async () => {
                  const r = await identityApi.reset(email);
                  toast(r.message);
                });
              }}
            >
              <p className="muted">
                Thay mật khẩu qua liên kết xác nhận email. Hệ thống chưa có API đổi mật khẩu trực
                tiếp.
              </p>
              <Field label="Email đã xác nhận" name="email" type="email" required maxLength={256} />
              <FormActions busy={mutation.busy} label="Gửi hướng dẫn đặt lại" />
            </form>
          )}
          <ErrorMessage error={mutation.error} />
          <Button kind="secondary" onClick={() => setSecurity((v) => !v)}>
            {security ? 'Đóng thiết lập MFA' : 'Thiết lập và quản lý MFA'}
          </Button>
          {security && (
            <div style={{ marginTop: 20 }}>
              <MfaForm allowDisable={!isAdmin(user!)} />
            </div>
          )}
        </Panel>
      </div>
    </>
  );
}
