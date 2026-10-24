// Canada Core Banking - Client Application
let state = {
  accounts: [],
  journals: [],
  alerts: [],
  scheduled: [],
  selectedStatementAccountId: null
};

// Formatting helpers
const formatCurrency = (val) => {
  return new Intl.NumberFormat('en-US', {
    style: 'currency',
    currency: 'USD',
    minimumFractionDigits: 2
  }).format(val || 0);
};

const formatDate = (isoStr) => {
  if (!isoStr) return '-';
  const d = new Date(isoStr);
  return d.toLocaleDateString('en-US', {
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit'
  });
};

// Toast notification helper
function showToast(message, type = 'success') {
  const container = document.getElementById('toastContainer');
  if (!container) return;

  const toast = document.createElement('div');
  toast.className = `toast ${type}`;
  toast.innerHTML = `
    <span>${type === 'success' ? '✓' : type === 'error' ? '✕' : '⚠️'}</span>
    <span>${message}</span>
  `;
  container.appendChild(toast);

  setTimeout(() => {
    toast.style.opacity = '0';
    toast.style.transform = 'translateY(10px)';
    toast.style.transition = 'all 0.3s ease';
    setTimeout(() => toast.remove(), 300);
  }, 4000);
}

// Tab Switching
function switchTab(tabId) {
  document.querySelectorAll('.tab-btn').forEach(btn => btn.classList.remove('active'));
  document.querySelectorAll('.tab-content').forEach(panel => panel.classList.remove('active'));

  const targetPanel = document.getElementById(`tab-${tabId}`);
  if (targetPanel) {
    targetPanel.classList.add('active');
  }

  // Highlight corresponding button
  const buttons = Array.from(document.querySelectorAll('.tab-btn'));
  const match = buttons.find(b => b.getAttribute('onclick')?.includes(`'${tabId}'`));
  if (match) match.classList.add('active');

  // Trigger data refreshes based on tab
  if (tabId === 'accounts') loadAccounts();
  if (tabId === 'ledger') loadLedgerJournals();
  if (tabId === 'fraud') loadFraudAlerts();
  if (tabId === 'statements') loadReconciliationHistory();
  if (tabId === 'transfers') loadScheduledTransfers();
}

// Modal Handlers
function openModal(id) {
  const el = document.getElementById(id);
  if (el) el.classList.add('active');
}

function closeModal(id) {
  const el = document.getElementById(id);
  if (el) el.classList.remove('active');
}

// Initial Data Loading
async function initApp() {
  await Promise.all([
    fetchAnalytics(),
    loadAccounts(),
    loadLedgerJournals(),
    loadFraudAlerts(),
    loadScheduledTransfers(),
    loadReconciliationHistory()
  ]);

  // Set default dates for statements
  const today = new Date().toISOString().split('T')[0];
  const thirtyDaysAgo = new Date(Date.now() - 30 * 24 * 60 * 60 * 1000).toISOString().split('T')[0];
  const startInput = document.getElementById('statementStartDate');
  const endInput = document.getElementById('statementEndDate');
  if (startInput) startInput.value = thirtyDaysAgo;
  if (endInput) endInput.value = today;

  // Poll analytics and alerts every 10 seconds
  setInterval(async () => {
    await fetchAnalytics();
    await loadFraudAlerts(true);
  }, 10000);
}

// 1. Fetch Analytics & KPIs
async function fetchAnalytics() {
  try {
    const res = await fetch('/api/analytics/overview');
    if (!res.ok) return;
    const data = await res.json();

    document.getElementById('kpiDeposits').textContent = formatCurrency(data.totalDepositsAcrossAccounts);
    document.getElementById('kpiVault').textContent = formatCurrency(data.vaultReserveAssetBalance);
    document.getElementById('kpiVolume').textContent = formatCurrency(data.total24HourTransferVolume);
    document.getElementById('kpiTxnCount').textContent = `${data.totalCompletedTransactions} transactions`;

    document.getElementById('kpiAlerts').textContent = data.openFraudAlertsCount;
    document.getElementById('kpiAlertsSubtitle').textContent = `${data.openFraudAlertsCount} requiring compliance action`;
    document.getElementById('tabAlertsCount').textContent = data.openFraudAlertsCount;

    const reconStatusEl = document.getElementById('kpiReconStatus');
    reconStatusEl.textContent = data.isLedgerCurrentlyBalanced ? 'PASSED' : 'DISCREPANCY';
    reconStatusEl.style.color = data.isLedgerCurrentlyBalanced ? 'var(--color-emerald)' : 'var(--color-rose)';

    const reconTimeEl = document.getElementById('kpiReconTime');
    reconTimeEl.textContent = `Audited ${formatDate(data.lastReconciledAt)}`;

    const headerStatus = document.getElementById('headerTrialBalanceStatus');
    if (data.isLedgerCurrentlyBalanced) {
      headerStatus.textContent = 'LEDGER BALANCED (0.00 VAR)';
      headerStatus.style.color = 'var(--color-emerald)';
    } else {
      headerStatus.textContent = 'LEDGER VARIANCE DETECTED';
      headerStatus.style.color = 'var(--color-rose)';
    }

    // Trial balance check
    const tbRes = await fetch('/api/ledger/trial-balance');
    if (tbRes.ok) {
      const tb = await tbRes.json();
      document.getElementById('overviewTotalDebits').textContent = formatCurrency(tb.totalDebits);
      document.getElementById('overviewTotalCredits').textContent = formatCurrency(tb.totalCredits);
      document.getElementById('overviewVariance').textContent = formatCurrency(tb.variance);
    }
  } catch (err) {
    console.error('Failed to fetch analytics:', err);
  }
}

