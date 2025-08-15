import React, { useState } from 'react';
import { Shield, CheckCircle, XCircle, AlertTriangle, RefreshCw, ChevronDown, ChevronRight } from 'lucide-react';
import { useClient } from '../contexts/ClientContext';
import { apiClient } from '../services/apiService';

const PermissionsCheckerTool = () => {
    const { clients } = useClient();
    const [selectedClient, setSelectedClient] = useState('');
    const [permissionMatrix, setPermissionMatrix] = useState(null);
    const [isLoading, setIsLoading] = useState(false);
    const [expandedEnvironments, setExpandedEnvironments] = useState(new Set());

    const checkClientPermissions = async (clientId) => {
        setIsLoading(true);
        try {
            const response = await apiClient.post(`/permissions/clients/${clientId}/check-all-permissions`);
            if (response.data) {
                setPermissionMatrix(response.data);
            }
        } catch (error) {
            console.error('Failed to check permissions:', error);
        } finally {
            setIsLoading(false);
        }
    };

    const toggleEnvironmentExpansion = (envId) => {
        const newExpanded = new Set(expandedEnvironments);
        if (newExpanded.has(envId)) {
            newExpanded.delete(envId);
        } else {
            newExpanded.add(envId);
        }
        setExpandedEnvironments(newExpanded);
    };

    const getStatusIcon = (status) => {
        switch (status) {
            case 'Ready':
                return <CheckCircle className="h-5 w-5 text-green-400" />;
            case 'SetupRequired':
                return <AlertTriangle className="h-5 w-5 text-yellow-400" />;
            case 'Error':
                return <XCircle className="h-5 w-5 text-red-400" />;
            default:
                return <AlertTriangle className="h-5 w-5 text-gray-400" />;
        }
    };

    const getStatusColor = (status) => {
        switch (status) {
            case 'Ready':
                return 'text-green-400 bg-green-900/20 border-green-800';
            case 'SetupRequired':
                return 'text-yellow-400 bg-yellow-900/20 border-yellow-800';
            case 'Error':
                return 'text-red-400 bg-red-900/20 border-red-800';
            default:
                return 'text-gray-400 bg-gray-900/20 border-gray-700';
        }
    };

    return (
        <div className="space-y-6">
            {/* Header */}
            <div className="mb-6">
                <div className="flex items-center space-x-3 mb-2">
                    <Shield className="h-8 w-8 text-blue-400" />
                    <h1 className="text-2xl font-bold text-white">Permissions Checker</h1>
                </div>
                <p className="text-gray-400">
                    Check Azure permissions across all clients and environments. Verify cost management access and other API permissions.
                </p>
            </div>

            {/* Client Selection */}
            <div className="bg-gray-900 border border-gray-800 rounded-lg p-6">
                <h2 className="text-lg font-semibold text-white mb-4">Select Client to Check</h2>
                <div className="flex space-x-4">
                    <select
                        value={selectedClient}
                        onChange={(e) => setSelectedClient(e.target.value)}
                        className="flex-1 border border-gray-600 bg-gray-700 rounded-lg px-3 py-2 text-white focus:outline-none focus:ring-2 focus:ring-blue-500"
                    >
                        <option value="">Select a client...</option>
                        {clients.map((client) => (
                            <option key={client.ClientId} value={client.ClientId}>
                                {client.Name}
                            </option>
                        ))}
                    </select>
                    <button
                        onClick={() => selectedClient && checkClientPermissions(selectedClient)}
                        disabled={!selectedClient || isLoading}
                        className="bg-blue-600 text-white px-6 py-2 rounded-lg hover:bg-blue-700 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
                    >
                        {isLoading ? (
                            <RefreshCw className="h-4 w-4 animate-spin inline mr-2" />
                        ) : (
                            <Shield className="h-4 w-4 inline mr-2" />
                        )}
                        Check Permissions
                    </button>
                </div>
            </div>

            {/* Results */}
            {permissionMatrix && (
                <div className="space-y-6">
                    {/* Overall Status */}
                    <div className="bg-gray-900 border border-gray-800 rounded-lg p-6">
                        <h2 className="text-lg font-semibold text-white mb-4">Overall Status</h2>
                        <div className="flex items-center space-x-3">
                            {getStatusIcon((permissionMatrix.overallStatus && permissionMatrix.overallStatus.includes('All')) ? 'Ready' : 'SetupRequired')}
                            <span className="text-lg font-medium text-white">{permissionMatrix.overallStatus || 'Unknown'}</span>
                        </div>
                        <p className="text-sm text-gray-400 mt-2">
                            Checked at: {permissionMatrix.checkedAt ? new Date(permissionMatrix.checkedAt).toLocaleString() : 'Never'}
                        </p>
                    </div>

                    {/* Environment Details */}
                    <div className="bg-gray-900 border border-gray-800 rounded-lg p-6">
                        <h2 className="text-lg font-semibold text-white mb-4">Environment Details</h2>
                        <div className="space-y-4">
                            {(permissionMatrix.environmentStatuses || []).map((env) => (
                                <div key={env.azureEnvironmentId} className="border border-gray-700 rounded-lg">
                                    <div 
                                        className="p-4 cursor-pointer hover:bg-gray-800/50"
                                        onClick={() => toggleEnvironmentExpansion(env.azureEnvironmentId)}
                                    >
                                        <div className="flex items-center justify-between">
                                            <div className="flex items-center space-x-3">
                                                {expandedEnvironments.has(env.azureEnvironmentId) ? (
                                                    <ChevronDown className="h-5 w-5 text-gray-400" />
                                                ) : (
                                                    <ChevronRight className="h-5 w-5 text-gray-400" />
                                                )}
                                                <div>
                                                    <h3 className="font-medium text-white">
                                                        {env.environmentName || 'Azure Environment'}
                                                    </h3>
                                                    <p className="text-sm text-gray-400">
                                                        {(env.subscriptionIds || []).length} subscription(s)
                                                    </p>
                                                </div>
                                            </div>
                                            <div className="flex items-center space-x-3">
                                                <span className={`px-3 py-1 rounded-full text-sm font-medium border ${getStatusColor(env.costManagementSetupStatus)}`}>
                                                    {env.costManagementSetupStatus}
                                                </span>
                                                {getStatusIcon(env.costManagementSetupStatus)}
                                            </div>
                                        </div>
                                    </div>

                                    {expandedEnvironments.has(env.azureEnvironmentId) && (
                                        <div className="border-t border-gray-700 p-4 bg-gray-800/30">
                                            <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                                                <div>
                                                    <h4 className="font-medium text-white mb-2">Available Permissions</h4>
                                                    <div className="space-y-1">
                                                        {(env.availablePermissions || []).length > 0 ? (
                                                            (env.availablePermissions || []).map((perm, index) => (
                                                                <div key={index} className="flex items-center space-x-2">
                                                                    <CheckCircle className="h-4 w-4 text-green-400" />
                                                                    <span className="text-sm text-gray-300">{perm}</span>
                                                                </div>
                                                            ))
                                                        ) : (
                                                            <p className="text-sm text-gray-500">No permissions detected</p>
                                                        )}
                                                    </div>
                                                </div>

                                                <div>
                                                    <h4 className="font-medium text-white mb-2">Missing Permissions</h4>
                                                    <div className="space-y-1">
                                                        {(env.missingPermissions || []).length > 0 ? (
                                                            (env.missingPermissions || []).map((perm, index) => (
                                                                <div key={index} className="flex items-center space-x-2">
                                                                    <XCircle className="h-4 w-4 text-red-400" />
                                                                    <span className="text-sm text-gray-300">{perm}</span>
                                                                </div>
                                                            ))
                                                        ) : (
                                                            <p className="text-sm text-green-400">All permissions available</p>
                                                        )}
                                                    </div>
                                                </div>
                                            </div>

                                            {env.lastError && (
                                                <div className="mt-4 p-3 bg-red-900/20 border border-red-800 rounded-lg">
                                                    <p className="text-sm text-red-300">
                                                        <strong>Last Error:</strong> {env.lastError}
                                                    </p>
                                                </div>
                                            )}

                                            <div className="mt-4 flex justify-between items-center text-sm text-gray-400">
                                                <span>
                                                    Subscriptions: {(env.subscriptionIds || []).join(', ') || 'None'}
                                                </span>
                                                {env.lastChecked && (
                                                    <span>
                                                        Last checked: {new Date(env.lastChecked).toLocaleString()}
                                                    </span>
                                                )}
                                            </div>
                                        </div>
                                    )}
                                </div>
                            ))}
                        </div>
                    </div>

                    {/* Summary Stats */}
                    <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
                        <div className="bg-gray-900 border border-gray-800 rounded-lg p-6">
                            <div className="flex items-center space-x-3">
                                <CheckCircle className="h-8 w-8 text-green-400" />
                                <div>
                                    <h3 className="text-lg font-semibold text-white">Ready</h3>
                                    <p className="text-2xl font-bold text-green-400">
                                        {(permissionMatrix.environmentStatuses || []).filter(e => e.hasCostManagementAccess).length}
                                    </p>
                                </div>
                            </div>
                        </div>

                        <div className="bg-gray-900 border border-gray-800 rounded-lg p-6">
                            <div className="flex items-center space-x-3">
                                <AlertTriangle className="h-8 w-8 text-yellow-400" />
                                <div>
                                    <h3 className="text-lg font-semibold text-white">Needs Setup</h3>
                                    <p className="text-2xl font-bold text-yellow-400">
                                        {(permissionMatrix.environmentStatuses || []).filter(e => !e.hasCostManagementAccess && e.costManagementSetupStatus === 'SetupRequired').length}
                                    </p>
                                </div>
                            </div>
                        </div>

                        <div className="bg-gray-900 border border-gray-800 rounded-lg p-6">
                            <div className="flex items-center space-x-3">
                                <XCircle className="h-8 w-8 text-red-400" />
                                <div>
                                    <h3 className="text-lg font-semibold text-white">Errors</h3>
                                    <p className="text-2xl font-bold text-red-400">
                                        {(permissionMatrix.environmentStatuses || []).filter(e => e.costManagementSetupStatus === 'Error').length}
                                    </p>
                                </div>
                            </div>
                        </div>
                    </div>
                </div>
            )}

            {/* No Results Message */}
            {!permissionMatrix && !isLoading && (
                <div className="bg-gray-900 border border-gray-800 rounded-lg p-8 text-center">
                    <Shield className="h-12 w-12 text-gray-400 mx-auto mb-4" />
                    <h3 className="text-lg font-medium text-white mb-2">No Results Yet</h3>
                    <p className="text-gray-400">
                        Select a client above and click "Check Permissions" to analyze their Azure environment access.
                    </p>
                </div>
            )}
        </div>
    );
};

export default PermissionsCheckerTool;