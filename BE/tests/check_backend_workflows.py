import os, time, secrets, subprocess, json, shutil, urllib.request, urllib.error
from pathlib import Path

root = Path(__file__).resolve().parents[1]
local_dotnet = root / '.tools/dotnet/dotnet.exe'
dotnet = Path(os.environ.get('MEDEQUIP_DOTNET') or
              (str(local_dotnet) if local_dotnet.is_file() else shutil.which('dotnet') or 'dotnet.exe')).resolve()
tmp = root / '.tools/identity-web-smoke'
tmp.mkdir(parents=True, exist_ok=True)
suffix = secrets.token_hex(8)
names = {owner: f'MedEquip_{owner}_Test_{suffix}' for owner in ('Identity', 'Equipment', 'Incident', 'Maintenance', 'Notification')}
created = []
processes = []
logs = []
password = 'Aa9!' + secrets.token_hex(20)
keys = [tmp / f'{suffix}-{part}.pem' for part in ('private', 'public')]
flags = subprocess.CREATE_NO_WINDOW
admin_recovery = []
docker_mode = os.environ.get('MEDEQUIP_TEST_DOCKER') == '1'
docker_settings = {}
if docker_mode:
    docker_settings = dict(line.split('=',1) for line in (root/'.tools/docker-verification.env').read_text().splitlines() if '=' in line)
    if not docker_settings.get('COMPOSE_PROJECT_NAME','').startswith('medequip-be-test-'):
        raise RuntimeError('Owned Compose test manifest required')
sql_auth = ['-S','tcp:127.0.0.1,14333','-U','sa'] if docker_mode else ['-S',r'(localdb)\MSSQLLocalDB','-E']
sql_env = dict(os.environ, SQLCMDPASSWORD=docker_settings['SQL_TEST_PASSWORD']) if docker_mode else dict(os.environ)

class DockerProcess:
    def __init__(self, container, handle): self.container, self.handle, self.stopped = container,handle,False
    def poll(self):
        if self.stopped: return 0
        state = subprocess.run(['docker','inspect','--format','{{.State.Running}}',self.container],capture_output=True,text=True,creationflags=flags)
        return None if state.returncode==0 and state.stdout.strip()=='true' else 0
    def terminate(self):
        subprocess.run(['docker','logs',self.container],stdout=self.handle,stderr=subprocess.STDOUT,creationflags=flags)
        subprocess.run(['docker','stop','--time','3',self.container],stdout=subprocess.DEVNULL,check=True,creationflags=flags)
        self.stopped=True
    def wait(self,timeout=12):
        if not self.stopped:
            subprocess.run(['docker','wait',self.container],stdout=subprocess.DEVNULL,timeout=timeout,check=True,creationflags=flags)
        return 0

def sql(statement):
    subprocess.run(['sqlcmd','-I',*sql_auth,'-C','-b','-d','master','-Q',statement],env=sql_env,check=True,creationflags=flags)

def start(owner, extra, bootstrap=False):
    folder = (root / 'src/Gateway/MedEquipSLA.Gateway' if owner == 'Gateway'
              else root / f'src/Services/{owner}/{owner}.Api')
    dll = 'MedEquipSLA.Gateway' if owner == 'Gateway' else owner + '.Api'
    env = dict(os.environ, ASPNETCORE_ENVIRONMENT='Development', DOTNET_ROOT=str(dotnet.parent), **extra)
    if owner in names:
        env.update(Migrations__ApplyOnStartup='true', ConnectionStrings__Database=
            r'Server=(localdb)\MSSQLLocalDB;Database='+names[owner]+';Integrated Security=True;Encrypt=True;TrustServerCertificate=True')
    path = tmp / f'{suffix}-{owner}{"-bootstrap" if bootstrap else ""}.log'
    handle = path.open('wb')
    logs.append(handle)
    if docker_mode:
        settings = dict(extra, ASPNETCORE_ENVIRONMENT='Development',Workers__Enabled=extra.get('Workers__Enabled','false'))
        if owner in names:
            settings.update(Migrations__ApplyOnStartup='true',ConnectionStrings__Database=
                'Server=sqlserver,1433;Database='+names[owner]+';User Id=sa;Password='+docker_settings['SQL_TEST_PASSWORD']+';Encrypt=True;TrustServerCertificate=True',
                ConnectionStrings__Redis='redis:6379,defaultDatabase=14,abortConnect=false',Jwt__PublicKeyPath='/run/keys/public.pem')
        if owner=='Identity': settings['Jwt__PrivateKeyPath']='/run/keys/private.pem'
        for setting,value in list(settings.items()):
            if setting in ('ASPNETCORE_URLS','Kestrel__Endpoints__Rest__Url','Kestrel__Endpoints__Grpc__Url'):
                settings[setting]=value.replace('localhost','0.0.0.0')
            elif setting not in ('Jwt__PublicKeyPath','Jwt__PrivateKeyPath') and isinstance(value,str) and value.startswith('http://'):
                for peer,port in [('identity',5111),('equipment',5112),('equipment',5113),('maintenance',5114),('incident',5115),('notification',5116)]:
                    value=value.replace(f'localhost:{port}',f'{peer}-api:{port}')
                settings[setting]=value.replace('127.0.0.1:','host.docker.internal:')
        command=['docker','run','--rm','--network',docker_settings['COMPOSE_PROJECT_NAME']+'_default',
            '--network-alias',owner.lower()+'-api','--label','medequip.verification='+suffix]
        if not bootstrap:
            command.append('-d')
            for port in {'Gateway':[5110],'Identity':[5111],'Equipment':[5112,5113],'Maintenance':[5114],'Incident':[5115],'Notification':[5116]}[owner]:
                command += ['-p',f'127.0.0.1:{port}:{port}']
        if owner in names:
            command += ['--mount',f'type=bind,source={keys[1]},target=/run/keys/public.pem,readonly']
        if owner=='Identity': command += ['--mount',f'type=bind,source={keys[0]},target=/run/keys/private.pem,readonly']
        # Use inherited environment names, avoiding secret values in Docker command arguments.
        for setting in settings: command += ['-e',setting]
        command += ['medequip-sla-'+owner.lower()]
        if bootstrap:
            result=subprocess.run(command,env=dict(os.environ,**settings),stdout=handle,stderr=subprocess.STDOUT,creationflags=flags,timeout=60)
            if result.returncode: raise RuntimeError('Container bootstrap failed; see '+str(path))
            return result
        result=subprocess.run(command,env=dict(os.environ,**settings),capture_output=True,text=True,creationflags=flags,check=True)
        proc=DockerProcess(result.stdout.strip(),handle);processes.append(proc);return proc
    proc = subprocess.Popen([str(dotnet), str(folder / f'bin/Release/net10.0/{dll}.dll')],cwd=folder,env=env,
        stdout=handle,stderr=subprocess.STDOUT,creationflags=flags)
    processes.append(proc)
    if bootstrap and proc.wait(timeout=45):
        raise RuntimeError('Admin bootstrap failed: '+str(path))
    return proc

