using System.Text.Json;
using System.Text.RegularExpressions;
using Compass.Core.Models;
using Compass.Core.Services.Naming;

namespace Compass.Core.Models.Assessment;

// NEW: NAMING SCHEME CONFIGURATION MODELS
public class NamingSchemeConfiguration
{
    public List<NamingComponent> Components { get; set; } = new();
    public string Separator { get; set; } = "-";
    public string CaseFormat { get; set; } = "lowercase"; // "lowercase", "uppercase", "camelCase", "PascalCase"
    public bool IsActive { get; set; } = true;
    public List<NamingSchemeExample> Examples { get; set; } = new();
}

public class NamingComponent
{
    public string ComponentType { get; set; } = string.Empty; // "company", "environment", "service", "resource-type", "instance"
    public int Position { get; set; } // 1, 2, 3, 4, 5
    public bool IsRequired { get; set; } = true;
    public string Format { get; set; } = string.Empty; // "3-letter abbreviation", "lowercase", "zero-padded numbers"
    public List<string> AllowedValues { get; set; } = new(); // e.g., ["prod", "dev", "shared"] for environment
    public string DefaultValue { get; set; } = string.Empty; // Default value when generating examples
    public ComponentValidationRules ValidationRules { get; set; } = new();
}

public class ComponentValidationRules
{
    public int? MinLength { get; set; }
    public int? MaxLength { get; set; }
    public string? Pattern { get; set; } // Regex pattern
    public bool AllowNumbers { get; set; } = true;
    public bool AllowSpecialChars { get; set; } = false;
    public List<string> ReservedWords { get; set; } = new(); // Words that shouldn't be used
}

public class NamingSchemeExample
{
    public string ResourceType { get; set; } = string.Empty; // "microsoft.compute/virtualmachines"
    public string ExampleName { get; set; } = string.Empty; //
    public Dictionary<string, string> ComponentValues { get; set; } = new();
    public bool IsValid { get; set; } = true;
    public List<string> ValidationMessages { get; set; } = new();
}

public class ComponentDefinition
{
    public string ComponentType { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string DefaultFormat { get; set; } = string.Empty;
    public List<string> CommonValues { get; set; } = new();
    public bool IsSystemDefined { get; set; } = true; // false for custom components
}

public class NamingValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Messages { get; set; } = new();
}

public class NamingSchemeValidationResult
{
    public AzureResource Resource { get; set; } = new();
    public bool IsCompliant { get; set; }
    public string Message { get; set; } = string.Empty;
    public Dictionary<string, string> DetectedComponents { get; set; } = new();
    public List<string> MissingComponents { get; set; } = new();
    public List<string> InvalidComponents { get; set; } = new();
    public string? SuggestedName { get; set; }
}