// Violations + Property + Owner + Directory wireframes

function ViolationsA() {
  return (
    <WFShell active="Community">
      <div style={{display:'flex', alignItems:'baseline'}}>
        <h1 className="wf-h1">Open <span className="hand">violations</span></h1>
        <div style={{marginLeft:'auto', display:'flex', gap:8}}>
          <div className="wf-btn ghost">+ Filter</div>
          <div className="wf-btn">Export</div>
        </div>
      </div>

      <div className="wf-card lav" style={{display:'flex', alignItems:'center', gap:10}}>
        <div style={{display:'flex', gap:6, padding:3, background:'var(--paper)', borderRadius:999, border:'1.5px solid var(--ink)'}}>
          <span className="wf-tab is-active">Open · 0</span>
          <span className="wf-tab">Closed · 4</span>
        </div>
        <span className="wf-sub" style={{marginLeft:10}}>From</span>
        <div className="wf-field dashed" style={{flex:'0 0 130px'}}>📅 ____ / __ / __</div>
        <span className="wf-sub">to</span>
        <div className="wf-field dashed" style={{flex:'0 0 130px'}}>📅 ____ / __ / __</div>
        <div className="wf-btn" style={{marginLeft:'auto'}}>↻ Refresh</div>
      </div>

      <div className="wf-card" style={{padding:0}}>
        <table className="wf-table">
          <thead><tr><th>Status</th><th>Location</th><th>Regarding</th><th>Opened</th><th>Attorney</th><th>Category</th></tr></thead>
        </table>
        <div style={{padding:'80px 20px', textAlign:'center', color:'var(--ok)', display:'flex', flexDirection:'column', alignItems:'center', gap:12}}>
          <div className="wf-ph" style={{width:80, height:80, borderRadius:'50%', borderColor:'var(--ok)', color:'var(--ok)'}}>✓</div>
          <div style={{fontSize:18, fontWeight:600}}>Thank you for your compliance!</div>
          <p className="wf-sub" style={{maxWidth: 360}}>No open violations on your property. Closed history is available in the tab above.</p>
        </div>
      </div>
      <span className="wf-note" style={{alignSelf:'flex-end'}}>empty state · friendly <span className="arrow"></span></span>
    </WFShell>
  );
}

