// Claims Agent (Ben) — drives the "Try it out" flow on /agents/claims.
// Loaded as a permanent script from App.razor so init runs on a full page
// load AND on Blazor enhanced navigation (inline <script> tags inside razor
// pages don't re-execute on enhanced nav).
//
// Flow: 1 pick a case → 2 confirm the claim details → 3 engage the Claims
// Agent (streamed) → claimant and policy checks, claim pack, expected benefit,
// Code of Practice timeframes and the Microsoft Teams approval (approval popup
// + in-page Approve / Reject as the Teams fallback) → lodge, which stays
// disabled until a person's approval is recorded. A claim whose claimant
// checks fail is never lodged: the Team Leader decides on the block.
// Opening /agents/claims?approvalId=CLM-…[&decision=approve|reject] (the
// links on the Teams card) jumps straight to the approval popup;
// ?caseNumber=REQ-… opens that case.
(function () {
    const STATUS_TEXT = {
        pending: 'Sent · Pending',
        approved: 'Approved',
        rejected: 'Rejected',
        lodged: 'Lodged',
        blocked: 'Blocked',
        cancelled: 'Superseded'
    };
    const LODGE_HINT = {
        pending: 'Lodging unlocks once the approval is recorded.',
        approved: 'Approved — the claim can now be lodged.',
        rejected: 'Rejected by the approver — the claim will not be lodged.',
        cancelled: 'Superseded by a newer approval — engage the agent again.',
        blocked: 'Blocked — nothing is lodged.'
    };
    const CHECK_ICON = { pass: '✓', fail: '✕', warn: '!', na: '–' };
    const PACK_STATUS = {
        verified: 'Verified',
        received: 'Received',
        prepared: 'Prepared',
        later: 'Later',
        'to-come': 'To come',
        missing: 'Missing',
        referred: 'Referred'
    };
    const POLL_MS = 4000;
    const TOM_CASE = 'REQ-20261111-LIF004';

    let escHandler = null;

    function escapeHtml(s) {
        return String(s ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
    }

    function numOrNull(v) {
        const s = String(v ?? '').trim();
        if (!s) return null;
        const n = parseFloat(s);
        return Number.isFinite(n) ? n : null;
    }

    function shorten(text, max) {
        const s = String(text ?? '');
        return s.length > max ? s.slice(0, max - 1) + '…' : s;
    }

    function timeOf(iso) {
        const d = iso ? new Date(iso) : new Date();
        return Number.isNaN(d.getTime()) ? '' : d.toLocaleTimeString('en-AU', { hour: 'numeric', minute: '2-digit' });
    }

    // "2026-11-11" → "Wed 11 Nov 2026"
    function fmtIsoDate(iso) {
        const m = /^(\d{4})-(\d{2})-(\d{2})$/.exec(String(iso ?? '').trim());
        if (!m) return iso || '';
        const d = new Date(Date.UTC(+m[1], +m[2] - 1, +m[3]));
        const days = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'];
        const months = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
        return `${days[d.getUTCDay()]} ${d.getUTCDate()} ${months[d.getUTCMonth()]} ${d.getUTCFullYear()}`;
    }

    function slug(text) {
        return String(text ?? '').toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/^-|-$/g, '');
    }

    function renderMarkdown(el, text) {
        if (typeof window.zcRenderMarkdown === 'function') return window.zcRenderMarkdown(el, text || '');
        el.textContent = text || '';
        return Promise.resolve();
    }

    function show(el, baseClass, cls, html) {
        el.hidden = false;
        el.className = baseClass + (cls ? ' ' + cls : '');
        el.innerHTML = html;
    }

    function isBlock(a) {
        return !!a && a.kind === 'block';
    }

    function statusText(a) {
        const s = (a && a.status) || 'pending';
        if (isBlock(a)) {
            return { pending: 'Awaiting Theo', blocked: 'Blocked', rejected: 'Block not approved', cancelled: 'Superseded' }[s] || s;
        }
        return STATUS_TEXT[s] || s;
    }

    function lodgedText(a) {
        return `Lodged with ${a.insurer || 'the insurer'} — reference ${a.lodgementReference || '(pending)'}${a.lodgementDate ? ', ' + a.lodgementDate : ''}.`;
    }

    function reasonsOf(a, preferred) {
        const list = (preferred && preferred.length) ? preferred
            : (a && a.approvalReasons && a.approvalReasons.length) ? a.approvalReasons
            : [a && a.reason];
        return list.filter(Boolean);
    }

    function init() {
        const root = document.querySelector('.claims-panel');
        if (!root || root.dataset.wired === '1') return;
        root.dataset.wired = '1';

        const $ = sel => root.querySelector(sel);

        // Step 1 — case
        const select = $('#claims-case-select');
        const refreshBtn = $('#claims-refresh-btn');
        const emptyEl = $('#claims-case-empty');
        const summaryEl = $('#claims-case-summary');
        const deeplinkEl = $('#claims-deeplink');
        const notAClaimEl = $('#claims-not-a-claim');
        const notAClaimText = $('#claims-not-a-claim-text');
        const notAClaimLinks = $('#claims-not-a-claim-links');
        // Step 2 — claim details
        const inputsStep = $('#claims-inputs-step');
        const form = $('#claims-inputs-form');
        const hintEl = $('#claims-inputs-hint');
        const policyFacts = $('#claims-policy-facts');
        const eventLabel = $('#claims-event-label');
        const eventText = $('#claims-event-text');
        const sumLabel = $('#claims-sum-label');
        const earningsLabel = $('#claims-earnings-label');
        const earningsText = $('#claims-earnings-text');
        const claimantOnFile = $('#claims-claimant-onfile');
        const accountOnFile = $('#claims-account-onfile');
        // Step 3 — engage + results
        const processStep = $('#claims-process-step');
        const processBtn = $('#claims-process-btn');
        const processStatus = $('#claims-process-status');
        const engageScope = $('.engage-tabs-scope');
        const resultEl = $('#claims-result');
        const claimantChecksEl = $('#claims-claimant-checks');
        const policyChecksEl = $('#claims-policy-checks');
        const packBody = $('#claims-pack-body');
        const benefitTitle = $('#claims-benefit-title');
        const benefitBody = $('#claims-benefit-body');
        const benefitResult = $('#claims-benefit-result');
        const benefitResultLabel = $('#claims-benefit-result-label');
        const benefitResultValue = $('#claims-benefit-result-value');
        const benefitNotes = $('#claims-benefit-notes');
        const benefitRule = $('#claims-benefit-rule');
        const timeframesEl = $('#claims-timeframes');
        const notesEl = $('#claims-notes');
        const activityCard = $('#claims-activity-card');
        const activityEl = $('#claims-activity');
        const approvalPill = $('#claims-approval-pill');
        const approvalReasons = $('#claims-approval-reasons');
        const recommendationEl = $('#claims-recommendation');
        const approvalApprover = $('#claims-approval-approver');
        const teamsPanel = $('#claims-teams');
        const teamsTitle = $('#claims-teams-title');
        const teamsPill = $('#claims-teams-pill');
        const teamsMsg = $('#claims-teams-msg');
        const teamsPreview = $('#claims-teams-preview');
        const teamsApprovalId = $('#claims-teams-approvalid');
        const teamsLink = $('#claims-teams-link');
        const popupBtn = $('#claims-popup-btn');
        const lodgeBtn = $('#claims-lodge-btn');
        const lodgeHint = $('#claims-lodge-hint');
        const teamsStatus = $('#claims-teams-status');
        const lodgedPanel = $('#claims-lodged');
        const lodgedTitle = $('#claims-lodged-title');
        const lodgedTextEl = $('#claims-lodged-text');
        const outcomePill = $('#claims-outcome-pill');
        const outcomeTimeline = $('#claims-outcome-timeline');
        const outcomeSummary = $('#claims-outcome-summary');
        const referredPanel = $('#claims-referred');
        const referredTitle = $('#claims-referred-title');
        const referredPill = $('#claims-referred-pill');
        const referredText = $('#claims-referred-text');
        const protectedEl = $('#claims-protected');
        const protectedAmount = $('#claims-protected-amount');
        const protectedNote = $('#claims-protected-note');
        const referredLodgeBtn = $('#claims-referred-lodge');
        const referredHint = $('#claims-referred-hint');
        const holdPanel = $('#claims-hold');
        const holdText = $('#claims-hold-text');
        const updateTitle = $('#claims-update-title');
        const updateEl = $('#claims-update');
        // Approval popup — lives outside the panel so `position: fixed`
        // isn't trapped by the panel's backdrop-filter.
        const modalEl = document.getElementById('claims-approval-modal');
        if (!modalEl) return;
        const $m = sel => modalEl.querySelector(sel);
        const modalTitle = $m('#claims-modal-title');
        const modalSub = $m('#claims-modal-sub');
        const modalDeeplink = $m('#claims-modal-deeplink');
        const modalReasons = $m('#claims-modal-reasons');
        const modalCase = $m('#claims-modal-case');
        const modalApprovalId = $m('#claims-modal-approval-id');
        const modalClient = $m('#claims-modal-client');
        const modalPolicy = $m('#claims-modal-policy');
        const modalType = $m('#claims-modal-type');
        const modalPayee = $m('#claims-modal-payee');
        const modalBenefitLabel = $m('#claims-modal-benefit-label');
        const modalBenefit = $m('#claims-modal-benefit');
        const modalRecommendation = $m('#claims-modal-recommendation');
        const modalExplainer = $m('#claims-modal-explainer');
        const modalStatus = $m('#claims-modal-status');
        const modalAgent = $m('#claims-modal-agent');
        const modalAgentBody = $m('#claims-modal-agent-body');
        const modalApproveBtn = $m('#claims-modal-approve-btn');
        const modalRejectBtn = $m('#claims-modal-reject-btn');
        const modalLodgeBtn = $m('#claims-modal-lodge-btn');

        let cases = [];
        let selected = null;
        let inputs = null;
        let lastRun = null;
        let currentApproval = null;
        let modalApproval = null;
        let pollTimer = null;
        let runSeq = 0;
        let deeplinkApprovalId = null;

        // ── Step 1: pick a case ──────────────────────────────────────

        async function loadCases(preferred) {
            try {
                const res = await fetch('/claims/cases');
                if (!res.ok) throw new Error('HTTP ' + res.status);
                cases = await res.json();
                const previous = preferred || select.value;
                select.innerHTML = '<option value="">— Select a case —</option>' +
                    cases.map(c => `<option value="${escapeHtml(c.caseNumber)}">${escapeHtml(c.caseNumber)} — ${escapeHtml(c.clientName)} (${escapeHtml(c.requestType)})</option>`).join('');
                emptyEl.hidden = cases.length > 0;

                // Keep the current case; otherwise open on Tom's income
                // protection claim (the canonical Claims example), else the
                // first case with a claim, else the first case.
                let target = '';
                if (previous && cases.some(c => c.caseNumber === previous)) {
                    target = previous;
                } else {
                    const tom = cases.find(c => c.caseNumber === TOM_CASE);
                    const claim = cases.find(c => c.claimFlow === 'lodge');
                    target = tom ? tom.caseNumber : claim ? claim.caseNumber : (cases[0] ? cases[0].caseNumber : '');
                }
                select.value = target;
                if (target && (!selected || selected.caseNumber !== target)) {
                    await selectCase(target);
                }
            } catch (err) {
                emptyEl.hidden = false;
                emptyEl.textContent = 'Failed to load cases: ' + err.message;
            }
        }

        async function selectCase(caseNumber) {
            resetResult();
            notAClaimEl.hidden = true;
            if (!caseNumber) {
                selected = null;
                inputs = null;
                summaryEl.hidden = true;
                inputsStep.hidden = true;
                processStep.hidden = true;
                return;
            }
            const seq = runSeq;
            try {
                const enc = encodeURIComponent(caseNumber);
                const [caseRes, inputsRes] = await Promise.all([
                    fetch(`/claims/cases/${enc}`),
                    fetch(`/claims/inputs/${enc}`)
                ]);
                if (!caseRes.ok) throw new Error('HTTP ' + caseRes.status);
                if (!inputsRes.ok) throw new Error('HTTP ' + inputsRes.status);
                const caseRecord = await caseRes.json();
                const caseInputs = await inputsRes.json();
                if (seq !== runSeq) return;
                selected = caseRecord;
                inputs = caseInputs;
            } catch (err) {
                selected = null;
                inputs = null;
                summaryEl.hidden = true;
                inputsStep.hidden = true;
                processStep.hidden = true;
                emptyEl.hidden = false;
                emptyEl.textContent = 'Failed to load the case: ' + err.message;
                return;
            }

            renderSummary();
            if (inputs.flow === 'not-a-claim') {
                renderNotAClaim(inputs.notAClaim || {});
                inputsStep.hidden = true;
                processStep.hidden = true;
                return;
            }
            renderInputs(inputs);
            inputsStep.hidden = false;
            processStep.hidden = false;
        }

        function renderSummary() {
            const c = selected;
            $('#claims-case-num').textContent = c.caseNumber;
            $('#claims-case-client').textContent = (inputs && inputs.clientDisplay) || [c.clientName, c.clientNumber].filter(Boolean).join(' · ');
            $('#claims-case-cover').textContent = c.cover || '—';
            $('#claims-case-type').textContent = c.requestType || '—';
            $('#claims-case-received').textContent = fmtIsoDate(c.receivedDate) || '—';
            $('#claims-case-adviser').textContent = [c.adviserName, c.referredBy].filter(Boolean).join(' · ') || '—';
            $('#claims-case-amount').textContent = c.amount || '—';
            $('#claims-case-priority').textContent = c.priority
                ? `${c.priority}${c.priorityReason ? ' — ' + c.priorityReason : ''}`
                : '—';
            $('#claims-case-summary-text').textContent = c.requestSummary || '';
            summaryEl.hidden = false;
        }

        function renderNotAClaim(info) {
            notAClaimText.textContent = info.note || 'This case is new advice — there is nothing to claim.';
            notAClaimLinks.innerHTML = (info.links || [])
                .map(l => `<a href="${escapeHtml(l.href)}">${escapeHtml(l.label)} →</a>`).join('');
            notAClaimEl.hidden = false;
        }

        // ── Step 2: claim details ────────────────────────────────────

        function fillSelect(sel, options, value) {
            sel.innerHTML = (options || []).map(o => `<option value="${escapeHtml(o.key)}">${escapeHtml(o.label)}</option>`).join('');
            if (value) sel.value = value;
        }

        function setValue(input, v) {
            input.value = (v === null || v === undefined) ? '' : String(v);
        }

        function fact(label, value) {
            return value ? `<dt>${escapeHtml(label)}</dt><dd>${escapeHtml(value)}</dd>` : '';
        }

        function renderInputs(inp) {
            const f = form.elements;
            const p = inp.policy || {};
            policyFacts.innerHTML = [
                fact('Policy', [p.insurer, p.policyNumber].filter(Boolean).join(' · ') + (p.heldOutsideSuper === false ? ' (inside super)' : ' (outside super)')),
                fact('Owner', p.policyOwner === p.lifeInsured ? `${p.policyOwner} (also the life insured)` : `${p.policyOwner} · life insured ${p.lifeInsured}`),
                fact('In force since', [p.started, p.timeInForce].filter(Boolean).join(' — ')),
                fact('Premiums', p.premiums),
                fact('Cover', (p.cover || []).join(' · ')),
                fact('Beneficiaries', (p.beneficiaries || []).map(b => `${b.name} (${b.relationship}) ${b.share}`).join(', ')),
                fact('Definition', p.definition),
                fact('Exclusions', p.exclusions)
            ].join('');

            fillSelect(f.claimType, inp.claimTypes, inp.claimType);
            setValue(f.eventDate, inp.eventDate);
            eventLabel.textContent = inp.eventLabel || 'Date of the event';
            eventText.innerHTML = [
                inp.eventDescription ? `<div>${escapeHtml(inp.eventDescription)}</div>` : '',
                inp.notification ? `<div class="muted">Notified: ${escapeHtml(inp.notification)}</div>` : ''
            ].join('');

            setValue(f.sumInsured, inp.claimType === 'income-protection' ? '' : inp.insuredAmount);
            setValue(f.insuredMonthly, inp.claimType === 'income-protection' ? inp.insuredAmount : '');
            setValue(f.waitingPeriodDays, inp.waitingPeriodDays);
            setValue(f.annualEarnings, inp.annualEarnings);
            setValue(f.beneficiarySharePercent, inp.beneficiarySharePercent);
            earningsLabel.textContent = `Earnings in the ${inp.earningsPeriod || '12 months before the disability'} (AUD)`;
            earningsText.textContent = inp.earningsEvidence
                ? `Evidence: ${inp.earningsEvidence}. Benefit basis: the lesser of the insured monthly benefit and ${inp.replacementRatePercent || 70}% of average monthly earnings.`
                : '';

            setValue(f.claimantName, inp.claimantName);
            fillSelect(f.claimantRelationship, inp.relationships, inp.claimantRelationship);
            const chips = [];
            if (inp.supportPerson) chips.push(`<span class="claims-chip">Support person<em>${escapeHtml(inp.supportPerson)}</em></span>`);
            if (inp.verifiedContact) chips.push(`<span class="claims-chip">Verified contact<em>${escapeHtml(inp.verifiedContact)}</em></span>`);
            if (inp.recentChange) chips.push(`<span class="claims-chip alert">Recent change<em>${escapeHtml(inp.recentChange)}</em></span>`);
            if (inp.vulnerable && inp.vulnerabilityNote) chips.push(`<span class="claims-chip">A person first<em>${escapeHtml(inp.vulnerabilityNote)}</em></span>`);
            claimantOnFile.innerHTML = chips.join('');

            setValue(f.accountName, inp.accountName);
            setValue(f.bsb, inp.bsb);
            setValue(f.account, inp.account);
            const acct = [];
            if (inp.accountOnFile) acct.push(`<span class="claims-chip">On file<em>${escapeHtml(inp.accountOnFile)}</em></span>`);
            if (inp.accountNote) acct.push(`<span class="claims-chip${inp.recentChange ? ' alert' : ''}">Note<em>${escapeHtml(inp.accountNote)}</em></span>`);
            accountOnFile.innerHTML = acct.join('');

            hintEl.textContent = inp.hint || '';
            hintEl.hidden = !inp.hint;
            applyTypeVisibility();
        }

        function applyTypeVisibility() {
            const type = form.elements.claimType.value;
            const income = type === 'income-protection';
            root.querySelectorAll('[data-field="income"]').forEach(el => { el.hidden = !income; });
            root.querySelectorAll('[data-field="lump"]').forEach(el => { el.hidden = income; });
            root.querySelectorAll('[data-field="death"]').forEach(el => { el.hidden = type !== 'death'; });
            sumLabel.textContent = income ? 'Sum insured (AUD)' : `Sum insured — ${type === 'death' || type === 'terminal-illness' ? 'life cover' : type === 'trauma' ? 'trauma cover' : 'TPD cover'} (AUD)`;
            if (!income && !form.elements.sumInsured.value && inputs && inputs.claimType !== 'income-protection') {
                setValue(form.elements.sumInsured, inputs.insuredAmount);
            }
        }

        function readInputs() {
            const f = form.elements;
            const type = f.claimType.value;
            const income = type === 'income-protection';
            const waiting = numOrNull(f.waitingPeriodDays.value);
            return {
                caseNumber: selected.caseNumber,
                claimType: type,
                claimantName: f.claimantName.value.trim() || null,
                claimantRelationship: f.claimantRelationship.value || null,
                accountName: f.accountName.value.trim() || null,
                bsb: f.bsb.value.trim() || null,
                account: f.account.value.trim() || null,
                eventDate: f.eventDate.value.trim() || null,
                insuredAmount: income ? numOrNull(f.insuredMonthly.value) : numOrNull(f.sumInsured.value),
                annualEarnings: income ? numOrNull(f.annualEarnings.value) : null,
                waitingPeriodDays: income && waiting !== null ? Math.round(waiting) : null,
                beneficiarySharePercent: type === 'death' ? numOrNull(f.beneficiarySharePercent.value) : null
            };
        }

        // ── Step 3: engage the Claims Agent ──────────────────────────

        function resetResult() {
            runSeq++;
            stopPolling();
            currentApproval = null;
            lastRun = null;
            resultEl.hidden = true;
            processStatus.hidden = true;
            teamsStatus.hidden = true;
        }

        async function processClaim() {
            if (!selected) return;
            resetResult();
            const seq = runSeq;
            processBtn.disabled = true;
            show(processStatus, 'claims-status', '', '<span class="spinner"></span>Engaging the Claims Agent…');

            try {
                const data = await window.zcAgentStream({
                    url: '/claims/process',
                    body: readInputs(),
                    onDelta: (_chunk, fullText) => {
                        if (seq === runSeq && window.engageTabsStreamNarrative) {
                            window.engageTabsStreamNarrative(engageScope, fullText);
                        }
                    },
                    onError: (msg) => {
                        if (seq === runSeq) {
                            show(processStatus, 'claims-status', 'warn', 'Agent error: ' + escapeHtml(msg) + ' — finishing with the offline result…');
                        }
                    }
                });
                if (seq !== runSeq) return;
                if (!data) throw new Error('No response from the Claims Agent');

                if (data.flow === 'not-a-claim') {
                    renderNotAClaim({ note: data.note, links: data.links });
                    show(processStatus, 'claims-status', '', 'Nothing to claim on this case.');
                    return;
                }

                const statusLine = !data.agentConfigured
                    ? 'Processed offline — the Foundry agent isn\'t configured, so Ben\'s Claims desk ran the claims_* tools locally.'
                    : data.agentError
                        ? `Claims Agent unavailable (${shorten(data.agentError, 140)}) — offline result shown.`
                        : 'Claims Agent processed the case.';
                show(processStatus, 'claims-status', data.agentError ? 'warn' : '', escapeHtml(statusLine));

                renderResult(data);
                if (window.engageTabsRender) window.engageTabsRender(engageScope, data);

                // A person must decide: open the approval popup straight away.
                if ((data.flow === 'lodge' || data.flow === 'refer') && data.claimApproval) {
                    openModal(data.claimApproval, { reasons: data.humanApprovalReasons });
                }
            } catch (err) {
                if (seq === runSeq) show(processStatus, 'claims-status', 'error', 'Failed to process: ' + escapeHtml(err.message));
            } finally {
                processBtn.disabled = false;
            }
        }

        function checkItem(c) {
            return `
                <li class="claims-check status-${escapeHtml(c.status)}">
                    <span class="claims-check-icon" aria-hidden="true">${CHECK_ICON[c.status] || '•'}</span>
                    <div class="claims-check-body">
                        <div class="claims-check-head"><strong>${escapeHtml(c.label)}</strong><span class="claims-check-value">${escapeHtml(c.value)}</span></div>
                        <div class="claims-check-detail">${escapeHtml(c.detail)}</div>
                        ${c.source ? `<div class="claims-check-source">${escapeHtml(c.source)}</div>` : ''}
                    </div>
                </li>`;
        }

        function renderResult(data) {
            lastRun = data;
            claimantChecksEl.innerHTML = (data.claimantChecks || []).map(checkItem).join('');
            policyChecksEl.innerHTML = (data.policyChecks || []).map(checkItem).join('');
            packBody.innerHTML = (data.claimPack || []).map(i => `
                <tr>
                    <td>${escapeHtml(i.document)}</td>
                    <td><span class="claims-pack-status st-${escapeHtml(i.status)}">${escapeHtml(PACK_STATUS[i.status] || i.status)}</span></td>
                    <td>${escapeHtml(i.note)}</td>
                </tr>`).join('');
            renderBenefit(data.benefit || {});
            timeframesEl.innerHTML = (data.timeframes || []).map(t => `<dt>${escapeHtml(t.label)}</dt><dd>${escapeHtml(t.value)}</dd>`).join('');
            notesEl.innerHTML = (data.notes || []).length
                ? data.notes.map(n => `<div class="claims-note tone-${escapeHtml(n.tone)}"><strong>${escapeHtml(n.title)}</strong>${escapeHtml(n.text)}</div>`).join('')
                : '<div class="claims-note tone-info">None.</div>';
            const activity = data.activity || [];
            activityCard.hidden = activity.length === 0;
            activityEl.innerHTML = activity.map(e => `<li><strong>${escapeHtml(e.when)}</strong> — ${escapeHtml(e.what)}</li>`).join('');
            renderApprovalSummary(data);

            teamsPanel.hidden = !data.claimApproval;
            lodgedPanel.hidden = true;
            referredPanel.hidden = data.flow !== 'refer';
            holdPanel.hidden = data.flow !== 'hold';
            if (data.flow === 'hold') {
                holdText.textContent = data.lodgeBlockedReason || 'Not ready to lodge.';
            }
            if (data.flow === 'refer') {
                referredTitle.textContent = 'Not lodged — referred to Financial Crime';
                referredText.textContent = (data.lodgeBlockedReason || '') + ' Theo decides on the block in Microsoft Teams.';
                referredHint.textContent = data.lodgeBlockedReason || 'Lodging is disabled.';
                referredLodgeBtn.title = data.lodgeBlockedReason || 'Lodging disabled';
                protectedEl.hidden = true;
            }

            updateTitle.textContent = data.confirmationTitle || 'Claim update draft (for Cara)';
            renderMarkdown(updateEl, data.claimUpdate || '');

            currentApproval = null;
            if (data.claimApproval) renderTeams(data.claimApproval);

            resultEl.hidden = false;
            resultEl.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
        }

        function renderBenefit(b) {
            benefitTitle.textContent = b.title || 'Expected benefit';
            benefitBody.innerHTML = (b.rows || []).map(r => `
                <tr class="kind-${escapeHtml(r.kind)}">
                    <td>${escapeHtml(r.label)}</td>
                    <td class="amount">${escapeHtml(r.display)}</td>
                </tr>`).join('');
            benefitResult.className = 'claims-net mode-' + (b.mode || 'income');
            benefitResultLabel.textContent = b.resultLabel || 'Expected benefit';
            benefitResultValue.textContent = b.resultDisplay || '';
            const notes = [];
            if (b.paidTo) notes.push(`<li><strong>Paid to:</strong> ${escapeHtml(b.paidTo)}</li>`);
            (b.notes || []).forEach(n => notes.push(`<li>${escapeHtml(n)}</li>`));
            if (b.basis) notes.push(`<li><strong>Basis:</strong> ${escapeHtml(b.basis)}</li>`);
            if (b.taxTreatment) notes.push(`<li><strong>Tax:</strong> ${escapeHtml(b.taxTreatment)}</li>`);
            benefitNotes.innerHTML = notes.join('');
            benefitRule.textContent = [b.ruleCited, b.taxRuleCited].filter(Boolean).length
                ? 'Rules cited: ' + [b.ruleCited, b.taxRuleCited].filter(Boolean).join(' · ')
                : '';
        }

        function renderApprovalSummary(data) {
            let pillText;
            let pillCls;
            if (data.flow === 'hold') { pillText = 'Not ready'; pillCls = 'optional'; }
            else if (data.flow === 'refer') { pillText = 'Block decision'; pillCls = 'required'; }
            else { pillText = 'Team Leader'; pillCls = 'required'; }
            approvalPill.textContent = pillText;
            approvalPill.className = 'claims-pill ' + pillCls;
            approvalReasons.innerHTML = (data.humanApprovalReasons || []).map(r => `<li>${escapeHtml(r)}</li>`).join('');
            recommendationEl.textContent = data.recommendation ? 'Recommendation: ' + data.recommendation : '';
            approvalApprover.textContent = data.approver && data.flow !== 'hold' ? 'Approver: ' + data.approver : '';
            approvalApprover.hidden = !approvalApprover.textContent;
        }

        // ── Microsoft Teams approval ─────────────────────────────────

        function renderTeams(a) {
            teamsApprovalId.textContent = a.approvalId;
            if (a.approvalUrl) {
                teamsLink.href = a.approvalUrl;
                teamsLink.textContent = a.approvalUrl;
            } else {
                teamsLink.removeAttribute('href');
                teamsLink.textContent = '(none)';
            }
            const block = isBlock(a);
            teamsTitle.textContent = block ? 'Microsoft Teams approval — block decision' : 'Microsoft Teams approval — lodgement';
            teamsMsg.innerHTML = a.teamsSent
                ? `Adaptive Card <strong>"${escapeHtml(a.cardTitle)}"</strong> posted to <strong>#${escapeHtml(a.teamsChannel || 'claim-approvals')}</strong> via <code>teams_sendApprovalCard</code>. ` +
                  'Approve or reject on the card below (the in-page stand-in for the approver\'s Teams client) or open the approval popup.'
                : `The Teams card wasn't sent (${escapeHtml(a.teamsMessage || 'unknown error')}). Use the approval popup instead.`;
            lodgeBtn.hidden = block;
            lodgeHint.hidden = block;
            teamsStatus.hidden = true;
            renderTeamsPreview(a);
            updateApprovalState(a);
        }

        function renderTeamsPreview(a) {
            const block = isBlock(a);
            const facts = [
                ['Case', a.caseNumber],
                ['Client', a.clientDisplay],
                ['Policy', [a.insurer, a.policyNumber].filter(Boolean).join(' ')],
                ['Cover', a.coverDisplay],
                ['Claim', `${a.claimTypeLabel} — claimant ${a.claimantName} (${(a.claimantRole || '').toLowerCase()})`],
                [block ? 'Amount claimed' : 'Expected benefit', a.expectedBenefitDisplay],
                [block ? 'Payee on the claim' : 'Payee', `${a.payeeName} — ${a.payeeAccount}`],
                ['Lodgement', !block && a.lodgementDate ? `${a.lodgementDate}, after approval` : ''],
                ['Approver', a.approver]
            ].filter(f => f[1]);

            teamsPreview.innerHTML = `
                <div class="tp-channel-head">
                    <span class="tp-logo" aria-hidden="true">T</span>
                    <span>Microsoft Teams · <strong>#${escapeHtml(a.teamsChannel || 'claim-approvals')}</strong></span>
                    <span class="tp-time">${escapeHtml(timeOf(a.createdAt))}</span>
                </div>
                <div class="tp-post">
                    <div class="tp-avatar" aria-hidden="true">B</div>
                    <div class="tp-post-body">
                        <div class="tp-author">Claims Agent (Ben)<span class="tp-app">App</span></div>
                        <div class="tp-card">
                            <div class="tp-card-title">${block ? '🛡️' : '🗂️'} ${escapeHtml(a.cardTitle)}</div>
                            <div class="tp-card-sub">Claims Agent (Ben) · ${escapeHtml(a.caseNumber)} · Approval id ${escapeHtml(a.approvalId)}</div>
                            <dl class="tp-facts">${facts.map(f => `<dt>${escapeHtml(f[0])}</dt><dd>${escapeHtml(f[1])}</dd>`).join('')}</dl>
                            <div class="tp-why-title">Checks</div>
                            <ul class="tp-checks">${(a.checks || []).map(c => `<li>${escapeHtml(c)}</li>`).join('')}</ul>
                            <div class="tp-why-title">Recommendation</div>
                            <div class="tp-rec">${escapeHtml(a.recommendation)}</div>
                            <div class="tp-why-title">Why approval is needed</div>
                            <ul class="tp-why">${reasonsOf(a).map(r => `<li>${escapeHtml(r)}</li>`).join('')}</ul>
                            <div class="tp-note">${block
                                ? 'Nothing is lodged. Approving confirms the block and the referral to Financial Crime.'
                                : 'Ben\'s agent lodges this claim only after it\'s approved. Bank details are masked.'}</div>
                            <div class="tp-actions" data-tp-actions></div>
                        </div>
                    </div>
                </div>`;
            renderPreviewActions(a);
        }

        function renderPreviewActions(a) {
            const el = teamsPreview.querySelector('[data-tp-actions]');
            if (!el || !a) return;
            const s = (a.status || 'pending').toLowerCase();
            if (s === 'pending') {
                el.innerHTML =
                    `<button type="button" class="tp-btn tp-approve" data-teams-action="approve">${isBlock(a) ? 'Approve block' : 'Approve'}</button>` +
                    '<button type="button" class="tp-btn tp-reject" data-teams-action="reject">Reject</button>' +
                    `<a class="tp-btn" href="${escapeHtml(a.approvalUrl || '#')}" target="_blank" rel="noopener">Open case in Caldova</a>`;
                return;
            }
            const who = a.decidedBy ? ` by ${a.decidedBy}` : '';
            const text = {
                approved: `✅ Approved${who}`,
                rejected: `❌ Rejected${who}`,
                lodged: `✅ Approved${who} · Lodged ${a.lodgementReference || ''}`.trim(),
                blocked: `🛡️ Block approved${who} — no claim lodged`,
                cancelled: 'Superseded by a newer approval'
            }[s] || s;
            el.innerHTML = `<div class="tp-decided tp-${escapeHtml(s)}">${escapeHtml(text)}</div>`;
        }

        function updateApprovalState(a) {
            currentApproval = Object.assign({}, currentApproval || {}, a);
            const s = (currentApproval.status || 'pending').toLowerCase();
            const block = isBlock(currentApproval);
            teamsPill.className = 'claims-pill status-' + s;
            teamsPill.textContent = s === 'pending' && !currentApproval.teamsSent ? 'Pending' : statusText(currentApproval);

            // Lodging stays disabled until a person's approval is recorded.
            lodgeBtn.disabled = block || s !== 'approved';
            lodgeBtn.textContent = s === 'lodged' ? 'Lodged' : 'Lodge claim';
            lodgeHint.textContent = s === 'lodged' ? lodgedText(currentApproval) : (LODGE_HINT[s] || '');
            popupBtn.disabled = !(s === 'pending' || (s === 'approved' && !block));

            renderPreviewActions(currentApproval);
            if (block) renderReferredState(currentApproval);
            else renderLodgedState(currentApproval);

            if (currentApproval.claimUpdate && (s === 'lodged' || s === 'blocked')) {
                renderMarkdown(updateEl, currentApproval.claimUpdate);
            }
            const base = (lastRun && lastRun.confirmationTitle) || 'Claim update draft (for Cara)';
            updateTitle.textContent = base + (
                s === 'rejected' && !block ? ' — not sent (lodgement rejected)'
                    : s === 'cancelled' ? ' — superseded'
                        : '');
            if (s === 'pending') startPolling(); else stopPolling();
        }

        function renderLodgedState(a) {
            if ((a.status || '') !== 'lodged') {
                lodgedPanel.hidden = true;
                return;
            }
            lodgedPanel.hidden = false;
            lodgedTitle.textContent = `Claim lodged with ${a.insurer || 'the insurer'} — reference ${a.lodgementReference || ''}`.trim();
            lodgedTextEl.textContent = `Lodged ${a.lodgementDate || ''} after ${a.decidedBy || 'the approver'} approved. ` +
                `${a.insurer || 'The insurer'} now works to the Life Insurance Code of Practice timeframes.` +
                ((a.insurerOutcome || []).length ? ' What happened next:' : '');
            outcomeTimeline.innerHTML = (a.insurerOutcome || [])
                .map(e => `<li><strong>${escapeHtml(e.when)}</strong> — ${escapeHtml(e.what)}</li>`).join('');
            if (a.outcomeLabel) {
                outcomePill.hidden = false;
                outcomePill.textContent = a.outcomeLabel;
                outcomePill.className = 'claims-pill outcome-' + slug(a.outcomeLabel);
            } else {
                outcomePill.hidden = true;
            }
            outcomeSummary.hidden = !a.outcomeSummary;
            outcomeSummary.textContent = a.outcomeSummary ? `Outcome: ${a.outcomeLabel ? a.outcomeLabel + ' — ' : ''}${a.outcomeSummary}` : '';
        }

        function renderReferredState(a) {
            referredPanel.hidden = false;
            const s = (a.status || 'pending').toLowerCase();
            referredPill.textContent = statusText(a);
            referredPill.className = 'claims-pill ' + (s === 'blocked' ? 'status-blocked' : s === 'pending' ? 'status-pending' : 'status-' + s);
            if (s === 'blocked') {
                referredTitle.textContent = `Blocked — nothing lodged with ${a.insurer || 'the insurer'}`;
                referredText.textContent = `${a.decidedBy || 'The Team Leader'} approved the block. Nothing was lodged with ${a.insurer || 'the insurer'}, and the case stays with Financial Crime.`;
                protectedEl.hidden = false;
                protectedAmount.textContent = a.outcomeSummary || 'No claim lodged';
                protectedNote.textContent = a.protectedNote || '';
                referredHint.textContent = 'Lodging is disabled — the claim is blocked.';
            } else if (s === 'rejected') {
                referredTitle.textContent = 'Block not approved — still not lodged';
                referredText.textContent = `${a.decidedBy || 'The Team Leader'} didn't approve the block. The claim stays with Financial Crime for review; nothing is lodged until the claimant is verified.`;
                protectedEl.hidden = true;
            } else if (s === 'cancelled') {
                referredTitle.textContent = 'Superseded — engage the agent again';
                protectedEl.hidden = true;
            }
        }

        // Keep the inline card, the popup and the deep-link banner in step
        // whenever an approval changes.
        function handleApprovalUpdate(a) {
            if (!a || !a.approvalId) return;
            if (currentApproval && currentApproval.approvalId === a.approvalId) updateApprovalState(a);
            syncModal(a);
            if (deeplinkApprovalId === a.approvalId) renderDeeplinkBanner(a);
        }

        async function postJson(url, body) {
            const res = await fetch(url, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(body || {})
            });
            let data = {};
            try { data = await res.json(); } catch (_) { /* empty body */ }
            if (!res.ok) {
                if (data && data.approval) handleApprovalUpdate(data.approval);
                throw new Error((data && data.error) || ('HTTP ' + res.status));
            }
            return data;
        }

        async function decide(action) {
            if (!currentApproval) return;
            const approval = currentApproval;
            teamsPreview.querySelectorAll('[data-teams-action]').forEach(b => { b.disabled = true; });
            show(teamsStatus, 'claims-teams-status', '', '<span class="spinner"></span>Recording the decision…');
            try {
                const data = await postJson(
                    `/claims/approvals/${encodeURIComponent(approval.approvalId)}/${action}`,
                    { decidedBy: `${approval.approver || 'Approver'} (in-page Teams fallback)` });
                handleApprovalUpdate(data);
                show(teamsStatus, 'claims-teams-status', '',
                    escapeHtml(`Decision recorded: ${statusText(data)}${data.decidedBy ? ' by ' + data.decidedBy : ''}.`));
            } catch (err) {
                show(teamsStatus, 'claims-teams-status', 'error', 'Failed: ' + escapeHtml(err.message));
                renderPreviewActions(currentApproval);
            }
        }

        async function lodgeApproval(approvalId) {
            const data = await postJson(`/claims/approvals/${encodeURIComponent(approvalId)}/lodge`, {});
            handleApprovalUpdate(data);
            return data;
        }

        async function lodgeInline() {
            if (!currentApproval) return;
            lodgeBtn.disabled = true;
            show(teamsStatus, 'claims-teams-status', '', '<span class="spinner"></span>Lodging the claim…');
            try {
                const data = await lodgeApproval(currentApproval.approvalId);
                show(teamsStatus, 'claims-teams-status', '', escapeHtml(lodgedText(data)));
            } catch (err) {
                show(teamsStatus, 'claims-teams-status', 'error', 'Lodgement refused: ' + escapeHtml(err.message));
                if (currentApproval) lodgeBtn.disabled = isBlock(currentApproval) || (currentApproval.status || '') !== 'approved';
            }
        }

        // Picks up decisions made elsewhere — e.g. from the Teams card link
        // opened in another tab.
        function startPolling() {
            stopPolling();
            if (!currentApproval) return;
            const id = currentApproval.approvalId;
            pollTimer = setInterval(async () => {
                if (!root.isConnected || !currentApproval || currentApproval.approvalId !== id) {
                    stopPolling();
                    return;
                }
                try {
                    const res = await fetch(`/claims/approvals/${encodeURIComponent(id)}`);
                    if (!res.ok) return;
                    const a = await res.json();
                    if (currentApproval && currentApproval.approvalId === id && a.status !== currentApproval.status) {
                        handleApprovalUpdate(a);
                        show(teamsStatus, 'claims-teams-status', '',
                            escapeHtml(`Approval updated: ${statusText(a)}${a.decidedBy ? ' by ' + a.decidedBy : ''}.`));
                    }
                } catch (_) { /* transient — try again on the next tick */ }
            }, POLL_MS);
        }

        function stopPolling() {
            if (pollTimer) {
                clearInterval(pollTimer);
                pollTimer = null;
            }
        }

        // ── Approval popup ───────────────────────────────────────────

        function openModal(a, opts) {
            if (!a || !a.approvalId) return;
            const options = opts || {};
            const block = isBlock(a);
            modalApproval = a;
            modalTitle.textContent = block ? 'Team Leader decision — block this claim?' : 'Claim lodgement approval';
            modalSub.textContent = `${a.cardTitle || ''} · Microsoft Teams`;
            if (options.decision === 'approve' || options.decision === 'reject') {
                modalDeeplink.hidden = false;
                modalDeeplink.textContent = `You followed the ${options.decision === 'approve' ? (block ? 'Approve block' : 'Approve') : 'Reject'} action on the Microsoft Teams card — confirm it below.`;
            } else {
                modalDeeplink.hidden = true;
            }
            modalReasons.innerHTML = reasonsOf(a, options.reasons).map(r => `<li>${escapeHtml(r)}</li>`).join('');
            modalCase.textContent = a.caseNumber || '';
            modalApprovalId.textContent = a.approvalId;
            modalClient.textContent = a.clientDisplay || a.clientName || '';
            modalPolicy.textContent = [a.insurer, a.policyNumber].filter(Boolean).join(' ') + (a.coverDisplay ? ` — ${a.coverDisplay}` : '');
            modalType.textContent = `${a.claimTypeLabel || ''} — claimant ${a.claimantName || ''} (${(a.claimantRole || '').toLowerCase()})`;
            modalPayee.textContent = `${a.payeeName || ''} — ${a.payeeAccount || ''}`;
            modalBenefitLabel.textContent = block ? 'Amount claimed' : 'Expected benefit';
            modalBenefit.textContent = a.expectedBenefitDisplay || '';
            modalRecommendation.textContent = a.recommendation || '';
            modalExplainer.innerHTML = block
                ? 'Stands in for <strong>Approve block</strong> on the Teams card. Nothing is lodged either way — approving confirms the block and the referral to Financial Crime.'
                : 'Stands in for <strong>Approve</strong> on the Teams card. Lodging stays locked until the approval is recorded.';
            modalApproveBtn.textContent = block ? 'Approve block' : 'Approve';
            modalLodgeBtn.hidden = block;
            modalStatus.hidden = true;
            modalAgent.hidden = true;
            modalAgentBody.innerHTML = '';
            syncModalButtons(a, true);
            modalEl.hidden = false;

            const focusBtn = options.decision === 'reject' ? modalRejectBtn
                : (!modalApproveBtn.disabled ? modalApproveBtn : modalLodgeBtn);
            if (focusBtn && !focusBtn.disabled && !focusBtn.hidden) focusBtn.focus();
        }

        function syncModalButtons(a, announce) {
            const s = (a.status || 'pending').toLowerCase();
            const block = isBlock(a);
            modalApproveBtn.disabled = s !== 'pending';
            modalRejectBtn.disabled = !(s === 'pending' || (s === 'approved' && !block));
            modalLodgeBtn.disabled = block || s !== 'approved';
            modalLodgeBtn.textContent = s === 'lodged' ? 'Lodged' : 'Lodge claim';
            if (announce && s !== 'pending') {
                const who = a.decidedBy ? ` by ${a.decidedBy}` : '';
                const text = {
                    approved: `Already approved${who} — the claim can now be lodged.`,
                    rejected: `Rejected${who} — ${block ? 'the claim stays with Financial Crime; nothing is lodged.' : 'the claim will not be lodged.'}`,
                    lodged: lodgedText(a),
                    blocked: `Block approved${who}. ${a.outcomeSummary || 'No claim lodged.'}`,
                    cancelled: 'Superseded by a newer approval for this case.'
                }[s] || s;
                show(modalStatus, 'claims-modal-status', '', escapeHtml(text));
            }
        }

        function syncModal(a) {
            if (!modalApproval || modalEl.hidden || modalApproval.approvalId !== a.approvalId) return;
            modalApproval = Object.assign({}, modalApproval, a);
            syncModalButtons(modalApproval, false);
        }

        function closeModal() {
            modalEl.hidden = true;
            modalApproval = null;
        }

        async function approveViaModal() {
            if (!modalApproval) return;
            const a = modalApproval;
            const block = isBlock(a);
            modalApproveBtn.disabled = true;
            modalRejectBtn.disabled = true;
            modalLodgeBtn.disabled = true;
            show(modalStatus, 'claims-modal-status', '', block
                ? '<span class="spinner"></span>Recording the block decision…'
                : '<span class="spinner"></span>Approving and resuming the Claims Agent conversation…');
            try {
                const data = await postJson(
                    `/claims/approvals/${encodeURIComponent(a.approvalId)}/agent-approve`,
                    { decidedBy: `${a.approver || 'Approver'} (approval popup)` });
                const approval = data.approval || {};
                handleApprovalUpdate(approval);
                if (data.agentNarrative) {
                    modalAgent.hidden = false;
                    renderMarkdown(modalAgentBody, data.agentNarrative);
                }
                const parts = [];
                const s = approval.status || '';
                if (s === 'lodged') parts.push(lodgedText(approval));
                else if (s === 'blocked') parts.push(`Block approved${approval.decidedBy ? ' by ' + approval.decidedBy : ''}. ${approval.outcomeSummary || 'No claim lodged.'}`);
                else parts.push(`Approval recorded${approval.decidedBy ? ' by ' + approval.decidedBy : ''}.`);
                if (!block && s !== 'lodged') {
                    if (data.agentError) parts.push(`Agent resume failed (${shorten(data.agentError, 140)}) — lodge the claim below.`);
                    else if (!data.agentConfigured) parts.push('Offline — lodge the claim below.');
                    else if (!data.agentResumeAttempted) parts.push('No agent conversation to resume — lodge the claim below.');
                }
                show(modalStatus, 'claims-modal-status', '', escapeHtml(parts.join(' ')));
                if (!modalLodgeBtn.disabled && !modalLodgeBtn.hidden) modalLodgeBtn.focus();
            } catch (err) {
                show(modalStatus, 'claims-modal-status', 'error', 'Failed: ' + escapeHtml(err.message));
                if (modalApproval) syncModalButtons(modalApproval, false);
            }
        }

        async function rejectViaModal() {
            if (!modalApproval) return;
            const a = modalApproval;
            modalApproveBtn.disabled = true;
            modalRejectBtn.disabled = true;
            modalLodgeBtn.disabled = true;
            show(modalStatus, 'claims-modal-status', '', '<span class="spinner"></span>Recording the rejection…');
            try {
                const data = await postJson(
                    `/claims/approvals/${encodeURIComponent(a.approvalId)}/reject`,
                    { decidedBy: `${a.approver || 'Approver'} (approval popup)` });
                handleApprovalUpdate(data);
                show(modalStatus, 'claims-modal-status', '', escapeHtml(isBlock(a)
                    ? `Block not approved${data.decidedBy ? ' by ' + data.decidedBy : ''} — the claim stays with Financial Crime; nothing is lodged.`
                    : `Rejected${data.decidedBy ? ' by ' + data.decidedBy : ''} — the claim will not be lodged.`));
            } catch (err) {
                show(modalStatus, 'claims-modal-status', 'error', 'Failed: ' + escapeHtml(err.message));
                if (modalApproval) syncModalButtons(modalApproval, false);
            }
        }

        async function lodgeViaModal() {
            if (!modalApproval) return;
            modalLodgeBtn.disabled = true;
            show(modalStatus, 'claims-modal-status', '', '<span class="spinner"></span>Lodging the claim…');
            try {
                const data = await lodgeApproval(modalApproval.approvalId);
                show(modalStatus, 'claims-modal-status', '', escapeHtml(lodgedText(data)));
            } catch (err) {
                show(modalStatus, 'claims-modal-status', 'error', 'Lodgement refused: ' + escapeHtml(err.message));
                if (modalApproval) syncModalButtons(modalApproval, false);
            }
        }

        // ── Teams card deep link: ?approvalId=CLM-…[&decision=…] ─────

        function renderDeeplinkBanner(a) {
            show(deeplinkEl, 'claims-deeplink', '',
                `Opened from the Microsoft Teams approval card — <strong>${escapeHtml(a.approvalId)}</strong> for case ${escapeHtml(a.caseNumber)} (${escapeHtml(statusText(a))}).`);
        }

        async function handleDeepLink() {
            const params = new URLSearchParams(window.location.search);
            const approvalId = params.get('approvalId');
            const caseParam = params.get('caseNumber');
            if (!approvalId) return caseParam;

            try {
                const res = await fetch(`/claims/approvals/${encodeURIComponent(approvalId)}`);
                let a = {};
                try { a = await res.json(); } catch (_) { /* not JSON */ }
                if (!res.ok) throw new Error((a && a.error) || ('HTTP ' + res.status));
                deeplinkApprovalId = a.approvalId;
                renderDeeplinkBanner(a);
                openModal(a, { decision: (params.get('decision') || '').toLowerCase() });
                return a.caseNumber;
            } catch (err) {
                show(deeplinkEl, 'claims-deeplink', 'error',
                    'This approval link could not be opened: ' + escapeHtml(err.message) +
                    '. Approvals are held in memory, so they reset when the app restarts.');
                return null;
            }
        }

        // ── Wiring ───────────────────────────────────────────────────

        select.addEventListener('change', () => selectCase(select.value));
        refreshBtn.addEventListener('click', () => loadCases());
        form.addEventListener('submit', ev => ev.preventDefault());
        form.elements.claimType.addEventListener('change', applyTypeVisibility);
        processBtn.addEventListener('click', processClaim);
        teamsPreview.addEventListener('click', ev => {
            const btn = ev.target.closest('[data-teams-action]');
            if (!btn) return;
            ev.preventDefault();
            decide(btn.dataset.teamsAction);
        });
        popupBtn.addEventListener('click', () => { if (currentApproval) openModal(currentApproval); });
        lodgeBtn.addEventListener('click', lodgeInline);
        modalApproveBtn.addEventListener('click', approveViaModal);
        modalRejectBtn.addEventListener('click', rejectViaModal);
        modalLodgeBtn.addEventListener('click', lodgeViaModal);
        modalEl.addEventListener('click', ev => {
            if (ev.target && ev.target.closest && ev.target.closest('[data-modal-dismiss]')) closeModal();
        });
        if (escHandler) document.removeEventListener('keydown', escHandler);
        escHandler = ev => {
            if (ev.key === 'Escape' && !modalEl.hidden) closeModal();
        };
        document.addEventListener('keydown', escHandler);

        handleDeepLink().then(preferred => loadCases(preferred));
    }

    init();

    if (!window.__claimsScenarioEnhancedHook) {
        window.__claimsScenarioEnhancedHook = true;
        const hook = () => init();
        if (typeof Blazor !== 'undefined' && typeof Blazor.addEventListener === 'function') {
            Blazor.addEventListener('enhancedload', hook);
        } else {
            document.addEventListener('enhancedload', hook);
        }
    }
})();
