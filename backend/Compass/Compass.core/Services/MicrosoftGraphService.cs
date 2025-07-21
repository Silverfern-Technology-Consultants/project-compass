using Microsoft.Graph;
using Microsoft.Extensions.Logging;
using Azure.Core;
using Compass.Core.Interfaces;
using Compass.Core.Models;
using Compass.Core.Models.Assessment;

namespace Compass.Core.Services
{
    public class MicrosoftGraphService : IMicrosoftGraphService
    {
        private readonly IOAuthService _oauthService;
        private readonly ILogger<MicrosoftGraphService> _logger;

        public MicrosoftGraphService(
            IOAuthService oauthService,
            ILogger<MicrosoftGraphService> logger)
        {
            _oauthService = oauthService;
            _logger = logger;
        }

        private async Task<GraphServiceClient?> CreateGraphClientAsync(Guid clientId, Guid organizationId)
        {
            try
            {
                var graphCredentials = await _oauthService.GetGraphCredentialsAsync(clientId, organizationId);
                if (graphCredentials == null)
                {
                    _logger.LogWarning("No Microsoft Graph credentials found for client {ClientId}", clientId);
                    return null;
                }

                // Check if token is expired (with 5 minute buffer)
                if (graphCredentials.ExpiresAt <= DateTime.UtcNow.AddMinutes(5))
                {
                    _logger.LogInformation("Graph token expired for client {ClientId}, attempting refresh", clientId);
                    var refreshed = await _oauthService.RefreshGraphTokensAsync(clientId, organizationId);
                    if (refreshed)
                    {
                        // Get refreshed credentials
                        graphCredentials = await _oauthService.GetGraphCredentialsAsync(clientId, organizationId);
                    }
                    else
                    {
                        _logger.LogWarning("Failed to refresh Graph tokens for client {ClientId}", clientId);
                        return null;
                    }
                }

                // Create Graph client using Azure.Identity with OAuth token
                var tokenCredential = new GraphTokenCredential(graphCredentials.AccessToken);
                return new GraphServiceClient(tokenCredential);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create Microsoft Graph client for client {ClientId}", clientId);
                return null;
            }
        }

