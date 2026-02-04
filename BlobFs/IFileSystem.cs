namespace BlobFs;

/// <summary>
/// Abstraction layer for filesystem operations
/// </summary>
public interface IFileSystem
{
    /// <summary>
    /// Lists all files in the filesystem
    /// </summary>
    /// <returns>Collection of file paths</returns>
    Task<IEnumerable<string>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the entire content of a file as a string
    /// </summary>
    /// <param name="path">Path to the file</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>File content as string</returns>
    Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the entire content of a file as a byte array
    /// </summary>
    /// <param name="path">Path to the file</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>File content as byte array</returns>
    Task<byte[]> ReadAllBytesAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens a stream to read from a file
    /// </summary>
    /// <param name="path">Path to the file</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Stream to read the file content</returns>
    Task<Stream> OpenReadAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a file exists
    /// </summary>
    /// <param name="path">Path to the file</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if the file exists, false otherwise</returns>
    Task<bool> ExistsAsync(string path, CancellationToken cancellationToken = default);
}
