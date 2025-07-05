import React, { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../../../contexts/AuthContext';
import { dashboardService } from '../../../services/DashboardService';
import { BarChart3, FileText, Calendar, AlertTriangle, CheckCircle, Clock, TrendingUp, ArrowRight, Building, Cloud } from 'lucide-react';

const StatsCard = ({ title, value, icon: Icon, color, change }) => (
    <div className="bg-gray-950 border border-gray-800 rounded p-6 hover:border-gray-700 transition-colors">
        <div className="flex items-start justify-between">
            <div>
                <p className="text-gray-400 text-sm">{title}</p>
                <p className="text-2xl font-bold text-white mt-1">{value}</p>
                {change && (
                    <p className={`text-sm mt-1 ${change.positive ? 'text-green-400' : 'text-red-400'}`}>
                        {change.positive ? '+' : ''}{change.value}% from last month
                    </p>
                )}
            </div>
            <Icon className={`w-8 h-8 ${color}`} />
        </div>
    </div>
);

const RecentAssessment = ({ assessment, onClick }) => {
    const getStatusIcon = (status) => {
        switch (status) {
            case 'Completed': return <CheckCircle size={16} className="text-green-400" />;
            case 'In Progress': return <Clock size={16} className="text-yellow-400" />;
            case 'Failed': return <AlertTriangle size={16} className="text-red-400" />;
            default: return <Clock size={16} className="text-gray-400" />;
        }
    };

    const getScoreColor = (score) => {
        if (!score) return 'text-gray-400';
        if (score >= 90) return 'text-green-400';
        if (score >= 70) return 'text-yellow-400';
        return 'text-red-400';
    };

    const formatAssessmentTitle = (assessment) => {
        // Use the actual assessment name from the API (PascalCase from backend)
        return assessment.Name || assessment.name || 'Untitled Assessment';
    };

    const formatDate = (dateString) => {
        if (!dateString) return 'Recent';
        try {
            const date = new Date(dateString);
            return date.toLocaleDateString();
        } catch {
            return 'Recent';
        }
    };

    return (
        <div
            className="flex items-center justify-between p-4 hover:bg-gray-900 rounded cursor-pointer transition-colors"
            onClick={() => onClick(assessment)}
        >
            <div className="flex items-center space-x-3">
                {getStatusIcon(assessment.Status || assessment.status)}
                <div>
                    <p className="text-white font-medium">{formatAssessmentTitle(assessment)}</p>
                    <p className="text-gray-400 text-sm">
                        {assessment.Environment || assessment.environment || 'Azure'} • {formatDate(assessment.StartedDate || assessment.createdDate)}
                    </p>
                </div>
            </div>
            <div className="flex items-center space-x-4">
                <div className="text-right">
                    <p className={`font-semibold ${getScoreColor(assessment.OverallScore || assessment.overallScore)}`}>
                        {assessment.OverallScore || assessment.overallScore ?
                            `${Math.round(assessment.OverallScore || assessment.overallScore)}%` : 'N/A'}
                    </p>
                    <p className="text-gray-400 text-sm">
                        {assessment.IssuesFound || assessment.issuesCount || 0} issues
                    </p>
                </div>
                <ArrowRight size={16} className="text-gray-400" />
            </div>
        </div>
    );
};

const QuickAction = ({ title, description, icon: Icon, onClick, color = "bg-gray-900" }) => (
    <div className={`${color} border border-gray-800 rounded p-6 hover:border-gray-700 transition-colors cursor-pointer`} onClick={onClick}>
        <div className="flex items-start space-x-3">
            <Icon size={24} className="text-yellow-600 mt-1" />
            <div>
                <h3 className="text-white font-semibold mb-1">{title}</h3>
                <p className="text-gray-400 text-sm">{description}</p>
            </div>
        </div>
    </div>
);

const ClientDashboard = ({ client }) => {
    const { user } = useAuth();
    const navigate = useNavigate();
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState(null);
    const [clientData, setClientData] = useState(null);
    const [realAssessments, setRealAssessments] = useState([]);

    useEffect(() => {
        if (client?.ClientId) {
            loadClientData(client.ClientId);
        }
    }, [client?.ClientId]);

    const loadClientData = async (clientId) => {
        try {
            setLoading(true);
            setError(null);

            // Load mock dashboard data
            const data = await dashboardService.getClientMetrics(clientId);
            setClientData(data);

            // Load real assessments from the API
            await loadRealAssessments(clientId);

            await loadClientSubscriptions(clientId);

        } catch (err) {
            console.error('Failed to load client data:', err);
            setError('Failed to load client dashboard');
        } finally {
            setLoading(false);
        }
    };

    const loadRealAssessments = async (clientId) => {
        try {
            // Import the apiService to use existing assessment API
            const { assessmentApi } = await import('../../../services/apiService');

            // Get all assessments using the correct method name
            const allAssessments = await assessmentApi.getAllAssessments();

            // Filter assessments for this client
            const clientAssessments = allAssessments.filter(assessment => {
                const assessmentClientId = assessment.ClientId || assessment.clientId;
                return assessmentClientId === clientId;
            });

            setRealAssessments(clientAssessments);

            // Update stats with real data
            if (clientAssessments.length > 0) {
                const completedAssessments = clientAssessments.filter(a =>
                    (a.Status || a.status) === 'Completed'
                );

                const averageScore = completedAssessments.length > 0
                    ? completedAssessments.reduce((sum, a) => sum + (a.OverallScore || a.overallScore || 0), 0) / completedAssessments.length
                    : 0;

                // Get the most recent assessment (by date)
                const mostRecentAssessment = clientAssessments.reduce((latest, current) => {
                    const currentDate = new Date(current.StartedDate || current.createdDate || 0);
                    const latestDate = new Date(latest?.StartedDate || latest?.createdDate || 0);
                    return currentDate > latestDate ? current : latest;
                }, null);

                // Only count active issues from the most recent assessment
                const activeIssues = mostRecentAssessment
                    ? (mostRecentAssessment.IssuesFound || mostRecentAssessment.issuesCount || 0)
                    : 0;

                // Get the most recent assessment date
                const mostRecentDate = mostRecentAssessment
                    ? (mostRecentAssessment.StartedDate || mostRecentAssessment.createdDate)
                    : null;

                // Format the date for display
                const formattedDate = mostRecentDate
                    ? new Date(mostRecentDate).toLocaleDateString()
                    : 'Never';

                // Update clientData with real stats
                setClientData(prev => ({
                    ...prev,
                    assessmentsCount: clientAssessments.length,
                    currentScore: Math.round(averageScore),
                    activeIssues: activeIssues, // Only from latest assessment
                    lastAssessmentDate: formattedDate,
                    mostRecentAssessment: mostRecentAssessment // Store for reference
                }));

                console.log('[ClientDashboard] Updated stats:', {
                    totalAssessments: clientAssessments.length,
                    averageScore: Math.round(averageScore),
                    activeIssuesFromLatest: activeIssues,
                    mostRecentAssessmentName: mostRecentAssessment?.Name || mostRecentAssessment?.name,
                    mostRecentDate: formattedDate
                });
            } else {
                console.log('[ClientDashboard] No assessments found for client:', clientId);
            }
        } catch (err) {
            console.error('[ClientDashboard] Failed to load real assessments:', err);
            // Fall back to mock data if real API fails
        }
    };
    const loadClientSubscriptions = async (clientId) => {
        try {
            // Import the Azure environments API
            const { azureEnvironmentsApi } = await import('../../../services/apiService');

            // Get Azure environments for this client
            const environments = await azureEnvironmentsApi.getClientEnvironments(clientId);


            // Count total subscriptions across all environments
            let totalSubscriptions = 0;
            if (environments && Array.isArray(environments)) {
                totalSubscriptions = environments.reduce((total, env) => {
                    const subscriptionIds = env.SubscriptionIds || env.subscriptionIds || [];
                    return total + subscriptionIds.length;
                }, 0);
            }

            // Update clientData with real subscription count
            setClientData(prev => ({
                ...prev,
                subscriptionsCount: totalSubscriptions
            }));

        } catch (err) {
            console.error('[ClientDashboard] Failed to load client subscriptions:', err);
            // Don't throw error, just log it - subscription count is not critical
        }
    };
    const handleNewAssessment = () => {
        navigate('/app/assessments');
        setTimeout(() => {
            window.dispatchEvent(new CustomEvent('openNewAssessmentModal', {
                detail: { preselectedClient: client }
            }));
        }, 100);
    };

    const handleViewAssessment = (assessment) => {
        // Store assessment data for the detail modal
        sessionStorage.setItem('viewAssessment', JSON.stringify(assessment));
        navigate('/app/assessments');
        setTimeout(() => {
            window.dispatchEvent(new CustomEvent('openAssessmentDetailModal', { detail: assessment }));
        }, 100);
    };

    if (loading) {
        return (
            <div className="space-y-6">
                <div className="text-center py-12">
                    <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-yellow-600 mx-auto mb-4"></div>
                    <p className="text-gray-400">Loading {client?.Name} dashboard...</p>
                </div>
            </div>
        );
    }

    if (error) {
        return (
            <div className="space-y-6">
                <div className="text-center py-12">
                    <AlertTriangle size={48} className="text-red-400 mx-auto mb-4" />
                    <p className="text-gray-400 mb-4">{error}</p>
                    <button
                        onClick={() => loadClientData(client?.ClientId)}
                        className="bg-yellow-600 hover:bg-yellow-700 text-black px-4 py-2 rounded font-medium transition-colors"
                    >
                        Retry
                    </button>
                </div>
            </div>
        );
    }

    const stats = [
        {
            title: 'Client Assessments',
            value: clientData?.assessmentsCount?.toString() || '0',
            icon: FileText,
            color: 'text-blue-400',
            change: clientData?.assessmentsGrowth
        },
        {
            title: 'Current Score',
            value: clientData?.currentScore ? `${clientData.currentScore}%` : 'N/A',
            icon: BarChart3,
            color: 'text-green-400',
            change: clientData?.scoreChange
        },
        {
            title: 'Active Issues',
            value: clientData?.activeIssues?.toString() || '0',
            icon: AlertTriangle,
            color: 'text-red-400',
            change: clientData?.issuesChange
        },
        {
            title: 'Subscriptions',
            value: clientData?.subscriptionsCount?.toString() || '0',
            icon: Cloud,
            color: 'text-purple-400',
            change: clientData?.subscriptionsChange
        }
    ];

    const quickActions = [
        {
            title: 'Start New Assessment',
            description: `Run governance analysis for ${client?.Name}`,
            icon: FileText,
            onClick: handleNewAssessment,
            color: 'bg-gray-900 hover:bg-gray-800'
        },
        {
            title: 'View Client Details',
            description: 'Manage client settings and information',
            icon: Building,
            onClick: () => navigate('/app/company/clients')
        },
        {
            title: 'Manage Subscriptions',
            description: 'Configure Azure environment connections',
            icon: Cloud,
            onClick: () => navigate('/app/assessments') // TODO: Navigate to Azure environments
        },
        {
            title: 'Generate Client Report',
            description: 'Create detailed governance report',
            icon: TrendingUp,
            onClick: () => navigate('/app/reports')
        }
    ];

    // Use real assessments if available, otherwise fall back to mock data
    const assessmentsToShow = realAssessments.length > 0 ? realAssessments : (clientData?.recentAssessments || []);

    return (
        <div className="space-y-6">
            {/* Client Header */}
            <div className="bg-gradient-to-r from-blue-600/10 to-blue-800/10 border border-blue-600/20 rounded p-6">
                <div className="flex items-start justify-between">
                    <div>
                        <div className="flex items-center space-x-3 mb-3">
                            <div className="bg-blue-600 text-white px-3 py-1 rounded-full text-sm font-medium">
                                CLIENT VIEW
                            </div>
                            <div className="text-blue-400 text-sm">
                                🏢 {client?.Name}
                            </div>
                        </div>
                        <div className="grid grid-cols-1 md:grid-cols-3 gap-4 text-sm">
                            <div>
                                <span className="text-gray-400">Last Assessment:</span>
                                <span className="text-white ml-2">{clientData?.lastAssessmentDate || 'Never'}</span>
                            </div>
                            <div>
                                <span className="text-gray-400">Subscriptions:</span>
                                <span className="text-white ml-2">{clientData?.subscriptionsCount || 0}</span>
                            </div>
                            <div>
                                <span className="text-gray-400">Active Issues:</span>
                                <span className="text-white ml-2">{clientData?.activeIssues || 0}</span>
                            </div>
                        </div>
                    </div>
                    <div className="text-right">
                        <p className="text-sm text-gray-400">Compliance Status</p>
                        <p className={`text-xl font-bold ${clientData?.currentScore >= 80 ? 'text-green-400' :
                            clientData?.currentScore >= 60 ? 'text-yellow-400' : 'text-red-400'
                            }`}>
                            {clientData?.currentScore ? `${clientData.currentScore}%` : 'No Data'}
                        </p>
                    </div>
                </div>
            </div>

            {/* Stats Grid */}
            <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6">
                {stats.map((stat, index) => (
                    <StatsCard key={index} {...stat} />
                ))}
            </div>

            {/* Client Assessments */}
            <div className="bg-gray-950 border border-gray-800 rounded">
                <div className="p-6 border-b border-gray-800">
                    <div className="flex items-center justify-between">
                        <div>
                            <h2 className="text-lg font-semibold text-white">Client Assessments for {client?.Name}</h2>
                            <p className="text-sm text-blue-400">
                                {realAssessments.length > 0
                                    ? `Showing ${realAssessments.length} real assessment(s)`
                                    : 'Showing assessments specific to this client'
                                }
                            </p>
                        </div>
                        <button
                            onClick={handleNewAssessment}
                            className="bg-yellow-600 hover:bg-yellow-700 text-black px-4 py-2 rounded font-medium transition-colors"
                        >
                            New Assessment
                        </button>
                    </div>
                </div>
                <div className="p-6">
                    {assessmentsToShow.length > 0 ? (
                        <div className="space-y-2">
                            {assessmentsToShow.slice(0, 5).map((assessment, index) => (
                                <RecentAssessment
                                    key={assessment.assessmentId || assessment.AssessmentId || assessment.id || index}
                                    assessment={assessment}
                                    onClick={handleViewAssessment}
                                />
                            ))}
                            {assessmentsToShow.length > 5 && (
                                <div className="pt-4 border-t border-gray-800">
                                    <button
                                        onClick={() => navigate('/app/assessments')}
                                        className="text-yellow-600 hover:text-yellow-700 text-sm font-medium"
                                    >
                                        View All Assessments ({assessmentsToShow.length} total) →
                                    </button>
                                </div>
                            )}
                        </div>
                    ) : (
                        <div className="text-center py-8">
                            <FileText size={48} className="text-gray-600 mx-auto mb-4" />
                            <h3 className="text-lg font-semibold text-white mb-2">No assessments yet for {client?.Name}</h3>
                            <p className="text-gray-400 mb-4">Start the first Azure governance assessment for this client.</p>
                            <button
                                onClick={handleNewAssessment}
                                className="bg-yellow-600 hover:bg-yellow-700 text-black px-4 py-2 rounded font-medium transition-colors"
                            >
                                Start First Assessment
                            </button>
                        </div>
                    )}
                </div>
            </div>

            {/* Quick Actions */}
            <div className="bg-gray-950 border border-gray-800 rounded">
                <div className="p-6 border-b border-gray-800">
                    <div className="flex items-center justify-between">
                        <h2 className="text-lg font-semibold text-white">Client Management Actions</h2>
                        <div className="text-xs text-blue-400 bg-blue-600/10 px-2 py-1 rounded">
                            Client: {client?.Name}
                        </div>
                    </div>
                </div>
                <div className="p-6">
                    <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                        {quickActions.map((action, index) => (
                            <QuickAction key={index} {...action} />
                        ))}
                    </div>
                </div>
            </div>
        </div>
    );
};

export default ClientDashboard;