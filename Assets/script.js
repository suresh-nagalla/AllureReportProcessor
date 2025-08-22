'use strict';

(() => {
    let reportData = null;
    let filteredTests = [];
    let currentPage = 1;
    let pageSize = 25;
    let sortField = null;
    let sortDirection = 'asc';
    // Slow tests filtering state
    let slowTestsAll = [];
    let slowTestsFiltered = [];
    let slowSuiteFilter = '';
    let slowStatusFilter = '';
    let slowLimit = 20; // default top N
    // Suite performance sorting state
    let suitePerfData = [];
    let suiteSortField = 'passRate';
    let suiteSortDirection = 'desc';

    const qs = sel => document.querySelector(sel);
    const qsa = sel => Array.from(document.querySelectorAll(sel));

    document.addEventListener('DOMContentLoaded', () => {
        try {
            hydrateReportData();
            applyStoredTheme();
            buildDashboard();
            populateSuiteFilter();
            loadSuitePerformance();
            loadSlowTests();
            loadTopSteps();
            setupEvents();
            filteredTests = [...(reportData.testResults || [])];
            applyFilters();
        } catch (err) { console.error('Report initialization failed:', err); }
    });

    function hydrateReportData() {
        const script = qs('#reportData');
        if (!script) throw new Error('reportData script tag not found');
        reportData = JSON.parse(script.textContent || '{}');
        if (!reportData || !reportData.overview) throw new Error('Invalid reportData payload');
    }

    /* Theme */
    function applyStoredTheme() { const pref = localStorage.getItem('aqd_theme') || 'dark'; document.body.setAttribute('data-theme', pref); updateThemeToggleLabel(pref); }
    function toggleTheme() { const current = document.body.getAttribute('data-theme') === 'light' ? 'light' : 'dark'; const next = current === 'light' ? 'dark' : 'light'; document.body.setAttribute('data-theme', next); localStorage.setItem('aqd_theme', next); updateThemeToggleLabel(next); }
    function updateThemeToggleLabel(theme) { const btn = qs('#themeToggle'); if (btn){ btn.textContent = theme === 'light' ? '🌙 Dark' : '☀️ Light'; btn.setAttribute('aria-label','Switch to '+(theme==='light'?'dark':'light')+' theme'); } }

    /* Dashboard */
    function buildDashboard() {
        const o = reportData?.overview; if (!o) return; const grid = qs('#metricsGrid'); if (!grid) return;
        const metrics = [
            { key:'total', label:'Total Tests', value:o.totalTests, className:'', desc:'Total executed tests.' },
            { key:'passed', label:'Passed', value:o.passedTests, className:'success', desc:'Tests that met all assertions.' },
            { key:'failed', label:'Failed', value:o.failedTests, className:o.failedTests?'error':'', desc:'Functional assertion failures.' },
            { key:'broken', label:'Broken', value:o.brokenTests, className:o.brokenTests?'warning':'', desc:'Infrastructure / unexpected errors.' },
            { key:'passRate', label:'Pass Rate', value:o.passRate.toFixed(1)+'%', className:o.passRate>=80?'success':'warning', desc:'Passed ÷ Total × 100.' },
            { key:'time', label:'Execution Time', value:o.executionTime, className:'', desc:'Aggregated total duration.' }
        ];
        grid.innerHTML = metrics.map(m=>`<div class="metric-card ${m.className}" tabindex="0" data-help="${escapeAttr(m.desc)}"><div class="metric-label">${escapeHtml(m.label)}</div><div class="metric-value">${escapeHtml(String(m.value))}</div></div>`).join('');
        setGeneratedTime();
        createStatusChart();
    }

    function setGeneratedTime(){ const el = qs('#generatedTime'); if (el && reportData?.overview?.generatedAt) el.textContent = 'Generated: '+reportData.overview.generatedAt; }

    function createStatusChart(){ const ctx = qs('#statusChart'); const o = reportData?.overview; if(!ctx||!window.Chart||!o) return; new Chart(ctx,{ type:'doughnut', data:{ labels:['Passed','Failed','Broken'], datasets:[{ data:[o.passedTests,o.failedTests,o.brokenTests], backgroundColor:['#34d399','#f87171','#fbbf24'], borderWidth:2 }]}, options:{ responsive:true, maintainAspectRatio:false, plugins:{ title:{display:true,text:'Test Status Distribution'}, legend:{position:'bottom'} } } }); }

    /* Suite Performance */
    function loadSuitePerformance(){
        const body=qs('#suitePerformanceBody');
        if(!body) return;
        suitePerfData = (reportData?.suitePerformance||[]).map(s=>({
            ...s,
            // normalize numeric fields
            passRate: typeof s.passRate === 'number' ? s.passRate : parseFloat(s.passRate)||0,
            totalDurationMs: typeof s.totalDurationMs === 'number' ? s.totalDurationMs : deriveMs(s.totalDurationReadable||'')
        }));
        sortAndRenderSuitePerformance();
        setupSuitePerfHeaderSorting();
    }

    function sortAndRenderSuitePerformance(){
        if(!suitePerfData.length) return;
        suitePerfData.sort((a,b)=>{
            let av, bv;
            switch(suiteSortField){
                case 'suiteName': av=(a.suiteName||'').toLowerCase(); bv=(b.suiteName||'').toLowerCase(); break;
                case 'totalDurationMs': av=a.totalDurationMs||0; bv=b.totalDurationMs||0; break;
                case 'passRate':
                default: av=a.passRate||0; bv=b.passRate||0; break;
            }
            if(av<bv) return suiteSortDirection==='asc'? -1:1;
            if(av>bv) return suiteSortDirection==='asc'? 1:-1;
            return 0;
        });
        const body=qs('#suitePerformanceBody');
        if(!body) return;
        body.innerHTML = suitePerfData.map((s,i)=>{
            const pr = s.passRate || 0;
            const cls = pr >= 80 ? 'passed' : pr >= 60 ? 'broken' : 'failed';
            return `<tr><td>${i+1}</td><td>${escapeHtml(s.suiteName||'')}</td><td>${s.totalTests||0}</td><td><span class=\"status-badge status-${cls}\">${pr.toFixed(1)}%</span></td><td>${escapeHtml(s.totalDurationReadable||formatDuration(s.totalDurationMs||0))}</td><td>${escapeHtml(s.avgDurationReadable||formatDuration(s.avgDurationMs||0))}</td><td><span class=\"perf-badge perf-${(s.performanceCategory||'').toLowerCase()}\">${escapeHtml(s.performanceCategory||'')}</span></td></tr>`;
        }).join('');
        updateSuitePerfSortIndicators();
    }

    function setupSuitePerfHeaderSorting(){
        const headerRow = qs('.performance-table thead tr');
        if(!headerRow) return;
        // Expected column order: #, Suite, Total, PassRate, TotalDuration, AvgDuration, Performance
        const headers = headerRow.querySelectorAll('th');
        if(!headers.length) return;
        // Add data-sort attributes
        const mapping = [null,'suiteName',null,'passRate','totalDurationMs'];
        headers.forEach((th,idx)=>{
            const key = mapping[idx];
            if(key){
                th.setAttribute('data-suite-sort', key);
                if(!th.querySelector('.suite-sort-indicator')){
                    const span = document.createElement('span');
                    span.className='suite-sort-indicator';
                    span.style.marginLeft='4px';
                    span.style.opacity='.6';
                    span.textContent='⇅';
                    th.appendChild(span);
                }
                th.style.cursor='pointer';
                th.addEventListener('click',()=>{
                    if(suiteSortField===key){
                        suiteSortDirection = suiteSortDirection==='asc'?'desc':'asc';
                    } else {
                        suiteSortField = key;
                        suiteSortDirection = key==='suiteName'? 'asc':'desc';
                    }
                    sortAndRenderSuitePerformance();
                });
            }
        });
    }

    function updateSuitePerfSortIndicators(){
        qsa('.suite-sort-indicator').forEach(el=>{ el.textContent='⇅'; el.style.opacity='.4'; });
        const active = qs(`th[data-suite-sort="${suiteSortField}"] .suite-sort-indicator`);
        if(active){
            active.textContent = suiteSortDirection==='asc' ? '↑' : '↓';
            active.style.opacity='1';
        }
    }

    function deriveMs(readable){
        if(!readable) return 0;
        // naive parse matching format produced by formatDuration
        const m = readable.match(/(\d+) m (\d+) s/);
        if(m) return (parseInt(m[1])*60 + parseInt(m[2]))*1000;
        const s = readable.match(/([0-9]+\.?[0-9]*) s/);
        if(s) return parseFloat(s[1])*1000;
        const ms = readable.match(/(\d+) ms/);
        if(ms) return parseInt(ms[1]);
        return 0;
    }

    /* Slow Tests */
    function loadSlowTests(){
        const body=qs('#slowTestsBody'); if(!body) return;
        // Inject filters UI if not present
        const sectionContainer = qs('#slow-tests .container');
        if(sectionContainer && !qs('#slowTestsFilters')){
            const filterWrap = document.createElement('div');
            filterWrap.className='filters';
            filterWrap.id='slowTestsFilters';
            filterWrap.innerHTML = `
              <div class="filter-group">
                <select id="slowSuiteFilter" class="filter-select">
                  <option value="">All Suites</option>
                </select>
              </div>
              <div class="filter-group">
                <select id="slowStatusFilter" class="filter-select">
                  <option value="">All Status</option>
                  <option value="Passed">Passed</option>
                  <option value="Failed">Failed</option>
                  <option value="Broken">Broken</option>
                </select>
              </div>
              <div class="filter-group">
                <select id="slowLimitFilter" class="filter-select">
                  <option value="5">Top 5</option>
                  <option value="10">Top 10</option>
                  <option value="20" selected>Top 20</option>
                  <option value="50">Top 50</option>
                  <option value="100">Top 100</option>
                  <option value="all">All</option>
                </select>
              </div>`;
            sectionContainer.insertBefore(filterWrap, sectionContainer.querySelector('.table-container'));
        }
        // Use ALL test results as slow tests source (not pre-trimmed list)
        slowTestsAll = (reportData?.testResults||[]) // already enriched from backend
            .filter(t => typeof t.durationMs === 'number')
            .map(t => t);
        // Populate suite options
        const suiteSel = qs('#slowSuiteFilter');
        if(suiteSel && suiteSel.options.length===1){
            [...new Set(slowTestsAll.map(t=>t.suiteName).filter(Boolean))].sort().forEach(s=>{ const opt=document.createElement('option'); opt.value=s; opt.textContent=s; suiteSel.appendChild(opt); });
        }
        // Event listeners
        suiteSel?.addEventListener('change',()=>{ slowSuiteFilter=suiteSel.value; filterSlowTests(); });
        const statusSel = qs('#slowStatusFilter');
        statusSel?.addEventListener('change',()=>{ slowStatusFilter=statusSel.value; filterSlowTests(); });
        const limitSel = qs('#slowLimitFilter');
        limitSel?.addEventListener('change',()=>{ const v=limitSel.value; slowLimit = v==='all'?Infinity:parseInt(v,10); filterSlowTests(); });
        // Ensure header has Tags column before Screenshot
        const headerRow=qs('.slow-tests-table thead tr');
        if(headerRow){ const headers=headerRow.querySelectorAll('th'); if(!Array.from(headers).some(h=>/tags/i.test(h.textContent||''))){ const th=document.createElement('th'); th.textContent='Tags'; headerRow.insertBefore(th, headers[headers.length-1]); } }
        filterSlowTests();
    }

    function filterSlowTests(){
        slowTestsFiltered = slowTestsAll.filter(t => (!slowSuiteFilter || t.suiteName===slowSuiteFilter) && (!slowStatusFilter || t.status===slowStatusFilter));
        // Sort by duration descending
        slowTestsFiltered.sort((a,b)=>(b.durationMs||0)-(a.durationMs||0));
        if (slowLimit !== Infinity) slowTestsFiltered = slowTestsFiltered.slice(0, slowLimit);
        renderSlowTestsTable();
    }

    function renderSlowTestsTable(){
        const body=qs('#slowTestsBody'); if(!body) return;
        const tagMap=new Map(); (reportData?.testResults||[]).forEach(tr=>{ const key=(tr.suiteName||'')+'||'+(tr.testCaseName||''); if(tr.caseTags) tagMap.set(key, tr.caseTags); });
        body.innerHTML = slowTestsFiltered.map((t,i)=>{ const file=t.screenshotFileName||t.screenshotPath||''; const link=file&&reportData.config.includeScreenshots?buildScreenshotLink(file,t):''; const tags=t.caseTags || tagMap.get((t.suiteName||'')+'||'+(t.testCaseName||'')) || ''; return `<tr><td>${i+1}</td><td>${escapeHtml(truncate(t.testCaseName||'',60))}</td><td>${escapeHtml(t.suiteName||'')}</td><td>${escapeHtml(t.duration||'')}</td><td><span class="status-badge status-${(t.status||'').toLowerCase()}">${escapeHtml(t.status||'')}</span></td><td><span class="perf-badge perf-${(t.performanceCategory||'').toLowerCase()}">${escapeHtml(t.performanceCategory||'')}</span></td><td class="tags-column">${escapeHtml(truncate(tags,30))}</td><td>${link}</td></tr>`; }).join('');
    }

    /* Top Steps */
    function loadTopSteps(){ const body=qs('#topStepsBody'); if(!body) return; const steps=reportData?.topSteps||[]; body.innerHTML=steps.map(s=>`<tr><td>${s.rank??''}</td><td title="${escapeAttr(s.stepName||'')}">${escapeHtml(s.truncatedStepName||'')}</td><td>${escapeHtml(s.avgDurationReadable||'')}</td><td>${escapeHtml(s.totalDurationReadable||'')}</td><td>${escapeHtml(s.maxDurationReadable||'')}</td><td>${s.count??0}</td><td>${(s.failRate??0).toFixed(1)}</td><td><span class="perf-badge perf-${(s.performanceCategory||'').toLowerCase()}">${escapeHtml(s.performanceCategory||'')}</span></td><td>${escapeHtml(s.reliabilityCategory||'')}</td></tr>`).join(''); }

    /* Suite Filter */
    function populateSuiteFilter(){ const sel=qs('#suiteFilter'); if(!sel) return; const suites=[...new Set((reportData?.testResults||[]).map(t=>t.suiteName).filter(Boolean))].sort(); const frag=document.createDocumentFragment(); suites.forEach(s=>{ const o=document.createElement('option'); o.value=s; o.textContent=s; frag.appendChild(o); }); sel.appendChild(frag); }

    /* Events */
    function setupEvents(){
        qs('#themeToggle')?.addEventListener('click',toggleTheme);
        qsa('.nav-link').forEach(a=>a.addEventListener('click',e=>{ e.preventDefault(); const id=a.getAttribute('href')?.slice(1); if(id) showSection(id); qsa('.nav-link').forEach(n=>n.classList.remove('active')); a.classList.add('active'); }));
        qs('#searchInput')?.addEventListener('input',applyFilters);
        qs('#statusFilter')?.addEventListener('change',applyFilters);
        qs('#suiteFilter')?.addEventListener('change',applyFilters);
        qs('#pageSizeSelect')?.addEventListener('change',e=>{ const v=e.target.value; pageSize = v==='all'?Number.MAX_SAFE_INTEGER:parseInt(v,10); currentPage=1; updateTestResults(); });
        qsa('#testResultsTable th[data-sort]').forEach(th=>th.addEventListener('click',()=>{ const field=th.getAttribute('data-sort'); if(!field) return; if(sortField===field){ sortDirection = sortDirection==='asc'?'desc':'asc'; } else { sortField=field; sortDirection='asc'; } updateSortIndicators(); applyFilters(); }));
        document.addEventListener('click',e=>{ const link=e.target.closest('.screenshot-link'); if(link){ e.preventDefault(); openScreenshotModal(link); }});
        qs('#modalClose')?.addEventListener('click',hideModal);
        qs('#screenshotModal')?.addEventListener('click',e=>{ if(e.target===e.currentTarget) hideModal(); });
        document.addEventListener('keydown',e=>{ if(e.key==='Escape') hideModal(); });
    }

    function showSection(id){ qsa('.section').forEach(s=>s.classList.remove('active')); qs('#'+id)?.classList.add('active'); }

    /* Filtering */
    function applyFilters(){ const tests=reportData?.testResults||[]; const search=(qs('#searchInput')?.value||'').toLowerCase().trim(); const statusVal=qs('#statusFilter')?.value||''; const suiteVal=qs('#suiteFilter')?.value||''; filteredTests = tests.filter(t=>{ const name=(t.testCaseName||'').toLowerCase(); const suite=(t.suiteName||'').toLowerCase(); const tags=(t.caseTags||'').toLowerCase(); const termMatch=!search||name.includes(search)||suite.includes(search)||tags.includes(search); const statusMatch=!statusVal||t.status===statusVal; const suiteMatch=!suiteVal||t.suiteName===suiteVal; return termMatch&&statusMatch&&suiteMatch; }); if(sortField){ const dir=sortDirection==='asc'?1:-1; filteredTests.sort((a,b)=>{ let av=a[sortField]; let bv=b[sortField]; if(sortField==='durationMs'){ av=parseInt(av)||0; bv=parseInt(bv)||0; } else { if(typeof av==='string') av=av.toLowerCase(); if(typeof bv==='string') bv=bv.toLowerCase(); } if(av<bv) return -1*dir; if(av>bv) return 1*dir; return 0; }); } currentPage=1; updateTestResults(); }

    function updateTestResults(){ const body=qs('#testResultsBody'); if(!body) return; const total=filteredTests.length; const totalPages=pageSize===Number.MAX_SAFE_INTEGER?1:Math.max(1,Math.ceil(total/pageSize)); currentPage=Math.min(currentPage,totalPages); const start=pageSize===Number.MAX_SAFE_INTEGER?0:(currentPage-1)*pageSize; const end=pageSize===Number.MAX_SAFE_INTEGER?total:Math.min(start+pageSize,total); const slice=filteredTests.slice(start,end); body.innerHTML=slice.map((t,i)=>{ const file=t.screenshotFileName||t.screenshotPath||''; const link=file&&reportData.config.includeScreenshots?buildScreenshotLink(file,t):''; return `<tr><td>${start+i+1}</td><td>${escapeHtml(t.suiteName||'')}</td><td>${escapeHtml(t.testCaseName||'')}</td><td><span class="status-badge status-${(t.status||'').toLowerCase()}">${escapeHtml(t.status||'')}</span></td><td>${escapeHtml(t.duration||'')}</td><td><span class="perf-badge perf-${(t.performanceCategory||'').toLowerCase()}">${escapeHtml(t.performanceCategory||'')}</span></td><td>${escapeHtml(truncate(t.caseTags||'',30))}</td><td>${escapeHtml(truncate(t.failingStep||'',40))}</td><td>${escapeHtml(truncate(t.failureReason||'',50))}</td><td>${link}</td></tr>`; }).join(''); const info=qs('#resultsInfo'); if(info){ const from=total===0?0:start+1; info.textContent=`Showing ${from}-${end} of ${total} tests`; } updatePagination(totalPages); }

    function buildScreenshotLink(file,t){ return `<a href="#" class="screenshot-link" data-file="${escapeAttr(file)}" data-suite="${escapeAttr(t.suiteName||'')}" data-name="${escapeAttr(t.testCaseName||'')}" data-duration="${escapeAttr(t.duration||'')}" data-status="${escapeAttr(t.status||'')}" data-perf="${escapeAttr(t.performanceCategory||'')}" data-tags="${escapeAttr(t.caseTags||'')}" data-failing="${escapeAttr(t.failingStep||'')}" data-reason="${escapeAttr(t.failureReason||'')}">View</a>`; }

    /* Pagination */
    function updatePagination(totalPages){ const container=qs('#pagination'); if(!container||pageSize===Number.MAX_SAFE_INTEGER||totalPages<=1){ if(container) container.innerHTML=''; return; } let html=''; html+=`<button class="page-btn${currentPage<=1?' disabled':''}" data-page="${currentPage-1}">Prev</button>`; const max=5; let start=Math.max(1,currentPage-Math.floor(max/2)); let end=Math.min(totalPages,start+max-1); if(end-start<max-1) start=Math.max(1,end-max+1); for(let i=start;i<=end;i++) html+=`<button class="page-btn${i===currentPage?' active':''}" data-page="${i}">${i}</button>`; html+=`<button class="page-btn${currentPage>=totalPages?' disabled':''}" data-page="${currentPage+1}">Next</button>`; container.innerHTML=html; container.querySelectorAll('[data-page]').forEach(btn=>btn.addEventListener('click',()=>{ const p=parseInt(btn.getAttribute('data-page')||'0',10); if(!isNaN(p)&&p>=1&&p<=totalPages&&p!==currentPage){ currentPage=p; updateTestResults(); }})); }

    function updateSortIndicators(){ qsa('.sort-indicator').forEach(i=>i.className='sort-indicator'); if(sortField){ const ind=qs(`th[data-sort="${sortField}"] .sort-indicator`); if(ind) ind.className='sort-indicator '+sortDirection; } }

    /* Modal & Zoom */
    function openScreenshotModal(link){ const meta={ file:link.dataset.file, suite:link.dataset.suite, name:link.dataset.name, duration:link.dataset.duration, status:link.dataset.status, perf:link.dataset.perf, tags:link.dataset.tags, failing:link.dataset.failing, reason:link.dataset.reason }; showScreenshot(meta); }

    let zoomState = { scale:1, x:0, y:0, dragging:false, startX:0, startY:0 };

    function resetZoom(img){ zoomState={ scale:1, x:0, y:0, dragging:false, startX:0, startY:0 }; applyTransform(img); }
    function applyTransform(img){ img.style.transform = `translate(${zoomState.x}px, ${zoomState.y}px) scale(${zoomState.scale})`; }

    function attachZoomHandlers(img){
        resetZoom(img);
        img.classList.add('zoom-enabled');
        img.onwheel = e => { e.preventDefault(); const delta = e.deltaY < 0 ? 0.1 : -0.1; const newScale = Math.min(5, Math.max(1, zoomState.scale + delta)); const rect = img.getBoundingClientRect(); const cx = e.clientX - rect.left; const cy = e.clientY - rect.top; const factor = newScale / zoomState.scale; zoomState.x = (zoomState.x - cx) * factor + cx; zoomState.y = (zoomState.y - cy) * factor + cy; zoomState.scale = newScale; applyTransform(img); img.classList.toggle('zoom-active', zoomState.scale>1); };
        img.onmousedown = e => { if(zoomState.scale<=1) return; zoomState.dragging=true; zoomState.startX=e.clientX - zoomState.x; zoomState.startY=e.clientY - zoomState.y; img.classList.add('dragging'); };
        window.addEventListener('mousemove', e => { if(!zoomState.dragging) return; zoomState.x = e.clientX - zoomState.startX; zoomState.y = e.clientY - zoomState.startY; applyTransform(img); });
        window.addEventListener('mouseup', ()=>{ zoomState.dragging=false; img.classList.remove('dragging'); });
    }

    function buildImageToolbar(img){ const toolbar = document.createElement('div'); toolbar.className='image-toolbar'; toolbar.innerHTML = `<button type="button" data-act="zoom-in">+</button><button type="button" data-act="zoom-out">-</button><button type="button" data-act="reset">Reset</button><button type="button" data-act="fit">Fit</button>`; toolbar.addEventListener('click', e=>{ const act = e.target.getAttribute('data-act'); if(!act) return; if(act==='zoom-in'){ zoomState.scale=Math.min(5,zoomState.scale+0.25); applyTransform(img); } else if(act==='zoom-out'){ zoomState.scale=Math.max(1,zoomState.scale-0.25); applyTransform(img); img.classList.toggle('zoom-active', zoomState.scale>1); } else if(act==='reset'){ resetZoom(img); img.classList.remove('zoom-active'); } else if(act==='fit'){ resetZoom(img); img.classList.remove('zoom-active'); }
        }); return toolbar; }

    function showScreenshot(meta){ if(!reportData?.config?.includeScreenshots||!meta.file) return; const modal=qs('#screenshotModal'); const img=qs('#modalImage'); const details=qs('#modalDetails'); const errorBox=qs('#modalError'); if(!modal||!img||!details) return; if(errorBox){ errorBox.style.display='none'; errorBox.textContent=''; }
        details.innerHTML = `<div><strong>Suite:</strong> ${escapeHtml(meta.suite||'')}</div><div><strong>Test:</strong> ${escapeHtml(meta.name||'')}</div><div><strong>Duration:</strong> ${escapeHtml(meta.duration||'')}</div>${meta.tags?`<div><strong>Tags:</strong> ${escapeHtml(meta.tags)}</div>`:''}${meta.failing?`<div><strong>Failing Step:</strong> ${escapeHtml(meta.failing)}</div>`:''}${meta.reason?`<div><strong>Failure Reason:</strong><br/><code>${escapeHtml(meta.reason)}</code></div>`:''}`;
        const original=meta.file; const base=original.split(/[/\\]+/).pop(); const candidates=[]; if(original.includes(':')||original.includes('\\')){ candidates.push('images/'+base, base, original); } else if(original.startsWith('images/')) { candidates.push(original, base); } else { candidates.push('images/'+original, original); }
        let idx=0; img.onerror=()=>{ if(++idx<candidates.length){ img.src=candidates[idx]; } else { img.onerror=null; if(errorBox){ errorBox.textContent='Screenshot not found: '+base; errorBox.style.display='block'; } } };
        let stage = img.parentElement; if(!stage.classList.contains('image-stage')){ const wrapper=document.createElement('div'); wrapper.className='image-stage'; img.replaceWith(wrapper); wrapper.appendChild(img); stage=wrapper; }
        if(!qs('.image-toolbar')){ const tb=buildImageToolbar(img); stage.parentElement?.insertBefore(tb, stage); }
        resetZoom(img); attachZoomHandlers(img);
        img.src=candidates[idx];
        modal.classList.add('show'); }

    function hideModal(){ const modal=qs('#screenshotModal'); if(modal) modal.classList.remove('show'); }

    /* Utils */
    function truncate(t,max){ if(!t||t.length<=max) return t||''; return t.slice(0,max-3)+'...'; }
    function escapeHtml(str){ if(str==null) return ''; const div=document.createElement('div'); div.textContent=String(str); return div.innerHTML; }
    function escapeAttr(str){ return escapeHtml(str).replace(/"/g,'&quot;'); }
    function formatDuration(ms){ ms=parseInt(ms)||0; if(ms<1000) return ms+' ms'; const s=ms/1000; if(s<60) return s.toFixed(1)+' s'; const m=Math.floor(s/60); const rs=Math.round(s%60); return `${m} m ${rs} s`; }
})();