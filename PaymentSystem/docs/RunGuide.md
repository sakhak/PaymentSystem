# PaymentSystem — run and use the application

## Start in Visual Studio

1. Stop the running application with Shift+F5.
2. Open the existing `PaymentSystem.slnx`, select PaymentSystem as the startup project, and choose the **http** launch profile.
3. Press Ctrl+F5. Open `http://localhost:5291/` if the browser does not open automatically.
4. On first use you are redirected to **Create your administrator**. Enter your name, email, and your own strong password. There is no default password.
5. Use **Admin → Manage products → Add product** to create products, then open **Products** to see the catalog.

If you see the old page, restart the app and press Ctrl+F5 in your browser. Do not leave a second instance using port 5291. HTTPS is available on port 7286 after trusting your development certificate; HTTP is for local Development only.

From the solution directory you can also run:

```powershell
dotnet restore .\PaymentSystem\PaymentSystem.csproj
dotnet run --project .\PaymentSystem\PaymentSystem.csproj --launch-profile http
```

Development startup applies the included EF Core migrations and initializes Admin/User roles. The existing local database is retained. Old payments have no account owner and are visible only to administrators. They are not assigned to arbitrary new users. Their original payment data is preserved.

## Pages

| Address | Purpose |
| --- | --- |
| `/` | Dashboard with real payment counts, recent transactions, confirmed totals by currency |
| `/Account/Setup` | One-time local administrator setup; Development and loopback only |
| `/Account/Login` | Email/password sign-in with failed-attempt lockout |
| `/Account/Register` | Create a regular user account |
| `/Account/Manage` | Edit name and change password |
| `/Products` | Search and browse active products |
| `/Payments/Create` | Create a custom payment |
| `/Payments/Details/{id}` | Scannable KHQR, countdown, transaction details, verification |
| `/Payments` | Search, filter, and page through payment history |
| `/Admin` | Administration overview and configuration readiness |
| `/Admin/Products` | Add, edit, publish, and archive products |
| `/Admin/Users` | Search users, change roles, activate/deactivate accounts |

Regular users see their own payments. Administrators see all payments. Registration always creates the User role. Account deactivation and role changes invalidate existing sessions. You cannot alter your own role or disable yourself through the admin page. Products are archived instead of deleted so payment history remains intact.

## Try the complete local flow

1. Create the administrator, add a product with a positive price, choose USD or KHR, and publish it.
2. Open a private browser window and register a regular user.
3. Find the product, select Buy now, enter a quantity and order ID, and generate KHQR.
4. Check the amount and currency on the payment page. The price is calculated on the server, not accepted from the browser.
5. A KHQR-compatible bank app can scan the code when the receiving account and environment are valid. **SIT is a testing environment; use the appropriate bank/Bakong testing process.**
6. Select Check payment with Bakong. Only a trusted matching transaction can mark the payment Paid.
7. Find the order in payment history and confirm its status.

KHQR expires after about five minutes. The UI hides an expired QR. Verification remains available for transactions made within the valid payment window. Transactions with a different amount, currency, receiver, or transaction time are rejected. A unique transaction-hash index prevents confirming two payments with the same transaction. Rechecking an already-paid payment is safe.

## Configure KHQR and Bakong

Use .NET user-secrets during development, or secure environment variables on a server. Do not put tokens in source files. From the solution directory, replace these placeholders:

```powershell
dotnet user-secrets set "Khqr:BakongAccountId" "YOUR_ACCOUNT@YOUR_BANK" --project .\PaymentSystem\PaymentSystem.csproj
dotnet user-secrets set "Khqr:MerchantName" "YOUR MERCHANT NAME" --project .\PaymentSystem\PaymentSystem.csproj
dotnet user-secrets set "Bakong:Token" "YOUR_BAKONG_TOKEN" --project .\PaymentSystem\PaymentSystem.csproj
```

