# Skill thiết kế FE trong UGTM

Ba skill dùng chung nằm trong `.agents/skills/`: Impeccable, UI/UX Pro Max và Playwright. Đọc `AGENTS.md` để chọn workflow, `PRODUCT.md` để hiểu nghiệp vụ và `DESIGN.md` để biết nền tảng giao diện đang có.

## Kiểm tra môi trường

Từ thư mục gốc, dùng PowerShell:

```powershell
./scripts/Test-AgentSkills.ps1
./scripts/Test-AgentSkills.ps1 -SmokeTest
# Nếu máy không có Edge:
./scripts/Test-AgentSkills.ps1 -SmokeTest -Browser chrome
```

Lệnh mặc định kiểm tra tệp, metadata cơ bản, ngữ cảnh và runtime. `-SmokeTest` chạy thêm tra cứu React, Impeccable engine và một browser headless riêng tại `about:blank`, lấy snapshot rồi đóng phiên. Đây không phải test giao diện UGTM hay test nghiệp vụ.

Cần Node.js 24.x/npm, Python 3 và browser phù hợp. Các script không cài Python hoặc sửa quyền hệ thống. Impeccable có thể tải engine vào cache cá nhân ở lần chạy đầu, với checksum từ manifest đi kèm; Playwright dùng npm cache. Lần đầu hoặc khi cache thiếu cần mạng và quyền ghi cache. Các phiên browser/snapshot nằm trong `artifacts/playwright/`, đã được Git bỏ qua.

## Windows / PowerShell

### Tra cứu UI/UX

```powershell
./scripts/Invoke-SkillPython.ps1 ./.agents/skills/ui-ux-pro-max/scripts/search.py 'form' --stack react -n 2
```

Runner thử Python trên PATH, `py -3` và thư mục Python cài theo người dùng. Nó bỏ qua alias Microsoft Store, kiểm tra runtime thực sự chạy được, dùng UTF-8, không tạo bytecode cache và trả exit code của script. Nếu cần chỉ định runtime:

```powershell
$env:UGTM_PYTHON = 'D:/Tools/Python/python.exe' # thay bằng executable thực có trên máy
```

Nếu runtime chỉ chạy được ngoài sandbox, yêu cầu quyền thực thi phù hợp qua công cụ của agent. Không cài Python lần nữa hoặc hạ chính sách bảo vệ để né lỗi quyền.

### Kiểm tra giao diện

```powershell
$pwcli = (Resolve-Path ./.agents/skills/playwright/scripts/playwright_cli.ps1).Path
& $pwcli '-s=ugtm-review' open http://localhost:5173 --browser msedge
& $pwcli '-s=ugtm-review' snapshot
& $pwcli '-s=ugtm-review' resize 390 844
& $pwcli '-s=ugtm-review' screenshot
& $pwcli '-s=ugtm-review' close
```

Wrapper PowerShell và Bash cùng dùng `@playwright/cli@0.1.24`. Chúng tự chuyển thư mục làm việc sang `artifacts/playwright/` để chứa output. Dùng đường dẫn tuyệt đối cho upload, config, file hoặc profile; không dùng profile browser cá nhân. Chỉ dùng `--headed` khi cần xem cửa sổ.

Trên Bash, gọi `bash .agents/skills/playwright/scripts/playwright_cli.sh <arguments>` để không phụ thuộc executable bit khi checkout. Các file Bash phải giữ LF. `PLAYWRIGHT_CLI_SESSION` chọn session mặc định; `-s=` hoặc `--session` ghi đè session đó. Đặt tên phiên riêng cho các tác vụ cùng lúc.

### Impeccable

```powershell
./.agents/skills/impeccable/scripts/impeccable.cmd context --target frontend/src/pages/DashboardPage.tsx
```

Giữ cwd ở root khi chạy context. Đọc playbook phù hợp theo `SKILL.md`; các wrapper và smoke test không cấp quyền đổi scope của yêu cầu.

Dự án dùng kiểm tra thiết kế thủ công; `.impeccable/config.json` ghi rõ automatic hook đang tắt. Điều này giữ nguyên workflow hiện tại, không gắn thêm hook vào mọi lần sửa file. Khi có thay đổi UI, agent có thể chạy `impeccable.cmd detect --json <changed-targets>` theo skill. Bật hook tự động hoặc chọn mặc định image-first/code-first là quyết định riêng, không được suy ra từ việc cài skill.

## Nhận diện và cập nhật

Trong Codex, có thể gọi `$impeccable`, `$ui-ux-pro-max` hoặc `$playwright`. Không cần MCP server cho các helper CLI/local search này. Các skill tự động được xem xét theo mô tả; không bắt agent chạy cả ba workflow cho mọi thay đổi.

Codex quét `.agents/skills` từ cwd lên root repository. Nếu cùng tên có ở thư mục cá nhân, các bản không được gộp; hãy kiểm tra đường dẫn khi chọn. Cấu trúc này dành cho agent hỗ trợ Agent Skills/AGENTS.md; công cụ khác cần cơ chế cấu hình riêng của công cụ đó. Xem [tài liệu Codex](https://learn.chatgpt.com/docs/build-skills). Nếu thay đổi chưa xuất hiện, khởi động lại phiên Codex.

Nguồn upstream:

- [Impeccable](https://github.com/pbakaus/impeccable): skill 4.8.2, engine pin trong `scripts/VERSION` và `engine-checksums.txt`.
- [UI/UX Pro Max](https://github.com/nextlevelbuilder/ui-ux-pro-max-skill): skill, scripts và CSV được đưa vào repository; không cần package Python ngoài cho search.
- [Playwright skill](https://github.com/openai/skills/tree/main/skills/.curated/playwright): đã bổ sung wrapper PowerShell và pin CLI cho dự án.

Khi cập nhật upstream, review diff và giữ các điều chỉnh đường dẫn, metadata, PowerShell wrapper và vị trí artifacts của dự án. Chạy lại kiểm tra skill và smoke test. Validator đầy đủ của `skill-creator` có thể dùng thêm nếu đã có trong môi trường; script kiểm tra dự án không phụ thuộc thư mục Codex cá nhân.
