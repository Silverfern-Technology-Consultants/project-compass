namespace Compass.Core.Models.Assessment;

// Supporting models for Graph analysis
public class ConditionalAccessCoverageReport
{
    public int TotalUsers { get; set; }
    public int UsersCoveredByMfa { get; set; }
    public int UsersWithoutMfa { get; set; }
    public List<string> UncoveredUsers { get; set; } = new();
    public List<string> PolicyGaps { get; set; } = new();
}

public class RoleAssignmentReport
{
    public int TotalRoleAssignments { get; set; }
    public int PrivilegedRoleAssignments { get; set; }
    public List<string> OverprivilegedUsers { get; set; } = new();
    public List<string> UnusedRoles { get; set; } = new();
}

// Custom model for SecurityScore (since Microsoft.Graph.Models.SecurityScore may not exist)
public class GraphSecurityScore
{
    public string Id { get; set; } = string.Empty;
    public double? CurrentScore { get; set; }
    public double? MaxScore { get; set; }
    public DateTime? CreatedDateTime { get; set; }
    public List<string> Vendors { get; set; } = new();
}