// Revised Statement / Billing — global status header + 3-card metrics + consolidated billing list

function Statement() {
  const rows = [
    {
      cycle: 'June 2026 Dues',
      due: 'Jun 01, 2026',
      total: '$175.00',
      status: 'upcoming',
      statusLabel: 'Upcoming',
      statusSub: 'Auto-Pay active',
      action: 'Manage Auto-Pay',
    },
    {
      cycle: 'May 2026 Dues',
      due: 'May 01, 2026',
      total: '$175.00',
      status: 'paid',
      statusLabel: 'Paid In Full',
      statusSub: 'Paid May 03',
      action: 'View Receipt',
    },
    {
      cycle: 'Special Assessment (Roof)',
      due: 'Apr 15, 2026',
      total: '$500.00',
      status: 'past',
      statusLabel: 'Past Due',
      statusSub: '+$25 Late Fee',
      action: 'Pay Balance',
    },
    {
      cycle: 'April 2026 Dues',
      due: 'Apr 01, 2026',
      total: '$175.00',
      status: 'overpaid',
      statusLabel: 'Overpaid',
      statusSub: '-$50 Credit Applied',
      action: 'View Details',
    },
  ];

  const dot = (s) => {
    const m = {
      upcoming: { bg: 'oklch(0.96 0.05 90)', fg: 'oklch(0.45 0.10 80)',  dot: '🟡' },
      paid:     { bg: 'oklch(0.94 0.08 160)', fg: 'oklch(0.38 0.10 160)', dot: '🟢' },
      past:     { bg: 'oklch(0.94 0.07 25)',  fg: 'oklch(0.45 0.15 25)',  dot: '🔴' },
      overpaid: { bg: 'oklch(0.94 0.08 160)', fg: 'oklch(0.38 0.10 160)', dot: '🟢' },
    };
    return m[s];
  };

  return (
    <WFShellSide sideActive="Statement">
      <div style={{display:'flex', alignItems:'baseline'}}>
        <h1 className="wf-h1">Billing & <span className="hand">payments</span></h1>
        <div style={{marginLeft:'auto', display:'flex', gap:8}}>
          <div className="wf-btn ghost">⎙ Print</div>
          <div className="wf-btn ghost">Export</div>
          <div className="wf-btn primary">Make a payment</div>
        </div>
      </div>

      {/* 1. GLOBAL STATUS HEADER */}
      <div className="wf-card" style={{
        display:'flex', alignItems:'center', gap:14,
        background: 'oklch(0.95 0.08 160)',
        borderColor: 'oklch(0.55 0.10 160)',
        padding:'16px 18px',
      }}>
        <span style={{
          padding:'6px 12px', borderRadius:999,
          background: 'oklch(0.45 0.10 160)', color:'#fff',
          fontWeight:700, fontSize:12, letterSpacing:'0.06em',
          textTransform:'uppercase', whiteSpace:'nowrap',
        }}>● Account in good standing</span>
        <div style={{fontSize:13, color:'oklch(0.32 0.08 160)'}}>
          Your account is paid ahead. Thank you for being a great neighbor!
        </div>
      </div>

      {/* 2. THREE-CARD METRIC OVERVIEW ROW */}
      <div className="wf-grid-3">
        <div className="wf-card">
          <div className="wf-field-label">Amount Due Now</div>
          <div style={{fontSize:32, fontFamily:'Geist Mono', fontWeight:700, color:'var(--ink)', marginTop:4}}>$0.00</div>
          <div className="wf-sub" style={{marginTop:4}}>No immediate payment required.</div>
        </div>
        <div className="wf-card" style={{borderColor:'oklch(0.55 0.10 160)'}}>
          <div className="wf-field-label">Prepaid Credit Balance</div>
          <div style={{fontSize:32, fontFamily:'Geist Mono', fontWeight:700, color:'oklch(0.45 0.10 160)', marginTop:4}}>+$120.00</div>
          <div className="wf-sub" style={{marginTop:4}}>Will automatically apply to your next cycle.</div>
        </div>
        <div className="wf-card">
          <div className="wf-field-label">Upcoming Assessment</div>
          <div style={{fontSize:24, fontFamily:'Geist Mono', fontWeight:500, color:'var(--ink)', marginTop:4}}>$175.00</div>
          <div className="wf-sub" style={{marginTop:4}}>Scheduled for July 1, 2026.</div>
        </div>
      </div>

      {/* 3. CONSOLIDATED SINGLE-ROW BILLING LIST */}
      <div className="wf-card" style={{padding:0}}>
        <div style={{padding:'14px 18px', display:'flex', alignItems:'baseline', borderBottom:'1.5px dashed var(--line)'}}>
          <div className="wf-h2" style={{margin:0}}>Billing history</div>
          <span className="wf-sub" style={{marginLeft:10, fontSize:11}}>last 12 months</span>
          <div style={{marginLeft:'auto', display:'flex', gap:8, alignItems:'center'}}>
            <div className="wf-field dashed" style={{width:180, padding:'5px 10px'}}>🔍 Search billing…</div>
            <div className="wf-btn ghost">+ Filter</div>
          </div>
        </div>
        <table className="wf-table">
          <thead>
            <tr>
              <th>Billing Cycle / Item</th>
              <th>Due Date</th>
              <th className="num">Total Charge</th>
              <th style={{textAlign:'center'}}>Payment Status</th>
              <th style={{textAlign:'right'}}>Action</th>
            </tr>
          </thead>
          <tbody>
            {rows.map((r,i) => {
              const c = dot(r.status);
              return (
                <tr key={i}>
                  <td style={{fontWeight:600}}>{r.cycle}</td>
                  <td style={{color:'var(--ink-soft)'}}>{r.due}</td>
                  <td className="num" style={{fontWeight:600}}>{r.total}</td>
                  <td style={{textAlign:'center'}}>
                    <div style={{display:'inline-flex', flexDirection:'column', alignItems:'center', gap:4}}>
                      <span style={{
                        display:'inline-flex', alignItems:'center', gap:6,
                        padding:'4px 12px', borderRadius:999,
                        background: c.bg, color: c.fg,
                        fontWeight:600, fontSize:11.5,
                        border:`1px solid ${c.fg}`,
                      }}>
                        <span style={{fontSize:8}}>●</span> {r.statusLabel}
                      </span>
                      <span style={{fontSize:10.5, color: r.status === 'past' ? c.fg : 'var(--ink-soft)'}}>{r.statusSub}</span>
                    </div>
                  </td>
                  <td style={{textAlign:'right'}}><span className="wf-link" style={{fontSize:12}}>{r.action}</span></td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
    </WFShellSide>
  );
}

Object.assign(window, { Statement });
