import React, { useState, useEffect, useCallback } from 'react';
import {
    CheckCircle, AlertTriangle, XCircle, FileText, BarChart3,
    Server, Shield, User, Clock, Calendar, Activity, TrendingUp,
    Settings, HardDrive, Database, Network, Zap, Globe, Tag, Target
} from 'lucide-react';
import { apiClient } from '../../../services/apiService';
import { getResourceTypeInfo } from '../../../utils/azureResourceIcons';
import { useTimezone } from '../../../contexts/TimezoneContext';
import { useAuth } from '../../../contexts/AuthContext';

const PreferencesAppliedColumn = ({ assessment, findings }) => {
    const [clientPreferences, setClientPreferences] = useState(null);
    const [preferencesLoading, setPreferencesLoading] = useState(false);

    const loadClientPreferences = useCallback(async () => {
        if (!assessment.clientId && !assessment.ClientId) {
            setClientPreferences({});
            return;
        }

        setPreferencesLoading(true);
        try {
            const clientId = assessment.clientId || assessment.ClientId;
            console.log('[PreferencesAppliedColumn] Loading preferences for client:', clientId);
            const response = await apiClient.get(`/ClientPreferences/client/${clientId}`);
            console.log('[PreferencesAppliedColumn] Raw preferences response:', response.data);
            setClientPreferences(response.data);
        } catch (error) {
            console.error('Failed to load client preferences:', error);
            
            if (error.response?.status === 404) {
                setClientPreferences({});
            } else {
                setClientPreferences({});
            }
        } finally {
            setPreferencesLoading(false);
        }
    }, [assessment.clientId, assessment.ClientId]);

    useEffect(() => {
        const assessmentId = assessment?.id || assessment?.assessmentId || assessment?.AssessmentId;
        if (assessmentId) {
            loadClientPreferences();
        }
    }, [assessment?.id, loadClientPreferences]);

    return (
        <div className="bg-gray-800 rounded-lg p-6 border border-gray-700 flex flex-col h-full min-h-0">
            <h3 className="text-lg font-semibold text-white mb-4 flex items-center space-x-2 flex-shrink-0">
                <Settings size={20} className="text-yellow-600" />
                <span>Preferences Applied</span>
            </h3>

            {/* Loading state for preferences */}
            {preferencesLoading && (
                <div className="flex-1 flex items-center justify-center">
                    <div className="text-center">
                        <div className="animate-spin rounded-full h-6 w-6 border-b-2 border-yellow-600 mx-auto mb-2"></div>
                        <p className="text-gray-400 text-xs">Loading preferences...</p>
                    </div>
                </div>
            )}

            {/* No preferences or default mode */}
            {(() => {
                const showDefault = !preferencesLoading && (!clientPreferences || Object.keys(clientPreferences).length === 0);
                return showDefault;
            })() && (
                <div className="flex-1 flex items-start">
                    <div className="w-full">
                        <div className="bg-gray-700/30 border border-gray-600 rounded-lg p-3 text-center">
                            <Settings size={24} className="text-gray-500 mx-auto mb-2" />
                            <h4 className="font-medium text-gray-300 mb-2 text-sm">Standard Assessment</h4>
                            <p className="text-gray-400 text-xs mb-3">
                                Using Governance Guardian defaults and best practices.
                            </p>
                            <div className="space-y-1 text-xs text-left">
                                <div className="flex items-center space-x-2">
                                    <div className="w-1.5 h-1.5 bg-gray-500 rounded-full"></div>
                                    <span className="text-gray-400">Industry-standard naming</span>
                                </div>
                                <div className="flex items-center space-x-2">
                                    <div className="w-1.5 h-1.5 bg-gray-500 rounded-full"></div>
                                    <span className="text-gray-400">Common required tags</span>
                                </div>
                                <div className="flex items-center space-x-2">
                                    <div className="w-1.5 h-1.5 bg-gray-500 rounded-full"></div>
                                    <span className="text-gray-400">General compliance</span>
                                </div>
                            </div>
                        </div>
                    </div>
                </div>
            )}

            {/* Client preferences applied */}
            {(() => {
                const hasPreferences = !preferencesLoading && clientPreferences && (() => {
                    // Check if any meaningful preferences exist using the actual backend field names
                    const hasNamingScheme = clientPreferences.NamingScheme;
                    
                    // Check for tags using actual backend field names
                    const requiredTags = clientPreferences.RequiredTags;
                    const hasTags = requiredTags && Array.isArray(requiredTags) && requiredTags.length > 0;
                    
                    // Check for compliance using actual backend field names
                    const compliance = clientPreferences.ComplianceFrameworks;
                    const hasCompliance = compliance && Array.isArray(compliance) && compliance.length > 0;
                    
                    const hasPreferencesId = clientPreferences.ClientPreferencesId;
                    
                    return hasNamingScheme || hasTags || hasCompliance || hasPreferencesId;
                })();
                return hasPreferences;
            })() && (
                <div className="flex-1 overflow-y-auto">
                    <div className="space-y-3">
                    {/* Client Custom Preferences Header */}
                    <div className="bg-blue-600/10 border border-blue-600/30 rounded-lg p-3">
                        <div className="flex items-center space-x-2 mb-2">
                            <User size={14} className="text-blue-400" />
                            <h4 className="font-medium text-blue-400 text-sm">Client Custom Preferences</h4>
                            <span className="inline-flex items-center px-2 py-0.5 rounded-full text-xs font-medium bg-blue-600 text-white">
                                Active
                            </span>
                        </div>
                        <div className="space-y-1 text-xs">
                            <div className="flex items-start space-x-2">
                                <CheckCircle size={12} className="text-blue-400 mt-0.5 flex-shrink-0" />
                                <span className="text-blue-300">Custom naming patterns for this client</span>
                            </div>
                            <div className="flex items-start space-x-2">
                                <CheckCircle size={12} className="text-blue-400 mt-0.5 flex-shrink-0" />
                                <span className="text-blue-300">Client-specific required tags</span>
                            </div>
                            <div className="flex items-start space-x-2">
                                <CheckCircle size={12} className="text-blue-400 mt-0.5 flex-shrink-0" />
                                <span className="text-blue-300">Enhanced compliance frameworks</span>
                            </div>
                        </div>
                    </div>

                    {/* Custom Findings Count */}
                    {findings.filter(f => f.isClientSpecific || f.IsClientSpecific).length > 0 && (
                        <div className="bg-orange-600/10 border border-orange-600/30 rounded-lg p-2">
                            <div className="flex items-center space-x-2 mb-1">
                                <AlertTriangle size={12} className="text-orange-400" />
                                <h4 className="font-medium text-orange-400 text-xs">Client-Specific Issues</h4>
                            </div>
                            <p className="text-orange-300 text-xs">
                                {findings.filter(f => f.isClientSpecific || f.IsClientSpecific).length} issues found using custom standards.
                            </p>
                        </div>
                    )}

                    {/* Naming Convention Details */}
                    <div className="bg-gray-700/50 rounded-lg p-4">
                        <h4 className="font-medium text-gray-300 mb-4 flex items-center space-x-2 text-sm">
                            <Tag size={16} className="text-gray-400" />
                            <span>NAMING CONVENTION</span>
                        </h4>

                        {/* Naming Framework Visualization */}
                        {clientPreferences.NamingScheme ? (
                            <div className="space-y-4">
                                {/* Framework Title */}
                                <div className="text-center">
                                    <h5 className="text-sm font-medium text-blue-300 mb-2">Client Naming Framework</h5>
                                </div>
                                
                                {/* Visual Framework */}
                                <div className="bg-gray-800 border border-gray-600 rounded-lg p-4">
                                    {(() => {
                                        try {
                                            const config = clientPreferences.NamingScheme;
                                            const components = config?.Components || [];
                                            const separator = config?.Separator || '-';
                                            
                                            return (
                                                <div className="space-y-4">
                                                    {/* Component Layout */}
                                                    <div className="flex items-center justify-center space-x-2 flex-wrap gap-2">
                                                        {components.map((component, index) => (
                                                            <React.Fragment key={index}>
                                                                <div className="bg-gray-700 border border-gray-600 rounded-lg px-3 py-2 text-center min-w-[80px] flex-shrink-0 h-16 flex flex-col justify-center">
                                                                    <div className="text-sm font-mono text-green-400 font-medium mb-1">
                                                                        {`{${component.ComponentType.toLowerCase()}}`}
                                                                    </div>
                                                                    <div className="text-xs text-gray-400 truncate">
                                                                        {component.ComponentType === 'Company' ? 'Company' :
                                                                         component.ComponentType === 'Environment' ? 'Environment' :
                                                                         component.ComponentType === 'Service' ? 'Service' :
                                                                         component.ComponentType === 'ResourceType' ? 'Resource Type' :
                                                                         component.ComponentType === 'Instance' ? 'Instance' :
                                                                         component.ComponentType}
                                                                    </div>
                                                                    {component.IsRequired && (
                                                                        <div className="mt-1">
                                                                            <span className="inline-flex items-center px-1.5 py-0.5 rounded text-xs bg-red-600 text-white font-medium">req</span>
                                                                        </div>
                                                                    )}
                                                                </div>
                                                                {index < components.length - 1 && (
                                                                    <div className="flex-shrink-0 text-yellow-400 font-bold text-lg">
                                                                        {separator}
                                                                    </div>
                                                                )}
                                                            </React.Fragment>
                                                        ))}
                                                    </div>
                                                    
                                                    {/* Example Output */}
                                                    <div className="border-t border-gray-700 pt-3">
                                                        <div className="text-sm text-gray-400 mb-2 text-center font-medium">Example Output:</div>
                                                        <div className="bg-gray-900 border border-gray-600 rounded-lg px-4 py-3 text-center">
                                                            <span className="font-mono text-sm text-green-300 font-medium">
                                                                {(() => {
                                                                    const examples = {
                                                                        'company': 'silverfern',
                                                                        'environment': 'development', 
                                                                        'service': 'guardian',
                                                                        'resourcetype': 'sqldb',
                                                                        'instance': '01',
                                                                        'location': 'eus'
                                                                    };
                                                                    
                                                                    const separator = config?.Separator || '-';
                                                                    const caseFormat = config?.CaseFormat || 'lowercase';
                                                                    
                                                                    const componentValues = components.map(comp => {
                                                                        const compType = comp.ComponentType.toLowerCase().replace('type', 'type');
                                                                        return examples[compType] || comp.ComponentType.toLowerCase();
                                                                    });
                                                                    
                                                                    const example = componentValues.join(separator);
                                                                    
                                                                    // Apply case formatting
                                                                    switch (caseFormat) {
                                                                        case 'uppercase':
                                                                            return example.toUpperCase();
                                                                        case 'PascalCase':
                                                                            return example.split(separator)
                                                                                .map(part => part.charAt(0).toUpperCase() + part.slice(1).toLowerCase())
                                                                                .join(separator);
                                                                        case 'camelCase':
                                                                            const parts = example.split(separator);
                                                                            return parts[0].toLowerCase() + 
                                                                                parts.slice(1)
                                                                                    .map(part => part.charAt(0).toUpperCase() + part.slice(1).toLowerCase())
                                                                                    .join('');
                                                                        case 'lowercase':
                                                                        default:
                                                                            return example.toLowerCase();
                                                                    }
                                                                })()}
                                                            </span>
                                                        </div>
                                                    </div>
                                                </div>
                                            );
                                        } catch (e) {
                                            return (
                                                <div className="bg-gray-900 border border-gray-600 rounded-lg px-4 py-3 text-center">
                                                    <span className="font-mono text-sm text-green-400">Custom naming scheme configured</span>
                                                </div>
                                            );
                                        }
                                    })()} 
                                </div>
                            </div>
                        ) : (
                            <div className="bg-gray-800 border border-gray-600 rounded-lg p-4">
                                <div className="text-center">
                                    <h5 className="text-sm font-medium text-gray-300 mb-2">Standard Naming Patterns</h5>
                                    <p className="text-gray-400 text-sm mb-3">Using industry-standard naming conventions and validation rules</p>
                                    <div className="bg-gray-900 border border-gray-600 rounded-lg px-4 py-3">
                                        <span className="font-mono text-sm text-gray-400">
                                            [resourcetype]-[environment]-[instance] or similar patterns
                                        </span>
                                    </div>
                                </div>
                            </div>
                        )}
                    </div>

                    {/* Required Tags */}
                    <div className="bg-gray-700/50 rounded-lg p-3">
                        <h4 className="font-medium text-gray-300 mb-2 flex items-center space-x-2 text-xs">
                            <Tag size={12} className="text-gray-400" />
                            <span>REQUIRED TAGS</span>
                        </h4>
                        
                        {(() => {
                            // Get all unique tags from both RequiredTags and CustomTags
                            const requiredTags = clientPreferences.RequiredTags || [];
                            const customTags = clientPreferences.CustomTags || [];
                            const allTags = [...new Set([...requiredTags, ...customTags])];
                            
                            if (allTags.length === 0) {
                                return (
                                    <div className="text-center py-2">
                                        <p className="text-gray-400 text-xs">No specific tags required</p>
                                    </div>
                                );
                            }
                            
                            return (
                                <div className="space-y-2">
                                    <div className="flex items-center justify-between mb-2">
                                        <p className="text-gray-300 text-xs font-medium">Required Tags:</p>
                                        <span className="text-gray-400 text-xs">
                                            {allTags.length} tag{allTags.length !== 1 ? 's' : ''} • {clientPreferences.TaggingApproach || 'comprehensive'}
                                        </span>
                                    </div>
                                    <div className="flex flex-wrap gap-1">
                                        {allTags.map(tag => {
                                            const isCustom = (clientPreferences.CustomTags || []).includes(tag);
                                            return (
                                                <span 
                                                    key={tag} 
                                                    className={`inline-flex items-center px-2 py-1 rounded-full text-xs font-medium border ${
                                                        isCustom 
                                                            ? 'bg-purple-600/20 text-purple-300 border-purple-600/30' 
                                                            : 'bg-blue-600/20 text-blue-300 border-blue-600/30'
                                                    }`}
                                                >
                                                    {tag}
                                                    {isCustom && (
                                                        <span className="ml-1 text-purple-400 text-xs">★</span>
                                                    )}
                                                </span>
                                            );
                                        })}
                                    </div>
                                    {(clientPreferences.CustomTags || []).length > 0 && (
                                        <p className="text-purple-300 text-xs mt-2">
                                            ★ = Client-specific custom tags
                                        </p>
                                    )}
                                </div>
                            );
                        })()}
                    </div>

                    {/* Compliance Framework */}
                    <div className="bg-gray-700/50 rounded-lg p-4">
                        <h4 className="font-medium text-gray-300 mb-4 flex items-center space-x-2 text-sm">
                            <Shield size={16} className="text-gray-400" />
                            <span>COMPLIANCE FRAMEWORKS</span>
                        </h4>
                        
                        {(() => {
                            // Get compliance frameworks directly from backend response
                            let selectedCompliances = clientPreferences.ComplianceFrameworks;
                            
                            console.log('[PreferencesAppliedColumn] Raw ComplianceFrameworks:', selectedCompliances);
                            console.log('[PreferencesAppliedColumn] All preferences keys:', Object.keys(clientPreferences));
                            
                            // Backend already deserializes JSON to arrays in MapToResponse, no parsing needed
                            const hasCompliances = selectedCompliances && Array.isArray(selectedCompliances) && selectedCompliances.length > 0;
                            console.log('[PreferencesAppliedColumn] hasCompliances:', hasCompliances);
                            
                            if (hasCompliances) {
                                return (
                                    <div className="space-y-3">
                                        <div className="flex items-center justify-between">
                                            <p className="text-gray-300 text-sm font-medium">Active Frameworks:</p>
                                            <span className="inline-flex items-center px-2 py-1 rounded-full text-xs font-medium bg-green-600/20 text-green-300 border border-green-600/30">
                                                {selectedCompliances.length} Selected
                                            </span>
                                        </div>
                                        
                                        {/* Compliance Framework Grid */}
                                        <div className="grid grid-cols-1 sm:grid-cols-2 gap-2">
                                            {selectedCompliances.map(framework => (
                                                <div key={framework} className="bg-gray-800 border border-gray-600 rounded-lg p-3">
                                                    <div className="flex items-center space-x-2">
                                                        <div className="w-2 h-2 bg-purple-400 rounded-full flex-shrink-0"></div>
                                                        <span className="text-purple-300 text-sm font-medium">{framework}</span>
                                                    </div>
                                                    <div className="text-gray-400 text-xs mt-1 ml-4">
                                                        {/* Add descriptions for common frameworks */}
                                                        {framework === 'SOC 2' && 'Service Organization Control 2'}
                                                        {framework === 'PCI DSS' && 'Payment Card Industry Standard'}
                                                        {framework === 'HIPAA' && 'Healthcare Privacy Protection'}
                                                        {framework === 'ISO 27001' && 'Information Security Management'}
                                                        {framework === 'GDPR' && 'General Data Protection Regulation'}
                                                        {framework === 'FedRAMP' && 'Federal Risk Authorization Program'}
                                                        {framework === 'NIST' && 'National Institute of Standards'}
                                                        {framework === 'CIS Controls' && 'Center for Internet Security'}
                                                        {framework === 'FISMA' && 'Federal Information Security Act'}
                                                        {framework === 'SOX' && 'Sarbanes-Oxley Act'}
                                                        {!['SOC 2', 'PCI DSS', 'HIPAA', 'ISO 27001', 'GDPR', 'FedRAMP', 'NIST', 'CIS Controls', 'FISMA', 'SOX'].includes(framework) && 'Compliance framework'}
                                                    </div>
                                                </div>
                                            ))}
                                        </div>
                                    </div>
                                );
                            } else {
                                return (
                                    <div className="bg-gray-800 border border-gray-600 rounded-lg p-4">
                                        <div className="text-center">
                                            <Shield size={24} className="text-gray-500 mx-auto mb-2" />
                                            <h5 className="text-sm font-medium text-gray-300 mb-2">Standard Compliance</h5>
                                            <p className="text-gray-400 text-sm mb-3">
                                                Using industry-standard governance practices and general compliance guidelines.
                                            </p>
                                            <div className="bg-gray-900 border border-gray-600 rounded-lg px-4 py-3">
                                                <p className="text-gray-400 text-xs">
                                                    No specific compliance frameworks selected for this client.
                                                </p>
                                            </div>
                                        </div>
                                    </div>
                                );
                            }
                        })()}
                    </div>

                    {/* Assessment Impact - REMOVED */}
                    </div>
                </div>
            )}
        </div>
    );
};

