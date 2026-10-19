using InvoiceDesk.Core;
using InvoiceDesk.Web.Components;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// same store and business rules as the desktop app
var dbPath = builder.Configuration["Data:Path"] ?? Path.Combine(builder.Environment.ContentRootPath, "App_Data", "invoicedesk.db");
Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
builder.Services.AddSingleton(new InvoiceStore(dbPath));

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseAntiforgery();

app.MapStaticAssets();

// CSV download for the current invoice filter
app.MapGet("/export/invoices.csv", (InvoiceStore store, DateTime? from, DateTime? to, string? customer) =>
    Results.File(System.Text.Encoding.UTF8.GetBytes(InvoiceStore.ToCsv(store.GetInvoices(from, to, customer))),
                 "text/csv", "invoices.csv"));

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
