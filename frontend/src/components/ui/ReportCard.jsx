import React from 'react';
import { FileText, Download, Share2, Eye, Calendar, BarChart3, PieChart, TrendingUp } from 'lucide-react';

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

export default ReportCard;