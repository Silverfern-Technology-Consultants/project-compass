using System.Collections.Generic;

namespace Compass.Core.Services.Naming;

/// <summary>
/// Azure resource type abbreviations based on Microsoft Cloud Adoption Framework
/// https://learn.microsoft.com/en-us/azure/cloud-adoption-framework/ready/azure-best-practices/resource-abbreviations
/// </summary>
public static class AzureResourceAbbreviations
{
    /// <summary>
    /// Official Microsoft abbreviations for Azure resource types
    /// </summary>
    public static readonly Dictionary<string, string[]> ResourceTypeAbbreviations = new(StringComparer.OrdinalIgnoreCase)
    {
        // AI and Machine Learning
        ["microsoft.cognitiveservices/accounts"] = new[] { "cog" },
        ["microsoft.machinelearningservices/workspaces"] = new[] { "mlw" },
        ["microsoft.search/searchservices"] = new[] { "srch" },

        // Analytics and IoT
        ["microsoft.analysisservices/servers"] = new[] { "as" },
        ["microsoft.databricks/workspaces"] = new[] { "dbw" },
        ["microsoft.datafactory/factories"] = new[] { "adf" },
        ["microsoft.datalakeanalytics/accounts"] = new[] { "dla" },
        ["microsoft.datalakestore/accounts"] = new[] { "dls" },
        ["microsoft.devices/iothubs"] = new[] { "iot" },
        ["microsoft.devices/provisioningservices"] = new[] { "provs" },
        ["microsoft.eventhub/namespaces"] = new[] { "evhns" },
        ["microsoft.eventhub/namespaces/eventhubs"] = new[] { "evh" },
        ["microsoft.hdinsight/clusters"] = new[] { "hdi" },
        ["microsoft.kusto/clusters"] = new[] { "dec" },
        ["microsoft.powerbi/workspacecollections"] = new[] { "pbiw" },
        ["microsoft.purview/accounts"] = new[] { "pview" },
        ["microsoft.streamanalytics/streamingjobs"] = new[] { "asa" },
        ["microsoft.synapse/workspaces"] = new[] { "syn" },
        ["microsoft.timeseriesinsights/environments"] = new[] { "tsi" },

        // Compute and Web
        ["microsoft.batch/batchaccounts"] = new[] { "ba" },
        ["microsoft.compute/availabilitysets"] = new[] { "avail" },
        ["microsoft.compute/cloudservices"] = new[] { "cld" },
        ["microsoft.compute/disks"] = new[] { "disk" },
        ["microsoft.compute/galleries"] = new[] { "gal" },
        ["microsoft.compute/snapshots"] = new[] { "snap" },
        ["microsoft.compute/virtualmachines"] = new[] { "vm" },
        ["microsoft.compute/virtualmachinescalesets"] = new[] { "vmss" },
        ["microsoft.servicefabric/clusters"] = new[] { "sf" },
        ["microsoft.web/serverfarms"] = new[] { "plan", "asp", "appplan" },
        ["microsoft.web/sites"] = new[] { "app", "webapp", "web" },
        ["microsoft.web/sites/slots"] = new[] { "slot" },
        ["microsoft.web/staticsites"] = new[] { "stapp" },

        // Containers
        ["microsoft.containerinstance/containergroups"] = new[] { "ci" },
        ["microsoft.containerregistry/registries"] = new[] { "cr", "acr", "registry" },
        ["microsoft.containerservice/managedclusters"] = new[] { "aks", "k8s", "kubernetes" },
        ["microsoft.servicefabricmesh/applications"] = new[] { "sfm" },
        ["microsoft.app/containerapps"] = new[] { "ca", "containerapp", "capp" },
        ["microsoft.app/managedenvironments"] = new[] { "cae" },

        // Databases
        ["microsoft.cache/redis"] = new[] { "redis" },
        ["microsoft.dbformariadb/servers"] = new[] { "mariadb" },
        ["microsoft.dbformysql/servers"] = new[] { "mysql" },
        ["microsoft.dbforpostgresql/servers"] = new[] { "psql" },
        ["microsoft.documentdb/databaseaccounts"] = new[] { "cosmos", "cosmosdb", "docdb" },
        ["microsoft.sql/managedinstances"] = new[] { "sqlmi" },
        ["microsoft.sql/servers"] = new[] { "sql", "sqlsrv", "sqlserver" },
        ["microsoft.sql/servers/databases"] = new[] { "sqldb", "db", "database" },
        ["microsoft.sql/servers/elasticpools"] = new[] { "sqlep" },
        ["microsoft.sqlvirtualmachine/sqlvirtualmachines"] = new[] { "sqlvm" },

        // Developer Tools
        ["microsoft.appconfiguration/configurationstores"] = new[] { "appcs" },
        ["microsoft.signalrservice/signalr"] = new[] { "sigr" },

        // DevOps
        ["microsoft.devtestlab/labs"] = new[] { "lab" },

        // Integration
        ["microsoft.apimanagement/service"] = new[] { "apim" },
        ["microsoft.logic/workflows"] = new[] { "logic" },
        ["microsoft.servicebus/namespaces"] = new[] { "sbns" },
        ["microsoft.servicebus/namespaces/queues"] = new[] { "sbq" },
        ["microsoft.servicebus/namespaces/topics"] = new[] { "sbt" },

        // Management and Governance
        ["microsoft.automation/automationaccounts"] = new[] { "aa" },
        ["microsoft.blueprint/blueprints"] = new[] { "bp" },
        ["microsoft.keyvault/vaults"] = new[] { "kv", "vault", "keyvault" },
        ["microsoft.managedidentity/userassignedidentities"] = new[] { "id" },
        ["microsoft.operationalinsights/workspaces"] = new[] { "log", "logs", "loganalytics", "law" },
        ["microsoft.operationsmanagement/solutions"] = new[] { "sol" },
        ["microsoft.portal/dashboards"] = new[] { "dash" },
        ["microsoft.resources/resourcegroups"] = new[] { "rg" },

        // Monitoring
        ["microsoft.insights/actiongroups"] = new[] { "ag" },
        ["microsoft.insights/components"] = new[] { "appi", "ai", "appinsights" },

        // Networking
        ["microsoft.cdn/profiles"] = new[] { "cdnp" },
        ["microsoft.cdn/profiles/endpoints"] = new[] { "cdne" },
        ["microsoft.classicnetwork/reservedips"] = new[] { "rip" },
        ["microsoft.network/applicationgateways"] = new[] { "agw" },
        ["microsoft.network/applicationsecuritygroups"] = new[] { "asg" },
        ["microsoft.network/azurefirewalls"] = new[] { "afw" },
        ["microsoft.network/bastionhosts"] = new[] { "bas" },
        ["microsoft.network/connections"] = new[] { "con" },
        ["microsoft.network/dnsresolvers"] = new[] { "dnspr" },
        ["microsoft.network/dnszones"] = new[] { "dnsz" },
        ["microsoft.network/expressroutecircuits"] = new[] { "erc" },
        ["microsoft.network/firewallpolicies"] = new[] { "afwp" },
        ["microsoft.network/frontdoors"] = new[] { "fd" },
        ["microsoft.network/frontdoorwebapplicationfirewallpolicies"] = new[] { "fdfp" },
        ["microsoft.network/loadbalancers"] = new[] { "lb", "loadbalancer", "elb" },
        ["microsoft.network/loadbalancers/inboundnatrules"] = new[] { "rule" },
        ["microsoft.network/localgateways"] = new[] { "lgw" },
        ["microsoft.network/natgateways"] = new[] { "ng" },
        ["microsoft.network/networkinterfaces"] = new[] { "nic" },
        ["microsoft.network/networksecuritygroups"] = new[] { "nsg", "securitygroup", "sg" },
        ["microsoft.network/networksecuritygroups/securityrules"] = new[] { "nsgsr" },
        ["microsoft.network/networkwatchers"] = new[] { "nw" },
        ["microsoft.network/privatednszones"] = new[] { "pdnsz" },
        ["microsoft.network/privateendpoints"] = new[] { "pep" },
        ["microsoft.network/privatelinkservices"] = new[] { "pl" },
        ["microsoft.network/publicipaddresses"] = new[] { "pip", "publicip", "ip" },
        ["microsoft.network/publicipprefixes"] = new[] { "ippre" },
        ["microsoft.network/routefilters"] = new[] { "rf" },
        ["microsoft.network/routetables"] = new[] { "rt" },
        ["microsoft.network/routetables/routes"] = new[] { "udr" },
        ["microsoft.network/serviceendpointpolicies"] = new[] { "se" },
        ["microsoft.network/trafficmanagerprofiles"] = new[] { "traf" },
        ["microsoft.network/virtualnetworkgateways"] = new[] { "vgw" },
        ["microsoft.network/virtualnetworks"] = new[] { "vnet", "vn", "virtualnet" },
        ["microsoft.network/virtualnetworks/subnets"] = new[] { "snet", "subnet", "sub" },
        ["microsoft.network/virtualnetworks/virtualnetworkpeerings"] = new[] { "peer" },
        ["microsoft.network/virtualwans"] = new[] { "vwan" },
        ["microsoft.network/vpngateways"] = new[] { "vpng" },
        ["microsoft.network/vpnserverconfigurations"] = new[] { "vpnsc" },
        ["microsoft.network/vpnsites"] = new[] { "vpns" },

        // Security
        ["microsoft.aad/domainservices"] = new[] { "aadds" },
        ["microsoft.keyvault/managedhsms"] = new[] { "kvmhsm" },

        // Storage
        ["microsoft.netapp/netappaccounts"] = new[] { "anf" },
        ["microsoft.netapp/netappaccounts/capacitypools"] = new[] { "anfcp" },
        ["microsoft.netapp/netappaccounts/capacitypools/volumes"] = new[] { "anfv" },
        ["microsoft.storage/storageaccounts"] = new[] { "st", "stg", "stor", "storage" },
        ["microsoft.storagesync/storagesyncservices"] = new[] { "sss" },
        ["microsoft.storsimple/managers"] = new[] { "ssimp" },

        // Web
        ["microsoft.certificateregistration/certificateorders"] = new[] { "cert" },
        ["microsoft.domainregistration/domains"] = new[] { "dom" },
        ["microsoft.notificationhubs/namespaces"] = new[] { "ntfns" },
        ["microsoft.notificationhubs/namespaces/notificationhubs"] = new[] { "ntf" }
    };

