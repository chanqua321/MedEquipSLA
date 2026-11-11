"""Create a review handoff, preserving the runnable repository and ownership paths.

No clone, git mutation, push, app DB access or secret/dependency packaging.
Only new output paths are accepted; existing deliveries are never overwritten.
"""
from pathlib import Path
import argparse
import hashlib
import json
import shutil
import subprocess
import zipfile

ROOT=Path(__file__).resolve().parent.parent
PEOPLE={
 '01_Khanh':('Khánh','Equipment và Equipment gRPC','feature/khanh-equipment'),
 '02_NghiemAnh':('Nghiêm Anh','Maintenance và Calibration','feature/nghiem-anh-maintenance'),
 '03_Phuc':('Phúc','Identity và Incident SLA','feature/phuc-identity-incident'),
 '04_Thang':('Thắng','Notification Gateway nền tảng và tích hợp','feature/thang-platform-notification')}
DENIED={'bin','obj','node_modules','dist','.git','.tools','.secrets','TestResults','Submit','__pycache__','.vs'}
SUFFIXES={'.pem','.key','.pfx','.p12','.mdf','.ldf','.ndf','.bak','.log','.trx','.coverage','.credentials','.user','.suo'}

def eligible(name):
 p=Path(name)
 if p.is_absolute() or '..' in p.parts or any(x in DENIED for x in p.parts):return False
 if name.startswith(('output/submission/','Submit/')) or p.suffix.lower() in SUFFIXES:return False
 if p.name=='secrets.json' or p.name=='.env' or p.name.startswith('.env.') and not p.name.endswith('.example'):return False
 return True

def owner(name):
 if name.startswith('src/Services/Equipment/') or name=='src/Contracts/MedEquipSLA.Contracts.Grpc/Protos/equipment.proto':return '01_Khanh'
 if name.startswith('src/Services/Maintenance/'):return '02_NghiemAnh'
 if name.startswith(('src/Services/Identity/','src/Services/Incident/')):return '03_Phuc'
 filename=Path(name).name
 if name.startswith('tests/MedEquipSLA.Database.IntegrationTests/'):
  if filename.startswith('Equipment'):return '01_Khanh'
  if filename.startswith(('Maintenance','Occurrence','ReminderWindow','WorkOrderGeneration')):return '02_NghiemAnh'
  if filename.startswith('IdentitySecurity'):return '03_Phuc'
 return '04_Thang'

