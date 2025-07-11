﻿import React, { useState, useEffect } from 'react';
import { createPortal } from 'react-dom';
import {
    X,
    Plus,
    Edit3,
    Trash2,
    Database,
    AlertCircle,
    CheckCircle,
    Loader2,
    Settings,
    Eye,
    TestTube,
    Cloud,
    Shield,
    Link,
    ExternalLink,
    RefreshCw,
    Unlink
} from 'lucide-react';
import { azureEnvironmentsApi, oauthApi } from '../../services/apiService';

const ManageSubscriptionsModal = ({ isOpen, onClose, client }) => {
    const [environments, setEnvironments] = useState([]);
    const [isLoading, setIsLoading] = useState(false);
    const [error, setError] = useState(null);
    const [showAddForm, setShowAddForm] = useState(false);
    const [editingEnvironment, setEditingEnvironment] = useState(null);
    const [testingConnection, setTestingConnection] = useState(null);
    const [connectionResults, setConnectionResults] = useState({});
    const [oauthLoading, setOauthLoading] = useState({});

    // Form state for adding/editing environments
    const [formData, setFormData] = useState({
        name: '',
        description: '',
        tenantId: '',
        subscriptionIds: [''],
        servicePrincipalId: '',
        servicePrincipalName: '',
        useOAuth: false  // OAuth toggle
    });
    const [formErrors, setFormErrors] = useState({});

    // Load environments when modal opens
    useEffect(() => {
        if (isOpen && client) {
            loadEnvironments();
        }
    }, [isOpen, client]);

    // Reset form when modal closes
    useEffect(() => {
        if (!isOpen) {
            setShowAddForm(false);
            setEditingEnvironment(null);
            setError(null);
            resetForm();
        }
    }, [isOpen]);

    const loadEnvironments = async () => {
        if (!client?.ClientId) return;

        setIsLoading(true);
        setError(null);

        try {
            console.log('[ManageSubscriptionsModal] Loading environments for client:', client.ClientId);
            const envs = await azureEnvironmentsApi.getClientEnvironments(client.ClientId);
            console.log('[ManageSubscriptionsModal] Loaded environments with OAuth status:', envs);
            setEnvironments(envs || []);

            // OAuth status is now included in the backend response - no separate API calls needed
        } catch (error) {
            console.error('[ManageSubscriptionsModal] Failed to load environments:', error);
            setError('Failed to load Azure environments');
        } finally {
            setIsLoading(false);
        }
    };

    const resetForm = () => {
        setFormData({
            name: '',
            description: '',
            tenantId: '',
            subscriptionIds: [''],
            servicePrincipalId: '',
            servicePrincipalName: '',
            useOAuth: false
        });
        setFormErrors({});
    };

    const handleInputChange = (e) => {
        const { name, value, type, checked } = e.target;
        setFormData(prev => ({
            ...prev,
            [name]: type === 'checkbox' ? checked : value
        }));

        // Clear error for this field
        if (formErrors[name]) {
            setFormErrors(prev => ({
                ...prev,
                [name]: null
            }));
        }
    };

    const handleSubscriptionIdsChange = (index, value) => {
        const newSubscriptionIds = [...formData.subscriptionIds];
        newSubscriptionIds[index] = value;
        setFormData(prev => ({
            ...prev,
            subscriptionIds: newSubscriptionIds
        }));
    };

    const addSubscriptionIdField = () => {
        setFormData(prev => ({
            ...prev,
            subscriptionIds: [...prev.subscriptionIds, '']
        }));
    };

    const removeSubscriptionIdField = (index) => {
        if (formData.subscriptionIds.length > 1) {
            const newSubscriptionIds = formData.subscriptionIds.filter((_, i) => i !== index);
            setFormData(prev => ({
                ...prev,
                subscriptionIds: newSubscriptionIds
            }));
        }
    };

    const validateForm = () => {
        const errors = {};

        if (!formData.name.trim()) {
            errors.name = 'Environment name is required';
        }

        if (!formData.tenantId.trim()) {
            errors.tenantId = 'Tenant ID is required';
        }

        const validSubscriptionIds = formData.subscriptionIds.filter(id => id.trim());
        if (validSubscriptionIds.length === 0) {
            errors.subscriptionIds = 'At least one subscription ID is required';
        }

        // If not using OAuth, validate Service Principal fields
        if (!formData.useOAuth) {
            if (!formData.servicePrincipalId.trim()) {
                errors.servicePrincipalId = 'Service Principal ID is required when not using OAuth';
            }
        }

        setFormErrors(errors);
        return Object.keys(errors).length === 0;
    };

    const handleSubmit = async (e) => {
        e.preventDefault();

        console.log('[ManageSubscriptionsModal] Form submitted with data:', formData);

        if (!validateForm()) {
            console.log('[ManageSubscriptionsModal] Form validation failed:', formErrors);
            return;
        }

        setIsLoading(true);
        setError(null);

        try {
            const environmentData = {
                clientId: client.ClientId,
                name: formData.name,
                description: formData.description,
                tenantId: formData.tenantId,
                subscriptionIds: formData.subscriptionIds.filter(id => id.trim()),
                servicePrincipalId: formData.servicePrincipalId,
                servicePrincipalName: formData.servicePrincipalName
            };

            console.log('[ManageSubscriptionsModal] Creating environment with data:', environmentData);

            let createdEnvironment;

            if (editingEnvironment) {
                const envId = editingEnvironment.EnvironmentId || editingEnvironment.Id || editingEnvironment.AzureEnvironmentId;
                createdEnvironment = await azureEnvironmentsApi.updateEnvironment(envId, environmentData);
                console.log('[ManageSubscriptionsModal] Environment updated:', createdEnvironment);
            } else {
                createdEnvironment = await azureEnvironmentsApi.createEnvironment(environmentData);
                console.log('[ManageSubscriptionsModal] Environment created:', createdEnvironment);
            }

            // If user selected OAuth, initiate OAuth flow after environment creation
            if (formData.useOAuth && !editingEnvironment) {
                console.log('[ManageSubscriptionsModal] Initiating OAuth setup for environment:', createdEnvironment);
                try {
                    await handleOAuthSetup(createdEnvironment.AzureEnvironmentId || createdEnvironment.environmentId, formData.name);
                } catch (oauthError) {
                    console.error('[ManageSubscriptionsModal] OAuth setup failed:', oauthError);
                    setError('Environment created but OAuth setup failed. You can set up OAuth later.');
                }
            }

            await loadEnvironments();
            setShowAddForm(false);
            setEditingEnvironment(null);
            resetForm();
        } catch (error) {
            console.error('[ManageSubscriptionsModal] Failed to save environment:', error);
            setError(
                error.response?.data?.message ||
                error.response?.data?.error ||
                'Failed to save environment'
            );
        } finally {
            setIsLoading(false);
        }
    };

    const handleOAuthSetup = async (environmentId, environmentName) => {
        console.log('[ManageSubscriptionsModal] Starting OAuth setup for environment:', environmentId, environmentName);
        setOauthLoading(prev => ({ ...prev, [environmentId]: true }));

        try {
            console.log('[ManageSubscriptionsModal] Calling oauthApi.initiateOAuth with:', {
                clientId: client.ClientId,
                clientName: client.Name,
                description: `OAuth setup for ${environmentName} environment`
            });

            const response = await oauthApi.initiateOAuth(
                client.ClientId,
                client.Name,
                `OAuth setup for ${environmentName} environment`
            );

            console.log('[ManageSubscriptionsModal] OAuth initiate response:', response);

            // Check if we have a valid authorization URL
            if (!response.authorizationUrl || !response.authorizationUrl.startsWith('http')) {
                console.error('[ManageSubscriptionsModal] Invalid authorization URL received:', response.authorizationUrl);
                setError('Invalid OAuth authorization URL received from server');
                return;
            }

            console.log('[ManageSubscriptionsModal] Opening OAuth window with URL:', response.authorizationUrl);

            // Open OAuth authorization URL in a new tab
            const authWindow = window.open(
                response.authorizationUrl,
                'oauth_authorization',
                'width=600,height=700,scrollbars=yes,resizable=yes'
            );

            if (!authWindow) {
                console.error('[ManageSubscriptionsModal] Failed to open OAuth popup window - popup blocked?');
                setError('Failed to open OAuth popup. Please check your popup blocker settings.');
                return;
            }

            console.log('[ManageSubscriptionsModal] OAuth window opened successfully');

            // Monitor the OAuth window
            const checkClosed = setInterval(() => {
                try {
                    if (authWindow.closed) {
                        console.log('[ManageSubscriptionsModal] OAuth window closed, refreshing environments');
                        clearInterval(checkClosed);
                        // Refresh environments to get updated OAuth status from backend
                        setTimeout(() => {
                            loadEnvironments();
                        }, 2000);
                    }
                } catch (error) {
                    console.error('[ManageSubscriptionsModal] Error checking OAuth window status:', error);
                    clearInterval(checkClosed);
                }
            }, 1000);

        } catch (error) {
            console.error('[ManageSubscriptionsModal] Failed to initiate OAuth:', error);
            setError(`Failed to start OAuth setup: ${error.message || 'Unknown error'}`);
        } finally {
            setOauthLoading(prev => ({ ...prev, [environmentId]: false }));
        }
    };

    const handleEdit = (environment) => {
        setEditingEnvironment(environment);
        setFormData({
            name: environment.Name || '',
            description: environment.Description || '',
            tenantId: environment.TenantId || '',
            subscriptionIds: environment.SubscriptionIds?.length ? environment.SubscriptionIds : [''],
            servicePrincipalId: environment.ServicePrincipalId || '',
            servicePrincipalName: environment.ServicePrincipalName || '',
            useOAuth: false // Don't auto-enable OAuth in edit mode
        });
        setShowAddForm(true);
    };

    const handleDelete = async (environmentId) => {
        if (!window.confirm('Are you sure you want to delete this environment? This action cannot be undone.')) {
            return;
        }

        setIsLoading(true);
        try {
            await azureEnvironmentsApi.deleteEnvironment(environmentId);
            await loadEnvironments();
        } catch (error) {
            console.error('[ManageSubscriptionsModal] Failed to delete environment:', error);
            setError('Failed to delete environment');
        } finally {
            setIsLoading(false);
        }
    };

    const handleTestConnection = async (environmentId) => {
        if (!environmentId) {
            setError('Invalid environment ID for connection test');
            return;
        }

        console.log('[ManageSubscriptionsModal] Testing connection for environment:', environmentId);
        setTestingConnection(environmentId);
        try {
            const result = await azureEnvironmentsApi.testConnection(environmentId);
            console.log('[ManageSubscriptionsModal] Connection test result:', result);

            // Store the test result for display
            setConnectionResults(prev => ({
                ...prev,
                [environmentId]: {
                    success: result.Success,
                    message: result.Message,
                    details: result.Details
                }
            }));

        } catch (error) {
            console.error('[ManageSubscriptionsModal] Connection test failed:', error);
            setConnectionResults(prev => ({
                ...prev,
                [environmentId]: {
                    success: false,
                    message: error.response?.data?.message || 'Connection test failed',
                    error: error.response?.data
                }
            }));
        } finally {
            setTestingConnection(null);
        }
    };

    const handleRevokeOAuth = async (environmentId) => {
        if (!window.confirm('Are you sure you want to revoke OAuth credentials? The environment will fall back to default credentials.')) {
            return;
        }

        console.log('[ManageSubscriptionsModal] Revoking OAuth for environment:', environmentId);
        setOauthLoading(prev => ({ ...prev, [environmentId]: true }));

        try {
            await oauthApi.revokeOAuthCredentials(environmentId);
            console.log('[ManageSubscriptionsModal] OAuth revoked successfully');

            // Refresh environments to get updated OAuth status from backend
            await loadEnvironments();

        } catch (error) {
            console.error('[ManageSubscriptionsModal] Failed to revoke OAuth:', error);
            setError('Failed to revoke OAuth credentials');
        } finally {
            setOauthLoading(prev => ({ ...prev, [environmentId]: false }));
        }
    };

    const cancelEdit = () => {
        setShowAddForm(false);
        setEditingEnvironment(null);
        resetForm();
    };

    if (!isOpen || !client) return null;

    return createPortal(
        <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center p-4 z-[60]">
            <div className="bg-gray-800 rounded-lg shadow-xl w-full max-w-5xl max-h-[90vh] overflow-y-auto">
                {/* Header */}
                <div className="flex items-center justify-between p-6 border-b border-gray-700">
                    <div className="flex items-center space-x-3">
                        <div className="w-10 h-10 bg-blue-600 rounded-lg flex items-center justify-center">
                            <Cloud size={20} className="text-white" />
                        </div>
                        <div>
                            <h2 className="text-lg font-semibold text-white">Manage Azure Environments</h2>
                            <p className="text-sm text-gray-400">{client.Name}</p>
                        </div>
                    </div>
                    <button
                        onClick={onClose}
                        className="text-gray-400 hover:text-white p-2 rounded-lg hover:bg-gray-700"
                    >
                        <X size={20} />
                    </button>
                </div>

                {/* Error Display */}
                {error && (
                    <div className="m-6 mb-0 bg-red-900/20 border border-red-800 rounded p-4">
                        <div className="flex items-center space-x-3">
                            <AlertCircle className="text-red-400" size={20} />
                            <div>
                                <h4 className="text-red-400 font-medium">Error</h4>
                                <p className="text-red-300 text-sm mt-1">{error}</p>
                            </div>
                        </div>
                    </div>
                )}

                <div className="p-6">
                    {/* Add Environment Button */}
                    {!showAddForm && (
                        <div className="flex items-center justify-between mb-6">
                            <div>
                                <h3 className="text-lg font-medium text-white">Azure Environments</h3>
                                <p className="text-sm text-gray-400">
                                    Manage Azure subscriptions and environments for {client.Name}
                                </p>
                            </div>
                            <button
                                onClick={() => setShowAddForm(true)}
                                className="px-4 py-2 bg-yellow-600 hover:bg-yellow-700 text-black rounded font-medium flex items-center space-x-2"
                            >
                                <Plus size={16} />
                                <span>Add Environment</span>
                            </button>
                        </div>
                    )}

                    {/* Add/Edit Form */}
                    {showAddForm && (
                        <div className="bg-gray-900 border border-gray-700 rounded-lg p-6 mb-6">
                            <div className="flex items-center justify-between mb-4">
                                <h4 className="text-lg font-medium text-white">
                                    {editingEnvironment ? 'Edit Environment' : 'Add New Environment'}
                                </h4>
                                <button
                                    onClick={cancelEdit}
                                    className="text-gray-400 hover:text-white"
                                >
                                    <X size={20} />
                                </button>
                            </div>

                            <form onSubmit={handleSubmit} className="space-y-4">
                                {/* OAuth vs Manual Setup Toggle */}
                                {!editingEnvironment && (
                                    <div className="bg-gray-800 border border-gray-600 rounded-lg p-4">
                                        <div className="flex items-center space-x-3 mb-3">
                                            <Shield size={20} className="text-yellow-400" />
                                            <h5 className="text-white font-medium">Connection Method</h5>
                                        </div>
                                        <div className="flex items-center space-x-4">
                                            <label className="flex items-center space-x-3 cursor-pointer">
                                                <input
                                                    type="radio"
                                                    name="connectionMethod"
                                                    checked={!formData.useOAuth}
                                                    onChange={() => setFormData(prev => ({ ...prev, useOAuth: false }))}
                                                    className="w-4 h-4 text-yellow-600 bg-gray-700 border-gray-600 focus:ring-yellow-600"
                                                />
                                                <span className="text-gray-300">Manual Service Principal Setup</span>
                                            </label>
                                            <label className="flex items-center space-x-3 cursor-pointer">
                                                <input
                                                    type="radio"
                                                    name="connectionMethod"
                                                    checked={formData.useOAuth}
                                                    onChange={() => setFormData(prev => ({ ...prev, useOAuth: true }))}
                                                    className="w-4 h-4 text-yellow-600 bg-gray-700 border-gray-600 focus:ring-yellow-600"
                                                />
                                                <span className="text-yellow-400 font-medium">OAuth Delegation (Recommended)</span>
                                            </label>
                                        </div>
                                        {formData.useOAuth && (
                                            <div className="mt-3 p-3 bg-yellow-900/20 border border-yellow-800 rounded">
                                                <p className="text-yellow-300 text-sm">
                                                    🚀 <strong>OAuth Delegation:</strong> Connect securely using Microsoft's OAuth flow.<br />
                                                    <strong>Note:</strong> You'll need administrative access to your client's Microsoft Entra ID (Azure AD) tenant to grant permissions.
                                                </p>
                                            </div>
                                        )}
                                    </div>
                                )}

                                <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                                    {/* Environment Name */}
                                    <div>
                                        <label className="block text-sm font-medium text-gray-300 mb-2">
                                            Environment Name *
                                        </label>
                                        <input
                                            type="text"
                                            name="name"
                                            value={formData.name}
                                            onChange={handleInputChange}
                                            className={`w-full px-3 py-2 bg-gray-700 border rounded text-white placeholder-gray-400 focus:outline-none focus:ring-2 focus:ring-yellow-600 ${formErrors.name ? 'border-red-500' : 'border-gray-600'
                                                }`}
                                            placeholder="Production, Development, etc."
                                        />
                                        {formErrors.name && (
                                            <p className="text-red-400 text-sm mt-1">{formErrors.name}</p>
                                        )}
                                    </div>

                                    {/* Tenant ID */}
                                    <div>
                                        <label className="block text-sm font-medium text-gray-300 mb-2">
                                            Microsoft Entra ID Tenant ID *
                                        </label>
                                        <input
                                            type="text"
                                            name="tenantId"
                                            value={formData.tenantId}
                                            onChange={handleInputChange}
                                            className={`w-full px-3 py-2 bg-gray-700 border rounded text-white placeholder-gray-400 focus:outline-none focus:ring-2 focus:ring-yellow-600 ${formErrors.tenantId ? 'border-red-500' : 'border-gray-600'
                                                }`}
                                            placeholder="xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"
                                        />
                                        {formErrors.tenantId && (
                                            <p className="text-red-400 text-sm mt-1">{formErrors.tenantId}</p>
                                        )}
                                        <p className="text-xs text-gray-400 mt-1">
                                            Found in Azure Portal → Microsoft Entra ID → Overview → Tenant ID
                                        </p>
                                    </div>
                                </div>

                                {/* Description */}
                                <div>
                                    <label className="block text-sm font-medium text-gray-300 mb-2">
                                        Description
                                    </label>
                                    <textarea
                                        name="description"
                                        value={formData.description}
                                        onChange={handleInputChange}
                                        rows={2}
                                        className="w-full px-3 py-2 bg-gray-700 border border-gray-600 rounded text-white placeholder-gray-400 focus:outline-none focus:ring-2 focus:ring-yellow-600"
                                        placeholder="Brief description of this environment"
                                    />
                                </div>

                                {/* Subscription IDs */}
                                <div>
                                    <label className="block text-sm font-medium text-gray-300 mb-2">
                                        Azure Subscription IDs *
                                    </label>
                                    {formData.subscriptionIds.map((subscriptionId, index) => (
                                        <div key={index} className="flex items-center space-x-2 mb-2">
                                            <input
                                                type="text"
                                                value={subscriptionId}
                                                onChange={(e) => handleSubscriptionIdsChange(index, e.target.value)}
                                                className="flex-1 px-3 py-2 bg-gray-700 border border-gray-600 rounded text-white placeholder-gray-400 focus:outline-none focus:ring-2 focus:ring-yellow-600"
                                                placeholder="xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"
                                            />
                                            {formData.subscriptionIds.length > 1 && (
                                                <button
                                                    type="button"
                                                    onClick={() => removeSubscriptionIdField(index)}
                                                    className="p-2 text-red-400 hover:text-red-300 hover:bg-gray-700 rounded"
                                                >
                                                    <Trash2 size={16} />
                                                </button>
                                            )}
                                        </div>
                                    ))}
                                    <button
                                        type="button"
                                        onClick={addSubscriptionIdField}
                                        className="text-sm text-yellow-600 hover:text-yellow-500 flex items-center space-x-1"
                                    >
                                        <Plus size={14} />
                                        <span>Add Another Subscription</span>
                                    </button>
                                    {formErrors.subscriptionIds && (
                                        <p className="text-red-400 text-sm mt-1">{formErrors.subscriptionIds}</p>
                                    )}
                                    <p className="text-xs text-gray-400 mt-1">
                                        Found in Azure Portal → Subscriptions → Overview → Subscription ID
                                    </p>
                                </div>

                                {/* Service Principal Information - Only show if not using OAuth */}
                                {!formData.useOAuth && (
                                    <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                                        <div>
                                            <label className="block text-sm font-medium text-gray-300 mb-2">
                                                Service Principal ID *
                                            </label>
                                            <input
                                                type="text"
                                                name="servicePrincipalId"
                                                value={formData.servicePrincipalId}
                                                onChange={handleInputChange}
                                                className={`w-full px-3 py-2 bg-gray-700 border rounded text-white placeholder-gray-400 focus:outline-none focus:ring-2 focus:ring-yellow-600 ${formErrors.servicePrincipalId ? 'border-red-500' : 'border-gray-600'
                                                    }`}
                                                placeholder="Service Principal Application ID"
                                            />
                                            {formErrors.servicePrincipalId && (
                                                <p className="text-red-400 text-sm mt-1">{formErrors.servicePrincipalId}</p>
                                            )}
                                        </div>

                                        <div>
                                            <label className="block text-sm font-medium text-gray-300 mb-2">
                                                Service Principal Name
                                            </label>
                                            <input
                                                type="text"
                                                name="servicePrincipalName"
                                                value={formData.servicePrincipalName}
                                                onChange={handleInputChange}
                                                className="w-full px-3 py-2 bg-gray-700 border border-gray-600 rounded text-white placeholder-gray-400 focus:outline-none focus:ring-2 focus:ring-yellow-600"
                                                placeholder="Service Principal Display Name"
                                            />
                                        </div>
                                    </div>
                                )}

                                {/* Form Actions */}
                                <div className="flex items-center justify-end space-x-3 pt-4 border-t border-gray-700">
                                    <button
                                        type="button"
                                        onClick={cancelEdit}
                                        className="px-4 py-2 text-gray-400 hover:text-white"
                                        disabled={isLoading}
                                    >
                                        Cancel
                                    </button>
                                    <button
                                        type="submit"
                                        disabled={isLoading}
                                        className="px-6 py-2 bg-yellow-600 hover:bg-yellow-700 text-black rounded font-medium flex items-center space-x-2 disabled:opacity-50"
                                    >
                                        {isLoading ? (
                                            <>
                                                <Loader2 size={16} className="animate-spin" />
                                                <span>Saving...</span>
                                            </>
                                        ) : (
                                            <>
                                                <CheckCircle size={16} />
                                                <span>
                                                    {editingEnvironment ? 'Update' : formData.useOAuth ? 'Create & Setup OAuth' : 'Create'} Environment
                                                </span>
                                            </>
                                        )}
                                    </button>
                                </div>
                            </form>
                        </div>
                    )}

                    {/* Environments List */}
                    {isLoading && !showAddForm ? (
                        <div className="text-center py-8">
                            <div className="w-8 h-8 border-2 border-yellow-600 border-t-transparent rounded-full animate-spin mx-auto mb-4"></div>
                            <p className="text-gray-400">Loading environments...</p>
                        </div>
                    ) : environments.length === 0 && !showAddForm ? (
                        <div className="text-center py-12">
                            <div className="w-16 h-16 bg-gray-600 rounded-full flex items-center justify-center mx-auto mb-4">
                                <Database size={32} className="text-gray-400" />
                            </div>
                            <h3 className="text-lg font-medium text-white mb-2">No Environments</h3>
                            <p className="text-gray-400 mb-4">
                                This client doesn't have any Azure environments configured yet.
                            </p>
                            <button
                                onClick={() => setShowAddForm(true)}
                                className="px-4 py-2 bg-yellow-600 hover:bg-yellow-700 text-black rounded font-medium flex items-center space-x-2 mx-auto"
                            >
                                <Plus size={16} />
                                <span>Add First Environment</span>
                            </button>
                        </div>
                    ) : !showAddForm ? (
                        <div className="space-y-4">
                            {environments.map((environment) => {
                                const envId = environment.EnvironmentId || environment.Id || environment.AzureEnvironmentId;

                                // Use OAuth status directly from backend response
                                const hasOAuth = environment.HasOAuthCredentials || false;
                                const connectionMethod = environment.ConnectionMethod || 'DefaultCredentials';

                                return (
                                    <div key={envId} className="bg-gray-900 border border-gray-700 rounded-lg p-4">
                                        <div className="flex items-start justify-between">
                                            <div className="flex-1">
                                                <div className="flex items-center space-x-3 mb-2">
                                                    <h4 className="text-lg font-medium text-white">{environment.Name}</h4>

                                                    {/* Connection Method Badge */}
                                                    <div className={`px-2 py-1 rounded text-xs font-medium flex items-center space-x-1 ${hasOAuth
                                                        ? 'bg-yellow-900/30 text-yellow-400 border border-yellow-800'
                                                        : 'bg-blue-900/30 text-blue-400 border border-blue-800'
                                                        }`}>
                                                        {hasOAuth ? (
                                                            <>
                                                                <Shield size={12} />
                                                                <span>OAuth</span>
                                                            </>
                                                        ) : (
                                                            <>
                                                                <Settings size={12} />
                                                                <span>Service Principal</span>
                                                            </>
                                                        )}
                                                    </div>

                                                    <div className={`px-2 py-1 rounded text-xs font-medium ${environment.IsActive
                                                        ? 'bg-green-900/30 text-green-400 border border-green-800'
                                                        : 'bg-gray-900/30 text-gray-400 border border-gray-700'
                                                        }`}>
                                                        {environment.IsActive ? 'Active' : 'Inactive'}
                                                    </div>
                                                </div>

                                                {environment.Description && (
                                                    <p className="text-sm text-gray-400 mb-3">{environment.Description}</p>
                                                )}

                                                {/* Connection Test Results */}
                                                {connectionResults[envId] && (
                                                    <div className={`mb-3 p-3 rounded border ${connectionResults[envId].success
                                                        ? 'bg-green-900/20 border-green-800'
                                                        : 'bg-red-900/20 border-red-800'
                                                        }`}>
                                                        <div className="flex items-center space-x-2">
                                                            {connectionResults[envId].success ? (
                                                                <>
                                                                    <CheckCircle size={16} className="text-green-400" />
                                                                    <span className="text-green-400 text-sm font-medium">Connection Test Successful</span>
                                                                </>
                                                            ) : (
                                                                <>
                                                                    <AlertCircle size={16} className="text-red-400" />
                                                                    <span className="text-red-400 text-sm font-medium">Connection Test Failed</span>
                                                                </>
                                                            )}
                                                        </div>
                                                        <p className="text-sm mt-1 text-gray-300">
                                                            {connectionResults[envId].message}
                                                        </p>
                                                        {connectionResults[envId].details && (
                                                            <p className="text-xs mt-1 text-gray-400">
                                                                Tested {connectionResults[envId].details.SubscriptionCount} subscription(s)
                                                                using {connectionResults[envId].details.ConnectionMethod} at {new Date(connectionResults[envId].details.TestedAt).toLocaleString()}
                                                            </p>
                                                        )}
                                                    </div>
                                                )}

                                                <div className="grid grid-cols-1 md:grid-cols-2 gap-4 text-sm">
                                                    <div>
                                                        <span className="text-gray-400">Tenant ID:</span>
                                                        <p className="text-gray-300 font-mono text-xs mt-1">{environment.TenantId}</p>
                                                    </div>

                                                    <div>
                                                        <span className="text-gray-400">Subscriptions:</span>
                                                        <p className="text-gray-300 mt-1">
                                                            {environment.SubscriptionIds?.length || 0} subscription(s)
                                                        </p>
                                                    </div>

                                                    {environment.ServicePrincipalName && (
                                                        <div>
                                                            <span className="text-gray-400">Service Principal:</span>
                                                            <p className="text-gray-300 mt-1">{environment.ServicePrincipalName}</p>
                                                        </div>
                                                    )}

                                                    <div>
                                                        <span className="text-gray-400">Created:</span>
                                                        <p className="text-gray-300 mt-1">
                                                            {new Date(environment.CreatedDate).toLocaleDateString()}
                                                        </p>
                                                    </div>
                                                </div>
                                            </div>

                                            {/* Environment Actions */}
                                            <div className="flex items-center space-x-2 ml-4">
                                                {/* OAuth Setup/Management Button */}
                                                {!hasOAuth ? (
                                                    <button
                                                        onClick={() => handleOAuthSetup(envId, environment.Name)}
                                                        disabled={oauthLoading[envId]}
                                                        className="p-2 text-yellow-400 hover:text-yellow-300 hover:bg-gray-700 rounded transition-colors disabled:opacity-50"
                                                        title="Setup OAuth Delegation"
                                                    >
                                                        {oauthLoading[envId] ? (
                                                            <Loader2 size={16} className="animate-spin" />
                                                        ) : (
                                                            <Link size={16} />
                                                        )}
                                                    </button>
                                                ) : (
                                                    <button
                                                        onClick={() => handleRevokeOAuth(envId)}
                                                        disabled={oauthLoading[envId]}
                                                        className="p-2 text-orange-400 hover:text-orange-300 hover:bg-gray-700 rounded transition-colors disabled:opacity-50"
                                                        title="Revoke OAuth Credentials"
                                                    >
                                                        {oauthLoading[envId] ? (
                                                            <Loader2 size={16} className="animate-spin" />
                                                        ) : (
                                                            <Unlink size={16} />
                                                        )}
                                                    </button>
                                                )}

                                                <button
                                                    onClick={() => handleTestConnection(envId)}
                                                    disabled={testingConnection === envId}
                                                    className="p-2 text-gray-400 hover:text-white hover:bg-gray-700 rounded transition-colors disabled:opacity-50"
                                                    title="Test Connection"
                                                >
                                                    {testingConnection === envId ? (
                                                        <Loader2 size={16} className="animate-spin" />
                                                    ) : (
                                                        <TestTube size={16} />
                                                    )}
                                                </button>

                                                <button
                                                    onClick={() => handleEdit(environment)}
                                                    className="p-2 text-gray-400 hover:text-white hover:bg-gray-700 rounded transition-colors"
                                                    title="Edit Environment"
                                                >
                                                    <Edit3 size={16} />
                                                </button>

                                                <button
                                                    onClick={() => handleDelete(envId)}
                                                    className="p-2 text-gray-400 hover:text-red-400 hover:bg-gray-700 rounded transition-colors"
                                                    title="Delete Environment"
                                                >
                                                    <Trash2 size={16} />
                                                </button>
                                            </div>
                                        </div>

                                        {/* Subscription IDs Details */}
                                        {environment.SubscriptionIds && environment.SubscriptionIds.length > 0 && (
                                            <div className="mt-4 pt-4 border-t border-gray-700">
                                                <span className="text-sm text-gray-400 mb-2 block">Subscription IDs:</span>
                                                <div className="grid grid-cols-1 lg:grid-cols-2 gap-2">
                                                    {environment.SubscriptionIds.map((subId, index) => (
                                                        <div key={index} className="bg-gray-800 rounded px-3 py-2">
                                                            <code className="text-xs text-gray-300">{subId}</code>
                                                        </div>
                                                    ))}
                                                </div>
                                            </div>
                                        )}
                                    </div>
                                );
                            })}
                        </div>
                    ) : null}
                </div>
            </div>
        </div>
        , document.body
    );
};

export default ManageSubscriptionsModal;