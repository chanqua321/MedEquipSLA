// Optional documentation renderer. Does not connect to SQL Server or run EF.
// From this folder: npm install --no-save @viz-js/viz@3.25.0 sharp@0.35.5
// Then: node render.cjs
const fs = require('fs');
const path = require('path');
const { instance } = require('@viz-js/viz');
const sharp = require('sharp');
const model = JSON.parse(fs.readFileSync(path.join(__dirname, 'erd-model.json'), 'utf8'));
const images = path.join(__dirname, 'images');
fs.mkdirSync(images, { recursive: true });
const escape = s => String(s).replaceAll('&','&amp;').replaceAll('<','&lt;').replaceAll('>','&gt;').replaceAll('"','&quot;');
const colors = ['#315aa1','#1f7b68','#98551b','#774b9c','#a03d62'];
function nodeLabel(t, color, compact=false) {
  let s = `<TABLE BORDER="0" CELLBORDER="1" CELLSPACING="0" CELLPADDING="7" COLOR="#ced8e4"><TR><TD COLSPAN="2" BGCOLOR="${color}"><FONT COLOR="white" POINT-SIZE="16"><B>${escape(t.name)}</B></FONT></TD></TR>`;
  const columns=t.columns.filter(c => t.show.includes(c.name));
  // Explicit minimum widths avoid Graphviz/WASM versus system-font metric differences.
  const nameWidth=Math.max(210,...columns.map(c => (c.name.length+(c.key?c.key.length+3:0))*8.2+25));
  const typeWidth=Math.max(150,...columns.map(c => (c.type.length+(c.nullable?2:0))*6.8+22));
  for(const c of columns) {
    const fill=c.key.includes('REF')?'#fff3dd':c.key.includes('PK')?'#edf2ff':c.key.includes('FK')?'#e9f7f1':'#ffffff';
    const keys=c.key?`<FONT COLOR="#4c5c72"> [${escape(c.key)}]</FONT>`:'';
    s+=`<TR><TD WIDTH="${Math.ceil(nameWidth)}" ALIGN="LEFT" BGCOLOR="${fill}"><FONT POINT-SIZE="12">${escape(c.name)}${keys}</FONT></TD><TD WIDTH="${Math.ceil(typeWidth)}" ALIGN="LEFT" BGCOLOR="${fill}"><FONT COLOR="#607086" POINT-SIZE="11">${escape(c.type)}${c.nullable?' ?':''}</FONT></TD></TR>`;
  }
  return s+'</TABLE>';
}
function subgraph(schema,index) {
  let s=`subgraph cluster_${schema.service} { label="${schema.database} | APPROVED v0.1"; fontname="Segoe UI"; fontsize=22; fontcolor="${colors[index]}"; style="rounded"; color="#bacbdc"; margin=24; `;
  for(const t of schema.tables) s+=`${schema.service}_${t.name} [label=<${nodeLabel(t,colors[index])}>];\n`;
  for(const t of schema.tables) for(const r of t.relations) {
    const tail=r.optional?'0..1':'1';const head=r.one?'0..1':'0..N';
    s+=`${schema.service}_${r.parent} -> ${schema.service}_${t.name} [dir=both, arrowtail="${r.optional?'teeodot':'teetee'}", arrowhead="${r.one?'teeodot':'crowodot'}", taillabel="${tail}", headlabel="${head}", label="${r.childColumns.join(', ')}", fontsize=10, color="#657b90", fontcolor="#506579", labeldistance=2.0];\n`;
  }
  const tech=model.technicalOwnership[schema.service].join('\\n');
  s+=`${schema.service}_technical [shape=box, style="rounded,filled", fillcolor="#f1f5f9", color="#c9d5df", label="Owned technical tables (separate local copies)\\n${tech}\\nSee TECHNICAL-TABLES.md for all columns"];\n`;
  s+='}\n'; return s;
}
const prefix='digraph ERD { graph [rankdir=TB, bgcolor="white", pad=0.5, nodesep=0.55, ranksep=0.85, splines=polyline, fontname="Segoe UI", labelloc=t, fontsize=22]; node [shape=plain, fontname="Segoe UI"]; edge [fontname="Segoe UI", arrowsize=0.7, penwidth=1.3];\n';
async function output(viz, filename, dot, maxWidth=3000) {
  fs.writeFileSync(path.join(images,filename+'.dot'),dot);
  const svg=viz.renderString(dot,{format:'svg',engine:'dot'});
  fs.writeFileSync(path.join(images,filename+'.svg'),svg);
  await sharp(Buffer.from(svg),{density:120,limitInputPixels:200000000}).resize({width:maxWidth,withoutEnlargement:true}).png().toFile(path.join(images,filename+'.png'));
}
function summarySvg() {
  const w=1800,h=1290;
  let s=`<svg xmlns="http://www.w3.org/2000/svg" width="${w}" height="${h}"><style>text{font-family:Segoe UI,Arial,sans-serif}</style><rect width="100%" height="100%" fill="#f5f8fc"/>`;
  const text=(x,y,v,size=21,color='#20364e',weight=400)=>`<text x="${x}" y="${y}" font-size="${size}" fill="${color}" font-weight="${weight}">${escape(v)}</text>`;
  s+=text(70,65,'MedEquipSLA | Database ERD review',36,'#17324d',700);
  s+=text(70,105,'SQL Server Code First • 5 owned databases • Approved v0.1 • Code First schema created',21,'#526981');
  s+=text(70,142,'Solid relationship lines in detailed ERDs = local FK. REF fields = logical IDs across services, never cross-database FK.',17,'#526981');
  const rows=[
    ['Identity','Users 1 — N UserRoles N — 1 Roles','Users 1 — N RefreshTokens','Identity claims / logins / token store','AuthorizationVersion + DepartmentId (REF)'],
    ['Equipment','Departments 1 — N EquipmentAssets','Categories 1 — N EquipmentAssets','Equipment 1 — N StateProjections / UseRegistrations','Status history + reconciliation checkpoint'],
    ['Maintenance','Schedule 1 — N Occurrences','Occurrence 0..1 — 0..1 WorkOrder','WorkOrder 1 — N Attempts 1 — N Measurements','Occurrence reminders + work activity history'],
    ['Incident','Policy 1 — N PolicyVersions 1 — N Incidents','Incident 1 — N ResolutionAttempts','Incident 1 — N Activities / SLA deadline alerts','Immutable SLA snapshot + final outcome'],
    ['Notification','Notification 1 — 0..1 EmailDelivery','EmailDelivery 1 — N DeliveryAttempts','Preferences by UserId (REF) + notification kind','Recipient scope + durable notification storage']
  ];
  for(let i=0;i<rows.length;i++) {
    const x=i%2===0?70:930;const y=190+Math.floor(i/2)*320;const width=800,height=280;
    s+=`<rect x="${x}" y="${y}" width="${width}" height="${height}" rx="16" fill="#fff" stroke="#d3deea"/><rect x="${x}" y="${y}" width="${width}" height="56" rx="16" fill="${colors[i]}"/>`;
    const schema=model.schemas[i];
    s+=text(x+24,y+37,schema.database,24,'#fff',650);
    for(let j=1;j<rows[i].length;j++)s+=text(x+24,y+92+(j-1)*34,rows[i][j],19);
    s+=text(x+24,y+250,`${schema.tables.length} business / Identity tables + ${model.technicalOwnership[schema.service].length} owned technical tables`,17,'#61758c');
  }
  s+=`<rect x="930" y="830" width="800" height="280" rx="16" fill="#fff4d8" stroke="#e8cc83"/>`;
  s+=text(954,873,'Approved decisions D1–D4',24,'#84530c',700);
  ['D1  SLA: time of the final accepted resolution attempt','D2  Remind by occurrence before WorkOrder creation','D3  Durable use registration + reconciliation','D4  Failed calibration keeps equipment blocked','Entities / migrations / databases created; workflow code pending'].forEach((v,i)=>s+=text(954,920+i*35,v,18,'#704d19'));
  s+=text(70,1180,'32 business / Identity tables + 15 service-owned technical tables = 47 application tables approved.',22,'#17324d',650);
  s+=text(70,1220,'Full keys, nullability and constraints: ERD-REVIEW.md + services/*.md. EF migrations history is excluded from this count.',18,'#526981');
  return s+'</svg>';
}
(async()=>{
  const viz=await instance();
  for(let i=0;i<model.schemas.length;i++) {
    const schema=model.schemas[i];
    await output(viz,schema.service+'-ERD',prefix+'label="'+schema.database+' — APPROVED / local FK only";\n'+subgraph(schema,i)+'}\n');
  }
  const technicalNodes=model.technicalTables.map(t=>`${t.name} [label=<${nodeLabel(t,'#536b83')}>];`).join('\n');
  await output(viz,'TECHNICAL-TABLES-ERD',prefix+'label="Technical table definitions — separate copies per owner, NOT a shared database";\n'+technicalNodes+'}\n',5000);
  await output(viz,'ALL-SERVICES-ERD',prefix+'label="MedEquipSLA — 5 separate databases / APPROVED v0.1";\n'+model.schemas.map(subgraph).join('\n')+'}\n',6500);
  const overview=summarySvg();fs.writeFileSync(path.join(images,'ERD-OVERVIEW.svg'),overview);
  await sharp(Buffer.from(overview)).png().toFile(path.join(images,'ERD-OVERVIEW.png'));
  console.log('Rendered five ERDs, all-services SVG/PNG and approval overview. No SQL or EF commands executed.');
})().catch(error=>{console.error(error);process.exitCode=1;});
