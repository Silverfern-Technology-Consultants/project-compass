import { useState, useEffect, useCallback } from 'react';
import { assessmentApi, testApi, apiUtils } from '../services/apiService';

// Custom hook for API calls with loading states
export const useApi = (apiCall, dependencies = []) => {
    const [data, setData] = useState(null);
    const [loading, setLoading] = useState(false);
    const [error, setError] = useState(null);

    const execute = useCallback(async (...args) => {
        try {
            setLoading(true);
            setError(null);
            const result = await apiCall(...args);
            setData(result);
            return result;
        } catch (err) {
            const errorInfo = apiUtils.handleApiError(err);
            setError(errorInfo);
            throw errorInfo;
        } finally {
            setLoading(false);
        }
    }, dependencies);

    return { data, loading, error, execute };
};

// Hook for managing assessments
export const useAssessments = () => {
    const [assessments, setAssessments] = useState([]);
    const [loading, setLoading] = useState(false);
    const [error, setError] = useState(null);

    const startAssessment = async (assessmentData) => {
        try {
            setLoading(true);
            setError(null);


            // Use the new client-scoped assessment request format
            // The backend now expects: { environmentId, name, type, options }
            const clientScopedRequest = {
                environmentId: assessmentData.environmentId, // Environment ID (backend resolves subscriptions)
                name: assessmentData.name,
                type: assessmentData.type, // Already a number (0=NamingConvention, 1=Tagging, 2=Full)
                options: assessmentData.options || {
                    includeRecommendations: true
                }
            };


            const result = await assessmentApi.startAssessment(clientScopedRequest);


            // Transform the assessment type back to string for display
            const typeMap = { 0: 'NamingConvention', 1: 'Tagging', 2: 'Full' };
            const displayType = typeMap[assessmentData.type] || 'Full';

            // Add new assessment to list with "In Progress" status
            const newAssessment = {
                id: result.assessmentId,
                assessmentId: result.assessmentId,
                name: assessmentData.name,
                environment: result.environmentName || 'Azure', // Use environment name from response
                status: 'In Progress',
                score: null,
                resourceCount: 0,
                issuesCount: 0,
                duration: '0s',
                date: 'Just now',
                type: displayType,
                // NEW: Include client context from response
                clientId: result.clientId,
                clientName: result.clientName,
                rawData: result
            };

            setAssessments(prev => [newAssessment, ...prev]);

            // Reload assessments after a short delay to get the completed assessment
            setTimeout(() => {
                loadAssessments();
            }, 2000);

            return result;
        } catch (err) {
            console.error('[useAssessments] Error starting assessment:', err);
            const errorInfo = apiUtils.handleApiError(err);
            setError(errorInfo);
            throw errorInfo;
        } finally {
            setLoading(false);
        }
    };

    const deleteAssessment = async (assessmentId) => {
        try {
            setLoading(true);
            setError(null);


            if (!assessmentId) {
                throw new Error(`Invalid assessment ID: ${assessmentId}`);
            }

            // Call API to delete assessment
            await assessmentApi.deleteAssessment(assessmentId);

            // Remove from local state - check all possible ID fields
            setAssessments(prev => {
                const updatedAssessments = prev.filter(assessment => {
                    return assessment.AssessmentId !== assessmentId &&
                        assessment.id !== assessmentId &&
                        assessment.assessmentId !== assessmentId;
                });
                return updatedAssessments;
            });

            return true;
        } catch (err) {
            const errorInfo = apiUtils.handleApiError(err);
            setError(errorInfo);
            console.error('Failed to delete assessment:', err);
            loadAssessments();
            throw errorInfo;
        } finally {
            setLoading(false);
        }
    };

    const getAssessmentStatus = async (assessmentId) => {
        try {
            const result = await assessmentApi.getAssessment(assessmentId);

            // Update assessment in list
            setAssessments(prev => prev.map(assessment =>
                assessment.id === assessmentId
                    ? { ...assessment, ...result }
                    : assessment
            ));

            return result;
        } catch (err) {
            console.error('Failed to get assessment status:', err);
            throw apiUtils.handleApiError(err);
        }
    };

    const loadAssessments = async () => {
        try {
            setLoading(true);
            setError(null);


            // Load real assessments from API
            const apiAssessments = await assessmentApi.getAllAssessments();


            // Transform the API response to match frontend expectations with proper client context
            // Updated transformation in loadAssessments function (lines ~120-145)
            // Replace the existing transformedAssessments mapping with this:

            const transformedAssessments = apiAssessments.map(assessment => {
                console.log('[useApi] Raw backend assessment:', assessment);

                // Calculate duration from StartedDate and CompletedDate
                const calculateDuration = (startedDate, completedDate) => {
                    if (!startedDate) return 'Unknown';

                    const start = new Date(startedDate);

                    if (!completedDate) {
                        return 'In Progress';
                    }

                    const end = new Date(completedDate);
                    const diffMs = end - start;

                    console.log('[useApi] Duration calculation:', {
                        startedDate,
                        completedDate,
                        diffMs,
                        diffSeconds: diffMs / 1000
                    });

                    if (diffMs < 1000) {
                        return `${diffMs}ms`;
                    } else if (diffMs < 60000) {
                        const totalSeconds = Math.round(diffMs / 100) / 10;
                        return `${totalSeconds}s`;
                    } else {
                        const diffMins = Math.floor(diffMs / 60000);
                        const diffSecs = Math.floor((diffMs % 60000) / 1000);
                        return `${diffMins}m ${diffSecs}s`;
                    }
                };

                return {
                    // ID mappings
                    id: assessment.AssessmentId,
                    assessmentId: assessment.AssessmentId,
                    AssessmentId: assessment.AssessmentId,

                    // Basic info
                    name: assessment.Name || 'Untitled Assessment',
                    environment: 'Production',
                    status: assessment.Status || 'Unknown',
                    score: assessment.OverallScore,
                    resourceCount: assessment.TotalResourcesAnalyzed || 0,
                    issuesCount: assessment.IssuesFound || 0,
                    type: assessment.AssessmentType || 'Full',

                    // FIXED: Map date fields properly from backend PascalCase to frontend camelCase
                    startedDate: assessment.StartedDate,
                    completedDate: assessment.CompletedDate,
                    StartedDate: assessment.StartedDate,  // Keep PascalCase for compatibility
                    CompletedDate: assessment.CompletedDate,  // Keep PascalCase for compatibility

                    // FIXED: Calculate duration from actual dates instead of hard-coding
                    duration: calculateDuration(assessment.StartedDate, assessment.CompletedDate),

                    // Display date
                    date: assessment.StartedDate ? new Date(assessment.StartedDate).toLocaleDateString() : 'Recent',

                    // Client context
                    clientId: assessment.ClientId,
                    clientName: assessment.ClientName,

                    // Raw data
                    rawData: assessment
                };
            });

            setAssessments(transformedAssessments);
        } catch (err) {
            console.error('Failed to load assessments:', err);
            const errorInfo = apiUtils.handleApiError(err);

            // If API call fails, fall back to empty list
            if (errorInfo.status === 404) {
                // No assessments found - this is normal for new users
                setAssessments([]);
                setError(null);
            } else if (errorInfo.status === 0) {
                // Connection error - show error but keep existing assessments
                setError({
                    ...errorInfo,
                    message: 'Unable to connect to server. Showing cached data.'
                });
            } else {
                setError(errorInfo);
                // Still show empty list rather than failing completely
                setAssessments([]);
            }
        } finally {
            setLoading(false);
        }
    };

    const refreshAssessments = async () => {
        try {
            await loadAssessments();
        } catch (err) {
            console.error('Failed to refresh assessments:', err);
            // Don't throw error on refresh - just log it
        }
    };

    // Poll for assessment updates
    const startPolling = useCallback((assessmentId, interval = 5000) => {
        const pollInterval = setInterval(async () => {
            try {
                const status = await getAssessmentStatus(assessmentId);

                // Stop polling if assessment is completed or failed
                if (status.status === 'Completed' || status.status === 'Failed') {
                    clearInterval(pollInterval);
                }
            } catch (err) {
                console.error('Polling error for assessment', assessmentId, err);
                // Continue polling on error
            }
        }, interval);

        return () => clearInterval(pollInterval);
    }, []);

    return {
        assessments,
        loading,
        error,
        startAssessment,
        deleteAssessment,
        getAssessmentStatus,
        loadAssessments,
        refreshAssessments,
        startPolling
    };
};

