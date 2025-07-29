import React, { useState, useEffect } from 'react';
import { useNavigate, Link } from 'react-router-dom';
import { useAuth } from '../../contexts/AuthContext';
import logo from '../../assets/images/256-256 Logo Transparent.png';

const LoginPage = () => {
    const [email, setEmail] = useState('');
    const [password, setPassword] = useState('');
    const [isLoading, setIsLoading] = useState(false);
    const [error, setError] = useState('');
    const { login, isAuthenticated, mfaRequired, mfaSetupRequired } = useAuth();
    const navigate = useNavigate();

    // Watch for authentication changes and navigate when complete
    useEffect(() => {
        if (isAuthenticated && !mfaRequired && !mfaSetupRequired) {
            navigate('/app/dashboard');
        }
    }, [isAuthenticated, mfaRequired, mfaSetupRequired, navigate]);

    const handleSubmit = async (e) => {
        e.preventDefault();
        setIsLoading(true);
        setError('');

        try {
            const result = await login(email, password);

            // Handle email verification requirement
            if (result.requiresEmailVerification || (result.customer && !result.customer.emailVerified)) {
                navigate('/verify-email', { state: { email, fromLogin: true } });
                setIsLoading(false);
                return;
            }

            // Handle MFA requirements - modals will be shown automatically by App.jsx
            if (result.requiresMfa) {
                // MFA modal will show, don't navigate yet - useEffect will handle navigation when auth completes
                setIsLoading(false);
                return;
            }

            if (result.requiresMfaSetup) {
                // MFA setup modal will show, don't navigate yet - useEffect will handle navigation when setup completes
                setIsLoading(false);
                return;
            }

            // Normal login without MFA - check if user is properly authenticated
            if (result.success || result.token || result.customer) {
                navigate('/app/dashboard');
            } else {
                console.warn('[LoginPage] Unexpected login result format:', result);
                setError('Login completed but response format was unexpected. Please try again.');
            }
        } catch (error) {
            console.error('[LoginPage] Login error:', error);
            setError(error.message || 'Login failed. Please try again.');
        } finally {
            // Only set loading false if not waiting for MFA
            setIsLoading(false);
        }
    };

    return (
        <div className={`min-h-screen bg-gray-950 flex flex-col justify-center py-12 sm:px-6 lg:px-8 relative overflow-hidden ${mfaRequired || mfaSetupRequired ? 'opacity-30 pointer-events-none' : ''}`}>
            {/* Enhanced background with vignette and flowing gradients */}
            <div className="absolute inset-0">
                {/* Flowing gradients */}
                <div className="absolute inset-0 bg-gradient-to-tl from-yellow-900/5 via-transparent to-yellow-800/3 animate-gradient-flow-1"></div>
                <div className="absolute inset-0 bg-gradient-to-br from-transparent via-yellow-700/3 to-transparent animate-gradient-flow-2"></div>
            </div>
            
            {/* Faint hexagonal mesh */}
            <div className="absolute inset-0 opacity-15">
                <div className="absolute inset-0 hexagonal-mesh"></div>
            </div>
            
            {/* Dynamic gradient orbs */}
            <div className="absolute top-1/4 left-1/4 w-96 h-96 bg-gradient-to-r from-yellow-400/10 to-yellow-600/5 rounded-full blur-3xl animate-float-slow"></div>
            <div className="absolute bottom-1/4 right-1/4 w-80 h-80 bg-gradient-to-r from-yellow-600/8 to-yellow-400/3 rounded-full blur-3xl animate-float-reverse"></div>
            <div className="absolute top-1/2 left-1/2 transform -translate-x-1/2 -translate-y-1/2 w-64 h-64 bg-gradient-to-r from-yellow-500/6 to-yellow-300/2 rounded-full blur-2xl animate-pulse-gentle"></div>
            
            {/* Animated particles */}
            <div className="absolute inset-0 overflow-hidden">
                <div className="absolute top-20 left-10 w-1 h-1 bg-yellow-400/30 rounded-full animate-twinkle-1"></div>
                <div className="absolute top-40 right-20 w-1 h-1 bg-yellow-300/40 rounded-full animate-twinkle-2"></div>
                <div className="absolute bottom-32 left-1/4 w-1 h-1 bg-yellow-500/35 rounded-full animate-twinkle-3"></div>
                <div className="absolute top-1/3 right-1/3 w-1 h-1 bg-yellow-400/25 rounded-full animate-twinkle-1"></div>
                <div className="absolute bottom-1/4 right-10 w-1 h-1 bg-yellow-300/30 rounded-full animate-twinkle-2"></div>
                <div className="absolute top-1/6 left-1/3 w-1 h-1 bg-yellow-500/20 rounded-full animate-twinkle-3"></div>
            </div>
            
            {/* Aurora-like light rays */}
            <div className="absolute inset-0 overflow-hidden">
                <div className="absolute -top-40 -left-40 w-80 h-2 bg-gradient-to-r from-transparent via-yellow-400/10 to-transparent rotate-45 animate-aurora-1"></div>
                <div className="absolute -bottom-40 -right-40 w-80 h-2 bg-gradient-to-r from-transparent via-yellow-300/8 to-transparent -rotate-45 animate-aurora-2"></div>
                <div className="absolute top-1/2 -left-20 w-60 h-1 bg-gradient-to-r from-transparent via-yellow-500/6 to-transparent rotate-12 animate-aurora-3"></div>
            </div>


            
            {/* Animated container - only animate on initial load, not during MFA */}
            <div className={`relative z-10 ${!mfaRequired && !mfaSetupRequired ? 'animate-fade-in' : ''}`}>
                <div className="sm:mx-auto sm:w-full sm:max-w-md">
                    {/* Logo */}
                    <div className={`flex flex-col items-center ${!mfaRequired && !mfaSetupRequired ? 'animate-slide-down' : ''}`}>
                        <div className="mb-4 relative">
                            {/* Logo glow effect */}
                            <div className="absolute inset-0 bg-yellow-400 rounded-full blur-xl opacity-10 scale-110 animate-pulse-glow"></div>
                            <img 
                                src={logo} 
                                alt="Silverfern Technology Consultants" 
                                className="w-20 h-20 object-contain relative z-10"
                            />
                        </div>
                        <h1 className={`text-3xl font-bold text-white mb-8 ${!mfaRequired && !mfaSetupRequired ? 'animate-fade-in-delay' : ''}`} style={{ fontFamily: 'Rockwell, serif' }}>
                            Governance Guardian
                        </h1>
                    </div>
                    <h2 className={`text-center text-3xl font-extrabold text-white ${!mfaRequired && !mfaSetupRequired ? 'animate-fade-in-delay-2' : ''}`}>
                        Sign in to your account
                    </h2>
                    <p className={`mt-2 text-center text-sm text-gray-400 ${!mfaRequired && !mfaSetupRequired ? 'animate-fade-in-delay-3' : ''}`}>
                        Or{' '}
                        <Link to="/register" className="font-medium text-yellow-400 hover:text-yellow-300 transition-colors">
                            create a new account
                        </Link>
                    </p>
                </div>

                <div className={`mt-8 sm:mx-auto sm:w-full sm:max-w-md ${!mfaRequired && !mfaSetupRequired ? 'animate-slide-up' : ''}`}>
                    <div className="bg-gray-900 py-8 px-4 shadow-xl border border-gray-800 sm:rounded-lg sm:px-10 backdrop-blur-sm bg-opacity-95">
                        <form className="space-y-6" onSubmit={handleSubmit}>
                            {error && (
                                <div className="bg-red-900 border border-red-800 text-red-300 px-4 py-3 rounded-md text-sm animate-shake">
                                    {error}
                                </div>
                            )}

                            <div className={!mfaRequired && !mfaSetupRequired ? 'animate-fade-in-delay-4' : ''}>
                                <label htmlFor="email" className="block text-sm font-medium text-gray-300">
                                    Email address
                                </label>
                                <div className="mt-1">
                                    <input
                                        id="email"
                                        name="email"
                                        type="email"
                                        autoComplete="email"
                                        required
                                        value={email}
                                        onChange={(e) => setEmail(e.target.value)}
                                        className="appearance-none block w-full px-3 py-2 border border-gray-700 rounded-md placeholder-gray-500 text-white bg-gray-800 focus:outline-none focus:ring-2 focus:ring-yellow-500 focus:border-yellow-500 sm:text-sm transition-all duration-200 hover:border-gray-600"
                                        placeholder="Enter your email"
                                        disabled={mfaRequired || mfaSetupRequired}
                                    />
                                </div>
                            </div>

                            <div className={!mfaRequired && !mfaSetupRequired ? 'animate-fade-in-delay-5' : ''}>
                                <label htmlFor="password" className="block text-sm font-medium text-gray-300">
                                    Password
                                </label>
                                <div className="mt-1">
                                    <input
                                        id="password"
                                        name="password"
                                        type="password"
                                        autoComplete="current-password"
                                        required
                                        value={password}
                                        onChange={(e) => setPassword(e.target.value)}
                                        className="appearance-none block w-full px-3 py-2 border border-gray-700 rounded-md placeholder-gray-500 text-white bg-gray-800 focus:outline-none focus:ring-2 focus:ring-yellow-500 focus:border-yellow-500 sm:text-sm transition-all duration-200 hover:border-gray-600"
                                        placeholder="Enter your password"
                                        disabled={mfaRequired || mfaSetupRequired}
                                    />
                                </div>
                            </div>

                            <div className={`flex items-center justify-between ${!mfaRequired && !mfaSetupRequired ? 'animate-fade-in-delay-6' : ''}`}>
                                <div className="flex items-center">
                                    <input
                                        id="remember-me"
                                        name="remember-me"
                                        type="checkbox"
                                        className="h-4 w-4 text-yellow-600 focus:ring-yellow-500 border-gray-600 bg-gray-800 rounded transition-colors"
                                        disabled={mfaRequired || mfaSetupRequired}
                                    />
                                    <label htmlFor="remember-me" className="ml-2 block text-sm text-gray-300">
                                        Remember me
                                    </label>
                                </div>

                                <div className="text-sm">
                                    <Link to="/forgot-password" className="font-medium text-yellow-400 hover:text-yellow-300 transition-colors">
                                        Forgot your password?
                                    </Link>
                                </div>
                            </div>

                            <div className={!mfaRequired && !mfaSetupRequired ? 'animate-fade-in-delay-7' : ''}>
                                <button
                                    type="submit"
                                    disabled={isLoading || mfaRequired || mfaSetupRequired}
                                    className="w-full flex justify-center py-2 px-4 border border-transparent rounded-md shadow-sm text-sm font-medium text-gray-900 bg-yellow-600 hover:bg-yellow-700 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-offset-gray-900 focus:ring-yellow-500 disabled:opacity-50 transition-all duration-200 transform hover:scale-105 disabled:hover:scale-100"
                                >
                                    {isLoading ? (
                                        <div className="flex items-center">
                                            <div className="animate-spin rounded-full h-5 w-5 border-2 border-gray-900 border-t-transparent mr-2"></div>
                                            <span className="animate-pulse">Signing in...</span>
                                        </div>
                                    ) : (
                                        'Sign in'
                                    )}
                                </button>
                            </div>
                        </form>

                        <div className={`mt-6 ${!mfaRequired && !mfaSetupRequired ? 'animate-fade-in-delay-8' : ''}`}>
                            <div className="relative">
                                <div className="absolute inset-0 flex items-center">
                                    <div className="w-full border-t border-gray-700" />
                                </div>
                                <div className="relative flex justify-center text-sm">
                                    <span className="px-2 bg-gray-900 text-gray-400">New to Governance Guardian?</span>
                                </div>
                            </div>

                            <div className="mt-6">
                                <Link
                                    to="/register"
                                    className="w-full flex justify-center py-2 px-4 border border-gray-700 rounded-md shadow-sm text-sm font-medium text-gray-300 bg-gray-800 hover:bg-gray-700 hover:text-white transition-all duration-200 transform hover:scale-105"
                                >
                                    Create an account
                                </Link>
                            </div>
                        </div>
                    </div>
                </div>

                {/* Footer links */}
                <div className={`mt-8 ${!mfaRequired && !mfaSetupRequired ? 'animate-fade-in-delay-8' : ''}`}>
                    <div className="flex flex-col items-center space-y-3">
                        <div className="flex items-center space-x-6 text-sm">
                            <a 
                                href="https://silverferntc.com" 
                                target="_blank" 
                                rel="noopener noreferrer" 
                                className="text-gray-400 hover:text-yellow-400 transition-colors"
                            >
                                Silverfern Technology Consultants
                            </a>
                            <span className="text-gray-600">•</span>
                            <a 
                                href="https://fernworks.io" 
                                target="_blank" 
                                rel="noopener noreferrer" 
                                className="text-gray-400 hover:text-yellow-400 transition-colors"
                            >
                                FernWorks
                            </a>
                        </div>
                        <div className="text-xs text-gray-500">
                            By logging in you agree to our{' '}
                            <a 
                                href="https://fernworks.io/privacy-policy" 
                                target="_blank" 
                                rel="noopener noreferrer" 
                                className="text-yellow-400 hover:text-yellow-300 transition-colors"
                            >
                                privacy policy
                            </a>
                            {' & '}
                            <a 
                                href="https://fernworks.io/terms-of-service" 
                                target="_blank" 
                                rel="noopener noreferrer" 
                                className="text-yellow-400 hover:text-yellow-300 transition-colors"
                            >
                                terms of service
                            </a>
                        </div>
                    </div>
                </div>
            </div>
        </div>
    );
};

export default LoginPage;