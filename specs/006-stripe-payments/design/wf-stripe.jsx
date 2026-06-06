// Stripe-based redesign of the one-time + recurring billing pages.
// Core idea across every variant: NekoHOA stops collecting raw card / bank
// numbers in its own form. Stripe collects + vaults them; we keep only a
// token + a "•• last4" label.

// ── shared bits ───────────────────────────────────────────────
function StripeWord({ size = 12 }) {
  return <b style={{fontWeight:600, color:'var(--ink)', letterSpacing:'-0.02em', fontSize:size}}>stripe</b>;
}

function PoweredByStripe({ subtle = false }) {
  return (
    <span style={{display:'inline-flex', alignItems:'center', gap:5, fontSize:11, color: subtle ? 'var(--ink-mute)' : 'var(--ink-soft)'}}>
      <span style={{fontSize:11}}>🔒</span> Powered by <StripeWord size={12}/>
    </span>
  );
}

// A bordered region that reads as "this is Stripe's iframe, not our form".
function StripeRegion({ title = 'Secure payment', children, foot }) {
  return (
    <div style={{
      border:'1.5px solid var(--ink)', borderRadius:14, overflow:'hidden',
      background:'var(--paper)', position:'relative',
    }}>
      <div style={{
        position:'absolute', top:-9, right:12, background:'var(--lav-2)',
        border:'1.5px solid var(--ink)', borderRadius:999, padding:'1px 10px',
        fontSize:10, fontFamily:'Geist Mono', letterSpacing:'.02em',
      }}>stripe iframe</div>
      <div style={{
        display:'flex', alignItems:'center', gap:8, padding:'10px 14px',
        background:'var(--lav)', borderBottom:'1.5px solid var(--line)',
      }}>
        <span>🔒</span>
        <b style={{fontSize:12.5}}>{title}</b>
        <span style={{marginLeft:'auto'}}><PoweredByStripe subtle/></span>
      </div>
      <div style={{padding:14}}>{children}</div>
      {foot && <div style={{padding:'10px 14px', borderTop:'1.5px dashed var(--line)', fontSize:11, color:'var(--ink-soft)'}}>{foot}</div>}
    </div>
  );
}

// Stripe Payment Element: method tabs + the fields Stripe renders for the
// active tab. Fields are SOLID (Stripe owns them) vs our dashed placeholders.
function PaymentElement({ active = 'card' }) {
  const tabs = [['card','💳 Card'], ['bank','🏦 US bank'], ['wallet','🍎 Wallet']];
  return (
    <div style={{display:'flex', flexDirection:'column', gap:12}}>
      <div style={{display:'grid', gridTemplateColumns:'1fr 1fr 1fr', gap:8}}>
        {tabs.map(([k,l]) => (
          <div key={k} style={{
            textAlign:'center', padding:'9px 6px', borderRadius:10, fontSize:12,
            border:'1.5px solid', borderColor: k===active ? 'var(--ink)' : 'var(--line)',
            background: k===active ? 'var(--pink)' : 'var(--paper)',
            fontWeight: k===active ? 600 : 400,
          }}>{l}</div>
        ))}
      </div>
      {active === 'card' ? (
        <>
          <div>
            <div className="wf-field-label">Card number</div>
            <div className="wf-field mono" style={{justifyContent:'space-between'}}>
              <span>1234 1234 1234 1234</span>
              <span style={{display:'flex', gap:4, fontSize:14}}>💳</span>
            </div>
          </div>
          <div className="wf-grid-2" style={{gap:8}}>
            <div><div className="wf-field-label">Expiry</div><div className="wf-field mono">MM / YY</div></div>
            <div><div className="wf-field-label">CVC</div><div className="wf-field mono">CVC</div></div>
          </div>
        </>
      ) : active === 'bank' ? (
        <>
          <div className="wf-field" style={{justifyContent:'space-between'}}>
            <span>🏦 &nbsp;Search for your bank</span><span>▾</span>
          </div>
          <div className="wf-grid-3" style={{gap:8}}>
            {['Chase','B of A','Wells','Fidelity','US Bank','Other'].map(b => (
              <div key={b} style={{textAlign:'center', padding:'10px 6px', border:'1.5px solid var(--line)', borderRadius:10, fontSize:11.5}}>{b}</div>
            ))}
          </div>
          <div style={{fontSize:11, color:'var(--ink-soft)'}}>Connects instantly via Stripe Financial Connections — no routing / account numbers typed.</div>
        </>
      ) : (
        <div style={{display:'flex', flexDirection:'column', gap:8}}>
          <div className="wf-btn" style={{justifyContent:'center', padding:'12px', background:'var(--ink)', color:'var(--paper)'}}>🍎  Pay</div>
          <div className="wf-btn" style={{justifyContent:'center', padding:'12px'}}>G  Pay</div>
        </div>
      )}
    </div>
  );
}

