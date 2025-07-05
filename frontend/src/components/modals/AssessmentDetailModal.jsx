import React, { useState, useEffect } from 'react';
import { createPortal } from 'react-dom';
import {
    X, CheckCircle, AlertTriangle, XCircle, FileText, BarChart3, Download,
    Eye, Filter, Search, ChevronLeft, ChevronRight, Server, Database, Network,
    ChevronDown, ChevronUp, Settings, User, Clock, Shield, AlertCircle,
    Target, Zap, Calendar, TrendingUp, Tag, Folder, MapPin, Activity,
    HardDrive, Globe
} from 'lucide-react';
import { assessmentApi, apiUtils } from '../../services/apiService';


const getResourceTypeInfo = (resourceType) => {
    const type = resourceType.toLowerCase();

    // Comprehensive Azure resource type mappings
    const resourceTypeMap = {
        // Compute
        'virtualmachines': {
            name: 'Virtual Machines',
            description: 'Cloud compute instances (VMs)',
            icon: <Server size={16} className="text-red-400" />,
            category: 'Compute'
        },
        'serverfarms': {
            name: 'App Service Plans',
            description: 'Hosting plans for web applications',
            icon: <Server size={16} className="text-blue-400" />,
            category: 'Compute'
        },
        'sites': {
            name: 'App Services',
            description: 'Web apps and API hosting',
            icon: <Globe size={16} className="text-green-400" />,
            category: 'Compute'
        },
        'functionapp': {
            name: 'Function Apps',
            description: 'Serverless compute functions',
            icon: <Zap size={16} className="text-yellow-400" />,
            category: 'Compute'
        },
        'containerapps': {
            name: 'Container Apps',
            description: 'Serverless containerized applications',
            icon: <Server size={16} className="text-cyan-400" />,
            category: 'Compute'
        },
        'managedenvironments': {
            name: 'Container App Environments',
            description: 'Managed environments for container apps',
            icon: <Server size={16} className="text-cyan-400" />,
            category: 'Compute'
        },
        'vmscalesets': {
            name: 'VM Scale Sets',
            description: 'Auto-scaling virtual machine groups',
            icon: <Server size={16} className="text-orange-400" />,
            category: 'Compute'
        },

        // Storage
        'storageaccounts': {
            name: 'Storage Accounts',
            description: 'Blob, file, queue & table storage',
            icon: <HardDrive size={16} className="text-orange-400" />,
            category: 'Storage'
        },
        'disks': {
            name: 'Managed Disks',
            description: 'Persistent storage for VMs',
            icon: <HardDrive size={16} className="text-gray-400" />,
            category: 'Storage'
        },

        // Database
        'databases': {
            name: 'SQL Databases',
            description: 'Managed relational databases',
            icon: <Database size={16} className="text-blue-400" />,
            category: 'Database'
        },
        'servers': {
            name: 'Database Servers',
            description: 'SQL Server hosting instances',
            icon: <Database size={16} className="text-purple-400" />,
            category: 'Database'
        },
        'cosmosdb': {
            name: 'Cosmos DB',
            description: 'Multi-model NoSQL database',
            icon: <Database size={16} className="text-purple-400" />,
            category: 'Database'
        },
        'mysql': {
            name: 'MySQL Databases',
            description: 'Managed MySQL database service',
            icon: <Database size={16} className="text-blue-400" />,
            category: 'Database'
        },
        'postgresql': {
            name: 'PostgreSQL Databases',
            description: 'Managed PostgreSQL database service',
            icon: <Database size={16} className="text-blue-400" />,
            category: 'Database'
        },

        // Networking
        'virtualnetworks': {
            name: 'Virtual Networks',
            description: 'Private network infrastructure',
            icon: <Network size={16} className="text-indigo-400" />,
            category: 'Networking'
        },
        'loadbalancers': {
            name: 'Load Balancers',
            description: 'Traffic distribution and high availability',
            icon: <Network size={16} className="text-green-400" />,
            category: 'Networking'
        },
        'publicipaddresses': {
            name: 'Public IP Addresses',
            description: 'Internet-facing IP addresses',
            icon: <Network size={16} className="text-yellow-400" />,
            category: 'Networking'
        },
        'networkinterfaces': {
            name: 'Network Interfaces',
            description: 'VM network connectivity',
            icon: <Network size={16} className="text-gray-400" />,
            category: 'Networking'
        },
        'networksecuritygroups': {
            name: 'Network Security Groups',
            description: 'Network-level security rules',
            icon: <Shield size={16} className="text-red-400" />,
            category: 'Networking'
        },
        'applicationgateways': {
            name: 'Application Gateways',
            description: 'Web application firewall & load balancer',
            icon: <Network size={16} className="text-purple-400" />,
            category: 'Networking'
        },

        // Security & Identity
        'vaults': {
            name: 'Key Vaults',
            description: 'Secrets & certificate management',
            icon: <Shield size={16} className="text-yellow-400" />,
            category: 'Security'
        },
        'userassignedidentities': {
            name: 'Managed Identities',
            description: 'Azure AD identities for services',
            icon: <User size={16} className="text-green-400" />,
            category: 'Security'
        },
        'actiongroups': {
            name: 'Action Groups',
            description: 'Alert notification and automation',
            icon: <AlertTriangle size={16} className="text-orange-400" />,
            category: 'Monitoring'
        },

        // Monitoring & Management
        'components': {
            name: 'Application Insights',
            description: 'Application performance monitoring',
            icon: <BarChart3 size={16} className="text-blue-400" />,
            category: 'Monitoring'
        },
        'workspaces': {
            name: 'Log Analytics Workspaces',
            description: 'Centralized logging & monitoring',
            icon: <Database size={16} className="text-purple-400" />,
            category: 'Monitoring'
        },
        'smartdetectoralertrules': {
            name: 'Smart Detector Alerts',
            description: 'AI-powered application monitoring',
            icon: <AlertTriangle size={16} className="text-red-400" />,
            category: 'Monitoring'
        },
        'autoscalesettings': {
            name: 'Autoscale Settings',
            description: 'Automatic resource scaling rules',
            icon: <TrendingUp size={16} className="text-green-400" />,
            category: 'Monitoring'
        },
        'staticsites': {
            name: 'Static Web Apps',
            description: 'Serverless static website hosting',
            icon: <Globe size={16} className="text-cyan-400" />,
            category: 'Web'
        },

        // Integration & Messaging
        'servicebus': {
            name: 'Service Bus',
            description: 'Enterprise messaging service',
            icon: <Network size={16} className="text-purple-400" />,
            category: 'Integration'
        },
        'eventhubs': {
            name: 'Event Hubs',
            description: 'Big data streaming platform',
            icon: <Network size={16} className="text-orange-400" />,
            category: 'Integration'
        },
        'logicapps': {
            name: 'Logic Apps',
            description: 'Workflow automation and integration',
            icon: <Zap size={16} className="text-blue-400" />,
            category: 'Integration'
        },

        // AI & Analytics
        'searchservices': {
            name: 'Cognitive Search',
            description: 'AI-powered search service',
            icon: <Search size={16} className="text-purple-400" />,
            category: 'AI/ML'
        },
        'cognitiveservices': {
            name: 'Cognitive Services',
            description: 'AI and machine learning APIs',
            icon: <BarChart3 size={16} className="text-green-400" />,
            category: 'AI/ML'
        }
    };

    // Try to find exact match first
    if (resourceTypeMap[type]) {
        return resourceTypeMap[type];
    }

    // Try partial matches for complex resource types
    for (const [key, info] of Object.entries(resourceTypeMap)) {
        if (type.includes(key) || key.includes(type)) {
            return info;
        }
    }

    // Default fallback for unknown types
    return {
        name: resourceType.charAt(0).toUpperCase() + resourceType.slice(1),
        description: 'Azure resource',
        icon: <FileText size={16} className="text-gray-400" />,
        category: 'Other'
    };
};

