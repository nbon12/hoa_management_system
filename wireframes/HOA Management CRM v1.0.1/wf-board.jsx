// Board / manager side — shared role-gated shell + ONE metric registry that
// drives every metric surface (hero stats, Work Processed, Community Metrics,
// and the glossary). Adding or retiring a metric is a one-line data change.

// ── roles ─────────────────────────────────────────────────────
const BOARD_ROLES = {
  board:      { label: 'Board member',       short: 'Board' },
  manager:    { label: 'Community manager',  short: 'Manager' },
  accountant: { label: 'Accountant',         short: 'Accountant' },
};

// ── the registry ──────────────────────────────────────────────
// One descriptor per metric. `surface` says where it can appear, `help` is the
// glossary copy, `status` drives the status cell, `tone` drives emphasis.
// Nothing below this line is hand-positioned — the tables map over it.
const METRICS = [
  // Work Processed — last 30 days
  { id:'assess-paid',   surface:'work', label:'Assessment Payments Processed', value:302, tone:'link',
    help:'Owner assessment payments that cleared in the last 30 days, counted per transaction — not per owner. Reversals are excluded.' },
  { id:'coll-referred', surface:'work', label:'Collections — Referred Accounts', value:1, tone:'link',
    help:'Delinquent accounts handed to the collections agency or attorney during the period.' },
  { id:'coll-resolved', surface:'work', label:'Collections — Resolved Accounts', value:0, tone:'link',
    help:'Accounts that left collections because the balance was paid, settled, or written off.' },
  { id:'conveyances',   surface:'work', label:'Conveyances Processed', value:2, tone:'link',
    help:'Ownership transfers recorded — a home sold and the account moved to the new owner.' },
  { id:'delinq-notice', surface:'work', label:'Delinquency Notices', value:19, tone:'link',
    help:'Late notices mailed or emailed under the collection policy. One account can receive several across a cycle.' },
  { id:'contacts',      surface:'work', label:'Resident Contacts', value:41, tone:'link',
    help:'Logged calls, emails, and portal messages handled by the management team.' },
  { id:'statements',    surface:'work', label:'Statements Mailed', value:25, tone:'link',
    help:'Printed statements physically mailed. Owners on paperless billing are not counted here.' },
  { id:'disbursements', surface:'work', label:'Vendor Disbursements', value:15, tone:'link',
    help:'Payments issued to vendors — checks and ACH combined.' },
  { id:'citations',     surface:'work', label:'Violation Citations', value:0, tone:'link',
    help:'Covenant violation notices issued after inspection.' },

  // Community metrics
  { id:'status',    surface:'community', label:'Community Status', value:'Live', status:'ok',
    help:'Whether the association is actively managed. “Live” means billing, collections, and reporting are all running.' },
  { id:'billing',   surface:'community', label:'Billing Document', value:'Coupons',
    help:'The document owners receive to pay — a coupon book, a mailed statement, or invoice only.' },
  { id:'calls',     surface:'community', label:'Calls Logged, Last 30 Days', value:41,
    help:'Inbound calls from this community’s owners, logged by the management team.' },
  { id:'fy',        surface:'community', label:'Current Fiscal Year', value:'Jan 1 – Dec 31, 2026',
    help:'The association’s budget year. Financial reports and the annual audit follow this window.' },
  { id:'over30',    surface:'community', hero:true, label:'Over 30-Days Delinquent',
    value:'10%', detail:'37 homeowners · ~$17k', status:'warn', tone:'warn',
    help:'Share of owners whose balance has been unpaid more than 30 days. The first threshold in most collection policies.' },
  { id:'over60',    surface:'community', hero:true, label:'Over 60-Days Delinquent',
    value:'8%', detail:'29 homeowners · ~$15k', status:'warn', tone:'warn',
    help:'Unpaid more than 60 days. Typically the point at which accounts become eligible for referral to collections.' },
  { id:'offsite',   surface:'community', label:'Off-Site Owners', value:'17%',
    help:'Owners whose mailing address is not the property — landlords and second-home owners. Higher shares often mean lower meeting turnout.' },
  { id:'ach',       surface:'community', hero:true, label:'Registered ACH Owners',
    value:'60%', detail:'229 owners', status:'ok', tone:'ok',
    help:'Owners paying by bank draft. Higher ACH adoption reduces both card fees and delinquency.' },
];

