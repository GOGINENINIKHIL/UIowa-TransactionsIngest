using System.Text.Json;
using System.Text.Json.Serialization;
using TransactionsIngest.Models;

namespace TransactionsIngest.Services;

public class ApiTransaction
{
    [JsonPropertyName("transactionId")]
    public string TransactionId { get; set; } = string.Empty;

    [JsonPropertyName("cardNumber")]
    public string CardNumber { get; set; } = string.Empty;

    [JsonPropertyName("locationCode")]
    public string LocationCode { get; set; } = string.Empty;

    [JsonPropertyName("productName")]
    public string ProductName { get; set; } = string.Empty;

    [JsonPropertyName("amount")]
    public decimal Amount { get; set; }

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; }
}

public class MockApiService
{
    private readonly bool _useMockFeed;
    private readonly string _baseUrl;
    private readonly string _endpoint;
    private readonly string _mockFeedPath;

    public MockApiService(bool useMockFeed, string baseUrl, string endpoint, string mockFeedPath = "")
    {
        _useMockFeed = useMockFeed;
        _baseUrl = baseUrl;
        _endpoint = endpoint;
        _mockFeedPath = mockFeedPath;
    }

    public Task<List<ApiTransaction>> FetchTransactionsAsync()
    {
        if (_useMockFeed)
        {
            // If a mock feed file is configured and exists, read from it
            if (!string.IsNullOrWhiteSpace(_mockFeedPath) && File.Exists(_mockFeedPath))
            {
                Console.WriteLine($"  Using mock feed file: {_mockFeedPath}");
                return Task.FromResult(ReadFromJsonFile(_mockFeedPath));
            }

            // Fall back to hardcoded mock data
            Console.WriteLine("  Using hardcoded mock data.");
            return Task.FromResult(GetMockTransactions());
        }

        // Real API call would go here in production
        throw new NotImplementedException("Real API calls not implemented. Set UseMockFeed=true.");
    }

    private List<ApiTransaction> ReadFromJsonFile(string path)
    {
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<List<ApiTransaction>>(json)
            ?? new List<ApiTransaction>();
    }

    private List<ApiTransaction> GetMockTransactions()
    {
        return new List<ApiTransaction>
        {
            new ApiTransaction
            {
                TransactionId = "T-1001",
                CardNumber = "4111111111111111",
                LocationCode = "STO-01",
                ProductName = "Wireless Mouse",
                Amount = 19.99m,
                Timestamp = new DateTime(2026, 3, 11, 0, 0, 0, DateTimeKind.Utc)
            },
            new ApiTransaction
            {
                TransactionId = "T-1002",
                CardNumber = "4000000000000002",
                LocationCode = "STO-02",
                ProductName = "USB-C Cable",
                Amount = 25.00m,
                Timestamp = new DateTime(2026, 3, 10, 22, 0, 0, DateTimeKind.Utc)
            },
            new ApiTransaction
            {
                TransactionId = "T-1003",
                CardNumber = "5500000000000004",
                LocationCode = "STO-01",
                ProductName = "HDMI Adapter",
                Amount = 15.49m,
                Timestamp = new DateTime(2026, 3, 10, 18, 0, 0, DateTimeKind.Utc)
            },
            new ApiTransaction
            {
                TransactionId = "T-1004",
                CardNumber = "4111111111111111",
                LocationCode = "STO-03",
                ProductName = "Keyboard",
                Amount = 45.00m,
                Timestamp = new DateTime(2026, 3, 10, 10, 0, 0, DateTimeKind.Utc)
            },
            new ApiTransaction
            {
                TransactionId = "T-1005",
                CardNumber = "4000000000000002",
                LocationCode = "STO-02",
                ProductName = "Mouse Pad",
                Amount = 9.99m,
                Timestamp = new DateTime(2026, 3, 10, 2, 0, 0, DateTimeKind.Utc)
            }
        };
    }
}