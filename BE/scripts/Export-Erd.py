"""Export editable draw.io from the approved, owner-specific database dictionary.

Full table cards and isolated FK panels share a page per database. FK panels repeat
only the referenced columns, so every connector has its own lane and cannot merge
with another connector. Run with Python 3; --preview requires Pillow for geometry QA.
"""
from pathlib import Path
import argparse
import copy
import html
import json
import re
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parent.parent
MODEL = json.loads((ROOT / 'docs/database/erd-model.json').read_text(encoding='utf-8'))
OUT = ROOT / 'output/handoff/MedEquipSLA_ERD.drawio'
W, RH, HH, GAP = 850, 36, 48, 120
COLORS = ['#DFEAF6', '#DDF1E6', '#FFF0DA', '#EEE4F5', '#F7E2EA']
file = ET.Element('mxfile', host='app.diagrams.net', type='device', agent='MedEquipSLA')
pages = []
counts = dict(tables=0, columns=0, foreignKeys=0)


def page(name):
    diagram = ET.SubElement(file, 'diagram', id=re.sub(r'\W', '', name), name=name)
    graph = ET.SubElement(diagram, 'mxGraphModel', grid='1', gridSize='10', page='0',
                          connect='1', arrows='1', fold='0', shadow='0')
    root = ET.SubElement(graph, 'root')
    ET.SubElement(root, 'mxCell', id='0')
    ET.SubElement(root, 'mxCell', id='1', parent='0')
    p = dict(name=name, root=root, graph=graph, rectangles=[], text=[], edges=[])
    pages.append(p)
    return p


def cell(p, ident, value, x, y, w, h, style, parent='1'):
    c = ET.SubElement(p['root'], 'mxCell', id=ident, value=value, style=style,
                      vertex='1', parent=parent)
    ET.SubElement(c, 'mxGeometry', x=str(x), y=str(y), width=str(w), height=str(h), **{'as': 'geometry'})
    return c


def note(p, ident, value, x, y, w, h, size=20):
    cell(p, ident, html.escape(value).replace('\n', '<br/>'), x, y, w, h,
         f'text;html=1;whiteSpace=wrap;align=left;verticalAlign=top;fontFamily=Arial;fontSize={size};spacing=12;')
    p['text'].append((x+12, y+12, value, size))


def table(p, t, ident, x, y, color, title=None, full=False):
    columns = t['columns']
    h = HH + RH*len(columns)
    cell(p, ident, title or t['name'], x, y, W, h,
         f'swimlane;html=0;startSize={HH};container=1;collapsible=0;recursiveResize=0;connectable=0;'
         f'fillColor={color};swimlaneFillColor=#FFFFFF;strokeColor=#879BAA;fontFamily=Arial;'
         'fontSize=21;fontStyle=1;align=left;spacingLeft=12;')
    p['rectangles'].append((x,y,W,h))
    p['text'].append((x+12,y+12,title or t['name'],21))
    anchors = {}
    for i,col in enumerate(columns):
        ident_row = ident+'__'+col['name']
        value = f'{col["key"] or "—"}  {col["name"]} : {col["type"]}  '+('NULL' if col['nullable'] else 'NOT NULL')
        bg = '#FFF3DA' if 'REF' in col['key'] else '#E6F3EC' if 'FK' in col['key'] else '#F0F4FA' if 'PK' in col['key'] else '#FFFFFF'
        cell(p, ident_row, value, 0, HH+i*RH, W, RH,
             f'text;html=0;whiteSpace=wrap;align=left;verticalAlign=middle;fontFamily=Arial;fontSize=15;'
             f'spacingLeft=12;strokeColor=#DCE4EC;fillColor={bg};', ident)
        anchors[col['name']] = (ident_row, y+HH+(i+.5)*RH)
        p['text'].append((x+12,y+HH+i*RH+10,value,15))
    if full:
        counts['tables'] += 1
        if t['name'] != '__EFMigrationsHistory': counts['columns'] += len(columns)
    return h, anchors


