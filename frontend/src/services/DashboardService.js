import { apiClient } from './apiService';

export const dashboardService = {
    // Get company/MSP overview metrics
    getCompanyMetrics: async () => {
        try {
            const response = await apiClient.get('/dashboard/company');
            return response.data;
        } catch (error) {
            console.error('Failed to fetch company metrics:', error);

            // Return mock data for development if API fails
            if (process.env.NODE_ENV === 'development') {
                return {
                    totalClients: 12,
                    totalAssessments: 45,
                    teamMembers: 5,
                    averageClientScore: 78,
                    clientsGrowth: { positive: true, value: 15 },
                    assessmentsGrowth: { positive: true, value: 23 },
                    teamGrowth: { positive: true, value: 25 },
                    scoreImprovement: { positive: true, value: 8 },
                    recentClients: [
                        {
                            id: 1,
                            name: 'Acme Corporation',
                            assessmentsCount: 8,
                            subscriptionsCount: 3,
                            lastScore: 85,
                            lastAssessment: '2024-06-20'
                        },
                        {
                            id: 2,
                            name: 'Global Tech Solutions',
                            assessmentsCount: 12,
                            subscriptionsCount: 5,
                            lastScore: 72,
                            lastAssessment: '2024-06-18'
                        },
                        {
                            id: 3,
                            name: 'StartupXYZ',
                            assessmentsCount: 3,
                            subscriptionsCount: 1,
                            lastScore: 65,
                            lastAssessment: '2024-06-15'
                        }
                    ]
                };
            }
            throw error;
        }
    },

    // Get client-specific metrics
    getClientMetrics: async (clientId) => {
        try {
            const response = await apiClient.get(`/dashboard/client/${clientId}`);
            return response.data;
        } catch (error) {
            console.error('Failed to fetch client metrics:', error);

            // Return mock data for development if API fails
            if (process.env.NODE_ENV === 'development') {
                return {
                    assessmentsCount: 8,
                    currentScore: 82,
                    activeIssues: 12,
                    subscriptionsCount: 3,
                    lastAssessmentDate: '2024-06-20',
                    assessmentsGrowth: { positive: true, value: 33 },
                    scoreChange: { positive: true, value: 12 },
                    issuesChange: { positive: false, value: 8 },
                    subscriptionsChange: { positive: true, value: 50 },
                    recentAssessments: [
                        {
                            AssessmentId: 'abc123def456',
                            Name: 'Q2 Compliance Review',
                            Status: 'Completed',
                            OverallScore: 82,
                            IssuesCount: 8,
                            Environment: 'Production',
                            Date: '2024-06-20'
                        },
                        {
                            AssessmentId: 'def789ghi012',
                            Name: 'Security Assessment',
                            Status: 'Completed',
                            OverallScore: 78,
                            IssuesCount: 15,
                            Environment: 'Production',
                            Date: '2024-06-15'
                        },
                        {
                            AssessmentId: 'ghi345jkl678',
                            Name: 'Monthly Review',
                            Status: 'In Progress',
                            OverallScore: null,
                            IssuesCount: 0,
                            Environment: 'Development',
                            Date: '2024-06-25'
                        }
                    ]
                };
            }
            throw error;
        }
    },

    // Get internal/MSP infrastructure metrics
    getInternalMetrics: async () => {
        try {
            const response = await apiClient.get('/dashboard/internal');
            return response.data;
        } catch (error) {
            console.error('Failed to fetch internal metrics:', error);

            // Return mock data for development if API fails
            if (process.env.NODE_ENV === 'development') {
                return {
                    subscriptionsCount: 2,
                    infraScore: 88,
                    securityIssues: 3,
                    monthlyCost: 2450,
                    lastAssessmentDate: '2024-06-22',
                    subscriptionsGrowth: { positive: false, value: 0 },
                    scoreChange: { positive: true, value: 5 },
                    securityIssuesChange: { positive: false, value: 50 },
                    costChange: { positive: false, value: 12 },
                    totalAssessments: 6,
                    recentAssessments: [
                        {
                            AssessmentId: 'int123abc456',
                            Name: 'Internal Infrastructure Audit',
                            Status: 'Completed',
                            OverallScore: 88,
                            IssuesCount: 3,
                            Environment: 'Internal Azure',
                            Date: '2024-06-22'
                        },
                        {
                            AssessmentId: 'int789def012',
                            Name: 'Security Baseline Check',
                            Status: 'Completed',
                            OverallScore: 85,
                            IssuesCount: 5,
                            Environment: 'Internal Azure',
                            Date: '2024-06-10'
                        }
                    ]
                };
            }
            throw error;
        }
    }
};

export default dashboardService;