// 2. Load Accounts
async function loadAccounts() {
  try {
    const res = await fetch('/api/accounts?includeInternal=false');
    if (!res.ok) return;
    state.accounts = await res.json();

    renderAccountsTable();
    populateAccountSelects();
  } catch (err) {
    console.error('Failed to load accounts:', err);
  }
}

function renderAccountsTable() {
  const tbody = document.getElementById('accountsTableBody');
  if (!tbody) return;

  if (state.accounts.length === 0) {
    tbody.innerHTML = '<tr><td colspan="9" style="text-align:center; padding: 20px;">No accounts found.</td></tr>';
    return;
  }

  tbody.innerHTML = state.accounts.map(a => {
    const kycClass = a.kycStatus === 'Verified' ? 'badge-verified' : a.kycStatus === 'Pending' ? 'badge-pending' : 'badge-unverified';
    const statusClass = a.status === 'Active' ? 'badge-active' : a.status === 'Frozen' ? 'badge-frozen' : 'badge-closed';

    return `
      <tr>
        <td><span class="code-ref">${a.accountNumber}</span></td>
        <td>
          <div style="font-weight: 600;">${a.accountHolderName}</div>
          <div style="font-size: 11px; color: var(--text-muted);">${a.email}</div>
        </td>
        <td><span style="font-size: 12px; font-weight: 500;">${a.accountType}</span></td>
        <td>
          <span class="badge ${kycClass}">${a.kycStatus}</span>
          ${a.kycStatus !== 'Verified' ? `
            <button class="btn btn-outline btn-sm" style="margin-left: 6px; padding: 2px 6px; font-size: 10px;" onclick="openKycModal(${a.id}, '${a.accountNumber}', '${a.accountHolderName}')">
              Verify
            </button>
          ` : ''}
        </td>
        <td style="text-align: right; font-weight: 700; font-family: var(--font-mono);">${formatCurrency(a.balance)}</td>
        <td style="text-align: right; font-weight: 600; font-family: var(--font-mono); color: var(--color-emerald);">${formatCurrency(a.availableBalance)}</td>
        <td style="text-align: right; font-family: var(--font-mono); font-size: 12px;">${formatCurrency(a.dailyTransferLimit)}</td>
        <td><span class="badge ${statusClass}">${a.status}</span></td>
        <td style="text-align: center;">
          <div style="display: flex; gap: 6px; justify-content: center;">
            <button class="btn btn-outline btn-sm" onclick="toggleAccountStatus(${a.id}, '${a.status}')">
              ${a.status === 'Active' ? '❄️ Freeze' : '⚡ Unfreeze'}
            </button>
            <button class="btn btn-outline btn-sm" onclick="viewStatementForAccount(${a.id})">
              📑 Statement
            </button>
          </div>
        </td>
      </tr>
    `;
  }).join('');
}

function populateAccountSelects() {
  const selects = [
    'expressSourceAccount',
    'expressDestAccount',
    'transferSourceAccount',
    'transferDestAccount',
    'faucetTargetAccount',
    'statementAccountSelect',
    'schedSourceAccount',
    'schedDestAccount',
    'modalFaucetAccount'
  ];

  selects.forEach(selectId => {
    const el = document.getElementById(selectId);
    if (!el) return;

    const currentVal = el.value;
    const defaultText = el.options[0]?.text || 'Select account...';

    let optionsHtml = `<option value="">${defaultText}</option>`;
    state.accounts.forEach(a => {
      optionsHtml += `<option value="${a.id}">${a.accountHolderName} (${a.accountNumber}) - ${formatCurrency(a.availableBalance)}</option>`;
    });

    el.innerHTML = optionsHtml;
    if (currentVal) el.value = currentVal;
  });
}

