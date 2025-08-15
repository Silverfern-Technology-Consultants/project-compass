// Cost Analysis API - NEW for Sprint 4
export const costAnalysisApi = {
    analyzeCostTrends: async (clientId, requestData) => {
        try {
            console.log('[costAnalysisApi] analyzeCostTrends request:', { clientId, requestData });
            
            // Transform to match backend CostAnalysisRequest model (PascalCase)
            const backendRequest = {
                SubscriptionIds: requestData.subscriptionIds || [],
                TimeRange: requestData.timeRange || 0, // LastMonthToThisMonth
                Aggregation: requestData.aggregation || 1, // ResourceType
                SortBy: requestData.sortBy || 0, // Name
                SortDirection: requestData.sortDirection || 0 // Ascending
            };
            
            const response = await apiClient.post(`/CostAnalysis/client/${clientId}/analyze`, backendRequest);
            console.log('[costAnalysisApi] analyzeCostTrends response:', response.data);
            return response.data;
        } catch (error) {
            console.error('[costAnalysisApi] analyzeCostTrends error:', error);
            throw error;
        }
    },
    
    exportCostAnalysisCsv: async (clientId, requestData) => {
        try {
            console.log('[costAnalysisApi] exportCostAnalysisCsv for client:', clientId);
            
            const backendRequest = {
                SubscriptionIds: requestData.subscriptionIds || [],
                TimeRange: requestData.timeRange || 0,
                Aggregation: requestData.aggregation || 1,
                SortBy: requestData.sortBy || 0,
                SortDirection: requestData.sortDirection || 0
            };
            
            const response = await fetch(`${API_BASE_URL}/CostAnalysis/client/${clientId}/export/csv`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'Authorization': `Bearer ${localStorage.getItem('compass_token')}`,
                },
                body: JSON.stringify(backendRequest)
            });
            
            if (!response.ok) {
                throw new Error(`Export failed: ${response.statusText}`);
            }
            
            return response; // Return the response for blob handling
        } catch (error) {
            console.error('[costAnalysisApi] exportCostAnalysisCsv error:', error);
            throw error;
        }
    }
};

﻿// apiService.js - Enhanced with MFA support, Team Management, and OAuth Integration
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
            const response = await apiClient.post('/assessments', assessmentData);
            return response.data;
        } catch (error) {
            console.error('[assessmentApi] Error starting assessment:', error);
            throw error;
        }
    },

    createAssessment: async (customerId, assessmentData) => {
        try {
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

            const response = await apiClient.post(`/assessments/${customerId}`, requestData);
            return response.data;
        } catch (error) {
            console.error('[assessmentApi] Error creating assessment:', error);
            throw error;
        }
    },

    getAllAssessments: async () => {
        try {
            const response = await apiClient.get('/assessments');
            return response.data;
        } catch (error) {
            console.error('[assessmentApi] Error getting all assessments:', error);
            throw error;
        }
    },

    getAssessment: async (assessmentId) => {
        try {
            const response = await apiClient.get(`/assessments/${assessmentId}`);
            return response.data;
        } catch (error) {
            console.error('[assessmentApi] Error getting assessment:', error);
            throw error;
        }
    },

    getAssessmentResults: async (assessmentId) => {
        try {
            const response = await apiClient.get(`/assessments/${assessmentId}/results`);
            return response.data;
        } catch (error) {
            console.error('[assessmentApi] Error getting assessment results:', error);
            throw error;
        }
    },

    getAssessmentFindings: async (assessmentId) => {
        try {
            const response = await apiClient.get(`/assessments/${assessmentId}/findings`);
            return response.data;
        } catch (error) {
            console.error('[assessmentApi] Error getting assessment findings:', error);
            throw error;
        }
    },

    deleteAssessment: async (assessmentId) => {
        try {
            const response = await apiClient.delete(`/assessments/${assessmentId}`);
            return response.data;
        } catch (error) {
            console.error('[assessmentApi] Error deleting assessment:', error);
            throw error;
        }
    },
    getAssessmentResources: async (assessmentId, queryParams = '') => {
        try {
            console.log('[assessmentApi] Getting assessment resources:', assessmentId);
            const url = queryParams
                ? `/assessments/${assessmentId}/resources?${queryParams}`
                : `/assessments/${assessmentId}/resources`;

            const response = await apiClient.get(url);
            console.log('[assessmentApi] Assessment resources response:', response.data);
            return response.data;
        } catch (error) {
            console.error('[assessmentApi] Failed to get assessment resources:', error);
            throw error;
        }
    },
    exportResourcesCsv: async (assessmentId) => {
        try {
            console.log('[assessmentApi] Exporting resources as CSV:', assessmentId);
            const response = await fetch(`${API_BASE_URL}/assessments/${assessmentId}/export/csv`, {
                method: 'GET',
                headers: {
                    'Authorization': `Bearer ${localStorage.getItem('compass_token')}`,
                },
            });

            if (!response.ok) {
                throw new Error(`Export failed: ${response.statusText}`);
            }

            return response; // Return the response for blob handling
        } catch (error) {
            console.error('[assessmentApi] Failed to export CSV:', error);
            throw error;
        }
    },
    exportResourcesExcel: async (assessmentId) => {
        try {
            console.log('[assessmentApi] Exporting resources as Excel:', assessmentId);
            const response = await fetch(`${API_BASE_URL}/assessments/${assessmentId}/export/xlsx`, {
                method: 'GET',
                headers: {
                    'Authorization': `Bearer ${localStorage.getItem('compass_token')}`,
                },
            });

            if (!response.ok) {
                throw new Error(`Export failed: ${response.statusText}`);
            }

            return response; // Return the response for blob handling
        } catch (error) {
            console.error('[assessmentApi] Failed to export Excel:', error);
            throw error;
        }
    },
};

