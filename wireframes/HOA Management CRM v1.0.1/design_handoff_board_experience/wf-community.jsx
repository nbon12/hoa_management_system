// Documents + Announcements + Calendar wireframes

function DocumentsA() {
  const docs = [
    ['ACC Form RM-Update.pdf', 'Forms', '04/01/26', '142 KB'],
    ['Sakura 2024-2025 Insurance.pdf', 'Insurance', '07/07/24', '1.2 MB'],
    ['Sakura Crossing Budget.pdf', 'Budgets', '01/01/24', '380 KB'],
    ['Pool rules & hours.pdf', 'Rules', '04/15/26', '95 KB'],
    ['Annual meeting minutes.pdf', 'Minutes', '03/12/26', '210 KB'],
    ['CC&R restated.pdf', 'Governing', '06/01/22', '2.8 MB'],
    ['Architectural guidelines.pdf', 'Rules', '02/01/25', '420 KB'],
    ['Reserve study summary.pdf', 'Financials', '11/01/25', '710 KB'],
  ];
  return (
    <WFShell active="Documents">
      <div style={{display:'flex', alignItems:'baseline'}}>
        <h1 className="wf-h1">Documents <span className="hand">library</span></h1>
        <div style={{marginLeft:'auto', display:'flex', gap:8}}>
          <div className="wf-field dashed" style={{width:200}}>🔍 Search documents…</div>
          <div className="wf-btn ghost">+ Filter</div>
        </div>
      </div>

      <div style={{display:'flex', gap:6, flexWrap:'wrap'}}>
        {['All · 24', 'Forms · 4', 'Insurance · 2', 'Budgets · 3', 'Rules · 5', 'Minutes · 6', 'Governing · 2', 'Financials · 2'].map((c,i) =>
          <span key={c} className={`wf-tab ${i === 0 ? 'is-active' : ''}`} style={{fontSize:11}}>{c}</span>
        )}
      </div>

      <div className="wf-card" style={{padding:0}}>
        <table className="wf-table">
          <thead><tr><th>Name</th><th>Type</th><th>Effective</th><th className="num">Size</th><th></th></tr></thead>
          <tbody>
            {docs.map((d,i) => (
              <tr key={i}>
                <td style={{display:'flex', alignItems:'center', gap:10}}>
                  <div className="wf-ph" style={{width:24, height:30, borderRadius:4, fontSize:8}}>PDF</div>
                  <span className="wf-link">{d[0]}</span>
                </td>
                <td><span className="wf-pill" style={{background:'var(--lav-2)'}}>{d[1]}</span></td>
                <td>{d[2]}</td>
                <td className="num">{d[3]}</td>
                <td><span className="wf-link">⬇</span></td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <span className="wf-note" style={{alignSelf:'flex-end'}}>category chips + table <span className="arrow"></span></span>
    </WFShell>
  );
}

function DocumentsB() {
  const groups = [
    {title: '⭐ Pinned', items: [['Pool rules · 2026', 'Apr 15'], ['Annual meeting minutes', 'Mar 12']]},
    {title: '📋 Forms', items: [['ACC submittal', 'Apr 1'], ['Maintenance request', 'Jan 8']]},
    {title: '📊 Financials', items: [['2026 budget', 'Jan 1'], ['Reserve study', 'Nov 1'], ['Audit · FY24', 'Aug 14']]},
    {title: '📜 Governing', items: [['CC&R', 'Jun 1 \'22'], ['Bylaws', 'Jun 1 \'22'], ['Architectural', 'Feb 1']]},
  ];
  return (
    <WFShellSide sideActive="Documents">
      <div style={{display:'flex', alignItems:'baseline'}}>
        <h1 className="wf-h1">Documents</h1>
        <div style={{marginLeft:'auto', display:'flex', gap:8}}>
          <div className="wf-field dashed" style={{width:200}}>🔍 Search…</div>
          <div className="wf-btn">⬆ Upload</div>
        </div>
      </div>

      <div className="wf-grid-2">
        {groups.map((g,i) => (
          <div key={i} className="wf-card">
            <div style={{display:'flex', alignItems:'baseline'}}>
              <div className="wf-h2" style={{margin:0}}>{g.title}</div>
              <span className="wf-link" style={{marginLeft:'auto', fontSize:11}}>see all →</span>
            </div>
            <div style={{display:'flex', flexDirection:'column', gap:8, marginTop:10}}>
              {g.items.map(([n,d],j) => (
                <div key={j} style={{display:'flex', gap:10, padding:'8px 10px', border:'1.5px dashed var(--line)', borderRadius:10, alignItems:'center'}}>
                  <div className="wf-ph" style={{width:28, height:34, borderRadius:4, fontSize:8}}>PDF</div>
                  <div style={{flex:1}}>
                    <div style={{fontSize:12, fontWeight:500}}>{n}</div>
                    <div className="wf-sub" style={{fontSize:11}}>{d}</div>
                  </div>
                  <span className="wf-link">⬇</span>
                </div>
              ))}
            </div>
          </div>
        ))}
      </div>
      <span className="wf-note violet" style={{alignSelf:'flex-end'}}>card grid · grouped <span className="arrow"></span></span>
    </WFShellSide>
  );
}

// ── Announcements ────────────────────────────────────────────
function AnnouncementsA() {
  const items = [
    {tag:'NEW', date:'05/04/26', t:'Community pool opening!', body:'The pool reopens Saturday May 24th at 10am. New seasonal hours posted in Documents.', pinned:true},
    {tag:'',    date:'04/28/26', t:'Landscape walkthrough this Friday', body:'Maintenance crew will be on common areas. Please move trash bins after pickup.'},
    {tag:'',    date:'04/12/26', t:'Reminder · ACC submission deadline', body:'Architectural change requests for May review are due April 30.'},
    {tag:'',    date:'03/18/26', t:'Annual meeting recap', body:'Board reviewed FY25 financials and elected new treasurer. Minutes in Documents.'},
  ];
  return (
    <WFShell active="Community">
      <div style={{display:'flex', alignItems:'baseline'}}>
        <h1 className="wf-h1">Announcements</h1>
        <div style={{marginLeft:'auto', display:'flex', gap:8}}>
          <div className="wf-btn ghost">📧 Subscribe</div>
        </div>
      </div>

      <div className="wf-grid-2">
        <div className="wf-col" style={{flex:2, gridColumn:'span 2'}}>
          {items.map((a,i) => (
            <div key={i} className={`wf-card ${a.pinned ? 'pink' : ''}`}>
              <div style={{display:'flex', alignItems:'center', gap:8}}>
                {a.pinned && <span className="wf-pill warn">📌 pinned</span>}
                {a.tag && <span className="wf-pill">{a.tag}</span>}
                <span className="wf-sub" style={{marginLeft:'auto', fontSize:11}}>{a.date}</span>
              </div>
              <div style={{fontSize:16, fontWeight:600, marginTop:6}}>{a.t}</div>
              <p className="wf-sub" style={{margin:'6px 0 0', fontSize:12}}>{a.body}</p>
              <div style={{display:'flex', gap:14, marginTop:10, fontSize:11}}>
                <span className="wf-link">Read more</span>
                <span className="wf-sub">·</span>
                <span className="wf-link">💬 4 comments</span>
              </div>
            </div>
          ))}
        </div>
      </div>
    </WFShell>
  );
}

function AnnouncementsB() {
  return (
    <WFShellSide sideActive="Announcements">
      <h1 className="wf-h1">Community feed</h1>

      <div style={{display:'flex', gap:6}}>
        <span className="wf-tab is-active">All</span>
        <span className="wf-tab">Board</span>
        <span className="wf-tab">Maintenance</span>
        <span className="wf-tab">Events</span>
        <span className="wf-tab">Emergencies</span>
      </div>

      <div className="wf-grid-2">
        <div className="wf-col">
          <div className="wf-card pink">
            <span className="wf-pill warn">📌 pinned</span>
            <div style={{fontSize:18, fontWeight:600, marginTop:8}}>Pool reopens May 24</div>
            <p className="wf-sub">Saturday at 10am. New seasonal hours posted in Documents.</p>
            <div className="wf-ph" style={{height:120, marginTop:8}}>image · pool</div>
            <div style={{display:'flex', gap:14, marginTop:10, fontSize:11}}>
              <span className="wf-link">💬 12</span>
              <span className="wf-link">❤ 38</span>
              <span className="wf-link" style={{marginLeft:'auto'}}>Add to calendar</span>
            </div>
          </div>

          <div className="wf-card">
            <div style={{display:'flex', gap:10, alignItems:'center'}}>
              <div className="wf-avatar">BM</div>
              <div>
                <div style={{fontSize:13, fontWeight:500}}>Board · Mary R.</div>
                <div className="wf-sub" style={{fontSize:11}}>Apr 28 · 4:12pm</div>
              </div>
            </div>
            <div style={{fontWeight:600, marginTop:8}}>Landscape walkthrough Friday</div>
            <p className="wf-sub" style={{fontSize:12}}>Crew on common areas. Please move bins after pickup.</p>
          </div>
        </div>

        <div className="wf-col">
          <div className="wf-card">
            <div style={{display:'flex', gap:10, alignItems:'center'}}>
              <div className="wf-avatar">BM</div>
              <div>
                <div style={{fontSize:13, fontWeight:500}}>Board · Treasurer</div>
                <div className="wf-sub" style={{fontSize:11}}>Apr 12 · 9:00am</div>
              </div>
            </div>
            <div style={{fontWeight:600, marginTop:8}}>ACC deadline Apr 30</div>
            <p className="wf-sub" style={{fontSize:12}}>Architectural change requests for May review.</p>
          </div>

          <div className="wf-card lav">
            <div className="wf-h2">📊 Quick poll</div>
            <p style={{fontSize:13, fontWeight:500, marginTop:4}}>Should we extend pool hours to 9pm?</p>
            <div style={{display:'flex', flexDirection:'column', gap:6, marginTop:8, fontSize:12}}>
              {[
                ['Yes, all summer', 62],
                ['Weekends only', 24],
                ['Keep current', 14],
              ].map(([t,p],i) => (
                <div key={i} style={{position:'relative', padding:'8px 10px', border:'1.5px solid var(--line)', borderRadius:8, overflow:'hidden'}}>
                  <div style={{position:'absolute', inset:0, width:`${p}%`, background:'var(--pink)'}}></div>
                  <div style={{position:'relative', display:'flex'}}>
                    <span>{t}</span>
                    <span style={{marginLeft:'auto', fontFamily:'Geist Mono'}}>{p}%</span>
                  </div>
                </div>
              ))}
            </div>
            <div className="wf-sub" style={{fontSize:11, marginTop:6}}>132 votes · closes Friday</div>
          </div>
        </div>
      </div>
      <span className="wf-note violet" style={{alignSelf:'flex-end'}}>feed-style · with poll <span className="arrow"></span></span>
    </WFShellSide>
  );
}

// ── HOA Calendar (new feature) ────────────────────────────────
function CalendarA() {
  const days = [
    [27,'',[],true],[28,'',[],true],[29,'',[],true],[30,'',[],true],[1,'',[]],[2,'',[]],[3,'',[]],
    [4,'',[]],[5,'',[]],[6,'',[]],[7,'',[]],[8,'',[]],[9,'',[]],[10,'',[]],
    [11,'',[]],[12,'',[]],[13,'',[]],[14,'',[]],[15,'',[]],[16,'',[]],[17,'',[]],
    [18,'',[]],[19,'',[]],[20,'',[{t:'Board mtg', c:'violet'}]],[21,'',[]],[22,'',[]],[23,'',[]],[24,'',[{t:'🏊 Pool opens', c:'pink'}]],
    [25,'',[]],[26,'',[]],[27,'today',[]],[28,'',[]],[29,'',[]],[30,'',[]],[31,'',[]],
  ];
  const upcoming = [
    {d:'May 20', t:'Board meeting', loc:'Clubhouse · 7pm', c:'Board'},
    {d:'May 24', t:'🏊 Pool opens', loc:'Sakura pool · 10am', c:'Amenity'},
    {d:'Jun 14', t:'Community potluck', loc:'Park pavilion · 5pm', c:'Social'},
    {d:'Sep 8',  t:'Pool closes for season', loc:'Sakura pool', c:'Amenity'},
    {d:'Oct 21', t:'Annual board mtg', loc:'Clubhouse · 7pm', c:'Board'},
  ];
  return (
    <WFShell active="Calendar">
      <div style={{display:'flex', alignItems:'baseline'}}>
        <h1 className="wf-h1">Community <span className="hand">calendar</span></h1>
        <div style={{marginLeft:'auto', display:'flex', gap:8, alignItems:'center'}}>
          <div className="wf-btn ghost">‹</div>
          <b style={{minWidth:100, textAlign:'center'}}>May 2026</b>
          <div className="wf-btn ghost">›</div>
          <span style={{borderLeft:'1.5px dashed var(--line)', paddingLeft:8, marginLeft:8, display:'flex', gap:6}}>
            <span className="wf-tab is-active">Month</span>
            <span className="wf-tab">List</span>
          </span>
          <div className="wf-btn primary" style={{marginLeft:8}}>+ Subscribe</div>
        </div>
      </div>

      <div style={{display:'grid', gridTemplateColumns:'2fr 1fr', gap:14}}>
        <div className="wf-card" style={{padding:14}}>
          <div style={{display:'grid', gridTemplateColumns:'repeat(7,1fr)', fontSize:11, color:'var(--ink-soft)', textTransform:'uppercase', letterSpacing:'.08em', borderBottom:'1.5px dashed var(--line)', paddingBottom:8}}>
            {['Sun','Mon','Tue','Wed','Thu','Fri','Sat'].map(d => <div key={d} style={{textAlign:'center'}}>{d}</div>)}
          </div>
          <div style={{display:'grid', gridTemplateColumns:'repeat(7,1fr)', gap:4, marginTop:6}}>
            {days.map(([n, mark, evs, dim],i) => (
              <div key={i} style={{
                aspectRatio:'1.1', padding:6, borderRadius:8,
                border:'1.5px solid', borderColor: mark === 'today' ? 'var(--ink)' : 'var(--line-soft)',
                background: mark === 'today' ? 'var(--pink)' : 'var(--paper)',
                opacity: dim ? 0.4 : 1, fontSize:11, display:'flex', flexDirection:'column', gap:4,
              }}>
                <div style={{fontWeight: mark === 'today' ? 700 : 500}}>{n}</div>
                {evs.map((e,j) => (
                  <div key={j} className="wf-pill" style={{
                    background: e.c === 'pink' ? 'var(--pink-2)' : e.c === 'violet' ? 'var(--lav-2)' : 'var(--paper)',
                    fontSize:10, padding:'2px 6px', whiteSpace:'nowrap', overflow:'hidden', textOverflow:'ellipsis',
                  }}>{e.t}</div>
                ))}
              </div>
            ))}
          </div>
          <div style={{display:'flex', gap:14, marginTop:14, fontSize:11, color:'var(--ink-soft)'}}>
            <span><span style={{display:'inline-block', width:10, height:10, background:'var(--lav-2)', borderRadius:3, marginRight:6}}></span>Board</span>
            <span><span style={{display:'inline-block', width:10, height:10, background:'var(--pink-2)', borderRadius:3, marginRight:6}}></span>Amenities</span>
            <span><span style={{display:'inline-block', width:10, height:10, background:'oklch(0.85 0.06 80)', borderRadius:3, marginRight:6}}></span>Social</span>
            <span><span style={{display:'inline-block', width:10, height:10, background:'oklch(0.82 0.07 200)', borderRadius:3, marginRight:6}}></span>Maintenance</span>
          </div>
        </div>

        <div className="wf-col">
          <div className="wf-card lav">
            <div className="wf-h2">Upcoming · next 60 days</div>
            <div style={{display:'flex', flexDirection:'column', gap:8, marginTop:8}}>
              {upcoming.map((e,i) => (
                <div key={i} style={{display:'flex', gap:10, padding:'8px 10px', border:'1.5px dashed var(--line)', background:'var(--paper)', borderRadius:10}}>
                  <div style={{width:48, textAlign:'center'}}>
                    <div style={{fontSize:10, color:'var(--ink-soft)', textTransform:'uppercase'}}>{e.d.split(' ')[0]}</div>
                    <div style={{fontSize:18, fontWeight:600, fontFamily:'Geist Mono'}}>{e.d.split(' ')[1]}</div>
                  </div>
                  <div style={{flex:1}}>
                    <div style={{fontSize:12, fontWeight:500}}>{e.t}</div>
                    <div className="wf-sub" style={{fontSize:11}}>{e.loc}</div>
                  </div>
                  <span className="wf-pill" style={{alignSelf:'flex-start', fontSize:10}}>{e.c}</span>
                </div>
              ))}
            </div>
          </div>
          <div className="wf-card dashed">
            <div className="wf-h2">📅 Subscribe</div>
            <p className="wf-sub" style={{fontSize:11}}>Add Sakura Heights events to Google / Apple calendar.</p>
            <div className="wf-field dashed mono" style={{fontSize:10, marginTop:6}}>https://nekohoa.com/cal/sakura.ics</div>
            <div style={{display:'flex', gap:6, marginTop:8}}>
              <div className="wf-btn ghost" style={{fontSize:11}}>Copy link</div>
              <div className="wf-btn ghost" style={{fontSize:11}}>+ Google</div>
              <div className="wf-btn ghost" style={{fontSize:11}}>+ Apple</div>
            </div>
          </div>
        </div>
      </div>
      <span className="wf-note" style={{alignSelf:'flex-end'}}>month grid + upcoming list <span className="arrow"></span></span>
    </WFShell>
  );
}

function CalendarB() {
  const months = ['May', 'Jun', 'Jul', 'Aug', 'Sep'];
  const events = [
    {m:'May', d:20, t:'Board meeting', loc:'Clubhouse · 7pm', c:'Board', col:'var(--lav-2)'},
    {m:'May', d:24, t:'🏊 Pool opens for season', loc:'Sakura pool · 10am', c:'Amenity', col:'var(--pink-2)'},
    {m:'Jun', d:14, t:'Community potluck', loc:'Park pavilion · 5pm', c:'Social', col:'oklch(0.85 0.06 80)'},
    {m:'Jun', d:17, t:'Board meeting', loc:'Clubhouse · 7pm', c:'Board', col:'var(--lav-2)'},
    {m:'Jul', d:4,  t:'July 4th party', loc:'Park · all day', c:'Social', col:'oklch(0.85 0.06 80)'},
    {m:'Jul', d:15, t:'Board meeting', loc:'Clubhouse · 7pm', c:'Board', col:'var(--lav-2)'},
    {m:'Aug', d:19, t:'Board meeting', loc:'Clubhouse · 7pm', c:'Board', col:'var(--lav-2)'},
    {m:'Sep', d:8,  t:'Pool closes for season', loc:'Sakura pool', c:'Amenity', col:'var(--pink-2)'},
    {m:'Sep', d:16, t:'Board meeting', loc:'Clubhouse · 7pm', c:'Board', col:'var(--lav-2)'},
  ];
  return (
    <WFShellSide sideActive="Calendar">
      <div style={{display:'flex', alignItems:'baseline'}}>
        <h1 className="wf-h1">Calendar</h1>
        <span className="wf-sub" style={{marginLeft:10}}>9 events · next 5 months</span>
        <div style={{marginLeft:'auto', display:'flex', gap:6}}>
          <span className="wf-tab">Month</span>
          <span className="wf-tab is-active">Timeline</span>
        </div>
      </div>

      <div className="wf-card lav" style={{display:'flex', gap:8, alignItems:'center'}}>
        <span className="wf-sub">Filter:</span>
        <span className="wf-pill" style={{background:'var(--lav-2)'}}>✓ Board</span>
        <span className="wf-pill" style={{background:'var(--pink-2)'}}>✓ Amenities</span>
        <span className="wf-pill" style={{background:'oklch(0.85 0.06 80)'}}>✓ Social</span>
        <span className="wf-pill" style={{background:'var(--paper)', color:'var(--ink-mute)'}}>○ Maintenance</span>
        <div className="wf-btn ghost" style={{marginLeft:'auto'}}>📅 Subscribe (.ics)</div>
      </div>

      <div className="wf-card" style={{padding:0}}>
        {months.map(m => (
          <div key={m} style={{display:'grid', gridTemplateColumns:'80px 1fr', borderBottom:'1.5px dashed var(--line)'}}>
            <div style={{padding:'18px 14px', borderRight:'1.5px dashed var(--line)', background:'var(--lav)'}}>
              <div className="hand" style={{fontSize:24}}>{m}</div>
              <div className="wf-sub" style={{fontSize:11}}>2026</div>
            </div>
            <div style={{padding:'10px 14px', display:'flex', flexDirection:'column', gap:6}}>
              {events.filter(e => e.m === m).map((e,i) => (
                <div key={i} style={{display:'flex', gap:14, padding:'8px 10px', borderRadius:8, alignItems:'center', background: 'var(--paper)', border:'1.5px solid var(--line-soft)'}}>
                  <div style={{width:40, textAlign:'center'}}>
                    <div style={{fontSize:18, fontWeight:600, fontFamily:'Geist Mono'}}>{e.d}</div>
                  </div>
                  <div style={{width:6, height:32, borderRadius:3, background:e.col, flexShrink:0}}></div>
                  <div style={{flex:1}}>
                    <div style={{fontSize:13, fontWeight:500}}>{e.t}</div>
                    <div className="wf-sub" style={{fontSize:11}}>{e.loc}</div>
                  </div>
                  <span className="wf-pill" style={{background:e.col, fontSize:10}}>{e.c}</span>
                  <span className="wf-link" style={{fontSize:11}}>RSVP</span>
                </div>
              ))}
              {events.filter(e => e.m === m).length === 0 && (
                <div className="wf-sub" style={{padding:'8px 10px', fontSize:11}}>nothing scheduled</div>
              )}
            </div>
          </div>
        ))}
      </div>
      <span className="wf-note violet" style={{alignSelf:'flex-end'}}>timeline · grouped by month <span className="arrow"></span></span>
    </WFShellSide>
  );
}

Object.assign(window, {
  DocumentsA, DocumentsB,
  AnnouncementsA, AnnouncementsB,
  CalendarA, CalendarB,
});
