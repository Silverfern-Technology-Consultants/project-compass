// frontend/src/components/pages/AcceptInvitePage.jsx
import React, { useState, useEffect } from 'react';
import { useSearchParams, useNavigate } from 'react-router-dom';
import { AuthApi, teamApi, apiUtils } from '../../services/apiService';

const AcceptInvitePage = () => {
    const [searchParams] = useSearchParams();
    const navigate = useNavigate();
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState(null);
    const [inviteData, setInviteData] = useState(null);
    const [formData, setFormData] = useState({
        firstName: '',
        lastName: '',
        password: '',
        confirmPassword: ''
    });
    const [step, setStep] = useState('validating'); // validating, register, success, error

    const token = searchParams.get('token');

    useEffect(() => {
        if (!token) {
            setError('Invalid invitation link');
            setStep('error');
            setLoading(false);
            return;
        }

        validateInvitation();
    }, [token]);

    const validateInvitation = async () => {
        try {
            setLoading(true);
            setError(null);

            // Call backend to validate invitation token - FIXED: Use correct API URL
            const response = await fetch(`${process.env.REACT_APP_API_URL || 'https://localhost:7163/api'}/team/validate-invite/${token}`, {
                method: 'GET',
                headers: {
                    'Content-Type': 'application/json'
                }
            });

            if (response.ok) {
                const data = await response.json();
                setInviteData(data);
                setStep('register');
            } else if (response.status === 404) {
                setError('Invitation not found or expired');
                setStep('error');
            } else if (response.status === 400) {
                const errorData = await response.json();
                setError(errorData.message || 'Invalid invitation');
                setStep('error');
            } else {
                setError('Failed to validate invitation');
                setStep('error');
            }
        } catch (err) {
            console.error('Error validating invitation:', err);
            setError('Failed to validate invitation');
            setStep('error');
        } finally {
            setLoading(false);
        }
    };

    const handleInputChange = (e) => {
        const { name, value } = e.target;
        setFormData(prev => ({
            ...prev,
            [name]: value
        }));
    };

    const handleAcceptInvitation = async (e) => {
        e.preventDefault();

        if (formData.password !== formData.confirmPassword) {
            setError('Passwords do not match');
            return;
        }

        if (formData.password.length < 6) {
            setError('Password must be at least 6 characters');
            return;
        }

        try {
            setLoading(true);
            setError(null);

            // Register new user and accept invitation
            const registerData = {
                firstName: formData.firstName,
                lastName: formData.lastName,
                email: inviteData.email,
                password: formData.password,
                companyName: inviteData.organizationName,
                invitationToken: token
            };

            const response = await AuthApi.register(registerData);

            if (response.success) {
                setStep('success');
                // Redirect to login after 3 seconds with success message
                setTimeout(() => {
                    navigate('/login', {
                        state: {
                            message: 'Account created successfully! You can now log in.',
                            email: inviteData.email,
                            type: 'success'
                        }
                    });
                }, 3000);
            } else {
                setError(response.message || 'Failed to accept invitation');
            }
        } catch (err) {
            console.error('Error accepting invitation:', err);
            const errorInfo = apiUtils.handleApiError(err);
            setError(errorInfo.message);
        } finally {
            setLoading(false);
        }
    };

    if (loading) {
        return (
            <div className="min-h-screen bg-gray-950 flex flex-col justify-center py-12 sm:px-6 lg:px-8">
                <div className="sm:mx-auto sm:w-full sm:max-w-md">
                    <div className="bg-gray-900 border border-gray-800 py-8 px-4 shadow sm:rounded-lg sm:px-10">
                        <div className="flex items-center justify-center">
                            <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-yellow-600"></div>
                            <span className="ml-3 text-gray-300">Validating invitation...</span>
                        </div>
                    </div>
                </div>
            </div>
        );
    }

    if (step === 'error') {
        return (
            <div className="min-h-screen bg-gray-950 flex flex-col justify-center py-12 sm:px-6 lg:px-8">
                <div className="sm:mx-auto sm:w-full sm:max-w-md">
                    <div className="text-center">
                        <h2 className="text-3xl font-extrabold text-white">
                            Invitation Error
                        </h2>
                        <p className="mt-2 text-sm text-gray-400">
                            There was a problem with your invitation
                        </p>
                    </div>

                    <div className="mt-8 bg-gray-900 border border-gray-800 py-8 px-4 shadow sm:rounded-lg sm:px-10">
                        <div className="text-center">
                            <div className="mx-auto flex items-center justify-center h-12 w-12 rounded-full bg-red-900">
                                <svg className="h-6 w-6 text-red-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
                                </svg>
                            </div>
                            <h3 className="mt-2 text-sm font-medium text-white">
                                {error}
                            </h3>
                            <p className="mt-1 text-sm text-gray-400">
                                Please contact the person who invited you for a new invitation link.
                            </p>
                            <div className="mt-6">
                                <button
                                    onClick={() => navigate('/login')}
                                    className="w-full flex justify-center py-2 px-4 border border-transparent rounded-md shadow-sm text-sm font-medium text-black bg-yellow-600 hover:bg-yellow-700 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-yellow-500"
                                >
                                    Go to Login
                                </button>
                            </div>
                        </div>
                    </div>
                </div>
            </div>
        );
    }

    if (step === 'success') {
        return (
            <div className="min-h-screen bg-gray-950 flex flex-col justify-center py-12 sm:px-6 lg:px-8">
                <div className="sm:mx-auto sm:w-full sm:max-w-md">
                    <div className="text-center">
                        <h2 className="text-3xl font-extrabold text-white">
                            Welcome to the Team!
                        </h2>
                        <p className="mt-2 text-sm text-gray-400">
                            Your account has been created successfully
                        </p>
                    </div>

                    <div className="mt-8 bg-gray-900 border border-gray-800 py-8 px-4 shadow sm:rounded-lg sm:px-10">
                        <div className="text-center">
                            <div className="mx-auto flex items-center justify-center h-12 w-12 rounded-full bg-green-900">
                                <svg className="h-6 w-6 text-green-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 13l4 4L19 7" />
                                </svg>
                            </div>
                            <h3 className="mt-2 text-sm font-medium text-white">
                                Account Created Successfully!
                            </h3>
                            <p className="mt-1 text-sm text-gray-400">
                                You'll be redirected to the login page shortly...
                            </p>
                        </div>
                    </div>
                </div>
            </div>
        );
    }

    return (
        <div className="min-h-screen bg-gray-950 flex flex-col justify-center py-12 sm:px-6 lg:px-8">
            <div className="sm:mx-auto sm:w-full sm:max-w-md">
                <div className="text-center">
                    <h2 className="text-3xl font-extrabold text-white">
                        Join {inviteData?.organizationName}
                    </h2>
                    <p className="mt-2 text-sm text-gray-400">
                        You've been invited as a {inviteData?.role}
                    </p>
                </div>

                <div className="mt-8 bg-gray-900 border border-gray-800 py-8 px-4 shadow sm:rounded-lg sm:px-10">
                    <form className="space-y-6" onSubmit={handleAcceptInvitation}>
                        <div className="grid grid-cols-2 gap-4">
                            <div>
                                <label htmlFor="firstName" className="block text-sm font-medium text-gray-300">
                                    First name
                                </label>
                                <div className="mt-1">
                                    <input
                                        id="firstName"
                                        name="firstName"
                                        type="text"
                                        required
                                        value={formData.firstName}
                                        onChange={handleInputChange}
                                        className="appearance-none block w-full px-3 py-2 border border-gray-600 rounded-md placeholder-gray-400 bg-gray-800 text-white focus:outline-none focus:ring-yellow-500 focus:border-yellow-500 sm:text-sm"
                                        placeholder="First name"
                                    />
                                </div>
                            </div>

                            <div>
                                <label htmlFor="lastName" className="block text-sm font-medium text-gray-300">
                                    Last name
                                </label>
                                <div className="mt-1">
                                    <input
                                        id="lastName"
                                        name="lastName"
                                        type="text"
                                        required
                                        value={formData.lastName}
                                        onChange={handleInputChange}
                                        className="appearance-none block w-full px-3 py-2 border border-gray-600 rounded-md placeholder-gray-400 bg-gray-800 text-white focus:outline-none focus:ring-yellow-500 focus:border-yellow-500 sm:text-sm"
                                        placeholder="Last name"
                                    />
                                </div>
                            </div>
                        </div>

                        <div>
                            <label htmlFor="email" className="block text-sm font-medium text-gray-300">
                                Email address
                            </label>
                            <div className="mt-1">
                                <input
                                    id="email"
                                    name="email"
                                    type="email"
                                    disabled
                                    value={inviteData?.email || ''}
                                    className="appearance-none block w-full px-3 py-2 border border-gray-600 rounded-md placeholder-gray-400 bg-gray-700 text-gray-400 sm:text-sm"
                                />
                            </div>
                        </div>

                        <div>
                            <label htmlFor="password" className="block text-sm font-medium text-gray-300">
                                Password
                            </label>
                            <div className="mt-1">
                                <input
                                    id="password"
                                    name="password"
                                    type="password"
                                    required
                                    value={formData.password}
                                    onChange={handleInputChange}
                                    className="appearance-none block w-full px-3 py-2 border border-gray-600 rounded-md placeholder-gray-400 bg-gray-800 text-white focus:outline-none focus:ring-yellow-500 focus:border-yellow-500 sm:text-sm"
                                    placeholder="Create a password"
                                />
                            </div>
                        </div>

                        <div>
                            <label htmlFor="confirmPassword" className="block text-sm font-medium text-gray-300">
                                Confirm Password
                            </label>
                            <div className="mt-1">
                                <input
                                    id="confirmPassword"
                                    name="confirmPassword"
                                    type="password"
                                    required
                                    value={formData.confirmPassword}
                                    onChange={handleInputChange}
                                    className="appearance-none block w-full px-3 py-2 border border-gray-600 rounded-md placeholder-gray-400 bg-gray-800 text-white focus:outline-none focus:ring-yellow-500 focus:border-yellow-500 sm:text-sm"
                                    placeholder="Confirm your password"
                                />
                            </div>
                        </div>

                        {error && (
                            <div className="bg-red-900 border border-red-800 text-red-300 px-4 py-3 rounded">
                                {error}
                            </div>
                        )}

                        <div>
                            <button
                                type="submit"
                                disabled={loading}
                                className="w-full flex justify-center py-2 px-4 border border-transparent rounded-md shadow-sm text-sm font-medium text-black bg-yellow-600 hover:bg-yellow-700 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-yellow-500 disabled:opacity-50 disabled:cursor-not-allowed"
                            >
                                {loading ? 'Creating Account...' : 'Accept Invitation & Create Account'}
                            </button>
                        </div>
                    </form>
                </div>
            </div>
        </div>
    );
};

export default AcceptInvitePage;