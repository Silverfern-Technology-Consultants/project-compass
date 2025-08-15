import React, { useState, useEffect, useRef } from 'react';
import { AlertCircle, RefreshCw, TrendingUp, TrendingDown, Play, Settings } from 'lucide-react';
import { useClient } from '../../contexts/ClientContext';
import { apiClient } from '../../services/apiService';
import { dashboardService } from '../../services/DashboardService';

// Import our components
import PermissionSetupScreen from '../cost-analysis/PermissionSetupScreen';
import CostSummaryCards from '../cost-analysis/CostSummaryCards';
import CostAnalysisTable from '../cost-analysis/CostAnalysisTable';
import DailyCostCalendar from '../cost-analysis/DailyCostCalendar';

const CostAnalysisPage = () => {
    const { clients, selectedClient, setSelectedClient } = useClient();
    const [permissionStatus, setPermissionStatus] = useState('checking');
    const [environmentsNeedingSetup, setEnvironmentsNeedingSetup] = useState([]);
    const [costData, setCostData] = useState(null);
    const [isLoading, setIsLoading] = useState(false);
    const [copiedCommand, setCopiedCommand] = useState('');

    // Query parameters state - aligned with Azure Cost Management API schema
    const [queryParams, setQueryParams] = useState({
        type: 'Usage', // Fixed as per requirements
        timeframe: 'Custom',
        timePeriod: {
            from: new Date(new Date().getFullYear(), new Date().getMonth() - 1, 1).toISOString().split('T')[0] + 'T00:00:00Z',
            to: new Date(new Date().getFullYear(), new Date().getMonth(), 0).toISOString().split('T')[0] + 'T23:59:59Z'
        },
        dataset: {
            granularity: 'Daily', // Default to Daily to show calendar
            aggregation: {
                totalCost: {
                    name: 'PreTaxCost',
                    function: 'Sum'
                }
            },
            grouping: [
                {
                    type: 'Dimension',
                    name: 'ResourceId' // Changed from ResourceType to ResourceId for better breakdown
                }
            ]
        }
    });

    // Separate option for previous period comparison (Governance Guardian feature)
    const [includePreviousPeriod, setIncludePreviousPeriod] = useState(true);
    
    // Track if custom timeframe is selected
    const [useCustomTimeframe, setUseCustomTimeframe] = useState(false);
    
    // Track which preset is currently selected
    const [selectedPreset, setSelectedPreset] = useState('lastMonth');
    
    // Option to show/hide the daily cost calendar
    const [showCostCalendar, setShowCostCalendar] = useState(true);
    
    // DEBUG: Anonymization toggle for screenshots
    const [anonymizeData, setAnonymizeData] = useState(false);
    const anonymizeInputRef = useRef(null);
    const [keySequence, setKeySequence] = useState([]);
    const keyTimeoutRef = useRef(null);

    // Auto-disable calendar when granularity is aggregated
    useEffect(() => {
        if (queryParams.dataset.granularity === 'None') {
            setShowCostCalendar(false);
        }
    }, [queryParams.dataset.granularity]);
    
    // Hidden keyboard shortcut: Press 'A' three times quickly to toggle anonymization
    useEffect(() => {
        const handleKeyPress = (event) => {
            if (event.key.toLowerCase() === 'a') {
                setKeySequence(prev => {
                    const newSequence = [...prev, Date.now()];
                    
                    // Clear timeout if it exists
                    if (keyTimeoutRef.current) {
                        clearTimeout(keyTimeoutRef.current);
                    }
                    
                    // Set new timeout to reset sequence
                    keyTimeoutRef.current = setTimeout(() => {
                        setKeySequence([]);
                    }, 2000); // Reset after 2 seconds
                    
                    // Check if we have 3 'A' presses within 2 seconds
                    if (newSequence.length >= 3) {
                        const recentPresses = newSequence.slice(-3);
                        const timeDiff = recentPresses[2] - recentPresses[0];
                        
                        if (timeDiff <= 2000) { // Within 2 seconds
                            setAnonymizeData(prev => !prev);
                            setKeySequence([]); // Reset sequence
                            
                            // Show brief feedback (optional)
                            console.log('Anonymization toggled:', !anonymizeData);
                        }
                    }
                    
                    return newSequence;
                });
            }
        };
        
        document.addEventListener('keydown', handleKeyPress);
        
        return () => {
            document.removeEventListener('keydown', handleKeyPress);
            if (keyTimeoutRef.current) {
                clearTimeout(keyTimeoutRef.current);
            }
        };
    }, [anonymizeData]);

    // UI state
    const [showQueryBuilder, setShowQueryBuilder] = useState(true);

    // Get client context from DashboardService if available
    useEffect(() => {
        const initializeClientContext = async () => {
            if (!selectedClient && clients?.length > 0) {
                // Try to get client context from dashboard
                try {
                    const companyMetrics = await dashboardService.getCompanyMetrics();
                    if (companyMetrics.recentClients?.length > 0) {
                        // Use the most recent client as default context
                        const defaultClient = clients.find(c => c.ClientId === companyMetrics.recentClients[0].id);
                        if (defaultClient) {
                            setSelectedClient(defaultClient);
                        }
                    }
                } catch (error) {
                    console.log('Could not load client context from dashboard, user will need to select');
                }
            }
        };

        initializeClientContext();
    }, [clients, selectedClient, setSelectedClient]);

    // Utility functions
    const formatCurrency = (amount, currency = 'USD') => {
        // Show more precision for very small amounts (including negative small amounts)
        const precision = Math.abs(amount) < 0.01 && amount !== 0 ? 6 : 2;
        
        return new Intl.NumberFormat('en-US', {
            style: 'currency',
            currency,
            minimumFractionDigits: precision,
            maximumFractionDigits: precision
        }).format(amount);
    };

    const formatPercentage = (percentage) => {
        // Handle edge cases for more meaningful display
        if (percentage === -999) {
            return 'N/A'; // When previous period was $0 (creation, not change)
        }
        if (Math.abs(percentage) < 0.1 && percentage !== 0) {
            return '<0.1%'; // Very small changes
        }
        
        const sign = percentage > 0 ? '+' : '';
        return `${sign}${percentage.toFixed(1)}%`;
    };

    const getChangeIcon = (percentage) => {
        if (percentage > 0) {
            return <TrendingUp className="text-red-400" size={16} />;
        } else if (percentage < 0) {
            return <TrendingDown className="text-green-400" size={16} />;
        }
        return <div className="w-4 h-4 bg-gray-400 rounded-full"></div>;
    };

    const getChangeColor = (percentage) => {
        // Handle special cases
        if (percentage === -999) {
            return 'text-gray-400'; // N/A (creation, not change)
        }
        
        // Standard color logic
        if (percentage > 0) {
            return 'text-red-400'; // Increased cost
        } else if (percentage < 0) {
            return 'text-green-400'; // Decreased cost
        }
        return 'text-gray-400'; // No change
    };

    // Anonymization utilities for screenshots
    const anonymizeResourceName = (name, index) => {
        if (!name || !anonymizeData) return name;
        
        // Convert resource names to generic patterns based on type
        if (name.toLowerCase().includes('sql') || name.toLowerCase().includes('database')) {
            return `Database-${String.fromCharCode(65 + (index % 26))}`; // Database-A, Database-B, etc.
        }
        if (name.toLowerCase().includes('storage') || name.toLowerCase().includes('blob')) {
            return `Storage-${String.fromCharCode(65 + (index % 26))}`;
        }
        if (name.toLowerCase().includes('vault') || name.toLowerCase().includes('key')) {
            return `KeyVault-${String.fromCharCode(65 + (index % 26))}`;
        }
        if (name.toLowerCase().includes('app') || name.toLowerCase().includes('web')) {
            return `WebApp-${String.fromCharCode(65 + (index % 26))}`;
        }
        if (name.toLowerCase().includes('vm') || name.toLowerCase().includes('virtual')) {
            return `VM-${String.fromCharCode(65 + (index % 26))}`;
        }
        if (name.toLowerCase().includes('log') || name.toLowerCase().includes('analytics')) {
            return `LogAnalytics-${String.fromCharCode(65 + (index % 26))}`;
        }
        
        // Default generic resource name
        return `Resource-${String.fromCharCode(65 + (index % 26))}`;
    };
    
    const anonymizeSubscriptionName = (name, index) => {
        if (!name || !anonymizeData) return name;
        
        const genericNames = ['Production', 'Development', 'Staging', 'Testing', 'Demo', 'Sandbox'];
        return genericNames[index % genericNames.length];
    };
    
    const anonymizeResourceGroup = (name, index) => {
        if (!name || !anonymizeData) return name;
        
        const environments = ['prod', 'dev', 'stage', 'test', 'demo'];
        const apps = ['webapp', 'api', 'database', 'storage', 'network'];
        const env = environments[index % environments.length];
        const app = apps[Math.floor(index / environments.length) % apps.length];
        return `rg-${env}-${app}-001`;
    };
    
    const anonymizeLocation = (location) => {
        if (!location || !anonymizeData) return location;
        
        const locationMap = {
            'eastus': 'East US',
            'eastus2': 'East US 2', 
            'westus': 'West US',
            'westus2': 'West US 2',
            'centralus': 'Central US',
            'northcentralus': 'North Central US',
            'southcentralus': 'South Central US',
            'westcentralus': 'West Central US'
        };
        
        return locationMap[location.toLowerCase()] || 'East US';
    };
    
    const anonymizeCostData = (data) => {
        if (!data || !anonymizeData) return data;
        
        return {
            ...data,
            items: data.items?.map((item, index) => ({
                ...item,
                name: anonymizeResourceName(item.name, index),
                subscriptionName: anonymizeSubscriptionName(item.subscriptionName, Math.floor(index / 5)),
                resourceGroup: anonymizeResourceGroup(item.resourceGroup, index),
                resourceLocation: anonymizeLocation(item.resourceLocation),
                groupingValues: {
                    ...item.groupingValues,
                    ServiceName: item.groupingValues?.ServiceName ? anonymizeResourceName(item.groupingValues.ServiceName, index) : undefined,
                    ResourceLocation: item.groupingValues?.ResourceLocation ? anonymizeLocation(item.groupingValues.ResourceLocation) : undefined
                }
            })) || []
        };
    };

    // Date preset options
    const getDatePresets = () => {
        const now = new Date();
        const presets = {
            'lastMonth': {
                label: 'Last Month',
                from: new Date(now.getFullYear(), now.getMonth() - 1, 1),
                to: new Date(now.getFullYear(), now.getMonth(), 0)
            },
            'thisMonth': {
                label: 'This Month to Date',
                from: new Date(now.getFullYear(), now.getMonth(), 1),
                to: now
            },
            'last3Months': {
                label: 'Last 3 Months',
                from: new Date(now.getFullYear(), now.getMonth() - 3, 1),
                to: now
            },
            'last6Months': {
                label: 'Last 6 Months',
                from: new Date(now.getFullYear(), now.getMonth() - 6, 1),
                to: now
            },
            'lastQuarter': {
                label: 'Last Quarter',
                from: new Date(now.getFullYear(), Math.floor(now.getMonth() / 3) * 3 - 3, 1),
                to: new Date(now.getFullYear(), Math.floor(now.getMonth() / 3) * 3, 0)
            },
            'yearToDate': {
                label: 'Year to Date',
                from: new Date(now.getFullYear(), 0, 1),
                to: now
            }
        };
        return presets;
    };

    // Apply date preset
    const applyDatePreset = (presetKey) => {
        const preset = getDatePresets()[presetKey];
        if (preset) {
            setQueryParams(prev => ({
                ...prev,
                timePeriod: {
                    from: preset.from.toISOString().split('T')[0] + 'T00:00:00Z',
                    to: preset.to.toISOString().split('T')[0] + 'T23:59:59Z'
                }
            }));
            setUseCustomTimeframe(false);
            setSelectedPreset(presetKey);
        }
    };

    // Grouping options based on Azure Cost Management API - sorted alphabetically by column
    const groupingOptions = [
        { value: 'ResourceLocation', label: 'Location' },
        { value: 'ResourceType', label: 'Resource Type' },
        { value: 'MeterCategory', label: 'Meter Category' },
        { value: 'ServiceName', label: 'Service Name' },
        { value: 'ResourceId', label: 'Resource' },
        { value: 'SubscriptionId', label: 'Subscription' },
        { value: 'ResourceGroup', label: 'Resource Group' },
        { value: 'Tags', label: 'Tags' }
    ];

    // Add/remove grouping dimension
    const toggleGrouping = (dimensionName) => {
        setQueryParams(prev => {
            const currentGrouping = prev.dataset.grouping || [];
            const exists = currentGrouping.find(g => g.name === dimensionName);
            
            let newGrouping;
            if (exists) {
                newGrouping = currentGrouping.filter(g => g.name !== dimensionName);
            } else {
                newGrouping = [...currentGrouping, { type: 'Dimension', name: dimensionName }];
            }

            return {
                ...prev,
                dataset: {
                    ...prev.dataset,
                    grouping: newGrouping
                }
            };
        });
    };

    // Main analysis function using Azure Cost Management Query API structure
    const runCostAnalysis = async () => {
        if (!selectedClient) {
            alert('Please select a client first');
            return;
        }
        
        setIsLoading(true);
        try {
            console.log('🔍 Running Cost Analysis with query:', queryParams);
            console.log('📋 Selected client:', selectedClient);
            
            // Convert our query parameters to the backend format
            const response = await apiClient.post(`/costanalysis/clients/${selectedClient.ClientId}/analyze-with-query`, {
                query: queryParams,
                includePreviousPeriod: includePreviousPeriod
            });

            console.log('✅ Cost Analysis Response:', response.data);
            console.log('📊 Number of items received:', response.data?.Items?.length || 0);
            console.log('💰 Summary data:', response.data?.Summary);

            // Normalize the API response to match frontend expectations (camelCase)
            const normalizedData = {
                ...response.data,
                items: response.data.Items?.map(item => ({
                    name: item.Name || 'Unknown',
                    resourceType: item.ResourceType || 'Unknown',
                    resourceGroup: item.ResourceGroup || '',
                    subscriptionId: item.SubscriptionId || '',
                    subscriptionName: item.SubscriptionName || '',
                    previousPeriodCost: item.PreviousPeriodCost || 0,
                    currentPeriodCost: item.CurrentPeriodCost || 0,
                    costDifference: item.CostDifference || 0,
                    percentageChange: item.PercentageChange || 0,
                    currency: item.Currency || 'USD',
                    dailyCosts: item.DailyCosts || [],
                    groupingValues: item.GroupingValues || {}
                })) || [],
                summary: {
                    totalPreviousPeriodCost: response.data.Summary?.TotalPreviousPeriodCost || 0,
                    totalCurrentPeriodCost: response.data.Summary?.TotalCurrentPeriodCost || 0,
                    totalCostDifference: response.data.Summary?.TotalCostDifference || 0,
                    totalPercentageChange: response.data.Summary?.TotalPercentageChange || 0,
                    currency: response.data.Summary?.Currency || 'USD',
                    itemCount: response.data.Summary?.ItemCount || 0
                }
            };
            
            setCostData(normalizedData);
            setPermissionStatus('ready');
            setShowQueryBuilder(false);
        } catch (error) {
            console.error('❌ Failed to run cost analysis:', error);
            console.error('📄 Error response:', error.response?.data);
            console.error('🔢 Error status:', error.response?.status);
            
            if (error.response?.status === 400) {
                const errorData = error.response.data;
                if (errorData.requiresSetup) {
                    setEnvironmentsNeedingSetup(errorData.environmentsNeedingSetup || []);
                    setPermissionStatus('needs-setup');
                } else {
                    setPermissionStatus('error');
                }
            } else {
                setPermissionStatus('error');
            }
        } finally {
            setIsLoading(false);
        }
    };

    // Permission management functions
    const getSetupInstructions = async (environmentId) => {
        try {
            const response = await apiClient.get(`/permissions/environments/${environmentId}/setup-instructions`);
            return response.data;
        } catch (error) {
            console.error('Failed to get setup instructions:', error);
            return null;
        }
    };

    const checkEnvironmentPermissions = async (environmentId) => {
        try {
            const response = await apiClient.post(`/permissions/environments/${environmentId}/check-cost-permissions`);
            if (response.data.hasCostAccess) {
                setPermissionStatus('ready');
            }
            return response.data;
        } catch (error) {
            console.error('Failed to check environment permissions:', error);
            return null;
        }
    };

    const copyCommand = (command, environmentId) => {
        navigator.clipboard.writeText(command);
        setCopiedCommand(environmentId);
        setTimeout(() => setCopiedCommand(''), 2000);
    };

    // Loading state
    if (isLoading && permissionStatus === 'checking') {
        return (
            <div className="space-y-6">
                <div>
                    <h1 className="text-2xl font-bold text-white">Cost Analysis</h1>
                    <p className="text-gray-400">Azure cost analysis for {selectedClient?.Name || 'selected client'}</p>
                </div>
                <div className="bg-gray-900 border border-gray-800 rounded p-8 text-center">
                    <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-yellow-600 mx-auto mb-4"></div>
                    <h2 className="text-xl font-semibold text-white mb-2">Preparing Cost Analysis</h2>
                    <p className="text-gray-400">Setting up the cost analysis environment...</p>
                </div>
            </div>
        );
    }

    // Permission setup required
    if (permissionStatus === 'needs-setup') {
        return (
            <PermissionSetupScreen 
                selectedClient={selectedClient}
                environmentsNeedingSetup={environmentsNeedingSetup}
                onGetInstructions={getSetupInstructions}
                onCheckPermissions={checkEnvironmentPermissions}
                onCopyCommand={copyCommand}
                copiedCommand={copiedCommand}
                onRecheckPermissions={() => setPermissionStatus('ready')}
                onChangeClient={() => setSelectedClient(null)}
                isLoading={isLoading}
            />
        );
    }

    // No client selected
    if (!selectedClient) {
        return (
            <div className="space-y-6">
                <div>
                    <h1 className="text-2xl font-bold text-white">Cost Analysis</h1>
                    <p className="text-gray-400">Azure cost analysis</p>
                </div>

                <div className="bg-gray-900 border border-gray-800 rounded p-8 text-center">
                    <AlertCircle className="h-12 w-12 text-yellow-400 mx-auto mb-4" />
                    <h2 className="text-xl font-semibold text-white mb-2">No Client Context</h2>
                    <p className="text-gray-400 mb-4">Please select a client from the Clients page to analyze their Azure costs.</p>
                    <button
                        onClick={() => window.location.href = '/clients'}
                        className="bg-yellow-600 text-black px-6 py-2 rounded-lg hover:bg-yellow-700 transition-colors font-medium"
                    >
                        Go to Clients
                    </button>
                </div>
            </div>
        );
    }

    // Query builder interface
    if (showQueryBuilder) {
        return (
            <div className="space-y-6">
                <div className="flex items-center justify-between">
                    <div>
                        <h1 className="text-2xl font-bold text-white">Cost Analysis</h1>
                        <p className="text-gray-400">Configure cost analysis for {selectedClient.Name}</p>
                    </div>
                    <button
                        onClick={() => setSelectedClient(null)}
                        className="px-4 py-2 bg-gray-700 hover:bg-gray-600 text-white rounded text-sm"
                    >
                        Change Client
                    </button>
                </div>

                <div className="bg-gray-900 border border-gray-800 rounded-lg p-6">
                    <div className="flex items-center space-x-3 mb-6">
                        <Settings className="text-yellow-600" size={24} />
                        <h2 className="text-lg font-semibold text-white">Query Parameters</h2>
                    </div>

                    <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
                        {/* Time Range Configuration */}
                        <div className="space-y-4">
                            <h3 className="text-white font-medium text-lg">Time Range</h3>
                            
                            {/* Quick Presets */}
                            <div className="space-y-3">
                                <h4 className="text-gray-300 text-sm font-medium">Quick Presets</h4>
                                <div className="grid grid-cols-3 gap-2">
                                    {Object.entries(getDatePresets()).map(([key, preset]) => (
                                        <button
                                            key={key}
                                            onClick={() => applyDatePreset(key)}
                                            className={`px-2 py-1.5 rounded text-sm transition-colors ${
                                                !useCustomTimeframe && selectedPreset === key
                                                    ? 'bg-yellow-600 text-black hover:bg-yellow-700 font-medium' 
                                                    : 'bg-gray-700 hover:bg-gray-600 text-white'
                                            }`}
                                        >
                                            {preset.label}
                                        </button>
                                    ))}
                                </div>
                                
                                {/* Show selected preset date range */}
                                {!useCustomTimeframe && selectedPreset && (
                                    <div className="bg-yellow-900/20 border border-yellow-800/50 rounded p-3">
                                        <p className="text-sm text-yellow-200">
                                            <span className="font-medium">{getDatePresets()[selectedPreset]?.label}:</span>
                                            {' '}
                                            {getDatePresets()[selectedPreset]?.from.toLocaleDateString()} - {getDatePresets()[selectedPreset]?.to.toLocaleDateString()}
                                        </p>
                                    </div>
                                )}
                            </div>

                            {/* Custom Timeframe Toggle */}
                            <div className="border-t border-gray-700 pt-3">
                                <button
                                    onClick={() => {
                                        setUseCustomTimeframe(!useCustomTimeframe);
                                        if (!useCustomTimeframe) {
                                            setSelectedPreset(null);
                                        }
                                    }}
                                    className={`px-4 py-2 rounded text-sm transition-colors w-full ${
                                        useCustomTimeframe 
                                            ? 'bg-yellow-600 text-black hover:bg-yellow-700 font-medium'
                                            : 'bg-gray-700 hover:bg-gray-600 text-white'
                                    }`}
                                >
                                    {useCustomTimeframe ? '✓ Custom Timeframe' : 'Custom Timeframe'}
                                </button>
                            </div>

                            {/* Custom date inputs - only show when custom is selected */}
                            {useCustomTimeframe && (
                                <div className="space-y-3 bg-gray-800 p-4 rounded border border-gray-700">
                                    <h4 className="text-gray-300 text-sm font-medium">Custom Date Range</h4>
                                    <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
                                        <div>
                                            <label className="block text-sm text-gray-400 mb-1">From Date</label>
                                            <input
                                                type="date"
                                                value={queryParams.timePeriod.from.split('T')[0]}
                                                onChange={(e) => setQueryParams(prev => ({
                                                    ...prev,
                                                    timePeriod: {
                                                        ...prev.timePeriod,
                                                        from: e.target.value + 'T00:00:00Z'
                                                    }
                                                }))}
                                                className="w-full px-3 py-2 bg-gray-700 border border-gray-600 rounded text-white focus:border-yellow-600 focus:ring-1 focus:ring-yellow-600"
                                            />
                                        </div>
                                        <div>
                                            <label className="block text-sm text-gray-400 mb-1">To Date</label>
                                            <input
                                                type="date"
                                                value={queryParams.timePeriod.to.split('T')[0]}
                                                onChange={(e) => setQueryParams(prev => ({
                                                    ...prev,
                                                    timePeriod: {
                                                        ...prev.timePeriod,
                                                        to: e.target.value + 'T23:59:59Z'
                                                    }
                                                }))}
                                                className="w-full px-3 py-2 bg-gray-700 border border-gray-600 rounded text-white focus:border-yellow-600 focus:ring-1 focus:ring-yellow-600"
                                            />
                                        </div>
                                    </div>
                                </div>
                            )}
                        </div>

                        {/* Data Breakdown Configuration */}
                        <div className="space-y-4">
                            <h3 className="text-white font-medium text-lg">Data Breakdown</h3>
                            <div className="bg-blue-900/20 border border-blue-800/50 rounded p-3">
                                <p className="text-sm text-blue-200">Select as few or as many dimensions as needed for your cost analysis. Only selected dimensions will appear as columns in the results.</p>
                            </div>
                            
                            <div className="space-y-2 bg-gray-800 p-4 rounded border border-gray-700">
                                <h4 className="text-gray-300 text-sm font-medium mb-3">Available Dimensions</h4>
                                <div className="grid grid-cols-2 gap-2">
                                    {groupingOptions.map((option) => {
                                        const isSelected = queryParams.dataset.grouping?.some(g => g.name === option.value);
                                        return (
                                            <label key={option.value} className="flex items-center space-x-3 cursor-pointer p-2 rounded hover:bg-gray-700/50 transition-colors">
                                                <input
                                                    type="checkbox"
                                                    checked={isSelected}
                                                    onChange={() => toggleGrouping(option.value)}
                                                    className="w-4 h-4 text-yellow-600 bg-gray-700 border-gray-600 rounded focus:ring-yellow-600 focus:ring-2"
                                                />
                                                <span className={`text-sm ${
                                                    isSelected ? 'text-yellow-200 font-medium' : 'text-white'
                                                }`}>{option.label}</span>
                                            </label>
                                        );
                                    })}
                                </div>
                            </div>
                        </div>
                    </div>

                    {/* Granularity and Display Options */}
                    <div className="mt-8 pt-6 border-t border-gray-700 space-y-6">
                        <div className="flex items-center space-x-2 mb-4">
                            <div className="w-2 h-2 bg-yellow-600 rounded-full"></div>
                            <h3 className="text-white font-medium text-lg">Analysis Options</h3>
                        </div>
                        <div className="bg-gray-800 p-4 rounded border border-gray-700">
                            <h4 className="text-gray-300 font-medium mb-3">Data Granularity</h4>
                            <div className="flex space-x-6">
                                <label className="flex items-center space-x-3 cursor-pointer p-2 rounded hover:bg-gray-700/50 transition-colors">
                                    <input
                                        type="radio"
                                        name="granularity"
                                        value="None"
                                        checked={queryParams.dataset.granularity === 'None'}
                                        onChange={(e) => setQueryParams(prev => ({
                                            ...prev,
                                            dataset: { ...prev.dataset, granularity: e.target.value }
                                        }))}
                                        className="text-yellow-600 bg-gray-700 border-gray-600 focus:ring-yellow-600"
                                    />
                                    <div>
                                        <span className="text-white font-medium">Aggregated</span>
                                        <p className="text-xs text-gray-400">Total costs for the period</p>
                                    </div>
                                </label>
                                <label className="flex items-center space-x-3 cursor-pointer p-2 rounded hover:bg-gray-700/50 transition-colors">
                                    <input
                                        type="radio"
                                        name="granularity"
                                        value="Daily"
                                        checked={queryParams.dataset.granularity === 'Daily'}
                                        onChange={(e) => setQueryParams(prev => ({
                                            ...prev,
                                            dataset: { ...prev.dataset, granularity: e.target.value }
                                        }))}
                                        className="text-yellow-600 bg-gray-700 border-gray-600 focus:ring-yellow-600"
                                    />
                                    <div>
                                        <span className="text-white font-medium">Daily</span>
                                        <p className="text-xs text-gray-400">Daily cost breakdown</p>
                                    </div>
                                </label>
                            </div>
                        </div>
                        
                        {/* Hidden keyboard shortcut for anonymization - press 'A' key 3 times quickly */}
                        <div style={{ position: 'absolute', left: '-9999px', opacity: 0 }}>
                            <input
                                ref={anonymizeInputRef}
                                type="checkbox"
                                checked={anonymizeData}
                                onChange={(e) => setAnonymizeData(e.target.checked)}
                                tabIndex={-1}
                            />
                        </div>

                        <div className="bg-gray-800 p-4 rounded border border-gray-700">
                            <h4 className="text-gray-300 font-medium mb-3">Display Options</h4>
                            <div className="space-y-4">
                                <label className="flex items-start space-x-3 cursor-pointer p-2 rounded hover:bg-gray-700/50 transition-colors">
                                    <input
                                        type="checkbox"
                                        checked={includePreviousPeriod}
                                        onChange={(e) => setIncludePreviousPeriod(e.target.checked)}
                                        className="w-4 h-4 text-yellow-600 bg-gray-700 border-gray-600 rounded focus:ring-yellow-600 focus:ring-2 mt-0.5"
                                    />
                                    <div>
                                        <span className="text-white font-medium">Include Previous Period Comparison</span>
                                        <p className="text-sm text-gray-400 mt-1">Show cost changes and percentage differences compared to the previous period</p>
                                    </div>
                                </label>
                                
                                <label className={`flex items-start space-x-3 p-2 rounded transition-colors ${
                                    queryParams.dataset.granularity === 'None' 
                                        ? 'cursor-not-allowed opacity-50' 
                                        : 'cursor-pointer hover:bg-gray-700/50'
                                }`}>
                                    <input
                                        type="checkbox"
                                        checked={showCostCalendar}
                                        disabled={queryParams.dataset.granularity === 'None'}
                                        onChange={(e) => setShowCostCalendar(e.target.checked)}
                                        className="w-4 h-4 text-yellow-600 bg-gray-700 border-gray-600 rounded focus:ring-yellow-600 focus:ring-2 disabled:opacity-50 disabled:cursor-not-allowed mt-0.5"
                                    />
                                    <div>
                                        <span className="text-white font-medium">Show Daily Cost Calendar</span>
                                        <p className="text-sm text-gray-400 mt-1">
                                            Display interactive calendar with daily cost breakdown
                                            {queryParams.dataset.granularity === 'None' 
                                                ? ' (requires Daily granularity)' 
                                                : ' (only shown for Daily granularity)'}
                                        </p>
                                    </div>
                                </label>
                            </div>
                        </div>
                    </div>

                    {/* Run Analysis Button */}
                    <div className="mt-8 flex justify-end">
                        <button
                            onClick={runCostAnalysis}
                            disabled={isLoading}
                            className="px-6 py-3 bg-yellow-600 hover:bg-yellow-700 disabled:opacity-50 text-black rounded-lg font-medium flex items-center space-x-2 transition-colors"
                        >
                            {isLoading ? (
                                <RefreshCw size={20} className="animate-spin" />
                            ) : (
                                <Play size={20} />
                            )}
                            <span>Run Cost Analysis</span>
                        </button>
                    </div>

                    {/* Preview Query Section */}
                    <div className="mt-6">
                        <details className="bg-gray-800 border border-gray-700 rounded-lg p-4">
                            <summary className="text-white font-medium cursor-pointer hover:text-yellow-600 transition-colors">
                                Preview Query (Click to expand)
                            </summary>
                            <div className="mt-4 space-y-4">
                                <div>
                                    <h4 className="text-sm font-medium text-gray-300 mb-2">Azure Cost Management Query:</h4>
                                    <div className="bg-gray-900 rounded p-3 overflow-x-auto">
                                        <pre className="text-sm text-gray-300 whitespace-pre-wrap">
                                            {JSON.stringify({
                                                type: queryParams.type,
                                                timeframe: queryParams.timeframe,
                                                timePeriod: queryParams.timePeriod,
                                                dataset: {
                                                    granularity: queryParams.dataset.granularity,
                                                    aggregation: {
                                                        totalCost: {
                                                            name: queryParams.dataset.aggregation.totalCost.name,
                                                            function: queryParams.dataset.aggregation.totalCost.function
                                                        }
                                                    },
                                                    grouping: queryParams.dataset.grouping
                                                }
                                            }, null, 2)}
                                        </pre>
                                    </div>
                                </div>
                                
                                <div>
                                    <h4 className="text-sm font-medium text-gray-300 mb-2">Governance Guardian Options:</h4>
                                    <div className="bg-gray-900 rounded p-3">
                                        <div className="text-sm text-gray-300">
                                            • Include Previous Period: <span className={includePreviousPeriod ? 'text-green-400' : 'text-red-400'}>
                                                {includePreviousPeriod ? 'Yes' : 'No'}
                                            </span>
                                            <br />
                                            • Show Cost Calendar: <span className={showCostCalendar ? 'text-green-400' : 'text-red-400'}>
                                                {showCostCalendar ? 'Yes' : 'No'}
                                            </span>
                                        </div>
                                    </div>
                                </div>
                            </div>
                        </details>
                    </div>
                </div>
            </div>
        );
    }

    // Cost analysis results
    if (permissionStatus === 'ready' && costData) {
        console.log('📈 Rendering cost analysis results with data:', costData);
        console.log('📊 Items to display:', costData.Items);
        console.log('💼 Summary to display:', costData.Summary);
        
        return (
            <div className="space-y-6">
                <div className="flex items-center justify-between">
                    <div>
                        <h1 className="text-2xl font-bold text-white">Cost Analysis Results</h1>
                        <p className="text-gray-400">Azure costs for {selectedClient.Name}</p>
                        {/* DEBUG INFO */}
                        <p className="text-xs text-gray-500 mt-1">
                            Debug: {costData?.Items?.length || 0} items | Status: {permissionStatus} | 
                            Summary Total: ${costData?.Summary?.TotalCurrentPeriodCost || 0}
                        </p>
                    </div>
                    <div className="flex items-center space-x-3">
                        <button
                            onClick={() => setShowQueryBuilder(true)}
                            className="px-4 py-2 bg-gray-700 hover:bg-gray-600 text-white rounded text-sm flex items-center space-x-2"
                        >
                            <Settings size={16} />
                            <span>Modify Query</span>
                        </button>
                        <button
                            onClick={runCostAnalysis}
                            disabled={isLoading}
                            className="px-4 py-2 bg-yellow-600 hover:bg-yellow-700 text-black rounded text-sm font-medium flex items-center space-x-2"
                        >
                            {isLoading ? (
                                <RefreshCw size={16} className="animate-spin" />
                            ) : (
                                <RefreshCw size={16} />
                            )}
                            <span>Refresh</span>
                        </button>
                    </div>
                </div>

                {/* Show raw data for debugging */}
                <div className="bg-gray-900 border border-gray-800 rounded-lg p-4">
                    <details>
                        <summary className="text-white font-medium cursor-pointer hover:text-yellow-600 transition-colors">
                            Debug: Raw API Response (Click to expand)
                        </summary>
                        <div className="mt-4 bg-gray-800 rounded p-3 overflow-x-auto">
                            <pre className="text-xs text-gray-300 whitespace-pre-wrap">
                                {JSON.stringify(costData, null, 2)}
                            </pre>
                        </div>
                    </details>
                </div>

                {costData?.Items?.length > 0 ? (
                    <>
                        <CostSummaryCards 
                            costData={anonymizeCostData(costData)}
                            formatCurrency={formatCurrency}
                            getChangeIcon={getChangeIcon}
                            getChangeColor={getChangeColor}
                            formatPercentage={formatPercentage}
                            queryParams={queryParams}
                            includePreviousPeriod={includePreviousPeriod}
                        />

                        {/* Daily Cost Calendar - show when Daily granularity is selected AND showCostCalendar is enabled */}
                        {showCostCalendar && costData?.OriginalQuery?.dataset?.granularity === 'Daily' && (
                            <div className="bg-gray-900 border border-gray-800 rounded-lg">
                                <details className="group" open>
                                    <summary className="flex items-center justify-between p-4 cursor-pointer hover:bg-gray-800/50 transition-colors rounded-t-lg">
                                        <div className="flex items-center space-x-3">
                                            <div className="w-2 h-2 bg-blue-400 rounded-full"></div>
                                            <h2 className="text-lg font-semibold text-white">Cost Calendar</h2>
                                        </div>
                                        <div className="text-gray-400 group-open:rotate-180 transition-transform">
                                            <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                                                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 9l-7 7-7-7" />
                                            </svg>
                                        </div>
                                    </summary>
                                    <div className="px-4 pb-4">
                                        <DailyCostCalendar 
                                            costData={anonymizeCostData(costData)}
                                            formatCurrency={formatCurrency}
                                        />
                                    </div>
                                </details>
                            </div>
                        )}

                        <CostAnalysisTable 
                            costData={anonymizeCostData(costData)}
                            queryParams={queryParams}
                            includePreviousPeriod={includePreviousPeriod}
                            formatCurrency={formatCurrency}
                            getChangeColor={getChangeColor}
                            getChangeIcon={getChangeIcon}
                            formatPercentage={formatPercentage}
                            onRefresh={runCostAnalysis}
                        />
                    </>
                ) : (
                    <div className="bg-gray-900 border border-gray-800 rounded p-8 text-center">
                        <AlertCircle className="h-12 w-12 text-yellow-400 mx-auto mb-4" />
                        <h2 className="text-xl font-semibold text-white mb-2">No Cost Data Found</h2>
                        <p className="text-gray-400 mb-4">
                            No cost data was found for the selected time period and filters. 
                            This could be because there were no costs during this period or the Azure subscriptions don't have cost data available.
                        </p>
                        <button
                            onClick={runCostAnalysis}
                            className="bg-yellow-600 text-black px-6 py-2 rounded-lg hover:bg-yellow-700 transition-colors font-medium"
                        >
                            Refresh Analysis
                        </button>
                    </div>
                )}
            </div>
        );
    }

    // Error state
    return (
        <div className="space-y-6">
            <div className="flex items-center justify-between">
                <div>
                    <h1 className="text-2xl font-bold text-white">Cost Analysis</h1>
                    <p className="text-gray-400">Azure cost analysis for {selectedClient?.Name}</p>
                </div>
                <button
                    onClick={() => setSelectedClient(null)}
                    className="px-4 py-2 bg-gray-700 hover:bg-gray-600 text-white rounded text-sm"
                >
                    Change Client
                </button>
            </div>

            <div className="bg-gray-900 border border-gray-800 rounded p-8 text-center">
                <AlertCircle className="h-12 w-12 text-red-400 mx-auto mb-4" />
                <h2 className="text-xl font-semibold text-white mb-2">Unable to Load Cost Analysis</h2>
                <p className="text-gray-400 mb-4">There was an error loading the cost analysis data.</p>
                <button
                    onClick={() => setShowQueryBuilder(true)}
                    className="bg-yellow-600 text-black px-6 py-2 rounded-lg hover:bg-yellow-700 transition-colors font-medium"
                >
                    Try Again
                </button>
            </div>
        </div>
    );
};

export default CostAnalysisPage;