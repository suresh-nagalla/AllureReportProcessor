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
    // Top steps sorting state
    let topStepsData = [];
    let topStepsSortField = 'rank';
    let topStepsSortDirection = 'asc';

    const qs = sel => document.querySelector(sel);
    const qsa = sel => Array.from(document.querySelectorAll(sel));

    document.addEventListener('DOMContentLoaded', () => {
        console.log('DOM Content Loaded - starting initialization...');
        try {
            hydrateReportData();
            console.log('✅ Data hydration successful');
            
            applyStoredTheme();
            console.log('✅ Theme applied');
            
            buildDashboard();
            console.log('✅ Dashboard built');
            
            populateSuiteFilter();
            console.log('✅ Suite filter populated');
            
            loadSuitePerformance();
            console.log('✅ Suite performance loaded');
            
            loadSlowTests();
            console.log('✅ Slow tests loaded');
            
            loadTopSteps();
            console.log('✅ Top steps loaded');
            
            loadFailureAnalysis();
            console.log('✅ Failure analysis loaded');
            
            setupEvents();
            console.log('✅ Events setup');
            
            addFilterLabels();
            console.log('✅ Filter labels added');
            
            filteredTests = [...(reportData.testResults || [])];
            console.log('✅ Filtered tests initialized:', filteredTests.length, 'tests');
            
            applyFilters();
            console.log('✅ Filters applied');
            
            console.log('🎉 Report initialization completed successfully!');
        } catch (err) { 
            console.error('❌ Report initialization failed:', err);
            console.error('Error stack:', err.stack);
            
            // Show error message to user
            const body = document.body;
            if (body) {
                const errorDiv = document.createElement('div');
                errorDiv.style.cssText = 'position:fixed;top:20px;left:20px;right:20px;background:#dc2626;color:white;padding:16px;border-radius:8px;z-index:9999;font-family:monospace;font-size:14px;box-shadow:0 4px 20px rgba(0,0,0,0.3);';
                errorDiv.innerHTML = `
                    <strong>⚠️ Report Loading Error</strong><br>
                    ${err.message}<br>
                    <small>Check browser console (F12) for details</small>
                `;
                body.appendChild(errorDiv);
                
                // Auto-remove after 10 seconds
                setTimeout(() => {
                    if (errorDiv.parentNode) {
                        errorDiv.parentNode.removeChild(errorDiv);
                    }
                }, 10000);
            }
        }
    });

    function addFilterLabels(){
        qsa('.filters').forEach(f=>{
            if(!f.querySelector('.filter-label')){
                const lbl=document.createElement('div');
                lbl.className='filter-label';
                lbl.textContent='Filters';
                f.prepend(lbl);
            }
        });
    }

    function hydrateReportData() {
        const script = qs('#reportData');
        if (!script) throw new Error('reportData script tag not found');
        
        try {
            const scriptContent = script.textContent || '{}';
            console.log('Raw report data length:', scriptContent.length);
            console.log('Raw report data preview:', scriptContent.substring(0, 500) + '...');
            
            reportData = JSON.parse(scriptContent);
            console.log('Parsed report data structure:', Object.keys(reportData));
            console.log('Overview data:', reportData.overview);
            console.log('Test results count:', reportData.testResults?.length || 0);
            console.log('Failure analysis available:', !!reportData.failureAnalysis);
            
            if (reportData.failureAnalysis) {
                console.log('Failure analysis structure:', Object.keys(reportData.failureAnalysis));
                console.log('Common failures count:', reportData.failureAnalysis.commonFailures?.length || 0);
            }
        } catch (e) {
            console.error('Failed to parse report data JSON:', e);
            console.error('Script content sample:', script.textContent?.substring(0, 1000));
            throw new Error('Invalid JSON in reportData: ' + e.message);
        }
        
        if (!reportData || !reportData.overview) throw new Error('Invalid reportData payload - missing overview');
        
        console.log('Report data loaded successfully with keys:', Object.keys(reportData));
    }

    /* Theme */
    function applyStoredTheme() { const pref = localStorage.getItem('aqd_theme') || 'dark'; document.body.setAttribute('data-theme', pref); updateThemeToggleLabel(pref); }
    function toggleTheme() { const current = document.body.getAttribute('data-theme') === 'light' ? 'light' : 'dark'; const next = current === 'light' ? 'dark' : 'light'; document.body.setAttribute('data-theme', next); localStorage.setItem('aqd_theme', next); updateThemeToggleLabel(next); }
    function updateThemeToggleLabel(theme) { const btn = qs('#themeToggle'); if (btn){ const next = theme==='light'? 'dark':'light'; btn.textContent = theme==='light' ? '☀️' : '🌙'; btn.dataset.label = 'Toggle Theme'; btn.setAttribute('aria-label','Switch to '+next+' theme'); } }

    /* Dashboard */
    function buildDashboard() {
        const o = reportData?.overview; 
        if (!o) {
            console.error('No overview data found in reportData:', reportData);
            console.error('reportData keys:', reportData ? Object.keys(reportData) : 'reportData is null/undefined');
            return;
        }
        const grid = qs('#metricsGrid'); 
        if (!grid) {
            console.error('Metrics grid element not found');
            return;
        }
        
        console.log('Building dashboard with overview:', o);
        
        const metrics = [
            { key:'total', label:'Total Tests', value:o.totalTests || 0, className:'', desc:'Total executed tests.' },
            { key:'passed', label:'Passed', value:o.passedTests || 0, className:'success', desc:'Tests that met all assertions.' },
            { key:'failed', label:'Failed', value:o.failedTests || 0, className:(o.failedTests||0)?'error':'', desc:'Functional assertion failures.' },
            { key:'broken', label:'Broken', value:o.brokenTests || 0, className:(o.brokenTests||0)?'warning':'', desc:'Infrastructure / unexpected errors.' },
            { key:'passRate', label:'Pass Rate', value:(o.passRate||0).toFixed(1)+'%', className:(o.passRate||0)>=80?'success':'warning', desc:'Passed ÷ Total × 100.' },
            { key:'time', label:'Execution Time', value:o.executionTime || 'Unknown', className:'', desc:'Aggregated total duration.' }
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
                case 'passedTests': av=a.passedTests||0; bv=b.passedTests||0; break;
                case 'failedTests': av=(a.allFailures||0); bv=(b.allFailures||0); break; // Use combined failures for sorting
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
            const passedBadge = s.passedTests > 0 ? `<span class="status-badge status-passed">${s.passedTests}</span>` : s.passedTests;
            const allFailures = s.allFailures || 0;
            const failuresBadge = allFailures > 0 ? `<span class="status-badge status-failed">${allFailures}</span>` : allFailures;
            return `<tr><td>${i+1}</td><td>${escapeHtml(s.suiteName||'')}</td><td>${s.totalTests||0}</td><td>${passedBadge}</td><td>${failuresBadge}</td><td><span class=\"status-badge status-${cls}\">${pr.toFixed(1)}%</span></td><td>${escapeHtml(s.totalDurationReadable||formatDuration(s.totalDurationMs||0))}</td><td>${escapeHtml(s.avgDurationReadable||formatDuration(s.avgDurationMs||0))}</td><td><span class=\"perf-badge perf-${(s.performanceCategory||'').toLowerCase()}\">${escapeHtml(s.performanceCategory||'')}</span></td></tr>`;
        }).join('');
        updateSuitePerfSortIndicators();
    }

    function setupSuitePerfHeaderSorting(){
        const headerRow = qs('.performance-table thead tr');
        if(!headerRow) return;
        const headers = headerRow.querySelectorAll('th');
        if(!headers.length) return;
        const mapping = [null,'suiteName',null,'passedTests','failedTests','passRate','totalDurationMs'];
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
        const body=qs('#slowTestsBody'); 
        if(!body) return;
        
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
        
        slowTestsAll = (reportData?.testResults||[])
            .filter(t => typeof t.durationMs === 'number')
            .map(t => t);
            
        const suiteSel = qs('#slowSuiteFilter');
        if(suiteSel && suiteSel.options.length===1){
            [...new Set(slowTestsAll.map(t=>t.suiteName).filter(Boolean))].sort().forEach(s=>{ 
                const opt=document.createElement('option'); 
                opt.value=s; 
                opt.textContent=s; 
                suiteSel.appendChild(opt); 
            });
        }
        
        suiteSel?.addEventListener('change',()=>{ slowSuiteFilter=suiteSel.value; filterSlowTests(); });
        const statusSel = qs('#slowStatusFilter');
        statusSel?.addEventListener('change',()=>{ slowStatusFilter=statusSel.value; filterSlowTests(); });
        const limitSel = qs('#slowLimitFilter');
        limitSel?.addEventListener('change',()=>{ const v=limitSel.value; slowLimit = v==='all'?Infinity:parseInt(v,10); filterSlowTests(); });
        
        const headerRow=qs('.slow-tests-table thead tr');
        if(headerRow){ 
            const headers=headerRow.querySelectorAll('th'); 
            if(!Array.from(headers).some(h=>/tags/i.test(h.textContent||''))){ 
                const th=document.createElement('th'); 
                th.textContent='Tags'; 
                headerRow.insertBefore(th, headers[headers.length-1]); 
            } 
        }
        filterSlowTests();
    }

    function filterSlowTests(){
        slowTestsFiltered = slowTestsAll.filter(t => (!slowSuiteFilter || t.suiteName===slowSuiteFilter) && (!slowStatusFilter || t.status===slowStatusFilter));
        slowTestsFiltered.sort((a,b)=>(b.durationMs||0)-(a.durationMs||0));
        if (slowLimit !== Infinity) slowTestsFiltered = slowTestsFiltered.slice(0, slowLimit);
        renderSlowTestsTable();
    }

    function renderSlowTestsTable(){
        const body=qs('#slowTestsBody'); 
        if(!body) return;
        const tagMap=new Map(); 
        (reportData?.testResults||[]).forEach(tr=>{ 
            const key=(tr.suiteName||'')+'||'+(tr.testCaseName||''); 
            if(tr.caseTags) tagMap.set(key, tr.caseTags); 
        });
        body.innerHTML = slowTestsFiltered.map((t,i)=>{ 
            const file=t.screenshotFileName||''; // Use screenshotFileName instead of screenshotPath
            const link=file&&reportData.config.includeScreenshots?buildScreenshotLink(file,t):''; 
            const tags=t.caseTags || tagMap.get((t.suiteName||'')+'||'+(t.testCaseName||'')) || ''; 
            return `<tr><td>${i+1}</td><td>${escapeHtml(truncate(t.testCaseName||'',60))}</td><td>${escapeHtml(t.suiteName||'')}</td><td>${escapeHtml(t.duration||'')}</td><td><span class="status-badge status-${(t.status||'').toLowerCase()}">${escapeHtml(t.status||'')}</span></td><td><span class="perf-badge perf-${(t.performanceCategory||'').toLowerCase()}">${escapeHtml(t.performanceCategory||'')}</span></td><td class="tags-column">${escapeHtml(truncate(tags,30))}</td><td>${link}</td></tr>`; 
        }).join('');
    }

    /* Top Steps */
    function loadTopSteps() {
        console.log('Loading top steps...');
        const body = qs('#topStepsBody');
        if (!body) {
            console.error('Top steps body element not found');
            return;
        }
        
        topStepsData = [...(reportData?.topSteps || [])];
        console.log('Top steps data:', topStepsData);
        
        if (topStepsData.length === 0) {
            console.warn('No top steps data available');
            body.innerHTML = '<tr><td colspan="10">No step data available</td></tr>';
            return;
        }
        
        sortAndRenderTopSteps();
        setupTopStepsHeaderSorting();
    }

    function sortAndRenderTopSteps() {
        const body = qs('#topStepsBody');
        if (!body) return;

        topStepsData.sort((a, b) => {
            let av = a[topStepsSortField];
            let bv = b[topStepsSortField];

            // Handle numeric sorting for specific fields
            if (['avgDurationMs', 'minDurationMs', 'maxDurationMs', 'totalDurationMs', 'count', 'failRate'].includes(topStepsSortField)) {
                av = Number(av) || 0;
                bv = Number(bv) || 0;
            } else if (topStepsSortField === 'rank') {
                av = Number(av) || 0;
                bv = Number(bv) || 0;
            } else {
                // String sorting for non-numeric fields
                av = String(av || '').toLowerCase();
                bv = String(bv || '').toLowerCase();
            }

            if (av < bv) return topStepsSortDirection === 'asc' ? -1 : 1;
            if (av > bv) return topStepsSortDirection === 'asc' ? 1 : -1;
            return 0;
        });

        body.innerHTML = topStepsData.map(s => {
            const rank = s.rank ?? '';
            const stepName = s.stepName || '';
            const truncatedStepName = s.truncatedStepName || '';
            const minDuration = s.minDurationReadable || '';
            const avgDuration = s.avgDurationReadable || '';
            const maxDuration = s.maxDurationReadable || '';
            const totalDuration = s.totalDurationReadable || '';
            const count = s.count ?? 0;
            const failRate = (s.failRate ?? 0).toFixed(1);
            const performanceCategory = (s.performanceCategory || '').toLowerCase();
            const reliabilityCategory = s.reliabilityCategory || '';
            
            return `<tr>
                <td>${rank}</td>
                <td title="${escapeAttr(stepName)}">${escapeHtml(truncatedStepName)}</td>
                <td>${escapeHtml(minDuration)}</td>
                <td>${escapeHtml(avgDuration)}</td>
                <td>${escapeHtml(maxDuration)}</td>
                <td>${escapeHtml(totalDuration)}</td>
                <td>${count}</td>
                <td>${failRate}</td>
                <td><span class="perf-badge perf-${performanceCategory}">${escapeHtml(s.performanceCategory || '')}</span></td>
                <td>${escapeHtml(reliabilityCategory)}</td>
            </tr>`;
        }).join('');
        
        updateTopStepsSortIndicators();
    }
    
    function setupTopStepsHeaderSorting() {
        qsa('.steps-table th[data-sort]').forEach(th => {
            th.addEventListener('click', () => {
                const field = th.getAttribute('data-sort');
                if (!field) return;

                if (topStepsSortField === field) {
                    topStepsSortDirection = topStepsSortDirection === 'asc' ? 'desc' : 'asc';
                } else {
                    topStepsSortField = field;
                    // Set appropriate default direction based on field type
                    if (['avgDurationMs', 'minDurationMs', 'maxDurationMs', 'totalDurationMs', 'count', 'failRate'].includes(field)) {
                        topStepsSortDirection = 'desc'; // Descending for timing and numeric fields (show highest first)
                    } else if (field === 'rank') {
                        topStepsSortDirection = 'asc'; // Ascending for rank (show rank 1 first)
                    } else {
                        topStepsSortDirection = 'asc'; // Ascending for other fields
                    }
                }
                sortAndRenderTopSteps();
            });
        });
    }

    function updateTopStepsSortIndicators() {
        qsa('.steps-table .sort-indicator').forEach(el => { el.textContent = '⇅'; el.style.opacity = '.4'; });
        const active = qs(`.steps-table th[data-sort="${topStepsSortField}"] .sort-indicator`);
        if (active) {
            active.textContent = topStepsSortDirection === 'asc' ? '↑' : '↓';
            active.style.opacity = '1';
        }
    }

    /* Failure Analysis - Unified Failure Analysis (Actionable Triage) */
    function loadFailureAnalysis() {
        console.log('Loading unified failure analysis...');
        
        if (!reportData?.testResults) {
            console.warn('No test results available for failure analysis');
            return;
        }

        // Check if failure analysis data exists
        if (!reportData.failureAnalysis) {
            console.warn('No failure analysis data available');
            renderEmptyFailureAnalysis();
            return;
        }

        console.log('Failure analysis data:', reportData.failureAnalysis);

        // Get failed tests from raw test results
        const failedTests = reportData.testResults.filter(t => t.status === 'Failed' || t.status === 'Broken');
        
        if (failedTests.length === 0) {
            renderEmptyFailureAnalysis();
            return;
        }

        // Use existing failure analysis data if available, otherwise normalize failures
        let normalizedFailures;
        if (reportData.failureAnalysis.commonFailures && reportData.failureAnalysis.commonFailures.length > 0) {
            // Use the C# generated analysis
            renderFailureAnalysisFromData(reportData.failureAnalysis, failedTests.length, reportData.testResults.length);
        } else {
            // Fallback to JavaScript analysis
            console.log('Using JavaScript fallback analysis');
            normalizedFailures = normalizeFailures(failedTests);
            
            // Cluster failures by pattern
            const clusters = clusterFailures(normalizedFailures);
            
            // Render all sections
            renderSummaryStrip(normalizedFailures, reportData.testResults.length);
            renderClustersTable(clusters, normalizedFailures.length);
            renderTestCaseRollup(normalizedFailures);
            renderSeleniumSpotlight(normalizedFailures);
            renderTimeoutSpotlight(normalizedFailures);
        }
        
        // Wire up interactive features
        wireRowActions();
    }

    function renderFailureAnalysisFromData(failureAnalysis, totalFailures, totalTests) {
        console.log('Rendering failure analysis from C# data');
        
        // Render failure categories summary
        const grid = qs('#failureStatsGrid');
        if (grid && failureAnalysis.failureCategories) {
            const fc = failureAnalysis.failureCategories;
            const failureRate = totalTests > 0 ? (totalFailures / totalTests * 100) : 0;
            
            const cards = [
                {
                    label: 'Total Failures',
                    value: `${totalFailures} (${failureRate.toFixed(1)}%)`,
                    className: 'error',
                    desc: `${totalFailures} failures out of ${totalTests} total tests`
                },
                {
                    label: 'Assertion Failures',
                    value: `${fc.assertionFailures} (${fc.assertionFailureRate.toFixed(1)}%)`,
                    className: 'critical',
                    desc: 'Test assertion and validation failures'
                },
                {
                    label: 'Selenium Issues',
                    value: `${fc.seleniumIssues} (${fc.seleniumFailureRate.toFixed(1)}%)`,
                    className: 'high',
                    desc: 'Browser automation and element issues'
                },
                {
                    label: 'Timeout Issues',
                    value: `${fc.timeoutIssues} (${fc.timeoutFailureRate.toFixed(1)}%)`,
                    className: 'warning',
                    desc: 'Wait and timeout related failures'
                }
            ];
            
            grid.innerHTML = cards.map(card => 
                `<div class="metric-card ${card.className}" tabindex="0" data-help="${escapeAttr(card.desc)}">
                    <div class="metric-label">${escapeHtml(card.label)}</div>
                    <div class="metric-value">${escapeHtml(card.value)}</div>
                </div>`
            ).join('');
        }

        // Render common failures
        const commonBody = qs('#commonFailuresBody');
        if (commonBody && failureAnalysis.commonFailures) {
            if (failureAnalysis.commonFailures.length === 0) {
                commonBody.innerHTML = '<tr><td colspan="7">No common failure patterns found</td></tr>';
            } else {
                commonBody.innerHTML = failureAnalysis.commonFailures.map(failure => {
                    const suites = failure.affectedSuites.slice(0, 3).join(', ') + 
                        (failure.affectedSuites.length > 3 ? ` (+${failure.affectedSuites.length - 3} more)` : '');
                    const tests = failure.affectedTestCases.slice(0, 5).join(', ') + 
                        (failure.affectedTestCases.length > 5 ? ` (+${failure.affectedTestCases.length - 5} more)` : '');
                    
                    return `<tr>
                        <td title="${escapeAttr(failure.pattern)}">${escapeHtml(truncateText(failure.pattern, 60))}</td>
                        <td><span class="status-badge status-${failure.category.toLowerCase()}">${escapeHtml(failure.category)}</span></td>
                        <td><span class="failure-count">${failure.failureCount}</span></td>
                        <td title="${escapeAttr(failure.affectedTestCases.join(', '))}">${escapeHtml(tests)}</td>
                        <td title="${escapeAttr(failure.affectedSuites.join(', '))}">${escapeHtml(suites)}</td>
                        <td><span class="impact-badge impact-${failure.impact.toLowerCase()}">${escapeHtml(failure.impact)}</span></td>
                        <td>${escapeHtml(failure.recommendedAction)}</td>
                    </tr>`;
                }).join('');
            }
        }

        // Render test case analysis
        const testCaseBody = qs('#testCaseFailuresBody');
        if (testCaseBody && failureAnalysis.testCaseAnalysis) {
            if (failureAnalysis.testCaseAnalysis.length === 0) {
                testCaseBody.innerHTML = '<tr><td colspan="6">No test case failures found</td></tr>';
            } else {
                testCaseBody.innerHTML = failureAnalysis.testCaseAnalysis.map(testCase => {
                    const suites = testCase.affectedSuites.slice(0, 2).join(', ') + 
                        (testCase.affectedSuites.length > 2 ? ` (+${testCase.affectedSuites.length - 2} more)` : '');
                    
                    return `<tr>
                        <td><strong>${escapeHtml(testCase.testCaseId)}</strong></td>
                        <td><span class="failure-count">${testCase.totalFailures}</span></td>
                        <td><span class="status-badge status-${testCase.failureCategory.toLowerCase()}">${escapeHtml(testCase.failureCategory)}</span></td>
                        <td title="${escapeAttr(testCase.primaryFailureReason)}">${escapeHtml(truncateText(testCase.primaryFailureReason, 60))}</td>
                        <td title="${escapeAttr(testCase.affectedSuites.join(', '))}">${escapeHtml(suites)}</td>
                        <td>
                            <button class="details-btn" data-testcase="${escapeAttr(testCase.testCaseId)}" type="button">Details</button>
                        </td>
                    </tr>`;
                }).join('');
            }
        }

        // Render Selenium analysis
        const seleniumBody = qs('#seleniumIssuesBody');
        const seleniumStats = qs('#seleniumStats');
        if (seleniumBody && failureAnalysis.seleniumAnalysis) {
            const sa = failureAnalysis.seleniumAnalysis;
            
            if (seleniumStats) {
                seleniumStats.innerHTML = `<div class="selenium-summary">
                    <span class="selenium-total">Total Selenium Issues: <strong>${sa.totalSeleniumIssues}</strong></span>
                </div>`;
            }
            
            if (sa.issueCategories && sa.issueCategories.length > 0) {
                seleniumBody.innerHTML = sa.issueCategories.map(category => 
                    `<tr>
                        <td>${escapeHtml(category.category)}</td>
                        <td><span class="failure-count">${category.count}</span></td>
                        <td>${category.percentage.toFixed(1)}%</td>
                        <td>${escapeHtml(category.recommendedFix)}</td>
                    </tr>`
                ).join('');
            } else {
                seleniumBody.innerHTML = '<tr><td colspan="4">No Selenium issues detected</td></tr>';
            }
        }

        // Render Timeout analysis
        const timeoutBody = qs('#timeoutIssuesBody');
        const timeoutStats = qs('#timeoutStats');
        if (timeoutBody && failureAnalysis.timeoutAnalysis) {
            const ta = failureAnalysis.timeoutAnalysis;
            
            if (timeoutStats) {
                const topSteps = ta.topTimeoutSteps && ta.topTimeoutSteps.length > 0 ? 
                    ta.topTimeoutSteps.slice(0, 3).map(step => `<span class="timeout-step">${escapeHtml(step)}</span>`).join('') : '';
                    
                timeoutStats.innerHTML = `<div class="timeout-summary">
                    <span class="timeout-total">Total Timeout Issues: <strong>${ta.totalTimeoutIssues}</strong></span>
                    ${topSteps ? `<div class="timeout-steps">Top contexts: ${topSteps}</div>` : ''}
                </div>`;
            }
            
            if (ta.timeoutCategories && ta.timeoutCategories.length > 0) {
                timeoutBody.innerHTML = ta.timeoutCategories.map(category => 
                    `<tr>
                        <td>${escapeHtml(category.type)}</td>
                        <td><span class="failure-count">${category.count}</span></td>
                        <td>${category.percentage.toFixed(1)}%</td>
                        <td>${escapeHtml(category.recommendedFix)}</td>
                    </tr>`
                ).join('');
            } else {
                timeoutBody.innerHTML = '<tr><td colspan="4">No timeout issues detected</td></tr>';
            }
        }
    }

    function normalizeFailures(failedTests) {
        return failedTests.map(test => {
            const reasonRaw = test.failureReason || '';
            const step = test.failingStep || '';
            const combinedText = `${reasonRaw} ${step}`.toLowerCase();
            
            // Apply categorization rules in priority order
            const category = categorizeFailure(reasonRaw, step);
            const patternKey = generatePatternKey(reasonRaw, step, category);
            
            // Extract Expected/Actual snippets from NUnit-style diffs
            const { expectedSnippet, actualSnippet } = extractNUnitSnippets(reasonRaw);
            
            // Extract locator if present
            const locator = extractLocator(reasonRaw, step);
            
            // Parse HTTP information
            const http = parseHttpInfo(reasonRaw);
            
            // Check if timeout
            const isTimeout = RX.timeout.test(combinedText);
            
            // Extract test case ID
            const testId = extractTestCaseId(test.caseTags) || test.testCaseName;
            
            return {
                testId,
                suite: test.suiteName || 'Unknown Suite',
                status: test.status.toLowerCase(),
                step: truncateText(step, 60),
                reasonRaw: reasonRaw,
                category,
                patternKey,
                expectedSnippet,
                actualSnippet,
                locator,
                http,
                isTimeout,
                durationMs: test.durationMs || 0,
                screenshot: test.screenshotFileName || null, // Use the correct field name
                tags: test.caseTags ? test.caseTags.split(',').map(t => t.trim()) : []
            };
        });
    }

    // Regex patterns for failure detection
    const RX = {
        elementNotFound: /Element not found|NoSuchElement|stale element|invalid selector/i,
        extractLocator: /Element not found:\s*(.+?)(?:\s+after|\s*$)/i,
        timeout: /(timed out|timeout|A task was canceled|wait.*exceeded)/i,
        nunitExpected: /^\s*Expected:\s*(.+)$/mi,
        nunitActual: /^\s*But was:\s*(.+)$/mi,
        messageMismatch: /Unexpected Error Message|Expected.*message.*But was/i,
        httpProblem: /(Problem\+JSON|HTTP (request|status)|status mismatch)/i,
        errorKeyInStep: /error as '([^']+)'/i,
        valueMismatch: /Expected.*but was/i,
        environment: /certificate|DNS|connection refused|env|rate limit/i
    };

    function categorizeFailure(reasonRaw, step) {
        const combinedText = `${reasonRaw} ${step}`.toLowerCase();
        
        // Priority order categorization rules
        if (RX.elementNotFound.test(combinedText)) return 'ElementNotFound';
        if (RX.timeout.test(combinedText)) return 'Timeout';
        if (RX.messageMismatch.test(reasonRaw)) return 'MessageMismatch';
        if (RX.valueMismatch.test(reasonRaw)) return 'ValueMismatch';
        if (RX.httpProblem.test(combinedText)) return 'HTTP';
        if (RX.environment.test(combinedText)) return 'Environment';
        
        return 'Other';
    }

    function generatePatternKey(reasonRaw, step, category) {
        switch (category) {
            case 'ElementNotFound':
                const locator = extractLocator(reasonRaw, step);
                return `ElementNotFound::${locator || 'UnknownElement'}`;
                
            case 'Timeout':
                const context = extractTimeoutContext(step);
                return `Timeout::${context}`;
                
            case 'MessageMismatch':
                const errorKey = extractErrorKey(step) || extractFirstWords(reasonRaw, 6);
                return `MessageMismatch::${errorKey}`;
                
            case 'ValueMismatch':
                const field = extractFieldName(reasonRaw) || 'UnknownField';
                return `ValueMismatch::${field}`;
                
            case 'HTTP':
                const method = extractHttpMethod(reasonRaw);
                return `HTTP::${method || 'UnknownMethod'}`;
                
            default:
                return `Other::${extractFirstWords(reasonRaw, 4)}`;
        }
    }

    function extractNUnitSnippets(reasonRaw) {
        const expectedMatch = RX.nunitExpected.exec(reasonRaw);
        const actualMatch = RX.nunitActual.exec(reasonRaw);
        
        return {
            expectedSnippet: expectedMatch ? truncateText(expectedMatch[1], 12, 'words') : null,
            actualSnippet: actualMatch ? truncateText(actualMatch[1], 12, 'words') : null
        };
    }

    function extractLocator(reasonRaw, step) {
        const match = RX.extractLocator.exec(reasonRaw);
        if (match) return normalizeWhitespace(match[1]);
        
        // Try to extract from step
        const stepLocatorMatch = /(?:element|locator|selector)[\s:]+['"]([^'"]+)['"]/i.exec(step);
        return stepLocatorMatch ? stepLocatorMatch[1] : null;
    }

    function parseHttpInfo(reasonRaw) {
        const httpMatch = /HTTP.*(\w+).*(\d{3})/i.exec(reasonRaw);
        if (httpMatch) {
            return {
                method: httpMatch[1],
                status: parseInt(httpMatch[2]),
                url: null // Could be extracted if pattern exists
            };
        }
        return null;
    }

    function extractTestCaseId(tags) {
        if (!tags) return null;
        const match = /\bC\d{4,5}\b/i.exec(tags);
        return match ? match[0].toUpperCase() : null;
    }

    function extractTimeoutContext(step) {
        if (/print.*file/i.test(step)) return 'WaitPrintFile';
        if (/navigate|page|load/i.test(step)) return 'RemoteNavigate';
        if (/element|locate|find/i.test(step)) return 'ElementWait';
        return 'General';
    }

    function extractErrorKey(step) {
        const match = RX.errorKeyInStep.exec(step);
        return match ? match[1] : null;
    }

    function extractFieldName(reasonRaw) {
        // Try to extract field name from common patterns
        const patterns = [
            /Expected (\w+)/i,
            /payment (\w+)/i,
            /status (\w+)/i
        ];
        
        for (const pattern of patterns) {
            const match = pattern.exec(reasonRaw);
            if (match) return match[1];
        }
        return null;
    }

    function extractHttpMethod(reasonRaw) {
        const match = /(GET|POST|PUT|DELETE|PATCH)/i.exec(reasonRaw);
        return match ? match[1].toUpperCase() : null;
    }

    function extractFirstWords(text, wordCount) {
        if (!text) return '';
        return text.split(/\s+/).slice(0, wordCount).join(' ');
    }

    function normalizeWhitespace(text) {
        return text ? text.replace(/\s+/g, ' ').trim() : '';
    }

    function clusterFailures(failures) {
        const clusters = {};
        
        failures.forEach(failure => {
            const key = failure.patternKey;
            if (!clusters[key]) {
                clusters[key] = {
                    category: failure.category,
                    tests: new Set(),
                    suites: new Set(),
                    count: 0,
                    examples: []
                };
            }
            
            clusters[key].tests.add(failure.testId);
            clusters[key].suites.add(failure.suite);
            clusters[key].count++;
            
            if (clusters[key].examples.length < 3) {
                clusters[key].examples.push(failure);
            }
        });
        
        return clusters;
    }

    function computeImpactBadge(cluster) {
        const score = cluster.count + 0.5 * (cluster.suites.size - 1);
        let label;
        
        if (score >= 6) label = 'High';
        else if (score >= 3) label = 'Medium';
        else label = 'Low';
        
        return { score, label };
    }

    function renderSummaryStrip(failures, totalTests) {
        const grid = qs('#failureStatsGrid');
        if (!grid) return;
        
        const totalFailures = failures.length;
        const failureRate = totalTests > 0 ? (totalFailures / totalTests * 100) : 0;
        
        // Category breakdown
        const categories = {
            ElementNotFound: failures.filter(f => f.category === 'ElementNotFound').length,
            Timeout: failures.filter(f => f.category === 'Timeout').length,
            MessageMismatch: failures.filter(f => f.category === 'MessageMismatch').length,
            ValueMismatch: failures.filter(f => f.category === 'ValueMismatch').length,
            HTTP: failures.filter(f => f.category === 'HTTP').length
        };
        
        // Top impacted suites
        const suiteFailures = {};
        failures.forEach(f => {
            suiteFailures[f.suite] = (suiteFailures[f.suite] || 0) + 1;
        });
        const topSuites = Object.entries(suiteFailures)
            .sort(([,a], [,b]) => b - a)
            .slice(0, 3)
            .map(([suite, count]) => `${suite} (${count})`);
        
        const cards = [
            {
                label: 'Total Failures',
                value: `${totalFailures} (${failureRate.toFixed(1)}%)`,
                className: 'error',
                desc: `${totalFailures} failures out of ${totalTests} total tests`
            },
            {
                label: 'Element Issues',
                value: `${categories.ElementNotFound} (${(categories.ElementNotFound/totalFailures*100).toFixed(1)}%)`,
                className: 'critical',
                desc: 'Element not found and locator issues'
            },
            {
                label: 'Timeouts',
                value: `${categories.Timeout} (${(categories.Timeout/totalFailures*100).toFixed(1)}%)`,
                className: 'high',
                desc: 'Timeout and wait-related failures'
            },
            {
                label: 'Assertions',
                value: `${categories.ValueMismatch + categories.MessageMismatch} (${((categories.ValueMismatch + categories.MessageMismatch)/totalFailures*100).toFixed(1)}%)`,
                className: 'warning',
                desc: 'Value mismatches and message assertion failures'
            }
        ];
        
        grid.innerHTML = cards.map(card => 
            `<div class="metric-card ${card.className}" tabindex="0" data-help="${escapeAttr(card.desc)}">
                <div class="metric-label">${escapeHtml(card.label)}</div>
                <div class="metric-value">${escapeHtml(card.value)}</div>
            </div>`
        ).join('') + 
        `<div class="metric-card info" tabindex="0" data-help="Suites with the most failures">
            <div class="metric-label">Top Impacted Suites</div>
            <div class="metric-value" style="font-size: 0.8em;">${escapeHtml(topSuites.join(', ') || 'None')}</div>
        </div>`;
    }

    function renderClustersTable(clusters, totalFailures) {
        const body = qs('#commonFailuresBody');
        if (!body) return;
        
        const clusterEntries = Object.entries(clusters)
            .map(([key, cluster]) => ({ key, ...cluster, impact: computeImpactBadge(cluster) }))
            .sort((a, b) => b.impact.score - a.impact.score);
        
        if (clusterEntries.length === 0) {
            body.innerHTML = '<tr><td colspan="9">No failure patterns found</td></tr>';
            return;
        }
        
        body.innerHTML = clusterEntries.map(cluster => {
            const percentage = ((cluster.count / totalFailures) * 100).toFixed(1);
            const suites = Array.from(cluster.suites).slice(0, 3).join(', ') + 
                (cluster.suites.size > 3 ? ` (+${cluster.suites.size - 3} more)` : '');
            const tests = Array.from(cluster.tests).slice(0, 5).join(', ') + 
                (cluster.tests.size > 5 ? ` (+${cluster.tests.size - 5} more)` : '');
            
            const businessSymptom = getBusinessSymptom(cluster.category, cluster.key);
            const rootCauseSignal = getRootCauseSignal(cluster.category, cluster.key);
            const nextAction = getNextAction(cluster.category);
            const owner = getOwner(suites);
            
            return `<tr>
                <td title="${escapeAttr(cluster.key)}">${escapeHtml(truncateText(cluster.key, 60))}</td>
                <td><span class="status-badge status-${cluster.category.toLowerCase()}">${escapeHtml(cluster.category)}</span></td>
                <td><span class="failure-count">${cluster.count}</span> (${percentage}%)</td>
                <td title="${escapeAttr(Array.from(cluster.suites).join(', '))}">${escapeHtml(suites)}</td>
                <td title="${escapeAttr(Array.from(cluster.tests).join(', '))}">${escapeHtml(tests)}</td>
                <td>${escapeHtml(businessSymptom)}</td>
                <td>${escapeHtml(rootCauseSignal)}</td>
                <td><span class="impact-badge impact-${cluster.impact.label.toLowerCase()}">${escapeHtml(cluster.impact.label)}</span></td>
                <td>${escapeHtml(nextAction)}</td>
                <td>${escapeHtml(owner)}</td>
            </tr>`;
        }).join('');
    }

    function renderTestCaseRollup(failures) {
        const body = qs('#testCaseFailuresBody');
        if (!body) return;
        
        // Group by test case ID
        const testCaseGroups = {};
        failures.forEach(failure => {
            const id = failure.testId;
            if (!testCaseGroups[id]) {
                testCaseGroups[id] = [];
            }
            testCaseGroups[id].push(failure);
        });
        
        const entries = Object.entries(testCaseGroups)
            .map(([testId, testFailures]) => {
                const categories = testFailures.map(f => f.category);
                const dominantCategory = getMostFrequent(categories);
                const primaryReason = testFailures[0].patternKey.split('::')[1] || dominantCategory;
                const affectedSuites = [...new Set(testFailures.map(f => f.suite))];
                const hasScreenshot = testFailures.some(f => f.screenshot);
                
                return {
                    testId,
                    failures: testFailures,
                    failureCount: testFailures.length,
                    dominantCategory,
                    primaryReason,
                    affectedSuites,
                    hasScreenshot
                };
            })
            .sort((a, b) => b.failureCount - a.failureCount);
        
        if (entries.length === 0) {
            body.innerHTML = '<tr><td colspan="7">No test case failures found</td></tr>';
            return;
        }
        
        body.innerHTML = entries.map(entry => {
            const suites = entry.affectedSuites.slice(0, 2).join(', ') + 
                (entry.affectedSuites.length > 2 ? ` (+${entry.affectedSuites.length - 2} more)` : '');
            const nextStep = getNextAction(entry.dominantCategory);
            
            return `<tr>
                <td><strong>${escapeHtml(entry.testId)}</strong></td>
                <td><span class="failure-count">${entry.failureCount}</span></td>
                <td><span class="status-badge status-${entry.dominantCategory.toLowerCase()}">${escapeHtml(entry.dominantCategory)}</span></td>
                <td title="${escapeAttr(entry.primaryFailureReason)}">${escapeHtml(truncateText(entry.primaryFailureReason, 60))}</td>
                <td title="${escapeAttr(entry.affectedSuites.join(', '))}">${escapeHtml(suites)}</td>
                <td>
                    ${entry.hasScreenshot ? `<button class="screenshot-btn" data-testcase="${escapeAttr(entry.testId)}" type="button">Screenshot</button>` : ''}
                    <button class="details-btn" data-testcase="${escapeAttr(entry.testId)}" type="button">Details</button>
                </td>
                <td>${escapeHtml(nextStep)}</td>
            </tr>`;
        }).join('');
    }

    function renderSeleniumSpotlight(failures) {
        const body = qs('#seleniumIssuesBody');
        if (!body) return;
        
        const seleniumFailures = failures.filter(f => 
            f.category === 'ElementNotFound' || f.category === 'Timeout'
        );
        
        if (seleniumFailures.length === 0) {
            body.innerHTML = '<tr><td colspan="4">No Selenium issues detected</td></tr>';
            return;
        }
        
        // Aggregate by issue type
        const issueTypes = {
            'Element Locator Issues': seleniumFailures.filter(f => f.category === 'ElementNotFound').length,
            'Timeout/Wait Issues': seleniumFailures.filter(f => f.category === 'Timeout' && f.isTimeout).length
        };
        
        const stats = qs('#seleniumStats');
        if (stats) {
            stats.innerHTML = `<div class="selenium-summary">
                <span class="selenium-total">Total Selenium Issues: <strong>${seleniumFailures.length}</strong></span>
            </div>`;
        }
        
        body.innerHTML = Object.entries(issueTypes)
            .filter(([, count]) => count > 0)
            .map(([type, count]) => {
                const percentage = ((count / seleniumFailures.length) * 100).toFixed(1);
                const recommendation = getSeleniumRecommendation(type);
                
                return `<tr>
                    <td>${escapeHtml(type)}</td>
                    <td><span class="failure-count">${count}</span></td>
                    <td>${percentage}%</td>
                    <td>${escapeHtml(recommendation)}</td>
                </tr>`;
            }).join('');
    }

    function renderTimeoutSpotlight(failures) {
        const body = qs('#timeoutIssuesBody');
        if (!body) return;
        
        const timeoutFailures = failures.filter(f => f.isTimeout);
        
        if (timeoutFailures.length === 0) {
            body.innerHTML = '<tr><td colspan="4">No timeout issues detected</td></tr>';
            return;
        }
        
        // Group by timeout context
        const contexts = {};
        timeoutFailures.forEach(f => {
            const context = extractTimeoutContext(f.step);
            if (!contexts[context]) {
                contexts[context] = [];
            }
            contexts[context].push(f);
        });
        
        const stats = qs('#timeoutStats');
        if (stats) {
            const topSteps = Object.entries(contexts)
                .sort(([,a], [,b]) => b.length - a.length)
                .slice(0, 3)
                .map(([context, failures]) => `<span class="timeout-step">${context} (${failures.length})</span>`)
                .join('');
                
            stats.innerHTML = `<div class="timeout-summary">
                <span class="timeout-total">Total Timeout Issues: <strong>${timeoutFailures.length}</strong></span>
                ${topSteps ? `<div class="timeout-steps">Top contexts: ${topSteps}</div>` : ''}
            </div>`;
        }
        
        body.innerHTML = Object.entries(contexts)
            .sort(([,a], [,b]) => b.length - a.length)
            .map(([context, contextFailures]) => {
                const percentage = ((contextFailures.length / timeoutFailures.length) * 100).toFixed(1);
                const avgDuration = contextFailures.reduce((sum, f) => sum + (f.durationMs || 0), 0) / contextFailures.length;
                const recommendation = getTimeoutRecommendation(context);
                
                return `<tr>
                    <td>${escapeHtml(context)}</td>
                    <td><span class="failure-count">${contextFailures.length}</span></td>
                    <td>${formatDuration(avgDuration)}</td>
                    <td>${escapeHtml(recommendation)}</td>
                </tr>`;
            }).join('');
    }

    function wireRowActions() {
        // Wire screenshot buttons
        document.addEventListener('click', e => {
            if (e.target.classList.contains('screenshot-btn')) {
                const testCaseId = e.target.getAttribute('data-testcase');
                // Find first failure with screenshot for this test case
                const failure = reportData.testResults.find(t => 
                    extractTestCaseId(t.caseTags) === testCaseId && t.screenshotFileName
                );
                if (failure) {
                    showScreenshotModal(failure.screenshotFileName, {
                        testCaseName: failure.testCaseName,
                        suiteName: failure.suiteName,
                        status: failure.status,
                        duration: failure.duration,
                        caseTags: failure.caseTags,
                        failingStep: failure.failingStep,
                        failureReason: failure.failureReason
                    });
                }
            }
        });
        
        // Wire details buttons
        document.addEventListener('click', e => {
            if (e.target.classList.contains('details-btn')) {
                const testCaseId = e.target.getAttribute('data-testcase');
                showTestCaseDetails(testCaseId);
            }
        });
    }

    function showTestCaseDetails(testCaseId) {
        const failures = reportData.testResults.filter(t => 
            extractTestCaseId(t.caseTags) === testCaseId && (t.status === 'Failed' || t.status === 'Broken')
        );
        
        if (failures.length === 0) return;
        
        const modal = qs('#screenshotModal');
        const modalTitle = qs('#modalTitle');
        const modalBody = qs('.modal-body');
        
        if (!modal || !modalTitle || !modalBody) return;
        
        modalTitle.innerHTML = `<i class="fa-solid fa-hashtag"></i> Test Case ${escapeHtml(testCaseId)} Failure Details`;
        
        const detailsHtml = `
            <div class="testcase-details">
                <div class="testcase-summary">
                    <h4>Summary</h4>
                    <p><strong>Total Failures:</strong> ${failures.length}</p>
                    <p><strong>Affected Suites:</strong> ${[...new Set(failures.map(f => f.suiteName))].join(', ')}</p>
                </div>
                <div class="failure-details">
                    <h4>Failure Details</h4>
                    <div class="table-container">
                        <table class="failure-details-table">
                            <thead>
                                <tr><th>Suite</th><th>Test Name</th><th>Duration</th><th>Failure Reason</th><th>Failing Step</th></tr>
                            </thead>
                            <tbody>
                                ${failures.map(failure => `
                                    <tr>
                                        <td>${escapeHtml(failure.suiteName)}</td>
                                        <td title="${escapeAttr(failure.testCaseName)}">${escapeHtml(truncateText(failure.testCaseName, 30))}</td>
                                        <td>${escapeHtml(failure.duration)}</td>
                                        <td title="${escapeAttr(failure.failureReason)}">${escapeHtml(truncateText(failure.failureReason, 40))}</td>
                                        <td title="${escapeAttr(failure.failingStep)}">${escapeHtml(truncateText(failure.failingStep, 40))}</td>
                                    </tr>
                                `).join('')}
                            </tbody>
                        </table>
                    </div>
                </div>
            </div>
        `;
        
        modalBody.innerHTML = detailsHtml;
        modal.classList.add('show');
    }

    function renderEmptyFailureAnalysis() {
        const sections = ['#failureStatsGrid', '#commonFailuresBody', '#testCaseFailuresBody', '#seleniumIssuesBody', '#timeoutIssuesBody'];
        sections.forEach(selector => {
            const element = qs(selector);
            if (element) {
                if (selector === '#failureStatsGrid') {
                    element.innerHTML = '<div class="metric-card success"><div class="metric-label">No Failures</div><div class="metric-value">All tests passed!</div></div>';
                } else {
                    const colspan = selector.includes('commonFailures') ? '10' : selector.includes('testCase') ? '7' : '4';
                    element.innerHTML = `<tr><td colspan="${colspan}">No failures to analyze</td></tr>`;
                }
            }
        });
    }

    // Helper functions for generating business-focused content
    function getBusinessSymptom(category, patternKey) {
        switch (category) {
            case 'ElementNotFound': return 'Cannot locate UI element';
            case 'Timeout': return 'Operation taking too long';
            case 'MessageMismatch': return 'Error message mismatch';
            case 'ValueMismatch': return 'Status/value transition mismatch';
            case 'HTTP': return 'API/service communication issue';
            default: return 'Unexpected test behavior';
        }
    }

    function getRootCauseSignal(category, patternKey) {
        switch (category) {
            case 'ElementNotFound': return 'Brittle locator or UI changes';
            case 'Timeout': return patternKey.includes('WaitPrintFile') ? 'Server processing delay' : 'Fixed wait insufficient';
            case 'MessageMismatch': return 'UI copy changes or feature flags';
            case 'ValueMismatch': return 'Business logic or state transition issue';
            case 'HTTP': return 'Service deployment or configuration';
            default: return 'Requires investigation';
        }
    }

    function getNextAction(category) {
        switch (category) {
            case 'ElementNotFound': return 'Stabilize selector, add explicit wait, review retries';
            case 'Timeout': return 'Replace fixed sleeps with condition waits, tune timeouts';
            case 'MessageMismatch': return 'Align expected copy with product source';
            case 'ValueMismatch': return 'Verify business rule seed/DB, check state transitions';
            case 'HTTP': return 'Capture and assert response details, enrich error handling';
            default: return 'Investigate root cause and add error handling';
        }
    }

    function getSeleniumRecommendation(issueType) {
        switch (issueType) {
            case 'Element Locator Issues': return 'Use data-testid attributes, avoid nth-child selectors';
            case 'Timeout/Wait Issues': return 'Implement explicit waits and element readiness checks';
            default: return 'Follow Selenium best practices';
        }
    }

    function getTimeoutRecommendation(context) {
        switch (context) {
            case 'WaitPrintFile': return 'Add server readiness probe, increase file generation timeout';
            case 'RemoteNavigate': return 'Add page load wait conditions, check network performance';
            case 'ElementWait': return 'Use WebDriverWait with expected conditions';
            default: return 'Implement condition-based waits instead of fixed delays';
        }
    }

    function getOwner(suites) {
        // Simple owner mapping - could be made configurable
        const OWNERS = {
            'StopModification': 'Team-VW',
            'GetDocument': 'Team-CP-ACH',
            'default': 'Unassigned'
        };
        
        for (const [suite, owner] of Object.entries(OWNERS)) {
            if (suites.includes(suite)) return owner;
        }
        return OWNERS.default;
    }

    function getMostFrequent(array) {
        const counts = {};
        array.forEach(item => counts[item] = (counts[item] || 0) + 1);
        return Object.keys(counts).reduce((a, b) => counts[a] > counts[b] ? a : b);
    }

    function truncateText(str, maxLength, unit = 'chars') {
        if (!str) return '';
        
        if (unit === 'words') {
            const words = str.split(/\s+/);
            if (words.length <= maxLength) return str;
            return words.slice(0, maxLength).join(' ') + '...';
        }
        
        if (str.length <= maxLength) return str;
        return str.substring(0, maxLength - 3) + '...';
    }

    // Additional utility functions needed for the report
    function formatDuration(ms) {
        if (!ms || ms < 0) return '0 ms';
        
        const seconds = Math.floor(ms / 1000);
        const minutes = Math.floor(seconds / 60);
        const hours = Math.floor(minutes / 60);
        
        if (hours > 0) {
            return `${hours} h ${minutes % 60} m ${seconds % 60} s`;
        } else if (minutes > 0) {
            return `${minutes} m ${seconds % 60} s`;
        } else if (seconds > 0) {
            return `${seconds} s`;
        } else {
            return `${ms} ms`;
        }
    }

    function escapeHtml(str) {
        if (!str) return '';
        const div = document.createElement('div');
        div.textContent = str;
        return div.innerHTML;
    }

    function escapeAttr(str) {
        if (!str) return '';
        return str.replace(/&/g, '&amp;')
                  .replace(/</g, '&lt;')
                  .replace(/>/g, '&gt;')
                  .replace(/"/g, '&quot;')
                  .replace(/'/g, '&#39;');
    }

    function truncate(str, maxLength) {
        if (!str || str.length <= maxLength) return str || '';
        return str.substring(0, maxLength - 3) + '...';
    }

    function buildScreenshotLink(fileName, testData) {
        if (!fileName) return '';
        return `<button class="view-details-btn" onclick="showScreenshotModal('images/${escapeAttr(fileName)}', ${escapeAttr(JSON.stringify(testData))})" type="button">View</button>`;
    }

    function showScreenshotModal(fileName, testData) {
        const modal = qs('#screenshotModal');
        const modalTitle = qs('#modalTitle');
        const modalImage = qs('#modalImage');
        const modalDetails = qs('#modalDetails');
        const modalError = qs('#modalError');
        
        if (!modal || !modalTitle || !modalImage || !modalDetails) return;
        
        modalTitle.innerHTML = `<i class="fa-solid fa-image"></i> Screenshot - ${escapeHtml(testData.testCaseName || 'Test')}`;
        
        // Build test details
        const details = [
            `<strong>Test Case:</strong> ${escapeHtml(testData.testCaseName || 'Unknown')}`,
            `<strong>Suite:</strong> ${escapeHtml(testData.suiteName || 'Unknown')}`,
            `<strong>Status:</strong> <span class="status-badge status-${(testData.status || 'unknown').toLowerCase()}">${escapeHtml(testData.status || 'Unknown')}</span>`,
            `<strong>Duration:</strong> ${escapeHtml(testData.duration || 'Unknown')}`,
            testData.caseTags ? `<strong>Tags:</strong> ${escapeHtml(testData.caseTags)}` : '',
            testData.failingStep ? `<strong>Failing Step:</strong> ${escapeHtml(testData.failingStep)}` : '',
            testData.failureReason ? `<strong>Failure Reason:</strong> ${escapeHtml(testData.failureReason)}` : ''
        ].filter(Boolean).join('<br>');
        
        modalDetails.innerHTML = details;
        
        // Load screenshot
        modalImage.onload = () => {
            if (modalError) modalError.style.display = 'none';
            modalImage.style.display = 'block';
        };
        
        modalImage.onerror = () => {
            if (modalError) {
                modalError.style.display = 'block';
                modalError.textContent = 'Failed to load screenshot';
            }
            modalImage.style.display = 'none';
        };
        
        // Ensure fileName includes proper path
        const imagePath = fileName.startsWith('images/') ? fileName : `images/${fileName}`;
        modalImage.src = imagePath;
        modal.classList.add('show');
    }

    /* Test Results Table and Navigation */
    function populateSuiteFilter() {
        const select = qs('#suiteFilter');
        if (!select) {
            console.error('Suite filter element not found');
            return;
        }
        
        if (!reportData?.testResults) {
            console.error('No test results available for suite filter');
            return;
        }
        
        console.log('Populating suite filter with', reportData.testResults.length, 'test results');
        const suites = [...new Set(reportData.testResults.map(t => t.suiteName).filter(Boolean))].sort();
        console.log('Found suites:', suites);
        
        suites.forEach(suite => {
            const option = document.createElement('option');
            option.value = suite;
            option.textContent = suite;
            select.appendChild(option);
        });
    }

    function applyFilters() {
        const searchTerm = (qs('#searchInput')?.value || '').toLowerCase();
        const statusFilter = qs('#statusFilter')?.value || '';
        const suiteFilter = qs('#suiteFilter')?.value || '';
        
        filteredTests = (reportData?.testResults || []).filter(test => {
            const matchesSearch = !searchTerm || 
                (test.testCaseName && test.testCaseName.toLowerCase().includes(searchTerm)) ||
                (test.caseTags && test.caseTags.toLowerCase().includes(searchTerm));
            const matchesStatus = !statusFilter || test.status === statusFilter;
            const matchesSuite = !suiteFilter || test.suiteName === suiteFilter;
            
            return matchesSearch && matchesStatus && matchesSuite;
        });
        
        currentPage = 1;
        renderTestResults();
        updateResultsInfo();
        renderPagination();
    }

    function renderTestResults() {
        const tbody = qs('#testResultsBody');
        if (!tbody) return;
        
        const startIndex = pageSize === 'all' ? 0 : (currentPage - 1) * pageSize;
        const endIndex = pageSize === 'all' ? filteredTests.length : startIndex + pageSize;
        const pageTests = filteredTests.slice(startIndex, endIndex);
        
        tbody.innerHTML = pageTests.map((test, index) => {
            const globalIndex = startIndex + index + 1;
            const tags = test.caseTags || '';
            const failingStep = test.failingStep ? truncate(test.failingStep, 40) : '';
            const failureReason = test.failureReason ? truncate(test.failureReason, 50) : '';
            const screenshotButton = test.screenshotFileName ? 
                `<button class="view-details-btn" onclick="showScreenshotModal('images/${escapeAttr(test.screenshotFileName)}', ${escapeAttr(JSON.stringify(test))})" type="button">View</button>` : '';
            
            return `<tr>
                <td>${globalIndex}</td>
                <td>${escapeHtml(test.suiteName)}</td>
                <td title="${escapeAttr(test.testCaseName)}">${escapeHtml(truncate(test.testCaseName, 60))}</td>
                <td><span class="status-badge status-${test.status.toLowerCase()}">${escapeHtml(test.status)}</span></td>
                <td>${escapeHtml(test.duration)}</td>
                <td><span class="perf-badge perf-${(test.performanceCategory || '').toLowerCase()}">${escapeHtml(test.performanceCategory || '')}</span></td>
                <td title="${escapeAttr(tags)}">${escapeHtml(truncate(tags, 30))}</td>
                <td title="${escapeAttr(test.failingStep || '')}">${escapeHtml(failingStep)}</td>
                <td title="${escapeAttr(test.failureReason || '')}">${escapeHtml(failureReason)}</td>
                <td>${screenshotButton}</td>
            </tr>`;
        }).join('');
    }

    function updateResultsInfo() {
        const info = qs('#resultsInfo');
        if (!info) return;
        
        const total = filteredTests.length;
        const allTotal = (reportData?.testResults || []).length;
        const startIndex = pageSize === 'all' ? 1 : (currentPage - 1) * pageSize + 1;
        const endIndex = pageSize === 'all' ? total : Math.min(currentPage * pageSize, total);
        
        if (total === 0) {
            info.textContent = 'No tests found';
        } else if (pageSize === 'all') {
            info.textContent = `Showing all ${total} tests` + (total !== allTotal ? ` (filtered from ${allTotal})` : '');
        } else {
            info.textContent = `Showing ${startIndex}-${endIndex} of ${total} tests` + (total !== allTotal ? ` (filtered from ${allTotal})` : '');
        }
    }

    function renderPagination() {
        const container = qs('#pagination');
        if (!container) return;
        
        if (pageSize === 'all' || filteredTests.length <= pageSize) {
            container.innerHTML = '';
            return;
        }
        
        const totalPages = Math.ceil(filteredTests.length / pageSize);
        const buttons = [];
        
        // Previous button
        buttons.push(`<button class="page-btn" ${currentPage === 1 ? 'disabled' : ''} onclick="changePage(${currentPage - 1})">Previous</button>`);
        
        // Page numbers (show up to 5 pages around current)
        let startPage = Math.max(1, currentPage - 2);
        let endPage = Math.min(totalPages, currentPage + 2);
        
        if (startPage > 1) {
            buttons.push(`<button class="page-btn" onclick="changePage(1)">1</button>`);
            if (startPage > 2) buttons.push('<span>...</span>');
        }
        
        for (let i = startPage; i <= endPage; i++) {
            buttons.push(`<button class="page-btn ${i === currentPage ? 'active' : ''}" onclick="changePage(${i})">${i}</button>`);
        }
        
        if (endPage < totalPages) {
            if (endPage < totalPages - 1) buttons.push('<span>...</span>');
            buttons.push(`<button class="page-btn" onclick="changePage(${totalPages})">${totalPages}</button>`);
        }
        
        // Next button
        buttons.push(`<button class="page-btn" ${currentPage === totalPages ? 'disabled' : ''} onclick="changePage(${currentPage + 1})">Next</button>`);
        
        container.innerHTML = buttons.join('');
    }

    function changePage(page) {
        if (page < 1 || page > Math.ceil(filteredTests.length / pageSize)) return;
        currentPage = page;
        renderTestResults();
        updateResultsInfo();
        renderPagination();
    }

    /* Event Setup */
    function setupEvents() {
        // Navigation
        qsa('.nav-link').forEach(link => {
            link.addEventListener('click', e => {
                e.preventDefault();
                const targetId = link.getAttribute('href').substring(1);
                showSection(targetId);
            });
        });
        
        // Theme toggle
        const themeBtn = qs('#themeToggle');
        if (themeBtn) themeBtn.addEventListener('click', toggleTheme);
        
        // Search and filters
        const searchInput = qs('#searchInput');
        if (searchInput) {
            searchInput.addEventListener('input', debounce(applyFilters, 300));
        }
        
        const statusFilter = qs('#statusFilter');
        if (statusFilter) statusFilter.addEventListener('change', applyFilters);
        
        const suiteFilter = qs('#suiteFilter');
        if (suiteFilter) suiteFilter.addEventListener('change', applyFilters);
        
        const pageSizeSelect = qs('#pageSizeSelect');
        if (pageSizeSelect) {
            pageSizeSelect.addEventListener('change', e => {
                pageSize = e.target.value === 'all' ? 'all' : parseInt(e.target.value);
                currentPage = 1;
                renderTestResults();
                updateResultsInfo();
                renderPagination();
            });
        }
        
        // Modal close
        const modalClose = qs('#modalClose');
        if (modalClose) {
            modalClose.addEventListener('click', () => {
                qs('#screenshotModal')?.classList.remove('show');
            });
        }
        
        // Click outside modal to close
        const modal = qs('#screenshotModal');
        if (modal) {
            modal.addEventListener('click', e => {
                if (e.target === modal) {
                    modal.classList.remove('show');
                }
            });
        }
        
        // Table sorting
        setupTableSorting();
    }

    function setupTableSorting() {
        qsa('#testResultsTable th[data-sort]').forEach(th => {
            th.addEventListener('click', () => {
                const field = th.getAttribute('data-sort');
                if (sortField === field) {
                    sortDirection = sortDirection === 'asc' ? 'desc' : 'asc';
                } else {
                    sortField = field;
                    sortDirection = 'asc';
                }
                sortTests();
                updateSortIndicators();
            });
        });
    }

    function sortTests() {
        filteredTests.sort((a, b) => {
            let aVal = a[sortField];
            let bVal = b[sortField];

            if (sortField === 'durationMs') {
                aVal = a.durationMs || 0;
                bVal = b.durationMs || 0;
            } else {
                aVal = String(aVal || '').toLowerCase();
                bVal = String(bVal || '').toLowerCase();
            }
            
            if (aVal < bVal) return sortDirection === 'asc' ? -1 : 1;
            if (aVal > bVal) return sortDirection === 'asc' ? 1 : -1;
            return 0;
        });
        
        renderTestResults();
    }

    function updateSortIndicators() {
        qsa('#testResultsTable .sort-indicator').forEach(indicator => {
            indicator.textContent = '⇅';
            indicator.style.opacity = '0.6';
        });
        
        const activeIndicator = qs(`#testResultsTable th[data-sort="${sortField}"] .sort-indicator`);
        if (activeIndicator) {
            activeIndicator.textContent = sortDirection === 'asc' ? '↑' : '↓';
            activeIndicator.style.opacity = '1';
        }
    }

    function showSection(sectionId) {
        qsa('.section').forEach(section => section.classList.remove('active'));
        qsa('.nav-link').forEach(link => link.classList.remove('active'));
        
        const targetSection = qs(`#${sectionId}`);
        const targetLink = qs(`a[href="#${sectionId}"]`);
        
        if (targetSection) targetSection.classList.add('active');
        if (targetLink) targetLink.classList.add('active');
    }

    function debounce(func, wait) {
        let timeout;
        return function executedFunction(...args) {
            const later = () => {
                clearTimeout(timeout);
                func(...args);
            };
            clearTimeout(timeout);
            timeout = setTimeout(later, wait);
        };
    }

    // Make functions globally available for onclick handlers
    window.changePage = changePage;
    window.showScreenshotModal = showScreenshotModal;
})();