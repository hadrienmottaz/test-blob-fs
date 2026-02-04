namespace BlobFs;

/// <summary>
/// Custom exceptions for filesystem operations
/// </summary>
public class FileSystemException : Exception
{
    public FileSystemException(string message) : base(message) { }
    public FileSystemException(string message, Exception innerException) : base(message, innerException) { }
}

public class BlobFileNotFoundException : FileSystemException
{
    public string Path { get; }
    
    public BlobFileNotFoundException(string path) 
        : base($"File not found: {path}")
    {
        Path = path;
    }
    
    public BlobFileNotFoundException(string path, Exception innerException) 
        : base($"File not found: {path}", innerException)
    {
        Path = path;
    }
}

public class NetworkException : FileSystemException
{
    public NetworkException(string message) : base(message) { }
    public NetworkException(string message, Exception innerException) : base(message, innerException) { }
}
