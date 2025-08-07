import React, { useState, useEffect } from 'react';
import { createPortal } from 'react-dom';
import { X, Shield, Lock, Server, AlertTriangle, CheckCircle, Download, Eye, Network, Database, Activity, Users, Target, Cloud } from 'lucide-react';
import { assessmentApi } from '../../services/apiService';

const SecurityPostureDetailModal = ({ isOpen, onClose, assessment }) => {
    const [assessmentResults, setAssessmentResults] = useState(null);
    const [findings, setFindings] = useState([]);
    const [loading, setLoading] = useState(false);
    const [error, setError] = useState('');
    const [activeTab, setActiveTab] = useState('overview');

    useEffect(() => {
        if (isOpen && assessment) {
            loadAssessmentDetails();
        }
    }, [isOpen, assessment]);

    const loadAssessmentDetails = async () => {
        try {
            setLoading(true);
            setError('');

            const assessmentId = assessment.id || assessment.assessmentId || assessment.AssessmentId;
            console.log('[SecurityPostureDetailModal] Loading details for assessment:', assessmentId);
            
            // Try results endpoint first (since assessment is completed)
            try {
                const resultsResponse = await assessmentApi.getAssessmentResults(assessmentId);
                console.log('[SecurityPostureDetailModal] Results response:', resultsResponse);
                setAssessmentResults(resultsResponse);
            } catch (resultsError) {
                console.warn('[SecurityPostureDetailModal] Failed to get results, trying fallback:', resultsError);
                // Fallback to basic assessment info
                const basicResponse = await assessmentApi.getAssessmentStatus(assessmentId);
                console.log('[SecurityPostureDetailModal] Basic response:', basicResponse);
                setAssessmentResults(basicResponse);
            }

            // Load findings
            const findingsResponse = await assessmentApi.getAssessmentFindings(assessmentId);
            console.log('[SecurityPostureDetailModal] Findings response:', findingsResponse);
            
            // Filter for Security-related findings
            const securityFindings = (findingsResponse || []).filter(f => 
                f.category === 'SecurityPosture' || 
                f.Category === 'SecurityPosture' ||
                f.category === 'Security' || 
                f.category === 'Network' ||
                f.category === 'DataEncryption' ||
                f.category === 'AdvancedThreatProtection'
            );
            
            console.log('[SecurityPostureDetailModal] Filtered security findings:', securityFindings.length);
            setFindings(securityFindings);
            
        } catch (err) {
            console.error('[SecurityPostureDetailModal] Failed to load assessment details:', err);
            setError('Failed to load security assessment details. Please try again.');
        } finally {
            setLoading(false);
        }
    };

    const getSeverityColor = (severity) => {
        const severityLower = severity?.toLowerCase();
        switch (severityLower) {
            case 'critical': return 'text-red-400 bg-red-400/10 border-red-400/20';
            case 'high': return 'text-orange-400 bg-orange-400/10 border-orange-400/20';
            case 'medium': return 'text-yellow-400 bg-yellow-400/10 border-yellow-400/20';
            case 'low': return 'text-blue-400 bg-blue-400/10 border-blue-400/20';
            default: return 'text-gray-400 bg-gray-400/10 border-gray-400/20';
        }
    };

    const formatAssessmentTitle = () => {
        const assessmentId = assessment?.id || assessment?.assessmentId || assessment?.AssessmentId || 'UNKNOWN';
        const shortId = assessmentId.toString().substring(0, 8).toUpperCase();
        const userEnteredName = assessment?.name || assessment?.Name || 'Security Posture Assessment';
        return `${userEnteredName} - ${shortId}`;
    };

    const formatDuration = (startDate, endDate) => {
        if (!startDate || !endDate) return 'N/A';
        
        const start = new Date(startDate);
        const end = new Date(endDate);
        const durationMs = end - start;
        
        if (durationMs < 1000) return `${durationMs}ms`;
        if (durationMs < 60000) return `${Math.round(durationMs / 1000)}s`;
        return `${Math.round(durationMs / 60000)}m ${Math.round((durationMs % 60000) / 1000)}s`;
    };

    const getResourceTypeIcon = (resourceType) => {
        const type = resourceType?.toLowerCase();
        if (type?.includes('network')) return Network;
        if (type?.includes('encryption') || type?.includes('dataencryption')) return Lock;
        if (type?.includes('defender') || type?.includes('threat')) return Shield;
        if (type?.includes('security')) return Shield;
        if (type?.includes('oauth')) return Users;
        if (type?.includes('incident')) return AlertTriangle;
        return Database;
    };

    if (!isOpen) return null;

    return createPortal(
        <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center p-4 z-50">
            <div className="bg-gray-800 rounded-lg shadow-xl w-full max-w-6xl max-h-[90vh] overflow-y-auto">
                {/* Header */}
                <div className="flex items-center justify-between p-6 border-b border-gray-700">
                    <div className="flex items-center space-x-3">
                        <Shield className="text-red-400" size={24} />
                        <div>
                            <h2 className="text-xl font-semibold text-white">{formatAssessmentTitle()}</h2>
                            <p className="text-gray-400 text-sm">Security Posture Assessment</p>
                        </div>
                    </div>
                    <button
                        onClick={onClose}
                        className="text-gray-400 hover:text-white transition-colors"
                    >
                        <X size={24} />
                    </button>
                </div>

                {/* Content */}
                <div className="p-6">
                    {error && (
                        <div className="mb-4 p-3 bg-red-500/10 border border-red-500/20 rounded-lg flex items-center">
                            <AlertTriangle className="text-red-400 mr-2" size={16} />
                            <span className="text-red-400 text-sm">{error}</span>
                        </div>
                    )}

                    {loading ? (
                        <div className="flex items-center justify-center py-12">
                            <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-yellow-500"></div>
                            <span className="ml-3 text-gray-300">Loading assessment details...</span>
                        </div>
                    ) : (
                        <>
                            {/* Score Summary */}
                            <div className="mb-6 bg-gray-700/30 rounded-lg p-4">
                                <div className="flex items-center justify-between">
                                    <div>
                                        <h3 className="text-lg font-medium text-white mb-1">Security Posture Assessment</h3>
                                        <p className="text-gray-400 text-sm">Comprehensive security evaluation across all Azure resources</p>
                                    </div>
                                    <div className="text-right">
                                        <div className="text-3xl font-bold text-red-400">
                                            {assessmentResults?.IssuesFound || assessment?.IssuesFound || 0}
                                        </div>
                                        <p className="text-gray-400 text-sm">security issues found</p>
                                    </div>
                                </div>
                                
                                {/* Quick Stats */}
                                <div className="grid grid-cols-1 md:grid-cols-4 gap-4 mt-4">
                                    <div className="bg-gray-800/50 rounded-lg p-3">
                                        <div className="flex items-center justify-between">
                                            <div>
                                                <p className="text-xs text-gray-400">Resources Analyzed</p>
                                                <p className="text-lg font-semibold text-white">
                                                    {assessmentResults?.TotalResourcesAnalyzed || assessment?.TotalResourcesAnalyzed || 0}
                                                </p>
                                            </div>
                                            <Database className="text-blue-400" size={18} />
                                        </div>
                                    </div>
                                    
                                    <div className="bg-gray-800/50 rounded-lg p-3">
                                        <div className="flex items-center justify-between">
                                            <div>
                                                <p className="text-xs text-gray-400">Critical Issues</p>
                                                <p className="text-lg font-semibold text-red-400">
                                                    {assessmentResults?.DetailedMetrics?.SeverityDistribution?.Critical || 0}
                                                </p>
                                            </div>
                                            <AlertTriangle className="text-red-400" size={18} />
                                        </div>
                                    </div>
                                    
                                    <div className="bg-gray-800/50 rounded-lg p-3">
                                        <div className="flex items-center justify-between">
                                            <div>
                                                <p className="text-xs text-gray-400">High Severity</p>
                                                <p className="text-lg font-semibold text-orange-400">
                                                    {assessmentResults?.DetailedMetrics?.SeverityDistribution?.High || 0}
                                                </p>
                                            </div>
                                            <Shield className="text-orange-400" size={18} />
                                        </div>
                                    </div>
                                    
                                    <div className="bg-gray-800/50 rounded-lg p-3">
                                        <div className="flex items-center justify-between">
                                            <div>
                                                <p className="text-xs text-gray-400">Duration</p>
                                                <p className="text-lg font-semibold text-white">
                                                    {formatDuration(assessmentResults?.StartedDate || assessment?.StartedDate, assessmentResults?.CompletedDate || assessment?.CompletedDate)}
                                                </p>
                                            </div>
                                            <Activity className="text-green-400" size={18} />
                                        </div>
                                    </div>
                                </div>
                            </div>

                            {/* Tabs */}
                            <div className="mb-6">
                                <div className="border-b border-gray-700">
                                    <nav className="-mb-px flex space-x-8">
                                        {[
                                            { id: 'overview', name: 'Overview', icon: Eye },
                                            { id: 'categories', name: 'By Category', icon: Target },
                                            { id: 'severity', name: 'By Severity', icon: AlertTriangle },
                                            { id: 'findings', name: 'All Findings', icon: Shield }
                                        ].map((tab) => (
                                            <button
                                                key={tab.id}
                                                onClick={() => setActiveTab(tab.id)}
                                                className={`flex items-center space-x-2 py-2 px-1 border-b-2 font-medium text-sm ${
                                                    activeTab === tab.id
                                                        ? 'border-yellow-500 text-yellow-400'
                                                        : 'border-transparent text-gray-400 hover:text-gray-300'
                                                }`}
                                            >
                                                <tab.icon size={16} />
                                                <span>{tab.name}</span>
                                                {tab.id === 'findings' && findings.length > 0 && (
                                                    <span className="bg-red-500 text-white text-xs rounded-full px-2 py-0.5">
                                                        {findings.length}
                                                    </span>
                                                )}
                                            </button>
                                        ))}
                                    </nav>
                                </div>
                            </div>

                            {/* Tab Content */}
                            {activeTab === 'overview' && (
                                <div className="space-y-6">
                                    {/* Assessment Details */}
                                    <div className="bg-gray-700/30 rounded-lg p-4">
                                        <h4 className="font-medium text-white mb-3">Assessment Information</h4>
                                        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                                            <div className="space-y-2">
                                                <div className="flex justify-between">
                                                    <span className="text-gray-400">Assessment Type:</span>
                                                    <span className="text-white">{assessmentResults?.Type === 13 ? 'Security: Full' : 'Security Assessment'}</span>
                                                </div>
                                                <div className="flex justify-between">
                                                    <span className="text-gray-400">Status:</span>
                                                    <span className="text-green-400">{assessmentResults?.Status === 2 ? 'Completed' : 'Unknown'}</span>
                                                </div>
                                                <div className="flex justify-between">
                                                    <span className="text-gray-400">Started:</span>
                                                    <span className="text-white">{assessmentResults?.StartedDate ? new Date(assessmentResults.StartedDate).toLocaleString() : 'N/A'}</span>
                                                </div>
                                                <div className="flex justify-between">
                                                    <span className="text-gray-400">Completed:</span>
                                                    <span className="text-white">{assessmentResults?.CompletedDate ? new Date(assessmentResults.CompletedDate).toLocaleString() : 'N/A'}</span>
                                                </div>
                                            </div>
                                            <div className="space-y-2">
                                                <div className="flex justify-between">
                                                    <span className="text-gray-400">Duration:</span>
                                                    <span className="text-white">{formatDuration(assessmentResults?.StartedDate, assessmentResults?.CompletedDate)}</span>
                                                </div>
                                                <div className="flex justify-between">
                                                    <span className="text-gray-400">Client:</span>
                                                    <span className="text-white">{assessment?.ClientName || assessment?.clientName || 'N/A'}</span>
                                                </div>
                                                <div className="flex justify-between">
                                                    <span className="text-gray-400">Resources Analyzed:</span>
                                                    <span className="text-white">{assessmentResults?.TotalResourcesAnalyzed || 0}</span>
                                                </div>
                                                <div className="flex justify-between">
                                                    <span className="text-gray-400">Issues Found:</span>
                                                    <span className="text-red-400">{assessmentResults?.IssuesFound || 0}</span>
                                                </div>
                                            </div>
                                        </div>
                                    </div>

                                    {/* Security Category Breakdown */}
                                    {assessmentResults?.DetailedMetrics?.ResourceTypeDistribution && (
                                        <div className="bg-gray-700/30 rounded-lg p-4">
                                            <h4 className="font-medium text-white mb-3">Security Areas Evaluated</h4>
                                            <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                                                {Object.entries(assessmentResults.DetailedMetrics.ResourceTypeDistribution).map(([type, count]) => {
                                                    const Icon = getResourceTypeIcon(type);
                                                    return (
                                                        <div key={type} className="flex items-center justify-between p-3 bg-gray-800/50 rounded-lg">
                                                            <div className="flex items-center space-x-2">
                                                                <Icon className="text-blue-400" size={16} />
                                                                <span className="text-gray-300 text-sm">{type.replace(/([A-Z])/g, ' $1').trim()}</span>
                                                            </div>
                                                            <span className="text-white font-medium">{count}</span>
                                                        </div>
                                                    );
                                                })}
                                            </div>
                                        </div>
                                    )}

                                    {/* Recommendations Summary */}
                                    {assessmentResults?.Recommendations && assessmentResults.Recommendations.length > 0 && (
                                        <div className="bg-gray-700/30 rounded-lg p-4">
                                            <h4 className="font-medium text-white mb-3">Key Recommendations</h4>
                                            {assessmentResults.Recommendations.map((recommendation, index) => (
                                                <div key={index} className="mb-4 last:mb-0 p-3 bg-gray-800/50 rounded-lg">
                                                    <div className="flex items-center justify-between mb-2">
                                                        <h5 className="font-medium text-white">{recommendation.Title}</h5>
                                                        <span className={`px-2 py-1 rounded-full text-xs font-medium ${
                                                            recommendation.Priority === 'High' ? 'bg-red-400/10 text-red-400' :
                                                            recommendation.Priority === 'Medium' ? 'bg-yellow-400/10 text-yellow-400' :
                                                            'bg-blue-400/10 text-blue-400'
                                                        }`}>
                                                            {recommendation.Priority} Priority
                                                        </span>
                                                    </div>
                                                    <p className="text-gray-300 text-sm mb-2">{recommendation.Description}</p>
                                                    <div className="flex items-center justify-between text-xs text-gray-400">
                                                        <span>Effort: {recommendation.EstimatedEffort}</span>
                                                        <span>{recommendation.AffectedResources?.length || 0} affected resources</span>
                                                    </div>
                                                </div>
                                            ))}
                                        </div>
                                    )}
                                </div>
                            )}

                            {activeTab === 'categories' && (
                                <div className="space-y-4">
                                    {assessmentResults?.DetailedMetrics?.ResourceTypeDistribution ? (
                                        Object.entries(assessmentResults.DetailedMetrics.ResourceTypeDistribution).map(([category, count]) => {
                                            const categoryFindings = findings.filter(f => 
                                                (f.ResourceType || f.resourceType)?.toLowerCase().includes(category.toLowerCase())
                                            );
                                            const Icon = getResourceTypeIcon(category);
                                            
                                            return (
                                                <div key={category} className="bg-gray-700/30 rounded-lg p-4">
                                                    <div className="flex items-center justify-between mb-4">
                                                        <div className="flex items-center space-x-3">
                                                            <Icon className="text-blue-400" size={24} />
                                                            <div>
                                                                <h4 className="font-medium text-white">{category.replace(/([A-Z])/g, ' $1').trim()}</h4>
                                                                <p className="text-gray-400 text-sm">{count} findings in this category</p>
                                                            </div>
                                                        </div>
                                                        <span className="bg-blue-500/10 text-blue-400 px-3 py-1 rounded-full text-sm font-medium">
                                                            {count}
                                                        </span>
                                                    </div>
                                                    
                                                    {categoryFindings.length > 0 && (
                                                        <div className="space-y-2">
                                                            {categoryFindings.slice(0, 3).map((finding, index) => (
                                                                <div key={index} className="bg-gray-800/50 rounded p-3">
                                                                    <div className="flex items-center justify-between mb-2">
                                                                        <span className="text-white text-sm font-medium">{finding.ResourceName || finding.resourceName}</span>
                                                                        <span className={`px-2 py-1 rounded-full text-xs font-medium ${getSeverityColor(finding.Severity || finding.severity)}`}>
                                                                            {finding.Severity || finding.severity}
                                                                        </span>
                                                                    </div>
                                                                    <p className="text-gray-300 text-xs">{(finding.Issue || finding.issue)?.substring(0, 100)}...</p>
                                                                </div>
                                                            ))}
                                                            {categoryFindings.length > 3 && (
                                                                <p className="text-gray-400 text-xs text-center py-2">
                                                                    +{categoryFindings.length - 3} more findings in this category
                                                                </p>
                                                            )}
                                                        </div>
                                                    )}
                                                </div>
                                            );
                                        })
                                    ) : (
                                        <div className="text-center py-8">
                                            <Target className="text-gray-600 mx-auto mb-3" size={48} />
                                            <h3 className="text-lg font-medium text-white mb-2">Category Analysis</h3>
                                            <p className="text-gray-400">No category breakdown available for this assessment.</p>
                                        </div>
                                    )}
                                </div>
                            )}

                            {activeTab === 'severity' && (
                                <div className="space-y-4">
                                    {assessmentResults?.DetailedMetrics?.SeverityDistribution ? (
                                        ['Critical', 'High', 'Medium', 'Low'].map(severity => {
                                            const count = assessmentResults.DetailedMetrics.SeverityDistribution[severity] || 0;
                                            const severityFindings = findings.filter(f => 
                                                (f.Severity || f.severity) === severity
                                            );
                                            
                                            if (count === 0) return null;
                                            
                                            return (
                                                <div key={severity} className="bg-gray-700/30 rounded-lg p-4">
                                                    <div className="flex items-center justify-between mb-4">
                                                        <div className="flex items-center space-x-3">
                                                            <AlertTriangle className={`${getSeverityColor(severity).includes('red') ? 'text-red-400' : 
                                                                getSeverityColor(severity).includes('orange') ? 'text-orange-400' : 
                                                                getSeverityColor(severity).includes('yellow') ? 'text-yellow-400' : 'text-blue-400'}`} size={24} />
                                                            <div>
                                                                <h4 className="font-medium text-white">{severity} Priority Issues</h4>
                                                                <p className="text-gray-400 text-sm">{count} findings require {severity.toLowerCase()} priority attention</p>
                                                            </div>
                                                        </div>
                                                        <span className={`px-3 py-1 rounded-full text-sm font-medium ${getSeverityColor(severity)}`}>
                                                            {count}
                                                        </span>
                                                    </div>
                                                    
                                                    {severityFindings.length > 0 && (
                                                        <div className="space-y-2">
                                                            {severityFindings.slice(0, 5).map((finding, index) => (
                                                                <div key={index} className="bg-gray-800/50 rounded p-3">
                                                                    <div className="flex items-start justify-between mb-2">
                                                                        <div className="flex-1">
                                                                            <p className="text-white text-sm font-medium mb-1">{finding.ResourceName || finding.resourceName}</p>
                                                                            <p className="text-gray-300 text-xs mb-2">{(finding.Issue || finding.issue)}</p>
                                                                            <div className="text-xs text-gray-400">
                                                                                <span className="mr-4">Type: {finding.ResourceType || finding.resourceType}</span>
                                                                                <span>Effort: {finding.EstimatedEffort || finding.estimatedEffort}</span>
                                                                            </div>
                                                                        </div>
                                                                    </div>
                                                                </div>
                                                            ))}
                                                            {severityFindings.length > 5 && (
                                                                <p className="text-gray-400 text-xs text-center py-2">
                                                                    +{severityFindings.length - 5} more {severity.toLowerCase()} priority findings
                                                                </p>
                                                            )}
                                                        </div>
                                                    )}
                                                </div>
                                            );
                                        })
                                    ) : (
                                        <div className="text-center py-8">
                                            <AlertTriangle className="text-gray-600 mx-auto mb-3" size={48} />
                                            <h3 className="text-lg font-medium text-white mb-2">Severity Analysis</h3>
                                            <p className="text-gray-400">No severity breakdown available for this assessment.</p>
                                        </div>
                                    )}
                                </div>
                            )}

                            {activeTab === 'findings' && (
                                <div className="space-y-4">
                                    {findings.length === 0 ? (
                                        <div className="text-center py-8">
                                            <CheckCircle className="text-green-400 mx-auto mb-3" size={48} />
                                            <h3 className="text-lg font-medium text-white mb-2">Strong Security Posture</h3>
                                            <p className="text-gray-400">No critical security issues were identified in this assessment.</p>
                                        </div>
                                    ) : (
                                        findings.map((finding) => (
                                            <div key={finding.Id || finding.id} className="bg-gray-700/30 rounded-lg p-4">
                                                <div className="flex items-start justify-between mb-2">
                                                    <div className="flex items-center space-x-2">
                                                        <span className={`inline-flex items-center px-2 py-1 rounded-full text-xs font-medium border ${getSeverityColor(finding.Severity || finding.severity)}`}>
                                                            <AlertTriangle size={12} className="mr-1" />
                                                            <span>{finding.Severity || finding.severity}</span>
                                                        </span>
                                                        <span className="text-sm text-gray-400">{finding.ResourceType || finding.resourceType}</span>
                                                    </div>
                                                </div>
                                                
                                                <h4 className="font-medium text-white mb-2">{finding.ResourceName || finding.resourceName}</h4>
                                                <p className="text-gray-300 text-sm mb-3">{finding.Issue || finding.issue}</p>
                                                
                                                <div className="bg-gray-800/50 rounded p-3">
                                                    <p className="text-sm text-gray-400 mb-1">Security Recommendation:</p>
                                                    <p className="text-sm text-gray-200">{finding.Recommendation || finding.recommendation}</p>
                                                </div>
                                                
                                                {(finding.EstimatedEffort || finding.estimatedEffort) && (
                                                    <div className="mt-2">
                                                        <span className="text-xs text-gray-400">Implementation effort: </span>
                                                        <span className="text-xs text-white">{finding.EstimatedEffort || finding.estimatedEffort}</span>
                                                    </div>
                                                )}
                                                
                                                {finding.ResourceId && (
                                                    <div className="mt-2">
                                                        <span className="text-xs text-gray-400">Resource ID: </span>
                                                        <span className="text-xs text-gray-300 font-mono break-all">{finding.ResourceId}</span>
                                                    </div>
                                                )}
                                            </div>
                                        ))
                                    )}
                                </div>
                            )}
                        </>
                    )}
                </div>

                {/* Footer */}
                <div className="flex items-center justify-between p-6 border-t border-gray-700">
                    <div className="text-sm text-gray-400">
                        Security Posture Assessment
                    </div>
                    <div className="flex items-center space-x-3">
                        <button
                            onClick={onClose}
                            className="px-4 py-2 text-gray-300 hover:text-white transition-colors"
                        >
                            Close
                        </button>
                        <button
                            className="flex items-center space-x-2 px-4 py-2 bg-yellow-500 text-black rounded-md hover:bg-yellow-600 transition-colors font-medium"
                        >
                            <Download size={16} />
                            <span>Export Report</span>
                        </button>
                    </div>
                </div>
            </div>
        </div>,
        document.body
    );
};

export default SecurityPostureDetailModal;