def req(route, method='GET', body=None, token=None, expected=200, base='http://localhost:5110', idem=None, recipient_key=None):
    if route.endswith('/auth/login') and body and body.get('userName')=='demo-admin' and body.get('password')==password:
        body = dict(body, recoveryCode=admin_recovery.pop(0))
    headers = {'Content-Type':'application/json'}
    if token: headers['Authorization'] = 'Bearer '+token
    if idem: headers['Idempotency-Key'] = idem
    if recipient_key: headers['X-Recipient-Lookup-Key'] = recipient_key
    try:
        response = urllib.request.urlopen(urllib.request.Request(base+route,method=method,headers=headers,
            data=json.dumps(body).encode() if body is not None else None),timeout=5)
    except urllib.error.HTTPError as error:
        response = error
    with response:
        raw = response.read()
        assert response.status in (expected if isinstance(expected, tuple) else (expected,)), (route, expected, response.status, raw[:250] if response.status >=400 else b'')
        return json.loads(raw) if raw and response.status <400 else None

def ready(port, proc):
    deadline = time.monotonic()+40
    while time.monotonic()<deadline and proc.poll() is None:
        try:
            urllib.request.urlopen(f'http://localhost:{port}/health/live',timeout=2).close()
            return
        except OSError:
            time.sleep(.3)
    raise RuntimeError(f'Port {port} not ready')

