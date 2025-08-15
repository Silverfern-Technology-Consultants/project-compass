import React from 'react';
import { Download, Target, AlertCircle, AlertTriangle, CheckCircle, User } from 'lucide-react';

const GovernanceRecommendationsTab = ({ assessment, findings, onExport }) => {
    // Group findings by category
    const findingsByCategory = findings.reduce((acc, finding) => {
        const category = finding.category || finding.Category || 'Other';
        if (!acc[category]) acc[category] = [];
        acc[category].push(finding);
        return acc;
    }, {});

    return (
        <div className="space-y-6">
            <div className="flex items-center justify-between">
                <h3 className="text-lg font-semibold text-white flex items-center space-x-2">
                    <Target size={20} className="text-yellow-600" />
                    <span>Actionable Recommendations</span>
                </h3>
                <div className="flex space-x-2">
                    <button 
                        onClick={() => onExport('pdf')}
                        className="px-4 py-2 bg-gray-700 text-white rounded-lg text-sm hover:bg-gray-600 transition-colors flex items-center space-x-2"
                    >
                        <Download size={16} />
                        <span>Export PDF</span>
                    </button>
                    <button 
                        onClick={() => onExport('docx')}
                        className="px-4 py-2 bg-yellow-600 text-black rounded-lg text-sm hover:bg-yellow-700 transition-colors flex items-center space-x-2"
                    >
                        <Download size={16} />
                        <span>Export DOCX</span>
                    </button>
                </div>
            </div>

            <div className="grid gap-6">
                {Object.entries(findingsByCategory).map(([category, categoryFindings]) => {
                    const severityCounts = categoryFindings.reduce((acc, finding) => {
                        const severity = (finding.severity || finding.Severity || 'Medium').toLowerCase();
                        acc[severity] = (acc[severity] || 0) + 1;
                        return acc;
                    }, {});

                    const priority = severityCounts.critical > 0 ? 'Critical' : 
                                    severityCounts.high > 0 ? 'High' : 
                                    severityCounts.medium > 0 ? 'Medium' : 'Low';

                    return (
                        <div key={category} className={`rounded-lg p-6 border ${
                            priority === 'Critical' ? 'bg-red-900/20 border-red-700' :
                            priority === 'High' ? 'bg-orange-900/20 border-orange-700' :
                            priority === 'Medium' ? 'bg-yellow-900/20 border-yellow-700' : 
                            'bg-blue-900/20 border-blue-700'
                        }`}>
                            <div className="flex items-start space-x-4">
                                <div className={`p-3 rounded-lg ${
                                    priority === 'Critical' ? 'bg-red-700' :
                                    priority === 'High' ? 'bg-orange-700' :
                                    priority === 'Medium' ? 'bg-yellow-700' : 'bg-blue-700'
                                }`}>
                                    {priority === 'Critical' ? <AlertCircle size={20} className="text-white" /> :
                                     priority === 'High' ? <AlertTriangle size={20} className="text-white" /> :
                                     priority === 'Medium' ? <AlertTriangle size={20} className="text-black" /> :
                                     <CheckCircle size={20} className="text-white" />}
                                </div>
                                <div className="flex-1">
                                    <div className="flex items-center space-x-3 mb-2">
                                        <h4 className="text-lg font-semibold text-white">
                                            {category === 'NamingConvention' ? 'Implement Consistent Naming Standards' :
                                             category === 'Tagging' ? 'Deploy Comprehensive Tagging Strategy' :
                                             `Address ${category} Issues`}
                                        </h4>
                                        <span className={`px-2 py-1 rounded-full text-xs font-medium ${
                                            priority === 'Critical' ? 'bg-red-600 text-white' :
                                            priority === 'High' ? 'bg-orange-600 text-white' :
                                            priority === 'Medium' ? 'bg-yellow-600 text-black' : 
                                            'bg-blue-600 text-white'
                                        }`}>
                                            {priority} Priority
                                        </span>
                                    </div>
                                    <p className="text-gray-300 mb-4">
                                        {categoryFindings.length} {category.toLowerCase()} issues require attention to improve governance compliance.
                                    </p>
                                    <div>
                                        <h5 className="font-medium text-white mb-3">Recommended Action Items:</h5>
                                        <div className="space-y-3">
                                            {(category === 'NamingConvention' ? [
                                                {
                                                    title: 'Define Naming Convention Policy',
                                                    description: 'Establish a clear naming standard (e.g., [env]-[app]-[resource]-[instance]) for all Azure resources'
                                                },
                                                {
                                                    title: 'Implement Azure Policy Enforcement',
                                                    description: 'Use Azure Policy to automatically enforce naming standards and prevent non-compliant resource creation'
                                                },
                                                {
                                                    title: 'Create Resource Naming Templates',
                                                    description: 'Develop ARM templates or Bicep files with standardized naming patterns for common resource types'
                                                },
                                                {
                                                    title: 'Remediate Existing Resources',
                                                    description: 'Update existing non-compliant resources to follow the new naming convention where possible'
                                                }
                                            ] : category === 'Tagging' ? [
                                                {
                                                    title: 'Define Required Tag Strategy',
                                                    description: 'Establish mandatory tags: Environment, Owner, CostCenter, and Project for all resources'
                                                },
                                                {
                                                    title: 'Implement Tag Policy Enforcement',
                                                    description: 'Create Azure Policy to enforce mandatory tags and prevent resource creation without proper tagging'
                                                },
                                                {
                                                    title: 'Set Up Automated Tagging',
                                                    description: 'Configure automatic tagging for new resources through resource groups, subscriptions, or deployment templates'
                                                },
                                                {
                                                    title: 'Bulk Tag Existing Resources',
                                                    description: 'Use PowerShell, CLI, or Azure Resource Graph to bulk tag existing untagged resources'
                                                }
                                            ] : [
                                                {
                                                    title: 'Review Individual Findings',
                                                    description: 'Assess each finding in detail to understand the specific compliance issues and their impact'
                                                },
                                                {
                                                    title: 'Prioritize Based on Business Impact',
                                                    description: 'Focus on critical and high-priority findings that affect security, compliance, or operations first'
                                                },
                                                {
                                                    title: 'Implement Remediation Plan',
                                                    description: 'Execute fixes systematically, starting with the highest priority items and working down'
                                                },
                                                {
                                                    title: 'Monitor Ongoing Compliance',
                                                    description: 'Set up regular assessments and monitoring to ensure continued compliance with governance standards'
                                                }
                                            ]).map((action, actionIndex) => (
                                                <div key={actionIndex} className="bg-gray-700/30 rounded-lg p-3 border border-gray-600">
                                                    <div className="flex items-start space-x-3">
                                                        <div className="bg-yellow-600 rounded-full w-6 h-6 flex items-center justify-center flex-shrink-0 mt-0.5">
                                                            <span className="text-black text-xs font-bold">{actionIndex + 1}</span>
                                                        </div>
                                                        <div className="flex-1">
                                                            <h6 className="text-white font-medium text-sm mb-1">{action.title}</h6>
                                                            <p className="text-gray-300 text-sm leading-relaxed">{action.description}</p>
                                                        </div>
                                                    </div>
                                                </div>
                                            ))}
                                        </div>
                                    </div>
                                </div>
                            </div>
                        </div>
                    );
                })}
            </div>

            {assessment.useClientPreferences && (
                <div className="bg-gradient-to-r from-blue-900/20 to-blue-800/20 rounded-lg p-6 border border-blue-700">
                    <h3 className="text-lg font-semibold text-white mb-4 flex items-center space-x-2">
                        <User size={20} className="text-blue-400" />
                        <span>Client-Specific Guidance</span>
                    </h3>
                    <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                        <div className="bg-gray-800/50 rounded-lg p-4 border border-blue-800/30">
                            <h4 className="font-medium text-white mb-3 flex items-center space-x-2">
                                <div className="w-2 h-2 bg-blue-400 rounded-full"></div>
                                <span>Applied Client Standards</span>
                            </h4>
                            <div className="space-y-2">
                                <div className="flex items-start space-x-2">
                                    <span className="text-blue-400 mt-1">•</span>
                                    <span className="text-sm text-gray-300"><strong>Priority Tags:</strong> Environment, Application, Schedule</span>
                                </div>
                                <div className="flex items-start space-x-2">
                                    <span className="text-blue-400 mt-1">•</span>
                                    <span className="text-sm text-gray-300"><strong>Naming:</strong> Environment prefix requirements</span>
                                </div>
                                <div className="flex items-start space-x-2">
                                    <span className="text-blue-400 mt-1">•</span>
                                    <span className="text-sm text-gray-300"><strong>Compliance:</strong> Enhanced security standards</span>
                                </div>
                            </div>
                        </div>
                        <div className="bg-gray-800/50 rounded-lg p-4 border border-blue-800/30">
                            <h4 className="font-medium text-white mb-3 flex items-center space-x-2">
                                <div className="w-2 h-2 bg-yellow-400 rounded-full"></div>
                                <span>Client-Specific Next Steps</span>
                            </h4>
                            <div className="space-y-2">
                                <div className="flex items-start space-x-2">
                                    <span className="text-yellow-400 mt-1">•</span>
                                    <span className="text-sm text-gray-300"><strong>Schedule:</strong> Client review meeting for findings discussion</span>
                                </div>
                                <div className="flex items-start space-x-2">
                                    <span className="text-yellow-400 mt-1">•</span>
                                    <span className="text-sm text-gray-300"><strong>Prioritize:</strong> Client-specific findings based on their standards</span>
                                </div>
                                <div className="flex items-start space-x-2">
                                    <span className="text-yellow-400 mt-1">•</span>
                                    <span className="text-sm text-gray-300"><strong>Monitor:</strong> Implement enhanced monitoring and compliance tracking</span>
                                </div>
                            </div>
                        </div>
                    </div>
                </div>
            )}
        </div>
    );
};

export default GovernanceRecommendationsTab;