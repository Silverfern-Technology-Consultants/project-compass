import axios from 'axios';

// Configure base URL - update this to match your API
const API_BASE_URL = process.env.REACT_APP_API_URL || 'https://localhost:7163/api';

// Create axios instance with default config
const apiClient = axios.create({
    baseURL: API_BASE_URL,
    headers: {
        'Content-Type': 'application/json',
    },
    timeout: 30000, // 30 second timeout
});

// Request interceptor for adding auth tokens
apiClient.interceptors.request.use(
    (config) => {
        // Add auth token if available
        const token = localStorage.getItem('authToken');
        if (token) {
            config.headers.Authorization = `Bearer ${token}`;
        }
        return config;
    },
    (error) => {
        return Promise.reject(error);
    }
);

// Response interceptor for handling errors
apiClient.interceptors.response.use(
    (response) => response,
    (error) => {
        if (error.response?.status === 401) {
            // Handle unauthorized - redirect to login
            localStorage.removeItem('authToken');
            window.location.href = '/login';
        }
        return Promise.reject(error);
    }
);

// Helper function to convert assessment type string to number
const getAssessmentTypeNumber = (typeString) => {
    switch (typeString) {
        case 'NamingConvention': return 0;
        case 'Tagging': return 1;
        case 'Full': return 2;
        default: return 2; // Default to Full
    }
};

// Helper function to convert assessment type number to string
const getAssessmentTypeString = (typeNumber) => {
    switch (typeNumber) {
        case 0: return 'NamingConvention';
        case 1: return 'Tagging';
        case 2: return 'Full';
        default: return 'Full';
    }
};

// Helper function to format time ago
const getTimeAgo = (dateString) => {
    const date = new Date(dateString);
    const now = new Date();
    const diffInMinutes = Math.floor((now - date) / (1000 * 60));

    if (diffInMinutes < 1) return 'Just now';
    if (diffInMinutes < 60) return `${diffInMinutes}m ago`;

    const diffInHours = Math.floor(diffInMinutes / 60);
    if (diffInHours < 24) return `${diffInHours}h ago`;

    const diffInDays = Math.floor(diffInHours / 24);
    if (diffInDays < 7) return `${diffInDays}d ago`;

    return date.toLocaleDateString();
};

// Helper function to calculate duration
const calculateDuration = (startDate, endDate) => {
    if (!endDate) return 'In progress...';

    const start = new Date(startDate);
    const end = new Date(endDate);
    const diffInMs = end - start;

    const minutes = Math.floor(diffInMs / (1000 * 60));
    const seconds = Math.floor((diffInMs % (1000 * 60)) / 1000);

    if (minutes > 0) {
        return `${minutes}m ${seconds}s`;
    }
    return `${seconds}s`;
};

// Assessment API calls
export const assessmentApi = {
    // Start new assessment
    startAssessment: async (assessmentData) => {
        console.log('Starting assessment with data:', assessmentData);

        const payload = {
            environmentId: assessmentData.environmentId || '3fa85f64-5717-4562-b3fc-2c963f66afa6',
            subscriptionIds: assessmentData.subscriptions.split('\n').filter(id => id.trim()),
            type: getAssessmentTypeNumber(assessmentData.type), // Convert string to number
            options: {
                analyzeNamingConventions: true,
                analyzeTagging: true,
                includeRecommendations: true
            }
        };

        console.log('Sending payload to API:', payload);

        const response = await apiClient.post('/Assessments', payload);
        return response.data;
    },

    // Get all assessments for a customer (NEW)
    getAllAssessments: async (customerId = '00000000-0000-0000-0000-000000000000') => {
        const response = await apiClient.get(`/Assessments/customer/${customerId}`);

        // DEBUG: Log the raw API response
        console.log('Raw API response:', response.data);
        console.log('Raw API response type:', typeof response.data);
        console.log('Raw API response length:', response.data?.length);

        // If response.data is empty or not an array, return empty array
        if (!response.data || !Array.isArray(response.data)) {
            console.log('API returned empty or invalid data, returning empty array');
            return [];
        }

        // Transform API response to frontend format
        const transformedData = response.data.map((assessment, index) => {
            console.log(`Transforming assessment ${index}:`, assessment);

            const transformed = {
                id: assessment.assessmentId || assessment.id,
                name: assessment.customerName || 'Azure Assessment',
                environment: extractEnvironmentFromType(assessment.assessmentType) || 'Production',
                status: assessment.status || 'Completed',
                score: assessment.overallScore ? Math.round(assessment.overallScore) : null,
                resourceCount: assessment.totalResourcesAnalyzed || 0,
                issuesCount: assessment.issuesFound || 0,
                duration: calculateDuration(assessment.startedDate, assessment.completedDate),
                date: getTimeAgo(assessment.startedDate),
                type: getAssessmentTypeString(assessment.assessmentType),
                assessmentType: assessment.assessmentType,
                startedDate: assessment.startedDate,
                completedDate: assessment.completedDate
            };

            console.log(`Transformed assessment ${index}:`, transformed);
            return transformed;
        });

        console.log('All transformed assessments:', transformedData);
        return transformedData;
    },

    // Delete assessment (UPDATED)
    deleteAssessment: async (assessmentId) => {
        console.log('Calling DELETE API for assessment:', assessmentId);
        const response = await apiClient.delete(`/Assessments/${assessmentId}`);
        console.log('Delete API response:', response.data);
        return response.data;
    },

    // Get assessment status
    getAssessment: async (assessmentId) => {
        const response = await apiClient.get(`/Assessments/${assessmentId}`);
        return response.data;
    },

    // Get assessment results
    getAssessmentResults: async (assessmentId) => {
        const response = await apiClient.get(`/Assessments/${assessmentId}/results`);
        return response.data;
    },

    // Get assessments for environment
    getAssessmentsByEnvironment: async (environmentId, limit = 10) => {
        const response = await apiClient.get(`/Assessments/environment/${environmentId}?limit=${limit}`);
        return response.data;
    },

    // Get assessment findings
    getAssessmentFindings: async (assessmentId, filters = {}) => {
        const params = new URLSearchParams();
        if (filters.category) params.append('category', filters.category);
        if (filters.severity) params.append('severity', filters.severity);
        if (filters.page) params.append('page', filters.page);
        if (filters.pageSize) params.append('pageSize', filters.pageSize);

        const response = await apiClient.get(`/Assessments/${assessmentId}/findings?${params}`);
        return response.data;
    },

    // Get recommendations
    getRecommendations: async (assessmentId) => {
        const response = await apiClient.get(`/Assessments/${assessmentId}/recommendations`);
        return response.data;
    },

    // Test Azure connection
    testConnection: async (subscriptionIds) => {
        const response = await apiClient.post('/Assessments/test-connection', subscriptionIds);
        return response.data;
    }
};

