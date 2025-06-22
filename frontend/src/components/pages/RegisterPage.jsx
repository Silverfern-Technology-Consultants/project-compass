import React, { useState } from 'react';
import { useNavigate, Link } from 'react-router-dom';
import { useAuth } from '../../contexts/AuthContext';
import { Eye, EyeOff, Shield } from 'lucide-react';

const RegisterPage = () => {
    const { register, isLoading, error, checkEmailAvailability } = useAuth();
    const navigate = useNavigate();
    const [showPassword, setShowPassword] = useState(false);
    const [formData, setFormData] = useState({
        firstName: '',
        lastName: '',
        email: '',
        companyName: '',
        password: '',
        confirmPassword: ''
    });
    const [formErrors, setFormErrors] = useState({});
    const [emailAvailable, setEmailAvailable] = useState(null);
    const [checkingEmail, setCheckingEmail] = useState(false);
    const [registrationSuccess, setRegistrationSuccess] = useState(false);

    const handleInputChange = (e) => {
        const { name, value } = e.target;
        setFormData(prev => ({
            ...prev,
            [name]: value
        }));

        // Clear field-specific errors
        if (formErrors[name]) {
            setFormErrors(prev => ({
                ...prev,
                [name]: ''
            }));
        }

        // Check email availability when email changes
        if (name === 'email' && value && value.includes('@')) {
            debounceEmailCheck(value);
        }
    };

    const debounceEmailCheck = React.useCallback(
        React.useMemo(() => {
            let timeoutId;
            return (email) => {
                clearTimeout(timeoutId);
                timeoutId = setTimeout(async () => {
                    setCheckingEmail(true);
                    try {
                        const available = await checkEmailAvailability(email);
                        setEmailAvailable(available);
                    } catch (error) {
                        console.error('Email check failed:', error);
                        setEmailAvailable(null);
                    } finally {
                        setCheckingEmail(false);
                    }
                }, 500);
            };
        }, [checkEmailAvailability]),
        [checkEmailAvailability]
    );

    const validateForm = () => {
        const errors = {};

        if (!formData.firstName.trim()) {
            errors.firstName = 'First name is required';
        }

        if (!formData.lastName.trim()) {
            errors.lastName = 'Last name is required';
        }

        if (!formData.email.trim()) {
            errors.email = 'Email is required';
        } else if (!/\S+@\S+\.\S+/.test(formData.email)) {
            errors.email = 'Please enter a valid email address';
        } else if (emailAvailable === false) {
            errors.email = 'This email is already registered';
        }

        if (!formData.companyName.trim()) {
            errors.companyName = 'Company name is required';
        }

        if (!formData.password) {
            errors.password = 'Password is required';
        } else if (formData.password.length < 8) {
            errors.password = 'Password must be at least 8 characters';
        }

        if (formData.password !== formData.confirmPassword) {
            errors.confirmPassword = 'Passwords do not match';
        }

        setFormErrors(errors);
        return Object.keys(errors).length === 0;
    };

    const handleSubmit = async (e) => {
        e.preventDefault();

        if (!validateForm()) {
            return;
        }

        try {
            const result = await register({
                firstName: formData.firstName.trim(),
                lastName: formData.lastName.trim(),
                email: formData.email.trim(),
                companyName: formData.companyName.trim(),
                password: formData.password
            });

            if (result.requiresEmailVerification) {
                setRegistrationSuccess(true);
                // Redirect to verification page after a delay
                setTimeout(() => {
                    navigate('/verify-email');
                }, 3000);
            } else {
                // If no email verification required, go to dashboard
                navigate('/app/dashboard');
            }
        } catch (error) {
            console.error('Registration failed:', error);
        }
    };

    const getEmailStatus = () => {
        if (checkingEmail) {
            return { icon: '⏳', color: 'text-gray-400', message: 'Checking...' };
        }
        if (emailAvailable === true) {
            return { icon: '✓', color: 'text-green-400', message: 'Available' };
        }
        if (emailAvailable === false) {
            return { icon: '✗', color: 'text-red-400', message: 'Taken' };
        }
        return null;
    };

    const emailStatus = getEmailStatus();

    // Show success message after registration
    if (registrationSuccess) {
        return (
            <div className="min-h-screen flex items-center justify-center bg-gray-950">
                <div className="max-w-md w-full space-y-8 p-8">
                    <div className="text-center">
                        <div className="mx-auto flex items-center justify-center h-12 w-12 rounded-full bg-green-100">
                            <svg className="h-6 w-6 text-green-600" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M5 13l4 4L19 7"></path>
                            </svg>
                        </div>
                        <h2 className="mt-6 text-center text-3xl font-extrabold text-white">
                            Account Created!
                        </h2>
                        <p className="mt-2 text-center text-sm text-gray-400">
                            We've sent a verification email to <span className="text-yellow-400">{formData.email}</span>
                        </p>
                        <p className="mt-4 text-center text-sm text-gray-400">
                            Please check your email and click the verification link to activate your account.
                        </p>
                        <p className="mt-2 text-center text-xs text-gray-500">
                            Redirecting to verification page in 3 seconds...
                        </p>
                        <div className="mt-6">
                            <button
                                onClick={() => navigate('/verify-email')}
                                className="w-full flex justify-center py-2 px-4 border border-transparent rounded-md shadow-sm text-sm font-medium text-gray-900 bg-yellow-500 hover:bg-yellow-400"
                            >
                                Go to Verification Page
                            </button>
                        </div>
                        <div className="mt-4">
                            <Link
                                to="/login"
                                className="w-full flex justify-center py-2 px-4 border border-gray-300 rounded-md shadow-sm text-sm font-medium text-gray-300 bg-gray-800 hover:bg-gray-700"
                            >
                                Back to Sign In
                            </Link>
                        </div>
                    </div>
                </div>
            </div>
        );
    }

    return (
        <div className="min-h-screen flex items-center justify-center bg-gray-950">
            <div className="max-w-md w-full space-y-8 p-8">
                <div>
                    <div className="mx-auto h-12 w-12 flex items-center justify-center bg-gradient-to-br from-yellow-400 to-yellow-600 rounded">
                        <Shield size={24} className="text-black" />
                    </div>
                    <h2 className="mt-6 text-center text-3xl font-extrabold text-white">
                        Create your account
                    </h2>
                    <p className="mt-2 text-center text-sm text-gray-400">
                        Join thousands of organizations using Compass for Azure governance
                    </p>
                </div>
                <form className="mt-8 space-y-6" onSubmit={handleSubmit}>
                    {error && (
                        <div className="rounded-md bg-red-900 border border-red-800 p-3">
                            <div className="text-sm text-red-300">{error}</div>
                        </div>
                    )}

                    <div className="grid grid-cols-1 gap-6">
                        <div className="grid grid-cols-2 gap-4">
                            <div>
                                <label htmlFor="firstName" className="block text-sm font-medium text-gray-300">
                                    First Name
                                </label>
                                <div className="mt-1">
                                    <input
                                        id="firstName"
                                        name="firstName"
                                        type="text"
                                        required
                                        value={formData.firstName}
                                        onChange={handleInputChange}
                                        className="appearance-none block w-full px-3 py-2 border border-gray-700 rounded-md placeholder-gray-500 text-white bg-gray-800 focus:outline-none focus:ring-yellow-500 focus:border-yellow-500 sm:text-sm"
                                        placeholder="First name"
                                    />
                                    {formErrors.firstName && (
                                        <p className="mt-1 text-sm text-red-400">{formErrors.firstName}</p>
                                    )}
                                </div>
                            </div>

                            <div>
                                <label htmlFor="lastName" className="block text-sm font-medium text-gray-300">
                                    Last Name
                                </label>
                                <div className="mt-1">
                                    <input
                                        id="lastName"
                                        name="lastName"
                                        type="text"
                                        required
                                        value={formData.lastName}
                                        onChange={handleInputChange}
                                        className="appearance-none block w-full px-3 py-2 border border-gray-700 rounded-md placeholder-gray-500 text-white bg-gray-800 focus:outline-none focus:ring-yellow-500 focus:border-yellow-500 sm:text-sm"
                                        placeholder="Last name"
                                    />
                                    {formErrors.lastName && (
                                        <p className="mt-1 text-sm text-red-400">{formErrors.lastName}</p>
                                    )}
                                </div>
                            </div>
                        </div>

                        <div>
                            <label htmlFor="email" className="block text-sm font-medium text-gray-300">
                                Email Address
                            </label>
                            <div className="mt-1 relative">
                                <input
                                    id="email"
                                    name="email"
                                    type="email"
                                    autoComplete="email"
                                    required
                                    value={formData.email}
                                    onChange={handleInputChange}
                                    className="appearance-none block w-full px-3 py-2 border border-gray-700 rounded-md placeholder-gray-500 text-white bg-gray-800 focus:outline-none focus:ring-yellow-500 focus:border-yellow-500 focus:z-10 sm:text-sm"
                                    placeholder="Enter your email address"
                                />
                                {emailStatus && (
                                    <div className={`absolute inset-y-0 right-0 pr-3 flex items-center ${emailStatus.color}`}>
                                        <span className="text-sm">{emailStatus.icon}</span>
                                    </div>
                                )}
                            </div>
                            {emailStatus && (
                                <p className={`mt-1 text-sm ${emailStatus.color}`}>{emailStatus.message}</p>
                            )}
                            {formErrors.email && (
                                <p className="mt-1 text-sm text-red-400">{formErrors.email}</p>
                            )}
                        </div>

                        <div>
                            <label htmlFor="companyName" className="block text-sm font-medium text-gray-300">
                                Company Name
                            </label>
                            <div className="mt-1">
                                <input
                                    id="companyName"
                                    name="companyName"
                                    type="text"
                                    required
                                    value={formData.companyName}
                                    onChange={handleInputChange}
                                    className="appearance-none block w-full px-3 py-2 border border-gray-700 rounded-md placeholder-gray-500 text-white bg-gray-800 focus:outline-none focus:ring-yellow-500 focus:border-yellow-500 sm:text-sm"
                                    placeholder="Your company name"
                                />
                                {formErrors.companyName && (
                                    <p className="mt-1 text-sm text-red-400">{formErrors.companyName}</p>
                                )}
                            </div>
                        </div>

                        <div>
                            <label htmlFor="password" className="block text-sm font-medium text-gray-300">
                                Password
                            </label>
                            <div className="mt-1 relative">
                                <input
                                    id="password"
                                    name="password"
                                    type={showPassword ? "text" : "password"}
                                    autoComplete="new-password"
                                    required
                                    value={formData.password}
                                    onChange={handleInputChange}
                                    className="appearance-none block w-full px-3 py-2 border border-gray-700 rounded-md placeholder-gray-500 text-white bg-gray-800 focus:outline-none focus:ring-yellow-500 focus:border-yellow-500 focus:z-10 sm:text-sm pr-10"
                                    placeholder="Create a strong password"
                                />
                                <button
                                    type="button"
                                    className="absolute inset-y-0 right-0 pr-3 flex items-center"
                                    onClick={() => setShowPassword(!showPassword)}
                                >
                                    {showPassword ? (
                                        <EyeOff className="h-4 w-4 text-gray-400" />
                                    ) : (
                                        <Eye className="h-4 w-4 text-gray-400" />
                                    )}
                                </button>
                            </div>
                            {formErrors.password && (
                                <p className="mt-1 text-sm text-red-400">{formErrors.password}</p>
                            )}
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
                                    autoComplete="new-password"
                                    required
                                    value={formData.confirmPassword}
                                    onChange={handleInputChange}
                                    className="appearance-none block w-full px-3 py-2 border border-gray-700 rounded-md placeholder-gray-500 text-white bg-gray-800 focus:outline-none focus:ring-yellow-500 focus:border-yellow-500 sm:text-sm"
                                    placeholder="Confirm your password"
                                />
                                {formErrors.confirmPassword && (
                                    <p className="mt-1 text-sm text-red-400">{formErrors.confirmPassword}</p>
                                )}
                            </div>
                        </div>
                    </div>

                    <div>
                        <button
                            type="submit"
                            disabled={isLoading || emailAvailable === false}
                            className="group relative w-full flex justify-center py-2 px-4 border border-transparent text-sm font-medium rounded-md text-gray-900 bg-yellow-500 hover:bg-yellow-400 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-offset-gray-900 focus:ring-yellow-500 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
                        >
                            {isLoading ? (
                                <div className="flex items-center">
                                    <div className="animate-spin rounded-full h-4 w-4 border-b-2 border-gray-900 mr-2"></div>
                                    Creating account...
                                </div>
                            ) : (
                                'Create account'
                            )}
                        </button>
                    </div>

                    <div className="text-center">
                        <p className="text-sm text-gray-400">
                            Already have an account?{' '}
                            <Link
                                to="/login"
                                className="font-medium text-yellow-400 hover:text-yellow-300 focus:outline-none"
                            >
                                Sign in here
                            </Link>
                        </p>
                    </div>
                </form>
            </div>
        </div>
    );
};

export default RegisterPage;