// Assessments API (for listing)
export const assessmentsApi = {
    getAssessments: async () => {
        try {
            const response = await apiClient.get('/assessments');
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
            const requestBody = {
                email,
                password,
                ...(mfaToken && { mfaToken, isBackupCode })
            };

            const response = await apiClient.post('/auth/login', requestBody);

            // Backend returns PascalCase, frontend expects camelCase
            // Transform the response to match frontend expectations
            const data = response.data;

            console.log('[AuthApi] Raw backend response:', data);

            return {
                success: data.Success,
                token: data.Token,           // PascalCase → camelCase
                customer: data.Customer,     // PascalCase → camelCase
                requiresMfa: data.RequiresMfa,
                requiresMfaSetup: data.RequiresMfaSetup,
                requiresEmailVerification: data.RequiresEmailVerification,
                message: data.Message
            };
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
            const response = await apiClient.get('/team/members');
            return response.data;
        } catch (error) {
            console.error('[teamApi] getTeamMembers error:', error);
            throw error;
        }
    },

    // FIXED: Transform property names to match backend expectations (PascalCase)
    inviteTeamMember: async (inviteData) => {
        try {

            // Transform to match backend InviteTeamMemberRequest model
            const requestData = {
                Email: inviteData.email,      // PascalCase for backend
                Role: inviteData.role,        // PascalCase for backend  
                Message: inviteData.message || "" // PascalCase for backend
            };

            const response = await apiClient.post('/team/invite', requestData);
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

            // Transform to match backend UpdateTeamMemberRequest model
            const requestData = {
                Role: updateData.role  // PascalCase for backend
            };

            const response = await apiClient.put(`/team/members/${memberId}`, requestData);
            return response.data;
        } catch (error) {
            console.error('[teamApi] updateTeamMember error:', error);
            throw error;
        }
    },

    removeTeamMember: async (memberId) => {
        try {
            const response = await apiClient.delete(`/team/members/${memberId}`);
            return response.data;
        } catch (error) {
            console.error('[teamApi] removeTeamMember error:', error);
            throw error;
        }
    },

    getTeamStats: async () => {
        try {
            const response = await apiClient.get('/team/stats');
            return response.data;
        } catch (error) {
            console.error('[teamApi] getTeamStats error:', error);
            throw error;
        }
    }
};

