import React from 'react';
import { Shield, CheckCircle, AlertTriangle, XCircle, Award } from 'lucide-react';

const CompliancePage = () => {
    const complianceFrameworks = [
        {
            name: 'SOC 2',
            status: 'Compliant',
            score: 96,
            lastAssessed: '2 days ago',
            issues: 1,
            icon: Shield
        },
        {
            name: 'ISO 27001',
            status: 'Needs Attention',
            score: 78,
            lastAssessed: '1 week ago',
            issues: 5,
            icon: Award
        },
        {
            name: 'Azure Well-Architected',
            status: 'Compliant',
            score: 94,
            lastAssessed: '1 day ago',
            issues: 2,
            icon: CheckCircle
        },
        {
            name: 'CIS Controls',
            status: 'Non-Compliant',
            score: 62,
            lastAssessed: '3 days ago',
            issues: 12,
            icon: XCircle
        }
    ];

    const getStatusColor = (status) => {
        switch (status) {
            case 'Compliant': return 'text-green-400';
            case 'Needs Attention': return 'text-yellow-400';
            case 'Non-Compliant': return 'text-red-400';
            default: return 'text-gray-400';
        }
    };

    const getStatusIcon = (status) => {
        switch (status) {
            case 'Compliant': return <CheckCircle size={20} className="text-green-400" />;
            case 'Needs Attention': return <AlertTriangle size={20} className="text-yellow-400" />;
            case 'Non-Compliant': return <XCircle size={20} className="text-red-400" />;
            default: return <Shield size={20} className="text-gray-400" />;
        }
    };

    return (
        <div className="space-y-6">
            {/* Overview Stats */}
            <div className="grid grid-cols-1 md:grid-cols-4 gap-6">
                <div className="bg-gray-900 border border-gray-800 rounded p-6">
                    <div className="flex items-center justify-between">
                        <div>
                            <p className="text-sm text-gray-400">Overall Score</p>
                            <p className="text-2xl font-bold text-white">82%</p>
                        </div>
                        <Shield size={24} className="text-yellow-600" />
                    </div>
                </div>
                <div className="bg-gray-900 border border-gray-800 rounded p-6">
                    <div className="flex items-center justify-between">
                        <div>
                            <p className="text-sm text-gray-400">Compliant</p>
                            <p className="text-2xl font-bold text-green-400">2</p>
                        </div>
                        <CheckCircle size={24} className="text-green-400" />
                    </div>
                </div>
                <div className="bg-gray-900 border border-gray-800 rounded p-6">
                    <div className="flex items-center justify-between">
                        <div>
                            <p className="text-sm text-gray-400">Needs Attention</p>
                            <p className="text-2xl font-bold text-yellow-400">1</p>
                        </div>
                        <AlertTriangle size={24} className="text-yellow-400" />
                    </div>
                </div>
                <div className="bg-gray-900 border border-gray-800 rounded p-6">
                    <div className="flex items-center justify-between">
                        <div>
                            <p className="text-sm text-gray-400">Non-Compliant</p>
                            <p className="text-2xl font-bold text-red-400">1</p>
                        </div>
                        <XCircle size={24} className="text-red-400" />
                    </div>
                </div>
            </div>

            {/* Compliance Frameworks */}
            <div className="bg-gray-950 border border-gray-800 rounded">
                <div className="p-6 border-b border-gray-800">
                    <h2 className="text-lg font-semibold text-white">Compliance Frameworks</h2>
                    <p className="text-gray-400 text-sm">Monitor your compliance status across different standards</p>
                </div>
                <div className="p-6">
                    <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
                        {complianceFrameworks.map((framework, index) => (
                            <div key={index} className="bg-gray-900 border border-gray-800 rounded p-6">
                                <div className="flex items-start justify-between mb-4">
                                    <div className="flex items-center space-x-3">
                                        <div className="p-2 bg-yellow-600 rounded">
                                            <framework.icon size={20} className="text-black" />
                                        </div>
                                        <div>
                                            <h3 className="text-lg font-semibold text-white">{framework.name}</h3>
                                            <p className="text-sm text-gray-400">Last assessed: {framework.lastAssessed}</p>
                                        </div>
                                    </div>
                                    {getStatusIcon(framework.status)}
                                </div>

                                <div className="grid grid-cols-3 gap-4 mb-4">
                                    <div>
                                        <p className="text-xs text-gray-400 uppercase tracking-wide">Score</p>
                                        <p className="text-lg font-bold text-white">{framework.score}%</p>
                                    </div>
                                    <div>
                                        <p className="text-xs text-gray-400 uppercase tracking-wide">Status</p>
                                        <p className={`text-sm font-medium ${getStatusColor(framework.status)}`}>
                                            {framework.status}
                                        </p>
                                    </div>
                                    <div>
                                        <p className="text-xs text-gray-400 uppercase tracking-wide">Issues</p>
                                        <p className="text-lg font-bold text-white">{framework.issues}</p>
                                    </div>
                                </div>

                                <div className="flex items-center justify-between">
                                    <div className="w-full bg-gray-800 rounded-full h-2 mr-4">
                                        <div
                                            className={`h-2 rounded-full ${framework.score >= 90 ? 'bg-green-600' :
                                                    framework.score >= 70 ? 'bg-yellow-600' :
                                                        'bg-red-600'
                                                }`}
                                            style={{ width: `${framework.score}%` }}
                                        ></div>
                                    </div>
                                    <button className="bg-yellow-600 hover:bg-yellow-700 text-black px-3 py-1 rounded text-sm font-medium transition-colors">
                                        View Details
                                    </button>
                                </div>
                            </div>
                        ))}
                    </div>
                </div>
            </div>

            {/* Recent Compliance Issues */}
            <div className="bg-gray-950 border border-gray-800 rounded">
                <div className="p-6 border-b border-gray-800">
                    <h2 className="text-lg font-semibold text-white">Recent Compliance Issues</h2>
                </div>
                <div className="p-6">
                    <div className="space-y-4">
                        <div className="flex items-center justify-between p-4 bg-gray-900 border border-gray-800 rounded">
                            <div className="flex items-center space-x-3">
                                <XCircle size={20} className="text-red-400" />
                                <div>
                                    <h3 className="font-medium text-white">Missing encryption at rest</h3>
                                    <p className="text-sm text-gray-400">Storage account: prodstorageaccount01</p>
                                </div>
                            </div>
                            <div className="flex items-center space-x-3">
                                <span className="bg-red-600 text-white px-2 py-1 rounded text-xs font-medium">High</span>
                                <button className="text-yellow-400 hover:text-yellow-300 text-sm">Fix</button>
                            </div>
                        </div>

                        <div className="flex items-center justify-between p-4 bg-gray-900 border border-gray-800 rounded">
                            <div className="flex items-center space-x-3">
                                <AlertTriangle size={20} className="text-yellow-400" />
                                <div>
                                    <h3 className="font-medium text-white">Inconsistent tagging strategy</h3>
                                    <p className="text-sm text-gray-400">23 resources missing required tags</p>
                                </div>
                            </div>
                            <div className="flex items-center space-x-3">
                                <span className="bg-yellow-600 text-black px-2 py-1 rounded text-xs font-medium">Medium</span>
                                <button className="text-yellow-400 hover:text-yellow-300 text-sm">Fix</button>
                            </div>
                        </div>

                        <div className="flex items-center justify-between p-4 bg-gray-900 border border-gray-800 rounded">
                            <div className="flex items-center space-x-3">
                                <AlertTriangle size={20} className="text-yellow-400" />
                                <div>
                                    <h3 className="font-medium text-white">Network security group rules too permissive</h3>
                                    <p className="text-sm text-gray-400">NSG: prod-web-nsg allows 0.0.0.0/0</p>
                                </div>
                            </div>
                            <div className="flex items-center space-x-3">
                                <span className="bg-yellow-600 text-black px-2 py-1 rounded text-xs font-medium">Medium</span>
                                <button className="text-yellow-400 hover:text-yellow-300 text-sm">Fix</button>
                            </div>
                        </div>
                    </div>
                </div>
            </div>

            {/* Compliance Actions */}
            <div className="bg-gray-900 border border-gray-800 rounded p-6">
                <h3 className="font-semibold text-white mb-4">Quick Actions</h3>
                <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
                    <button className="p-4 bg-gray-800 hover:bg-gray-700 rounded transition-colors text-left">
                        <h4 className="font-medium text-white mb-1">Run Compliance Scan</h4>
                        <p className="text-sm text-gray-400">Check all frameworks against current environment</p>
                    </button>
                    <button className="p-4 bg-gray-800 hover:bg-gray-700 rounded transition-colors text-left">
                        <h4 className="font-medium text-white mb-1">Generate Report</h4>
                        <p className="text-sm text-gray-400">Create executive compliance summary</p>
                    </button>
                    <button className="p-4 bg-gray-800 hover:bg-gray-700 rounded transition-colors text-left">
                        <h4 className="font-medium text-white mb-1">Schedule Assessment</h4>
                        <p className="text-sm text-gray-400">Set up automated compliance monitoring</p>
                    </button>
                </div>
            </div>
        </div>
    );
};

export default CompliancePage;