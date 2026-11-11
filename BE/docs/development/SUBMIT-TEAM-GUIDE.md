# Bàn giao Submit cho team

Phân công đã được chủ dự án xác nhận: Khánh — Equipment/gRPC; Nghiêm Anh — Maintenance/Calibration; Phúc — Identity và Incident/SLA; Thắng — Notification, Gateway, nền tảng và tích hợp. Đây là phạm vi bàn giao/review, không xác nhận ai đã tạo code hoặc hoàn thành nhiệm vụ.

## Cấu trúc gói

- `CodeDayDu/MedEquipSLA`: một bản source đầy đủ, giữ nguyên solution, project references, C0/C1 và năm service. Build và chạy từ đây sau cấu hình môi trường.
- `CodeTheoThanhVien/01_Khanh`, `02_NghiemAnh`, `03_Phuc`, `04_Thang`: các file thuộc phạm vi từng người, giữ đường dẫn tương đối với root repository. Đây là fragment để review/đưa vào checkout chung, không phải các solution độc lập.
- `TaiLieu`: Word API/demo, Word phân công từ đầu, ERD draw.io và các hướng dẫn nghiệp vụ/database/runbook hiện hành.
- `MANIFEST.json`: hash SHA256 các file source và owner bàn giao; dùng kiểm tra gói, không thay bằng chứng đóng góp.

Không có `.git`, `.env` thật, khóa RSA, password môi trường, `.tools`, dependencies đã cài, bin/obj, logs hoặc TestResults. Cần restore .NET/NPM và tạo cấu hình riêng. Frontend đi kèm theo source hiện tại nhưng chưa được nghiệm thu trong lượt BE; không lấy trạng thái BE để suy ra FE hoàn tất.

## Quy trình branch và push

1. Mỗi người dùng checkout repository chung duy nhất trên máy mình. Nếu chưa có, clone repository một lần theo quyền GitHub được cấp. ZIP không chứa lịch sử Git hoặc quyền truy cập GitHub.
2. Kiểm tra `git status`; commit/stash công việc riêng trước khi cập nhật main. Không chép đè file người khác đang sửa và không reset/clean/force push.
3. Tạo branch tương ứng từ main đã đồng bộ: `feature/khanh-equipment`, `feature/nghiem-anh-maintenance`, `feature/phuc-identity-incident`, `feature/thang-platform-notification`.
4. So sánh fragment của mình với checkout, chép/chỉnh đúng các đường dẫn cần thiết. Không chép cả folder Submit hay CodeTheoThanhVien vào repository. File shared contracts/build/CI/docs do Thắng điều phối; Khánh và các owner phải review contract liên quan trước khi merge.
5. Restore/build/test theo README. Dùng database test riêng; không tự apply migration vào application DB cũ. Commit bằng tài khoản Git của chính người thực hiện và mô tả đúng thay đổi/review, không giả tên tác giả cho code được bàn giao.
6. `git add` chính xác file đã review, `git diff --cached`, commit, `git push -u origin <branch>`; mở PR vào main và gắn minh chứng thực. Thắng phối hợp thứ tự merge shared contract trước, rồi services và integration. Không coi gói bàn giao là đã push lên remote.

Ví dụ sau khi đã review/chạy test ở checkout của Khánh:

```powershell
git switch -c feature/khanh-equipment
git add src/Services/Equipment src/Contracts/MedEquipSLA.Contracts.Grpc/Protos/equipment.proto
git diff --cached
git commit -m "Review and integrate Equipment backend handoff"
git push -u origin feature/khanh-equipment
```

Đây là hướng dẫn cho thành viên, không phải lệnh đã được chạy trong lượt đóng gói. Không bịa commit/PR hoặc contribution history. Source bàn giao có nhiều thay đổi chưa commit từ các lượt trước; phải review diff với remote trước khi đưa vào main.

## Tạo lại gói

Từ root checkout, chạy `python scripts/Package-Team-Submit.py`. Script chụp file Git tracked/unignored hiện hữu, loại các đường dẫn bí mật/generated, không clone/worktree và không thực hiện Git mutation/network. Nếu folder Submit hoặc ZIP đã tồn tại, script dừng để bảo toàn bản cũ; tự chọn tên mới qua `--folder` và `--zip` khi cần, không tự xóa bản trước.
