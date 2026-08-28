// Dashboard, Statement, Payments wireframes (2 variants each)

// ── Dashboard ─────────────────────────────────────────────────
function DashboardA() {
  return (
    <WFShell active="Dashboard">
      <div style={{display:'flex', alignItems:'baseline', gap:10}}>
        <h1 className="wf-h1">My <span className="hand">dashboard</span></h1>
        <span className="wf-sub" style={{marginLeft:'auto'}}>Last activity 04/21/2026 · 3:58 PM</span>
      </div>

      <div className="wf-card pink" style={{display:'flex', alignItems:'center', gap:14}}>
        <div className="wf-ph" style={{width:46, height:46, borderRadius:12, flexShrink:0}}>📣</div>
        <div style={{flex:1}}>
          <div style={{fontSize:11, color:'var(--ink-soft)'}}>05/04/2026</div>
          <div style={{fontWeight:600}}>Mass email — Subject: Community pool opening!</div>
        </div>
        <span className="wf-link">View all →</span>
      </div>

      <div style={{display:'grid', gridTemplateColumns:'1fr 1fr', gap:14}}>
        <div className="wf-col">
          <div className="wf-card" style={{borderColor:'var(--rose)'}}>
            <div style={{display:'flex', alignItems:'center', gap:8}}>
              <span style={{color:'var(--warn)'}}>⚠</span>
              <span style={{fontWeight:600}}>Payment due</span>
              <span className="wf-pill warn" style={{marginLeft:'auto'}}>due 6/1</span>
            </div>
            <div style={{fontSize:34, fontFamily:'Geist Mono', margin:'10px 0 12px'}}>$35.00</div>
            <div style={{display:'flex', gap:8}}>
              <div className="wf-btn primary">Pay now</div>
              <div className="wf-btn ghost">View statement</div>
            </div>
          </div>

          <div className="wf-card">
            <div className="wf-h2">Open violations</div>
            <div style={{display:'flex', alignItems:'center', gap:10, color:'var(--ok)'}}>
              <span>✓</span> Thank you for your compliance!
            </div>
            <div className="wf-link" style={{marginTop:10, fontSize:12}}>View all →</div>
          </div>

          <div className="wf-card">
            <div className="wf-h2">My documents</div>
            <table className="wf-table">
              <thead><tr><th>Type</th><th>Date</th><th>File</th></tr></thead>
              <tbody>
                <tr><td>Forms</td><td>04/01/26</td><td className="wf-link">ACC Form</td></tr>
                <tr><td>Insurance</td><td>07/07/24</td><td className="wf-link">Sakura ‘24-‘25</td></tr>
                <tr><td>Budget</td><td>01/01/24</td><td className="wf-link">Sakura Budget</td></tr>
              </tbody>
            </table>
          </div>
        </div>

        <div className="wf-col">
          <div className="wf-card">
            <div className="wf-h2">Account activity</div>
            <ul style={{margin:0, padding:0, listStyle:'none', display:'flex', flexDirection:'column', gap:8, fontSize:12}}>
              {['05/01/26 — $35.00 charge posted', '04/02/26 — $35.00 payment posted', '04/01/26 — $35.00 charge posted', '03/02/26 — $35.00 payment posted', '03/01/26 — $35.00 charge posted'].map((l,i) =>
                <li key={i} style={{display:'flex', gap:8, paddingBottom:6, borderBottom:'1px dashed var(--line-soft)'}}>
                  <span className="wf-link" style={{flex:1}}>{l}</span>
                </li>
              )}
            </ul>
          </div>

          <div className="wf-card lav">
            <div className="wf-h2">My community</div>
            <div className="wf-sub">Association expenses · last 12 mo</div>
            <div style={{display:'flex', gap:14, alignItems:'center', marginTop:10}}>
              <div className="wf-donut"></div>
              <div style={{fontSize:11, display:'grid', gridTemplateColumns:'auto 1fr', gap:'3px 8px', flex:1}}>
                <span style={{width:8, height:8, background:'var(--rose)', borderRadius:2, alignSelf:'center'}}></span><span>Pool ops · $57,640</span>
                <span style={{width:8, height:8, background:'var(--violet)', borderRadius:2, alignSelf:'center'}}></span><span>Landscape · $52,684</span>
                <span style={{width:8, height:8, background:'var(--lav-2)', borderRadius:2, alignSelf:'center'}}></span><span>Mgmt fee · $12,758</span>
                <span style={{width:8, height:8, background:'var(--pink-2)', borderRadius:2, alignSelf:'center'}}></span><span>Insurance · $10,803</span>
                <span style={{width:8, height:8, background:'oklch(0.85 0.06 80)', borderRadius:2, alignSelf:'center'}}></span><span>Repairs · $21,640</span>
                <span style={{width:8, height:8, background:'oklch(0.82 0.07 200)', borderRadius:2, alignSelf:'center'}}></span><span>Other · $25,506</span>
              </div>
            </div>
          </div>

          <div className="wf-card dashed">
            <div className="wf-h2">Quick links</div>
            <div style={{display:'flex', flexDirection:'column', gap:5, fontSize:12}}>
              <span className="wf-link">Set up recurring payments</span>
              <span className="wf-link">Update contact info</span>
              <span className="wf-link">Update mailing address</span>
            </div>
          </div>
        </div>
      </div>
    </WFShell>
  );
}