`Bakong:BaseUrl` must match the token's environment. The application now defaults to `https://api-bakong.nbc.gov.kh` for real banking-app payments. A valid production credential is required. For SIT testing, explicitly configure `https://sit-api-bakong.nbc.gov.kh` with a matching SIT credential; SIT pages show a test-mode warning. HTTPS is required for upstream connections; only loopback test servers are permitted over HTTP during Development. Redirects are not followed. Never share the token or user-secrets output.

The Admin readiness panel reports only whether values exist. It does not prove the account or token is accepted by Bakong. Network failures, absent tokens, rejected tokens, mismatches, and timeouts do not confirm payment. Raw provider responses are not sent to users.

## Automatic payment updates

See [real-time verification and troubleshooting](RealtimePayments.md). A background worker reconciles recent pending payments while the application is running, even with no browser open. The checkout page reads saved status every three seconds, and dashboard/history refresh status every five seconds. There is no need to click the check button or refresh the page for automatic confirmation.

If Bakong returns HTTP 401 or 403, replace the API token with a valid token for the configured environment using user-secrets or your deployment secret store. Do not post the token into chat or commit it. A paid bank receipt cannot be used to bypass trusted verification.

## API and Postman

The existing `/api/payments`, `/api/payments/{id}`, `/api/payments/khqr`, `/api/payments/create`, and `/api/payments/check` routes now require login. They enforce the same ownership rules as the pages. Unsafe requests also require antiforgery protection because authentication uses cookies.

For Postman, use the same cookie jar to GET `/Account/Login`, POST its form fields plus `__RequestVerificationToken`, then GET `/api/payments/csrf`. Include the returned token as `X-CSRF-TOKEN` on API POST requests. Retain the login and antiforgery cookies. Anonymous API calls return 401. A POST without a valid token returns 400. API lists are paginated (50 per page, `?page=2`).

## Validation

From the solution directory:

```powershell
dotnet build .\PaymentSystem\PaymentSystem.csproj --configuration Release
python .\PaymentSystem\scripts\smoke_test.py
python .\PaymentSystem\scripts\realtime_test.py
```

Python is needed only for developer tests, not to run the app. The integration script creates a temporary SQLite database and a local fake Bakong server, starts the compiled app on a free loopback port, and removes test data afterward. It tests authorization, product checkout, price tampering, antiforgery, ownership, verification mismatches, duplicate confirmation, expiry, archiving, session revocation, profile changes, and logout. It makes no real bank transaction. Run it as your normal Windows account so ASP.NET Core can access its Windows-protected cookie keys.

`scripts/check_legacy_migration.py` backs up the local database under ignored `backups/` and migrates a separate copy. It compares every original payment field and checks foreign keys. The original remains unchanged by this script.

Manually check the pages at desktop and mobile widths, form validation messages, keyboard navigation, and countdown. Automated HTTP checks do not verify visual appearance or real bank behavior.

## Architecture

`ApplicationDbContext` now inherits `IdentityDbContext<ApplicationUser>`. Identity provides password hashing, account tokens, roles, and lockout. Razor Page models handle validated form requests; `PaymentService` centralizes payment creation, access scoping, and verification for both Razor Pages and controllers. `KhqrService` wraps the existing SDK. `BakongService` calls the server-side provider API. Bootstrap is served locally; no Node build, CDN, or frontend framework is needed.

## Deployment boundary

This is a working local application, not a certification that a financial service is production-ready. Real Bakong confirmation still requires your valid credentials and live/SIT end-to-end testing. Before external deployment, configure HTTPS, persistent SQLite storage and backups, persistent protected Data Protection keys, a real domain, monitored logs, and an environment-appropriate receiving account. Review banking provider requirements with the organization operating the app.

Production does not automatically migrate unless `Database:ApplyMigrations=true`; apply reviewed migrations as part of deployment. Prepare your first administrator locally before moving the secured database to production; public first-user admin setup is deliberately unavailable there. Local Identity accounts do not require email confirmation, and email-based recovery/2FA delivery is not configured. Do not expose registration publicly until those account policies and abuse protection match your deployment needs.
