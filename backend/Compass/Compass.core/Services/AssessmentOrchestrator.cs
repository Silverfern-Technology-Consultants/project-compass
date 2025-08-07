using Compass.Core.Models;
using Compass.Core.Models.Assessment;
using Compass.Core.Interfaces;
using Compass.Data.Entities;
using Microsoft.Extensions.Logging;
using Compass.Data.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

namespace Compass.Core.Services;

public interface IAssessmentOrchestrator
{
    Task<Guid> StartAssessmentAsync(AssessmentRequest request, CancellationToken cancellationToken = default);
    Task<AssessmentResult?> GetAssessmentResultAsync(Guid assessmentId, CancellationToken cancellationToken = default);
    Task ProcessPendingAssessmentsAsync(CancellationToken cancellationToken = default);
}

public class AssessmentOrchestrator : IAssessmentOrchestrator
{
    private readonly IAzureResourceGraphService _resourceGraphService;
    private readonly INamingConventionAnalyzer _namingAnalyzer;
    private readonly ITaggingAnalyzer _taggingAnalyzer;
    private readonly IDependencyAnalyzer _dependencyAnalyzer;
    private readonly IIdentityAccessAssessmentAnalyzer _identityAccessAnalyzer;
    private readonly IAssessmentRepository _assessmentRepository;
    private readonly IOAuthService _oauthService;
    private readonly IClientPreferencesRepository _clientPreferencesRepository;
    private readonly ILogger<AssessmentOrchestrator> _logger;
    private readonly IBusinessContinuityAssessmentAnalyzer _businessContinuityAnalyzer;
    private readonly ISecurityPostureAssessmentAnalyzer _securityPostureAnalyzer;
    private readonly IServiceProvider _serviceProvider;

