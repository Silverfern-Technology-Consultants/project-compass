import React, { useState, useEffect, useCallback } from 'react';
import { Play, FileText, Calendar, Search, MoreVertical, Eye, Download, Trash2, CheckCircle, XCircle, AlertCircle, Building2 } from 'lucide-react';
import { useAssessments, useAzureTest } from '../../hooks/useApi';
import NewAssessmentModal from '../modals/NewAssessmentModal';
import AssessmentDetailModal from '../modals/AssessmentDetailModal';
import { useClient } from '../../contexts/ClientContext';

// Helper function to calculate stats
const calculateStats = (assessments, selectedClient, isInternalSelected) => {
    // Get assessments for current context
    let contextAssessments;
    if (!selectedClient) {
        contextAssessments = assessments;
    } else if (isInternalSelected()) {
        contextAssessments = assessments.filter(a => !a.clientId || a.clientId === 'internal');
    } else {
        contextAssessments = assessments.filter(a => a.clientId === selectedClient.ClientId);
    }

    const completedAssessments = contextAssessments.filter(a => a.status === 'Completed');
    const avgScore = completedAssessments.length > 0
        ? Math.round(completedAssessments.reduce((sum, a) => sum + (a.score || 0), 0) / completedAssessments.length)
        : 0;

    const totalIssues = contextAssessments.reduce((sum, a) => sum + (a.issuesCount || 0), 0);

    const lastAssessment = contextAssessments.length > 0
        ? contextAssessments.sort((a, b) => new Date(b.rawData?.startedDate || 0) - new Date(a.rawData?.startedDate || 0))[0]
        : null;

    return {
        total: contextAssessments.length,
        completed: completedAssessments.length,
        avgScore,
        totalIssues,
        lastAssessmentDate: lastAssessment?.date || 'Never'
    };
};

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

    // Enhanced display format: "User Name - Assessment ID"
    const formatAssessmentTitle = (assessment) => {
        // Handle both id and assessmentId fields from API
        const assessmentId = assessment.id || assessment.assessmentId;
        const shortId = assessmentId ? assessmentId.substring(0, 8).toUpperCase() : 'UNKNOWN';

        // Try to get the user-entered name, fall back to generated name
        const userEnteredName = assessment.rawData?.name || assessment.userEnteredName || assessment.name || 'Untitled Assessment';
        return `${userEnteredName} - ${shortId}`;
    };

    // Format company and type for subtitle - UPDATED with client context
    const formatAssessmentSubtitle = (assessment) => {
        const parts = [];

        // Add client information if available
        if (assessment.clientName) {
            parts.push(`Client: ${assessment.clientName}`);
        } else if (assessment.clientId === 'internal' || !assessment.clientId) {
            parts.push('Internal MSP');
        }

        // Add environment info
        parts.push(`${assessment.environment} Environment`);

        return parts.join(' • ');
    };

    return (
        <div className="bg-gray-900 border border-gray-800 rounded p-6 hover:border-gray-700 transition-colors">
            <div className="flex items-start justify-between mb-4">
                <div className="flex-1">
                    <h3 className="text-lg font-semibold text-white mb-1">{formatAssessmentTitle(assessment)}</h3>
                    <p className="text-gray-400 text-sm">{formatAssessmentSubtitle(assessment)}</p>
                </div>
                <div className="flex items-center space-x-2">
                    <span className={`text-xs px-2 py-1 rounded ${getStatusColor(assessment.status)}`}>
                        {assessment.status}
                    </span>
                    <div className="relative">
                        <button
                            onClick={() => setShowDropdown(!showDropdown)}
                            className="p-1 rounded hover:bg-gray-800 text-gray-400"
                        >
                            <MoreVertical size={16} />
                        </button>
                        {showDropdown && (
                            <div className="absolute right-0 top-8 w-48 bg-gray-800 border border-gray-700 rounded shadow-lg z-10">
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
                        className="text-sm bg-yellow-600 hover:bg-yellow-700 text-black px-3 py-1 rounded transition-colors"
                    >
                        View Details
                    </button>
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

    const handleTestConnection = useCallback(async () => {
        if (!subscriptionIds || subscriptionIds.length === 0) return;

        try {
            await testConnection(subscriptionIds);
        } catch (error) {
            console.error('Connection test failed:', error);
        }
    }, [subscriptionIds, testConnection]);

    useEffect(() => {
        if (isOpen && subscriptionIds && subscriptionIds.length > 0) {
            handleTestConnection();
        }
    }, [isOpen, subscriptionIds, handleTestConnection]);

    if (!isOpen) return null;

    return (
        <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50">
            <div className="bg-gray-900 border border-gray-800 rounded w-full max-w-md mx-4">
                <div className="p-6 border-b border-gray-800">
                    <h2 className="text-xl font-semibold text-white">Testing Azure Connection</h2>
                </div>

                <div className="p-6">
                    <div className="flex items-center space-x-3 mb-4">
                        <div className="flex-shrink-0">
                            {loading ? (
                                <div className="animate-spin rounded-full h-6 w-6 border-b-2 border-yellow-600"></div>
                            ) : connectionStatus?.success ? (
                                <CheckCircle size={24} className="text-green-400" />
                            ) : (
                                <XCircle size={24} className="text-red-400" />
                            )}
                        </div>
                        <div>
                            <h3 className="text-lg font-medium text-white">
                                {loading ? 'Testing connection...' :
                                    connectionStatus?.success ? 'Connection successful!' : 'Connection failed'}
                            </h3>
                            <p className="text-gray-400">
                                {loading ? 'Verifying access to Azure subscriptions' :
                                    connectionStatus?.message || 'Unable to connect to Azure subscriptions'}
                            </p>
                        </div>
                    </div>

                    {subscriptionIds && subscriptionIds.length > 0 && (
                        <div className="bg-gray-800 rounded p-3 mb-4">
                            <p className="text-sm text-gray-300 mb-2">Testing subscriptions:</p>
                            <ul className="text-sm text-gray-400 space-y-1">
                                {subscriptionIds.map((id, index) => (
                                    <li key={index} className="font-mono">{id}</li>
                                ))}
                            </ul>
                        </div>
                    )}
                </div>

                <div className="p-6 border-t border-gray-800 flex justify-end">
                    <button
                        onClick={onClose}
                        className="px-4 py-2 bg-yellow-600 hover:bg-yellow-700 text-black rounded font-medium transition-colors"
                    >
                        {connectionStatus?.success ? 'Continue' : 'Close'}
                    </button>
                </div>
            </div>
        </div>
    );
};

const AssessmentsPage = () => {
    const { selectedClient, isInternalSelected, getClientDisplayName } = useClient();
    const {
        assessments,
        loading: assessmentsLoading,
        error: assessmentsError,
        startAssessment,
        deleteAssessment,
        refreshAssessments,
        loadAssessments
    } = useAssessments();

    const [searchTerm, setSearchTerm] = useState('');
    const [statusFilter, setStatusFilter] = useState('All');
    const [showNewModal, setShowNewModal] = useState(false);
    const [showDetailModal, setShowDetailModal] = useState(false);
    const [showDeleteModal, setShowDeleteModal] = useState(false);
    const [showConnectionTest, setShowConnectionTest] = useState(false);
    const [selectedAssessment, setSelectedAssessment] = useState(null);
    const [assessmentToDelete, setAssessmentToDelete] = useState(null);
    const [testSubscriptions, setTestSubscriptions] = useState([]);

    // Load assessments on mount - run only once
    useEffect(() => {
        if (loadAssessments) {
            loadAssessments();
        }
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, []); // Empty dependency array - run only once on mount

    // Close dropdown when clicking outside
    useEffect(() => {
        const handleClickOutside = (event) => {
            if (!event.target.closest('.relative')) {
                // Close any open dropdowns
            }
        };

        document.addEventListener('click', handleClickOutside);
        return () => document.removeEventListener('click', handleClickOutside);
    }, []);

    // Listen for dashboard events to auto-open modals
    useEffect(() => {
        const handleOpenNewAssessmentModal = () => {
            setShowNewModal(true);
        };

        const handleOpenAssessmentDetailModal = (event) => {
            const assessment = event.detail;
            setSelectedAssessment(assessment);
            setShowDetailModal(true);
        };

        // Check for stored assessment to view from dashboard
        const storedAssessment = sessionStorage.getItem('viewAssessment');
        if (storedAssessment) {
            try {
                const assessment = JSON.parse(storedAssessment);
                setSelectedAssessment(assessment);
                setShowDetailModal(true);
                sessionStorage.removeItem('viewAssessment');
            } catch (error) {
                console.error('Failed to parse stored assessment:', error);
            }
        }

        window.addEventListener('openNewAssessmentModal', handleOpenNewAssessmentModal);
        window.addEventListener('openAssessmentDetailModal', handleOpenAssessmentDetailModal);

        return () => {
            window.removeEventListener('openNewAssessmentModal', handleOpenNewAssessmentModal);
            window.removeEventListener('openAssessmentDetailModal', handleOpenAssessmentDetailModal);
        };
    }, []);

    // NEW: Client-aware filtering function
    const getFilteredAssessments = () => {
        if (!selectedClient) {
            // No client selected - show all assessments
            return assessments.filter(assessment => {
                const name = assessment.name || '';
                const environment = assessment.environment || '';
                const status = assessment.status || '';

                const matchesSearch = name.toLowerCase().includes(searchTerm.toLowerCase()) ||
                    environment.toLowerCase().includes(searchTerm.toLowerCase());
                const matchesStatus = statusFilter === 'All' || status === statusFilter;
                return matchesSearch && matchesStatus;
            });
        }

        if (isInternalSelected()) {
            // Internal selected - show assessments with no client (internal assessments)
            return assessments.filter(assessment => {
                const isInternalAssessment = !assessment.clientId || assessment.clientId === 'internal';

                if (!isInternalAssessment) return false;

                const name = assessment.name || '';
                const environment = assessment.environment || '';
                const status = assessment.status || '';

                const matchesSearch = name.toLowerCase().includes(searchTerm.toLowerCase()) ||
                    environment.toLowerCase().includes(searchTerm.toLowerCase());
                const matchesStatus = statusFilter === 'All' || status === statusFilter;
                return matchesSearch && matchesStatus;
            });
        }

        // Specific client selected - show only that client's assessments
        return assessments.filter(assessment => {
            const isClientAssessment = assessment.clientId === selectedClient.ClientId;

            if (!isClientAssessment) return false;

            const name = assessment.name || '';
            const environment = assessment.environment || '';
            const status = assessment.status || '';

            const matchesSearch = name.toLowerCase().includes(searchTerm.toLowerCase()) ||
                environment.toLowerCase().includes(searchTerm.toLowerCase());
            const matchesStatus = statusFilter === 'All' || status === statusFilter;
            return matchesSearch && matchesStatus;
        });
    };

    // Use the new filtering function
    const filteredAssessments = getFilteredAssessments();

    // Calculate stats for current context
    const stats = calculateStats(assessments, selectedClient, isInternalSelected);

    // Helper function to convert assessment type string to number (matching backend enum)
    const getAssessmentTypeNumber = (typeString) => {
        const types = {
            'NamingConvention': 0,
            'Tagging': 1,
            'Full': 2
        };
        return types[typeString] ?? 2; // Default to Full
    };

    const handleStartAssessment = async (formData) => {
        try {
            // Build the client-scoped assessment request matching backend model
            const assessmentRequest = {
                environmentId: formData.environmentId, // Environment ID from the selected environment
                name: formData.name,
                type: formData.type, // Already converted to number in NewAssessmentModal
                options: formData.options || {
                    includeRecommendations: true
                }
            };

            // Use the updated assessment API call
            const result = await startAssessment(assessmentRequest);

            // Close modals
            setShowNewModal(false);

        } catch (error) {
            console.error('Failed to start assessment:', error);

            // Extract meaningful error message
            const errorMessage = error.response?.data?.message ||
                error.response?.data?.error ||
                error.message ||
                'Unknown error occurred';

            alert(`Failed to start assessment: ${errorMessage}`);
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
            // The API returns 'AssessmentId' in the JSON response
            const assessmentId = assessment.AssessmentId || assessment.assessmentId || assessment.id;

            if (!assessmentId) {
                throw new Error('Assessment ID not found');
            }

            await deleteAssessment(assessmentId);
            await refreshAssessments();

        } catch (error) {
            console.error('Failed to delete assessment:', error);
            alert(`Failed to delete assessment: ${error.message}`);
            throw error;
        }
    };

    if (assessmentsLoading && (!assessments || assessments.length === 0)) {
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
        <div className="space-y-6 pt-6">
            {/* Header with Stats Cards - UPDATED */}
            <div className="space-y-4">
                {/* Title and New Assessment Button */}
                <div className="flex items-center justify-between">
                    <h1 className="text-2xl font-bold text-white">
                        Assessments {selectedClient ? `- ${getClientDisplayName()}` : ''}
                    </h1>
                    <button
                        onClick={() => setShowNewModal(true)}
                        className="flex items-center space-x-2 bg-yellow-600 hover:bg-yellow-700 text-black px-4 py-2 rounded font-medium transition-colors"
                    >
                        <Play size={16} />
                        <span>New Assessment</span>
                    </button>
                </div>

                {/* Stats Cards Row */}
                <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
                    <div className="bg-gray-900 border border-gray-800 rounded p-4">
                        <div className="flex items-center justify-between">
                            <div>
                                <p className="text-sm text-gray-400">Total Assessments</p>
                                <p className="text-2xl font-bold text-white">{stats.total}</p>
                                <p className="text-xs text-gray-500 mt-1">
                                    {stats.completed} completed
                                </p>
                            </div>
                            <FileText size={24} className="text-blue-400" />
                        </div>
                    </div>

                    <div className="bg-gray-900 border border-gray-800 rounded p-4">
                        <div className="flex items-center justify-between">
                            <div>
                                <p className="text-sm text-gray-400">Average Score</p>
                                <p className={`text-2xl font-bold ${stats.avgScore >= 90 ? 'text-green-400' :
                                        stats.avgScore >= 70 ? 'text-yellow-400' :
                                            stats.avgScore > 0 ? 'text-red-400' : 'text-gray-500'
                                    }`}>
                                    {stats.avgScore > 0 ? `${stats.avgScore}%` : 'N/A'}
                                </p>
                                <p className="text-xs text-gray-500 mt-1">
                                    {stats.completed > 0 ? `${stats.completed} assessments` : 'No data'}
                                </p>
                            </div>
                            <div className={`text-2xl ${stats.avgScore >= 90 ? 'text-green-400' :
                                    stats.avgScore >= 70 ? 'text-yellow-400' :
                                        stats.avgScore > 0 ? 'text-red-400' : 'text-gray-500'
                                }`}>
                                📊
                            </div>
                        </div>
                    </div>

                    <div className="bg-gray-900 border border-gray-800 rounded p-4">
                        <div className="flex items-center justify-between">
                            <div>
                                <p className="text-sm text-gray-400">Active Issues</p>
                                <p className={`text-2xl font-bold ${stats.totalIssues > 50 ? 'text-red-400' :
                                        stats.totalIssues > 20 ? 'text-yellow-400' :
                                            stats.totalIssues > 0 ? 'text-blue-400' : 'text-green-400'
                                    }`}>
                                    {stats.totalIssues}
                                </p>
                                <p className="text-xs text-gray-500 mt-1">
                                    Across all assessments
                                </p>
                            </div>
                            <AlertCircle size={24} className={
                                stats.totalIssues > 50 ? 'text-red-400' :
                                    stats.totalIssues > 20 ? 'text-yellow-400' :
                                        stats.totalIssues > 0 ? 'text-blue-400' : 'text-green-400'
                            } />
                        </div>
                    </div>

                    <div className="bg-gray-900 border border-gray-800 rounded p-4">
                        <div className="flex items-center justify-between">
                            <div>
                                <p className="text-sm text-gray-400">Last Assessment</p>
                                <p className="text-lg font-bold text-white">
                                    {stats.lastAssessmentDate}
                                </p>
                                <p className="text-xs text-gray-500 mt-1">
                                    {stats.total > 0 ? 'Most recent' : 'No assessments yet'}
                                </p>
                            </div>
                            <Calendar size={24} className="text-purple-400" />
                        </div>
                    </div>
                </div>
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
            <div className="flex flex-col sm:flex-row gap-4">
                <div className="relative flex-1">
                    <Search size={16} className="absolute left-3 top-1/2 transform -translate-y-1/2 text-gray-400" />
                    <input
                        type="text"
                        placeholder="Search assessments..."
                        value={searchTerm}
                        onChange={(e) => setSearchTerm(e.target.value)}
                        className="w-full pl-10 pr-4 py-2 bg-gray-900 border border-gray-700 rounded text-white focus:border-yellow-600 focus:outline-none"
                    />
                </div>
                <select
                    value={statusFilter}
                    onChange={(e) => setStatusFilter(e.target.value)}
                    className="px-4 py-2 bg-gray-900 border border-gray-700 rounded text-white focus:border-yellow-600 focus:outline-none"
                >
                    <option value="All">All Status</option>
                    <option value="Completed">Completed</option>
                    <option value="In Progress">In Progress</option>
                    <option value="Failed">Failed</option>
                </select>
            </div>

            {/* Assessment Grid */}
            {filteredAssessments.length > 0 ? (
                <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
                    {filteredAssessments.map((assessment) => (
                        <AssessmentCard
                            key={assessment.id || assessment.assessmentId || Math.random()}
                            assessment={assessment}
                            onView={handleViewAssessment}
                            onDelete={handleDeleteAssessment}
                        />
                    ))}
                </div>
            ) : (
                <div className="bg-gray-950 border border-gray-800 rounded p-12 text-center">
                    <FileText size={48} className="text-gray-600 mx-auto mb-4" />
                    <h3 className="text-lg font-semibold text-white mb-2">
                        {selectedClient ? `No assessments found for ${getClientDisplayName()}` : 'No assessments found'}
                    </h3>
                    <p className="text-gray-400 mb-4">
                        {(assessments?.length || 0) === 0
                            ? selectedClient
                                ? `Start your first assessment for ${getClientDisplayName()} to begin monitoring Azure governance.`
                                : "Start your first assessment to begin monitoring Azure governance."
                            : "Try adjusting your search or filters."
                        }
                    </p>
                    <button
                        onClick={() => setShowNewModal(true)}
                        className="bg-yellow-600 hover:bg-yellow-700 text-black px-4 py-2 rounded font-medium transition-colors"
                    >
                        {selectedClient ? `Start Assessment for ${selectedClient.Name}` : 'Start Your First Assessment'}
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