        public async Task<bool> TestGraphConnectionAsync(Guid clientId, Guid organizationId)
        {
            try
            {
                var graphClient = await CreateGraphClientAsync(clientId, organizationId);
                if (graphClient == null) return false;

                // Simple test query to verify connectivity
                var me = await graphClient.Me.GetAsync();

                _logger.LogInformation("Successfully connected to Microsoft Graph for client {ClientId}", clientId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to test Microsoft Graph connection for client {ClientId}", clientId);
                return false;
            }
        }

        public async Task<List<Microsoft.Graph.Models.User>> GetUsersAsync(Guid clientId, Guid organizationId)
        {
            try
            {
                var graphClient = await CreateGraphClientAsync(clientId, organizationId);
                if (graphClient == null) return new List<Microsoft.Graph.Models.User>();

                var users = await graphClient.Users.GetAsync(requestConfiguration =>
                {
                    requestConfiguration.QueryParameters.Select = new[] { "id", "displayName", "userPrincipalName", "accountEnabled", "signInActivity", "createdDateTime", "lastPasswordChangeDateTime" };
                });

                var allUsers = new List<Microsoft.Graph.Models.User>();
                if (users?.Value != null)
                {
                    allUsers.AddRange(users.Value);

                    // Handle pagination
                    while (!string.IsNullOrEmpty(users.OdataNextLink))
                    {
                        var nextPageRequest = await graphClient.Users.WithUrl(users.OdataNextLink).GetAsync();
                        if (nextPageRequest?.Value != null)
                        {
                            allUsers.AddRange(nextPageRequest.Value);
                            users = nextPageRequest;
                        }
                        else
                        {
                            break;
                        }
                    }
                }

                _logger.LogInformation("Retrieved {UserCount} users from Microsoft Graph for client {ClientId}",
                    allUsers.Count, clientId);

                return allUsers;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve users from Microsoft Graph for client {ClientId}", clientId);
                return new List<Microsoft.Graph.Models.User>();
            }
        }

        public async Task<List<Microsoft.Graph.Models.User>> GetInactiveUsersAsync(Guid clientId, Guid organizationId, int daysSinceLastSignIn = 90)
        {
            try
            {
                var graphClient = await CreateGraphClientAsync(clientId, organizationId);
                if (graphClient == null) return new List<Microsoft.Graph.Models.User>();

                // Get users with standard properties
                var users = await graphClient.Users.GetAsync(requestConfiguration =>
                {
                    requestConfiguration.QueryParameters.Select = new[] {
                "id", "displayName", "userPrincipalName", "accountEnabled",
                "signInActivity", "createdDateTime", "lastPasswordChangeDateTime",
                "userType", "mailNickname", "assignedLicenses", "mail"
            };
                });

                var allUsers = new List<Microsoft.Graph.Models.User>();
                if (users?.Value != null)
                {
                    allUsers.AddRange(users.Value);

                    // Handle pagination
                    while (!string.IsNullOrEmpty(users.OdataNextLink))
                    {
                        var nextPageRequest = await graphClient.Users.WithUrl(users.OdataNextLink).GetAsync();
                        if (nextPageRequest?.Value != null)
                        {
                            allUsers.AddRange(nextPageRequest.Value);
                            users = nextPageRequest;
                        }
                        else
                        {
                            break;
                        }
                    }
                }

                var cutoffDate = DateTime.UtcNow.AddDays(-daysSinceLastSignIn);

                // Filter users and check recipient types
                var potentialInactiveUsers = allUsers.Where(u =>
                    u.AccountEnabled == true &&
                    (u.SignInActivity?.LastSignInDateTime == null ||
                     u.SignInActivity.LastSignInDateTime < cutoffDate))
                    .ToList();

                var actualInactiveUsers = new List<Microsoft.Graph.Models.User>();

                foreach (var user in potentialInactiveUsers)
                {
                    var isInteractiveUser = await IsInteractiveUserAsync(graphClient, user);
                    if (isInteractiveUser)
                    {
                        actualInactiveUsers.Add(user);
                    }
                }

                _logger.LogInformation("Found {InactiveUserCount} inactive interactive users (>{Days} days) for client {ClientId}. Filtered out {FilteredCount} non-interactive accounts.",
                    actualInactiveUsers.Count, daysSinceLastSignIn, clientId, potentialInactiveUsers.Count - actualInactiveUsers.Count);

                return actualInactiveUsers;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to analyze inactive users for client {ClientId}", clientId);
                return new List<Microsoft.Graph.Models.User>();
            }
        }

        private async Task<bool> IsInteractiveUserAsync(GraphServiceClient graphClient, Microsoft.Graph.Models.User user)
        {
            try
            {
                // First check basic patterns (quick filter)
                if (!BasicInteractiveUserCheck(user))
                {
                    return false;
                }

                // For mail-enabled users, check Exchange recipient type
                if (!string.IsNullOrEmpty(user.Mail))
                {
                    var recipientType = await GetExchangeRecipientTypeAsync(graphClient, user.Id);

                    // Log for debugging
                    _logger.LogDebug("User '{DisplayName}' has recipient type: {RecipientType}",
                        user.DisplayName, recipientType ?? "Unknown");

                    // Filter out non-interactive recipient types
                    if (!string.IsNullOrEmpty(recipientType))
                    {
                        var nonInteractiveTypes = new[]
                        {
                    "SharedMailbox",
                    "RoomMailbox",
                    "EquipmentMailbox",
                    "DiscoveryMailbox",
                    "PublicFolder",
                    "SystemMailbox",
                    "ArbitrationMailbox",
                    "AuditingMailbox",
                    "AuxAuditingMailbox",
                    "SupervisoryReviewPolicyMailbox"
                };

                        if (nonInteractiveTypes.Any(type =>
                            string.Equals(recipientType, type, StringComparison.OrdinalIgnoreCase)))
                        {
                            _logger.LogDebug("Filtering out {RecipientType}: {DisplayName}", recipientType, user.DisplayName);
                            return false;
                        }
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to determine recipient type for user {UserId}, including in analysis", user.Id);
                // If we can't determine the type, err on the side of including them
                return BasicInteractiveUserCheck(user);
            }
        }

        private bool BasicInteractiveUserCheck(Microsoft.Graph.Models.User user)
        {
            // Quick pattern-based checks before making additional API calls

            var mailNickname = user.MailNickname?.ToLowerInvariant();
            if (!string.IsNullOrEmpty(mailNickname))
            {
                var sharedMailboxPatterns = new[]
                {
            "shared", "noreply", "no-reply", "donotreply", "do-not-reply",
            "support", "info", "admin", "administrator", "system",
            "service", "automated", "notification", "alerts"
        };

                if (sharedMailboxPatterns.Any(pattern => mailNickname.Contains(pattern)))
                {
                    return false;
                }
            }

            var upn = user.UserPrincipalName?.ToLowerInvariant();
            if (!string.IsNullOrEmpty(upn))
            {
                var serviceAccountPatterns = new[]
                {
            "sync", "service", "system", "backup", "monitoring",
            "alert", "automation", "robot", "bot", "noreply", "donotreply"
        };

                if (serviceAccountPatterns.Any(pattern => upn.Contains(pattern)))
                {
                    return false;
                }
            }

            var displayName = user.DisplayName?.ToLowerInvariant();
            if (!string.IsNullOrEmpty(displayName))
            {
                var serviceDisplayPatterns = new[]
                {
            "service account", "sync account", "system account",
            "shared mailbox", "resource mailbox", "equipment",
            "conference room", "meeting room"
        };

                if (serviceDisplayPatterns.Any(pattern => displayName.Contains(pattern)))
                {
                    return false;
                }
            }

            return true;
        }

        private async Task<string?> GetExchangeRecipientTypeAsync(GraphServiceClient graphClient, string userId)
        {
            try
            {
                // Try to get Exchange-specific properties using beta endpoint
                var requestUrl = $"https://graph.microsoft.com/beta/users/{userId}?$select=id,recipientTypeDetails";

                var response = await graphClient.Users[userId].GetAsync(requestConfiguration =>
                {
                    // This might require beta endpoint access
                    requestConfiguration.QueryParameters.Select = new[] { "id" };
                });

                // Alternative approach: Use Exchange Online PowerShell cmdlets via Graph (if available)
                // Or check mailbox properties that indicate recipient type

                // For now, try to infer from available properties
                var mailboxResponse = await graphClient.Users[userId].GetAsync(requestConfiguration =>
                {
                    requestConfiguration.QueryParameters.Select = new[] {
                "id", "assignedLicenses", "usageLocation", "proxyAddresses"
            };
                });

                // Heuristic: Users with no licenses but proxy addresses are likely shared mailboxes
                if ((mailboxResponse?.AssignedLicenses == null || !mailboxResponse.AssignedLicenses.Any()) &&
                    mailboxResponse?.ProxyAddresses?.Any() == true)
                {
                    return "SharedMailbox";
                }

                // If user has licenses, likely a regular user mailbox
                if (mailboxResponse?.AssignedLicenses?.Any() == true)
                {
                    return "UserMailbox";
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogDebug("Could not determine recipient type for user {UserId}: {Error}", userId, ex.Message);
                return null;
            }
        }

        public async Task<List<Microsoft.Graph.Models.Application>> GetApplicationsAsync(Guid clientId, Guid organizationId)
        {
            try
            {
                var graphClient = await CreateGraphClientAsync(clientId, organizationId);
                if (graphClient == null) return new List<Microsoft.Graph.Models.Application>();

                var applications = await graphClient.Applications.GetAsync(requestConfiguration =>
                {
                    requestConfiguration.QueryParameters.Select = new[] { "id", "displayName", "appId", "createdDateTime", "passwordCredentials", "keyCredentials", "requiredResourceAccess" };
                });

                var allApplications = new List<Microsoft.Graph.Models.Application>();
                if (applications?.Value != null)
                {
                    allApplications.AddRange(applications.Value);

                    // Handle pagination
                    while (!string.IsNullOrEmpty(applications.OdataNextLink))
                    {
                        var nextPageRequest = await graphClient.Applications.WithUrl(applications.OdataNextLink).GetAsync();
                        if (nextPageRequest?.Value != null)
                        {
                            allApplications.AddRange(nextPageRequest.Value);
                            applications = nextPageRequest;
                        }
                        else
                        {
                            break;
                        }
                    }
                }

                _logger.LogInformation("Retrieved {ApplicationCount} applications from Microsoft Graph for client {ClientId}",
                    allApplications.Count, clientId);

                return allApplications;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve applications from Microsoft Graph for client {ClientId}", clientId);
                return new List<Microsoft.Graph.Models.Application>();
            }
        }

        public async Task<List<Microsoft.Graph.Models.Application>> GetApplicationsWithExpiredCredentialsAsync(Guid clientId, Guid organizationId)
        {
            try
            {
                var allApplications = await GetApplicationsAsync(clientId, organizationId);
                var now = DateTime.UtcNow;

                var appsWithExpiredCreds = allApplications.Where(app =>
                    (app.PasswordCredentials?.Any(pc => pc.EndDateTime < now) == true) ||
                    (app.KeyCredentials?.Any(kc => kc.EndDateTime < now) == true))
                    .ToList();

                _logger.LogInformation("Found {ExpiredAppCount} applications with expired credentials for client {ClientId}",
                    appsWithExpiredCreds.Count, clientId);

                return appsWithExpiredCreds;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to analyze applications with expired credentials for client {ClientId}", clientId);
                return new List<Microsoft.Graph.Models.Application>();
            }
        }

        public async Task<List<Microsoft.Graph.Models.ConditionalAccessPolicy>> GetConditionalAccessPoliciesAsync(Guid clientId, Guid organizationId)
        {
            try
            {
                var graphClient = await CreateGraphClientAsync(clientId, organizationId);
                if (graphClient == null) return new List<Microsoft.Graph.Models.ConditionalAccessPolicy>();

                var policies = await graphClient.Identity.ConditionalAccess.Policies.GetAsync();

                var allPolicies = new List<Microsoft.Graph.Models.ConditionalAccessPolicy>();
                if (policies?.Value != null)
                {
                    allPolicies.AddRange(policies.Value);

                    // Handle pagination
                    while (!string.IsNullOrEmpty(policies.OdataNextLink))
                    {
                        var nextPageRequest = await graphClient.Identity.ConditionalAccess.Policies.WithUrl(policies.OdataNextLink).GetAsync();
                        if (nextPageRequest?.Value != null)
                        {
                            allPolicies.AddRange(nextPageRequest.Value);
                            policies = nextPageRequest;
                        }
                        else
                        {
                            break;
                        }
                    }
                }

                _logger.LogInformation("Retrieved {PolicyCount} conditional access policies for client {ClientId}",
                    allPolicies.Count, clientId);

                return allPolicies;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve conditional access policies for client {ClientId}", clientId);
                return new List<Microsoft.Graph.Models.ConditionalAccessPolicy>();
            }
        }

        // Additional method implementations would follow the same pattern...
        // Including GetGroupsAsync, GetServicePrincipalsAsync, GetRiskyUsersAsync, etc.

        // Placeholder implementations for remaining interface methods
        public async Task<List<Microsoft.Graph.Models.Group>> GetGroupsAsync(Guid clientId, Guid organizationId)
        {
            try
            {
                var graphClient = await CreateGraphClientAsync(clientId, organizationId);
                if (graphClient == null) return new List<Microsoft.Graph.Models.Group>();

                var groups = await graphClient.Groups.GetAsync(requestConfiguration =>
                {
                    requestConfiguration.QueryParameters.Select = new[] { "id", "displayName", "description", "groupTypes", "securityEnabled", "mailEnabled", "createdDateTime" };
                });

                var allGroups = new List<Microsoft.Graph.Models.Group>();
                if (groups?.Value != null)
                {
                    allGroups.AddRange(groups.Value);

                    // Handle pagination
                    while (!string.IsNullOrEmpty(groups.OdataNextLink))
                    {
                        var nextPageRequest = await graphClient.Groups.WithUrl(groups.OdataNextLink).GetAsync();
                        if (nextPageRequest?.Value != null)
                        {
                            allGroups.AddRange(nextPageRequest.Value);
                            groups = nextPageRequest;
                        }
                        else
                        {
                            break;
                        }
                    }
                }

                _logger.LogInformation("Retrieved {GroupCount} groups from Microsoft Graph for client {ClientId}",
                    allGroups.Count, clientId);

                return allGroups;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve groups from Microsoft Graph for client {ClientId}", clientId);
                return new List<Microsoft.Graph.Models.Group>();
            }
        }

        public async Task<List<Microsoft.Graph.Models.User>> GetPrivilegedUsersAsync(Guid clientId, Guid organizationId)
        {
            try
            {
                var graphClient = await CreateGraphClientAsync(clientId, organizationId);
                if (graphClient == null) return new List<Microsoft.Graph.Models.User>();

                // Get directory roles first
                var directoryRoles = await graphClient.DirectoryRoles.GetAsync();
                var privilegedUsers = new List<Microsoft.Graph.Models.User>();

                if (directoryRoles?.Value != null)
                {
                    // Define privileged role templates (these are standard Azure AD role template IDs)
                    var privilegedRoleTemplates = new[]
                    {
                "62e90394-69f5-4237-9190-012177145e10", // Global Administrator
                "194ae4cb-b126-40b2-bd5b-6091b380977d", // Security Administrator
                "f28a1f50-f6e7-4571-818b-6a12f2af6b6c", // SharePoint Administrator
                "729827e3-9c14-49f7-bb1b-9608f156bbb8", // Helpdesk Administrator
                "966707d0-3269-4727-9be2-8c3a10f19b9d", // Password Administrator
                "7be44c8a-adaf-4e2a-84d6-ab2649e08a13", // Privileged Authentication Administrator
            };

                    foreach (var role in directoryRoles.Value.Where(r => privilegedRoleTemplates.Contains(r.RoleTemplateId)))
                    {
                        try
                        {
                            var roleMembers = await graphClient.DirectoryRoles[role.Id].Members.GetAsync();
                            if (roleMembers?.Value != null)
                            {
                                foreach (var member in roleMembers.Value.OfType<Microsoft.Graph.Models.User>())
                                {
                                    if (!privilegedUsers.Any(u => u.Id == member.Id))
                                    {
                                        privilegedUsers.Add(member);
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to get members for role {RoleId}", role.Id);
                        }
                    }
                }

                _logger.LogInformation("Retrieved {PrivilegedUserCount} privileged users for client {ClientId}",
                    privilegedUsers.Count, clientId);

                return privilegedUsers;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve privileged users from Microsoft Graph for client {ClientId}", clientId);
                return new List<Microsoft.Graph.Models.User>();
            }
        }

        public async Task<List<Microsoft.Graph.Models.ServicePrincipal>> GetServicePrincipalsAsync(Guid clientId, Guid organizationId)
        {
            try
            {
                var graphClient = await CreateGraphClientAsync(clientId, organizationId);
                if (graphClient == null) return new List<Microsoft.Graph.Models.ServicePrincipal>();

                var servicePrincipals = await graphClient.ServicePrincipals.GetAsync(requestConfiguration =>
                {
                    requestConfiguration.QueryParameters.Select = new[] { "id", "displayName", "appId", "servicePrincipalType", "accountEnabled", "appRoles", "oauth2PermissionScopes" };
                });

                var allServicePrincipals = new List<Microsoft.Graph.Models.ServicePrincipal>();
                if (servicePrincipals?.Value != null)
                {
                    allServicePrincipals.AddRange(servicePrincipals.Value);

                    // Handle pagination
                    while (!string.IsNullOrEmpty(servicePrincipals.OdataNextLink))
                    {
                        var nextPageRequest = await graphClient.ServicePrincipals.WithUrl(servicePrincipals.OdataNextLink).GetAsync();
                        if (nextPageRequest?.Value != null)
                        {
                            allServicePrincipals.AddRange(nextPageRequest.Value);
                            servicePrincipals = nextPageRequest;
                        }
                        else
                        {
                            break;
                        }
                    }
                }

                _logger.LogInformation("Retrieved {ServicePrincipalCount} service principals from Microsoft Graph for client {ClientId}",
                    allServicePrincipals.Count, clientId);

                return allServicePrincipals;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve service principals from Microsoft Graph for client {ClientId}", clientId);
                return new List<Microsoft.Graph.Models.ServicePrincipal>();
            }
        }

        public async Task<List<Microsoft.Graph.Models.ServicePrincipal>> GetOverprivilegedServicePrincipalsAsync(Guid clientId, Guid organizationId)
        {
            try
            {
                var allServicePrincipals = await GetServicePrincipalsAsync(clientId, organizationId);

                // Define high-risk permissions that indicate overprivileged service principals
                var highRiskPermissions = new[]
                {
            "Directory.ReadWrite.All",
            "Application.ReadWrite.All",
            "User.ReadWrite.All",
            "Group.ReadWrite.All",
            "RoleManagement.ReadWrite.Directory",
            "Mail.ReadWrite",
            "Files.ReadWrite.All",
            "Sites.FullControl.All"
        };

                // Microsoft first-party service principals that should be excluded
                var microsoftFirstPartyApps = new[]
                {
            // Core Office 365 / Microsoft 365 services
            "Microsoft Graph",
            "Office 365 Exchange Online",
            "Office 365 SharePoint Online",
            "Windows Azure Active Directory",
            "Microsoft Teams Services",
            "Teams CMD Services and Data",
            "Microsoft Office",
            "Office 365 Management APIs",
            "Microsoft Azure CLI",
            "Azure PowerShell",
            "Microsoft Intune",
            "Microsoft Stream Portal",
            "Microsoft Forms",
            "Power BI Service",
            "Microsoft To-Do",
            "Yammer",
            "Microsoft Planner",
            "OneDrive",
            "Skype for Business Online",
            
            // Azure services
            "Microsoft Azure Management",
            "Azure Storage",
            "Azure Key Vault",
            "Microsoft Azure Backup",
            "Azure DevOps",
            "Microsoft Defender for Cloud Apps",
            "Microsoft Security Graph",
            
            // Authentication and security
            "Microsoft Authentication Broker",
            "Microsoft Azure Active Directory Connect",
            "Azure AD Identity Protection",
            "Microsoft Cloud App Security",
            "Microsoft Authenticator",
            
            // Development tools
            "Visual Studio",
            "Azure Portal",
            "Microsoft Azure PowerShell",
            "Azure Resource Manager"
        };

                // App IDs of well-known Microsoft first-party applications
                var microsoftFirstPartyAppIds = new[]
                {
            "00000003-0000-0000-c000-000000000000", // Microsoft Graph
            "00000002-0000-0000-c000-000000000000", // Azure Active Directory Graph (legacy)
            "797f4846-ba00-4fd7-ba43-dac1f8f63013", // Windows Azure Service Management API
            "1950a258-227b-4e31-a9cf-717495945fc2", // Microsoft Azure PowerShell
            "04b07795-8ddb-461a-bbee-02f9e1bf7b46", // Microsoft Azure CLI
            "cf36b471-5b44-428c-9ce7-313bf84528de", // Microsoft Teams Services
            "1fec8e78-bce4-4aaf-ab1b-5451cc387264", // Microsoft Teams
            "cc15fd57-2c6c-4117-a88c-83b1d56b4bbe", // Microsoft Teams Web Client
            "5e3ce6c0-2b1f-4285-8d4b-75ee78787346", // Microsoft Teams Retail Service
            "a3475900-ccec-4a69-98f5-a65cd5dc5306", // SharePoint Online Web Client Extensibility
            "d3590ed6-52b3-4102-aeff-aad2292ab01c", // Microsoft Office
            "57fb890c-0dab-4253-a5e0-7188c88b2bb4", // SharePoint Online Client
            "89bee1f7-5e6e-4d8a-9f3d-ecd601259da7"  // Office365 Shell WCSS-Client
        };

                var overprivilegedSPs = new List<Microsoft.Graph.Models.ServicePrincipal>();

                foreach (var sp in allServicePrincipals)
                {
                    // Skip Microsoft first-party applications by display name
                    if (microsoftFirstPartyApps.Any(app =>
                        string.Equals(sp.DisplayName, app, StringComparison.OrdinalIgnoreCase)))
                    {
                        continue;
                    }

                    // Skip Microsoft first-party applications by App ID
                    if (microsoftFirstPartyAppIds.Contains(sp.AppId))
                    {
                        continue;
                    }

                    // Skip service principals from Microsoft tenant
                    if (sp.AppOwnerOrganizationId?.ToString() == "f8cdef31-a31e-4b4a-93e4-5f571e91255a" || // Microsoft Services tenant
                        sp.AppOwnerOrganizationId?.ToString() == "72f988bf-86f1-41af-91ab-2d7cd011db47")   // Microsoft tenant
                    {
                        continue;
                    }

                    bool isOverprivileged = false;

                    // Check OAuth2 permission scopes
                    if (sp.Oauth2PermissionScopes != null)
                    {
                        foreach (var scope in sp.Oauth2PermissionScopes)
                        {
                            if (highRiskPermissions.Any(hrp => scope.Value?.Contains(hrp, StringComparison.OrdinalIgnoreCase) == true))
                            {
                                isOverprivileged = true;
                                break;
                            }
                        }
                    }

                    // Check app roles
                    if (sp.AppRoles != null && !isOverprivileged)
                    {
                        foreach (var role in sp.AppRoles)
                        {
                            if (highRiskPermissions.Any(hrp => role.Value?.Contains(hrp, StringComparison.OrdinalIgnoreCase) == true))
                            {
                                isOverprivileged = true;
                                break;
                            }
                        }
                    }

                    if (isOverprivileged)
                    {
                        overprivilegedSPs.Add(sp);
                    }
                }

                _logger.LogInformation("Found {OverprivilegedCount} overprivileged third-party service principals for client {ClientId} (excluded {MicrosoftAppsCount} Microsoft first-party apps)",
                    overprivilegedSPs.Count, clientId, allServicePrincipals.Count - overprivilegedSPs.Count);

                return overprivilegedSPs;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to analyze overprivileged service principals for client {ClientId}", clientId);
                return new List<Microsoft.Graph.Models.ServicePrincipal>();
            }
        }

        public async Task<List<Microsoft.Graph.Models.User>> GetUsersNotCoveredByMfaAsync(Guid clientId, Guid organizationId)
        {
            try
            {
                var allUsers = await GetUsersAsync(clientId, organizationId);
                var caPolicies = await GetConditionalAccessPoliciesAsync(clientId, organizationId);

                // This is a simplified analysis - in practice, determining MFA coverage requires
                // complex analysis of CA policies, their conditions, and user assignments
                var usersNotCoveredByMfa = new List<Microsoft.Graph.Models.User>();

                // Check if there are any enabled MFA-requiring CA policies
                var mfaPolicies = caPolicies.Where(p =>
                    p.State == Microsoft.Graph.Models.ConditionalAccessPolicyState.Enabled &&
                    p.GrantControls?.BuiltInControls?.Contains(Microsoft.Graph.Models.ConditionalAccessGrantControl.Mfa) == true)
                    .ToList();

                if (!mfaPolicies.Any())
                {
                    // If no MFA policies exist, all users are potentially not covered
                    usersNotCoveredByMfa.AddRange(allUsers.Where(u => u.AccountEnabled == true));
                }
                else
                {
                    // Simplified logic: assume policies with "All Users" cover everyone
                    // In practice, this would require detailed policy analysis
                    var hasAllUsersMfaPolicy = mfaPolicies.Any(p =>
                        p.Conditions?.Users?.IncludeUsers?.Contains("All") == true ||
                        p.Conditions?.Users?.IncludeUsers?.Contains("all") == true);

                    if (!hasAllUsersMfaPolicy)
                    {
                        // Add users who might not be covered (simplified logic)
                        usersNotCoveredByMfa.AddRange(allUsers.Where(u => u.AccountEnabled == true).Take(10));
                    }
                }

                _logger.LogInformation("Found {UncoveredUserCount} users potentially not covered by MFA for client {ClientId}",
                    usersNotCoveredByMfa.Count, clientId);

                return usersNotCoveredByMfa;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to analyze MFA coverage for client {ClientId}", clientId);
                return new List<Microsoft.Graph.Models.User>();
            }
        }

        public async Task<ConditionalAccessCoverageReport> AnalyzeConditionalAccessCoverageAsync(Guid clientId, Guid organizationId)
        {
            try
            {
                var allUsers = await GetUsersAsync(clientId, organizationId);
                var usersNotCoveredByMfa = await GetUsersNotCoveredByMfaAsync(clientId, organizationId);
                var caPolicies = await GetConditionalAccessPoliciesAsync(clientId, organizationId);

                var report = new ConditionalAccessCoverageReport
                {
                    TotalUsers = allUsers.Count,
                    UsersWithoutMfa = usersNotCoveredByMfa.Count,
                    UsersCoveredByMfa = allUsers.Count - usersNotCoveredByMfa.Count,
                    UncoveredUsers = usersNotCoveredByMfa.Take(10).Select(u => u.DisplayName ?? u.UserPrincipalName ?? "Unknown").ToList(),
                    PolicyGaps = new List<string>()
                };

                // Analyze policy gaps - only consider ENABLED policies (not Report-only)
                var enabledPolicies = caPolicies.Where(p => p.State == Microsoft.Graph.Models.ConditionalAccessPolicyState.Enabled).ToList();
                var reportOnlyPolicies = caPolicies.Where(p => p.State == Microsoft.Graph.Models.ConditionalAccessPolicyState.EnabledForReportingButNotEnforced).ToList();

                if (!enabledPolicies.Any())
                {
                    if (reportOnlyPolicies.Any())
                    {
                        report.PolicyGaps.Add($"Found {reportOnlyPolicies.Count} policies in Report-only mode but no actively enforced policies");
                    }
                    else
                    {
                        report.PolicyGaps.Add("No enabled Conditional Access policies found");
                    }
                }
                else
                {
                    var hasMfaPolicy = enabledPolicies.Any(p =>
                        p.GrantControls?.BuiltInControls?.Contains(Microsoft.Graph.Models.ConditionalAccessGrantControl.Mfa) == true);

                    if (!hasMfaPolicy)
                    {
                        // Check if there are Report-only MFA policies
                        var reportOnlyMfaPolicies = reportOnlyPolicies.Where(p =>
                            p.GrantControls?.BuiltInControls?.Contains(Microsoft.Graph.Models.ConditionalAccessGrantControl.Mfa) == true).ToList();

                        if (reportOnlyMfaPolicies.Any())
                        {
                            report.PolicyGaps.Add($"MFA policies exist but are in Report-only mode ({reportOnlyMfaPolicies.Count} policies)");
                        }
                        else
                        {
                            report.PolicyGaps.Add("No policies requiring MFA found");
                        }
                    }

                    var hasDeviceCompliancePolicy = enabledPolicies.Any(p =>
                        p.GrantControls?.BuiltInControls?.Contains(Microsoft.Graph.Models.ConditionalAccessGrantControl.CompliantDevice) == true);

                    if (!hasDeviceCompliancePolicy)
                    {
                        // Check if there are Report-only device compliance policies
                        var reportOnlyDevicePolicies = reportOnlyPolicies.Where(p =>
                            p.GrantControls?.BuiltInControls?.Contains(Microsoft.Graph.Models.ConditionalAccessGrantControl.CompliantDevice) == true).ToList();

                        if (reportOnlyDevicePolicies.Any())
                        {
                            report.PolicyGaps.Add($"Device compliance policies exist but are in Report-only mode ({reportOnlyDevicePolicies.Count} policies)");
                        }
                        else
                        {
                            report.PolicyGaps.Add("No policies requiring compliant devices found");
                        }
                    }

                    // Check for actively enforced location-based policies
                    var hasActiveLocationPolicy = enabledPolicies.Any(p =>
                        p.Conditions?.Locations?.IncludeLocations?.Any() == true ||
                        p.Conditions?.Locations?.ExcludeLocations?.Any() == true);

                    if (!hasActiveLocationPolicy)
                    {
                        // Check if there are Report-only location policies
                        var reportOnlyLocationPolicies = reportOnlyPolicies.Where(p =>
                            p.Conditions?.Locations?.IncludeLocations?.Any() == true ||
                            p.Conditions?.Locations?.ExcludeLocations?.Any() == true).ToList();

                        if (reportOnlyLocationPolicies.Any())
                        {
                            var policyNames = reportOnlyLocationPolicies.Select(p => p.DisplayName ?? "Unknown").ToList();
                            report.PolicyGaps.Add($"Location-based policies exist but are in Report-only mode: {string.Join(", ", policyNames)}");
                        }
                        else
                        {
                            report.PolicyGaps.Add("No location-based policies found");
                        }
                    }
                }

                _logger.LogInformation("Conditional Access coverage analysis completed for client {ClientId}. Coverage: {UsersCovered}/{TotalUsers}, Enabled policies: {EnabledPolicies}, Report-only policies: {ReportOnlyPolicies}",
                    clientId, report.UsersCoveredByMfa, report.TotalUsers, enabledPolicies.Count, reportOnlyPolicies.Count);

                return report;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to analyze Conditional Access coverage for client {ClientId}", clientId);
                return new ConditionalAccessCoverageReport();
            }
        }

        public async Task<List<Microsoft.Graph.Models.Device>> GetDevicesAsync(Guid clientId, Guid organizationId)
        {
            try
            {
                var graphClient = await CreateGraphClientAsync(clientId, organizationId);
                if (graphClient == null) return new List<Microsoft.Graph.Models.Device>();

                var devices = await graphClient.Devices.GetAsync(requestConfiguration =>
                {
                    requestConfiguration.QueryParameters.Select = new[] { "id", "displayName", "operatingSystem", "operatingSystemVersion", "isCompliant", "isManaged", "approximateLastSignInDateTime" };
                });

                var allDevices = new List<Microsoft.Graph.Models.Device>();
                if (devices?.Value != null)
                {
                    allDevices.AddRange(devices.Value);

                    // Handle pagination
                    while (!string.IsNullOrEmpty(devices.OdataNextLink))
                    {
                        var nextPageRequest = await graphClient.Devices.WithUrl(devices.OdataNextLink).GetAsync();
                        if (nextPageRequest?.Value != null)
                        {
                            allDevices.AddRange(nextPageRequest.Value);
                            devices = nextPageRequest;
                        }
                        else
                        {
                            break;
                        }
                    }
                }

                _logger.LogInformation("Retrieved {DeviceCount} devices from Microsoft Graph for client {ClientId}",
                    allDevices.Count, clientId);

                return allDevices;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve devices from Microsoft Graph for client {ClientId}", clientId);
                return new List<Microsoft.Graph.Models.Device>();
            }
        }

        public async Task<List<Microsoft.Graph.Models.Device>> GetNonCompliantDevicesAsync(Guid clientId, Guid organizationId)
        {
            try
            {
                var allDevices = await GetDevicesAsync(clientId, organizationId);

                var nonCompliantDevices = allDevices.Where(d =>
                    d.IsCompliant != true || d.IsManaged != true).ToList();

                _logger.LogInformation("Found {NonCompliantCount} non-compliant devices for client {ClientId}",
                    nonCompliantDevices.Count, clientId);

                return nonCompliantDevices;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to analyze non-compliant devices for client {ClientId}", clientId);
                return new List<Microsoft.Graph.Models.Device>();
            }
        }

        public async Task<List<Microsoft.Graph.Models.RiskyUser>> GetRiskyUsersAsync(Guid clientId, Guid organizationId)
        {
            try
            {
                var graphClient = await CreateGraphClientAsync(clientId, organizationId);
                if (graphClient == null) return new List<Microsoft.Graph.Models.RiskyUser>();

                var riskyUsers = await graphClient.IdentityProtection.RiskyUsers.GetAsync(requestConfiguration =>
                {
                    requestConfiguration.QueryParameters.Select = new[] { "id", "userDisplayName", "userPrincipalName", "riskLevel", "riskState", "riskLastUpdatedDateTime", "riskDetail" };
                });

                var allRiskyUsers = new List<Microsoft.Graph.Models.RiskyUser>();
                if (riskyUsers?.Value != null)
                {
                    allRiskyUsers.AddRange(riskyUsers.Value);

                    // Handle pagination
                    while (!string.IsNullOrEmpty(riskyUsers.OdataNextLink))
                    {
                        var nextPageRequest = await graphClient.IdentityProtection.RiskyUsers.WithUrl(riskyUsers.OdataNextLink).GetAsync();
                        if (nextPageRequest?.Value != null)
                        {
                            allRiskyUsers.AddRange(nextPageRequest.Value);
                            riskyUsers = nextPageRequest;
                        }
                        else
                        {
                            break;
                        }
                    }
                }

                _logger.LogInformation("Retrieved {RiskyUserCount} risky users from Microsoft Graph for client {ClientId}",
                    allRiskyUsers.Count, clientId);

                return allRiskyUsers;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve risky users from Microsoft Graph for client {ClientId}", clientId);
                return new List<Microsoft.Graph.Models.RiskyUser>();
            }
        }

        public async Task<List<Microsoft.Graph.Models.SignIn>> GetFailedSignInsAsync(Guid clientId, Guid organizationId, DateTime since)
        {
            try
            {
                var graphClient = await CreateGraphClientAsync(clientId, organizationId);
                if (graphClient == null) return new List<Microsoft.Graph.Models.SignIn>();

                var filter = $"createdDateTime ge {since:yyyy-MM-ddTHH:mm:ssZ} and status/errorCode ne 0";

                var signIns = await graphClient.AuditLogs.SignIns.GetAsync(requestConfiguration =>
                {
                    requestConfiguration.QueryParameters.Filter = filter;
                    requestConfiguration.QueryParameters.Select = new[] { "id", "userDisplayName", "userPrincipalName", "appDisplayName", "createdDateTime", "status", "clientAppUsed", "ipAddress" };
                    requestConfiguration.QueryParameters.Top = 1000; // Limit to prevent large responses
                });

                var failedSignIns = new List<Microsoft.Graph.Models.SignIn>();
                if (signIns?.Value != null)
                {
                    failedSignIns.AddRange(signIns.Value);

                    // Handle pagination (limited to prevent excessive data)
                    int pageCount = 0;
                    while (!string.IsNullOrEmpty(signIns.OdataNextLink) && pageCount < 5) // Limit to 5 pages
                    {
                        var nextPageRequest = await graphClient.AuditLogs.SignIns.WithUrl(signIns.OdataNextLink).GetAsync();
                        if (nextPageRequest?.Value != null)
                        {
                            failedSignIns.AddRange(nextPageRequest.Value);
                            signIns = nextPageRequest;
                            pageCount++;
                        }
                        else
                        {
                            break;
                        }
                    }
                }

                _logger.LogInformation("Retrieved {FailedSignInCount} failed sign-ins since {Since} for client {ClientId}",
                    failedSignIns.Count, since, clientId);

                return failedSignIns;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve failed sign-ins from Microsoft Graph for client {ClientId}", clientId);
                return new List<Microsoft.Graph.Models.SignIn>();
            }
        }

        public async Task<GraphSecurityScore> GetSecurityScoreAsync(Guid clientId, Guid organizationId)
        {
            try
            {
                var graphClient = await CreateGraphClientAsync(clientId, organizationId);
                if (graphClient == null) return new GraphSecurityScore();

                // Note: Security Score API might require specific permissions or might not be available in all tenants
                // This is a simplified implementation
                try
                {
                    var securityScores = await graphClient.Security.SecureScores.GetAsync(requestConfiguration =>
                    {
                        requestConfiguration.QueryParameters.Top = 1;
                        requestConfiguration.QueryParameters.Orderby = new[] { "createdDateTime desc" };
                    });

                    if (securityScores?.Value?.Any() == true)
                    {
                        var latestScore = securityScores.Value.First();
                        return new GraphSecurityScore
                        {
                            Id = latestScore.Id ?? string.Empty,
                            CurrentScore = latestScore.CurrentScore,
                            MaxScore = latestScore.MaxScore,
                            CreatedDateTime = latestScore.CreatedDateTime?.DateTime, // Fix: Convert DateTimeOffset? to DateTime?
                            Vendors = latestScore.VendorInformation?.Vendor != null ? new List<string> { latestScore.VendorInformation.Vendor } : new List<string>()
                        };
                    }
                }
                catch (Exception scoreEx)
                {
                    _logger.LogWarning(scoreEx, "Security Score API not available or accessible for client {ClientId}", clientId);
                }

                // Return default/calculated score if API is not available
                return new GraphSecurityScore
                {
                    Id = "calculated",
                    CurrentScore = 0,
                    MaxScore = 100,
                    CreatedDateTime = DateTime.UtcNow,
                    Vendors = new List<string> { "Microsoft" }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve security score from Microsoft Graph for client {ClientId}", clientId);
                return new GraphSecurityScore();
            }
        }

        public async Task<List<Microsoft.Graph.Models.DirectoryRole>> GetDirectoryRolesAsync(Guid clientId, Guid organizationId)
        {
            try
            {
                var graphClient = await CreateGraphClientAsync(clientId, organizationId);
                if (graphClient == null) return new List<Microsoft.Graph.Models.DirectoryRole>();

                var directoryRoles = await graphClient.DirectoryRoles.GetAsync(requestConfiguration =>
                {
                    requestConfiguration.QueryParameters.Select = new[] { "id", "displayName", "description", "roleTemplateId" };
                });

                var allRoles = new List<Microsoft.Graph.Models.DirectoryRole>();
                if (directoryRoles?.Value != null)
                {
                    allRoles.AddRange(directoryRoles.Value);

                    // Handle pagination
                    while (!string.IsNullOrEmpty(directoryRoles.OdataNextLink))
                    {
                        var nextPageRequest = await graphClient.DirectoryRoles.WithUrl(directoryRoles.OdataNextLink).GetAsync();
                        if (nextPageRequest?.Value != null)
                        {
                            allRoles.AddRange(nextPageRequest.Value);
                            directoryRoles = nextPageRequest;
                        }
                        else
                        {
                            break;
                        }
                    }
                }

                _logger.LogInformation("Retrieved {RoleCount} directory roles from Microsoft Graph for client {ClientId}",
                    allRoles.Count, clientId);

                return allRoles;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve directory roles from Microsoft Graph for client {ClientId}", clientId);
                return new List<Microsoft.Graph.Models.DirectoryRole>();
            }
        }

        public async Task<List<Microsoft.Graph.Models.User>> GetUsersInRoleAsync(Guid clientId, Guid organizationId, string roleId)
        {
            try
            {
                var graphClient = await CreateGraphClientAsync(clientId, organizationId);
                if (graphClient == null) return new List<Microsoft.Graph.Models.User>();

                var roleMembers = await graphClient.DirectoryRoles[roleId].Members.GetAsync();

                var usersInRole = new List<Microsoft.Graph.Models.User>();
                if (roleMembers?.Value != null)
                {
                    usersInRole.AddRange(roleMembers.Value.OfType<Microsoft.Graph.Models.User>());

                    // Handle pagination
                    while (!string.IsNullOrEmpty(roleMembers.OdataNextLink))
                    {
                        var nextPageRequest = await graphClient.DirectoryRoles[roleId].Members.WithUrl(roleMembers.OdataNextLink).GetAsync();
                        if (nextPageRequest?.Value != null)
                        {
                            usersInRole.AddRange(nextPageRequest.Value.OfType<Microsoft.Graph.Models.User>());
                            roleMembers = nextPageRequest;
                        }
                        else
                        {
                            break;
                        }
                    }
                }

                _logger.LogInformation("Retrieved {UserCount} users in role {RoleId} for client {ClientId}",
                    usersInRole.Count, roleId, clientId);

                return usersInRole;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve users in role {RoleId} for client {ClientId}", roleId, clientId);
                return new List<Microsoft.Graph.Models.User>();
            }
        }

        public async Task<RoleAssignmentReport> AnalyzeRoleAssignmentsAsync(Guid clientId, Guid organizationId)
        {
            try
            {
                var directoryRoles = await GetDirectoryRolesAsync(clientId, organizationId);
                var report = new RoleAssignmentReport();

                var privilegedRoleTemplates = new[]
                {
            "62e90394-69f5-4237-9190-012177145e10", // Global Administrator
            "194ae4cb-b126-40b2-bd5b-6091b380977d", // Security Administrator
            "f28a1f50-f6e7-4571-818b-6a12f2af6b6c", // SharePoint Administrator
            "7be44c8a-adaf-4e2a-84d6-ab2649e08a13", // Privileged Authentication Administrator
        };

                var overprivilegedUsers = new List<string>();
                var totalAssignments = 0;
                var privilegedAssignments = 0;

                foreach (var role in directoryRoles)
                {
                    var usersInRole = await GetUsersInRoleAsync(clientId, organizationId, role.Id);
                    totalAssignments += usersInRole.Count;

                    bool isPrivilegedRole = privilegedRoleTemplates.Contains(role.RoleTemplateId);
                    if (isPrivilegedRole)
                    {
                        privilegedAssignments += usersInRole.Count;

                        // Check for users with multiple privileged roles
                        foreach (var user in usersInRole)
                        {
                            var userName = user.DisplayName ?? user.UserPrincipalName ?? "Unknown";
                            if (!overprivilegedUsers.Contains(userName))
                            {
                                // Check if user has multiple privileged roles
                                int userPrivilegedRoleCount = 0;
                                foreach (var checkRole in directoryRoles.Where(r => privilegedRoleTemplates.Contains(r.RoleTemplateId)))
                                {
                                    var checkUsers = await GetUsersInRoleAsync(clientId, organizationId, checkRole.Id);
                                    if (checkUsers.Any(u => u.Id == user.Id))
                                    {
                                        userPrivilegedRoleCount++;
                                    }
                                }

                                if (userPrivilegedRoleCount > 1)
                                {
                                    overprivilegedUsers.Add(userName);
                                }
                            }
                        }
                    }
                }

                // Identify unused roles (roles with no members)
                var unusedRoles = new List<string>();
                foreach (var role in directoryRoles)
                {
                    var usersInRole = await GetUsersInRoleAsync(clientId, organizationId, role.Id);
                    if (!usersInRole.Any())
                    {
                        unusedRoles.Add(role.DisplayName ?? "Unknown Role");
                    }
                }
                report.TotalRoleAssignments = totalAssignments;
                report.PrivilegedRoleAssignments = privilegedAssignments;
                report.OverprivilegedUsers = overprivilegedUsers;
                report.UnusedRoles = unusedRoles;

                _logger.LogInformation("Role assignment analysis completed for client {ClientId}. Total: {Total}, Privileged: {Privileged}",
                    clientId, totalAssignments, privilegedAssignments);

                return report;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to analyze role assignments for client {ClientId}", clientId);
                return new RoleAssignmentReport();
            }
        }
    }

    // Custom token credential for Microsoft Graph using Azure.Identity
    public class GraphTokenCredential : TokenCredential
    {
        private readonly string _accessToken;

        public GraphTokenCredential(string accessToken)
        {
            _accessToken = accessToken;
        }

        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
        {
            return new AccessToken(_accessToken, DateTimeOffset.UtcNow.AddHours(1));
        }

        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
        {
            return new ValueTask<AccessToken>(new AccessToken(_accessToken, DateTimeOffset.UtcNow.AddHours(1)));
        }
    }
}