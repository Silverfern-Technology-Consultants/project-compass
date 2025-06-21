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
        // Updated to use the same token key as AuthContext
        const token = localStorage.getItem('compass_token') || localStorage.getItem('authToken');
        if (token) {
            config.headers.Authorization = `Bearer ${token}`;
        }

        // Add logging for debugging
        console.log(`[API] ${config.method?.toUpperCase()} ${config.url}`, {
            params: config.params,
            data: config.data,
            headers: config.headers
        });

        return config;
    },
    (error) => {
        console.error('[API] Request error:', error);
        return Promise.reject(error);
    }
);

// Response interceptor for handling errors
apiClient.interceptors.response.use(
    (response) => {
        console.log(`[API] Response ${response.status}:`, response.data);
        return response;
    },
    (error) => {
        console.error('[API] Response error:', error);

        // Enhanced error logging to see response data
        if (error.response) {
            console.error('[API] Error status:', error.response.status);
            console.error('[API] Error data:', error.response.data);
            console.error('[API] Error headers:', error.response.headers);
        } else if (error.request) {
            console.error('[API] No response received:', error.request);
        } else {
            console.error('[API] Request setup error:', error.message);
        }

        if (error.response?.status === 401) {
            // Handle unauthorized - clear both token keys and redirect to login
            localStorage.removeItem('compass_token');
            localStorage.removeItem('authToken');
            window.location.href = '/login';
        }
        return Promise.reject(error);
    }
);

// Authentication API class for cleaner organization
export class AuthApi {
    // Set auth token manually (used by AuthContext)
    static setAuthToken(token) {
        if (token) {
            apiClient.defaults.headers.common['Authorization'] = `Bearer ${token}`;
            localStorage.setItem('compass_token', token);
        } else {
            delete apiClient.defaults.headers.common['Authorization'];
            localStorage.removeItem('compass_token');
            localStorage.removeItem('authToken');
        }
    }

    // Login with enhanced error handling
    static async login(email, password) {
        try {
            console.log('[AuthApi] Attempting login with:', { email, passwordLength: password.length });
            const response = await apiClient.post('/auth/login', { email, password });
            console.log('[AuthApi] Login response:', response.data);
            return response.data;
        } catch (error) {
            console.error('[AuthApi] Login error details:', {
                status: error.response?.status,
                data: error.response?.data,
                message: error.message
            });
            throw error;
        }
    }

    // Register
    static async register(userData) {
        const response = await apiClient.post('/auth/register', userData);
        return response.data;
    }

    // Verify email
    static async verifyEmail(token) {
        const response = await apiClient.post('/auth/verify-email', { token });
        return response.data;
    }

    // Resend verification
    static async resendVerification(email) {
        const response = await apiClient.post('/auth/resend-verification', { email });
        return response.data;
    }

    // Get current user
    static async getCurrentUser() {
        const response = await apiClient.get('/auth/me');
        return response.data;
    }

    // Check email availability
    static async checkEmailAvailability(email) {
        const response = await apiClient.post('/auth/check-email', { email });
        return response.data;
    }
}

