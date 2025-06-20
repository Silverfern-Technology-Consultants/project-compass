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

            console.log('Starting assessment with data:', assessmentData);

            const result = await assessmentApi.startAssessment(assessmentData);

            // Add new assessment to list with "In Progress" status
            const newAssessment = {
                id: result.assessmentId,
                name: assessmentData.name,
                environment: assessmentData.environment,
                status: 'In Progress',
                score: null,
                resourceCount: 0,
                issuesCount: 0,
                duration: '0s',
                date: 'Just now',
                type: assessmentData.type
            };

            setAssessments(prev => [newAssessment, ...prev]);

            // Reload assessments after a short delay to get the completed assessment
            setTimeout(() => {
                loadAssessments();
            }, 2000);

            return result;
        } catch (err) {
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

            console.log('Deleting assessment:', assessmentId);

            // Call API to delete assessment
            await assessmentApi.deleteAssessment(assessmentId);

            // Remove from local state immediately for better UX
            setAssessments(prev => prev.filter(assessment => assessment.id !== assessmentId));

            console.log('Assessment deleted successfully from API and local state');

            return true;
        } catch (err) {
            const errorInfo = apiUtils.handleApiError(err);
            setError(errorInfo);
            console.error('Failed to delete assessment:', err);

            // Reload assessments to ensure state consistency
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

            console.log('Loading assessments from API...');

            // Load real assessments from API
            const apiAssessments = await assessmentApi.getAllAssessments();

            console.log('Loaded assessments:', apiAssessments);

            setAssessments(apiAssessments);
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
            console.log('Refreshing assessments...');
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
                    console.log(`Polling stopped for assessment ${assessmentId}: ${status.status}`);
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

            console.log('Testing Azure connection for subscriptions:', subscriptionIds);

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

            console.log('Running test analysis for subscriptions:', subscriptionIds);

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