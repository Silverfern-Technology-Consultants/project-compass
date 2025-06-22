import React, { useState, useEffect } from 'react';
import { useAuth } from '../../contexts/AuthContext';
import { useAssessments } from '../../hooks/useApi';
import { BarChart3, FileText, Calendar, AlertTriangle, CheckCircle, Clock, TrendingUp, ArrowRight } from 'lucide-react';

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

    // Enhanced display format: "ID-XXXX | Assessment Name"
    const formatAssessmentTitle = (assessment) => {
        const shortId = assessment.id.substring(0, 8).toUpperCase();
        return `${shortId} | ${assessment.name}`;
    };

    return (
        <div
            className="flex items-center justify-between p-4 hover:bg-gray-900 rounded cursor-pointer transition-colors"
            onClick={() => onClick(assessment)}
        >
            <div className="flex items-center space-x-3">
                {getStatusIcon(assessment.status)}
                <div>
                    <p className="text-white font-medium">{formatAssessmentTitle(assessment)}</p>
                    <p className="text-gray-400 text-sm">{assessment.environment} • {assessment.date}</p>
                </div>
            </div>
            <div className="flex items-center space-x-4">
                <div className="text-right">
                    <p className={`font-semibold ${getScoreColor(assessment.score)}`}>
                        {assessment.score ? `${assessment.score}%` : 'N/A'}
                    </p>
                    <p className="text-gray-400 text-sm">{assessment.issuesCount} issues</p>
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

const DashboardPage = ({ setCurrentPage }) => {
    const { user } = useAuth();
    const { assessments, loading: assessmentsLoading, error: assessmentsError, loadAssessments } = useAssessments();
    const [stats, setStats] = useState([
        { title: 'Total Assessments', value: '-', icon: FileText, color: 'text-blue-400' },
        { title: 'Average Score', value: '-', icon: BarChart3, color: 'text-green-400' },
        { title: 'Critical Issues', value: '-', icon: AlertTriangle, color: 'text-red-400' },
        { title: 'Compliance Rate', value: '-', icon: CheckCircle, color: 'text-yellow-400' }
    ]);

    // Load assessments on component mount
    useEffect(() => {
        loadAssessments();
    }, []); // Empty dependency array - only run once on mount

    // Calculate real stats from assessments
    useEffect(() => {
        if (assessments && assessments.length > 0) {
            const completedAssessments = assessments.filter(a => a.status === 'Completed');
            const totalAssessments = assessments.length;

            // Calculate average score
            const avgScore = completedAssessments.length > 0
                ? Math.round(completedAssessments.reduce((sum, a) => sum + (a.score || 0), 0) / completedAssessments.length)
                : 0;

            // Calculate total critical issues (assuming high severity issues)
            const totalIssues = assessments.reduce((sum, a) => sum + (a.issuesCount || 0), 0);

            // Calculate compliance rate (assessments with score >= 80)
            const compliantAssessments = completedAssessments.filter(a => (a.score || 0) >= 80).length;
            const complianceRate = completedAssessments.length > 0
                ? Math.round((compliantAssessments / completedAssessments.length) * 100)
                : 0;

            setStats([
                {
                    title: 'Total Assessments',
                    value: totalAssessments.toString(),
                    icon: FileText,
                    color: 'text-blue-400',
                    change: { positive: true, value: 12 }
                },
                {
                    title: 'Average Score',
                    value: avgScore > 0 ? `${avgScore}%` : 'N/A',
                    icon: BarChart3,
                    color: 'text-green-400',
                    change: avgScore > 0 ? { positive: avgScore >= 70, value: 5 } : null
                },
                {
                    title: 'Total Issues',
                    value: totalIssues.toString(),
                    icon: AlertTriangle,
                    color: 'text-red-400',
                    change: { positive: false, value: 3 }
                },
                {
                    title: 'Compliance Rate',
                    value: `${complianceRate}%`,
                    icon: CheckCircle,
                    color: 'text-yellow-400',
                    change: { positive: complianceRate >= 80, value: 8 }
                }
            ]);
        }
    }, [assessments]);

    // Get recent assessments (last 5)
    const recentAssessments = assessments ? assessments.slice(0, 5) : [];

    const handleNewAssessment = () => {
        setCurrentPage('assessments');
        // Small delay to ensure page navigation completes, then trigger new assessment modal
        setTimeout(() => {
            // Dispatch a custom event that the AssessmentsPage can listen for
            window.dispatchEvent(new CustomEvent('openNewAssessmentModal'));
        }, 100);
    };

    const handleViewAssessment = (assessment) => {
        // Store the assessment to view in sessionStorage for the AssessmentsPage
        sessionStorage.setItem('viewAssessment', JSON.stringify(assessment));
        setCurrentPage('assessments');
        // Dispatch event to open the detail modal
        setTimeout(() => {
            window.dispatchEvent(new CustomEvent('openAssessmentDetailModal', { detail: assessment }));
        }, 100);
    };

    const handleViewAllAssessments = () => {
        setCurrentPage('assessments');
    };

    const quickActions = [
        {
            title: 'Start New Assessment',
            description: 'Begin a comprehensive Azure governance analysis',
            icon: FileText,
            onClick: handleNewAssessment,
            color: 'bg-gray-900 hover:bg-gray-800'
        },
        {
            title: 'View All Assessments',
            description: 'Browse your complete assessment history',
            icon: BarChart3,
            onClick: handleViewAllAssessments
        },
        {
            title: 'Generate Report',
            description: 'Create a detailed governance report',
            icon: TrendingUp,
            onClick: () => setCurrentPage('reports')
        },
        {
            title: 'Team Management',
            description: 'Manage team access and permissions',
            icon: CheckCircle,
            onClick: () => setCurrentPage('team')
        }
    ];

    return (
        <div className="space-y-6">
            {/* Welcome Header */}
            {user && (
                <div className="bg-gradient-to-r from-yellow-600/10 to-yellow-800/10 border border-yellow-600/20 rounded p-6">
                    <h1 className="text-2xl font-bold text-white mb-2">
                        Welcome back, {user.firstName || 'User'}! 👋
                    </h1>
                    <p className="text-gray-400">
                        {user.companyName} • {user.subscriptionStatus || 'Trial'} Plan
                    </p>
                </div>
            )}

            {/* Stats Grid */}
            <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6">
                {stats.map((stat, index) => (
                    <StatsCard key={index} {...stat} />
                ))}
            </div>

            {/* Recent Assessments */}
            <div className="bg-gray-950 border border-gray-800 rounded">
                <div className="p-6 border-b border-gray-800">
                    <div className="flex items-center justify-between">
                        <h2 className="text-lg font-semibold text-white">Recent Assessments</h2>
                        <button
                            onClick={handleNewAssessment}
                            className="bg-yellow-600 hover:bg-yellow-700 text-black px-4 py-2 rounded font-medium transition-colors"
                        >
                            New Assessment
                        </button>
                    </div>
                </div>
                <div className="p-6">
                    {assessmentsLoading ? (
                        <div className="text-center py-8">
                            <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-yellow-600 mx-auto mb-4"></div>
                            <p className="text-gray-400">Loading recent assessments...</p>
                        </div>
                    ) : assessmentsError ? (
                        <div className="text-center py-8">
                            <AlertTriangle size={48} className="text-red-400 mx-auto mb-4" />
                            <p className="text-gray-400 mb-4">Unable to load recent assessments</p>
                            <button
                                onClick={handleNewAssessment}
                                className="bg-yellow-600 hover:bg-yellow-700 text-black px-4 py-2 rounded font-medium transition-colors"
                            >
                                Start New Assessment
                            </button>
                        </div>
                    ) : recentAssessments.length > 0 ? (
                        <div className="space-y-2">
                            {recentAssessments.map((assessment, index) => (
                                <RecentAssessment
                                    key={assessment.id || index}
                                    assessment={assessment}
                                    onClick={handleViewAssessment}
                                />
                            ))}
                            {assessments.length > 5 && (
                                <div className="pt-4 border-t border-gray-800">
                                    <button
                                        onClick={handleViewAllAssessments}
                                        className="text-yellow-600 hover:text-yellow-700 text-sm font-medium"
                                    >
                                        View All Assessments ({assessments.length} total) →
                                    </button>
                                </div>
                            )}
                        </div>
                    ) : (
                        <div className="text-center py-8">
                            <FileText size={48} className="text-gray-600 mx-auto mb-4" />
                            <h3 className="text-lg font-semibold text-white mb-2">No assessments yet</h3>
                            <p className="text-gray-400 mb-4">Start your first Azure governance assessment to see insights here.</p>
                            <button
                                onClick={handleNewAssessment}
                                className="bg-yellow-600 hover:bg-yellow-700 text-black px-4 py-2 rounded font-medium transition-colors"
                            >
                                Start Your First Assessment
                            </button>
                        </div>
                    )}
                </div>
            </div>

            {/* Quick Actions */}
            <div className="bg-gray-950 border border-gray-800 rounded">
                <div className="p-6 border-b border-gray-800">
                    <h2 className="text-lg font-semibold text-white">Quick Actions</h2>
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

export default DashboardPage;