function ViolationsB() {
  const closed = [
    ['Trash bins out past pickup', '04/12/26', 'Maintenance', 'Closed'],
    ['Lawn height exceeded 6"', '03/02/26', 'Landscape', 'Closed'],
    ['Holiday lights past Jan 15', '01/22/26', 'Architectural', 'Closed'],
    ['Vehicle parked on grass', '11/04/25', 'Parking', 'Closed'],
  ];
  return (
    <WFShellSide sideActive="Violations">
      <h1 className="wf-h1">Violations</h1>

      <div className="wf-grid-3">
        <div className="wf-card" style={{borderColor:'var(--ok)'}}>
          <div className="wf-field-label">Open</div>
          <div style={{fontSize:28, fontFamily:'Geist Mono', color:'var(--ok)'}}>0</div>
          <div className="wf-pill ok">all clear</div>
        </div>
        <div className="wf-card">
          <div className="wf-field-label">Closed (12 mo)</div>
          <div style={{fontSize:28, fontFamily:'Geist Mono'}}>4</div>
          <div className="wf-sub">last: Apr 12</div>
        </div>
        <div className="wf-card">
          <div className="wf-field-label">Most common</div>
          <div style={{fontWeight:600}}>Landscape</div>
          <div className="wf-sub">2 of 4</div>
        </div>
      </div>

      <div className="wf-card" style={{padding:0}}>
        <div style={{display:'flex', borderBottom:'1.5px dashed var(--line)'}}>
          <div style={{padding:'10px 16px', borderBottom:'2px solid var(--rose)', fontWeight:600}}>History</div>
          <div style={{padding:'10px 16px', color:'var(--ink-soft)'}}>Rules</div>
          <div style={{padding:'10px 16px', color:'var(--ink-soft)'}}>Appeal a notice</div>
        </div>
        <table className="wf-table">
          <thead><tr><th>Issue</th><th>Date</th><th>Category</th><th>Status</th><th></th></tr></thead>
          <tbody>
            {closed.map((r,i) => (
              <tr key={i}>
                <td>{r[0]}</td>
                <td>{r[1]}</td>
                <td><span className="wf-pill" style={{background:'var(--lav-2)'}}>{r[2]}</span></td>
                <td><span className="wf-pill ok">{r[3]}</span></td>
                <td className="wf-link">view →</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <span className="wf-note violet" style={{alignSelf:'flex-end'}}>summary + history table <span className="arrow"></span></span>
    </WFShellSide>
  );
}

// ── Property Information ──────────────────────────────────────
function PropertyA() {
  return (
    <WFShellSide sideActive="Info">
      <div style={{display:'flex', alignItems:'baseline'}}>
        <h1 className="wf-h1">Property <span className="hand">info</span></h1>
        <span className="wf-pill ok" style={{marginLeft:10}}>✓ Active</span>
        <div style={{marginLeft:'auto', display:'flex', gap:8}}>
          <div className="wf-btn ghost">Request changes</div>
        </div>
      </div>

      <div className="wf-grid-2">
        <div className="wf-card">
          <div className="wf-field-label">Account number</div>
          <div className="mono" style={{fontSize:14, fontWeight:600, marginTop:4}}>R0670853L0541192</div>
        </div>
        <div className="wf-card">
          <div className="wf-field-label">Annual assessment</div>
          <div style={{fontSize:18, fontFamily:'Geist Mono', marginTop:4}}>$35.00</div>
          <div className="wf-sub">due 1st of each month</div>
        </div>
      </div>

      <div className="wf-card">
        <div className="wf-h2">🏠 Property details</div>
        <div style={{display:'grid', gridTemplateColumns:'repeat(3, 1fr)', gap:'14px 24px'}}>
          {[
            ['Account #', 'R0670853L0541192'],
            ['Community ID', 'SAKURA'],
            ['Check digit / PIN', '0'],
            ['Lot', '151'],
            ['Phase', '—'],
            ['Section', 'SF'],
            ['Block', '—'],
            ['Fiscal year', '2026'],
            ['Year built', '2014'],
          ].map(([k,v]) => (
            <div key={k}>
              <div className="wf-field-label">{k}</div>
              <div style={{fontSize:13, fontWeight:500}}>{v}</div>
            </div>
          ))}
        </div>
      </div>

      <div className="wf-card">
        <div className="wf-h2">📑 Assessment rules · 2026</div>
        <table className="wf-table">
          <thead><tr><th>Rule type</th><th>Rule</th></tr></thead>
          <tbody>
            <tr><td>Regular assessment</td><td><b>$35.00</b> due the <b>1st</b> of each <b>month</b>.</td></tr>
            <tr><td>Late fee</td><td><b>$20.00</b> per missed period, applied 30 days after the due date.</td></tr>
            <tr><td>Finance charge</td><td>18.00% per annum, simple compounding, 365-day year.</td></tr>
          </tbody>
        </table>
      </div>
    </WFShellSide>
  );
}

function PropertyB() {
  return (
    <WFShellSide sideActive="Info">
      <h1 className="wf-h1">714 Keystone Park Dr</h1>
      <p className="wf-sub" style={{marginTop:-4}}>Sakura Heights · lot 151 · section SF</p>

      <div style={{display:'grid', gridTemplateColumns:'1.4fr 1fr', gap:14}}>
        <div className="wf-card" style={{padding:0}}>
          <div className="wf-ph" style={{height:200, border:'none', borderRadius:'14px 14px 0 0'}}>satellite map · placeholder</div>
          <div style={{padding:14}}>
            <div className="wf-h2">At a glance</div>
            <div style={{display:'grid', gridTemplateColumns:'1fr 1fr', gap:'10px 20px', fontSize:12}}>
              <div><div className="wf-field-label">Account</div><span className="mono">R0670853L0541192</span></div>
              <div><div className="wf-field-label">Status</div><span className="wf-pill ok">active</span></div>
              <div><div className="wf-field-label">Lot</div>151</div>
              <div><div className="wf-field-label">Section</div>SF</div>
              <div><div className="wf-field-label">Fiscal year</div>2026</div>
              <div><div className="wf-field-label">Year built</div>2014</div>
            </div>
          </div>
        </div>

        <div className="wf-col">
          <div className="wf-card lav">
            <div className="wf-h2">Monthly dues</div>
            <div style={{fontSize:28, fontFamily:'Geist Mono'}}>$35.00</div>
            <div className="wf-sub">due 1st · annual $420</div>
          </div>
          <div className="wf-card pink">
            <div className="wf-h2">Late fee</div>
            <div style={{fontSize:18, fontFamily:'Geist Mono', color:'var(--rose)'}}>$20.00</div>
            <div className="wf-sub">applied 30 days after due</div>
          </div>
          <div className="wf-card">
            <div className="wf-h2">Finance charge</div>
            <div style={{fontSize:18, fontFamily:'Geist Mono'}}>18.00%</div>
            <div className="wf-sub">per annum, simple, 365 day</div>
          </div>
        </div>
      </div>

      <div className="wf-card dashed">
        <div className="wf-h2">Late fee timeline · 2026</div>
        <div style={{position:'relative', padding:'24px 0 30px'}}>
          <div style={{height:2, background:'var(--line)', position:'relative'}}>
            {[0,1,2,3,4,5,6,7,8,9,10,11].map(i => (
              <div key={i} style={{position:'absolute', left:`${(i/11)*100}%`, top:-4, transform:'translateX(-50%)'}}>
                <div style={{width:10, height:10, borderRadius:'50%', background:'var(--rose)', border:'1.5px solid var(--ink)'}}></div>
                <div style={{position:'absolute', top:14, left:'50%', transform:'translateX(-50%)', fontSize:10, color:'var(--ink-soft)', whiteSpace:'nowrap'}}>
                  {['Jan','Feb','Mar','Apr','May','Jun','Jul','Aug','Sep','Oct','Nov','Dec'][i]}
                </div>
              </div>
            ))}
          </div>
        </div>
        <p className="wf-sub" style={{fontSize:11}}>+$20 added each month past 30 days late.</p>
      </div>
      <span className="wf-note violet" style={{alignSelf:'flex-end'}}>map header + timeline <span className="arrow"></span></span>
    </WFShellSide>
  );
}

// ── Owner Information ─────────────────────────────────────────
function OwnerA() {
  return (
    <WFShellSide sideActive="Owner">
      <div style={{display:'flex', alignItems:'baseline'}}>
        <h1 className="wf-h1">Owner <span className="hand">info</span></h1>
        <div style={{marginLeft:'auto', display:'flex', gap:8}}>
          <div className="wf-btn ghost">Cancel</div>
          <div className="wf-btn primary">Save changes</div>
        </div>
      </div>

      <div className="wf-grid-2">
        <div className="wf-card">
          <div style={{display:'flex', alignItems:'center', gap:8}}>
            <span className="wf-h2" style={{margin:0}}>👤 Account details</span>
            <span className="wf-pill ok" style={{marginLeft:'auto'}}>active</span>
          </div>
          <p className="wf-sub" style={{margin:'2px 0 14px'}}>Ownership info on file with the association.</p>
          <div style={{display:'grid', gridTemplateColumns:'1fr 1fr', gap:'10px 20px'}}>
            {[['First name','Nicholas'],['Last name','Bonilla'],['Owner name 2','—'],['Member since','Unknown'],['Account #','R0670853L0541192'],['Community','Sakura Heights'],['Property address','714 Keystone Park Dr'],['Voting rights','Yes']].map(([k,v]) => (
              <div key={k}>
                <div className="wf-field-label">{k}</div>
                <div className="wf-field dashed">{v}</div>
              </div>
            ))}
          </div>
        </div>

        <div className="wf-col">
          <div className="wf-card">
            <div className="wf-h2">📬 Mailing address</div>
            <p className="wf-sub" style={{margin:'2px 0 10px'}}>Where statements and notices are sent.</p>
            <div className="wf-card lav" style={{padding:12}}>
              <div style={{display:'flex', alignItems:'center', gap:10}}>
                <b>Mail to property</b>
                <span className="wf-toggle on" style={{marginLeft:'auto'}}></span>
              </div>
              <p className="wf-sub" style={{fontSize:11, marginTop:6}}>
                714 Keystone Park Dr, Morrisville, NC 27560
              </p>
            </div>
            <div className="wf-link" style={{marginTop:10, fontSize:12}}>+ Add alternate address</div>
          </div>
          <div className="wf-card dashed">
            <div className="wf-h2">📜 Address history</div>
            <table className="wf-table">
              <thead><tr><th>Event</th><th>Address</th><th>Date</th></tr></thead>
              <tbody>
                <tr><td>Update</td><td>139 Henry's Watch Ln, Pittsboro NC</td><td>Jul 3 '23</td></tr>
                <tr><td>Update</td><td>714 Keystone Park Dr, Morrisville NC</td><td>Dec 14 '21</td></tr>
                <tr><td>Update</td><td>714 Keystone Park Dr, Morrisville NC</td><td>Dec 8 '21</td></tr>
              </tbody>
            </table>
          </div>
        </div>
      </div>
    </WFShellSide>
  );
}

function OwnerB() {
  return (
    <WFShellSide sideActive="Owner">
      <h1 className="wf-h1">Owner profile</h1>

      <div style={{display:'grid', gridTemplateColumns:'auto 1fr auto', gap:18, alignItems:'center'}} className="wf-card">
        <div className="wf-avatar" style={{width:64, height:64, fontSize:24}}>NB</div>
        <div>
          <div style={{fontSize:18, fontWeight:600}}>Nicholas Bonilla</div>
          <div className="wf-sub">Owner · voting rights enabled · member since 2021</div>
          <div style={{display:'flex', gap:6, marginTop:6}}>
            <span className="wf-pill">📧 nicholas@example.com</span>
            <span className="wf-pill">📞 (919) ___-____</span>
          </div>
        </div>
        <div className="wf-btn">Edit profile</div>
      </div>

      <div className="wf-grid-2">
        <div className="wf-card">
          <div className="wf-h2">Co-owners</div>
          <div className="wf-card dashed" style={{padding:'14px', display:'flex', alignItems:'center', gap:10, color:'var(--ink-soft)'}}>
            <div className="wf-ph" style={{width:36, height:36, borderRadius:'50%'}}>+</div>
            Add a co-owner
          </div>
        </div>
        <div className="wf-card">
          <div className="wf-h2">Mailing preferences</div>
          <div style={{display:'flex', flexDirection:'column', gap:10}}>
            <label style={{display:'flex', alignItems:'center', gap:10}}>
              <span className="wf-toggle on"></span>
              <div><div style={{fontWeight:500}}>Mail to property</div><div className="wf-sub" style={{fontSize:11}}>714 Keystone Park Dr</div></div>
            </label>
            <label style={{display:'flex', alignItems:'center', gap:10}}>
              <span className="wf-toggle on"></span>
              <div><div style={{fontWeight:500}}>Email statements (paperless)</div><div className="wf-sub" style={{fontSize:11}}>Faster · save paper</div></div>
            </label>
            <label style={{display:'flex', alignItems:'center', gap:10}}>
              <span className="wf-toggle"></span>
              <div><div style={{fontWeight:500}}>SMS reminders</div><div className="wf-sub" style={{fontSize:11}}>Day before due</div></div>
            </label>
          </div>
        </div>
      </div>

      <div className="wf-card">
        <div className="wf-h2">Address history</div>
        <table className="wf-table">
          <thead><tr><th>Date</th><th>Event</th><th>Address</th></tr></thead>
          <tbody>
            <tr><td>Jul 3, 2023</td><td><span className="wf-pill">change</span></td><td>139 Henry's Watch Ln, Pittsboro NC 27312</td></tr>
            <tr><td>Dec 14, 2021</td><td><span className="wf-pill">change</span></td><td>714 Keystone Park Dr, Morrisville NC 27560</td></tr>
            <tr><td>Dec 8, 2021</td><td><span className="wf-pill ok">created</span></td><td>714 Keystone Park Dr, Morrisville NC 27560</td></tr>
          </tbody>
        </table>
      </div>
      <span className="wf-note violet" style={{alignSelf:'flex-end'}}>profile-card style <span className="arrow"></span></span>
    </WFShellSide>
  );
}

// ── Directory Sharing ─────────────────────────────────────────
function DirectoryA() {
  return (
    <WFShell active="Community">
      <h1 className="wf-h1">Directory <span className="hand">sharing</span></h1>
      <p className="wf-sub" style={{marginTop:-6}}>Choose what to share in the resident directory. Only verified neighbors can see it.</p>

      <div className="wf-card pink" style={{display:'flex', gap:10}}>
        <span>ⓘ</span>
        <p style={{margin:0, fontSize:12}}>
          Nothing is shared by default. You can turn fields on individually below.
        </p>
      </div>

      <div className="wf-grid-2">
        <div className="wf-card">
          <div className="wf-h2">Owner record</div>
          <p className="wf-sub" style={{fontSize:11}}>Your ownership record and property address.</p>
          <div style={{display:'flex', flexDirection:'column', gap:10, marginTop:10}}>
            <div style={{display:'flex', alignItems:'center', gap:10, padding:'10px 12px', border:'1.5px solid var(--line)', borderRadius:10}}>
              <div className="wf-avatar">NB</div>
              <div style={{flex:1}}>
                <div style={{fontWeight:500}}>Nicholas Bonilla</div>
                <div className="wf-sub" style={{fontSize:11}}>Owner</div>
              </div>
              <span className="wf-toggle"></span>
            </div>
            <div style={{display:'flex', alignItems:'center', gap:10, padding:'10px 12px', border:'1.5px solid var(--line)', borderRadius:10}}>
              <div className="wf-ph" style={{width:32, height:32, borderRadius:8}}>📍</div>
              <div style={{flex:1}}>
                <div style={{fontWeight:500}}>Mailing address</div>
                <div className="wf-sub" style={{fontSize:11}}>139 Henry's Watch Ln, Pittsboro</div>
              </div>
              <span className="wf-toggle"></span>
            </div>
          </div>
        </div>

        <div className="wf-col">
          <div className="wf-card lav">
            <div className="wf-field-label">Directory preview</div>
            <div style={{fontSize:18, fontWeight:600, marginTop:6}}>714 Keystone Park Dr</div>
            <p className="wf-sub" style={{fontSize:11, marginTop:6}}>
              The owner & contact info has not been shared with the directory.
            </p>
          </div>
          <div className="wf-card dashed">
            <div className="wf-h2">🔒 Privacy</div>
            <p className="wf-sub" style={{fontSize:11, lineHeight:1.6}}>
              Only verified residents of <b>Sakura Heights</b> can view the directory.
              Your information is never shared publicly or with third parties.
            </p>
          </div>
        </div>
      </div>

      <div style={{display:'flex', gap:8, alignSelf:'flex-end'}}>
        <div className="wf-btn ghost">Cancel</div>
        <div className="wf-btn primary">Save preferences</div>
      </div>
    </WFShell>
  );
}

function DirectoryB() {
  const fields = [
    ['Name', 'Nicholas Bonilla', false],
    ['Property address', '714 Keystone Park Dr', true],
    ['Email', 'nicholas@example.com', false],
    ['Phone', '(919) ___-____', false],
    ['Move-in date', 'Dec 2021', false],
  ];
  return (
    <WFShellSide sideActive="Directory">
      <div style={{display:'flex', alignItems:'baseline'}}>
        <h1 className="wf-h1">Directory</h1>
        <span className="wf-sub" style={{marginLeft:10}}>Sakura Heights · 248 households</span>
      </div>

      <div className="wf-card">
        <div className="wf-h2">What I share</div>
        <p className="wf-sub" style={{fontSize:11}}>Toggle each field. Changes save automatically.</p>
        <table className="wf-table">
          <thead><tr><th>Field</th><th>Value</th><th>Shared?</th></tr></thead>
          <tbody>
            {fields.map(([k,v,on],i) => (
              <tr key={i}>
                <td>{k}</td>
                <td className="wf-sub">{v}</td>
                <td><span className={`wf-toggle ${on ? 'on' : ''}`}></span></td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <div className="wf-card dashed">
        <div className="wf-h2">🔒 Privacy promise</div>
        <ul style={{margin:0, paddingLeft:18, color:'var(--ink-soft)', fontSize:12, lineHeight:1.7}}>
          <li>Only verified residents of Sakura Heights see the directory.</li>
          <li>We never share your info with third parties.</li>
          <li>You can revoke any time — changes are immediate.</li>
        </ul>
      </div>
      <span className="wf-note violet" style={{alignSelf:'flex-end'}}>field toggle table <span className="arrow"></span></span>
    </WFShellSide>
  );
}

Object.assign(window, {
  ViolationsA, ViolationsB,
  PropertyA, PropertyB,
  OwnerA, OwnerB,
  DirectoryA, DirectoryB,
});
