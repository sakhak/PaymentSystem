Run the API with:

```powershell
dotnet run --project PaymentSystem\PaymentSystem.csproj --launch-profile https
```

## Bakong configuration

The configured base URL in `appsettings.json` points to Bakong SIT. Use a Bakong SIT token with this URL; production tokens must be paired with the production base URL.

The documented transaction-check request is `POST /v1/check_transaction_by_md5` with a JSON body containing `md5` and a Bearer authorization token. A `403 Forbidden` means Bakong denied authorization; verify that the token was issued for SIT and is valid for your account. See the [Bakong SIT Open API documentation](https://sit-api-bakong.nbc.gov.kh/document).

From the `PaymentSystem` project directory (the folder containing `PaymentSystem.csproj`), set the token in .NET user-secrets. Do not put it in `appsettings.json` or commit it:

```powershell
dotnet user-secrets set "Bakong:Token" "<your Bakong SIT token>" --project .\PaymentSystem.csproj
```

The API uses the token as a Bearer token automatically. Restart the API after changing the secret. If Bakong still rejects the request, inspect the application log for the upstream status and a truncated response excerpt; never share or log the token itself.