namespace TransactionsIngest.Models;

public class AppSettings
{
    public ConnectionStrings ConnectionStrings { get; set; } = new();
    public ApiSettings ApiSettings { get; set; } = new();
    public IngestSettings IngestSettings { get; set; } = new();
}

public class ConnectionStrings
{
    public string DefaultConnection { get; set; } = string.Empty;
}

public class ApiSettings
{
    public string BaseUrl { get; set; } = string.Empty;
    public string TransactionsEndpoint { get; set; } = string.Empty;
    public bool UseMockFeed { get; set; } = true;
}

public class IngestSettings
{
    public int LookbackHours { get; set; } = 24;
}