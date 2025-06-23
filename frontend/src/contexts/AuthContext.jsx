import React, { createContext, useContext, useReducer, useEffect } from 'react';
import { AuthApi, MfaApi } from '../services/apiService';

// Simple JWT decoder function (inline to avoid extra file)
const decodeJWT = (token) => {
    try {
        if (!token) return null;
        const parts = token.split('.');
        if (parts.length !== 3) return null;
        const payload = parts[1];
        const paddedPayload = payload + '='.repeat((4 - payload.length % 4) % 4);
        const decodedPayload = atob(paddedPayload);
        return JSON.parse(decodedPayload);
    } catch (error) {
        console.error('Error decoding JWT token:', error);
        return null;
    }
};

const getEmailVerifiedFromToken = (token) => {
    try {
        const decoded = decodeJWT(token);
        if (!decoded) return null;
        const emailVerified = decoded.email_verified;
        if (emailVerified === 'true' || emailVerified === true) return true;
        if (emailVerified === 'false' || emailVerified === false) return false;
        return null;
    } catch (error) {
        console.error('Error extracting email verification from token:', error);
        return null;
    }
};

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
        case 'SET_MFA_REQUIRED':
            return { ...state, mfaRequired: action.payload, isLoading: false };
        case 'SET_MFA_SETUP_REQUIRED':
            return { ...state, mfaSetupRequired: action.payload, isLoading: false };
        case 'SET_PENDING_LOGIN':
            return { ...state, pendingLogin: action.payload };
        case 'LOGIN_SUCCESS':
            return {
                ...state,
                user: action.payload.user,
                token: action.payload.token,
                isAuthenticated: true,
                mfaRequired: false,
                mfaSetupRequired: false,
                pendingLogin: null,
                error: null,
                isLoading: false
            };
        case 'LOGOUT':
            return {
                user: null,
                token: null,
                isAuthenticated: false,
                isLoading: false,
                error: null,
                mfaRequired: false,
                mfaSetupRequired: false,
                pendingLogin: null
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
    error: null,
    mfaRequired: false,
    mfaSetupRequired: false,
    pendingLogin: null
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
                    console.log('[Auth] User subscription status:', userResponse.subscriptionStatus);
                    console.log('[Auth] User trial end date:', userResponse.trialEndDate);

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

            // Handle different response structures
            const customer = response.customer || response.user || response;
            const token = response.token;

            // If customer object is missing emailVerified field, try to get it from JWT token
            if (customer && customer.emailVerified === undefined && token) {
                const emailVerifiedFromToken = getEmailVerifiedFromToken(token);
                if (emailVerifiedFromToken !== null) {
                    customer.emailVerified = emailVerifiedFromToken;
                    console.log('[Auth] Email verified status from JWT token:', customer.emailVerified);
                }
            }

            // Check email verification status
            if (customer && customer.emailVerified === false) {
                console.log('[Auth] Email verification required for user');
                dispatch({ type: 'SET_LOADING', payload: false });
                return {
                    requiresEmailVerification: true,
                    customer,
                    email: customer.email || email
                };
            }

            // Handle MFA requirements
            if (response.requiresMfa) {
                console.log('[Auth] MFA verification required');
                // Store login credentials for MFA verification
                dispatch({ type: 'SET_PENDING_LOGIN', payload: { email, password } });
                dispatch({ type: 'SET_MFA_REQUIRED', payload: true });
                return { requiresMfa: true };
            }

            if (response.requiresMfaSetup) {
                console.log('[Auth] MFA setup required');
                // Store temporary token for MFA setup
                if (token) {
                    AuthApi.setAuthToken(token);
                    dispatch({ type: 'SET_TOKEN', payload: token });
                }
                if (customer) {
                    dispatch({ type: 'SET_USER', payload: customer });
                }
                dispatch({ type: 'SET_MFA_SETUP_REQUIRED', payload: true });
                return { requiresMfaSetup: true, customer, token };
            }

            // Normal successful login
            if (!token || !customer) {
                console.error('[Auth] Missing token or customer in response:', { token: !!token, customer: !!customer });
                throw new Error('Invalid response from server - missing authentication data');
            }

            console.log('[Auth] Login successful, storing token and user data');
            console.log('[Auth] Customer email verified status:', customer.emailVerified);

            // Store token
            localStorage.setItem('compass_token', token);
            AuthApi.setAuthToken(token);

            // Update state
            dispatch({ type: 'LOGIN_SUCCESS', payload: { user: customer, token } });

            console.log('[Auth] Login completed successfully');
            return { success: true, customer, token };
        } catch (error) {
            console.error('[Auth] Login failed:', error);
            console.error('[Auth] Error response:', error.response?.data);

            const errorMessage = error.response?.data?.message || error.message || 'Login failed';
            dispatch({ type: 'SET_ERROR', payload: errorMessage });
            throw new Error(errorMessage);
        }
    };

    const verifyMfa = async (mfaCode, isBackupCode = false) => {
        console.log('[Auth] Verifying MFA code');
        dispatch({ type: 'SET_LOADING', payload: true });
        dispatch({ type: 'CLEAR_ERROR' });

        try {
            const { email, password } = state.pendingLogin;
            if (!email || !password) {
                throw new Error('No pending login found');
            }

            // Call login again with MFA code
            const response = await AuthApi.login(email, password, mfaCode, isBackupCode);
            console.log('[Auth] MFA verification response:', response);

            const { success, token, customer } = response;

            if (!success || !token || !customer) {
                throw new Error('MFA verification failed');
            }

            // Store token and complete login
            localStorage.setItem('compass_token', token);
            AuthApi.setAuthToken(token);

            // Update state
            dispatch({ type: 'LOGIN_SUCCESS', payload: { user: customer, token } });

            console.log('[Auth] MFA verification completed successfully');
            return { success: true, customer, token };
        } catch (error) {
            console.error('[Auth] MFA verification failed:', error);
            const errorMessage = error.response?.data?.message || error.message || 'MFA verification failed';
            dispatch({ type: 'SET_ERROR', payload: errorMessage });
            throw new Error(errorMessage);
        }
    };

    const completeMfaVerification = (response) => {
        console.log('[Auth] Completing MFA verification');
        if (response.token) {
            localStorage.setItem('compass_token', response.token);
            AuthApi.setAuthToken(response.token);
            dispatch({ type: 'SET_TOKEN', payload: response.token });
        }
        if (response.customer || response.user) {
            dispatch({ type: 'SET_USER', payload: response.customer || response.user });
        }
        dispatch({ type: 'SET_MFA_REQUIRED', payload: false });
    };

    const completeMfaSetup = () => {
        console.log('[Auth] Completing MFA setup');
        dispatch({ type: 'SET_MFA_SETUP_REQUIRED', payload: false });
        // User is already logged in with the temp token
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

    // MFA methods
    const getMfaStatus = async () => {
        try {
            const response = await MfaApi.getMfaStatus();
            // Map backend property names to frontend expectations
            return {
                isMfaEnabled: response.isEnabled,
                mfaSetupDate: response.setupDate,
                lastMfaUsedDate: response.lastUsedDate,
                backupCodesRemaining: response.backupCodesRemaining,
                requiresMfaSetup: response.requiresSetup
            };
        } catch (error) {
            throw error;
        }
    };

    const setupMfa = async () => {
        try {
            const response = await MfaApi.setupMfa();
            return response;
        } catch (error) {
            throw error;
        }
    };

    const verifyMfaSetup = async (totpCode) => {
        try {
            const response = await MfaApi.verifyMfaSetup(totpCode);
            return response;
        } catch (error) {
            throw error;
        }
    };

    const disableMfa = async (password, mfaCode) => {
        try {
            const response = await MfaApi.disableMfa(password, mfaCode);
            return response;
        } catch (error) {
            throw error;
        }
    };

    const regenerateBackupCodes = async (mfaCode) => {
        try {
            const response = await MfaApi.regenerateBackupCodes(mfaCode);
            return response;
        } catch (error) {
            throw error;
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
        mfaRequired: state.mfaRequired,
        mfaSetupRequired: state.mfaSetupRequired,

        // Actions
        login,
        register,
        logout,
        verifyEmail,
        resendVerification,
        checkEmailAvailability,
        clearError,
        completeMfaVerification,
        completeMfaSetup,
        verifyMfa, // NEW: MFA verification method

        // MFA methods
        getMfaStatus,
        setupMfa,
        verifyMfaSetup,
        disableMfa,
        regenerateBackupCodes
    };

    return (
        <AuthContext.Provider value={value}>
            {children}
        </AuthContext.Provider>
    );
};