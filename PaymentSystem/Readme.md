# KHQR PaymentSystem

The application now includes sign-in, user registration, profiles, a product catalog,
product checkout, a payment dashboard, payment history, KHQR display, and administration.

See [the current run guide](docs/RunGuide.md) for setup, page addresses, account roles,
Bakong configuration, automated tests, and deployment limitations.

Run the UI locally with:

```powershell
dotnet run --project PaymentSystem\PaymentSystem.csproj --launch-profile http
```

Open http://localhost:5291/ and create your administrator on first use. There is no
default password. Restart any older running instance before launching this version.

## Bakong configuration

The configured base URL in `appsettings.json` points to Bakong SIT. Use a Bakong SIT token with this URL; production tokens must be paired with the production base URL.

The documented transaction-check request is `POST /v1/check_transaction_by_md5` with a JSON body containing `md5` and a Bearer authorization token. A `403 Forbidden` means Bakong denied authorization; verify that the token was issued for SIT and is valid for your account. See the [Bakong SIT Open API documentation](https://sit-api-bakong.nbc.gov.kh/document).

From the `PaymentSystem` project directory (the folder containing `PaymentSystem.csproj`), set the token in .NET user-secrets. Do not put it in `appsettings.json` or commit it:

```powershell
dotnet user-secrets set "Bakong:Token" "<your Bakong SIT token>" --project .\PaymentSystem.csproj
```

The API uses the token as a Bearer token automatically. Restart the application after changing the secret. If Bakong rejects the request, inspect the application log for the upstream status; never share or log the token itself. Raw provider responses are not exposed in the UI or API.