const GovernanceOverviewTab = ({ assessment, findings, resources, resourceFilters, loading, error }) => {
    const { user } = useAuth();
    const { formatAssessmentDate } = useTimezone();
    
    const getScoreColor = (score) => {
        if (score >= 80) return 'text-green-400';
        if (score >= 60) return 'text-yellow-400';
        if (score >= 40) return 'text-orange-400';
        return 'text-red-400';
    };

    const getCriticalCount = () => findings.filter(f => (f.severity || f.Severity || '').toLowerCase() === 'critical').length;
    const getHighCount = () => findings.filter(f => (f.severity || f.Severity || '').toLowerCase() === 'high').length;

    if (loading) {
        return (
            <div className="text-center py-12">
                <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-yellow-600 mx-auto mb-4"></div>
                <p className="text-gray-400">Loading assessment data...</p>
            </div>
        );
    }

    if (error) {
        return (
            <div className="bg-red-900/20 border border-red-700 rounded-lg p-4">
                <div className="flex items-center space-x-2 mb-2">
                    <XCircle size={20} className="text-red-400" />
                    <p className="text-red-200 font-medium">Error Loading Data</p>
                </div>
                <p className="text-red-200">{error}</p>
            </div>
        );
    }

    return (
        <div className="space-y-6 h-full overflow-hidden">
            {/* Main Content Grid - Three Column Layout */}
            <div className="grid grid-cols-1 lg:grid-cols-3 gap-6 h-full min-h-0">
                {/* Assessment Details and Findings Summary - Left Column */}
                <div className="space-y-6 overflow-y-auto min-h-0">
                {/* Assessment Details */}
                <div className="bg-gray-800 rounded-lg p-6 border border-gray-700 flex-shrink-0">
                    <h3 className="text-lg font-semibold text-white mb-4 flex items-center space-x-2">
                        <Activity size={20} className="text-yellow-600" />
                        <span>Assessment Details</span>
                    </h3>
                    <div className="space-y-4">
                        <div className="flex items-center space-x-3">
                            <User size={16} className="text-blue-400" />
                            <div>
                                <p className="text-sm text-gray-400">Client</p>
                                <p className="text-white font-medium">{assessment.clientName || 'Unknown Client'}</p>
                            </div>
                        </div>
                        <div className="flex items-center space-x-3">
                            <Shield size={16} className="text-purple-400" />
                            <div>
                                <p className="text-sm text-gray-400">Assessment ID</p>
                                <p className="text-white font-mono text-xs">
                                    {assessment.assessmentId || assessment.AssessmentId || assessment.id || 'Unknown'}
                                </p>
                            </div>
                        </div>
                        <div className="flex items-center space-x-3">
                            <BarChart3 size={16} className="text-green-400" />
                            <div>
                                <p className="text-sm text-gray-400">Assessment Type</p>
                                <p className="text-white">
                                    {assessment.assessmentType || assessment.AssessmentType || 
                                     assessment.type || assessment.Type || 'Governance Assessment'}
                                </p>
                            </div>
                        </div>
                        <div className="flex items-center space-x-3">
                            <Calendar size={16} className="text-green-400" />
                            <div>
                                <p className="text-sm text-gray-400">Started</p>
                                <p className="text-white">
                                    {(() => {
                                        const startDate = assessment.startedDate || assessment.StartedDate || 
                                                         assessment.createdDate || assessment.CreatedDate ||
                                                         assessment.date || assessment.Date;
                                        return startDate ? formatAssessmentDate(startDate) : 'Unknown';
                                    })()}
                                </p>
                            </div>
                        </div>
                        <div className="flex items-center space-x-3">
                            <Clock size={16} className="text-orange-400" />
                            <div>
                                <p className="text-sm text-gray-400">Status</p>
                                <span className="inline-flex items-center px-3 py-1 rounded-full text-sm font-medium bg-green-700 text-white">
                                    Completed
                                </span>
                            </div>
                        </div>
                        <div className="flex items-center space-x-3">
                            <Server size={16} className="text-cyan-400" />
                            <div>
                                <p className="text-sm text-gray-400">Environment</p>
                                <p className="text-white">{assessment.environment || 'Production'}</p>
                            </div>
                        </div>
                        <div className="flex items-center space-x-3">
                            <Settings size={16} className="text-blue-400" />
                            <div>
                                <p className="text-sm text-gray-400">Client Preferences</p>
                                <div className="flex items-center space-x-2">
                                    {(assessment.useClientPreferences || assessment.UseClientPreferences) ? (
                                        <>
                                            <p className="text-blue-400 font-medium">Applied</p>
                                            <span className="inline-flex items-center px-2 py-1 rounded-full text-xs font-medium bg-blue-600/20 text-blue-300 border border-blue-600/30">
                                                Enhanced Standards
                                            </span>
                                        </>
                                    ) : (
                                        <p className="text-gray-400 font-medium">Standard Assessment</p>
                                    )}
                                </div>
                            </div>
                        </div>
                    </div>
                </div>

                {/* Findings Summary */}
                <div className="bg-gray-800 rounded-lg p-6 border border-gray-700 flex-shrink-0">
                    <h3 className="text-lg font-semibold text-white mb-4 flex items-center space-x-2">
                        <TrendingUp size={20} className="text-yellow-600" />
                        <span>Findings Summary</span>
                    </h3>
                    
                    <div className="space-y-4">
                        {/* Summary Stats */}
                        <div className="grid grid-cols-2 gap-4 mb-6">
                            {/* Resources Analyzed */}
                            <div className="bg-gray-700/50 rounded-lg p-4">
                                <div className="flex items-center space-x-3">
                                    <Server size={18} className="text-blue-400" />
                                    <div>
                                        <p className="text-3xl font-bold text-white">{assessment.resourceCount || resources.length}</p>
                                        <p className="text-sm text-gray-400">Resources Analyzed</p>
                                    </div>
                                </div>
                            </div>
                            
                            {/* Total Issues Found */}
                            <div className="bg-gray-700/50 rounded-lg p-4">
                                <div className="flex items-center space-x-3">
                                    <AlertTriangle size={18} className="text-red-400" />
                                    <div>
                                        <p className="text-3xl font-bold text-white">{findings.length}</p>
                                        <p className="text-sm text-gray-400">Total Issues Found</p>
                                    </div>
                                </div>
                            </div>
                        </div>
                        {/* Severity Distribution Chart */}
                        <div className="bg-gray-700/50 rounded-lg p-5">
                            <h4 className="text-lg font-medium text-gray-300 mb-4">Severity Distribution</h4>
                            <div className="flex items-center justify-center mb-6">
                                <div className="relative w-40 h-40">
                                    <svg viewBox="0 0 42 42" className="w-40 h-40 transform -rotate-90">
                                        {(() => {
                                            const severityData = ['Critical', 'High', 'Medium', 'Low'].map(severity => {
                                                const count = findings.filter(f => {
                                                    const findingSeverity = (f.severity || f.Severity || '').toLowerCase();
                                                    return findingSeverity === severity.toLowerCase();
                                                }).length;
                                                return { severity, count };
                                            });

                                            const total = severityData.reduce((sum, item) => sum + item.count, 0);
                                            const colors = ['#ef4444', '#f97316', '#eab308', '#3b82f6'];

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
                                            <div className="text-xl font-bold text-white">{findings.length}</div>
                                            <div className="text-sm text-gray-400">Total</div>
                                        </div>
                                    </div>
                                </div>
                            </div>
                            
                            {/* Legend */}
                            <div className="grid grid-cols-2 gap-3 text-sm">
                                {['Critical', 'High', 'Medium', 'Low'].map((severity, index) => {
                                    const count = findings.filter(f => {
                                        const findingSeverity = (f.severity || f.Severity || '').toLowerCase();
                                        return findingSeverity === severity.toLowerCase();
                                    }).length;
                                    const colors = ['#ef4444', '#f97316', '#eab308', '#3b82f6'];

                                    return (
                                        <div key={severity} className="flex items-center space-x-2">
                                            <div className="w-4 h-4 rounded-full" style={{ backgroundColor: colors[index] }}></div>
                                            <span className="text-gray-300">{severity}: {count}</span>
                                        </div>
                                    );
                                })}
                            </div>
                        </div>
                    </div>
                </div>
                </div>

                {/* Resource Types - Center Column */}
                <div className="bg-gray-800 rounded-lg p-6 border border-gray-700 flex flex-col h-full min-h-0 overflow-hidden">
                    <h3 className="text-lg font-semibold text-white mb-4 flex items-center space-x-2 flex-shrink-0">
                        <BarChart3 size={20} className="text-yellow-600" />
                        <span>Resource Types</span>
                        <span className="text-sm text-gray-400">
                            ({resourceFilters.ResourceTypes ? Object.keys(resourceFilters.ResourceTypes).length : 0})
                        </span>
                    </h3>

                    <div className="flex-1 overflow-y-auto space-y-2 min-h-0">
                        {resourceFilters.ResourceTypes && Object.entries(resourceFilters.ResourceTypes)
                            .sort(([, a], [, b]) => b - a)
                            .map(([type, count]) => {
                                const typeInfo = getResourceTypeInfo(type, 20); // Increase icon size to 20px
                                return (
                                    <div key={type} className="flex items-center justify-between p-2 bg-gray-700 rounded hover:bg-gray-600/50 transition-colors">
                                        <div className="flex items-center space-x-3 flex-1 min-w-0"> {/* Increase spacing */}
                                            {typeInfo.icon}
                                            <div className="min-w-0 flex-1">
                                                <div className="flex items-center space-x-2">
                                                    <p className="text-base font-medium text-white truncate">{typeInfo.name}</p> {/* Increase text size */}
                                                    <span className="inline-flex items-center px-1.5 py-0.5 rounded text-xs font-medium bg-gray-600 text-gray-300 shrink-0">
                                                        {typeInfo.category}
                                                    </span>
                                                </div>
                                            </div>
                                        </div>
                                        <div className="text-right ml-2 shrink-0">
                                            <span className="text-base font-bold text-white">{count}</span> {/* Increase count size */}
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

                {/* Preferences Applied - Right Column */}
                <PreferencesAppliedColumn assessment={assessment} findings={findings} />
            </div>
        </div>
    );
};

export default GovernanceOverviewTab;