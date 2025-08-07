import React, { useState, useEffect } from 'react';
import { createPortal } from 'react-dom';
import { X, AlertCircle, CheckCircle, Clock, RefreshCw, Eye } from 'lucide-react';
import { assessmentApi } from '../../services/apiService';

const AssessmentProgressModal = ({ isOpen, onClose, assessmentId, onComplete }) => {
    const [assessment, setAssessment] = useState(null);
    const [loading, setLoading] = useState(false);
    const [error, setError] = useState('');
    const [progress, setProgress] = useState(0);
    const [currentPhase, setCurrentPhase] = useState('Initializing');

    useEffect(() => {
        if (isOpen && assessmentId) {
            startPolling();
        }
    }, [isOpen, assessmentId]);

    const startPolling = () => {
        const poll = async () => {
            try {
                setLoading(true);
                const response = await assessmentApi.getAssessmentStatus(assessmentId);
                setAssessment(response);
                setError('');

                // Update progress based on status
                if (response.status === 'Pending') {
                    setProgress(10);
                    setCurrentPhase('Assessment queued');
                } else if (response.status === 'InProgress') {
                    setProgress(50);
                    setCurrentPhase('Analyzing Azure resources');
                } else if (response.status === 'Completed') {
                    setProgress(100);
                    setCurrentPhase('Analysis complete');
                    
                    // Auto-close and trigger completion callback
                    setTimeout(() => {
                        onComplete && onComplete(response);
                        onClose();
                    }, 2000);
                } else if (response.status === 'Failed') {
                    setProgress(0);
                    setCurrentPhase('Assessment failed');
                    setError('Assessment failed to complete');
                }
            } catch (err) {
                console.error('Failed to get assessment status:', err);
                setError('Failed to get assessment status');
            } finally {
                setLoading(false);
            }
        };

        // Initial poll
        poll();

        // Continue polling every 5 seconds while modal is open
        const interval = setInterval(() => {
            if (assessment?.status === 'Completed' || assessment?.status === 'Failed') {
                clearInterval(interval);
                return;
            }
            poll();
        }, 5000);

        return () => clearInterval(interval);
    };

    const handleViewDetails = () => {
        if (assessment) {
            onComplete && onComplete(assessment);
            onClose();
        }
    };

    const getStatusIcon = () => {
        if (assessment?.status === 'Completed') {
            return <CheckCircle className="text-green-400" size={24} />;
        } else if (assessment?.status === 'Failed') {
            return <AlertCircle className="text-red-400" size={24} />;
        } else {
            return <RefreshCw className="text-yellow-400 animate-spin" size={24} />;
        }
    };

    const getStatusColor = () => {
        if (assessment?.status === 'Completed') return 'text-green-400';
        if (assessment?.status === 'Failed') return 'text-red-400';
        return 'text-yellow-400';
    };

    const formatDuration = () => {
        if (!assessment?.startedDate) return 'N/A';
        
        const start = new Date(assessment.startedDate);
        const now = assessment.completedDate ? new Date(assessment.completedDate) : new Date();
        const duration = Math.floor((now - start) / 1000);
        
        if (duration < 60) return `${duration}s`;
        if (duration < 3600) return `${Math.floor(duration / 60)}m ${duration % 60}s`;
        return `${Math.floor(duration / 3600)}h ${Math.floor((duration % 3600) / 60)}m`;
    };

    if (!isOpen) return null;

    return createPortal(
        <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center p-4 z-50">
            <div className="bg-gray-800 rounded-lg shadow-xl w-full max-w-md">
                {/* Header */}
                <div className="flex items-center justify-between p-6 border-b border-gray-700">
                    <h2 className="text-xl font-semibold text-white">Assessment Progress</h2>
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
                            <AlertCircle className="text-red-400 mr-2" size={16} />
                            <span className="text-red-400 text-sm">{error}</span>
                        </div>
                    )}

                    {assessment && (
                        <div className="space-y-6">
                            {/* Status */}
                            <div className="text-center">
                                <div className="flex items-center justify-center mb-3">
                                    {getStatusIcon()}
                                </div>
                                <h3 className={`text-lg font-medium ${getStatusColor()}`}>
                                    {currentPhase}
                                </h3>
                                <p className="text-gray-400 text-sm mt-1">
                                    Assessment: {assessment.name || 'Unnamed Assessment'}
                                </p>
                            </div>

                            {/* Progress Bar */}
                            <div className="space-y-2">
                                <div className="flex justify-between text-sm">
                                    <span className="text-gray-400">Progress</span>
                                    <span className="text-white">{progress}%</span>
                                </div>
                                <div className="w-full bg-gray-700 rounded-full h-2">
                                    <div 
                                        className={`h-2 rounded-full transition-all duration-500 ${
                                            assessment.status === 'Completed' ? 'bg-green-500' :
                                            assessment.status === 'Failed' ? 'bg-red-500' :
                                            'bg-yellow-500'
                                        }`}
                                        style={{ width: `${progress}%` }}
                                    />
                                </div>
                            </div>

                            {/* Assessment Details */}
                            <div className="bg-gray-700/50 rounded-lg p-4 space-y-3">
                                <div className="flex justify-between">
                                    <span className="text-gray-400">Type:</span>
                                    <span className="text-white">{assessment.assessmentType}</span>
                                </div>
                                <div className="flex justify-between">
                                    <span className="text-gray-400">Status:</span>
                                    <span className={getStatusColor()}>{assessment.status}</span>
                                </div>
                                <div className="flex justify-between">
                                    <span className="text-gray-400">Duration:</span>
                                    <span className="text-white">{formatDuration()}</span>
                                </div>
                                {assessment.overallScore && (
                                    <div className="flex justify-between">
                                        <span className="text-gray-400">Score:</span>
                                        <span className="text-white">{assessment.overallScore}%</span>
                                    </div>
                                )}
                            </div>

                            {/* Real-time Updates */}
                            {assessment.status !== 'Completed' && assessment.status !== 'Failed' && (
                                <div className="flex items-center justify-center text-sm text-gray-400">
                                    <Clock size={14} className="mr-2" />
                                    Updating every 5 seconds...
                                </div>
                            )}
                        </div>
                    )}

                    {loading && !assessment && (
                        <div className="flex items-center justify-center py-8">
                            <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-yellow-500"></div>
                            <span className="ml-3 text-gray-300">Loading assessment status...</span>
                        </div>
                    )}
                </div>

                {/* Footer */}
                <div className="flex items-center justify-between p-6 border-t border-gray-700">
                    <button
                        onClick={onClose}
                        className="px-4 py-2 text-gray-300 hover:text-white transition-colors"
                    >
                        Close
                    </button>
                    
                    {assessment?.status === 'Completed' && (
                        <button
                            onClick={handleViewDetails}
                            className="flex items-center space-x-2 px-4 py-2 bg-yellow-500 text-black rounded-md hover:bg-yellow-600 transition-colors font-medium"
                        >
                            <Eye size={16} />
                            <span>View Results</span>
                        </button>
                    )}
                </div>
            </div>
        </div>,
        document.body
    );
};

export default AssessmentProgressModal;
