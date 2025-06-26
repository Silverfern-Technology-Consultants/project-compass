// apiService.js - Enhanced with MFA support and Team Management
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
    return types[typeString] ?? 2; // Default to Full
};

// Assessment API
export const assessmentApi = {
    startAssessment: async (assessmentData) => {
        try {
            console.log('[assessmentApi] Starting assessment:', assessmentData);

            const response = await apiClient.post('/assessments', assessmentData);
            console.log('[assessmentApi] Assessment started:', response.data);
            return response.data;
        } catch (error) {
            console.error('[assessmentApi] Error starting assessment:', error);
            throw error;
        }
    },

    createAssessment: async (customerId, assessmentData) => {
        try {
            console.log('[assessmentApi] Creating assessment:', { customerId, assessmentData });

            const assessmentType = getAssessmentTypeNumber(assessmentData.type);
            const subscriptionIds = assessmentData.subscriptionIds.map(sub =>
                typeof sub === 'string' ? sub : sub.subscriptionId || sub.id
            );

            const requestData = {
                name: assessmentData.name,
                subscriptionIds: subscriptionIds,
                type: assessmentType,
                includeRecommendations: assessmentData.includeRecommendations || true
            };

            console.log('[assessmentApi] Sending request:', requestData);

            const response = await apiClient.post(`/assessments/${customerId}`, requestData);
            console.log('[assessmentApi] Assessment created:', response.data);
            return response.data;
        } catch (error) {
            console.error('[assessmentApi] Error creating assessment:', error);
            throw error;
        }
    },

    getAllAssessments: async () => {
        try {
            console.log('[assessmentApi] Getting all assessments...');
            const response = await apiClient.get('/assessments');
            console.log('[assessmentApi] All assessments response:', response.data);
            return response.data;
        } catch (error) {
            console.error('[assessmentApi] Error getting all assessments:', error);
            throw error;
        }
    },

    getAssessment: async (assessmentId) => {
        try {
            console.log('[assessmentApi] Getting assessment:', assessmentId);
            const response = await apiClient.get(`/assessments/${assessmentId}`);
            console.log('[assessmentApi] Assessment response:', response.data);
            return response.data;
        } catch (error) {
            console.error('[assessmentApi] Error getting assessment:', error);
            throw error;
        }
    },

    getAssessmentFindings: async (assessmentId) => {
        try {
            console.log('[assessmentApi] Getting assessment findings:', assessmentId);
            const response = await apiClient.get(`/assessments/${assessmentId}/findings`);
            console.log('[assessmentApi] Assessment findings response:', response.data);
            return response.data;
        } catch (error) {
            console.error('[assessmentApi] Error getting assessment findings:', error);
            throw error;
        }
    },

    deleteAssessment: async (assessmentId) => {
        try {
            console.log('[assessmentApi] Deleting assessment:', assessmentId);
            const response = await apiClient.delete(`/assessments/${assessmentId}`);
            console.log('[assessmentApi] Assessment deleted:', response.data);
            return response.data;
        } catch (error) {
            console.error('[assessmentApi] Error deleting assessment:', error);
            throw error;
        }
    }
};

// Assessments API (for listing)
export const assessmentsApi = {
    getAssessments: async () => {
        try {
            console.log('[assessmentsApi] Getting assessments...');
            const response = await apiClient.get('/assessments');
            console.log('[assessmentsApi] Assessments response:', response.data);
            return response.data;
        } catch (error) {
            console.error('[assessmentsApi] Error getting assessments:', error);
            throw error;
        }
    }
};