// A vaulted method row (what we keep after Stripe collects: brand + last4).
function VaultedMethod({ icon, label, meta, sel, badge }) {
  return (
    <div style={{
      display:'flex', alignItems:'center', gap:12, padding:'12px 14px', borderRadius:12,
      border:'1.5px solid', borderColor: sel ? 'var(--ink)' : 'var(--line)',
      background: sel ? 'var(--pink)' : 'var(--paper)',
    }}>
      <span className={`wf-radio ${sel ? 'on' : ''}`}></span>
      <span style={{fontSize:18}}>{icon}</span>
      <div style={{flex:1}}>
        <div style={{fontWeight: sel ? 600 : 500, fontSize:13}}>{label}</div>
        <div className="wf-sub" style={{fontSize:11}}>{meta}</div>
      </div>
      {badge && <span className="wf-pill ok" style={{fontSize:10}}>{badge}</span>}
    </div>
  );
}

// Minimal browser chrome to show a Stripe-HOSTED page (the redirect target).
function BrowserChrome({ url, children }) {
  return (
    <div style={{height:'100%', display:'flex', flexDirection:'column', background:'var(--lav)', borderRadius:12, overflow:'hidden', border:'1.5px solid var(--line)'}}>
      <div style={{display:'flex', alignItems:'center', gap:10, padding:'10px 14px', borderBottom:'1.5px solid var(--line)', background:'var(--paper)'}}>
        <span style={{display:'flex', gap:6}}>
          {['#e7a6b6','#e9d39a','#a9d8b0'].map(c => <span key={c} style={{width:11, height:11, borderRadius:'50%', background:c, border:'1.5px solid var(--ink)'}}></span>)}
        </span>
        <div style={{flex:1, display:'flex', alignItems:'center', gap:8, padding:'6px 12px', borderRadius:999, background:'var(--lav)', border:'1.5px solid var(--line)'}}>
          <span style={{fontSize:11}}>🔒</span>
          <span className="mono" style={{fontSize:11, color:'var(--ink-soft)'}}>{url}</span>
        </div>
      </div>
      <div style={{flex:1, overflow:'auto', display:'grid', placeItems:'center', padding:'26px 20px'}}>
        {children}
      </div>
    </div>
  );
}