// OAuth API - NEW for Sprint 3
export const oauthApi = {
    // Initiate OAuth flow for a client
    initiateOAuth: async (clientId, clientName, description = null) => {
        try {
            const requestData = {
                ClientId: clientId,
                ClientName: clientName,
                Description: description
            };

            console.log('[oauthApi] initiateOAuth request:', requestData);

            const response = await apiClient.post('/AzureEnvironment/oauth/initiate', requestData);

            console.log('[oauthApi] initiateOAuth raw response:', response);
            console.log('[oauthApi] initiateOAuth response data:', response.data);
            console.log('[oauthApi] initiateOAuth response data keys:', Object.keys(response.data));

            // Log all possible field variations to debug the field name mismatch
            const responseData = response.data;
            console.log('[oauthApi] Checking authorization URL fields:');
            console.log('  - authorizationUrl:', responseData.authorizationUrl);
            console.log('  - AuthorizationUrl:', responseData.AuthorizationUrl);
            console.log('  - authorization_url:', responseData.authorization_url);
            console.log('  - url:', responseData.url);
            console.log('  - Url:', responseData.Url);

            // Try to find the authorization URL in different field name formats
            const authUrl = responseData.authorizationUrl ||
                responseData.AuthorizationUrl ||
                responseData.authorization_url ||
                responseData.url ||
                responseData.Url;

            if (authUrl) {
                console.log('[oauthApi] Found authorization URL:', authUrl);
                return {
                    authorizationUrl: authUrl,
                    state: responseData.state || responseData.State,
                    expiresAt: responseData.expiresAt || responseData.ExpiresAt
                };
            } else {
                console.error('[oauthApi] No authorization URL found in response:', responseData);
                throw new Error('No authorization URL found in OAuth response');
            }

        } catch (error) {
            console.error('[oauthApi] initiateOAuth error:', error);
            console.error('[oauthApi] Error response:', error.response?.data);
            throw error;
        }
    },

    // Test OAuth credentials for an environment
    testOAuthCredentials: async (environmentId) => {
        try {
            console.log('[oauthApi] testOAuthCredentials for environment:', environmentId);
            const response = await apiClient.post(`/AzureEnvironment/${environmentId}/test-oauth`);
            console.log('[oauthApi] testOAuthCredentials response:', response.data);
            return response.data;
        } catch (error) {
            console.error('[oauthApi] testOAuthCredentials error:', error);
            // Return false instead of throwing for OAuth status checks
            return false;
        }
    },

    // Revoke OAuth credentials for an environment
    revokeOAuthCredentials: async (environmentId) => {
        try {
            console.log('[oauthApi] revokeOAuthCredentials for environment:', environmentId);
            const response = await apiClient.delete(`/AzureEnvironment/${environmentId}/oauth`);
            console.log('[oauthApi] revokeOAuthCredentials response:', response.data);
            return response.data;
        } catch (error) {
            console.error('[oauthApi] revokeOAuthCredentials error:', error);
            throw error;
        }
    },

    // Refresh OAuth tokens for an environment
    refreshOAuthTokens: async (environmentId) => {
        try {
            console.log('[oauthApi] refreshOAuthTokens for environment:', environmentId);
            const response = await apiClient.post(`/AzureEnvironment/${environmentId}/oauth/refresh`);
            console.log('[oauthApi] refreshOAuthTokens response:', response.data);
            return response.data;
        } catch (error) {
            console.error('[oauthApi] refreshOAuthTokens error:', error);
            throw error;
        }
    }
};