// Hook for testing Azure connectivity
export const useAzureTest = () => {
    const [connectionStatus, setConnectionStatus] = useState(null);
    const [testResults, setTestResults] = useState(null);
    const [loading, setLoading] = useState(false);
    const [error, setError] = useState(null);

    const testConnection = async (subscriptionIds) => {
        try {
            setLoading(true);
            setError(null);
            const result = await testApi.testAzureConnection(subscriptionIds);
            setConnectionStatus(result);
            return result;
        } catch (err) {
            const errorInfo = apiUtils.handleApiError(err);
            setError(errorInfo);
            setConnectionStatus({
                success: false,
                message: errorInfo.message,
                subscriptionIds: subscriptionIds,
                testedAt: new Date().toISOString()
            });
            throw errorInfo;
        } finally {
            setLoading(false);
        }
    };

    const runTestAnalysis = async (subscriptionIds) => {
        try {
            setLoading(true);
            setError(null);
            const [resourcesResult, namingResult, taggingResult] = await Promise.allSettled([
                testApi.getSampleResources(subscriptionIds),
                testApi.testNamingAnalysis(subscriptionIds),
                testApi.testTaggingAnalysis(subscriptionIds)
            ]);

            const results = {
                resources: resourcesResult.status === 'fulfilled' ? resourcesResult.value : null,
                naming: namingResult.status === 'fulfilled' ? namingResult.value : null,
                tagging: taggingResult.status === 'fulfilled' ? taggingResult.value : null,
                errors: [
                    resourcesResult.status === 'rejected' ? resourcesResult.reason : null,
                    namingResult.status === 'rejected' ? namingResult.reason : null,
                    taggingResult.status === 'rejected' ? taggingResult.reason : null
                ].filter(Boolean)
            };

            setTestResults(results);
            return results;
        } catch (err) {
            const errorInfo = apiUtils.handleApiError(err);
            setError(errorInfo);
            throw errorInfo;
        } finally {
            setLoading(false);
        }
    };

    const clearTestResults = () => {
        setConnectionStatus(null);
        setTestResults(null);
        setError(null);
    };

    return {
        connectionStatus,
        testResults,
        loading,
        error,
        testConnection,
        runTestAnalysis,
        clearTestResults
    };
};