const bySurface = s => METRICS.filter(m => m.surface === s);
const heroes    = () => METRICS.filter(m => m.hero);

// ── community record ──────────────────────────────────────────
const COMMUNITY = {
  legalName: 'Keystone Crossing Owners Association, Inc.',
  name: 'KEYCROSS',
  guid: '8f3c1e7a-42d9-4b60-9a15-c7e0b2d84f31',
  county: 'Durham County, NC',
  formed: 'March 14, 2005',
  managedSince: 'January 1, 2019',
  description: 'Keystone Crossing Owners is a master association for the master planned community also known as Keystone Crossing Subdivision, developed by KB Homes. There are two (2) sub associations within this community: Keystone Crossing SF and Keystone Crossing TH. Amenities include pool and park. Situated between NC-540, NC-147, and I-40 just outside of the Research Triangle Park and RDU Airport.',
};

// ── nav ───────────────────────────────────────────────────────
// "My Communities" appears only when the person actually holds more than one —
// board member or manager alike. One community, and it is not drawn at all.
function boardNav({ role = 'board', communities = 1 }) {
  const g = [];
  if (communities > 1) g.push({ group: null, items: [{ label: 'My Communities' }] });
  g.push({ group: null, items: [{ label: 'Community Home' }] });
  g.push({ group: 'Community management', items: [
    { label: 'Architectural Applications' },
    { label: 'Board Approvals' },
    { label: 'Maintenance Work Orders' },
    { label: 'Announcements' },
  ]});
  g.push({ group: 'Finance', items: [
    { label: 'AP Ledger', roles: ['manager', 'accountant'] },
    { label: 'Submitted Invoices' },
    { label: 'Vendor Aging', roles: ['manager', 'accountant'] },
  ]});
  g.push({ group: 'Vendors', items: [{ label: 'Vendor Management' }] });
  g.push({ group: null, items: [{ label: 'Reports', stub: true }] });
  return g;
}

// ── shell ─────────────────────────────────────────────────────
function WFShellBoard({
  sideActive = 'Community Home', role = 'board', communities = 1,
  glossary = null, onGlossary, children,
}) {
  const nav = boardNav({ role, communities });
  return (
    <div className="wf">
      <div className="wf-top">
        <div className="wf-logo"><span className="wf-logo-mark"></span>NekoHOA</div>
        <div style={{marginLeft:'auto'}} className="wf-user">
          <span className="wfb-mode"><span>Resident</span><span className="on">Board</span></span>
          <span className="wf-pill">🔔 3</span>
          <span className="wf-avatar">NB</span>
        </div>
      </div>

      <div className="wfb-banner">
        <b>Board mode</b>
        <span style={{opacity:.85}}>· you are seeing association-wide data, not just your home</span>
      </div>

      <div className="wf-strip">
        <span className="wf-pin"></span>
        <b>Keystone Crossing (Master)</b>
        <span>· {BOARD_ROLES[role].label}</span>
        <span className="mono" style={{marginLeft:'auto', fontSize:11, color:'var(--ink-mute)'}}>{COMMUNITY.name}</span>
      </div>

      <div style={{display:'flex', flex:1, minHeight:0}}>
        <aside className="wf-side" style={{width:196}}>
          {nav.map((s, i) => (
            <React.Fragment key={i}>
              {s.group && <div style={{fontSize:10.5, textTransform:'uppercase', letterSpacing:'.08em', color:'var(--ink-mute)', padding:'12px 12px 4px', fontWeight:600}}>{s.group}</div>}
              {s.items.map(it => {
                const locked = it.roles && !it.roles.includes(role);
                return (
                  <div key={it.label}
                    className={`wf-side-item ${s.group ? 'wfb-side-child' : ''} ${it.label === sideActive ? 'is-active' : ''} ${locked ? 'wfb-side-item locked' : ''}`}
                    style={{display:'flex', alignItems:'center', gap:6}}>
                    <span style={{flex:1}}>{it.label}</span>
                    {locked && <span title="Not available to your role">🔒</span>}
                    {it.stub && <span className="wf-pill" style={{fontSize:9, padding:'1px 6px'}}>spec</span>}
                  </div>
                );
              })}
            </React.Fragment>
          ))}
        </aside>

        <div className="wf-body" style={{overflow:'auto'}}>{children}</div>

        {glossary && <GlossaryPanel target={glossary}/>}
      </div>
    </div>
  );
}

