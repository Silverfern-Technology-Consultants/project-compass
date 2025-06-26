import React, { useState, useEffect } from 'react';
import { Building2, Clock, Users, Plus, AlertCircle, RefreshCw } from 'lucide-react';
import { useClient } from '../../contexts/ClientContext';
import { useAuth } from '../../contexts/AuthContext';

const MyClientsPage = () => {
    const { clients, isLoading, error, loadClients } = useClient();
    const { user } = useAuth();
    const [localLoading, setLocalLoading] = useState(false);

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

    const getClientStats = () => {
        return {
            totalClients: clients.length,
            activeClients: clients.filter(c => c.IsActive).length,
            inactiveClients: clients.filter(c => !c.IsActive).length
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
                    <p className="text-gray-400">Manage your MSP clients and their assessments</p>
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
                    <button className="px-4 py-2 bg-yellow-600 hover:bg-yellow-700 text-black rounded text-sm font-medium flex items-center space-x-2">
                        <Plus size={16} />
                        <span>Add Client</span>
                    </button>
                </div>
            </div>

            {/* Stats Cards */}
            <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
                <div className="bg-gray-900 border border-gray-800 rounded p-6">
                    <div className="flex items-center justify-between">
                        <div>
                            <p className="text-sm text-gray-400">Total Clients</p>
                            <p className="text-2xl font-bold text-white">{stats.totalClients}</p>
                            <p className="text-xs text-gray-500 mt-1">
                                {stats.activeClients} active, {stats.inactiveClients} inactive
                            </p>
                        </div>
                        <Building2 size={24} className="text-blue-400" />
                    </div>
                </div>

                <div className="bg-gray-900 border border-gray-800 rounded p-6">
                    <div className="flex items-center justify-between">
                        <div>
                            <p className="text-sm text-gray-400">Active Subscriptions</p>
                            <p className="text-2xl font-bold text-white">-</p>
                            <p className="text-xs text-gray-500 mt-1">Coming soon</p>
                        </div>
                        <Users size={24} className="text-green-400" />
                    </div>
                </div>

                <div className="bg-gray-900 border border-gray-800 rounded p-6">
                    <div className="flex items-center justify-between">
                        <div>
                            <p className="text-sm text-gray-400">Recent Assessments</p>
                            <p className="text-2xl font-bold text-white">-</p>
                            <p className="text-xs text-gray-500 mt-1">Coming soon</p>
                        </div>
                        <Clock size={24} className="text-yellow-400" />
                    </div>
                </div>
            </div>

            {/* Client List */}
            {clients.length === 0 ? (
                <div className="bg-gray-900 border border-gray-800 rounded p-12 text-center">
                    <div className="max-w-md mx-auto">
                        <div className="w-16 h-16 bg-blue-600 rounded-full flex items-center justify-center mx-auto mb-6">
                            <Building2 size={32} className="text-white" />
                        </div>

                        <h2 className="text-xl font-semibold text-white mb-3">No Clients Yet</h2>
                        <p className="text-gray-400 mb-6">
                            Start by adding your first client to organize their Azure subscriptions and assessments.
                        </p>

                        <button className="px-6 py-3 bg-yellow-600 hover:bg-yellow-700 text-black rounded font-medium flex items-center space-x-2 mx-auto">
                            <Plus size={20} />
                            <span>Add Your First Client</span>
                        </button>
                    </div>
                </div>
            ) : (
                <div className="bg-gray-900 border border-gray-800 rounded">
                    <div className="p-6 border-b border-gray-800">
                        <h2 className="text-lg font-semibold text-white">Client List</h2>
                        <p className="text-sm text-gray-400">Manage your client accounts and their configurations</p>
                    </div>

                    <div className="divide-y divide-gray-800">
                        {clients.map((client) => (
                            <div key={client.ClientId} className="p-6 hover:bg-gray-800/50 transition-colors">
                                <div className="flex items-center justify-between">
                                    <div className="flex items-center space-x-4">
                                        <div className="w-12 h-12 bg-blue-600 rounded-lg flex items-center justify-center">
                                            <Building2 size={24} className="text-white" />
                                        </div>
                                        <div>
                                            <h3 className="text-white font-medium">{client.Name}</h3>
                                            {client.Description && (
                                                <p className="text-sm text-gray-400">{client.Description}</p>
                                            )}
                                            <div className="flex items-center space-x-4 mt-1">
                                                {client.ContactEmail && (
                                                    <span className="text-xs text-gray-500">{client.ContactEmail}</span>
                                                )}
                                                {client.Industry && (
                                                    <span className="text-xs text-gray-500">{client.Industry}</span>
                                                )}
                                            </div>
                                        </div>
                                    </div>
                                    <div className="flex items-center space-x-3">
                                        <div className={`px-2 py-1 rounded text-xs font-medium ${client.IsActive
                                                ? 'bg-green-900/30 text-green-400 border border-green-800'
                                                : 'bg-gray-900/30 text-gray-400 border border-gray-700'
                                            }`}>
                                            {client.IsActive ? 'Active' : 'Inactive'}
                                        </div>
                                        <button className="text-gray-400 hover:text-white p-2 rounded hover:bg-gray-700">
                                            <Users size={16} />
                                        </button>
                                    </div>
                                </div>
                            </div>
                        ))}
                    </div>
                </div>
            )}
        </div>
    );
};

export default MyClientsPage;