// Helper function to extract environment from assessment type
const extractEnvironmentFromType = (assessmentType) => {
    // You can enhance this based on your naming patterns
    return 'Production'; // Default for now
};

// Test API calls
export const testApi = {
    // Test Azure connection
    testAzureConnection: async (subscriptionIds) => {
        const response = await apiClient.post('/Test/azure-connection', subscriptionIds);
        return response.data;
    },

    // Get sample resources
    getSampleResources: async (subscriptionIds) => {
        const response = await apiClient.post('/Test/azure-resources', subscriptionIds);
        return response.data;
    },

    // Test naming analysis
    testNamingAnalysis: async (subscriptionIds) => {
        const response = await apiClient.post('/Test/test-naming-analysis', subscriptionIds);
        return response.data;
    },

    // Test tagging analysis
    testTaggingAnalysis: async (subscriptionIds) => {
        const response = await apiClient.post('/Test/test-tagging-analysis', subscriptionIds);
        return response.data;
    },

    // Get system status
    getSystemStatus: async () => {
        const response = await apiClient.get('/Test/system-status');
        return response.data;
    }
};

// Client Preferences API calls
export const clientPreferencesApi = {
    // Get client preferences
    getClientPreferences: async (customerId) => {
        const response = await apiClient.get(`/ClientPreferences/customer/${customerId}`);
        return response.data;
    },

    // Save client preferences
    saveClientPreferences: async (preferences) => {
        const response = await apiClient.post('/ClientPreferences', preferences);
        return response.data;
    },

    // Update client preferences
    updateClientPreferences: async (customerId, preferences) => {
        const response = await apiClient.put(`/ClientPreferences/customer/${customerId}`, preferences);
        return response.data;
    },

    // Get Safe Haven demo preferences
    getSafeHavenDemo: async () => {
        const response = await apiClient.get('/ClientPreferences/demo/safe-haven');
        return response.data;
    },

    // Run preference-based assessment
    runPreferenceBasedAssessment: async (customerId, requestData) => {
        const response = await apiClient.post(`/ClientPreferences/customer/${customerId}/assess`, requestData);
        return response.data;
    }
};

// Health check
export const healthApi = {
    checkHealth: async () => {
        const response = await apiClient.get('/health');
        return response.data;
    }
};

// Generic API utility functions
export const apiUtils = {
    // Handle API errors gracefully
    handleApiError: (error) => {
        console.error('API Error:', error);

        if (error.response) {
            // Server responded with error status
            console.error('Error Response:', error.response.data);
            return {
                message: error.response.data?.error || error.response.data?.message || `Server error (${error.response.status})`,
                status: error.response.status,
                details: error.response.data
            };
        } else if (error.request) {
            // Request made but no response
            return {
                message: 'Unable to connect to server. Please check your connection.',
                status: 0,
                details: error.request
            };
        } else {
            // Other error
            return {
                message: error.message || 'An unexpected error occurred',
                status: -1,
                details: error
            };
        }
    },

    // Format API responses consistently
    formatResponse: (response) => {
        return {
            success: true,
            data: response.data,
            message: response.message || 'Success'
        };
    }
};

export default apiClient;