// ── glossary panel ────────────────────────────────────────────
// Fed by the same registry — every metric's `help` string, scrolled to the
// one that was clicked. No separate content source to keep in sync.
function GlossaryPanel({ target }) {
  const items = METRICS.filter(m => m.help);
  // The panel opens scrolled to the clicked term, so the target renders first
  // — in a static frame a real scrollTop would put it below the fold.
  const ordered = target
    ? [...items.filter(m => m.id === target), ...items.filter(m => m.id !== target)]
    : items;
  return (
    <aside className="wfb-glossary">
      <div style={{display:'flex', alignItems:'center', gap:8, marginBottom:6}}>
        <b style={{fontSize:13}}>What this means</b>
        <span style={{marginLeft:'auto', color:'var(--ink-mute)'}}>✕</span>
      </div>
      <p className="wf-sub" style={{fontSize:11, marginBottom:8}}>Definitions for every metric on this page.</p>
      {ordered.map(m => (
        <div key={m.id} className={`wfb-gloss-item ${m.id === target ? 'is-target' : ''}`}>
          {m.id === target && <div style={{fontSize:9.5, letterSpacing:'.07em', textTransform:'uppercase', color:'var(--violet)', fontWeight:700, marginBottom:3}}>jumped here</div>}
          <div style={{fontWeight:600, fontSize:12, marginBottom:3}}>{m.label}</div>
          <div style={{fontSize:11.5, color:'var(--ink-soft)', lineHeight:1.55}}>{m.help}</div>
        </div>
      ))}
    </aside>
  );
}

// ── metric renderers ──────────────────────────────────────────
function HeroStat({ m }) {
  return (
    <div className={`wfb-hero ${m.tone || ''}`}>
      <div className="wfb-hero-lab">{m.label}</div>
      <div className="wfb-hero-val">{m.value}</div>
      {m.detail && <div className="wfb-hero-sub">{m.detail}</div>}
    </div>
  );
}

function StatusCell({ status }) {
  if (status === 'ok')   return <span style={{color:'oklch(0.55 0.12 160)', fontWeight:600}}>✓</span>;
  if (status === 'warn') return <span className="wf-pill warn" style={{fontSize:10}}>watch</span>;
  return <span style={{color:'var(--ink-mute)'}}>—</span>;
}

// One table for every metric surface. `valueHead` renames the value column;
// the help link is always the right-most column.
function MetricTable({ rows, metricHead = 'Metric', valueHead = 'Value', showStatus = true, onHelp }) {
  return (
    <table className="wf-table">
      <thead>
        <tr>
          <th>{metricHead}</th>
          {showStatus && <th style={{width:80}}>Status</th>}
          <th className="num" style={{width:150}}>{valueHead}</th>
          <th style={{width:64, textAlign:'center'}}>Help</th>
        </tr>
      </thead>
      <tbody>
        {rows.map(m => (
          <tr key={m.id}>
            <td>{m.label}</td>
            {showStatus && <td><StatusCell status={m.status}/></td>}
            <td className="num">
              <span style={{color: m.tone === 'link' ? 'var(--rose)' : 'var(--ink)', fontWeight: m.tone === 'warn' ? 600 : 400}}>{m.value}</span>
              {m.detail && <div className="wf-sub" style={{fontSize:10.5}}>{m.detail}</div>}
            </td>
            <td style={{textAlign:'center'}}><span className="wfb-help">?</span></td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}

Object.assign(window, {
  BOARD_ROLES, METRICS, COMMUNITY, bySurface, heroes,
  boardNav, WFShellBoard, GlossaryPanel, HeroStat, StatusCell, MetricTable,
});
