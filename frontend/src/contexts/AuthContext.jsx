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

// Extract user data from JWT token
const extractUserFromJWT = (token) => {
    const decoded = decodeJWT(token);
    if (!decoded) return null;

    console.log('[AuthContext] JWT decoded payload:', decoded);

    // Extract names with multiple fallback options
    const extractFirstName = () => {
        if (decoded.given_name) return decoded.given_name;
        if (decoded.FirstName) return decoded.FirstName;
        if (decoded.name) {
            const parts = decoded.name.split(' ');
            return parts[0] || '';
        }
        return '';
    };

    const extractLastName = () => {
        if (decoded.family_name) return decoded.family_name;
        if (decoded.LastName) return decoded.LastName;
        if (decoded.name) {
            const parts = decoded.name.split(' ');
            return parts.slice(1).join(' ') || '';
        }
        return '';
    };

    const extractOrganizationName = () => {
        // Try multiple possible field names for organization
        return decoded.organization_name ||
            decoded.OrganizationName ||
            decoded.company_name ||
            decoded.CompanyName ||
            decoded.companyName ||
            '';
    };

    const user = {
        // Core identity fields
        customerId: decoded.nameid || decoded.sub || decoded.CustomerId,
        email: decoded.email || decoded.Email,

        // Name fields with multiple fallbacks
        firstName: extractFirstName(),
        lastName: extractLastName(),
        name: decoded.name || `${extractFirstName()} ${extractLastName()}`.trim(),

        // Role and permissions
        role: decoded.role || decoded.Role,

        // Organization context
        organizationId: decoded.organization_id || decoded.OrganizationId,
        organizationName: extractOrganizationName(),

        // Legacy field mappings for backward compatibility
        companyName: extractOrganizationName(),

        // Email verification
        emailVerified: decoded.email_verified === 'true' || decoded.email_verified === true,

        // Additional JWT fields that might be useful
        subscriptionStatus: decoded.subscription_status || decoded.SubscriptionStatus || 'Trial',

        // Keep original fields for debugging
        _jwtFields: {
            given_name: decoded.given_name,
            family_name: decoded.family_name,
            organization_name: decoded.organization_name,
            company_name: decoded.company_name,
            name: decoded.name,
            role: decoded.role,
            organization_id: decoded.organization_id
        }
    };

    console.log('[AuthContext] Extracted user data:', user);
    return user;
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
        case 'SET_VALIDATION_CHECKING':
            return { ...state, isValidatingUser: action.payload };
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
    pendingLogin: null,
    isValidatingUser: false
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

                    // Set token in API service first
                    AuthApi.setAuthToken(token);

                    // Extract user data from JWT token first
                    const userFromJWT = extractUserFromJWT(token);

                    if (userFromJWT) {
                        dispatch({ type: 'SET_TOKEN', payload: token });
                        dispatch({ type: 'SET_USER', payload: userFromJWT });
                    }

                    // Try to verify token and get additional user info from backend
                    try {
                        const userResponse = await AuthApi.getCurrentUser();

                        // Merge JWT data with backend response, preferring backend data (keeping PascalCase)
                        const mergedUser = {
                            ...userFromJWT,
                            ...userResponse,
                            // Ensure these critical fields come from JWT if missing from backend
                            CustomerId: userResponse.CustomerId || userFromJWT?.customerId,
                            Role: userResponse.Role || userFromJWT?.role,
                            OrganizationId: userResponse.OrganizationId || userFromJWT?.organizationId
                        };

                        dispatch({ type: 'SET_USER', payload: mergedUser });
                    } catch (backendError) {
                        console.warn('[Auth] Backend verification failed, using JWT data only:', backendError);
                        // Continue with JWT data if backend call fails
                        if (!userFromJWT) {
                            throw backendError;
                        }
                    }
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
                dispatch({ type: 'SET_LOADING', payload: false });
            }
        };

        initializeAuth();
    }, []);

    // NEW: User validation function
    const validateCurrentUser = async () => {
        if (!state.isAuthenticated || !state.token || state.isValidatingUser) {
            return true; // Skip validation if not authenticated or already validating
        }

        try {
            dispatch({ type: 'SET_VALIDATION_CHECKING', payload: true });


            const userResponse = await AuthApi.getCurrentUser();

            dispatch({ type: 'SET_VALIDATION_CHECKING', payload: false });
            return true;
        } catch (error) {
            console.warn('[Auth] User validation failed:', error);
            dispatch({ type: 'SET_VALIDATION_CHECKING', payload: false });

            // If user doesn't exist (404) or unauthorized (401), log them out
            if (error.response?.status === 404 || error.response?.status === 401) {
                logout();
                return false;
            }

            // For other errors (network issues, etc.), don't log out
            console.warn('[Auth] User validation failed due to network/server error, keeping user logged in');
            return true;
        }
    };

    // NEW: Auto-validate user on page navigation
    useEffect(() => {
        const handleVisibilityChange = () => {
            // When user returns to the tab, validate they still exist
            if (document.visibilityState === 'visible' && state.isAuthenticated) {
                validateCurrentUser();
            }
        };

        const handleFocus = () => {
            // When window gets focus, validate user
            if (state.isAuthenticated) {

                validateCurrentUser();
            }
        };

        document.addEventListener('visibilitychange', handleVisibilityChange);
        window.addEventListener('focus', handleFocus);

        return () => {
            document.removeEventListener('visibilitychange', handleVisibilityChange);
            window.removeEventListener('focus', handleFocus);
        };
    }, [state.isAuthenticated, state.token]);

    const login = async (email, password) => {

        dispatch({ type: 'SET_LOADING', payload: true });
        dispatch({ type: 'CLEAR_ERROR' });

        try {
            const response = await AuthApi.login(email, password);

            // Handle different response structures
            const customer = response.customer || response.user || response;
            const token = response.token;

            // If customer object is missing emailVerified field, try to get it from JWT token
            if (customer && customer.emailVerified === undefined && token) {
                const emailVerifiedFromToken = getEmailVerifiedFromToken(token);
                if (emailVerifiedFromToken !== null) {
                    customer.emailVerified = emailVerifiedFromToken;
                }
            }

            // Check email verification status
            if (customer && customer.emailVerified === false) {
                dispatch({ type: 'SET_LOADING', payload: false });
                return {
                    requiresEmailVerification: true,
                    customer,
                    email: customer.email || email
                };
            }

            // Handle MFA requirements
            if (response.requiresMfa) {
                // Store login credentials for MFA verification
                dispatch({ type: 'SET_PENDING_LOGIN', payload: { email, password } });
                dispatch({ type: 'SET_MFA_REQUIRED', payload: true });
                return { requiresMfa: true };
            }

            if (response.requiresMfaSetup) {
                // Store temporary token for MFA setup
                if (token) {
                    AuthApi.setAuthToken(token);
                    dispatch({ type: 'SET_TOKEN', payload: token });

                    // Extract user data from JWT
                    const userFromJWT = extractUserFromJWT(token);
                    const mergedUser = { ...userFromJWT, ...customer };
                    dispatch({ type: 'SET_USER', payload: mergedUser });
                }
                dispatch({ type: 'SET_MFA_SETUP_REQUIRED', payload: true });
                return { requiresMfaSetup: true, customer, token };
            }

            // Normal successful login
            if (!token || !customer) {
                console.error('[Auth] Missing token or customer in response:', { token: !!token, customer: !!customer });
                throw new Error('Invalid response from server - missing authentication data');
            }


            // Extract user data from JWT and merge with customer data
            const userFromJWT = extractUserFromJWT(token);
            const mergedUser = {
                ...userFromJWT,
                ...customer,
                // Ensure critical fields from JWT are preserved
                customerId: customer.customerId || userFromJWT?.customerId,
                role: customer.role || userFromJWT?.role,
                organizationId: customer.organizationId || userFromJWT?.organizationId
            };


            // Store token
            localStorage.setItem('compass_token', token);
            AuthApi.setAuthToken(token);

            // Update state
            dispatch({ type: 'LOGIN_SUCCESS', payload: { user: mergedUser, token } });

            return { success: true, customer: mergedUser, token };
        } catch (error) {
            console.error('[Auth] Login failed:', error);
            console.error('[Auth] Error response:', error.response?.data);

            const errorMessage = error.response?.data?.message || error.message || 'Login failed';
            dispatch({ type: 'SET_ERROR', payload: errorMessage });
            throw new Error(errorMessage);
        }
    };

    const verifyMfa = async (mfaCode, isBackupCode = false) => {
        dispatch({ type: 'SET_LOADING', payload: true });
        dispatch({ type: 'CLEAR_ERROR' });

        try {
            const { email, password } = state.pendingLogin;
            if (!email || !password) {
                throw new Error('No pending login found');
            }

            // Call login again with MFA code
            const response = await AuthApi.login(email, password, mfaCode, isBackupCode);
            const { success, token, customer } = response;

            if (!success || !token || !customer) {
                throw new Error('MFA verification failed');
            }

            // Extract user data from JWT and merge
            const userFromJWT = extractUserFromJWT(token);
            const mergedUser = { ...userFromJWT, ...customer };

            // Store token and complete login
            localStorage.setItem('compass_token', token);
            AuthApi.setAuthToken(token);

            // Update state
            dispatch({ type: 'LOGIN_SUCCESS', payload: { user: mergedUser, token } });

            return { success: true, customer: mergedUser, token };
        } catch (error) {
            console.error('[Auth] MFA verification failed:', error);
            const errorMessage = error.response?.data?.message || error.message || 'MFA verification failed';
            dispatch({ type: 'SET_ERROR', payload: errorMessage });
            throw new Error(errorMessage);
        }
    };

    const completeMfaVerification = (response) => {
        if (response.token) {
            localStorage.setItem('compass_token', response.token);
            AuthApi.setAuthToken(response.token);
            dispatch({ type: 'SET_TOKEN', payload: response.token });
        }
        if (response.customer || response.user) {
            const customer = response.customer || response.user;
            const userFromJWT = extractUserFromJWT(response.token);
            const mergedUser = { ...userFromJWT, ...customer };
            dispatch({ type: 'SET_USER', payload: mergedUser });
        }
        dispatch({ type: 'SET_MFA_REQUIRED', payload: false });
    };

    const completeMfaSetup = () => {
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
            // Keep PascalCase properties to match backend response
            return {
                IsMfaEnabled: response.IsEnabled,
                MfaSetupDate: response.SetupDate,
                LastMfaUsedDate: response.LastUsedDate,
                BackupCodesRemaining: response.BackupCodesRemaining,
                RequiresMfaSetup: response.RequiresSetup
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
        isValidatingUser: state.isValidatingUser, // NEW: Validation state

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
        validateCurrentUser, // NEW: Manual user validation

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