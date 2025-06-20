import React, { useState } from 'react';
import { FileText, Download, Share2, Eye, Calendar, Filter, Search, BarChart3, PieChart, TrendingUp, AlertTriangle } from 'lucide-react';

const ReportCard = ({ report, onView, onDownload, onShare }) => {
    const getStatusColor = (status) => {
        switch (status) {
            case 'Ready': return 'bg-green-600 text-white';
            case 'Generating': return 'bg-yellow-600 text-black';
            case 'Failed': return 'bg-red-600 text-white';
            default: return 'bg-gray-600 text-white';
        }
    };

    const getTypeIcon = (type) => {
        switch (type) {
            case 'Full Assessment': return <BarChart3 size={20} />;
            case 'Executive Summary': return <PieChart size={20} />;
            case 'Compliance Report': return <FileText size={20} />;
            case 'Trend Analysis': return <TrendingUp size={20} />;
            default: return <FileText size={20} />;
        }
    };

    return (
        <div className="bg-gray-900 border border-gray-800 rounded p-6 hover:border-gray-700 transition-colors">
            <div className="flex items-start justify-between mb-4">
                <div className="flex items-center space-x-3">
                    <div className="p-2 bg-yellow-600 rounded text-black">
                        {getTypeIcon(report.type)}
                    </div>
                    <div>
                        <h3 className="text-lg font-semibold text-white">{report.title}</h3>
                        <p className="text-gray-400 text-sm">{report.environment}</p>
                    </div>
                </div>
                <div className={`px-3 py-1 rounded text-sm font-medium ${getStatusColor(report.status)}`}>
                    {report.status}
                </div>
            </div>

            <div className="grid grid-cols-2 md:grid-cols-4 gap-4 mb-4">
                <div>
                    <p className="text-xs text-gray-400 uppercase tracking-wide">Score</p>
                    <p className="text-lg font-bold text-white">{report.score}%</p>
                </div>
                <div>
                    <p className="text-xs text-gray-400 uppercase tracking-wide">Issues</p>
                    <p className="text-lg font-bold text-white">{report.issuesFound}</p>
                </div>
                <div>
                    <p className="text-xs text-gray-400 uppercase tracking-wide">Resources</p>
                    <p className="text-lg font-bold text-white">{report.resourceCount}</p>
                </div>
                <div>
                    <p className="text-xs text-gray-400 uppercase tracking-wide">Generated</p>
                    <p className="text-lg font-bold text-white">{report.generatedDate}</p>
                </div>
            </div>

            <div className="flex items-center justify-between">
                <div className="flex items-center space-x-2 text-sm text-gray-400">
                    <Calendar size={14} />
                    <span>Assessment: {report.assessmentDate}</span>
                </div>
                {report.status === 'Ready' && (
                    <div className="flex items-center space-x-2">
                        <button
                            onClick={() => onView(report)}
                            className="flex items-center space-x-1 px-3 py-1 bg-yellow-600 hover:bg-yellow-700 text-black rounded text-sm font-medium transition-colors"
                        >
                            <Eye size={14} />
                            <span>View</span>
                        </button>
                        <button
                            onClick={() => onDownload(report)}
                            className="flex items-center space-x-1 px-3 py-1 bg-gray-800 hover:bg-gray-700 text-white rounded text-sm transition-colors"
                        >
                            <Download size={14} />
                            <span>Download</span>
                        </button>
                        <button
                            onClick={() => onShare(report)}
                            className="flex items-center space-x-1 px-3 py-1 bg-gray-800 hover:bg-gray-700 text-white rounded text-sm transition-colors"
                        >
                            <Share2 size={14} />
                            <span>Share</span>
                        </button>
                    </div>
                )}
            </div>
        </div>
    );
};

const ComplianceTrend = () => {
    const trendData = [
        { month: 'Jan', score: 78 },
        { month: 'Feb', score: 82 },
        { month: 'Mar', score: 85 },
        { month: 'Apr', score: 89 },
        { month: 'May', score: 92 },
        { month: 'Jun', score: 94 }
    ];

    return (
        <div className="bg-gray-900 border border-gray-800 rounded p-6">
            <div className="flex items-center justify-between mb-6">
                <h3 className="text-lg font-semibold text-white">Compliance Trend</h3>
                <div className="flex items-center space-x-2 text-sm text-green-400">
                    <TrendingUp size={16} />
                    <span>+16% this quarter</span>
                </div>
            </div>

            <div className="space-y-4">
                {trendData.map((data, index) => (
                    <div key={index} className="flex items-center justify-between">
                        <span className="text-gray-400 text-sm w-12">{data.month}</span>
                        <div className="flex-1 mx-4">
                            <div className="bg-gray-800 rounded-full h-2">
                                <div
                                    className="bg-gradient-to-r from-yellow-600 to-green-600 h-2 rounded-full transition-all duration-300"
                                    style={{ width: `${data.score}%` }}
                                ></div>
                            </div>
                        </div>
                        <span className="text-white font-medium text-sm w-12 text-right">{data.score}%</span>
                    </div>
                ))}
            </div>
        </div>
    );
};

