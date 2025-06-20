import React, { useState, useEffect } from 'react';
import { Play, FileText, Calendar, Filter, Search, MoreVertical, Eye, Download, Trash2, CheckCircle, XCircle, AlertCircle } from 'lucide-react';
import { useAssessments, useAzureTest } from '../../hooks/useApi';
import NewAssessmentModal from '../modals/NewAssessmentModal';
import AssessmentDetailModal from '../modals/AssessmentDetailModal';

const AssessmentCard = ({ assessment, onView, onDelete }) => {
    const [showDropdown, setShowDropdown] = useState(false);

    const getStatusColor = (status) => {
        switch (status) {
            case 'Completed': return 'bg-green-600 text-white';
            case 'In Progress': return 'bg-yellow-600 text-black';
            case 'Failed': return 'bg-red-600 text-white';
            default: return 'bg-gray-600 text-white';
        }
    };

    const getScoreColor = (score) => {
        if (score >= 90) return 'text-green-400';
        if (score >= 70) return 'text-yellow-400';
        return 'text-red-400';
    };

    return (
        <div className="bg-gray-900 border border-gray-800 rounded p-6 hover:border-gray-700 transition-colors">
            <div className="flex items-start justify-between mb-4">
                <div className="flex-1">
                    <h3 className="text-lg font-semibold text-white mb-1">{assessment.name}</h3>
                    <p className="text-gray-400 text-sm">{assessment.environment}</p>
                </div>
                <div className="flex items-center space-x-2">
                    <div className={`px-3 py-1 rounded text-sm font-medium ${getStatusColor(assessment.status)}`}>
                        {assessment.status}
                    </div>
                    <div className="relative">
                        <button
                            onClick={() => setShowDropdown(!showDropdown)}
                            className="p-1 rounded hover:bg-gray-800 text-gray-400"
                        >
                            <MoreVertical size={16} />
                        </button>

                        {showDropdown && (
                            <div className="absolute right-0 mt-2 w-48 bg-gray-800 border border-gray-700 rounded shadow-lg z-10">
                                <button
                                    onClick={() => {
                                        onView(assessment);
                                        setShowDropdown(false);
                                    }}
                                    className="w-full text-left px-4 py-2 text-sm text-gray-300 hover:bg-gray-700 flex items-center space-x-2"
                                >
                                    <Eye size={14} />
                                    <span>View Details</span>
                                </button>

                                {assessment.status === 'Completed' && (
                                    <button
                                        onClick={() => setShowDropdown(false)}
                                        className="w-full text-left px-4 py-2 text-sm text-gray-300 hover:bg-gray-700 flex items-center space-x-2"
                                    >
                                        <Download size={14} />
                                        <span>Export Report</span>
                                    </button>
                                )}

                                <button
                                    onClick={() => {
                                        onDelete(assessment);
                                        setShowDropdown(false);
                                    }}
                                    className="w-full text-left px-4 py-2 text-sm text-red-400 hover:bg-gray-700 flex items-center space-x-2"
                                >
                                    <Trash2 size={14} />
                                    <span>Delete</span>
                                </button>
                            </div>
                        )}
                    </div>
                </div>
            </div>

            <div className="grid grid-cols-2 md:grid-cols-4 gap-4 mb-4">
                <div>
                    <p className="text-xs text-gray-400 uppercase tracking-wide">Overall Score</p>
                    <p className={`text-xl font-bold ${assessment.score ? getScoreColor(assessment.score) : 'text-gray-500'}`}>
                        {assessment.score ? `${assessment.score}%` : 'N/A'}
                    </p>
                </div>
                <div>
                    <p className="text-xs text-gray-400 uppercase tracking-wide">Resources</p>
                    <p className="text-lg font-semibold text-white">{assessment.resourceCount}</p>
                </div>
                <div>
                    <p className="text-xs text-gray-400 uppercase tracking-wide">Issues Found</p>
                    <p className="text-lg font-semibold text-white">{assessment.issuesCount}</p>
                </div>
                <div>
                    <p className="text-xs text-gray-400 uppercase tracking-wide">Duration</p>
                    <p className="text-lg font-semibold text-white">{assessment.duration}</p>
                </div>
            </div>

            <div className="flex items-center justify-between">
                <div className="flex items-center space-x-2 text-sm text-gray-400">
                    <Calendar size={14} />
                    <span>{assessment.date}</span>
                </div>
                <div className="flex items-center space-x-2">
                    <button
                        onClick={() => onView(assessment)}
                        className="flex items-center space-x-1 px-3 py-1 bg-yellow-600 hover:bg-yellow-700 text-black rounded text-sm font-medium transition-colors"
                    >
                        <Eye size={14} />
                        <span>View</span>
                    </button>
                    {assessment.status === 'Completed' && (
                        <button className="flex items-center space-x-1 px-3 py-1 bg-gray-800 hover:bg-gray-700 text-white rounded text-sm transition-colors">
                            <Download size={14} />
                            <span>Export</span>
                        </button>
                    )}
                </div>
            </div>
        </div>
    );
};