function updateExpressBalance() {
  const sourceId = document.getElementById('expressSourceAccount')?.value;
  const helpEl = document.getElementById('expressSourceBalanceHelp');
  if (!helpEl) return;

  const acc = state.accounts.find(a => a.id == sourceId);
  if (acc) {
    helpEl.textContent = `Available: ${formatCurrency(acc.availableBalance)} • Daily Limit: ${formatCurrency(acc.dailyTransferLimit)}`;
  } else {
    helpEl.textContent = 'Available: $0.00';
  }
}

function updateTransferBalance() {
  const sourceId = document.getElementById('transferSourceAccount')?.value;
  const displayEl = document.getElementById('transferSourceBalanceDisplay');
  if (!displayEl) return;

  const acc = state.accounts.find(a => a.id == sourceId);
  if (acc) {
    displayEl.textContent = `Available: ${formatCurrency(acc.availableBalance)} • Single Limit: ${formatCurrency(acc.singleTransferLimit)} • Daily Limit: ${formatCurrency(acc.dailyTransferLimit)} (${acc.kycStatus})`;
  } else {
    displayEl.textContent = 'Available: $0.00 • Single Limit: $0.00 • Daily Limit: $0.00';
  }
}

function toggleTransferRail() {
  const rail = document.getElementById('transferRail')?.value;
  const destGroup = document.getElementById('destAccountGroup');
  const interFields = document.getElementById('interBankFields');

  if (rail === 'inter') {
    destGroup.style.display = 'none';
    interFields.style.display = 'block';
  } else {
    destGroup.style.display = 'block';
    interFields.style.display = 'none';
  }
}

// 3. Load Ledger Journals
async function loadLedgerJournals() {
  try {
    const res = await fetch('/api/ledger/journals?pageSize=40');
    if (!res.ok) return;
    state.journals = await res.json();

    renderOverviewJournals();
    renderGeneralLedger();
  } catch (err) {
    console.error('Failed to load journals:', err);
  }
}

function renderOverviewJournals() {
  const tbody = document.getElementById('overviewJournalsTableBody');
  if (!tbody) return;

  if (state.journals.length === 0) {
    tbody.innerHTML = '<tr><td colspan="8" style="text-align:center; padding: 20px;">No journals recorded yet.</td></tr>';
    return;
  }

  tbody.innerHTML = state.journals.slice(0, 8).map(j => {
    const statusClass = j.status === 'Posted' ? 'badge-posted' : j.status === 'Held' ? 'badge-held' : 'badge-reversed';
    return `
      <tr>
        <td><span class="code-ref">${j.journalNumber}</span></td>
        <td><code>${j.referenceNumber}</code></td>
        <td><span style="font-weight: 500;">${j.transactionType}</span></td>
        <td>${j.sourceHolderName ? `${j.sourceHolderName}` : '<span style="color:var(--text-muted)">Central Vault</span>'}</td>
        <td>${j.destinationHolderName ? `${j.destinationHolderName}` : (j.externalInstitutionName || '<span style="color:var(--text-muted)">Central Vault</span>')}</td>
        <td style="text-align: right; font-weight: 700; font-family: var(--font-mono);">${formatCurrency(j.amount)}</td>
        <td><span class="badge ${statusClass}">${j.status}</span></td>
        <td style="color: var(--text-muted); font-size: 12px;">${formatDate(j.timestamp)}</td>
      </tr>
    `;
  }).join('');
}

function renderGeneralLedger() {
  const tbody = document.getElementById('ledgerTableBody');
  if (!tbody) return;

  if (state.journals.length === 0) {
    tbody.innerHTML = '<tr><td colspan="9" style="text-align:center; padding: 20px;">No general ledger vouchers found.</td></tr>';
    return;
  }

  tbody.innerHTML = state.journals.map(j => {
    const statusClass = j.status === 'Posted' ? 'badge-posted' : j.status === 'Held' ? 'badge-held' : 'badge-reversed';
    
    // Build balanced double-entry rows preview
    const entriesHtml = j.entries && j.entries.length > 0 ? `
      <div class="journal-split-box">
        ${j.entries.map(e => `
          <div class="journal-row">
            <span>
              <span class="badge ${e.entryType === 'Debit' ? 'badge-debit' : 'badge-credit'}">${e.entryType}</span>
              <strong style="margin-left: 6px;">${e.accountNumber}</strong> (${e.accountHolderName})
            </span>
            <span style="font-family: var(--font-mono); font-weight: 600;">
              ${e.entryType === 'Debit' ? 'DR' : 'CR'} ${formatCurrency(e.amount)}
            </span>
          </div>
        `).join('')}
      </div>
    ` : '<span style="color:var(--text-muted); font-size: 11px;">Hold pending compliance release</span>';

    return `
      <tr>
        <td><span class="code-ref">${j.journalNumber}</span></td>
        <td><code>${j.referenceNumber}</code></td>
        <td><span style="font-weight: 600;">${j.transactionType}</span></td>
        <td style="max-width: 220px; font-size: 12px; color: var(--text-secondary);">${j.description}</td>
        <td style="text-align: right; font-weight: 700; font-family: var(--font-mono);">${formatCurrency(j.amount)}</td>
        <td>${entriesHtml}</td>
        <td><span class="badge ${statusClass}">${j.status}</span></td>
        <td style="font-size: 12px; color: var(--text-muted);">${formatDate(j.timestamp)}</td>
        <td style="text-align: center;">
          ${j.status === 'Posted' && j.transactionType !== 'Reversal' ? `
            <button class="btn btn-outline btn-sm" onclick="openReversalModal(${j.id}, '${j.journalNumber}')">
              ↩ Reverse
            </button>
          ` : '-'}
        </td>
      </tr>
    `;
  }).join('');
}

