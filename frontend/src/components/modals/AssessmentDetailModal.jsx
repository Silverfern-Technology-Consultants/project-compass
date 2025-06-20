import React, { useState, useEffect } from 'react';
import { X, CheckCircle, AlertTriangle, XCircle, FileText, BarChart3 } from 'lucide-react';
import { assessmentApi, apiUtils } from '../../services/apiService';


const AssessmentDetailModal = ({ isOpen, onClose, assessment }) => {
    const [findings, setFindings] = useState([]);
    const [loading, setLoading] = useState(false);
    const [error, setError] = useState(null);
    const [activeTab, setActiveTab] = useState('overview');

    useEffect(() => {
        if (isOpen && assessment?.id) {
            loadFindings();
        }
    }, [isOpen, assessment]);

    const loadFindings = async () => {
        try {
            setLoading(true);
            setError(null);
            const results = await assessmentApi.getAssessmentFindings(assessment.id);
            setFindings(results);
        } catch (err) {
            const errorInfo = apiUtils.handleApiError(err);
            setError(errorInfo.message);
        } finally {
            setLoading(false);
        }
    };

    if (!isOpen || !assessment) return null;

    const getSeverityIcon = (severity) => {
        switch (severity?.toLowerCase()) {
            case 'high': return <XCircle size={16} className="text-red-400" />;
            case 'medium': return <AlertTriangle size={16} className="text-yellow-400" />;
            case 'low': return <CheckCircle size={16} className="text-blue-400" />;
            default: return <FileText size={16} className="text-gray-400" />;
        }
    };

    const getSeverityColor = (severity) => {
        switch (severity?.toLowerCase()) {
            case 'high': return 'bg-red-600 text-white';
            case 'medium': return 'bg-yellow-600 text-black';
            case 'low': return 'bg-blue-600 text-white';
            default: return 'bg-gray-600 text-white';
        }
    };

    const findingsByCategory = findings.reduce((acc, finding) => {
        const category = finding.category || 'Other';
        if (!acc[category]) acc[category] = [];
        acc[category].push(finding);
        return acc;
    }, {});

    return (
        <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50">
            <div className="bg-gray-900 border border-gray-800 rounded w-full max-w-6xl mx-4 max-h-[90vh] overflow-hidden">
                <div className="flex items-center justify-between p-6 border-b border-gray-800">
                    <div>
                        <h2 className="text-xl font-semibold text-white">{assessment.name}</h2>
                        <p className="text-gray-400">{assessment.environment} Environment</p>
                    </div>
                    <button
                        onClick={onClose}
                        className="p-2 rounded hover:bg-gray-800 text-gray-400 hover:text-white"
                    >
                        <X size={20} />
                    </button>
                </div>

                {/* Tabs */}
                <div className="border-b border-gray-800">
                    <nav className="flex space-x-8 px-6">
                        {['overview', 'findings', 'recommendations'].map((tab) => (
                            <button
                                key={tab}
                                onClick={() => setActiveTab(tab)}
                                className={`py-4 px-1 border-b-2 font-medium text-sm capitalize transition-colors ${activeTab === tab
                                        ? 'border-yellow-600 text-yellow-600'
                                        : 'border-transparent text-gray-500 hover:text-gray-300'
                                    }`}
                            >
                                {tab}
                            </button>
                        ))}
                    </nav>
                </div>

                <div className="p-6 overflow-y-auto max-h-[60vh]">
                    {/* Overview Tab */}
                    {activeTab === 'overview' && (
                        <div className="space-y-6">
                            {/* Score Summary */}
                            <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
                                <div className="bg-gray-800 rounded p-4">
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
                                <div className="bg-gray-800 rounded p-4">
                                    <p className="text-sm text-gray-400">Resources Analyzed</p>
                                    <p className="text-2xl font-bold text-white">{assessment.resourceCount}</p>
                                </div>
                                <div className="bg-gray-800 rounded p-4">
                                    <p className="text-sm text-gray-400">Issues Found</p>
                                    <p className="text-2xl font-bold text-white">{assessment.issuesCount}</p>
                                </div>
                                <div className="bg-gray-800 rounded p-4">
                                    <p className="text-sm text-gray-400">Assessment Type</p>
                                    <p className="text-lg font-semibold text-white">{assessment.type}</p>
                                </div>
                            </div>

                            {/* Assessment Details */}
                            <div className="bg-gray-800 rounded p-6">
                                <h3 className="text-lg font-semibold text-white mb-4">Assessment Details</h3>
                                <div className="grid grid-cols-1 md:grid-cols-2 gap-4 text-sm">
                                    <div>
                                        <p className="text-gray-400">Started</p>
                                        <p className="text-white">{assessment.date}</p>
                                    </div>
                                    <div>
                                        <p className="text-gray-400">Duration</p>
                                        <p className="text-white">{assessment.duration}</p>
                                    </div>
                                    <div>
                                        <p className="text-gray-400">Status</p>
                                        <p className="text-white">{assessment.status}</p>
                                    </div>
                                    <div>
                                        <p className="text-gray-400">Environment</p>
                                        <p className="text-white">{assessment.environment}</p>
                                    </div>
                                </div>
                            </div>
                        </div>
                    )}

                    {/* Findings Tab */}
                    {activeTab === 'findings' && (
                        <div className="space-y-6">
                            {loading && (
                                <div className="text-center py-8">
                                    <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-yellow-600 mx-auto mb-4"></div>
                                    <p className="text-gray-400">Loading findings...</p>
                                </div>
                            )}

                            {error && (
                                <div className="bg-red-900 border border-red-700 rounded p-4">
                                    <p className="text-red-200">{error}</p>
                                </div>
                            )}

                            {!loading && !error && Object.keys(findingsByCategory).length === 0 && (
                                <div className="text-center py-8">
                                    <CheckCircle size={48} className="text-green-400 mx-auto mb-4" />
                                    <h3 className="text-lg font-semibold text-white mb-2">No Issues Found</h3>
                                    <p className="text-gray-400">This assessment found no governance issues.</p>
                                </div>
                            )}

                            {!loading && !error && Object.entries(findingsByCategory).map(([category, categoryFindings]) => (
                                <div key={category} className="bg-gray-800 rounded p-6">
                                    <h3 className="text-lg font-semibold text-white mb-4 capitalize">
                                        {category} ({categoryFindings.length})
                                    </h3>
                                    <div className="space-y-3">
                                        {categoryFindings.slice(0, 10).map((finding, index) => (
                                            <div key={finding.id || index} className="bg-gray-700 rounded p-4">
                                                <div className="flex items-start justify-between mb-2">
                                                    <div className="flex items-center space-x-2">
                                                        {getSeverityIcon(finding.severity)}
                                                        <h4 className="font-medium text-white">{finding.resourceName}</h4>
                                                    </div>
                                                    <span className={`px-2 py-1 rounded text-xs font-medium ${getSeverityColor(finding.severity)}`}>
                                                        {finding.severity}
                                                    </span>
                                                </div>
                                                <p className="text-gray-300 text-sm mb-2">{finding.issue}</p>
                                                <p className="text-gray-400 text-sm">
                                                    <strong>Recommendation:</strong> {finding.recommendation}
                                                </p>
                                                <p className="text-gray-500 text-xs mt-2">
                                                    Resource: {finding.resourceType}
                                                </p>
                                            </div>
                                        ))}
                                        {categoryFindings.length > 10 && (
                                            <p className="text-gray-400 text-sm text-center py-2">
                                                ... and {categoryFindings.length - 10} more issues
                                            </p>
                                        )}
                                    </div>
                                </div>
                            ))}
                        </div>
                    )}

                    {/* Recommendations Tab */}
                    {activeTab === 'recommendations' && (
                        <div className="space-y-6">
                            <div className="bg-gray-800 rounded p-6">
                                <h3 className="text-lg font-semibold text-white mb-4">Key Recommendations</h3>
                                <div className="space-y-4">
                                    <div className="bg-gray-700 rounded p-4">
                                        <h4 className="font-medium text-white mb-2">Improve Tagging Strategy</h4>
                                        <p className="text-gray-300 text-sm mb-2">
                                            Only {Math.round((1 / 11) * 100)}% of your resources have proper tags.
                                            Implement a comprehensive tagging strategy.
                                        </p>
                                        <p className="text-gray-400 text-sm">
                                            <strong>Priority:</strong> High • <strong>Effort:</strong> Medium
                                        </p>
                                    </div>
                                    <div className="bg-gray-700 rounded p-4">
                                        <h4 className="font-medium text-white mb-2">Standardize Naming Conventions</h4>
                                        <p className="text-gray-300 text-sm mb-2">
                                            {Math.round(assessment.score || 0)}% naming compliance. Consider implementing consistent naming patterns.
                                        </p>
                                        <p className="text-gray-400 text-sm">
                                            <strong>Priority:</strong> Medium • <strong>Effort:</strong> Low
                                        </p>
                                    </div>
                                </div>
                            </div>
                        </div>
                    )}
                </div>

                <div className="border-t border-gray-800 p-6 flex justify-end">
                    <button
                        onClick={onClose}
                        className="px-4 py-2 text-gray-300 hover:text-white transition-colors"
                    >
                        Close
                    </button>
                </div>
            </div>
        </div>
    );
};

export default AssessmentDetailModal;