const IssueBreakdown = () => {
    const issues = [
        { category: 'Naming Conventions', count: 23, severity: 'Medium', color: 'bg-yellow-600' },
        { category: 'Missing Tags', count: 18, severity: 'High', color: 'bg-red-600' },
        { category: 'Resource Organization', count: 12, severity: 'Low', color: 'bg-blue-600' },
        { category: 'Security Compliance', count: 8, severity: 'High', color: 'bg-red-600' },
        { category: 'Cost Optimization', count: 15, severity: 'Medium', color: 'bg-yellow-600' }
    ];

    return (
        <div className="bg-gray-900 border border-gray-800 rounded p-6">
            <div className="flex items-center justify-between mb-6">
                <h3 className="text-lg font-semibold text-white">Issue Breakdown</h3>
                <div className="flex items-center space-x-2 text-sm text-gray-400">
                    <AlertTriangle size={16} />
                    <span>76 total issues</span>
                </div>
            </div>

            <div className="space-y-4">
                {issues.map((issue, index) => (
                    <div key={index} className="flex items-center justify-between p-3 bg-gray-800 rounded">
                        <div className="flex items-center space-x-3">
                            <div className={`w-3 h-3 rounded-full ${issue.color}`}></div>
                            <span className="text-white font-medium">{issue.category}</span>
                        </div>
                        <div className="flex items-center space-x-3">
                            <span className={`px-2 py-1 rounded text-xs font-medium ${issue.severity === 'High' ? 'bg-red-600 text-white' :
                                    issue.severity === 'Medium' ? 'bg-yellow-600 text-black' :
                                        'bg-blue-600 text-white'
                                }`}>
                                {issue.severity}
                            </span>
                            <span className="text-white font-bold">{issue.count}</span>
                        </div>
                    </div>
                ))}
            </div>
        </div>
    );
};

