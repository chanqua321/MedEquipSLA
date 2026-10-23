# Git Branch, Commit và Pull Request

Repo Private: [chanqua321/MedEquipSLA](https://github.com/chanqua321/MedEquipSLA). Owner cấp quyền cho thành viên, không dùng token trong remote URL. Credential manager/SSH giữ credential ở ngoài repo.

## Clone và tạo feature branch

```sh
git clone https://github.com/chanqua321/MedEquipSLA.git
cd MedEquipSLA
git remote -v
git status
git switch main
git pull --ff-only origin main
git switch -c feature/equipment-search
```

`feature/equipment-search` là ví dụ tên branch mới. Các nhóm branch: `feature/identity-*`, `feature/equipment-*`, `feature/incident-*`, `feature/maintenance-*`, `feature/notification-*`, `fix/*`, `docs/*`. Không tạo lại branch đã tồn tại hoặc switch bỏ qua thay đổi chưa commit của người khác.

## Cập nhật từ main và xử lý conflict

Khi working tree sạch hoặc công việc đã commit an toàn:

```sh
git fetch origin
git merge origin/main
```

Đọc từng conflict, trao đổi owner, giữ cả yêu cầu hợp lệ thay vì chọn ours/theirs toàn file. Xóa markers, build/test rồi `git add <file-đã-giải-quyết>` và `git commit`. `<...>` là placeholder, thay bằng đường dẫn thật. Nếu chưa giải quyết đúng, `git merge --abort` quay lại trước merge, không xóa công việc đã commit.

Rebase chỉ dùng trên feature branch riêng chưa chia sẻ: `git rebase origin/main`, giải quyết rồi `git rebase --continue`, hoặc `git rebase --abort`. Với branch đã có người khác dùng, ưu tiên merge; không rewrite lịch sử của người khác. **Không force push main**; không dùng reset --hard/clean để xử lý conflict khi có công việc chưa bảo toàn.

## Commit và push feature

Conventional Commits: `feat(equipment): add scoped search`, `fix(incident): preserve SLA snapshot`, `docs: explain gRPC probes`, `test(maintenance): cover occurrence uniqueness`, `chore: update development tooling`. Mô tả hành vi, không bịa chức năng đã xong.

```sh
git diff
git diff --check
git add docs/API-DOCUMENTATION.md
git diff --cached
git commit -m "docs: clarify API implementation status"
git push -u origin HEAD
```

Stage đúng file của nhiệm vụ; không mặc định `git add .` nếu workspace có thay đổi của người khác. Kiểm tra cả files tracked vì .gitignore không tự loại file đã commit.

## Pull Request, review và merge

Mở Compare & pull request trên GitHub, base `main`, compare feature branch. PR gồm vấn đề/hành vi mới, owner/BR/tiêu chí PRN232, migration/contract impact, build/test evidence và phần chưa làm. Reviewer xem dependencies, authorization/scope, DB ownership, secrets, concurrency/events và docs. CI hiện build backend/frontend; chưa chạy business E2E.

Merge sau review và checks phù hợp. Sau merge, `git switch main` rồi `git pull --ff-only origin main`. Xóa feature branch chỉ khi chắc công việc đã vào main và không còn người dùng. Không tự merge/bỏ thay đổi của thành viên khác chỉ vì CI xanh.

## Push trực tiếp main khi owner cho phép

Lượt hoàn thiện foundation/documentation này owner đã yêu cầu push main. Trước push: kiểm tra remote đúng/private, fetch main, bảo toàn source/migration trên feature branch hiện tại, đồng bộ không force, chạy các checks và commit chỉ file liên quan. Nếu conflict/check fail/thiếu quyền thì dừng push và báo cáo; không tắt check hoặc force để vượt lỗi.

Quyền exception của lượt này không phải quy trình thường ngày. Team nên cấu hình branch protection/required review theo owner; chưa khẳng định protection đã được bật.

## Secrets và evidence

Không commit `.env`, local credentials, PEM/private key, database files, bin/obj/node_modules/dist/TestResults. Nếu secret từng được commit: dừng công bố/push phần liên quan, báo owner, revoke/rotate rồi xử lý history có phối hợp; xóa file hiện tại không làm secret cũ an toàn. Không tự rewrite shared history. Mỗi thành viên ghi commit/PR/evidence thật trong [Team Responsibilities](TEAM-RESPONSIBILITIES.md).