// 4. Load Fraud Alerts
async function loadFraudAlerts(silent = false) {
  try {
    const res = await fetch('/api/fraud/alerts');
    if (!res.ok) return;
    state.alerts = await res.json();

    const openCount = state.alerts.filter(a => a.status === 'Open' || a.status === 'UnderReview').length;
    document.getElementById('tabAlertsCount').textContent = openCount;

    renderFraudAlerts();
  } catch (err) {
    if (!silent) console.error('Failed to load fraud alerts:', err);
  }
}

function renderFraudAlerts() {
  const tbody = document.getElementById('fraudAlertsTableBody');
  if (!tbody) return;

  if (state.alerts.length === 0) {
    tbody.innerHTML = '<tr><td colspan="7" style="text-align:center; padding: 20px;">No fraud alerts detected. Surveillance radar clear.</td></tr>';
    return;
  }

  tbody.innerHTML = state.alerts.map(a => {
    const scoreColor = a.riskScore >= 75 ? 'var(--color-rose)' : a.riskScore >= 45 ? 'var(--color-amber)' : 'var(--color-emerald)';
    const statusClass = a.status === 'Approved' ? 'badge-posted' : a.status === 'Rejected' ? 'badge-closed' : 'badge-held';

    return `
      <tr>
        <td><span class="code-ref">#ALT-${a.id}</span></td>
        <td>
          <div style="font-weight: 600;">${a.accountHolderName}</div>
          <div style="font-size: 11px; color: var(--text-muted);">${a.accountNumber}</div>
        </td>
        <td style="font-family: var(--font-mono); font-weight: 700;">${formatCurrency(a.flaggedAmount)}</td>
        <td>
          <div style="font-family: var(--font-mono); font-weight: 800; color: ${scoreColor};">
            ${a.riskScore} / 100
          </div>
          <div class="risk-meter">
            <div class="risk-meter-fill ${a.riskScore >= 75 ? 'risk-high' : a.riskScore >= 45 ? 'risk-med' : 'risk-low'}" style="width: ${a.riskScore}%;"></div>
          </div>
        </td>
        <td style="max-width: 220px;">
          <div style="font-size: 11px; font-weight: 600; color: ${scoreColor};">${a.triggeredRules}</div>
          <div style="font-size: 11px; color: var(--text-muted); white-space: nowrap; overflow: hidden; text-overflow: ellipsis;">${a.details}</div>
        </td>
        <td><span class="badge ${statusClass}">${a.status}</span></td>
        <td>
          ${a.status === 'Open' || a.status === 'UnderReview' ? `
            <button class="btn btn-primary btn-sm" onclick="openFraudReviewModal(${a.id}, ${a.riskScore}, '${a.severity}', '${escape(a.details)}')">
              Review
            </button>
          ` : `
            <span style="font-size: 11px; color: var(--text-muted);">${a.status} by ${a.reviewedBy || 'Officer'}</span>
          `}
        </td>
      </tr>
    `;
  }).join('');
}

