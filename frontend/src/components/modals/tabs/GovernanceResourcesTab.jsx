import React, { useState } from 'react';
import { 
    Download, Server, Search, Filter, X, MapPin, 
    Shield, Database, HardDrive, BarChart3, User, Zap, FileText 
} from 'lucide-react';
import { assessmentApi } from '../../../services/apiService';

const getResourceTypeInfo = (resourceType) => {
    const type = resourceType.toLowerCase();

    const resourceTypeMap = {
        'keyvaults': {
            name: 'Key Vaults',
            icon: <Shield size={16} className="text-yellow-400" />,
        },
        'components': {
            name: 'Application Insights',
            icon: <BarChart3 size={16} className="text-orange-400" />,
        },
        'managedidentities': {
            name: 'Managed Identities',
            icon: <User size={16} className="text-blue-400" />,
        },
        'workspaces': {
            name: 'Log Analytics Workspaces',
            icon: <BarChart3 size={16} className="text-purple-400" />,
        },
        'storageaccounts': {
            name: 'Storage Accounts',
            icon: <HardDrive size={16} className="text-green-400" />,
        },
        'databases': {
            name: 'SQL Databases',
            icon: <Database size={16} className="text-cyan-400" />,
        },
        'servers': {
            name: 'SQL Servers',
            icon: <Database size={16} className="text-purple-400" />,
        },
        'sites': {
            name: 'Functions/Logic Apps',
            icon: <Zap size={16} className="text-purple-400" />,
        }
    };

    if (resourceTypeMap[type]) {
        return resourceTypeMap[type];
    }

    return {
        name: resourceType.charAt(0).toUpperCase() + resourceType.slice(1),
        icon: <FileText size={16} className="text-gray-400" />,
    };
};

