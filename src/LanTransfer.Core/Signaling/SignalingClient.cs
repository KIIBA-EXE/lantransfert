using System.Net.Http.Json;
using System.Text.Json;

namespace LanTransfer.Core.Signaling;

/// <summary>
/// Client for communicating with the signaling server.
/// Handles code registration and peer lookup.
/// </summary>
public class SignalingClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly string _serverUrl;
    private string? _currentCode;
    private CancellationTokenSource? _refreshCts;
    private bool _disposed;
    
    /// <summary>
    /// Event raised when a code is obtained.
    /// </summary>
    public event EventHandler<string>? CodeObtained;
    
    /// <summary>
    /// Current share code, or null if not registered.
    /// </summary>
    public string? CurrentCode => _currentCode;
    
    /// <summary>
    /// Creates a new signaling client.
    /// </summary>
    /// <param name="serverUrl">URL of the signaling server.</param>
    public SignalingClient(string serverUrl = "http://localhost:3000")
    {
        _serverUrl = serverUrl.TrimEnd('/');
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10)
        };
    }
    
    /// <summary>
    /// Registers with the signaling server and obtains a share code.
    /// </summary>
    /// <param name="port">Local TCP port.</param>
    /// <param name="name">Display name.</param>
    /// <returns>The share code.</returns>
    public async Task<string?> RegisterAsync(int port, string name)
    {
        try
        {
            var response = await _httpClient.PostAsync(
                $"{_serverUrl}/register?port={port}&name={Uri.EscapeDataString(name)}",
                null
            );
            
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"[Signaling] Registration failed: {response.StatusCode}");
                return null;
            }
            
            var result = await response.Content.ReadFromJsonAsync<RegisterResponse>();
            
            if (result?.Code != null)
            {
                _currentCode = result.Code;
                Console.WriteLine($"[Signaling] Registered with code: {_currentCode}");
                
                // Start refresh loop
                StartRefreshLoop();
                
                CodeObtained?.Invoke(this, _currentCode);
                return _currentCode;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Signaling] Registration error: {ex.Message}");
        }
        
        return null;
    }
    
    /// <summary>
    /// Looks up a peer by their share code.
    /// </summary>
    /// <param name="code">The share code to lookup.</param>
    /// <returns>Peer info or null if not found.</returns>
    public async Task<PeerInfo?> LookupAsync(string code)
    {
        try
        {
            var response = await _httpClient.GetAsync(
                $"{_serverUrl}/lookup?code={Uri.EscapeDataString(code.ToUpperInvariant())}"
            );
            
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"[Signaling] Lookup failed: {response.StatusCode}");
                return null;
            }
            
            var result = await response.Content.ReadFromJsonAsync<PeerInfo>();
            
            if (result != null)
            {
                Console.WriteLine($"[Signaling] Found peer: {result.Name} at {result.Ip}:{result.Port}");
                return result;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Signaling] Lookup error: {ex.Message}");
        }
        
        return null;
    }
    
    /// <summary>
    /// Unregisters from the signaling server.
    /// </summary>
    public async Task UnregisterAsync()
    {
        _refreshCts?.Cancel();
        
        if (_currentCode == null) return;
        
        try
        {
            await _httpClient.PostAsync(
                $"{_serverUrl}/unregister?code={_currentCode}",
                null
            );
            Console.WriteLine($"[Signaling] Unregistered: {_currentCode}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Signaling] Unregister error: {ex.Message}");
        }
        
        _currentCode = null;
    }
    
    /// <summary>
    /// Starts the refresh loop to keep the registration alive.
    /// </summary>
    private void StartRefreshLoop()
    {
        _refreshCts?.Cancel();
        _refreshCts = new CancellationTokenSource();
        
        Task.Run(async () =>
        {
            while (!_refreshCts.Token.IsCancellationRequested && _currentCode != null)
            {
                try
                {
                    // Refresh every 2 minutes (before 5 minute expiry)
                    await Task.Delay(TimeSpan.FromMinutes(2), _refreshCts.Token);
                    
                    await _httpClient.PostAsync(
                        $"{_serverUrl}/refresh?code={_currentCode}",
                        null
                    );
                    
                    Console.WriteLine($"[Signaling] Refreshed: {_currentCode}");
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Signaling] Refresh error: {ex.Message}");
                }
            }
        });
    }
    
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        
        _refreshCts?.Cancel();
        _refreshCts?.Dispose();
        _httpClient.Dispose();
        
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Response from the register endpoint.
/// </summary>
public class RegisterResponse
{
    public string? Code { get; set; }
    public int ExpiresIn { get; set; }
}

/// <summary>
/// Peer information from the lookup endpoint.
/// </summary>
public class PeerInfo
{
    public string? Ip { get; set; }
    public int Port { get; set; }
    public string? Name { get; set; }
}
