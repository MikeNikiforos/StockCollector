using Newtonsoft.Json;

using System.Collections.Concurrent;
using System.Net;

class Program
{
    public class TicketRecord
    {
        public string Ticker { get; set; }
        public string Sentiment { get; set; }
        public long NoOfComments { get; set; }

        public TicketRecord(string ticker, string sentiment, long noOfComments)
        {
            Ticker = ticker ?? throw new ArgumentNullException(nameof(ticker));
            Sentiment = sentiment;
            NoOfComments = noOfComments;
        }
    }

    static readonly HttpClient httpClient = new HttpClient();
    static DateTime lastRequestTime = DateTime.MinValue;
    
    static int requestCount = 0;
    static readonly TimeSpan rateLimitResetInterval = TimeSpan.FromMinutes(1);
    const int YEAR = 2023;
    static string apiUrlBase = $"https://tradestie.com/api/v1/apps/reddit?date={YEAR}-";
    const int RATELIMIT = 60;
    static ILogger logger = new FileLogger(@"c:\temp\results\enhanced-2.txt");
    
    static async Task Main(string[] args)
    {
        // Mocked data collections to store ticker information and records
        var tickerCollection = new ConcurrentDictionary<string, int>();
        var recordCollection = new ConcurrentDictionary<string, TicketRecord>();

        for (int month = 4; month <= 6; month++)
        {
            // Construct the API URL base for the current month
            string apiUrl = $"{apiUrlBase}{month:D2}-";

            await ProcessMonthData(month, YEAR, tickerCollection, recordCollection);
        }

        // Display the mocked data collections
        await DisplayDataCollections(tickerCollection, recordCollection);

        // Fake call to store both collections in a pretend database
        await StoreInDatabase(tickerCollection, recordCollection);
    }
    static async Task ProcessMonthData(int month, int year, ConcurrentDictionary<string, int> tickerCollection, ConcurrentDictionary<string, TicketRecord> recordCollection)
    {

        
        // Using Task.WhenAll will paralleize the api calls, thus more performant.  However, the logging will be out of order.
        //var tasks = new List<Task>();
        var numOfDaysInMonth = DateTime.DaysInMonth(year, month);
        for (int day = 1; day <= numOfDaysInMonth; day++)
        {
            string fullUrl = $"{apiUrlBase}{month:D2}-{day:D2}";
            //tasks.Add(Task.Run(async () =>
            //{
                var responseData = await GetApiData(fullUrl);
                await ProcessApiData(responseData, tickerCollection, recordCollection);
            //}));
        }
        //await Task.WhenAll(tasks);
    }

    static async Task<string> GetApiData(string apiUrl)
    {
        await EnsureRateLimit();

        var response = await httpClient.GetAsync(apiUrl);

        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            var retryAfter = response.Headers.Contains("Retry-After")
                ? TimeSpan.FromSeconds(int.Parse(response.Headers.GetValues("Retry-After").FirstOrDefault() ?? "60"))
                : TimeSpan.FromSeconds(60);

            await logger.Log($"Rate limit hit. Waiting for {retryAfter.TotalSeconds} seconds.");
            await Task.Delay(retryAfter); // Wait for the retry period
            return await GetApiData(apiUrl); // Retry the request after waiting
        }

