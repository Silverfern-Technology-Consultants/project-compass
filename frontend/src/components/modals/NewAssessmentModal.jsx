import React, { useState, useEffect } from 'react';
import { createPortal } from 'react-dom';
import { Play, Building2, Cloud, AlertCircle, TestTube, Loader2, CheckCircle, AlertTriangle } from 'lucide-react';
import { useClient } from '../../contexts/ClientContext';
import { azureEnvironmentsApi } from '../../services/apiService';

const NewAssessmentModal = ({ isOpen, onClose, onStart }) => {
    const { clients, selectedClient, isInternalSelected } = useClient();
    const [formData, setFormData] = useState({
        name: '',
        clientId: '',
        environmentId: '',
        type: 'Full'
    });
    const [environments, setEnvironments] = useState([]);
    const [loadingEnvironments, setLoadingEnvironments] = useState(false);
    const [environmentError, setEnvironmentError] = useState(null);
    const [testingConnection, setTestingConnection] = useState(null);
    const [connectionResults, setConnectionResults] = useState({});

    // Reset form when modal opens/closes
    useEffect(() => {
        if (isOpen) {
            // Pre-select current client if one is selected
            const initialClientId = selectedClient && !selectedClient.isInternal ? selectedClient.ClientId : '';
            setFormData(prev => ({
                ...prev,
                clientId: initialClientId,
                environmentId: '',
                name: ''
            }));

            // Load environments if client is pre-selected
            if (initialClientId) {
                loadClientEnvironments(initialClientId);
            }
        } else {
            // Reset form when modal closes
            setFormData({
                name: '',
                clientId: '',
                environmentId: '',
                type: 'Full'
            });
            setEnvironments([]);
            setEnvironmentError(null);
            setConnectionResults({});
            setTestingConnection(null);
        }
    }, [isOpen, selectedClient]);

    const loadClientEnvironments = async (clientId) => {
        if (!clientId || clientId === 'internal') {
            setEnvironments([]);
            return;
        }

        try {
            setLoadingEnvironments(true);
            setEnvironmentError(null);

            // Import API service to avoid circular dependency
            const { apiClient } = await import('../../services/apiService');
            const response = await apiClient.get(`/AzureEnvironment/client/${clientId}`);

            setEnvironments(response.data.filter(env => env.IsActive));
        } catch (error) {
            console.error('[NewAssessmentModal] Error loading environments:', error);
            setEnvironmentError('Failed to load environments for this client');
            setEnvironments([]);
        } finally {
            setLoadingEnvironments(false);
        }
    };

    const handleClientChange = (clientId) => {
        setFormData(prev => ({
            ...prev,
            clientId,
            environmentId: '' // Reset environment when client changes
        }));

        // Clear connection results when client changes
        setConnectionResults({});

        // Load environments for new client
        if (clientId && clientId !== 'internal') {
            loadClientEnvironments(clientId);
        } else {
            setEnvironments([]);
        }
    };

    const handleTestConnection = async (environmentId) => {
        setTestingConnection(environmentId);
        try {
            const result = await azureEnvironmentsApi.testConnection(environmentId);
            setConnectionResults(prev => ({
                ...prev,
                [environmentId]: { success: true, result }
            }));
        } catch (error) {
            console.error('Connection test failed:', error);
            setConnectionResults(prev => ({
                ...prev,
                [environmentId]: {
                    success: false,
                    error: error.response?.data?.message || 'Connection test failed'
                }
            }));
        } finally {
            setTestingConnection(null);
        }
    };

    const getSelectedEnvironment = () => {
        return environments.find(env => env.AzureEnvironmentId === formData.environmentId);
    };

    const handleSubmit = async (e) => {
        e.preventDefault();

        const selectedEnv = getSelectedEnvironment();
        if (!selectedEnv) {
            alert('Please select an environment');
            return;
        }

        try {
            // Build the client-scoped assessment request
            const assessmentRequest = {
                name: formData.name,
                environmentId: formData.environmentId,
                type: formData.type === 'Full' ? 2 : formData.type === 'NamingConvention' ? 0 : 1,
                options: {
                    includeRecommendations: true
                }
            };

            await onStart(assessmentRequest);

            // Reset form
            setFormData({
                name: '',
                clientId: '',
                environmentId: '',
                type: 'Full'
            });
            setEnvironments([]);
            setConnectionResults({});

        } catch (error) {
            console.error('[NewAssessmentModal] Error starting assessment:', error);
            // Error is handled by parent component
        }
    };

    if (!isOpen) return null;

    return createPortal (
        <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-[60]">
            <div className="bg-gray-900 border border-gray-800 rounded w-full max-w-lg mx-4">
                <div className="p-6 border-b border-gray-800">
                    <h2 className="text-xl font-semibold text-white">Start New Assessment</h2>
                    <p className="text-gray-400 text-sm mt-1">Create an assessment for a client environment</p>
                </div>

                <form onSubmit={handleSubmit} className="p-6 space-y-4">
                    {/* Assessment Name */}
                    <div>
                        <label className="block text-sm font-medium text-gray-300 mb-2">Assessment Name</label>
                        <input
                            type="text"
                            value={formData.name}
                            onChange={(e) => setFormData({ ...formData, name: e.target.value })}
                            className="w-full bg-gray-800 border border-gray-700 rounded px-3 py-2 text-white focus:outline-none focus:border-yellow-600"
                            placeholder="Q4 2024 Governance Review"
                            required
                        />
                    </div>

                    {/* Client Selection */}
                    <div>
                        <label className="block text-sm font-medium text-gray-300 mb-2">Select Client</label>
                        <select
                            value={formData.clientId}
                            onChange={(e) => handleClientChange(e.target.value)}
                            className="w-full bg-gray-800 border border-gray-700 rounded px-3 py-2 text-white focus:outline-none focus:border-yellow-600"
                            required
                        >
                            <option value="">Choose a client...</option>
                            {clients.map((client) => (
                                <option key={client.ClientId} value={client.ClientId}>
                                    {client.Name}
                                </option>
                            ))}
                        </select>
                        {clients.length === 0 && (
                            <p className="text-gray-500 text-sm mt-1">
                                No clients available. Add a client first to create assessments.
                            </p>
                        )}
                    </div>

                    {/* Environment Selection */}
                    <div>
                        <label className="block text-sm font-medium text-gray-300 mb-2">Azure Environment</label>

                        {!formData.clientId ? (
                            <div className="w-full bg-gray-800 border border-gray-700 rounded px-3 py-2 text-gray-500">
                                Select a client first
                            </div>
                        ) : loadingEnvironments ? (
                            <div className="w-full bg-gray-800 border border-gray-700 rounded px-3 py-2 text-gray-400 flex items-center space-x-2">
                                <div className="w-4 h-4 border-2 border-yellow-600 border-t-transparent rounded-full animate-spin"></div>
                                <span>Loading environments...</span>
                            </div>
                        ) : environmentError ? (
                            <div className="w-full bg-red-900 border border-red-700 rounded px-3 py-2 text-red-300 flex items-center space-x-2">
                                <AlertCircle size={16} />
                                <span>{environmentError}</span>
                            </div>
                        ) : environments.length === 0 ? (
                            <div className="w-full bg-gray-800 border border-gray-700 rounded px-3 py-2 text-gray-500">
                                No environments configured for this client
                            </div>
                        ) : (
                            <select
                                value={formData.environmentId}
                                onChange={(e) => setFormData({ ...formData, environmentId: e.target.value })}
                                className="w-full bg-gray-800 border border-gray-700 rounded px-3 py-2 text-white focus:outline-none focus:border-yellow-600"
                                required
                            >
                                <option value="">Choose an environment...</option>
                                {environments.map((env) => (
                                    <option key={env.AzureEnvironmentId} value={env.AzureEnvironmentId}>
                                        {env.Name} ({env.SubscriptionIds?.length || 0} subscriptions)
                                    </option>
                                ))}
                            </select>
                        )}

                        {/* Environment Details */}
                        {formData.environmentId && getSelectedEnvironment() && (
                            <div className="mt-2 p-3 bg-gray-800 border border-gray-700 rounded">
                                <div className="flex items-center justify-between mb-3">
                                    <div className="flex items-center space-x-2">
                                        <Cloud size={16} className="text-blue-400" />
                                        <span className="text-sm font-medium text-white">
                                            {getSelectedEnvironment().Name}
                                        </span>
                                    </div>

                                    {/* Test Connection Button */}
                                    <button
                                        type="button"
                                        onClick={() => handleTestConnection(getSelectedEnvironment().AzureEnvironmentId)}
                                        disabled={testingConnection === getSelectedEnvironment().AzureEnvironmentId}
                                        className="px-3 py-1.5 bg-blue-600 hover:bg-blue-700 text-white rounded text-sm font-medium flex items-center space-x-2 disabled:opacity-50 transition-colors"
                                    >
                                        {testingConnection === getSelectedEnvironment().AzureEnvironmentId ? (
                                            <>
                                                <Loader2 size={14} className="animate-spin" />
                                                <span>Testing...</span>
                                            </>
                                        ) : (
                                            <>
                                                <TestTube size={14} />
                                                <span>Test Connection</span>
                                            </>
                                        )}
                                    </button>
                                </div>

                                {/* Connection Test Results */}
                                {connectionResults[getSelectedEnvironment().AzureEnvironmentId] && (
                                    <div className={`mb-3 p-3 rounded border ${connectionResults[getSelectedEnvironment().AzureEnvironmentId].success
                                            ? 'bg-green-900/20 border-green-800'
                                            : 'bg-red-900/20 border-red-800'
                                        }`}>
                                        <div className="flex items-center space-x-2">
                                            {connectionResults[getSelectedEnvironment().AzureEnvironmentId].success ? (
                                                <>
                                                    <CheckCircle size={16} className="text-green-400" />
                                                    <span className="text-green-400 text-sm font-medium">Connection Successful</span>
                                                </>
                                            ) : (
                                                <>
                                                    <AlertTriangle size={16} className="text-red-400" />
                                                    <span className="text-red-400 text-sm font-medium">Connection Failed</span>
                                                </>
                                            )}
                                        </div>
                                        {!connectionResults[getSelectedEnvironment().AzureEnvironmentId].success && (
                                            <p className="text-red-300 text-sm mt-1">
                                                {connectionResults[getSelectedEnvironment().AzureEnvironmentId].error}
                                            </p>
                                        )}
                                    </div>
                                )}

                                <div className="text-xs text-gray-400 space-y-2">
                                    <div className="flex justify-between">
                                        <span className="font-medium text-gray-300">Azure Subscription Count:</span>
                                        <span>{getSelectedEnvironment().SubscriptionIds?.length || 0}</span>
                                    </div>

                                    {getSelectedEnvironment().SubscriptionIds?.length > 0 && (
                                        <div>
                                            <div className="font-medium text-gray-300 mb-1">Azure Subscription IDs:</div>
                                            <div className="bg-gray-900 rounded p-2 max-h-20 overflow-y-auto">
                                                {getSelectedEnvironment().SubscriptionIds.map((subId, index) => (
                                                    <div key={index} className="font-mono text-xs text-gray-400 break-all">
                                                        {subId}
                                                    </div>
                                                ))}
                                            </div>
                                        </div>
                                    )}

                                    <div className="flex justify-between">
                                        <span className="font-medium text-gray-300">Entra ID Tenant:</span>
                                        <span className="font-mono text-xs">{getSelectedEnvironment().TenantId}</span>
                                    </div>

                                    {getSelectedEnvironment().Description && (
                                        <div>
                                            <div className="font-medium text-gray-300 mb-1">Description:</div>
                                            <div className="text-gray-400">{getSelectedEnvironment().Description}</div>
                                        </div>
                                    )}

                                    {/* Connection Status */}
                                    {getSelectedEnvironment().LastConnectionTest !== null && (
                                        <div className="flex justify-between items-center pt-1 border-t border-gray-700">
                                            <span className="font-medium text-gray-300">Last Connection:</span>
                                            <div className="flex items-center space-x-1">
                                                <div className={`w-2 h-2 rounded-full ${getSelectedEnvironment().LastConnectionTest ? 'bg-green-400' : 'bg-red-400'
                                                    }`}></div>
                                                <span className={getSelectedEnvironment().LastConnectionTest ? 'text-green-400' : 'text-red-400'}>
                                                    {getSelectedEnvironment().LastConnectionTest ? 'Success' : 'Failed'}
                                                </span>
                                            </div>
                                        </div>
                                    )}
                                </div>
                            </div>
                        )}
                    </div>

                    {/* Assessment Type */}
                    <div>
                        <label className="block text-sm font-medium text-gray-300 mb-2">Assessment Type</label>
                        <select
                            value={formData.type}
                            onChange={(e) => setFormData({ ...formData, type: e.target.value })}
                            className="w-full bg-gray-800 border border-gray-700 rounded px-3 py-2 text-white focus:outline-none focus:border-yellow-600"
                        >
                            <option value="Full">Full Assessment</option>
                            <option value="NamingConvention">Naming Conventions Only</option>
                            <option value="Tagging">Tagging Compliance Only</option>
                        </select>
                    </div>

                    {/* Action Buttons */}
                    <div className="flex items-center justify-end space-x-3 pt-4">
                        <button
                            type="button"
                            onClick={onClose}
                            className="px-4 py-2 text-gray-300 hover:text-white transition-colors"
                        >
                            Cancel
                        </button>
                        <button
                            type="submit"
                            disabled={!formData.clientId || !formData.environmentId || !formData.name || loadingEnvironments}
                            className="flex items-center space-x-2 bg-yellow-600 hover:bg-yellow-700 disabled:bg-gray-600 disabled:cursor-not-allowed text-black px-4 py-2 rounded font-medium transition-colors"
                        >
                            <Play size={16} />
                            <span>Start Assessment</span>
                        </button>
                    </div>
                </form>
            </div>
        </div>
        ,document.body
    );
};

export default NewAssessmentModal;