const DeleteConfirmationModal = ({ isOpen, onClose, onConfirm, assessment }) => {
    const [isDeleting, setIsDeleting] = useState(false);

    const handleDelete = async () => {
        setIsDeleting(true);
        try {
            await onConfirm(assessment);
            onClose();
        } catch (error) {
            console.error('Delete failed:', error);
        } finally {
            setIsDeleting(false);
        }
    };

    if (!isOpen) return null;

    return (
        <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50">
            <div className="bg-gray-900 border border-gray-800 rounded w-full max-w-md mx-4">
                <div className="p-6 border-b border-gray-800">
                    <h2 className="text-xl font-semibold text-white">Delete Assessment</h2>
                </div>

                <div className="p-6">
                    <div className="flex items-center space-x-3 mb-4">
                        <div className="flex-shrink-0">
                            <AlertCircle size={24} className="text-red-400" />
                        </div>
                        <div>
                            <h3 className="text-lg font-medium text-white">Are you sure?</h3>
                            <p className="text-gray-400">
                                This will permanently delete the assessment "{assessment?.name}" and all associated data.
                            </p>
                        </div>
                    </div>

                    <div className="bg-red-900 bg-opacity-20 border border-red-700 rounded p-3 mb-4">
                        <p className="text-red-200 text-sm">
                            <strong>Warning:</strong> This action cannot be undone. All findings, recommendations, and reports will be permanently deleted.
                        </p>
                    </div>
                </div>

                <div className="p-6 border-t border-gray-800 flex justify-end space-x-3">
                    <button
                        onClick={onClose}
                        disabled={isDeleting}
                        className="px-4 py-2 text-gray-300 hover:text-white transition-colors disabled:opacity-50"
                    >
                        Cancel
                    </button>
                    <button
                        onClick={handleDelete}
                        disabled={isDeleting}
                        className="flex items-center space-x-2 px-4 py-2 bg-red-600 hover:bg-red-700 text-white rounded font-medium transition-colors disabled:opacity-50"
                    >
                        {isDeleting ? (
                            <>
                                <div className="animate-spin rounded-full h-4 w-4 border-b-2 border-white"></div>
                                <span>Deleting...</span>
                            </>
                        ) : (
                            <>
                                <Trash2 size={16} />
                                <span>Delete Assessment</span>
                            </>
                        )}
                    </button>
                </div>
            </div>
        </div>
    );
};