// ══════════════════════════════════════════════════════════════
// ONE-TIME · IDEA A — hosted checkout redirect
// Our page only picks the amount, then hands off to Stripe Checkout.
// ══════════════════════════════════════════════════════════════
function StripeOneTimeRedirect() {
  return (
    <WFShellSide sideActive="One-time">
      <h1 className="wf-h1">Make a <span className="hand">one-time</span> payment</h1>
      <p className="wf-sub" style={{marginTop:-6}}>Pick what to pay — we hand the rest to Stripe’s secure checkout.</p>

      <div style={{display:'grid', gridTemplateColumns:'1.4fr 1fr', gap:14}}>
        <div className="wf-col">
          <div className="wf-card">
            <div className="wf-h2">Choose amount</div>
            <div style={{display:'flex', flexDirection:'column', gap:8, fontSize:13}}>
              {[
                ['Current balance','as of today','$35.00', true],
                ['Next assessment','due 6/1','$35.00', false],
                ['Balance + next','paid through July','$70.00', false],
              ].map(([t,s,a,sel],i) => (
                <label key={i} style={{display:'flex', gap:10, alignItems:'center', padding:'10px 12px', borderRadius:10, border:'1.5px solid', borderColor: sel?'var(--ink)':'var(--line)', background: sel?'var(--pink)':'var(--paper)'}}>
                  <span className={`wf-radio ${sel?'on':''}`}></span>
                  <span style={{flex:1}}>{t} <span className="wf-sub">{s}</span></span>
                  <b className="mono">{a}</b>
                </label>
              ))}
              <label style={{display:'flex', gap:10, alignItems:'center', padding:'4px 12px'}}>
                <span className="wf-radio"></span>
                <span style={{flex:1}}>Other amount</span>
                <div className="wf-field dashed mono" style={{width:120}}>$ ____</div>
              </label>
            </div>
          </div>

          <div className="wf-card lav" style={{display:'flex', gap:10, alignItems:'flex-start'}}>
            <span style={{fontSize:15}}>🔒</span>
            <div style={{fontSize:12, lineHeight:1.6}}>
              <b>NekoHOA never sees your card or bank number.</b> Tapping continue opens Stripe’s
              PCI-certified checkout. You return here automatically when it’s done.
            </div>
          </div>
        </div>

        <div className="wf-col">
          <div className="wf-card lav" style={{position:'sticky', top:0}}>
            <div className="wf-h2">Summary</div>
            <div style={{display:'flex', justifyContent:'space-between', fontSize:12, padding:'4px 0'}}><span>Amount</span><b className="mono">$35.00</b></div>
            <div style={{display:'flex', justifyContent:'space-between', fontSize:12, padding:'4px 0'}}><span>Card fee <span className="wf-sub">(if card)</span></span><b className="mono">$1.05</b></div>
            <hr className="wf-divider"/>
            <div style={{display:'flex', justifyContent:'space-between', padding:'8px 0'}}><b>Total</b><b className="mono" style={{fontSize:18}}>$36.05</b></div>
            <div className="wf-btn primary" style={{justifyContent:'center', width:'100%', padding:'11px'}}>Continue to secure checkout →</div>
            <div style={{display:'flex', justifyContent:'center', marginTop:10}}><PoweredByStripe/></div>
          </div>
          <div className="wf-card dashed" style={{fontSize:11, color:'var(--ink-soft)', lineHeight:1.6}}>
            Fee is only added if you choose a card inside Stripe. Bank (ACH) is free — Stripe shows the
            exact total before you confirm.
          </div>
        </div>
      </div>
      <span className="wf-note" style={{alignSelf:'flex-end'}}>A · amount on us, payment on Stripe <span className="arrow"></span></span>
    </WFShellSide>
  );
}

// The Stripe-HOSTED checkout page the redirect lands on (browser-framed).
function StripeHostedCheckout() {
  return (
    <div className="wf" style={{background:'var(--lav)', padding:16}}>
      <BrowserChrome url="checkout.stripe.com/c/pay/nekohoa…">
        <div style={{width:380, maxWidth:'100%', background:'var(--paper)', border:'1.5px solid var(--ink)', borderRadius:16, overflow:'hidden'}}>
          <div style={{padding:'16px 18px', borderBottom:'1.5px dashed var(--line)', display:'flex', alignItems:'center', gap:10}}>
            <span className="wf-logo-mark"></span>
            <div>
              <div style={{fontWeight:600}}>NekoHOA</div>
              <div className="wf-sub" style={{fontSize:11}}>Sakura Heights · assessment</div>
            </div>
            <b className="mono" style={{marginLeft:'auto', fontSize:18}}>$36.05</b>
          </div>
          <div style={{padding:18, display:'flex', flexDirection:'column', gap:14}}>
            <div>
              <div className="wf-field-label">Email</div>
              <div className="wf-field">nicholas.b@email.com</div>
            </div>
            <PaymentElement active="card"/>
            <div>
              <div className="wf-field-label">Name on card</div>
              <div className="wf-field">Nicholas Bonilla</div>
            </div>
            <label style={{display:'flex', gap:8, alignItems:'center', fontSize:12}}>
              <span className="wf-check on"></span> Save this card for faster payments next time
            </label>
            <div className="wf-btn primary" style={{justifyContent:'center', padding:'13px', fontSize:14}}>Pay $36.05</div>
            <div style={{display:'flex', justifyContent:'center'}}><PoweredByStripe/></div>
          </div>
        </div>
      </BrowserChrome>
      <span className="wf-note violet" style={{position:'absolute', right:24, bottom:14}}>↑ the redirect target — fully hosted by Stripe</span>
    </div>
  );
}

