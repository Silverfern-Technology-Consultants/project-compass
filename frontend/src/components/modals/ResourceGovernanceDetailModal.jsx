import React, { useState, useEffect } from 'react';
import { createPortal } from 'react-dom';
import { X, FileText, Tag, AlertTriangle, CheckCircle, Download, Eye, Settings } from 'lucide-react';
import { assessmentApi } from '../../services/apiService';

const ResourceGovernanceDetailModal = ({ isOpen, onClose, assessment }) => {
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
            
            const [resultsResponse, findingsResponse] = await Promise.all([
                assessmentApi.getAssessmentResults(assessmentId),
                assessmentApi.getAssessmentFindings(assessmentId)
            ]);

            console.log('[ResourceGovernanceDetailModal] Results response:', resultsResponse);
            console.log('[ResourceGovernanceDetailModal] Findings response:', findingsResponse);
            console.log('[ResourceGovernanceDetailModal] First finding:', findingsResponse?.[0]);
            
            setAssessmentResults(resultsResponse);
            
            // Don't filter findings - show all governance-related findings
            // The backend should only return relevant findings for this assessment
            setFindings(findingsResponse || []);
        } catch (err) {
            console.error('Failed to load assessment details:', err);
            setError('Failed to load assessment details');
        } finally {
            setLoading(false);
        }
    };

    const getSeverityColor = (severity) => {
        switch (severity?.toLowerCase()) {
            case 'critical': return 'text-red-400 bg-red-400/10 border-red-400/20';
            case 'high': return 'text-orange-400 bg-orange-400/10 border-orange-400/20';
            case 'medium': return 'text-yellow-400 bg-yellow-400/10 border-yellow-400/20';
            case 'low': return 'text-blue-400 bg-blue-400/10 border-blue-400/20';
            default: return 'text-gray-400 bg-gray-400/10 border-gray-400/20';
        }
    };

    const formatAssessmentTitle = () => {
        const assessmentId = assessment?.id || assessment?.assessmentId || 'UNKNOWN';
        const shortId = assessmentId.substring(0, 8).toUpperCase();
        const userEnteredName = assessment?.name || assessment?.rawData?.name || 'Resource Governance Assessment';
        return `${userEnteredName} - ${shortId}`;
    };

    const getScoreColor = (score) => {
        if (score >= 90) return 'text-green-400';
        if (score >= 70) return 'text-yellow-400';
        return 'text-red-400';
    };

    const getCategoryFindings = (category) => {
        // Handle different category name variations
        return findings.filter(f => {
            const findingCategory = (f.category || f.Category || '').toLowerCase();
            const targetCategory = category.toLowerCase();
            
            // Check for exact match or partial match
            return findingCategory === targetCategory || 
                   findingCategory.includes(targetCategory.replace('convention', '')) ||
                   (targetCategory === 'namingconvention' && findingCategory.includes('naming')) ||
                   (targetCategory === 'tagging' && findingCategory.includes('tag'));
        });
    };

    if (!isOpen) return null;

    return createPortal(
        <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center p-4 z-50">
            <div className="bg-gray-800 rounded-lg shadow-xl w-full max-w-6xl max-h-[90vh] overflow-y-auto">
                {/* Header */}
                <div className="flex items-center justify-between p-6 border-b border-gray-700">
                    <div className="flex items-center space-x-3">
                        <Settings className="text-green-400" size={24} />
                        <div>
                            <h2 className="text-xl font-semibold text-white">{formatAssessmentTitle()}</h2>
                            <p className="text-gray-400 text-sm">Resource Governance Assessment</p>
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
                            {(assessment?.score !== undefined || assessmentResults?.OverallScore !== undefined) && (
                                <div className="mb-6 bg-gray-700/30 rounded-lg p-4">
                                    <div className="flex items-center justify-between">
                                        <div>
                                            <h3 className="text-lg font-medium text-white mb-1">Governance Score</h3>
                                            <p className="text-gray-400 text-sm">Resource naming and tagging compliance</p>
                                        </div>
                                        <div className="text-right">
                                            <div className={`text-3xl font-bold ${getScoreColor(assessment.score || assessmentResults?.OverallScore)}`}>
                                                {assessment.score || assessmentResults?.OverallScore}%
                                            </div>
                                            <p className="text-gray-400 text-sm">{assessment.issuesCount || assessmentResults?.IssuesFound || findings.length} governance issues</p>
                                        </div>
                                    </div>
                                </div>
                            )}

                                            {/* Tabs */}
                            <div className="mb-6">
                                <div className="border-b border-gray-700">
                                    <nav className="-mb-px flex space-x-8">
                                        {[
                                            { id: 'overview', name: 'Overview', icon: Eye },
                                            { id: 'findings', name: 'Findings', icon: AlertTriangle },
                                            { id: 'resources', name: 'Resources', icon: Settings }
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
                                                {tab.id === 'resources' && assessmentResults?.TotalResourcesAnalyzed && (
                                                    <span className="bg-blue-500 text-white text-xs rounded-full px-2 py-0.5">
                                                        {assessmentResults.TotalResourcesAnalyzed}
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
                                                    <p className="text-sm text-gray-400">Resources Analyzed</p>
                                                    <p className="text-2xl font-bold text-white">
                                                        {assessmentResults?.TotalResourcesAnalyzed || assessment?.resourceCount || 'N/A'}
                                                    </p>
                                                </div>
                                                <Settings className="text-blue-400" size={24} />
                                            </div>
                                        </div>
                                        
                                        <div className="bg-gray-700/50 rounded-lg p-4">
                                            <div className="flex items-center justify-between">
                                                <div>
                                                    <p className="text-sm text-gray-400">Total Issues Found</p>
                                                    <p className="text-2xl font-bold text-red-400">
                                                        {assessmentResults?.IssuesFound || findings.length || assessment?.issuesCount || 'N/A'}
                                                    </p>
                                                </div>
                                                <AlertTriangle className="text-red-400" size={24} />
                                            </div>
                                        </div>
                                        
                                        <div className="bg-gray-700/50 rounded-lg p-4">
                                            <div className="flex items-center justify-between">
                                                <div>
                                                    <p className="text-sm text-gray-400">Compliance Rate</p>
                                                    <p className="text-2xl font-bold text-green-400">
                                                        {(() => {
                                                            const total = assessmentResults?.TotalResourcesAnalyzed || assessment?.resourceCount || 0;
                                                            const issues = assessmentResults?.IssuesFound || findings.length || 0;
                                                            const compliant = Math.max(0, total - issues);
                                                            return total > 0 ? `${Math.round((compliant / total) * 100)}%` : 'N/A';
                                                        })()}
                                                    </p>
                                                </div>
                                                <CheckCircle className="text-green-400" size={24} />
                                            </div>
                                        </div>
                                    </div>

                                    <div className="bg-gray-700/30 rounded-lg p-4">
                                        <h4 className="font-medium text-white mb-3">Assessment Summary</h4>
                                        <div className="space-y-2">
                                            <div className="flex justify-between">
                                                <span className="text-gray-400">Assessment Type:</span>
                                                <span className="text-white">Resource Governance Assessment</span>
                                            </div>
                                            <div className="flex justify-between">
                                                <span className="text-gray-400">Completed:</span>
                                                <span className="text-white">{assessment?.date || new Date(assessmentResults?.CompletedDate || assessmentResults?.StartedDate).toLocaleDateString()}</span>
                                            </div>
                                            <div className="flex justify-between">
                                                <span className="text-gray-400">Duration:</span>
                                                <span className="text-white">{(() => {
                                                    const start = new Date(assessmentResults?.StartedDate);
                                                    const end = new Date(assessmentResults?.CompletedDate);
                                                    const diffMs = end - start;
                                                    return diffMs > 0 ? `${Math.round(diffMs / 1000)}s` : 'N/A';
                                                })()}</span>
                                            </div>
                                            <div className="flex justify-between">
                                                <span className="text-gray-400">Client:</span>
                                                <span className="text-white">{assessment?.clientName || 'JoJa Mart'}</span>
                                            </div>
                                        </div>
                                    </div>
                                </div>
                            )}

                            {activeTab === 'findings' && (
                                <div className="space-y-4">
                                    <div className="flex items-center justify-between mb-4">
                                        <h3 className="text-lg font-semibold text-white">Governance Findings ({findings.length} total)</h3>
                                    </div>
                                    
                                    {findings.length === 0 ? (
                                        <div className="text-center py-8">
                                            <CheckCircle className="text-green-400 mx-auto mb-3" size={48} />
                                            <h3 className="text-lg font-medium text-white mb-2">Excellent Governance</h3>
                                            <p className="text-gray-400">No governance issues were identified in this assessment.</p>
                                        </div>
                                    ) : (
                                        <div className="space-y-6">
                                            {/* Group findings by category */}
                                            {Object.entries(
                                                findings.reduce((groups, finding) => {
                                                    const category = finding.category || finding.Category || 'Other';
                                                    if (!groups[category]) groups[category] = [];
                                                    groups[category].push(finding);
                                                    return groups;
                                                }, {})
                                            ).map(([category, categoryFindings]) => (
                                                <div key={category} className="bg-gray-800 rounded-lg border border-gray-700">
                                                    <div className="p-4 border-b border-gray-700">
                                                        <h4 className="text-lg font-semibold text-white capitalize flex items-center space-x-2">
                                                            <span>{category}</span>
                                                            <span className="text-sm text-gray-400">({categoryFindings.length} issues)</span>
                                                        </h4>
                                                    </div>
                                                    <div className="p-4 space-y-3">
                                                        {categoryFindings.map((finding, index) => (
                                                            <div key={finding.id || index} className="bg-gray-700/30 rounded-lg p-4">
                                                                <div className="flex items-start justify-between mb-2">
                                                                    <div className="flex items-center space-x-2">
                                                                        <span className={`inline-flex items-center px-2 py-1 rounded-full text-xs font-medium border ${getSeverityColor(finding.severity || finding.Severity)}`}>
                                                                            <AlertTriangle size={12} className="mr-1" />
                                                                            <span>{finding.severity || finding.Severity}</span>
                                                                        </span>
                                                                        <span className="text-sm text-gray-400">{finding.resourceType || finding.ResourceType}</span>
                                                                    </div>
                                                                </div>
                                                                
                                                                <h5 className="font-medium text-white mb-2">{finding.resourceName || finding.ResourceName}</h5>
                                                                <p className="text-gray-300 text-sm mb-3">{finding.issue || finding.Issue}</p>
                                                                
                                                                <div className="bg-gray-800/50 rounded p-3">
                                                                    <p className="text-sm text-gray-400 mb-1">Recommendation:</p>
                                                                    <p className="text-sm text-gray-200">{finding.recommendation || finding.Recommendation}</p>
                                                                </div>
                                                                
                                                                {(finding.estimatedEffort || finding.EstimatedEffort) && (
                                                                    <div className="mt-2">
                                                                        <span className="text-xs text-gray-400">Implementation effort: </span>
                                                                        <span className="text-xs text-white">{finding.estimatedEffort || finding.EstimatedEffort}</span>
                                                                    </div>
                                                                )}
                                                            </div>
                                                        ))}
                                                    </div>
                                                </div>
                                            ))}
                                        </div>
                                    )}
                                </div>
                            )}

                            {activeTab === 'resources' && (
                                <div className="space-y-4">
                                    <div className="flex items-center justify-between mb-4">
                                        <h3 className="text-lg font-semibold text-white">Azure Resources ({assessmentResults?.TotalResourcesAnalyzed || 'N/A'})</h3>
                                        <div className="flex space-x-2">
                                            <button className="px-4 py-2 bg-gray-700 text-white rounded-lg text-sm hover:bg-gray-600 transition-colors flex items-center space-x-2">
                                                <Download size={16} />
                                                <span>Export CSV</span>
                                            </button>
                                            <button className="px-4 py-2 bg-yellow-600 text-black rounded-lg text-sm hover:bg-yellow-700 transition-colors flex items-center space-x-2">
                                                <Download size={16} />
                                                <span>Export Excel</span>
                                            </button>
                                        </div>
                                    </div>
                                    
                                    <div className="bg-gray-800 rounded-lg border border-gray-700">
                                        <div className="p-4">
                                            <p className="text-gray-400 text-sm mb-4">
                                                Showing all Azure resources analyzed in this assessment. Resources with issues are highlighted.
                                            </p>
                                            
                                            {/* Mock resources based on API data */}
                                            <div className="space-y-2">
                                                {assessmentResults?.Recommendations?.map((rec, index) => 
                                                    rec.AffectedResources?.map((resourceName, resIndex) => (
                                                        <div key={`${index}-${resIndex}`} className="flex items-center justify-between p-3 bg-gray-700/50 rounded-lg border border-red-600/20">
                                                            <div className="flex items-center space-x-3">
                                                                <div className="w-2 h-2 bg-red-500 rounded-full"></div>
                                                                <div>
                                                                    <p className="font-medium text-white">{resourceName}</p>
                                                                    <p className="text-sm text-gray-400">{rec.Category} Issue</p>
                                                                </div>
                                                            </div>
                                                            <div className="flex items-center space-x-2">
                                                                <span className="px-2 py-1 bg-red-600 text-white text-xs rounded-full">Issues</span>
                                                                <span className="text-sm text-gray-400">0% - 0 tags</span>
                                                            </div>
                                                        </div>
                                                    ))
                                                ) || (
                                                    <div className="text-center py-8 text-gray-400">
                                                        <Settings size={48} className="mx-auto mb-2 opacity-50" />
                                                        <p>No resource details available</p>
                                                    </div>
                                                )}
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
                    <div className="text-sm text-gray-400">
                        Resource Governance Assessment
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

export default ResourceGovernanceDetailModal;