try:
    subprocess.run(['pwsh','-NoProfile','-Command',
        '$rsa=[Security.Cryptography.RSA]::Create(2048); '
        f'[IO.File]::WriteAllText(\'{keys[0]}\',$rsa.ExportPkcs8PrivateKeyPem()); '
        f'[IO.File]::WriteAllText(\'{keys[1]}\',$rsa.ExportSubjectPublicKeyInfoPem())'],
        check=True,creationflags=flags)
    for name in names.values():
        sql(f'CREATE DATABASE [{name}]')
        created.append(name)
    recipient_key = secrets.token_hex(32)
    identity = dict(Jwt__PublicKeyPath=str(keys[1]), Jwt__PrivateKeyPath=str(keys[0]), Identity__RecipientLookupKey=recipient_key,
        Identity__PublicWebAddress='https://web.tests.invalid', Equipment__Address='http://localhost:5112',
        Kestrel__Endpoints__Rest__Url='http://localhost:5111', Kestrel__Endpoints__Grpc__Url='http://localhost:5117')
    start('Identity', dict(identity, BootstrapAdmin__Run='true', BootstrapAdmin__UserName='demo-admin',
        BootstrapAdmin__Email='admin@example.test', BootstrapAdmin__Password=password), bootstrap=True)
    ready(5111, start('Identity', identity))
    # Bootstrap alone grants no Admin session. Enroll through the actual password-authenticated MFA endpoints.
    setup = req('/api/auth/mfa/setup','POST',{'userName':'demo-admin','password':password,'method':'Totp'},base='http://localhost:5111')
    import base64, hashlib, hmac, struct
    secret = base64.b32decode(setup['secret']+'='*((8-len(setup['secret'])%8)%8))
    digest = hmac.new(secret,struct.pack('>Q',int(time.time())//30),hashlib.sha1).digest()
    offset = digest[-1]&15
    code = f'{(struct.unpack(">I",digest[offset:offset+4])[0]&0x7fffffff)%1000000:06d}'
    admin_recovery.extend(req('/api/auth/mfa/confirm','POST',{'userId':setup['userId'],'setupToken':setup['setupToken'],'code':code},base='http://localhost:5111')['recoveryCodes'])
    registration_secret = secrets.token_hex(32)
    maintenance_secret = secrets.token_hex(32)
    workorder_secret = secrets.token_hex(32)
    equipment_env = dict(Jwt__PublicKeyPath=str(keys[1]), Grpc__IncidentRegistrationKey=registration_secret,
        Grpc__MaintenanceRegistrationKey=maintenance_secret,
        Grpc__MaintenanceWorkOrderKey=workorder_secret,
        Kestrel__Endpoints__Rest__Url='http://localhost:5112', Kestrel__Endpoints__Grpc__Url='http://localhost:5113',
        Identity__Address='http://localhost:5111')
    equipment_proc = start('Equipment', equipment_env)
    ready(5112, equipment_proc)
    ready(5115, start('Incident', dict(Jwt__PublicKeyPath=str(keys[1]), ASPNETCORE_URLS='http://localhost:5115',
        Grpc__IncidentRegistrationKey=registration_secret, Grpc__EquipmentAddress='http://localhost:5113',
        Identity__Address='http://localhost:5111')))
    maintenance_env = dict(Jwt__PublicKeyPath=str(keys[1]), ASPNETCORE_URLS='http://localhost:5114',
        Identity__Address='http://localhost:5111', Grpc__EquipmentAddress='http://localhost:5113',
        Grpc__MaintenanceRegistrationKey=maintenance_secret, Grpc__MaintenanceWorkOrderKey=workorder_secret,
        Identity__RecipientLookupKey=recipient_key)
    maintenance_proc = start('Maintenance', maintenance_env)
    ready(5114, maintenance_proc)
    ready(5116, start('Notification', dict(Jwt__PublicKeyPath=str(keys[1]), ASPNETCORE_URLS='http://localhost:5116',
        Identity__Address='http://localhost:5111')))
    gateway = start('Gateway', dict(ASPNETCORE_URLS='http://localhost:5110',
        ReverseProxy__Clusters__notification__Destinations__primary__Address='http://localhost:5116',
        ReverseProxy__Clusters__maintenance__Destinations__primary__Address='http://localhost:5114',
        ReverseProxy__Clusters__identity__Destinations__primary__Address='http://localhost:5111',
        ReverseProxy__Clusters__equipment__Destinations__primary__Address='http://localhost:5112', ReverseProxy__Clusters__incident__Destinations__primary__Address='http://localhost:5115'))
    ready(5110, gateway)
    for owner,port in (('Identity',5111),('Equipment',5112),('Maintenance',5114),('Incident',5115),('Notification',5116)):
        spec=req('/swagger/v1/swagger.json',base=f'http://localhost:{port}')
        assert spec['paths']
        if owner=='Identity':
            assert spec['paths']['/api/auth/login']['post'].get('security')==[]
            internal=spec['paths']['/api/internal/notification-recipients']['post']
            assert any(p['name']=='X-Recipient-Lookup-Key' and p['required'] for p in internal['parameters'])
        if os.environ.get('MEDEQUIP_TEST_CAPTURE_DOCS')=='1':
            dest=root/'docs/api/openapi'/f'{owner}.json'
            dest.parent.mkdir(parents=True,exist_ok=True)
            dest.write_text(json.dumps(spec,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print('PASS: Swagger for five services, public login security and internal service credential documentation')
    auth='/identity/api/auth'
    dep='/equipment/api/departments'
    req(dep,expected=401)
    credentials={'userName':'demo-admin','password':password}
    session=req(auth+'/login','POST',credentials)
    other=req(auth+'/login','POST',credentials)
    token=session['accessToken']
    sql(f"""USE [{names['Identity']}];
DECLARE @id uniqueidentifier = NEWID();
INSERT dbo.AspNetUsers (Id, UserName, NormalizedUserName, Email, NormalizedEmail, EmailConfirmed, PasswordHash, SecurityStamp, ConcurrencyStamp,
 PhoneNumberConfirmed, TwoFactorEnabled, LockoutEnabled, AccessFailedCount, FullName, IsActive, AuthorizationVersion, CreatedAtUtc, UpdatedAtUtc)
SELECT @id, 'demo-tech', 'DEMO-TECH', 'tech@example.test', 'TECH@EXAMPLE.TEST', 1, PasswordHash, NEWID(), NEWID(),
 0, 0, 1, 0, 'Demo Technician', 1, 1, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET()
FROM dbo.AspNetUsers WHERE NormalizedUserName = 'DEMO-ADMIN';
INSERT dbo.AspNetUserRoles (UserId, RoleId) SELECT @id, Id FROM dbo.AspNetRoles WHERE Name = 'Technician';""")
    tech = req(auth+'/login','POST',{'userName':'demo-tech','password':password})
    directory = '/identity/api/users'
    req(directory,token=tech['accessToken'],expected=403)
    employee = req(directory+'?search=Demo',token=token)['items'][0]
    access = directory+'/'+employee['id']+'/access'
    req(access,'PUT',{'active':False,'rowVersion':employee['rowVersion']},token,204)
    req(auth+'/me',token=tech['accessToken'],expected=401)
    req(auth+'/refresh','POST',{'refreshToken':tech['refreshToken']},expected=401)
    req(access,'PUT',{'active':True,'rowVersion':employee['rowVersion']},token,409)
    employee = req(directory,token=token)['items'][0]
    req(access,'PUT',{'active':True,'rowVersion':employee['rowVersion']},token,204)
    req(auth+'/login','POST',{'userName':'demo-tech','password':password})
    print('PASS: employee directory RBAC, disable, session revocation, stale write, reactivate')
    invite = {'email':'invited@example.test','fullName':'Invited Employee','role':'Technician'}
    req(directory,'POST',invite,tech['accessToken'],403)
    invited = req(directory,'POST',invite,token,202)
    req(directory,'POST',invite,token,409)
    req(directory,'POST',dict(invite,email='extra-admin@example.test',role='Admin'),token,400)
    req(auth+'/login','POST',{'userName':invite['email'],'password':password},expected=401)
    # Read the opaque event reference in the owned test catalog; only the authenticated Identity endpoint can render the secret.
    def query_scalar(statement):
        return subprocess.check_output(['sqlcmd','-I',*sql_auth,'-C','-b','-d',names['Identity'],
            '-h','-1','-W','-Q','SET NOCOUNT ON; '+statement],env=sql_env,creationflags=flags,text=True).strip()
    ticket_id = query_scalar(f"SELECT JSON_VALUE(PayloadJson,'$.TicketId') FROM dbo.OutboxMessages WHERE AggregateType='SecurityTicket' AND JSON_VALUE(PayloadJson,'$.UserId')='{invited['userId']}'")
    mail = req('/api/internal/security-mail/'+invited['userId']+'/'+ticket_id,base='http://localhost:5111',recipient_key=recipient_key)
    from urllib.parse import urlparse,parse_qs
    link = parse_qs(urlparse(mail['body'].splitlines()[0]).query)
    activation = {'userId':invited['userId'],'ticketId':ticket_id,'token':link['token'][0],'password':password}
    req(auth+'/activate','POST',activation,expected=204)
    req(auth+'/activate','POST',activation,expected=400)
    invite_session = req(auth+'/login','POST',{'userName':invite['email'],'password':password})
    current = req(directory+'?search=Invited',token=token)['items'][0]
    req(directory+'/'+invited['userId']+'/profile','PUT',{'fullName':'Invited Employee Updated','role':'Technician',
        'rowVersion':current['rowVersion']},token,204)
    req(auth+'/me',token=invite_session['accessToken'],expected=401)
    print('PASS: employee invitation RBAC, duplicate email, no extra Admin, single-use activation and profile/session revocation')
    import io,zipfile
    workbook = io.BytesIO()
    with zipfile.ZipFile(workbook,'w') as archive:
        rows = [['Email','FullName','Role','DepartmentId'],['imported@example.test','Imported Employee','Technician',''],
                ['invalid','Bad Row','Admin',''],['imported@example.test','Duplicate Row','Technician','']]
        xml = '<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><sheetData>'
        for number,values in enumerate(rows,1):
            xml += f'<row r="{number}">'+''.join(f'<c r="{chr(65+i)}{number}" t="inlineStr"><is><t>{value}</t></is></c>' for i,value in enumerate(values))+'</row>'
        archive.writestr('xl/worksheets/sheet1.xml',xml+'</sheetData></worksheet>')
    def import_employees(preview):
        boundary = 'import-'+secrets.token_hex(12)
        data = f'--{boundary}\r\nContent-Disposition: form-data; name="file"; filename="employees.xlsx"\r\nContent-Type: application/vnd.openxmlformats-officedocument.spreadsheetml.sheet\r\n\r\n'.encode()+workbook.getvalue()+f'\r\n--{boundary}--\r\n'.encode()
        with urllib.request.urlopen(urllib.request.Request('http://localhost:5110'+directory+'/import?preview='+str(preview).lower(),data=data,
            headers={'Content-Type':'multipart/form-data; boundary='+boundary,'Authorization':'Bearer '+token}),timeout=8) as response:
            return json.load(response)
    assert [x['status'] for x in import_employees(True)['rows']]==['Valid','InvalidRoleOrDepartment','DuplicateInWorkbook']
    assert [x['status'] for x in import_employees(False)['rows']]==['Created','InvalidRoleOrDepartment','DuplicateInWorkbook']
    assert import_employees(False)['rows'][0]['status']=='Duplicate'
    print('PASS: Excel preview, per-row validation, partial success and no duplicate accounts on repeat import')
    for _ in range(5):
        req(auth+'/login','POST',{'userName':'demo-tech','password':'wrong-password'},expected=401)
    req(auth+'/login','POST',{'userName':'demo-tech','password':password},expected=401)
    sql(f"""USE [{names['Identity']}];
IF NOT EXISTS(SELECT 1 FROM dbo.AspNetUsers WHERE UserName='demo-tech' AND AccessFailedCount=5
  AND DATEDIFF(MINUTE,SYSDATETIMEOFFSET(),LockoutEnd) BETWEEN 14 AND 15) THROW 50001,'Wrong first lockout',1;
UPDATE dbo.AspNetUsers SET LockoutEnd=DATEADD(MINUTE,-1,SYSDATETIMEOFFSET()) WHERE UserName='demo-tech';""")
    req(auth+'/login','POST',{'userName':'demo-tech','password':'wrong-password'},expected=401)
    sql(f"""USE [{names['Identity']}];
IF NOT EXISTS(SELECT 1 FROM dbo.AspNetUsers WHERE UserName='demo-tech' AND AccessFailedCount=6
  AND DATEDIFF(MINUTE,SYSDATETIMEOFFSET(),LockoutEnd) BETWEEN 29 AND 30) THROW 50002,'Wrong second lockout',1;""")
    print('PASS: escalating login lockout and retained failed attempts')
    swagger = req('/swagger/v1/swagger.json', base='http://localhost:5115')
    assert swagger['components']['securitySchemes']['Bearer']['scheme']=='bearer'
    form_schema = swagger['paths']['/api/incidents/{id}/attempts']['post']['requestBody']['content']['multipart/form-data']
    assert 'photo' in json.dumps(form_schema)
    with urllib.request.urlopen('http://localhost:5115/swagger/index.html',timeout=5) as page:
        assert page.status == 200
    seed = (root/'tests/database/Incident-Behavior.sql').read_text(encoding='utf-8').splitlines()
    seed = '\n'.join(line for line in seed if line.startswith('INSERT ') ) 
    seed = '\n'.join(seed.splitlines()[:3])
    sql(f"USE [{names['Incident']}];\n"+seed)
    path='/incident/api/incidents'
    req(path,expected=401)
    incident=req(path,token=token)[0]
    sql(f"USE [{names['Identity']}]; UPDATE dbo.AspNetUsers SET LockoutEnd=NULL WHERE UserName='demo-tech';")
    technician=req(auth+'/login','POST',{'userName':'demo-tech','password':password})['accessToken']
    techid=req(auth+'/me',token=technician)['id']
    assert incident['status']=='Open'
    incident_path=path+'/'+incident['id']
    req(incident_path+'/start','POST',{'rowVersion':incident['rowVersion']},technician,403)
    req(incident_path+'/assignment','PUT',{'technicianId':techid,'rowVersion':incident['rowVersion']},technician,403)
    req(incident_path+'/assignment','PUT',{'technicianId':techid,'rowVersion':incident['rowVersion']},token,204)
    req(incident_path+'/start','POST',{'rowVersion':incident['rowVersion']},technician,409)
    incident=req(path,token=token)[0]
    req(incident_path+'/start','POST',{'rowVersion':incident['rowVersion']},technician,204)
    incident=req(path,token=token)[0]
    repair=path+'/'+incident['id']+'/attempts'
    import base64
    png=base64.b64decode('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jTt8AAAAASUVORK5CYII=')
    def submit(photo, expected):
        parts=[]
        boundary='MedEquipSmokeBoundary'
        for name,value in dict(notes='Repair notes',result='Test run passed',rowVersion=incident['rowVersion']).items():
            parts.append(f'--{boundary}\r\nContent-Disposition: form-data; name="{name}"\r\n\r\n{value}\r\n'.encode())
        if photo is not None:
            parts.append(f'--{boundary}\r\nContent-Disposition: form-data; name="photo"; filename="evidence.png"\r\nContent-Type: image/png\r\n\r\n'.encode()+photo+b'\r\n')
        body=b''.join(parts)+f'--{boundary}--\r\n'.encode()
        request=urllib.request.Request('http://localhost:5110'+repair,data=body,headers={
            'Content-Type':'multipart/form-data; boundary='+boundary,'Authorization':'Bearer '+technician})
        try:
            response=urllib.request.urlopen(request,timeout=8)
        except urllib.error.HTTPError as error:
            response=error
        with response:
            assert response.status==expected, (expected,response.status,response.read())
    submit(None,400)
    submit(b'not an image',400)
    submit(png,201)
    submit(png,409)
    record=req(repair,token=token)[0]
    assert record['hasPhoto'] and record['reviewDecision']=='Pending'
    assert req(path,token=token)[0]['status']=='Resolved'
    download='http://localhost:5110'+repair+'/'+record['id']+'/photo'
    with urllib.request.urlopen(urllib.request.Request(download,headers={'Authorization':'Bearer '+token}),timeout=5) as result:
        assert result.headers.get_content_type()=='image/png' and result.read()==png
    print('PASS: Swagger UI and multipart contract, missing/invalid photo, submission, stale repeat and authenticated photo download')
    incident=req(path,token=token)[0]
    review=repair+'/'+record['id']+'/reject'
    payload={'reason':'Repeat loaded test','rowVersion':incident['rowVersion']}
    req(review,'POST',payload,technician,403)
    req(review,'POST',payload,token,204)
    req(review,'POST',payload,token,409)
    incident=req(path,token=token)[0]
    assert incident['status']=='InProgress'
    submit(png,201)
    history=req(repair,token=token)
    assert [x['reviewDecision'] for x in history]==['Pending','Rejected']
    assert history[1]['reviewReason']=='Repeat loaded test' and history[1]['hasPhoto']
    print('PASS: assign, start, reject with reason, resubmit, RBAC, concurrency and retained evidence')
    incident = req(path,token=token)[0]
    accept = repair+'/'+history[0]['id']+'/accept'
    review_input = {'rowVersion':incident['rowVersion']}
    req(accept,'POST',review_input,technician,403)
    req(accept,'POST',review_input,token,204)
    req(accept,'POST',review_input,token,409)
    assert req(path,token=token)[0]['status']=='Closed'
    assert req(repair,token=token)[0]['reviewDecision']=='Accepted'
    print('PASS: Admin acceptance closes the incident, preserves history and rejects a stale review')
    me=req(auth+'/me',token=token)
    assert me['roles']==['Admin']
    created_dep=req(dep,'POST',{'code':'TEST-KHOA','name':'Khoa kiểm thử'},token,201)
    row=dep+'/'+created_dep['id']
    version=created_dep['rowVersion']
    edited=req(row,'PUT',{'name':'Khoa đã sửa','rowVersion':version},token)
    req(row,'PUT',{'name':'Tranh chấp','rowVersion':version},token,409)
    assert req(dep+'?search=TEST-KHOA&active=true&sort=code&page=1&pageSize=10',token=token)['total']==1
    from urllib.parse import urlencode
    req(row+'?'+urlencode({'version':edited['rowVersion']}),'DELETE',token=token,expected=204)
    assert not req(row,token=token)['isActive']
    from uuid import uuid4
    equipment_path = '/equipment/api/equipment'
    categories = '/equipment/api/equipment-categories'
    category_input = {'code':'TEST-EQP','name':'Test category'}
    category = req(categories,'POST',category_input,token,201)
    req(categories,'POST',category_input,token,409)
    assert req(categories+'?search=TEST-EQP',token=token)[0]['id']==category['id']
    spare=req(categories,'POST',{'code':'TEST-ARCHIVE','name':'Archive candidate'},token,201)
    spare_path=categories+'/'+spare['id']
    req(spare_path,'GET',token=technician,expected=403)
    spare_updated=req(spare_path,'PUT',{'name':'Renamed category','description':'Retain historical links','active':True,'rowVersion':spare['rowVersion']},token)
    req(spare_path,'PUT',{'name':'Stale category','active':True,'rowVersion':spare['rowVersion']},token,409)
    req(spare_path+'?'+urlencode({'version':spare['rowVersion']}),'DELETE',token=token,expected=409)
    req(spare_path+'?'+urlencode({'version':spare_updated['rowVersion']}),'DELETE',token=token,expected=204)
    assert req(spare_path,token=token)['isActive'] is False
    assert req(categories+'?search=TEST-ARCHIVE',token=token)[0]['name']=='Renamed category'
    print('PASS: Category CRUD/logical archive, retained history, Admin-only and stale RowVersion rejection')
    active_dept = req(dep,'POST',{'code':'TEST-ACTIVE','name':'Active department'},token,201)
    profile = {'name':'Test ventilator','serialNumber':'TEST-SERIAL','categoryId':category['id']}
    asset_input = {'code':'TEST-DEVICE','departmentId':active_dept['id'],'profile':profile}
    req(equipment_path,'POST',asset_input,technician,403)
    req(equipment_path,'POST',dict(asset_input,departmentId=created_dep['id']),token,400)
    req(equipment_path,'POST',dict(asset_input,profile=dict(profile,categoryId=str(uuid4()))),token,400)
    asset = req(equipment_path,'POST',asset_input,token,201)
    asset_id = asset['id']
    req(equipment_path,'POST',dict(asset_input,code='ANOTHER'),token,409)
    asset_path = equipment_path+'/'+asset_id+'/profile'
    update = {'profile':dict(profile,name='Updated ventilator'),'rowVersion':asset['rowVersion']}
    assert req(asset_path,'PUT',update,token)['profile']['name']=='Updated ventilator'
    req(asset_path,'PUT',update,token,409)
    assert req(asset_path,token=token)['departmentId']==active_dept['id']
    sql(f"""USE [{names['Equipment']}];
    IF (SELECT COUNT(*) FROM dbo.AuditEntries WHERE EntityId='{asset_id}' AND Action IN ('EquipmentCreated','EquipmentUpdated')) <> 2
        THROW 50003, 'Expected exactly two equipment audits', 1;""")
    print('PASS: Admin equipment catalog, active references, unique serial, stale row version and audit')
    req(equipment_path,expected=401)
    rows = req(equipment_path+'?search=ventilator',token=token)
    assert rows['items'][0]['id']==asset_id and not rows['hasNext']
    assert req(path+'?id='+incident['id'],token=token)[0]['status']=='Closed'
    sql(f"""USE [{names['Incident']}];
    UPDATE dbo.SlaPolicyVersions SET EffectiveFromUtc=DATEADD(day,-1,SYSDATETIMEOFFSET()), EffectiveToUtc=NULL;
    """)
    sla_route = '/incident/api/sla-policies'
    req(sla_route, token=technician, expected=403)
    config = {'enabled':True,'responseDurationMinutes':20,'resolutionDurationMinutes':40}
    req(sla_route+'/High','PUT',config,token,204)
    high = next(row for row in req(sla_route,token=token) if row['priority']=='High')
    req(sla_route+'/High','PUT',dict(config,rowVersion=high['rowVersion'],resolutionDurationMinutes=45),token,204)
    req(sla_route+'/High','PUT',dict(config,rowVersion=high['rowVersion']),token,409)
    high = next(row for row in req(sla_route,token=token) if row['priority']=='High')
    assert high['version']['version']==2 and high['version']['resolutionDurationMinutes']==45
    print('PASS: Admin SLA configuration creates new versions and rejects stale edits')
    key = str(uuid4())
    create_input = {'equipmentId':asset_id,'title':'Device issue','description':'Power failure','priority':'High'}
    created_incident = req(path,'POST',create_input,token,201,idem=key)
    assert req(path,'POST',create_input,token,201,idem=key)==created_incident
    req(path,'POST',dict(create_input,title='Another'),token,409,idem=key)
    req(path,'POST',create_input,token,409,idem=str(uuid4()))
    assert req(path+'?id='+created_incident['id'],token=token)[0]['status']=='Open'
    sql(f"""USE [{names['Equipment']}];
    IF (SELECT COUNT(*) FROM dbo.EquipmentUseRegistrations WHERE EquipmentId='{asset_id}' AND State='Pending') <> 1
        THROW 50003, 'Registration must be created once', 1;""")
    print('PASS: Gateway equipment search, Incident creation over Equipment gRPC and SQL idempotency')
    target_dept = req(dep,'POST',{'code':'TEST-TARGET','name':'Target department'},token,201)
    current_asset = req(asset_path,token=token)
    transfer = equipment_path+'/'+asset_id+'/department-transfer'
    move = {'departmentId':target_dept['id'],'reason':'Relocation','rowVersion':current_asset['rowVersion']}
    req(transfer,'POST',move,technician,403)
    req(transfer,'POST',dict(move,departmentId=created_dep['id']),token,400)
    assert req(transfer,'POST',move,token)['departmentId']==target_dept['id']
    req(transfer,'POST',move,token,409)
    sql(f"""USE [{names['Equipment']}];
    IF NOT EXISTS (SELECT 1 FROM dbo.EquipmentStatusHistories h JOIN dbo.OutboxMessages o ON o.EventId=h.SourceEventId
      WHERE h.EquipmentId='{asset_id}' AND h.FromDepartmentId='{active_dept['id']}' AND h.ToDepartmentId='{target_dept['id']}'
        AND h.Reason='Relocation' AND o.EventType='EquipmentDepartmentChanged' AND o.Status='Pending')
      THROW 50003, 'Transfer history and outbox are required', 1;
    USE [{names['Incident']}];
    IF NOT EXISTS (SELECT 1 FROM dbo.Incidents WHERE Id='{created_incident['id']}' AND DepartmentIdSnapshot='{active_dept['id']}')
      THROW 50004, 'Incident snapshot changed during transfer', 1;""")
    print('PASS: Equipment transfer RBAC, stale write, history/outbox and retained Incident snapshot')
    maintenance_seed = (root/'tests/database/Maintenance-Behavior.sql').read_text(encoding='utf-8').splitlines()
    parents = [next(line for line in maintenance_seed if line.startswith('INSERT INTO [dbo].['+table+']'))
               for table in ('MaintenanceSchedules','ScheduleOccurrences','WorkOrders','WorkOrderAttempts')]
    sql(f"USE [{names['Maintenance']}];\n"+'\n'.join(parents))
    schedules = '/maintenance/api/maintenance-schedules'
    req(schedules,expected=401)
    req(schedules,token=technician,expected=403)
    req(schedules+'?page=0',token=token,expected=400)
    schedule = req(schedules,token=token)['items'][0]
    assert req(schedules+'?search='+schedule['code']+'&active=true',token=token)['items'][0]['id']==schedule['id']
    assert req(schedules+'?active=false',token=token)['items']==[]
    occurrences = req(schedules+'/'+schedule['id']+'/occurrences',token=token)['items']
    assert len(occurrences)==1 and occurrences[0]['workOrderId']
    req(schedules+'/'+str(uuid4())+'/occurrences',token=token,expected=404)
    print('PASS: Maintenance schedules Admin RBAC, search, active filter and occurrence work-order link')
    from concurrent.futures import ThreadPoolExecutor
    timing = {'anchorAtUtc':'2027-02-01T10:00:00+07:00','intervalDays':14}
    data = {'equipmentId':asset_id,'workType':'Calibration','timing':timing}
    req(schedules,'POST',data,technician,403,idem=str(uuid4()))
    sql(f"""USE [{names['Identity']}];
    DECLARE @id uniqueidentifier = NEWID();
    INSERT dbo.AspNetUsers (Id, UserName, NormalizedUserName, Email, NormalizedEmail, EmailConfirmed, PasswordHash,
        SecurityStamp, ConcurrencyStamp, PhoneNumberConfirmed, TwoFactorEnabled, LockoutEnabled, AccessFailedCount,
        FullName, IsActive, AuthorizationVersion, CreatedAtUtc, UpdatedAtUtc, DepartmentId, DepartmentNameSnapshot)
    SELECT @id,'schedule-staff','SCHEDULE-STAFF','schedule@example.test','SCHEDULE@EXAMPLE.TEST',1,PasswordHash,NEWID(),
        NEWID(),0,0,1,0,'Schedule Staff',1,1,SYSDATETIMEOFFSET(),SYSDATETIMEOFFSET(),'{target_dept['id']}','Target department'
        FROM dbo.AspNetUsers WHERE NormalizedUserName='DEMO-ADMIN';
    INSERT dbo.AspNetUserRoles (UserId, RoleId) SELECT @id,Id FROM dbo.AspNetRoles WHERE Name='DepartmentStaff';""")
    staff = req(auth+'/login','POST',{'userName':'schedule-staff','password':password})['accessToken']
    req(schedules,'POST',data,staff,403,idem=str(uuid4()))
    req(schedules,'POST',data,token,400)
    retired = req(equipment_path,'POST',dict(asset_input,code='RETIRED-DEVICE',profile=dict(profile,serialNumber=None)),token,201)
    sql(f"""USE [{names['Equipment']}]; UPDATE dbo.EquipmentAssets SET IsRetired=1, OperationalStatus='Retired',
        RetiredAtUtc=SYSDATETIMEOFFSET(), RetiredByUserId='{me['id']}' WHERE Id='{retired['id']}';""")
    req(schedules,'POST',dict(data,equipmentId=retired['id']),token,409,idem=str(uuid4()))
    offline_key = str(uuid4())
    equipment_proc.terminate()
    equipment_proc.wait(timeout=12)
    req(schedules,'POST',data,token,503,idem=offline_key)
    ready(5112,start('Equipment',equipment_env))
    first = None
    for _ in range(15):
        first = req(schedules,'POST',data,token,(201,503),idem=offline_key)
        if first is not None:
            break
        time.sleep(1)
    assert first is not None, 'gRPC client did not recover after Equipment restart'
    assert first['timing']['reminderLeadMinutes']==1440
    assert first['timing']['anchorAtUtc']=='2027-02-01T03:00:00+00:00'
    assert req(schedules,'POST',data,token,201,idem=offline_key)==first
    req(schedules,'POST',dict(data,workType='Maintenance'),token,409,idem=offline_key)
    req(schedules+'/'+first['id'],token=technician,expected=403)
    assert req(schedules+'/'+first['id'],token=token)==first
    req(schedules+'/'+str(uuid4()),token=token,expected=404)
    timeout_key = str(uuid4())
    import socketserver
    from threading import Event, Thread
    unblock = Event()
    class SilentEndpoint(socketserver.BaseRequestHandler):
        def handle(self):
            unblock.wait(timeout=10)

    maintenance_proc.terminate()
    maintenance_proc.wait(timeout=12)
    with socketserver.ThreadingTCPServer(('127.0.0.1',0), SilentEndpoint) as endpoint:
        endpoint.daemon_threads = True
        server_thread = Thread(target=endpoint.serve_forever, daemon=True)
        server_thread.start()
        stalled = start('Maintenance',dict(maintenance_env,
            Grpc__EquipmentAddress=f'http://127.0.0.1:{endpoint.server_address[1]}'))
        try:
            ready(5114,stalled)
            start_timeout = time.monotonic()
            req(schedules,'POST',data,token,503,idem=timeout_key)
            assert time.monotonic()-start_timeout >= 2.5
        finally:
            unblock.set()
            endpoint.shutdown()
            stalled.terminate()
            stalled.wait(timeout=12)
    maintenance_proc = start('Maintenance',maintenance_env)
    ready(5114,maintenance_proc)
    assert req(schedules,'POST',data,token,201,idem=timeout_key)['id']
    failure_key = str(uuid4())
    sql(f"""USE [{names['Maintenance']}];
    ALTER TABLE dbo.OutboxMessages WITH NOCHECK ADD CONSTRAINT TestScheduleCommitFailure
        CHECK (EventType <> 'MaintenanceScheduleCreated');""")
    req(schedules,'POST',data,token,500,idem=failure_key)
    sql(f"""USE [{names['Maintenance']}];
    IF NOT EXISTS (SELECT 1 FROM dbo.IdempotencyRequests WHERE IdempotencyKey='{failure_key}' AND State='Pending')
    OR EXISTS (SELECT 1 FROM dbo.MaintenanceSchedules s JOIN dbo.IdempotencyRequests i ON i.OperationId=s.Id
        WHERE i.IdempotencyKey='{failure_key}')
        THROW 50003, 'Expected local rollback and durable Pending', 1;
    ALTER TABLE dbo.OutboxMessages DROP CONSTRAINT TestScheduleCommitFailure;""")
    maintenance_proc.terminate()
    maintenance_proc.wait(timeout=12)
    maintenance_proc = start('Maintenance',maintenance_env)
    ready(5114,maintenance_proc)
    recovered = req(schedules,'POST',data,token,201,idem=failure_key)
    sql(f"""USE [{names['Equipment']}];
    IF (SELECT COUNT(*) FROM dbo.EquipmentUseRegistrations WHERE SourceOperationId='{recovered['id']}' AND ResourceType='MaintenanceSchedule') <> 1
        THROW 50003, 'Retry must reuse remote registration', 1;""")
    parallel_key = str(uuid4())
    with ThreadPoolExecutor(max_workers=2) as pool:
        results = list(pool.map(lambda _: req(schedules,'POST',data,token,201,idem=parallel_key), range(2)))
    assert results[0]==results[1]
    with ThreadPoolExecutor(max_workers=2) as pool:
        distinct = list(pool.map(lambda key: req(schedules,'POST',data,token,201,idem=key), [str(uuid4()), str(uuid4())]))
    assert distinct[0]['id']!=distinct[1]['id']
    sql(f"""USE [{names['Maintenance']}];
    IF (SELECT COUNT(*) FROM dbo.OutboxMessages WHERE AggregateId='{results[0]['id']}') <> 1
    OR (SELECT COUNT(*) FROM dbo.AuditEntries WHERE EntityId='{results[0]['id']}') <> 1
        THROW 50003, 'Expected exactly one create audit and outbox', 1;""")
    schedule_path = schedules+'/'+schedule['id']
    existing = req(schedule_path,token=token)
    revision = {'timing':dict(existing['timing'],isActive=False,intervalDays=7), 'rowVersion':existing['rowVersion']}
    with ThreadPoolExecutor(max_workers=2) as pool:
        writes = list(pool.map(lambda _: req(schedule_path,'PUT',revision,token,(200,409)), range(2)))
    assert len([item for item in writes if item is not None])==1
    edited_schedule = req(schedule_path,token=token)
    assert not edited_schedule['timing']['isActive']
    sql(f"""USE [{names['Maintenance']}];
    IF (SELECT COUNT(*) FROM dbo.OutboxMessages WHERE AggregateId='{schedule['id']}' AND EventType='MaintenanceScheduleDeactivated') <> 1
    OR (SELECT COUNT(*) FROM dbo.AuditEntries WHERE EntityId='{schedule['id']}' AND Action='MaintenanceScheduleDeactivated') <> 1
        THROW 50003, 'Expected one deactivate audit and outbox', 1;""")
    req(schedule_path,'PUT',revision,token,409)
    assert req(schedule_path+'/occurrences',token=token)['items']==occurrences
    from datetime import datetime, timezone
    activated_at = datetime.now(timezone.utc)
    reactivated = req(schedule_path,'PUT',{
        'timing':dict(edited_schedule['timing'],isActive=True),
        'rowVersion':edited_schedule['rowVersion']
    },token)
    assert reactivated['timing']['isActive']
    assert datetime.fromisoformat(reactivated['nextOccurrenceAtUtc'])>=activated_at
    assert req(schedule_path+'/occurrences',token=token)['items']==occurrences
    print('PASS: Schedule D3 creation, fault/restart recovery, concurrency, inactive edit and reactivation preserving occurrences')
    work = '/maintenance/api/work-orders'
    req(work,expected=401)
    req(work+'?status=Invalid',token=token,expected=400)
    orders = req(work,token=token)['items']
    assert len(orders)==1
    assert req(work+'/'+occurrences[0]['workOrderId'],token=token)['id']==orders[0]['id']
    req(work+'/'+str(uuid4()),token=token,expected=404)
    order = orders[0]
    assert req(work+'?search='+order['code']+'&status='+order['status'],token=technician)['items'][0]['id']==order['id']
    assert req(work+'?page=2',token=token)['items']==[]
    assert len(req(work+'/'+order['id']+'/attempts',token=token))==1
    req(work+'/'+str(uuid4())+'/attempts',token=token,expected=404)
    assert '/api/work-orders' in req('/swagger/v1/swagger.json',base='http://localhost:5114')['paths']
    assign = work+'/'+order['id']+'/assignment'
    technician_id = req(auth+'/me',token=technician)['id']
    assignment = {'technicianId':technician_id,'rowVersion':order['rowVersion']}
    req(assign,'PUT',assignment,technician,403)
    req(assign,'PUT',dict(assignment,technicianId=str(uuid4())),token,400)
    # Legacy rows have no verifiable D3 operation and must remain fail-closed.
    req(assign,'PUT',assignment,token,409)
    fresh_asset = req(equipment_path,'POST',dict(asset_input,code='WORKORDER-DEVICE',profile=dict(profile,serialNumber=None)),token,201)
    due_schedule = req(schedules,'POST',{'equipmentId':fresh_asset['id'],'workType':'Calibration',
        'timing':{'anchorAtUtc':datetime.now(timezone.utc).isoformat(),'intervalDays':7,
                  'reminderLeadMinutes':30,'completionWindowMinutes':60,'isActive':True}},token,201,idem=str(uuid4()))
    # Enable production workers only after the legacy fixture assertions above.
    maintenance_proc.terminate()
    maintenance_proc.wait(timeout=12)
    maintenance_proc = start('Maintenance',dict(maintenance_env,Workers__Enabled='true',Workers__WorkOrderGeneration__Enabled='true'))
    ready(5114,maintenance_proc)
    deadline = time.monotonic()+45
    generated = []
    while time.monotonic()<deadline:
        generated = [x for x in req(work,token=token)['items'] if x['equipmentId']==fresh_asset['id']]
        if generated: break
        time.sleep(.5)
    assert len(generated)==1, 'Scheduler did not generate a registered WorkOrder'
    order = generated[0]
    assign = work+'/'+order['id']+'/assignment'
    assignment = {'technicianId':technician_id,'rowVersion':order['rowVersion']}
    req(assign,'PUT',assignment,token,204)
    req(assign,'PUT',assignment,token,409)
    assert req(work+'/'+order['id'],token=token)['assignedTechnicianId']==technician_id
    sql(f"""USE [{names['Maintenance']}];
    IF (SELECT COUNT(*) FROM dbo.WorkOrderActivities WHERE WorkOrderId='{order['id']}' AND Action='Assigned') <> 1
        THROW 50003, 'Assignment activity must be written once', 1;""")
    print('PASS: Maintenance Swagger, search, pagination, history, Admin assignment, stale update and activity audit')
    detail = req(work+'/'+order['id'],token=token)
    req(work+'/'+order['id']+'/start','POST',{'rowVersion':detail['rowVersion']},technician,204)
    detail = req(work+'/'+order['id'],token=token)
    boundary = 'medequip-'+secrets.token_hex(12)
    measurements = [{'code':'P','name':'Pressure','unit':'bar','value':5,'minimum':4,'maximum':6}]
    fields = {'notes':'Calibrated and tested running equipment','result':'Passed','rowVersion':detail['rowVersion'],
              'measurementsJson':json.dumps(measurements)}
    parts = [f'--{boundary}\r\nContent-Disposition: form-data; name="{name}"\r\n\r\n{value}\r\n'.encode() for name,value in fields.items()]
    parts.append(f'--{boundary}\r\nContent-Disposition: form-data; name="photo"; filename="running.png"\r\nContent-Type: image/png\r\n\r\n'.encode()+png+b'\r\n')
    with urllib.request.urlopen(urllib.request.Request('http://localhost:5110'+work+'/'+order['id']+'/attempts',
        data=b''.join(parts)+f'--{boundary}--\r\n'.encode(),headers={'Content-Type':'multipart/form-data; boundary='+boundary,
        'Authorization':'Bearer '+technician}),timeout=8) as response:
        assert response.status==204
    detail = req(work+'/'+order['id'],token=token)
    req(work+'/'+order['id']+'/review','POST',{'rowVersion':detail['rowVersion'],'accept':True},token,204)
    assert req(work+'/'+order['id'],token=token)['status']=='Closed'
    print('PASS: real Scheduler -> D3 WorkOrder -> technician -> calibration measurements/photo -> Admin review Closed')
    recipients_route = '/identity/api/internal/notification-recipients'
    req(recipients_route,'POST',{'technicianId':technician_id},expected=401)
    req(recipients_route,'POST',{'technicianId':technician_id},expected=401,recipient_key='wrong-key')
    resolved = req(recipients_route,'POST',{'technicianId':technician_id},recipient_key=recipient_key)['userIds']
    assert set(resolved)=={me['id'],technician_id}
    assert req(recipients_route,'POST',{},recipient_key=recipient_key)['userIds']==[me['id']]
    sql(f"""USE [{names['Identity']}];
    UPDATE dbo.AspNetUsers SET IsActive=0, AuthorizationVersion=AuthorizationVersion+1 WHERE Id='{technician_id}';""")
    assert req(recipients_route,'POST',{'technicianId':technician_id},recipient_key=recipient_key)['userIds']==[me['id']]
    sql(f"""USE [{names['Identity']}]; UPDATE dbo.AspNetUsers SET IsActive=1 WHERE Id='{technician_id}';""")
    technician = req(auth+'/login','POST',{'userName':'demo-tech','password':password})['accessToken']
    print('PASS: service-authenticated recipient lookup, Admin fallback, assigned active Technician and inactive exclusion')
    notices = '/notification/api/notifications'
    req(notices,expected=401)
    req(notices+'?page=0',token=token,expected=400)
    notice_id = str(uuid4())
    sql(f"""USE [{names['Notification']}];
    INSERT dbo.Notifications (Id, RecipientUserId, SourceEventId, Kind, Title, Body, ResourceType, ResourceId, CreatedAtUtc)
    VALUES ('{notice_id}', '{me['id']}', NEWID(), 'SlaResponseBreached', 'SLA alert', 'Details', 'Incident', NEWID(), SYSDATETIMEOFFSET());""")
    assert req(notices,token=technician)['items']==[]
    req(notices+'/'+notice_id+'/read','PUT',token=technician,expected=404)
    assert req(notices+'?unreadOnly=true',token=token)['items'][0]['id']==notice_id
    req(notices+'/'+notice_id+'/read','PUT',token=token,expected=204)
    assert req(notices+'?unreadOnly=true',token=token)['items']==[]
    read_at = req(notices,token=token)['items'][0]['readAtUtc']
    req(notices+'/'+notice_id+'/read','PUT',token=token,expected=204)
    assert req(notices,token=token)['items'][0]['readAtUtc']==read_at
    assert '/api/notifications' in req('/swagger/v1/swagger.json',base='http://localhost:5116')['paths']
    print('PASS: Notification Swagger, recipient isolation, unread filter and idempotent mark-read')
    for owner in ('equipment','maintenance','incident'):
        report = f'/{owner}/api/reports/{owner}'
        req(report,token=technician,expected=403)
        req(report+'?page=0',token=token,expected=400)
        data = req(report,token=token)
        assert data['items'] and data['dataScope'] and data['generatedAtUtc']
        with urllib.request.urlopen(urllib.request.Request('http://localhost:5110'+report+'?format=csv',
            headers={'Authorization':'Bearer '+token}),timeout=5) as response:
            assert response.headers.get_content_type()=='text/csv' and b'ResponseOutcome' in response.read()
    print('PASS: Admin owner-local reports and CSV export, invalid pagination and Technician denial')
    refreshed=req(auth+'/refresh','POST',{'refreshToken':session['refreshToken']})
    assert refreshed['refreshExpiresAtUtc']==session['refreshExpiresAtUtc']
    req(auth+'/logout','POST',token=refreshed['accessToken'],expected=204)
    req(auth+'/me',token=refreshed['accessToken'],expected=401)
    req(dep,token=refreshed['accessToken'],expected=401)
    req(auth+'/me',token=other['accessToken'])
    req(auth+'/logout?all=true','POST',token=other['accessToken'],expected=204)
    req(auth+'/me',token=other['accessToken'],expected=401)
    print('PASS: Gateway login, current role, department create/list/filter/update/conflict/archive, fixed refresh expiry, logout and isolation between sessions.')
finally:
    for proc in reversed(processes):
        if proc.poll() is None: proc.terminate()
        proc.wait(timeout=12)
    for handle in logs: handle.close()
    for name in created:
        sql(f'ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{name}]')
    for key in keys: key.unlink(missing_ok=True)








