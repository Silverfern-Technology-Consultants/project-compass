import React, { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../../../contexts/AuthContext';
import { dashboardService } from '../../../services/DashboardService';
import { BarChart3, FileText, Users, AlertTriangle, CheckCircle, Clock, TrendingUp, ArrowRight, Building } from 'lucide-react';

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

const ClientCard = ({ client, onClick }) => (
    <div
        className="flex items-center justify-between p-4 hover:bg-gray-900 rounded cursor-pointer transition-colors border border-gray-800"
        onClick={() => onClick(client)}
    >
        <div className="flex items-center space-x-3">
            <Building size={20} className="text-yellow-600" />
            <div>
                <p className="text-white font-medium">{client.name}</p>
                <p className="text-gray-400 text-sm">{client.assessmentsCount || 0} assessments • {client.subscriptionsCount || 0} subscriptions</p>
            </div>
        </div>
        <div className="flex items-center space-x-4">
            <div className="text-right">
                <p className={`font-semibold ${client.lastScore >= 80 ? 'text-green-400' : client.lastScore >= 60 ? 'text-yellow-400' : 'text-red-400'}`}>
                    {client.lastScore ? `${client.lastScore}%` : 'No data'}
                </p>
                <p className="text-gray-400 text-sm">Last assessment</p>
            </div>
            <ArrowRight size={16} className="text-gray-400" />
        </div>
    </div>
);

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

const CompanyDashboard = () => {
    const { user } = useAuth();
    const navigate = useNavigate();
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState(null);
    const [companyData, setCompanyData] = useState(null);

    useEffect(() => {
        loadCompanyMetrics();
    }, []);

    const loadCompanyMetrics = async () => {
        try {
            setLoading(true);
            setError(null);
            const data = await dashboardService.getCompanyMetrics();
            setCompanyData(data);
        } catch (err) {
            console.error('Failed to load company metrics:', err);
            setError('Failed to load company overview');
        } finally {
            setLoading(false);
        }
    };

    if (loading) {
        return (
            <div className="space-y-6">
                <div className="text-center py-12">
                    <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-yellow-600 mx-auto mb-4"></div>
                    <p className="text-gray-400">Loading company overview...</p>
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
                        onClick={loadCompanyMetrics}
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
            title: 'Total Clients',
            value: companyData?.totalClients?.toString() || '0',
            icon: Building,
            color: 'text-blue-400',
            change: companyData?.clientsGrowth
        },
        {
            title: 'Total Assessments',
            value: companyData?.totalAssessments?.toString() || '0',
            icon: FileText,
            color: 'text-green-400',
            change: companyData?.assessmentsGrowth
        },
        {
            title: 'Team Members',
            value: companyData?.teamMembers?.toString() || '0',
            icon: Users,
            color: 'text-purple-400',
            change: companyData?.teamGrowth
        },
        {
            title: 'Avg Client Score',
            value: companyData?.averageClientScore ? `${companyData.averageClientScore}%` : 'N/A',
            icon: BarChart3,
            color: 'text-yellow-400',
            change: companyData?.scoreImprovement
        }
    ];

    const quickActions = [
        {
            title: 'Add New Client',
            description: 'Onboard a new client to your MSP services',
            icon: Building,
            onClick: () => navigate('/app/company/clients'),
            color: 'bg-gray-900 hover:bg-gray-800'
        },
        {
            title: 'Invite Team Member',
            description: 'Add a new team member to your organization',
            icon: Users,
            onClick: () => navigate('/app/company/team')
        },
        {
            title: 'View All Assessments',
            description: 'Browse assessments across all clients',
            icon: FileText,
            onClick: () => navigate('/app/assessments')
        },
        {
            title: 'Generate Company Report',
            description: 'Create organization-wide performance report',
            icon: TrendingUp,
            onClick: () => navigate('/app/reports')
        }
    ];

    return (
        <div className="space-y-6">
            {/* Welcome Header */}
            {user && (
                <div className="bg-gradient-to-r from-yellow-600/10 to-yellow-800/10 border border-yellow-600/20 rounded p-6">
                    <div className="flex items-start justify-between">
                        <div>
                            <h1 className="text-2xl font-bold text-white mb-2">
                                Welcome to {user.companyName}! 👋
                            </h1>
                            <div className="grid grid-cols-1 md:grid-cols-3 gap-4 text-sm">
                                <div>
                                    <span className="text-gray-400">Plan:</span>
                                    <span className="text-white ml-2">{user.subscriptionStatus || 'Trial'}</span>
                                </div>
                                <div>
                                    <span className="text-gray-400">Total Clients:</span>
                                    <span className="text-white ml-2">{stats[0]?.value || 0}</span>
                                </div>
                                <div>
                                    <span className="text-gray-400">Team Members:</span>
                                    <span className="text-white ml-2">{stats[2]?.value || 0}</span>
                                </div>
                            </div>
                        </div>
                    </div>
                </div>
            )}

            {/* Stats Grid */}
            <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6">
                {stats.map((stat, index) => (
                    <StatsCard key={index} {...stat} />
                ))}
            </div>

            {/* Recent Client Activity */}
            <div className="bg-gray-950 border border-gray-800 rounded">
                <div className="p-6 border-b border-gray-800">
                    <div className="flex items-center justify-between">
                        <h2 className="text-lg font-semibold text-white">Recent Client Activity</h2>
                        <button
                            onClick={() => navigate('/app/company/clients')}
                            className="bg-yellow-600 hover:bg-yellow-700 text-black px-4 py-2 rounded font-medium transition-colors"
                        >
                            Manage Clients
                        </button>
                    </div>
                </div>
                <div className="p-6">
                    {companyData?.recentClients?.length > 0 ? (
                        <div className="space-y-3">
                            {companyData.recentClients.map((client, index) => (
                                <ClientCard
                                    key={client.id || index}
                                    client={client}
                                />
                            ))}
                            {companyData.totalClients > 5 && (
                                <div className="pt-4 border-t border-gray-800">
                                    <button
                                        onClick={() => navigate('/app/company/clients')}
                                        className="text-yellow-600 hover:text-yellow-700 text-sm font-medium"
                                    >
                                        View All Clients ({companyData.totalClients} total) →
                                    </button>
                                </div>
                            )}
                        </div>
                    ) : (
                        <div className="text-center py-8">
                            <Building size={48} className="text-gray-600 mx-auto mb-4" />
                            <h3 className="text-lg font-semibold text-white mb-2">No clients yet</h3>
                            <p className="text-gray-400 mb-4">Add your first client to start managing their Azure governance.</p>
                            <button
                                onClick={() => navigate('/app/company/clients')}
                                className="bg-yellow-600 hover:bg-yellow-700 text-black px-4 py-2 rounded font-medium transition-colors"
                            >
                                Add Your First Client
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

export default CompanyDashboard;