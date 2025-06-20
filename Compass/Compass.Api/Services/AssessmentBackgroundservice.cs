using Compass.Core.Services;

namespace Compass.Api.Services;

public class AssessmentBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AssessmentBackgroundService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(1);

    public AssessmentBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<AssessmentBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Assessment Background Service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Create a new scope for each processing cycle to avoid DbContext disposal issues
                using var scope = _serviceProvider.CreateScope();
                var orchestrator = scope.ServiceProvider.GetRequiredService<IAssessmentOrchestrator>();

                await orchestrator.ProcessPendingAssessmentsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing pending assessments");
            }

            // Wait before checking again
            try
            {
                await Task.Delay(_checkInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Expected when the service is stopping
                break;
            }
        }

        _logger.LogInformation("Assessment Background Service stopped");
    }
}