// Auth API class - ENHANCED for MFA support
export class AuthApi {
    // CRITICAL: This method is required for MFA functionality
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
            console.log('[AuthApi] Login attempt:', {
                email,
                password: '***',
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

// Team Management API - FIXED property casing to match backend
export const teamApi = {
    getTeamMembers: async () => {
        try {
            console.log('[teamApi] Getting team members...');
            const response = await apiClient.get('/team/members');
            console.log('[teamApi] Team members response:', response.data);
            return response.data;
        } catch (error) {
            console.error('[teamApi] getTeamMembers error:', error);
            throw error;
        }
    },

    // FIXED: Transform property names to match backend expectations (PascalCase)
    inviteTeamMember: async (inviteData) => {
        try {
            console.log('[teamApi] Inviting team member:', inviteData);

            // Transform to match backend InviteTeamMemberRequest model
            const requestData = {
                Email: inviteData.email,      // PascalCase for backend
                Role: inviteData.role,        // PascalCase for backend  
                Message: inviteData.message || "" // PascalCase for backend
            };

            console.log('[teamApi] Sending request data:', requestData);
            const response = await apiClient.post('/team/invite', requestData);
            console.log('[teamApi] Invite response:', response.data);
            return response.data;
        } catch (error) {
            console.error('[teamApi] inviteTeamMember error:', error);
            console.error('[teamApi] Error details:', error.response?.data);
            throw error;
        }
    },

    // FIXED: Transform property names to match backend expectations  
    updateTeamMember: async (memberId, updateData) => {
        try {
            console.log('[teamApi] Updating team member:', { memberId, updateData });

            // Transform to match backend UpdateTeamMemberRequest model
            const requestData = {
                Role: updateData.role  // PascalCase for backend
            };

            const response = await apiClient.put(`/team/members/${memberId}`, requestData);
            console.log('[teamApi] Update response:', response.data);
            return response.data;
        } catch (error) {
            console.error('[teamApi] updateTeamMember error:', error);
            throw error;
        }
    },

    removeTeamMember: async (memberId) => {
        try {
            console.log('[teamApi] Removing team member:', memberId);
            const response = await apiClient.delete(`/team/members/${memberId}`);
            console.log('[teamApi] Remove response:', response.data);
            return response.data;
        } catch (error) {
            console.error('[teamApi] removeTeamMember error:', error);
            throw error;
        }
    },

    getTeamStats: async () => {
        try {
            console.log('[teamApi] Getting team stats...');
            const response = await apiClient.get('/team/stats');
            console.log('[teamApi] Team stats response:', response.data);
            return response.data;
        } catch (error) {
            console.error('[teamApi] getTeamStats error:', error);
            throw error;
        }
    }
};

// Azure Environments API
export const azureEnvironmentsApi = {
    getEnvironments: async () => {
        const response = await apiClient.get('/azure-environments');
        return response.data;
    },

    createEnvironment: async (environmentData) => {
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
export const clientApi = {
    getClients: async () => {
        try {
            console.log('[clientApi] Getting clients...');
            const response = await apiClient.get('/client');
            console.log('[clientApi] Clients response:', response.data);
            return response.data;
        } catch (error) {
            console.error('[clientApi] getClients error:', error);
            throw error;
        }
    },

    getClient: async (clientId) => {
        try {
            console.log('[clientApi] Getting client:', clientId);
            const response = await apiClient.get(`/client/${clientId}`);
            console.log('[clientApi] Client response:', response.data);
            return response.data;
        } catch (error) {
            console.error('[clientApi] getClient error:', error);
            throw error;
        }
    },

    createClient: async (clientData) => {
        try {
            console.log('[clientApi] Creating client:', clientData);

            // Transform to match backend CreateClientRequest model (PascalCase)
            const requestData = {
                Name: clientData.name,
                Description: clientData.description || "",
                ContactEmail: clientData.contactEmail || "",
                ContactPhone: clientData.contactPhone || "",
                Address: clientData.address || "",
                Industry: clientData.industry || "",
                IsActive: clientData.isActive !== false // Default to true
            };

            const response = await apiClient.post('/client', requestData);
            console.log('[clientApi] Create client response:', response.data);
            return response.data;
        } catch (error) {
            console.error('[clientApi] createClient error:', error);
            throw error;
        }
    },

    updateClient: async (clientId, clientData) => {
        try {
            console.log('[clientApi] Updating client:', { clientId, clientData });

            // Transform to match backend UpdateClientRequest model (PascalCase)
            const requestData = {
                Name: clientData.name,
                Description: clientData.description || "",
                ContactEmail: clientData.contactEmail || "",
                ContactPhone: clientData.contactPhone || "",
                Address: clientData.address || "",
                Industry: clientData.industry || "",
                IsActive: clientData.isActive !== false
            };

            const response = await apiClient.put(`/client/${clientId}`, requestData);
            console.log('[clientApi] Update client response:', response.data);
            return response.data;
        } catch (error) {
            console.error('[clientApi] updateClient error:', error);
            throw error;
        }
    },

    deleteClient: async (clientId) => {
        try {
            console.log('[clientApi] Deleting client:', clientId);
            const response = await apiClient.delete(`/client/${clientId}`);
            console.log('[clientApi] Delete client response:', response.data);
            return response.data;
        } catch (error) {
            console.error('[clientApi] deleteClient error:', error);
            throw error;
        }
    },

    getClientStats: async (clientId) => {
        try {
            console.log('[clientApi] Getting client stats:', clientId);
            const response = await apiClient.get(`/client/${clientId}/stats`);
            console.log('[clientApi] Client stats response:', response.data);
            return response.data;
        } catch (error) {
            console.error('[clientApi] getClientStats error:', error);
            throw error;
        }
    }
};

// Default export
export default {
    AuthApi,
    MfaApi,
    teamApi,
    assessmentApi,
    assessmentsApi,
    azureEnvironmentsApi,
    clientApi,  // Add this line
    testApi,
    apiUtils,
    apiClient
};