// 5. Scheduled Transfers
async function loadScheduledTransfers() {
  try {
    const res = await fetch('/api/transfers/scheduled');
    if (!res.ok) return;
    state.scheduled = await res.json();

    const tbody = document.getElementById('scheduledTableBody');
    if (!tbody) return;

    if (state.scheduled.length === 0) {
      tbody.innerHTML = '<tr><td colspan="6" style="text-align:center; padding: 16px;">No recurring schedules configured.</td></tr>';
      return;
    }

    tbody.innerHTML = state.scheduled.map(s => `
      <tr>
        <td>${s.sourceAccount?.accountHolderName || `Account #${s.sourceAccountId}`}</td>
        <td>${s.destinationAccount?.accountHolderName || `Account #${s.destinationAccountId}`}</td>
        <td style="font-weight: 700; font-family: var(--font-mono);">${formatCurrency(s.amount)}</td>
        <td><span class="badge badge-active">${s.frequency}</span></td>
        <td style="font-size: 12px; color: var(--text-muted);">${formatDate(s.nextExecutionDate)}</td>
        <td>
          <button class="btn btn-outline btn-sm" onclick="cancelScheduled(${s.id})">Cancel</button>
        </td>
      </tr>
    `).join('');
  } catch (err) {
    console.error('Failed to load scheduled transfers:', err);
  }
}

async function cancelScheduled(id) {
  try {
    const res = await fetch(`/api/transfers/scheduled/${id}`, { method: 'DELETE' });
    if (res.ok) {
      showToast('Scheduled transfer cancelled.');
      await loadScheduledTransfers();
    }
  } catch (err) {
    showToast('Failed to cancel scheduled transfer', 'error');
  }
}

// 6. Reconciliation
async function loadReconciliationHistory() {
  try {
    const res = await fetch('/api/reconciliation/history');
    if (!res.ok) return;
    const history = await res.json();

    const tbody = document.getElementById('reconciliationTableBody');
    if (!tbody) return;

    if (history.length === 0) {
      tbody.innerHTML = '<tr><td colspan="6" style="text-align:center; padding: 16px;">No audits recorded.</td></tr>';
      return;
    }

    tbody.innerHTML = history.map(r => `
      <tr>
        <td><span class="code-ref">#AUD-${r.id}</span></td>
        <td style="font-size: 12px;">${formatDate(r.auditTimestamp)}</td>
        <td>${r.totalAccountsAudited} accounts</td>
        <td style="font-family: var(--font-mono); font-size: 12px;">
          DR ${formatCurrency(r.totalLedgerDebits)} = CR ${formatCurrency(r.totalLedgerCredits)}
        </td>
        <td style="font-weight: 600; color: ${r.discrepancyCount === 0 ? 'var(--color-emerald)' : 'var(--color-rose)'};">
          ${r.discrepancyCount === 0 ? '0 Discrepancies' : `${r.discrepancyCount} Found (${formatCurrency(r.totalDiscrepancyAmount)})`}
        </td>
        <td>
          <span class="badge ${r.status === 'Passed' ? 'badge-verified' : 'badge-closed'}">${r.status}</span>
        </td>
      </tr>
    `).join('');
  } catch (err) {
    console.error('Failed to load reconciliation history:', err);
  }
}

async function runManualReconciliation() {
  try {
    showToast('Executing automated general ledger audit...', 'warning');
    const res = await fetch('/api/reconciliation/run', { method: 'POST' });
    const data = await res.json();

    if (res.ok) {
      showToast(`Reconciliation ${data.status}: ${data.summaryMessage}`, data.status === 'Passed' ? 'success' : 'error');
      await Promise.all([
        fetchAnalytics(),
        loadReconciliationHistory()
      ]);
    } else {
      showToast('Reconciliation run failed', 'error');
    }
  } catch (err) {
    showToast('Network error running reconciliation', 'error');
  }
}

// 7. Transfer Execution Handlers
async function handleExpressTransfer(e) {
  e.preventDefault();
  const sourceId = document.getElementById('expressSourceAccount').value;
  const destId = document.getElementById('expressDestAccount').value;
  const amount = parseFloat(document.getElementById('expressAmount').value);
  const memo = document.getElementById('expressMemo').value;

  if (sourceId === destId) {
    showToast('Source and destination accounts must be different.', 'error');
    return;
  }

  await executeTransfer({
    sourceAccountId: parseInt(sourceId),
    destinationAccountId: parseInt(destId),
    amount: amount,
    description: memo,
    isInterBank: false
  });
}

async function handleTransferSubmit(e) {
  e.preventDefault();
  const rail = document.getElementById('transferRail').value;
  const sourceId = document.getElementById('transferSourceAccount').value;
  const amount = parseFloat(document.getElementById('transferAmount').value);
  const description = document.getElementById('transferDescription').value;

  const payload = {
    sourceAccountId: parseInt(sourceId),
    amount: amount,
    description: description,
    isInterBank: rail === 'inter'
  };

  if (rail === 'inter') {
    payload.externalInstitutionName = document.getElementById('interBankName').value;
    payload.externalAccountNumber = document.getElementById('interAccountNumber').value || '984029104829';
    payload.externalRoutingOrSwift = '021000021';
  } else {
    const destId = document.getElementById('transferDestAccount').value;
    if (!destId) {
      showToast('Please select destination recipient account.', 'error');
      return;
    }
    if (sourceId === destId) {
      showToast('Source and destination accounts must be different.', 'error');
      return;
    }
    payload.destinationAccountId = parseInt(destId);
  }

  await executeTransfer(payload);
}

async function executeTransfer(payload) {
  try {
    const res = await fetch('/api/transfers', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload)
    });

    const data = await res.json();

    if (res.status === 202 || data.heldForFraudReview) {
      showToast(`⚠️ Transfer HELD for fraud review! Risk score: ${data.riskScore}/100`, 'warning');
      switchTab('fraud');
    } else if (res.ok) {
      showToast(`✓ Transfer completed successfully! Ref: ${data.referenceNumber}`);
      document.getElementById('expressAmount').value = '';
      document.getElementById('transferAmount').value = '';
    } else {
      showToast(data.message || 'Transfer failed validation.', 'error');
    }

    await Promise.all([
      fetchAnalytics(),
      loadAccounts(),
      loadLedgerJournals(),
      loadFraudAlerts()
    ]);
  } catch (err) {
    showToast('Network error during transfer execution.', 'error');
  }
}

// 8. Faucet Funding
function setFaucetAmount(amount) {
  const el = document.getElementById('faucetAmount');
  if (el) el.value = amount;
}

function openFaucetModal() {
  openModal('faucetModal');
}

async function handleFaucetSubmit(e) {
  e.preventDefault();
  const accountId = document.getElementById('faucetTargetAccount').value;
  const amount = parseFloat(document.getElementById('faucetAmount').value);

  await executeFaucetDeposit(parseInt(accountId), amount);
}

async function handleModalFaucetSubmit(e) {
  e.preventDefault();
  const accountId = document.getElementById('modalFaucetAccount').value;
  const amount = parseFloat(document.getElementById('modalFaucetAmount').value);

  await executeFaucetDeposit(parseInt(accountId), amount);
  closeModal('faucetModal');
}

async function executeFaucetDeposit(accountId, amount) {
  try {
    const res = await fetch('/api/transfers/faucet', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ accountId, amount })
    });

    const data = await res.json();
    if (res.ok) {
      showToast(`Funded ${formatCurrency(amount)} into account.`);
      await Promise.all([
        fetchAnalytics(),
        loadAccounts(),
        loadLedgerJournals()
      ]);
    } else {
      showToast(data.message || 'Faucet funding failed', 'error');
    }
  } catch (err) {
    showToast('Failed to fund account', 'error');
  }
}

// 9. Account Creation
function openCreateAccountModal() {
  openModal('createAccountModal');
}

async function handleCreateAccountSubmit(e) {
  e.preventDefault();
  const holder = document.getElementById('newAccountHolder').value;
  const email = document.getElementById('newAccountEmail').value;
  const phone = document.getElementById('newAccountPhone').value;
  const type = document.getElementById('newAccountType').value;
  const deposit = parseFloat(document.getElementById('newAccountInitialDeposit').value) || 0;

  try {
    const res = await fetch('/api/accounts', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        accountHolderName: holder,
        email: email,
        phone: phone,
        accountType: type,
        initialDeposit: deposit
      })
    });

    const data = await res.json();
    if (res.ok) {
      showToast(`Account created: ${data.accountNumber}`);
      closeModal('createAccountModal');
      document.getElementById('createAccountForm').reset();
      await Promise.all([
        fetchAnalytics(),
        loadAccounts(),
        loadLedgerJournals()
      ]);
    } else {
      showToast(data.message || 'Failed to create account.', 'error');
    }
  } catch (err) {
    showToast('Error creating account.', 'error');
  }
}

// 10. KYC Verification
function openKycModal(id, num, holder) {
  document.getElementById('kycAccountId').value = id;
  document.getElementById('kycAccountDisplay').value = `${holder} (${num})`;
  openModal('kycModal');
}

async function handleKycSubmit(e) {
  e.preventDefault();
  const accountId = document.getElementById('kycAccountId').value;
  const docType = document.getElementById('kycDocType').value;
  const docNumber = document.getElementById('kycDocNumber').value;

  try {
    const res = await fetch(`/api/accounts/${accountId}/verify-kyc`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        documentType: docType,
        documentNumber: docNumber,
        autoApprove: true
      })
    });

    const data = await res.json();
    if (res.ok) {
      showToast(`Account ${data.accountNumber} KYC Verified! Daily limit upgraded to ${formatCurrency(data.dailyTransferLimit)}.`);
      closeModal('kycModal');
      await Promise.all([
        fetchAnalytics(),
        loadAccounts()
      ]);
    } else {
      showToast(data.message || 'KYC verification failed', 'error');
    }
  } catch (err) {
    showToast('Failed to submit verification', 'error');
  }
}

// 11. Freeze / Unfreeze Account
async function toggleAccountStatus(accountId, currentStatus) {
  const newStatus = currentStatus === 'Active' ? 'Frozen' : 'Active';
  try {
    const res = await fetch(`/api/accounts/${accountId}/status`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        status: newStatus,
        reason: newStatus === 'Frozen' ? 'Compliance administrative lock' : 'Compliance lock released'
      })
    });

    const data = await res.json();
    if (res.ok) {
      showToast(`Account ${data.accountNumber} is now ${data.status}.`);
      await loadAccounts();
    } else {
      showToast(data.message || 'Status update failed', 'error');
    }
  } catch (err) {
    showToast('Failed to update account status', 'error');
  }
}

// 12. Fraud Review
function openFraudReviewModal(alertId, score, severity, detailsEncoded) {
  document.getElementById('reviewAlertId').value = alertId;
  document.getElementById('reviewRiskScoreDisplay').textContent = `${score} / 100`;
  document.getElementById('reviewSeverityBadge').textContent = severity.toUpperCase();
  document.getElementById('reviewDetailsBox').textContent = unescape(detailsEncoded);
  openModal('fraudReviewModal');
}

function toggleFreezeOption() {
  const decision = document.getElementById('reviewDecision').value;
  const group = document.getElementById('freezeOptionGroup');
  group.style.display = decision === 'Rejected' ? 'block' : 'none';
}

async function handleFraudReviewSubmit(e) {
  e.preventDefault();
  const alertId = document.getElementById('reviewAlertId').value;
  const decision = document.getElementById('reviewDecision').value;
  const notes = document.getElementById('reviewNotes').value || 'Compliance Officer standard review';
  const freeze = document.getElementById('reviewFreezeAccount')?.checked || false;

  try {
    const res = await fetch(`/api/fraud/alerts/${alertId}/review`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        decision: decision,
        reviewNotes: notes,
        reviewedBy: 'Compliance Officer (Sarah Jenkins)',
        freezeAccountIfRejected: freeze
      })
    });

    const data = await res.json();
    if (res.ok) {
      showToast(`Alert #${alertId} resolved as ${decision}.`);
      closeModal('fraudReviewModal');
      await Promise.all([
        fetchAnalytics(),
        loadAccounts(),
        loadLedgerJournals(),
        loadFraudAlerts()
      ]);
    } else {
      showToast(data.message || 'Failed to review alert', 'error');
    }
  } catch (err) {
    showToast('Error reviewing alert', 'error');
  }
}

