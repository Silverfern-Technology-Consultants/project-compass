import React, { useState, useEffect } from 'react';
import { createPortal } from 'react-dom';
import { X, Settings, Save, Loader2, AlertCircle, Info, CheckCircle } from 'lucide-react';
import { apiClient } from '../../services/apiService';

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
        namingStyle: 'mixed', // 'standardized', 'mixed', 'legacy'
        environmentIndicators: 'required', // 'required', 'recommended', 'optional', 'none'
        namingElements: [], // Array of required elements
        customPatterns: [], // For advanced users

        // Tagging Strategy  
        taggingApproach: 'basic', // 'comprehensive', 'basic', 'minimal', 'custom'
        selectedTags: [], // Individual tag selection
        customTags: [], // User-defined tags
        customTagInput: '', // Text input for new custom tag
        enforceTagCompliance: true,

        // Compliance Framework
        complianceLevel: 'none', // 'strict', 'moderate', 'basic', 'none'
        selectedCompliances: [], // Individual compliance selection

        // Organization Scale
        environmentSize: 'medium', // 'small', 'medium', 'large', 'enterprise'
        organizationMethod: 'environment' // 'environment', 'application', 'business-unit', 'hybrid'
    });

    // Constants and templates
    const namingTemplates = {
        standardized: {
            title: "Standardized Naming",
            description: "All resources follow consistent patterns (recommended for new environments)",
            patterns: ["[company]-[env]-[service]-[number]", "[env]-[resourcetype]-[purpose]"],
            example: "acme-prod-web-01, dev-vm-database"
        },
        mixed: {
            title: "Mixed Conventions",
            description: "Allow multiple naming styles (good for existing environments)",
            patterns: ["Flexible patterns allowed"],
            example: "CORP-AZ-BACKUP, DataProcessPlan, workers-sg"
        },
        legacy: {
            title: "Legacy Preservation",
            description: "Keep existing naming, focus on tagging for governance",
            patterns: ["No naming requirements"],
            example: "Existing names maintained"
        }
    };

    const taggingTemplates = {
        comprehensive: {
            title: "Comprehensive Tagging",
            description: "Detailed tagging for cost allocation and governance",
            tags: ["Environment", "CostCenter", "Owner", "Application", "Project", "Backup", "Compliance"]
        },
        basic: {
            title: "Essential Tagging",
            description: "Core tags for basic governance and cost tracking",
            tags: ["Environment", "CostCenter", "Owner", "Application"]
        },
        minimal: {
            title: "Minimal Tagging",
            description: "Only environment tagging for basic organization",
            tags: ["Environment"]
        },
        custom: {
            title: "Custom Selection",
            description: "Choose specific tags that meet your needs",
            tags: []
        }
    };

    const availableTags = [
        { name: "Environment", description: "prod, dev, test, staging" },
        { name: "CostCenter", description: "Department or cost allocation code" },
        { name: "Owner", description: "Resource owner or team responsible" },
        { name: "Application", description: "Application or service name" },
        { name: "Project", description: "Project or initiative name" },
        { name: "Backup", description: "Backup schedule or requirement" },
        { name: "Compliance", description: "Compliance framework requirements" },
        { name: "Location", description: "Geographic location or region" },
        { name: "Department", description: "Business department" },
        { name: "Schedule", description: "Operating schedule (24x7, business hours)" },
        { name: "Criticality", description: "Business criticality level" },
        { name: "DataClassification", description: "Data sensitivity level" }
    ];

    const availableCompliances = [
        { name: "SOC 2", description: "Service Organization Control 2" },
        { name: "HIPAA", description: "Health Insurance Portability and Accountability Act" },
        { name: "PCI DSS", description: "Payment Card Industry Data Security Standard" },
        { name: "ISO 27001", description: "Information Security Management Systems" },
        { name: "GDPR", description: "General Data Protection Regulation" },
        { name: "FedRAMP", description: "Federal Risk and Authorization Management Program" },
        { name: "NIST", description: "National Institute of Standards and Technology" },
        { name: "CIS Controls", description: "Center for Internet Security Controls" },
        { name: "FISMA", description: "Federal Information Security Management Act" },
        { name: "SOX", description: "Sarbanes-Oxley Act" }
    ];

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
            if (field === 'taggingApproach' && taggingTemplates[value]) {
                updated.selectedTags = taggingTemplates[value].tags;
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

    const handleAddCustomTag = () => {
        const tagName = formData.customTagInput.trim();
        if (tagName && !formData.customTags.includes(tagName) && !formData.selectedTags.includes(tagName)) {
            setFormData(prev => ({
                ...prev,
                customTags: [...prev.customTags, tagName],
                selectedTags: [...prev.selectedTags, tagName],
                customTagInput: '' // Clear the input field
            }));
        }
    };

    const handleCustomTagInputChange = (e) => {
        setFormData(prev => ({
            ...prev,
            customTagInput: e.target.value
        }));
    };

    const handleCustomTagKeyPress = (e) => {
        if (e.key === 'Enter') {
            e.preventDefault();
            handleAddCustomTag();
        }
    };

    const handleRemoveCustomTag = (tagToRemove) => {
        setFormData(prev => ({
            ...prev,
            customTags: prev.customTags.filter(tag => tag !== tagToRemove),
            selectedTags: prev.selectedTags.filter(tag => tag !== tagToRemove)
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

                // Map backend data to enhanced form structure
                setFormData({
                    namingStyle: prefs.NamingStyle || 'mixed',
                    environmentIndicators: prefs.EnvironmentIndicators ? 'required' : 'optional',
                    namingElements: prefs.RequiredNamingElements || [],
                    customPatterns: prefs.AllowedNamingPatterns || [],
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
            // Transform enhanced form data to backend format
            const backendData = {
                // Backend expects PascalCase properties
                AllowedNamingPatterns: formData.customPatterns.filter(p => p.trim()),
                RequiredNamingElements: formData.namingElements.filter(e => e.trim()),
                EnvironmentIndicators: formData.environmentIndicators === 'required',
                RequiredTags: [...formData.selectedTags, ...formData.customTags],
                EnforceTagCompliance: formData.enforceTagCompliance,
                ComplianceFrameworks: formData.selectedCompliances,

                // Add new structured preferences
                NamingStyle: formData.namingStyle,
                TaggingApproach: formData.taggingApproach,
                CustomTags: formData.customTags,
                ComplianceLevel: formData.complianceLevel,
                EnvironmentSize: formData.environmentSize,
                OrganizationMethod: formData.organizationMethod
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

            // Reset state
            setHasExistingPreferences(false);
            setPreferences(null);
            setFormData({
                namingStyle: 'mixed',
                environmentIndicators: 'required',
                namingElements: [],
                customPatterns: [],
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
                                    className={`flex-1 px-4 py-2 rounded-md text-sm font-medium transition-colors ${activeTab === 'naming'
                                            ? 'bg-yellow-600 text-black'
                                            : 'text-gray-300 hover:text-white hover:bg-gray-600'
                                        }`}
                                >
                                    Naming Strategy
                                </button>
                                <button
                                    onClick={() => setActiveTab('tagging')}
                                    className={`flex-1 px-4 py-2 rounded-md text-sm font-medium transition-colors ${activeTab === 'tagging'
                                            ? 'bg-yellow-600 text-black'
                                            : 'text-gray-300 hover:text-white hover:bg-gray-600'
                                        }`}
                                >
                                    Tagging Strategy
                                </button>
                                <button
                                    onClick={() => setActiveTab('compliance')}
                                    className={`flex-1 px-4 py-2 rounded-md text-sm font-medium transition-colors ${activeTab === 'compliance'
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
                            {/* Naming Strategy Tab */}
                            {activeTab === 'naming' && (
                                <div className="space-y-6">
                                    {/* Naming Convention Approach */}
                                    <div>
                                        <h3 className="text-white font-medium mb-4">Naming Convention Approach</h3>
                                        <div className="space-y-3">
                                            {Object.entries(namingTemplates).map(([key, template]) => (
                                                <label key={key} className="flex items-start space-x-3 p-4 bg-gray-700 rounded-lg cursor-pointer hover:bg-gray-600">
                                                    <input
                                                        type="radio"
                                                        name="namingStyle"
                                                        value={key}
                                                        checked={formData.namingStyle === key}
                                                        onChange={(e) => handleRadioChange('namingStyle', e.target.value)}
                                                        className="mt-1 text-yellow-600 focus:ring-yellow-600"
                                                    />
                                                    <div className="flex-1">
                                                        <div className="text-white font-medium">{template.title}</div>
                                                        <div className="text-gray-300 text-sm mt-1">{template.description}</div>
                                                        <div className="text-gray-400 text-xs mt-2">
                                                            <strong>Example:</strong> {template.example}
                                                        </div>
                                                    </div>
                                                </label>
                                            ))}
                                        </div>
                                    </div>

                                    {/* Environment Indicators */}
                                    <div>
                                        <h3 className="text-white font-medium mb-4">Environment Indicators</h3>
                                        <div className="space-y-3">
                                            {[
                                                { value: 'required', label: 'Required', desc: 'All resources must indicate environment (prod, dev, test)' },
                                                { value: 'recommended', label: 'Recommended', desc: 'Environment indicators preferred but not enforced' },
                                                { value: 'optional', label: 'Optional', desc: 'Environment indicators used only where helpful' },
                                                { value: 'none', label: 'Not Used', desc: 'No environment indicators in naming' }
                                            ].map(option => (
                                                <label key={option.value} className="flex items-start space-x-3 p-3 bg-gray-700 rounded cursor-pointer hover:bg-gray-600">
                                                    <input
                                                        type="radio"
                                                        name="environmentIndicators"
                                                        value={option.value}
                                                        checked={formData.environmentIndicators === option.value}
                                                        onChange={(e) => handleRadioChange('environmentIndicators', e.target.value)}
                                                        className="mt-1 text-yellow-600 focus:ring-yellow-600"
                                                    />
                                                    <div>
                                                        <div className="text-white font-medium">{option.label}</div>
                                                        <div className="text-gray-300 text-sm">{option.desc}</div>
                                                    </div>
                                                </label>
                                            ))}
                                        </div>
                                    </div>

                                    {/* Required Naming Elements */}
                                    <div>
                                        <h3 className="text-white font-medium mb-4">Required Naming Elements</h3>
                                        <p className="text-gray-400 text-sm mb-4">
                                            Select elements that must be present in resource names for easier identification and organization.
                                        </p>
                                        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                                            {[
                                                { element: 'Company Code', example: 'acme, corp, abc' },
                                                { element: 'Environment', example: 'prod, dev, test, stage' },
                                                { element: 'Location', example: 'eastus, westus, central' },
                                                { element: 'Application', example: 'web, api, db, crm' },
                                                { element: 'Resource Type', example: 'vm, storage, vnet, lb' },
                                                { element: 'Sequence Number', example: '01, 02, 001, 002' }
                                            ].map(item => (
                                                <label key={item.element} className="flex items-start space-x-3 p-3 bg-gray-700 rounded cursor-pointer hover:bg-gray-600">
                                                    <input
                                                        type="checkbox"
                                                        checked={formData.namingElements.includes(item.element)}
                                                        onChange={() => handleCheckboxChange('namingElements', item.element)}
                                                        className="mt-1 text-yellow-600 focus:ring-yellow-600"
                                                    />
                                                    <div className="flex-1">
                                                        <div className="text-gray-300 text-sm font-medium">{item.element}</div>
                                                        <div className="text-gray-400 text-xs mt-1">
                                                            Examples: {item.example}
                                                        </div>
                                                    </div>
                                                </label>
                                            ))}
                                        </div>
                                    </div>
                                </div>
                            )}

                            {/* Tagging Strategy Tab */}
                            {activeTab === 'tagging' && (
                                <div className="space-y-6">
                                    {/* Tagging Approach */}
                                    <div>
                                        <h3 className="text-white font-medium mb-4">Tagging Strategy</h3>
                                        <div className="space-y-3">
                                            {Object.entries(taggingTemplates).map(([key, template]) => (
                                                <label key={key} className="flex items-start space-x-3 p-4 bg-gray-700 rounded-lg cursor-pointer hover:bg-gray-600">
                                                    <input
                                                        type="radio"
                                                        name="taggingApproach"
                                                        value={key}
                                                        checked={formData.taggingApproach === key}
                                                        onChange={(e) => handleRadioChange('taggingApproach', e.target.value)}
                                                        className="mt-1 text-yellow-600 focus:ring-yellow-600"
                                                    />
                                                    <div className="flex-1">
                                                        <div className="text-white font-medium">{template.title}</div>
                                                        <div className="text-gray-300 text-sm mt-1">{template.description}</div>
                                                        {template.tags.length > 0 && (
                                                            <div className="text-gray-400 text-xs mt-2">
                                                                <strong>Includes:</strong> {template.tags.join(', ')}
                                                            </div>
                                                        )}
                                                    </div>
                                                </label>
                                            ))}
                                        </div>
                                    </div>

                                    {/* Tag Selection */}
                                    <div>
                                        <h3 className="text-white font-medium mb-4">Required Tags</h3>
                                        <p className="text-gray-400 text-sm mb-4">
                                            Select the tags that must be present on all resources for proper governance and cost tracking.
                                        </p>
                                        <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
                                            {availableTags.map(tag => (
                                                <label key={tag.name} className="flex items-start space-x-3 p-3 bg-gray-700 rounded cursor-pointer hover:bg-gray-600">
                                                    <input
                                                        type="checkbox"
                                                        checked={formData.selectedTags.includes(tag.name)}
                                                        onChange={() => handleCheckboxChange('selectedTags', tag.name)}
                                                        className="mt-1 text-yellow-600 focus:ring-yellow-600"
                                                    />
                                                    <div className="flex-1">
                                                        <div className="text-gray-300 text-sm font-medium">{tag.name}</div>
                                                        <div className="text-gray-400 text-xs mt-1">{tag.description}</div>
                                                    </div>
                                                </label>
                                            ))}
                                        </div>
                                    </div>

                                    {/* Custom Tags */}
                                    <div>
                                        <h3 className="text-white font-medium mb-4">Custom Tags</h3>
                                        <p className="text-gray-400 text-sm mb-4">
                                            Add organization-specific tags that aren't covered by the standard options.
                                        </p>

                                        {/* Custom Tag Input */}
                                        <div className="mb-4">
                                            <div className="flex space-x-3">
                                                <input
                                                    type="text"
                                                    value={formData.customTagInput}
                                                    onChange={handleCustomTagInputChange}
                                                    onKeyPress={handleCustomTagKeyPress}
                                                    placeholder="Enter custom tag name..."
                                                    className="flex-1 px-3 py-2 bg-gray-700 border border-gray-600 rounded text-white placeholder-gray-400 focus:outline-none focus:ring-2 focus:ring-yellow-600"
                                                />
                                                <button
                                                    onClick={handleAddCustomTag}
                                                    disabled={!formData.customTagInput.trim()}
                                                    className="px-4 py-2 bg-yellow-600 hover:bg-yellow-700 disabled:bg-gray-600 disabled:cursor-not-allowed text-black rounded text-sm font-medium"
                                                >
                                                    Add Tag
                                                </button>
                                            </div>
                                        </div>

                                        {/* Display Custom Tags */}
                                        {formData.customTags.length > 0 && (
                                            <div className="mb-4">
                                                <div className="flex flex-wrap gap-2">
                                                    {formData.customTags.map(tag => (
                                                        <div key={tag} className="flex items-center space-x-2 bg-yellow-600 text-black px-3 py-1 rounded-full text-sm">
                                                            <span>{tag}</span>
                                                            <button
                                                                onClick={() => handleRemoveCustomTag(tag)}
                                                                className="text-black hover:text-gray-700"
                                                            >
                                                                <X size={14} />
                                                            </button>
                                                        </div>
                                                    ))}
                                                </div>
                                            </div>
                                        )}
                                    </div>

                                    {/* Tag Compliance */}
                                    <div>
                                        <h3 className="text-white font-medium mb-4">Tag Compliance</h3>
                                        <label className="flex items-center space-x-3">
                                            <input
                                                type="checkbox"
                                                checked={formData.enforceTagCompliance}
                                                onChange={(e) => setFormData(prev => ({ ...prev, enforceTagCompliance: e.target.checked }))}
                                                className="text-yellow-600 focus:ring-yellow-600"
                                            />
                                            <div>
                                                <span className="text-white font-medium">Enforce Tag Compliance</span>
                                                <p className="text-gray-400 text-sm">Flag resources as non-compliant if missing required tags</p>
                                            </div>
                                        </label>
                                    </div>
                                </div>
                            )}

                            {/* Compliance Tab */}
                            {activeTab === 'compliance' && (
                                <div className="space-y-6">
                                    {/* No Specific Requirements Option */}
                                    <div>
                                        <label className="flex items-center space-x-3 p-4 bg-gray-700 rounded-lg cursor-pointer hover:bg-gray-600">
                                            <input
                                                type="checkbox"
                                                checked={formData.selectedCompliances.length === 0}
                                                onChange={(e) => {
                                                    if (e.target.checked) {
                                                        setFormData(prev => ({ ...prev, selectedCompliances: [] }));
                                                    }
                                                }}
                                                className="text-yellow-600 focus:ring-yellow-600"
                                            />
                                            <div>
                                                <div className="text-white font-medium">No Specific Requirements</div>
                                                <div className="text-gray-300 text-sm">Standard governance only - no compliance frameworks required</div>
                                            </div>
                                        </label>
                                    </div>

                                    {/* Compliance Frameworks - Always Visible */}
                                    <div>
                                        <h3 className="text-white font-medium mb-4">Applicable Frameworks</h3>
                                        <p className="text-gray-400 text-sm mb-4">
                                            Select the compliance frameworks that apply to this client's environment.
                                        </p>

                                        {/* Quick Selection - Common Frameworks */}
                                        <div className="mb-6">
                                            <h4 className="text-gray-300 font-medium mb-3">Common Frameworks</h4>
                                            <div className="grid grid-cols-1 md:grid-cols-3 gap-3">
                                                {[
                                                    { name: "PCI DSS", description: "Payment Card Industry Data Security Standard" },
                                                    { name: "SOC 2", description: "Service Organization Control 2" },
                                                    { name: "CIS Controls", description: "Center for Internet Security Controls" }
                                                ].map(compliance => (
                                                    <label key={compliance.name} className="flex items-start space-x-3 p-4 bg-blue-900/20 border border-blue-800 rounded-lg cursor-pointer hover:bg-blue-900/30">
                                                        <input
                                                            type="checkbox"
                                                            checked={formData.selectedCompliances.includes(compliance.name)}
                                                            onChange={() => handleCheckboxChange('selectedCompliances', compliance.name)}
                                                            className="mt-1 text-yellow-600 focus:ring-yellow-600"
                                                        />
                                                        <div className="flex-1">
                                                            <div className="text-blue-300 text-sm font-medium">{compliance.name}</div>
                                                            <div className="text-blue-400 text-xs mt-1">{compliance.description}</div>
                                                        </div>
                                                    </label>
                                                ))}
                                            </div>
                                        </div>

                                        {/* All Available Frameworks */}
                                        <div>
                                            <h4 className="text-gray-300 font-medium mb-3">All Available Frameworks</h4>
                                            <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
                                                {availableCompliances.map(compliance => (
                                                    <label key={compliance.name} className="flex items-start space-x-3 p-3 bg-gray-700 rounded cursor-pointer hover:bg-gray-600">
                                                        <input
                                                            type="checkbox"
                                                            checked={formData.selectedCompliances.includes(compliance.name)}
                                                            onChange={() => handleCheckboxChange('selectedCompliances', compliance.name)}
                                                            className="mt-1 text-yellow-600 focus:ring-yellow-600"
                                                        />
                                                        <div className="flex-1">
                                                            <div className="text-gray-300 text-sm font-medium">{compliance.name}</div>
                                                            <div className="text-gray-400 text-xs mt-1">{compliance.description}</div>
                                                        </div>
                                                    </label>
                                                ))}
                                            </div>
                                        </div>
                                    </div>
                                </div>
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
        </div>
        , document.body
    );
};

export default ClientPreferencesModal;