        return await response.Content.ReadAsStringAsync();
    }

    static async Task EnsureRateLimit()
    {
        // Track requests and the time when the first request was made
        if (lastRequestTime == DateTime.MinValue)
        {
            lastRequestTime = DateTime.Now;
        }

        // Check how much time has passed since the first request in the current minute
        var elapsedTime = DateTime.Now - lastRequestTime;

        if (elapsedTime < rateLimitResetInterval && requestCount >= RATELIMIT)
        {
            // If too many requests have been made in the current minute, wait for the reset
            var waitTime = rateLimitResetInterval - elapsedTime;
            await logger.Log($"Rate limit reached. Waiting for {waitTime.TotalSeconds} seconds.");
            await Task.Delay(waitTime); // Wait until the rate limit resets
            lastRequestTime = DateTime.Now; // Reset the start time for the next batch
            requestCount = 0; // Reset the count of requests
        }

        // After waiting (if necessary), make the request and update the count
        requestCount++;
    }

    static async Task ProcessApiData(string responseData, ConcurrentDictionary<string, int> tickerCollection, ConcurrentDictionary<string, TicketRecord> recordCollection)
    {
        try
        {
            // Deserialize JSON response
            var data = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(responseData);
            if (data == null || data.Count == 0) return;

            // Loop through each data entry
            foreach (var entry in data)
            {
                if (entry == null) continue;

                await ProcessEntry(entry, tickerCollection, recordCollection);
            }
        }
        catch (JsonException ex)
        {
            await logger.Log($"Error deserializing response: {ex.Message}");
        }
    }

    static async Task ProcessEntry(Dictionary<string, object> entry, ConcurrentDictionary<string, int> tickerCollection, ConcurrentDictionary<string, TicketRecord> recordCollection)
    {
        var ticker = (string)entry.GetValueOrDefault("ticker");
        var sentiment = (string)entry.GetValueOrDefault("sentiment");
        var noOfComments = (long)entry.GetValueOrDefault("no_of_comments");

        var newRecord = new TicketRecord(ticker, sentiment, noOfComments);

        await SaveTicker(newRecord.Ticker, tickerCollection);
        await SaveRecord(newRecord, recordCollection);
    }


    static async Task SaveTicker(string ticker, ConcurrentDictionary<string, int> tickerCollection)
    {
        tickerCollection.AddOrUpdate(
                ticker,             
                1,                 
                (key, oldValue) => oldValue + 1 
            );
        await logger.Log($"Saved Ticker: {ticker}");
    }

    static async Task SaveRecord(TicketRecord newRecord, ConcurrentDictionary<string, TicketRecord> recordCollection)
    {
        recordCollection.AddOrUpdate(
            newRecord.Ticker, 
            newRecord,        
            (key, existingRecord) =>
            {
                // Update logic
                existingRecord.Sentiment = newRecord.Sentiment;
                existingRecord.NoOfComments += newRecord.NoOfComments;
                return existingRecord;
            });

        await logger.Log($"Saved Record: Ticker - {newRecord.Ticker}, Sentiment - {newRecord.Sentiment}, Comments - {newRecord.NoOfComments}");
    }

    static async Task DisplayDataCollections(ConcurrentDictionary<string, int> tickerCollection, ConcurrentDictionary<string, TicketRecord> recordCollection)
    {
        await logger.Log("\nMocked Data Collections - Tickers:");
        foreach (var ticket in tickerCollection)
        {
            await logger.Log($"Ticker: {ticket.Key}, Records Count: {ticket.Value}");
        }

        await logger.Log("\nMocked Data Collections - Records:");
        foreach (var record in recordCollection)
        {
            await logger.Log($"Ticker: {record.Value.Ticker}, Sentiment: {record.Value.Sentiment}, Comments: {record.Value.NoOfComments}");
        }
    }

    // Fake call to store both collections in a pretend database
    static async Task StoreInDatabase(ConcurrentDictionary<string, int> tickerCollection, ConcurrentDictionary<string, TicketRecord> recordCollection)
    {
        await logger.Log("\nFake Call: Storing both data collections in a pretend database.");
    }

}

public interface ILogger
{
    Task Log(string message);
}

public class ConsoleLogger : ILogger
{
    public Task Log(string message)
    {
        Console.WriteLine(message);
        return Task.CompletedTask;
    }
}

public class FileLogger : ILogger
{
    private readonly string _filePath;
    private static readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

    public FileLogger(string filePath)
    {
        _filePath = filePath;

        // Ensure the directory exists
        var directory = Path.GetDirectoryName(filePath);
        if (directory != null && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Create the file if it doesn't exist
        if (!File.Exists(_filePath))
        {
            File.Create(_filePath).Dispose(); // Dispose the file stream immediately after creating the file
        }
    }

    public async Task Log(string message)
    {
        await _semaphore.WaitAsync();
        try
        {
            await File.AppendAllTextAsync(_filePath, message + Environment.NewLine);
        }
        finally
        {
            _semaphore.Release();
        }
    }
}