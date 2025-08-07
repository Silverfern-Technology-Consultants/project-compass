import React, { useState, useEffect } from 'react';
import { createPortal } from 'react-dom';
import { X, Shield, Activity, HardDrive, AlertTriangle, CheckCircle, Download, Eye, Clock, Database } from 'lucide-react';
import { assessmentApi } from '../../services/apiService';

const BCDRDetailModal = ({ isOpen, onClose, assessment }) => {
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
            console.log('[BCDRDetailModal] Loading details for assessment:', assessmentId);
            
            // Try results endpoint first (since assessment is completed)
            try {
                const resultsResponse = await assessmentApi.getAssessmentResults(assessmentId);
                console.log('[BCDRDetailModal] Results response:', resultsResponse);
                setAssessmentResults(resultsResponse);
            } catch (resultsError) {
                console.warn('[BCDRDetailModal] Failed to get results, trying fallback:', resultsError);
                // Fallback to basic assessment info
                const basicResponse = await assessmentApi.getAssessmentStatus(assessmentId);
                console.log('[BCDRDetailModal] Basic response:', basicResponse);
                setAssessmentResults(basicResponse);
            }

            // Load findings
            const findingsResponse = await assessmentApi.getAssessmentFindings(assessmentId);
            console.log('[BCDRDetailModal] Findings response:', findingsResponse);
            
            // Filter for BCDR-related findings
            const bcdrFindings = (findingsResponse || []).filter(f => 
                f.category === 'BusinessContinuity' || 
                f.Category === 'BusinessContinuity' ||
                f.category === 'BCDR' || 
                f.category === 'Backup' ||
                f.category === 'DisasterRecovery'
            );
            
            console.log('[BCDRDetailModal] Filtered BCDR findings:', bcdrFindings.length);
            setFindings(bcdrFindings);
            
        } catch (err) {
            console.error('[BCDRDetailModal] Failed to load assessment details:', err);
            setError('Failed to load BCDR assessment details. Please try again.');
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
        const userEnteredName = assessment?.name || assessment?.Name || 'Business Continuity & Disaster Recovery Assessment';
        return `${userEnteredName} - ${shortId}`;
    };

    const getScoreColor = (score) => {
        if (score >= 90) return 'text-green-400';
        if (score >= 70) return 'text-yellow-400';
        return 'text-red-400';
    };

    const getScoreDescription = (score) => {
        if (score >= 90) return 'Excellent BCDR readiness';
        if (score >= 70) return 'Good BCDR foundation with improvements needed';
        if (score >= 50) return 'Moderate BCDR gaps require attention';
        if (score >= 25) return 'Significant BCDR improvements required';
        return 'Critical BCDR gaps need immediate attention';
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

    const groupFindingsBySeverity = (findings) => {
        const groups = {
            'High': [],
            'Medium': [],
            'Low': [],
            'Critical': []
        };
        
        findings.forEach(finding => {
            const severity = finding.severity || finding.Severity || 'Medium';
            if (groups[severity]) {
                groups[severity].push(finding);
            } else {
                groups['Medium'].push(finding);
            }
        });
        
        return groups;
    };

    const getResourceTypeIcon = (resourceType) => {
        const type = resourceType?.toLowerCase();
        if (type?.includes('backup')) return HardDrive;
        if (type?.includes('recovery')) return Activity;
        if (type?.includes('business')) return Shield;
        return Database;
    };

    if (!isOpen) return null;

    return createPortal(
        <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center p-4 z-50">
            <div className="bg-gray-800 rounded-lg shadow-xl w-full max-w-6xl max-h-[90vh] overflow-y-auto">
                {/* Header */}
                <div className="flex items-center justify-between p-6 border-b border-gray-700">
                    <div className="flex items-center space-x-3">
                        <Activity className="text-yellow-400" size={24} />
                        <div>
                            <h2 className="text-xl font-semibold text-white">{formatAssessmentTitle()}</h2>
                            <p className="text-gray-400 text-sm">Business Continuity & Disaster Recovery Assessment</p>
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
                            <div className="mb-6">
                                <div className="bg-gray-700/30 rounded-lg p-6">
                                    <div className="flex items-center justify-between mb-4">
                                        <div>
                                            <h3 className="text-lg font-medium text-white mb-2">BCDR Readiness Score</h3>
                                            <p className="text-gray-400 text-sm">Overall business continuity and disaster recovery posture</p>
                                        </div>
                                        <div className="text-right">
                                            <div className={`text-4xl font-bold mb-1 ${getScoreColor(assessmentResults?.overallScore || assessmentResults?.OverallScore || 0)}`}>
                                                {assessmentResults?.overallScore || assessmentResults?.OverallScore || 0}%
                                            </div>
                                            <p className="text-sm text-gray-400">{
                                                getScoreDescription(assessmentResults?.overallScore || assessmentResults?.OverallScore || 0)
                                            }</p>
                                        </div>
                                    </div>
                                    
                                    {/* Quick Stats */}
                                    <div className="grid grid-cols-3 gap-4 mt-4">
                                        <div className="bg-gray-800/50 rounded-lg p-3 text-center">
                                            <div className="text-2xl font-bold text-red-400">{findings.length}</div>
                                            <div className="text-xs text-gray-400 mt-1">BCDR Issues</div>
                                        </div>
                                        <div className="bg-gray-800/50 rounded-lg p-3 text-center">
                                            <div className="text-2xl font-bold text-blue-400">
                                                {assessmentResults?.totalResourcesAnalyzed || assessmentResults?.TotalResourcesAnalyzed || 0}
                                            </div>
                                            <div className="text-xs text-gray-400 mt-1">Resources</div>
                                        </div>
                                        <div className="bg-gray-800/50 rounded-lg p-3 text-center">
                                            <div className="text-2xl font-bold text-yellow-400">
                                                {formatDuration(
                                                    assessmentResults?.startedDate || assessmentResults?.StartedDate,
                                                    assessmentResults?.completedDate || assessmentResults?.CompletedDate
                                                )}
                                            </div>
                                            <div className="text-xs text-gray-400 mt-1">Duration</div>
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
                                            { id: 'backup', name: 'Backup Analysis', icon: HardDrive },
                                            { id: 'recovery', name: 'Recovery Planning', icon: Activity },
                                            { id: 'findings', name: 'BCDR Findings', icon: AlertTriangle }
                                        ].map((tab) => (
                                            <button
                                                key={tab.id}
                                                onClick={() => setActiveTab(tab.id)}
                                                className={`flex items-center space-x-2 py-2 px-1 border-b-2 font-medium text-sm transition-colors ${
                                                    activeTab === tab.id
                                                        ? 'border-yellow-500 text-yellow-400'
                                                        : 'border-transparent text-gray-400 hover:text-gray-300 hover:border-gray-300'
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
                                    <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
                                        <div className="bg-gray-700/50 rounded-lg p-4">
                                            <div className="flex items-center justify-between">
                                                <div>
                                                    <p className="text-sm text-gray-400">Backup Coverage</p>
                                                    <p className="text-2xl font-bold text-green-400">
                                                        {assessmentResults?.backupCoverage || 'N/A'}
                                                    </p>
                                                </div>
                                                <HardDrive className="text-green-400" size={24} />
                                            </div>
                                        </div>
                                        
                                        <div className="bg-gray-700/50 rounded-lg p-4">
                                            <div className="flex items-center justify-between">
                                                <div>
                                                    <p className="text-sm text-gray-400">Recovery Points</p>
                                                    <p className="text-2xl font-bold text-blue-400">
                                                        {assessmentResults?.recoveryPoints || 'N/A'}
                                                    </p>
                                                </div>
                                                <Activity className="text-blue-400" size={24} />
                                            </div>
                                        </div>
                                        
                                        <div className="bg-gray-700/50 rounded-lg p-4">
                                            <div className="flex items-center justify-between">
                                                <div>
                                                    <p className="text-sm text-gray-400">RTO Compliance</p>
                                                    <p className="text-2xl font-bold text-purple-400">
                                                        {assessmentResults?.rtoCompliance || 'N/A'}
                                                    </p>
                                                </div>
                                                <Clock className="text-purple-400" size={24} />
                                            </div>
                                        </div>
                                    </div>

                                    <div className="bg-gray-700/30 rounded-lg p-4">
                                        <h4 className="font-medium text-white mb-3">Assessment Summary</h4>
                                        <div className="space-y-2">
                                            <div className="flex justify-between">
                                                <span className="text-gray-400">Assessment Type:</span>
                                                <span className="text-white">{assessment?.assessmentType || assessment?.type}</span>
                                            </div>
                                            <div className="flex justify-between">
                                                <span className="text-gray-400">Completed:</span>
                                                <span className="text-white">{assessment?.date}</span>
                                            </div>
                                            <div className="flex justify-between">
                                                <span className="text-gray-400">Duration:</span>
                                                <span className="text-white">{assessment?.duration || 'N/A'}</span>
                                            </div>
                                            <div className="flex justify-between">
                                                <span className="text-gray-400">Client:</span>
                                                <span className="text-white">{assessment?.clientName || 'Internal'}</span>
                                            </div>
                                        </div>
                                    </div>
                                </div>
                            )}

                            {activeTab === 'findings' && (
                                <div className="space-y-4">
                                    {findings.length === 0 ? (
                                        <div className="text-center py-8">
                                            <CheckCircle className="text-green-400 mx-auto mb-3" size={48} />
                                            <h3 className="text-lg font-medium text-white mb-2">Excellent BCDR Posture</h3>
                                            <p className="text-gray-400">No business continuity or disaster recovery issues found. Your environment appears to be well protected.</p>
                                        </div>
                                    ) : (
                                        <>
                                            {/* Severity Summary */}
                                            <div className="bg-gray-700/30 rounded-lg p-4 mb-4">
                                                <h4 className="font-medium text-white mb-3">Issues by Priority</h4>
                                                <div className="grid grid-cols-4 gap-4">
                                                    {Object.entries(groupFindingsBySeverity(findings)).map(([severity, severityFindings]) => (
                                                        severityFindings.length > 0 && (
                                                            <div key={severity} className="text-center">
                                                                <div className={`text-2xl font-bold ${
                                                                    severity === 'High' ? 'text-orange-400' :
                                                                    severity === 'Medium' ? 'text-yellow-400' :
                                                                    severity === 'Low' ? 'text-blue-400' :
                                                                    'text-red-400'
                                                                }`}>
                                                                    {severityFindings.length}
                                                                </div>
                                                                <div className="text-xs text-gray-400 mt-1">{severity} Priority</div>
                                                            </div>
                                                        )
                                                    ))}
                                                </div>
                                            </div>

                                            {/* Findings List */}
                                            {Object.entries(groupFindingsBySeverity(findings)).map(([severity, severityFindings]) => (
                                                severityFindings.length > 0 && (
                                                    <div key={severity} className="mb-6">
                                                        <h4 className="font-medium text-white mb-3 flex items-center">
                                                            <span className={`w-3 h-3 rounded-full mr-3 ${
                                                                severity === 'High' ? 'bg-orange-400' :
                                                                severity === 'Medium' ? 'bg-yellow-400' :
                                                                severity === 'Low' ? 'bg-blue-400' :
                                                                'bg-red-400'
                                                            }`}></span>
                                                            {severity} Priority Issues ({severityFindings.length})
                                                        </h4>
                                                        <div className="space-y-3">
                                                            {severityFindings.map((finding) => {
                                                                const ResourceIcon = getResourceTypeIcon(finding.resourceType || finding.ResourceType);
                                                                return (
                                                                    <div key={finding.id || finding.Id} className="bg-gray-700/30 rounded-lg p-4">
                                                                        <div className="flex items-start justify-between mb-3">
                                                                            <div className="flex items-center space-x-3">
                                                                                <ResourceIcon className="text-gray-400" size={20} />
                                                                                <div>
                                                                                    <h5 className="font-medium text-white">
                                                                                        {finding.resourceName || finding.ResourceName}
                                                                                    </h5>
                                                                                    <p className="text-sm text-gray-400">
                                                                                        {finding.resourceType || finding.ResourceType}
                                                                                    </p>
                                                                                </div>
                                                                            </div>
                                                                            <span className={`inline-flex items-center px-2 py-1 rounded-full text-xs font-medium border ${getSeverityColor(finding.severity || finding.Severity)}`}>
                                                                                <AlertTriangle size={12} className="mr-1" />
                                                                                {finding.severity || finding.Severity}
                                                                            </span>
                                                                        </div>
                                                                        
                                                                        <div className="mb-3">
                                                                            <p className="text-sm text-gray-300 mb-2">
                                                                                <span className="font-medium text-red-400">Issue:</span> {finding.issue || finding.Issue}
                                                                            </p>
                                                                        </div>
                                                                        
                                                                        <div className="bg-gray-800/50 rounded-lg p-3">
                                                                            <p className="text-sm text-gray-400 mb-1">BCDR Recommendation:</p>
                                                                            <p className="text-sm text-gray-200">{finding.recommendation || finding.Recommendation}</p>
                                                                        </div>
                                                                        
                                                                        {(finding.estimatedEffort || finding.EstimatedEffort) && (
                                                                            <div className="mt-3 flex items-center justify-between">
                                                                                <span className="text-xs text-gray-400">Implementation Effort:</span>
                                                                                <span className={`text-xs px-2 py-1 rounded ${
                                                                                    (finding.estimatedEffort || finding.EstimatedEffort) === 'High' ? 'bg-red-500/20 text-red-400' :
                                                                                    (finding.estimatedEffort || finding.EstimatedEffort) === 'Medium' ? 'bg-yellow-500/20 text-yellow-400' :
                                                                                    'bg-green-500/20 text-green-400'
                                                                                }`}>
                                                                                    {finding.estimatedEffort || finding.EstimatedEffort}
                                                                                </span>
                                                                            </div>
                                                                        )}
                                                                    </div>
                                                                );
                                                            })}
                                                        </div>
                                                    </div>
                                                )
                                            ))}
                                        </>
                                    )}
                                </div>
                            )}

                            {(activeTab === 'backup' || activeTab === 'recovery') && (
                                <div className="space-y-6">
                                    {/* Category-specific findings */}
                                    {(() => {
                                        const categoryFindings = findings.filter(f => 
                                            activeTab === 'backup' ? 
                                                (f.resourceType === 'Backup' || f.ResourceType === 'Backup') :
                                                (f.resourceType === 'DisasterRecovery' || f.ResourceType === 'DisasterRecovery' || 
                                                 f.resourceType === 'BusinessContinuity' || f.ResourceType === 'BusinessContinuity')
                                        );
                                        
                                        if (categoryFindings.length === 0) {
                                            return (
                                                <div className="text-center py-8">
                                                    <Activity className="text-gray-600 mx-auto mb-3" size={48} />
                                                    <h3 className="text-lg font-medium text-white mb-2">
                                                        {activeTab === 'backup' ? 'Backup Analysis' : 'Recovery Planning Analysis'}
                                                    </h3>
                                                    <p className="text-gray-400">
                                                        No specific {activeTab === 'backup' ? 'backup' : 'recovery'} issues found in this category.
                                                    </p>
                                                    <p className="text-gray-500 text-sm mt-2">
                                                        View the findings tab for all BCDR recommendations.
                                                    </p>
                                                </div>
                                            );
                                        }
                                        
                                        return (
                                            <div className="space-y-4">
                                                <div className="bg-gray-700/30 rounded-lg p-4">
                                                    <h4 className="font-medium text-white mb-2">
                                                        {activeTab === 'backup' ? 'Backup Coverage Analysis' : 'Recovery Planning Analysis'}
                                                    </h4>
                                                    <p className="text-sm text-gray-400">
                                                        Found {categoryFindings.length} {activeTab === 'backup' ? 'backup-related' : 'recovery-related'} issues requiring attention.
                                                    </p>
                                                </div>
                                                
                                                {categoryFindings.map((finding) => {
                                                    const ResourceIcon = getResourceTypeIcon(finding.resourceType || finding.ResourceType);
                                                    return (
                                                        <div key={finding.id || finding.Id} className="bg-gray-700/30 rounded-lg p-4">
                                                            <div className="flex items-start justify-between mb-2">
                                                                <div className="flex items-center space-x-3">
                                                                    <ResourceIcon className="text-gray-400" size={20} />
                                                                    <div>
                                                                        <h5 className="font-medium text-white">
                                                                            {finding.resourceName || finding.ResourceName}
                                                                        </h5>
                                                                        <p className="text-sm text-gray-400">
                                                                            {finding.resourceType || finding.ResourceType}
                                                                        </p>
                                                                    </div>
                                                                </div>
                                                                <span className={`inline-flex items-center px-2 py-1 rounded-full text-xs font-medium border ${getSeverityColor(finding.severity || finding.Severity)}`}>
                                                                    {finding.severity || finding.Severity}
                                                                </span>
                                                            </div>
                                                            
                                                            <p className="text-gray-300 text-sm mb-3">{finding.issue || finding.Issue}</p>
                                                            
                                                            <div className="bg-gray-800/50 rounded p-3">
                                                                <p className="text-sm text-gray-400 mb-1">Recommendation:</p>
                                                                <p className="text-sm text-gray-200">{finding.recommendation || finding.Recommendation}</p>
                                                            </div>
                                                        </div>
                                                    );
                                                })}
                                            </div>
                                        );
                                    })()
                                    }
                                </div>
                            )}
                        </>
                    )}
                </div>

                {/* Footer */}
                <div className="flex items-center justify-between p-6 border-t border-gray-700">
                    <div className="text-sm text-gray-400">
                        Business Continuity & Disaster Recovery Assessment
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

export default BCDRDetailModal;
