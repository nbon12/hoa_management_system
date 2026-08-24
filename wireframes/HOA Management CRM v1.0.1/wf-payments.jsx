// One-time payment + Recurring (ACH) wireframes

function PaymentA() {
  return (
    <WFShell active="Payments">
      <h1 className="wf-h1">Make a <span className="hand">one-time</span> payment</h1>
      <p className="wf-sub" style={{marginTop:-6}}>Pay your assessment online with credit card or eCheck.</p>

      <div style={{display:'grid', gridTemplateColumns:'1.5fr 1fr', gap:14}}>
        <div className="wf-col">
          <div className="wf-card">
            <div className="wf-h2">Property</div>
            <div style={{display:'grid', gridTemplateColumns:'120px 1fr', gap:'6px 14px', fontSize:12}}>
              <span className="wf-sub">Address</span><b>714 Keystone Park Dr</b>
              <span className="wf-sub">Community</span><b>Sakura Heights</b>
              <span className="wf-sub">Account</span><span className="mono">R0670853L0541192</span>
            </div>
          </div>

          <div className="wf-card pink" style={{display:'flex', gap:10, alignItems:'flex-start'}}>
            <span style={{color:'var(--rose)'}}>ⓘ</span>
            <div style={{fontSize:12}}>
              You're enrolled in ACH. Your regular assessment will be auto-drafted on the next invoice date.
            </div>
          </div>

          <div className="wf-card">
            <div className="wf-h2">Choose amount</div>
            <div style={{display:'flex', flexDirection:'column', gap:10, fontSize:13}}>
              <label style={{display:'flex', gap:10, alignItems:'center'}}>
                <span className="wf-radio on"></span>
                <span style={{flex:1}}>Current balance <span className="wf-sub">(as of today)</span></span>
                <b className="mono">$35.00</b>
              </label>
              <label style={{display:'flex', gap:10, alignItems:'center'}}>
                <span className="wf-radio"></span>
                <span style={{flex:1}}>Next assessment <span className="wf-sub">due 6/1</span></span>
                <b className="mono">$35.00</b>
              </label>
              <label style={{display:'flex', gap:10, alignItems:'center'}}>
                <span className="wf-radio"></span>
                <span style={{flex:1}}>Balance + next assessment</span>
                <b className="mono">$70.00</b>
              </label>
              <label style={{display:'flex', gap:10, alignItems:'center'}}>
                <span className="wf-radio"></span>
                <span style={{flex:1}}>Other amount</span>
                <div className="wf-field dashed mono" style={{width:120}}>$ ____</div>
              </label>
            </div>
          </div>

          <div className="wf-card">
            <div className="wf-h2">Payment method</div>
            <div style={{display:'flex', gap:10}}>
              <div className="wf-btn primary" style={{flex:1, justifyContent:'center'}}>💳 Credit card</div>
              <div className="wf-btn" style={{flex:1, justifyContent:'center'}}>🏦 eCheck</div>
            </div>
            <p className="wf-sub" style={{marginTop:10}}>Bank may charge a service fee for credit/debit. eCheck is free.</p>
          </div>
        </div>

        <div className="wf-col">
          <div className="wf-card lav" style={{position:'sticky', top:0}}>
            <div className="wf-h2">Summary</div>
            <div style={{display:'flex', justifyContent:'space-between', fontSize:12, padding:'4px 0'}}>
              <span>Amount</span><b className="mono">$35.00</b>
            </div>
            <div style={{display:'flex', justifyContent:'space-between', fontSize:12, padding:'4px 0'}}>
              <span>Processing fee</span><b className="mono">$1.95</b>
            </div>
            <hr className="wf-divider"/>
            <div style={{display:'flex', justifyContent:'space-between', padding:'8px 0'}}>
              <b>Total</b><b className="mono" style={{fontSize:18}}>$36.95</b>
            </div>
            <div className="wf-btn primary" style={{justifyContent:'center', width:'100%'}}>Continue →</div>
            <p className="wf-sub" style={{fontSize:11, marginTop:8}}>Posts within 1 business day.</p>
          </div>
          <div className="wf-card dashed">
            <div className="wf-h2">Pay by mail</div>
            <p className="wf-sub" style={{fontSize:11, lineHeight:1.6}}>
              Payment Processing Center<br/>
              C/O NekoHOA · 8508 Park Rd<br/>
              PMB #118 · Charlotte, NC 28210
            </p>
          </div>
        </div>
      </div>
      <span className="wf-note" style={{alignSelf:'flex-end'}}>two-column · sticky summary <span className="arrow"></span></span>
    </WFShell>
  );
}

