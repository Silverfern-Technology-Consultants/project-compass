import React, { useState } from 'react';
import { AlertCircle, RefreshCw, TrendingUp, TrendingDown } from 'lucide-react';
import { useClient } from '../contexts/ClientContext';
import { apiClient } from '../services/apiService';

// Import our components
import ClientSelector from './cost-analysis/ClientSelector';
import PermissionSetupScreen from './cost-analysis/PermissionSetupScreen';
import FilterControls from './cost-analysis/FilterControls';
import CostSummaryCards from './cost-analysis/CostSummaryCards';
import CostAnalysisTable from './cost-analysis/CostAnalysisTable';

const CostAnalysisPage = () => {
    const { clients } = useClient();
    const [selectedClient, setSelectedClient] = useState(null);
    const [permissionStatus, setPermissionStatus] = useState('checking');
    const [environmentsNeedingSetup, setEnvironmentsNeedingSetup] = useState([]);
    const [costData, setCostData] = useState(null);
    const [isLoading, setIsLoading] = useState(false);
    const [copiedCommand, setCopiedCommand] = useState('');

    // Filter state
    const [timeRange, setTimeRange] = useState('LastMonthToThisMonth');
    const [aggregation, setAggregation] = useState('ResourceType');
    const [sortBy, setSortBy] = useState('CostDifference');
    const [sortDirection, setSortDirection] = useState('Descending');

    // Utility functions
    const formatCurrency = (amount, currency = 'USD') => {
        return new Intl.NumberFormat('en-US', {
            style: 'currency',
            currency,
            minimumFractionDigits: 2,
            maximumFractionDigits: 2
        }).format(amount);
    };

    const formatPercentage = (percentage) => {
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
        if (percentage > 0) {
            return 'text-red-400';
        } else if (percentage < 0) {
            return 'text-green-400';
        }
        return 'text-gray-400';
    };

    // Main analysis function
    const checkPermissionsAndLoadData = async (clientId = selectedClient?.ClientId) => {
        if (!clientId) return;
        
        setIsLoading(true);
        try {
            // Convert enum values for backend
            const timeRangeValue = {
                'LastMonthToThisMonth': 0,
                'Last3Months': 1,
                'Last6Months': 2,
                'LastYearToThisYear': 3,
                'Custom': 4
            }[timeRange] || 0;

            const aggregationValue = {
                'None': 0,
                'ResourceType': 1,
                'ResourceGroup': 2,
                'Subscription': 3,
                'Daily': 4
            }[aggregation] || 1;

            const sortByValue = {
                'Name': 0,
                'ResourceType': 1,
                'PreviousPeriodCost': 2,
                'CurrentPeriodCost': 3,
                'CostDifference': 4,
                'PercentageChange': 5
            }[sortBy] || 4;

            const sortDirectionValue = {
                'Ascending': 0,
                'Descending': 1
            }[sortDirection] || 1;

            // Try to run cost analysis - it will tell us if permissions are missing
            const response = await apiClient.post(`/costanalysis/clients/${clientId}/analyze`, {
                TimeRange: timeRangeValue,
                Aggregation: aggregationValue,
                SortBy: sortByValue,
                SortDirection: sortDirectionValue
            });

            setCostData(response.data);
            setPermissionStatus('ready');
        } catch (error) {
            console.error('Failed to check permissions or load cost data:', error);
            
            if (error.response?.status === 400) {
                // Check if it's a permission error
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
                await checkPermissionsAndLoadData();
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

    // Event handlers
    const handleClientSelect = (client) => {
        setSelectedClient(client);
        setCostData(null);
        setPermissionStatus('checking');
        checkPermissionsAndLoadData(client.ClientId);
    };

    const handleChangeClient = () => {
        setSelectedClient(null);
        setCostData(null);
        setPermissionStatus('checking');
    };

    const handleApplyFilters = () => {
        checkPermissionsAndLoadData();
    };

    // Render different screens based on state
    if (!selectedClient) {
        return (
            <ClientSelector 
                clients={clients} 
                onClientSelect={handleClientSelect} 
            />
        );
    }

    // Loading state
    if (isLoading && permissionStatus === 'checking') {
        return (
            <div className="space-y-6">
                <div>
                    <h1 className="text-2xl font-bold text-white">Cost Analysis</h1>
                    <p className="text-gray-400">Azure cost trends for {selectedClient.Name}</p>
                </div>
                <div className="bg-gray-900 border border-gray-800 rounded p-8 text-center">
                    <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-yellow-600 mx-auto mb-4"></div>
                    <h2 className="text-xl font-semibold text-white mb-2">Checking Cost Analysis Access</h2>
                    <p className="text-gray-400">Verifying permissions and loading cost data...</p>
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
                onRecheckPermissions={checkPermissionsAndLoadData}
                onChangeClient={handleChangeClient}
                isLoading={isLoading}
            />
        );
    }

    // Cost analysis ready and loaded
    if (permissionStatus === 'ready' && costData) {
        return (
            <div className="space-y-6">
                <div className="flex items-center justify-between">
                    <div>
                        <h1 className="text-2xl font-bold text-white">Cost Analysis</h1>
                        <p className="text-gray-400">Azure cost trends for {selectedClient.Name}</p>
                    </div>
                    <div className="flex items-center space-x-3">
                        <button
                            onClick={handleChangeClient}
                            className="px-4 py-2 bg-gray-700 hover:bg-gray-600 text-white rounded text-sm"
                        >
                            Change Client
                        </button>
                        <button
                            onClick={checkPermissionsAndLoadData}
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

                <FilterControls 
                    timeRange={timeRange}
                    setTimeRange={setTimeRange}
                    aggregation={aggregation}
                    setAggregation={setAggregation}
                    sortBy={sortBy}
                    setSortBy={setSortBy}
                    sortDirection={sortDirection}
                    setSortDirection={setSortDirection}
                    onApplyFilters={handleApplyFilters}
                    isLoading={isLoading}
                />

                <CostSummaryCards 
                    costData={costData}
                    formatCurrency={formatCurrency}
                    getChangeIcon={getChangeIcon}
                    getChangeColor={getChangeColor}
                    formatPercentage={formatPercentage}
                />

                <CostAnalysisTable 
                    costData={costData}
                    aggregation={aggregation}
                    formatCurrency={formatCurrency}
                    getChangeColor={getChangeColor}
                    getChangeIcon={getChangeIcon}
                    formatPercentage={formatPercentage}
                    onRefresh={checkPermissionsAndLoadData}
                />
            </div>
        );
    }

    // Error state
    return (
        <div className="space-y-6">
            <div className="flex items-center justify-between">
                <div>
                    <h1 className="text-2xl font-bold text-white">Cost Analysis</h1>
                    <p className="text-gray-400">Azure cost trends for {selectedClient.Name}</p>
                </div>
                <button
                    onClick={handleChangeClient}
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
                    onClick={checkPermissionsAndLoadData}
                    className="bg-yellow-600 text-black px-6 py-2 rounded-lg hover:bg-yellow-700 transition-colors font-medium"
                >
                    Try Again
                </button>
            </div>
        </div>
    );
};

export default CostAnalysisPage;