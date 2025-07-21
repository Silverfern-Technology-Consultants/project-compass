using Compass.Core.Models.Assessment;

namespace Compass.Core.Interfaces;

public interface IEnterpriseApplicationsAnalyzer
{
    Task<IdentityAccessResults> AnalyzeAsync(string[] subscriptionIds, CancellationToken cancellationToken = default);
    Task<IdentityAccessResults> AnalyzeWithOAuthAsync(string[] subscriptionIds, Guid clientId, Guid organizationId, CancellationToken cancellationToken = default);
}

public interface IStaleUsersDevicesAnalyzer
{
    Task<IdentityAccessResults> AnalyzeAsync(string[] subscriptionIds, CancellationToken cancellationToken = default);
    Task<IdentityAccessResults> AnalyzeWithOAuthAsync(string[] subscriptionIds, Guid clientId, Guid organizationId, CancellationToken cancellationToken = default);
}

public interface IResourceIamRbacAnalyzer
{
    Task<IdentityAccessResults> AnalyzeAsync(string[] subscriptionIds, CancellationToken cancellationToken = default);
    Task<IdentityAccessResults> AnalyzeWithOAuthAsync(string[] subscriptionIds, Guid clientId, Guid organizationId, CancellationToken cancellationToken = default);
}

public interface IConditionalAccessAnalyzer
{
    Task<IdentityAccessResults> AnalyzeAsync(string[] subscriptionIds, CancellationToken cancellationToken = default);
    Task<IdentityAccessResults> AnalyzeWithOAuthAsync(string[] subscriptionIds, Guid clientId, Guid organizationId, CancellationToken cancellationToken = default);
}

public interface IIdentityFullAnalyzer
{
    Task<IdentityAccessResults> AnalyzeAsync(string[] subscriptionIds, CancellationToken cancellationToken = default);
    Task<IdentityAccessResults> AnalyzeWithOAuthAsync(string[] subscriptionIds, Guid clientId, Guid organizationId, CancellationToken cancellationToken = default);
}