import React, { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../../../contexts/AuthContext';
import { dashboardService } from '../../../services/DashboardService';
import { BarChart3, FileText, Calendar, AlertTriangle, CheckCircle, Clock, TrendingUp, ArrowRight, Server, Cloud } from 'lucide-react';

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
        const shortId = assessment.AssessmentId ? assessment.AssessmentId.substring(0, 8).toUpperCase() : 'UNKNOWN';
        return `${shortId} | ${assessment.Name || 'Internal Assessment'}`;
    };

    return (
        <div
            className="flex items-center justify-between p-4 hover:bg-gray-900 rounded cursor-pointer transition-colors"
            onClick={() => onClick(assessment)}
        >
            <div className="flex items-center space-x-3">
                {getStatusIcon(assessment.Status)}
                <div>
                    <p className="text-white font-medium">{formatAssessmentTitle(assessment)}</p>
                    <p className="text-gray-400 text-sm">{assessment.Environment || 'Internal Azure'} • {assessment.Date || 'Recent'}</p>
                </div>
            </div>
            <div className="flex items-center space-x-4">
                <div className="text-right">
                    <p className={`font-semibold ${getScoreColor(assessment.OverallScore)}`}>
                        {assessment.OverallScore ? `${Math.round(assessment.OverallScore)}%` : 'N/A'}
                    </p>
                    <p className="text-gray-400 text-sm">{assessment.IssuesCount || 0} issues</p>
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

const InternalDashboard = () => {
    const { user } = useAuth();
    const navigate = useNavigate();
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState(null);
    const [internalData, setInternalData] = useState(null);

    useEffect(() => {
        loadInternalMetrics();
    }, []);

    const loadInternalMetrics = async () => {
        try {
            setLoading(true);
            setError(null);
            const data = await dashboardService.getInternalMetrics();
            setInternalData(data);
        } catch (err) {
            console.error('Failed to load internal metrics:', err);
            setError('Failed to load internal dashboard');
        } finally {
            setLoading(false);
        }
    };

    const handleNewAssessment = () => {
        navigate('/app/assessments');
        setTimeout(() => {
            window.dispatchEvent(new CustomEvent('openNewAssessmentModal', {
                detail: { isInternalAssessment: true }
            }));
        }, 100);
    };

    const handleViewAssessment = (assessment) => {
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
                    <p className="text-gray-400">Loading internal infrastructure dashboard...</p>
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
                        onClick={loadInternalMetrics}
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
            title: 'Internal Subscriptions',
            value: internalData?.subscriptionsCount?.toString() || '0',
            icon: Cloud,
            color: 'text-blue-400',
            change: internalData?.subscriptionsGrowth
        },
        {
            title: 'Infrastructure Score',
            value: internalData?.infraScore ? `${internalData.infraScore}%` : 'N/A',
            icon: BarChart3,
            color: 'text-green-400',
            change: internalData?.scoreChange
        },
        {
            title: 'Security Issues',
            value: internalData?.securityIssues?.toString() || '0',
            icon: AlertTriangle,
            color: 'text-red-400',
            change: internalData?.securityIssuesChange
        },
        {
            title: 'Monthly Cost',
            value: internalData?.monthlyCost ? `$${internalData.monthlyCost}` : 'N/A',
            icon: TrendingUp,
            color: 'text-purple-400',
            change: internalData?.costChange
        }
    ];

    const quickActions = [
        {
            title: 'Start Internal Assessment',
            description: 'Analyze your MSP\'s Azure infrastructure',
            icon: FileText,
            onClick: handleNewAssessment,
            color: 'bg-gray-900 hover:bg-gray-800'
        },
        {
            title: 'Add Azure Subscription',
            description: 'Connect internal Azure subscriptions',
            icon: Cloud,
            onClick: () => navigate('/app/assessments') // TODO: Navigate to Azure environments
        },
        {
            title: 'View Infrastructure Reports',
            description: 'Review internal compliance reports',
            icon: TrendingUp,
            onClick: () => navigate('/app/reports')
        },
        {
            title: 'Manage Internal Settings',
            description: 'Configure MSP infrastructure settings',
            icon: Server,
            onClick: () => navigate('/app/company/settings')
        }
    ];

    return (
        <div className="space-y-6">
            {/* Internal Header */}
            <div className="bg-gradient-to-r from-purple-600/10 to-purple-800/10 border border-purple-600/20 rounded p-6">
                <div className="flex items-start justify-between">
                    <div>
                        <div className="flex items-center space-x-3 mb-3">
                            <div className="bg-purple-600 text-white px-3 py-1 rounded-full text-sm font-medium">
                                INTERNAL VIEW
                            </div>
                            <div className="text-purple-400 text-sm">
                                🏭 MSP Infrastructure
                            </div>
                        </div>
                        <div className="grid grid-cols-1 md:grid-cols-3 gap-4 text-sm">
                            <div>
                                <span className="text-gray-400">Last Assessment:</span>
                                <span className="text-white ml-2">{internalData?.lastAssessmentDate || 'Never'}</span>
                            </div>
                            <div>
                                <span className="text-gray-400">Subscriptions:</span>
                                <span className="text-white ml-2">{internalData?.subscriptionsCount || 0}</span>
                            </div>
                            <div>
                                <span className="text-gray-400">Security Issues:</span>
                                <span className="text-white ml-2">{internalData?.securityIssues || 0}</span>
                            </div>
                        </div>
                    </div>
                    <div className="text-right">
                        <p className="text-sm text-gray-400">Infrastructure Health</p>
                        <p className={`text-xl font-bold ${internalData?.infraScore >= 85 ? 'text-green-400' :
                                internalData?.infraScore >= 70 ? 'text-yellow-400' : 'text-red-400'
                            }`}>
                            {internalData?.infraScore ? `${internalData.infraScore}%` : 'No Data'}
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

            {/* Internal Assessments */}
            <div className="bg-gray-950 border border-gray-800 rounded">
                <div className="p-6 border-b border-gray-800">
                    <div className="flex items-center justify-between">
                        <div>
                            <h2 className="text-lg font-semibold text-white">Internal MSP Assessments</h2>
                            <p className="text-sm text-purple-400">Showing your company's internal infrastructure assessments</p>
                        </div>
                        <button
                            onClick={handleNewAssessment}
                            className="bg-yellow-600 hover:bg-yellow-700 text-black px-4 py-2 rounded font-medium transition-colors"
                        >
                            New Internal Assessment
                        </button>
                    </div>
                </div>
                <div className="p-6">
                    {internalData?.recentAssessments?.length > 0 ? (
                        <div className="space-y-2">
                            {internalData.recentAssessments.map((assessment, index) => (
                                <RecentAssessment
                                    key={assessment.id || index}
                                    assessment={assessment}
                                    onClick={handleViewAssessment}
                                />
                            ))}
                            {internalData.totalAssessments > 5 && (
                                <div className="pt-4 border-t border-gray-800">
                                    <button
                                        onClick={() => navigate('/app/assessments')}
                                        className="text-yellow-600 hover:text-yellow-700 text-sm font-medium"
                                    >
                                        View All Internal Assessments ({internalData.totalAssessments} total) →
                                    </button>
                                </div>
                            )}
                        </div>
                    ) : (
                        <div className="text-center py-8">
                            <Server size={48} className="text-gray-600 mx-auto mb-4" />
                            <h3 className="text-lg font-semibold text-white mb-2">No internal assessments yet</h3>
                            <p className="text-gray-400 mb-4">Configure your internal Azure subscriptions and start your first assessment.</p>
                            <button
                                onClick={handleNewAssessment}
                                className="bg-yellow-600 hover:bg-yellow-700 text-black px-4 py-2 rounded font-medium transition-colors"
                            >
                                Start First Internal Assessment
                            </button>
                        </div>
                    )}
                </div>
            </div>

            {/* Quick Actions */}
            <div className="bg-gray-950 border border-gray-800 rounded">
                <div className="p-6 border-b border-gray-800">
                    <div className="flex items-center justify-between">
                        <h2 className="text-lg font-semibold text-white">Internal Infrastructure Actions</h2>
                        <div className="text-xs text-purple-400 bg-purple-600/10 px-2 py-1 rounded">
                            Internal MSP
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

export default InternalDashboard;