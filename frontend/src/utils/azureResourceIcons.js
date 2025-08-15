import React from 'react';
import {
    CheckCircle, AlertTriangle, XCircle, FileText, BarChart3,
    Server, Shield, User, Clock, Calendar, Activity, TrendingUp,
    Settings, HardDrive, Database, Network, Zap, Globe, Tag, Target
} from 'lucide-react';

// Azure Icon Component - Now working with correct PUBLIC_URL path
export const AzureIcon = ({ iconPath, alt, size = 16, fallbackIcon = null }) => {
    const [imageError, setImageError] = React.useState(false);
    
    const fullPath = `${process.env.PUBLIC_URL}/AzureIcons/${iconPath}`;
    
    if (imageError) {
        return (
            <div style={{ width: size, height: size, display: 'inline-block' }}>
                {fallbackIcon}
            </div>
        );
    }
    
    return (
        <img 
            src={fullPath}
            alt={alt}
            style={{ width: size, height: size }}
            onError={() => setImageError(true)}
        />
    );
};

// Resource type mapping with Azure icons and fallbacks
const resourceTypeMap = {
    'keyvaults': {
        name: 'Key Vaults',
        description: 'Secrets & certificate management',
        iconPath: 'security/10245-icon-service-Key-Vaults.svg',
        fallbackIcon: <Shield size={16} className="text-yellow-400" />,
        category: 'Security'
    },
    'components': {
        name: 'Application Insights', 
        description: 'Application performance monitoring',
        iconPath: 'monitor/00012-icon-service-Application-Insights.svg',
        fallbackIcon: <BarChart3 size={16} className="text-orange-400" />,
        category: 'Monitoring'
    },
    'managedidentities': {
        name: 'Managed Identities',
        description: 'Azure AD identities for services',
        iconPath: 'identity/10227-icon-service-Managed-Identities.svg',
        fallbackIcon: <User size={16} className="text-blue-400" />,
        category: 'Security'
    },
    'workspaces': {
        name: 'Log Analytics Workspaces',
        description: 'Centralized logging & monitoring',
        iconPath: 'monitor/00009-icon-service-Log-Analytics-Workspaces.svg',
        fallbackIcon: <BarChart3 size={16} className="text-purple-400" />,
        category: 'Monitoring'
    },
    'storageaccounts': {
        name: 'Storage Accounts',
        description: 'Blob, file, queue & table storage',
        iconPath: 'storage/10086-icon-service-Storage-Accounts.svg',
        fallbackIcon: <HardDrive size={16} className="text-green-400" />,
        category: 'Storage'
    },
    'databases': {
        name: 'SQL Databases',
        description: 'Managed relational databases',
        iconPath: 'databases/10130-icon-service-SQL-Database.svg',
        fallbackIcon: <Database size={16} className="text-cyan-400" />,
        category: 'Database'
    },
    'servers': {
        name: 'SQL Servers',
        description: 'SQL Server hosting instances',
        iconPath: 'databases/10132-icon-service-SQL-Server.svg',
        fallbackIcon: <Database size={16} className="text-purple-400" />,
        category: 'Database'
    },
    'sites': {
        name: 'Functions/Logic Apps',
        description: 'Serverless applications',
        iconPath: 'compute/10029-icon-service-Function-Apps.svg',
        fallbackIcon: <Zap size={16} className="text-purple-400" />,
        category: 'Compute'
    },
    'vaults': {
        name: 'Key Vaults',
        description: 'Secrets & certificate management', 
        iconPath: 'security/10245-icon-service-Key-Vaults.svg',
        fallbackIcon: <Shield size={16} className="text-yellow-400" />,
        category: 'Security'
    },
    'recoveryvaults': {
        name: 'Recovery Services Vaults',
        description: 'Backup and disaster recovery',
        iconPath: 'storage/00017-icon-service-Recovery-Services-Vaults.svg',
        fallbackIcon: <Shield size={16} className="text-blue-400" />,
        category: 'Storage'
    },
    'virtualnetworks': {
        name: 'Virtual Networks',
        description: 'Network isolation and connectivity',
        iconPath: 'networking/10061-icon-service-Virtual-Networks.svg',
        fallbackIcon: <Network size={16} className="text-blue-400" />,
        category: 'Networking'
    },
    'virtualmachines': {
        name: 'Virtual Machines',
        description: 'Scalable compute resources',
        iconPath: 'compute/10021-icon-service-Virtual-Machine.svg',
        fallbackIcon: <Server size={16} className="text-green-400" />,
        category: 'Compute'
    },
    'networkinterfaces': {
        name: 'Network Interfaces',
        description: 'VM network connectivity',
        iconPath: 'networking/10080-icon-service-Network-Interfaces.svg',
        fallbackIcon: <Network size={16} className="text-gray-400" />,
        category: 'Networking'
    },
    'disks': {
        name: 'Managed Disks',
        description: 'Persistent storage for VMs',
        iconPath: 'compute/10032-icon-service-Disks.svg',
        fallbackIcon: <HardDrive size={16} className="text-orange-400" />,
        category: 'Compute'
    },
    'networksecuritygroups': {
        name: 'Network Security Groups',
        description: 'Network access control',
        iconPath: 'networking/10067-icon-service-Network-Security-Groups.svg',
        fallbackIcon: <Shield size={16} className="text-red-400" />,
        category: 'Security'
    },
    'containerregistries': {
        name: 'Container Registries',
        description: 'Container image storage',
        iconPath: 'containers/10105-icon-service-Container-Registries.svg',
        fallbackIcon: <Database size={16} className="text-blue-400" />,
        category: 'Containers'
    },
    'kubernetesservices': {
        name: 'Kubernetes Services',
        description: 'Managed Kubernetes clusters',
        iconPath: 'containers/10023-icon-service-Kubernetes-Services.svg',
        fallbackIcon: <Server size={16} className="text-blue-400" />,
        category: 'Compute'
    },
    'userassignedidentities': {
        name: 'User Assigned Identities',
        description: 'User-managed identities for applications',
        iconPath: 'identity/10227-icon-service-Managed-Identities.svg',
        fallbackIcon: <User size={16} className="text-blue-400" />,
        category: 'Security'
    },
    'containerapps': {
        name: 'Container Apps',
        description: 'Serverless containerized applications',
        iconPath: 'containers/10104-icon-service-Container-Instances.svg',
        fallbackIcon: <Server size={16} className="text-purple-400" />,
        category: 'Containers'
    },
    'serverfarms': {
        name: 'App Service Plans',
        description: 'Hosting plans for web applications',
        iconPath: 'compute/10035-icon-service-App-Services.svg',
        fallbackIcon: <Server size={16} className="text-green-400" />,
        category: 'Compute'
    },
    'managedenvironments': {
        name: 'Container App Environments',
        description: 'Managed environments for container apps',
        iconPath: 'containers/10104-icon-service-Container-Instances.svg',
        fallbackIcon: <Network size={16} className="text-purple-400" />,
        category: 'Containers'
    },
    'staticsites': {
        name: 'Static Web Apps',
        description: 'Static website hosting',
        iconPath: 'web/01007-icon-service-Static-Apps.svg',
        fallbackIcon: <Globe size={16} className="text-cyan-400" />,
        category: 'Web'
    },
    'actiongroups': {
        name: 'Action Groups',
        description: 'Notification and automation groups',
        iconPath: 'monitor/10066-icon-service-Network-Watcher.svg',
        fallbackIcon: <Settings size={16} className="text-orange-400" />,
        category: 'Monitoring'
    }
};

