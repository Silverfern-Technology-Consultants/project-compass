import React, { useState } from 'react';
import { Building2, ChevronDown } from 'lucide-react';

const ClientSelector = ({ clients, onClientSelect }) => {
    const [showClientDropdown, setShowClientDropdown] = useState(false);

    const handleClientSelect = (client) => {
        onClientSelect(client);
        setShowClientDropdown(false);
    };

    return (
        <div className="space-y-6">
            <div>
                <h1 className="text-2xl font-bold text-white">Cost Analysis</h1>
                <p className="text-gray-400">Analyze Azure cost trends for your clients</p>
            </div>

            <div className="bg-gray-900 border border-gray-800 rounded p-8 text-center">
                <div className="max-w-md mx-auto">
                    <div className="w-16 h-16 bg-blue-600 rounded-full flex items-center justify-center mx-auto mb-6">
                        <Building2 size={32} className="text-white" />
                    </div>
                    <h2 className="text-xl font-semibold text-white mb-3">Select a Client</h2>
                    <p className="text-gray-400 mb-6">
                        Choose a client to analyze their Azure cost trends and spending patterns.
                    </p>
                    
                    <div className="relative">
                        <button
                            onClick={() => setShowClientDropdown(!showClientDropdown)}
                            className="w-full px-4 py-3 bg-gray-700 border border-gray-600 rounded-lg text-white text-left flex items-center justify-between hover:bg-gray-600 transition-colors"
                        >
                            <span className="flex items-center space-x-3">
                                <Building2 size={16} className="text-gray-400" />
                                <span>Select a client...</span>
                            </span>
                            <ChevronDown size={16} className={`text-gray-400 transition-transform ${
                                showClientDropdown ? 'rotate-180' : ''
                            }`} />
                        </button>

                        {showClientDropdown && (
                            <>
                                <div className="fixed inset-0 z-10" onClick={() => setShowClientDropdown(false)} />
                                <div className="absolute top-full left-0 right-0 mt-1 bg-gray-800 border border-gray-700 rounded-lg shadow-xl z-20 max-h-60 overflow-y-auto">
                                    {clients && clients.length > 0 ? (
                                        clients.map((client) => (
                                            <button
                                                key={client.ClientId}
                                                onClick={() => handleClientSelect(client)}
                                                className="w-full px-4 py-3 text-left hover:bg-gray-700 transition-colors border-b border-gray-700 last:border-b-0"
                                            >
                                                <div className="flex items-center space-x-3">
                                                    <div className="w-8 h-8 bg-blue-600 rounded-lg flex items-center justify-center flex-shrink-0">
                                                        <Building2 size={16} className="text-white" />
                                                    </div>
                                                    <div className="flex-1 min-w-0">
                                                        <p className="text-white font-medium truncate">{client.Name}</p>
                                                        {client.Industry && (
                                                            <p className="text-sm text-gray-400 truncate">{client.Industry}</p>
                                                        )}
                                                    </div>
                                                </div>
                                            </button>
                                        ))
                                    ) : (
                                        <div className="px-4 py-3 text-gray-400 text-center">
                                            No clients available
                                        </div>
                                    )}
                                </div>
                            </>
                        )}
                    </div>
                </div>
            </div>
        </div>
    );
};

export default ClientSelector;