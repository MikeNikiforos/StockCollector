# Program Summary

## The results.txt file contain the output of the application

## Key Flaws in the Original Program
The original program had several critical issues, including:
1. **API Rate Limit Handling**: The program failed to account for the data partner API's rate limit of 60 calls per minute. Exceeding this limit resulted in `Too Many Requests` errors.
2. **Data Aggregation**: It stored incorrect counts and lacked aggregation, resulting in duplicate entries for the same ticker rather than consolidating them.
3. **Skipped Data**: The program missed processing data for May 31st.
4. **Error Handling**:
   - API calls lacked error handling for rate limits or server errors.
   - Requests were not retried on failure.
5. **Sequential Processing**:
   - API data processing was done in a single-threaded, sequential manner without considering rate limits.
6. **Poor Data Structure**:
   - Used `List<Tuple<string, int>>` for collections, which lacked clarity and proper typing.
   - Added duplicate entries for tickers instead of aggregating them.
7  **Instantiating** HttpClient: A new HttpClient was created for every API call. This is a bad practice as it can lead to socket exhaustion and performance issues due to inefficient connection reuse
8. **Separation of Concerns**:
   - Used tuples instead of strongly typed classes.
   - Mixed concerns, such as direct API calls within nested loops and processing API data in a single method.
   - Lacked thread safety.
9. **Logging**:
   - Limited to `Console.WriteLine`, making it unsuitable for large-scale or production use.

## Enhancements in the Updated Version
The updated program introduced several improvements:
- **Thread Safety**: 
  - Collections were replaced with `ConcurrentDictionary` for thread-safe aggregation.
  - The `AddOrUpdate` method ensures a single entry per ticker, incrementing counts as needed.
- **Rate Limiting**:
  - Implemented a configurable rate-limiting mechanism to handle a maximum of 60 API requests per minute.
  - Added retry logic for `Too Many Requests` responses, parsing the `Retry-After` header and delaying as necessary.
- **Improved Design**:
  - Introduced a strongly typed `TicketRecord` class, enhancing readability and maintainability.
  - Modularized the code into reusable methods, such as `GetApiData` for API requests.
  - Encapsulated logging into an `ILogger` interface, supporting both console and file logging.
- **Error Resilience**:
  - Added robust error handling for API responses.
  - Gracefully handled deserialization errors while continuing processing.
- **Logging**:
  - Logs now include deserialization errors and detailed tracking of API retries and rate-limiting.

## Remaining Areas for Improvement
While the updated version is significantly better, further enhancements could include:
- **Enhanced Logging**:
  - Incorporate metrics to monitor API request success rates and error patterns.
- **Testing**:
  - Add unit tests for key methods to ensure reliability and maintainability.
