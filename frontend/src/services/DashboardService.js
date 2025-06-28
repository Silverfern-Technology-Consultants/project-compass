// Updated DashboardService.js - Fix field mapping and error handling
import { apiClient } from './apiService';

export const dashboardService = {
    // Get company/MSP overview metrics
    getCompanyMetrics: async () => {
        try {
            console.log('[DashboardService] Fetching company metrics...');
            const response = await apiClient.get('/dashboard/company');
            console.log('[DashboardService] Raw backend response:', response.data);

            // Transform PascalCase backend response to camelCase for frontend
            const transformedData = {
                totalClients: response.data.TotalClients || 0,
                totalAssessments: response.data.TotalAssessments || 0,
                teamMembers: response.data.TeamMembers || 0,
                averageClientScore: response.data.AverageClientScore || 0,
                clientsGrowth: response.data.ClientsGrowth || { positive: true, value: 0 },
                assessmentsGrowth: response.data.AssessmentsGrowth || { positive: true, value: 0 },
                teamGrowth: response.data.TeamGrowth || { positive: true, value: 0 },
                scoreImprovement: response.data.ScoreImprovement || { positive: true, value: 0 },
                recentClients: (response.data.RecentClients || []).map(client => ({
                    id: client.Id,
                    name: client.Name,
                    assessmentsCount: client.AssessmentsCount || 0,
                    subscriptionsCount: client.SubscriptionsCount || 0,
                    lastScore: client.LastScore || 0,
                    lastAssessment: client.LastAssessment
                }))
            };

            console.log('[DashboardService] Transformed data:', transformedData);
            return transformedData;
        } catch (error) {
            console.error('[DashboardService] API call failed:', error);
            console.error('[DashboardService] Error details:', {
                status: error.response?.status,
                statusText: error.response?.statusText,
                data: error.response?.data,
                message: error.message
            });

            // Return mock data for development if API fails
            if (process.env.NODE_ENV === 'development') {
                console.warn('[DashboardService] Using fallback mock data due to API failure');
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
            console.log(`[DashboardService] Fetching client metrics for: ${clientId}`);
            const response = await apiClient.get(`/dashboard/client/${clientId}`);
            console.log('[DashboardService] Client metrics response:', response.data);

            // Transform PascalCase to camelCase
            const transformedData = {
                assessmentsCount: response.data.AssessmentsCount || 0,
                currentScore: response.data.CurrentScore || 0,
                activeIssues: response.data.ActiveIssues || 0,
                subscriptionsCount: response.data.SubscriptionsCount || 0,
                lastAssessmentDate: response.data.LastAssessmentDate || 'Never',
                assessmentsGrowth: response.data.AssessmentsGrowth || { positive: true, value: 0 },
                scoreChange: response.data.ScoreChange || { positive: true, value: 0 },
                issuesChange: response.data.IssuesChange || { positive: false, value: 0 },
                subscriptionsChange: response.data.SubscriptionsChange || { positive: true, value: 0 },
                recentAssessments: (response.data.RecentAssessments || []).map(assessment => ({
                    AssessmentId: assessment.AssessmentId,
                    Name: assessment.Name,
                    Status: assessment.Status,
                    OverallScore: assessment.OverallScore,
                    IssuesCount: assessment.IssuesCount || 0,
                    Environment: assessment.Environment || 'Azure',
                    Date: assessment.Date
                }))
            };

            return transformedData;
        } catch (error) {
            console.error(`[DashboardService] Failed to fetch client metrics for ${clientId}:`, error);

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
            console.log('[DashboardService] Fetching internal metrics...');
            const response = await apiClient.get('/dashboard/internal');
            console.log('[DashboardService] Internal metrics response:', response.data);

            // Transform PascalCase to camelCase
            const transformedData = {
                subscriptionsCount: response.data.SubscriptionsCount || 0,
                infraScore: response.data.InfraScore || 0,
                securityIssues: response.data.SecurityIssues || 0,
                monthlyCost: response.data.MonthlyCost || 0,
                lastAssessmentDate: response.data.LastAssessmentDate || 'Never',
                subscriptionsGrowth: response.data.SubscriptionsGrowth || { positive: false, value: 0 },
                scoreChange: response.data.ScoreChange || { positive: true, value: 0 },
                securityIssuesChange: response.data.SecurityIssuesChange || { positive: false, value: 0 },
                costChange: response.data.CostChange || { positive: false, value: 0 },
                totalAssessments: response.data.TotalAssessments || 0,
                recentAssessments: (response.data.RecentAssessments || []).map(assessment => ({
                    AssessmentId: assessment.AssessmentId,
                    Name: assessment.Name,
                    Status: assessment.Status,
                    OverallScore: assessment.OverallScore,
                    IssuesCount: assessment.IssuesCount || 0,
                    Environment: assessment.Environment || 'Internal Azure',
                    Date: assessment.Date
                }))
            };

            return transformedData;
        } catch (error) {
            console.error('[DashboardService] Failed to fetch internal metrics:', error);

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