const GovernanceResourcesTab = ({ assessment, resources, resourceFilters, findings, onExport, onLoadResources }) => {
    const [resourceSearch, setResourceSearch] = useState('');
    const [selectedFilters, setSelectedFilters] = useState({
        resourceType: '',
        resourceGroup: '',
        location: ''
    });
    const [loading, setLoading] = useState(false);
    const [currentResources, setCurrentResources] = useState(resources);

    const loadResourcesWithFilters = async () => {
        try {
            setLoading(true);
            const params = new URLSearchParams({
                page: '1',
                limit: '50'
            });

            if (resourceSearch) params.append('search', resourceSearch);
            if (selectedFilters.resourceType) params.append('resourceType', selectedFilters.resourceType);
            if (selectedFilters.resourceGroup) params.append('resourceGroup', selectedFilters.resourceGroup);
            if (selectedFilters.location) params.append('location', selectedFilters.location);

            const response = await assessmentApi.getAssessmentResources(assessment.id, params.toString());
            setCurrentResources(response.Resources || []);
        } catch (error) {
            console.error('Error loading filtered resources:', error);
        } finally {
            setLoading(false);
        }
    };

    const clearFilters = () => {
        setSelectedFilters({
            resourceType: '',
            resourceGroup: '',
            location: ''
        });
        setResourceSearch('');
        setCurrentResources(resources);
    };

    return (
        <div className="space-y-6">
            <div className="bg-gray-800 rounded-lg p-6 border border-gray-700">
                <div className="flex items-center justify-between mb-6">
                    <h3 className="text-lg font-semibold text-white flex items-center space-x-2">
                        <Server size={20} className="text-yellow-600" />
                        <span>Azure Resources</span>
                    </h3>
                    <div className="flex space-x-2">
                        <button 
                            onClick={() => onExport('csv')}
                            className="px-4 py-2 bg-gray-700 text-white rounded-lg text-sm hover:bg-gray-600 transition-colors flex items-center space-x-2"
                        >
                            <Download size={16} />
                            <span>Export CSV</span>
                        </button>
                        <button 
                            onClick={() => onExport('xlsx')}
                            className="px-4 py-2 bg-yellow-600 text-black rounded-lg text-sm hover:bg-yellow-700 transition-colors flex items-center space-x-2"
                        >
                            <Download size={16} />
                            <span>Export Excel</span>
                        </button>
                    </div>
                </div>

                {/* Search and Filters */}
                <div className="grid grid-cols-1 md:grid-cols-5 gap-4 mb-6">
                    <div className="relative">
                        <Search size={16} className="absolute left-3 top-1/2 transform -translate-y-1/2 text-gray-400" />
                        <input
                            type="text"
                            placeholder="Search resources..."
                            value={resourceSearch}
                            onChange={(e) => setResourceSearch(e.target.value)}
                            onKeyPress={(e) => e.key === 'Enter' && loadResourcesWithFilters()}
                            className="w-full pl-10 pr-4 py-2 bg-gray-900 border border-gray-600 rounded-lg text-white text-sm focus:border-yellow-600 focus:outline-none"
                        />
                    </div>

                    <select 
                        value={selectedFilters.resourceType} 
                        onChange={(e) => setSelectedFilters(prev => ({ ...prev, resourceType: e.target.value }))}
                        className="px-3 py-2 bg-gray-900 border border-gray-600 rounded-lg text-white text-sm focus:border-yellow-600 focus:outline-none"
                    >
                        <option value="">All Types</option>
                        {Object.entries(resourceFilters.ResourceTypes || {}).map(([type, count]) => (
                            <option key={type} value={type}>{type} ({count})</option>
                        ))}
                    </select>

                    <select 
                        value={selectedFilters.resourceGroup} 
                        onChange={(e) => setSelectedFilters(prev => ({ ...prev, resourceGroup: e.target.value }))}
                        className="px-3 py-2 bg-gray-900 border border-gray-600 rounded-lg text-white text-sm focus:border-yellow-600 focus:outline-none"
                    >
                        <option value="">All Resource Groups</option>
                        {Object.entries(resourceFilters.ResourceGroups || {}).map(([rg, count]) => (
                            <option key={rg} value={rg}>{rg} ({count})</option>
                        ))}
                    </select>

                    <select 
                        value={selectedFilters.location} 
                        onChange={(e) => setSelectedFilters(prev => ({ ...prev, location: e.target.value }))}
                        className="px-3 py-2 bg-gray-900 border border-gray-600 rounded-lg text-white text-sm focus:border-yellow-600 focus:outline-none"
                    >
                        <option value="">All Locations</option>
                        {Object.entries(resourceFilters.Locations || {}).map(([loc, count]) => (
                            <option key={loc} value={loc}>{loc} ({count})</option>
                        ))}
                    </select>

                    <button 
                        onClick={loadResourcesWithFilters}
                        disabled={loading}
                        className="px-4 py-2 bg-yellow-600 hover:bg-yellow-700 text-black rounded-lg text-sm font-medium transition-colors flex items-center justify-center space-x-2 disabled:opacity-50"
                    >
                        <Filter size={16} />
                        <span>{loading ? 'Loading...' : 'Apply Filters'}</span>
                    </button>
                </div>

                <div className="flex items-center justify-between text-sm text-gray-400 mb-4">
                    <span className="flex items-center space-x-2">
                        <span>Showing {currentResources.length} resources</span>
                        {(Object.keys(selectedFilters).some(key => selectedFilters[key]) || resourceSearch) && (
                            <span className="px-2 py-1 bg-yellow-600 text-black text-xs rounded-full">Filtered</span>
                        )}
                    </span>
                    {(Object.keys(selectedFilters).some(key => selectedFilters[key]) || resourceSearch) && (
                        <button
                            onClick={clearFilters}
                            className="text-yellow-600 hover:text-yellow-500 transition-colors flex items-center space-x-1"
                        >
                            <X size={14} />
                            <span>Clear Filters</span>
                        </button>
                    )}
                </div>

                {/* Resources Table */}
                <div className="bg-gray-800 rounded-lg border border-gray-700 overflow-hidden">
                    <div className="overflow-x-auto">
                        <table className="w-full">
                            <thead className="bg-gray-700 border-b border-gray-600">
                                <tr>
                                    <th className="px-4 py-3 text-left text-xs font-medium text-gray-300 uppercase tracking-wider">Resource</th>
                                    <th className="px-4 py-3 text-left text-xs font-medium text-gray-300 uppercase tracking-wider">Type</th>
                                    <th className="px-4 py-3 text-left text-xs font-medium text-gray-300 uppercase tracking-wider">Resource Group</th>
                                    <th className="px-4 py-3 text-left text-xs font-medium text-gray-300 uppercase tracking-wider">Location</th>
                                    <th className="px-4 py-3 text-left text-xs font-medium text-gray-300 uppercase tracking-wider">Environment</th>
                                    <th className="px-4 py-3 text-left text-xs font-medium text-gray-300 uppercase tracking-wider">Compliance</th>
                                </tr>
                            </thead>
                            <tbody className="divide-y divide-gray-600">
                                {currentResources.length > 0 ? currentResources.map((resource, index) => {
                                    const hasIssues = findings.some(f => 
                                        (f.resourceName || f.ResourceName) === (resource.Name || resource.name)
                                    );
                                    const tagCount = resource.Tags ? Object.keys(resource.Tags).length : 0;

                                    return (
                                        <tr key={index} className="hover:bg-gray-700/50 transition-colors">
                                            <td className="px-4 py-3">
                                                <div className="flex items-center space-x-3">
                                                    <div className={`w-2 h-2 rounded-full flex-shrink-0 ${
                                                        hasIssues ? 'bg-red-500' : 'bg-green-500'
                                                    }`}></div>
                                                    <div className="min-w-0 flex-1">
                                                        <p className="text-sm font-medium text-white truncate">
                                                            {resource.Name || resource.name || 'Unknown'}
                                                        </p>
                                                        {resource.Id && (
                                                            <p className="text-xs text-gray-400 truncate">
                                                                {resource.Id.split('/').pop()}
                                                            </p>
                                                        )}
                                                    </div>
                                                </div>
                                            </td>
                                            <td className="px-4 py-3">
                                                <div className="flex items-center space-x-2">
                                                    {getResourceTypeInfo(resource.Type || resource.type).icon}
                                                    <span className="text-sm text-gray-300">
                                                        {resource.Type || resource.type || 'Unknown'}
                                                    </span>
                                                </div>
                                            </td>
                                            <td className="px-4 py-3">
                                                <span className="text-sm text-gray-300">
                                                    {resource.ResourceGroup || resource.resourceGroup || 'Unknown'}
                                                </span>
                                            </td>
                                            <td className="px-4 py-3">
                                                <div className="flex items-center space-x-1">
                                                    <MapPin size={14} className="text-gray-400" />
                                                    <span className="text-sm text-gray-300">
                                                        {resource.Location || resource.location || 'Unknown'}
                                                    </span>
                                                </div>
                                            </td>
                                            <td className="px-4 py-3">
                                                <span className={`inline-flex items-center px-2 py-1 rounded-full text-xs font-medium ${
                                                    (resource.Environment || resource.environment || '').toLowerCase() === 'production' ? 'bg-red-600/20 text-red-300' :
                                                    (resource.Environment || resource.environment || '').toLowerCase() === 'development' ? 'bg-blue-600/20 text-blue-300' :
                                                    'bg-gray-600/20 text-gray-300'
                                                }`}>
                                                    {resource.Environment || resource.environment || 'Unknown'}
                                                </span>
                                            </td>
                                            <td className="px-4 py-3">
                                                <div className="flex items-center space-x-2">
                                                    <span className="text-sm text-gray-300">
                                                        0% • {tagCount} tags
                                                    </span>
                                                    {hasIssues ? (
                                                        <div className="relative group">
                                                            <span className="inline-flex items-center px-2 py-1 rounded-full text-xs font-medium bg-red-600 text-white cursor-pointer">
                                                                Issues
                                                            </span>
                                                            <div className="absolute bottom-full left-1/2 transform -translate-x-1/2 mb-2 px-3 py-2 bg-gray-800 text-white text-xs rounded-lg shadow-lg border border-gray-600 whitespace-nowrap opacity-0 group-hover:opacity-100 transition-opacity duration-200 z-10">
                                                                {(() => {
                                                                    const resourceIssues = findings.filter(f => 
                                                                        (f.resourceName || f.ResourceName) === (resource.Name || resource.name)
                                                                    );
                                                                    return resourceIssues.length > 0 ? 
                                                                        resourceIssues.map(issue => {
                                                                            const issueText = issue.issue || issue.Issue || 'Unknown issue';
                                                                            return issueText.length > 50 ? `${issueText.substring(0, 50)}...` : issueText;
                                                                        }).join(', ') : 'Issues found';
                                                                })()} 
                                                            </div>
                                                        </div>
                                                    ) : (
                                                        <span className="inline-flex items-center px-2 py-1 rounded-full text-xs font-medium bg-green-600 text-white">
                                                            Clean
                                                        </span>
                                                    )}
                                                </div>
                                            </td>
                                        </tr>
                                    );
                                }) : (
                                    <tr>
                                        <td colSpan="6" className="px-4 py-8 text-center">
                                            <div className="text-gray-400">
                                                <Server size={48} className="mx-auto mb-4 opacity-50" />
                                                <p>No resources found</p>
                                                <p className="text-sm mt-1">Try adjusting your search or filters</p>
                                            </div>
                                        </td>
                                    </tr>
                                )}
                            </tbody>
                        </table>
                    </div>
                </div>
            </div>
        </div>
    );
};

export default GovernanceResourcesTab;