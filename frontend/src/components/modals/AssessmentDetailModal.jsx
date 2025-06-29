import React, { useState, useEffect } from 'react';
import { createPortal } from 'react-dom';
import { X, CheckCircle, AlertTriangle, XCircle, FileText, BarChart3, Download, Eye, Filter, Search, ChevronLeft, ChevronRight, Server, Database, Network } from 'lucide-react';
import { assessmentApi, apiUtils } from '../../services/apiService';

const AssessmentDetailModal = ({ isOpen, onClose, assessment }) => {
    const [findings, setFindings] = useState([]);
    const [resources, setResources] = useState([]);
    const [resourceFilters, setResourceFilters] = useState({});
    const [resourceSearch, setResourceSearch] = useState('');
    const [resourcePage, setResourcePage] = useState(1);
    const [resourceTotalPages, setResourceTotalPages] = useState(1);
    const [resourceTotalCount, setResourceTotalCount] = useState(0);
    const [selectedFilters, setSelectedFilters] = useState({
        resourceType: '',
        resourceGroup: '',
        location: '',
        environment: ''
    });
    const [loading, setLoading] = useState(false);
    const [error, setError] = useState(null);
    const [activeTab, setActiveTab] = useState('overview');
    const [tabLoading, setTabLoading] = useState({});

    useEffect(() => {
        if (isOpen && assessment?.id) {
            console.log('[AssessmentDetailModal] Loading assessment details for:', assessment.id);
            console.log('[AssessmentDetailModal] Assessment object:', assessment);
            loadFindings();
        }
    }, [isOpen, assessment]);

    const loadFindings = async () => {
        try {
            setLoading(true);
            setError(null);
            console.log('[AssessmentDetailModal] Fetching findings for assessment:', assessment.id);

            const results = await assessmentApi.getAssessmentFindings(assessment.id);
            console.log('[AssessmentDetailModal] Raw findings response:', results);
            console.log('[AssessmentDetailModal] First finding structure:', results?.[0]);

            setFindings(results || []);
        } catch (err) {
            console.error('[AssessmentDetailModal] Error loading findings:', err);
            const errorInfo = apiUtils.handleApiError(err);
            setError(errorInfo.message);
        } finally {
            setLoading(false);
        }
    };

    const loadResources = async (page = 1, resetSearch = false) => {
        try {
            setTabLoading(prev => ({ ...prev, resources: true }));
            setError(null);

            const searchTerm = resetSearch ? '' : resourceSearch;
            if (resetSearch) {
                setResourceSearch('');
            }

            console.log('[AssessmentDetailModal] Fetching resources for assessment:', assessment.id);

            const params = new URLSearchParams({
                page: page.toString(),
                limit: '50'
            });

            if (searchTerm) params.append('search', searchTerm);
            if (selectedFilters.resourceType) params.append('resourceType', selectedFilters.resourceType);
            if (selectedFilters.resourceGroup) params.append('resourceGroup', selectedFilters.resourceGroup);
            if (selectedFilters.location) params.append('location', selectedFilters.location);
            if (selectedFilters.environment) params.append('environmentFilter', selectedFilters.environment);

            const response = await assessmentApi.getAssessmentResources(assessment.id, params.toString());

            console.log('[AssessmentDetailModal] Raw API response:', response);

            // Force PascalCase reading - backend returns PascalCase
            setResources(response.Resources || []);
            setResourceFilters(response.Filters || {});
            setResourcePage(response.Page || 1);
            setResourceTotalPages(response.TotalPages || 1);
            setResourceTotalCount(response.TotalCount || 0);

            console.log('[AssessmentDetailModal] Loaded resources count:', (response.Resources || []).length);
            console.log('[AssessmentDetailModal] First resource structure:', response.Resources?.[0]);
            console.log('[AssessmentDetailModal] First resource properties:', response.Resources?.[0] ? Object.keys(response.Resources[0]) : 'No resources');
        } catch (err) {
            console.error('[AssessmentDetailModal] Error loading resources:', err);
            const errorInfo = apiUtils.handleApiError(err);
            setError(errorInfo.message);
        } finally {
            setTabLoading(prev => ({ ...prev, resources: false }));
        }
    };

    const handleTabChange = async (tab) => {
        setActiveTab(tab);
        setTabLoading(prev => ({ ...prev, [tab]: true }));

        // Load data for specific tabs
        if (tab === 'resources' && resources.length === 0) {
            await loadResources(1, true);
        } else {
            // Simulate loading for other tabs
            setTimeout(() => {
                setTabLoading(prev => ({ ...prev, [tab]: false }));
            }, 300);
        }
    };

    const handleExport = async (format) => {
        try {
            console.log(`[AssessmentDetailModal] Exporting resources as ${format.toUpperCase()}`);

            const response = await (format === 'csv'
                ? assessmentApi.exportResourcesCsv(assessment.id)
                : assessmentApi.exportResourcesExcel(assessment.id));

            if (!response.ok) {
                throw new Error(`Export failed: ${response.statusText}`);
            }

            // Get filename from Content-Disposition header or create one
            const contentDisposition = response.headers.get('Content-Disposition');
            let filename = `resources.${format}`;

            console.log(`[AssessmentDetailModal] DEBUG - Response headers:`, Array.from(response.headers.entries()));
            console.log(`[AssessmentDetailModal] DEBUG - Content-Disposition raw:`, contentDisposition);
            console.log(`[AssessmentDetailModal] DEBUG - Content-Disposition exists:`, !!contentDisposition);

            if (contentDisposition) {
                console.log(`[AssessmentDetailModal] Content-Disposition header:`, contentDisposition);

                // Try RFC 5987 encoded filename first (filename*=UTF-8'')
                let filenameStarMatch = contentDisposition.match(/filename\*=UTF-8''([^;]+)/i);
                console.log(`[AssessmentDetailModal] DEBUG - filename* regex match:`, filenameStarMatch);

                if (filenameStarMatch && filenameStarMatch[1]) {
                    filename = decodeURIComponent(filenameStarMatch[1]);
                    console.log(`[AssessmentDetailModal] Extracted filename* (UTF-8):`, filename);
                } else {
                    // Fallback to regular filename= (without quotes)
                    const filenameMatch = contentDisposition.match(/filename=([^;]+)/i);
                    console.log(`[AssessmentDetailModal] DEBUG - filename= regex match:`, filenameMatch);

                    if (filenameMatch && filenameMatch[1]) {
                        filename = filenameMatch[1].trim();
                        console.log(`[AssessmentDetailModal] Extracted filename:`, filename);
                    } else {
                        console.log(`[AssessmentDetailModal] DEBUG - No filename match found, using default:`, filename);
                    }
                }
            } else {
                console.log(`[AssessmentDetailModal] DEBUG - No Content-Disposition header found, using default filename:`, filename);
            }

            // Download the file
            const blob = await response.blob();
            const downloadUrl = window.URL.createObjectURL(blob);
            const link = document.createElement('a');
            link.href = downloadUrl;
            link.download = filename;
            document.body.appendChild(link);
            link.click();
            document.body.removeChild(link);
            window.URL.revokeObjectURL(downloadUrl);

            console.log(`[AssessmentDetailModal] Successfully exported ${format.toUpperCase()}: ${filename}`);
        } catch (error) {
            console.error(`[AssessmentDetailModal] Export ${format} failed:`, error);
            alert(`Failed to export ${format.toUpperCase()}: ${error.message}`);
        }
    };

    const getResourceIcon = (resourceTypeName) => {
        if (!resourceTypeName) return <FileText size={16} className="text-gray-400" />;

        const type = resourceTypeName.toLowerCase();
        if (type.includes('virtualmachine') || type.includes('vm')) {
            return <Server size={16} className="text-blue-400" />;
        }
        if (type.includes('database') || type.includes('sql')) {
            return <Database size={16} className="text-green-400" />;
        }
        if (type.includes('network') || type.includes('vnet')) {
            return <Network size={16} className="text-purple-400" />;
        }
        return <FileText size={16} className="text-gray-400" />;
    };

    if (!isOpen || !assessment) return null;

    const getSeverityIcon = (severity) => {
        const sev = severity?.toLowerCase();
        switch (sev) {
            case 'critical': return <XCircle size={16} className="text-red-500" />;
            case 'high': return <XCircle size={16} className="text-red-400" />;
            case 'medium': return <AlertTriangle size={16} className="text-yellow-400" />;
            case 'low': return <CheckCircle size={16} className="text-blue-400" />;
            default: return <FileText size={16} className="text-gray-400" />;
        }
    };

    const getSeverityColor = (severity) => {
        const sev = severity?.toLowerCase();
        switch (sev) {
            case 'critical': return 'bg-red-700 text-white';
            case 'high': return 'bg-red-600 text-white';
            case 'medium': return 'bg-yellow-600 text-black';
            case 'low': return 'bg-blue-600 text-white';
            default: return 'bg-gray-600 text-white';
        }
    };

    const findingsByCategory = findings.reduce((acc, finding) => {
        // Try multiple possible field names for category
        const category = finding.category || finding.Category || 'Other';
        if (!acc[category]) acc[category] = [];
        acc[category].push(finding);
        return acc;
    }, {});

    // Helper function to extract duration from assessment object
    const getAssessmentDuration = () => {
        console.log('[AssessmentDetailModal] Assessment object:', assessment);
        console.log('[AssessmentDetailModal] Available keys:', Object.keys(assessment));

        // Try to get dates with multiple field name variations (PascalCase and camelCase)
        const startDate = assessment.startedDate ||
            assessment.StartedDate ||
            assessment.date ||
            assessment.Date;

        const endDate = assessment.completedDate ||
            assessment.CompletedDate ||
            assessment.endDate ||
            assessment.EndDate;

        console.log('[AssessmentDetailModal] Extracted startDate:', startDate);
        console.log('[AssessmentDetailModal] Extracted endDate:', endDate);
        console.log('[AssessmentDetailModal] StartedDate (PascalCase):', assessment.StartedDate);
        console.log('[AssessmentDetailModal] CompletedDate (PascalCase):', assessment.CompletedDate);

        if (startDate && endDate) {
            const start = new Date(startDate);
            const end = new Date(endDate);
            const diffMs = end - start;

            console.log('[AssessmentDetailModal] Duration calculation:', {
                start: start.toISOString(),
                end: end.toISOString(),
                diffMs: diffMs,
                diffSeconds: diffMs / 1000
            });

            if (diffMs < 1000) {
                // Less than 1 second
                return `${diffMs}ms`;
            } else if (diffMs < 60000) {
                // Less than 1 minute - show seconds with decimal
                const totalSeconds = Math.round(diffMs / 100) / 10;
                return `${totalSeconds}s`;
            } else {
                // More than 1 minute - show minutes and seconds
                const diffMins = Math.floor(diffMs / 60000);
                const diffSecs = Math.floor((diffMs % 60000) / 1000);
                return `${diffMins}m ${diffSecs}s`;
            }
        }

        // If we only have a start date, show "In Progress"
        if (startDate && !endDate) {
            return 'In Progress';
        }

        return 'Unknown';
    };

    const renderFindingCard = (finding, index) => {
        console.log('[AssessmentDetailModal] Rendering finding:', finding);

        // Try multiple possible field names for each property (backend might use PascalCase)
        const getIssueTitle = () => {
            return finding.issue ||
                finding.Issue ||
                finding.title ||
                finding.Title ||
                finding.name ||
                finding.Name ||
                finding.description ||
                finding.Description ||
                'Governance Issue Found';
        };

        const getResourceName = () => {
            return finding.resourceName ||
                finding.ResourceName ||
                finding.resource ||
                finding.Resource ||
                finding.resourceId ||
                finding.ResourceId ||
                'Azure Resource';
        };

        const getSeverity = () => {
            return finding.severity ||
                finding.Severity ||
                'Medium';
        };

        const getRecommendation = () => {
            return finding.recommendation ||
                finding.Recommendation ||
                'Review and update this resource to meet governance standards.';
        };

        const getResourceType = () => {
            return finding.resourceType ||
                finding.ResourceType ||
                finding.type ||
                finding.Type ||
                'Unknown';
        };

        const getEstimatedEffort = () => {
            return finding.estimatedEffort ||
                finding.EstimatedEffort ||
                'Medium';
        };

        const issueTitle = getIssueTitle();
        const resourceName = getResourceName();
        const severity = getSeverity();
        const recommendation = getRecommendation();
        const resourceType = getResourceType();
        const estimatedEffort = getEstimatedEffort();

        return (
            <div key={finding.id || finding.Id || finding.findingId || finding.FindingId || index}
                className="bg-gray-700 rounded p-4 border border-gray-600">
                <div className="flex items-start justify-between mb-3">
                    <div className="flex items-center space-x-3">
                        {getSeverityIcon(severity)}
                        <div>
                            <h4 className="font-medium text-white text-sm">
                                {issueTitle}
                            </h4>
                            <p className="text-gray-400 text-xs">
                                {resourceName}
                            </p>
                        </div>
                    </div>
                    <span className={`px-2 py-1 rounded text-xs font-medium ${getSeverityColor(severity)}`}>
                        {severity}
                    </span>
                </div>

                {/* Issue Description */}
                <div className="mb-3">
                    <p className="text-gray-300 text-sm">
                        {issueTitle !== recommendation ? issueTitle : 'This resource does not meet governance standards.'}
                    </p>
                </div>

                {/* Recommendation */}
                <div className="mb-3">
                    <p className="text-gray-400 text-sm">
                        <strong className="text-gray-300">Recommendation:</strong> {recommendation}
                    </p>
                </div>

                {/* Resource Details */}
                <div className="flex flex-wrap gap-4 text-xs text-gray-500">
                    <span>Type: {resourceType}</span>
                    {finding.resourceId && (
                        <span>ID: {finding.resourceId.substring(0, 20)}...</span>
                    )}
                    <span>Effort: {estimatedEffort}</span>
                </div>
            </div>
        );
    };

    return createPortal(
        // FIXED: Much higher z-index to be above header (z-30)
        <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-[50] p-4">
            {/* ENHANCED: Increased modal size for better resource viewing - 90vw width, 90vh height */}
            <div className="bg-gray-900 border border-gray-800 rounded w-[90vw] h-[90vh] overflow-hidden flex flex-col">
                {/* ENHANCED Header - Cleaner presentation */}
                <div className="flex items-center justify-between p-6 border-b border-gray-800 flex-shrink-0">
                    <div>
                        <h2 className="text-xl font-semibold text-white">{assessment.name}</h2>
                        <div className="flex items-center space-x-3 text-sm text-gray-400 mt-1">
                            <span>{assessment.environment || 'Production'} Environment</span>
                            <span className="text-gray-600">•</span>
                            <span>Score: {assessment.score ? `${assessment.score}%` : 'N/A'}</span>
                        </div>
                    </div>
                    <button
                        onClick={onClose}
                        className="p-2 rounded hover:bg-gray-800 text-gray-400 hover:text-white transition-colors"
                    >
                        <X size={20} />
                    </button>
                </div>

                {/* Tabs */}
                <div className="border-b border-gray-800 flex-shrink-0">
                    <nav className="flex space-x-8 px-6">
                        {['overview', 'findings', 'recommendations', 'resources'].map((tab) => (
                            <button
                                key={tab}
                                onClick={() => handleTabChange(tab)}
                                className={`py-4 px-1 border-b-2 font-medium text-sm capitalize transition-colors relative ${activeTab === tab
                                    ? 'border-yellow-600 text-yellow-600'
                                    : 'border-transparent text-gray-500 hover:text-gray-300'
                                    }`}
                            >
                                {tab}
                                {tabLoading[tab] && (
                                    <div className="absolute -top-1 -right-1">
                                        <div className="w-2 h-2 bg-yellow-600 rounded-full animate-pulse"></div>
                                    </div>
                                )}
                            </button>
                        ))}
                    </nav>
                </div>

                {/* Content Area - ENHANCED: Better scrolling and height management */}
                <div className="flex-1 overflow-y-auto">
                    <div className="p-6">
                        {/* Overview Tab */}
                        {activeTab === 'overview' && (
                            <div className="space-y-6">
                                {/* Score Summary */}
                                <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
                                    <div className="bg-gray-800 rounded p-4 border border-gray-700">
                                        <div className="flex items-center justify-between">
                                            <div>
                                                <p className="text-sm text-gray-400">Overall Score</p>
                                                <p className="text-2xl font-bold text-white">
                                                    {assessment.score ? `${assessment.score}%` : 'N/A'}
                                                </p>
                                            </div>
                                            <BarChart3 size={24} className="text-yellow-600" />
                                        </div>
                                    </div>
                                    <div className="bg-gray-800 rounded p-4 border border-gray-700">
                                        <p className="text-sm text-gray-400">Resources Analyzed</p>
                                        <p className="text-2xl font-bold text-white">{assessment.resourceCount || 'N/A'}</p>
                                    </div>
                                    <div className="bg-gray-800 rounded p-4 border border-gray-700">
                                        <p className="text-sm text-gray-400">Issues Found</p>
                                        <p className="text-2xl font-bold text-white">{assessment.issuesCount || findings.length}</p>
                                    </div>
                                    <div className="bg-gray-800 rounded p-4 border border-gray-700">
                                        <p className="text-sm text-gray-400">Assessment Type</p>
                                        <p className="text-lg font-semibold text-white">{assessment.type || 'Unknown'}</p>
                                    </div>
                                </div>

                                {/* Assessment Details - FIXED Duration */}
                                <div className="bg-gray-800 rounded p-6 border border-gray-700">
                                    <h3 className="text-lg font-semibold text-white mb-4">Assessment Details</h3>
                                    <div className="grid grid-cols-1 md:grid-cols-2 gap-6 text-sm">
                                        <div>
                                            <p className="text-gray-400 mb-1">Started</p>
                                            <p className="text-white">{assessment.date || assessment.startedDate || 'Unknown'}</p>
                                        </div>
                                        <div>
                                            <p className="text-gray-400 mb-1">Duration</p>
                                            <p className="text-white">{getAssessmentDuration()}</p>
                                        </div>
                                        <div>
                                            <p className="text-gray-400 mb-1">Status</p>
                                            <span className={`px-2 py-1 rounded text-xs font-medium ${assessment.status === 'Completed' ? 'bg-green-700 text-white' :
                                                assessment.status === 'In Progress' ? 'bg-yellow-700 text-white' :
                                                    'bg-gray-700 text-white'
                                                }`}>
                                                {assessment.status || 'Unknown'}
                                            </span>
                                        </div>
                                        <div>
                                            <p className="text-gray-400 mb-1">Environment</p>
                                            <p className="text-white">{assessment.environment || 'Unknown'}</p>
                                        </div>
                                        {/* Add more details if available */}
                                        {assessment.completedDate && (
                                            <div>
                                                <p className="text-gray-400 mb-1">Completed</p>
                                                <p className="text-white">{assessment.completedDate}</p>
                                            </div>
                                        )}
                                        {assessment.subscriptionCount && (
                                            <div>
                                                <p className="text-gray-400 mb-1">Subscriptions</p>
                                                <p className="text-white">{assessment.subscriptionCount}</p>
                                            </div>
                                        )}
                                    </div>
                                </div>

                                {/* Quick Stats */}
                                {findings.length > 0 && (
                                    <div className="bg-gray-800 rounded p-6 border border-gray-700">
                                        <h3 className="text-lg font-semibold text-white mb-4">Findings Summary</h3>
                                        <div className="grid grid-cols-2 md:grid-cols-4 gap-4 text-center">
                                            {['Critical', 'High', 'Medium', 'Low'].map(severity => {
                                                const count = findings.filter(f => {
                                                    const findingSeverity = (f.severity || f.Severity || '').toLowerCase();
                                                    return findingSeverity === severity.toLowerCase();
                                                }).length;
                                                return (
                                                    <div key={severity} className="bg-gray-700 rounded p-3">
                                                        <p className={`text-2xl font-bold ${getSeverityColor(severity).includes('red') ? 'text-red-400' :
                                                            getSeverityColor(severity).includes('yellow') ? 'text-yellow-400' :
                                                                getSeverityColor(severity).includes('blue') ? 'text-blue-400' : 'text-gray-400'}`}>
                                                            {count}
                                                        </p>
                                                        <p className="text-sm text-gray-400">{severity}</p>
                                                    </div>
                                                );
                                            })}
                                        </div>
                                    </div>
                                )}
                            </div>
                        )}

                        {/* ENHANCED Findings Tab */}
                        {activeTab === 'findings' && (
                            <div className="space-y-6">
                                {loading && (
                                    <div className="text-center py-12">
                                        <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-yellow-600 mx-auto mb-4"></div>
                                        <p className="text-gray-400">Loading findings...</p>
                                    </div>
                                )}

                                {error && (
                                    <div className="bg-red-900 border border-red-700 rounded p-4">
                                        <p className="text-red-200">{error}</p>
                                        <button
                                            onClick={loadFindings}
                                            className="mt-2 px-3 py-1 bg-red-700 text-white rounded text-sm hover:bg-red-600 transition-colors"
                                        >
                                            Retry
                                        </button>
                                    </div>
                                )}

                                {!loading && !error && findings.length === 0 && (
                                    <div className="text-center py-12">
                                        <CheckCircle size={48} className="text-green-400 mx-auto mb-4" />
                                        <h3 className="text-lg font-semibold text-white mb-2">No Issues Found</h3>
                                        <p className="text-gray-400">This assessment found no governance issues.</p>
                                    </div>
                                )}

                                {!loading && !error && Object.entries(findingsByCategory).map(([category, categoryFindings]) => (
                                    <div key={category} className="bg-gray-800 rounded p-6 border border-gray-700">
                                        <div className="flex items-center justify-between mb-4">
                                            <h3 className="text-lg font-semibold text-white capitalize">
                                                {category} ({categoryFindings.length})
                                            </h3>
                                            <div className="flex items-center space-x-2">
                                                <span className="text-sm text-gray-400">
                                                    {categoryFindings.filter(f => (f.severity || f.Severity || '').toLowerCase() === 'high').length} High,{' '}
                                                    {categoryFindings.filter(f => (f.severity || f.Severity || '').toLowerCase() === 'medium').length} Medium,{' '}
                                                    {categoryFindings.filter(f => (f.severity || f.Severity || '').toLowerCase() === 'low').length} Low
                                                </span>
                                            </div>
                                        </div>
                                        <div className="space-y-3">
                                            {categoryFindings.map((finding, index) => renderFindingCard(finding, index))}
                                        </div>
                                    </div>
                                ))}
                            </div>
                        )}

                        {/* ENHANCED Recommendations Tab */}
                        {activeTab === 'recommendations' && (
                            <div className="space-y-6">
                                <div className="bg-gray-800 rounded p-6 border border-gray-700">
                                    <div className="flex items-center justify-between mb-4">
                                        <h3 className="text-lg font-semibold text-white">Priority Recommendations</h3>
                                        <div className="flex space-x-2">
                                            <button className="px-3 py-1 bg-gray-700 text-white rounded text-sm hover:bg-gray-600 transition-colors flex items-center space-x-1">
                                                <Download size={14} />
                                                <span>Export PDF</span>
                                            </button>
                                            <button className="px-3 py-1 bg-gray-700 text-white rounded text-sm hover:bg-gray-600 transition-colors flex items-center space-x-1">
                                                <Download size={14} />
                                                <span>Export DOCX</span>
                                            </button>
                                        </div>
                                    </div>

                                    <div className="space-y-4">
                                        {/* High Priority Recommendations */}
                                        <div className="bg-red-900/20 border border-red-700 rounded p-4">
                                            <div className="flex items-center space-x-2 mb-3">
                                                <XCircle size={16} className="text-red-400" />
                                                <h4 className="font-medium text-white">Critical Issues Require Immediate Attention</h4>
                                            </div>
                                            <p className="text-gray-300 text-sm mb-3">
                                                {findings.filter(f => {
                                                    const sev = (f.severity || f.Severity || '').toLowerCase();
                                                    return sev === 'critical' || sev === 'high';
                                                }).length} critical/high severity issues found.
                                                Address these immediately to improve security and compliance.
                                            </p>
                                            <div className="flex items-center justify-between text-sm">
                                                <span className="text-red-300"><strong>Priority:</strong> Critical</span>
                                                <span className="text-gray-400"><strong>Effort:</strong> High</span>
                                                <span className="text-gray-400"><strong>Timeline:</strong> 1-2 weeks</span>
                                            </div>
                                        </div>

                                        {/* Tagging Recommendation */}
                                        <div className="bg-yellow-900/20 border border-yellow-700 rounded p-4">
                                            <div className="flex items-center space-x-2 mb-3">
                                                <AlertTriangle size={16} className="text-yellow-400" />
                                                <h4 className="font-medium text-white">Improve Resource Tagging Strategy</h4>
                                            </div>
                                            <p className="text-gray-300 text-sm mb-3">
                                                Many resources lack proper tags for cost management and organization.
                                                Implement a comprehensive tagging strategy for better governance.
                                            </p>
                                            <div className="flex items-center justify-between text-sm">
                                                <span className="text-yellow-300"><strong>Priority:</strong> High</span>
                                                <span className="text-gray-400"><strong>Effort:</strong> Medium</span>
                                                <span className="text-gray-400"><strong>Timeline:</strong> 2-4 weeks</span>
                                            </div>
                                        </div>

                                        {/* Naming Convention Recommendation */}
                                        <div className="bg-blue-900/20 border border-blue-700 rounded p-4">
                                            <div className="flex items-center space-x-2 mb-3">
                                                <FileText size={16} className="text-blue-400" />
                                                <h4 className="font-medium text-white">Standardize Naming Conventions</h4>
                                            </div>
                                            <p className="text-gray-300 text-sm mb-3">
                                                Current naming compliance: {Math.round(assessment.score || 0)}%.
                                                Implement consistent naming patterns to improve resource identification and management.
                                            </p>
                                            <div className="flex items-center justify-between text-sm">
                                                <span className="text-blue-300"><strong>Priority:</strong> Medium</span>
                                                <span className="text-gray-400"><strong>Effort:</strong> Low</span>
                                                <span className="text-gray-400"><strong>Timeline:</strong> 1-2 weeks</span>
                                            </div>
                                        </div>
                                    </div>
                                </div>

                                {/* Action Plan */}
                                <div className="bg-gray-800 rounded p-6 border border-gray-700">
                                    <h3 className="text-lg font-semibold text-white mb-4">Recommended Action Plan</h3>
                                    <div className="space-y-3">
                                        <div className="flex items-center space-x-3 p-3 bg-gray-700 rounded">
                                            <div className="w-6 h-6 bg-red-600 text-white rounded-full flex items-center justify-center text-xs font-bold">1</div>
                                            <span className="text-gray-300">Address all critical and high severity findings immediately</span>
                                        </div>
                                        <div className="flex items-center space-x-3 p-3 bg-gray-700 rounded">
                                            <div className="w-6 h-6 bg-yellow-600 text-black rounded-full flex items-center justify-center text-xs font-bold">2</div>
                                            <span className="text-gray-300">Implement tagging strategy for untagged resources</span>
                                        </div>
                                        <div className="flex items-center space-x-3 p-3 bg-gray-700 rounded">
                                            <div className="w-6 h-6 bg-blue-600 text-white rounded-full flex items-center justify-center text-xs font-bold">3</div>
                                            <span className="text-gray-300">Establish naming convention standards and update non-compliant resources</span>
                                        </div>
                                        <div className="flex items-center space-x-3 p-3 bg-gray-700 rounded">
                                            <div className="w-6 h-6 bg-green-600 text-white rounded-full flex items-center justify-center text-xs font-bold">4</div>
                                            <span className="text-gray-300">Schedule regular assessments to maintain compliance</span>
                                        </div>
                                    </div>
                                </div>
                            </div>
                        )}

                        {/* NEW: Resources Tab */}
                        {activeTab === 'resources' && (
                            <div className="space-y-6">
                                {/* Resources Header and Controls */}
                                <div className="bg-gray-800 rounded p-6 border border-gray-700">
                                    <div className="flex items-center justify-between mb-4">
                                        <h3 className="text-lg font-semibold text-white">Azure Resources</h3>
                                        <div className="flex space-x-2">
                                            <button
                                                onClick={() => handleExport('csv')}
                                                className="px-3 py-1 bg-gray-700 text-white rounded text-sm hover:bg-gray-600 transition-colors flex items-center space-x-1"
                                            >
                                                <Download size={14} />
                                                <span>Export CSV</span>
                                            </button>
                                            <button
                                                onClick={() => handleExport('xlsx')}
                                                className="px-3 py-1 bg-yellow-600 text-black rounded text-sm hover:bg-yellow-700 transition-colors flex items-center space-x-1"
                                            >
                                                <Download size={14} />
                                                <span>Export Excel</span>
                                            </button>
                                        </div>
                                    </div>

                                    {/* Search and Filters */}
                                    <div className="grid grid-cols-1 md:grid-cols-5 gap-4 mb-4">
                                        <div className="relative">
                                            <Search size={16} className="absolute left-3 top-1/2 transform -translate-y-1/2 text-gray-400" />
                                            <input
                                                type="text"
                                                placeholder="Search resources..."
                                                value={resourceSearch}
                                                onChange={(e) => setResourceSearch(e.target.value)}
                                                onKeyPress={(e) => e.key === 'Enter' && loadResources(1)}
                                                className="w-full pl-10 pr-4 py-2 bg-gray-900 border border-gray-600 rounded text-white text-sm focus:border-yellow-600 focus:outline-none"
                                            />
                                        </div>

                                        <select
                                            value={selectedFilters.resourceType}
                                            onChange={(e) => setSelectedFilters(prev => ({ ...prev, resourceType: e.target.value }))}
                                            className="px-3 py-2 bg-gray-900 border border-gray-600 rounded text-white text-sm focus:border-yellow-600 focus:outline-none"
                                        >
                                            <option value="">All Types</option>
                                            {Object.entries(resourceFilters.ResourceTypes || {}).map(([type, count]) => (
                                                <option key={type} value={type}>{type} ({count})</option>
                                            ))}
                                        </select>

                                        <select
                                            value={selectedFilters.resourceGroup}
                                            onChange={(e) => setSelectedFilters(prev => ({ ...prev, resourceGroup: e.target.value }))}
                                            className="px-3 py-2 bg-gray-900 border border-gray-600 rounded text-white text-sm focus:border-yellow-600 focus:outline-none"
                                        >
                                            <option value="">All Resource Groups</option>
                                            {Object.entries(resourceFilters.ResourceGroups || {}).map(([rg, count]) => (
                                                <option key={rg} value={rg}>{rg} ({count})</option>
                                            ))}
                                        </select>

                                        <select
                                            value={selectedFilters.location}
                                            onChange={(e) => setSelectedFilters(prev => ({ ...prev, location: e.target.value }))}
                                            className="px-3 py-2 bg-gray-900 border border-gray-600 rounded text-white text-sm focus:border-yellow-600 focus:outline-none"
                                        >
                                            <option value="">All Locations</option>
                                            {Object.entries(resourceFilters.Locations || {}).map(([loc, count]) => (
                                                <option key={loc} value={loc}>{loc} ({count})</option>
                                            ))}
                                        </select>

                                        <button
                                            onClick={() => loadResources(1)}
                                            className="px-4 py-2 bg-yellow-600 hover:bg-yellow-700 text-black rounded text-sm font-medium transition-colors"
                                        >
                                            Apply Filters
                                        </button>
                                    </div>

                                    {/* Resource Count and Clear Filters */}
                                    <div className="flex items-center justify-between text-sm text-gray-400 mb-4">
                                        <span>
                                            Showing {resources.length} of {resourceTotalCount} resources
                                        </span>
                                        <button
                                            onClick={() => {
                                                setSelectedFilters({ resourceType: '', resourceGroup: '', location: '', environment: '' });
                                                setResourceSearch('');
                                                loadResources(1, true);
                                            }}
                                            className="text-yellow-600 hover:text-yellow-500 transition-colors"
                                        >
                                            Clear Filters
                                        </button>
                                    </div>
                                </div>

                                {/* Resources Table */}
                                {tabLoading.resources ? (
                                    <div className="text-center py-12">
                                        <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-yellow-600 mx-auto mb-4"></div>
                                        <p className="text-gray-400">Loading resources...</p>
                                    </div>
                                ) : resources.length > 0 ? (
                                    <div className="bg-gray-800 rounded border border-gray-700 overflow-hidden">
                                        <div className="overflow-x-auto">
                                            <table className="w-full">
                                                <thead className="bg-gray-700">
                                                    <tr>
                                                        <th className="px-4 py-3 text-left text-xs font-medium text-gray-300 uppercase tracking-wider">Resource</th>
                                                        <th className="px-4 py-3 text-left text-xs font-medium text-gray-300 uppercase tracking-wider">Type</th>
                                                        <th className="px-4 py-3 text-left text-xs font-medium text-gray-300 uppercase tracking-wider">Resource Group</th>
                                                        <th className="px-4 py-3 text-left text-xs font-medium text-gray-300 uppercase tracking-wider">Location</th>
                                                        <th className="px-4 py-3 text-left text-xs font-medium text-gray-300 uppercase tracking-wider">Environment</th>
                                                        <th className="px-4 py-3 text-left text-xs font-medium text-gray-300 uppercase tracking-wider">Tags</th>
                                                    </tr>
                                                </thead>
                                                <tbody className="divide-y divide-gray-700">
                                                    {resources.map((resource, index) => {
                                                        // Debug log for table rendering
                                                        if (index === 0) {
                                                            console.log('[TABLE DEBUG] Rendering first resource:', {
                                                                Name: resource.Name,
                                                                ResourceTypeName: resource.ResourceTypeName,
                                                                ResourceGroup: resource.ResourceGroup,
                                                                Location: resource.Location,
                                                                Environment: resource.Environment,
                                                                TagCount: resource.TagCount
                                                            });
                                                        }

                                                        return (
                                                            <tr key={resource.Id || index} className="hover:bg-gray-700 transition-colors">
                                                                <td className="px-4 py-3">
                                                                    <div className="flex items-center space-x-3">
                                                                        {getResourceIcon(resource.ResourceTypeName)}
                                                                        <div>
                                                                            <p className="text-sm font-medium text-white">{resource.Name}</p>
                                                                            {resource.Kind && resource.Kind !== "" && resource.Kind !== "null" && (
                                                                                <p className="text-xs text-gray-400">{resource.Kind}</p>
                                                                            )}
                                                                        </div>
                                                                    </div>
                                                                </td>
                                                                <td className="px-4 py-3">
                                                                    <span className="text-sm text-gray-300">{resource.ResourceTypeName}</span>
                                                                </td>
                                                                <td className="px-4 py-3">
                                                                    <span className="text-sm text-gray-300">{resource.ResourceGroup || 'N/A'}</span>
                                                                </td>
                                                                <td className="px-4 py-3">
                                                                    <span className="text-sm text-gray-300">{resource.Location || 'N/A'}</span>
                                                                </td>
                                                                <td className="px-4 py-3">
                                                                    {resource.Environment ? (
                                                                        <span className="inline-flex items-center px-2 py-1 rounded-full text-xs font-medium bg-blue-600 text-white">
                                                                            {resource.Environment}
                                                                        </span>
                                                                    ) : (
                                                                        <span className="text-gray-500 text-sm">Unknown</span>
                                                                    )}
                                                                </td>
                                                                <td className="px-4 py-3">
                                                                    <div className="flex items-center space-x-2">
                                                                        <span className="text-sm text-gray-300">{resource.TagCount} tags</span>
                                                                        {resource.TagCount > 0 && resource.Tags && Object.keys(resource.Tags).length > 0 && (
                                                                            <div className="flex flex-wrap gap-1">
                                                                                {Object.entries(resource.Tags).slice(0, 2).map(([key, value]) => (
                                                                                    <span key={key} className="inline-flex items-center px-1.5 py-0.5 rounded text-xs font-medium bg-gray-600 text-gray-200">
                                                                                        {key}: {value}
                                                                                    </span>
                                                                                ))}
                                                                                {resource.TagCount > 2 && (
                                                                                    <span className="text-xs text-gray-400">+{resource.TagCount - 2} more</span>
                                                                                )}
                                                                            </div>
                                                                        )}
                                                                    </div>
                                                                </td>
                                                            </tr>
                                                        );
                                                    })}
                                                </tbody>
                                            </table>
                                        </div>

                                        {/* Pagination */}
                                        {resourceTotalPages > 1 && (
                                            <div className="px-4 py-3 bg-gray-700 border-t border-gray-600 flex items-center justify-between">
                                                <div className="text-sm text-gray-400">
                                                    Page {resourcePage} of {resourceTotalPages}
                                                </div>
                                                <div className="flex items-center space-x-2">
                                                    <button
                                                        onClick={() => loadResources(resourcePage - 1)}
                                                        disabled={resourcePage <= 1}
                                                        className="p-2 rounded hover:bg-gray-600 text-gray-400 hover:text-white transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
                                                    >
                                                        <ChevronLeft size={16} />
                                                    </button>
                                                    <span className="text-sm text-gray-300">
                                                        {resourcePage} / {resourceTotalPages}
                                                    </span>
                                                    <button
                                                        onClick={() => loadResources(resourcePage + 1)}
                                                        disabled={resourcePage >= resourceTotalPages}
                                                        className="p-2 rounded hover:bg-gray-600 text-gray-400 hover:text-white transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
                                                    >
                                                        <ChevronRight size={16} />
                                                    </button>
                                                </div>
                                            </div>
                                        )}
                                    </div>
                                ) : (
                                    <div className="text-center py-12">
                                        <Server size={48} className="text-gray-600 mx-auto mb-4" />
                                        <h3 className="text-lg font-semibold text-white mb-2">No Resources Found</h3>
                                        <p className="text-gray-400">
                                            {Object.keys(selectedFilters).some(key => selectedFilters[key]) || resourceSearch
                                                ? "Try adjusting your search or filters."
                                                : "No Azure resources were found for this assessment."
                                            }
                                        </p>
                                    </div>
                                )}
                            </div>
                        )}
                    </div>
                </div>

                {/* Footer */}
                <div className="border-t border-gray-800 p-6 flex justify-between items-center flex-shrink-0">
                    <div className="text-sm text-gray-400">
                        Last updated: {new Date().toLocaleDateString()}
                    </div>
                    <div className="flex space-x-3">
                        <button
                            onClick={onClose}
                            className="px-4 py-2 text-gray-300 hover:text-white transition-colors"
                        >
                            Close
                        </button>
                        <button className="px-4 py-2 bg-yellow-600 hover:bg-yellow-700 text-black rounded font-medium transition-colors">
                            Export Report
                        </button>
                    </div>
                </div>
            </div>
        </div>
        , document.body
    );
};

export default AssessmentDetailModal;