namespace PortfolioChat.Services;

public class LogService
{
    private readonly ILogger<LogService> _logger;

    public LogService(ILogger<LogService> logger)
    {
        _logger = logger;
    }

    public void LogInfo(string message)
    {
        _logger.LogInformation(message);
    }

    public void LogError(string message, Exception ex)
    {
        _logger.LogError(ex, message);
    }

    public void LogWarning(string message)
    {
        _logger.LogWarning(message);
    }

    public void LogDebug(string message)
    {
        _logger.LogDebug(message);
    }

    public void LogCritical(string message, Exception ex)
    {
        _logger.LogCritical(ex, message);
    }

    public void LogTrace(string message)
    {
        _logger.LogTrace(message);
    }
}