    /// <summary>
    /// Get the primary abbreviation for a resource type
    /// </summary>
    public static string GetPrimaryAbbreviation(string resourceType)
    {
        if (ResourceTypeAbbreviations.TryGetValue(resourceType, out var abbreviations))
        {
            return abbreviations[0];
        }
        return "res"; // Default fallback
    }

    /// <summary>
    /// Get all valid abbreviations for a resource type
    /// </summary>
    public static string[] GetValidAbbreviations(string resourceType)
    {
        return ResourceTypeAbbreviations.TryGetValue(resourceType, out var abbreviations)
            ? abbreviations
            : new[] { "res" };
    }

    /// <summary>
    /// Check if an abbreviation is valid for a resource type
    /// </summary>
    public static bool IsValidAbbreviation(string resourceType, string abbreviation)
    {
        if (ResourceTypeAbbreviations.TryGetValue(resourceType, out var validAbbreviations))
        {
            return validAbbreviations.Contains(abbreviation, StringComparer.OrdinalIgnoreCase);
        }
        return abbreviation.Equals("res", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Get abbreviation considering both resource type and kind (for Function Apps, etc.)
    /// </summary>
    public static string GetAbbreviationWithKind(string resourceType, string? resourceKind = null)
    {
        var typeLower = resourceType.ToLowerInvariant();
        var kindLower = resourceKind?.ToLowerInvariant();

        // Special handling for Function Apps (type = microsoft.web/sites, kind = functionapp,linux)
        if (typeLower == "microsoft.web/sites" && !string.IsNullOrEmpty(kindLower) && kindLower.Contains("functionapp"))
        {
            return "func";
        }

        // Use standard abbreviation
        return GetPrimaryAbbreviation(resourceType);
    }

    /// <summary>
    /// Check if a string is a known resource type abbreviation
    /// </summary>
    public static bool IsKnownAbbreviation(string abbreviation)
    {
        return ResourceTypeAbbreviations.Values
            .Any(abbreviations => abbreviations.Contains(abbreviation, StringComparer.OrdinalIgnoreCase));
    }
}