function DashboardB() {
  return (
    <WFShellSide sideActive="Dashboard">
      <h1 className="wf-h1">Hi <span className="hand">Nicholas</span> 👋</h1>
      <p className="wf-sub">Here's what's happening at Sakura Heights this week.</p>

      <div className="wf-grid-4">
        <div className="wf-card">
          <div className="wf-field-label">Balance</div>
          <div style={{fontSize:22, fontFamily:'Geist Mono'}}>$35.00</div>
          <div className="wf-pill warn" style={{marginTop:6}}>due 6/1</div>
        </div>
        <div className="wf-card">
          <div className="wf-field-label">Violations</div>
          <div style={{fontSize:22, fontFamily:'Geist Mono'}}>0</div>
          <div className="wf-pill ok" style={{marginTop:6}}>compliant</div>
        </div>
        <div className="wf-card">
          <div className="wf-field-label">Next event</div>
          <div style={{fontWeight:600, fontSize:13}}>Pool opens</div>
          <div className="wf-sub">May 24 · Sat</div>
        </div>
        <div className="wf-card">
          <div className="wf-field-label">Documents</div>
          <div style={{fontSize:22, fontFamily:'Geist Mono'}}>12</div>
          <div className="wf-sub">3 new this month</div>
        </div>
      </div>

      <div style={{display:'grid', gridTemplateColumns:'2fr 1fr', gap:14}}>
        <div className="wf-card">
          <div style={{display:'flex', alignItems:'baseline'}}>
            <div className="wf-h2">Recent activity</div>
            <span className="wf-link" style={{marginLeft:'auto', fontSize:12}}>View ledger →</span>
          </div>
          <table className="wf-table">
            <thead><tr><th>Date</th><th>Description</th><th className="num">Charge</th><th className="num">Payment</th></tr></thead>
            <tbody>
              <tr><td>05/01/26</td><td>Assessment for May 2026</td><td className="num">$35.00</td><td className="num">—</td></tr>
              <tr><td>04/02/26</td><td>Payment received</td><td className="num">—</td><td className="num">$35.00</td></tr>
              <tr><td>04/01/26</td><td>Assessment for April 2026</td><td className="num">$35.00</td><td className="num">—</td></tr>
              <tr><td>03/02/26</td><td>Payment received</td><td className="num">—</td><td className="num">$35.00</td></tr>
            </tbody>
          </table>
        </div>

        <div className="wf-col">
          <div className="wf-card pink">
            <div style={{display:'flex', alignItems:'baseline'}}>
              <div className="wf-h2">Pinned announcement</div>
              <span className="wf-pill" style={{marginLeft:'auto'}}>NEW</span>
            </div>
            <div style={{fontWeight:600, marginTop:6}}>Pool opens May 24</div>
            <p className="wf-sub" style={{margin:'4px 0 0'}}>Renovations finished. New hours posted in Documents.</p>
          </div>
          <div className="wf-card">
            <div className="wf-h2">This week</div>
            <ul style={{margin:0, padding:0, listStyle:'none', fontSize:12, display:'flex', flexDirection:'column', gap:8}}>
              <li style={{display:'flex', gap:8}}><span className="wf-pill" style={{background:'var(--lav-2)'}}>tue</span> Board meeting · 7pm</li>
              <li style={{display:'flex', gap:8}}><span className="wf-pill" style={{background:'var(--pink-2)'}}>fri</span> Landscape walkthrough</li>
              <li style={{display:'flex', gap:8}}><span className="wf-pill ok">sat</span> Pool opens</li>
            </ul>
          </div>
        </div>
      </div>
      <span className="wf-note violet" style={{alignSelf:'flex-end'}}>sidebar variant <span className="arrow"></span></span>
    </WFShellSide>
  );
}

