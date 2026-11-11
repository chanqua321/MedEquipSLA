# Chạy thử Identity và RBAC

Bản chạy thử nối Web qua Gateway đến Identity và Equipment. Đăng nhập dùng tên tài khoản bootstrap. Token nằm trong bộ nhớ của tab, vì vậy tải lại trang sẽ yêu cầu đăng nhập lại. Nên chạy trên máy local vì MFA và vòng đời tài khoản vẫn đang triển khai.

## Chuẩn bị khóa JWT trên Windows

Mở PowerShell 7 tại thư mục MedEquipSLA. Tạo khóa riêng cho môi trường thử nếu chưa có. Thư mục .secrets nằm trong .gitignore.

```powershell
New-Item -ItemType Directory -Force .secrets | Out-Null
$private = Join-Path (Get-Location) '.secrets/identity-private.pem'
$public = Join-Path (Get-Location) '.secrets/identity-public.pem'
if (!(Test-Path $private) -and !(Test-Path $public)) {
    $rsa = [System.Security.Cryptography.RSA]::Create(2048)
    try {
        [IO.File]::WriteAllText($private, $rsa.ExportPkcs8PrivateKeyPem())
        [IO.File]::WriteAllText($public, $rsa.ExportSubjectPublicKeyInfoPem())
    } finally { $rsa.Dispose() }
}
if (!(Test-Path $private) -or !(Test-Path $public)) { throw 'Cần đủ cặp khóa JWT' }
$env:Jwt__PrivateKeyPath = $private
$env:Jwt__PublicKeyPath = $public
```

Nếu LocalDB chưa có Admin, chạy bootstrap một lần. Nhập email thử, tên đăng nhập và mật khẩu mạnh tối thiểu 12 ký tự tại máy. Bootstrap báo lỗi nếu Admin hoặc tên đăng nhập đã tồn tại.

```powershell
$env:BootstrapAdmin__UserName = Read-Host 'Tên đăng nhập'
$env:BootstrapAdmin__Email = Read-Host 'Email'
$env:BootstrapAdmin__Password = Read-Host 'Mật khẩu' -MaskInput
try {
    dotnet run --project src/Services/Identity/Identity.Api --launch-profile Identity -- --BootstrapAdmin:Run=true
} finally {
    Remove-Item Env:BootstrapAdmin__Password, Env:BootstrapAdmin__Email, Env:BootstrapAdmin__UserName
}
```

## Chạy và kiểm tra

1. Trong terminal vừa cấu hình khóa, chạy `dotnet run --project src/Services/Identity/Identity.Api --launch-profile Identity`.
2. Trong terminal mới tại thư mục MedEquipSLA, đặt `$env:Jwt__PublicKeyPath = (Resolve-Path .secrets/identity-public.pem).Path`, rồi chạy `dotnet run --project src/Services/Equipment/Equipment.Api --launch-profile Equipment`.
3. Chạy `dotnet run --project src/Gateway/MedEquipSLA.Gateway` ở terminal thứ ba.
4. Trong `src/Web`, chạy `npm ci` nếu chưa cài dependencies, sau đó chạy `npm run dev`. Mở địa chỉ do Vite in ra.
5. Đăng nhập Admin, tạo khoa, lọc và đổi tên. Chọn Ngừng sử dụng, rồi lọc trạng thái Đã ngừng để kiểm tra hồ sơ còn trong danh sách. Mục tra cứu thiết bị dùng ID của một bản ghi Equipment đã có.
6. Đăng nhập ở hai tab. Đăng xuất một tab rồi thao tác ở tab còn lại. Đăng xuất tất cả để kiểm tra tab còn lại phải xác thực lại ở yêu cầu kế tiếp.

Trong mục Nhân viên, Admin có thể tìm, phân trang, vô hiệu hóa và kích hoạt lại nhân viên hiện có. Thử vô hiệu hóa một Technician đang đăng nhập rồi dùng lại phiên của Technician. Kích hoạt lại chỉ áp dụng sau khi nhân viên đã xác minh email và có mật khẩu.

TOTP, lời mời và email kích hoạt, tạo nhân viên, import Excel, thu hồi quyền khi đổi khoa hoặc vai trò, và gỡ phân công liên service là các phần còn mở so với bản chốt nghiệp vụ.