const AssessmentDetailModal = ({ isOpen, onClose, assessment }) => {
    const [findings, setFindings] = useState([]);
    const [resources, setResources] = useState([]);
    const [resourceFilters, setResourceFilters] = useState({});
    const [resourceSearch, setResourceSearch] = useState('');
    const [resourcePage, setResourcePage] = useState(1);
    const [resourceTotalPages, setResourceTotalPages] = useState(1);
    const [resourceTotalCount, setResourceTotalCount] = useState(0);
    const [clientPreferences, setClientPreferences] = useState(null);
    const [showAllResourceTypes, setShowAllResourceTypes] = useState(false);
    const [selectedFilters, setSelectedFilters] = useState({
        resourceType: '',
        resourceGroup: '',
        location: '',
        environment: ''
    });
    const [loading, setLoading] = useState(false);
    const [error, setError] = useState(null);
    const [activeTab, setActiveTab] = useState('overview');
    const [tabLoading, setTabLoading] = useState({});
    const [expandedFindings, setExpandedFindings] = useState({});
    const [expandedCategories, setExpandedCategories] = useState({});

    // NEW: Track tab states per assessment ID
    const [assessmentTabStates, setAssessmentTabStates] = useState({});

    const loadClientPreferences = async () => {
        if (!assessment.clientId && !assessment.ClientId) return;

        try {
            console.log('[AssessmentDetailModal] Loading client preferences for client:', assessment.clientId || assessment.ClientId);

            // Try different import approaches based on your apiService structure
            try {
                // Option 1: Try importing from the same service used elsewhere
                const { apiService } = await import('../../services/apiService');

                // Try to call the client preferences endpoint directly
                const clientId = assessment.clientId || assessment.ClientId;
                const response = await apiService.get(`/api/ClientPreferences/${clientId}`);

                console.log('[AssessmentDetailModal] Loaded client preferences:', response.data);
                setClientPreferences(response.data);

            } catch (importError) {
                console.log('[AssessmentDetailModal] Direct API call approach...');

                // Option 2: Use fetch directly if the import fails
                const clientId = assessment.clientId || assessment.ClientId;
                const response = await fetch(`${process.env.REACT_APP_API_URL || 'https://localhost:7163'}/api/ClientPreferences/${clientId}`, {
                    method: 'GET',
                    headers: {
                        'Content-Type': 'application/json',
                        'Authorization': `Bearer ${localStorage.getItem('token') || sessionStorage.getItem('token')}`
                    }
                });

                if (response.ok) {
                    const preferences = await response.json();
                    console.log('[AssessmentDetailModal] Loaded client preferences via fetch:', preferences);
                    setClientPreferences(preferences);
                } else {
                    console.log('[AssessmentDetailModal] Client preferences API returned:', response.status);
                    // Set empty preferences to stop loading state
                    setClientPreferences({});
                }
            }

        } catch (error) {
            console.error('[AssessmentDetailModal] Failed to load client preferences:', error);
            // Set empty preferences to stop loading state and show fallback content
            setClientPreferences({});
        }
    };

    useEffect(() => {
        if (isOpen && assessment?.id) {
            console.log('[AssessmentDetailModal] Loading assessment details for:', assessment.id);
            console.log('[AssessmentDetailModal] Assessment object:', assessment);

            // Load saved tab state for this specific assessment
            const assessmentId = assessment.id || assessment.AssessmentId || assessment.assessmentId;
            const savedTab = assessmentTabStates[assessmentId] || 'overview';
            setActiveTab(savedTab);

            loadFindings();
            loadClientPreferences(); // ADD THIS LINE TO THE EXISTING useEffect
        }
    }, [isOpen, assessment]);

    const loadFindings = async () => {
        try {
            setLoading(true);
            setError(null);
            console.log('[AssessmentDetailModal] Fetching findings for assessment:', assessment.id);

            const results = await assessmentApi.getAssessmentFindings(assessment.id);
            console.log('[AssessmentDetailModal] Raw findings response:', results);
            console.log('[AssessmentDetailModal] First finding structure:', results?.[0]);

            setFindings(results || []);
        } catch (err) {
            console.error('[AssessmentDetailModal] Error loading findings:', err);
            const errorInfo = apiUtils.handleApiError(err);
            setError(errorInfo.message);
        } finally {
            setLoading(false);
        }
    };
    const loadResources = async (page = 1, resetSearch = false) => {
        try {
            setTabLoading(prev => ({ ...prev, resources: true }));
            setError(null);

            const searchTerm = resetSearch ? '' : resourceSearch;
            if (resetSearch) {
                setResourceSearch('');
            }

            console.log('[AssessmentDetailModal] Fetching resources for assessment:', assessment.id);

            const params = new URLSearchParams({
                page: page.toString(),
                limit: '50'
            });

            if (searchTerm) params.append('search', searchTerm);
            if (selectedFilters.resourceType) params.append('resourceType', selectedFilters.resourceType);
            if (selectedFilters.resourceGroup) params.append('resourceGroup', selectedFilters.resourceGroup);
            if (selectedFilters.location) params.append('location', selectedFilters.location);
            if (selectedFilters.environment) params.append('environmentFilter', selectedFilters.environment);

            const response = await assessmentApi.getAssessmentResources(assessment.id, params.toString());

            console.log('[AssessmentDetailModal] Raw API response:', response);

            // Force PascalCase reading - backend returns PascalCase
            setResources(response.Resources || []);
            setResourceFilters(response.Filters || {});
            setResourcePage(response.Page || 1);
            setResourceTotalPages(response.TotalPages || 1);
            setResourceTotalCount(response.TotalCount || 0);

            console.log('[AssessmentDetailModal] Loaded resources count:', (response.Resources || []).length);
            console.log('[AssessmentDetailModal] First resource structure:', response.Resources?.[0]);
            console.log('[AssessmentDetailModal] First resource properties:', response.Resources?.[0] ? Object.keys(response.Resources[0]) : 'No resources');
        } catch (err) {
            console.error('[AssessmentDetailModal] Error loading resources:', err);
            const errorInfo = apiUtils.handleApiError(err);
            setError(errorInfo.message);
        } finally {
            setTabLoading(prev => ({ ...prev, resources: false }));
        }
    };

    const handleTabChange = async (tab) => {
        setActiveTab(tab);
        setTabLoading(prev => ({ ...prev, [tab]: true }));

        // NEW: Save tab state for this specific assessment
        const assessmentId = assessment.id || assessment.AssessmentId || assessment.assessmentId;
        setAssessmentTabStates(prev => ({
            ...prev,
            [assessmentId]: tab
        }));

        // Load data for specific tabs
        if (tab === 'resources' && resources.length === 0) {
            await loadResources(1, true);
        } else {
            // Simulate loading for other tabs
            setTimeout(() => {
                setTabLoading(prev => ({ ...prev, [tab]: false }));
            }, 300);
        }
    };

    const handleExport = async (format) => {
        try {
            console.log(`[AssessmentDetailModal] Exporting resources as ${format.toUpperCase()}`);

            const response = await (format === 'csv'
                ? assessmentApi.exportResourcesCsv(assessment.id)
                : assessmentApi.exportResourcesExcel(assessment.id));

            if (!response.ok) {
                throw new Error(`Export failed: ${response.statusText}`);
            }

            // Get filename from Content-Disposition header or create one
            const contentDisposition = response.headers.get('Content-Disposition');
            let filename = `resources.${format}`;

            if (contentDisposition) {
                // Try RFC 5987 encoded filename first (filename*=UTF-8'')
                let filenameStarMatch = contentDisposition.match(/filename\*=UTF-8''([^;]+)/i);

                if (filenameStarMatch && filenameStarMatch[1]) {
                    filename = decodeURIComponent(filenameStarMatch[1]);
                } else {
                    // Fallback to regular filename= (without quotes)
                    const filenameMatch = contentDisposition.match(/filename=([^;]+)/i);
                    if (filenameMatch && filenameMatch[1]) {
                        filename = filenameMatch[1].trim();
                    }
                }
            }

            // Download the file
            const blob = await response.blob();
            const downloadUrl = window.URL.createObjectURL(blob);
            const link = document.createElement('a');
            link.href = downloadUrl;
            link.download = filename;
            document.body.appendChild(link);
            link.click();
            document.body.removeChild(link);
            window.URL.revokeObjectURL(downloadUrl);

            console.log(`[AssessmentDetailModal] Successfully exported ${format.toUpperCase()}: ${filename}`);
        } catch (error) {
            console.error(`[AssessmentDetailModal] Export ${format} failed:`, error);
            alert(`Failed to export ${format.toUpperCase()}: ${error.message}`);
        }
    };

    const getResourceIcon = (resourceTypeName) => {
        if (!resourceTypeName) return <FileText size={16} className="text-gray-400" />;

        const type = resourceTypeName.toLowerCase();
        if (type.includes('virtualmachine') || type.includes('vm')) {
            return <Server size={16} className="text-blue-400" />;
        }
        if (type.includes('database') || type.includes('sql')) {
            return <Database size={16} className="text-green-400" />;
        }
        if (type.includes('network') || type.includes('vnet')) {
            return <Network size={16} className="text-purple-400" />;
        }
        return <FileText size={16} className="text-gray-400" />;
    };

    const toggleFindingExpansion = (findingId) => {
        setExpandedFindings(prev => ({
            ...prev,
            [findingId]: !prev[findingId]
        }));
    };

    const toggleCategoryExpansion = (category) => {
        setExpandedCategories(prev => ({
            ...prev,
            [category]: !prev[category]
        }));
    };
    if (!isOpen || !assessment) return null;

    const getSeverityIcon = (severity) => {
        const sev = severity?.toLowerCase();
        switch (sev) {
            case 'critical': return <AlertCircle size={16} className="text-red-500" />;
            case 'high': return <XCircle size={16} className="text-red-400" />;
            case 'medium': return <AlertTriangle size={16} className="text-yellow-400" />;
            case 'low': return <CheckCircle size={16} className="text-blue-400" />;
            default: return <FileText size={16} className="text-gray-400" />;
        }
    };

    const getSeverityColor = (severity) => {
        const sev = severity?.toLowerCase();
        switch (sev) {
            case 'critical': return 'bg-red-600 text-white border-red-500';
            case 'high': return 'bg-red-500 text-white border-red-400';
            case 'medium': return 'bg-yellow-500 text-black border-yellow-400';
            case 'low': return 'bg-blue-500 text-white border-blue-400';
            default: return 'bg-gray-500 text-white border-gray-400';
        }
    };

    const getSeverityBgColor = (severity) => {
        const sev = severity?.toLowerCase();
        switch (sev) {
            case 'critical': return 'bg-red-900/30 border-red-700';
            case 'high': return 'bg-red-900/20 border-red-600';
            case 'medium': return 'bg-yellow-900/20 border-yellow-600';
            case 'low': return 'bg-blue-900/20 border-blue-600';
            default: return 'bg-gray-800 border-gray-600';
        }
    };

    const getScoreColor = (score) => {
        if (score >= 80) return 'text-green-400';
        if (score >= 60) return 'text-yellow-400';
        if (score >= 40) return 'text-orange-400';
        return 'text-red-400';
    };

    const getScoreIcon = (score) => {
        if (score >= 80) return <CheckCircle size={20} className="text-green-400" />;
        if (score >= 60) return <AlertTriangle size={20} className="text-yellow-400" />;
        return <XCircle size={20} className="text-red-400" />;
    };

    // Group findings by category with deduplication
    const findingsByCategory = findings.reduce((acc, finding) => {
        const category = finding.category || finding.Category || 'Other';
        if (!acc[category]) acc[category] = [];
        acc[category].push(finding);
        return acc;
    }, {});

    // Group similar findings within each category
    const groupSimilarFindings = (categoryFindings) => {
        const grouped = {};

        categoryFindings.forEach(finding => {
            const issue = finding.issue || finding.Issue || 'Governance Issue';
            const key = issue.toLowerCase().replace(/[^a-z0-9]/g, '');

            if (!grouped[key]) {
                grouped[key] = {
                    issue: issue,
                    resources: [],
                    severity: finding.severity || finding.Severity || 'Medium',
                    recommendation: finding.recommendation || finding.Recommendation || 'Review and update resource to meet governance standards.',
                    estimatedEffort: finding.estimatedEffort || finding.EstimatedEffort || 'Medium',
                    isClientSpecific: finding.isClientSpecific || false
                };
            }

            grouped[key].resources.push({
                name: finding.resourceName || finding.ResourceName || 'Azure Resource',
                type: finding.resourceType || finding.ResourceType || 'Unknown',
                id: finding.resourceId || finding.ResourceId || '',
                findingId: finding.id || finding.Id || finding.findingId || finding.FindingId
            });
        });

        return Object.values(grouped);
    };

    // Helper function to extract duration from assessment object
    const getAssessmentDuration = () => {
        const startDate = assessment.startedDate || assessment.StartedDate || assessment.date || assessment.Date;
        const endDate = assessment.completedDate || assessment.CompletedDate || assessment.endDate || assessment.EndDate;

        if (startDate && endDate) {
            const start = new Date(startDate);
            const end = new Date(endDate);
            const diffMs = end - start;

            if (diffMs < 1000) {
                return `${diffMs}ms`;
            } else if (diffMs < 60000) {
                const totalSeconds = Math.round(diffMs / 100) / 10;
                return `${totalSeconds}s`;
            } else {
                const diffMins = Math.floor(diffMs / 60000);
                const diffSecs = Math.floor((diffMs % 60000) / 1000);
                return `${diffMins}m ${diffSecs}s`;
            }
        }

        if (startDate && !endDate) {
            return 'In Progress';
        }

        return 'Unknown';
    };
    const renderGroupedFinding = (groupedFinding, categoryKey, groupKey) => {
        const findingKey = `${categoryKey}-${groupKey}`;
        const isExpanded = expandedFindings[findingKey];
        const resourceCount = groupedFinding.resources.length;
        const primaryResource = groupedFinding.resources[0];

        return (
            <div
                key={findingKey}
                className={`border rounded-lg transition-all duration-200 ${getSeverityBgColor(groupedFinding.severity)}`}
            >
                {/* Collapsed Header with Resource Name */}
                <div
                    className="p-4 cursor-pointer hover:bg-gray-700/50 transition-colors"
                    onClick={() => toggleFindingExpansion(findingKey)}
                >
                    <div className="flex items-start justify-between">
                        <div className="flex items-start space-x-3 flex-1">
                            <div className="flex items-center space-x-2">
                                {getSeverityIcon(groupedFinding.severity)}
                                {isExpanded ? <ChevronUp size={16} className="text-gray-400" /> : <ChevronDown size={16} className="text-gray-400" />}
                            </div>
                            <div className="flex-1 min-w-0">
                                <div className="flex items-center space-x-2 mb-1">
                                    <h4 className="font-medium text-white text-sm truncate">
                                        {groupedFinding.issue}
                                    </h4>
                                    {groupedFinding.isClientSpecific && (
                                        <span className="inline-flex items-center px-2 py-1 rounded-full text-xs font-medium bg-blue-600 text-white">
                                            <Settings size={12} className="mr-1" />
                                            Client Rule
                                        </span>
                                    )}
                                </div>
                                <div className="flex items-center space-x-2 text-xs text-gray-400">
                                    <span className="font-medium text-gray-300">{primaryResource.name}</span>
                                    {resourceCount > 1 && (
                                        <span>+ {resourceCount - 1} more resource{resourceCount > 2 ? 's' : ''}</span>
                                    )}
                                </div>
                            </div>
                        </div>
                        <div className="flex items-center space-x-2">
                            <span className={`px-2 py-1 rounded-full text-xs font-medium border ${getSeverityColor(groupedFinding.severity)}`}>
                                {groupedFinding.severity}
                            </span>
                        </div>
                    </div>
                </div>

                {/* Expanded Content */}
                {isExpanded && (
                    <div className="border-t border-gray-600 p-4 space-y-4">
                        {/* Affected Resources - Show First */}
                        <div>
                            <h5 className="font-medium text-white text-sm mb-2">Affected Resources ({resourceCount})</h5>
                            <div className="space-y-2 max-h-32 overflow-y-auto">
                                {groupedFinding.resources.map((resource, index) => (
                                    <div key={resource.findingId || index} className="flex items-center space-x-3 p-2 bg-gray-700/50 rounded">
                                        {getResourceIcon(resource.type)}
                                        <div className="flex-1 min-w-0">
                                            <p className="text-sm font-medium text-white truncate">{resource.name}</p>
                                            <p className="text-xs text-gray-400">{resource.type}</p>
                                        </div>
                                    </div>
                                ))}
                            </div>
                        </div>

                        {/* Recommendation */}
                        <div className="bg-gray-800/50 rounded p-3">
                            <div className="flex items-start space-x-2">
                                <Target size={16} className="text-blue-400 mt-0.5 flex-shrink-0" />
                                <div>
                                    <h5 className="font-medium text-white text-sm mb-1">Recommendation</h5>
                                    <p className="text-gray-300 text-sm">{groupedFinding.recommendation}</p>
                                </div>
                            </div>
                        </div>

                        {/* Effort Only */}
                        <div className="flex items-center space-x-2 text-sm">
                            <Zap size={14} className="text-yellow-400" />
                            <span className="text-gray-400">Estimated Effort:</span>
                            <span className="text-white">{groupedFinding.estimatedEffort}</span>
                        </div>
                    </div>
                )}
            </div>
        );
    };

    const renderCategorySection = (category, categoryFindings) => {
        const groupedFindings = groupSimilarFindings(categoryFindings);
        const isExpanded = expandedCategories[category] !== false; // Default to expanded
        const totalResources = categoryFindings.length;
        const severityCounts = categoryFindings.reduce((acc, finding) => {
            const severity = (finding.severity || finding.Severity || 'Medium').toLowerCase();
            acc[severity] = (acc[severity] || 0) + 1;
            return acc;
        }, {});

        return (
            <div key={category} className="bg-gray-800 rounded-lg border border-gray-700 overflow-hidden">
                <div
                    className="p-4 cursor-pointer hover:bg-gray-700/50 transition-colors border-b border-gray-700"
                    onClick={() => toggleCategoryExpansion(category)}
                >
                    <div className="flex items-center justify-between">
                        <div className="flex items-center space-x-3">
                            {isExpanded ? <ChevronUp size={18} className="text-gray-400" /> : <ChevronDown size={18} className="text-gray-400" />}
                            <div>
                                <h3 className="text-lg font-semibold text-white capitalize flex items-center space-x-2">
                                    <span>{category}</span>
                                    <span className="text-sm text-gray-400">({totalResources} issues)</span>
                                </h3>
                                <div className="flex items-center space-x-4 text-sm text-gray-400 mt-1">
                                    {severityCounts.critical && <span className="text-red-400">{severityCounts.critical} Critical</span>}
                                    {severityCounts.high && <span className="text-red-400">{severityCounts.high} High</span>}
                                    {severityCounts.medium && <span className="text-yellow-400">{severityCounts.medium} Medium</span>}
                                    {severityCounts.low && <span className="text-blue-400">{severityCounts.low} Low</span>}
                                </div>
                            </div>
                        </div>
                        <div className="flex items-center space-x-2">
                            <span className="text-sm text-gray-400">{groupedFindings.length} groups</span>
                        </div>
                    </div>
                </div>

                {isExpanded && (
                    <div className="p-4 space-y-3">
                        {groupedFindings.map((groupedFinding, index) =>
                            renderGroupedFinding(groupedFinding, category, index)
                        )}
                    </div>
                )}
            </div>
        );
    };
    // Main return statement
    return createPortal(
        <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-[50] p-4">
            <div className="bg-gray-900 border border-gray-800 rounded-lg w-[95vw] h-[95vh] overflow-hidden flex flex-col">
                {/* Enhanced Header with Client Preferences Badge */}
                <div className="flex items-center justify-between p-6 border-b border-gray-800 flex-shrink-0">
                    <div className="flex-1">
                        <div className="flex items-center space-x-4 mb-2">
                            <h2 className="text-xl font-semibold text-white">{assessment.name}</h2>
                            {assessment.useClientPreferences && (
                                <span className="inline-flex items-center px-3 py-1 rounded-full text-sm font-medium bg-blue-600 text-white">
                                    <Settings size={14} className="mr-1" />
                                    Client Preferences Applied
                                </span>
                            )}
                        </div>
                        <div className="flex items-center space-x-4 text-sm text-gray-400">
                            <div className="flex items-center space-x-1">
                                <User size={14} />
                                <span>{assessment.clientName || 'Unknown Client'}</span>
                            </div>
                            <div className="flex items-center space-x-1">
                                <Server size={14} />
                                <span>{assessment.environment || 'Production'} Environment</span>
                            </div>
                            <div className="flex items-center space-x-1">
                                {getScoreIcon(assessment.score)}
                                <span className={`font-medium ${getScoreColor(assessment.score)}`}>
                                    Score: {assessment.score ? `${assessment.score}%` : 'N/A'}
                                </span>
                            </div>
                        </div>
                    </div>
                    <button onClick={onClose} className="p-2 rounded-lg hover:bg-gray-800 text-gray-400 hover:text-white transition-colors">
                        <X size={20} />
                    </button>
                </div>

                {/* Enhanced Tabs */}
                <div className="border-b border-gray-800 flex-shrink-0">
                    <nav className="flex space-x-8 px-6">
                        {[
                            { id: 'overview', label: 'Overview', icon: BarChart3 },
                            { id: 'findings', label: 'Findings', icon: AlertTriangle },
                            { id: 'recommendations', label: 'Recommendations', icon: Target },
                            { id: 'resources', label: 'Resources', icon: Server }
                        ].map((tab) => (
                            <button
                                key={tab.id}
                                onClick={() => handleTabChange(tab.id)}
                                className={`py-4 px-1 border-b-2 font-medium text-sm transition-colors relative flex items-center space-x-2 ${activeTab === tab.id ? 'border-yellow-600 text-yellow-600' : 'border-transparent text-gray-500 hover:text-gray-300'
                                    }`}
                            >
                                <tab.icon size={16} />
                                <span>{tab.label}</span>
                                {tabLoading[tab.id] && (
                                    <div className="absolute -top-1 -right-1">
                                        <div className="w-2 h-2 bg-yellow-600 rounded-full animate-pulse"></div>
                                    </div>
                                )}
                            </button>
                        ))}
                    </nav>
                </div>

                {/* Enhanced Content Area */}
                <div className="flex-1 overflow-y-auto">
                    <div className="p-6">
                        {/* Enhanced Overview Tab */}
                        {activeTab === 'overview' && (
                            <div className="space-y-6">
                                {/* Top Stats Cards */}
                                <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 xl:grid-cols-6 gap-4">
                                    {/* Overall Score */}
                                    <div className="bg-gradient-to-br from-gray-800 to-gray-700 rounded-lg p-4 border border-gray-600">
                                        <div className="flex items-center justify-between mb-3">
                                            <div className="p-2 bg-gray-600 rounded-lg">{getScoreIcon(assessment.score)}</div>
                                            <div className="text-right">
                                                <p className="text-2xl font-bold text-white">{assessment.score ? `${assessment.score}%` : 'N/A'}</p>
                                                <p className="text-s text-gray-400">Overall Score</p>
                                            </div>
                                        </div>
                                        <div className="w-full bg-gray-600 rounded-full h-1.5">
                                            <div
                                                className={`h-1.5 rounded-full transition-all duration-300 ${assessment.score >= 80 ? 'bg-green-500' : assessment.score >= 60 ? 'bg-yellow-500' : assessment.score >= 40 ? 'bg-orange-500' : 'bg-red-500'
                                                    }`}
                                                style={{ width: `${assessment.score || 0}%` }}
                                            ></div>
                                        </div>
                                    </div>

                                    {/* Resources Analyzed */}
                                    <div className="bg-gradient-to-br from-gray-800 to-gray-700 rounded-lg p-4 border border-gray-600">
                                        <div className="flex items-center justify-between">
                                            <div className="p-2 bg-gray-600 rounded-lg"><Server size={18} className="text-gray-300" /></div>
                                            <div className="text-right">
                                                <p className="text-2xl font-bold text-white">{assessment.resourceCount || 'N/A'}</p>
                                                <p className="text-s text-gray-400">Resources Analyzed</p>
                                            </div>
                                        </div>
                                    </div>

                                    {/* Issues Found */}
                                    <div className="bg-gradient-to-br from-gray-800 to-gray-700 rounded-lg p-4 border border-gray-600">
                                        <div className="flex items-center justify-between">
                                            <div className="p-2 bg-gray-600 rounded-lg"><AlertTriangle size={18} className="text-gray-300" /></div>
                                            <div className="text-right">
                                                <p className="text-2xl font-bold text-white">{assessment.issuesCount || findings.length}</p>
                                                <p className="text-s text-gray-400">Total Issues Found</p>
                                            </div>
                                        </div>
                                    </div>

                                    {/* Compliance Rate */}
                                    <div className="bg-gradient-to-br from-gray-800 to-gray-700 rounded-lg p-4 border border-gray-600">
                                        <div className="flex items-center justify-between">
                                            <div className="p-2 bg-gray-600 rounded-lg"><CheckCircle size={18} className="text-gray-300" /></div>
                                            <div className="text-right">
                                                <p className="text-2xl font-bold text-white">
                                                    {(assessment.resourceCount && (assessment.issuesCount || findings.length)) ?
                                                        `${Math.max(0, Math.round(((assessment.resourceCount - (assessment.issuesCount || findings.length)) / assessment.resourceCount) * 100))}%`
                                                        : assessment.resourceCount && !(assessment.issuesCount || findings.length) ? '100%'
                                                            : 'N/A'
                                                    }
                                                </p>
                                                <p className="text-s text-gray-400">Compliance Rate</p>
                                            </div>
                                        </div>
                                    </div>

                                    {/* Critical Issues */}
                                    <div className="bg-gradient-to-br from-gray-800 to-gray-700 rounded-lg p-4 border border-gray-600">
                                        <div className="flex items-center justify-between">
                                            <div className="p-2 bg-gray-600 rounded-lg"><XCircle size={18} className="text-gray-300" /></div>
                                            <div className="text-right">
                                                <p className="text-2xl font-bold text-white">
                                                    {findings.filter(f => (f.severity || f.Severity || '').toLowerCase() === 'critical').length}
                                                </p>
                                                <p className="text-s text-gray-400">Priority: Critical</p>
                                            </div>
                                        </div>
                                    </div>

                                    <div className="bg-gradient-to-br from-gray-800 to-gray-700 rounded-lg p-4 border border-gray-600">
                                        <div className="flex items-center justify-between">
                                            <div className="p-2 bg-gray-600 rounded-lg"><XCircle size={18} className="text-gray-300" /></div>
                                            <div className="text-right">
                                                <p className="text-2xl font-bold text-white">
                                                    {findings.filter(f => (f.severity || f.Severity || '').toLowerCase() === 'high').length}
                                                </p>
                                                <p className="text-s text-gray-400">Priority: High</p>
                                            </div>
                                        </div>
                                    </div>
                                    
                                </div>

                                {/* Main Content Grid - Assessment Details + Compact Findings + Resource Types */}
                                <div className="grid grid-cols-1 lg:grid-cols-3 gap-6 h-full">
                                    {/* Assessment Details - Left Column */}
                                    <div className="bg-gray-800 rounded-lg p-6 border border-gray-700 flex flex-col">
                                        <h3 className="text-lg font-semibold text-white mb-6 flex items-center space-x-2 flex-shrink-0">
                                            <Activity size={20} className="text-yellow-600" />
                                            <span>Assessment Details</span>
                                        </h3>
                                        <div className="space-y-4 flex-1 overflow-y-auto">
                                            <div className="flex items-center space-x-3">
                                                <User size={16} className="text-blue-400" />
                                                <div>
                                                    <p className="text-sm text-gray-400">Client</p>
                                                    <p className="text-white font-medium">{assessment.clientName || 'Unknown Client'}</p>
                                                </div>
                                            </div>

                                            {/* Assessment Type */}
                                            <div className="flex items-center space-x-3">
                                                <Shield size={16} className="text-purple-400" />
                                                <div>
                                                    <p className="text-sm text-gray-400">Assessment Type</p>
                                                    <div className="flex items-center space-x-2">
                                                        <span className="text-white font-medium">{assessment.type || 'Full Assessment'}</span>
                                                        <span className="inline-flex items-center px-2 py-1 rounded-full text-xs font-medium bg-purple-600/20 text-purple-300 border border-purple-600/30">
                                                            {assessment.type === 'NamingConvention' ? 'Naming Only' :
                                                                assessment.type === 'Tagging' ? 'Tagging Only' :
                                                                    assessment.type === 'Full' ? 'Complete Analysis' :
                                                                        assessment.type || 'Full Analysis'}
                                                        </span>
                                                    </div>
                                                </div>
                                            </div>

                                            <div className="flex items-center space-x-3">
                                                <Calendar size={16} className="text-green-400" />
                                                <div>
                                                    <p className="text-sm text-gray-400">Started</p>
                                                    <p className="text-white">{assessment.date || assessment.startedDate || 'Unknown'}</p>
                                                </div>
                                            </div>
                                            <div className="flex items-center space-x-3">
                                                <Clock size={16} className="text-purple-400" />
                                                <div>
                                                    <p className="text-sm text-gray-400">Duration</p>
                                                    <p className="text-white">{getAssessmentDuration()}</p>
                                                </div>
                                            </div>
                                            <div className="flex items-center space-x-3">
                                                <CheckCircle size={16} className="text-green-400" />
                                                <div>
                                                    <p className="text-sm text-gray-400">Status</p>
                                                    <span className={`px-3 py-1 rounded-full text-sm font-medium ${assessment.status === 'Completed' ? 'bg-green-700 text-white' : assessment.status === 'In Progress' ? 'bg-yellow-700 text-white' : 'bg-gray-700 text-white'
                                                        }`}>
                                                        {assessment.status || 'Unknown'}
                                                    </span>
                                                </div>
                                            </div>
                                            <div className="flex items-center space-x-3">
                                                <Server size={16} className="text-orange-400" />
                                                <div>
                                                    <p className="text-sm text-gray-400">Environment</p>
                                                    <p className="text-white">{assessment.environment || 'Unknown'}</p>
                                                </div>
                                            </div>
                                            {(assessment.useClientPreferences ||
                                                assessment.UseClientPreferences ||
                                                assessment.clientId ||
                                                assessment.ClientId ||
                                                findings.some(f => f.isClientSpecific || f.IsClientSpecific)) && (
                                                    <div className="flex items-center space-x-3">
                                                        <Settings size={16} className="text-blue-400" />
                                                        <div>
                                                            <p className="text-sm text-gray-400">Client Preferences</p>
                                                            <div className="flex items-center space-x-2">
                                                                <p className="text-blue-400 font-medium">Applied</p>
                                                                <span className="inline-flex items-center px-2 py-1 rounded-full text-xs font-medium bg-blue-600/20 text-blue-300 border border-blue-600/30">
                                                                    {findings.filter(f => f.isClientSpecific || f.IsClientSpecific).length > 0
                                                                        ? `${findings.filter(f => f.isClientSpecific || f.IsClientSpecific).length} Custom Rules`
                                                                        : 'Enhanced Standards'
                                                                    }
                                                                </span>
                                                            </div>
                                                        </div>
                                                    </div>
                                                )}
                                        </div>
                                    </div>

                                    {/* Findings Summary - Middle Column */}
                                    <div className="bg-gray-800 rounded-lg p-6 border border-gray-700 flex flex-col">
                                        <h3 className="text-lg font-semibold text-white mb-4 flex items-center space-x-2 flex-shrink-0">
                                            <TrendingUp size={20} className="text-yellow-600" />
                                            <span>Findings Summary</span>
                                        </h3>
                                        <div className="space-y-4 flex-1 overflow-y-auto">
                                            {/* Donut Chart for Severity Distribution */}
                                            <div className="bg-gray-700/50 rounded-lg p-4">
                                                <h4 className="text-sm font-medium text-gray-300 mb-3">Severity Distribution</h4>
                                                <div className="flex items-center justify-center mb-4">
                                                    <div className="relative w-32 h-32">
                                                        <svg viewBox="0 0 42 42" className="w-32 h-32 transform -rotate-90">
                                                            {(() => {
                                                                const severityData = ['Critical', 'High', 'Medium', 'Low'].map(severity => {
                                                                    const count = findings.filter(f => {
                                                                        const findingSeverity = (f.severity || f.Severity || '').toLowerCase();
                                                                        return findingSeverity === severity.toLowerCase();
                                                                    }).length;
                                                                    return { severity, count };
                                                                });

                                                                const total = severityData.reduce((sum, item) => sum + item.count, 0);
                                                                const colors = ['#ef4444', '#f97316', '#eab308', '#3b82f6']; // red, orange, yellow, blue

                                                                if (total === 0) {
                                                                    return (
                                                                        <circle
                                                                            cx="21"
                                                                            cy="21"
                                                                            r="15.915"
                                                                            fill="transparent"
                                                                            stroke="#374151"
                                                                            strokeWidth="3"
                                                                        />
                                                                    );
                                                                }

                                                                let cumulativePercentage = 0;
                                                                return severityData.map((item, index) => {
                                                                    if (item.count === 0) return null;

                                                                    const percentage = (item.count / total) * 100;
                                                                    const strokeDasharray = `${percentage} ${100 - percentage}`;
                                                                    const strokeDashoffset = -cumulativePercentage;

                                                                    cumulativePercentage += percentage;

                                                                    return (
                                                                        <circle
                                                                            key={item.severity}
                                                                            cx="21"
                                                                            cy="21"
                                                                            r="15.915"
                                                                            fill="transparent"
                                                                            stroke={colors[index]}
                                                                            strokeWidth="3"
                                                                            strokeDasharray={strokeDasharray}
                                                                            strokeDashoffset={strokeDashoffset}
                                                                            className="transition-all duration-300"
                                                                        />
                                                                    );
                                                                });
                                                            })()}
                                                        </svg>
                                                        <div className="absolute inset-0 flex items-center justify-center">
                                                            <div className="text-center">
                                                                <div className="text-lg font-bold text-white">{findings.length}</div>
                                                                <div className="text-xs text-gray-400">Total</div>
                                                            </div>
                                                        </div>
                                                    </div>
                                                </div>

                                                {/* Legend */}
                                                <div className="grid grid-cols-2 gap-2 text-xs">
                                                    {['Critical', 'High', 'Medium', 'Low'].map((severity, index) => {
                                                        const count = findings.filter(f => {
                                                            const findingSeverity = (f.severity || f.Severity || '').toLowerCase();
                                                            return findingSeverity === severity.toLowerCase();
                                                        }).length;
                                                        const colors = ['#ef4444', '#f97316', '#eab308', '#3b82f6'];

                                                        return (
                                                            <div key={severity} className="flex items-center space-x-2">
                                                                <div className="w-3 h-3 rounded-full" style={{ backgroundColor: colors[index] }}></div>
                                                                <span className="text-gray-300">{severity}: {count}</span>
                                                            </div>
                                                        );
                                                    })}
                                                </div>
                                            </div>

                                            {/* Detailed Severity Bars */}
                                            <div className="space-y-3">
                                                {['Critical', 'High', 'Medium', 'Low'].map(severity => {
                                                    const count = findings.filter(f => {
                                                        const findingSeverity = (f.severity || f.Severity || '').toLowerCase();
                                                        return findingSeverity === severity.toLowerCase();
                                                    }).length;
                                                    const percentage = findings.length > 0 ? Math.round((count / findings.length) * 100) : 0;
                                                    const maxCount = Math.max(...['Critical', 'High', 'Medium', 'Low'].map(s =>
                                                        findings.filter(f => (f.severity || f.Severity || '').toLowerCase() === s.toLowerCase()).length
                                                    ));
                                                    const barWidth = maxCount > 0 ? (count / maxCount) * 100 : 0;

                                                    return (
                                                        <div key={severity} className="bg-gray-700 rounded-lg p-3">
                                                            <div className="flex items-center justify-between mb-2">
                                                                <div className="flex items-center space-x-2">
                                                                    {getSeverityIcon(severity)}
                                                                    <span className="text-white font-medium text-sm">{severity}</span>
                                                                </div>
                                                                <div className="flex items-center space-x-2">
                                                                    <span className="text-lg font-bold text-white">{count}</span>
                                                                    <span className="text-xs text-gray-400">({percentage}%)</span>
                                                                </div>
                                                            </div>
                                                            <div className="w-full bg-gray-600 rounded-full h-2">
                                                                <div
                                                                    className={`h-2 rounded-full transition-all duration-500 ${severity === 'Critical' ? 'bg-red-500' :
                                                                            severity === 'High' ? 'bg-orange-500' :
                                                                                severity === 'Medium' ? 'bg-yellow-500' : 'bg-blue-500'
                                                                        }`}
                                                                    style={{ width: `${barWidth}%` }}
                                                                ></div>
                                                            </div>
                                                        </div>
                                                    );
                                                })}
                                            </div>
                                        </div>
                                    </div>

                                    {/* Resource Types - Right Column */}
                                    <div className="bg-gray-800 rounded-lg p-6 border border-gray-700 flex flex-col">
                                        <h3 className="text-lg font-semibold text-white mb-4 flex items-center space-x-2 flex-shrink-0">
                                            <BarChart3 size={20} className="text-yellow-600" />
                                            <span>Resource Types</span>
                                            <span className="text-sm text-gray-400">
                                                ({resourceFilters.ResourceTypes ? Object.keys(resourceFilters.ResourceTypes).length : 0})
                                            </span>
                                        </h3>

                                        <div className="flex-1 overflow-y-auto custom-scrollbar space-y-4">
                                            {/* Top Resource Types Chart */}
                                            {resourceFilters.ResourceTypes && Object.keys(resourceFilters.ResourceTypes).length > 0 && (
                                                <div className="bg-gray-700/50 rounded-lg p-4">
                                                    <h4 className="text-sm font-medium text-gray-300 mb-3">Top Resource Types</h4>
                                                    <div className="space-y-2">
                                                        {Object.entries(resourceFilters.ResourceTypes)
                                                            .sort(([, a], [, b]) => b - a)
                                                            .slice(0, 5)
                                                            .map(([type, count], index) => {
                                                                const maxCount = Math.max(...Object.values(resourceFilters.ResourceTypes));
                                                                const barWidth = (count / maxCount) * 100;
                                                                const colors = ['#eab308', '#f97316', '#3b82f6', '#10b981', '#8b5cf6'];

                                                                return (
                                                                    <div key={type} className="space-y-1">
                                                                        <div className="flex items-center justify-between text-xs">
                                                                            <span className="text-gray-300 truncate">{getResourceTypeInfo(type).name}</span>
                                                                            <span className="text-white font-medium">{count}</span>
                                                                        </div>
                                                                        <div className="w-full bg-gray-600 rounded-full h-2">
                                                                            <div
                                                                                className="h-2 rounded-full transition-all duration-500"
                                                                                style={{
                                                                                    width: `${barWidth}%`,
                                                                                    backgroundColor: colors[index % colors.length]
                                                                                }}
                                                                            ></div>
                                                                        </div>
                                                                    </div>
                                                                );
                                                            })
                                                        }
                                                    </div>
                                                </div>
                                            )}

                                            {/* Detailed Resource Type List */}
                                            <div className="space-y-3">
                                                {resourceFilters.ResourceTypes && Object.entries(resourceFilters.ResourceTypes)
                                                    .sort(([, a], [, b]) => b - a)
                                                    .map(([type, count]) => {
                                                        const typeInfo = getResourceTypeInfo(type);
                                                        return (
                                                            <div key={type} className="flex items-center justify-between p-3 bg-gray-700 rounded-lg hover:bg-gray-600/50 transition-colors">
                                                                <div className="flex items-center space-x-3 flex-1 min-w-0">
                                                                    {typeInfo.icon}
                                                                    <div className="min-w-0 flex-1">
                                                                        <div className="flex items-center space-x-2 mb-1">
                                                                            <p className="text-sm font-medium text-white truncate">{typeInfo.name}</p>
                                                                            <span className="inline-flex items-center px-2 py-0.5 rounded-full text-xs font-medium bg-gray-600 text-gray-300 shrink-0">
                                                                                {typeInfo.category}
                                                                            </span>
                                                                        </div>
                                                                        <p className="text-xs text-gray-400 truncate">{typeInfo.description}</p>
                                                                    </div>
                                                                </div>
                                                                <div className="text-right ml-3 shrink-0">
                                                                    <span className="text-lg font-bold text-white">{count}</span>
                                                                    <p className="text-xs text-gray-400">resource{count !== 1 ? 's' : ''}</p>
                                                                </div>
                                                            </div>
                                                        );
                                                    })
                                                }

                                                {/* Empty state */}
                                                {(!resourceFilters.ResourceTypes || Object.keys(resourceFilters.ResourceTypes).length === 0) && (
                                                    <div className="text-center py-8 text-gray-400">
                                                        <FileText size={48} className="mx-auto mb-2 opacity-50" />
                                                        <p>No resource type data available</p>
                                                    </div>
                                                )}
                                            </div>
                                        </div>
                                    </div>

                                    {/* Resource Types - Right Column */}
                                    
                                </div>
                            </div>
                        )}
                        
                        {/* Enhanced Findings Tab */}
                        {activeTab === 'findings' && (
                            <div className="space-y-6">
                                {loading && (
                                    <div className="text-center py-12">
                                        <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-yellow-600 mx-auto mb-4"></div>
                                        <p className="text-gray-400">Loading findings...</p>
                                    </div>
                                )}

                                {error && (
                                    <div className="bg-red-900/20 border border-red-700 rounded-lg p-4">
                                        <div className="flex items-center space-x-2 mb-2">
                                            <XCircle size={20} className="text-red-400" />
                                            <p className="text-red-200 font-medium">Error Loading Findings</p>
                                        </div>
                                        <p className="text-red-200 mb-3">{error}</p>
                                        <button onClick={loadFindings} className="px-4 py-2 bg-red-700 text-white rounded-lg text-sm hover:bg-red-600 transition-colors">
                                            Retry
                                        </button>
                                    </div>
                                )}

                                {!loading && !error && findings.length === 0 && (
                                    <div className="text-center py-12">
                                        <CheckCircle size={48} className="text-green-400 mx-auto mb-4" />
                                        <h3 className="text-lg font-semibold text-white mb-2">No Issues Found</h3>
                                        <p className="text-gray-400">This assessment found no governance issues.</p>
                                    </div>
                                )}

                                {!loading && !error && findings.length > 0 && (
                                    <div className="space-y-4">
                                        <div className="flex items-center justify-between">
                                            <h3 className="text-lg font-semibold text-white">Governance Findings ({findings.length} total)</h3>
                                            <div className="flex items-center space-x-2">
                                                <button
                                                    onClick={() => {
                                                        const allExpanded = Object.keys(findingsByCategory).every(cat => expandedCategories[cat] !== false);
                                                        const newState = {};
                                                        Object.keys(findingsByCategory).forEach(cat => { newState[cat] = !allExpanded; });
                                                        setExpandedCategories(newState);
                                                    }}
                                                    className="px-3 py-1 text-sm bg-gray-700 text-white rounded-lg hover:bg-gray-600 transition-colors"
                                                >
                                                    {Object.keys(findingsByCategory).every(cat => expandedCategories[cat] !== false) ? 'Collapse All' : 'Expand All'}
                                                </button>
                                            </div>
                                        </div>
                                        {Object.entries(findingsByCategory).map(([category, categoryFindings]) => renderCategorySection(category, categoryFindings))}
                                    </div>
                                )}
                            </div>
                        )}

                        {/* Enhanced Recommendations Tab */}
                        {activeTab === 'recommendations' && (
                            <div className="space-y-6">
                                <div className="flex items-center justify-between">
                                    <h3 className="text-lg font-semibold text-white flex items-center space-x-2">
                                        <Target size={20} className="text-yellow-600" />
                                        <span>Actionable Recommendations</span>
                                    </h3>
                                    <div className="flex space-x-2">
                                        <button className="px-4 py-2 bg-gray-700 text-white rounded-lg text-sm hover:bg-gray-600 transition-colors flex items-center space-x-2">
                                            <Download size={16} />
                                            <span>Export PDF</span>
                                        </button>
                                        <button className="px-4 py-2 bg-yellow-600 text-black rounded-lg text-sm hover:bg-yellow-700 transition-colors flex items-center space-x-2">
                                            <Download size={16} />
                                            <span>Export DOCX</span>
                                        </button>
                                    </div>
                                </div>

                                {Object.entries(findingsByCategory).map(([category, categoryFindings]) => {
                                    const severityCounts = categoryFindings.reduce((acc, finding) => {
                                        const severity = (finding.severity || finding.Severity || 'Medium').toLowerCase();
                                        acc[severity] = (acc[severity] || 0) + 1;
                                        return acc;
                                    }, {});

                                    let recommendations = [];
                                    if (category.toLowerCase().includes('naming')) {
                                        recommendations.push({
                                            title: "Implement Consistent Naming Standards",
                                            description: `${categoryFindings.length} resources don't follow naming conventions. Establish and enforce a standardized naming pattern across all Azure resources.`,
                                            actions: [
                                                "Define naming convention policy (e.g., [env]-[app]-[resource]-[instance])",
                                                "Use Azure Policy to enforce naming standards",
                                                "Create resource naming templates",
                                                "Update existing non-compliant resources"
                                            ],
                                            priority: severityCounts.high > 0 ? 'High' : severityCounts.medium > 0 ? 'Medium' : 'Low'
                                        });
                                    } else if (category.toLowerCase().includes('tagging')) {
                                        recommendations.push({
                                            title: "Deploy Comprehensive Tagging Strategy",
                                            description: `${categoryFindings.length} resources are missing required tags. Implement a mandatory tagging policy for cost management and governance.`,
                                            actions: [
                                                "Define required tags: Environment, Owner, CostCenter, Project",
                                                "Create Azure Policy to enforce mandatory tags",
                                                "Set up automated tagging for new resources",
                                                "Bulk tag existing untagged resources"
                                            ],
                                            priority: severityCounts.high > 0 ? 'High' : 'Medium'
                                        });
                                    } else {
                                        recommendations.push({
                                            title: `Address ${category} Issues`,
                                            description: `${categoryFindings.length} ${category.toLowerCase()} issues require attention to improve governance compliance.`,
                                            actions: [
                                                "Review and assess each individual finding",
                                                "Prioritize fixes based on business impact",
                                                "Implement remediation plan",
                                                "Monitor ongoing compliance"
                                            ],
                                            priority: severityCounts.critical > 0 ? 'Critical' : severityCounts.high > 0 ? 'High' : 'Medium'
                                        });
                                    }

                                    return recommendations.map((rec, index) => (
                                        <div key={`${category}-${index}`} className={`rounded-lg p-6 border ${rec.priority === 'Critical' ? 'bg-red-900/20 border-red-700' :
                                                rec.priority === 'High' ? 'bg-orange-900/20 border-orange-700' :
                                                    rec.priority === 'Medium' ? 'bg-yellow-900/20 border-yellow-700' : 'bg-blue-900/20 border-blue-700'
                                            }`}>
                                            <div className="flex items-start space-x-4">
                                                <div className={`p-3 rounded-lg ${rec.priority === 'Critical' ? 'bg-red-700' :
                                                        rec.priority === 'High' ? 'bg-orange-700' :
                                                            rec.priority === 'Medium' ? 'bg-yellow-700' : 'bg-blue-700'
                                                    }`}>
                                                    {rec.priority === 'Critical' ? <AlertCircle size={20} className="text-white" /> :
                                                        rec.priority === 'High' ? <AlertTriangle size={20} className="text-white" /> :
                                                            rec.priority === 'Medium' ? <AlertTriangle size={20} className="text-black" /> :
                                                                <CheckCircle size={20} className="text-white" />}
                                                </div>
                                                <div className="flex-1">
                                                    <div className="flex items-center space-x-3 mb-2">
                                                        <h4 className="text-lg font-semibold text-white">{rec.title}</h4>
                                                        <span className={`px-2 py-1 rounded-full text-xs font-medium ${rec.priority === 'Critical' ? 'bg-red-600 text-white' :
                                                                rec.priority === 'High' ? 'bg-orange-600 text-white' :
                                                                    rec.priority === 'Medium' ? 'bg-yellow-600 text-black' : 'bg-blue-600 text-white'
                                                            }`}>
                                                            {rec.priority} Priority
                                                        </span>
                                                    </div>
                                                    <p className="text-gray-300 mb-4">{rec.description}</p>
                                                    <div>
                                                        <h5 className="font-medium text-white mb-2">Action Items:</h5>
                                                        <ul className="space-y-1">
                                                            {rec.actions.map((action, actionIndex) => (
                                                                <li key={actionIndex} className="flex items-start space-x-2">
                                                                    <span className="text-gray-400 mt-1">•</span>
                                                                    <span className="text-gray-300 text-sm">{action}</span>
                                                                </li>
                                                            ))}
                                                        </ul>
                                                    </div>
                                                </div>
                                            </div>
                                        </div>
                                    ));
                                })}

                                {assessment.useClientPreferences && (
                                    <div className="bg-gradient-to-r from-blue-900/20 to-blue-800/20 rounded-lg p-6 border border-blue-700">
                                        <h3 className="text-lg font-semibold text-white mb-4 flex items-center space-x-2">
                                            <User size={20} className="text-blue-400" />
                                            <span>Client-Specific Guidance</span>
                                        </h3>
                                        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                                            <div className="bg-gray-800/50 rounded-lg p-4">
                                                <h4 className="font-medium text-white mb-2">Applied Standards</h4>
                                                <ul className="text-sm text-gray-300 space-y-1">
                                                    <li>• Priority Tags: Environment, Application, Schedule</li>
                                                    <li>• Naming: Environment prefix requirements</li>
                                                    <li>• Compliance: Enhanced security standards</li>
                                                </ul>
                                            </div>
                                            <div className="bg-gray-800/50 rounded-lg p-4">
                                                <h4 className="font-medium text-white mb-2">Next Steps</h4>
                                                <ul className="text-sm text-gray-300 space-y-1">
                                                    <li>• Schedule client review meeting</li>
                                                    <li>• Prioritize client-specific findings</li>
                                                    <li>• Implement enhanced monitoring</li>
                                                </ul>
                                            </div>
                                        </div>
                                    </div>
                                )}
                            </div>
                        )}
                        {/* Enhanced Resources Tab */}
                        {activeTab === 'resources' && (
                            <div className="space-y-6">
                                <div className="bg-gray-800 rounded-lg p-6 border border-gray-700">
                                    <div className="flex items-center justify-between mb-6">
                                        <h3 className="text-lg font-semibold text-white flex items-center space-x-2">
                                            <Server size={20} className="text-yellow-600" />
                                            <span>Azure Resources</span>
                                        </h3>
                                        <div className="flex space-x-2">
                                            <button onClick={() => handleExport('csv')} className="px-4 py-2 bg-gray-700 text-white rounded-lg text-sm hover:bg-gray-600 transition-colors flex items-center space-x-2">
                                                <Download size={16} />
                                                <span>Export CSV</span>
                                            </button>
                                            <button onClick={() => handleExport('xlsx')} className="px-4 py-2 bg-yellow-600 text-black rounded-lg text-sm hover:bg-yellow-700 transition-colors flex items-center space-x-2">
                                                <Download size={16} />
                                                <span>Export Excel</span>
                                            </button>
                                        </div>
                                    </div>

                                    <div className="grid grid-cols-1 md:grid-cols-5 gap-4 mb-6">
                                        <div className="relative">
                                            <Search size={16} className="absolute left-3 top-1/2 transform -translate-y-1/2 text-gray-400" />
                                            <input
                                                type="text"
                                                placeholder="Search resources..."
                                                value={resourceSearch}
                                                onChange={(e) => setResourceSearch(e.target.value)}
                                                onKeyPress={(e) => e.key === 'Enter' && loadResources(1)}
                                                className="w-full pl-10 pr-4 py-2 bg-gray-900 border border-gray-600 rounded-lg text-white text-sm focus:border-yellow-600 focus:outline-none"
                                            />
                                        </div>

                                        <select value={selectedFilters.resourceType} onChange={(e) => setSelectedFilters(prev => ({ ...prev, resourceType: e.target.value }))} className="px-3 py-2 bg-gray-900 border border-gray-600 rounded-lg text-white text-sm focus:border-yellow-600 focus:outline-none">
                                            <option value="">All Types</option>
                                            {Object.entries(resourceFilters.ResourceTypes || {}).map(([type, count]) => (
                                                <option key={type} value={type}>{type} ({count})</option>
                                            ))}
                                        </select>

                                        <select value={selectedFilters.resourceGroup} onChange={(e) => setSelectedFilters(prev => ({ ...prev, resourceGroup: e.target.value }))} className="px-3 py-2 bg-gray-900 border border-gray-600 rounded-lg text-white text-sm focus:border-yellow-600 focus:outline-none">
                                            <option value="">All Resource Groups</option>
                                            {Object.entries(resourceFilters.ResourceGroups || {}).map(([rg, count]) => (
                                                <option key={rg} value={rg}>{rg} ({count})</option>
                                            ))}
                                        </select>

                                        <select value={selectedFilters.location} onChange={(e) => setSelectedFilters(prev => ({ ...prev, location: e.target.value }))} className="px-3 py-2 bg-gray-900 border border-gray-600 rounded-lg text-white text-sm focus:border-yellow-600 focus:outline-none">
                                            <option value="">All Locations</option>
                                            {Object.entries(resourceFilters.Locations || {}).map(([loc, count]) => (
                                                <option key={loc} value={loc}>{loc} ({count})</option>
                                            ))}
                                        </select>

                                        <button onClick={() => loadResources(1)} className="px-4 py-2 bg-yellow-600 hover:bg-yellow-700 text-black rounded-lg text-sm font-medium transition-colors flex items-center justify-center space-x-2">
                                            <Filter size={16} />
                                            <span>Apply Filters</span>
                                        </button>
                                    </div>

                                    <div className="flex items-center justify-between text-sm text-gray-400 mb-4">
                                        <span className="flex items-center space-x-2">
                                            <span>Showing {resources.length} of {resourceTotalCount} Resources</span>
                                            {(Object.keys(selectedFilters).some(key => selectedFilters[key]) || resourceSearch) && (
                                                <span className="px-2 py-1 bg-yellow-600 text-black text-xs rounded-full">Filtered</span>
                                            )}
                                        </span>
                                        <button
                                            onClick={() => {
                                                setSelectedFilters({ resourceType: '', resourceGroup: '', location: '', environment: '' });
                                                setResourceSearch('');
                                                loadResources(1, true);
                                            }}
                                            className="text-yellow-600 hover:text-yellow-500 transition-colors flex items-center space-x-1"
                                        >
                                            <X size={14} />
                                            <span>Clear Filters</span>
                                        </button>
                                    </div>
                                </div>

                                {tabLoading.resources ? (
                                    <div className="text-center py-12">
                                        <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-yellow-600 mx-auto mb-4"></div>
                                        <p className="text-gray-400">Loading resources...</p>
                                    </div>
                                ) : resources.length > 0 ? (
                                    <div className="bg-gray-800 rounded-lg border border-gray-700 overflow-hidden">
                                        <div className="overflow-x-auto">
                                            <table className="w-full">
                                                <thead className="bg-gray-700">
                                                    <tr>
                                                        <th className="px-6 py-4 text-left text-xs font-medium text-gray-300 uppercase tracking-wider">
                                                            <div className="flex items-center space-x-2">
                                                                <Server size={16} />
                                                                <span>Resource</span>
                                                            </div>
                                                        </th>
                                                        <th className="px-6 py-4 text-left text-xs font-medium text-gray-300 uppercase tracking-wider">
                                                            <div className="flex items-center space-x-2">
                                                                <FileText size={16} />
                                                                <span>Type</span>
                                                            </div>
                                                        </th>
                                                        <th className="px-6 py-4 text-left text-xs font-medium text-gray-300 uppercase tracking-wider">
                                                            <div className="flex items-center space-x-2">
                                                                <Folder size={16} />
                                                                <span>Resource Group</span>
                                                            </div>
                                                        </th>
                                                        <th className="px-6 py-4 text-left text-xs font-medium text-gray-300 uppercase tracking-wider">
                                                            <div className="flex items-center space-x-2">
                                                                <MapPin size={16} />
                                                                <span>Location</span>
                                                            </div>
                                                        </th>
                                                        <th className="px-6 py-4 text-left text-xs font-medium text-gray-300 uppercase tracking-wider">
                                                            <div className="flex items-center space-x-2">
                                                                <Activity size={16} />
                                                                <span>Environment</span>
                                                            </div>
                                                        </th>
                                                        <th className="px-6 py-4 text-left text-xs font-medium text-gray-300 uppercase tracking-wider">
                                                                <div className="flex items-center space-x-2">
                                                                <Tag size={16} />
                                                                <span>Compliance</span>
                                                            </div>
                                                        </th>
                                                    </tr>
                                                </thead>
                                                <tbody className="divide-y divide-gray-700">
                                                    {resources.map((resource, index) => {
                                                        const complianceScore = resource.TagCount > 0 ? Math.min(100, (resource.TagCount / 5) * 100) : 0;
                                                        const hasIssues = findings.some(f => (f.resourceName || f.ResourceName) === resource.Name);

                                                        return (
                                                            <tr key={resource.Id || index} className="hover:bg-gray-700/50 transition-colors">
                                                                <td className="px-6 py-4">
                                                                    <div className="flex items-center space-x-3">
                                                                        <div className="relative">
                                                                            {getResourceIcon(resource.ResourceTypeName)}
                                                                            {hasIssues && (<div className="absolute -top-1 -right-1 w-3 h-3 bg-red-500 rounded-full border border-gray-800"></div>)}
                                                                        </div>
                                                                        <div className="min-w-0 flex-1">
                                                                            <p className="text-sm font-medium text-white truncate">{resource.Name}</p>
                                                                            {resource.Kind && resource.Kind !== "" && resource.Kind !== "null" && (
                                                                                <p className="text-xs text-gray-400 truncate">{resource.Kind}</p>
                                                                            )}
                                                                        </div>
                                                                    </div>
                                                                </td>
                                                                <td className="px-6 py-4">
                                                                    <span className="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium bg-gray-700 text-gray-300">
                                                                        {resource.ResourceTypeName}
                                                                    </span>
                                                                </td>
                                                                <td className="px-6 py-4">
                                                                    <span className="text-sm text-gray-300">{resource.ResourceGroup || 'N/A'}</span>
                                                                </td>
                                                                <td className="px-6 py-4">
                                                                    <span className="text-sm text-gray-300">{resource.Location || 'N/A'}</span>
                                                                </td>
                                                                <td className="px-6 py-4">
                                                                    {resource.Environment ? (
                                                                        <span className={`inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium ${resource.Environment.toLowerCase() === 'production' || resource.Environment.toLowerCase() === 'prod' ? 'bg-red-600 text-white' :
                                                                                resource.Environment.toLowerCase() === 'development' || resource.Environment.toLowerCase() === 'dev' ? 'bg-blue-600 text-white' :
                                                                                    resource.Environment.toLowerCase() === 'staging' || resource.Environment.toLowerCase() === 'test' ? 'bg-yellow-600 text-black' : 'bg-gray-600 text-white'
                                                                            }`}>
                                                                            {resource.Environment}
                                                                        </span>
                                                                    ) : (
                                                                        <span className="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium bg-gray-700 text-gray-400">Unknown</span>
                                                                    )}
                                                                </td>
                                                                <td className="px-6 py-4">
                                                                    <div className="flex items-center space-x-3">
                                                                        <div className="flex items-center space-x-2">
                                                                            <div className="w-16 bg-gray-700 rounded-full h-2">
                                                                                <div
                                                                                    className={`h-2 rounded-full transition-all duration-300 ${complianceScore >= 80 ? 'bg-green-500' : complianceScore >= 60 ? 'bg-yellow-500' : complianceScore >= 40 ? 'bg-orange-500' : 'bg-red-500'
                                                                                        }`}
                                                                                    style={{ width: `${complianceScore}%` }}
                                                                                ></div>
                                                                            </div>
                                                                            <span className="text-xs text-gray-400">{Math.round(complianceScore)}%</span>
                                                                        </div>
                                                                        <div className="text-sm text-gray-300">{resource.TagCount} tags</div>
                                                                        {hasIssues && (
                                                                            <button
                                                                                onClick={(e) => {
                                                                                    e.stopPropagation();
                                                                                    const resourceIssues = findings.filter(f => (f.resourceName || f.ResourceName) === resource.Name);
                                                                                    alert(`Issues for ${resource.Name}:\n\n${resourceIssues.map(issue => `• ${issue.issue || issue.Issue || 'Governance issue'}`).join('\n')}`);
                                                                                }}
                                                                                className="inline-flex items-center px-2 py-1 rounded-full text-xs font-medium bg-red-700 text-white hover:bg-red-600 transition-colors cursor-pointer"
                                                                            >
                                                                                Issues
                                                                            </button>
                                                                        )}
                                                                    </div>
                                                                    {resource.TagCount > 0 && resource.Tags && Object.keys(resource.Tags).length > 0 && (
                                                                        <div className="flex flex-wrap gap-1 mt-2">
                                                                            {Object.entries(resource.Tags).slice(0, 3).map(([key, value]) => (
                                                                                <span key={key} className="inline-flex items-center px-1.5 py-0.5 rounded text-xs font-medium bg-gray-600 text-gray-200">
                                                                                    {key}: {value}
                                                                                </span>
                                                                            ))}
                                                                            {resource.TagCount > 3 && (<span className="text-xs text-gray-400">+{resource.TagCount - 3} more</span>)}
                                                                        </div>
                                                                    )}
                                                                </td>
                                                            </tr>
                                                        );
                                                    })}
                                                </tbody>
                                            </table>
                                        </div>

                                        {resourceTotalPages > 1 && (
                                            <div className="px-6 py-4 bg-gray-700 border-t border-gray-600 flex items-center justify-between">
                                                <div className="text-sm text-gray-400">
                                                    Showing {((resourcePage - 1) * 50) + 1} to {Math.min(resourcePage * 50, resourceTotalCount)} of {resourceTotalCount} Resources
                                                </div>
                                                <div className="flex items-center space-x-2">
                                                    <button onClick={() => loadResources(resourcePage - 1)} disabled={resourcePage <= 1} className="p-2 rounded-lg hover:bg-gray-600 text-gray-400 hover:text-white transition-colors disabled:opacity-50 disabled:cursor-not-allowed">
                                                        <ChevronLeft size={16} />
                                                    </button>
                                                    <div className="flex items-center space-x-1">
                                                        {[...Array(Math.min(5, resourceTotalPages))].map((_, i) => {
                                                            const pageNum = Math.max(1, Math.min(resourcePage - 2, resourceTotalPages - 4)) + i;
                                                            if (pageNum > resourceTotalPages) return null;
                                                            return (
                                                                <button
                                                                    key={pageNum}
                                                                    onClick={() => loadResources(pageNum)}
                                                                    className={`px-3 py-1 rounded-lg text-sm transition-colors ${pageNum === resourcePage ? 'bg-yellow-600 text-black' : 'text-gray-400 hover:text-white hover:bg-gray-600'
                                                                        }`}
                                                                >
                                                                    {pageNum}
                                                                </button>
                                                            );
                                                        })}
                                                    </div>
                                                    <button onClick={() => loadResources(resourcePage + 1)} disabled={resourcePage >= resourceTotalPages} className="p-2 rounded-lg hover:bg-gray-600 text-gray-400 hover:text-white transition-colors disabled:opacity-50 disabled:cursor-not-allowed">
                                                        <ChevronRight size={16} />
                                                    </button>
                                                </div>
                                            </div>
                                        )}
                                    </div>
                                ) : (
                                    <div className="text-center py-12 bg-gray-800 rounded-lg border border-gray-700">
                                        <Server size={48} className="text-gray-600 mx-auto mb-4" />
                                        <h3 className="text-lg font-semibold text-white mb-2">No Resources Found</h3>
                                        <p className="text-gray-400 mb-4">
                                            {Object.keys(selectedFilters).some(key => selectedFilters[key]) || resourceSearch
                                                ? "Try adjusting your search or filters to see more resources."
                                                : "No Azure resources were found for this assessment."
                                            }
                                        </p>
                                        {(Object.keys(selectedFilters).some(key => selectedFilters[key]) || resourceSearch) && (
                                            <button
                                                onClick={() => {
                                                    setSelectedFilters({ resourceType: '', resourceGroup: '', location: '', environment: '' });
                                                    setResourceSearch('');
                                                    loadResources(1, true);
                                                }}
                                                className="px-4 py-2 bg-yellow-600 text-black rounded-lg hover:bg-yellow-700 transition-colors"
                                            >
                                                Clear All Filters
                                            </button>
                                        )}
                                    </div>
                                )}
                            </div>
                        )}
                    </div>
                </div>

                {/* Enhanced Footer */}
                <div className="border-t border-gray-800 p-6 flex justify-between items-center flex-shrink-0 bg-gray-900/50">
                    <div className="flex items-center space-x-4 text-sm text-gray-400">
                        <span>Last updated: {assessment.completedDate ? new Date(assessment.completedDate).toLocaleDateString() : new Date().toLocaleDateString()}</span>
                        {assessment.useClientPreferences && (
                            <span className="flex items-center space-x-1">
                                <Settings size={14} className="text-blue-400" />
                                <span className="text-blue-400">Client preferences applied</span>
                            </span>
                        )}
                        {/* NEW: FernWorks.io link */}
                        <a
                            href="https://fernworks.io"
                            target="_blank"
                            rel="noopener noreferrer"
                            className="flex items-center space-x-1 text-yellow-600 hover:text-yellow-500 transition-colors"
                        >
                            <span>Powered by FernWorks.io</span>
                        </a>
                    </div>
                    <div className="flex space-x-3">
                        <button onClick={onClose} className="px-4 py-2 text-gray-300 hover:text-white transition-colors">Close</button>
                        <button className="px-6 py-2 bg-yellow-600 hover:bg-yellow-700 text-black rounded-lg font-medium transition-colors flex items-center space-x-2">
                            <Download size={16} />
                            <span>Export Report</span>
                        </button>
                    </div>
                </div>
            </div>
        </div>
        , document.body
    );
};

export default AssessmentDetailModal;