// Hook for system health
export const useSystemHealth = () => {
    const [health, setHealth] = useState(null);
    const [loading, setLoading] = useState(false);
    const [lastChecked, setLastChecked] = useState(null);

    const checkHealth = async () => {
        try {
            setLoading(true);
            const status = await testApi.getSystemStatus();
            setHealth(status);
            setLastChecked(new Date());
        } catch (err) {
            console.error('Health check failed:', err);
            setHealth({
                status: 'unhealthy',
                error: err.message,
                timestamp: new Date().toISOString()
            });
            setLastChecked(new Date());
        } finally {
            setLoading(false);
        }
    };

    useEffect(() => {
        // Initial health check
        checkHealth();

        // Check health every 5 minutes
        const interval = setInterval(checkHealth, 5 * 60 * 1000);

        return () => clearInterval(interval);
    }, []);

    return {
        health,
        loading,
        lastChecked,
        checkHealth
    };
};

// Hook for real-time assessment updates
export const useAssessmentUpdates = (assessmentId) => {
    const [assessment, setAssessment] = useState(null);
    const [loading, setLoading] = useState(false);
    const [error, setError] = useState(null);

    useEffect(() => {
        if (!assessmentId) return;

        let pollInterval;

        const startPolling = async () => {
            try {
                setLoading(true);
                const result = await assessmentApi.getAssessment(assessmentId);
                setAssessment(result);

                // Continue polling if assessment is in progress
                if (result.status === 'Pending' || result.status === 'InProgress') {
                    pollInterval = setInterval(async () => {
                        try {
                            const updatedResult = await assessmentApi.getAssessment(assessmentId);
                            setAssessment(updatedResult);

                            // Stop polling if completed or failed
                            if (updatedResult.status === 'Completed' || updatedResult.status === 'Failed') {
                                clearInterval(pollInterval);
                            }
                        } catch (pollError) {
                            console.error('Polling error:', pollError);
                        }
                    }, 3000); // Poll every 3 seconds
                }
            } catch (err) {
                setError(apiUtils.handleApiError(err));
            } finally {
                setLoading(false);
            }
        };

        startPolling();

        return () => {
            if (pollInterval) {
                clearInterval(pollInterval);
            }
        };
    }, [assessmentId]);

    return { assessment, loading, error };
};