// Azure Environments API
export const azureEnvironmentsApi = {
    // Get all environments (organization-scoped)
    getEnvironments: async () => {
        const response = await apiClient.get('/AzureEnvironment');
        return response.data;
    },

    // Get environments for a specific client
    getClientEnvironments: async (clientId) => {
        try {
            const response = await apiClient.get(`/AzureEnvironment/client/${clientId}`);
            return response.data;
        } catch (error) {
            console.error('[azureEnvironmentsApi] getClientEnvironments error:', error);
            throw error;
        }
    },

    // Get a specific environment by ID
    getEnvironment: async (environmentId) => {
        try {
            const response = await apiClient.get(`/AzureEnvironment/${environmentId}`);
            return response.data;
        } catch (error) {
            console.error('[azureEnvironmentsApi] getEnvironment error:', error);
            throw error;
        }
    },

    // Create new environment for a client
    createEnvironment: async (environmentData) => {
        try {

            // Transform to match backend CreateAzureEnvironmentRequest model (PascalCase)
            const requestData = {
                ClientId: environmentData.clientId,
                Name: environmentData.name,
                Description: environmentData.description || "",
                TenantId: environmentData.tenantId,
                SubscriptionIds: environmentData.subscriptionIds || [],
                ServicePrincipalId: environmentData.servicePrincipalId || "",
                ServicePrincipalName: environmentData.servicePrincipalName || ""
            };

            const response = await apiClient.post('/AzureEnvironment', requestData);
            return response.data;
        } catch (error) {
            console.error('[azureEnvironmentsApi] createEnvironment error:', error);
            throw error;
        }
    },

    // Update existing environment
    updateEnvironment: async (environmentId, environmentData) => {
        try {

            // Transform to match backend UpdateAzureEnvironmentRequest model (PascalCase)
            const requestData = {
                Name: environmentData.name,
                Description: environmentData.description || "",
                TenantId: environmentData.tenantId,
                SubscriptionIds: environmentData.subscriptionIds || [],
                ServicePrincipalId: environmentData.servicePrincipalId || "",
                ServicePrincipalName: environmentData.servicePrincipalName || "",
                IsActive: environmentData.isActive !== false
            };

            const response = await apiClient.put(`/AzureEnvironment/${environmentId}`, requestData);
            return response.data;
        } catch (error) {
            console.error('[azureEnvironmentsApi] updateEnvironment error:', error);
            throw error;
        }
    },

    // Delete environment
    deleteEnvironment: async (environmentId) => {
        try {
            const response = await apiClient.delete(`/AzureEnvironment/${environmentId}`);
            return response.data;
        } catch (error) {
            console.error('[azureEnvironmentsApi] deleteEnvironment error:', error);
            throw error;
        }
    },

    // Test environment connection
    testConnection: async (environmentId) => {
        try {
            const response = await apiClient.post(`/AzureEnvironment/${environmentId}/test-connection`);
            return response.data;
        } catch (error) {
            console.error('[azureEnvironmentsApi] testConnection error:', error);
            throw error;
        }
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
            const response = await apiClient.get('/client');
            return response.data;
        } catch (error) {
            console.error('[clientApi] getClients error:', error);
            throw error;
        }
    },

    getClient: async (clientId) => {
        try {
            const response = await apiClient.get(`/client/${clientId}`);
            return response.data;
        } catch (error) {
            console.error('[clientApi] getClient error:', error);
            throw error;
        }
    },

    createClient: async (clientData) => {
        try {
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
            return response.data;
        } catch (error) {
            console.error('[clientApi] createClient error:', error);
            throw error;
        }
    },

    updateClient: async (clientId, clientData) => {
        try {
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
            return response.data;
        } catch (error) {
            console.error('[clientApi] updateClient error:', error);
            throw error;
        }
    },

    deleteClient: async (clientId) => {
        try {
            const response = await apiClient.delete(`/client/${clientId}`);
            return response.data;
        } catch (error) {
            console.error('[clientApi] deleteClient error:', error);
            throw error;
        }
    },

    getClientStats: async (clientId) => {
        try {
            const response = await apiClient.get(`/client/${clientId}/stats`);
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
    oauthApi,  // NEW: OAuth API
    costAnalysisApi, // NEW: Cost Analysis API
    clientApi,
    testApi,
    apiUtils,
    apiClient
};