overview = page('00 Đọc ERD')
note(overview, 'title', 'MedEquipSLA — ERD vật lý SQL Server\nCập nhật schema 07/11/2026', 50,30,2600,130,30)
note(overview, 'guide', 'Mỗi tab service chứa toàn bộ bảng/cột phía trên và từng quan hệ FK ở các panel riêng phía dưới. '
     'Panel lặp lại đúng các cột tham gia khóa để đọc quan hệ; không phải bảng mới. '
     'Mỗi FK có một connector độc lập, neo trực tiếp vào dòng khóa; không connector nào nối vào connector khác.\n'
     'PK: khóa chính. FK: khóa ngoại nội bộ. UK: unique/filtered index (xem dictionary SQL). '
     'REF: ID logic service khác, không phải SQL FK. Vạch = một, vòng tròn = tùy chọn, chân quạ = nhiều. '
     'FK ghép được liệt kê đầy đủ trong tên panel. Tất cả DeleteBehavior.NoAction.\n'
     'Nguồn: docs/database/erd-model.json, EF configurations/migrations và SQL schema tests. '
     '54 bảng ứng dụng + 5 EF history. Schema tests so sánh nullability, kiểu SQL, FK, index/check và rowversion. '
     'Database ứng dụng vẫn InitialCreate; schema mới đã chạy trên database test SQL Server 2022. '
     'ERD không tự apply database.',50,190,2600,360,22)

base_tech = {t['name']:t for t in MODEL['technicalTables']}
for index,schema in enumerate(MODEL['schemas']):
    owner = schema['service']
    overrides = MODEL.get('technicalTableOverrides',{}).get(owner,{})
    tables = copy.deepcopy(schema['tables']) + [copy.deepcopy(overrides.get(n,base_tech[n])) for n in MODEL['technicalOwnership'][owner]]
    history = dict(name='__EFMigrationsHistory',columns=[dict(name='MigrationId',type='nvarchar(150)',key='PK',nullable=False),dict(name='ProductVersion',type='nvarchar(32)',key='',nullable=False)],relations=[])
    table_map = {t['name']:t for t in tables}
    relations = [(t,r) for t in tables for r in t.get('relations',[])]
    note(overview,'owner_'+owner, f'{owner} — {schema["database"]}\n{len(tables)} bảng ứng dụng; {len(relations)} FK nội bộ; 1 EF history',
         50,600+index*140,2600,120,23)
    p=page(owner)
    note(p,owner+'_title', f'{schema["database"]}\n{len(tables)} bảng ứng dụng + 1 EF history; {len(relations)} FK. '
         'Các bảng đầy đủ cột ở phần A; quan hệ khóa ở phần B.',50,25,2900,140,24)
    y=200
    for start in range(0,len(tables)+1,3):
        heights=[]
        for col,t in enumerate((tables+[history])[start:start+3]):
            h,_=table(p,t,owner+'_full_'+t['name'],50+col*(W+GAP),y,COLORS[index],full=True)
            heights.append(h)
        y+=max(heights)+120
    note(p,owner+'_fk_title','B. Quan hệ FK — mỗi panel có đường nối riêng\nBản rút gọn chỉ hiển thị cột tham gia khóa. Không có FK xuyên database.',50,y,2900,130,24)
    y+=170
    for j,(child,rel) in enumerate(relations,1):
        parent=table_map[rel['parent']]
        parent_cols=[next(c for c in parent['columns'] if c['name']==n) for n in rel['parentColumns']]
        child_cols=[next(c for c in child['columns'] if c['name']==n) for n in rel['childColumns']]
        tag=f'{owner}_FK{j:02}'
        label=f'FK{j:02}: {parent["name"]} ({", ".join(rel["parentColumns"])}) → {child["name"]} ({", ".join(rel["childColumns"])}) | NO ACTION'
        note(p,tag+'_definition',label,50,y,2900,80,19)
        y+=95
        ph,pa=table(p,dict(name=parent['name'],columns=parent_cols),tag+'_parent',50,y,COLORS[index],title=parent['name']+' [cha]')
        ch,ca=table(p,dict(name=child['name'],columns=child_cols),tag+'_child',1900,y,COLORS[index],title=child['name']+' [con]')
        source,sy=pa[rel['parentColumns'][0]];target,ty=ca[rel['childColumns'][0]]
        assert sy==ty
        edge=ET.SubElement(p['root'],'mxCell',id=tag,value='0..1' if rel['one'] else '0..N',edge='1',parent='1',source=source,target=target,
            style='edgeStyle=none;noEdgeStyle=1;orthogonal=1;html=0;rounded=0;strokeColor=#325C7D;strokeWidth=2;'
            f'startArrow={"ERzeroToOne" if rel["optional"] else "ERone"};endArrow={"ERzeroToOne" if rel["one"] else "ERzeroToMany"};'
            'exitX=1;exitY=0.5;exitPerimeter=0;entryX=0;entryY=0.5;entryPerimeter=0;fontSize=18;labelBackgroundColor=#FFFFFF;')
        ET.SubElement(edge,'mxGeometry',relative='1',**{'as':'geometry'})
        p['edges'].append(((50+W,sy),(1900,ty)))
        counts['foreignKeys']+=1
        y+=max(ph,ch)+140
    p['graph'].set('pageWidth','3000');p['graph'].set('pageHeight',str(y+100))


