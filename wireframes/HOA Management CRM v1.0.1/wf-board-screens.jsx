// Board-side screens. All metric content comes from the registry in wf-board.jsx.

// ══ 1 · The switch moment ═════════════════════════════════════
// One login. A resident who also serves on the board sees an invitation on
// their own dashboard; taking it swaps the shell into board mode.
function BoardModeSwitch() {
  return (
    <div className="wf">
      <div className="wf-top">
        <div className="wf-logo"><span className="wf-logo-mark"></span>NekoHOA</div>
        <div style={{marginLeft:'auto'}} className="wf-user">
          <span className="wf-btn violet" style={{padding:'5px 12px', fontSize:11.5}}>🗝️ Enter board mode</span>
          <span className="wf-pill">🔔 3</span>
          <span className="wf-avatar">NB</span>
        </div>
      </div>
      <div className="wf-strip">
        <span className="wf-pin"></span><b>Sakura Heights</b><span>· 714 Keystone Park Dr</span>
      </div>
      <div className="wf-body">
        <h1 className="wf-h1">Welcome back, <span className="hand">Nicholas</span></h1>
        <div className="wf-grid-3">
          <div className="wf-card"><div className="wf-field-label">Balance</div><div className="mono" style={{fontSize:24}}>$35.00</div><div className="wf-sub">Due Jun 1</div></div>
          <div className="wf-card"><div className="wf-field-label">Auto-pay</div><div style={{fontSize:18, fontWeight:600}}>On</div><div className="wf-sub">Fidelity •• 747</div></div>
          <div className="wf-card"><div className="wf-field-label">Violations</div><div style={{fontSize:18, fontWeight:600}}>None</div><div className="wf-sub">Last check Apr 2</div></div>
        </div>
        <div className="wf-card dashed" style={{flex:1}}>
          <div className="wf-h2">Recent activity</div>
          <div className="wf-ph" style={{height:120}}>resident dashboard — unchanged</div>
        </div>

        <div style={{display:'flex', gap:10, alignItems:'flex-start'}}>
          <span className="wf-note violet">the way in sits with the other account controls, top-right — nothing added to the page body</span>
        </div>
      </div>
    </div>
  );
}

// Metrics live on Community Home — kept as its own block so the page and the
// glossary-open state share one source.
function MetricsSection() {
  return (
    <>
      <div className="wf-grid-3">
        {heroes().map(m => <HeroStat key={m.id} m={m}/>)}
      </div>
      <div className="wf-card">
        <div style={{display:'flex', alignItems:'center', gap:8, marginBottom:4}}>
          <div className="wf-h2" style={{margin:0}}>Community metrics</div>
          <span className="wf-sub" style={{marginLeft:'auto', fontSize:11}}>8 tracked</span>
        </div>
        <MetricTable rows={bySurface('community')}/>
      </div>
    </>
  );
}

