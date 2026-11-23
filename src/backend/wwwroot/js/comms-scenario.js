// Client Communications page — "Try it out" wiring (case list, Cara's case
// summary, notes and the drafts for every audience: client letters, call
// scripts, referral-partner updates and SMS).
// Loaded as a permanent script from App.razor so init runs reliably on full
// page load AND on Blazor enhanced navigation.

    (function () {
        // Draft groups, in the order they appear on the page.
        const GROUPS = [
            { audience: 'client', icon: '✉️', label: 'Client letters & summaries' },
            { audience: 'call', icon: '📞', label: 'Calls — scripts and talking points' },
            { audience: 'partner', icon: '🤝', label: 'Referral partner' },
            { audience: 'sms', icon: '📱', label: 'SMS' }
        ];

        function init() {
        const root = document.querySelector('.comms-panel');
        if (!root || root.dataset.wired === '1') return;
        root.dataset.wired = '1';

        const $ = sel => root.querySelector(sel);
        const selectEl = $('#comms-case-select');
        const refreshBtn = $('#comms-refresh-btn');
        const caseSummaryEl = $('#comms-case-summary');
        const step2El = $('#comms-step-2');
        const processBtn = $('#comms-process-btn');
        const processStatus = $('#comms-process-status');
        const resultsEl = $('#comms-results');
        const draftsEl = $('#comms-drafts');
        const stagePill = $('#comms-stage-pill');
        const outcomePill = $('#comms-outcome-pill');
        const sentimentLabel = $('#comms-sentiment-label');
        const sentimentText = $('#comms-sentiment-text');
        const summaryText = $('#comms-summary-text');
        const nextStepsEl = $('#comms-next-steps');
        const flagsEl = $('#comms-flags');
        const flagsList = $('#comms-flags-list');
        const approvalPill = $('#comms-approval-pill');
        const approvalReason = $('#comms-approval-reason');
        const notesEl = $('#comms-notes');
        const adviserNoteEl = $('#comms-adviser-note');
        const partnerNoteEl = $('#comms-partner-note');
        const sourcesEl = $('#comms-sources');
        const approverEl = $('#comms-approver');
        const groupsEl = $('#comms-draft-groups');
        const engageScope = $('.engage-tabs-scope');

        let selectedCase = null;

        function escapeHtml(s) {
            return String(s ?? '').replace(/[&<>"']/g, c => ({ '&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;' }[c]));
        }

        function hideResults() {
            resultsEl.hidden = true;
            draftsEl.hidden = true;
        }

        async function loadCases() {
            selectEl.disabled = true;
            selectEl.innerHTML = '<option value="">Loading cases…</option>';
            caseSummaryEl.hidden = true;
            step2El.hidden = true;
            processBtn.disabled = true;
            processStatus.hidden = true;
            selectedCase = null;
            hideResults();
            try {
                const res = await fetch('/communications/cases');
                if (!res.ok) throw new Error('HTTP ' + res.status);
                const cases = await res.json();
                if (!cases.length) {
                    selectEl.innerHTML = '<option value="">No cases yet — open one with the Onboarding Agent first</option>';
                    return;
                }
                selectEl.innerHTML =
                    '<option value="">— select a case —</option>' +
                    cases.map(c => `<option value="${escapeHtml(c.caseNumber)}">${escapeHtml(c.caseNumber)} · ${escapeHtml(c.clientName)} · ${escapeHtml(c.requestType)}</option>`).join('');
                selectEl.disabled = false;
            } catch (err) {
                selectEl.innerHTML = `<option value="">Failed to load cases: ${escapeHtml(err.message)}</option>`;
            }
        }

        function referredByText(referredBy) {
            return !referredBy || /^\s*(none|existing)/i.test(referredBy) ? 'Existing client' : referredBy;
        }

        async function selectCase(caseNumber) {
            hideResults();
            processStatus.hidden = true;
            if (!caseNumber) {
                selectedCase = null;
                caseSummaryEl.hidden = true;
                step2El.hidden = true;
                processBtn.disabled = true;
                return;
            }
            try {
                const res = await fetch(`/communications/cases/${encodeURIComponent(caseNumber)}`);
                if (!res.ok) throw new Error('HTTP ' + res.status);
                selectedCase = await res.json();
                const c = selectedCase;
                $('#comms-case-client').textContent = [c.clientName, c.clientEmail].filter(Boolean).join(' · ');
                $('#comms-case-number').textContent = c.clientNumber || '—';
                $('#comms-case-type').textContent = c.requestType || '—';
                $('#comms-case-priority').textContent = `${c.priority || '—'}${c.priorityReason ? ' — ' + c.priorityReason : ''}`;
                $('#comms-case-adviser').textContent = c.adviserName || '—';
                $('#comms-case-referred').textContent = referredByText(c.referredBy);
                $('#comms-case-pref').textContent = c.preferredContact || '—';
                $('#comms-case-amount').textContent = c.amount || '—';
                $('#comms-case-cover').textContent = c.cover || '—';
                $('#comms-case-request').textContent = c.requestSummary || '—';
                caseSummaryEl.hidden = false;
                // Reveal step 2 (engage agent) now that a case is picked.
                step2El.hidden = false;
                processBtn.disabled = false;
            } catch (err) {
                selectedCase = null;
                caseSummaryEl.hidden = true;
                step2El.hidden = true;
                processBtn.disabled = true;
                processStatus.hidden = false;
                processStatus.className = 'comms-status error';
                processStatus.textContent = 'Failed to load case: ' + err.message;
            }
        }

        function outcomeClass(outcome) {
            const o = String(outcome || '').toLowerCase();
            if (o.startsWith('completed')) return 'completed';
            if (o.startsWith('blocked') || o.startsWith('declined')) return 'blocked';
            if (o.startsWith('partial')) return 'partial';
            return '';
        }

        function smsInfo(text) {
            const len = String(text || '').length;
            // GSM-7 fits 160 characters in one SMS (153 per part when split);
            // anything outside plain ASCII switches to UCS-2 (70 / 67).
            const unicode = /[^\x00-\x7F]/.test(text || '');
            const single = unicode ? 70 : 160;
            const multi = unicode ? 67 : 153;
            const parts = len <= single ? 1 : Math.ceil(len / multi);
            return `${len} characters · ${parts} SMS${parts > 1 ? ' parts' : ''}${unicode ? ' (unicode)' : ''}`;
        }

        function renderCompliance(items) {
            return (items || []).map(i => {
                const status = String(i.status || '').toLowerCase();
                const cls = status === 'pass' ? 'pass' : status === 'warn' ? 'warn' : 'na';
                const mark = cls === 'pass' ? '✓' : cls === 'warn' ? '!' : '–';
                return `<li class="${cls}"><span class="mark" aria-hidden="true">${mark}</span>` +
                    `<span><span class="lbl">${escapeHtml(i.label)}</span>` +
                    (i.note ? ` — <span class="note">${escapeHtml(i.note)}</span>` : '') +
                    `</span></li>`;
            }).join('');
        }

        function renderDraft(d) {
            const isSms = d.audience === 'sms';
            const card = document.createElement('article');
            card.className = 'comms-draft';
            const approval = d.approvalRequired
                ? '<span class="comms-approval-pill required">Human approval required</span>'
                : '<span class="comms-approval-pill optional">Spot-check</span>';
            card.innerHTML =
                `<div class="comms-draft-head">` +
                    `<span class="comms-draft-title">${escapeHtml(d.audienceLabel)}</span>` +
                    `<span class="comms-draft-channel">${escapeHtml(d.channel)}</span>` +
                    approval +
                `</div>` +
                `<div class="comms-draft-meta">` +
                    `<span class="k">To</span><span>${escapeHtml(d.recipient)}</span>` +
                    (d.subject ? `<span class="k">${d.audience === 'call' ? 'Purpose' : 'Subject'}</span><span>${escapeHtml(d.subject)}</span>` : '') +
                `</div>` +
                `<div class="comms-channel-body agent-md${isSms ? ' comms-sms' : ''}"></div>` +
                (isSms ? `<div class="comms-sms-count">${escapeHtml(smsInfo(d.body))}</div>` : '') +
                `<div class="comms-draft-foot">` +
                    `<div><h5>Tone</h5><p class="comms-tone">${escapeHtml(d.tone)}</p>` +
                    `<h5 style="margin-top:0.55rem">Approval</h5><p class="comms-tone">${escapeHtml(d.approvalReason)}</p></div>` +
                    `<div><h5>Compliance check</h5><ul class="comms-compliance">${renderCompliance(d.compliance)}</ul></div>` +
                `</div>`;
            const body = card.querySelector('.comms-channel-body');
            if (isSms) {
                body.textContent = d.body || '';
            } else if (typeof window.zcRenderMarkdown === 'function') {
                window.zcRenderMarkdown(body, d.body || '');
            } else {
                body.textContent = d.body || '';
            }
            return card;
        }

        function renderDrafts(data) {
            const drafts = data.drafts || [];
            groupsEl.innerHTML = '';
            const known = GROUPS.map(g => g.audience);
            const groups = GROUPS.concat(
                [...new Set(drafts.map(d => d.audience).filter(a => !known.includes(a)))]
                    .map(a => ({ audience: a, icon: '📝', label: a })));
            groups.forEach(g => {
                const items = drafts.filter(d => d.audience === g.audience);
                if (!items.length) return;
                const section = document.createElement('div');
                section.className = 'comms-audience';
                section.innerHTML =
                    `<div class="comms-audience-head"><span aria-hidden="true">${g.icon}</span> ${escapeHtml(g.label)}` +
                    `<span class="comms-audience-count">${items.length} draft${items.length > 1 ? 's' : ''}</span></div>`;
                items.forEach(d => section.appendChild(renderDraft(d)));
                groupsEl.appendChild(section);
            });
            approverEl.textContent = data.approver || '—';
        }

        function renderNotes(data) {
            const adviserNote = data.adviserNote || '';
            const partnerNote = data.partnerNote || '';
            const sources = data.sources || [];
            adviserNoteEl.hidden = !adviserNote;
            $('#comms-adviser-note-text').textContent = adviserNote;
            partnerNoteEl.hidden = !partnerNote;
            $('#comms-partner-note-text').textContent = partnerNote;
            sourcesEl.hidden = !sources.length;
            $('#comms-sources-list').innerHTML = sources.map(s => `<li>${escapeHtml(s)}</li>`).join('');
            notesEl.hidden = !adviserNote && !partnerNote && !sources.length;
        }

        function renderSummary(data) {
            const s = data.summary || {};
            stagePill.textContent = data.stage || '';
            outcomePill.textContent = data.outcome || '';
            outcomePill.className = 'comms-outcome-pill ' + outcomeClass(data.outcome);
            sentimentLabel.textContent = s.sentimentLabel || '';
            sentimentText.textContent = s.sentiment || '';
            summaryText.textContent = s.whatHappened || '';
            nextStepsEl.innerHTML = (s.whatsNext || []).map(step => `<li>${escapeHtml(step)}</li>`).join('');

            const flags = data.vulnerabilityFlags || [];
            flagsEl.hidden = false;
            flagsList.innerHTML = flags.length
                ? flags.map(f => `<li>${escapeHtml(f)}</li>`).join('')
                : '<li>No vulnerability indicators on this case.</li>';
            approvalPill.textContent = data.humanApprovalRequired ? 'Human approval required' : 'Spot-check only';
            approvalPill.className = 'comms-approval-pill ' + (data.humanApprovalRequired ? 'required' : 'optional');
            approvalReason.textContent = [data.humanApprovalReason, data.approver ? 'Approver: ' + data.approver + '.' : '']
                .filter(Boolean).join(' ');
            renderNotes(data);
        }

        async function processCase() {
            if (!selectedCase) return;
            processBtn.disabled = true;
            processStatus.hidden = false;
            processStatus.className = 'comms-status';
            processStatus.innerHTML = '<span class="spinner"></span>Cara is reading the case and drafting for each audience…';
            hideResults();

            try {
                const data = await window.zcAgentStream({
                    url: '/communications/process',
                    body: { caseNumber: selectedCase.caseNumber },
                    onDelta: (_chunk, fullText) => {
                        if (window.engageTabsStreamNarrative) {
                            window.engageTabsStreamNarrative(engageScope, fullText);
                        }
                    },
                    onError: (msg) => {
                        processStatus.className = 'comms-status error';
                        processStatus.textContent = 'Agent error: ' + msg;
                    }
                });
                if (!data) throw new Error('No response from the agent');

                processStatus.className = 'comms-status';
                if (data.agentConfigured && data.agentNotes) {
                    processStatus.textContent = 'Client Communications Agent drafted the messages — Cara\'s live narrative is in the Agent Narrative tab; the review-ready drafts are below.';
                } else if (data.agentConfigured) {
                    processStatus.className = 'comms-status error';
                    processStatus.textContent = 'Live agent unavailable' + (data.agentError ? ' (' + data.agentError + ')' : '') +
                        ' — showing Cara\'s offline drafts.';
                } else {
                    processStatus.textContent = 'Drafted offline (Foundry agent not configured) — Cara\'s drafts for this case are below.';
                }

                renderSummary(data);
                renderDrafts(data);

                resultsEl.hidden = false;
                draftsEl.hidden = false;
                resultsEl.scrollIntoView({ behavior: 'smooth', block: 'start' });

                // Surface input + raw output in the Engage Agent sub-tabs.
                if (window.engageTabsRender) {
                    window.engageTabsRender(engageScope, data);
                }
            } catch (err) {
                processStatus.className = 'comms-status error';
                processStatus.textContent = 'Failed to draft: ' + err.message;
            } finally {
                processBtn.disabled = !selectedCase;
            }
        }

        selectEl.addEventListener('change', () => selectCase(selectEl.value));
        refreshBtn.addEventListener('click', loadCases);
        processBtn.addEventListener('click', processCase);

        loadCases();
        }

        init();

        if (!window.__commsScenarioEnhancedHook) {
            window.__commsScenarioEnhancedHook = true;
            const hook = () => init();
            if (typeof Blazor !== 'undefined' && typeof Blazor.addEventListener === 'function') {
                Blazor.addEventListener('enhancedload', hook);
            } else {
                document.addEventListener('enhancedload', hook);
            }
        }
    })();
