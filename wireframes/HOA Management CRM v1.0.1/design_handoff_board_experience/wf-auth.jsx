// Portal selection + Login wireframes (2 variants each)

// ── Portal Selection ──────────────────────────────────────────
function PortalSelectA() {
  const portals = [
    { t: 'Resident', d: 'Pay dues, view violations, your account.' },
    { t: 'Board / Mgmt', d: 'Run the community, review reports.' },
    { t: 'Vendor', d: 'Submit invoices and work orders.' },
    { t: 'Closing', d: 'Statements of account, estoppels.' },
    { t: 'Attorney', d: 'Referred accounts and statements.' },
  ];
  return (
    <div className="wf" style={{padding: 32, justifyContent:'flex-start', background:'linear-gradient(180deg, var(--pink) 0%, var(--lav) 60%, var(--paper) 100%)'}}>
      <div style={{display:'flex', alignItems:'center', gap:10, justifyContent:'center', marginBottom: 20}}>
        <span className="wf-logo-mark"></span>
        <span style={{fontFamily:'Caveat', fontSize:30, fontWeight:700}}>NekoHOA</span>
      </div>
      <h1 className="wf-h1" style={{textAlign:'center'}}>Choose your <span className="hand">portal</span></h1>
      <p className="wf-sub" style={{textAlign:'center', marginBottom:24}}>Pick the role that matches you.</p>
      <div className="wf-grid-3" style={{gap:14, padding: '0 16px'}}>
        {portals.slice(0,3).map(p => (
          <div key={p.t} className="wf-card" style={{minHeight: 150, display:'flex', flexDirection:'column', gap:10}}>
            <div className="wf-ph" style={{width:40, height:40, borderRadius:10}}>◈</div>
            <div style={{fontWeight:600}}>{p.t}</div>
            <div className="wf-sub" style={{flex:1}}>{p.d}</div>
            <div className="wf-link">Get started →</div>
          </div>
        ))}
      </div>
      <div className="wf-grid-2" style={{gap:14, padding:'14px 16% 0'}}>
        {portals.slice(3).map(p => (
          <div key={p.t} className="wf-card" style={{minHeight: 130, display:'flex', flexDirection:'column', gap:10}}>
            <div className="wf-ph" style={{width:40, height:40, borderRadius:10}}>◈</div>
            <div style={{fontWeight:600}}>{p.t}</div>
            <div className="wf-sub" style={{flex:1}}>{p.d}</div>
            <div className="wf-link">Get started →</div>
          </div>
        ))}
      </div>
      <div style={{marginTop:'auto', textAlign:'center', padding:16, color:'var(--ink-mute)', fontSize:11}}>
        privacy · terms · support@nekohoa.com
      </div>
      <span className="wf-note" style={{position:'absolute', top:120, right:60}}>
        soft pastel hero <span className="arrow"></span>
      </span>
    </div>
  );
}

