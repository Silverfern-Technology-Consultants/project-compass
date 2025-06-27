import React, { useState, useEffect } from 'react';
import { Building2, Plus, AlertCircle, RefreshCw, Search, Filter, Grid, List } from 'lucide-react';
import { useClient } from '../../contexts/ClientContext';
import { useAuth } from '../../contexts/AuthContext';
import ClientCard from '../ui/ClientCard';
import AddClientModal from '../modals/AddClientModal';
import EditClientModal from '../modals/EditClientModal';
import ManageSubscriptionsModal from '../modals/ManageSubscriptionsModal';
import ClientDetailsModal from '../modals/ClientDetailsModal';

const MyClientsPage = () => {
    const { clients, isLoading, error, loadClients } = useClient();
    const { user } = useAuth();
    const [localLoading, setLocalLoading] = useState(false);
    const [searchTerm, setSearchTerm] = useState('');
    const [filterStatus, setFilterStatus] = useState('all');
    const [viewMode, setViewMode] = useState('grid'); // 'grid' or 'list'
    const [showAddModal, setShowAddModal] = useState(false);
    const [showEditModal, setShowEditModal] = useState(false);
    const [showSubscriptionsModal, setShowSubscriptionsModal] = useState(false);
    const [showDetailsModal, setShowDetailsModal] = useState(false);
    const [selectedClient, setSelectedClient] = useState(null);

    // Load clients on mount if not already loaded
    useEffect(() => {
        if (clients.length === 0 && !isLoading && !localLoading) {
            setLocalLoading(true);
            loadClients().finally(() => setLocalLoading(false));
        }
    }, [clients.length, isLoading, loadClients, localLoading]);

    const handleRefresh = async () => {
        setLocalLoading(true);
        try {
            await loadClients();
        } finally {
            setLocalLoading(false);
        }
    };

    const handleAddClient = () => {
        setShowAddModal(true);
    };

    const handleEditClient = (client) => {
        setSelectedClient(client);
        setShowEditModal(true);
    };

    const handleManageSubscriptions = (client) => {
        setSelectedClient(client);
        setShowSubscriptionsModal(true);
    };

    const handleViewDetails = (client) => {
        setSelectedClient(client);
        setShowDetailsModal(true);
    };

    const handleClientAdded = () => {
        loadClients(); // Refresh the client list
    };

    const handleClientUpdated = () => {
        loadClients(); // Refresh the client list
    };

    // Filter and search clients
    const filteredClients = clients.filter(client => {
        const matchesSearch = !searchTerm ||
            client.Name.toLowerCase().includes(searchTerm.toLowerCase()) ||
            client.Industry?.toLowerCase().includes(searchTerm.toLowerCase()) ||
            client.ContactName?.toLowerCase().includes(searchTerm.toLowerCase()) ||
            client.ContactEmail?.toLowerCase().includes(searchTerm.toLowerCase());

        const matchesFilter = filterStatus === 'all' ||
            client.Status?.toLowerCase() === filterStatus.toLowerCase();

        return matchesSearch && matchesFilter;
    });

    const getClientStats = () => {
        return {
            totalClients: clients.length,
            activeClients: clients.filter(c => c.Status?.toLowerCase() === 'active').length,
            inactiveClients: clients.filter(c => c.Status?.toLowerCase() === 'inactive').length,
            filteredCount: filteredClients.length
        };
    };

    const stats = getClientStats();

    if (isLoading || localLoading) {
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

            {/* Search and Filter Bar */}
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
            {filteredClients.length === 0 ? (
                <div className="bg-gray-900 border border-gray-800 rounded p-12 text-center">
                    <div className="max-w-md mx-auto">
                        {searchTerm || filterStatus !== 'all' ? (
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
                                        onClick={() => { setSearchTerm(''); setFilterStatus('all'); }}
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
                    {filteredClients.map((client) => (
                        <ClientCard
                            key={client.ClientId}
                            client={client}
                            onEdit={handleEditClient}
                            onManageSubscriptions={handleManageSubscriptions}
                            onViewDetails={handleViewDetails}
                        />
                    ))}
                </div>
            )}

            {/* Results Summary */}
            {filteredClients.length > 0 && (searchTerm || filterStatus !== 'all') && (
                <div className="text-center text-sm text-gray-400">
                    Showing {filteredClients.length} of {clients.length} clients
                    {searchTerm && ` matching "${searchTerm}"`}
                    {filterStatus !== 'all' && ` with status "${filterStatus}"`}
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
        </div>
    );
};

export default MyClientsPage;