// 13. Simulated Attacks
async function triggerSimulatedAttack(scenarioType) {
  if (state.accounts.length === 0) {
    showToast('No accounts available for simulation.', 'error');
    return;
  }

  // Target customer4 (David Miller) or first account
  const target = state.accounts.find(a => a.accountHolderName.includes('David')) || state.accounts[0];

  try {
    showToast(`Injecting synthetic ${scenarioType} attack vector...`, 'warning');
    const res = await fetch('/api/fraud/simulate-attack', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        accountId: target.id,
        scenarioType: scenarioType
      })
    });

    const data = await res.json();
    if (res.ok) {
      showToast(`🚨 Fraud Radar Triggered! Generated ${data.alertsGenerated} alert(s).`, 'error');
      await Promise.all([
        fetchAnalytics(),
        loadFraudAlerts()
      ]);
    } else {
      showToast(data.message || 'Attack simulation failed', 'error');
    }
  } catch (err) {
    showToast('Failed to trigger attack test', 'error');
  }
}

// 14. Reversal Journals
function openReversalModal(journalId, journalNumber) {
  document.getElementById('reversalJournalId').value = journalId;
  document.getElementById('reversalJournalDisplay').value = `Journal Voucher ${journalNumber}`;
  openModal('reversalModal');
}

