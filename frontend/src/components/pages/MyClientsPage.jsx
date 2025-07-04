import React, { useState, useEffect } from 'react';
import { Building2, Plus, AlertCircle, RefreshCw, Search, Filter, Grid, List, X, SortAsc } from 'lucide-react';
import { useClient } from '../../contexts/ClientContext';
import { useAuth } from '../../contexts/AuthContext';
import ClientCard from '../ui/ClientCard';
import AddClientModal from '../modals/AddClientModal';
import EditClientModal from '../modals/EditClientModal';
import ManageSubscriptionsModal from '../modals/ManageSubscriptionsModal';
import ClientDetailsModal from '../modals/ClientDetailsModal';
import ClientPreferencesModal from '../modals/ClientPreferencesModal';

const MyClientsPage = () => {
    const { clients, isLoading, error, loadClients } = useClient();
    const { user } = useAuth();
    const [localLoading, setLocalLoading] = useState(false);
    const [searchTerm, setSearchTerm] = useState('');
    const [filterStatus, setFilterStatus] = useState('all');
    const [viewMode, setViewMode] = useState('grid'); // 'grid' or 'list'
    const [sortBy, setSortBy] = useState('name'); // 'name', 'lastActivity', 'assessmentCount'
    const [sortOrder, setSortOrder] = useState('asc'); // 'asc' or 'desc'
    const [activeFilters, setActiveFilters] = useState([]);

    // Modal states
    const [showAddModal, setShowAddModal] = useState(false);
    const [showEditModal, setShowEditModal] = useState(false);
    const [showSubscriptionsModal, setShowSubscriptionsModal] = useState(false);
    const [showDetailsModal, setShowDetailsModal] = useState(false);
    const [showPreferencesModal, setShowPreferencesModal] = useState(false);
    const [selectedClient, setSelectedClient] = useState(null);
    const [hasLoaded, setHasLoaded] = useState(false);

    // Load clients on mount only
    useEffect(() => {
        if (!hasLoaded && !isLoading && !localLoading) {
            setLocalLoading(true);
            setHasLoaded(true);
            loadClients().finally(() => setLocalLoading(false));
        }
    }, [hasLoaded, isLoading, localLoading]);

    const handleRefresh = async () => {
        setLocalLoading(true);
        try {
            await loadClients();
        } finally {
            setLocalLoading(false);
        }
    };

    // Get unique industries for filter chips
    const getUniqueIndustries = () => {
        const industries = clients
            .map(client => client.Industry)
            .filter(Boolean)
            .filter((industry, index, arr) => arr.indexOf(industry) === index);
        return industries.sort();
    };

    // Filter and search clients
    const filteredClients = clients.filter(client => {
        const matchesSearch = !searchTerm ||
            client.Name.toLowerCase().includes(searchTerm.toLowerCase()) ||
            client.Industry?.toLowerCase().includes(searchTerm.toLowerCase()) ||
            client.ContactName?.toLowerCase().includes(searchTerm.toLowerCase()) ||
            client.ContactEmail?.toLowerCase().includes(searchTerm.toLowerCase()) ||
            client.Description?.toLowerCase().includes(searchTerm.toLowerCase());

        const matchesStatus = filterStatus === 'all' ||
            client.Status?.toLowerCase() === filterStatus.toLowerCase();

        const matchesActiveFilters = activeFilters.every(filter => {
            if (filter.type === 'industry') {
                return client.Industry === filter.value;
            }
            if (filter.type === 'hasPreferences') {
                return client.HasPreferences === true;
            }
            if (filter.type === 'hasAssessments') {
                return (client.AssessmentCount || 0) > 0;
            }
            return true;
        });

        return matchesSearch && matchesStatus && matchesActiveFilters;
    });

    // Sort clients
    const sortedClients = [...filteredClients].sort((a, b) => {
        let aValue, bValue;

        switch (sortBy) {
            case 'name':
                aValue = a.Name.toLowerCase();
                bValue = b.Name.toLowerCase();
                break;
            case 'assessmentCount':
                aValue = a.AssessmentCount || 0;
                bValue = b.AssessmentCount || 0;
                break;
            case 'lastActivity':
                // Placeholder - would use actual last activity date
                aValue = a.LastActivityDate || a.CreatedDate;
                bValue = b.LastActivityDate || b.CreatedDate;
                break;
            default:
                aValue = a.Name.toLowerCase();
                bValue = b.Name.toLowerCase();
        }

        if (sortOrder === 'asc') {
            return aValue > bValue ? 1 : -1;
        } else {
            return aValue < bValue ? 1 : -1;
        }
    });

    const addFilter = (type, value, label) => {
        const newFilter = { type, value, label };
        if (!activeFilters.some(f => f.type === type && f.value === value)) {
            setActiveFilters([...activeFilters, newFilter]);
        }
    };

    const removeFilter = (filterToRemove) => {
        setActiveFilters(activeFilters.filter(f =>
            !(f.type === filterToRemove.type && f.value === filterToRemove.value)
        ));
    };

    const clearAllFilters = () => {
        setActiveFilters([]);
        setFilterStatus('all');
        setSearchTerm('');
    };

    const toggleSort = (newSortBy) => {
        if (sortBy === newSortBy) {
            setSortOrder(sortOrder === 'asc' ? 'desc' : 'asc');
        } else {
            setSortBy(newSortBy);
            setSortOrder('asc');
        }
    };

    // Event handlers
    const handleAddClient = () => setShowAddModal(true);
    const handleEditClient = (client) => { setSelectedClient(client); setShowEditModal(true); };
    const handleManageSubscriptions = (client) => { setSelectedClient(client); setShowSubscriptionsModal(true); };
    const handleViewDetails = (client) => { setSelectedClient(client); setShowDetailsModal(true); };
    const handleManagePreferences = (client) => { setSelectedClient(client); setShowPreferencesModal(true); };
    const handleNewAssessment = (client) => {
        // Navigate to new assessment page with client pre-selected
        console.log('Navigate to new assessment for client:', client.Name);
    };

    const handleClientAdded = () => loadClients();
    const handleClientUpdated = () => loadClients();
    const handlePreferencesUpdated = () => {
        console.log('Client preferences updated successfully');
        loadClients(); // Refresh to update HasPreferences flag
    };

    const getClientStats = () => {
        return {
            totalClients: clients.length,
            activeClients: clients.filter(c => c.Status?.toLowerCase() === 'active').length,
            inactiveClients: clients.filter(c => c.Status?.toLowerCase() === 'inactive').length,
            filteredCount: sortedClients.length
        };
    };

    const stats = getClientStats();
    const uniqueIndustries = getUniqueIndustries();

    if ((isLoading || localLoading) && !hasLoaded) {
        return (
            <div className="space-y-6">
                <div className="flex items-center justify-between">
                    <div>
                        <h1 className="text-2xl font-bold text-white">My Clients</h1>
                        <p className="text-gray-400">Manage your MSP clients and their assessments</p>
                    </div>
                </div>

                <div className="bg-gray-900 border border-gray-800 rounded p-12 text-center">
                    <div className="w-8 h-8 border-2 border-yellow-600 border-t-transparent rounded-full animate-spin mx-auto mb-4"></div>
                    <p className="text-gray-400">Loading clients...</p>
                </div>
            </div>
        );
    }

    if (error) {
        return (
            <div className="space-y-6">
                <div className="flex items-center justify-between">
                    <div>
                        <h1 className="text-2xl font-bold text-white">My Clients</h1>
                        <p className="text-gray-400">Manage your MSP clients and their assessments</p>
                    </div>
                </div>

                <div className="bg-red-900/20 border border-red-800 rounded p-6">
                    <div className="flex items-center space-x-3">
                        <AlertCircle className="text-red-400" size={24} />
                        <div>
                            <h3 className="text-red-400 font-medium">Error Loading Clients</h3>
                            <p className="text-red-300 text-sm mt-1">{error}</p>
                        </div>
                    </div>
                    <button
                        onClick={handleRefresh}
                        className="mt-4 px-4 py-2 bg-red-600 hover:bg-red-700 text-white rounded text-sm flex items-center space-x-2"
                    >
                        <RefreshCw size={16} />
                        <span>Retry</span>
                    </button>
                </div>
            </div>
        );
    }

    return (
        <div className="space-y-6">
            {/* Header */}
            <div className="flex items-center justify-between">
                <div>
                    <h1 className="text-2xl font-bold text-white">My Clients</h1>
                    <p className="text-gray-400">
                        {stats.filteredCount === stats.totalClients
                            ? `Managing ${stats.totalClients} client${stats.totalClients !== 1 ? 's' : ''}`
                            : `Showing ${stats.filteredCount} of ${stats.totalClients} clients`
                        }
                    </p>
                </div>
                <div className="flex items-center space-x-3">
                    <button
                        onClick={handleRefresh}
                        disabled={localLoading}
                        className="px-4 py-2 bg-gray-700 hover:bg-gray-600 text-white rounded text-sm flex items-center space-x-2 disabled:opacity-50"
                    >
                        <RefreshCw size={16} className={localLoading ? 'animate-spin' : ''} />
                        <span>Refresh</span>
                    </button>
                    <button
                        onClick={handleAddClient}
                        className="px-4 py-2 bg-yellow-600 hover:bg-yellow-700 text-black rounded text-sm font-medium flex items-center space-x-2"
                    >
                        <Plus size={16} />
                        <span>Add Client</span>
                    </button>
                </div>
            </div>

            {/* Stats Cards */}
            <div className="grid grid-cols-1 md:grid-cols-4 gap-6">
                <div className="bg-gray-900 border border-gray-800 rounded p-6">
                    <div className="flex items-center justify-between">
                        <div>
                            <p className="text-sm text-gray-400">Total Clients</p>
                            <p className="text-2xl font-bold text-white">{stats.totalClients}</p>
                        </div>
                        <Building2 size={24} className="text-blue-400" />
                    </div>
                </div>

                <div className="bg-gray-900 border border-gray-800 rounded p-6">
                    <div className="flex items-center justify-between">
                        <div>
                            <p className="text-sm text-gray-400">Active</p>
                            <p className="text-2xl font-bold text-green-400">{stats.activeClients}</p>
                        </div>
                        <div className="w-6 h-6 bg-green-600 rounded-full"></div>
                    </div>
                </div>

                <div className="bg-gray-900 border border-gray-800 rounded p-6">
                    <div className="flex items-center justify-between">
                        <div>
                            <p className="text-sm text-gray-400">Inactive</p>
                            <p className="text-2xl font-bold text-gray-400">{stats.inactiveClients}</p>
                        </div>
                        <div className="w-6 h-6 bg-gray-600 rounded-full"></div>
                    </div>
                </div>

                <div className="bg-gray-900 border border-gray-800 rounded p-6">
                    <div className="flex items-center justify-between">
                        <div>
                            <p className="text-sm text-gray-400">Total Assessments</p>
                            <p className="text-2xl font-bold text-yellow-400">
                                {clients.reduce((sum, client) => sum + (client.AssessmentCount || 0), 0)}
                            </p>
                        </div>
                        <div className="w-6 h-6 bg-yellow-600 rounded-full"></div>
                    </div>
                </div>
            </div>

            {/* Quick Filter Chips */}
            <div className="flex flex-wrap items-center gap-2">
                <span className="text-sm text-gray-400">Quick filters:</span>

                {/* Industry filters */}
                {uniqueIndustries.slice(0, 5).map(industry => (
                    <button
                        key={industry}
                        onClick={() => addFilter('industry', industry, industry)}
                        className="px-3 py-1 bg-blue-900/30 hover:bg-blue-900/50 text-blue-300 text-sm rounded-full border border-blue-800 transition-colors"
                    >
                        {industry}
                    </button>
                ))}

                {/* Special filters */}
                <button
                    onClick={() => addFilter('hasPreferences', true, 'Has Preferences')}
                    className="px-3 py-1 bg-purple-900/30 hover:bg-purple-900/50 text-purple-300 text-sm rounded-full border border-purple-800 transition-colors"
                >
                    Has Preferences
                </button>

                <button
                    onClick={() => addFilter('hasAssessments', true, 'Has Assessments')}
                    className="px-3 py-1 bg-green-900/30 hover:bg-green-900/50 text-green-300 text-sm rounded-full border border-green-800 transition-colors"
                >
                    Has Assessments
                </button>
            </div>

            {/* Active Filters */}
            {activeFilters.length > 0 && (
                <div className="flex flex-wrap items-center gap-2">
                    <span className="text-sm text-gray-400">Active filters:</span>
                    {activeFilters.map((filter, index) => (
                        <div
                            key={index}
                            className="flex items-center space-x-2 px-3 py-1 bg-yellow-900/30 text-yellow-300 text-sm rounded-full border border-yellow-800"
                        >
                            <span>{filter.label}</span>
                            <button
                                onClick={() => removeFilter(filter)}
                                className="text-yellow-400 hover:text-yellow-200"
                            >
                                <X size={14} />
                            </button>
                        </div>
                    ))}
                    <button
                        onClick={clearAllFilters}
                        className="px-3 py-1 text-gray-400 hover:text-white text-sm transition-colors"
                    >
                        Clear all
                    </button>
                </div>
            )}

            {/* Search, Filter, and Sort Bar */}
            <div className="bg-gray-900 border border-gray-800 rounded p-4">
                <div className="flex items-center justify-between space-x-4">
                    <div className="flex items-center space-x-4 flex-1">
                        {/* Search */}
                        <div className="relative flex-1 max-w-md">
                            <Search size={18} className="absolute left-3 top-1/2 transform -translate-y-1/2 text-gray-400" />
                            <input
                                type="text"
                                placeholder="Search clients..."
                                value={searchTerm}
                                onChange={(e) => setSearchTerm(e.target.value)}
                                className="w-full pl-10 pr-4 py-2 bg-gray-700 border border-gray-600 rounded text-white placeholder-gray-400 focus:outline-none focus:ring-2 focus:ring-yellow-600"
                            />
                            {searchTerm && (
                                <button
                                    onClick={() => setSearchTerm('')}
                                    className="absolute right-3 top-1/2 transform -translate-y-1/2 text-gray-400 hover:text-white"
                                >
                                    <X size={16} />
                                </button>
                            )}
                        </div>

                        {/* Status Filter */}
                        <div className="flex items-center space-x-2">
                            <Filter size={18} className="text-gray-400" />
                            <select
                                value={filterStatus}
                                onChange={(e) => setFilterStatus(e.target.value)}
                                className="px-3 py-2 bg-gray-700 border border-gray-600 rounded text-white focus:outline-none focus:ring-2 focus:ring-yellow-600"
                            >
                                <option value="all">All Status</option>
                                <option value="active">Active</option>
                                <option value="inactive">Inactive</option>
                                <option value="pending">Pending</option>
                                <option value="suspended">Suspended</option>
                            </select>
                        </div>

                        {/* Sort Options */}
                        <div className="flex items-center space-x-2">
                            <SortAsc size={18} className="text-gray-400" />
                            <select
                                value={`${sortBy}-${sortOrder}`}
                                onChange={(e) => {
                                    const [newSortBy, newSortOrder] = e.target.value.split('-');
                                    setSortBy(newSortBy);
                                    setSortOrder(newSortOrder);
                                }}
                                className="px-3 py-2 bg-gray-700 border border-gray-600 rounded text-white focus:outline-none focus:ring-2 focus:ring-yellow-600"
                            >
                                <option value="name-asc">Name A-Z</option>
                                <option value="name-desc">Name Z-A</option>
                                <option value="assessmentCount-desc">Most Assessments</option>
                                <option value="assessmentCount-asc">Least Assessments</option>
                                <option value="lastActivity-desc">Recently Active</option>
                                <option value="lastActivity-asc">Least Active</option>
                            </select>
                        </div>
                    </div>

                    {/* View Mode Toggle */}
                    <div className="flex items-center space-x-2 bg-gray-700 rounded p-1">
                        <button
                            onClick={() => setViewMode('grid')}
                            className={`p-2 rounded transition-colors ${viewMode === 'grid'
                                ? 'bg-yellow-600 text-black'
                                : 'text-gray-400 hover:text-white'
                                }`}
                        >
                            <Grid size={16} />
                        </button>
                        <button
                            onClick={() => setViewMode('list')}
                            className={`p-2 rounded transition-colors ${viewMode === 'list'
                                ? 'bg-yellow-600 text-black'
                                : 'text-gray-400 hover:text-white'
                                }`}
                        >
                            <List size={16} />
                        </button>
                    </div>
                </div>
            </div>

            {/* Client List */}
            {sortedClients.length === 0 ? (
                <div className="bg-gray-900 border border-gray-800 rounded p-12 text-center">
                    <div className="max-w-md mx-auto">
                        {searchTerm || filterStatus !== 'all' || activeFilters.length > 0 ? (
                            <>
                                <div className="w-16 h-16 bg-gray-600 rounded-full flex items-center justify-center mx-auto mb-6">
                                    <Search size={32} className="text-gray-400" />
                                </div>
                                <h2 className="text-xl font-semibold text-white mb-3">No Clients Found</h2>
                                <p className="text-gray-400 mb-6">
                                    No clients match your current search criteria. Try adjusting your search terms or filters.
                                </p>
                                <div className="flex justify-center space-x-3">
                                    <button
                                        onClick={clearAllFilters}
                                        className="px-4 py-2 bg-gray-700 hover:bg-gray-600 text-white rounded"
                                    >
                                        Clear Filters
                                    </button>
                                    <button
                                        onClick={handleAddClient}
                                        className="px-4 py-2 bg-yellow-600 hover:bg-yellow-700 text-black rounded font-medium"
                                    >
                                        Add New Client
                                    </button>
                                </div>
                            </>
                        ) : (
                            <>
                                <div className="w-16 h-16 bg-blue-600 rounded-full flex items-center justify-center mx-auto mb-6">
                                    <Building2 size={32} className="text-white" />
                                </div>
                                <h2 className="text-xl font-semibold text-white mb-3">No Clients Yet</h2>
                                <p className="text-gray-400 mb-6">
                                    Start by adding your first client to organize their Azure subscriptions and assessments.
                                </p>
                                <button
                                    onClick={handleAddClient}
                                    className="px-6 py-3 bg-yellow-600 hover:bg-yellow-700 text-black rounded font-medium flex items-center space-x-2 mx-auto"
                                >
                                    <Plus size={20} />
                                    <span>Add Your First Client</span>
                                </button>
                            </>
                        )}
                    </div>
                </div>
            ) : (
                <div className={
                    viewMode === 'grid'
                        ? 'grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 gap-6'
                        : 'space-y-4'
                }>
                    {sortedClients.map((client) => (
                        <ClientCard
                            key={client.ClientId}
                            client={client}
                            viewMode={viewMode}
                            onEdit={handleEditClient}
                            onManageSubscriptions={handleManageSubscriptions}
                            onViewDetails={handleViewDetails}
                            onManagePreferences={handleManagePreferences}
                            onNewAssessment={handleNewAssessment}
                        />
                    ))}
                </div>
            )}

            {/* Results Summary */}
            {sortedClients.length > 0 && (searchTerm || filterStatus !== 'all' || activeFilters.length > 0) && (
                <div className="text-center text-sm text-gray-400">
                    Showing {sortedClients.length} of {clients.length} clients
                    {searchTerm && ` matching "${searchTerm}"`}
                    {filterStatus !== 'all' && ` with status "${filterStatus}"`}
                    {activeFilters.length > 0 && ` with ${activeFilters.length} filter${activeFilters.length > 1 ? 's' : ''}`}
                </div>
            )}

            {/* Modals */}
            <AddClientModal
                isOpen={showAddModal}
                onClose={() => setShowAddModal(false)}
                onClientAdded={handleClientAdded}
            />

            <EditClientModal
                isOpen={showEditModal}
                onClose={() => setShowEditModal(false)}
                client={selectedClient}
                onClientUpdated={handleClientUpdated}
            />

            <ManageSubscriptionsModal
                isOpen={showSubscriptionsModal}
                onClose={() => setShowSubscriptionsModal(false)}
                client={selectedClient}
            />

            <ClientDetailsModal
                isOpen={showDetailsModal}
                onClose={() => setShowDetailsModal(false)}
                client={selectedClient}
                onEdit={handleEditClient}
                onManageSubscriptions={handleManageSubscriptions}
            />

            <ClientPreferencesModal
                isOpen={showPreferencesModal}
                onClose={() => setShowPreferencesModal(false)}
                client={selectedClient}
                onPreferencesUpdated={handlePreferencesUpdated}
            />
        </div>
    );
};

export default MyClientsPage;