def validate():
    for p in pages:
        ids={c.get('id'):c for c in p['root']}
        assert len(ids)==len(p['root'])
        for c in p['root']:
            if c.get('edge')=='1':
                assert ids[c.get('source')].get('vertex')=='1' and ids[c.get('target')].get('vertex')=='1'
        for i,(a,b) in enumerate(p['edges']):
            assert a[1]==b[1] and a[0]<b[0]
            assert all(other[0][1]!=a[1] for other in p['edges'][i+1:])
            for x,y,w,h in p['rectangles']:
                assert not (y<a[1]<y+h and max(x,a[0])<min(x+w,b[0]))
    assert counts['tables']==59
    assert counts['foreignKeys']==sum(len(t.get('relations',[])) for s in MODEL['schemas'] for t in
        s['tables']+[MODEL.get('technicalTableOverrides',{}).get(s['service'],{}).get(n,base_tech[n]) for n in MODEL['technicalOwnership'][s['service']]])


def previews():
    from PIL import Image, ImageDraw, ImageFont
    qa=ROOT/'.tools/drawio-qa-current';qa.mkdir(exist_ok=True)
    for p in pages:
        # Split very tall canvases into independent, readable tiles; no image pixel limit workaround.
        ymax=max(y+100 for x,y,text,size in p['text'])
        for start in range(0,int(ymax)+1,2400):
            im=Image.new('RGB',(3000,2400),'white');d=ImageDraw.Draw(im)
            for x,y,w,h in p['rectangles']:
                if y+h>=start and y<start+2400:d.rectangle((x,y-start,x+w,y+h-start),outline='#879BAA',width=2)
            for a,b in p['edges']:
                if start<=a[1]<start+2400:
                    d.line((a[0],a[1]-start,b[0],b[1]-start),fill='#325C7D',width=3)
                    for dy in [-10,0,10]:d.line((b[0]-25,b[1]-start,b[0],b[1]-start+dy),fill='#325C7D',width=2)
            for x,y,text,size in p['text']:
                if start<=y<start+2400:
                    d.multiline_text((x,y-start),text,font=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',size),fill='#172B3A',spacing=8)
            im.thumbnail((1800,1440));im.save(qa/f'{p["name"]}-{start//2400+1}.png')
    (qa/'validation.json').write_text(json.dumps(counts,indent=2),encoding='utf-8')


if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--preview',action='store_true');args=parser.parse_args()
    validate()
    OUT.parent.mkdir(parents=True,exist_ok=True)
    ET.indent(file)
    ET.ElementTree(file).write(OUT,encoding='utf-8',xml_declaration=True)
    if args.preview:previews()
    print(json.dumps(counts));print(OUT)