// ── Account Statement ─────────────────────────────────────────
const STMT_ROWS = [
  ['Regular Assessment', '06/01/25', 'RAS-2025M6-7716699-56', 'Assessment for June 2025', '$35.00', '—', '$35.00'],
  ['Payment',            '06/02/25', '91831926',              'Payment received',         '—',     '$35.00','$0.00'],
  ['Regular Assessment', '07/01/25', 'RAS-2025M7-7827136-154','Assessment for July 2025', '$35.00', '—',    '$35.00'],
  ['Payment',            '07/02/25', '92330089',              'Payment received',         '—',     '$35.00','$0.00'],
  ['Regular Assessment', '08/01/25', 'RAS-2025M8-7934446-14', 'Assessment for August 2025','$35.00','—',    '$35.00'],
  ['Payment',            '08/04/25', '92867139',              'Payment received',         '—',     '$35.00','$0.00'],
  ['Regular Assessment', '09/01/25', 'RAS-2025M9-8037887-76', 'Assessment for September 2025','$35.00','—', '$35.00'],
  ['Payment',            '09/02/25', '93292684',              'Payment received',         '—',     '$35.00','$0.00'],
  ['Regular Assessment', '10/01/25', 'RAS-2025M10-8166899-300','Assessment for October 2025','$35.00','—', '$35.00'],
  ['Payment',            '10/02/25', '93787455',              'Payment received',         '—',     '$35.00','$0.00'],
  ['Regular Assessment', '11/01/25', 'RAS-2025M11-8273129-327','Assessment for November 2025','$35.00','—','$35.00'],
  ['Payment',            '11/04/25', '94396006',              'Payment received',         '—',     '$35.00','$0.00'],
  ['Regular Assessment', '05/01/26', 'RAS-2026M5-9017522-121','Assessment for May 2026',  '$35.00', '—',    '$35.00'],
  ['Regular Assessment', '06/01/26', 'RAS-2026M6-9146058-248','Assessment for June 2026', '$35.00', '—',    '$70.00'],
];