def digest(path):return hashlib.sha256(path.read_bytes()).hexdigest()
def text(path,value):path.parent.mkdir(parents=True,exist_ok=True);path.write_text(value.strip()+'\n',encoding='utf-8')
def main():
 ap=argparse.ArgumentParser();ap.add_argument('--folder',default='Submit');ap.add_argument('--zip',default='output/submission/MedEquipSLA_Submit_Team.zip');a=ap.parse_args()
 target=(ROOT/a.folder).resolve();archive=(ROOT/a.zip).resolve()
 if ROOT not in target.parents or ROOT not in archive.parents:raise SystemExit('Output paths must remain inside the existing workspace.')
 if target.exists() or archive.exists():raise SystemExit('Output exists; refusing to overwrite. Select new output names explicitly.')
 git=subprocess.run(['git','-c','core.quotepath=false','ls-files','--cached','--others','--exclude-standard','-z'],cwd=ROOT,capture_output=True,check=True)
 names=sorted(set(n for n in git.stdout.decode('utf-8').split('\0') if n and eligible(n) and (ROOT/n).is_file()))
 # Output directory is not present while source is inventoried, preventing recursive copies.
 full=target/'CodeDayDu/MedEquipSLA';fragments=target/'CodeTheoThanhVien';docs=target/'TaiLieu'
 members={key:[] for key in PEOPLE};manifest=[]
 for name in names:
  src=ROOT/name
  if src.is_symlink():raise SystemExit('Symlink review required: '+name)
  who=owner(name);members[who].append(name)
  for dest in [full/name,fragments/who/name]:
   dest.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(src,dest)
  manifest.append(dict(path=name,sha256=digest(src),owner=PEOPLE[who][0]))
 for who,(person,scope,branch) in PEOPLE.items():
  base=fragments/who
  text(base/'README-DOC-TRUOC.md',f'''# Bàn giao cho {person}

Phạm vi: {scope}. Branch đề xuất: `{branch}`.
Đây là phân công review/tích hợp, không xác nhận tác giả hoặc công việc đã hoàn tất của thành viên.

Các file bên dưới giữ nguyên đường dẫn repository. Gói này không build độc lập; bản solution đầy đủ ở ../../CodeDayDu/MedEquipSLA. Xem ../../HUONG-DAN-PUSH-GIT.md trước khi đưa file vào checkout của mình. Không đẩy cả folder Submit lên Git, không dùng tên người khác làm tác giả commit.

File shared còn phụ thuộc owner khác; phải điều phối contract và merge qua PR. FILES.txt liệt kê {len(members[who])} file thuộc phạm vi bàn giao.
''')
  text(base/'FILES.txt','\n'.join(members[who]))
 guide=(ROOT/'docs/development/SUBMIT-TEAM-GUIDE.md').read_text(encoding='utf-8')
 text(target/'HUONG-DAN-PUSH-GIT.md',guide)
 assets=['output/documents/MedEquipSLA_API_va_Demo_LuongChinh.docx','output/documents/MedEquipSLA_PhanCongBackend_TuDau.docx','output/handoff/MedEquipSLA_ERD.drawio']
 for name in assets:
  src=ROOT/name
  if not src.is_file():raise SystemExit('Missing required handoff artifact: '+name)
  for dest in [docs/src.name,full/name]:
   dest.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(src,dest)
 for name in ['docs/BUSINESS-DOCUMENTATION.md','docs/DATABASE-DOCUMENTATION.md','docs/API-DOCUMENTATION.md','docs/development/BACKEND-RUNBOOK.md','docs/development/BACKEND-COMPLETION-TRACKER.md']:
  shutil.copy2(ROOT/name,docs/Path(name).name)
 text(docs/'DOC-TRUOC.md','''# Tài liệu đọc trước

1. Word MedEquipSLA_API_va_Demo_LuongChinh.docx: 25 trang, danh mục 106 API, quyền/request và thứ tự demo chính ở 10 trang đầu.
2. Word MedEquipSLA_PhanCongBackend_TuDau.docx: kế hoạch từ đầu cho bốn người; không thay lịch sử đóng góp.
3. ERD draw.io cùng Business/Database/API/runbook: mở bản gốc trong CodeDayDu/MedEquipSLA/docs để các link tương đối hoạt động. Các bản Markdown ở đây là bản đọc nhanh, link về repo có thể cần mở từ bản gốc.
4. Backend tracker ghi evidence/giới hạn. BE có 157/157 kiểm thử SQL2022/Redis và Docker E2E; FE và rollout application DB không được nghiệm thu từ kết quả này.

Không lấy các Word cấu hình/ERD cũ vào gói vì chúng không mô tả đủ schema protocol mới.
''')
 text(target/'README-DOC-TRUOC.md','''# MedEquipSLA Submit cho team review

Mở TaiLieu/MedEquipSLA_API_va_Demo_LuongChinh.docx trước: có request, token/role, RowVersion, ảnh biên bản và thứ tự demo báo hỏng cũng như bảo trì hiệu chuẩn.

Build từ CodeDayDu/MedEquipSLA. CodeTheoThanhVien là các fragment bàn giao, không phải bốn dự án độc lập. Tên Khánh/Nghiêm Anh/Phúc/Thắng chỉ phạm vi review được chủ dự án chốt, không chứng minh tác giả code.

HUONG-DAN-PUSH-GIT.md và README từng người hướng dẫn tích hợp file đúng đường dẫn vào branch/PR của mình. ZIP không có .git hoặc secrets và không tự push lên GitHub. Không copy nguyên folder Submit vào repository.

Sau giải nén cần SDK theo global.json, restore dependencies, tự tạo .env/JWT/service keys theo runbook. Không dùng app database InitialCreate cũ với binary mới khi chưa operator rollout migrations. Frontend là source hiện có chưa nghiệm thu trong lượt này.
''')
 text(target/'MANIFEST.json',json.dumps(dict(description='Ownership handoff, not contribution attribution',files=manifest),ensure_ascii=False,indent=2))
 for item in manifest:
  if digest(full/item['path'])!=item['sha256']:raise SystemExit('Source hash mismatch: '+item['path'])
 archive.parent.mkdir(parents=True,exist_ok=True)
 with zipfile.ZipFile(archive,'x',compression=zipfile.ZIP_DEFLATED,compresslevel=6) as z:
  for p in sorted(target.rglob('*')):
   if p.is_file():z.write(p,p.relative_to(target.parent).as_posix())
 with zipfile.ZipFile(archive) as z:
  if z.testzip() is not None:raise SystemExit('ZIP CRC verification failed.')
  for item in manifest:
   name=target.name+'/CodeDayDu/MedEquipSLA/'+item['path']
   if hashlib.sha256(z.read(name)).hexdigest()!=item['sha256']:raise SystemExit('ZIP hash mismatch: '+name)
 info=dict(folder=str(target),zip=str(archive),zipBytes=archive.stat().st_size,sourceFiles=len(manifest),memberFiles={PEOPLE[k][0]:len(v) for k,v in members.items()},sha256=digest(archive))
 text(archive.with_suffix('.sha256'),info['sha256']+'  '+archive.name)
 print(json.dumps(info,ensure_ascii=False,indent=2))

if __name__=='__main__':main()
