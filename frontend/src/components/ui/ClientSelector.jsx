import React from 'react';
import { ChevronDown, Building2, Home } from 'lucide-react';
import { useClient } from '../../contexts/ClientContext';
import { useAuth } from '../../contexts/AuthContext';

const ClientSelector = ({ dropdownOpen, setDropdownOpen, onOutsideClick }) => {
    const {
        clients,
        selectedClient,
        selectClient,
        selectInternalClient,
        clearSelection,
        getClientDisplayName,
        isInternalSelected
    } = useClient();
    const { user } = useAuth();

    const handleClientSelect = (client) => {
        if (client === 'internal') {
            selectInternalClient();
        } else if (client === null) {
            clearSelection();
        } else {
            selectClient(client);
        }
        setDropdownOpen(false);
    };

    const getDisplayIcon = () => {
        if (!selectedClient) return <Building2 size={16} className="text-gray-400" />;
        if (isInternalSelected()) return <Home size={16} className="text-yellow-600" />;
        return <Building2 size={16} className="text-blue-400" />;
    };

    const getDisplayText = () => {
        const displayName = getClientDisplayName();
        // Truncate long names for display
        return displayName.length > 25 ? `${displayName.substring(0, 22)}...` : displayName;
    };

    const getDisplayTextColor = () => {
        if (!selectedClient) return 'text-gray-400';
        if (isInternalSelected()) return 'text-yellow-600';
        return 'text-blue-400';
    };

    return (
        <div className="relative">
            <button
                onClick={() => setDropdownOpen(!dropdownOpen)}
                className="flex items-center space-x-2 px-3 py-2 rounded border border-gray-700 hover:border-gray-600 bg-gray-800 hover:bg-gray-750 transition-colors min-w-[200px]"
            >
                {getDisplayIcon()}
                <span className={`text-sm font-medium ${getDisplayTextColor()} flex-1 text-left`}>
                    {getDisplayText()}
                </span>
                <ChevronDown
                    size={16}
                    className={`text-gray-400 transition-transform ${dropdownOpen ? 'rotate-180' : ''}`}
                />
            </button>

            {dropdownOpen && (
                <>
                    {/* Backdrop - Uses shared onOutsideClick */}
                    <div
                        className="fixed inset-0 z-10"
                        onClick={onOutsideClick}
                    />

                    {/* Dropdown Content */}
                    <div className="absolute left-0 mt-2 w-64 bg-gray-800 border border-gray-700 rounded-md shadow-lg z-20 max-h-80 overflow-y-auto">
                        <div className="py-1">
                            {/* No Client Selected Option */}
                            <button
                                onClick={() => handleClientSelect(null)}
                                className={`w-full text-left px-4 py-2 text-sm hover:bg-gray-700 flex items-center space-x-2 ${!selectedClient ? 'bg-gray-700 text-white' : 'text-gray-300'
                                    }`}
                            >
                                <Building2 size={16} className="text-gray-400" />
                                <span>No Client Selected</span>
                            </button>

                            {/* Internal/Company Option */}
                            <button
                                onClick={() => handleClientSelect('internal')}
                                className={`w-full text-left px-4 py-2 text-sm hover:bg-gray-700 flex items-center space-x-2 ${isInternalSelected() ? 'bg-gray-700 text-yellow-600' : 'text-gray-300'
                                    }`}
                            >
                                <Home size={16} className="text-yellow-600" />
                                <div className="flex flex-col">
                                    <span>{user?.CompanyName || 'Internal'}</span>
                                    <span className="text-xs text-gray-500">Internal Infrastructure</span>
                                </div>
                            </button>

                            {/* Separator */}
                            {clients.length > 0 && (
                                <div className="border-t border-gray-700 my-1" />
                            )}

                            {/* Client List */}
                            {clients.length === 0 ? (
                                <div className="px-4 py-3 text-sm text-gray-500 text-center">
                                    No clients configured
                                </div>
                            ) : (
                                clients.map((client) => (
                                    <button
                                        key={client.ClientId}
                                        onClick={() => handleClientSelect(client)}
                                        className={`w-full text-left px-4 py-2 text-sm hover:bg-gray-700 flex items-center space-x-2 ${selectedClient?.ClientId === client.ClientId && !isInternalSelected()
                                            ? 'bg-gray-700 text-blue-400'
                                            : 'text-gray-300'
                                            }`}
                                    >
                                        <Building2 size={16} className="text-blue-400" />
                                        <div className="flex flex-col min-w-0 flex-1">
                                            <span className="truncate">{client.Name}</span>
                                            {client.Description && (
                                                <span className="text-xs text-gray-500 truncate">
                                                    {client.Description}
                                                </span>
                                            )}
                                        </div>
                                    </button>
                                ))
                            )}
                        </div>
                    </div>
                </>
            )}
        </div>
    );
};

export default ClientSelector;