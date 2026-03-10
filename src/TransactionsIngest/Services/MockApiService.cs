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

    public MockApiService(bool useMockFeed, string baseUrl, string endpoint)
    {
        _useMockFeed = useMockFeed;
        _baseUrl = baseUrl;
        _endpoint = endpoint;
    }

    public Task<List<ApiTransaction>> FetchTransactionsAsync()
    {
        if (_useMockFeed)
            return Task.FromResult(GetMockTransactions());

        // Real API call would go here in production
        throw new NotImplementedException("Real API calls not implemented. Set UseMockFeed=true.");
    }

    private List<ApiTransaction> GetMockTransactions()
    {
        var now = DateTime.UtcNow;

        return new List<ApiTransaction>
        {
            new ApiTransaction
            {
                TransactionId = "T-1001",
                CardNumber = "4111111111111111",
                LocationCode = "STO-01",
                ProductName = "Wireless Mouse",
                Amount = 19.99m,
                Timestamp = now.AddHours(-2)
            },
            new ApiTransaction
            {
                TransactionId = "T-1002",
                CardNumber = "4000000000000002",
                LocationCode = "STO-02",
                ProductName = "USB-C Cable",
                Amount = 25.00m,
                Timestamp = now.AddHours(-5)
            },
            new ApiTransaction
            {
                TransactionId = "T-1003",
                CardNumber = "5500000000000004",
                LocationCode = "STO-01",
                ProductName = "HDMI Adapter",
                Amount = 15.49m,
                Timestamp = now.AddHours(-10)
            },
            new ApiTransaction
            {
                TransactionId = "T-1004",
                CardNumber = "4111111111111111",
                LocationCode = "STO-03",
                ProductName = "Keyboard",
                Amount = 45.00m,
                Timestamp = now.AddHours(-20)
            },
            new ApiTransaction
            {
                TransactionId = "T-1005",
                CardNumber = "4000000000000002",
                LocationCode = "STO-02",
                ProductName = "Mouse Pad",
                Amount = 9.99m,
                Timestamp = now.AddHours(-23)
            }
        };
    }
}