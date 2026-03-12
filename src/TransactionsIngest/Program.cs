using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using TransactionsIngest.Data;
using TransactionsIngest.Models;
using TransactionsIngest.Services;

// Build configuration from appsettings.json
var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .Build();

// Read settings
var connectionString = configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string not found.");

var apiSettings = configuration.GetSection("ApiSettings").Get<ApiSettings>()
    ?? throw new InvalidOperationException("ApiSettings not found.");

var ingestSettings = configuration.GetSection("IngestSettings").Get<IngestSettings>()
    ?? throw new InvalidOperationException("IngestSettings not found.");

// Set up DbContext with SQLite
var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
    .UseSqlite(connectionString)
    .Options;

await using var db = new AppDbContext(dbOptions);

// Ensure database and tables are created
await db.Database.EnsureCreatedAsync();

Console.WriteLine("=== Transactions Ingest Job Started ===");
Console.WriteLine($"  Time        : {DateTime.UtcNow:u}");
Console.WriteLine($"  Lookback    : {ingestSettings.LookbackHours} hours");
Console.WriteLine($"  Mock Feed   : {apiSettings.UseMockFeed}");
Console.WriteLine("=======================================");

// Fetch transactions from API (or mock)
var apiService = new MockApiService(
    apiSettings.UseMockFeed,
    apiSettings.BaseUrl,
    apiSettings.TransactionsEndpoint,
    apiSettings.MockFeedPath);

var transactions = await apiService.FetchTransactionsAsync();
Console.WriteLine($"\nFetched {transactions.Count} transaction(s) from API.\n");

// Run the ingest
var ingestService = new IngestService(db, ingestSettings.LookbackHours);
await ingestService.RunAsync(transactions);

Console.WriteLine("\n=== Ingest Job Finished ===");