async function handleReversalSubmit(e) {
  e.preventDefault();
  const id = document.getElementById('reversalJournalId').value;
  const reason = document.getElementById('reversalReason').value;

  try {
    const res = await fetch(`/api/ledger/journals/${id}/reverse`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        reason: reason,
        reversedBy: 'Senior Accounting Auditor'
      })
    });

    const data = await res.json();
    if (res.ok) {
      showToast(`Journal reversed! Posted Reversal Voucher ${data.reversalJournalNumber}`);
      closeModal('reversalModal');
      document.getElementById('reversalReason').value = '';
      await Promise.all([
        fetchAnalytics(),
        loadAccounts(),
        loadLedgerJournals()
      ]);
    } else {
      showToast(data.message || 'Reversal failed', 'error');
    }
  } catch (err) {
    showToast('Failed to post reversal', 'error');
  }
}

// 15. Statements
function viewStatementForAccount(accountId) {
  state.selectedStatementAccountId = accountId;
  switchTab('statements');
  const select = document.getElementById('statementAccountSelect');
  if (select) select.value = accountId;
  handleGenerateStatement(new Event('submit'));
}

async function handleGenerateStatement(e) {
  if (e) e.preventDefault();
  const accountId = document.getElementById('statementAccountSelect').value;
  if (!accountId) {
    showToast('Please select an account for statement generation', 'error');
    return;
  }
  state.selectedStatementAccountId = accountId;

  const start = document.getElementById('statementStartDate').value;
  const end = document.getElementById('statementEndDate').value;

  let url = `/api/statements/${accountId}`;
  const params = [];
  if (start) params.push(`startDate=${encodeURIComponent(start)}`);
  if (end) params.push(`endDate=${encodeURIComponent(end)}`);
  if (params.length > 0) url += `?${params.join('&')}`;

  try {
    const res = await fetch(url);
    if (!res.ok) return;
    const stmt = await res.json();

    document.getElementById('statementPreviewArea').style.display = 'block';
    document.getElementById('stmtHolder').textContent = stmt.accountHolderName;
    document.getElementById('stmtMeta').textContent = `${stmt.accountNumber} • ${stmt.accountType} • Routing: 021000021`;

    document.getElementById('stmtEndingBalance').textContent = formatCurrency(stmt.endingBalance);
    document.getElementById('stmtOpeningBalance').textContent = formatCurrency(stmt.startingBalance);
    document.getElementById('stmtTotalDeposits').textContent = `+${formatCurrency(stmt.totalDeposits)}`;
    document.getElementById('stmtTotalWithdrawals').textContent = `-${formatCurrency(stmt.totalWithdrawals)}`;

    const linesBody = document.getElementById('stmtLinesTableBody');
    if (stmt.transactions.length === 0) {
      linesBody.innerHTML = '<tr><td colspan="6" style="text-align:center; padding: 16px;">No transactions recorded in this period.</td></tr>';
    } else {
      linesBody.innerHTML = stmt.transactions.map(t => {
        const isCredit = t.direction.includes('Inflow');
        return `
          <tr>
            <td style="font-size: 11px;">${formatDate(t.date)}</td>
            <td><code>${t.referenceNumber}</code></td>
            <td style="font-size: 12px;">${t.description}</td>
            <td><span class="badge ${isCredit ? 'badge-credit' : 'badge-debit'}">${t.direction}</span></td>
            <td style="text-align: right; font-family: var(--font-mono); font-weight: 600; color: ${isCredit ? 'var(--color-emerald)' : 'var(--color-rose)'};">
              ${isCredit ? '+' : '-'}${formatCurrency(t.amount)}
            </td>
            <td style="text-align: right; font-family: var(--font-mono); font-weight: 700;">
              ${formatCurrency(t.balanceAfter)}
            </td>
          </tr>
        `;
      }).join('');
    }
  } catch (err) {
    showToast('Failed to generate statement preview', 'error');
  }
}