function PaymentB() {
  return (
    <WFShellSide sideActive="One-time">
      <h1 className="wf-h1"><span className="hand">3 steps</span> · pay your dues</h1>

      <div style={{display:'flex', gap:8, alignItems:'center', fontSize:11, color:'var(--ink-soft)'}}>
        <span className="wf-pill" style={{background:'var(--rose)', color:'var(--paper)', borderColor:'var(--ink)'}}>1 amount</span>
        <span style={{flex:'0 0 30px', borderTop:'1.5px dashed var(--line)'}}></span>
        <span className="wf-pill">2 method</span>
        <span style={{flex:'0 0 30px', borderTop:'1.5px dashed var(--line)'}}></span>
        <span className="wf-pill">3 review</span>
      </div>

      <div className="wf-card" style={{padding:'24px'}}>
        <div className="wf-h2">Step 1 — How much?</div>
        <p className="wf-sub" style={{marginBottom:14}}>Pick a preset or type your own.</p>
        <div className="wf-grid-3" style={{gap:10}}>
          {[
            ['Current', '$35.00', 'as of today', true],
            ['Next due', '$35.00', 'due 6/1', false],
            ['Both', '$70.00', 'paid through July', false],
          ].map(([t,a,s,sel],i) => (
            <div key={i} className="wf-card" style={{
              borderColor: sel ? 'var(--ink)' : 'var(--line)',
              background: sel ? 'var(--pink)' : 'var(--paper)',
              borderStyle: sel ? 'solid' : 'dashed',
              textAlign:'center', padding:'14px 10px'
            }}>
              <div className="wf-field-label">{t}</div>
              <div style={{fontSize:22, fontFamily:'Geist Mono', margin:'4px 0'}}>{a}</div>
              <div className="wf-sub" style={{fontSize:11}}>{s}</div>
            </div>
          ))}
        </div>
        <div style={{marginTop:14}}>
          <div className="wf-field-label">Or custom amount</div>
          <div className="wf-field dashed" style={{maxWidth:200}}>$ ______</div>
        </div>
      </div>

      <div className="wf-grid-2">
        <div className="wf-card" style={{opacity:0.55}}>
          <div className="wf-h2">Step 2 — Method</div>
          <p className="wf-sub">Card or eCheck. Available next.</p>
        </div>
        <div className="wf-card" style={{opacity:0.55}}>
          <div className="wf-h2">Step 3 — Review</div>
          <p className="wf-sub">Confirm and submit.</p>
        </div>
      </div>

      <div style={{display:'flex', gap:8, marginTop:'auto'}}>
        <div className="wf-btn ghost">← Cancel</div>
        <div className="wf-btn primary" style={{marginLeft:'auto'}}>Continue to method →</div>
      </div>
      <span className="wf-note violet" style={{alignSelf:'flex-end'}}>stepper variant <span className="arrow"></span></span>
    </WFShellSide>
  );
}

