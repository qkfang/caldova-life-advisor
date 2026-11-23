// Paraplanning "Try it out" wiring (/agents/paraplanning).
//
// Loaded globally from App.razor so it runs on a full page load AND re-binds
// after Blazor enhanced navigation (`enhancedload`) — inline <script> tags in
// enhanced-navigated pages are not reliably executed.
//
// Flow: 1 pick a Client Case (claim cases get a short note pointing to Claims)
// → 2 engage the Paraplanning Agent (streamed narrative + engage sub-tabs)
// → 3 needs analysis with the working → 4 strategy, ownership and rules
// checklist (each item cites a clause; select it to read the rule) → 5 draft
// SoA outline with premiums and commission in dollars → 6 adviser sign-off.
// Endpoints: /paraplanning/cases, /paraplanning/cases/{n},
// /paraplanning/rules/{n}, /paraplanning/advice/{n}, /paraplanning/process.
// Open the page with ?case=REQ-… to pre-select a case.
(function () {
    function escapeHtml(s) {
        return String(s ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
    }

    function formatDate(value) {
        if (!value) return '';
        const d = new Date(value);
        if (isNaN(d.getTime())) return String(value);
        return d.toLocaleString('en-AU', { day: 'numeric', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit' });
    }

    function formatReceived(value) {
        const m = /^(\d{4})-(\d{2})-(\d{2})$/.exec(String(value || ''));
        if (!m) return value || '';
        const d = new Date(Number(m[1]), Number(m[2]) - 1, Number(m[3]));
        return d.toLocaleDateString('en-AU', { weekday: 'short', day: 'numeric', month: 'short', year: 'numeric' });
    }

    // Split a clauseRef such as "SUP-4" or "COM-2, COM-5" into clause ids.
    function clauseTokens(ref) {
        return String(ref || '')
            .split(/[\s,;/]+/)
            .map(t => t.trim().replace(/[().]+$/, '').replace(/^[(]+/, ''))
            .filter(t => /^[A-Za-z]+-[A-Za-z0-9.]+$/.test(t));
    }

    function statusPillClass(status) {
        switch (String(status || '').toLowerCase()) {
            case 'readyforadviser': return 'ready';
            case 'needmoreinfo': return 'more';
            default: return 'na';
        }
    }

    function normaliseStatus(s) {
        const v = String(s || '').toLowerCase();
        return v === 'pass' || v === 'fail' ? v : 'info';
    }

    function init() {
        const root = document.querySelector('.pp-panel');
        if (!root || root.dataset.wired === '1') return;
        root.dataset.wired = '1';

        const $ = sel => root.querySelector(sel);
        const select = $('#pp-case-select');
        const refreshBtn = $('#pp-refresh-btn');
        const emptyEl = $('#pp-empty');
        const card = $('#pp-case-card');
        const docsWrap = $('#pp-docs');
        const docsList = $('#pp-docs-list');
        const noteEl = $('#pp-note');
        const processStep = $('#pp-process-step');
        const processBtn = $('#pp-process-btn');
        const processStatus = $('#pp-process-status');
        const engageScope = $('.engage-tabs-scope');
        const outputSteps = ['#pp-needs-step', '#pp-checklist-step', '#pp-soa-step', '#pp-signoff-step'].map($);
        const rulesEl = $('#pp-rules');

        let currentCase = null;
        let currentRules = null;
        let clauseIndex = {};
        let selectSeq = 0;
        const params = new URLSearchParams(window.location.search);
        let pendingPreselect = params.get('caseNumber') || params.get('case');

        function hideOutputs() {
            outputSteps.forEach(el => { if (el) el.hidden = true; });
        }

        function resetSelection() {
            card.hidden = true;
            docsWrap.hidden = true;
            noteEl.hidden = true;
            processStep.hidden = true;
            processStatus.hidden = true;
            hideOutputs();
        }

        async function loadCases() {
            select.innerHTML = '<option value="">— Loading cases —</option>';
            emptyEl.hidden = true;
            try {
                const res = await fetch('/paraplanning/cases');
                if (!res.ok) throw new Error('HTTP ' + res.status);
                const cases = await res.json();
                if (!cases.length) {
                    select.innerHTML = '<option value="">— No cases in memory —</option>';
                    emptyEl.hidden = false;
                    resetSelection();
                    return;
                }
                const previous = currentCase ? currentCase.caseNumber : '';
                select.innerHTML =
                    '<option value="">— Select a case —</option>' +
                    cases.map(c => `<option value="${escapeHtml(c.caseNumber)}">${escapeHtml(c.caseNumber)} — ${escapeHtml(c.clientName)} (${escapeHtml(c.requestType)})${c.applicable ? '' : ' · Claims'}</option>`).join('');
                if (previous && cases.some(c => c.caseNumber === previous)) select.value = previous;

                if (pendingPreselect) {
                    const wanted = pendingPreselect.toLowerCase();
                    pendingPreselect = null;
                    const match = cases.find(c => String(c.caseNumber).toLowerCase() === wanted);
                    if (match) {
                        select.value = match.caseNumber;
                        selectCase(match.caseNumber);
                    }
                }
            } catch (err) {
                select.innerHTML = '<option value="">— Failed to load —</option>';
                emptyEl.hidden = false;
                emptyEl.textContent = 'Failed to load cases: ' + err.message;
            }
        }

        async function selectCase(caseNumber) {
            const seq = ++selectSeq;
            currentCase = null;
            currentRules = null;
            resetSelection();
            if (!caseNumber) return;
            try {
                const enc = encodeURIComponent(caseNumber);
                const [caseRes, rulesRes, adviceRes] = await Promise.all([
                    fetch(`/paraplanning/cases/${enc}`),
                    fetch(`/paraplanning/rules/${enc}`),
                    fetch(`/paraplanning/advice/${enc}`)
                ]);
                if (!caseRes.ok) throw new Error('HTTP ' + caseRes.status);
                const c = await caseRes.json();
                const rules = rulesRes.ok ? await rulesRes.json() : null;
                const advice = adviceRes.ok ? await adviceRes.json() : null;
                if (seq !== selectSeq) return;

                currentCase = c;
                currentRules = rules;
                renderCard(c);
                card.hidden = false;

                if (advice && advice.applicable === false) {
                    renderNote(advice);
                    return;
                }
                renderDocChips(rules);
                processStep.hidden = false;
                processBtn.disabled = false;
            } catch (err) {
                if (seq !== selectSeq) return;
                emptyEl.hidden = false;
                emptyEl.textContent = 'Failed to load case: ' + err.message;
            }
        }

        function renderCard(c) {
            const p = (c.priority || '').toLowerCase();
            const prioClass = ['urgent', 'high', 'standard'].includes(p) ? p : 'standard';
            card.innerHTML = `
                <div class="row"><div class="k">Case no.</div><div class="v mono">${escapeHtml(c.caseNumber)}</div></div>
                <div class="row"><div class="k">Opened</div><div class="v">${escapeHtml(formatDate(c.createdAt))}</div></div>
                <div class="row"><div class="k">Client</div><div class="v">${escapeHtml(c.clientName)}</div></div>
                <div class="row"><div class="k">Client no.</div><div class="v mono">${escapeHtml(c.clientNumber)}</div></div>
                <div class="row"><div class="k">Cover</div><div class="v">${escapeHtml(c.cover)}</div></div>
                <div class="row"><div class="k">Request type</div><div class="v">${escapeHtml(c.requestType)}</div></div>
                <div class="row"><div class="k">Received</div><div class="v">${escapeHtml(formatReceived(c.receivedDate))}</div></div>
                <div class="row"><div class="k">Adviser</div><div class="v">${escapeHtml(c.adviserName)}</div></div>
                <div class="row"><div class="k">Referred by</div><div class="v">${escapeHtml(c.referredBy || 'Existing client')}</div></div>
                <div class="row"><div class="k">Headline figure</div><div class="v">${escapeHtml(c.amount)}</div></div>
                <div class="row span-2"><div class="k">Summary</div><div class="v">${escapeHtml(c.requestSummary)}</div></div>
                <div class="row span-2"><div class="k">Priority</div><div class="v"><span class="prio-pill ${prioClass}">${escapeHtml(c.priority)}</span>${escapeHtml(c.priorityReason)}</div></div>
            `;
        }

        function renderNote(advice) {
            $('#pp-note-text').textContent = advice.notApplicableNote || 'This case is looked after by another agent.';
            const links = Array.isArray(advice.redirects) ? advice.redirects : [];
            $('#pp-note-links').innerHTML = links
                .map(l => `<a href="${escapeHtml(l.href)}">${escapeHtml(l.label)} →</a>`).join('');
            noteEl.hidden = false;
        }

        function renderDocChips(rules) {
            const docs = (rules && Array.isArray(rules.documents)) ? rules.documents : [];
            docsList.innerHTML = docs.map(d => `<span class="pp-doc-chip" title="${escapeHtml(d.summary)}">${escapeHtml(d.title)}</span>`).join('');
            docsWrap.hidden = docs.length === 0;
        }

        // ── Step 3 — needs analysis ─────────────────────────────────────
        function renderNeeds(report) {
            const needs = Array.isArray(report.needs) ? report.needs : [];
            $('#pp-needs').innerHTML = needs.length ? needs.map(n => {
                const rows = (n.lines || []).map(l => {
                    const kind = ['add', 'less', 'total', 'result', 'note'].includes(l.kind) ? l.kind : 'add';
                    if (kind === 'note' && !l.amount) {
                        return `<tr class="note"><td colspan="2">${escapeHtml(l.label)}</td></tr>`;
                    }
                    return `<tr class="${kind}"><td>${escapeHtml(l.label)}</td><td class="amt">${escapeHtml(l.amount)}</td></tr>`;
                }).join('');
                return `
                    <div class="pp-need">
                        <div class="pp-need-head">
                            <span class="pp-need-title">${escapeHtml(n.title)}</span>
                            <span class="pp-need-who">${escapeHtml(n.lifeInsured)}</span>
                        </div>
                        <table>${rows}</table>
                        <div class="pp-need-rec">
                            <div class="k">Recommended</div>
                            <div class="v">${escapeHtml(n.recommended)}</div>
                            <div class="b">${escapeHtml(n.basis)}</div>
                        </div>
                    </div>`;
            }).join('') : '<p class="muted">The needs analysis is waiting for the completed fact find.</p>';
            const total = $('#pp-needs-total');
            total.textContent = report.needsTotal || '';
            total.hidden = !report.needsTotal;
        }

        // ── Step 4 — checklist and rule documents ───────────────────────
        function renderRules(rules) {
            const docs = (rules && Array.isArray(rules.documents)) ? rules.documents : [];
            clauseIndex = {};
            if (!docs.length) {
                rulesEl.innerHTML = '<div class="muted">No rule documents found for this case.</div>';
                $('#pp-rules-count').textContent = '';
                return;
            }
            rulesEl.innerHTML = docs.map(d => {
                const sections = (d.sections || []).map(s => {
                    const id = String(s.clauseId || '');
                    clauseIndex[id.toUpperCase()] = { id, title: s.title, text: s.text, doc: d.title };
                    return `
                        <li class="rule-clause" data-clause="${escapeHtml(id)}">
                            <span class="clause">${escapeHtml(id)}</span><span class="title">${escapeHtml(s.title)}</span><span class="marker"></span>
                            <span class="desc">${escapeHtml(s.text)}</span>
                            ${s.legalReference ? `<span class="ref">Source: ${escapeHtml(s.legalReference)}</span>` : ''}
                        </li>`;
                }).join('');
                const requirements = (d.requirements || []).map(r => `<li>${escapeHtml(r)}</li>`).join('');
                const restrictions = (d.restrictions || []).map(r => `<li>${escapeHtml(r)}</li>`).join('');
                return `
                    <details class="rule-doc" data-doc="${escapeHtml(d.id)}">
                        <summary>
                            <span class="doc-chevron" aria-hidden="true">▶</span>
                            <span class="doc-title">${escapeHtml(d.title)}</span>
                            <span class="doc-type">${escapeHtml(d.documentType)}</span>
                        </summary>
                        <div class="rule-doc-body">
                            <div class="rule-meta">
                                <div class="k">Issuer</div><div>${escapeHtml(d.issuer)}</div>
                                <div class="k">Applies to</div><div>${escapeHtml(d.appliesTo)}</div>
                                <div class="k">Current</div><div>${escapeHtml(d.effectiveDate)}</div>
                            </div>
                            <p class="rule-summary">${escapeHtml(d.summary)}</p>
                            <ul class="rule-clauses">${sections}</ul>
                            <div class="rule-lists">
                                ${requirements ? `<div><h5>What must be on file</h5><ul>${requirements}</ul></div>` : ''}
                                ${restrictions ? `<div><h5>Limits and restrictions</h5><ul>${restrictions}</ul></div>` : ''}
                            </div>
                            <div class="rule-kb">Knowledge base: <em>${escapeHtml(d.title)}</em> (life_knowledge)</div>
                        </div>
                    </details>`;
            }).join('');
            $('#pp-rules-count').textContent = `${docs.length} document${docs.length === 1 ? '' : 's'}`;
        }

        function jumpToClause(clauseId) {
            const key = String(clauseId || '').toUpperCase();
            const target = Array.from(rulesEl.querySelectorAll('.rule-clause'))
                .find(li => String(li.dataset.clause || '').toUpperCase() === key);
            if (!target) return;
            const details = target.closest('details');
            if (details) details.open = true;
            target.scrollIntoView({ behavior: 'smooth', block: 'center' });
            target.classList.remove('flash');
            void target.offsetWidth;
            target.classList.add('flash');
        }

        // Colour the clauses by the checklist result that cites them (a
        // failing check outranks a note, which outranks a pass).
        function markCitedClauses(items) {
            const rank = { fail: 3, info: 2, pass: 1 };
            const byClause = {};
            (items || []).forEach(it => {
                const status = normaliseStatus(it.status);
                clauseTokens(it.clauseRef).forEach(id => {
                    const key = id.toUpperCase();
                    if (!byClause[key] || rank[status] > rank[byClause[key]]) byClause[key] = status;
                });
            });
            rulesEl.querySelectorAll('.rule-clause').forEach(li => {
                li.classList.remove('cited-pass', 'cited-fail', 'cited-info');
                const status = byClause[String(li.dataset.clause || '').toUpperCase()];
                const marker = li.querySelector('.marker');
                if (status) {
                    li.classList.add('cited-' + status);
                    if (marker) marker.textContent = 'Cited — ' + status;
                } else if (marker) {
                    marker.textContent = '';
                }
            });
        }

        function clauseChips(ref) {
            const tokens = clauseTokens(ref);
            if (!tokens.length) return ref ? `<span class="clause-ref">${escapeHtml(ref)}</span>` : '';
            const docs = new Set();
            const chips = tokens.map(id => {
                const c = clauseIndex[id.toUpperCase()];
                if (c) docs.add(c.doc);
                const tip = c ? `${c.doc} — ${c.id} ${c.title}: ${c.text}` : id;
                return `<button type="button" class="clause-ref" data-clause-jump="${escapeHtml(id)}" title="${escapeHtml(tip)}">${escapeHtml(id)}</button>`;
            }).join('');
            const docNames = Array.from(docs);
            return chips + (docNames.length ? `<span class="clause-doc">${escapeHtml(docNames.join('; '))}</span>` : '');
        }

        function renderChecklist(checklist) {
            const items = Array.isArray(checklist.items) ? checklist.items : [];
            const pill = $('#pp-status-pill');
            pill.textContent = checklist.statusLabel || 'Status';
            pill.className = 'pp-pill ' + statusPillClass(checklist.status);
            $('#pp-source').textContent = checklist.source === 'agent'
                ? "From the agent's JSON checklist"
                : 'Offline checklist (case file and rule documents)';
            $('#pp-status-reason').textContent = checklist.statusReason || '';

            const counts = { pass: 0, fail: 0, info: 0 };
            items.forEach(it => { counts[normaliseStatus(it.status)]++; });
            $('#pp-tally').innerHTML =
                `<span class="pass">${counts.pass} pass</span> · <span class="fail">${counts.fail} fail</span> · <span class="info">${counts.info} note${counts.info === 1 ? '' : 's'}</span>`;

            const icon = s => s === 'pass' ? '✓' : (s === 'fail' ? '✗' : 'ⓘ');
            const word = s => s === 'pass' ? 'Pass' : (s === 'fail' ? 'Fail' : 'Note');
            const groups = [];
            items.forEach(it => {
                const cat = it.category || 'Checks';
                let g = groups.find(x => x.cat === cat);
                if (!g) { g = { cat, items: [] }; groups.push(g); }
                g.items.push(it);
            });
            $('#pp-checklist').innerHTML = groups.map(g => `
                <div class="pp-cat">${escapeHtml(g.cat)}</div>
                <ul class="pp-items">${g.items.map(it => {
                    const status = normaliseStatus(it.status);
                    return `
                    <li class="${status}">
                        <span class="icon ${status}" aria-hidden="true">${icon(status)}</span>
                        <div>
                            <div class="label">
                                <span>${escapeHtml(it.label)}</span>
                                <span class="status-text">${word(status)}</span>
                                ${clauseChips(it.clauseRef)}
                            </div>
                            <div class="finding">${escapeHtml(it.finding)}</div>
                        </div>
                    </li>`;
                }).join('')}</ul>`).join('') || '<p class="muted">No checklist items were returned.</p>';
        }

        // ── Step 5 — SoA outline, premiums, commission ──────────────────
        function renderSoa(report) {
            $('#pp-soa-summary').textContent = report.summary || '';
            const sections = Array.isArray(report.soaOutline) ? report.soaOutline : [];
            $('#pp-soa').innerHTML = sections.map(s => `
                <div class="pp-soa-section${['commission', 'replacement', 'second-review'].includes(s.id) ? ' highlight' : ''}">
                    <h5>${escapeHtml(s.title)}</h5>
                    <ul>${(s.points || []).map(p => `<li>${escapeHtml(p)}</li>`).join('')}</ul>
                </div>`).join('') || '<p class="muted">The SoA outline is drafted once the fact find is complete.</p>';

            const premiums = Array.isArray(report.premiums) ? report.premiums : [];
            $('#pp-premiums').innerHTML = premiums.length
                ? '<thead><tr><th>Life insured</th><th>Cover</th><th class="num">Sum insured</th><th>Structure</th><th class="num">Premium (yr 1)</th><th>Paid by</th></tr></thead><tbody>' +
                  premiums.map(p => `<tr><td>${escapeHtml(p.lifeInsured)}</td><td>${escapeHtml(p.cover)}</td><td class="num">${escapeHtml(p.sumInsured)}</td>` +
                      `<td>${escapeHtml(p.structure)}</td><td class="num">${escapeHtml(p.premium)}</td><td>${escapeHtml(p.paidBy)}</td></tr>`).join('') + '</tbody>'
                : '';
            $('#pp-premium-note').textContent = report.premiumNote || '';

            const com = report.commission;
            const comWrap = $('#pp-commission');
            if (com) {
                $('#pp-commission-grid').innerHTML = [
                    ['Insurer', com.insurer],
                    ['Premium basis', com.premiumBasis],
                    ['Upfront', com.upfront],
                    ['Ongoing', com.ongoing],
                ].map(([k, v]) => `<div class="pp-commission-cell"><div class="k">${escapeHtml(k)}</div><div class="v">${escapeHtml(v)}</div></div>`).join('');
                $('#pp-commission-notes').innerHTML = [com.basis, com.consent, com.gst, com.clawback]
                    .filter(Boolean).map(x => `<li>${escapeHtml(x)}</li>`).join('');
                comWrap.hidden = false;
            } else {
                comWrap.hidden = true;
            }
        }

        // ── Step 6 — adviser sign-off ───────────────────────────────────
        function renderSignOff(report) {
            const s = report.signOff || {};
            $('#pp-signoff-heading').textContent = s.heading || 'Adviser sign-off required: Amelia';
            const status = $('#pp-signoff-status');
            status.textContent = s.status || '';
            status.className = 'pp-signoff-status' + (/^signed/i.test(s.status || '') ? '' : ' pending');
            $('#pp-signoff-statement').textContent = s.statement || '';
            $('#pp-signoff-history').innerHTML = (s.history || []).map(h => `<li>${escapeHtml(h)}</li>`).join('') || '<li>Not drafted yet.</li>';
            $('#pp-signoff-approvals').innerHTML = (s.approvals || []).map(a => `<li>${escapeHtml(a)}</li>`).join('');
            $('#pp-signature-name').textContent = s.adviser || 'Amelia, Senior Risk Adviser';
            $('#pp-plain').textContent = report.plainEnglishExplanation ? 'In plain English: ' + report.plainEnglishExplanation : '';
        }

        async function processCase() {
            if (!currentCase) return;
            const caseNumber = currentCase.caseNumber;
            processBtn.disabled = true;
            processStatus.hidden = false;
            processStatus.className = 'pp-status';
            processStatus.innerHTML = '<span class="spinner"></span>Engaging the Paraplanning Agent…';
            hideOutputs();

            try {
                const data = await window.zcAgentStream({
                    url: '/paraplanning/process',
                    body: { caseNumber },
                    onDelta: (_chunk, fullText) => {
                        if (window.engageTabsStreamNarrative) window.engageTabsStreamNarrative(engageScope, fullText);
                    },
                    onError: (msg) => {
                        processStatus.className = 'pp-status error';
                        processStatus.textContent = 'Agent error: ' + msg;
                    }
                });
                if (!data) throw new Error('No response from the agent');
                if (!currentCase || currentCase.caseNumber !== caseNumber) return;

                if (window.engageTabsRender) window.engageTabsRender(engageScope, data);

                if (data.applicable === false) {
                    processStatus.className = 'pp-status';
                    processStatus.textContent = 'Not a paraplanning case — see the note above.';
                    return;
                }

                const report = data.adviceReport || {};
                const checklist = data.agentChecklist || { items: report.items, status: report.status, statusLabel: report.statusLabel, statusReason: report.statusReason, source: 'deterministic' };

                if (data.agentError) {
                    processStatus.className = 'pp-status warn';
                    processStatus.textContent = 'The Paraplanning Agent is unavailable (' + data.agentError + ') — showing the offline paraplanning draft.';
                } else if (!data.agentConfigured) {
                    processStatus.className = 'pp-status';
                    processStatus.textContent = 'Prepared offline (Foundry agent not configured) — the needs analysis, checklist and SoA outline come from the case file and rule documents.';
                } else if (checklist.source !== 'agent') {
                    processStatus.className = 'pp-status warn';
                    processStatus.textContent = "The agent's reply had no readable checklist block — showing the offline checklist alongside its narrative.";
                } else {
                    processStatus.className = 'pp-status';
                    processStatus.textContent = 'The Paraplanning Agent prepared the draft for Amelia.';
                }

                renderNeeds(report);
                renderRules(currentRules);
                renderChecklist(checklist);
                markCitedClauses(checklist.items);
                renderSoa(report);
                renderSignOff(report);
                outputSteps.forEach(el => { if (el) el.hidden = false; });
            } catch (err) {
                processStatus.className = 'pp-status error';
                processStatus.textContent = 'Failed to process: ' + err.message;
            } finally {
                processBtn.disabled = false;
            }
        }

        root.addEventListener('click', e => {
            const chip = e.target.closest('[data-clause-jump]');
            if (!chip) return;
            e.preventDefault();
            jumpToClause(chip.dataset.clauseJump);
        });
        select.addEventListener('change', () => selectCase(select.value));
        refreshBtn.addEventListener('click', loadCases);
        processBtn.addEventListener('click', processCase);

        loadCases();
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }

    if (!window.__paraplanningScenarioHook) {
        window.__paraplanningScenarioHook = true;
        if (typeof Blazor !== 'undefined' && typeof Blazor.addEventListener === 'function') {
            Blazor.addEventListener('enhancedload', init);
        } else {
            document.addEventListener('enhancedload', init);
        }
    }
})();
