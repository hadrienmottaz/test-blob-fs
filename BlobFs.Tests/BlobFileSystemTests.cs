using System.Net;
using Moq;
using Moq.Protected;
using Xunit;

namespace BlobFs.Tests;

public class BlobFileSystemTests
{
    private readonly BlobFileSystemOptions _options;
    private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;
    private readonly HttpClient _httpClient;

    public BlobFileSystemTests()
    {
        _options = new BlobFileSystemOptions
        {
            BaseUrl = "https://example.blob.core.windows.net/container",
            SasToken = "sv=2021-06-08&ss=b&srt=sco&sp=r&sig=test",
            MaxRetryAttempts = 2,
            InitialRetryDelayMs = 100,
            TimeoutSeconds = 30
        };

        _mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_mockHttpMessageHandler.Object);
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenOptionsIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => new BlobFileSystem(null!));
    }

    [Fact]
    public void Constructor_ThrowsArgumentException_WhenBaseUrlIsEmpty()
    {
        var options = new BlobFileSystemOptions { BaseUrl = "", SasToken = "token" };
        Assert.Throws<ArgumentException>(() => new BlobFileSystem(options));
    }

    [Fact]
    public void Constructor_ThrowsArgumentException_WhenSasTokenIsEmpty()
    {
        var options = new BlobFileSystemOptions { BaseUrl = "https://example.com", SasToken = "" };
        Assert.Throws<ArgumentException>(() => new BlobFileSystem(options));
    }

    [Fact]
    public async Task ReadAllTextAsync_ReturnsContent_WhenFileExists()
    {
        // Arrange
        var testContent = "Hello, World!";
        var testPath = "test.txt";
        
        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(testContent)
            });

        var fileSystem = new BlobFileSystem(_options, _httpClient);

        // Act
        var result = await fileSystem.ReadAllTextAsync(testPath);

        // Assert
        Assert.Equal(testContent, result);
    }

    [Fact]
    public async Task ReadAllTextAsync_ThrowsFileNotFoundException_WhenFileNotFound()
    {
        // Arrange
        var testPath = "nonexistent.txt";
        
        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.NotFound
            });

        var fileSystem = new BlobFileSystem(_options, _httpClient);

        // Act & Assert
        await Assert.ThrowsAsync<BlobFileNotFoundException>(() => 
            fileSystem.ReadAllTextAsync(testPath));
    }

    [Fact]
    public async Task ReadAllTextAsync_ThrowsArgumentException_WhenPathIsEmpty()
    {
        // Arrange
        var fileSystem = new BlobFileSystem(_options, _httpClient);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            fileSystem.ReadAllTextAsync(""));
    }

    [Fact]
    public async Task ReadAllBytesAsync_ReturnsBytes_WhenFileExists()
    {
        // Arrange
        var testContent = new byte[] { 1, 2, 3, 4, 5 };
        var testPath = "test.bin";
        
        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new ByteArrayContent(testContent)
            });

        var fileSystem = new BlobFileSystem(_options, _httpClient);

        // Act
        var result = await fileSystem.ReadAllBytesAsync(testPath);

        // Assert
        Assert.Equal(testContent, result);
    }

    [Fact]
    public async Task OpenReadAsync_ReturnsStream_WhenFileExists()
    {
        // Arrange
        var testContent = "Stream content";
        var testPath = "test.txt";
        
        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(testContent)
            });

        var fileSystem = new BlobFileSystem(_options, _httpClient);

        // Act
        using var stream = await fileSystem.OpenReadAsync(testPath);
        using var reader = new StreamReader(stream);
        var result = await reader.ReadToEndAsync();

        // Assert
        Assert.Equal(testContent, result);
    }

    [Fact]
    public async Task ExistsAsync_ReturnsTrue_WhenFileExists()
    {
        // Arrange
        var testPath = "test.txt";
        
        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Head),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK
            });

        var fileSystem = new BlobFileSystem(_options, _httpClient);

        // Act
        var result = await fileSystem.ExistsAsync(testPath);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task ExistsAsync_ReturnsFalse_WhenFileNotFound()
    {
        // Arrange
        var testPath = "nonexistent.txt";
        
        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Head),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.NotFound
            });

        var fileSystem = new BlobFileSystem(_options, _httpClient);

        // Act
        var result = await fileSystem.ExistsAsync(testPath);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ListAsync_CallsCorrectEndpoint()
    {
        // Arrange
        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => 
                    req.Method == HttpMethod.Get && 
                    req.RequestUri!.AbsolutePath.Contains("list")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("[]")
            });

        var fileSystem = new BlobFileSystem(_options, _httpClient);

        // Act
        var result = await fileSystem.ListAsync();

        // Assert
        Assert.NotNull(result);
        _mockHttpMessageHandler.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(req => 
                req.Method == HttpMethod.Get && 
                req.RequestUri!.AbsolutePath.Contains("list")),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task ReadAllTextAsync_RetriesOnTransientFailure()
    {
        // Arrange
        var testContent = "Success after retry";
        var testPath = "test.txt";
        var callCount = 0;
        
        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                callCount++;
                if (callCount == 1)
                {
                    return new HttpResponseMessage
                    {
                        StatusCode = HttpStatusCode.InternalServerError
                    };
                }
                return new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(testContent)
                };
            });

        var fileSystem = new BlobFileSystem(_options, _httpClient);

        // Act
        var result = await fileSystem.ReadAllTextAsync(testPath);

        // Assert
        Assert.Equal(testContent, result);
        Assert.Equal(2, callCount);
    }

    [Fact]
    public async Task ReadAllTextAsync_ThrowsNetworkException_AfterMaxRetries()
    {
        // Arrange
        var testPath = "test.txt";
        
        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.InternalServerError
            });

        var fileSystem = new BlobFileSystem(_options, _httpClient);

        // Act & Assert
        await Assert.ThrowsAsync<NetworkException>(() => 
            fileSystem.ReadAllTextAsync(testPath));
    }

    [Fact]
    public async Task ReadAllTextAsync_BuildsCorrectUrlWithSasToken()
    {
        // Arrange
        var testPath = "folder/test.txt";
        HttpRequestMessage? capturedRequest = null;
        
        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, ct) => capturedRequest = req)
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("test")
            });

        var fileSystem = new BlobFileSystem(_options, _httpClient);

        // Act
        await fileSystem.ReadAllTextAsync(testPath);

        // Assert
        Assert.NotNull(capturedRequest);
        var url = capturedRequest.RequestUri!.ToString();
        Assert.Contains(_options.BaseUrl, url);
        Assert.Contains(testPath, url);
        Assert.Contains(_options.SasToken.TrimStart('?'), url);
    }
}