// ══════════════════════════════════════════════════════════════
// ONE-TIME · IDEA B — embedded Payment Element (no redirect)
// ══════════════════════════════════════════════════════════════
function StripeOneTimeEmbedded() {
  const [tab, setTab] = React.useState('card');
  return (
    <WFShellSide sideActive="One-time">
      <h1 className="wf-h1">Pay your <span className="hand">dues</span></h1>
      <div style={{display:'grid', gridTemplateColumns:'1fr 1.1fr', gap:16}}>
        <div className="wf-col">
          <div className="wf-card">
            <div className="wf-h2">Amount</div>
            <div style={{display:'flex', flexDirection:'column', gap:8, fontSize:13}}>
              {[['Current balance','$35.00',true],['Next assessment','$35.00',false],['Both','$70.00',false]].map(([t,a,sel],i)=>(
                <label key={i} style={{display:'flex', gap:10, alignItems:'center'}}>
                  <span className={`wf-radio ${sel?'on':''}`}></span>
                  <span style={{flex:1}}>{t}</span><b className="mono">{a}</b>
                </label>
              ))}
            </div>
          </div>
          <div className="wf-card lav">
            <div style={{display:'flex', justifyContent:'space-between', fontSize:12, padding:'3px 0'}}><span>Amount</span><b className="mono">$35.00</b></div>
            <div style={{display:'flex', justifyContent:'space-between', fontSize:12, padding:'3px 0'}}><span>Fee <span className="wf-sub">{tab==='card'?'card 3%':'ACH free'}</span></span><b className="mono">{tab==='card'?'$1.05':'$0.00'}</b></div>
            <hr className="wf-divider"/>
            <div style={{display:'flex', justifyContent:'space-between', padding:'6px 0'}}><b>Total</b><b className="mono" style={{fontSize:17}}>{tab==='card'?'$36.05':'$35.00'}</b></div>
          </div>
          <div className="wf-card dashed" style={{fontSize:11, color:'var(--ink-soft)', lineHeight:1.6}}>
            The payment box on the right is rendered <b>by Stripe inside our page</b>. Card and bank
            details go straight to Stripe — they never reach NekoHOA’s servers. No redirect.
          </div>
        </div>

        <div className="wf-col">
          <StripeRegion title="Pay $35.00" foot={<span><PoweredByStripe/> · we store only a token + “•• last4”.</span>}>
            <div style={{marginBottom:12}}>
              <div className="wf-field-label">Email</div>
              <div className="wf-field">nicholas.b@email.com</div>
            </div>
            <div style={{display:'none'}}>{setTab && tab}</div>
            <PaymentElementInteractive tab={tab} setTab={setTab}/>
          </StripeRegion>
          <div className="wf-btn primary" style={{justifyContent:'center', padding:'12px', fontSize:14}}>{tab==='card'?'Pay $36.05':'Pay $35.00'}</div>
        </div>
      </div>
      <span className="wf-note violet" style={{alignSelf:'flex-end'}}>B · embedded element, stays on our page <span className="arrow"></span></span>
    </WFShellSide>
  );
}