    public AssessmentOrchestrator(
        IAzureResourceGraphService resourceGraphService,
        INamingConventionAnalyzer namingAnalyzer,
        ITaggingAnalyzer taggingAnalyzer,
        IDependencyAnalyzer dependencyAnalyzer,
        IIdentityAccessAssessmentAnalyzer identityAccessAnalyzer,
        IBusinessContinuityAssessmentAnalyzer businessContinuityAnalyzer,
        ISecurityPostureAssessmentAnalyzer securityPostureAnalyzer,
        IAssessmentRepository assessmentRepository,
        IOAuthService oauthService,
        IClientPreferencesRepository clientPreferencesRepository,
        IServiceProvider serviceProvider,
        ILogger<AssessmentOrchestrator> logger)
    {
        _resourceGraphService = resourceGraphService;
        _namingAnalyzer = namingAnalyzer;
        _taggingAnalyzer = taggingAnalyzer;
        _dependencyAnalyzer = dependencyAnalyzer;
        _identityAccessAnalyzer = identityAccessAnalyzer;
        _businessContinuityAnalyzer = businessContinuityAnalyzer;
        _securityPostureAnalyzer = securityPostureAnalyzer;
        _assessmentRepository = assessmentRepository;
        _oauthService = oauthService;
        _clientPreferencesRepository = clientPreferencesRepository;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    private async Task ProcessResourceGovernanceAssessmentInScopeAsync(
        IServiceProvider scopedServices,
        Guid assessmentId,
        AssessmentRequest request,
        AzureEnvironment environment,
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing PURE Resource Governance assessment {AssessmentId} with scoped services - ONLY naming and tagging", assessmentId);

        // Get scoped services
        var assessmentRepository = scopedServices.GetRequiredService<IAssessmentRepository>();
        var resourceGraphService = scopedServices.GetRequiredService<IAzureResourceGraphService>();
        var oauthService = scopedServices.GetRequiredService<IOAuthService>();
        var clientPreferencesRepository = scopedServices.GetRequiredService<IClientPreferencesRepository>();
        var namingAnalyzer = scopedServices.GetRequiredService<INamingConventionAnalyzer>();
        var taggingAnalyzer = scopedServices.GetRequiredService<ITaggingAnalyzer>();
        var dependencyAnalyzer = scopedServices.GetRequiredService<IDependencyAnalyzer>();

        // Get client preferences for preference-aware analysis (GOVERNANCE ONLY)
        ClientAssessmentConfiguration? clientConfig = null;
        if (environment.ClientId.HasValue && request.UseClientPreferences)
        {
            try
            {
                var clientPreferences = await clientPreferencesRepository.GetByClientIdAsync(environment.ClientId.Value, organizationId);
                if (clientPreferences != null)
                {
                    clientConfig = MapToClientConfig(clientPreferences);
                    _logger.LogInformation("Using client preferences for GOVERNANCE assessment {AssessmentId}, client {ClientId}",
                        assessmentId, environment.ClientId.Value);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to retrieve client preferences for assessment {AssessmentId}", assessmentId);
            }
        }

        // Collect Azure resources using scoped services
        var resources = await CollectAzureResourcesWithScopedServicesAsync(resourceGraphService, oauthService, environment, organizationId, cancellationToken);

        if (!resources.Any())
        {
            _logger.LogWarning("No resources found for Resource Governance assessment {AssessmentId}", assessmentId);
            await assessmentRepository.UpdateAssessmentAsync(assessmentId, 100m, AssessmentStatus.Completed.ToString(), DateTime.UtcNow);
            return;
        }

        // Save resources to database using scoped repository
        await SaveAssessmentResourcesWithScopedServicesAsync(assessmentRepository, assessmentId, resources);

        // Run ONLY governance analyses based on assessment type
        NamingConventionResults? namingResults = null;
        TaggingResults? taggingResults = null;
        DependencyAnalysisResults? dependencyResults = null;

        // STRICT: Only analyze what the assessment type requests
        if (request.Type == AssessmentType.NamingConvention || request.Type == AssessmentType.GovernanceFull || request.Type == AssessmentType.Full)
        {
            _logger.LogInformation("Running naming convention analysis for governance assessment");
            if (clientConfig != null && namingAnalyzer is IPreferenceAwareNamingAnalyzer preferenceAwareAnalyzer)
            {
                namingResults = await preferenceAwareAnalyzer.AnalyzeNamingConventionsAsync(resources, clientConfig, cancellationToken);
            }
            else
            {
                namingResults = await namingAnalyzer.AnalyzeNamingConventionsAsync(resources, cancellationToken);
            }
        }

        if (request.Type == AssessmentType.Tagging || request.Type == AssessmentType.GovernanceFull || request.Type == AssessmentType.Full)
        {
            _logger.LogInformation("Running tagging analysis for governance assessment");
            if (clientConfig != null && taggingAnalyzer is IPreferenceAwareTaggingAnalyzer preferenceAwareTagAnalyzer)
            {
                taggingResults = await preferenceAwareTagAnalyzer.AnalyzeTaggingAsync(resources, clientConfig, cancellationToken);
            }
            else
            {
                taggingResults = await taggingAnalyzer.AnalyzeTaggingAsync(resources, cancellationToken);
            }
        }

        // Always run dependency analysis for comprehensive insights
        dependencyResults = await dependencyAnalyzer.AnalyzeDependenciesAsync(resources, cancellationToken);

        // Calculate overall score
        var overallScore = CalculateGovernanceScore(namingResults, taggingResults, request.Type);

        // Save findings using scoped services
        await SaveGovernanceAssessmentFindingsWithScopedServicesAsync(assessmentRepository, assessmentId, namingResults, taggingResults, dependencyResults, clientConfig);

        // Complete assessment
        await assessmentRepository.UpdateAssessmentAsync(assessmentId, overallScore, AssessmentStatus.Completed.ToString(), DateTime.UtcNow);

        _logger.LogInformation("PURE Resource Governance assessment {AssessmentId} completed with score {Score}%", assessmentId, overallScore);
    }

    private async Task ProcessBusinessContinuityAssessmentInScopeAsync(
        IServiceProvider scopedServices,
        Guid assessmentId,
        AssessmentRequest request,
        AzureEnvironment environment,
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing PURE Business Continuity assessment {AssessmentId} with scoped services - ONLY BCDR, no governance or IAM", assessmentId);

        // Get scoped services
        var assessmentRepository = scopedServices.GetRequiredService<IAssessmentRepository>();
        var businessContinuityAnalyzer = scopedServices.GetRequiredService<IBusinessContinuityAssessmentAnalyzer>();

        try
        {
            // Run BCDR analysis using the scoped analyzer
            var bcdrResults = await businessContinuityAnalyzer.AnalyzeBusinessContinuityAsync(
                request.SubscriptionIds,
                request.Type,
                cancellationToken);

            // Save BCDR-specific findings using scoped repository
            await SaveBusinessContinuityFindingsWithScopedServicesAsync(assessmentRepository, assessmentId, bcdrResults);

            // Complete assessment
            await assessmentRepository.UpdateAssessmentAsync(assessmentId, bcdrResults.Score, AssessmentStatus.Completed.ToString(), DateTime.UtcNow);

            _logger.LogInformation("PURE Business Continuity assessment {AssessmentId} completed with score {Score}%", assessmentId, bcdrResults.Score);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process BCDR assessment {AssessmentId}", assessmentId);
            throw;
        }
    }

    private async Task ProcessSecurityPostureAssessmentInScopeAsync(
        IServiceProvider scopedServices,
        Guid assessmentId,
        AssessmentRequest request,
        AzureEnvironment environment,
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing PURE Security Posture assessment {AssessmentId} with scoped services - ONLY security posture, no governance or IAM", assessmentId);

        // Get scoped services
        var assessmentRepository = scopedServices.GetRequiredService<IAssessmentRepository>();
        var securityPostureAnalyzer = scopedServices.GetRequiredService<ISecurityPostureAssessmentAnalyzer>();
        var oauthService = scopedServices.GetRequiredService<IOAuthService>();

        try
        {
            SecurityPostureResults securityResults;

            // Use OAuth-enhanced analysis if available
            if (environment.ClientId.HasValue)
            {
                try
                {
                    var hasOAuthCredentials = await oauthService.TestCredentialsAsync(environment.ClientId.Value, organizationId);
                    if (hasOAuthCredentials)
                    {
                        _logger.LogInformation("Using OAuth-enhanced Security Posture analysis for assessment {AssessmentId}", assessmentId);
                        securityResults = await securityPostureAnalyzer.AnalyzeSecurityPostureWithOAuthAsync(
                            request.SubscriptionIds,
                            environment.ClientId.Value,
                            organizationId,
                            request.Type,
                            cancellationToken);
                    }
                    else
                    {
                        _logger.LogInformation("OAuth not available, using standard Security Posture analysis for assessment {AssessmentId}", assessmentId);
                        securityResults = await securityPostureAnalyzer.AnalyzeSecurityPostureAsync(
                            request.SubscriptionIds,
                            request.Type,
                            cancellationToken);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "OAuth Security Posture analysis failed, falling back to standard analysis");
                    securityResults = await securityPostureAnalyzer.AnalyzeSecurityPostureAsync(
                        request.SubscriptionIds,
                        request.Type,
                        cancellationToken);
                }
            }
            else
            {
                _logger.LogInformation("No client context, using standard Security Posture analysis for assessment {AssessmentId}", assessmentId);
                securityResults = await securityPostureAnalyzer.AnalyzeSecurityPostureAsync(
                    request.SubscriptionIds,
                    request.Type,
                    cancellationToken);
            }

            // Save Security-specific findings using scoped repository
            await SaveSecurityPostureFindingsWithScopedServicesAsync(assessmentRepository, assessmentId, securityResults);

            // Complete assessment
            await assessmentRepository.UpdateAssessmentAsync(assessmentId, securityResults.Score, AssessmentStatus.Completed.ToString(), DateTime.UtcNow);

            _logger.LogInformation("PURE Security Posture assessment {AssessmentId} completed with score {Score}%", assessmentId, securityResults.Score);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process Security Posture assessment {AssessmentId}", assessmentId);
            throw;
        }
    }

    private async Task ProcessIdentityAccessManagementAssessmentInScopeAsync(
        IServiceProvider scopedServices,
        Guid assessmentId,
        AssessmentRequest request,
        AzureEnvironment environment,
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing SCOPED PURE Identity Access Management assessment {AssessmentId} - ONLY IAM, no governance", assessmentId);

        var assessmentRepository = scopedServices.GetRequiredService<IAssessmentRepository>();
        var identityAccessAnalyzer = scopedServices.GetRequiredService<IIdentityAccessAssessmentAnalyzer>();
        var oauthService = scopedServices.GetRequiredService<IOAuthService>();

        try
        {
            IdentityAccessResults iamResults;

            // Use OAuth-enhanced analysis if available
            if (environment.ClientId.HasValue)
            {
                try
                {
                    var hasOAuthCredentials = await oauthService.TestCredentialsAsync(environment.ClientId.Value, organizationId);
                    if (hasOAuthCredentials)
                    {
                        _logger.LogInformation("Using OAuth-enhanced IAM analysis for SCOPED assessment {AssessmentId}", assessmentId);
                        iamResults = await identityAccessAnalyzer.AnalyzeIdentityAccessWithOAuthAsync(
                            request.SubscriptionIds,
                            environment.ClientId.Value,
                            organizationId,
                            request.Type,
                            cancellationToken);
                    }
                    else
                    {
                        _logger.LogInformation("OAuth not available, using standard IAM analysis for SCOPED assessment {AssessmentId}", assessmentId);
                        iamResults = await identityAccessAnalyzer.AnalyzeIdentityAccessAsync(
                            request.SubscriptionIds,
                            request.Type,
                            cancellationToken);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "OAuth IAM analysis failed, falling back to standard analysis");
                    iamResults = await identityAccessAnalyzer.AnalyzeIdentityAccessAsync(
                        request.SubscriptionIds,
                        request.Type,
                        cancellationToken);
                }
            }
            else
            {
                _logger.LogInformation("No client context, using standard IAM analysis for SCOPED assessment {AssessmentId}", assessmentId);
                iamResults = await identityAccessAnalyzer.AnalyzeIdentityAccessAsync(
                    request.SubscriptionIds,
                    request.Type,
                    cancellationToken);
            }

            // Save IAM-specific findings
            await SaveIdentityAccessFindingsInScopeAsync(scopedServices, assessmentId, iamResults);

            // Complete assessment
            await assessmentRepository.UpdateAssessmentAsync(assessmentId, iamResults.Score, AssessmentStatus.Completed.ToString(), DateTime.UtcNow);

            _logger.LogInformation("SCOPED PURE Identity Access Management assessment {AssessmentId} completed with score {Score}%", assessmentId, iamResults.Score);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process SCOPED IAM assessment {AssessmentId}", assessmentId);
            throw;
        }
    }

    public async Task<Guid> StartAssessmentAsync(AssessmentRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting category-separated assessment for organization {OrganizationId}, type {AssessmentType}",
            request.OrganizationId, request.Type);

        // Determine assessment category
        var category = AssessmentModelStructure.GetCategory(request.Type);

        _logger.LogInformation("Creating assessment with Type: {Type} ({TypeInt}), Category: {Category}",
            request.Type, (int)request.Type, category);

        var assessment = new Assessment
        {
            Id = Guid.NewGuid(),
            EnvironmentId = request.EnvironmentId,
            Name = request.Name,
            AssessmentType = request.Type.ToString(),
            AssessmentCategory = category.ToString(),
            Status = AssessmentStatus.Pending.ToString(),
            StartedDate = DateTime.UtcNow,
            CustomerId = request.CustomerId,
            OrganizationId = request.OrganizationId,
            CustomerName = "Organization Member"
        };

        await _assessmentRepository.CreateAsync(assessment);

        // Assessment is now queued for background processing - return immediately
        _logger.LogInformation("Assessment {AssessmentId} queued for background processing", assessment.Id);
        
        // Start background processing with new service scope (fire and forget)
        _ = Task.Run(async () =>
        {
            try
            {
                // Create a new scope to avoid DbContext threading issues
                using var scope = _serviceProvider.CreateScope();
                var scopedOrchestrator = scope.ServiceProvider.GetRequiredService<IAssessmentOrchestrator>();
                
                // Use ProcessCategorizedAssessmentInScope which will use the scoped services
                await ProcessCategorizedAssessmentInScopeAsync(scope.ServiceProvider, assessment.Id, request, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process categorized assessment {AssessmentId} for organization {OrganizationId}",
                    assessment.Id, request.OrganizationId);
                
                // Try to mark as failed using a new scope
                try
                {
                    using var errorScope = _serviceProvider.CreateScope();
                    var scopedRepository = errorScope.ServiceProvider.GetRequiredService<IAssessmentRepository>();
                    await scopedRepository.UpdateStatusAsync(assessment.Id, AssessmentStatus.Failed.ToString());
                }
                catch (Exception markFailedEx)
                {
                    _logger.LogError(markFailedEx, "Failed to mark assessment {AssessmentId} as failed", assessment.Id);
                }
            }
        });

        return assessment.Id;
    }

    public async Task<AssessmentResult?> GetAssessmentResultAsync(Guid assessmentId, CancellationToken cancellationToken = default)
    {
        var assessment = await _assessmentRepository.GetByIdAsync(assessmentId);
        if (assessment == null) return null;

        var findings = await _assessmentRepository.GetFindingsByAssessmentIdAsync(assessmentId);

        return MapToEnhancedAssessmentResult(assessment, findings);
    }

    public async Task ProcessPendingAssessmentsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var pendingAssessments = await _assessmentRepository.GetPendingAssessmentsAsync();
            
            if (!pendingAssessments.Any())
            {
                _logger.LogDebug("No pending assessments found");
                return;
            }

            _logger.LogInformation("Found {Count} pending assessments to process", pendingAssessments.Count);

            foreach (var assessment in pendingAssessments)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                try
                {
                    _logger.LogInformation("Processing pending assessment {AssessmentId}", assessment.Id);
                    
                    // Create assessment request from stored assessment
                    var request = new AssessmentRequest
                    {
                        CustomerId = assessment.CustomerId,
                        OrganizationId = assessment.OrganizationId,
                        EnvironmentId = assessment.EnvironmentId,
                        Name = assessment.Name,
                        Type = Enum.Parse<AssessmentType>(assessment.AssessmentType),
                        SubscriptionIds = new string[0], // Will be resolved from environment
                        UseClientPreferences = true // Default to true for background processing
                    };

                    await ProcessCategorizedAssessmentAsync(assessment.Id, request, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to process pending assessment {AssessmentId}", assessment.Id);
                    await MarkAssessmentAsFailed(assessment.Id, ex.Message);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ProcessPendingAssessmentsAsync");
        }
    }

    private async Task ProcessCategorizedAssessmentInScopeAsync(
        IServiceProvider scopedServices, 
        Guid assessmentId, 
        AssessmentRequest request, 
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing SCOPED categorized assessment {AssessmentId} with type {AssessmentType} in category {Category}",
            assessmentId, request.Type, AssessmentModelStructure.GetCategory(request.Type));

        // Get scoped services
        var assessmentRepository = scopedServices.GetRequiredService<IAssessmentRepository>();
        var resourceGraphService = scopedServices.GetRequiredService<IAzureResourceGraphService>();
        var oauthService = scopedServices.GetRequiredService<IOAuthService>();
        var clientPreferencesRepository = scopedServices.GetRequiredService<IClientPreferencesRepository>();
        var namingAnalyzer = scopedServices.GetRequiredService<INamingConventionAnalyzer>();
        var taggingAnalyzer = scopedServices.GetRequiredService<ITaggingAnalyzer>();
        var dependencyAnalyzer = scopedServices.GetRequiredService<IDependencyAnalyzer>();
        var identityAccessAnalyzer = scopedServices.GetRequiredService<IIdentityAccessAssessmentAnalyzer>();
        var businessContinuityAnalyzer = scopedServices.GetRequiredService<IBusinessContinuityAssessmentAnalyzer>();
        var securityPostureAnalyzer = scopedServices.GetRequiredService<ISecurityPostureAssessmentAnalyzer>();

        // Update status to InProgress
        await assessmentRepository.UpdateStatusAsync(assessmentId, AssessmentStatus.InProgress.ToString());

        // Get environment details
        var environment = await assessmentRepository.GetEnvironmentByIdAsync(request.EnvironmentId);
        if (environment == null)
        {
            _logger.LogError("Environment {EnvironmentId} not found for assessment {AssessmentId}",
                request.EnvironmentId, assessmentId);
            await assessmentRepository.UpdateStatusAsync(assessmentId, AssessmentStatus.Failed.ToString());
            return;
        }

        if (!request.OrganizationId.HasValue)
        {
            _logger.LogError("OrganizationId is null for assessment {AssessmentId}", assessmentId);
            await assessmentRepository.UpdateStatusAsync(assessmentId, AssessmentStatus.Failed.ToString());
            return;
        }

        var organizationId = request.OrganizationId.Value;
        var category = AssessmentModelStructure.GetCategory(request.Type);

        try
        {
            switch (category)
            {
                case AssessmentCategory.ResourceGovernance:
                    _logger.LogInformation("Processing SCOPED GOVERNANCE-ONLY assessment - no security or IAM analysis");
                    await ProcessResourceGovernanceAssessmentInScopeAsync(scopedServices, assessmentId, request, environment, organizationId, cancellationToken);
                    break;

                case AssessmentCategory.IdentityAccessManagement:
                    _logger.LogInformation("Processing SCOPED IAM-ONLY assessment - no governance or security posture analysis");
                    await ProcessIdentityAccessManagementAssessmentInScopeAsync(scopedServices, assessmentId, request, environment, organizationId, cancellationToken);
                    break;

                case AssessmentCategory.BusinessContinuity:
                    _logger.LogInformation("Processing SCOPED BCDR-ONLY assessment - no governance, IAM, or security posture analysis");
                    await ProcessBusinessContinuityAssessmentInScopeAsync(scopedServices, assessmentId, request, environment, organizationId, cancellationToken);
                    break;

                case AssessmentCategory.SecurityPosture:
                    _logger.LogInformation("Processing SCOPED SECURITY POSTURE-ONLY assessment - no governance or IAM analysis");
                    await ProcessSecurityPostureAssessmentInScopeAsync(scopedServices, assessmentId, request, environment, organizationId, cancellationToken);
                    break;

                default:
                    throw new ArgumentException($"Unsupported assessment category: {category}");
            }

            _logger.LogInformation("SCOPED categorized assessment {AssessmentId} completed successfully in category {Category} with strict separation",
                assessmentId, category);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process SCOPED {Category} assessment {AssessmentId}", category, assessmentId);
            await assessmentRepository.UpdateStatusAsync(assessmentId, AssessmentStatus.Failed.ToString());
        }
    }

