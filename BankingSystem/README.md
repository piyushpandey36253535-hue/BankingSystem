# 🏛️ Canada Core Banking System

> High-fidelity institutional core banking platform simulating multi-tier accounts, money transfers, double-entry general ledger, real-time fraud detection & AML surveillance, and automated zero-variance ledger reconciliation.

[![.NET 8.0](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![C#](https://img.shields.io/badge/C%23-12.0-239120?logo=csharp)](https://docs.microsoft.com/en-us/dotnet/csharp/)
[![EF Core](https://img.shields.io/badge/Entity%20Framework-Core%208.0-512BD4)](https://docs.microsoft.com/en-us/ef/core/)
[![Database](https://img.shields.io/badge/Database-SQL%20Server%20%7C%20SQLite-CC292B)](https://www.microsoft.com/sql-server)
[![Tests](https://img.shields.io/badge/Tests-8%20Passed-10B981)](./BankingSystem.Tests)

---

## 🌟 Key Features

### 1. 👥 Multi-Tier Account Lifecycle & KYC Verification
- **Account Types**: Personal Checking, High-Yield Savings, Commercial Business, Central Vault Reserve (`VAULT-RESERVE-01`), and Settlement Clearing (`CLEARING-ACH-01`).
- **KYC Verification Workflow**: Simulates government identity verification (Passport, Driver's License, National ID).
- **Dynamic Tier Limits**: Verifying KYC immediately elevates daily transfer limits from **$1,000.00** to **$50,000.00** (Personal) or **$250,000.00** (Commercial).
- **Administrative Account Control**: Real-time 1-click administrative freeze/unfreeze operations.

### 2. ⚖️ Strict Double-Entry General Ledger
- **Financial Grade Invariant**: Every economic transaction creates balanced debits and credits atomically.
  $$\sum \text{Debits} = \sum \text{Credits}$$
- **Accounting Equation**:
  $$\text{Assets (Vault Reserves + Clearing)} = \text{Liabilities (Customer Deposits)} + \text{Equity (Capital Reserves)}$$
- **Immutability & Audit Trail**: Ledger entries are permanent. Reversals are performed via offsetting journal vouchers.
- **Trial Balance Verification**: Instantaneous general ledger trial balance validation endpoint.

### 3. 💸 Money-Transfer Simulation
- **Intra-Bank Transfers**: Instant internal settlement between customer accounts.
- **Inter-Bank Wire / ACH**: Simulated routing to external banking institutions (JPMorgan Chase, Bank of America, Wells Fargo, Citibank, Barclays) via Federal Reserve Fedwire representation.
- **Simulated Capital Faucet**: On-demand liquidity injection from Central Vault reserves into customer accounts for testing.
- **Recurring Scheduled Transfers**: Periodic automated transfer execution via background workers (Daily, Weekly, Monthly).

### 4. 🛡️ Real-Time Fraud Surveillance Radar
- **AML Structuring / Smurfing Rule**: Flags transactions between **$9,000.00 and $9,999.99** designed to evade the $10k Bank Secrecy Act / CTR currency reporting threshold.
- **Velocity Burst Rule**: Flags high-frequency activity ($\ge 3$ outgoing transfers in sub-minute intervals).
- **Historic Outlier Spike**: Flags anomalous transactions $> 4\times$ the account's 10-tx rolling average.
- **KYC Limit Breach**: Prevents unverified accounts from executing high-value transfers.
- **Compliance Action Console**: High-risk transfers are placed on **HOLD** (reducing `AvailableBalance` while preserving `Balance`), generating a compliance alert for **Approval & Release** or **Rejection & Account Freeze**.
- **Interactive Attack Sandbox**: One-click synthetic attack simulator to test fraud surveillance rules in real-time.

### 5. 📑 Statements & Automated Zero-Variance Reconciliation
- **Official Statements**: Itemized chronological bank statements with opening balance, inflows, outflows, ending balance, and printable HTML view.
- **Automated Reconciliation Background Worker**: Periodically audits every account by comparing its row balance against $\sum \text{Credits} - \sum \text{Debits}$ from raw ledger entries, logging zero-variance confirmation.

---

## 🏗️ Technology Stack

- **Framework**: ASP.NET Core 8.0 Web API & SPA
- **Language**: C# 12.0
- **ORM & Data**: Entity Framework Core 8.0
- **Database Support**:
  - **SQL Server**: Native enterprise database provider
  - **SQLite**: Local out-of-the-box development and testing
- **Background Jobs**: `IHostedService` / `BackgroundService` workers:
  - `ScheduledTransferProcessorHostedService`: Evaluates and executes recurring transfers
  - `ReconciliationHostedService`: Runs periodic trial balance and ledger audits
- **Testing**: xUnit with SQLite in-memory isolated database provider

---

## 🚀 Quick Start

### Prerequisites
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

### 1. Clone & Run
```bash
git clone https://github.com/piyush-pandey142/CanadaCoreBanking.git
cd CanadaCoreBanking
dotnet run
```
The application will start and listen on **`http://localhost:5248`** (or default configured port).

### 2. Run Automated Test Suite
```bash
cd BankingSystem.Tests
dotnet test
```

---

## 📊 API Reference

| Endpoint | Method | Description |
| :--- | :---: | :--- |
| `/api/accounts` | `GET` | List all accounts (filter internal accounts) |
| `/api/accounts` | `POST` | Create a new retail or business account |
| `/api/accounts/{id}/verify-kyc` | `POST` | Submit KYC identity verification and elevate limits |
| `/api/accounts/{id}/status` | `PUT` | Freeze or unfreeze an account |
| `/api/transfers` | `POST` | Execute intra-bank or inter-bank money transfer |
| `/api/transfers/faucet` | `POST` | Inject simulated cash into an account |
| `/api/transfers/scheduled` | `GET / POST` | Manage recurring scheduled transfers |
| `/api/ledger/journals` | `GET` | List double-entry journals with debit/credit entries |
| `/api/ledger/journals/{id}/reverse` | `POST` | Post a double-entry reversing transaction |
| `/api/ledger/trial-balance` | `GET` | Verify general ledger trial balance |
| `/api/fraud/alerts` | `GET` | List compliance fraud alerts |
| `/api/fraud/alerts/{id}/review` | `POST` | Approve (release hold) or reject fraud alert |
| `/api/fraud/simulate-attack` | `POST` | Trigger synthetic attack vector test |
| `/api/statements/{accountId}` | `GET` | Generate structured statement JSON |
| `/api/statements/{accountId}/html` | `GET` | View printable official HTML statement |
| `/api/reconciliation/history` | `GET` | View historical reconciliation audit records |
| `/api/reconciliation/run` | `POST` | Execute on-demand general ledger reconciliation |
| `/api/analytics/overview` | `GET` | High-level system KPIs and ledger integrity status |

---

## 🔒 Security & Compliance Note
All money, transactions, and account details in this system are strictly **simulated** for architectural demonstration, financial ledger testing, and compliance surveillance education.

---

## 📄 License
MIT License. Free for educational and commercial exploration.