function PaymentElementInteractive({ tab, setTab }) {
  const tabs = [['card','💳 Card'], ['bank','🏦 US bank'], ['wallet','🍎 Wallet']];
  return (
    <div style={{display:'flex', flexDirection:'column', gap:12}}>
      <div style={{display:'grid', gridTemplateColumns:'1fr 1fr 1fr', gap:8}}>
        {tabs.map(([k,l]) => (
          <button key={k} onClick={()=>setTab(k)} style={{
            cursor:'pointer', fontFamily:'inherit', textAlign:'center', padding:'9px 6px', borderRadius:10, fontSize:12,
            border:'1.5px solid', borderColor: k===tab ? 'var(--ink)' : 'var(--line)',
            background: k===tab ? 'var(--pink)' : 'var(--paper)', color:'var(--ink)',
            fontWeight: k===tab ? 600 : 400,
          }}>{l}</button>
        ))}
      </div>
      {tab === 'card' ? (
        <>
          <div><div className="wf-field-label">Card number</div><div className="wf-field mono" style={{justifyContent:'space-between'}}><span>1234 1234 1234 1234</span><span>💳</span></div></div>
          <div className="wf-grid-2" style={{gap:8}}>
            <div><div className="wf-field-label">Expiry</div><div className="wf-field mono">MM / YY</div></div>
            <div><div className="wf-field-label">CVC</div><div className="wf-field mono">CVC</div></div>
          </div>
        </>
      ) : tab === 'bank' ? (
        <>
          <div className="wf-field" style={{justifyContent:'space-between'}}><span>🏦 &nbsp;Search for your bank</span><span>▾</span></div>
          <div className="wf-grid-3" style={{gap:8}}>
            {['Chase','B of A','Wells','Fidelity','US Bank','Other'].map(b => (
              <div key={b} style={{textAlign:'center', padding:'10px 6px', border:'1.5px solid var(--line)', borderRadius:10, fontSize:11.5}}>{b}</div>
            ))}
          </div>
          <div style={{fontSize:11, color:'var(--ink-soft)'}}>Instant connect via Stripe Financial Connections — no routing/account typed.</div>
        </>
      ) : (
        <div style={{display:'flex', flexDirection:'column', gap:8}}>
          <div className="wf-btn" style={{justifyContent:'center', padding:'12px', background:'var(--ink)', color:'var(--paper)'}}>🍎  Pay</div>
          <div className="wf-btn" style={{justifyContent:'center', padding:'12px'}}>G  Pay</div>
        </div>
      )}
    </div>
  );
}

// ══════════════════════════════════════════════════════════════
// ONE-TIME · IDEA C — saved (vaulted) methods, one-tap
// ══════════════════════════════════════════════════════════════
function StripeOneTimeSaved() {
  return (
    <WFShellSide sideActive="One-time">
      <div style={{display:'flex', alignItems:'baseline', gap:10}}>
        <h1 className="wf-h1">One-tap <span className="hand">pay</span></h1>
        <span className="wf-sub">using a method on file</span>
      </div>

      <div style={{display:'grid', gridTemplateColumns:'1.2fr 1fr', gap:16}}>
        <div className="wf-col">
          <div className="wf-card">
            <div className="wf-h2">Amount</div>
            <div style={{display:'flex', gap:10}}>
              <div className="wf-card" style={{flex:1, textAlign:'center', borderColor:'var(--ink)', background:'var(--pink)', padding:'14px 8px'}}>
                <div className="wf-field-label">Current balance</div>
                <div className="mono" style={{fontSize:26, margin:'2px 0'}}>$35.00</div>
              </div>
              <div className="wf-card dashed" style={{flex:1, textAlign:'center', padding:'14px 8px'}}>
                <div className="wf-field-label">Other</div>
                <div className="wf-field dashed mono" style={{marginTop:6}}>$ ____</div>
              </div>
            </div>
          </div>

          <div className="wf-card">
            <div style={{display:'flex', alignItems:'baseline'}}>
              <div className="wf-h2" style={{margin:0}}>Pay with</div>
              <span className="wf-sub" style={{marginLeft:8, fontSize:11}}>saved in Stripe</span>
            </div>
            <div style={{display:'flex', flexDirection:'column', gap:8, marginTop:10}}>
              <VaultedMethod icon="🏦" label="Fidelity checking" meta="•• 747 · ACH · no fee" sel badge="default"/>
              <VaultedMethod icon="💳" label="Visa" meta="•• 4242 · exp 09/27 · 3% fee"/>
              <div className="wf-btn ghost" style={{justifyContent:'center', padding:'11px'}}>+ Add a payment method</div>
            </div>
            <p className="wf-sub" style={{fontSize:11, marginTop:10, lineHeight:1.6}}>
              “Add” opens a small Stripe sheet to capture & vault the method. We only keep the brand and
              last 4 digits shown above.
            </p>
          </div>
        </div>

        <div className="wf-col">
          <div className="wf-card lav" style={{position:'sticky', top:0}}>
            <div className="wf-h2">Review</div>
            <div style={{display:'flex', justifyContent:'space-between', fontSize:12, padding:'4px 0'}}><span>Amount</span><b className="mono">$35.00</b></div>
            <div style={{display:'flex', justifyContent:'space-between', fontSize:12, padding:'4px 0'}}><span>Method</span><b>Fidelity •• 747</b></div>
            <div style={{display:'flex', justifyContent:'space-between', fontSize:12, padding:'4px 0'}}><span>Fee</span><b className="mono">$0.00</b></div>
            <hr className="wf-divider"/>
            <div style={{display:'flex', justifyContent:'space-between', padding:'8px 0'}}><b>Total</b><b className="mono" style={{fontSize:18}}>$35.00</b></div>
            <div className="wf-btn primary" style={{justifyContent:'center', width:'100%', padding:'12px', fontSize:14}}>Pay $35.00</div>
            <div style={{display:'flex', justifyContent:'center', marginTop:10}}><PoweredByStripe/></div>
          </div>
        </div>
      </div>
      <span className="wf-note" style={{alignSelf:'flex-end'}}>C · vaulted methods, fastest repeat pay <span className="arrow"></span></span>
    </WFShellSide>
  );
}

