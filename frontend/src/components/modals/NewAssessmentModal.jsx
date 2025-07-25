﻿import React, { useState, useEffect } from 'react';
import { createPortal } from 'react-dom';
import { X, AlertCircle, CheckCircle, Info } from 'lucide-react';
import { assessmentApi, azureEnvironmentsApi, apiClient } from '../../services/apiService';

const NewAssessmentModal = ({ isOpen, onClose, onAssessmentCreated = () => { }, selectedClient = null }) => {
    const [step, setStep] = useState(1);
    const [assessmentName, setAssessmentName] = useState('');
    const [assessmentType, setAssessmentType] = useState(2); // Default to Full assessment
    const [selectedEnvironment, setSelectedEnvironment] = useState('');
    const [currentClient, setCurrentClient] = useState(selectedClient);
    const [useClientPreferences, setUseClientPreferences] = useState(false);
    const [environments, setEnvironments] = useState([]);
    const [clients, setClients] = useState([]);
    const [loading, setLoading] = useState(false);
    const [error, setError] = useState('');
    const [isCreating, setIsCreating] = useState(false);

    const assessmentTypes = [
        {
            id: 0,
            name: 'Naming Convention Only',
            description: 'Analyze resource naming patterns and consistency',
            icon: '📝',
            estimatedTime: '2-3 minutes'
        },
        {
            id: 1,
            name: 'Tagging Compliance Only',
            description: 'Evaluate resource tagging coverage and quality',
            icon: '🏷️',
            estimatedTime: '2-3 minutes'
        },
        {
            id: 2,
            name: 'Governance: Full Assessment',
            description: 'Complete naming and tagging analysis with recommendations',
            icon: '🔍',
            estimatedTime: '3-5 minutes',
            recommended: true
        }
    ];

    useEffect(() => {
        if (isOpen) {
            loadInitialData();
        }
    }, [isOpen]);

    useEffect(() => {
        if (selectedClient) {
            setCurrentClient(selectedClient);
            loadEnvironmentsForClient(selectedClient.clientId);
        }
    }, [selectedClient]);

    const loadInitialData = async () => {
        try {
            setLoading(true);

            // Load accessible clients using the assessments API
            const clientsResponse = await apiClient.get('/assessments/clients');
            setClients(clientsResponse.data || []);

            // If no client is pre-selected, load all environments
            // Skip loading environments initially - they'll be loaded when client is selected
            if (!selectedClient) {
                setEnvironments([]);
            }

        } catch (error) {
            console.error('Failed to load initial data:', error);
            setError('Failed to load assessment data. Please try again.');
        } finally {
            setLoading(false);
        }
    };

    const loadEnvironmentsForClient = async (clientId) => {
        try {
            console.log('[NewAssessmentModal] Loading environments for client:', clientId);
            // Use the correct API endpoint format
            const response = await apiClient.get(`/AzureEnvironment/client/${clientId}`);
            console.log('[NewAssessmentModal] Environments response:', response);
            console.log('[NewAssessmentModal] Environments data:', response.data);

            const envData = response.data || [];
            console.log('[NewAssessmentModal] Setting environments to:', envData);
            setEnvironments(envData);

            // Force a re-render check
            setTimeout(() => {
                console.log('[NewAssessmentModal] Environments state after update:', envData);
            }, 100);

        } catch (error) {
            console.error('[NewAssessmentModal] Failed to load client environments:', error);
            console.error('[NewAssessmentModal] Error details:', error.response?.data);
            setError('Failed to load client environments.');
            setEnvironments([]);
        }
    };

    const handleClientChange = (clientId) => {
        console.log('[NewAssessmentModal] Client changed to:', clientId);
        const client = clients.find(c => c.ClientId === clientId);
        console.log('[NewAssessmentModal] Found client:', client);
        setCurrentClient(client);
        setSelectedEnvironment('');
        setUseClientPreferences(false);

        if (client) {
            loadEnvironmentsForClient(client.ClientId);
        } else {
            // If no client selected, clear environments
            setEnvironments([]);
        }
    };

    const handleNext = () => {
        if (step === 1) {
            if (!assessmentName.trim()) {
                setError('Please enter an assessment name.');
                return;
            }
            if (!currentClient) {
                setError('Please select a client.');
                return;
            }
            setError('');
            setStep(2);
        } else if (step === 2) {
            if (!selectedEnvironment) {
                setError('Please select an Azure environment.');
                return;
            }
            setError('');
            setStep(3);
        }
    };

    const handleBack = () => {
        if (step > 1) {
            setStep(step - 1);
            setError('');
        }
    };

    const handleSubmit = async () => {
        try {
            setIsCreating(true);
            setError('');

            const assessmentData = {
                environmentId: selectedEnvironment,
                name: assessmentName,
                type: assessmentType,
                useClientPreferences: useClientPreferences
            };

            console.log('[NewAssessmentModal] Submitting assessment:', assessmentData);
            const response = await assessmentApi.startAssessment(assessmentData);
            console.log('[NewAssessmentModal] Assessment response:', response);

            if (response && (response.assessmentId || response.AssessmentId)) {
                console.log('[NewAssessmentModal] Assessment created successfully:', response.assessmentId || response.AssessmentId);

                // Call the callback if provided
                if (typeof onAssessmentCreated === 'function') {
                    onAssessmentCreated(response);
                } else {
                    console.warn('[NewAssessmentModal] onAssessmentCreated callback not provided or not a function');
                }

                handleClose();
            } else {
                console.error('[NewAssessmentModal] Invalid response format:', response);
                setError('Assessment created but response format was unexpected. Please check the assessments list.');
            }
        } catch (error) {
            console.error('[NewAssessmentModal] Failed to create assessment:', error);
            console.error('[NewAssessmentModal] Error response:', error.response?.data);

            if (error.response?.status === 402) {
                setError('Assessment limit reached. Please upgrade your subscription or contact support.');
            } else if (error.response?.data?.error) {
                setError(error.response.data.error);
            } else if (error.response?.data?.message) {
                setError(error.response.data.message);
            } else if (error.message) {
                setError(error.message);
            } else {
                setError('Failed to create assessment. Please try again.');
            }
        } finally {
            setIsCreating(false);
        }
    };

    const handleClose = () => {
        setStep(1);
        setAssessmentName('');
        setAssessmentType(2);
        setSelectedEnvironment('');
        setCurrentClient(selectedClient);
        setUseClientPreferences(false);
        setError('');
        setIsCreating(false);
        onClose();
    };

    const getSelectedEnvironmentDetails = () => {
        return environments.find(env =>
            (env.AzureEnvironmentId || env.azureEnvironmentId) === selectedEnvironment
        );
    };

    const getSelectedAssessmentType = () => {
        return assessmentTypes.find(type => type.id === assessmentType);
    };

    const canUseClientPreferences = () => {
        return currentClient && currentClient.ClientId && selectedEnvironment;
    };

    if (!isOpen) return null;

    return createPortal(
        <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center p-4 z-50">
            <div className="bg-gray-800 rounded-lg shadow-xl w-full max-w-2xl max-h-[90vh] overflow-y-auto">
                {/* Header */}
                <div className="flex items-center justify-between p-6 border-b border-gray-700">
                    <h2 className="text-xl font-semibold text-white">Create New Assessment</h2>
                    <button
                        onClick={handleClose}
                        className="text-gray-400 hover:text-white transition-colors"
                    >
                        <X size={24} />
                    </button>
                </div>

                {/* Progress Steps */}
                <div className="px-6 py-4 border-b border-gray-700">
                    <div className="flex items-center justify-between">
                        {[1, 2, 3].map((stepNumber) => (
                            <div key={stepNumber} className="flex items-center">
                                <div className={`w-8 h-8 rounded-full flex items-center justify-center text-sm font-medium ${step >= stepNumber
                                        ? 'bg-yellow-500 text-black'
                                        : 'bg-gray-600 text-gray-300'
                                    }`}>
                                    {stepNumber}
                                </div>
                                <div className={`ml-2 text-sm ${step >= stepNumber ? 'text-white' : 'text-gray-400'
                                    }`}>
                                    {stepNumber === 1 ? 'Details' : stepNumber === 2 ? 'Environment' : 'Review'}
                                </div>
                                {stepNumber < 3 && (
                                    <div className={`w-16 h-0.5 ml-4 ${step > stepNumber ? 'bg-yellow-500' : 'bg-gray-600'
                                        }`} />
                                )}
                            </div>
                        ))}
                    </div>
                </div>

                {/* Content */}
                <div className="p-6">
                    {error && (
                        <div className="mb-4 p-3 bg-red-500/10 border border-red-500/20 rounded-lg flex items-center">
                            <AlertCircle className="text-red-400 mr-2" size={16} />
                            <span className="text-red-400 text-sm">{error}</span>
                        </div>
                    )}

                    {loading ? (
                        <div className="flex items-center justify-center py-8">
                            <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-yellow-500"></div>
                            <span className="ml-3 text-gray-300">Loading...</span>
                        </div>
                    ) : (
                        <>
                            {/* Step 1: Assessment Details */}
                            {step === 1 && (
                                <div className="space-y-6">
                                    {/* Client Selection - MOVED TO TOP AND MADE REQUIRED */}
                                    <div>
                                        <label className="block text-sm font-medium text-gray-300 mb-2">
                                            Client <span className="text-red-400">*</span>
                                        </label>
                                        <select
                                            value={currentClient?.ClientId || ''}
                                            onChange={(e) => handleClientChange(e.target.value)}
                                            className="w-full px-3 py-2 bg-gray-700 border border-gray-600 rounded-md text-white focus:outline-none focus:ring-2 focus:ring-yellow-500 focus:border-transparent"
                                            required
                                        >
                                            <option value="">Select a client...</option>
                                            {clients.map((client) => (
                                                <option key={client.ClientId} value={client.ClientId}>
                                                    {client.Name}
                                                </option>
                                            ))}
                                        </select>
                                        <p className="text-xs text-gray-400 mt-1">
                                            Select the client for this assessment
                                        </p>
                                    </div>

                                    <div>
                                        <label className="block text-sm font-medium text-gray-300 mb-2">
                                            Assessment Name <span className="text-red-400">*</span>
                                        </label>
                                        <input
                                            type="text"
                                            value={assessmentName}
                                            onChange={(e) => setAssessmentName(e.target.value)}
                                            className="w-full px-3 py-2 bg-gray-700 border border-gray-600 rounded-md text-white placeholder-gray-400 focus:outline-none focus:ring-2 focus:ring-yellow-500 focus:border-transparent"
                                            placeholder="Enter assessment name..."
                                            autoFocus
                                        />
                                    </div>

                                    <div>
                                        <label className="block text-sm font-medium text-gray-300 mb-3">
                                            Assessment Type
                                        </label>
                                        <div className="space-y-3">
                                            {assessmentTypes.map((type) => (
                                                <div
                                                    key={type.id}
                                                    className={`relative p-4 border rounded-lg cursor-pointer transition-all ${assessmentType === type.id
                                                            ? 'border-yellow-500 bg-yellow-500/10'
                                                            : 'border-gray-600 bg-gray-700/50 hover:border-gray-500'
                                                        }`}
                                                    onClick={() => setAssessmentType(type.id)}
                                                >
                                                    <div className="flex items-start">
                                                        <div className="text-2xl mr-3">{type.icon}</div>
                                                        <div className="flex-1">
                                                            <div className="flex items-center">
                                                                <h3 className="font-medium text-white">{type.name}</h3>
                                                                {type.recommended && (
                                                                    <span className="ml-2 px-2 py-1 text-xs bg-yellow-500 text-black rounded-full">
                                                                        Recommended
                                                                    </span>
                                                                )}
                                                            </div>
                                                            <p className="text-sm text-gray-400 mt-1">{type.description}</p>
                                                            <p className="text-xs text-gray-500 mt-1">
                                                                Estimated time: {type.estimatedTime}
                                                            </p>
                                                        </div>
                                                        <div className={`w-5 h-5 rounded-full border-2 ${assessmentType === type.id
                                                                ? 'border-yellow-500 bg-yellow-500'
                                                                : 'border-gray-400'
                                                            }`}>
                                                            {assessmentType === type.id && (
                                                                <CheckCircle className="w-full h-full text-black" />
                                                            )}
                                                        </div>
                                                    </div>
                                                </div>
                                            ))}
                                        </div>
                                    </div>
                                </div>
                            )}

                            {/* Step 2: Environment Selection */}
                            {step === 2 && (
                                <div className="space-y-6">
                                    <div>
                                        <label className="block text-sm font-medium text-gray-300 mb-3">
                                            Select Azure Environment <span className="text-red-400">*</span>
                                        </label>
                                        {currentClient && (
                                            <div className="mb-4 p-3 bg-blue-500/10 border border-blue-500/20 rounded-lg">
                                                <div className="flex items-center">
                                                    <Info className="text-blue-400 mr-2" size={16} />
                                                    <span className="text-blue-400 text-sm">
                                                        Showing environments for: <strong>{currentClient.Name}</strong>
                                                    </span>
                                                </div>
                                            </div>
                                        )}
                                        <div className="space-y-3">
                                            {(() => {
                                                console.log('[NewAssessmentModal] Rendering environments:', environments);
                                                console.log('[NewAssessmentModal] Environments length:', environments.length);
                                                return null;
                                            })()}
                                            {environments.length === 0 ? (
                                                <div className="p-4 bg-gray-700/50 border border-gray-600 rounded-lg text-center">
                                                    <p className="text-gray-400">No Azure environments found</p>
                                                    <p className="text-sm text-gray-500 mt-1">
                                                        {currentClient ?
                                                            `No environments configured for ${currentClient.Name}` :
                                                            'Please configure Azure environments first'
                                                        }
                                                    </p>
                                                </div>
                                            ) : (
                                                environments.map((env) => {
                                                    console.log('[NewAssessmentModal] Rendering environment:', env);
                                                    return (
                                                        <div
                                                            key={env.AzureEnvironmentId || env.azureEnvironmentId || env.id}
                                                            className={`p-4 border rounded-lg cursor-pointer transition-all ${selectedEnvironment === (env.AzureEnvironmentId || env.azureEnvironmentId)
                                                                    ? 'border-yellow-500 bg-yellow-500/10'
                                                                    : 'border-gray-600 bg-gray-700/50 hover:border-gray-500'
                                                                }`}
                                                            onClick={() => setSelectedEnvironment(env.AzureEnvironmentId || env.azureEnvironmentId)}
                                                        >
                                                            <div className="flex items-center justify-between">
                                                                <div>
                                                                    <h3 className="font-medium text-white">{env.Name || env.name}</h3>
                                                                    <p className="text-sm text-gray-400 mt-1">
                                                                        {env.SubscriptionIds?.length || env.subscriptionIds?.length || 0} subscription(s)
                                                                    </p>
                                                                    {(env.ClientName || env.clientName) && (
                                                                        <p className="text-xs text-gray-500 mt-1">
                                                                            Client: {env.ClientName || env.clientName}
                                                                        </p>
                                                                    )}
                                                                </div>
                                                                <div className={`w-5 h-5 rounded-full border-2 ${selectedEnvironment === (env.AzureEnvironmentId || env.azureEnvironmentId)
                                                                        ? 'border-yellow-500 bg-yellow-500'
                                                                        : 'border-gray-400'
                                                                    }`}>
                                                                    {selectedEnvironment === (env.AzureEnvironmentId || env.azureEnvironmentId) && (
                                                                        <CheckCircle className="w-full h-full text-black" />
                                                                    )}
                                                                </div>
                                                            </div>
                                                        </div>
                                                    );
                                                })
                                            )}
                                        </div>
                                    </div>

                                    {/* Client Preferences Option */}
                                    {canUseClientPreferences() && (
                                        <div className="p-4 bg-gray-700/30 border border-gray-600 rounded-lg">
                                            <label className="flex items-start">
                                                <input
                                                    type="checkbox"
                                                    checked={useClientPreferences}
                                                    onChange={(e) => {
                                                        console.log('[NewAssessmentModal] Client preferences checkbox changed:', e.target.checked);
                                                        setUseClientPreferences(e.target.checked);
                                                    }}
                                                    className="mt-1 mr-3 w-4 h-4 text-yellow-500 bg-gray-700 border-gray-600 rounded focus:ring-yellow-500 focus:ring-2"
                                                />
                                                <div>
                                                    <span className="text-sm font-medium text-gray-300">
                                                        Use client-specific preferences for this assessment
                                                    </span>
                                                    <p className="text-xs text-gray-400 mt-1">
                                                        Assessment will apply client-specific naming patterns, required tags, and compliance frameworks if configured.
                                                    </p>
                                                </div>
                                            </label>
                                        </div>
                                    )}
                                </div>
                            )}

                            {/* Step 3: Review */}
                            {step === 3 && (
                                <div className="space-y-6">
                                    <div>
                                        <h3 className="text-lg font-medium text-white mb-4">Review Assessment Details</h3>
                                        <div className="space-y-4">
                                            <div className="p-4 bg-gray-700/50 border border-gray-600 rounded-lg">
                                                <div className="grid grid-cols-2 gap-4">
                                                    <div>
                                                        <label className="text-sm font-medium text-gray-400">Assessment Name</label>
                                                        <p className="text-white">{assessmentName}</p>
                                                    </div>
                                                    <div>
                                                        <label className="text-sm font-medium text-gray-400">Type</label>
                                                        <p className="text-white">{getSelectedAssessmentType()?.name}</p>
                                                    </div>
                                                    <div>
                                                        <label className="text-sm font-medium text-gray-400">Environment</label>
                                                        <p className="text-white">{getSelectedEnvironmentDetails()?.Name || getSelectedEnvironmentDetails()?.name}</p>
                                                    </div>
                                                    <div>
                                                        <label className="text-sm font-medium text-gray-400">Subscriptions</label>
                                                        <p className="text-white">{getSelectedEnvironmentDetails()?.SubscriptionIds?.length || getSelectedEnvironmentDetails()?.subscriptionIds?.length || 0}</p>
                                                    </div>
                                                </div>
                                            </div>

                                            {currentClient && (
                                                <div className="p-4 bg-blue-500/10 border border-blue-500/20 rounded-lg">
                                                    <div className="flex items-center mb-2">
                                                        <Info className="text-blue-400 mr-2" size={16} />
                                                        <span className="text-sm font-medium text-blue-400">Client-Scoped Assessment</span>
                                                    </div>
                                                    <p className="text-sm text-gray-300">
                                                        <strong>Client:</strong> {currentClient.Name}
                                                    </p>
                                                    {useClientPreferences && (
                                                        <p className="text-sm text-gray-300 mt-1">
                                                            <strong>Preferences:</strong> Client-specific standards will be applied
                                                        </p>
                                                    )}
                                                </div>
                                            )}

                                            <div className="p-4 bg-gray-700/50 border border-gray-600 rounded-lg">
                                                <h4 className="font-medium text-white mb-2">What will be analyzed:</h4>
                                                <ul className="text-sm text-gray-300 space-y-1">
                                                    {assessmentType === 0 && (
                                                        <li>• Resource naming patterns and consistency</li>
                                                    )}
                                                    {assessmentType === 1 && (
                                                        <li>• Resource tagging coverage and compliance</li>
                                                    )}
                                                    {assessmentType === 2 && (
                                                        <>
                                                            <li>• Resource naming patterns and consistency</li>
                                                            <li>• Resource tagging coverage and compliance</li>
                                                            <li>• Dependency analysis and environment separation</li>
                                                        </>
                                                    )}
                                                    {useClientPreferences && (
                                                        <li>• Client-specific governance standards and requirements</li>
                                                    )}
                                                </ul>
                                            </div>
                                        </div>
                                    </div>
                                </div>
                            )}
                        </>
                    )}
                </div>

                {/* Footer */}
                <div className="flex items-center justify-between p-6 border-t border-gray-700">
                    <div className="flex items-center">
                        {step > 1 && (
                            <button
                                onClick={handleBack}
                                className="px-4 py-2 text-gray-300 hover:text-white transition-colors"
                            >
                                Back
                            </button>
                        )}
                    </div>
                    <div className="flex items-center space-x-3">
                        <button
                            onClick={handleClose}
                            className="px-4 py-2 text-gray-300 hover:text-white transition-colors"
                        >
                            Cancel
                        </button>
                        {step < 3 ? (
                            <button
                                onClick={handleNext}
                                className="px-6 py-2 bg-yellow-500 text-black rounded-md hover:bg-yellow-600 transition-colors font-medium"
                            >
                                Next
                            </button>
                        ) : (
                            <button
                                onClick={handleSubmit}
                                disabled={isCreating}
                                className="px-6 py-2 bg-yellow-500 text-black rounded-md hover:bg-yellow-600 transition-colors font-medium disabled:opacity-50 disabled:cursor-not-allowed flex items-center"
                            >
                                {isCreating ? (
                                    <>
                                        <div className="animate-spin rounded-full h-4 w-4 border-b-2 border-black mr-2"></div>
                                        Creating...
                                    </>
                                ) : (
                                    'Create Assessment'
                                )}
                            </button>
                        )}
                    </div>
                </div>
            </div>
        </div>,
        document.body
    );
};

export default NewAssessmentModal;