import React, { createContext, useContext, useReducer, useEffect } from 'react';
import { AuthApi } from '../services/apiService';

// Auth state management
const authReducer = (state, action) => {
    switch (action.type) {
        case 'SET_LOADING':
            return { ...state, isLoading: action.payload };
        case 'SET_USER':
            return {
                ...state,
                user: action.payload,
                isAuthenticated: !!action.payload,
                isLoading: false
            };
        case 'SET_TOKEN':
            return { ...state, token: action.payload };
        case 'SET_ERROR':
            return { ...state, error: action.payload, isLoading: false };
        case 'LOGOUT':
            return {
                user: null,
                token: null,
                isAuthenticated: false,
                isLoading: false,
                error: null
            };
        case 'CLEAR_ERROR':
            return { ...state, error: null };
        default:
            return state;
    }
};

const initialState = {
    user: null,
    token: localStorage.getItem('compass_token'),
    isAuthenticated: false,
    isLoading: true,
    error: null
};

const AuthContext = createContext();

export const useAuth = () => {
    const context = useContext(AuthContext);
    if (!context) {
        throw new Error('useAuth must be used within an AuthProvider');
    }
    return context;
};

export const AuthProvider = ({ children }) => {
    const [state, dispatch] = useReducer(authReducer, initialState);

    // Initialize auth state on app load
    useEffect(() => {
        const initializeAuth = async () => {
            const token = localStorage.getItem('compass_token');

            if (token) {
                try {
                    console.log('[Auth] Initializing with stored token');

                    // Set token in API service first
                    AuthApi.setAuthToken(token);

                    // Verify token and get user info
                    console.log('[Auth] Verifying token with backend');
                    const userResponse = await AuthApi.getCurrentUser();

                    console.log('[Auth] Token verified successfully:', userResponse);

                    dispatch({ type: 'SET_TOKEN', payload: token });
                    dispatch({ type: 'SET_USER', payload: userResponse });
                } catch (error) {
                    console.error('[Auth] Token verification failed:', error);
                    console.error('[Auth] Error details:', {
                        status: error.response?.status,
                        message: error.response?.data?.message,
                        data: error.response?.data
                    });

                    // Invalid token, clear it and stay logged out
                    localStorage.removeItem('compass_token');
                    AuthApi.setAuthToken(null);
                    dispatch({ type: 'LOGOUT' });
                }
            } else {
                console.log('[Auth] No stored token found');
                dispatch({ type: 'SET_LOADING', payload: false });
            }
        };

        initializeAuth();
    }, []);

    const login = async (email, password) => {
        console.log('[Auth] Starting login process for:', email);

        dispatch({ type: 'SET_LOADING', payload: true });
        dispatch({ type: 'CLEAR_ERROR' });

        try {
            const response = await AuthApi.login(email, password);
            console.log('[Auth] Login API response:', response);

            const { token, customer } = response;

            if (!token || !customer) {
                throw new Error('Invalid response from server');
            }

            console.log('[Auth] Login successful, storing token and user data');

            // Store token
            localStorage.setItem('compass_token', token);
            AuthApi.setAuthToken(token);

            // Update state
            dispatch({ type: 'SET_TOKEN', payload: token });
            dispatch({ type: 'SET_USER', payload: customer });

            console.log('[Auth] Login completed successfully');
            return customer;
        } catch (error) {
            console.error('[Auth] Login failed:', error);

            const errorMessage = error.response?.data?.message || error.message || 'Login failed';
            dispatch({ type: 'SET_ERROR', payload: errorMessage });
            throw new Error(errorMessage);
        }
    };

    const register = async (userData) => {
        dispatch({ type: 'SET_LOADING', payload: true });
        dispatch({ type: 'CLEAR_ERROR' });

        try {
            const response = await AuthApi.register(userData);

            // Registration successful, but may require email verification
            dispatch({ type: 'SET_LOADING', payload: false });

            return response;
        } catch (error) {
            const errorMessage = error.response?.data?.message || 'Registration failed';
            dispatch({ type: 'SET_ERROR', payload: errorMessage });
            throw new Error(errorMessage);
        }
    };

    const logout = () => {
        console.log('[Auth] Logging out user');
        localStorage.removeItem('compass_token');
        AuthApi.setAuthToken(null);
        dispatch({ type: 'LOGOUT' });
    };

    const verifyEmail = async (token) => {
        try {
            const response = await AuthApi.verifyEmail(token);
            return response;
        } catch (error) {
            const errorMessage = error.response?.data?.message || 'Email verification failed';
            throw new Error(errorMessage);
        }
    };

    const resendVerification = async (email) => {
        try {
            const response = await AuthApi.resendVerification(email);
            return response;
        } catch (error) {
            const errorMessage = error.response?.data?.message || 'Failed to resend verification email';
            throw new Error(errorMessage);
        }
    };

    const checkEmailAvailability = async (email) => {
        try {
            const response = await AuthApi.checkEmailAvailability(email);
            return response.available;
        } catch (error) {
            console.error('Email check failed:', error);
            return false;
        }
    };

    const clearError = () => {
        dispatch({ type: 'CLEAR_ERROR' });
    };

    const value = {
        // State
        user: state.user,
        token: state.token,
        isAuthenticated: state.isAuthenticated,
        isLoading: state.isLoading,
        error: state.error,

        // Actions
        login,
        register,
        logout,
        verifyEmail,
        resendVerification,
        checkEmailAvailability,
        clearError
    };

    return (
        <AuthContext.Provider value={value}>
            {children}
        </AuthContext.Provider>
    );
};