// ── Recurring / ACH ───────────────────────────────────────────
function RecurringA() {
  return (
    <WFShell active="Payments">
      <div style={{display:'flex', alignItems:'baseline', gap:10}}>
        <h1 className="wf-h1">Recurring <span className="hand">payments</span></h1>
        <span className="wf-pill ok" style={{marginLeft:8}}>✓ Enrolled in ACH</span>
        <div style={{marginLeft:'auto', display:'flex', gap:8}}>
          <div className="wf-btn ghost">🗑 Turn off</div>
          <div className="wf-btn">✎ Edit</div>
        </div>
      </div>

      <div className="wf-card pink" style={{display:'flex', gap:10}}>
        <span>ⓘ</span>
        <p style={{margin:0, fontSize:12, lineHeight:1.6}}>
          ACH withdrawals will continue until you cancel. Each property needs its own setup.
          Takes effect on your next draft date.
        </p>
      </div>

      <div className="wf-card">
        <div className="wf-h2">ACH settings</div>
        <div style={{display:'grid', gridTemplateColumns:'1fr 1fr', gap:14}}>
          <div>
            <div className="wf-field-label">Amount to pay</div>
            <div style={{display:'flex', flexDirection:'column', gap:6, fontSize:12}}>
              <label style={{display:'flex', gap:8, alignItems:'center'}}><span className="wf-radio on"></span> Assessment charges</label>
              <label style={{display:'flex', gap:8, alignItems:'center'}}><span className="wf-radio"></span> Open balance</label>
              <label style={{display:'flex', gap:8, alignItems:'center'}}><span className="wf-radio"></span> Fixed amount</label>
            </div>
          </div>
          <div>
            <div className="wf-field-label">Draft date *</div>
            <div className="wf-field dashed">2nd of the month ▾</div>
          </div>
          <div>
            <div className="wf-field-label">Bank name *</div>
            <div className="wf-field dashed">Fidelity Investments</div>
          </div>
          <div>
            <div className="wf-field-label">Account type</div>
            <div style={{display:'flex', gap:14, fontSize:12, marginTop:6}}>
              <label style={{display:'flex', gap:6, alignItems:'center'}}><span className="wf-radio on"></span> Checking</label>
              <label style={{display:'flex', gap:6, alignItems:'center'}}><span className="wf-radio"></span> Savings</label>
            </div>
          </div>
          <div>
            <div className="wf-field-label">Routing #</div>
            <div className="wf-field dashed mono">XXXXXX681</div>
          </div>
          <div>
            <div className="wf-field-label">Account #</div>
            <div className="wf-field dashed mono">XXXXXX747</div>
          </div>
        </div>
      </div>

      <div className="wf-grid-2">
        <div className="wf-card lav">
          <div className="wf-h2">Next charge</div>
          <div style={{display:'flex', justifyContent:'space-between', fontSize:12}}>
            <span>Date</span><b>06/01/2026</b>
          </div>
          <div style={{display:'flex', justifyContent:'space-between', fontSize:12}}>
            <span>Amount</span><b className="mono">$35.00</b>
          </div>
          <div style={{display:'flex', justifyContent:'space-between', fontSize:12}}>
            <span>Processing fee</span><b className="mono">$1.95</b>
          </div>
        </div>
        <div className="wf-card dashed">
          <div className="wf-h2">Authorization</div>
          <p className="wf-sub" style={{fontSize:11, lineHeight:1.6}}>
            By enrolling, you authorize NekoHOA to withdraw the amount above
            from the bank on file each month, until cancelled.
          </p>
          <label style={{display:'flex', gap:8, fontSize:12, marginTop:8}}>
            <span className="wf-check on"></span> I agree to the ACH agreement
          </label>
        </div>
      </div>

      <div style={{display:'flex', gap:8, alignSelf:'flex-end'}}>
        <div className="wf-btn ghost">Cancel</div>
        <div className="wf-btn primary">Save changes</div>
      </div>
    </WFShell>
  );
}