const ReportsPage = () => {
    const [searchTerm, setSearchTerm] = useState('');
    const [typeFilter, setTypeFilter] = useState('All');
    const [statusFilter, setStatusFilter] = useState('All');

    const reports = [
        {
            id: 1,
            title: 'Production Environment - June 2024',
            type: 'Full Assessment',
            environment: 'Production',
            status: 'Ready',
            score: 94,
            issuesFound: 12,
            resourceCount: 247,
            assessmentDate: 'Jun 15, 2024',
            generatedDate: '2 hours ago'
        },
        {
            id: 2,
            title: 'Executive Summary - Q2 2024',
            type: 'Executive Summary',
            environment: 'All Environments',
            status: 'Ready',
            score: 89,
            issuesFound: 45,
            resourceCount: 892,
            assessmentDate: 'Jun 30, 2024',
            generatedDate: '1 day ago'
        },
        {
            id: 3,
            title: 'Development Environment Check',
            type: 'Full Assessment',
            environment: 'Development',
            status: 'Generating',
            score: 78,
            issuesFound: 23,
            resourceCount: 156,
            assessmentDate: 'Jul 1, 2024',
            generatedDate: 'In progress...'
        },
        {
            id: 4,
            title: 'SOC 2 Compliance Report',
            type: 'Compliance Report',
            environment: 'Production',
            status: 'Ready',
            score: 96,
            issuesFound: 3,
            resourceCount: 247,
            assessmentDate: 'Jun 20, 2024',
            generatedDate: '3 days ago'
        },
        {
            id: 5,
            title: 'Quarterly Trend Analysis',
            type: 'Trend Analysis',
            environment: 'All Environments',
            status: 'Ready',
            score: 92,
            issuesFound: 8,
            resourceCount: 892,
            assessmentDate: 'Jun 30, 2024',
            generatedDate: '1 week ago'
        },
        {
            id: 6,
            title: 'Staging Environment Assessment',
            type: 'Full Assessment',
            environment: 'Staging',
            status: 'Failed',
            score: 0,
            issuesFound: 0,
            resourceCount: 89,
            assessmentDate: 'Jun 28, 2024',
            generatedDate: '2 days ago'
        }
    ];

    const filteredReports = reports.filter(report => {
        const matchesSearch = report.title.toLowerCase().includes(searchTerm.toLowerCase()) ||
            report.environment.toLowerCase().includes(searchTerm.toLowerCase());
        const matchesType = typeFilter === 'All' || report.type === typeFilter;
        const matchesStatus = statusFilter === 'All' || report.status === statusFilter;
        return matchesSearch && matchesType && matchesStatus;
    });

    const handleViewReport = (report) => {
        console.log('View report:', report);
        // Here you would navigate to the report detail view
    };

    const handleDownloadReport = (report) => {
        console.log('Download report:', report);
        // Here you would trigger the download
    };

    const handleShareReport = (report) => {
        console.log('Share report:', report);
        // Here you would open share modal
    };

    const readyReports = reports.filter(r => r.status === 'Ready').length;
    const generatingReports = reports.filter(r => r.status === 'Generating').length;
    const avgScore = Math.round(reports.filter(r => r.status === 'Ready').reduce((acc, r) => acc + r.score, 0) / readyReports);

    return (
        <div className="space-y-6">
            {/* Header */}
            <div className="flex items-center justify-between">
                <div>
                    <h1 className="text-2xl font-bold text-white">Reports</h1>
                    <p className="text-gray-400">View and manage your assessment reports and analytics</p>
                </div>
                <button className="flex items-center space-x-2 bg-yellow-600 hover:bg-yellow-700 text-black px-4 py-2 rounded font-medium transition-colors">
                    <FileText size={16} />
                    <span>Generate Report</span>
                </button>
            </div>

            {/* Stats Cards */}
            <div className="grid grid-cols-1 md:grid-cols-4 gap-6">
                <div className="bg-gray-900 border border-gray-800 rounded p-6">
                    <div className="flex items-center justify-between">
                        <div>
                            <p className="text-sm text-gray-400">Ready Reports</p>
                            <p className="text-2xl font-bold text-white">{readyReports}</p>
                        </div>
                        <FileText size={24} className="text-green-400" />
                    </div>
                </div>
                <div className="bg-gray-900 border border-gray-800 rounded p-6">
                    <div className="flex items-center justify-between">
                        <div>
                            <p className="text-sm text-gray-400">Generating</p>
                            <p className="text-2xl font-bold text-yellow-400">{generatingReports}</p>
                        </div>
                        <div className="animate-spin">
                            <BarChart3 size={24} className="text-yellow-400" />
                        </div>
                    </div>
                </div>
                <div className="bg-gray-900 border border-gray-800 rounded p-6">
                    <div className="flex items-center justify-between">
                        <div>
                            <p className="text-sm text-gray-400">Average Score</p>
                            <p className="text-2xl font-bold text-white">{avgScore}%</p>
                        </div>
                        <TrendingUp size={24} className="text-green-400" />
                    </div>
                </div>
                <div className="bg-gray-900 border border-gray-800 rounded p-6">
                    <div className="flex items-center justify-between">
                        <div>
                            <p className="text-sm text-gray-400">This Month</p>
                            <p className="text-2xl font-bold text-white">8</p>
                        </div>
                        <Calendar size={24} className="text-blue-400" />
                    </div>
                </div>
            </div>

            {/* Analytics Row */}
            <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
                <ComplianceTrend />
                <IssueBreakdown />
            </div>

            {/* Filters */}
            <div className="bg-gray-900 border border-gray-800 rounded p-4">
                <div className="flex flex-col sm:flex-row gap-4">
                    <div className="flex-1 relative">
                        <Search className="absolute left-3 top-1/2 transform -translate-y-1/2 text-gray-400" size={16} />
                        <input
                            type="text"
                            placeholder="Search reports..."
                            value={searchTerm}
                            onChange={(e) => setSearchTerm(e.target.value)}
                            className="w-full bg-gray-800 border border-gray-700 rounded pl-10 pr-4 py-2 text-white focus:outline-none focus:border-yellow-600"
                        />
                    </div>
                    <div className="flex items-center space-x-3">
                        <Filter size={16} className="text-gray-400" />
                        <select
                            value={typeFilter}
                            onChange={(e) => setTypeFilter(e.target.value)}
                            className="bg-gray-800 border border-gray-700 rounded px-3 py-2 text-white focus:outline-none focus:border-yellow-600"
                        >
                            <option value="All">All Types</option>
                            <option value="Full Assessment">Full Assessment</option>
                            <option value="Executive Summary">Executive Summary</option>
                            <option value="Compliance Report">Compliance Report</option>
                            <option value="Trend Analysis">Trend Analysis</option>
                        </select>
                        <select
                            value={statusFilter}
                            onChange={(e) => setStatusFilter(e.target.value)}
                            className="bg-gray-800 border border-gray-700 rounded px-3 py-2 text-white focus:outline-none focus:border-yellow-600"
                        >
                            <option value="All">All Status</option>
                            <option value="Ready">Ready</option>
                            <option value="Generating">Generating</option>
                            <option value="Failed">Failed</option>
                        </select>
                    </div>
                </div>
            </div>

            {/* Reports Grid */}
            <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
                {filteredReports.map(report => (
                    <ReportCard
                        key={report.id}
                        report={report}
                        onView={handleViewReport}
                        onDownload={handleDownloadReport}
                        onShare={handleShareReport}
                    />
                ))}
            </div>

            {filteredReports.length === 0 && (
                <div className="bg-gray-900 border border-gray-800 rounded p-12 text-center">
                    <FileText size={48} className="text-gray-600 mx-auto mb-4" />
                    <h3 className="text-lg font-semibold text-white mb-2">No reports found</h3>
                    <p className="text-gray-400 mb-4">Try adjusting your search or filters, or generate a new report.</p>
                    <button className="bg-yellow-600 hover:bg-yellow-700 text-black px-4 py-2 rounded font-medium transition-colors">
                        Generate Your First Report
                    </button>
                </div>
            )}
        </div>
    );
};

export default ReportsPage;