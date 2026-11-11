import type { User } from '../api/http';
export const isAdmin = (user: User) => user.roles.includes('Admin');
export const isTech = (user: User) => user.roles.includes('Technician');
export const canWork = (user: User) => isAdmin(user) || isTech(user);
export const roleLabel = (user: User) =>
  isAdmin(user) ? 'Quản trị viên' : isTech(user) ? 'Kỹ thuật viên' : 'Nhân viên khoa';
export const NAV = [
  { path: 'dashboard', label: 'Tổng quan', icon: 'grid', group: 'Điều hành', roles: [] },
  { path: 'equipment', label: 'Thiết bị', icon: 'monitor', group: 'Điều hành', roles: [] },
  { path: 'incidents', label: 'Sự cố', icon: 'activity', group: 'Điều hành', roles: [] },
  {
    path: 'schedules',
    label: 'Lịch bảo trì',
    icon: 'calendar',
    group: 'Công việc',
    roles: ['Admin'],
  },
  {
    path: 'workorders',
    label: 'Lệnh công việc',
    icon: 'clipboard',
    group: 'Công việc',
    roles: ['Admin', 'Technician'],
  },
  {
    path: 'calibration',
    label: 'Hiệu chuẩn',
    icon: 'sliders',
    group: 'Công việc',
    roles: ['Admin', 'Technician'],
  },
  {
    path: 'departments',
    label: 'Khoa phòng',
    icon: 'building',
    group: 'Quản trị',
    roles: ['Admin'],
  },
  {
    path: 'categories',
    label: 'Loại thiết bị',
    icon: 'layers',
    group: 'Quản trị',
    roles: ['Admin'],
  },
  { path: 'users', label: 'Nhân sự', icon: 'users', group: 'Quản trị', roles: ['Admin'] },
  { path: 'sla', label: 'Chính sách SLA', icon: 'clock', group: 'Quản trị', roles: ['Admin'] },
  { path: 'reports', label: 'Báo cáo', icon: 'chart', group: 'Quản trị', roles: ['Admin'] },
  {
    path: 'retirement',
    label: 'Thanh lý thiết bị',
    icon: 'archive',
    group: 'Quản trị',
    roles: ['Admin'],
  },
  {
    path: 'operations',
    label: 'Yêu cầu chờ đối soát',
    icon: 'shield',
    group: 'Quản trị',
    roles: ['Admin'],
  },
  {
    path: 'audit',
    label: 'Lịch sử kiểm soát',
    icon: 'history',
    group: 'Quản trị',
    roles: ['Admin'],
  },
  { path: 'notifications', label: 'Thông báo', icon: 'bell', group: 'Tài khoản', roles: [] },
  { path: 'account', label: 'Tài khoản', icon: 'user', group: 'Tài khoản', roles: [] },
] as const;
export function routeAllowed(path: string, user: User) {
  const item = NAV.find((x) => x.path === path);
  return Boolean(
    item &&
    (item.roles.length === 0 ||
      (item.roles as readonly string[]).some((role) => user.roles.includes(role))),
  );
}
export function canExecute(user: User, assigned: string | null, status: string) {
  return (
    isTech(user) && user.id === assigned && ['Scheduled', 'Open', 'InProgress'].includes(status)
  );
}