const ConnectionTestModal = ({ isOpen, onClose, subscriptionIds }) => {
    const { testConnection, connectionStatus, loading } = useAzureTest();
    const [testStarted, setTestStarted] = useState(false);

    const handleTestConnection = async () => {
        if (!subscriptionIds || subscriptionIds.length === 0) return;

        setTestStarted(true);
        try {
            await testConnection(subscriptionIds);
        } catch (error) {
            console.error('Connection test failed:', error);
        }
    };

    if (!isOpen) return null;

    const getStatusIcon = () => {
        if (loading) return <div className="animate-spin rounded-full h-6 w-6 border-b-2 border-yellow-600"></div>;
        if (connectionStatus?.success) return <CheckCircle size={24} className="text-green-400" />;
        if (connectionStatus && !connectionStatus.success) return <XCircle size={24} className="text-red-400" />;
        return <AlertCircle size={24} className="text-gray-400" />;
    };

    return (
        <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50">
            <div className="bg-gray-900 border border-gray-800 rounded w-full max-w-md mx-4">
                <div className="p-6 border-b border-gray-800">
                    <h2 className="text-xl font-semibold text-white">Test Azure Connection</h2>
                </div>

                <div className="p-6 text-center">
                    <div className="mb-4">
                        {getStatusIcon()}
                    </div>

                    {!testStarted && (
                        <div>
                            <p className="text-gray-400 mb-4">
                                Test connection to {subscriptionIds?.length || 0} Azure subscription(s)
                            </p>
                            <button
                                onClick={handleTestConnection}
                                className="bg-yellow-600 hover:bg-yellow-700 text-black px-4 py-2 rounded font-medium transition-colors"
                            >
                                Start Connection Test
                            </button>
                        </div>
                    )}

                    {loading && (
                        <div>
                            <h3 className="text-lg font-semibold text-white mb-2">Testing Connection...</h3>
                            <p className="text-gray-400">Verifying access to Azure subscriptions</p>
                        </div>
                    )}

                    {connectionStatus && !loading && (
                        <div>
                            <h3 className={`text-lg font-semibold mb-2 ${connectionStatus.success ? 'text-green-400' : 'text-red-400'}`}>
                                {connectionStatus.success ? 'Connection Successful!' : 'Connection Failed'}
                            </h3>
                            <p className="text-gray-400 mb-4">{connectionStatus.message}</p>

                            {connectionStatus.success && (
                                <div className="bg-gray-800 rounded p-3 mb-4">
                                    <p className="text-sm text-gray-300">
                                        Successfully connected to Azure Resource Graph API
                                    </p>
                                </div>
                            )}
                        </div>
                    )}
                </div>

                <div className="p-6 border-t border-gray-800 flex justify-end">
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

const AssessmentsPage = () => {
    const [searchTerm, setSearchTerm] = useState('');
    const [statusFilter, setStatusFilter] = useState('All');
    const [showNewModal, setShowNewModal] = useState(false);
    const [showConnectionTest, setShowConnectionTest] = useState(false);
    const [showDetailModal, setShowDetailModal] = useState(false);
    const [showDeleteModal, setShowDeleteModal] = useState(false);
    const [selectedAssessment, setSelectedAssessment] = useState(null);
    const [assessmentToDelete, setAssessmentToDelete] = useState(null);
    const [testSubscriptions, setTestSubscriptions] = useState([]);

    const {
        assessments,
        loading: assessmentsLoading,
        error: assessmentsError,
        startAssessment,
        deleteAssessment,
        loadAssessments,
        refreshAssessments
    } = useAssessments();

    // Load assessments on component mount
    useEffect(() => {
        loadAssessments();
    }, []);

    // Close dropdown when clicking outside
    useEffect(() => {
        const handleClickOutside = () => {
            // This will be handled by individual dropdown components
        };

        document.addEventListener('click', handleClickOutside);
        return () => document.removeEventListener('click', handleClickOutside);
    }, []);

    const filteredAssessments = assessments.filter(assessment => {
        const matchesSearch = assessment.name.toLowerCase().includes(searchTerm.toLowerCase()) ||
            assessment.environment.toLowerCase().includes(searchTerm.toLowerCase());
        const matchesStatus = statusFilter === 'All' || assessment.status === statusFilter;
        return matchesSearch && matchesStatus;
    });

    const handleStartAssessment = async (formData) => {
        try {
            console.log('Starting assessment with form data:', formData);

            // Parse subscription IDs
            const subscriptionIds = formData.subscriptions
                .split('\n')
                .map(id => id.trim())
                .filter(id => id.length > 0);

            if (subscriptionIds.length === 0) {
                alert('Please provide at least one subscription ID');
                return;
            }

            // Test connection first
            setTestSubscriptions(subscriptionIds);
            setShowConnectionTest(true);

            // Start the assessment
            const result = await startAssessment({
                ...formData,
                environmentId: '00000000-0000-0000-0000-000000000000', // Mock environment ID
                subscriptions: formData.subscriptions
            });

            console.log('Assessment started:', result);

            // Close modals
            setShowConnectionTest(false);
            setShowNewModal(false);

        } catch (error) {
            console.error('Failed to start assessment:', error);
            alert(`Failed to start assessment: ${error.message}`);
        }
    };

    const handleViewAssessment = (assessment) => {
        setSelectedAssessment(assessment);
        setShowDetailModal(true);
    };

    const handleDeleteAssessment = (assessment) => {
        setAssessmentToDelete(assessment);
        setShowDeleteModal(true);
    };

    const confirmDeleteAssessment = async (assessment) => {
        try {
            console.log('Deleting assessment:', assessment.id);
            await deleteAssessment(assessment.id);

            // Show success message
            console.log('Assessment deleted successfully');

            // Optionally refresh the list (though the hook should handle this automatically)
            await refreshAssessments();

        } catch (error) {
            console.error('Failed to delete assessment:', error);
            alert(`Failed to delete assessment: ${error.message}`);
            throw error; // Re-throw to let the modal handle the error state
        }
    };

    if (assessmentsLoading && assessments.length === 0) {
        return (
            <div className="space-y-6">
                <div className="flex items-center justify-center p-12">
                    <div className="text-center">
                        <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-yellow-600 mx-auto mb-4"></div>
                        <p className="text-gray-400">Loading assessments...</p>
                    </div>
                </div>
            </div>
        );
    }

    return (
        <div className="space-y-6">
            {/* Header */}
            <div className="flex items-center justify-between">
                <div>
                    <h1 className="text-2xl font-bold text-white">Assessments</h1>
                    <p className="text-gray-400">Manage and monitor your Azure governance assessments</p>
                </div>
                <button
                    onClick={() => setShowNewModal(true)}
                    className="flex items-center space-x-2 bg-yellow-600 hover:bg-yellow-700 text-black px-4 py-2 rounded font-medium transition-colors"
                >
                    <Play size={16} />
                    <span>New Assessment</span>
                </button>
            </div>

            {/* Error Display */}
            {assessmentsError && (
                <div className="bg-red-900 border border-red-700 rounded p-4">
                    <div className="flex items-center space-x-2">
                        <XCircle size={20} className="text-red-400" />
                        <div>
                            <h3 className="font-medium text-white">Error</h3>
                            <p className="text-red-200">{assessmentsError.message}</p>
                        </div>
                    </div>
                </div>
            )}

            {/* Filters */}
            <div className="bg-gray-900 border border-gray-800 rounded p-4">
                <div className="flex flex-col sm:flex-row gap-4">
                    <div className="flex-1 relative">
                        <Search className="absolute left-3 top-1/2 transform -translate-y-1/2 text-gray-400" size={16} />
                        <input
                            type="text"
                            placeholder="Search assessments..."
                            value={searchTerm}
                            onChange={(e) => setSearchTerm(e.target.value)}
                            className="w-full bg-gray-800 border border-gray-700 rounded pl-10 pr-4 py-2 text-white focus:outline-none focus:border-yellow-600"
                        />
                    </div>
                    <div className="flex items-center space-x-3">
                        <Filter size={16} className="text-gray-400" />
                        <select
                            value={statusFilter}
                            onChange={(e) => setStatusFilter(e.target.value)}
                            className="bg-gray-800 border border-gray-700 rounded px-3 py-2 text-white focus:outline-none focus:border-yellow-600"
                        >
                            <option value="All">All Status</option>
                            <option value="Completed">Completed</option>
                            <option value="In Progress">In Progress</option>
                            <option value="Failed">Failed</option>
                            <option value="Pending">Pending</option>
                        </select>
                    </div>
                </div>
            </div>

            {/* Assessments Grid */}
            <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
                {filteredAssessments.map(assessment => (
                    <AssessmentCard
                        key={assessment.id}
                        assessment={assessment}
                        onView={handleViewAssessment}
                        onDelete={handleDeleteAssessment}
                    />
                ))}
            </div>

            {filteredAssessments.length === 0 && !assessmentsLoading && (
                <div className="bg-gray-900 border border-gray-800 rounded p-12 text-center">
                    <FileText size={48} className="text-gray-600 mx-auto mb-4" />
                    <h3 className="text-lg font-semibold text-white mb-2">No assessments found</h3>
                    <p className="text-gray-400 mb-4">Try adjusting your search or filters, or start a new assessment.</p>
                    <button
                        onClick={() => setShowNewModal(true)}
                        className="bg-yellow-600 hover:bg-yellow-700 text-black px-4 py-2 rounded font-medium transition-colors"
                    >
                        Start Your First Assessment
                    </button>
                </div>
            )}

            {/* Loading indicator for new assessments */}
            {assessmentsLoading && assessments.length > 0 && (
                <div className="text-center py-4">
                    <div className="animate-spin rounded-full h-6 w-6 border-b-2 border-yellow-600 mx-auto mb-2"></div>
                    <p className="text-gray-400">Loading assessments...</p>
                </div>
            )}

            {/* New Assessment Modal */}
            <NewAssessmentModal
                isOpen={showNewModal}
                onClose={() => setShowNewModal(false)}
                onStart={handleStartAssessment}
            />

            {/* Connection Test Modal */}
            <ConnectionTestModal
                isOpen={showConnectionTest}
                onClose={() => setShowConnectionTest(false)}
                subscriptionIds={testSubscriptions}
            />

            {/* Assessment Detail Modal */}
            <AssessmentDetailModal
                isOpen={showDetailModal}
                onClose={() => setShowDetailModal(false)}
                assessment={selectedAssessment}
            />

            {/* Delete Confirmation Modal */}
            <DeleteConfirmationModal
                isOpen={showDeleteModal}
                onClose={() => {
                    setShowDeleteModal(false);
                    setAssessmentToDelete(null);
                }}
                onConfirm={confirmDeleteAssessment}
                assessment={assessmentToDelete}
            />
        </div>
    );
};

export default AssessmentsPage;