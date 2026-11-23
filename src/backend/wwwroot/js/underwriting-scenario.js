// Underwriting & New Business "Try it out" wiring (/agents/underwriting).
//
// Loaded as a permanent script from App.razor so init runs on a full page
// load AND on Blazor enhanced navigation (inline <script> tags inside razor
// pages do not re-execute on enhanced nav).
//
// Flow: 1 pick a Client Case (claim cases get a short note pointing to
// Claims) → 2 insurer and requirements → 3 disclosure checklist (the duty to
// take reasonable care first) → 4 engage Uma's agent, which generates the
// application and disclosure summary PDF → 5 underwriting timeline and the
// decision in plain English → 6 the client's decision with Amelia.
// Endpoints: /underwriting/cases, /underwriting/cases/{n},
// /underwriting/plan/{n}, /underwriting/process,
// /underwriting/application/{id}.pdf. Open with ?case=REQ-… to pre-select.
(function () {
    function init() {
        const root = document.querySelector('.uw-panel');
        if (!root || root.dataset.wired === '1') return;
        root.dataset.wired = '1';

        // Re-query the live document each time so a Blazor enhanced
        // navigation swap never leaves us writing into a detached node.
        const $ = sel => {
            const r = document.querySelector('.uw-panel') || root;
            return r ? r.querySelector(sel) : null;
        };

        function escapeHtml(s) {
            return String(s ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
        }
        function show(sel, visible) {
            const el = $(sel);
            if (el) el.hidden = !visible;
        }
        function setText(sel, value) {
            const el = $(sel);
            if (el) el.textContent = value == null ? '' : value;
        }
        function setHtml(sel, html) {
            const el = $(sel);
            if (el) el.innerHTML = html;
        }
        function tone(t) {
            return ['ok', 'warn', 'blocked', 'info'].includes(t) ? t : 'info';
        }
        function money(v) {
            const n = Number(v);
            if (!isFinite(n) || n === 0) return '—';
            return n.toLocaleString('en-AU', { style: 'currency', currency: 'AUD', maximumFractionDigits: n % 1 === 0 ? 0 : 2 });
        }
        function formatReceived(value) {
            const m = /^(\d{4})-(\d{2})-(\d{2})$/.exec(String(value || ''));
            if (!m) return value || '';
            const d = new Date(Number(m[1]), Number(m[2]) - 1, Number(m[3]));
            return d.toLocaleDateString('en-AU', { weekday: 'short', day: 'numeric', month: 'short', year: 'numeric' });
        }

        let selectSeq = 0;

        function resetAll() {
            ['#uw-case-summary', '#uw-note', '#uw-load-status', '#uw-insurer-step', '#uw-disclosure-step',
             '#uw-engage-step', '#uw-process-status', '#uw-pdf', '#uw-timeline-step', '#uw-client-step']
                .forEach(sel => show(sel, false));
            const btn = $('#uw-process-btn');
            if (btn) btn.disabled = true;
        }

        function loadError(message) {
            const status = $('#uw-load-status');
            if (!status) return;
            status.hidden = false;
            status.className = 'uw-status error';
            status.textContent = message;
        }

        // ── Step 1: cases ────────────────────────────────────────────────
        async function loadCases(preferred) {
            const select = $('#uw-case-select');
            if (select) select.innerHTML = '<option value="">— Loading cases —</option>';
            show('#uw-empty', false);
            resetAll();
            try {
                const res = await fetch('/underwriting/cases');
                if (!res.ok) throw new Error('HTTP ' + res.status);
                const cases = await res.json();
                if (!cases.length) {
                    if (select) select.innerHTML = '<option value="">— No cases available —</option>';
                    show('#uw-empty', true);
                    return;
                }
                if (select) {
                    select.innerHTML = '<option value="">— Select a case —</option>' + cases.map(c =>
                        `<option value="${escapeHtml(c.caseNumber)}">${escapeHtml(c.caseNumber)} — ${escapeHtml(c.clientName)} (${escapeHtml(c.requestType)})${c.applicable ? '' : ' · Claims'}</option>`
                    ).join('');
                    if (preferred) {
                        const match = cases.find(c => String(c.caseNumber).toLowerCase() === String(preferred).toLowerCase());
                        if (match) {
                            select.value = match.caseNumber;
                            selectCase();
                        }
                    }
                }
            } catch (err) {
                if (select) select.innerHTML = '<option value="">— Failed to load —</option>';
                loadError('Failed to load cases: ' + err.message);
            }
        }

        async function selectCase() {
            const select = $('#uw-case-select');
            const caseNumber = select ? select.value : '';
            const seq = ++selectSeq;
            resetAll();
            if (!caseNumber) return;
            try {
                const enc = encodeURIComponent(caseNumber);
                const [caseRes, planRes] = await Promise.all([
                    fetch('/underwriting/cases/' + enc),
                    fetch('/underwriting/plan/' + enc)
                ]);
                if (!caseRes.ok) throw new Error('HTTP ' + caseRes.status);
                if (!planRes.ok) throw new Error('HTTP ' + planRes.status);
                const c = await caseRes.json();
                const plan = await planRes.json();
                if (seq !== selectSeq) return;

                setHtml('#uw-case-summary',
                    `<div><span class="k">Case</span>${escapeHtml(c.caseNumber)}</div>` +
                    `<div><span class="k">Client</span>${escapeHtml(c.clientName)}</div>` +
                    `<div><span class="k">Client no.</span>${escapeHtml(c.clientNumber)}</div>` +
                    `<div><span class="k">Cover</span>${escapeHtml(c.cover)}</div>` +
                    `<div><span class="k">Request type</span>${escapeHtml(c.requestType)}</div>` +
                    `<div><span class="k">Received</span>${escapeHtml(formatReceived(c.receivedDate))}</div>` +
                    `<div><span class="k">Adviser</span>${escapeHtml(c.adviserName)}</div>` +
                    `<div><span class="k">Referred by</span>${escapeHtml(c.referredBy || 'Existing client')}</div>` +
                    `<div><span class="k">Priority</span>${escapeHtml(c.priority)}</div>` +
                    `<div class="wide"><span class="k">Summary</span>${escapeHtml(c.requestSummary)}</div>`);
                show('#uw-case-summary', true);

                if (!plan.applicable) {
                    setText('#uw-note-text', plan.notApplicableNote || 'This case is looked after by another agent.');
                    setHtml('#uw-note-links', (plan.redirects || [])
                        .map(l => `<a href="${escapeHtml(l.href)}">${escapeHtml(l.label)} →</a>`).join(''));
                    show('#uw-note', true);
                    return;
                }

                renderInsurer(plan);
                renderDisclosures(plan);
                show('#uw-insurer-step', true);
                show('#uw-disclosure-step', true);
                show('#uw-engage-step', true);
                const btn = $('#uw-process-btn');
                if (btn) btn.disabled = false;
            } catch (err) {
                if (seq !== selectSeq) return;
                loadError('Failed to load case: ' + err.message);
            }
        }

        // ── Step 2: insurer and requirements ─────────────────────────────
        function renderInsurer(plan) {
            setText('#uw-plan-status', `As at ${plan.asAtLabel}. ${plan.overallStatus}. Lodged: ${plan.lodgedOn}.`);
            const ins = plan.insurer;
            if (ins) {
                const list = arr => (arr || []).map(x => `<li>${escapeHtml(x)}</li>`).join('');
                const contacts = [ins.newBusinessContact, ins.underwritingContact, ins.claimsContact, ins.claimsIntegrityContact].filter(Boolean);
                setHtml('#uw-insurer',
                    '<div>' +
                        `<div><span class="uw-insurer-name">${escapeHtml(ins.name)}</span><span class="uw-insurer-prefix">${escapeHtml(ins.policyPrefix)}…</span></div>` +
                        `<div>${escapeHtml(ins.profile)}</div>` +
                        `<span class="k">Cover</span><div>${escapeHtml(ins.covers)}</div>` +
                        `<span class="k">Contacts</span><ul>${list(contacts)}</ul>` +
                        `<span class="k">Cooling-off</span><div>${escapeHtml(ins.coolingOff)}</div>` +
                    '</div>' +
                    '<div>' +
                        `<span class="k">Service standards</span><ul>${list(ins.serviceLevels)}</ul>` +
                        `<span class="k">Underwriting thresholds</span><ul>${list(ins.underwritingThresholds)}</ul>` +
                        `<span class="k">Life Advice Guru cases</span><div>${escapeHtml(ins.practiceCases)}</div>` +
                        `<span class="k">Chasing</span><div>${escapeHtml(ins.chasing)}</div>` +
                    '</div>');
            } else {
                setHtml('#uw-insurer', `<div>${escapeHtml(plan.headline)}</div>`);
            }

            const covers = Array.isArray(plan.covers) ? plan.covers : [];
            setHtml('#uw-covers', covers.length
                ? '<thead><tr><th>Life insured</th><th>Cover</th><th class="num">Sum insured</th><th>Ownership</th><th class="num">Premium (yr 1)</th><th>Paid by</th></tr></thead><tbody>' +
                  covers.map(cv => '<tr>' +
                      `<td>${escapeHtml(cv.lifeInsured)}</td>` +
                      `<td>${escapeHtml(cv.cover)}${cv.policyNumber ? `<p class="muted">Policy ${escapeHtml(cv.policyNumber)}</p>` : ''}</td>` +
                      `<td class="num">${escapeHtml(cv.sumInsured)}</td>` +
                      `<td>${escapeHtml(cv.ownership)}</td>` +
                      `<td class="num">${escapeHtml(money(cv.premiumStandard))}</td>` +
                      `<td>${escapeHtml(cv.paidBy)}</td>` +
                  '</tr>').join('') + '</tbody>'
                : '<tbody><tr><td>The covers come from the signed Statement of Advice.</td></tr></tbody>');
            setText('#uw-premium-note', covers.length ? `Quoted ${money(plan.annualPremiumStandard)} a year at standard rates (year 1).` : plan.premiumNote);

            const lives = Array.isArray(plan.livesInsured) ? plan.livesInsured : [];
            setHtml('#uw-lives', lives.map(l => {
                const sums = [
                    l.lifeSumInsured ? `Life ${money(l.lifeSumInsured)}` : null,
                    l.tpdSumInsured ? `TPD ${money(l.tpdSumInsured)}` : null,
                    l.traumaSumInsured ? `Trauma ${money(l.traumaSumInsured)}` : null,
                    l.incomeProtectionMonthly ? `IP ${money(l.incomeProtectionMonthly)} a month` : null,
                ].filter(Boolean).join(' · ');
                return '<div class="uw-life">' +
                    `<div class="uw-life-name">${escapeHtml(l.name)} · age ${escapeHtml(l.age)}</div>` +
                    `<div>${escapeHtml(l.occupation)}${l.smoker ? ' · ' + escapeHtml(l.smoker) : ''}</div>` +
                    `<div class="uw-life-sums">${escapeHtml(sums)}${l.businessCover ? ' · ' + escapeHtml(l.businessCover) : ''}</div>` +
                '</div>';
            }).join(''));

            const reqs = Array.isArray(plan.requirements) ? plan.requirements : [];
            setHtml('#uw-requirements', reqs.length
                ? '<thead><tr><th>Life insured</th><th>Requirement</th><th>Why</th><th>Status</th></tr></thead><tbody>' +
                  reqs.map(r => '<tr>' +
                      `<td>${escapeHtml(r.lifeInsured)}</td>` +
                      `<td><strong>${escapeHtml(r.requirement)}</strong><p class="muted">${escapeHtml(r.category)}</p></td>` +
                      `<td>${escapeHtml(r.reason)}${r.outcome ? `<p class="muted">${escapeHtml(r.outcome)}</p>` : ''}</td>` +
                      `<td><span class="uw-pill ${tone(r.tone)}">${escapeHtml(r.status)}</span>${r.date ? `<p class="muted">${escapeHtml(r.date)}</p>` : ''}</td>` +
                  '</tr>').join('') + '</tbody>'
                : '');
        }

        // ── Step 3: disclosure checklist ─────────────────────────────────
        function renderDisclosures(plan) {
            setText('#uw-duty-script', plan.dutyOfCareScript);
            setText('#uw-duty-ack', plan.dutyAcknowledgement);
            const icons = { done: '✓', disclosed: '✎', action: '!', info: 'ⓘ' };
            const words = { done: 'Done', disclosed: 'Disclosed', action: 'To do', info: 'Note' };
            setHtml('#uw-disclosures', (plan.disclosures || []).map(d => {
                const s = ['done', 'disclosed', 'action', 'info'].includes(d.status) ? d.status : 'info';
                return `<li class="${s}"><span class="icon" aria-hidden="true">${icons[s]}</span>` +
                    `<div><span class="label">${escapeHtml(d.label)}</span><span class="state">${words[s]}</span>` +
                    `<div class="detail">${escapeHtml(d.detail)}</div></div></li>`;
            }).join(''));
        }

        // ── Step 4: engage + PDF ─────────────────────────────────────────
        async function processCase() {
            const select = $('#uw-case-select');
            const caseNumber = select ? select.value : '';
            if (!caseNumber) return;
            const btn = $('#uw-process-btn');
            const status = $('#uw-process-status');
            const scope = $('.engage-tabs-scope');
            if (btn) btn.disabled = true;
            if (status) {
                status.hidden = false;
                status.className = 'uw-status';
                status.innerHTML = '<span class="spinner"></span>Engaging the Underwriting Agent…';
            }
            show('#uw-pdf', false);
            show('#uw-timeline-step', false);
            show('#uw-client-step', false);

            try {
                const data = await window.zcAgentStream({
                    url: '/underwriting/process',
                    body: { caseNumber },
                    onDelta: (_chunk, fullText) => {
                        if (window.engageTabsStreamNarrative) window.engageTabsStreamNarrative(scope, fullText);
                    },
                    onError: (msg) => {
                        const s = $('#uw-process-status');
                        if (s) {
                            s.className = 'uw-status error';
                            s.textContent = 'Agent error: ' + msg + ' — showing the offline underwriting plan.';
                        }
                    }
                });
                if (!data) throw new Error('No response from the agent');
                if (window.engageTabsRender) window.engageTabsRender(scope, data);

                const s = $('#uw-process-status');
                if (data.applicable === false) {
                    if (s) { s.className = 'uw-status'; s.textContent = 'Not an underwriting case — see the note above.'; }
                    return;
                }
                if (s) {
                    if (data.agentError) {
                        s.className = 'uw-status warn';
                        s.textContent = 'The live agent didn\'t complete (' + data.agentError + ') — showing the offline underwriting plan and PDF.';
                    } else if (!data.agentConfigured) {
                        s.className = 'uw-status';
                        s.textContent = 'Prepared offline (Foundry agent not configured) — the same APL directory, requirement checks and PDF the MCP tools use.';
                    } else {
                        s.className = 'uw-status';
                        s.textContent = 'The Underwriting Agent prepared the application summary.';
                    }
                }

                renderPdf(data);
                renderTimeline(data.plan || {});
                renderClientDecision(data.plan || {});
                const step = $('#uw-timeline-step');
                if (step) step.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
            } catch (err) {
                const s = $('#uw-process-status');
                if (s) {
                    s.className = 'uw-status error';
                    s.textContent = 'Failed to process: ' + err.message;
                }
            } finally {
                const b = $('#uw-process-btn');
                const sel = $('#uw-case-select');
                if (b) b.disabled = !(sel && sel.value);
            }
        }

        function renderPdf(data) {
            const link = $('#uw-pdf-link');
            const open = $('#uw-pdf-open');
            if (!data.applicationPdfUrl || !link) {
                show('#uw-pdf', false);
                return;
            }
            const fileName = data.applicationPdfFileName || 'application-summary.pdf';
            link.href = data.applicationPdfUrl;
            link.setAttribute('download', fileName);
            if (open) open.href = data.applicationPdfUrl;
            setText('#uw-pdf-name', fileName);
            const kb = data.applicationPdfBytes ? ` · ${Math.max(1, Math.round(data.applicationPdfBytes / 1024))} KB` : '';
            setText('#uw-pdf-sub', `${(data.plan && data.plan.applicationStatus) || ''}${kb}`);
            show('#uw-pdf', true);
        }

        // ── Step 5: timeline and decision ────────────────────────────────
        function renderTimeline(plan) {
            setHtml('#uw-timeline', (plan.timeline || []).map(e =>
                `<li class="${e.humanDecision ? 'human' : tone(e.tone)}">` +
                    `<div class="date">${escapeHtml(e.date)}${e.humanDecision ? '<span class="human-tag">Human decision</span>' : ''}</div>` +
                    `<div>${escapeHtml(e.event)}</div>` +
                    `<div class="who">${escapeHtml(e.who)}</div>` +
                '</li>').join(''));
            const levels = plan.insurer && Array.isArray(plan.insurer.serviceLevels) ? plan.insurer.serviceLevels : [];
            setText('#uw-sla', levels.length ? `${plan.insurer.name} service levels: ${levels.join(' ')}` : '');
            setText('#uw-decision-summary', plan.decisionSummary);
            setHtml('#uw-decisions', (plan.decisions || []).map(d =>
                `<div class="uw-decision ${tone(d.tone)}">` +
                    '<div class="uw-decision-head">' +
                        `<span class="uw-pill ${tone(d.tone)}">${escapeHtml(d.decision)}</span>` +
                        `<span class="uw-decision-cover">${escapeHtml(d.cover)}</span>` +
                        `<span class="uw-decision-who">${escapeHtml(d.lifeInsured)}</span>` +
                    '</div>' +
                    `<p>${escapeHtml(d.plainEnglish)}</p>` +
                '</div>').join('') +
                (plan.premiumNote ? `<p class="uw-premium-note">${escapeHtml(plan.premiumNote)}</p>` : ''));
            show('#uw-timeline-step', true);
        }

        // ── Step 6: client decision with the adviser ─────────────────────
        function renderClientDecision(plan) {
            const cd = plan.clientDecision || {};
            const pill = $('#uw-client-pill');
            if (pill) {
                pill.className = 'uw-pill ' + tone(cd.tone);
                pill.textContent = cd.status || '';
            }
            setText('#uw-client-who', `${cd.who || ''}${cd.date ? ' · ' + cd.date : ''}`);
            setText('#uw-client-summary', cd.summary);
            setHtml('#uw-client-points', (cd.points || []).map(p => `<li>${escapeHtml(p)}</li>`).join(''));
            setText('#uw-cooling-off', cd.coolingOff);
            show('#uw-cooling-off', !!cd.coolingOff);
            setText('#uw-existing-cover', cd.existingCoverNote);
            show('#uw-existing-cover', !!cd.existingCoverNote);

            const approval = $('#uw-approval');
            if (approval) approval.className = 'uw-approval' + (plan.humanApprovalRequired ? ' required' : '');
            setText('#uw-approval-pill', plan.humanApprovalRequired ? 'Human decision' : 'No approval needed');
            setText('#uw-approval-reason', plan.humanApprovalReason || '');
            show('#uw-client-step', true);
        }

        // ── Wiring ───────────────────────────────────────────────────────
        const selectInit = $('#uw-case-select');
        const refreshInit = $('#uw-refresh-btn');
        const processInit = $('#uw-process-btn');
        if (selectInit) selectInit.addEventListener('change', selectCase);
        if (refreshInit) refreshInit.addEventListener('click', () => loadCases());
        if (processInit) processInit.addEventListener('click', processCase);

        // Deep link: /agents/underwriting?case=REQ-… pre-selects that case.
        const params = new URLSearchParams(window.location.search);
        loadCases(params.get('caseNumber') || params.get('case'));
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }

    if (!window.__underwritingScenarioHook) {
        window.__underwritingScenarioHook = true;
        const hook = () => init();
        if (typeof Blazor !== 'undefined' && typeof Blazor.addEventListener === 'function') {
            Blazor.addEventListener('enhancedload', hook);
        } else {
            document.addEventListener('enhancedload', hook);
        }
    }
})();
