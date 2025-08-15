import React, { useState, useEffect } from 'react';
import { createPortal } from 'react-dom';
import { X, Settings, Save, Loader2, AlertCircle, Info, CheckCircle } from 'lucide-react';
import { apiClient } from '../../services/apiService';
import NamingStrategyTab from './ClientPreferences/NamingStrategyTab';
import TaggingStrategyTab from './ClientPreferences/TaggingStrategyTab';
import ComplianceTab from './ClientPreferences/ComplianceTab';
import ServiceAbbreviationsTab from './ClientPreferences/ServiceAbbreviationsTab';

const ClientPreferencesModal = ({ isOpen, onClose, client, onPreferencesUpdated }) => {
    // State declarations
    const [preferences, setPreferences] = useState(null);
    const [isLoading, setIsLoading] = useState(false);
    const [isSaving, setIsSaving] = useState(false);
    const [error, setError] = useState(null);
    const [hasExistingPreferences, setHasExistingPreferences] = useState(false);
    const [activeTab, setActiveTab] = useState('naming');
    const [showDeleteConfirm, setShowDeleteConfirm] = useState(false);
    const [isDeleting, setIsDeleting] = useState(false);

    // Enhanced form data with guided options
    const [formData, setFormData] = useState({
        // Naming Convention Strategy
        namingStyle: 'mixed',
        environmentIndicators: 'required',
        namingElements: [],
        customPatterns: [],

        // Naming Scheme Configuration
        namingScheme: {
            components: [],
            separator: '-',
            caseFormat: 'lowercase'
        },
        acceptedCompanyNames: [],
        companyNameInput: '',

        // Service Abbreviations
        serviceAbbreviations: [],

        // Tagging Strategy  
        taggingApproach: 'basic',
        selectedTags: [],
        customTags: [],
        customTagInput: '',
        enforceTagCompliance: true,

        // Compliance Framework
        complianceLevel: 'none',
        selectedCompliances: [],

        // Organization Scale
        environmentSize: 'medium',
        organizationMethod: 'environment'
    });

    // useEffect hooks
    useEffect(() => {
        if (isOpen && client?.ClientId) {
            loadClientPreferences();
        }
    }, [isOpen, client?.ClientId]);

    useEffect(() => {
        if (!isOpen) {
            setError(null);
            setPreferences(null);
            setHasExistingPreferences(false);
            setActiveTab('naming');
            setShowDeleteConfirm(false);
            setIsDeleting(false);
            setFormData({
                namingStyle: 'mixed',
                environmentIndicators: 'required',
                namingElements: [],
                customPatterns: [],
                namingScheme: {
                    components: [],
                    separator: '-',
                    caseFormat: 'lowercase'
                },
                acceptedCompanyNames: [],
                companyNameInput: '',
                serviceAbbreviations: [],
                taggingApproach: 'basic',
                selectedTags: [],
                customTags: [],
                customTagInput: '',
                enforceTagCompliance: true,
                complianceLevel: 'none',
                selectedCompliances: [],
                environmentSize: 'medium',
                organizationMethod: 'environment'
            });
        }
    }, [isOpen]);

    // Helper functions
    const handleRadioChange = (field, value) => {
        setFormData(prev => {
            const updated = { ...prev, [field]: value };

            // Auto-populate based on template selection
            if (field === 'taggingApproach') {
                const taggingTemplates = {
                    comprehensive: ["Environment", "CostCenter", "Owner", "Application", "Project", "Backup", "Compliance"],
                    basic: ["Environment", "CostCenter", "Owner", "Application"],
                    minimal: ["Environment"],
                    custom: []
                };
                if (taggingTemplates[value]) {
                    updated.selectedTags = taggingTemplates[value];
                }
            }

            return updated;
        });
    };

    const handleCheckboxChange = (field, value) => {
        setFormData(prev => ({
            ...prev,
            [field]: prev[field].includes(value)
                ? prev[field].filter(item => item !== value)
                : [...prev[field], value]
        }));
    };

    // Main functions
    const loadClientPreferences = async () => {
        setIsLoading(true);
        setError(null);

        try {
            const response = await apiClient.get(`/clientpreferences/client/${client.ClientId}`);
            const prefs = response.data;

            if (prefs && Object.keys(prefs).length > 0) {
                setHasExistingPreferences(true);
                setPreferences(prefs);

                console.log('Loading preferences:', prefs); // Debug log
                console.log('NamingScheme from backend:', prefs.NamingScheme); // Debug log
                
                const mappedNamingScheme = prefs.NamingScheme ? {
                    components: (prefs.NamingScheme.Components || []).map(comp => ({
                        componentType: comp.ComponentType,
                        position: comp.Position,
                        isRequired: comp.IsRequired,
                        allowedValues: comp.AllowedValues || [],
                        defaultValue: comp.DefaultValue || ''
                    })),
                    separator: prefs.NamingScheme.Separator || '-',
                    caseFormat: prefs.NamingScheme.CaseFormat || 'lowercase'
                } : {
                    components: [],
                    separator: '-',
                    caseFormat: 'lowercase'
                };
                
                console.log('Mapped naming scheme:', mappedNamingScheme); // Debug log

                setFormData({
                    namingStyle: prefs.NamingStyle || 'mixed',
                    environmentIndicators: prefs.EnvironmentIndicators ? 'required' : 'optional',
                    namingElements: prefs.RequiredNamingElements || [],
                    customPatterns: prefs.AllowedNamingPatterns || [],
                    
                    // Use the mapped naming scheme
                    namingScheme: mappedNamingScheme,
                    acceptedCompanyNames: prefs.AcceptedCompanyNames || [],
                    companyNameInput: '',
                    serviceAbbreviations: prefs.ServiceAbbreviations || [],
                    
                    taggingApproach: prefs.TaggingApproach || 'basic',
                    selectedTags: prefs.RequiredTags || [],
                    customTags: prefs.CustomTags || [],
                    customTagInput: '',
                    enforceTagCompliance: prefs.EnforceTagCompliance !== false,
                    complianceLevel: prefs.ComplianceLevel || 'none',
                    selectedCompliances: prefs.ComplianceFrameworks || [],
                    environmentSize: prefs.EnvironmentSize || 'medium',
                    organizationMethod: prefs.OrganizationMethod || 'environment'
                });
            } else {
                setHasExistingPreferences(false);
            }
        } catch (error) {
            console.error('Failed to load client preferences:', error);
            if (error.response?.status === 404) {
                setHasExistingPreferences(false);
            } else {
                setError('Failed to load client preferences. Please try again.');
            }
        } finally {
            setIsLoading(false);
        }
    };

    const handleSave = async () => {
        setIsSaving(true);
        setError(null);

        try {
            const backendData = {
                AllowedNamingPatterns: formData.customPatterns.filter(p => p.trim()),
                RequiredNamingElements: formData.namingElements.filter(e => e.trim()),
                EnvironmentIndicators: formData.environmentIndicators === 'required',
                RequiredTags: [...formData.selectedTags, ...formData.customTags],
                EnforceTagCompliance: formData.enforceTagCompliance,
                ComplianceFrameworks: formData.selectedCompliances,
                NamingStyle: formData.namingStyle,
                TaggingApproach: formData.taggingApproach,
                CustomTags: formData.customTags,
                ComplianceLevel: formData.complianceLevel,
                EnvironmentSize: formData.environmentSize,
                OrganizationMethod: formData.organizationMethod,
                NamingScheme: (formData.namingScheme?.components || []).length > 0 
                    ? formData.namingScheme 
                    : null,
                AcceptedCompanyNames: formData.acceptedCompanyNames,
                ServiceAbbreviations: formData.serviceAbbreviations
            };

            await apiClient.post(`/clientpreferences/client/${client.ClientId}`, backendData);

            if (onPreferencesUpdated) {
                onPreferencesUpdated();
            }

            onClose();
        } catch (error) {
            console.error('Failed to save client preferences:', error);

            let errorMessage = 'Failed to save preferences. Please try again.';
            if (error.response?.data?.message) {
                errorMessage = error.response.data.message;
            } else if (error.response?.data) {
                errorMessage = error.response.data;
            }

            setError(errorMessage);
        } finally {
            setIsSaving(false);
        }
    };

    const handleDelete = async () => {
        setIsDeleting(true);
        setError(null);

        try {
            await apiClient.delete(`/clientpreferences/client/${client.ClientId}`);

            setHasExistingPreferences(false);
            setPreferences(null);
            setFormData({
                namingStyle: 'mixed',
                environmentIndicators: 'required',
                namingElements: [],
                customPatterns: [],
                namingScheme: {
                    components: [],
                    separator: '-',
                    caseFormat: 'lowercase'
                },
                acceptedCompanyNames: [],
                companyNameInput: '',
                serviceAbbreviations: [],
                taggingApproach: 'basic',
                selectedTags: [],
                customTags: [],
                customTagInput: '',
                enforceTagCompliance: true,
                complianceLevel: 'none',
                selectedCompliances: [],
                environmentSize: 'medium',
                organizationMethod: 'environment'
            });

            if (onPreferencesUpdated) {
                onPreferencesUpdated();
            }

            setShowDeleteConfirm(false);
            onClose();
        } catch (error) {
            console.error('Failed to delete client preferences:', error);

            let errorMessage = 'Failed to delete preferences. Please try again.';
            if (error.response?.data?.message) {
                errorMessage = error.response.data.message;
            } else if (error.response?.data) {
                errorMessage = error.response.data;
            }

            setError(errorMessage);
        } finally {
            setIsDeleting(false);
        }
    };

    if (!isOpen || !client) return null;

    return createPortal(
        <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center p-4 z-50">
            <div className="bg-gray-800 rounded-lg shadow-xl w-full max-w-4xl max-h-[90vh] overflow-y-auto">
                {/* Header */}
                <div className="flex items-center justify-between p-6 border-b border-gray-700">
                    <div className="flex items-center space-x-3">
                        <div className="w-10 h-10 bg-purple-600 rounded-lg flex items-center justify-center">
                            <Settings size={20} className="text-white" />
                        </div>
                        <div>
                            <h2 className="text-lg font-semibold text-white">Client Governance Preferences</h2>
                            <p className="text-sm text-gray-400">
                                Configure governance standards for {client.Name}
                            </p>
                        </div>
                    </div>
                    <button
                        onClick={onClose}
                        className="text-gray-400 hover:text-white p-2 rounded-lg hover:bg-gray-700"
                    >
                        <X size={20} />
                    </button>
                </div>

                {/* Loading State */}
                {isLoading && (
                    <div className="p-6 text-center">
                        <div className="w-8 h-8 border-2 border-yellow-600 border-t-transparent rounded-full animate-spin mx-auto mb-4"></div>
                        <p className="text-gray-400">Loading preferences...</p>
                    </div>
                )}

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

                {/* Content */}
                {!isLoading && (
                    <>
                        {/* Status Banner */}
                        <div className={`m-6 mb-0 p-4 rounded border ${hasExistingPreferences
                                ? 'bg-green-900/20 border-green-800'
                                : 'bg-blue-900/20 border-blue-800'
                            }`}>
                            <div className="flex items-center space-x-3">
                                {hasExistingPreferences ? (
                                    <CheckCircle className="text-green-400" size={20} />
                                ) : (
                                    <Info className="text-blue-400" size={20} />
                                )}
                                <div>
                                    <h4 className={`font-medium ${hasExistingPreferences ? 'text-green-400' : 'text-blue-400'}`}>
                                        {hasExistingPreferences ? 'Custom Preferences Active' : 'Using Default Governance'}
                                    </h4>
                                    <p className={`text-sm mt-1 ${hasExistingPreferences ? 'text-green-300' : 'text-blue-300'}`}>
                                        {hasExistingPreferences
                                            ? 'This client has customized governance standards that will enhance assessment recommendations.'
                                            : 'Set client-specific preferences to get more targeted governance recommendations.'
                                        }
                                    </p>
                                </div>
                            </div>
                        </div>

                        {/* Environment Scale Context */}
                        <div className="px-6 pt-6">
                            <div className="bg-gray-700 rounded-lg p-4 mb-6">
                                <h3 className="text-white font-medium mb-3">Environment Scale</h3>
                                <div className="grid grid-cols-2 md:grid-cols-4 gap-3">
                                    {[
                                        { value: 'small', label: 'Small', desc: '< 50 resources' },
                                        { value: 'medium', label: 'Medium', desc: '50-200 resources' },
                                        { value: 'large', label: 'Large', desc: '200-500 resources' },
                                        { value: 'enterprise', label: 'Enterprise', desc: '500+ resources' }
                                    ].map(option => (
                                        <label key={option.value} className="flex items-center space-x-2 cursor-pointer">
                                            <input
                                                type="radio"
                                                name="environmentSize"
                                                value={option.value}
                                                checked={formData.environmentSize === option.value}
                                                onChange={(e) => handleRadioChange('environmentSize', e.target.value)}
                                                className="text-yellow-600 focus:ring-yellow-600"
                                            />
                                            <div>
                                                <div className="text-white text-sm font-medium">{option.label}</div>
                                                <div className="text-gray-400 text-xs">{option.desc}</div>
                                            </div>
                                        </label>
                                    ))}
                                </div>
                            </div>
                        </div>

                        {/* Tab Navigation */}
                        <div className="px-6">
                            <div className="flex space-x-1 bg-gray-700 rounded-lg p-1">
                                <button
                                    onClick={() => setActiveTab('naming')}
                                    className={`flex-1 px-3 py-2 rounded-md text-sm font-medium transition-colors ${activeTab === 'naming'
                                            ? 'bg-yellow-600 text-black'
                                            : 'text-gray-300 hover:text-white hover:bg-gray-600'
                                        }`}
                                >
                                    Naming
                                </button>
                                <button
                                    onClick={() => setActiveTab('abbreviations')}
                                    className={`flex-1 px-3 py-2 rounded-md text-sm font-medium transition-colors ${activeTab === 'abbreviations'
                                            ? 'bg-yellow-600 text-black'
                                            : 'text-gray-300 hover:text-white hover:bg-gray-600'
                                        }`}
                                >
                                    Abbreviations
                                </button>
                                <button
                                    onClick={() => setActiveTab('tagging')}
                                    className={`flex-1 px-3 py-2 rounded-md text-sm font-medium transition-colors ${activeTab === 'tagging'
                                            ? 'bg-yellow-600 text-black'
                                            : 'text-gray-300 hover:text-white hover:bg-gray-600'
                                        }`}
                                >
                                    Tagging
                                </button>
                                <button
                                    onClick={() => setActiveTab('compliance')}
                                    className={`flex-1 px-3 py-2 rounded-md text-sm font-medium transition-colors ${activeTab === 'compliance'
                                            ? 'bg-yellow-600 text-black'
                                            : 'text-gray-300 hover:text-white hover:bg-gray-600'
                                        }`}
                                >
                                    Compliance
                                </button>
                            </div>
                        </div>

                        {/* Tab Content */}
                        <div className="p-6">
                            {activeTab === 'naming' && (
                                <NamingStrategyTab 
                                    formData={formData}
                                    setFormData={setFormData}
                                    handleRadioChange={handleRadioChange}
                                />
                            )}
                            
                            {activeTab === 'abbreviations' && (
                                <ServiceAbbreviationsTab 
                                    formData={formData}
                                    setFormData={setFormData}
                                />
                            )}
                            
                            {activeTab === 'tagging' && (
                                <TaggingStrategyTab 
                                    formData={formData}
                                    setFormData={setFormData}
                                    handleRadioChange={handleRadioChange}
                                    handleCheckboxChange={handleCheckboxChange}
                                />
                            )}
                            
                            {activeTab === 'compliance' && (
                                <ComplianceTab 
                                    formData={formData}
                                    setFormData={setFormData}
                                    handleCheckboxChange={handleCheckboxChange}
                                />
                            )}
                        </div>

                        {/* Footer */}
                        <div className="flex items-center justify-between p-6 border-t border-gray-700">
                            <div>
                                {hasExistingPreferences && (
                                    <button
                                        type="button"
                                        onClick={() => setShowDeleteConfirm(true)}
                                        className="px-4 py-2 text-red-400 hover:text-red-300 hover:bg-red-900/20 rounded transition-colors"
                                        disabled={isSaving || isDeleting}
                                    >
                                        Delete Preferences
                                    </button>
                                )}
                            </div>
                            <div className="flex items-center space-x-3">
                                <button
                                    type="button"
                                    onClick={onClose}
                                    className="px-4 py-2 text-gray-400 hover:text-white transition-colors"
                                    disabled={isSaving || isDeleting}
                                >
                                    Cancel
                                </button>
                                <button
                                    onClick={handleSave}
                                    disabled={isSaving || isDeleting}
                                    className="px-6 py-2 bg-yellow-600 hover:bg-yellow-700 text-black rounded font-medium flex items-center space-x-2 disabled:opacity-50"
                                >
                                    {isSaving ? (
                                        <>
                                            <Loader2 size={16} className="animate-spin" />
                                            <span>Saving...</span>
                                        </>
                                    ) : (
                                        <>
                                            <Save size={16} />
                                            <span>Save Preferences</span>
                                        </>
                                    )}
                                </button>
                            </div>
                        </div>
                    </>
                )}
            </div>

            {/* Delete Confirmation Modal */}
            {showDeleteConfirm && (
                <div className="fixed inset-0 bg-black bg-opacity-75 flex items-center justify-center z-[60]">
                    <div className="bg-gray-800 rounded-lg p-6 max-w-md w-full mx-4 border border-gray-700">
                        <div className="flex items-center mb-4">
                            <AlertCircle className="h-6 w-6 text-red-400 mr-3" />
                            <h3 className="text-lg font-semibold text-white">Confirm Delete</h3>
                        </div>
                        <p className="text-gray-300 mb-6">
                            Are you sure you want to delete the governance preferences for <strong className="text-white">{client?.Name}</strong>?
                            This action cannot be undone and future assessments will use default standards.
                        </p>
                        <div className="flex justify-end space-x-3">
                            <button
                                type="button"
                                className="px-4 py-2 text-gray-300 hover:text-white hover:bg-gray-700 rounded transition-colors"
                                onClick={() => setShowDeleteConfirm(false)}
                                disabled={isDeleting}
                            >
                                Cancel
                            </button>
                            <button
                                type="button"
                                className="px-4 py-2 bg-red-600 text-white rounded hover:bg-red-500 transition-colors disabled:opacity-50 flex items-center space-x-2"
                                onClick={handleDelete}
                                disabled={isDeleting}
                            >
                                {isDeleting ? (
                                    <>
                                        <Loader2 size={16} className="animate-spin" />
                                        <span>Deleting...</span>
                                    </>
                                ) : (
                                    <span>Delete Preferences</span>
                                )}
                            </button>
                        </div>
                    </div>
                </div>
            )}
        </div>,
        document.body
    );
};

export default ClientPreferencesModal;