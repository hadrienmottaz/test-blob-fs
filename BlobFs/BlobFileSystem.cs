using System.Net;
using Polly;
using Polly.Retry;

namespace BlobFs;

/// <summary>
/// Configuration options for BlobFileSystem
/// </summary>
public class BlobFileSystemOptions
{
    /// <summary>
    /// Base URL for blob storage endpoint
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// SAS token for authentication
    /// </summary>
    public string SasToken { get; set; } = string.Empty;

    /// <summary>
    /// Maximum number of retry attempts (default: 3)
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Initial delay between retries in milliseconds (default: 1000)
    /// </summary>
    public int InitialRetryDelayMs { get; set; } = 1000;

    /// <summary>
    /// Maximum delay between retries in milliseconds (default: 30000)
    /// </summary>
    public int MaxRetryDelayMs { get; set; } = 30000;

    /// <summary>
    /// HTTP timeout in seconds (default: 100)
    /// </summary>
    public int TimeoutSeconds { get; set; } = 100;
}

/// <summary>
/// Implementation of IFileSystem that accesses Azure blobs through HTTP with SAS token
/// </summary>
public class BlobFileSystem : IFileSystem
{
    private readonly HttpClient _httpClient;
    private readonly BlobFileSystemOptions _options;
    private readonly ResiliencePipeline _retryPipeline;

    public BlobFileSystem(BlobFileSystemOptions options, HttpClient? httpClient = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        
        if (string.IsNullOrWhiteSpace(_options.BaseUrl))
            throw new ArgumentException("BaseUrl cannot be empty", nameof(options));
        
        if (string.IsNullOrWhiteSpace(_options.SasToken))
            throw new ArgumentException("SasToken cannot be empty", nameof(options));

        _httpClient = httpClient ?? new HttpClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);

        // Create retry pipeline with exponential backoff
        _retryPipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = _options.MaxRetryAttempts,
                BackoffType = DelayBackoffType.Exponential,
                Delay = TimeSpan.FromMilliseconds(_options.InitialRetryDelayMs),
                MaxDelay = TimeSpan.FromMilliseconds(_options.MaxRetryDelayMs),
                ShouldHandle = new PredicateBuilder().Handle<HttpRequestException>()
                    .Handle<TaskCanceledException>()
            })
            .Build();
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<string>> ListAsync(CancellationToken cancellationToken = default)
    {
        // Placeholder for list operation - to be filled in later
        // This would typically call an HTTP endpoint to list blobs
        try
        {
            var url = BuildUrl("list");
            
            var response = await _retryPipeline.ExecuteAsync(async ct =>
            {
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                var resp = await _httpClient.SendAsync(request, ct);
                
                // Throw exception for retryable status codes
                if (resp.StatusCode == HttpStatusCode.RequestTimeout ||
                    resp.StatusCode == HttpStatusCode.TooManyRequests ||
                    (int)resp.StatusCode >= 500)
                {
                    throw new HttpRequestException($"Request failed with status code {resp.StatusCode}");
                }
                
                return resp;
            }, cancellationToken);

            response.EnsureSuccessStatusCode();
            
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            
            // For now, return an empty list as this is a placeholder
            // In a real implementation, this would parse the response and return blob names
            return Array.Empty<string>();
        }
        catch (HttpRequestException ex)
        {
            throw new NetworkException("Failed to list files", ex);
        }
        catch (TaskCanceledException ex)
        {
            throw new NetworkException("Request timed out while listing files", ex);
        }
    }

    /// <inheritdoc/>
    public async Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default)
    {
        var bytes = await ReadAllBytesAsync(path, cancellationToken);
        return System.Text.Encoding.UTF8.GetString(bytes);
    }

    /// <inheritdoc/>
    public async Task<byte[]> ReadAllBytesAsync(string path, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Path cannot be empty", nameof(path));

        try
        {
            var url = BuildUrl(path);
            
            var response = await _retryPipeline.ExecuteAsync(async ct =>
            {
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                var resp = await _httpClient.SendAsync(request, ct);
                
                // Don't retry on 404
                if (resp.StatusCode == HttpStatusCode.NotFound)
                {
                    return resp;
                }
                
                // Throw exception for retryable status codes
                if (resp.StatusCode == HttpStatusCode.RequestTimeout ||
                    resp.StatusCode == HttpStatusCode.TooManyRequests ||
                    (int)resp.StatusCode >= 500)
                {
                    throw new HttpRequestException($"Request failed with status code {resp.StatusCode}");
                }
                
                return resp;
            }, cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                throw new FileNotFoundException(path);
            }

            response.EnsureSuccessStatusCode();
            
            return await response.Content.ReadAsByteArrayAsync(cancellationToken);
        }
        catch (FileNotFoundException)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            throw new NetworkException($"Failed to read file: {path}", ex);
        }
        catch (TaskCanceledException ex)
        {
            throw new NetworkException($"Request timed out while reading file: {path}", ex);
        }
    }

    /// <inheritdoc/>
    public async Task<Stream> OpenReadAsync(string path, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Path cannot be empty", nameof(path));

        try
        {
            var url = BuildUrl(path);
            
            var response = await _retryPipeline.ExecuteAsync(async ct =>
            {
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                var resp = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
                
                // Don't retry on 404
                if (resp.StatusCode == HttpStatusCode.NotFound)
                {
                    return resp;
                }
                
                // Throw exception for retryable status codes
                if (resp.StatusCode == HttpStatusCode.RequestTimeout ||
                    resp.StatusCode == HttpStatusCode.TooManyRequests ||
                    (int)resp.StatusCode >= 500)
                {
                    throw new HttpRequestException($"Request failed with status code {resp.StatusCode}");
                }
                
                return resp;
            }, cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                throw new FileNotFoundException(path);
            }

            response.EnsureSuccessStatusCode();
            
            return await response.Content.ReadAsStreamAsync(cancellationToken);
        }
        catch (FileNotFoundException)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            throw new NetworkException($"Failed to open file stream: {path}", ex);
        }
        catch (TaskCanceledException ex)
        {
            throw new NetworkException($"Request timed out while opening file stream: {path}", ex);
        }
    }

    /// <inheritdoc/>
    public async Task<bool> ExistsAsync(string path, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Path cannot be empty", nameof(path));

        try
        {
            var url = BuildUrl(path);
            
            var response = await _retryPipeline.ExecuteAsync(async ct =>
            {
                var request = new HttpRequestMessage(HttpMethod.Head, url);
                return await _httpClient.SendAsync(request, ct);
            }, cancellationToken);

            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
            return false;
        }
        catch (TaskCanceledException)
        {
            return false;
        }
    }

    private string BuildUrl(string path)
    {
        // Normalize path
        var normalizedPath = path.TrimStart('/');
        
        // Build URL with SAS token
        var baseUrl = _options.BaseUrl.TrimEnd('/');
        var url = $"{baseUrl}/{normalizedPath}";
        
        // Add SAS token (check if it already has query parameters)
        var sasToken = _options.SasToken.TrimStart('?');
        var separator = url.Contains('?') ? "&" : "?";
        
        return $"{url}{separator}{sasToken}";
    }
}
