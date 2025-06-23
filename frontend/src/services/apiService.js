// apiService.js - Enhanced with MFA support
import axios from 'axios';

const API_BASE_URL = process.env.REACT_APP_API_URL || 'https://localhost:7163/api';

// Create axios instance with default config
export const apiClient = axios.create({
    baseURL: API_BASE_URL,
    timeout: 30000,
    headers: {
        'Content-Type': 'application/json',
    },
});

// Request interceptor to add auth token
apiClient.interceptors.request.use(
    (config) => {
        const token = localStorage.getItem('compass_token');
        if (token) {
            config.headers.Authorization = `Bearer ${token}`;
        }
        return config;
    },
    (error) => {
        console.error('[API] Request interceptor error:', error);
        return Promise.reject(error);
    }
);

// Response interceptor for handling auth errors
apiClient.interceptors.response.use(
    (response) => response,
    (error) => {
        console.error('[API] Response interceptor error:', error);

        if (error.response?.status === 401) {
            // Handle unauthorized - clear token and redirect to login
            localStorage.removeItem('compass_token');
            localStorage.removeItem('authToken');
            window.location.href = '/login';
        }
        return Promise.reject(error);
    }
);

// Helper functions
const getAssessmentTypeNumber = (typeString) => {
    const types = {
        'Naming Convention': 0,
        'Tagging': 1,
        'Full': 2,
        'NamingConvention': 0
    };
    return types[typeString] ?? 2;
};

const formatTimeAgo = (date) => {
    if (!date) return 'Unknown';

    try {
        // Parse the date string - handle various formats
        let past;
        if (typeof date === 'string') {
            // Try parsing as ISO string first
            past = new Date(date);

            // If that fails, try other common formats
            if (isNaN(past.getTime())) {
                // Handle SQL Server datetime format or other formats
                past = new Date(date.replace(' ', 'T'));
            }
        } else {
            past = new Date(date);
        }

        // Check if the date is valid
        if (isNaN(past.getTime())) {
            console.warn('Invalid date received:', date);
            return 'Unknown';
        }

        const now = new Date();
        const diffInMs = now.getTime() - past.getTime();

        // For debugging - log the actual dates
        console.log('Date comparison:', { now: now.toISOString(), past: past.toISOString(), diffInMs });

        // Handle very small differences or future dates
        if (diffInMs < 0) {
            console.warn('Future date detected:', { now, past, diffInMs });
            return 'Just now'; // Treat future dates as "just now"
        }

        if (diffInMs < 60000) { // Less than 1 minute
            return 'Just now';
        }

        const diffInMinutes = Math.floor(diffInMs / (1000 * 60));
        if (diffInMinutes < 60) {
            return `${diffInMinutes}m ago`;
        }

        const diffInHours = Math.floor(diffInMinutes / 60);
        if (diffInHours < 24) {
            return `${diffInHours}h ago`;
        }

        const diffInDays = Math.floor(diffInHours / 24);
        if (diffInDays < 7) {
            return `${diffInDays}d ago`;
        }

        // For dates older than a week, show the actual date
        return past.toLocaleDateString();

    } catch (error) {
        console.error('Error formatting time:', error, 'Original date:', date);
        return 'Unknown';
    }
};

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

// Generate meaningful assessment names based on assessment data
const generateAssessmentName = (assessment) => {
    const type = assessment.assessmentType || assessment.type || 'Azure';
    const customerName = assessment.customerName || 'Environment';

    // Create a descriptive name based on type and customer
    switch (type.toLowerCase()) {
        case 'namingconvention':
        case 'naming convention':
            return `${customerName} - Naming Analysis`;
        case 'tagging':
            return `${customerName} - Tagging Assessment`;
        case 'full':
            return `${customerName} - Full Governance Review`;
        default:
            return `${customerName} - Azure Assessment`;
    }
};

