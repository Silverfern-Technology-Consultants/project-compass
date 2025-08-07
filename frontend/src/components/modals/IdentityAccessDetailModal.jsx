import React, { useState, useEffect } from 'react';
import { createPortal } from 'react-dom';
import { X, Shield, Users, Key, AlertTriangle, CheckCircle, Clock, Download, Eye, RefreshCw } from 'lucide-react';
import { assessmentApi } from '../../services/apiService';

const IdentityAccessDetailModal = ({ isOpen, onClose, assessment }) => {
    const [assessmentData, setAssessmentData] = useState(null);
    const [assessmentResults, setAssessmentResults] = useState(null);
    const [findings, setFindings] = useState([]);
    const [loading, setLoading] = useState(false);
    const [error, setError] = useState('');
    const [activeTab, setActiveTab] = useState('overview');
    const [progress, setProgress] = useState(0);
    const [status, setStatus] = useState('Unknown');
    const [refreshInterval, setRefreshInterval] = useState(null);

    useEffect(() => {
        if (isOpen && assessment) {
            loadAssessmentDetails();
            
            // Only set up polling if assessment is actually in progress
            const status = assessment.status || assessment.rawData?.status;
            if (status === 'InProgress' || status === 'Pending') {
                // Poll every 5 seconds for in-progress assessments only
                const interval = setInterval(() => {
                    loadAssessmentDetails();
                }, 5000);
                setRefreshInterval(interval);
            }
        }
        
        return () => {
            if (refreshInterval) {
                clearInterval(refreshInterval);
                setRefreshInterval(null);
            }
        };
    }, [isOpen, assessment]);

    const loadAssessmentDetails = async () => {
        try {
            // Prevent concurrent calls
            if (loading) return;
            
            const wasLoading = !assessmentData; // Only show loading spinner on first call
            if (wasLoading) setLoading(true);
            setError('');

            const assessmentId = assessment.id || assessment.assessmentId || assessment.AssessmentId;
            console.log('[IdentityAccessDetailModal] Loading assessment details for ID:', assessmentId);
            
            // Try the results endpoint first since we know this assessment is completed
            try {
                console.log('[IdentityAccessDetailModal] Trying results endpoint...');
                const [resultsResponse, findingsResponse] = await Promise.all([
                    assessmentApi.getAssessmentResults(assessmentId),
                    assessmentApi.getAssessmentFindings(assessmentId)
                ]);
                
                console.log('[IdentityAccessDetailModal] Results response:', resultsResponse);
                console.log('[IdentityAccessDetailModal] Findings response:', findingsResponse);
                console.log('[IdentityAccessDetailModal] First finding:', findingsResponse?.[0]);
                
                setAssessmentResults(resultsResponse);
                setFindings(findingsResponse || []);
                setStatus('Completed');
                setProgress(100);
                
                // Create mock assessment data if we don't have it
                if (!assessmentData) {
                    setAssessmentData({
                        ...resultsResponse,
                        Progress: 100,
                        Status: 2
                    });
                }
                
                return; // Success, exit early
            } catch (resultsError) {
                console.warn('[IdentityAccessDetailModal] Results endpoint failed:', resultsError);
                // Continue to try basic assessment endpoint
            }
            
            // Fallback: try basic assessment call
            console.log('[IdentityAccessDetailModal] Trying basic assessment endpoint...');
            const assessmentResponse = await assessmentApi.getAssessment(assessmentId);
            setAssessmentData(assessmentResponse);
            
            // Update progress and status from response
            const currentProgress = assessmentResponse.Progress || 100; // Assume completed if no progress
            const currentStatusCode = assessmentResponse.Status;
            
            setProgress(currentProgress);
            
            // Map status codes to readable status
            const statusMap = {
                0: 'Pending',
                1: 'InProgress', 
                2: 'Completed',
                3: 'Failed'
            };
            const readableStatus = statusMap[currentStatusCode] || 'Completed'; // Default to completed
            setStatus(readableStatus);
            
            // If completed, try to load detailed results
            if (currentStatusCode === 2 || readableStatus === 'Completed' || currentProgress === 100) {
                try {
                    const [resultsResponse, findingsResponse] = await Promise.all([
                        assessmentApi.getAssessmentResults(assessmentId),
                        assessmentApi.getAssessmentFindings(assessmentId)
                    ]);
                    
                    setAssessmentResults(resultsResponse);
                    setFindings(findingsResponse || []);
                    setStatus('Completed');
                } catch (detailsError) {
                    console.warn('Could not load detailed results:', detailsError);
                    // Don't fail - we still have basic assessment data
                }
                
                // Stop polling when completed
                if (refreshInterval) {
                    clearInterval(refreshInterval);
                    setRefreshInterval(null);
                }
            }
            
        } catch (err) {
            console.error('Failed to load assessment details:', err);
            setError(`Failed to load assessment details: ${err.message}`);
        } finally {
            if (loading) setLoading(false); // Only clear loading if it was set
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

    const getSeverityIcon = (severity) => {
        switch (severity?.toLowerCase()) {
            case 'critical':
            case 'high':
                return <AlertTriangle size={16} />;
            case 'medium':
                return <Clock size={16} />;
            case 'low':
                return <CheckCircle size={16} />;
            default:
                return <Eye size={16} />;
        }
    };

    const formatAssessmentTitle = () => {
        const userEnteredName = assessment?.name || assessment?.rawData?.name || 'Identity & Access Assessment';
        return userEnteredName; // Remove the ID suffix
    };

    const getScoreColor = (score) => {
        if (score >= 90) return 'text-green-400';
        if (score >= 70) return 'text-yellow-400';
        return 'text-red-400';
    };

    const getProgressPhase = (progress) => {
        if (progress === 0) return 'Initializing assessment...';
        if (progress <= 20) return 'Connecting to Azure AD tenant...';
        if (progress <= 40) return 'Retrieving user accounts and roles...';
        if (progress <= 60) return 'Analyzing privileged access patterns...';
        if (progress <= 80) return 'Checking conditional access policies...';
        if (progress < 100) return 'Generating security recommendations...';
        return 'Assessment complete!';
    };

    const getProgressDescription = (progress) => {
        if (progress === 0) return 'Starting identity and access management analysis';
        if (progress <= 20) return 'Establishing secure connection to Azure Active Directory';
        if (progress <= 40) return 'Collecting user accounts, groups, and role assignments';
        if (progress <= 60) return 'Examining admin roles and privileged access configurations';
        if (progress <= 80) return 'Reviewing conditional access policies and security settings';
        if (progress < 100) return 'Compiling findings and generating actionable recommendations';
        return 'Analysis complete - results are ready for review';
    };

    const calculateDuration = (startDate, endDate) => {
        if (!startDate || !endDate) return 'N/A';
        const start = new Date(startDate);
        const end = new Date(endDate);
        const diffMs = end - start;
        const diffSecs = Math.round(diffMs / 1000);
        
        if (diffSecs < 60) return `${diffSecs} seconds`;
        const diffMins = Math.floor(diffSecs / 60);
        const remainingSecs = diffSecs % 60;
        return remainingSecs > 0 ? `${diffMins}m ${remainingSecs}s` : `${diffMins} minutes`;
    };

    if (!isOpen) return null;

    return createPortal(
        <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center p-4 z-50">
            <div className="bg-gray-800 rounded-lg shadow-xl w-full max-w-6xl max-h-[90vh] overflow-y-auto">
                {/* Header */}
                <div className="flex items-center justify-between p-6 border-b border-gray-700">
                    <div className="flex items-center space-x-3">
                        <Shield className="text-blue-400" size={24} />
                        <div>
                            <h2 className="text-xl font-semibold text-white">{formatAssessmentTitle()}</h2>
                            <p className="text-gray-400 text-sm">Identity & Access Management Assessment</p>
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
                            {/* Score Summary or Progress */}
                            {(status === 'InProgress' || status === 'Pending') && findings.length === 0 ? (
                                <div className="mb-6 space-y-4">
                                    <div className="bg-blue-500/10 border border-blue-500/20 rounded-lg p-4">
                                        <div className="flex items-center justify-between">
                                            <div>
                                                <h3 className="text-lg font-medium text-white mb-1 flex items-center">
                                                    <RefreshCw className="animate-spin mr-2" size={20} />
                                                    Assessment In Progress
                                                </h3>
                                                <p className="text-gray-400 text-sm">{getProgressPhase(progress)}</p>
                                            </div>
                                            <div className="text-right">
                                                <div className="text-2xl font-bold text-blue-400">{progress}%</div>
                                                <p className="text-gray-400 text-sm">Complete</p>
                                            </div>
                                        </div>
                                        <div className="mt-4">
                                            <div className="w-full bg-gray-700 rounded-full h-2">
                                                <div 
                                                    className="bg-blue-500 h-2 rounded-full transition-all duration-500" 
                                                    style={{width: `${progress}%`}}
                                                ></div>
                                            </div>
                                            <p className="text-xs text-gray-400 mt-2">{getProgressDescription(progress)}</p>
                                        </div>
                                    </div>
                                    
                                    {/* Progress Status Window */}
                                    <div className="bg-gray-900/50 border border-gray-700 rounded-lg">
                                        <div className="p-3 border-b border-gray-700">
                                            <h4 className="text-sm font-medium text-white flex items-center">
                                                <Eye className="mr-2" size={14} />
                                                Assessment Progress
                                            </h4>
                                        </div>
                                        <div className="p-3">
                                            <div className="font-mono text-sm text-gray-300 space-y-2">
                                                <div className="flex items-center space-x-2">
                                                    <div className={`w-2 h-2 rounded-full ${progress > 0 ? 'bg-green-400' : 'bg-gray-600'}`}></div>
                                                    <span className={progress > 0 ? 'text-green-400' : 'text-gray-500'}>Connecting to Azure AD tenant</span>
                                                </div>
                                                <div className="flex items-center space-x-2">
                                                    <div className={`w-2 h-2 rounded-full ${progress > 20 ? 'bg-green-400' : progress > 0 ? 'bg-yellow-400 animate-pulse' : 'bg-gray-600'}`}></div>
                                                    <span className={progress > 20 ? 'text-green-400' : progress > 0 ? 'text-yellow-400' : 'text-gray-500'}>Retrieving user accounts and roles</span>
                                                </div>
                                                <div className="flex items-center space-x-2">
                                                    <div className={`w-2 h-2 rounded-full ${progress > 50 ? 'bg-green-400' : progress > 20 ? 'bg-yellow-400 animate-pulse' : 'bg-gray-600'}`}></div>
                                                    <span className={progress > 50 ? 'text-green-400' : progress > 20 ? 'text-yellow-400' : 'text-gray-500'}>Analyzing privileged access patterns</span>
                                                </div>
                                                <div className="flex items-center space-x-2">
                                                    <div className={`w-2 h-2 rounded-full ${progress > 75 ? 'bg-green-400' : progress > 50 ? 'bg-yellow-400 animate-pulse' : 'bg-gray-600'}`}></div>
                                                    <span className={progress > 75 ? 'text-green-400' : progress > 50 ? 'text-yellow-400' : 'text-gray-500'}>Checking conditional access policies</span>
                                                </div>
                                                <div className="flex items-center space-x-2">
                                                    <div className={`w-2 h-2 rounded-full ${progress >= 100 ? 'bg-green-400' : progress > 75 ? 'bg-yellow-400 animate-pulse' : 'bg-gray-600'}`}></div>
                                                    <span className={progress >= 100 ? 'text-green-400' : progress > 75 ? 'text-yellow-400' : 'text-gray-500'}>Generating security recommendations</span>
                                                </div>
                                            </div>
                                        </div>
                                    </div>
                                </div>
                            ) : (status === 'Completed' || findings.length > 0) && (assessmentResults?.OverallScore !== undefined || assessmentData?.OverallScore !== undefined || assessment?.score !== undefined) ? (
                                <div className="mb-6 bg-gray-700/30 rounded-lg p-4">
                                    <div className="flex items-center justify-between">
                                        <div>
                                            <h3 className="text-lg font-medium text-white mb-1">Overall Security Score</h3>
                                            <p className="text-gray-400 text-sm">Identity and access management posture</p>
                                        </div>
                                        <div className="text-right">
                                            <div className={`text-3xl font-bold ${getScoreColor((assessmentResults?.OverallScore || assessmentData?.OverallScore || 0))}`}>
                                                {(assessmentResults?.OverallScore || assessmentData?.OverallScore || 0)}%
                                            </div>
                                            <p className="text-gray-400 text-sm">{assessmentResults?.IssuesFound || findings.length || 0} issues found</p>
                                            {assessmentData?.StartedDate && assessmentData?.CompletedDate && (
                                                <p className="text-gray-500 text-xs mt-1">
                                                    Duration: {calculateDuration(assessmentData.StartedDate, assessmentData.CompletedDate)}
                                                </p>
                                            )}
                                        </div>
                                    </div>
                                </div>
                            ) : null}

                            {/* Tabs */}
                            <div className="mb-6">
                                <div className="border-b border-gray-700">
                                    <nav className="-mb-px flex space-x-8">
                                        {[
                                            { id: 'overview', name: 'Overview', icon: Eye },
                                            { id: 'rbac', name: 'RBAC Analysis', icon: Users },
                                            { id: 'privileged', name: 'Privileged Access', icon: Key },
                                            { id: 'findings', name: 'Security Findings', icon: AlertTriangle }
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
                                    <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
                                        <div className="bg-gray-700/50 rounded-lg p-4">
                                            <div className="flex items-center justify-between">
                                                <div>
                                                    <p className="text-sm text-gray-400">Total Resources</p>
                                                    <p className="text-2xl font-bold text-white">
                                                        {assessmentResults?.TotalResourcesAnalyzed || assessmentData?.TotalResourcesAnalyzed || 0}
                                                    </p>
                                                </div>
                                                <Users className="text-blue-400" size={24} />
                                            </div>
                                        </div>
                                        
                                        <div className="bg-gray-700/50 rounded-lg p-4">
                                            <div className="flex items-center justify-between">
                                                <div>
                                                    <p className="text-sm text-gray-400">Security Issues</p>
                                                    <p className="text-2xl font-bold text-orange-400">
                                                        {assessmentResults?.IssuesFound || findings.length || 0}
                                                    </p>
                                                </div>
                                                <Key className="text-orange-400" size={24} />
                                            </div>
                                        </div>
                                        
                                        <div className="bg-gray-700/50 rounded-lg p-4">
                                            <div className="flex items-center justify-between">
                                                <div>
                                                    <p className="text-sm text-gray-400">Overall Score</p>
                                                    <p className={`text-2xl font-bold ${getScoreColor(assessmentResults?.OverallScore || assessmentData?.OverallScore || assessment?.score || 0)}`}>
                                                        {(assessmentResults?.OverallScore || assessmentData?.OverallScore || assessment?.score || 0)}%
                                                    </p>
                                                </div>
                                                <Shield className="text-purple-400" size={24} />
                                            </div>
                                        </div>
                                    </div>

                                    <div className="bg-gray-700/30 rounded-lg p-4">
                                        <h4 className="font-medium text-white mb-3">Assessment Summary</h4>
                                        <div className="space-y-2">
                                            <div className="flex justify-between">
                                                <span className="text-gray-400">Assessment ID:</span>
                                                <span className="text-white font-mono text-sm">
                                                    {assessment?.id || assessment?.assessmentId || assessment?.AssessmentId || 'N/A'}
                                                </span>
                                            </div>
                                            <div className="flex justify-between">
                                                <span className="text-gray-400">Assessment Type:</span>
                                                <span className="text-white">{assessment?.assessmentType || assessment?.type || 'Identity Full'}</span>
                                            </div>
                                            <div className="flex justify-between">
                                                <span className="text-gray-400">Started:</span>
                                                <span className="text-white">
                                                    {(assessmentResults?.StartedDate || assessmentData?.StartedDate) ? 
                                                        new Date(assessmentResults?.StartedDate || assessmentData?.StartedDate).toLocaleString() : 
                                                        (assessment?.date || 'N/A')
                                                    }
                                                </span>
                                            </div>
                                            <div className="flex justify-between">
                                                <span className="text-gray-400">Status:</span>
                                                <span className={`text-white px-2 py-1 rounded text-xs ${
                                                    status === 'InProgress' || status === 'Pending' ? 'bg-blue-600' : 
                                                    status === 'Completed' ? 'bg-green-600' : 
                                                    status === 'Failed' ? 'bg-red-600' : 'bg-gray-600'
                                                }`}>
                                                    {status}
                                                </span>
                                            </div>
                                            {status === 'InProgress' && progress > 0 && (
                                                <div className="flex justify-between">
                                                    <span className="text-gray-400">Progress:</span>
                                                    <span className="text-white">{progress}% complete</span>
                                                </div>
                                            )}
                                            {(assessmentResults?.CompletedDate || assessmentData?.CompletedDate) && (
                                                <div className="flex justify-between">
                                                    <span className="text-gray-400">Completed:</span>
                                                    <span className="text-white">
                                                        {new Date(assessmentResults?.CompletedDate || assessmentData?.CompletedDate).toLocaleString()}
                                                    </span>
                                                </div>
                                            )}
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
                                    <div className="flex items-center justify-between mb-4">
                                        <h3 className="text-lg font-semibold text-white">Identity & Access Findings ({findings.length} total)</h3>
                                    </div>
                                    
                                    {findings.length === 0 ? (
                                        <div className="text-center py-8">
                                            <CheckCircle className="text-green-400 mx-auto mb-3" size={48} />
                                            <h3 className="text-lg font-medium text-white mb-2">No Critical Issues Found</h3>
                                            <p className="text-gray-400">Your identity and access management appears to be well configured.</p>
                                        </div>
                                    ) : (
                                        <div className="space-y-6">
                                            {/* Group findings by severity for better organization */}
                                            {['Critical', 'High', 'Medium', 'Low'].map(severity => {
                                                const severityFindings = findings.filter(f => 
                                                    (f.severity || f.Severity || '').toLowerCase() === severity.toLowerCase()
                                                );
                                                
                                                if (severityFindings.length === 0) return null;
                                                
                                                return (
                                                    <div key={severity} className="bg-gray-800 rounded-lg border border-gray-700">
                                                        <div className="p-4 border-b border-gray-700">
                                                            <h4 className="text-lg font-semibold text-white capitalize flex items-center space-x-2">
                                                                <span>{severity} Priority Issues</span>
                                                                <span className="text-sm text-gray-400">({severityFindings.length} findings)</span>
                                                            </h4>
                                                        </div>
                                                        <div className="p-4 space-y-3">
                                                            {severityFindings.map((finding, index) => (
                                                                <div key={finding.id || finding.Id || index} className="bg-gray-700/30 rounded-lg p-4">
                                                                    <div className="flex items-start justify-between mb-2">
                                                                        <div className="flex items-center space-x-2">
                                                                            <span className={`inline-flex items-center px-2 py-1 rounded-full text-xs font-medium border ${getSeverityColor(finding.severity || finding.Severity)}`}>
                                                                                {getSeverityIcon(finding.severity || finding.Severity)}
                                                                                <span className="ml-1">{finding.severity || finding.Severity}</span>
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
                                                                            <span className="text-xs text-gray-400">Estimated effort: </span>
                                                                            <span className="text-xs text-white">{finding.estimatedEffort || finding.EstimatedEffort}</span>
                                                                        </div>
                                                                    )}
                                                                </div>
                                                            ))}
                                                        </div>
                                                    </div>
                                                );
                                            })}
                                        </div>
                                    )}
                                </div>
                            )}

                            {(activeTab === 'rbac' || activeTab === 'privileged') && (
                                <div className="space-y-4">
                                    <div className="flex items-center justify-between mb-4">
                                        <h3 className="text-lg font-semibold text-white">
                                            {activeTab === 'rbac' ? 'RBAC Analysis' : 'Privileged Access Analysis'}
                                        </h3>
                                    </div>
                                    
                                    {(() => {
                                        // Filter findings relevant to the current tab
                                        const relevantFindings = findings.filter(f => {
                                            const resourceType = (f.resourceType || f.ResourceType || '').toLowerCase();
                                            if (activeTab === 'rbac') {
                                                return resourceType.includes('role') || resourceType.includes('assignment') || resourceType.includes('rbac');
                                            } else if (activeTab === 'privileged') {
                                                return resourceType.includes('privileged') || resourceType.includes('admin') || resourceType.includes('conditional');
                                            }
                                            return false;
                                        });
                                        
                                        if (relevantFindings.length > 0) {
                                            return relevantFindings.map((finding, index) => (
                                                <div key={finding.id || finding.Id || index} className="bg-gray-700/30 rounded-lg p-4">
                                                    <div className="flex items-start justify-between mb-2">
                                                        <div className="flex items-center space-x-2">
                                                            <span className={`inline-flex items-center px-2 py-1 rounded-full text-xs font-medium border ${getSeverityColor(finding.severity || finding.Severity)}`}>
                                                                {getSeverityIcon(finding.severity || finding.Severity)}
                                                                <span className="ml-1">{finding.severity || finding.Severity}</span>
                                                            </span>
                                                            <span className="text-sm text-gray-400">{finding.resourceType || finding.ResourceType}</span>
                                                        </div>
                                                    </div>
                                                    
                                                    <h4 className="font-medium text-white mb-2">{finding.resourceName || finding.ResourceName}</h4>
                                                    <p className="text-gray-300 text-sm mb-3">{finding.issue || finding.Issue}</p>
                                                    
                                                    <div className="bg-gray-800/50 rounded p-3">
                                                        <p className="text-sm text-gray-400 mb-1">Recommendation:</p>
                                                        <p className="text-sm text-gray-200">{finding.recommendation || finding.Recommendation}</p>
                                                    </div>
                                                    
                                                    {(finding.estimatedEffort || finding.EstimatedEffort) && (
                                                        <div className="mt-2">
                                                            <span className="text-xs text-gray-400">Estimated effort: </span>
                                                            <span className="text-xs text-white">{finding.estimatedEffort || finding.EstimatedEffort}</span>
                                                        </div>
                                                    )}
                                                </div>
                                            ));
                                        } else {
                                            // Show all findings if no specific filter matches
                                            if (findings.length > 0) {
                                                return findings.map((finding, index) => (
                                                    <div key={finding.id || finding.Id || index} className="bg-gray-700/30 rounded-lg p-4">
                                                        <div className="flex items-start justify-between mb-2">
                                                            <div className="flex items-center space-x-2">
                                                                <span className={`inline-flex items-center px-2 py-1 rounded-full text-xs font-medium border ${getSeverityColor(finding.severity || finding.Severity)}`}>
                                                                    {getSeverityIcon(finding.severity || finding.Severity)}
                                                                    <span className="ml-1">{finding.severity || finding.Severity}</span>
                                                                </span>
                                                                <span className="text-sm text-gray-400">{finding.resourceType || finding.ResourceType}</span>
                                                            </div>
                                                        </div>
                                                        
                                                        <h4 className="font-medium text-white mb-2">{finding.resourceName || finding.ResourceName}</h4>
                                                        <p className="text-gray-300 text-sm mb-3">{finding.issue || finding.Issue}</p>
                                                        
                                                        <div className="bg-gray-800/50 rounded p-3">
                                                            <p className="text-sm text-gray-400 mb-1">Recommendation:</p>
                                                            <p className="text-sm text-gray-200">{finding.recommendation || finding.Recommendation}</p>
                                                        </div>
                                                        
                                                        {(finding.estimatedEffort || finding.EstimatedEffort) && (
                                                            <div className="mt-2">
                                                                <span className="text-xs text-gray-400">Estimated effort: </span>
                                                                <span className="text-xs text-white">{finding.estimatedEffort || finding.EstimatedEffort}</span>
                                                            </div>
                                                        )}
                                                    </div>
                                                ));
                                            } else {
                                                return (
                                                    <div className="text-center py-8">
                                                        <Shield className="text-gray-600 mx-auto mb-3" size={48} />
                                                        <h3 className="text-lg font-medium text-white mb-2">
                                                            {activeTab === 'rbac' ? 'RBAC Analysis' : 'Privileged Access Analysis'}
                                                        </h3>
                                                        <p className="text-gray-400">
                                                            No {activeTab === 'rbac' ? 'role-based access control' : 'privileged access'} issues found.
                                                        </p>
                                                        <p className="text-gray-500 text-sm mt-2">
                                                            Your identity management appears to be well configured.
                                                        </p>
                                                    </div>
                                                );
                                            }
                                        }
                                    })()}
                                </div>
                            )}
                        </>
                    )}
                </div>

                {/* Footer */}
                <div className="flex items-center justify-between p-6 border-t border-gray-700">
                    <div className="text-sm text-gray-400">
                        Identity & Access Management Assessment
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

export default IdentityAccessDetailModal;