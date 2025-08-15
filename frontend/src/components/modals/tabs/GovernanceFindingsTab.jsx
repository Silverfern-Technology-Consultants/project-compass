import React, { useState, useEffect, useCallback } from 'react';
import { CheckCircle, AlertTriangle, XCircle, ChevronDown, ChevronUp, Target, Zap, Info, Tag, Settings, BookOpen } from 'lucide-react';
import { apiClient } from '../../../services/apiService';
import ImproveDetectionModal from '../ImproveDetectionModal';

const GovernanceFindingsTab = ({ findings, loading, error, assessment }) => {
    const [expandedCategories, setExpandedCategories] = useState({});
    const [expandedFindings, setExpandedFindings] = useState({});
    const [clientPreferences, setClientPreferences] = useState(null);
    const [preferencesLoading, setPreferencesLoading] = useState(false);
    
    // Improve Detection Modal state
    const [improveDetectionModal, setImproveDetectionModal] = useState({
        isOpen: false,
        resourceName: '',
        unknownComponent: '',
        finding: null
    });

    const getSeverityIcon = (severity) => {
        const sev = severity?.toLowerCase();
        switch (sev) {
            case 'critical': return <AlertTriangle size={16} className="text-red-500" />;
            case 'high': return <XCircle size={16} className="text-red-400" />;
            case 'medium': return <AlertTriangle size={16} className="text-yellow-400" />;
            case 'low': return <CheckCircle size={16} className="text-blue-400" />;
            default: return <AlertTriangle size={16} className="text-gray-400" />;
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

    const loadClientPreferences = useCallback(async () => {
        if (!assessment?.clientId && !assessment?.ClientId) {
            setClientPreferences({});
            return;
        }

        setPreferencesLoading(true);
        try {
            const clientId = assessment.clientId || assessment.ClientId;
            const response = await apiClient.get(`/ClientPreferences/client/${clientId}`);
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
    }, [assessment?.clientId, assessment?.ClientId]);

    useEffect(() => {
        if (assessment) {
            loadClientPreferences();
        }
    }, [assessment, loadClientPreferences]);

    const parseNamingIssue = (issue) => {
        if (!issue) return { summary: 'Unknown issue', details: [] };
        
        const lines = issue.split(';').map(line => line.trim()).filter(line => line);
        const resourceName = lines[0] || 'Unknown resource';
        
        const details = [];
        let summary = 'Naming convention issues found';
        
        for (const line of lines) {
            // Handle missing components
            if (line.includes('Missing component:') || line.includes('Missing:')) {
                const missingMatch = line.match(/Missing component: Doesn't follow client naming scheme: (.+)/) || 
                                   line.match(/Missing: (.+)/);
                if (missingMatch) {
                    const missingComponents = missingMatch[1].split(', ').map(comp => comp.trim());
                    missingComponents.forEach(component => {
                        details.push({ 
                            type: 'missing', 
                            text: `Missing required component: '${component}'`
                        });
                    });
                    summary = `Missing required naming components: ${missingComponents.join(', ')}`;
                }
            }
            // Handle wrong position
            else if (line.includes('wrong position')) {
                const matches = line.match(/(\w+):\s+'([^']+)'\s+in wrong position \((\d+)\), should be position (\d+)/);
                if (matches) {
                    const [, component, value, currentPos, correctPos] = matches;
                    details.push({ 
                        type: 'position', 
                        text: `Component '${component}' (value: '${value}') is in position ${currentPos}, should be in position ${correctPos}` 
                    });
                    summary = `Component positioning errors found`;
                }
            }
            // Handle expected vs found mismatches
            else if (line.includes('Position') && line.includes('found')) {
                const matches = line.match(/Position (\d+): found '([^']+)' component but expected '([^']+)'/);
                if (matches) {
                    const [, position, found, expected] = matches;
                    details.push({ 
                        type: 'mismatch', 
                        text: `Position ${position}: Found '${found}' component but expected '${expected}'` 
                    });
                    summary = `Component type mismatches found`;
                }
            }
            // Handle generic naming scheme violations
            else if (line.includes("Doesn't follow client naming scheme")) {
                const componentMatch = line.match(/Doesn't follow client naming scheme: (.+)/);
                if (componentMatch) {
                    const components = componentMatch[1].split(', ').map(comp => comp.trim());
                    components.forEach(component => {
                        details.push({ 
                            type: 'violation', 
                            text: `Does not follow naming scheme for component: '${component}'`
                        });
                    });
                    summary = `Naming scheme violations: ${components.join(', ')}`;
                }
            }
        }
        
        // If no specific details were parsed, use the original issue as a fallback
        if (details.length === 0) {
            details.push({ 
                type: 'general', 
                text: issue
            });
        }
        
        return { summary, details, resourceName };
    };

    // Detect if a finding has unknown components that could benefit from interactive learning
    const hasUnknownComponents = (finding) => {
        const issue = finding.issue || finding.Issue || '';
        const resourceName = finding.resourceName || finding.ResourceName || '';
        
        // Look for common indicators of unknown components in naming convention findings
        const unknownIndicators = [
            'Unknown component',
            'unrecognized component',
            'Invalid component',
            'unexpected component',
            'not recognized',
            'unclear abbreviation'
        ];
        
        return unknownIndicators.some(indicator => 
            issue.toLowerCase().includes(indicator.toLowerCase())
        );
    };

    // Extract unknown component from finding text
    const extractUnknownComponent = (finding) => {
        const issue = finding.issue || finding.Issue || '';
        const resourceName = finding.resourceName || finding.ResourceName || '';
        
        // Try to extract component from issue text patterns
        const patterns = [
            /Unknown component[:\s]+['"]([^'"]+)['"]/i,
            /unrecognized component[:\s]+['"]([^'"]+)['"]/i,
            /Invalid component[:\s]+['"]([^'"]+)['"]/i,
            /component[:\s]+['"]([^'"]+)['"].*unknown/i
        ];
        
        for (const pattern of patterns) {
            const match = issue.match(pattern);
            if (match && match[1]) {
                return match[1];
            }
        }
        
        // Fallback: try to extract from resource name parts
        const parts = resourceName.split(/[-_.]/); 
        if (parts.length > 1) {
            // Return the part that's most likely unknown (usually middle parts)
            return parts.find(part => 
                part.length >= 2 && 
                part.length <= 10 && 
                !/^(dev|test|prod|staging|qa|uat|shared|vm|sql|kv|st|rg|01|02|03|001|002|003)$/i.test(part)
            ) || parts[1];
        }
        
        return '';
    };

    // Handle opening the improve detection modal
    const handleImproveDetection = (finding) => {
        const resourceName = finding.resourceName || finding.ResourceName || '';
        const unknownComponent = extractUnknownComponent(finding);
        
        setImproveDetectionModal({
            isOpen: true,
            resourceName,
            unknownComponent,
            finding
        });
    };

    // Handle saving the new mapping
    const handleSaveMapping = async (mapping) => {
        try {
            const clientId = assessment?.clientId || assessment?.ClientId;
            
            if (!clientId) {
                throw new Error('No client ID available');
            }

            // Get current preferences
            const currentPrefs = await apiClient.get(`/clientpreferences/client/${clientId}`);
            const existingData = currentPrefs.data;
            
            // Add the new service abbreviation
            const existingAbbreviations = existingData.ServiceAbbreviations || [];
            const newAbbreviation = {
                Abbreviation: mapping.abbreviation,
                FullName: mapping.fullName,
                CreatedDate: mapping.createdDate,
                CreatedBy: mapping.createdBy
            };
            
            const updatedAbbreviations = [...existingAbbreviations, newAbbreviation];
            
            // Update preferences with new abbreviation
            const updateData = {
                ...existingData,
                ServiceAbbreviations: updatedAbbreviations
            };
            
            await apiClient.post(`/clientpreferences/client/${clientId}`, updateData);
            
            // Reload client preferences to refresh the display
            await loadClientPreferences();
            
            console.log('Successfully saved service abbreviation mapping:', mapping);
            
        } catch (error) {
            console.error('Failed to save mapping:', error);
            throw error;
        }
    };

    // Close improve detection modal
    const handleCloseImproveDetection = () => {
        setImproveDetectionModal({
            isOpen: false,
            resourceName: '',
            unknownComponent: '',
            finding: null
        });
    };

    // Group findings by category
    const findingsByCategory = findings.reduce((acc, finding) => {
        const category = finding.category || finding.Category || 'Other';
        if (!acc[category]) acc[category] = [];
        acc[category].push(finding);
        return acc;
    }, {});

    if (loading) {
        return (
            <div className="text-center py-12">
                <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-yellow-600 mx-auto mb-4"></div>
                <p className="text-gray-400">Loading findings...</p>
            </div>
        );
    }

    if (error) {
        return (
            <div className="bg-red-900/20 border border-red-700 rounded-lg p-4">
                <div className="flex items-center space-x-2 mb-2">
                    <XCircle size={20} className="text-red-400" />
                    <p className="text-red-200 font-medium">Error Loading Findings</p>
                </div>
                <p className="text-red-200">{error}</p>
            </div>
        );
    }

    return (
        <div className="space-y-6">
            <div className="flex items-center justify-between">
                <h3 className="text-lg font-semibold text-white">
                    Governance Findings ({findings.length} total)
                </h3>
                <div className="flex space-x-2">
                    <button 
                        onClick={() => {
                            const allExpanded = Object.keys(findingsByCategory).every(cat => expandedCategories[cat] !== false);
                            const newState = {};
                            Object.keys(findingsByCategory).forEach(cat => { newState[cat] = !allExpanded; });
                            setExpandedCategories(newState);
                        }}
                        className="px-3 py-1 text-sm bg-gray-700 text-white rounded-lg hover:bg-gray-600 transition-colors"
                    >
                        {Object.keys(findingsByCategory).every(cat => expandedCategories[cat] !== false) ? 'Collapse All Categories' : 'Expand All Categories'}
                    </button>
                    <button 
                        onClick={() => {
                            const allFindingsExpanded = Object.keys(expandedFindings).length > 0 && Object.values(expandedFindings).every(v => v);
                            setExpandedFindings(allFindingsExpanded ? {} : 
                                Object.keys(findingsByCategory).reduce((acc, category, catIndex) => {
                                    findingsByCategory[category].forEach((_, findingIndex) => {
                                        acc[`${category}-${findingIndex}`] = true;
                                    });
                                    return acc;
                                }, {})
                            );
                        }}
                        className="px-3 py-1 text-sm bg-yellow-700 text-white rounded-lg hover:bg-yellow-600 transition-colors"
                    >
                        {Object.keys(expandedFindings).length > 0 && Object.values(expandedFindings).every(v => v) ? 'Collapse All Findings' : 'Expand All Findings'}
                    </button>
                </div>
            </div>

            {findings.length === 0 ? (
                <div className="text-center py-12">
                    <CheckCircle size={48} className="text-green-400 mx-auto mb-4" />
                    <h3 className="text-lg font-semibold text-white mb-2">No Issues Found</h3>
                    <p className="text-gray-400">This assessment found no governance issues.</p>
                </div>
            ) : (
                <div className="space-y-4">
                    {Object.entries(findingsByCategory).map(([category, categoryFindings]) => {
                        const isExpanded = expandedCategories[category] !== false;
                        const severityCounts = categoryFindings.reduce((acc, finding) => {
                            const severity = (finding.severity || finding.Severity || 'Medium').toLowerCase();
                            acc[severity] = (acc[severity] || 0) + 1;
                            return acc;
                        }, {});

                        return (
                            <div key={category} className="bg-gray-800 rounded-lg border border-gray-700 overflow-hidden">
                                <div 
                                    className="p-4 cursor-pointer hover:bg-gray-700/50 transition-colors border-b border-gray-700"
                                    onClick={() => setExpandedCategories(prev => ({ ...prev, [category]: !isExpanded }))}
                                >
                                    <div className="flex items-center justify-between">
                                        <div className="flex items-center space-x-3">
                                            {isExpanded ? <ChevronUp size={18} className="text-gray-400" /> : <ChevronDown size={18} className="text-gray-400" />}
                                            <div>
                                                <h3 className="text-lg font-semibold text-white capitalize flex items-center space-x-2">
                                                    <span>{category}</span>
                                                    <span className="text-sm text-gray-400">({categoryFindings.length} issues)</span>
                                                </h3>
                                                <div className="flex items-center space-x-4 text-sm text-gray-400 mt-1">
                                                    {severityCounts.critical && <span className="text-red-400">{severityCounts.critical} Critical</span>}
                                                    {severityCounts.high && <span className="text-red-400">{severityCounts.high} High</span>}
                                                    {severityCounts.medium && <span className="text-yellow-400">{severityCounts.medium} Medium</span>}
                                                    {severityCounts.low && <span className="text-blue-400">{severityCounts.low} Low</span>}
                                                </div>
                                            </div>
                                        </div>
                                    </div>
                                </div>

                                {isExpanded && (
                                    <div className="space-y-4">
                                        {/* Show Naming Scheme for NamingConvention category */}
                                        {category === 'NamingConvention' && (
                                            <div className="bg-gray-700/30 border-t border-gray-600 p-4">
                                                <h4 className="text-white text-sm font-medium mb-3 flex items-center space-x-2">
                                                    <Tag size={16} className="text-yellow-600" />
                                                    <span>Current Naming Scheme</span>
                                                    {preferencesLoading && (
                                                        <div className="animate-spin rounded-full h-4 w-4 border-b-2 border-yellow-600 ml-2"></div>
                                                    )}
                                                </h4>
                                                
                                                {/* Naming Scheme Display */}
                                                {clientPreferences?.NamingScheme ? (
                                                    <div className="space-y-3">
                                                        <div className="text-xs text-blue-300 mb-2">
                                                            <Settings size={12} className="inline mr-1" />
                                                            Client Custom Naming Scheme
                                                        </div>
                                                        
                                                        {/* Visual Framework */}
                                                        <div className="bg-gray-800 border border-gray-600 rounded-lg p-3">
                                                            {(() => {
                                                                try {
                                                                    const config = clientPreferences.NamingScheme;
                                                                    const components = config?.Components || [];
                                                                    const separator = config?.Separator || '-';
                                                                    
                                                                    return (
                                                                        <div className="space-y-3">
                                                                            {/* Component Layout */}
                                                                            <div className="flex items-center justify-center space-x-1 flex-wrap gap-1">
                                                                                {components.map((component, index) => (
                                                                                    <React.Fragment key={index}>
                                                                                        <div className="bg-gray-700 border border-gray-600 rounded px-2 py-1 text-center min-w-[60px] flex-shrink-0">
                                                                                            <div className="text-xs font-mono text-green-400 font-medium">
                                                                                                {(() => {
                                                                                                    const componentType = component.ComponentType.toLowerCase();
                                                                                                    if (componentType === 'resourcetype') {
                                                                                                        return 'vm'; // Show actual resource type example
                                                                                                    }
                                                                                                    return `{${componentType}}`;
                                                                                                })()}
                                                                                            </div>
                                                                                            {component.IsRequired && (
                                                                                                <div className="mt-0.5">
                                                                                                    <span className="inline-flex items-center px-1 py-0.5 rounded text-xs bg-red-600 text-white font-medium">req</span>
                                                                                                </div>
                                                                                            )}
                                                                                        </div>
                                                                                        {index < components.length - 1 && (
                                                                                            <div className="flex-shrink-0 text-yellow-400 font-bold">
                                                                                                {separator}
                                                                                            </div>
                                                                                        )}
                                                                                    </React.Fragment>
                                                                                ))}
                                                                            </div>
                                                                            
                                                                            {/* Example Output */}
                                                                            <div className="border-t border-gray-700 pt-2">
                                                                                <div className="text-xs text-gray-400 mb-1 text-center">Example:</div>
                                                                                <div className="bg-gray-900 border border-gray-600 rounded px-3 py-2 text-center">
                                                                                    <span className="font-mono text-xs text-green-300 font-medium">
                                                                                        {(() => {
                                                                                            const examples = {
                                                                                                'company': 'contoso',
                                                                                                'environment': 'dev', 
                                                                                                'service': 'web',
                                                                                                'resourcetype': 'vm',
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
                                                                        <div className="bg-gray-900 border border-gray-600 rounded px-3 py-2 text-center">
                                                                            <span className="font-mono text-xs text-green-400">Custom naming scheme configured</span>
                                                                        </div>
                                                                    );
                                                                }
                                                            })()} 
                                                        </div>
                                                    </div>
                                                ) : (
                                                    <div className="space-y-2">
                                                        <div className="text-xs text-gray-400 mb-2">
                                                            <Settings size={12} className="inline mr-1" />
                                                            Standard Assessment Mode
                                                        </div>
                                                        <div className="bg-gray-800 border border-gray-600 rounded-lg p-3">
                                                            <div className="text-center">
                                                                <p className="text-gray-300 text-xs mb-2">Using industry-standard naming patterns</p>
                                                                <div className="bg-gray-900 border border-gray-600 rounded px-3 py-2">
                                                                    <span className="font-mono text-xs text-gray-400">
                                                                        vm-prod-01, sql-dev-02, web-test-03
                                                                    </span>
                                                                </div>
                                                                <p className="text-gray-400 text-xs mt-2">Examples: Virtual Machine, SQL Database, Web App</p>
                                                            </div>
                                                        </div>
                                                    </div>
                                                )}
                                            </div>
                                        )}
                                        
                                        {/* Individual Findings */}
                                        <div className="p-4 space-y-3">
                                        {categoryFindings.map((finding, index) => {
                                            const findingKey = `${category}-${index}`;
                                            const isExpanded = expandedFindings[findingKey];
                                            const issue = finding.issue || finding.Issue || '';
                                            const parsedIssue = category === 'NamingConvention' ? parseNamingIssue(issue) : null;
                                            
                                            return (
                                                <div key={index} className="border border-gray-600 rounded-lg overflow-hidden hover:border-gray-500 transition-colors">
                                                    <div 
                                                        className="p-4 cursor-pointer hover:bg-gray-700/30 transition-colors"
                                                        onClick={() => setExpandedFindings(prev => ({ ...prev, [findingKey]: !isExpanded }))}
                                                    >
                                                        <div className="flex items-start justify-between">
                                                            <div className="flex items-start space-x-3 flex-1">
                                                                <div className="flex items-center space-x-2">
                                                                    {isExpanded ? <ChevronUp size={16} className="text-gray-400" /> : <ChevronDown size={16} className="text-gray-400" />}
                                                                    {getSeverityIcon(finding.severity || finding.Severity)}
                                                                </div>
                                                                <div className="flex-1">
                                                                    <h5 className="font-medium text-white text-sm mb-1">
                                                                        {finding.resourceName || finding.ResourceName}
                                                                    </h5>
                                                                    <p className="text-gray-400 text-sm mb-2">
                                                                        {parsedIssue ? parsedIssue.summary : (issue.length > 100 ? `${issue.substring(0, 100)}...` : issue)}
                                                                    </p>
                                                                </div>
                                                            </div>
                                                            <div className="flex flex-col items-end space-y-2">
                                                                <div className="flex items-center space-x-2">
                                                                    <span className={`px-2 py-1 rounded-full text-xs font-medium border ${
                                                                        getSeverityColor(finding.severity || finding.Severity)
                                                                    }`}>
                                                                        {finding.severity || finding.Severity || 'Medium'}
                                                                        <span className="ml-1 text-xs opacity-75">(Priority)</span>
                                                                    </span>
                                                                </div>
                                                                {(finding.estimatedEffort || finding.EstimatedEffort) && (
                                                                    <div className="flex items-center space-x-1 text-xs text-gray-400">
                                                                        <Zap size={12} className="text-yellow-400" />
                                                                        <span>{finding.estimatedEffort || finding.EstimatedEffort}</span>
                                                                        <span className="ml-1 opacity-75">(Effort)</span>
                                                                    </div>
                                                                )}
                                                                
                                                                {/* Improve Detection Button for unknown components */}
                                                                {category === 'NamingConvention' && hasUnknownComponents(finding) && (
                                                                    <div className="mt-3">
                                                                        <button
                                                                            onClick={(e) => {
                                                                                e.stopPropagation();
                                                                                handleImproveDetection(finding);
                                                                            }}
                                                                            className="flex items-center space-x-2 px-3 py-2 bg-blue-600 hover:bg-blue-700 text-white text-sm rounded-lg transition-colors"
                                                                        >
                                                                            <BookOpen size={14} />
                                                                            <span>Improve Detection</span>
                                                                        </button>
                                                                        <p className="text-xs text-gray-400 mt-1">
                                                                            Teach the system about this abbreviation to improve future assessments
                                                                        </p>
                                                                    </div>
                                                                )}
                                                            </div>
                                                        </div>
                                                    </div>
                                                    
                                                    {isExpanded && (
                                                        <div className="border-t border-gray-600 bg-gray-800/50">
                                                            <div className="p-4 space-y-4">
                                                                <div>
                                                                    <h6 className="text-white text-sm font-medium mb-2 flex items-center space-x-2">
                                                                        <Info size={14} className="text-blue-400" />
                                                                        <span>Issue Details</span>
                                                                    </h6>
                                                                    {parsedIssue && parsedIssue.details.length > 0 ? (
                                                                        <div className="space-y-2">
                                                                            {parsedIssue.details.map((detail, detailIndex) => (
                                                                                <div key={detailIndex} className="bg-gray-700/50 rounded p-3">
                                                                                    <div className="flex items-start space-x-2">
                                                                                        <div className={`w-2 h-2 rounded-full mt-2 flex-shrink-0 ${
                                                                                            detail.type === 'missing' ? 'bg-red-500' :
                                                                                            detail.type === 'position' ? 'bg-yellow-500' :
                                                                                            detail.type === 'mismatch' ? 'bg-blue-500' :
                                                                                            detail.type === 'violation' ? 'bg-orange-500' :
                                                                                            'bg-gray-500'
                                                                                        }`}></div>
                                                                                        <p className="text-gray-300 text-sm">{detail.text}</p>
                                                                                    </div>
                                                                                </div>
                                                                            ))}
                                                                        </div>
                                                                    ) : (
                                                                        <div className="bg-gray-700/50 rounded p-3">
                                                                            <p className="text-gray-300 text-sm">{issue}</p>
                                                                        </div>
                                                                    )}
                                                                </div>
                                                                
                                                                {(finding.recommendation || finding.Recommendation) && (
                                                                    <div>
                                                                        <h6 className="text-blue-300 text-sm font-medium mb-2 flex items-center space-x-2">
                                                                            <Target size={14} className="text-blue-400" />
                                                                            <span>Recommendation</span>
                                                                        </h6>
                                                                        <div className="bg-blue-900/20 border border-blue-800 rounded p-3">
                                                                            <p className="text-gray-300 text-sm">
                                                                                {finding.recommendation || finding.Recommendation}
                                                                            </p>
                                                                        </div>
                                                                    </div>
                                                                )}
                                                            </div>
                                                        </div>
                                                    )}
                                                </div>
                                            );
                                        })}
                                        </div>
                                    </div>
                                )}
                            </div>
                        );
                    })}
                </div>
            )}

            {/* Improve Detection Modal */}
            <ImproveDetectionModal
                isOpen={improveDetectionModal.isOpen}
                onClose={handleCloseImproveDetection}
                resourceName={improveDetectionModal.resourceName}
                unknownComponent={improveDetectionModal.unknownComponent}
                onSaveMapping={handleSaveMapping}
                client={{ Name: assessment?.clientName || 'Unknown Client' }}
            />
        </div>
    );
};

export default GovernanceFindingsTab;