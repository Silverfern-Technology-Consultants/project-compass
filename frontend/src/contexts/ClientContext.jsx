import React, { createContext, useContext, useReducer, useEffect } from 'react';
import { useAuth } from './AuthContext';

// Client context reducer
const clientReducer = (state, action) => {
    switch (action.type) {
        case 'SET_CLIENTS':
            return { ...state, clients: action.payload, isLoading: false };
        case 'SET_SELECTED_CLIENT':
            return { ...state, selectedClient: action.payload };
        case 'SET_LOADING':
            return { ...state, isLoading: action.payload };
        case 'SET_ERROR':
            return { ...state, error: action.payload, isLoading: false };
        case 'CLEAR_ERROR':
            return { ...state, error: null };
        case 'ADD_CLIENT':
            return { ...state, clients: [...state.clients, action.payload] };
        case 'UPDATE_CLIENT':
            return {
                ...state,
                clients: state.clients.map(client =>
                    client.ClientId === action.payload.ClientId ? action.payload : client
                )
            };
        case 'REMOVE_CLIENT':
            return {
                ...state,
                clients: state.clients.filter(client => client.ClientId !== action.payload),
                selectedClient: state.selectedClient?.ClientId === action.payload ? null : state.selectedClient
            };
        default:
            return state;
    }
};

const initialState = {
    clients: [],
    selectedClient: null,
    isLoading: false,
    error: null
};

const ClientContext = createContext();

export const useClient = () => {
    const context = useContext(ClientContext);
    if (!context) {
        throw new Error('useClient must be used within a ClientProvider');
    }
    return context;
};

export const ClientProvider = ({ children }) => {
    const [state, dispatch] = useReducer(clientReducer, initialState);
    const { user, isAuthenticated } = useAuth();

    // Load clients when user is authenticated
    useEffect(() => {
        if (isAuthenticated && user?.OrganizationId) {
            loadClients();
        }
    }, [isAuthenticated, user?.OrganizationId]);

    // Load selected client from localStorage on mount
    useEffect(() => {
        if (state.clients.length > 0) {
            const savedClientId = localStorage.getItem('compass_selected_client');
            if (savedClientId) {
                const client = state.clients.find(c => c.ClientId === savedClientId);
                if (client) {
                    dispatch({ type: 'SET_SELECTED_CLIENT', payload: client });
                }
            }
        }
    }, [state.clients]);

    const loadClients = async () => {
        try {
            dispatch({ type: 'SET_LOADING', payload: true });

            // Import dynamically to avoid circular dependency
            const { clientApi } = await import('../services/apiService');
            const clients = await clientApi.getClients();

            dispatch({ type: 'SET_CLIENTS', payload: clients });
        } catch (error) {
            console.error('[ClientContext] Error loading clients:', error);
            dispatch({ type: 'SET_ERROR', payload: error.message });
        }
    };

    const selectClient = (client) => {
        dispatch({ type: 'SET_SELECTED_CLIENT', payload: client });

        // Persist selection
        if (client) {
            localStorage.setItem('compass_selected_client', client.ClientId);
        } else {
            localStorage.removeItem('compass_selected_client');
        }
    };

    const selectInternalClient = () => {
        const internalClient = {
            ClientId: 'internal',
            Name: user?.CompanyName || 'Internal',
            isInternal: true
        };
        selectClient(internalClient);
    };

    const clearSelection = () => {
        selectClient(null);
    };

    const addClient = (client) => {
        dispatch({ type: 'ADD_CLIENT', payload: client });
    };

    const updateClient = (client) => {
        dispatch({ type: 'UPDATE_CLIENT', payload: client });
    };

    const removeClient = (clientId) => {
        dispatch({ type: 'REMOVE_CLIENT', payload: clientId });
    };

    const getClientDisplayName = () => {
        if (!state.selectedClient) return 'No Client Selected';
        if (state.selectedClient.isInternal) return `${state.selectedClient.Name} - Internal`;
        return state.selectedClient.Name;
    };

    const isInternalSelected = () => {
        return state.selectedClient?.isInternal === true;
    };

    const value = {
        // State
        clients: state.clients,
        selectedClient: state.selectedClient,
        isLoading: state.isLoading,
        error: state.error,

        // Actions
        loadClients,
        selectClient,
        selectInternalClient,
        clearSelection,
        addClient,
        updateClient,
        removeClient,

        // Utilities
        getClientDisplayName,
        isInternalSelected
    };

    return (
        <ClientContext.Provider value={value}>
            {children}
        </ClientContext.Provider>
    );
};