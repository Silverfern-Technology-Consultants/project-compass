import React, { useState, useEffect } from 'react';
import { useSearchParams, useNavigate } from 'react-router-dom';
import { useAuth } from '../../contexts/AuthContext';

const VerifyEmailPage = () => {
    const [status, setStatus] = useState('verifying'); // 'verifying', 'success', 'error', 'resent', 'pending'
    const [message, setMessage] = useState('');
    const [email, setEmail] = useState('');
    const [isResending, setIsResending] = useState(false);
    const { verifyEmail, resendVerification, user } = useAuth();
    const [searchParams] = useSearchParams();
    const navigate = useNavigate();

    useEffect(() => {
        // Extract token from URL parameters
        const token = searchParams.get('token');

        if (token) {
            handleEmailVerification(token);
        } else if (user && !user.emailVerified) {
            setStatus('pending');
            setEmail(user.email);
            setMessage('Please check your email for a verification link.');
        } else {
            setStatus('error');
            setMessage('No verification token provided. Please check your email for the verification link.');
        }
    }, [searchParams, user]);

    const handleEmailVerification = async (token) => {
        try {
            setStatus('verifying');
            setMessage('Verifying your email address...');

            const result = await verifyEmail(token);
            setStatus('success');
            setMessage(result.message || 'Email verified successfully! You can now sign in.');

            // Redirect to login after a delay
            setTimeout(() => {
                navigate('/login');
            }, 3000);
        } catch (error) {
            setStatus('error');
            setMessage(error.message || 'Email verification failed. The link may be expired or invalid.');
        }
    };

    const handleResendVerification = async () => {
        if (!email) {
            setMessage('Please enter your email address.');
            return;
        }

        setIsResending(true);
        try {
            await resendVerification(email);
            setStatus('resent');
            setMessage('Verification email sent! Please check your inbox and spam folder.');
        } catch (error) {
            setMessage(error.message || 'Failed to resend verification email.');
        } finally {
            setIsResending(false);
        }
    };

    const handleBackToLogin = () => {
        navigate('/login');
    };

    const handleCreateNewAccount = () => {
        navigate('/register');
    };

    const renderContent = () => {
        switch (status) {
            case 'verifying':
                return (
                    <div className="text-center">
                        <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-blue-600 mx-auto"></div>
                        <h2 className="mt-4 text-2xl font-bold text-gray-900">Verifying your email...</h2>
                        <p className="mt-2 text-sm text-gray-600">Please wait while we verify your email address.</p>
                    </div>
                );

            case 'success':
                return (
                    <div className="text-center">
                        <div className="mx-auto flex items-center justify-center h-12 w-12 rounded-full bg-green-100">
                            <svg className="h-6 w-6 text-green-600" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M5 13l4 4L19 7"></path>
                            </svg>
                        </div>
                        <h2 className="mt-4 text-2xl font-bold text-gray-900">Email Verified!</h2>
                        <p className="mt-2 text-sm text-gray-600">{message}</p>
                        <p className="mt-2 text-xs text-gray-500">Redirecting to login in 3 seconds...</p>
                        <div className="mt-6">
                            <button
                                onClick={handleBackToLogin}
                                className="w-full flex justify-center py-2 px-4 border border-transparent rounded-md shadow-sm text-sm font-medium text-white bg-blue-600 hover:bg-blue-700"
                            >
                                Sign In Now
                            </button>
                        </div>
                    </div>
                );

            case 'pending':
                return (
                    <div className="text-center">
                        <div className="mx-auto flex items-center justify-center h-12 w-12 rounded-full bg-yellow-100">
                            <svg className="h-6 w-6 text-yellow-600" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M12 8v4m0 4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z"></path>
                            </svg>
                        </div>
                        <h2 className="mt-4 text-2xl font-bold text-gray-900">Verify Your Email</h2>
                        <p className="mt-2 text-sm text-gray-600">{message}</p>

                        <div className="mt-6 space-y-4">
                            <div>
                                <label htmlFor="email" className="block text-sm font-medium text-gray-700">
                                    Email address
                                </label>
                                <div className="mt-1">
                                    <input
                                        id="email"
                                        name="email"
                                        type="email"
                                        value={email}
                                        onChange={(e) => setEmail(e.target.value)}
                                        className="appearance-none block w-full px-3 py-2 border border-gray-300 rounded-md placeholder-gray-400 focus:outline-none focus:ring-blue-500 focus:border-blue-500 sm:text-sm"
                                        placeholder="Enter your email"
                                    />
                                </div>
                            </div>

                            <button
                                onClick={handleResendVerification}
                                disabled={isResending}
                                className="w-full flex justify-center py-2 px-4 border border-transparent rounded-md shadow-sm text-sm font-medium text-white bg-blue-600 hover:bg-blue-700 disabled:opacity-50"
                            >
                                {isResending ? 'Sending...' : 'Resend Verification Email'}
                            </button>

                            <button
                                onClick={handleBackToLogin}
                                className="w-full flex justify-center py-2 px-4 border border-gray-300 rounded-md shadow-sm text-sm font-medium text-gray-700 bg-white hover:bg-gray-50"
                            >
                                Back to Sign In
                            </button>
                        </div>
                    </div>
                );

            case 'resent':
                return (
                    <div className="text-center">
                        <div className="mx-auto flex items-center justify-center h-12 w-12 rounded-full bg-blue-100">
                            <svg className="h-6 w-6 text-blue-600" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M3 8l7.89 4.26a2 2 0 002.22 0L21 8M5 19h14a2 2 0 002-2V7a2 2 0 00-2-2H5a2 2 0 00-2 2v10a2 2 0 002 2z"></path>
                            </svg>
                        </div>
                        <h2 className="mt-4 text-2xl font-bold text-gray-900">Email Sent!</h2>
                        <p className="mt-2 text-sm text-gray-600">{message}</p>
                        <div className="mt-6">
                            <button
                                onClick={handleBackToLogin}
                                className="w-full flex justify-center py-2 px-4 border border-gray-300 rounded-md shadow-sm text-sm font-medium text-gray-700 bg-white hover:bg-gray-50"
                            >
                                Back to Sign In
                            </button>
                        </div>
                    </div>
                );

            case 'error':
            default:
                return (
                    <div className="text-center">
                        <div className="mx-auto flex items-center justify-center h-12 w-12 rounded-full bg-red-100">
                            <svg className="h-6 w-6 text-red-600" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M6 18L18 6M6 6l12 12"></path>
                            </svg>
                        </div>
                        <h2 className="mt-4 text-2xl font-bold text-gray-900">Verification Failed</h2>
                        <p className="mt-2 text-sm text-gray-600">{message}</p>

                        <div className="mt-6 space-y-3">
                            <div>
                                <label htmlFor="resend-email" className="block text-sm font-medium text-gray-700">
                                    Resend verification to:
                                </label>
                                <div className="mt-1">
                                    <input
                                        id="resend-email"
                                        name="resend-email"
                                        type="email"
                                        value={email}
                                        onChange={(e) => setEmail(e.target.value)}
                                        className="appearance-none block w-full px-3 py-2 border border-gray-300 rounded-md placeholder-gray-400 focus:outline-none focus:ring-blue-500 focus:border-blue-500 sm:text-sm"
                                        placeholder="Enter your email"
                                    />
                                </div>
                            </div>

                            <button
                                onClick={handleResendVerification}
                                disabled={isResending || !email}
                                className="w-full flex justify-center py-2 px-4 border border-transparent rounded-md shadow-sm text-sm font-medium text-white bg-blue-600 hover:bg-blue-700 disabled:opacity-50"
                            >
                                {isResending ? 'Sending...' : 'Resend Verification Email'}
                            </button>

                            <button
                                onClick={handleCreateNewAccount}
                                className="w-full flex justify-center py-2 px-4 border border-gray-300 rounded-md shadow-sm text-sm font-medium text-gray-700 bg-white hover:bg-gray-50"
                            >
                                Create New Account
                            </button>

                            <button
                                onClick={handleBackToLogin}
                                className="w-full flex justify-center py-2 px-4 border border-gray-300 rounded-md shadow-sm text-sm font-medium text-gray-700 bg-white hover:bg-gray-50"
                            >
                                Back to Sign In
                            </button>
                        </div>
                    </div>
                );
        }
    };

    return (
        <div className="min-h-screen bg-gray-50 flex flex-col justify-center py-12 sm:px-6 lg:px-8">
            <div className="sm:mx-auto sm:w-full sm:max-w-md">
                {/* Logo */}
                <div className="flex justify-center">
                    <div className="w-16 h-16 bg-gradient-to-br from-yellow-400 to-yellow-600 rounded-lg flex items-center justify-center">
                        <svg className="w-10 h-10 text-gray-900" fill="currentColor" viewBox="0 0 24 24">
                            <path d="M12 2L2 7l10 5 10-5-10-5zM2 17l10 5 10-5M2 12l10 5 10-5" />
                        </svg>
                    </div>
                </div>
            </div>

            <div className="mt-8 sm:mx-auto sm:w-full sm:max-w-md">
                <div className="bg-white py-8 px-4 shadow sm:rounded-lg sm:px-10">
                    {renderContent()}
                </div>
            </div>
        </div>
    );
};

export default VerifyEmailPage;