function PortalSelectB() {
  const portals = [
    { t: 'Resident', d: 'Your account, payments, requests.', big: true },
    { t: 'Board / Mgmt', d: 'Manage the community.' },
    { t: 'Vendor', d: 'Invoices & work orders.' },
    { t: 'Closing', d: 'Estoppels & statements.' },
    { t: 'Attorney', d: 'Referred accounts.' },
  ];
  return (
    <div className="wf" style={{padding: 0, background: 'var(--paper)'}}>
      <div style={{padding:'18px 28px', borderBottom:'1.5px dashed var(--line)', display:'flex', alignItems:'center', gap:10}}>
        <span className="wf-logo-mark"></span>
        <span style={{fontFamily:'Caveat', fontSize:24, fontWeight:700}}>NekoHOA</span>
        <span style={{marginLeft:'auto', fontSize:12, color:'var(--ink-soft)'}}>Portal selection</span>
      </div>
      <div style={{flex:1, display:'grid', gridTemplateColumns:'1.2fr 1fr', gap:0}}>
        <div style={{padding:'40px 36px', background:'var(--pink)', display:'flex', flexDirection:'column', justifyContent:'center'}}>
          <div className="wf-note">welcome back <span className="arrow"></span></div>
          <h1 className="wf-h1" style={{fontSize: 36, lineHeight:1.1, margin:'8px 0 12px'}}>
            Pick the <span className="hand">door</span> you came through.
          </h1>
          <p style={{color:'var(--ink-soft)', maxWidth: 320}}>Five portals, one community. Most folks want the Resident door.</p>
          <div className="wf-ph" style={{marginTop:24, height:160, maxWidth:340}}>illustrated houses</div>
        </div>
        <div style={{padding:'40px 28px', display:'flex', flexDirection:'column', gap:10, justifyContent:'center'}}>
          {portals.map((p, i) => (
            <div key={p.t} className="wf-card" style={{
              display:'flex', alignItems:'center', gap:14, padding:'14px 16px',
              background: p.big ? 'var(--lav)' : 'var(--paper)',
              borderColor: p.big ? 'var(--ink)' : 'var(--line)',
            }}>
              <div className="wf-ph" style={{width:36, height:36, flexShrink:0, borderRadius:8}}>◇</div>
              <div style={{flex:1}}>
                <div style={{fontWeight:600}}>{p.t} {p.big && <span className="wf-pill" style={{marginLeft:6}}>most popular</span>}</div>
                <div className="wf-sub">{p.d}</div>
              </div>
              <div style={{color:'var(--rose)', fontSize:18}}>→</div>
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}

// ── Login ─────────────────────────────────────────────────────
function LoginA() {
  return (
    <div className="wf" style={{padding:'32px 36px', background:'linear-gradient(135deg, var(--pink) 0%, var(--lav) 100%)'}}>
      <div style={{display:'flex', alignItems:'center', gap:10}}>
        <span className="wf-logo-mark"></span>
        <span style={{fontFamily:'Caveat', fontSize:24, fontWeight:700}}>NekoHOA</span>
        <span style={{marginLeft:'auto', fontSize:12, color:'var(--ink-soft)'}}>Resident portal</span>
      </div>
      <div style={{flex:1, display:'grid', gridTemplateColumns:'1fr 1fr', gap:24, alignItems:'center', marginTop:20}}>
        <div className="wf-card" style={{padding:'28px 26px'}}>
          <div className="wf-note">login card <span className="arrow"></span></div>
          <h1 className="wf-h1" style={{margin:'4px 0 16px'}}>Welcome <span className="hand">home</span></h1>
          <div style={{display:'flex', flexDirection:'column', gap:10}}>
            <div>
              <div className="wf-field-label">Username or email</div>
              <div className="wf-field dashed">you@example.com</div>
            </div>
            <div>
              <div className="wf-field-label">Password</div>
              <div className="wf-field dashed">••••••••</div>
            </div>
            <div style={{display:'flex', alignItems:'center', gap:8, fontSize:12}}>
              <span className="wf-check"></span> Remember me
              <span className="wf-link" style={{marginLeft:'auto'}}>Reset password</span>
            </div>
            <div className="wf-btn primary" style={{justifyContent:'center', padding:'10px 14px'}}>Sign in</div>
            <div style={{textAlign:'center', color:'var(--ink-mute)', fontSize:11}}>or</div>
            <div className="wf-btn" style={{justifyContent:'center', padding:'10px 14px'}}>Continue with Google</div>
            <div style={{textAlign:'center', fontSize:12, marginTop:6}}>
              No account? <span className="wf-link">Register</span>
            </div>
          </div>
        </div>
        <div style={{display:'flex', flexDirection:'column', gap:14}}>
          <div className="wf-card pink">
            <div className="wf-h2">Quick pay</div>
            <p className="wf-sub" style={{marginBottom:10}}>Pay your dues without logging in. Just enter your 16-digit account number.</p>
            <div className="wf-field dashed">acct # _ _ _ _ - _ _ _ _ - _ _ _ _ - _ _ _ _</div>
            <div className="wf-btn" style={{marginTop:10}}>Pay now →</div>
          </div>
          <div className="wf-card dashed">
            <div className="wf-h2">Need help?</div>
            <ul style={{margin:0, paddingLeft:18, color:'var(--ink-soft)', fontSize:12, lineHeight:1.7}}>
              <li>Forgot your username? <span className="wf-link">Retrieve it</span></li>
              <li>Validate an email <span className="wf-link">here</span></li>
              <li>Email support@nekohoa.com</li>
            </ul>
          </div>
        </div>
      </div>
    </div>
  );
}

function LoginB() {
  return (
    <div className="wf" style={{padding:0}}>
      <div style={{flex:1, display:'grid', gridTemplateColumns:'1fr 1fr'}}>
        <div style={{background:'var(--lav)', padding:'40px 38px', display:'flex', flexDirection:'column'}}>
          <div style={{display:'flex', alignItems:'center', gap:10}}>
            <span className="wf-logo-mark"></span>
            <span style={{fontFamily:'Caveat', fontSize:24, fontWeight:700}}>NekoHOA</span>
          </div>
          <div style={{flex:1, display:'flex', flexDirection:'column', justifyContent:'center', maxWidth: 360}}>
            <div className="wf-note violet">single column · big type</div>
            <h1 style={{fontSize:38, lineHeight:1.05, margin:'8px 0 20px', letterSpacing:'-0.02em'}}>
              Sign in to your <span className="hand" style={{color:'var(--violet)'}}>community.</span>
            </h1>
            <p className="wf-sub" style={{marginBottom: 24, fontSize:13}}>
              The Sakura Heights resident portal — bills, violations, neighbors, all in one place.
            </p>
            <div className="wf-ph" style={{height: 120, maxWidth: 320}}>placeholder · neighborhood scene</div>
          </div>
          <div style={{fontSize:11, color:'var(--ink-mute)'}}>powered by NekoHOA · v1.0</div>
        </div>
        <div style={{padding:'40px 38px', display:'flex', flexDirection:'column', justifyContent:'center', maxWidth: 380}}>
          <div className="wf-h2" style={{fontSize:13, color:'var(--ink-soft)', textTransform:'uppercase', letterSpacing:'0.1em'}}>Sign in</div>
          <div style={{display:'flex', flexDirection:'column', gap:14, marginTop:14}}>
            <div className="wf-btn" style={{justifyContent:'center', padding:'10px 14px'}}>◯  Continue with Google</div>
            <div style={{display:'flex', alignItems:'center', gap:8, color:'var(--ink-mute)', fontSize:11}}>
              <hr className="wf-divider" style={{flex:1}}/> or with email <hr className="wf-divider" style={{flex:1}}/>
            </div>
            <div>
              <div className="wf-field-label">Email</div>
              <div className="wf-field dashed">you@example.com</div>
            </div>
            <div>
              <div className="wf-field-label">Password</div>
              <div className="wf-field dashed">••••••••</div>
            </div>
            <div className="wf-btn primary" style={{justifyContent:'center', padding:'10px 14px'}}>Sign in →</div>
            <div style={{display:'flex', justifyContent:'space-between', fontSize:12}}>
              <span className="wf-link">Forgot password</span>
              <span className="wf-link">Quick pay (no login)</span>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}

// ── Registration ─────────────────────────────────────────────
function Registration() {
  return (
    <div className="wf" style={{padding:0}}>
      <div style={{padding:'18px 28px', borderBottom:'1.5px dashed var(--line)', display:'flex', alignItems:'center', gap:10}}>
        <span className="wf-logo-mark"></span>
        <span style={{fontFamily:'Caveat', fontSize:24, fontWeight:700}}>NekoHOA</span>
        <span style={{marginLeft:'auto', fontSize:12, color:'var(--ink-soft)'}}>Resident registration</span>
      </div>

      <div style={{display:'flex', alignItems:'center', gap:10, padding:'16px 36px 8px', fontSize:11, color:'var(--ink-soft)'}}>
        <span className="wf-pill" style={{background:'var(--rose)', color:'var(--paper)', borderColor:'var(--ink)'}}>1 find property</span>
        <span style={{flex:'0 0 40px', borderTop:'1.5px dashed var(--line)'}}></span>
        <span className="wf-pill">2 confirm</span>
        <span style={{flex:'0 0 40px', borderTop:'1.5px dashed var(--line)'}}></span>
        <span className="wf-pill">3 create login</span>
      </div>

      <div style={{flex:1, display:'grid', gridTemplateColumns:'1fr 1fr', gap:24, padding:'12px 36px 32px', alignItems:'flex-start'}}>
        <div className="wf-col">
          <div>
            <h1 className="wf-h1" style={{margin:0}}>Find your <span className="hand">property</span></h1>
            <p className="wf-sub" style={{marginTop:4}}>Enter your address or 16-digit account number to get started.</p>
          </div>

          <div className="wf-card">
            <div className="wf-h2">Look up by address</div>
            <div className="wf-field-label">Street address</div>
            <div className="wf-field dashed">714 Keystone Park Dr</div>
            <div className="wf-grid-3" style={{marginTop:10, gap:8}}>
              <div>
                <div className="wf-field-label">City</div>
                <div className="wf-field dashed">Morrisville</div>
              </div>
              <div>
                <div className="wf-field-label">State</div>
                <div className="wf-field dashed">NC ▾</div>
              </div>
              <div>
                <div className="wf-field-label">ZIP</div>
                <div className="wf-field dashed">27560</div>
              </div>
            </div>
          </div>

          <div style={{textAlign:'center', color:'var(--ink-mute)', fontSize:11, display:'flex', alignItems:'center', gap:8}}>
            <hr className="wf-divider" style={{flex:1}}/> or <hr className="wf-divider" style={{flex:1}}/>
          </div>

          <div className="wf-card dashed">
            <div className="wf-h2">Look up by account number</div>
            <p className="wf-sub" style={{fontSize:11, marginTop:2}}>Find it on the bottom of any paper statement.</p>
            <div className="wf-field dashed mono" style={{marginTop:8}}>R _ _ _ _ _ _ _ L _ _ _ _ _ _ _</div>
          </div>

          <div className="wf-btn primary" style={{justifyContent:'center', padding:'10px 14px'}}>Find my property →</div>
        </div>

        <div className="wf-col">
          <div className="wf-note">we'll confirm what we found <span className="arrow"></span></div>

          <div className="wf-card lav">
            <div style={{display:'flex', alignItems:'center', gap:8}}>
              <span className="wf-pill ok">✓ Property found</span>
              <span className="wf-sub" style={{marginLeft:'auto', fontSize:11}}>1 match</span>
            </div>
            <div style={{display:'flex', gap:14, marginTop:14, alignItems:'flex-start'}}>
              <div className="wf-ph" style={{width:80, height:80, borderRadius:10, flexShrink:0}}>🏠</div>
              <div style={{flex:1}}>
                <div style={{fontSize:16, fontWeight:600}}>714 Keystone Park Dr</div>
                <div className="wf-sub">Morrisville, NC 27560</div>
                <div style={{display:'flex', gap:6, marginTop:8, flexWrap:'wrap'}}>
                  <span className="wf-pill">Sakura Heights</span>
                  <span className="wf-pill">Lot 151</span>
                  <span className="wf-pill">Active</span>
                </div>
              </div>
            </div>

            <hr className="wf-divider" style={{margin:'14px 0'}}/>

            <div style={{fontSize:12, fontWeight:500, marginBottom:6}}>Is this your property?</div>
            <div style={{display:'flex', gap:8}}>
              <div className="wf-btn primary" style={{flex:1, justifyContent:'center'}}>Yes, that's me →</div>
              <div className="wf-btn ghost">Not mine</div>
            </div>
          </div>

          <div className="wf-card dashed">
            <div className="wf-h2">🔒 Owner verification</div>
            <p className="wf-sub" style={{fontSize:11, lineHeight:1.6}}>
              After confirming, we'll match what you enter against the owner on
              file. If it doesn't match, we can mail a one-time PIN to the
              property address.
            </p>
          </div>

          <div style={{fontSize:12, textAlign:'center'}}>
            Already registered? <span className="wf-link">Sign in instead</span>
          </div>
        </div>
      </div>
    </div>
  );
}

Object.assign(window, { PortalSelectA, PortalSelectB, LoginA, LoginB, Registration });