// Helper function to convert assessment type string to number
const getAssessmentTypeNumber = (typeString) => {
    switch (typeString) {
        case 'NamingConvention': return 0;
        case 'Tagging': return 1;
        case 'Full': return 2;
        default: return 2;
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

// Remove duplicate helper functions from bottom of file

// Assessments API (SIMPLIFIED AND FIXED)
export const assessmentApi = {
    // Get all assessments for a customer
    getAllAssessments: async (customerId = '9bc034b0-852f-4618-9434-c040d13de712') => {
        try {
            console.log('[assessmentApi] getAllAssessments called with customerId:', customerId);
            const response = await apiClient.get(`/assessments/customer/${customerId}`);
            console.log('[assessmentApi] Raw API response:', response.data);

            // Return empty array if no data
            if (!response.data || !Array.isArray(response.data)) {
                console.log('[assessmentApi] No assessments found, returning empty array');
                return [];
            }

            // Simple transformation
            return response.data.map(assessment => ({
                id: assessment.assessmentId || assessment.id,
                name: 'Azure Assessment',
                environment: 'Production',
                status: assessment.status || 'Completed',
                score: assessment.overallScore ? Math.round(assessment.overallScore) : null,
                resourceCount: assessment.totalResourcesAnalyzed || 0,
                issuesCount: assessment.issuesFound || 0,
                duration: '2m 15s',
                date: 'Just now',
                type: 'Full'
            }));
        } catch (error) {
            console.error('[assessmentApi] getAllAssessments error:', error);
            return [];
        }
    },

    // Create new assessment
    startAssessment: async (assessmentData) => {
        try {
            console.log('[assessmentApi] startAssessment called with:', assessmentData);

            const payload = {
                environmentId: assessmentData.environmentId || '3fa85f64-5717-4562-b3fc-2c963f66afa6',
                subscriptionIds: Array.isArray(assessmentData.subscriptions)
                    ? assessmentData.subscriptions
                    : assessmentData.subscriptions.split('\n').filter(id => id.trim()),
                type: getAssessmentTypeNumber(assessmentData.type),
                options: {
                    analyzeNamingConventions: true,
                    analyzeTagging: true,
                    includeRecommendations: true
                }
            };

            console.log('[assessmentApi] Sending payload:', payload);
            const response = await apiClient.post('/assessments', payload);
            console.log('[assessmentApi] startAssessment response:', response.data);
            return response.data;
        } catch (error) {
            console.error('[assessmentApi] startAssessment error:', error);
            throw error;
        }
    },

    // Get assessment by ID
    getAssessment: async (assessmentId) => {
        const response = await apiClient.get(`/assessments/${assessmentId}`);
        return response.data;
    },

    // Get assessment findings
    getAssessmentFindings: async (assessmentId) => {
        const response = await apiClient.get(`/assessments/${assessmentId}/findings`);
        return response.data;
    },

    // Delete assessment
    deleteAssessment: async (assessmentId) => {
        const response = await apiClient.delete(`/assessments/${assessmentId}`);
        return response.data;
    }
};

// Also export as assessmentsApi for consistency
export const assessmentsApi = assessmentApi;

// Debug: Log what we're exporting to verify the functions exist
console.log('[apiService] Exporting assessmentApi with functions:', Object.keys(assessmentApi));
console.log('[apiService] getAllAssessments function:', typeof assessmentApi.getAllAssessments);
console.log('[apiService] startAssessment function:', typeof assessmentApi.startAssessment);

// Azure Environments API (UNCHANGED)
export const azureEnvironmentsApi = {
    // Get customer environments
    getCustomerEnvironments: async (customerId) => {
        const response = await apiClient.get(`/azure-environments/customer/${customerId}`);
        return response.data;
    },

    // Add new environment
    addEnvironment: async (environmentData) => {
        const response = await apiClient.post('/azure-environments', environmentData);
        return response.data;
    },

    // Update environment
    updateEnvironment: async (environmentId, environmentData) => {
        const response = await apiClient.put(`/azure-environments/${environmentId}`, environmentData);
        return response.data;
    },

    // Delete environment
    deleteEnvironment: async (environmentId) => {
        const response = await apiClient.delete(`/azure-environments/${environmentId}`);
        return response.data;
    },

    // Test connection
    testConnection: async (environmentId) => {
        const response = await apiClient.post(`/azure-environments/${environmentId}/test-connection`);
        return response.data;
    }
};

// Test APIs (UNCHANGED)
export const testApi = {
    // Seed test data
    seedTestData: async () => {
        const response = await apiClient.post('/Test/seed-data');
        return response.data;
    },

    // Test Azure connection
    testAzureConnection: async (subscriptionIds) => {
        const response = await apiClient.post('/Test/azure-connection', subscriptionIds);
        return response.data;
    },

    // Test assessment creation
    testAssessmentCreation: async (customerId) => {
        const response = await apiClient.post(`/Test/test-assessment/${customerId}`);
        return response.data;
    },

    // Test Resource Graph query
    testResourceGraphQuery: async (subscriptionIds) => {
        const response = await apiClient.post('/Test/test-resource-graph', subscriptionIds);
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

// Client Preferences API calls (UNCHANGED)
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

// Account & Subscription APIs
export const accountApi = {
    // Get profile
    getProfile: async () => {
        const response = await apiClient.get('/Account/profile');
        return response.data;
    },

    // Update profile
    updateProfile: async (profileData) => {
        const response = await apiClient.put('/Account/profile', profileData);
        return response.data;
    },

    // Test connection
    testConnection: async (subscriptionIds) => {
        const response = await apiClient.post('/Account/test-connection', subscriptionIds);
        return response.data;
    },

    // Start trial
    startTrial: async (trialData) => {
        const response = await apiClient.post('/Account/start-trial', trialData);
        return response.data;
    },

    // Get subscription status
    getSubscriptionStatus: async () => {
        const response = await apiClient.get('/Account/subscription-status');
        return response.data;
    }
};

// Licensing API
export const licensingApi = {
    // Get available features
    getAvailableFeatures: async () => {
        const response = await apiClient.get('/licensing/features');
        return response.data;
    },

    // Get current limits
    getCurrentLimits: async () => {
        const response = await apiClient.get('/licensing/limits');
        return response.data;
    },

    // Validate assessment access
    validateAssessmentAccess: async () => {
        const response = await apiClient.post('/licensing/validate-assessment');
        return response.data;
    },

    // Track usage
    trackUsage: async (usageData) => {
        const response = await apiClient.post('/licensing/track-usage', usageData);
        return response.data;
    },

    // Get usage report
    getUsageReport: async (billingPeriod) => {
        const response = await apiClient.get('/licensing/usage-report', {
            params: { billingPeriod }
        });
        return response.data;
    }
};

// Health check (UNCHANGED)
export const healthApi = {
    checkHealth: async () => {
        const response = await apiClient.get('/health');
        return response.data;
    }
};

// Generic API utility functions (UNCHANGED)
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

// Export default client
export default apiClient;