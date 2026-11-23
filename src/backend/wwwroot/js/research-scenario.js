// Insurance Research Agent page (/agents/insurance-research) — "Try it out".
// Loaded as a permanent script from App.razor so init runs reliably on full
// page load AND on Blazor enhanced navigation (inline <script> tags inside
// razor pages do not re-execute on enhanced nav).
//
// Flow (advice cases): pick a Client Case → engage Ivy's agent
// (/research/process, streamed) → the insurers compared (table + chart, the
// insurer recommended for Amelia's review highlighted with the reasons) →
// definitions that matter → stepped vs level over 10 years → build the
// comparison workbook (/research/workbook) → chat (/research/chat).
// Claim cases get a policy wording lookup for the claim's insurer instead,
// with a handover to the Claims Agent.
(function () {
    const MERMAID_URL = 'https://cdn.jsdelivr.net/npm/mermaid@11/dist/mermaid.esm.min.mjs';
    const STEPPED_COLOUR = '#14b8a6';
    const LEVEL_COLOUR = '#94a3b8';
    const INSURER_COLOURS = ['#0f766e', '#1d4ed8', '#9333ea', '#b45309'];
    let mermaidSeq = 0;

    // ── Mermaid (agent narrative only; the page's own charts are SVG) ───────
    function loadMermaid() {
        if (!window.__irMermaidPromise) {
            window.__irMermaidPromise = import(MERMAID_URL).then(mod => {
                const mermaid = mod.default || mod;
                mermaid.initialize({
                    startOnLoad: false,
                    securityLevel: 'strict',
                    theme: 'neutral',
                    suppressErrorRendering: true,
                    xyChart: { width: 720, height: 340, titleFontSize: 15 },
                    themeVariables: {
                        background: 'transparent',
                        xyChart: { backgroundColor: 'transparent', plotColorPalette: '#14b8a6, #94a3b8', titleColor: '#0b1f3a' }
                    }
                });
                return mermaid;
            });
        }
        return window.__irMermaidPromise;
    }

    async function upgradeMermaidBlocks(container) {
        if (!container) return;
        const nodes = Array.from(container.querySelectorAll(
            '.agent-md pre.mermaid:not([data-ir-mmd]), .agent-md div.mermaid:not([data-ir-mmd]), .agent-md code.language-mermaid:not([data-ir-mmd])'));
        for (const node of nodes) {
            node.setAttribute('data-ir-mmd', 'pending');
            const target = node.tagName === 'CODE' && node.parentElement && node.parentElement.tagName === 'PRE' ? node.parentElement : node;
            const source = (node.textContent || '').trim();
            if (!source) continue;
            try {
                const mermaid = await loadMermaid();
                const result = await mermaid.render('ir-mmd-' + Date.now() + '-' + (mermaidSeq++), source);
                if (!result || !result.svg || !target.isConnected) continue;
                const wrap = document.createElement('div');
                wrap.className = 'ir-mermaid-rendered';
                wrap.setAttribute('data-ir-mmd', 'done');
                wrap.innerHTML = result.svg;
                target.replaceWith(wrap);
            } catch (_) {
                node.setAttribute('data-ir-mmd', 'failed');
            }
        }
    }

    // ── Formatting helpers ─────────────────────────────────────────────────
    function escapeHtml(s) {
        return String(s ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
    }

    function money(v) {
        const n = Number(v);
        if (v === null || v === undefined || v === '' || !isFinite(n)) return '—';
        const whole = Math.abs(n - Math.round(n)) < 0.005;
        return n.toLocaleString('en-AU', {
            style: 'currency', currency: 'AUD',
            minimumFractionDigits: whole ? 0 : 2, maximumFractionDigits: whole ? 0 : 2
        });
    }

    // "2026-11-03" → "Tue 3 Nov 2026" (Australian date, no time zone shift).
    function auDate(iso) {
        const m = /^(\d{4})-(\d{2})-(\d{2})/.exec(String(iso || ''));
        if (!m) return iso || '';
        const d = new Date(Number(m[1]), Number(m[2]) - 1, Number(m[3]));
        const days = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'];
        const months = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
        return `${days[d.getDay()]} ${d.getDate()} ${months[d.getMonth()]} ${d.getFullYear()}`;
    }

    function shortMoney(v) {
        const n = Number(v) || 0;
        if (Math.abs(n) >= 1000) return '$' + (n / 1000).toLocaleString('en-AU', { maximumFractionDigits: n >= 100000 ? 0 : 1 }) + 'k';
        return '$' + Math.round(n).toLocaleString('en-AU');
    }

    function niceMax(v) {
        const n = Math.max(1, Number(v) || 1) * 1.12;
        const pow = Math.pow(10, Math.floor(Math.log10(n)));
        const steps = [1, 1.5, 2, 2.5, 3, 4, 5, 6, 8, 10];
        for (const s of steps) if (n <= s * pow) return s * pow;
        return 10 * pow;
    }

    const RESULT_META = {
        meets: { cls: 'ir-res-meets', icon: '✓', label: 'Meets' },
        partly: { cls: 'ir-res-partly', icon: '◐', label: 'Partly' },
        gap: { cls: 'ir-res-gap', icon: '✗', label: 'Gap' },
        info: { cls: 'ir-res-info', icon: 'i', label: 'See wording' },
        na: { cls: 'ir-res-info', icon: '–', label: 'Not stated' }
    };

    function resultChip(result) {
        const m = RESULT_META[result] || RESULT_META.info;
        return `<span class="ir-res ${m.cls}">${m.icon} ${escapeHtml(m.label)}</span>`;
    }

    function priorityClass(priority) {
        const p = String(priority || '').toLowerCase();
        if (p.startsWith('must')) return 'must';
        if (p.startsWith('important')) return 'important';
        if (p.includes('claim')) return 'claim';
        return '';
    }

    // ── SVG charts (no external dependency) ────────────────────────────────
    // Grouped columns: one group per insurer, one bar per series.
    function groupedBarSvg(categories, series, highlightIndex) {
        const W = 680, H = 300, L = 64, R = 16, T = 18, B = 64;
        const max = niceMax(Math.max(...series.flatMap(s => s.values.map(v => Number(v) || 0))));
        const plotW = W - L - R, plotH = H - T - B;
        const groupW = plotW / Math.max(1, categories.length);
        const barW = Math.min(46, (groupW * 0.7) / Math.max(1, series.length));
        const y = v => T + plotH - (Number(v) || 0) / max * plotH;
        let svg = `<svg viewBox="0 0 ${W} ${H}" role="img" aria-label="Year-1 premium by insurer, stepped and level">`;
        for (let i = 0; i <= 4; i++) {
            const v = max * i / 4, yy = y(v);
            svg += `<line x1="${L}" x2="${W - R}" y1="${yy}" y2="${yy}" stroke="#e4e7ec"/>` +
                   `<text x="${L - 8}" y="${yy + 4}" text-anchor="end" font-size="11" fill="#667085">${escapeHtml(shortMoney(v))}</text>`;
        }
        categories.forEach((cat, ci) => {
            const gx = L + ci * groupW + (groupW - barW * series.length) / 2;
            series.forEach((s, si) => {
                const v = Number(s.values[ci]) || 0, x = gx + si * barW, yy = y(v);
                const stroke = ci === highlightIndex && si === 0 ? ' stroke="#0f766e" stroke-width="2"' : '';
                svg += `<rect x="${x + 2}" y="${yy}" width="${barW - 4}" height="${T + plotH - yy}" rx="3" fill="${s.colour}"${stroke}>` +
                       `<title>${escapeHtml(cat)} — ${escapeHtml(s.name)}: ${escapeHtml(money(v))}</title></rect>` +
                       `<text x="${x + barW / 2}" y="${yy - 5}" text-anchor="middle" font-size="10.5" fill="#344054">${escapeHtml(money(v))}</text>`;
            });
            const label = ci === highlightIndex ? '★ ' + cat : cat;
            svg += `<text x="${L + ci * groupW + groupW / 2}" y="${T + plotH + 18}" text-anchor="middle" font-size="12" ` +
                   `font-weight="${ci === highlightIndex ? 700 : 500}" fill="${ci === highlightIndex ? '#0f766e' : '#344054'}">${escapeHtml(label)}</text>`;
        });
        let lx = L;
        series.forEach(s => {
            svg += `<rect x="${lx}" y="${H - 22}" width="12" height="12" rx="2" fill="${s.colour}"/>` +
                   `<text x="${lx + 17}" y="${H - 12}" font-size="11.5" fill="#344054">${escapeHtml(s.name)}</text>`;
            lx += 30 + s.name.length * 6.4;
        });
        return svg + '</svg>';
    }

    // Lines over years; dashed series for level premiums; optional marker year.
    function lineSvg(labels, series, markerIndex, markerText) {
        const W = 680, H = 300, L = 64, R = 18, T = 18, B = 62;
        const max = niceMax(Math.max(...series.flatMap(s => s.values.map(v => Number(v) || 0))));
        const plotW = W - L - R, plotH = H - T - B;
        const x = i => L + (labels.length <= 1 ? plotW / 2 : i * plotW / (labels.length - 1));
        const y = v => T + plotH - (Number(v) || 0) / max * plotH;
        let svg = `<svg viewBox="0 0 ${W} ${H}" role="img" aria-label="Stepped and level premiums over 10 years">`;
        for (let i = 0; i <= 4; i++) {
            const v = max * i / 4, yy = y(v);
            svg += `<line x1="${L}" x2="${W - R}" y1="${yy}" y2="${yy}" stroke="#e4e7ec"/>` +
                   `<text x="${L - 8}" y="${yy + 4}" text-anchor="end" font-size="11" fill="#667085">${escapeHtml(shortMoney(v))}</text>`;
        }
        labels.forEach((lab, i) => {
            svg += `<text x="${x(i)}" y="${T + plotH + 17}" text-anchor="middle" font-size="11" fill="#667085">${escapeHtml(lab)}</text>`;
        });
        if (markerIndex !== null && markerIndex !== undefined && markerIndex >= 0) {
            const mx = x(markerIndex);
            svg += `<line x1="${mx}" x2="${mx}" y1="${T}" y2="${T + plotH}" stroke="#f59e0b" stroke-dasharray="4 4"/>` +
                   `<text x="${Math.min(mx + 6, W - R - 4)}" y="${T + 12}" text-anchor="${mx > W - 180 ? 'end' : 'start'}" font-size="11" fill="#954f12">${escapeHtml(markerText || '')}</text>`;
        }
        series.forEach(s => {
            const pts = s.values.map((v, i) => `${x(i)},${y(v)}`).join(' ');
            svg += `<polyline points="${pts}" fill="none" stroke="${s.colour}" stroke-width="${s.bold ? 3 : 2.2}"` +
                   `${s.dashed ? ' stroke-dasharray="7 5"' : ''} stroke-linejoin="round" stroke-linecap="round"/>`;
            s.values.forEach((v, i) => {
                svg += `<circle cx="${x(i)}" cy="${y(v)}" r="${s.dashed ? 2.6 : 3.2}" fill="${s.dashed ? '#fff' : s.colour}" stroke="${s.colour}" stroke-width="1.5">` +
                       `<title>${escapeHtml(s.name)} — ${escapeHtml(labels[i])}: ${escapeHtml(money(v))}</title></circle>`;
            });
        });
        let lx = L;
        series.forEach(s => {
            svg += `<line x1="${lx}" x2="${lx + 22}" y1="${H - 16}" y2="${H - 16}" stroke="${s.colour}" stroke-width="3"${s.dashed ? ' stroke-dasharray="6 4"' : ''}/>` +
                   `<text x="${lx + 28}" y="${H - 12}" font-size="11.5" fill="#344054">${escapeHtml(s.name)}</text>`;
            lx += 40 + s.name.length * 6.4;
        });
        return svg + '</svg>';
    }

    function init() {
        const initialRoot = document.querySelector('.ir-panel');
        if (!initialRoot || initialRoot.dataset.wired === '1') return;
        initialRoot.dataset.wired = '1';

        // Always re-query against the live document so a Blazor enhanced
        // navigation swap never leaves us writing into a detached node.
        const $ = sel => {
            const r = document.querySelector('.ir-panel') || initialRoot;
            return r ? r.querySelector(sel) : null;
        };

        let selectedCase = null;
        let comparison = null;
        let previousResponseId = null;
        let engaged = false;
        let svlInsurer = null;
        let svlMode = 'annual';
        const referenceMap = new Map(); // url||title -> { title, url, kind }

        function setText(id, value) {
            const el = $('#' + id);
            if (el) el.textContent = value == null || value === '' ? '—' : value;
        }

        function show(id, visible) {
            const el = $('#' + id);
            if (el) el.hidden = !visible;
        }

        function setList(id, items, mapFn) {
            const el = $('#' + id);
            if (!el) return;
            const list = Array.isArray(items) ? items : [];
            el.innerHTML = list.map(i => `<li>${mapFn ? mapFn(i) : escapeHtml(i)}</li>`).join('');
        }

        function setStatus(id, text, isError, spinner) {
            const el = $('#' + id);
            if (!el) return;
            el.hidden = !text;
            el.className = 'ir-status' + (isError ? ' error' : '');
            el.innerHTML = (spinner ? '<span class="spinner"></span>' : '') + escapeHtml(text || '');
        }

        // Only visible steps are numbered, so a claim case reads 1, 2, 3.
        function renumber() {
            const root = document.querySelector('.ir-panel') || initialRoot;
            let n = 1;
            root.querySelectorAll('.ir-step').forEach(step => {
                const num = step.querySelector('.ir-step-num');
                if (num && !step.hidden) num.textContent = String(n++);
            });
        }

        // Upgrade ```mermaid blocks in the agent narrative and chat replies.
        if (window.__irMermaidObserver) window.__irMermaidObserver.disconnect();
        let mermaidTimer = null;
        const observer = new MutationObserver(() => {
            clearTimeout(mermaidTimer);
            mermaidTimer = setTimeout(() => upgradeMermaidBlocks(document.querySelector('.ir-panel')), 200);
        });
        observer.observe(initialRoot, { childList: true, subtree: true });
        window.__irMermaidObserver = observer;

        // ── Step 1: cases ────────────────────────────────────────────────
        async function loadCases(preferred) {
            const sel = $('#ir-case-select');
            if (sel) sel.innerHTML = '<option value="">— Loading cases —</option>';
            show('ir-empty', false);
            resetCase();
            try {
                const res = await fetch('/research/cases');
                if (!res.ok) throw new Error('HTTP ' + res.status);
                const cases = await res.json();
                if (!cases.length) {
                    if (sel) sel.innerHTML = '<option value="">— No cases available —</option>';
                    show('ir-empty', true);
                    return;
                }
                if (sel) {
                    sel.innerHTML = '<option value="">— Select a case —</option>' + cases.map(c => {
                        const kind = c.kind === 'claim' ? 'claim · wording lookup' : c.kind === 'advice' ? 'advice · compare insurers' : 'research';
                        return `<option value="${escapeHtml(c.caseNumber)}">${escapeHtml(c.caseNumber)} — ${escapeHtml(c.clientName)} (${escapeHtml(c.requestType)} · ${escapeHtml(kind)})</option>`;
                    }).join('');
                    if (preferred && cases.some(c => c.caseNumber === preferred)) {
                        sel.value = preferred;
                        selectCase(preferred);
                    }
                }
            } catch (err) {
                if (sel) sel.innerHTML = '<option value="">— Failed to load —</option>';
                show('ir-compare-step', true);
                setStatus('ir-process-status', 'Failed to load cases: ' + err.message, true);
            }
        }

        function resetCase() {
            selectedCase = null;
            show('ir-case', false);
            show('ir-compare-step', false);
            const btn = $('#ir-process-btn');
            if (btn) btn.disabled = true;
            resetResults();
            renumber();
        }

        function resetResults() {
            engaged = false;
            comparison = null;
            svlInsurer = null;
            show('ir-compare-results', false);
            show('ir-wording-results', false);
            show('ir-defs-step', false);
            show('ir-svl-step', false);
            show('ir-wb-step', false);
            show('ir-wb-card', false);
            setStatus('ir-wb-status', '');
            setStatus('ir-process-status', '');
            resetChatAndRefs();
        }

        async function selectCase(caseNumber) {
            resetCase();
            if (!caseNumber) return;
            try {
                const res = await fetch('/research/cases/' + encodeURIComponent(caseNumber));
                if (!res.ok) throw new Error('HTTP ' + res.status);
                selectedCase = await res.json();
                renderCase(selectedCase);
                show('ir-case', true);
                show('ir-compare-step', true);
                const btn = $('#ir-process-btn');
                if (btn) btn.disabled = false;
                renumber();
            } catch (err) {
                show('ir-compare-step', true);
                setStatus('ir-process-status', 'Failed to load case: ' + err.message, true);
            }
        }

        function renderCase(c) {
            const r = c.research || {};
            const isClaim = r.kind === 'claim';
            setText('ir-case-num', c.caseNumber);
            const kind = $('#ir-case-kind');
            if (kind) {
                kind.textContent = isClaim ? 'Claim · policy wording lookup' : r.kind === 'advice' ? 'Advice · compare the APL insurers' : 'Research';
                kind.className = 'ir-pill' + (isClaim ? ' claim' : '');
            }
            const status = $('#ir-case-status');
            if (status) {
                status.hidden = !r.caseStatus;
                status.textContent = r.caseStatus ? 'Case status: ' + r.caseStatus : '';
            }
            setText('ir-case-client', c.clientName);
            setText('ir-case-clientno', c.clientNumber);
            setText('ir-case-type', c.requestType);
            setText('ir-case-received', auDate(c.receivedDate));
            setText('ir-case-adviser', c.adviserName);
            setText('ir-case-referred', c.referredBy);
            setText('ir-case-cover', c.cover);
            setText('ir-case-priority', c.priorityReason ? `${c.priority} — ${c.priorityReason}` : c.priority);

            const note = $('#ir-journey-note');
            if (note) {
                note.hidden = !r.journeyNote;
                note.innerHTML = r.journeyNote
                    ? escapeHtml(r.journeyNote) + (isClaim && r.handoverUrl
                        ? ` <a href="${escapeHtml(r.handoverUrl)}">Open the ${escapeHtml(r.handoverAgentName || 'Claims Agent')} →</a>`
                        : '')
                    : '';
            }

            setText('ir-brief-title', r.title);
            setText('ir-brief-summary', r.summary);
            setList('ir-brief-facts', r.keyFacts);
            show('ir-brief-facts-block', (r.keyFacts || []).length > 0);

            const coverHead = $('#ir-brief-cover-head');
            if (coverHead) coverHead.textContent = isClaim ? 'Policy on file' : "Cover to research — from Penny's needs analysis";
            const coverItems = isClaim ? r.policyOnFile : r.coverRequested;
            setList('ir-brief-cover', coverItems);
            show('ir-brief-cover-block', (coverItems || []).length > 0);
            const budget = $('#ir-brief-budget');
            if (budget) {
                budget.hidden = !r.monthlyBudget;
                budget.textContent = r.monthlyBudget ? `Budget for premiums from cash flow: up to ${money(r.monthlyBudget)} a month.` : '';
            }

            const defsHead = $('#ir-brief-defs-head');
            if (defsHead) defsHead.textContent = isClaim ? 'Wording to look up' : `Definitions that matter for ${r.clientFirstNames || 'this client'}`;
            setList('ir-brief-defs', r.definitionsThatMatter, d =>
                `<span class="ir-prio ${priorityClass(d.priority)}">${escapeHtml(isClaim ? 'Claim' : d.priority)}</span>` +
                `<span><strong>${escapeHtml(d.title)}</strong> — ${escapeHtml(d.whyItMatters)}</span>`);
            show('ir-brief-defs-block', (r.definitionsThatMatter || []).length > 0);

            const sheets = $('#ir-sheets');
            if (sheets) {
                sheets.innerHTML = (c.productSheets || []).map(s =>
                    `<div class="ir-sheet${s.relevant ? ' relevant' : ' dim'}" tabindex="0">` +
                        `<span class="ir-sheet-name">${escapeHtml(s.insurer)}</span>` +
                        `<span class="ir-sheet-prod">${escapeHtml(s.productName)}</span>` +
                        `<span class="ir-sheet-status">${escapeHtml(s.status)}</span>` +
                        `<a class="ir-sheet-ext" href="${escapeHtml(s.url)}" target="_blank" rel="noopener" title="Open the product sheet (JSON)" aria-label="Open the ${escapeHtml(s.insurer)} product sheet">↗</a>` +
                        '<div class="ir-sheet-pop" role="tooltip">' +
                            `<strong>${escapeHtml(s.pdsTitle)}</strong>` +
                            (s.notQuotedReason ? `<pre>${escapeHtml(s.notQuotedReason)}</pre><br/>` : '') +
                            `<pre>${escapeHtml(s.preview)}</pre>` +
                        '</div>' +
                    '</div>').join('');
            }

            setText('ir-compare-title', isClaim ? 'Look up the policy wording' : r.kind === 'advice' ? 'Compare the APL insurers' : 'Research');
            const chips = $('#ir-chat-suggestions');
            if (chips) {
                chips.innerHTML = (c.chatSuggestions || []).map(q =>
                    `<button type="button" class="ir-chip" data-question="${escapeHtml(q)}" disabled>${escapeHtml(q)}</button>`).join('');
            }
        }

        // ── Step 2: engage the agent ─────────────────────────────────────
        async function processCase() {
            if (!selectedCase) return;
            const btn = $('#ir-process-btn');
            const scope = $('.engage-tabs-scope');
            if (btn) btn.disabled = true;
            resetResults();
            renumber();
            setStatus('ir-process-status', 'Engaging the Insurance Research Agent…', false, true);

            try {
                const data = await window.zcAgentStream({
                    url: '/research/process',
                    body: { caseNumber: selectedCase.caseNumber },
                    onDelta: (_chunk, fullText) => {
                        if (window.engageTabsStreamNarrative) window.engageTabsStreamNarrative(scope, fullText);
                    },
                    onError: (msg) => setStatus('ir-process-status', 'Agent error: ' + msg + ' — showing the offline research.', true)
                });
                if (!data) throw new Error('No response from the agent');

                if (data.agentError) {
                    setStatus('ir-process-status', 'The live agent didn\'t complete (' + data.agentError + ') — showing the offline research.', true);
                } else if (!data.agentConfigured) {
                    setStatus('ir-process-status', 'Offline research — the Foundry agent isn\'t configured here, so the page shows the same results the MCP tools return.');
                } else {
                    setStatus('ir-process-status', 'The Insurance Research Agent finished its research.');
                }

                if (window.engageTabsRender) window.engageTabsRender(scope, data);
                renderResults(data);
                engaged = true;
                if (data.previousResponseId) previousResponseId = data.previousResponseId;
                if (data.agentRawOutput && Array.isArray(data.agentRawOutput.citations)) {
                    mergeCitations(data.agentRawOutput.citations);
                    renderReferences();
                }
                show('ir-chat-step', true);
                setChatEnabled(true);
                renumber();
            } catch (err) {
                setStatus('ir-process-status', 'Failed to process: ' + err.message, true);
            } finally {
                const b = $('#ir-process-btn');
                if (b) b.disabled = !selectedCase;
            }
        }

        function renderResults(data) {
            const live = data.agentConfigured && !data.agentError && data.agentRawOutput;
            const modeText = live ? (data.mcpEnabled ? 'Live agent · MCP tools' : 'Live agent') : 'Offline research';
            if (data.kind === 'advice' && data.comparison) {
                comparison = data.comparison;
                renderComparison(comparison, modeText, live);
                renderDefinitions(comparison, (data.research && data.research.clientFirstNames) || 'this client');
                const hasProjection = Array.isArray(comparison.projections) && comparison.projections.length > 0;
                show('ir-defs-step', (comparison.definitions || []).length > 0);
                show('ir-svl-step', hasProjection);
                show('ir-wb-step', comparison.quotesOnFile);
                if (hasProjection) renderSvl();
            } else if (data.wording) {
                renderWording(data.wording, modeText, live);
            }
        }

        function renderComparison(c, modeText, live) {
            const rec = c.recommendation || {};
            setText('ir-compare-headline', rec.headline);
            const mode = $('#ir-compare-mode');
            if (mode) { mode.className = 'ir-mode-pill' + (live ? ' live' : ''); mode.textContent = modeText; }
            setList('ir-rec-reasons', rec.reasons);
            show('ir-rec', (rec.reasons || []).length > 0);

            const rows = Array.isArray(c.rows) ? c.rows : [];
            const quoted = rows.filter(r => r.quoted);
            const cashFlow = quoted.some(r => r.cashFlowMonthly !== null && r.cashFlowMonthly !== undefined);
            const table = $('#ir-compare-table');
            if (table) {
                const cols = 7 + (cashFlow ? 1 : 0);
                table.innerHTML =
                    '<thead><tr><th>Rank</th><th>Insurer</th><th class="num">Year 1 (stepped)</th><th class="num">Monthly</th>' +
                    (cashFlow ? '<th class="num">From cash flow</th>' : '') +
                    '<th class="num">Year 1 (level)</th><th class="num">10-year total<br/>stepped / level</th><th>Definitions</th></tr></thead><tbody>' +
                    rows.map(r => {
                        if (!r.quoted) {
                            return `<tr class="notquoted"><td>—</td><td><strong>${escapeHtml(r.insurer)}</strong></td>` +
                                   `<td colspan="${cols - 2}">Not quoted — ${escapeHtml(r.notQuotedReason || '')}</td></tr>`;
                        }
                        return `<tr class="${r.recommended ? 'recommended' : ''}">` +
                            `<td>${r.rank}${r.recommended ? ' ★' : ''}</td>` +
                            `<td><strong>${escapeHtml(r.insurer)}</strong>${r.recommended ? '<span class="ir-tag">Recommended</span>' : ''}` +
                                `<div class="muted">${escapeHtml(r.quoteReference || '')} · ${escapeHtml(r.pdsTitle || '')}</div></td>` +
                            `<td class="num"><strong>${escapeHtml(money(r.year1Stepped))}</strong></td>` +
                            `<td class="num">${escapeHtml(money(r.monthlyStepped))}</td>` +
                            (cashFlow ? `<td class="num">${r.cashFlowMonthly != null ? escapeHtml(money(r.cashFlowMonthly)) + ' a month' : '—'}</td>` : '') +
                            `<td class="num">${escapeHtml(money(r.year1Level))}</td>` +
                            `<td class="num">${escapeHtml(money(r.tenYearStepped))} / ${escapeHtml(money(r.tenYearLevel))}</td>` +
                            `<td>${escapeHtml(r.definitionSummary || '')}</td></tr>`;
                    }).join('') + '</tbody>';
            }

            const chart = $('#ir-year1-chart');
            if (chart) {
                chart.innerHTML = quoted.length
                    ? groupedBarSvg(quoted.map(r => r.insurer), [
                        { name: 'Stepped — year 1', values: quoted.map(r => r.year1Stepped), colour: STEPPED_COLOUR },
                        { name: 'Level — year 1', values: quoted.map(r => r.year1Level), colour: LEVEL_COLOUR }
                    ], quoted.findIndex(r => r.recommended))
                    : '<div class="empty">No quotes on file yet.</div>';
            }

            const covers = $('#ir-covers-table');
            if (covers) {
                covers.innerHTML =
                    '<thead><tr><th>Insurer</th><th>Cover</th><th>Sum insured / benefit</th><th>Held</th><th class="num">Year 1 (stepped)</th><th class="num">Year 1 (level)</th></tr></thead><tbody>' +
                    quoted.map(r =>
                        (r.covers || []).map(cv =>
                            `<tr class="${r.recommended ? 'recommended' : ''}"><td>${escapeHtml(r.insurer)}</td><td>${escapeHtml(cv.cover)}</td>` +
                            `<td>${escapeHtml(cv.sumInsured || '')}</td><td>${escapeHtml(cv.ownership || '')}</td>` +
                            `<td class="num">${escapeHtml(money(cv.year1Premium))}</td><td class="num">${escapeHtml(money(cv.levelYear1Premium))}</td></tr>`).join('') +
                        (r.structure ? `<tr><td></td><td colspan="5" class="muted">${escapeHtml(r.structure)}</td></tr>` : '')
                    ).join('') + '</tbody>';
            }
            show('ir-covers-details', quoted.length > 0);

            setList('ir-not-chosen', rec.notChosen);
            setList('ir-observations', (c.observations || []).length ? c.observations : ['No further notes for this case.']);
            const commission = $('#ir-commission');
            if (commission) commission.innerHTML = '<strong>Commission:</strong> ' + escapeHtml(c.commissionNote || '');
            setText('ir-compare-approval-text', c.humanApproval);
            show('ir-compare-results', true);
            show('ir-wording-results', false);
        }

        function renderDefinitions(c, names) {
            setText('ir-defs-title', `Definitions that matter for ${names}`);
            const defs = Array.isArray(c.definitions) ? c.definitions : [];
            const table = $('#ir-defs-table');
            if (!table || !defs.length) return;
            const recommended = (c.rows || []).find(r => r.recommended);
            const insurers = defs[0].insurers.map(i => i.insurer);
            table.innerHTML =
                '<thead><tr><th>Definition</th><th>Priority</th>' +
                insurers.map(name => {
                    const isRec = recommended && recommended.insurer === name;
                    return `<th class="${isRec ? 'recommended' : ''}">${escapeHtml(name)}${isRec ? ' ★' : ''}</th>`;
                }).join('') + '</tr></thead><tbody>' +
                defs.map(d =>
                    `<tr><td class="def"><strong>${escapeHtml(d.title)}</strong><span class="why">${escapeHtml(d.whyItMatters)}</span></td>` +
                    `<td><span class="ir-prio ${priorityClass(d.priority)}">${escapeHtml(d.priority)}</span></td>` +
                    d.insurers.map(cell =>
                        `<td title="${escapeHtml(cell.wording ? cell.pdsTitle + ': ' + cell.wording : cell.pdsTitle)}">${resultChip(cell.result)}` +
                        `<span class="note">${escapeHtml(cell.note)}</span></td>`).join('') +
                    '</tr>').join('') + '</tbody>';
        }

        // ── Step 4: stepped vs level ─────────────────────────────────────
        function renderSvl() {
            if (!comparison) return;
            const projections = comparison.projections || [];
            if (!svlInsurer || !projections.some(p => p.insurer === svlInsurer)) {
                const rec = projections.find(p => p.recommended) || projections[0];
                svlInsurer = rec ? rec.insurer : null;
            }
            const seg = $('#ir-svl-insurers');
            if (seg) {
                seg.innerHTML = projections.map(p =>
                    `<button type="button" class="ir-seg-btn${p.insurer === svlInsurer ? ' active' : ''}" data-insurer="${escapeHtml(p.insurer)}">` +
                    `${escapeHtml(p.insurer)}${p.recommended ? ' ★' : ''}</button>`).join('');
            }
            const modeSeg = $('#ir-svl-mode');
            if (modeSeg) modeSeg.querySelectorAll('.ir-seg-btn').forEach(b => b.classList.toggle('active', b.dataset.mode === svlMode));

            const p = projections.find(x => x.insurer === svlInsurer);
            if (!p) return;
            const colourIndex = Math.max(0, projections.indexOf(p));
            const colour = INSURER_COLOURS[colourIndex % INSURER_COLOURS.length];
            const years = p.years || [];
            const labels = years.map(y => 'Yr ' + y.year);
            const cumulative = svlMode === 'cumulative';
            const series = [
                { name: (cumulative ? 'Total paid — stepped' : 'Stepped premium'), values: years.map(y => cumulative ? y.cumulativeStepped : y.stepped), colour: colour, bold: true },
                { name: (cumulative ? 'Total paid — level' : 'Level premium'), values: years.map(y => cumulative ? y.cumulativeLevel : y.level), colour: colour, dashed: true }
            ];
            const markerYear = cumulative ? p.levelCheaperInTotalFromYear : p.steppedAboveLevelFromYear;
            const markerText = markerYear
                ? (cumulative ? `Level cheaper in total from year ${markerYear}` : `Stepped passes level in year ${markerYear}`)
                : '';
            const chart = $('#ir-svl-chart');
            if (chart) chart.innerHTML = lineSvg(labels, series, markerYear ? markerYear - 1 : null, markerText);

            const summary = $('#ir-svl-summary');
            if (summary) summary.innerHTML = `<strong>${escapeHtml(p.insurer)}${p.recommended ? ' (recommended for Amelia\'s review)' : ''}:</strong> ${escapeHtml(p.summary)}`;

            const row = (comparison.rows || []).find(r => r.insurer === p.insurer);
            const budget = comparison.monthlyBudget;
            const budgetEl = $('#ir-svl-budget');
            if (budgetEl) {
                const hasCashFlow = budget && row && row.cashFlowMonthly != null;
                budgetEl.hidden = !hasCashFlow;
                if (hasCashFlow) {
                    const within = v => (Number(v) <= Number(budget) ? 'within' : 'over');
                    budgetEl.textContent = `From cash flow in year 1: ${money(row.cashFlowMonthly)} a month stepped (${within(row.cashFlowMonthly)} budget)` +
                        (row.levelCashFlowMonthly != null ? `, ${money(row.levelCashFlowMonthly)} a month level (${within(row.levelCashFlowMonthly)} budget)` : '') +
                        ` — the budget is ${money(budget)} a month.`;
                }
            }

            const table = $('#ir-svl-table');
            if (table) {
                table.innerHTML =
                    `<thead><tr><th>Year</th><th>${escapeHtml(p.agesLabel || 'Age')}</th><th class="num">Stepped</th><th class="num">Level</th>` +
                    '<th class="num">Total paid (stepped)</th><th class="num">Total paid (level)</th></tr></thead><tbody>' +
                    years.map(y =>
                        `<tr class="${y.year === p.steppedAboveLevelFromYear || y.year === p.levelCheaperInTotalFromYear ? 'recommended' : ''}">` +
                        `<td>${y.year}</td><td>${escapeHtml(y.ages)}</td><td class="num">${escapeHtml(money(y.stepped))}</td>` +
                        `<td class="num">${escapeHtml(money(y.level))}</td><td class="num">${escapeHtml(money(y.cumulativeStepped))}</td>` +
                        `<td class="num">${escapeHtml(money(y.cumulativeLevel))}</td></tr>`).join('') + '</tbody>';
            }
            setText('ir-svl-label', p.label);
        }

        // ── Claim cases: policy wording lookup ───────────────────────────
        function renderWording(w, modeText, live) {
            setText('ir-wording-title', [w.insurer, w.policyNumber].filter(Boolean).join(' — ') || w.title);
            setText('ir-wording-pds', w.pdsTitle ? 'Cite: ' + w.pdsTitle : w.summary);
            const mode = $('#ir-wording-mode');
            if (mode) { mode.className = 'ir-mode-pill' + (live ? ' live' : ''); mode.textContent = modeText; }
            setList('ir-wording-policy', (w.policyOnFile || []).length ? w.policyOnFile : ['The policy schedule isn\'t on the case yet.']);
            const items = $('#ir-wording-items');
            if (items) {
                items.innerHTML = (w.items || []).map(i =>
                    '<div class="ir-wording-card">' +
                        `<h5>${escapeHtml(i.heading)}</h5><div class="src">${escapeHtml(i.source)}</div>` +
                        `<blockquote>${escapeHtml(i.wording)}</blockquote>` +
                        (i.plainEnglish ? `<p class="plain"><em>In plain English:</em> ${escapeHtml(i.plainEnglish)}</p>` : '') +
                        `<p class="means"><strong>What it means here:</strong> ${escapeHtml(i.whatItMeans)}</p>` +
                    '</div>').join('');
            }
            setText('ir-handover-text', w.handover);
            const link = $('#ir-handover-link');
            if (link) {
                link.hidden = !w.handoverUrl;
                if (w.handoverUrl) link.href = w.handoverUrl;
            }
            setList('ir-wording-next', w.nextSteps);
            setText('ir-wording-approval', w.humanApproval);
            show('ir-wording-results', true);
            show('ir-compare-results', false);
        }

        // ── Step 5: workbook ─────────────────────────────────────────────
        async function buildWorkbook() {
            if (!selectedCase) return;
            const btn = $('#ir-wb-btn');
            if (btn) btn.disabled = true;
            show('ir-wb-card', false);
            setStatus('ir-wb-status', 'Building the comparison workbook…', false, true);
            try {
                const notes = (($('#ir-wb-notes') || {}).value || '').trim();
                const res = await fetch('/research/workbook', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ caseNumber: selectedCase.caseNumber, notes: notes || null })
                });
                const data = await res.json().catch(() => ({}));
                if (!res.ok || !data.downloadUrl) throw new Error(data.error || ('HTTP ' + res.status));
                const link = $('#ir-wb-link');
                if (link) {
                    link.href = data.downloadUrl;
                    link.setAttribute('download', data.fileName || 'insurer-comparison.xlsx');
                }
                setText('ir-wb-name', data.fileName);
                const charts = (data.charts || []).length;
                setText('ir-wb-sheets', (data.sheets || []).join(' · ') + (charts ? ` · ${charts} chart${charts === 1 ? '' : 's'}` : ''));
                show('ir-wb-card', true);
                setStatus('ir-wb-status', '');
            } catch (err) {
                setStatus('ir-wb-status', 'The workbook could not be built: ' + err.message, true);
            } finally {
                const b = $('#ir-wb-btn');
                if (b) b.disabled = false;
            }
        }

        // ── Step 6: chat + references ────────────────────────────────────
        function setChatEnabled(enabled) {
            const send = $('#ir-chat-send');
            const input = $('#ir-chat-input');
            if (send) send.disabled = !enabled;
            if (input) input.disabled = !enabled;
            const root = document.querySelector('.ir-panel') || initialRoot;
            root.querySelectorAll('.ir-chip').forEach(c => { c.disabled = !enabled; });
        }

        function resetChatAndRefs() {
            previousResponseId = null;
            referenceMap.clear();
            renderReferences();
            const messages = $('#ir-chat-messages');
            if (messages) messages.innerHTML = '<div class="ir-chat-empty">No follow-up messages yet. Ask a question below.</div>';
            show('ir-chat-step', false);
            setChatEnabled(false);
            setStatus('ir-chat-status', '');
        }

        // Web-grounded citations carry an http(s) URL; Azure AI Search hits don't.
        function classifyCitation(c) {
            return /^https?:\/\//i.test(c && c.url ? String(c.url) : '') ? 'web' : 'doc';
        }

        function mergeCitations(citations) {
            if (!Array.isArray(citations)) return;
            for (const c of citations) {
                if (!c) continue;
                const key = c.url || c.title;
                if (!key || referenceMap.has(key)) continue;
                referenceMap.set(key, { title: c.title || '', url: c.url || '', kind: classifyCitation(c) });
            }
        }

        function renderReferences() {
            const list = $('#ir-refs-list');
            const count = $('#ir-refs-count');
            if (count) count.textContent = String(referenceMap.size);
            if (!list) return;
            if (referenceMap.size === 0) {
                list.innerHTML = '<div class="ir-refs-empty">References from the agent will appear here.</div>';
                return;
            }
            list.innerHTML = Array.from(referenceMap.values()).map((r, i) => {
                const label = r.kind === 'web' ? 'Web' : 'Document';
                const title = r.title || r.url || 'Reference ' + (i + 1);
                const titleHtml = r.url
                    ? `<a href="${escapeHtml(r.url)}" target="_blank" rel="noopener noreferrer">[${i + 1}] ${escapeHtml(title)}</a>`
                    : `<span>[${i + 1}] ${escapeHtml(title)}</span>`;
                const urlHtml = r.url && r.url !== title ? `<span class="ir-ref-url">${escapeHtml(r.url)}</span>` : '';
                return `<div class="ir-ref-card"><span class="ir-ref-kind ir-ref-kind-${r.kind}">${label}</span><div>${titleHtml}</div>${urlHtml}</div>`;
            }).join('');
        }

        function nowLabel() {
            return new Date().toLocaleTimeString('en-AU', { hour: '2-digit', minute: '2-digit' });
        }

        function appendChatMessage(role, text) {
            const messages = $('#ir-chat-messages');
            if (!messages) return null;
            const empty = messages.querySelector('.ir-chat-empty');
            if (empty) empty.remove();
            const wrap = document.createElement('div');
            wrap.className = 'ir-chat-msg ir-chat-msg-' + role;
            const bubble = document.createElement('div');
            bubble.className = 'ir-chat-bubble';
            if (role === 'assistant') {
                const md = document.createElement('div');
                md.className = 'agent-md';
                if (text && typeof window.zcRenderMarkdown === 'function') window.zcRenderMarkdown(md, text);
                else md.textContent = text || '';
                bubble.appendChild(md);
            } else {
                bubble.textContent = text || '';
            }
            wrap.appendChild(bubble);
            const meta = document.createElement('div');
            meta.className = 'ir-chat-meta';
            meta.textContent = (role === 'user' ? 'You' : 'Insurance Research Agent') + ' · ' + nowLabel();
            wrap.appendChild(meta);
            messages.appendChild(wrap);
            messages.scrollTop = messages.scrollHeight;
            return bubble;
        }

        function streamIntoBubble(bubble, fullText, final) {
            if (!bubble) return;
            let md = bubble.querySelector('.agent-md');
            if (!md) {
                md = document.createElement('div');
                md.className = 'agent-md';
                bubble.innerHTML = '';
                bubble.appendChild(md);
            }
            if (final && typeof window.zcRenderMarkdown === 'function') window.zcRenderMarkdown(md, fullText || '');
            else md.textContent = fullText || '';
            const messages = $('#ir-chat-messages');
            if (messages) messages.scrollTop = messages.scrollHeight;
        }

        async function sendChatMessage(text) {
            if (!text || !engaged || !selectedCase) return;
            setChatEnabled(false);
            appendChatMessage('user', text);
            const bubble = appendChatMessage('assistant', '');
            setStatus('ir-chat-status', 'The Insurance Research Agent is thinking…', false, true);
            try {
                const data = await window.zcAgentStream({
                    url: '/research/chat',
                    body: { previousResponseId: previousResponseId, message: text, caseNumber: selectedCase.caseNumber },
                    onDelta: (_chunk, fullText) => streamIntoBubble(bubble, fullText, false),
                    onError: (msg) => setStatus('ir-chat-status', 'Agent error: ' + msg, true)
                });
                if (!data) throw new Error('No response from the agent');
                if (data.previousResponseId) previousResponseId = data.previousResponseId;
                streamIntoBubble(bubble, data.agentNotes || '', true);
                if (data.agentRawOutput && Array.isArray(data.agentRawOutput.citations)) {
                    mergeCitations(data.agentRawOutput.citations);
                    renderReferences();
                }
                if (data.agentError) setStatus('ir-chat-status', 'The live agent didn\'t reply (' + data.agentError + ') — answered from the research on this case.', true);
                else setStatus('ir-chat-status', '');
            } catch (err) {
                setStatus('ir-chat-status', 'Failed to send: ' + err.message, true);
            } finally {
                setChatEnabled(true);
                const input = $('#ir-chat-input');
                if (input) {
                    input.value = '';
                    input.focus();
                }
            }
        }

        // ── Wiring ───────────────────────────────────────────────────────
        const selectInit = $('#ir-case-select');
        if (selectInit) selectInit.addEventListener('change', () => selectCase(selectInit.value));
        const refreshInit = $('#ir-refresh-btn');
        if (refreshInit) refreshInit.addEventListener('click', () => loadCases());
        const processInit = $('#ir-process-btn');
        if (processInit) processInit.addEventListener('click', processCase);
        const wbInit = $('#ir-wb-btn');
        if (wbInit) wbInit.addEventListener('click', buildWorkbook);
        const chatFormInit = $('#ir-chat-form');
        if (chatFormInit) {
            chatFormInit.addEventListener('submit', (e) => {
                e.preventDefault();
                const input = $('#ir-chat-input');
                const text = (input && input.value || '').trim();
                if (text) sendChatMessage(text);
            });
        }
        // Delegated clicks for content that is re-rendered per case.
        initialRoot.addEventListener('click', (e) => {
            const chip = e.target.closest('.ir-chip');
            if (chip && !chip.disabled) {
                sendChatMessage(chip.dataset.question || chip.textContent);
                return;
            }
            const insurerBtn = e.target.closest('#ir-svl-insurers .ir-seg-btn');
            if (insurerBtn) {
                svlInsurer = insurerBtn.dataset.insurer;
                renderSvl();
                return;
            }
            const modeBtn = e.target.closest('#ir-svl-mode .ir-seg-btn');
            if (modeBtn) {
                svlMode = modeBtn.dataset.mode === 'cumulative' ? 'cumulative' : 'annual';
                renderSvl();
            }
        });

        // Deep link: /agents/insurance-research?caseNumber=REQ-… preselects the case.
        const params = new URLSearchParams(window.location.search);
        const preferred = params.get('caseNumber') || params.get('case');
        loadCases(preferred);
    }

    init();

    if (!window.__irScenarioEnhancedHook) {
        window.__irScenarioEnhancedHook = true;
        const hook = () => init();
        if (typeof Blazor !== 'undefined' && typeof Blazor.addEventListener === 'function') {
            Blazor.addEventListener('enhancedload', hook);
        } else {
            document.addEventListener('enhancedload', hook);
        }
    }
})();