// Main function to get resource type information
export const getResourceTypeInfo = (resourceType, iconSize = 16) => {
    const type = resourceType.toLowerCase();
    
    if (resourceTypeMap[type]) {
        const info = resourceTypeMap[type];
        return {
            ...info,
            icon: (
                <AzureIcon 
                    iconPath={info.iconPath}
                    alt={info.name}
                    size={iconSize}
                    fallbackIcon={React.cloneElement(info.fallbackIcon, { size: iconSize })}
                />
            )
        };
    }

    // Default fallback for unknown resource types
    return {
        name: resourceType.charAt(0).toUpperCase() + resourceType.slice(1),
        description: 'Azure resource',
        icon: (
            <AzureIcon 
                iconPath="general/10007-icon-service-Resource-Groups.svg"
                alt="Azure Resource"
                size={iconSize}
                fallbackIcon={<FileText size={iconSize} className="text-gray-400" />}
            />
        ),
        category: 'Other'
    };
};

// Helper function to get just the icon for a resource type
export const getResourceTypeIcon = (resourceType, iconSize = 16) => {
    return getResourceTypeInfo(resourceType, iconSize).icon;
};

// Helper function to get category color class
export const getCategoryColor = (category) => {
    const categoryColors = {
        'Security': 'text-red-400',
        'Monitoring': 'text-orange-400', 
        'Storage': 'text-green-400',
        'Database': 'text-cyan-400',
        'Compute': 'text-blue-400',
        'Networking': 'text-purple-400',
        'Containers': 'text-indigo-400',
        'Web': 'text-pink-400',
        'Other': 'text-gray-400'
    };
    
    return categoryColors[category] || 'text-gray-400';
};

export default { getResourceTypeInfo, getResourceTypeIcon, getCategoryColor, AzureIcon };