function StatementA() {
  return (
    <WFShell active="Payments">
      <div style={{display:'flex', alignItems:'flex-start', gap:10}}>
        <div>
          <h1 className="wf-h1">Account <span className="hand">statement</span></h1>
          <p className="wf-sub">Every charge and payment posted to your account.</p>
        </div>
        <div style={{marginLeft:'auto', display:'flex', gap:8}}>
          <div className="wf-btn ghost">⎙ Print</div>
          <div className="wf-btn">Set up recurring</div>
          <div className="wf-btn primary">Make payment</div>
        </div>
      </div>

      <div className="wf-card lav" style={{display:'flex', alignItems:'center', gap:14}}>
        <div style={{display:'flex', gap:6, padding:3, background:'var(--paper)', borderRadius:999, border:'1.5px solid var(--ink)'}}>
          <span className="wf-tab is-active">Statement</span>
          <span className="wf-tab">Open balance</span>
        </div>
        <div className="wf-field dashed" style={{flex:'0 0 130px'}}>📅 05/04/25</div>
        <span className="wf-sub">to</span>
        <div className="wf-field dashed" style={{flex:'0 0 130px'}}>📅 06/01/26</div>
        <div className="wf-btn">↻ Refresh</div>
        <div style={{marginLeft:'auto', textAlign:'right'}}>
          <div className="wf-field-label" style={{margin:0}}>Balance</div>
          <div className="mono" style={{fontSize:18, fontWeight:600}}>$70.00</div>
        </div>
      </div>

      <div className="wf-card" style={{padding:0, overflow:'hidden'}}>
        <div className="wf-scroll" style={{maxHeight: 420}}>
          <table className="wf-table">
            <thead><tr><th>Type</th><th>Date</th><th>Doc #</th><th>Description</th><th className="num">Charge</th><th className="num">Payment</th><th className="num">Balance</th></tr></thead>
            <tbody>
              {STMT_ROWS.map((r,i) => (
                <tr key={i} style={i === STMT_ROWS.length-1 ? {background:'var(--pink)'} : {}}>
                  <td>{r[0]}</td><td>{r[1]}</td><td className="wf-link mono" style={{fontSize:11}}>{r[2]}</td>
                  <td>{r[3]}</td><td className="num">{r[4]}</td><td className="num">{r[5]}</td><td className="num">{r[6]}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        <div style={{padding:'10px 14px', display:'flex', gap:14, justifyContent:'flex-end', background:'var(--lav)', borderTop:'1.5px solid var(--line)'}}>
          <span style={{fontSize:11, color:'var(--ink-soft)'}}>Total charges</span><b className="mono">$455.00</b>
          <span style={{fontSize:11, color:'var(--ink-soft)'}}>Total payments</span><b className="mono">$385.00</b>
          <span style={{fontSize:11, color:'var(--ink-soft)'}}>Balance</span><b className="mono" style={{color:'var(--rose)'}}>$70.00</b>
        </div>
      </div>
    </WFShell>
  );
}

function StatementB() {
  return (
    <WFShellSide sideActive="Statement">
      <div style={{display:'flex', alignItems:'baseline'}}>
        <h1 className="wf-h1">Statement</h1>
        <span className="wf-sub" style={{marginLeft:10}}>Jun 2025 → Jun 2026</span>
        <div style={{marginLeft:'auto', display:'flex', gap:8}}>
          <div className="wf-btn ghost">Export CSV</div>
          <div className="wf-btn primary">Pay $70.00</div>
        </div>
      </div>

      <div className="wf-grid-3">
        <div className="wf-card pink"><div className="wf-field-label">Total charges</div><div style={{fontSize:22, fontFamily:'Geist Mono'}}>$455.00</div></div>
        <div className="wf-card lav"><div className="wf-field-label">Total payments</div><div style={{fontSize:22, fontFamily:'Geist Mono'}}>$385.00</div></div>
        <div className="wf-card" style={{borderColor:'var(--rose)'}}><div className="wf-field-label">Outstanding</div><div style={{fontSize:22, fontFamily:'Geist Mono', color:'var(--rose)'}}>$70.00</div></div>
      </div>

      <div className="wf-card" style={{padding:0}}>
        <div style={{padding:'10px 14px', display:'flex', gap:8, alignItems:'center', borderBottom:'1.5px dashed var(--line)'}}>
          <div className="wf-field dashed" style={{flex:'0 0 200px'}}>🔍 Search ledger…</div>
          <div className="wf-pill">Type: All</div>
          <div className="wf-pill">Year: 2026</div>
          <div className="wf-btn ghost" style={{marginLeft:'auto'}}>+ Filter</div>
        </div>
        <div className="wf-scroll" style={{maxHeight: 380}}>
          <table className="wf-table">
            <thead><tr><th>Date</th><th>Type</th><th>Description</th><th className="num">Amount</th><th className="num">Running</th></tr></thead>
            <tbody>
              {STMT_ROWS.map((r,i) => {
                const isCharge = r[0] !== 'Payment';
                return (
                  <tr key={i}>
                    <td>{r[1]}</td>
                    <td><span className="wf-pill" style={{background: isCharge ? 'var(--pink-2)':'var(--lav-2)'}}>{isCharge ? 'charge' : 'payment'}</span></td>
                    <td>{r[3]}</td>
                    <td className="num">{isCharge ? r[4] : `-${r[5]}`}</td>
                    <td className="num">{r[6]}</td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      </div>
      <span className="wf-note violet" style={{alignSelf:'flex-end'}}>summary cards on top <span className="arrow"></span></span>
    </WFShellSide>
  );
}

Object.assign(window, { DashboardA, DashboardB, StatementA, StatementB });
