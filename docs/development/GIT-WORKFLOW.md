# Git Branch / Pull Request

1. Team owner xác nhận GitHub account/organization và tạo repository Private; quyền truy cập từng thành viên do owner cấp.
2. Branch chính main; mỗi nhiệm vụ dùng feature/<issue>-<scope>, fix/<issue>-<scope> hoặc docs/<scope>.
3. Pull main trước khi tạo branch, commit nhỏ với mô tả thay đổi. Không commit .env, PEM/private keys, password, bin/obj/node_modules.
4. Restore/build backend, npm ci/build frontend; thêm tests theo thay đổi; cập nhật Compliance Matrix và Evidence tương ứng.
5. Push branch, mở PR về main. PR nêu vấn đề, hành vi mới, tiêu chí PRN232 liên quan, kết quả kiểm tra thực tế và phần chưa làm.
6. Reviewer kiểm tra dependency direction, database ownership, auth/data scope, migrations và secrets. Merge sau review và CI xanh, không nhận PASS theo lời mô tả khi thiếu evidence.

Khi có GitHub CLI đã đăng nhập và owner phê duyệt đích, có thể chạy `gh repo create MedEquipSLA --private --source . --remote origin --push`. Không dùng --public. Không chạy lệnh này nếu chưa có tài khoản/quyền đã xác nhận. Không điền tên tài khoản/URL giả trong README.
