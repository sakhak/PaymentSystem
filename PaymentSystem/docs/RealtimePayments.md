# Automatic payment verification

## How the application updates payments

`PaymentVerificationWorker` runs inside the existing ASP.NET Core process. Every two seconds it looks for due, unconfirmed orders created within the past 24 hours. Each bank request receives its own dependency-injection scope and database context. Up to four orders are checked concurrently in batches of 20.

`PaymentService.VerifyAsync` first claims a 30-second lease in SQLite. This prevents a worker, a manual check, and multiple tabs from checking the same order simultaneously. A process restart or cancelled request releases the claim naturally when that lease expires.

For a valid QR with no transaction yet, the next bank attempt is scheduled about five seconds later. Expired QR codes are checked every 30 seconds during the 24-hour reconciliation period. Provider failures, credential problems, and mismatches back off for 60 seconds. Actual timings also depend on provider response time and workload. Older orders remain manually checkable. Do not leave the application stopped or let hosting put it to sleep if you need ongoing reconciliation.

The payment page polls a read-only, authenticated status handler every three seconds. It displays the last bank check, a safe diagnostic message, and the current payment status. When confirmed, it removes the QR and check button, shows success, and inserts the confirmation time and bank reference without a page reload. Polling pauses in hidden tabs and backs off during connection problems. Dashboard and history statuses refresh every five seconds.

The page does not call Bakong directly. No provider token or raw response reaches the browser. GET status requests do not change payment state and are not cached. Manual checks still use POST plus antiforgery protection. All handlers enforce ownership/admin rules. Paid orders stop provider checks.

## Why a payment can remain unconfirmed

| Message | Action |
| --- | --- |
| No transaction found | Check the selected environment, the exact order's QR, and the bank receipt. The worker retries. |
| Credential rejected | Obtain or renew a valid token for the selected Bakong environment. |
| Cannot reach Bakong / timeout | Check server network/DNS and provider availability; automatic retries back off. |
| Receiving account / amount / currency mismatch | Reconcile the receipt with the original order. The app must not confirm a different transfer. |
| Outside payment window | Review the late transfer with the receiving bank; the app does not silently treat a late transfer as an in-window payment. |
| Older order has no account snapshot | Legacy orders are preserved, but cannot be safely auto-confirmed without the original receiving account data. |

Pending means unconfirmed; Expired means the QR payment window has closed. Neither proves that funds did not move. A transaction made before expiry can still be confirmed later, once Bakong returns it. Do not ask a customer to pay again solely because a receipt has not reconciled.

Admin → Payments → Needs attention lists stored verification problems. Open the order for its diagnostic and last-check timestamp. Admin overview shows the actual configured provider host and whether the token is present; presence is not proof of validity.

## Real banking apps versus SIT

Real-bank payments require the production provider, a real receiving account, and a valid production API token. The application defaults to `https://api-bakong.nbc.gov.kh`. SIT is explicitly a test environment. Switching the endpoint does not turn a SIT token into a production token.

Store credentials locally from the solution folder:

```powershell
dotnet user-secrets set "Bakong:BaseUrl" "https://api-bakong.nbc.gov.kh" --project .\PaymentSystem\PaymentSystem.csproj
dotnet user-secrets set "Bakong:Token" "REPLACE_WITH_YOUR_VALID_PRODUCTION_TOKEN" --project .\PaymentSystem\PaymentSystem.csproj
```

Replace the placeholder privately on your own machine. Restart the app after replacing credentials. Never paste the token in chat, screenshots, logs, or source control. Request and renew credentials through the official Bakong onboarding process. [NBC integration guide](https://bakong.nbc.gov.kh/en/download/KHQR/integration/QR%20Payment%20Integration.pdf), [Open API documentation](https://sit-api-bakong.nbc.gov.kh/document).

The older NBC guide also lists `.org.kh` hosts. Use the current endpoint supplied by your Bakong onboarding channel, with its matching credentials; do not disable TLS certificate verification or hardcode DNS/IP workarounds.

## Operator command

To check a specific order with the same service, from the solution folder:

```powershell
dotnet run --project .\PaymentSystem\PaymentSystem.csproj --configuration Release --no-build --launch-profile http -- --verify-order YOUR_ORDER_REFERENCE
```

This command prints a safe result and exits without starting a web listener. It honors the retry lease and updates payment state only after trusted verification.

## Configuration

- `PaymentVerification:Enabled`: defaults to true. False disables automatic worker checks.
- `PaymentVerification:IntervalSeconds`: defaults to 5, constrained to 1–60. Controls the retry interval for active QR codes with no transaction found.

Configure these on the server; the browser cannot change them. Check Bakong's current usage limits before increasing traffic. The retry schedule is designed for this small application, not an unlimited-throughput payment gateway.

## Verification

The existing smoke suite covers ownership, CSRF, prices, credentials, mismatches, duplicate hashes, roles, and lockout. `scripts/realtime_test.py` additionally tests actual background execution, a transition from not found to verified with no manual check, persisted state, live dashboard/history handlers, stopped checks after payment, protected status endpoints, credential diagnostics, and retry leases. It uses a temporary SQLite database and a fake bank server; no real money moves.

Build and run both from the solution directory:

```powershell
dotnet build .\PaymentSystem\PaymentSystem.csproj --configuration Release
python .\PaymentSystem\scripts\smoke_test.py
python .\PaymentSystem\scripts\realtime_test.py
```

Manually verify desktop/mobile appearance and browser updates by keeping a payment page open while a trusted test transaction is completed. Live bank confirmation depends on valid provider credentials and provider availability.
