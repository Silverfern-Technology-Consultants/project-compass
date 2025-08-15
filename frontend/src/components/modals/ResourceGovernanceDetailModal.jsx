import React, { useState, useEffect } from 'react';
import { createPortal } from 'react-dom';
import { X, BarChart3, AlertTriangle, Target, Server, User, Download } from 'lucide-react';
import { assessmentApi, apiUtils } from '../../services/apiService';
import GovernanceOverviewTab from './tabs/GovernanceOverviewTab';
import GovernanceFindingsTab from './tabs/GovernanceFindingsTab';
import GovernanceRecommendationsTab from './tabs/GovernanceRecommendationsTab';
import GovernanceResourcesTab from './tabs/GovernanceResourcesTab';

const ResourceGovernanceDetailModal = ({ isOpen, onClose, assessment }) => {
    const [activeTab, setActiveTab] = useState('overview');
    const [findings, setFindings] = useState([]);
    const [resources, setResources] = useState([]);
    const [resourceFilters, setResourceFilters] = useState({});
    const [loading, setLoading] = useState(false);
    const [error, setError] = useState(null);
    const [fullAssessment, setFullAssessment] = useState(null);

    useEffect(() => {
        if (isOpen && assessment?.id) {
            loadAssessmentData();
        }
    }, [isOpen, assessment]);

    const loadAssessmentData = async () => {
        try {
            setLoading(true);
            setError(null);
            
            // Load full assessment details to get client preferences info
            const assessmentDetails = await assessmentApi.getAssessment(assessment.id);
            setFullAssessment(assessmentDetails);
            
            // Load findings for all tabs
            const findingsResponse = await assessmentApi.getAssessmentFindings(assessment.id);
            setFindings(findingsResponse || []);

            // Load resources and filters for overview/resources tabs
            const resourcesResponse = await assessmentApi.getAssessmentResources(
                assessment.id, 
                'page=1&limit=50'
            );
            
            setResources(resourcesResponse.Resources || []);
            setResourceFilters(resourcesResponse.Filters || {});
        } catch (err) {
            console.error('Error loading assessment data:', err);
            const errorInfo = apiUtils.handleApiError(err);
            setError(errorInfo.message);
        } finally {
            setLoading(false);
        }
    };

    const handleExport = async (format) => {
        try {
            console.log(`Exporting assessment report as ${format.toUpperCase()}`);
            let response;
            
            if (format === 'csv') {
                response = await assessmentApi.exportResourcesCsv(assessment.id);
            } else if (format === 'xlsx') {
                response = await assessmentApi.exportResourcesExcel(assessment.id);
            }
            
            if (!response.ok) {
                throw new Error(`Export failed: ${response.statusText}`);
            }

            const blob = await response.blob();
            const downloadUrl = window.URL.createObjectURL(blob);
            const link = document.createElement('a');
            link.href = downloadUrl;
            link.download = `${assessment.clientName || 'Assessment'}-Report.${format}`;
            document.body.appendChild(link);
            link.click();
            document.body.removeChild(link);
            window.URL.revokeObjectURL(downloadUrl);
        } catch (error) {
            console.error(`Export ${format} failed:`, error);
            alert(`Failed to export ${format.toUpperCase()}: ${error.message}`);
        }
    };

    const getScoreColor = (score) => {
        if (score >= 80) return 'text-green-400';
        if (score >= 60) return 'text-yellow-400';
        if (score >= 40) return 'text-orange-400';
        return 'text-red-400';
    };

    // Use fullAssessment if available, fallback to passed assessment
    const displayAssessment = fullAssessment || assessment;

    if (!isOpen || !assessment) return null;

    return createPortal(
        <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-[50] p-4">
            <div className="bg-gray-900 border border-gray-800 rounded-lg w-[95vw] h-[95vh] overflow-hidden flex flex-col">
                {/* Header */}
                <div className="flex items-center justify-between p-6 border-b border-gray-800 flex-shrink-0">
                    <div className="flex-1">
                        <div className="flex items-center space-x-4 mb-2">
                            <h2 className="text-xl font-semibold text-white">
                                {assessment.assessmentType === 'Full' ? 'Resource Governance Assessment' : 
                                 assessment.assessmentType === 'NamingConvention' ? 'Naming Convention Assessment' :
                                 assessment.assessmentType === 'Tagging' ? 'Tagging Compliance Assessment' :
                                 'Governance Assessment'}
                            </h2>
                        </div>
                        <div className="flex items-center space-x-4 text-sm text-gray-400">
                            <div className="flex items-center space-x-1">
                                <User size={14} />
                                <span>{assessment.clientName || 'Unknown Client'}</span>
                            </div>
                            <div className="flex items-center space-x-1">
                                <Server size={14} />
                                <span>{assessment.environment || 'Production'} Environment</span>
                            </div>
                            <div className="flex items-center space-x-1">
                                <Target size={14} />
                                <span className={`font-medium ${getScoreColor(displayAssessment.overallScore || displayAssessment.OverallScore || displayAssessment.score)}`}>
                                    Score: {(displayAssessment.overallScore || displayAssessment.OverallScore || displayAssessment.score) ? 
                                        `${(displayAssessment.overallScore || displayAssessment.OverallScore || displayAssessment.score).toFixed(2)}%` : 'N/A'}
                                </span>
                            </div>
                        </div>
                    </div>
                    <button onClick={onClose} className="p-2 rounded-lg hover:bg-gray-800 text-gray-400 hover:text-white transition-colors">
                        <X size={20} />
                    </button>
                </div>

                {/* Tabs */}
                <div className="border-b border-gray-800 flex-shrink-0">
                    <nav className="flex space-x-8 px-6">
                        {[
                            { id: 'overview', label: 'Overview', icon: BarChart3 },
                            { id: 'findings', label: 'Findings', icon: AlertTriangle },
                            { id: 'recommendations', label: 'Recommendations', icon: Target },
                            { id: 'resources', label: 'Resources', icon: Server }
                        ].map((tab) => (
                            <button
                                key={tab.id}
                                onClick={() => setActiveTab(tab.id)}
                                className={`py-4 px-1 border-b-2 font-medium text-sm transition-colors flex items-center space-x-2 ${
                                    activeTab === tab.id 
                                        ? 'border-yellow-600 text-yellow-600' 
                                        : 'border-transparent text-gray-500 hover:text-gray-300'
                                }`}
                            >
                                <tab.icon size={16} />
                                <span>{tab.label}</span>
                            </button>
                        ))}
                    </nav>
                </div>

                {/* Content */}
                <div className="flex-1 overflow-y-auto">
                    <div className="p-6">
                        {activeTab === 'overview' && (
                            <GovernanceOverviewTab 
                                assessment={displayAssessment}
                                findings={findings}
                                resources={resources}
                                resourceFilters={resourceFilters}
                                loading={loading}
                                error={error}
                            />
                        )}

                        {activeTab === 'findings' && (
                            <GovernanceFindingsTab 
                                findings={findings}
                                loading={loading}
                                error={error}
                                assessment={displayAssessment}
                            />
                        )}

                        {activeTab === 'recommendations' && (
                            <GovernanceRecommendationsTab 
                                assessment={displayAssessment}
                                findings={findings}
                                onExport={handleExport}
                            />
                        )}

                        {activeTab === 'resources' && (
                            <GovernanceResourcesTab 
                                assessment={displayAssessment}
                                resources={resources}
                                resourceFilters={resourceFilters}
                                findings={findings}
                                onExport={handleExport}
                                onLoadResources={loadAssessmentData}
                            />
                        )}
                    </div>
                </div>

                {/* Footer */}
                <div className="flex items-center justify-between p-6 border-t border-gray-800 flex-shrink-0">
                    <div className="text-sm text-gray-400">
                        Last updated: {assessment.date || 'Unknown'} • Powered by FernWorks.io
                    </div>
                    <div className="flex space-x-3">
                        <button 
                            onClick={onClose}
                            className="px-4 py-2 text-gray-300 hover:text-white transition-colors"
                        >
                            Close
                        </button>
                        <button 
                            onClick={() => handleExport('pdf')}
                            className="px-6 py-2 bg-yellow-600 text-black rounded-lg hover:bg-yellow-700 transition-colors font-medium flex items-center space-x-2"
                        >
                            <Download size={16} />
                            <span>Export Report</span>
                        </button>
                    </div>
                </div>
            </div>
        </div>,
        document.body
    );
};

export default ResourceGovernanceDetailModal;