// Architectural items waiting on this board member, pulled from the same list
// the Architectural Applications page renders.
function NeedsYourVote() {
  const pending = ARC_APPS.filter(a => a.mine === null);
  return (
    <div className="wf-card" style={{borderColor:'var(--violet)', background:'var(--lav)'}}>
      <div style={{display:'flex', alignItems:'center', gap:8, marginBottom:10}}>
        <div className="wf-h2" style={{margin:0}}>Needs your vote</div>
        <span className="wf-pill warn" style={{fontSize:10}}>{pending.length} open</span>
        <span className="wf-link" style={{marginLeft:'auto', fontSize:11.5}}>All architectural applications →</span>
      </div>
      <div style={{display:'flex', flexDirection:'column', gap:8}}>
        {pending.map(a => (
          <div key={a.id} style={{display:'flex', alignItems:'center', gap:12, padding:'11px 13px', background:'var(--paper)', border:'1.5px solid var(--line)', borderRadius:11}}>
            <span className="mono" style={{fontSize:11, color:'var(--ink-soft)', width:70}}>{a.id}</span>
            <div style={{flex:1}}>
              <div style={{fontWeight:600, fontSize:12.5}}>{a.project}</div>
              <div className="wf-sub" style={{fontSize:11}}>{a.addr} · {a.owner}</div>
            </div>
            <span className="wf-link" style={{fontSize:11.5}}>📎 {a.files.length}</span>
            <Tally v={a.votes}/>
            <span className="wf-sub" style={{fontSize:11}}>due {a.due}</span>
            <div style={{display:'flex', gap:5}}>
              <span className="wf-btn" style={{padding:'4px 10px', fontSize:11, background:'oklch(0.92 0.06 160)'}}>Approve</span>
              <span className="wf-btn" style={{padding:'4px 10px', fontSize:11}}>Deny</span>
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}

// ══ 2 · Board home ════════════════════════════════════════════
function BoardHome() {
  const work = bySurface('work');
  return (
    <WFShellBoard sideActive="Community Home" role="board" communities={1}>
      <h1 className="wf-h1">Keystone Crossing <span className="hand">at a glance</span></h1>

      <NeedsYourVote/>

      <div style={{display:'grid', gridTemplateColumns:'1.15fr 1fr', gap:14}}>
        <div className="wf-card" style={{padding:0, overflow:'hidden'}}>
          <div style={{display:'flex', alignItems:'center', gap:8, padding:'12px 16px', borderBottom:'1.5px dashed var(--line)'}}>
            <b style={{fontSize:14}}>Community photos</b>
            <span className="wf-sub" style={{marginLeft:'auto', fontSize:11}}>8 photos</span>
          </div>
          <div style={{display:'grid', gridTemplateColumns:'2fr 1fr 1fr', gridTemplateRows:'92px 92px', gap:6, padding:12}}>
            <div className="wf-ph" style={{gridRow:'span 2'}}>entrance</div>
            <div className="wf-ph">pool</div>
            <div className="wf-ph">park</div>
            <div className="wf-ph">clubhouse</div>
            <div className="wf-ph">+5</div>
          </div>
        </div>

        <div className="wf-card">
          <div style={{display:'flex', alignItems:'center', gap:8, marginBottom:10}}>
            <b style={{fontSize:14}}>Community calendar</b>
            <span className="wf-sub" style={{marginLeft:'auto', fontSize:11}}>June 2026</span>
          </div>
          <div style={{display:'grid', gridTemplateColumns:'repeat(7,1fr)', gap:3, fontSize:10.5, textAlign:'center'}}>
            {['S','M','T','W','T','F','S'].map((d,i) => <div key={i} style={{color:'var(--ink-mute)', fontWeight:600, padding:'2px 0'}}>{d}</div>)}
            {Array.from({length:30}, (_, i) => {
              const day = i + 1, ev = [2,11,18,24].includes(day);
              return (
                <div key={day} style={{padding:'5px 0', borderRadius:6, position:'relative',
                  background: ev ? 'var(--pink)' : 'transparent',
                  border: ev ? '1.5px solid var(--rose)' : '1.5px solid transparent', fontWeight: ev ? 600 : 400}}>
                  {day}
                </div>
              );
            })}
          </div>
          <hr className="wf-divider" style={{margin:'12px 0'}}/>
          <div style={{display:'flex', flexDirection:'column', gap:7, fontSize:11.5}}>
            {[['Jun 2','Assessment draft runs'],['Jun 11','Board meeting · 7pm'],['Jun 18','Pool inspection'],['Jun 24','ARC review deadline']].map(([d,t],i)=>(
              <div key={i} style={{display:'flex', gap:8}}>
                <b className="mono" style={{width:46, flexShrink:0, fontSize:11}}>{d}</b><span>{t}</span>
              </div>
            ))}
          </div>
        </div>
      </div>

      <div className="wf-card">
        <div style={{display:'flex', alignItems:'center', gap:8, marginBottom:4}}>
          <div className="wf-h2" style={{margin:0}}>Work Processed — last 30 days</div>
          <span className="wf-sub" style={{marginLeft:'auto', fontSize:11}}>What the management company did for you</span>
        </div>
        <MetricTable rows={work} metricHead="Work area" valueHead="Count" showStatus={false}/>
      </div>

      <MetricsSection/>

      <span className="wf-note" style={{alignSelf:'flex-end'}}>help moved to the right-most column <span className="arrow"></span></span>
    </WFShellBoard>
  );
}

// ══ 3 · Community metrics + overview, glossary open ═══════════
function BoardMetrics() {
  return (
    <WFShellBoard sideActive="Community Home" role="board" communities={1} glossary="over60">
      <span className="wf-sub" style={{fontSize:11, color:'var(--ink-mute)'}}>↑ same page as Community Home, scrolled down</span>
      <MetricsSection/>
      <span className="wf-note violet" style={{alignSelf:'flex-end'}}>every row, hero, and definition ← one registry <span className="arrow"></span></span>
    </WFShellBoard>
  );
}

// ══ 3b · Community overview ═══════════════════════════════════
function BoardOverview() {
  return (
    <WFShellBoard sideActive="Community Home" role="board" communities={1}>
      <h1 className="wf-h1">Community <span className="hand">overview</span></h1>
      <p className="wf-sub" style={{marginTop:-6}}>The association’s record of itself — read-only for the board, editable by the manager.</p>

      <div className="wf-card lav">
        <div style={{display:'grid', gridTemplateColumns:'1fr 1fr', gap:'14px 20px', fontSize:12.5}}>
          {[
            ['Legal name', COMMUNITY.legalName],
            ['Community name', COMMUNITY.name],
            ['County', COMMUNITY.county],
            ['Formation date', COMMUNITY.formed],
            ['Management start date', COMMUNITY.managedSince],
          ].map(([k,v],i)=>(
            <div key={i}><div className="wf-field-label">{k}</div><div style={{fontWeight:500}}>{v}</div></div>
          ))}
          <div><div className="wf-field-label">Community GUID</div><div className="mono" style={{fontSize:11, color:'var(--ink-soft)', wordBreak:'break-all'}}>{COMMUNITY.guid}</div></div>
        </div>
        <hr className="wf-divider" style={{margin:'16px 0'}}/>
        <div className="wf-field-label">Description</div>
        <p style={{margin:'4px 0 0', fontSize:12.5, lineHeight:1.7, textWrap:'pretty', maxWidth:'72ch'}}>{COMMUNITY.description}</p>
      </div>

      <div className="wf-grid-2">
        <div className="wf-card dashed">
          <div className="wf-h2">Sub-associations</div>
          <div style={{display:'flex', flexDirection:'column', gap:8, fontSize:12.5}}>
            {[['Keystone Crossing SF','Single family · 214 homes'],['Keystone Crossing TH','Townhome · 168 homes']].map(([n,d],i)=>(
              <div key={i} style={{display:'flex', alignItems:'center', gap:10, padding:'10px 12px', border:'1.5px solid var(--line)', borderRadius:10}}>
                <span className="wf-pin"></span>
                <div style={{flex:1}}><div style={{fontWeight:500}}>{n}</div><div className="wf-sub" style={{fontSize:11}}>{d}</div></div>
                <span className="wf-link">open</span>
              </div>
            ))}
          </div>
        </div>
        <div className="wf-card dashed">
          <div className="wf-h2">Amenities</div>
          <div style={{display:'flex', gap:8, flexWrap:'wrap'}}>
            {['Pool','Park','Walking trails','Dog run'].map(a => <span key={a} className="wf-pill">{a}</span>)}
          </div>
        </div>
      </div>

      <span className="wf-note violet" style={{alignSelf:'flex-end'}}>Community GUID drives queries; Community Name is the human handle <span className="arrow"></span></span>
    </WFShellBoard>
  );
}

// ══ 4 · Architectural applications ════════════════════════════
// Attachments live in S3; the grid links out rather than embedding them.
const ARC_APPS = [
  { id:'ARC-1042', addr:'711 Keystone Park Dr #29', owner:'Praneeth Pattyam', project:'Fence replacement — 6ft cedar', recd:'05/28/26', due:'06/27/26', votes:{yes:2,no:0,non:3}, mine:null,
    files:[{n:'fence-plan.pdf',s:'1.2 MB'},{n:'elevation.jpg',s:'840 KB'},{n:'plat-survey.pdf',s:'2.1 MB'}] },
  { id:'ARC-1041', addr:'725 Keystone Park Dr',     owner:'Stephanie H Ross', project:'Solar panel array, rear roof', recd:'05/21/26', due:'06/20/26', votes:{yes:3,no:1,non:1}, mine:'yes',
    files:[{n:'solar-layout.pdf',s:'3.4 MB'},{n:'spec-sheet.pdf',s:'660 KB'}] },
  { id:'ARC-1039', addr:'105 Mainline Station',     owner:'Hasan Mehdi',      project:'Exterior repaint — Sage 4021', recd:'05/14/26', due:'06/13/26', votes:{yes:1,no:2,non:2}, mine:'no',
    files:[{n:'color-chip.png',s:'220 KB'}] },
  { id:'ARC-1036', addr:'735 Keystone Park Dr',     owner:'Daniel Koontz',    project:'Detached shed, 10x12',        recd:'05/02/26', due:'06/01/26', votes:{yes:4,no:0,non:1}, mine:'yes',
    files:[] },
];

function Tally({ v }) {
  return (
    <span className="wfb-tally" title={`${v.yes} approve · ${v.no} deny · ${v.non} not voted`}>
      {Array.from({length:v.yes}, (_,i) => <i key={'y'+i} className="yes"></i>)}
      {Array.from({length:v.no},  (_,i) => <i key={'n'+i} className="no"></i>)}
      {Array.from({length:v.non}, (_,i) => <i key={'o'+i} className="non"></i>)}
      <span style={{fontSize:10.5, color:'var(--ink-soft)', marginLeft:4}}>{v.yes}/{v.yes+v.no+v.non}</span>
    </span>
  );
}

function BoardArchApps() {
  return (
    <WFShellBoard sideActive="Architectural Applications" role="board" communities={1}>
      <div style={{display:'flex', alignItems:'baseline', gap:12}}>
        <h1 className="wf-h1">Architectural <span className="hand">applications</span></h1>
        <span className="wf-pill warn" style={{marginLeft:'auto'}}>1 awaiting your vote</span>
      </div>

      <div style={{display:'flex', gap:8, alignItems:'center'}}>
        <div className="wf-btn primary">Open · 4</div>
        <div className="wf-btn ghost">Closed · 27</div>
        <div className="wf-field dashed" style={{marginLeft:'auto', width:200}}>🔍 Search address or owner</div>
      </div>

      <div className="wf-card" style={{padding:0, overflow:'hidden'}}>
        <table className="wf-table">
          <thead><tr>
            <th style={{width:88}}>ID</th><th>Property</th><th>Project</th>
            <th style={{width:112}}>Attachments</th>
            <th style={{width:78}}>Due</th><th style={{width:132}}>Board votes</th><th style={{width:150}}>Your vote</th>
          </tr></thead>
          <tbody>
            {ARC_APPS.map(a => (
              <tr key={a.id}>
                <td className="mono" style={{fontSize:11}}>{a.id}</td>
                <td><div style={{fontWeight:500}}>{a.addr}</div><div className="wf-sub" style={{fontSize:10.5}}>{a.owner}</div></td>
                <td>{a.project}</td>
                <td>
                  {a.files.length ? (
                    <span className="wf-link" title={a.files.map(f => f.n).join(', ')} style={{fontSize:11.5}}>
                      📎 {a.files.length} file{a.files.length > 1 ? 's' : ''}
                    </span>
                  ) : (
                    <span style={{color:'var(--ink-mute)', fontSize:11.5}}>none</span>
                  )}
                </td>
                <td className="mono" style={{fontSize:11}}>{a.due}</td>
                <td><Tally v={a.votes}/></td>
                <td>
                  {a.mine === null ? (
                    <div style={{display:'flex', gap:5}}>
                      <span className="wf-btn" style={{padding:'4px 9px', fontSize:11, background:'oklch(0.92 0.06 160)'}}>Approve</span>
                      <span className="wf-btn" style={{padding:'4px 9px', fontSize:11}}>Deny</span>
                      <span className="wf-btn ghost" style={{padding:'4px 9px', fontSize:11}}>Info</span>
                    </div>
                  ) : (
                    <span className="wf-pill" style={{fontSize:10.5, background: a.mine==='yes' ? 'oklch(0.92 0.06 160)' : 'oklch(0.92 0.07 30)'}}>
                      you voted {a.mine === 'yes' ? 'approve' : 'deny'}
                    </span>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <div className="wf-grid-2">
        <div className="wf-card">
          <div className="wf-h2">ARC-1042 · fence replacement</div>
          <div className="wf-grid-2" style={{gap:10, marginBottom:12}}>
            <div><div className="wf-field-label">Owner</div><b>Praneeth Pattyam</b></div>
            <div><div className="wf-field-label">Received</div><b>05/28/26</b></div>
          </div>
          <div className="wf-field-label">Attachments <span style={{textTransform:'none', letterSpacing:0, fontWeight:400, color:'var(--ink-mute)'}}>· stored in S3, opens in a new tab</span></div>
          <div style={{display:'flex', flexDirection:'column', gap:6, marginTop:6}}>
            {ARC_APPS[0].files.map(f => (
              <div key={f.n} style={{display:'flex', alignItems:'center', gap:10, padding:'8px 11px', border:'1.5px solid var(--line)', borderRadius:10}}>
                <span style={{fontSize:14}}>📄</span>
                <span className="wf-link" style={{flex:1, fontSize:12}}>{f.n}</span>
                <span className="wf-sub" style={{fontSize:10.5}}>{f.s}</span>
                <span className="wf-sub" style={{fontSize:11}}>↗</span>
              </div>
            ))}
          </div>
        </div>
        <div className="wf-card lav">
          <div className="wf-h2">Cast your vote</div>
          <div className="wf-field-label">Comment to the board</div>
          <div className="wf-field dashed" style={{height:64, alignItems:'flex-start'}}>Optional — visible to the board and the manager</div>
          <div style={{display:'flex', gap:8, marginTop:12}}>
            <div className="wf-btn" style={{flex:1, justifyContent:'center', background:'oklch(0.92 0.06 160)'}}>✓ Approve</div>
            <div className="wf-btn" style={{flex:1, justifyContent:'center'}}>✕ Deny</div>
            <div className="wf-btn ghost" style={{flex:1, justifyContent:'center'}}>Request info</div>
          </div>
          <p className="wf-sub" style={{fontSize:11, marginTop:10, lineHeight:1.55}}>
            Three of five votes decide. The manager records the outcome and notifies the owner.
          </p>
        </div>
      </div>
    </WFShellBoard>
  );
}

// ══ 5 · Vendor management, board view ═════════════════════════
const VENDORS = [
  { name:'Bartlett Tree Experts',     type:'General service', approved:true,  coi:'03/14/27', last:'07/26/25', spend:'$8,400' },
  { name:'BrightView Landscapes LLC', type:'General service', approved:false, coi:'11/02/26', last:'08/01/26', spend:'$46,200' },
  { name:'Capital Exteriors & Reno',  type:'General service', approved:true,  coi:'01/30/27', last:'06/08/26', spend:'$12,750' },
  { name:'CT Signs and Graphics',     type:'General service', approved:true,  coi:'09/12/26', last:'10/20/25', spend:'$1,980' },
  { name:'Covenant Pool Care',        type:'General service', approved:true,  coi:'06/30/26', last:'07/31/26', spend:'$21,600', expiring:true },
  { name:'Duke Energy',               type:'Utility',         approved:true,  coi:'—',        last:'07/28/26', spend:'$33,410' },
  { name:'Hatch, Little & Bunn LLP',  type:'Attorney / CPA',  approved:true,  coi:'04/01/27', last:'05/08/26', spend:'$9,120' },
];

function BoardVendors() {
  return (
    <WFShellBoard sideActive="Vendor Management" role="board" communities={1}>
      <div style={{display:'flex', alignItems:'baseline', gap:12}}>
        <h1 className="wf-h1">Vendors</h1>
        <span className="wf-sub">who works for the association, and are they cleared to</span>
      </div>

      <div className="wf-grid-3">
        <div className="wfb-hero"><div className="wfb-hero-lab">Active vendors</div><div className="wfb-hero-val">34</div><div className="wfb-hero-sub">7 shown · filtered</div></div>
        <div className="wfb-hero warn"><div className="wfb-hero-lab">Insurance expiring ≤60d</div><div className="wfb-hero-val">1</div><div className="wfb-hero-sub">Covenant Pool Care</div></div>
        <div className="wfb-hero"><div className="wfb-hero-lab">Awaiting board approval</div><div className="wfb-hero-val">1</div><div className="wfb-hero-sub">BrightView Landscapes</div></div>
      </div>

      <div className="wf-card" style={{padding:0, overflow:'hidden'}}>
        <div style={{display:'flex', alignItems:'center', gap:8, padding:'12px 16px', borderBottom:'1.5px dashed var(--line)'}}>
          <b style={{fontSize:14}}>Vendor roster</b>
          <span className="wf-btn ghost" style={{marginLeft:'auto', padding:'4px 10px', fontSize:11}}>⊞ Show all columns</span>
          <span className="wf-btn ghost" style={{padding:'4px 10px', fontSize:11}}>↓ Export</span>
        </div>
        <table className="wf-table">
          <thead><tr>
            <th>Vendor</th><th style={{width:130}}>Type</th><th style={{width:96}}>Approved</th>
            <th style={{width:110}}>Insurance</th><th style={{width:96}}>Last activity</th><th className="num" style={{width:100}}>YTD spend</th>
          </tr></thead>
          <tbody>
            {VENDORS.map(v => (
              <tr key={v.name}>
                <td style={{fontWeight:500}}>{v.name}</td>
                <td className="wf-sub" style={{fontSize:11.5}}>{v.type}</td>
                <td>{v.approved
                  ? <span style={{color:'oklch(0.55 0.12 160)', fontWeight:600}}>✓</span>
                  : <span className="wf-btn" style={{padding:'3px 9px', fontSize:10.5}}>Review</span>}</td>
                <td>
                  <span className="mono" style={{fontSize:11}}>{v.coi}</span>
                  {v.expiring && <div><span className="wf-pill warn" style={{fontSize:9.5, marginTop:2}}>expiring</span></div>}
                </td>
                <td className="mono" style={{fontSize:11, color:'var(--ink-soft)'}}>{v.last}</td>
                <td className="num">{v.spend}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <span className="wf-note" style={{alignSelf:'flex-end'}}>board sees approval + insurance; managers open the full grid <span className="arrow"></span></span>
    </WFShellBoard>
  );
}

// ══ 6 · Nav variants ══════════════════════════════════════════
function BoardNavMulti() {
  return (
    <WFShellBoard sideActive="My Communities" role="manager" communities={4}>
      <h1 className="wf-h1">My communities</h1>
      <p className="wf-sub" style={{marginTop:-6}}>Appears only when you hold more than one — board member or manager alike.</p>
      <div className="wf-card" style={{padding:0, overflow:'hidden'}}>
        <table className="wf-table">
          <thead><tr><th>Community</th><th style={{width:150}}>Manager</th><th style={{width:130}}>Accountant</th><th style={{width:78}}>Status</th><th style={{width:110}}>Over 30d</th></tr></thead>
          <tbody>
            {[
              ['Keystone Crossing (Master)','Aaliyah Shipp','Aaron Chiles','10%'],
              ['Keystone Crossing SF','Aaliyah Shipp','Aaron Chiles','7%'],
              ['Keystone Crossing TH','Aaliyah Shipp','Aaron Chiles','14%'],
              ['Mainline Station HOA','Devon Pierce','Aaron Chiles','4%'],
            ].map((r,i)=>(
              <tr key={i}>
                <td><span className="wf-link">{r[0]}</span></td>
                <td className="wf-sub" style={{fontSize:11.5}}>{r[1]}</td>
                <td className="wf-sub" style={{fontSize:11.5}}>{r[2]}</td>
                <td><span style={{color:'oklch(0.55 0.12 160)', fontWeight:600}}>✓</span></td>
                <td className="num">{r[3]}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <span className="wf-note violet" style={{alignSelf:'flex-end'}}>role here is Manager — Finance items unlock <span className="arrow"></span></span>
    </WFShellBoard>
  );
}

Object.assign(window, {
  BoardModeSwitch, BoardHome, BoardMetrics, BoardOverview, BoardArchApps, BoardVendors, BoardNavMulti, Tally,
});