// ══════════════════════════════════════════════════════════════
// RECURRING · IDEA A — auto-pay config ours, method-on-file via Stripe
// ══════════════════════════════════════════════════════════════
function StripeRecurringSaved() {
  return (
    <WFShellSide sideActive="Recurring">
      <div style={{display:'flex', alignItems:'baseline'}}>
        <h1 className="wf-h1">Auto-pay</h1>
        <span className="wf-sub" style={{marginLeft:10}}>set it & forget it</span>
        <div className="wf-toggle on" style={{marginLeft:'auto'}}></div>
      </div>

      <div className="wf-card lav" style={{display:'grid', gridTemplateColumns:'1fr 1fr 1fr', gap:14}}>
        <div><div className="wf-field-label">Status</div><div style={{fontWeight:600}}>Active</div></div>
        <div><div className="wf-field-label">Next draft</div><div style={{fontWeight:600}}>Jun 2 · $35.00</div></div>
        <div><div className="wf-field-label">Method</div><div style={{fontWeight:600}}>Fidelity •• 747</div></div>
      </div>

      <div className="wf-grid-2">
        <div className="wf-card">
          <div className="wf-h2">What gets paid</div>
          <div style={{display:'flex', flexDirection:'column', gap:8, fontSize:12, marginTop:6}}>
            {[['Just the assessment',true,'$35/mo'],['Whatever I owe (open balance)',false,'variable'],['A fixed amount I pick',false,'$ ____']].map(([t,sel,sub],i)=>(
              <div key={i} style={{padding:'10px 12px', borderRadius:10, border:'1.5px solid', borderColor: sel?'var(--ink)':'var(--line)', background: sel?'var(--pink)':'var(--paper)', display:'flex', alignItems:'center', gap:10}}>
                <span className={`wf-radio ${sel?'on':''}`}></span>
                <div style={{flex:1}}><div style={{fontWeight: sel?600:400}}>{t}</div><div className="wf-sub" style={{fontSize:11}}>{sub}</div></div>
              </div>
            ))}
          </div>
          <div style={{marginTop:14}}>
            <div className="wf-field-label">Draft date</div>
            <div className="wf-field dashed">2nd of each month ▾</div>
          </div>
        </div>

        <div className="wf-card">
          <div className="wf-h2">Payment method on file</div>
          <div style={{display:'flex', flexDirection:'column', gap:8, marginTop:6}}>
            <VaultedMethod icon="🏦" label="Fidelity checking" meta="•• 747 · ACH · no fee" sel badge="auto-pay"/>
            <VaultedMethod icon="💳" label="Visa" meta="•• 4242 · backup if ACH fails"/>
          </div>
          <div style={{display:'flex', gap:8, marginTop:12}}>
            <div className="wf-btn" style={{flex:1, justifyContent:'center'}}>＋ Add via Stripe</div>
            <div className="wf-btn ghost" style={{flex:1, justifyContent:'center'}}>↻ Replace</div>
          </div>
          <div className="wf-card lav" style={{marginTop:12, padding:'10px 12px', fontSize:11, lineHeight:1.6}}>
            We store a reusable <b>Stripe token</b>, not the bank number. Adding or replacing opens a
            Stripe sheet; the mandate to draft monthly is captured there.
          </div>
        </div>
      </div>

      <div className="wf-card dashed">
        <div style={{display:'flex', alignItems:'baseline'}}>
          <div className="wf-h2" style={{margin:0}}>Upcoming & past drafts</div>
          <span className="wf-sub" style={{marginLeft:8, fontSize:11}}>Stripe runs these automatically</span>
        </div>
        <table className="wf-table">
          <thead><tr><th>Date</th><th>Method</th><th className="num">Amount</th><th>Status</th></tr></thead>
          <tbody>
            {[['05/02/26','Fidelity •• 747','$35.00','Paid'],['06/02/26','Fidelity •• 747','$35.00','Scheduled'],['07/02/26','Fidelity •• 747','$35.00','Scheduled']].map((r,i)=>(
              <tr key={i}><td>{r[0]}</td><td>{r[1]}</td><td className="num">{r[2]}</td><td><span className={`wf-pill ${r[3]==='Paid'?'ok':''}`}>{r[3]}</span></td></tr>
            ))}
          </tbody>
        </table>
      </div>
      <span className="wf-note" style={{alignSelf:'flex-end'}}>A · we own the rules, Stripe owns the method <span className="arrow"></span></span>
    </WFShellSide>
  );
}

