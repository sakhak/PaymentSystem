# Lesson 6 — Razor Pages UI foundation

Razor Pages produces HTML in the existing ASP.NET Core app. The Web API still handles `/api/payments/...` requests. Pages can later use the same injected services as controllers without making HTTP calls back to this app.

## Files and purpose

- `Program.cs`: `AddRazorPages()` registers page services, `MapRazorPages()` exposes page routes, and `UseStaticFiles()` serves CSS from `wwwroot`.
- `Pages/_ViewImports.cshtml`: imports the page namespace and enables Tag Helpers. For example, `asp-page="/Index"` generates a page URL.
- `Pages/_ViewStart.cshtml`: selects the shared layout for all pages.
- `Pages/Shared/_Layout.cshtml`: owns the common HTML document, navigation, styles, main container, and footer. `RenderBody()` inserts the current page's content. The optional Scripts section lets future pages add scripts only when needed.
- `Pages/Index.cshtml`: the home page content. `ViewData["Title"]` supplies its browser title; `@Model.Message` displays the C# model's message.
- `Pages/Index.cshtml.cs`: `IndexModel` inherits `PageModel`. `OnGet()` runs for a GET request and sets the message. `private set` lets the model change the message while other code can only read it.
- `wwwroot/css/site.css`: application colors, spacing, responsive layout, keyboard focus, and reduced-motion support.

Bootstrap 5.3.8 is loaded from its CDN with a pinned integrity hash. Internet access is required for Bootstrap. Custom CSS is local. The menu wraps on small screens, so no JavaScript or collapse plugin is needed.

## Run

Stop any existing instance first (Shift+F5 in Visual Studio). Select the `http` launch profile and press Ctrl+F5. Open `http://localhost:5291/` manually. Alternatively, from the solution directory:

```powershell
dotnet run --project .\PaymentSystem\PaymentSystem.csproj --launch-profile http
```

HTTP is available in Development to avoid local certificate issues. Other environments retain HTTPS redirection. No authentication has been added in this lesson.

## Test

1. Confirm the browser displays the KHQR navigation, green introduction panel, three feature cards, and footer.
2. Select Workspace and Getting started. Each link should scroll to its section.
3. Resize to mobile width. Cards should stack and navigation should remain usable.
4. Press Tab: links should show focus. The first link skips to the main content.
5. Check `http://localhost:5291/css/site.css` returns CSS.
6. In Postman, GET `/api/payments/{id}` using a known ID. Check the existing response still works. A missing ID returns 404.

The feature cards describe later lessons. They do not claim that a dashboard, create-payment form, or payment history UI has been implemented yet. There are no fabricated payment totals or paid statuses.

## Common problems

- Old plain page: stop/restart the app, then Ctrl+F5 in the browser to refresh cached assets.
- Missing Bootstrap styling: check internet access and the browser network panel for a blocked CDN request.
- Missing local CSS: check `UseStaticFiles()` and the `wwwroot/css/site.css` path.
- MSB3021/MSB3026 or address already in use: stop the existing app before building or launching again.
- NuGet/EF Core missing references: run `dotnet restore .\PaymentSystem\PaymentSystem.csproj` from the solution directory.
- Literal `asp-page` attributes in the HTML: check `_ViewImports.cshtml` includes MVC Tag Helpers.

## Practical assignment

Change the welcome heading in `Index.cshtml` and the accent color in `site.css`. Restart, refresh, and check both desktop and mobile widths. The next lesson connects the dashboard to real SQLite payment data.
