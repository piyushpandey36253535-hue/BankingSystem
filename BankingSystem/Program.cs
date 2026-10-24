using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using BankingSystem.Data;
using BankingSystem.Services;

var builder = WebApplication.CreateBuilder(args);

// Configure Database Provider (supports both SQL Server and SQLite seamlessly)
var dbProvider = builder.Configuration["DatabaseProvider"] ?? "Sqlite";

builder.Services.AddDbContext<BankingDbContext>(options =>
{
    if (dbProvider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
    {
        var sqlServerConnection = builder.Configuration.GetConnectionString("SqlServerConnection")
            ?? "Server=localhost,1433;Database=CanadaBankingDb;User Id=sa;Password=YourStrong@Password;TrustServerCertificate=True;";
        options.UseSqlServer(sqlServerConnection);
    }
    else
    {
        var sqliteConnection = builder.Configuration.GetConnectionString("SqliteConnection") ?? "Data Source=banking.db";
        options.UseSqlite(sqliteConnection);
    }
});

// Register Core Banking Domain Services
builder.Services.AddScoped<ILedgerService, LedgerService>();
builder.Services.AddScoped<IAccountService, AccountService>();
builder.Services.AddScoped<IFraudDetectionService, FraudDetectionService>();
builder.Services.AddScoped<ITransferService, TransferService>();
builder.Services.AddScoped<IStatementService, StatementService>();
builder.Services.AddScoped<IReconciliationService, ReconciliationService>();

// Register Background Jobs (Hosted Services)
builder.Services.AddHostedService<ScheduledTransferProcessorHostedService>();
builder.Services.AddHostedService<ReconciliationHostedService>();

// Configure Controllers & JSON serialization
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

// Enable CORS for flexibility
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Seed Database on startup
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<BankingDbContext>();
        await DbInitializer.InitializeAsync(db);
        logger.LogInformation("BankingSystem database initialized and seeded successfully with provider: {Provider}", dbProvider);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "An error occurred during database seeding: {Message}", ex.Message);
    }
}

app.UseCors("AllowAll");
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseRouting();

app.MapControllers();
app.MapFallbackToFile("index.html");

app.Run();