// ══════════════════════════════════════════════════════════════
// RECURRING · IDEA B — embedded setup (SetupIntent), one screen
// ══════════════════════════════════════════════════════════════
function StripeRecurringEmbedded() {
  return (
    <WFShellSide sideActive="Recurring">
      <h1 className="wf-h1">Set up <span className="hand">auto-pay</span></h1>
      <p className="wf-sub" style={{marginTop:-6}}>One screen — choose the rules, then authorize the method with Stripe.</p>

      <div style={{display:'grid', gridTemplateColumns:'1fr 1.1fr', gap:16}}>
        <div className="wf-col">
          <div className="wf-card">
            <div className="wf-h2">① What & when</div>
            <div className="wf-field-label">Pay each month</div>
            <div style={{display:'flex', flexDirection:'column', gap:6, fontSize:12, marginBottom:14}}>
              <label style={{display:'flex', gap:8, alignItems:'center'}}><span className="wf-radio on"></span> Just the assessment · $35</label>
              <label style={{display:'flex', gap:8, alignItems:'center'}}><span className="wf-radio"></span> Whatever I owe</label>
              <label style={{display:'flex', gap:8, alignItems:'center'}}><span className="wf-radio"></span> Fixed amount</label>
            </div>
            <div className="wf-field-label">On the</div>
            <div className="wf-field dashed">2nd of each month ▾</div>
          </div>
          <div className="wf-card lav" style={{fontSize:12, lineHeight:1.6}}>
            <b>How it works:</b> Stripe saves the method once (a <span className="mono">SetupIntent</span>),
            then charges it on your schedule. No card on our servers, no re-entry each month.
          </div>
        </div>

        <div className="wf-col">
          <StripeRegion
            title="② Authorize a method"
            foot={<span>By saving you authorize NekoHOA to charge this monthly until cancelled. <PoweredByStripe/></span>}
          >
            <PaymentElement active="bank"/>
            <label style={{display:'flex', gap:8, alignItems:'flex-start', fontSize:11.5, marginTop:14, lineHeight:1.5}}>
              <span className="wf-check on" style={{flexShrink:0, marginTop:1}}></span>
              I agree to the recurring ACH mandate — $35 on the 2nd monthly, until I turn auto-pay off.
            </label>
          </StripeRegion>
          <div className="wf-btn primary" style={{justifyContent:'center', padding:'12px', fontSize:14}}>Turn on auto-pay</div>
        </div>
      </div>
      <span className="wf-note violet" style={{alignSelf:'flex-end'}}>B · rules + Stripe setup on one screen <span className="arrow"></span></span>
    </WFShellSide>
  );
}