function RecurringB() {
  const [method, setMethod] = React.useState('ach'); // 'ach' | 'card'

  const drafts = [
    { date: '05/02/26', source: method === 'ach' ? 'Fidelity ••747' : 'Visa ••4242', amount: '$36.95', status: 'Paid' },
    { date: '06/02/26', source: method === 'ach' ? 'Fidelity ••747' : 'Visa ••4242', amount: '$36.95', status: 'Scheduled' },
    { date: '07/02/26', source: method === 'ach' ? 'Fidelity ••747' : 'Visa ••4242', amount: '$36.95', status: 'Scheduled' },
    { date: '08/02/26', source: method === 'ach' ? 'Fidelity ••747' : 'Visa ••4242', amount: '$36.95', status: 'Scheduled' },
  ];

  return (
    <WFShellSide sideActive="Recurring">
      <div style={{display:'flex', alignItems:'baseline'}}>
        <h1 className="wf-h1">Auto-pay</h1>
        <span className="wf-sub" style={{marginLeft:10}}>set it & forget it</span>
        <div className="wf-toggle on" style={{marginLeft:'auto'}}></div>
      </div>

      <div className="wf-card lav" style={{display:'grid', gridTemplateColumns:'1fr 1fr 1fr', gap:14}}>
        <div><div className="wf-field-label">Status</div><div style={{fontWeight:600}}>Active</div></div>
        <div><div className="wf-field-label">Next draft</div><div style={{fontWeight:600}}>Jun 2 · $36.95</div></div>
        <div><div className="wf-field-label">Source</div><div style={{fontWeight:600}}>{method === 'ach' ? 'Fidelity ••747' : 'Visa ••4242'}</div></div>
      </div>

      <div className="wf-grid-2">
        <div className="wf-card">
          <div className="wf-h2">What gets paid</div>
          <div style={{display:'flex', flexDirection:'column', gap:8, fontSize:12, marginTop:6}}>
            {[
              ['Just the assessment', true, '$35/mo'],
              ['Whatever I owe (open balance)', false, 'variable'],
              ['A fixed amount I pick', false, '$ ____'],
            ].map(([t,sel,sub],i) => (
              <div key={i} style={{
                padding:'10px 12px', borderRadius:10,
                border:'1.5px solid', borderColor: sel ? 'var(--ink)' : 'var(--line)',
                background: sel ? 'var(--pink)' : 'var(--paper)',
                display:'flex', alignItems:'center', gap:10,
              }}>
                <span className={`wf-radio ${sel ? 'on' : ''}`}></span>
                <div style={{flex:1}}>
                  <div style={{fontWeight: sel ? 600 : 400}}>{t}</div>
                  <div className="wf-sub" style={{fontSize:11}}>{sub}</div>
                </div>
              </div>
            ))}
          </div>
        </div>

        <div className="wf-card">
          <div style={{display:'flex', alignItems:'baseline', marginBottom:10}}>
            <div className="wf-h2" style={{margin:0}}>When &amp; where</div>
          </div>

          <div className="wf-field-label">How to pay</div>
          <div style={{
            display:'grid', gridTemplateColumns:'1fr 1fr', gap:6, padding:3,
            background:'var(--lav)', borderRadius:10, border:'1.5px solid var(--line)',
            marginBottom:14,
          }}>
            <button
              onClick={() => setMethod('ach')}
              style={{
                cursor:'pointer', border:'none', padding:'8px 10px', borderRadius:7,
                background: method === 'ach' ? 'var(--paper)' : 'transparent',
                fontWeight: method === 'ach' ? 600 : 400,
                color: method === 'ach' ? 'var(--ink)' : 'var(--ink-soft)',
                boxShadow: method === 'ach' ? '0 1px 2px rgba(0,0,0,0.05)' : 'none',
                fontFamily:'inherit', fontSize:12,
              }}
            >🏦 Bank (ACH)</button>
            <button
              onClick={() => setMethod('card')}
              style={{
                cursor:'pointer', border:'none', padding:'8px 10px', borderRadius:7,
                background: method === 'card' ? 'var(--paper)' : 'transparent',
                fontWeight: method === 'card' ? 600 : 400,
                color: method === 'card' ? 'var(--ink)' : 'var(--ink-soft)',
                boxShadow: method === 'card' ? '0 1px 2px rgba(0,0,0,0.05)' : 'none',
                fontFamily:'inherit', fontSize:12,
              }}
            >💳 Credit card</button>
          </div>

          <div className="wf-field-label">Draft date</div>
          <div className="wf-field dashed">2nd of each month ▾</div>

          {method === 'ach' ? (
            <div style={{marginTop:12, display:'flex', flexDirection:'column', gap:10}}>
              <div>
                <div className="wf-field-label">Bank name</div>
                <div className="wf-field dashed">Fidelity Investments</div>
              </div>
              <div className="wf-grid-2" style={{gap:8}}>
                <div>
                  <div className="wf-field-label">Routing #</div>
                  <div className="wf-field dashed mono">XXXXXX681</div>
                </div>
                <div>
                  <div className="wf-field-label">Account #</div>
                  <div className="wf-field dashed mono">XXXXXX747</div>
                </div>
              </div>
              <div>
                <div className="wf-field-label">Account type</div>
                <div style={{display:'flex', gap:14, fontSize:12, marginTop:4}}>
                  <label style={{display:'flex', gap:6, alignItems:'center'}}><span className="wf-radio on"></span> Checking</label>
                  <label style={{display:'flex', gap:6, alignItems:'center'}}><span className="wf-radio"></span> Savings</label>
                </div>
              </div>
              <div className="wf-card pink" style={{padding:'8px 10px', fontSize:11, marginTop:4}}>
                $1.95 processing fee per draft.
              </div>
            </div>
          ) : (
            <div style={{marginTop:12, display:'flex', flexDirection:'column', gap:10}}>
              <div>
                <div className="wf-field-label">Cardholder name</div>
                <div className="wf-field dashed">Nicholas Bonilla</div>
              </div>
              <div>
                <div className="wf-field-label">Card number</div>
                <div className="wf-field dashed mono">4242 4242 4242 ____</div>
              </div>
              <div className="wf-grid-3" style={{gap:8}}>
                <div>
                  <div className="wf-field-label">Expires</div>
                  <div className="wf-field dashed mono">MM/YY</div>
                </div>
                <div>
                  <div className="wf-field-label">CVC</div>
                  <div className="wf-field dashed mono">•••</div>
                </div>
                <div>
                  <div className="wf-field-label">ZIP</div>
                  <div className="wf-field dashed mono">27560</div>
                </div>
              </div>
              <div className="wf-card pink" style={{padding:'8px 10px', fontSize:11, marginTop:4}}>
                3% processing fee per draft · charged at time of payment.
              </div>
            </div>
          )}
        </div>
      </div>

      <div className="wf-card dashed">
        <div style={{display:'flex', alignItems:'baseline'}}>
          <div className="wf-h2" style={{margin:0}}>Drafts</div>
          <span className="wf-sub" style={{marginLeft:8, fontSize:11}}>past &amp; scheduled</span>
        </div>
        <table className="wf-table">
          <thead><tr><th>Date</th><th>Source</th><th className="num">Amount</th><th>Status</th></tr></thead>
          <tbody>
            {drafts.map((d,i) => (
              <tr key={i}>
                <td>{d.date}</td>
                <td>{d.source}</td>
                <td className="num">{d.amount}</td>
                <td>
                  <span className={`wf-pill ${d.status === 'Paid' ? 'ok' : ''}`}>{d.status}</span>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <span className="wf-note violet" style={{alignSelf:'flex-end'}}>plain-language toggle <span className="arrow"></span></span>
    </WFShellSide>
  );
}

Object.assign(window, { PaymentA, PaymentB, RecurringA, RecurringB });