function openPrintableStatement() {
  const accountId = state.selectedStatementAccountId || document.getElementById('statementAccountSelect').value;
  if (!accountId) {
    showToast('Please select an account first', 'error');
    return;
  }
  const start = document.getElementById('statementStartDate').value;
  const end = document.getElementById('statementEndDate').value;

  let url = `/api/statements/${accountId}/html`;
  const params = [];
  if (start) params.push(`startDate=${encodeURIComponent(start)}`);
  if (end) params.push(`endDate=${encodeURIComponent(end)}`);
  if (params.length > 0) url += `?${params.join('&')}`;

  window.open(url, '_blank');
}

// 16. Scheduled Modal Handlers
function openScheduledTransferModal() {
  openModal('scheduledModal');
}

async function handleCreateScheduledSubmit(e) {
  e.preventDefault();
  const source = document.getElementById('schedSourceAccount').value;
  const dest = document.getElementById('schedDestAccount').value;
  const amount = parseFloat(document.getElementById('schedAmount').value);
  const freq = document.getElementById('schedFrequency').value;
  const desc = document.getElementById('schedDescription').value;

  if (source === dest) {
    showToast('Source and destination cannot be identical.', 'error');
    return;
  }

  try {
    const res = await fetch('/api/transfers/scheduled', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        sourceAccountId: parseInt(source),
        destinationAccountId: parseInt(dest),
        amount: amount,
        frequency: freq,
        description: desc,
        startDate: new Date().toISOString()
      })
    });

    if (res.ok) {
      showToast('Recurring transfer scheduled successfully.');
      closeModal('scheduledModal');
      await loadScheduledTransfers();
    } else {
      showToast('Failed to schedule transfer', 'error');
    }
  } catch (err) {
    showToast('Error scheduling transfer', 'error');
  }
}

// Initialize on DOM Ready
document.addEventListener('DOMContentLoaded', initApp);
