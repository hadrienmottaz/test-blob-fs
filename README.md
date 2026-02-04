# BlobFs - Azure Blob Filesystem Abstraction Layer

A C# library that provides a filesystem abstraction layer for accessing Azure blobs through HTTP calls with SAS tokens. This library enables you to replace direct filesystem access code with a blob-based implementation that includes robust retry logic and error handling for network issues.

## Features

- **Filesystem Abstraction**: Clean interface (`IFileSystem`) that can be used anywhere you need file access
- **Azure Blob Support**: Access blobs via HTTP with SAS token authentication
- **Retry Mechanism**: Built-in exponential backoff retry logic using Polly for handling transient network failures
- **Error Handling**: Custom exceptions for different error scenarios (file not found, network errors)
- **Async/Await**: Fully asynchronous API for optimal performance
- **Stream Support**: Efficient streaming for large files

## Installation

Add the BlobFs project reference to your solution, or build and reference the compiled DLL.

## Usage

### Basic Example

```csharp
using BlobFs;

// Configure the blob filesystem
var options = new BlobFileSystemOptions
{
    BaseUrl = "https://mystorageaccount.blob.core.windows.net/mycontainer",
    SasToken = "sv=2021-06-08&ss=b&srt=sco&sp=r&sig=yoursastoken",
    MaxRetryAttempts = 3,
    InitialRetryDelayMs = 1000,
    MaxRetryDelayMs = 30000,
    TimeoutSeconds = 100
};

// Create the filesystem instance
IFileSystem fileSystem = new BlobFileSystem(options);

// Read a text file
string content = await fileSystem.ReadAllTextAsync("path/to/file.txt");

// Read binary data
byte[] data = await fileSystem.ReadAllBytesAsync("path/to/image.png");

// Check if a file exists
bool exists = await fileSystem.ExistsAsync("path/to/file.txt");

// Open a stream for reading (useful for large files)
using (var stream = await fileSystem.OpenReadAsync("path/to/largefile.dat"))
{
    // Process the stream
}

// List files (placeholder - to be implemented)
var files = await fileSystem.ListAsync();
```

### Replace Disk File Access

You can easily replace code that reads from the local filesystem:

**Before (reading from disk):**
```csharp
// Direct file system access
string content = await File.ReadAllTextAsync("C:\\data\\file.txt");
byte[] data = await File.ReadAllBytesAsync("C:\\data\\image.png");
bool exists = File.Exists("C:\\data\\file.txt");
```

**After (reading from blob storage):**
```csharp
// Using the abstraction layer
IFileSystem fileSystem = new BlobFileSystem(options);

string content = await fileSystem.ReadAllTextAsync("file.txt");
byte[] data = await fileSystem.ReadAllBytesAsync("image.png");
bool exists = await fileSystem.ExistsAsync("file.txt");
```

### Dependency Injection

For production use, it's recommended to use HttpClientFactory to avoid socket exhaustion:

```csharp
// In your Startup.cs or Program.cs
services.AddHttpClient();
services.Configure<BlobFileSystemOptions>(configuration.GetSection("BlobStorage"));
services.AddSingleton<IFileSystem>(sp =>
{
    var options = sp.GetRequiredService<IOptions<BlobFileSystemOptions>>().Value;
    var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
    var httpClient = httpClientFactory.CreateClient();
    return new BlobFileSystem(options, httpClient);
});

// In your service
public class MyService
{
    private readonly IFileSystem _fileSystem;
    
    public MyService(IFileSystem fileSystem)
    {
        _fileSystem = fileSystem;
    }
    
    public async Task ProcessFile(string path)
    {
        var content = await _fileSystem.ReadAllTextAsync(path);
        // Process content...
    }
}
```

### Configuration

The `BlobFileSystemOptions` class allows you to configure:

- **BaseUrl**: The base URL of your Azure Blob Storage container
- **SasToken**: The Shared Access Signature token for authentication
- **MaxRetryAttempts**: Maximum number of retry attempts (default: 3)
- **InitialRetryDelayMs**: Initial delay between retries in milliseconds (default: 1000)
- **MaxRetryDelayMs**: Maximum delay between retries in milliseconds (default: 30000)
- **TimeoutSeconds**: HTTP request timeout in seconds (default: 100)

### Error Handling

The library provides custom exceptions for different error scenarios:

```csharp
try
{
    var content = await fileSystem.ReadAllTextAsync("missing.txt");
}
catch (BlobFileNotFoundException ex)
{
    // File doesn't exist in blob storage
    Console.WriteLine($"File not found: {ex.Path}");
}
catch (NetworkException ex)
{
    // Network error occurred (after retries exhausted)
    Console.WriteLine($"Network error: {ex.Message}");
}
catch (FileSystemException ex)
{
    // Other filesystem error
    Console.WriteLine($"Filesystem error: {ex.Message}");
}
```

## Retry Logic

The library automatically retries failed requests with exponential backoff for:
- HTTP request exceptions
- Task cancellation (timeout) exceptions
- Server errors (5xx status codes)
- Request timeout (408) status codes
- Too many requests (429) status codes

File not found errors (404) are not retried.

## List Method

The `ListAsync()` method is currently a placeholder that returns an empty list. This method is designed to call an HTTP endpoint to retrieve a list of blobs. You can implement this method according to your specific blob storage API:

```csharp
// Placeholder - will be implemented based on your specific requirements
var files = await fileSystem.ListAsync();
```

## Building and Testing

Build the solution:
```bash
dotnet build
```

Run the tests:
```bash
dotnet test
```

## License

This project is provided as-is for demonstration purposes.