    private async Task ProcessCategorizedAssessmentAsync(Guid assessmentId, AssessmentRequest request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing STRICTLY categorized assessment {AssessmentId} with type {AssessmentType} in category {Category}",
            assessmentId, request.Type, AssessmentModelStructure.GetCategory(request.Type));

        // Update status to InProgress
        await _assessmentRepository.UpdateStatusAsync(assessmentId, AssessmentStatus.InProgress.ToString());

        // Get environment details
        var environment = await _assessmentRepository.GetEnvironmentByIdAsync(request.EnvironmentId);
        if (environment == null)
        {
            _logger.LogError("Environment {EnvironmentId} not found for assessment {AssessmentId}",
                request.EnvironmentId, assessmentId);
            await MarkAssessmentAsFailed(assessmentId, "Environment not found");
            return;
        }

        if (!request.OrganizationId.HasValue)
        {
            _logger.LogError("OrganizationId is null for assessment {AssessmentId}", assessmentId);
            await MarkAssessmentAsFailed(assessmentId, "Organization context not found");
            return;
        }

        var organizationId = request.OrganizationId.Value;

        // Determine what analysis to run based on assessment category - STRICT SEPARATION
        var category = AssessmentModelStructure.GetCategory(request.Type);

        try
        {
            switch (category)
            {
                case AssessmentCategory.ResourceGovernance:
                    _logger.LogInformation("Processing GOVERNANCE-ONLY assessment - no security or IAM analysis");
                    await ProcessResourceGovernanceAssessmentAsync(assessmentId, request, environment, organizationId, cancellationToken);
                    break;

                case AssessmentCategory.IdentityAccessManagement:
                    _logger.LogInformation("Processing IAM-ONLY assessment - no governance or security posture analysis");
                    await ProcessIdentityAccessManagementAssessmentAsync(assessmentId, request, environment, organizationId, cancellationToken);
                    break;

                case AssessmentCategory.BusinessContinuity:
                    _logger.LogInformation("Processing BCDR-ONLY assessment - no governance, IAM, or security posture analysis");
                    await ProcessBusinessContinuityAssessmentAsync(assessmentId, request, environment, organizationId, cancellationToken);
                    break;

                case AssessmentCategory.SecurityPosture:
                    _logger.LogInformation("Processing SECURITY POSTURE-ONLY assessment - no governance or IAM analysis");
                    await ProcessSecurityPostureAssessmentAsync(assessmentId, request, environment, organizationId, cancellationToken);
                    break;

                default:
                    throw new ArgumentException($"Unsupported assessment category: {category}");
            }

            _logger.LogInformation("Categorized assessment {AssessmentId} completed successfully in category {Category} with strict separation",
                assessmentId, category);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process {Category} assessment {AssessmentId}", category, assessmentId);
            await MarkAssessmentAsFailed(assessmentId, ex.Message);
        }
    }

    private async Task ProcessResourceGovernanceAssessmentAsync(
        Guid assessmentId,
        AssessmentRequest request,
        AzureEnvironment environment,
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing PURE Resource Governance assessment {AssessmentId} - ONLY naming and tagging", assessmentId);

        // Get client preferences for preference-aware analysis (GOVERNANCE ONLY)
        ClientAssessmentConfiguration? clientConfig = null;
        if (environment.ClientId.HasValue && request.UseClientPreferences)
        {
            try
            {
                var clientPreferences = await _clientPreferencesRepository.GetByClientIdAsync(environment.ClientId.Value, organizationId);
                if (clientPreferences != null)
                {
                    clientConfig = MapToClientConfig(clientPreferences);
                    _logger.LogInformation("Using client preferences for GOVERNANCE assessment {AssessmentId}, client {ClientId}",
                        assessmentId, environment.ClientId.Value);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to retrieve client preferences for assessment {AssessmentId}", assessmentId);
            }
        }

        // Collect Azure resources
        var resources = await CollectAzureResourcesAsync(environment, organizationId, cancellationToken);

        if (!resources.Any())
        {
            _logger.LogWarning("No resources found for Resource Governance assessment {AssessmentId}", assessmentId);
            await _assessmentRepository.UpdateAssessmentAsync(assessmentId, 100m, AssessmentStatus.Completed.ToString(), DateTime.UtcNow);
            return;
        }

        // Save resources to database
        await SaveAssessmentResourcesAsync(assessmentId, resources);

        // Run ONLY governance analyses based on assessment type
        NamingConventionResults? namingResults = null;
        TaggingResults? taggingResults = null;
        DependencyAnalysisResults? dependencyResults = null;

        // STRICT: Only analyze what the assessment type requests
        if (request.Type == AssessmentType.NamingConvention || request.Type == AssessmentType.GovernanceFull || request.Type == AssessmentType.Full)
        {
            _logger.LogInformation("Running naming convention analysis for governance assessment");
            if (clientConfig != null && _namingAnalyzer is IPreferenceAwareNamingAnalyzer preferenceAwareAnalyzer)
            {
                namingResults = await preferenceAwareAnalyzer.AnalyzeNamingConventionsAsync(resources, clientConfig, cancellationToken);
            }
            else
            {
                namingResults = await _namingAnalyzer.AnalyzeNamingConventionsAsync(resources, cancellationToken);
            }
        }

        if (request.Type == AssessmentType.Tagging || request.Type == AssessmentType.GovernanceFull || request.Type == AssessmentType.Full)
        {
            _logger.LogInformation("Running tagging analysis for governance assessment");
            if (clientConfig != null && _taggingAnalyzer is IPreferenceAwareTaggingAnalyzer preferenceAwareTagAnalyzer)
            {
                taggingResults = await preferenceAwareTagAnalyzer.AnalyzeTaggingAsync(resources, clientConfig, cancellationToken);
            }
            else
            {
                taggingResults = await _taggingAnalyzer.AnalyzeTaggingAsync(resources, cancellationToken);
            }
        }

        // Always run dependency analysis for comprehensive insights
        dependencyResults = await _dependencyAnalyzer.AnalyzeDependenciesAsync(resources, cancellationToken);

        // Calculate overall score
        var overallScore = CalculateGovernanceScore(namingResults, taggingResults, request.Type);

        // Save findings
        await SaveGovernanceAssessmentFindings(assessmentId, namingResults, taggingResults, dependencyResults, clientConfig);

        // Complete assessment
        await _assessmentRepository.UpdateAssessmentAsync(assessmentId, overallScore, AssessmentStatus.Completed.ToString(), DateTime.UtcNow);

        _logger.LogInformation("PURE Resource Governance assessment {AssessmentId} completed with score {Score}%", assessmentId, overallScore);
    }

    private async Task ProcessIdentityAccessManagementAssessmentAsync(
        Guid assessmentId,
        AssessmentRequest request,
        AzureEnvironment environment,
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing PURE Identity Access Management assessment {AssessmentId} - ONLY IAM, no governance", assessmentId);

        try
        {
            IdentityAccessResults iamResults;

            // Use OAuth-enhanced analysis if available
            if (environment.ClientId.HasValue)
            {
                try
                {
                    var hasOAuthCredentials = await _oauthService.TestCredentialsAsync(environment.ClientId.Value, organizationId);
                    if (hasOAuthCredentials)
                    {
                        _logger.LogInformation("Using OAuth-enhanced IAM analysis for assessment {AssessmentId}", assessmentId);
                        iamResults = await _identityAccessAnalyzer.AnalyzeIdentityAccessWithOAuthAsync(
                            request.SubscriptionIds,
                            environment.ClientId.Value,
                            organizationId,
                            request.Type,
                            cancellationToken);
                    }
                    else
                    {
                        _logger.LogInformation("OAuth not available, using standard IAM analysis for assessment {AssessmentId}", assessmentId);
                        iamResults = await _identityAccessAnalyzer.AnalyzeIdentityAccessAsync(
                            request.SubscriptionIds,
                            request.Type,
                            cancellationToken);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "OAuth IAM analysis failed, falling back to standard analysis");
                    iamResults = await _identityAccessAnalyzer.AnalyzeIdentityAccessAsync(
                        request.SubscriptionIds,
                        request.Type,
                        cancellationToken);
                }
            }
            else
            {
                _logger.LogInformation("No client context, using standard IAM analysis for assessment {AssessmentId}", assessmentId);
                iamResults = await _identityAccessAnalyzer.AnalyzeIdentityAccessAsync(
                    request.SubscriptionIds,
                    request.Type,
                    cancellationToken);
            }

            // Save IAM-specific findings
            await SaveIdentityAccessFindings(assessmentId, iamResults);

            // Complete assessment
            await _assessmentRepository.UpdateAssessmentAsync(assessmentId, iamResults.Score, AssessmentStatus.Completed.ToString(), DateTime.UtcNow);

            _logger.LogInformation("PURE Identity Access Management assessment {AssessmentId} completed with score {Score}%", assessmentId, iamResults.Score);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process IAM assessment {AssessmentId}", assessmentId);
            throw;
        }
    }

    private async Task ProcessBusinessContinuityAssessmentAsync(
        Guid assessmentId,
        AssessmentRequest request,
        AzureEnvironment environment,
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing PURE Business Continuity assessment {AssessmentId} - ONLY BCDR, no governance or IAM", assessmentId);

        try
        {
            // Run BCDR analysis using the real analyzer
            var bcdrResults = await _businessContinuityAnalyzer.AnalyzeBusinessContinuityAsync(
                request.SubscriptionIds,
                request.Type,
                cancellationToken);

            // Save BCDR-specific findings
            await SaveBusinessContinuityFindings(assessmentId, bcdrResults);

            // Complete assessment
            await _assessmentRepository.UpdateAssessmentAsync(assessmentId, bcdrResults.Score, AssessmentStatus.Completed.ToString(), DateTime.UtcNow);

            _logger.LogInformation("PURE Business Continuity assessment {AssessmentId} completed with score {Score}%", assessmentId, bcdrResults.Score);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process BCDR assessment {AssessmentId}", assessmentId);
            throw;
        }
    }

    private async Task ProcessSecurityPostureAssessmentAsync(
        Guid assessmentId,
        AssessmentRequest request,
        AzureEnvironment environment,
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing PURE Security Posture assessment {AssessmentId} - ONLY security posture, no governance or IAM", assessmentId);

        try
        {
            SecurityPostureResults securityResults;

            // Use OAuth-enhanced analysis if available
            if (environment.ClientId.HasValue)
            {
                try
                {
                    var hasOAuthCredentials = await _oauthService.TestCredentialsAsync(environment.ClientId.Value, organizationId);
                    if (hasOAuthCredentials)
                    {
                        _logger.LogInformation("Using OAuth-enhanced Security Posture analysis for assessment {AssessmentId}", assessmentId);
                        securityResults = await _securityPostureAnalyzer.AnalyzeSecurityPostureWithOAuthAsync(
                            request.SubscriptionIds,
                            environment.ClientId.Value,
                            organizationId,
                            request.Type,
                            cancellationToken);
                    }
                    else
                    {
                        _logger.LogInformation("OAuth not available, using standard Security Posture analysis for assessment {AssessmentId}", assessmentId);
                        securityResults = await _securityPostureAnalyzer.AnalyzeSecurityPostureAsync(
                            request.SubscriptionIds,
                            request.Type,
                            cancellationToken);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "OAuth Security Posture analysis failed, falling back to standard analysis");
                    securityResults = await _securityPostureAnalyzer.AnalyzeSecurityPostureAsync(
                        request.SubscriptionIds,
                        request.Type,
                        cancellationToken);
                }
            }
            else
            {
                _logger.LogInformation("No client context, using standard Security Posture analysis for assessment {AssessmentId}", assessmentId);
                securityResults = await _securityPostureAnalyzer.AnalyzeSecurityPostureAsync(
                    request.SubscriptionIds,
                    request.Type,
                    cancellationToken);
            }

            // Save Security-specific findings
            await SaveSecurityPostureFindings(assessmentId, securityResults);

            // Complete assessment
            await _assessmentRepository.UpdateAssessmentAsync(assessmentId, securityResults.Score, AssessmentStatus.Completed.ToString(), DateTime.UtcNow);

            _logger.LogInformation("PURE Security Posture assessment {AssessmentId} completed with score {Score}%", assessmentId, securityResults.Score);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process Security Posture assessment {AssessmentId}", assessmentId);
            throw;
        }
    }

    private async Task<List<AzureResource>> CollectAzureResourcesAsync(
        AzureEnvironment environment,
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        List<AzureResource> resources;

        if (environment.ClientId.HasValue)
        {
            try
            {
                var hasOAuthCredentials = await _oauthService.TestCredentialsAsync(environment.ClientId.Value, organizationId);
                if (hasOAuthCredentials)
                {
                    resources = await _resourceGraphService.GetResourcesWithOAuthAsync(
                        environment.SubscriptionIds.ToArray(),
                        environment.ClientId.Value,
                        organizationId,
                        cancellationToken);
                }
                else
                {
                    resources = await _resourceGraphService.GetResourcesAsync(environment.SubscriptionIds.ToArray(), cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "OAuth failed, falling back to default credentials");
                resources = await _resourceGraphService.GetResourcesAsync(environment.SubscriptionIds.ToArray(), cancellationToken);
            }
        }
        else
        {
            resources = await _resourceGraphService.GetResourcesAsync(environment.SubscriptionIds.ToArray(), cancellationToken);
        }

        return resources;
    }

    private async Task SaveAssessmentResourcesAsync(Guid assessmentId, List<AzureResource> resources)
    {
        if (!resources.Any()) return;

        var assessmentResources = resources.Select(r => new AssessmentResource
        {
            Id = Guid.NewGuid(),
            AssessmentId = assessmentId,
            ResourceId = r.Id,
            Name = r.Name,
            Type = r.Type,
            ResourceTypeName = r.ResourceTypeName,
            ResourceGroup = r.ResourceGroup,
            Location = r.Location,
            SubscriptionId = r.SubscriptionId,
            Kind = r.Kind,
            Sku = r.Sku,
            Tags = JsonSerializer.Serialize(r.Tags),
            TagCount = r.TagCount,
            Environment = r.Environment,
            Properties = r.Properties,
            CreatedAt = DateTime.UtcNow
        }).ToList();

        await _assessmentRepository.CreateResourcesAsync(assessmentResources);
    }

    private async Task SaveGovernanceAssessmentFindings(
        Guid assessmentId,
        NamingConventionResults? namingResults,
        TaggingResults? taggingResults,
        DependencyAnalysisResults? dependencyResults,
        ClientAssessmentConfiguration? clientConfig)
    {
        var findings = new List<AssessmentFinding>();

        // Save naming convention findings
        if (namingResults?.Violations != null)
        {
            foreach (var violation in namingResults.Violations)
            {
                var recommendation = $"Suggested name: {violation.SuggestedName}";
                if (clientConfig != null)
                {
                    recommendation = EnhanceRecommendationWithClientPreferences(violation, clientConfig);
                }

                findings.Add(new AssessmentFinding
                {
                    Id = Guid.NewGuid(),
                    AssessmentId = assessmentId,
                    Category = "NamingConvention",
                    ResourceId = violation.ResourceId,
                    ResourceName = violation.ResourceName,
                    ResourceType = violation.ResourceType,
                    Severity = violation.Severity,
                    Issue = violation.Issue,
                    Recommendation = recommendation,
                    EstimatedEffort = GetEffortEstimate(violation.ViolationType)
                });
            }
        }

        // Save tagging findings
        if (taggingResults?.Violations != null)
        {
            foreach (var violation in taggingResults.Violations)
            {
                var recommendation = violation.MissingTags.Any()
                    ? $"Add missing tags: {string.Join(", ", violation.MissingTags)}"
                    : "Review and fix tag values";

                if (clientConfig != null && clientConfig.HasTaggingPreferences)
                {
                    var clientTags = clientConfig.GetEffectiveRequiredTags();
                    if (clientTags.Any())
                    {
                        recommendation += $". Based on client preferences, prioritize these tags: {string.Join(", ", clientTags.Take(5))}";
                    }
                }

                findings.Add(new AssessmentFinding
                {
                    Id = Guid.NewGuid(),
                    AssessmentId = assessmentId,
                    Category = "Tagging",
                    ResourceId = violation.ResourceId,
                    ResourceName = violation.ResourceName,
                    ResourceType = violation.ResourceType,
                    Severity = violation.Severity,
                    Issue = violation.Issue,
                    Recommendation = recommendation,
                    EstimatedEffort = violation.MissingTags.Count > 3 ? "High" : "Medium"
                });
            }
        }

        // Save dependency findings (existing logic)
        if (dependencyResults != null)
        {
            // Add dependency analysis findings...
            // (Keep existing dependency finding logic from original orchestrator)
        }

        if (findings.Any())
        {
            await _assessmentRepository.CreateFindingsAsync(findings);
        }
    }

    private async Task SaveIdentityAccessFindingsInScopeAsync(IServiceProvider scopedServices, Guid assessmentId, IdentityAccessResults iamResults)
    {
        var assessmentRepository = scopedServices.GetRequiredService<IAssessmentRepository>();
        var findings = new List<AssessmentFinding>();

        foreach (var securityFinding in iamResults.SecurityFindings)
        {
            findings.Add(new AssessmentFinding
            {
                Id = Guid.NewGuid(),
                AssessmentId = assessmentId,
                Category = "IdentityAccess",
                ResourceId = securityFinding.ResourceId,
                ResourceName = securityFinding.ResourceName,
                ResourceType = securityFinding.FindingType,
                Severity = securityFinding.Severity,
                Issue = securityFinding.Description,
                Recommendation = securityFinding.Recommendation,
                EstimatedEffort = GetIamEffortEstimate(securityFinding.FindingType)
            });
        }

        if (findings.Any())
        {
            await assessmentRepository.CreateFindingsAsync(findings);
        }
    }

    private async Task SaveIdentityAccessFindings(Guid assessmentId, IdentityAccessResults iamResults)
    {
        var findings = new List<AssessmentFinding>();

        foreach (var securityFinding in iamResults.SecurityFindings)
        {
            findings.Add(new AssessmentFinding
            {
                Id = Guid.NewGuid(),
                AssessmentId = assessmentId,
                Category = "IdentityAccess",
                ResourceId = securityFinding.ResourceId,
                ResourceName = securityFinding.ResourceName,
                ResourceType = securityFinding.FindingType,
                Severity = securityFinding.Severity,
                Issue = securityFinding.Description,
                Recommendation = securityFinding.Recommendation,
                EstimatedEffort = GetIamEffortEstimate(securityFinding.FindingType)
            });
        }

        if (findings.Any())
        {
            await _assessmentRepository.CreateFindingsAsync(findings);
        }
    }

    private async Task SaveBusinessContinuityFindings(Guid assessmentId, BusinessContinuityResults bcResults)
    {
        var findings = new List<AssessmentFinding>();

        foreach (var bcFinding in bcResults.Findings)
        {
            findings.Add(new AssessmentFinding
            {
                Id = Guid.NewGuid(),
                AssessmentId = assessmentId,
                Category = "BusinessContinuity",
                ResourceId = bcFinding.ResourceId,
                ResourceName = bcFinding.ResourceName,
                ResourceType = bcFinding.Category,
                Severity = bcFinding.Severity,
                Issue = bcFinding.Issue,
                Recommendation = bcFinding.Recommendation,
                EstimatedEffort = "Medium"
            });
        }

        if (findings.Any())
        {
            await _assessmentRepository.CreateFindingsAsync(findings);
        }
    }

    private async Task SaveSecurityPostureFindings(Guid assessmentId, SecurityPostureResults securityResults)
    {
        var findings = new List<AssessmentFinding>();

        foreach (var securityFinding in securityResults.SecurityFindings)
        {
            findings.Add(new AssessmentFinding
            {
                Id = Guid.NewGuid(),
                AssessmentId = assessmentId,
                Category = "SecurityPosture",
                ResourceId = securityFinding.ResourceId,
                ResourceName = securityFinding.ResourceName,
                ResourceType = securityFinding.Category,
                Severity = securityFinding.Severity,
                Issue = securityFinding.Issue,
                Recommendation = securityFinding.Recommendation,
                EstimatedEffort = "Medium"
            });
        }

        if (findings.Any())
        {
            await _assessmentRepository.CreateFindingsAsync(findings);
        }
    }

    private decimal CalculateGovernanceScore(NamingConventionResults? namingResults, TaggingResults? taggingResults, AssessmentType type)
    {
        var scores = new List<decimal>();

        if (type == AssessmentType.NamingConvention && namingResults != null)
        {
            return namingResults.Score;
        }

        if (type == AssessmentType.Tagging && taggingResults != null)
        {
            return taggingResults.Score;
        }

        if (type == AssessmentType.GovernanceFull || type == AssessmentType.Full)
        {
            if (namingResults != null) scores.Add(namingResults.Score);
            if (taggingResults != null) scores.Add(taggingResults.Score);
        }

        return scores.Any() ? Math.Round(scores.Average(), 2) : 0m;
    }

    private string GetEffortEstimate(string violationType)
    {
        return violationType switch
        {
            "InvalidCharacters" => "High",
            "ClientPreferenceViolation" => "Medium",
            "MissingRequiredElement" => "High",
            "MissingResourceTypePrefix" => "Medium",
            "NameTooLong" => "Medium",
            "InconsistentPattern" => "Low",
            _ => "Medium"
        };
    }

    private string GetIamEffortEstimate(string findingType)
    {
        return findingType switch
        {
            "ApplicationExcessivePermissions" => "High",
            "OverprivilegedSubscriptionAccess" => "High",
            "OverprivilegedServicePrincipal" => "High",
            "ApplicationExpiredCredentials" => "Medium",
            "CustomRoleUsage" => "Medium",
            "ConditionalAccessAnalysisLimited" => "Low",
            _ => "Medium"
        };
    }

    private string EnhanceRecommendationWithClientPreferences(NamingViolation violation, ClientAssessmentConfiguration clientConfig)
    {
        var baseRecommendation = $"Suggested name: {violation.SuggestedName}";

        if (violation.ViolationType == "ClientPreferenceViolation" && clientConfig.HasNamingPreferences)
        {
            var preferredPatterns = clientConfig.GetEffectiveNamingPatterns();
            if (preferredPatterns.Any())
            {
                return $"{baseRecommendation}. Client prefers {string.Join(" or ", preferredPatterns)} naming pattern.";
            }
        }

        if (violation.ViolationType == "MissingRequiredElement" && clientConfig.AreEnvironmentIndicatorsRequired())
        {
            return $"{baseRecommendation}. Client requires environment indicators in resource names (e.g., dev, test, prod).";
        }

        return baseRecommendation;
    }

    private ClientAssessmentConfiguration MapToClientConfig(Compass.Data.Entities.ClientPreferences preferences)
    {
        return ClientAssessmentConfiguration.FromClientPreferences(preferences);
    }

    private AssessmentResult MapToEnhancedAssessmentResult(Assessment assessment, List<AssessmentFinding> findings)
    {
        var result = new AssessmentResult
        {
            AssessmentId = assessment.Id,
            EnvironmentId = assessment.EnvironmentId,
            Type = Enum.Parse<AssessmentType>(assessment.AssessmentType),
            Category = Enum.Parse<AssessmentCategory>(assessment.AssessmentCategory),
            Status = Enum.Parse<AssessmentStatus>(assessment.Status),
            OverallScore = assessment.OverallScore ?? 0,
            StartedDate = assessment.StartedDate,
            CompletedDate = assessment.CompletedDate,
            TotalResourcesAnalyzed = findings.Select(f => f.ResourceId).Distinct().Count(),
            IssuesFound = findings.Count,
            Recommendations = GenerateEnhancedRecommendations(findings)
        };

        result.DetailedMetrics = GenerateDetailedMetrics(findings);
        return result;
    }

    private List<AssessmentRecommendation> GenerateEnhancedRecommendations(List<AssessmentFinding> findings)
    {
        var recommendations = new List<AssessmentRecommendation>();
        var findingsByCategory = findings.GroupBy(f => f.Category).ToList();

        foreach (var categoryGroup in findingsByCategory)
        {
            var category = categoryGroup.Key;
            var categoryFindings = categoryGroup.ToList();
            var highSeverityCount = categoryFindings.Count(f => f.Severity == "High");
            var totalCount = categoryFindings.Count;

            var recommendation = category switch
            {
                "NamingConvention" => new AssessmentRecommendation
                {
                    Category = "NamingConvention",
                    Title = "Standardize Naming Conventions",
                    Description = $"Found {totalCount} naming convention violations across {categoryFindings.Select(f => f.ResourceType).Distinct().Count()} resource types.",
                    Priority = highSeverityCount > 0 ? "High" : totalCount > 10 ? "Medium" : "Low",
                    EstimatedEffort = totalCount > 20 ? "High" : totalCount > 5 ? "Medium" : "Low",
                    AffectedResources = categoryFindings.Select(f => f.ResourceName).Distinct().Take(10).ToList()
                },
                "Tagging" => new AssessmentRecommendation
                {
                    Category = "Tagging",
                    Title = "Implement Comprehensive Tagging Strategy",
                    Description = $"Found {totalCount} tagging violations that impact cost allocation and governance.",
                    Priority = highSeverityCount > 0 ? "High" : "Medium",
                    EstimatedEffort = totalCount > 50 ? "High" : totalCount > 20 ? "Medium" : "Low",
                    AffectedResources = categoryFindings.Select(f => f.ResourceName).Distinct().Take(10).ToList()
                },
                "IdentityAccess" => new AssessmentRecommendation
                {
                    Category = "IdentityAccess",
                    Title = "Address Identity and Access Management Issues",
                    Description = $"Found {totalCount} identity security issues that require immediate attention.",
                    Priority = "High",
                    EstimatedEffort = "High",
                    AffectedResources = categoryFindings.Select(f => f.ResourceName).Distinct().Take(10).ToList()
                },
                "BusinessContinuity" => new AssessmentRecommendation
                {
                    Category = "BusinessContinuity",
                    Title = "Improve Business Continuity Posture",
                    Description = $"Found {totalCount} business continuity gaps that could impact disaster recovery.",
                    Priority = "Medium",
                    EstimatedEffort = "High",
                    AffectedResources = categoryFindings.Select(f => f.ResourceName).Distinct().Take(10).ToList()
                },
                "SecurityPosture" => new AssessmentRecommendation
                {
                    Category = "SecurityPosture",
                    Title = "Enhance Security Posture",
                    Description = $"Found {totalCount} security issues that could expose the environment to threats.",
                    Priority = "High",
                    EstimatedEffort = "Medium",
                    AffectedResources = categoryFindings.Select(f => f.ResourceName).Distinct().Take(10).ToList()
                },
                _ => new AssessmentRecommendation
                {
                    Category = category,
                    Title = $"Address {category} Issues",
                    Description = $"Found {totalCount} issues in category {category}.",
                    Priority = highSeverityCount > 0 ? "High" : "Medium",
                    EstimatedEffort = "Medium",
                    AffectedResources = categoryFindings.Select(f => f.ResourceName).Distinct().Take(10).ToList()
                }
            };

            recommendations.Add(recommendation);
        }

        return recommendations;
    }

    private Dictionary<string, object> GenerateDetailedMetrics(List<AssessmentFinding> findings)
    {
        var metrics = new Dictionary<string, object>();

        // Category distribution
        var categoryStats = findings.GroupBy(f => f.Category)
            .ToDictionary(g => g.Key, g => g.Count());
        metrics["CategoryDistribution"] = categoryStats;

        // Severity distribution
        var severityStats = findings.GroupBy(f => f.Severity)
            .ToDictionary(g => g.Key, g => g.Count());
        metrics["SeverityDistribution"] = severityStats;

        // Resource type distribution
        var resourceTypeStats = findings.GroupBy(f => f.ResourceType)
            .ToDictionary(g => g.Key, g => g.Count());
        metrics["ResourceTypeDistribution"] = resourceTypeStats;

        return metrics;
    }

    private async Task MarkAssessmentAsFailed(Guid assessmentId, string errorMessage)
    {
        try
        {
            await _assessmentRepository.UpdateStatusAsync(assessmentId, AssessmentStatus.Failed.ToString());
            _logger.LogInformation("Marked assessment {AssessmentId} as failed: {Error}", assessmentId, errorMessage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to mark assessment {AssessmentId} as failed", assessmentId);
        }
    }

    // New scoped service helper methods to avoid DbContext threading issues
    private async Task<List<AzureResource>> CollectAzureResourcesWithScopedServicesAsync(
        IAzureResourceGraphService resourceGraphService,
        IOAuthService oauthService,
        AzureEnvironment environment,
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        List<AzureResource> resources;

        if (environment.ClientId.HasValue)
        {
            try
            {
                var hasOAuthCredentials = await oauthService.TestCredentialsAsync(environment.ClientId.Value, organizationId);
                if (hasOAuthCredentials)
                {
                    resources = await resourceGraphService.GetResourcesWithOAuthAsync(
                        environment.SubscriptionIds.ToArray(),
                        environment.ClientId.Value,
                        organizationId,
                        cancellationToken);
                }
                else
                {
                    resources = await resourceGraphService.GetResourcesAsync(environment.SubscriptionIds.ToArray(), cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "OAuth failed, falling back to default credentials");
                resources = await resourceGraphService.GetResourcesAsync(environment.SubscriptionIds.ToArray(), cancellationToken);
            }
        }
        else
        {
            resources = await resourceGraphService.GetResourcesAsync(environment.SubscriptionIds.ToArray(), cancellationToken);
        }

        return resources;
    }

    private async Task SaveAssessmentResourcesWithScopedServicesAsync(
        IAssessmentRepository assessmentRepository,
        Guid assessmentId,
        List<AzureResource> resources)
    {
        if (!resources.Any()) return;

        var assessmentResources = resources.Select(r => new AssessmentResource
        {
            Id = Guid.NewGuid(),
            AssessmentId = assessmentId,
            ResourceId = r.Id,
            Name = r.Name,
            Type = r.Type,
            ResourceTypeName = r.ResourceTypeName,
            ResourceGroup = r.ResourceGroup,
            Location = r.Location,
            SubscriptionId = r.SubscriptionId,
            Kind = r.Kind,
            Sku = r.Sku,
            Tags = System.Text.Json.JsonSerializer.Serialize(r.Tags),
            TagCount = r.TagCount,
            Environment = r.Environment,
            Properties = r.Properties,
            CreatedAt = DateTime.UtcNow
        }).ToList();

        await assessmentRepository.CreateResourcesAsync(assessmentResources);
    }

    private async Task SaveGovernanceAssessmentFindingsWithScopedServicesAsync(
        IAssessmentRepository assessmentRepository,
        Guid assessmentId,
        NamingConventionResults? namingResults,
        TaggingResults? taggingResults,
        DependencyAnalysisResults? dependencyResults,
        ClientAssessmentConfiguration? clientConfig)
    {
        var findings = new List<AssessmentFinding>();

        // Save naming convention findings
        if (namingResults?.Violations != null)
        {
            foreach (var violation in namingResults.Violations)
            {
                var recommendation = $"Suggested name: {violation.SuggestedName}";
                if (clientConfig != null)
                {
                    recommendation = EnhanceRecommendationWithClientPreferences(violation, clientConfig);
                }

                findings.Add(new AssessmentFinding
                {
                    Id = Guid.NewGuid(),
                    AssessmentId = assessmentId,
                    Category = "NamingConvention",
                    ResourceId = violation.ResourceId,
                    ResourceName = violation.ResourceName,
                    ResourceType = violation.ResourceType,
                    Severity = violation.Severity,
                    Issue = violation.Issue,
                    Recommendation = recommendation,
                    EstimatedEffort = GetEffortEstimate(violation.ViolationType)
                });
            }
        }

        // Save tagging findings
        if (taggingResults?.Violations != null)
        {
            foreach (var violation in taggingResults.Violations)
            {
                var recommendation = violation.MissingTags.Any()
                    ? $"Add missing tags: {string.Join(", ", violation.MissingTags)}"
                    : "Review and fix tag values";

                if (clientConfig != null && clientConfig.HasTaggingPreferences)
                {
                    var clientTags = clientConfig.GetEffectiveRequiredTags();
                    if (clientTags.Any())
                    {
                        recommendation += $". Based on client preferences, prioritize these tags: {string.Join(", ", clientTags.Take(5))}";
                    }
                }

                findings.Add(new AssessmentFinding
                {
                    Id = Guid.NewGuid(),
                    AssessmentId = assessmentId,
                    Category = "Tagging",
                    ResourceId = violation.ResourceId,
                    ResourceName = violation.ResourceName,
                    ResourceType = violation.ResourceType,
                    Severity = violation.Severity,
                    Issue = violation.Issue,
                    Recommendation = recommendation,
                    EstimatedEffort = violation.MissingTags.Count > 3 ? "High" : "Medium"
                });
            }
        }

        // Save dependency findings (if needed)
        if (dependencyResults != null)
        {
            // Add dependency analysis findings if needed
        }

        if (findings.Any())
        {
            await assessmentRepository.CreateFindingsAsync(findings);
        }
    }

    private async Task SaveBusinessContinuityFindingsWithScopedServicesAsync(
        IAssessmentRepository assessmentRepository,
        Guid assessmentId,
        BusinessContinuityResults bcResults)
    {
        var findings = new List<AssessmentFinding>();

        foreach (var bcFinding in bcResults.Findings)
        {
            findings.Add(new AssessmentFinding
            {
                Id = Guid.NewGuid(),
                AssessmentId = assessmentId,
                Category = "BusinessContinuity",
                ResourceId = bcFinding.ResourceId,
                ResourceName = bcFinding.ResourceName,
                ResourceType = bcFinding.Category,
                Severity = bcFinding.Severity,
                Issue = bcFinding.Issue,
                Recommendation = bcFinding.Recommendation,
                EstimatedEffort = "Medium"
            });
        }

        if (findings.Any())
        {
            await assessmentRepository.CreateFindingsAsync(findings);
        }
    }

    private async Task SaveSecurityPostureFindingsWithScopedServicesAsync(
        IAssessmentRepository assessmentRepository,
        Guid assessmentId,
        SecurityPostureResults securityResults)
    {
        var findings = new List<AssessmentFinding>();

        foreach (var securityFinding in securityResults.SecurityFindings)
        {
            findings.Add(new AssessmentFinding
            {
                Id = Guid.NewGuid(),
                AssessmentId = assessmentId,
                Category = "SecurityPosture",
                ResourceId = securityFinding.ResourceId,
                ResourceName = securityFinding.ResourceName,
                ResourceType = securityFinding.Category,
                Severity = securityFinding.Severity,
                Issue = securityFinding.Issue,
                Recommendation = securityFinding.Recommendation,
                EstimatedEffort = "Medium"
            });
        }

        if (findings.Any())
        {
            await assessmentRepository.CreateFindingsAsync(findings);
        }
    }
}