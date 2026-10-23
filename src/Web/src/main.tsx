import React from 'react';
import { createRoot } from 'react-dom/client';
import './style.css';
const services = ['Identity', 'Equipment', 'Maintenance', 'Incident', 'Notification'];
function App() {
  return <main><p className="eyebrow">PRN232 · FOUNDATION</p><h1>MedEquipSLA</h1><p>Quản lý thiết bị, bảo trì, hiệu chuẩn và SLA sự cố.</p><aside><strong>Scaffold — chưa có chức năng nghiệp vụ.</strong><p>Chưa có đăng nhập, CRUD, quét SLA, tiêu thụ event hoặc dữ liệu demo. Không dùng màn hình này làm bằng chứng hoàn thành Final Assignment.</p></aside><section>{services.map(s => <article key={s}><h2>{s}</h2><p>API → CQRS handlers → Repository → EF Core</p><code>MedEquip_{s}</code></article>)}</section><footer>SQL Server Code First · Database per service · CQRS · Clean Architecture</footer></main>;
}
createRoot(document.getElementById('root')!).render(<React.StrictMode><App/></React.StrictMode>);
