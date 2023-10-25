using ManagerApp;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Havit.Blazor.Components.Web;
using Havit.Blazor.Components.Web.Bootstrap;

using SqliteWasmHelper;
using Microsoft.EntityFrameworkCore;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddSqliteWasmDbContextFactory<CryptoTaxManDbContext>(
  opts => opts.UseSqlite("Data Source=./config/exchangerates.db"));
builder.Services.AddHxServices();
builder.Services.AddHxMessenger();
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

await builder.Build().RunAsync();