// ══════════════════════════════════════════════════════════════
// RECURRING · IDEA C — delegate fully to Stripe Customer Portal
// ══════════════════════════════════════════════════════════════
function StripeRecurringPortal() {
  return (
    <WFShellSide sideActive="Recurring">
      <h1 className="wf-h1">Auto-pay <span className="hand">summary</span></h1>
      <p className="wf-sub" style={{marginTop:-6}}>The lightest build — NekoHOA shows status; Stripe’s portal does the editing.</p>

      <div className="wf-grid-2">
        <div className="wf-card">
          <div style={{display:'flex', alignItems:'center', gap:8}}>
            <span className="wf-pill ok">✓ Active</span>
            <span className="wf-sub" style={{fontSize:11}}>since Jan 2026</span>
          </div>
          <div style={{display:'grid', gridTemplateColumns:'1fr 1fr', gap:'12px 14px', marginTop:14, fontSize:13}}>
            <div><div className="wf-field-label">Plan</div><b>Monthly assessment</b></div>
            <div><div className="wf-field-label">Amount</div><b className="mono">$35.00 / mo</b></div>
            <div><div className="wf-field-label">Next draft</div><b>Jun 2, 2026</b></div>
            <div><div className="wf-field-label">Method</div><b>Fidelity •• 747</b></div>
          </div>
        </div>

        <div className="wf-card lav" style={{display:'flex', flexDirection:'column'}}>
          <div className="wf-h2">Manage your plan</div>
          <p className="wf-sub" style={{fontSize:12, lineHeight:1.6}}>
            Update the payment method, change the amount, pause, or download receipts in Stripe’s
            secure billing portal. Changes sync back to NekoHOA automatically.
          </p>
          <div style={{display:'flex', flexDirection:'column', gap:8, marginTop:'auto'}}>
            <div className="wf-btn primary" style={{justifyContent:'center', padding:'12px'}}>Open billing portal ↗</div>
            <div style={{display:'flex', justifyContent:'center'}}><PoweredByStripe/></div>
          </div>
        </div>
      </div>

      <div className="wf-card dashed">
        <div className="wf-h2">What the portal handles for you</div>
        <div className="wf-grid-3" style={{gap:12, marginTop:4}}>
          {[
            ['💳','Swap method','card ↔ bank, update expiry'],
            ['⏸','Pause / cancel','self-serve, with confirmation'],
            ['🧾','Receipts','download or email any draft'],
            ['🔁','Retry failures','smart retries on declines'],
            ['✉️','Dunning emails','Stripe nudges before a lapse'],
            ['🧮','Proration','handles mid-cycle changes'],
          ].map(([ic,t,s],i)=>(
            <div key={i} style={{display:'flex', gap:10, alignItems:'flex-start'}}>
              <span style={{fontSize:16}}>{ic}</span>
              <div><div style={{fontWeight:600, fontSize:12.5}}>{t}</div><div className="wf-sub" style={{fontSize:11}}>{s}</div></div>
            </div>
          ))}
        </div>
      </div>
      <span className="wf-note violet" style={{alignSelf:'flex-end'}}>C · least to build, most Stripe-owned <span className="arrow"></span></span>
    </WFShellSide>
  );
}

Object.assign(window, {
  StripeOneTimeRedirect, StripeHostedCheckout, StripeOneTimeEmbedded,
  StripeOneTimeSaved, StripeRecurringSaved, StripeRecurringEmbedded, StripeRecurringPortal,
});
