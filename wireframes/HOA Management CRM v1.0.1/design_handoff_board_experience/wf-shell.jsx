// NekoHOA wireframe shell components — top nav, property strip, body.
// Used by every artboard so visuals stay consistent.

const TABS = [
  'Dashboard', 'Payments', 'Property', 'Community', 'Documents', 'Calendar'
];

function WFShell({ active = 'Dashboard', children, scroll = true }) {
  return (
    <div className="wf">
      <div className="wf-top">
        <div className="wf-logo">
          <span className="wf-logo-mark"></span>
          NekoHOA
        </div>
        <nav className="wf-tabs">
          {TABS.map(t => (
            <span key={t} className={`wf-tab ${t === active ? 'is-active' : ''}`}>{t}</span>
          ))}
        </nav>
        <div className="wf-user">
          <span className="wf-pill">🔔 3</span>
          <span className="wf-avatar">NB</span>
        </div>
      </div>
      <div className="wf-strip">
        <span className="wf-pin"></span>
        <b>Sakura Heights</b>
        <span>· 714 Keystone Park Dr</span>
        <span className="mono" style={{marginLeft:'auto', fontSize:11, color:'var(--ink-mute)'}}>R0670853L0541192</span>
      </div>
      <div className="wf-body" style={scroll ? {overflow:'auto'} : {}}>
        {children}
      </div>
    </div>
  );
}

function WFShellSide({ active = 'Dashboard', sideActive, children }) {
  const side = [
    { group: null, items: ['Dashboard'] },
    { group: 'Payments', items: ['Statement', 'One-time', 'Recurring'] },
    { group: 'Property', items: ['Info', 'Owner', 'Directory'] },
    { group: 'Community', items: ['Announcements', 'Calendar', 'Violations', 'Documents'] },
  ];
  return (
    <div className="wf">
      <div className="wf-top">
        <div className="wf-logo">
          <span className="wf-logo-mark"></span>
          NekoHOA
        </div>
        <div style={{marginLeft:'auto'}} className="wf-user">
          <span className="wf-pill">🔔 3</span>
          <span className="wf-avatar">NB</span>
        </div>
      </div>
      <div className="wf-strip">
        <span className="wf-pin"></span>
        <b>Sakura Heights</b>
        <span>· 714 Keystone Park Dr</span>
      </div>
      <div style={{display:'flex', flex:1, minHeight:0}}>
        <aside className="wf-side">
          {side.map((s, i) => (
            <React.Fragment key={i}>
              {s.group && <div style={{fontSize:10.5, textTransform:'uppercase', letterSpacing:'.08em', color:'var(--ink-mute)', padding:'10px 12px 4px'}}>{s.group}</div>}
              {s.items.map(it => (
                <div key={it} className={`wf-side-item ${it === sideActive ? 'is-active' : ''}`}>{it}</div>
              ))}
            </React.Fragment>
          ))}
        </aside>
        <div className="wf-body" style={{overflow:'auto'}}>
          {children}
        </div>
      </div>
    </div>
  );
}

Object.assign(window, { WFShell, WFShellSide });