// Generate environment name based on available data
const generateEnvironmentName = (assessment) => {
    // Try to infer environment from customer name or assessment type
    const customerName = assessment.customerName || '';

    if (customerName.toLowerCase().includes('prod')) return 'Production';
    if (customerName.toLowerCase().includes('dev')) return 'Development';
    if (customerName.toLowerCase().includes('test')) return 'Testing';
    if (customerName.toLowerCase().includes('staging')) return 'Staging';

    // Default based on assessment maturity
    if (assessment.overallScore >= 80) return 'Production';
    if (assessment.overallScore >= 60) return 'Staging';
    return 'Development';
};

// Authentication API class
export class AuthApi {
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

    static async login(email, password, mfaToken = null, isBackupCode = false) {
        try {
            console.log('[AuthApi] Attempting login with:', {
                email,
                passwordLength: password.length,
                hasMfaToken: !!mfaToken,
                isBackupCode
            });

            const requestBody = {
                email,
                password,
                ...(mfaToken && { mfaToken, isBackupCode })
            };

            const response = await apiClient.post('/auth/login', requestBody);
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

    static async register(userData) {
        const response = await apiClient.post('/auth/register', userData);
        return response.data;
    }

    static async verifyEmail(token) {
        const response = await apiClient.post('/auth/verify-email', { token });
        return response.data;
    }

    static async resendVerification(email) {
        const response = await apiClient.post('/auth/resend-verification', { email });
        return response.data;
    }

    static async getCurrentUser() {
        const response = await apiClient.get('/auth/me');
        return response.data;
    }

    static async checkEmailAvailability(email) {
        const response = await apiClient.post('/auth/check-email', { email });
        return response.data;
    }
}

// MFA API class - FIXED field names to match backend expectations
export class MfaApi {
    static async getMfaStatus() {
        const response = await apiClient.get('/mfa/status');
        return response.data;
    }

    static async setupMfa() {
        const response = await apiClient.post('/mfa/setup');
        return response.data;
    }

    static async verifyMfaSetup(totpCode) {
        const response = await apiClient.post('/mfa/verify-setup', { totpCode });
        return response.data;
    }

    static async verifyMfa(token, isBackupCode = false) {
        const response = await apiClient.post('/mfa/verify', {
            token,
            isBackupCode
        });
        return response.data;
    }

    static async disableMfa(password, mfaCode) {
        console.log('[MfaApi] Disabling MFA with:', { password: '***', token: mfaCode });
        const response = await apiClient.post('/mfa/disable', {
            password,
            token: mfaCode  // FIXED: Use "token" field name, not "mfaCode"
        });
        return response.data;
    }

    static async regenerateBackupCodes(mfaCode) {
        const response = await apiClient.post('/mfa/regenerate-backup-codes', {
            token: mfaCode  // FIXED: Use "token" field name, not "mfaCode"
        });
        return response.data;
    }
}

// Enhanced Assessments API with proper naming
export const assessmentApi = {
    // Get all assessments for a customer with enhanced naming
    getAllAssessments: async (customerId = '9bc034b0-852f-4618-9434-c040d13de712') => {
        try {
            console.log('[assessmentApi] getAllAssessments called with customerId:', customerId);
            const response = await apiClient.get(`/assessments/customer/${customerId}`);
            console.log('[assessmentApi] Raw API response:', response.data);

            if (!response.data || !Array.isArray(response.data)) {
                console.log('[assessmentApi] No assessments found, returning empty array');
                return [];
            }

            // Enhanced transformation with proper naming
            return response.data.map(assessment => {
                const assessmentName = generateAssessmentName(assessment);
                const environmentName = generateEnvironmentName(assessment);
                const timeAgo = formatTimeAgo(assessment.startedDate);
                const duration = calculateDuration(assessment.startedDate, assessment.completedDate);

                return {
                    id: assessment.assessmentId || assessment.id,
                    name: assessmentName,
                    environment: environmentName,
                    status: assessment.status || 'Completed',
                    score: assessment.overallScore ? Math.round(assessment.overallScore) : null,
                    resourceCount: assessment.totalResourcesAnalyzed || 0,
                    issuesCount: assessment.issuesFound || 0,
                    duration: duration,
                    date: timeAgo,
                    type: assessment.assessmentType || 'Full',
                    rawData: assessment // Include raw data for debugging
                };
            });
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
                name: assessmentData.name, // ✅ FIXED: Add the user-entered assessment name
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

// Azure Environments API
export const azureEnvironmentsApi = {
    getCustomerEnvironments: async (customerId) => {
        const response = await apiClient.get(`/azure-environments/customer/${customerId}`);
        return response.data;
    },

    addEnvironment: async (environmentData) => {
        const response = await apiClient.post('/azure-environments', environmentData);
        return response.data;
    },

    updateEnvironment: async (environmentId, environmentData) => {
        const response = await apiClient.put(`/azure-environments/${environmentId}`, environmentData);
        return response.data;
    },

    deleteEnvironment: async (environmentId) => {
        const response = await apiClient.delete(`/azure-environments/${environmentId}`);
        return response.data;
    },

    testConnection: async (environmentId) => {
        const response = await apiClient.post(`/azure-environments/${environmentId}/test-connection`);
        return response.data;
    }
};

// Test APIs
export const testApi = {
    seedTestData: async () => {
        const response = await apiClient.post('/Test/seed-data');
        return response.data;
    },

    testAzureConnection: async (subscriptionIds) => {
        const response = await apiClient.post('/Test/azure-connection', subscriptionIds);
        return response.data;
    },

    testAssessmentCreation: async (customerId) => {
        const response = await apiClient.post(`/Test/test-assessment/${customerId}`);
        return response.data;
    },

    testResourceGraphQuery: async (subscriptionIds) => {
        const response = await apiClient.post('/Test/test-resource-graph', subscriptionIds);
        return response.data;
    },

    testNamingAnalysis: async (subscriptionIds) => {
        const response = await apiClient.post('/Test/test-naming-analysis', subscriptionIds);
        return response.data;
    },

    testTaggingAnalysis: async (subscriptionIds) => {
        const response = await apiClient.post('/Test/test-tagging-analysis', subscriptionIds);
        return response.data;
    },

    getDatabaseStatus: async () => {
        const response = await apiClient.get('/Test/database-status');
        return response.data;
    },

    resetDatabase: async () => {
        const response = await apiClient.post('/Test/reset-database');
        return response.data;
    }
};

// API utilities
export const apiUtils = {
    handleApiError: (error) => {
        console.error('[apiUtils] Handling API error:', error);

        if (error.response) {
            // Server responded with error status
            return {
                message: error.response.data?.message || error.response.data?.error || 'Server error occurred',
                status: error.response.status,
                details: error.response.data
            };
        } else if (error.request) {
            // Request made but no response received
            return {
                message: 'Network error - unable to reach server',
                status: 0,
                details: 'Please check your internet connection'
            };
        } else {
            // Something else happened
            return {
                message: error.message || 'Unknown error occurred',
                status: -1,
                details: error.toString()
            };
        }
    },

    isAuthError: (error) => {
        return error.response?.status === 401 || error.response?.status === 403;
    },

    retry: async (apiCall, maxRetries = 3, delay = 1000) => {
        for (let i = 0; i < maxRetries; i++) {
            try {
                return await apiCall();
            } catch (error) {
                if (i === maxRetries - 1 || apiUtils.isAuthError(error)) {
                    throw error;
                }
                await new Promise(resolve => setTimeout(resolve, delay * Math.pow(2, i)));
            }
        }
    }
};

// Default export
export default {
    AuthApi,
    MfaApi,
    assessmentApi,
    assessmentsApi,
    azureEnvironmentsApi,
    testApi,
    apiUtils,
    apiClient
};