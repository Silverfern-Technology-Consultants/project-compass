import React, { useState } from 'react';
import { useAuth } from '../../contexts/AuthContext';
import { User, Mail, Building, Calendar, CreditCard } from 'lucide-react';

const ProfilePage = () => {
    const { user, logout } = useAuth();
    const [isEditing, setIsEditing] = useState(false);
    const [formData, setFormData] = useState({
        firstName: user?.FirstName || '',
        lastName: user?.LastName || '',
        email: user?.Email || '',
        companyName: user?.CompanyName || ''
    });

    const handleInputChange = (e) => {
        const { name, value } = e.target;
        setFormData(prev => ({
            ...prev,
            [name]: value
        }));
    };

    const handleSave = async () => {
        // TODO: Implement profile update API call
        console.log('Saving profile:', formData);
        setIsEditing(false);
    };

    const handleCancel = () => {
        setFormData({
            firstName: user?.FirstName || '',
            lastName: user?.LastName || '',
            email: user?.Email || '',
            companyName: user?.CompanyName || ''
        });
        setIsEditing(false);
    };

    return (
        <div className="min-h-screen bg-gray-950">
            {/* Main Content */}
            <div className="max-w-4xl mx-auto py-8 px-4 sm:px-6 lg:px-8">
                {/* Header */}
                <div className="mb-8">
                    <h1 className="text-2xl font-bold text-white">Account Settings</h1>
                    <p className="text-gray-400">Manage your account information and preferences</p>
                </div>

                <div className="grid grid-cols-1 lg:grid-cols-3 gap-8">
                    {/* Profile Information */}
                    <div className="lg:col-span-2">
                        <div className="bg-gray-900 border border-gray-800 rounded-lg">
                            <div className="p-6 border-b border-gray-800">
                                <div className="flex items-center justify-between">
                                    <h2 className="text-lg font-semibold text-white">Profile Information</h2>
                                    {!isEditing ? (
                                        <button
                                            onClick={() => setIsEditing(true)}
                                            className="bg-yellow-600 hover:bg-yellow-700 text-black px-4 py-2 rounded font-medium transition-colors"
                                        >
                                            Edit Profile
                                        </button>
                                    ) : (
                                        <div className="flex space-x-2">
                                            <button
                                                onClick={handleSave}
                                                className="bg-green-600 hover:bg-green-700 text-white px-4 py-2 rounded font-medium transition-colors"
                                            >
                                                Save
                                            </button>
                                            <button
                                                onClick={handleCancel}
                                                className="bg-gray-600 hover:bg-gray-700 text-white px-4 py-2 rounded font-medium transition-colors"
                                            >
                                                Cancel
                                            </button>
                                        </div>
                                    )}
                                </div>
                            </div>

                            <div className="p-6 space-y-6">
                                <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                                    <div>
                                        <label className="block text-sm font-medium text-gray-300 mb-2">
                                            First Name
                                        </label>
                                        {isEditing ? (
                                            <input
                                                type="text"
                                                name="firstName"
                                                value={formData.firstName}
                                                onChange={handleInputChange}
                                                className="w-full px-3 py-2 bg-gray-800 border border-gray-700 rounded text-white focus:ring-2 focus:ring-yellow-600 focus:border-transparent"
                                            />
                                        ) : (
                                            <p className="text-white">{user?.firstName}</p>
                                        )}
                                    </div>

                                    <div>
                                        <label className="block text-sm font-medium text-gray-300 mb-2">
                                            Last Name
                                        </label>
                                        {isEditing ? (
                                            <input
                                                type="text"
                                                name="lastName"
                                                value={formData.lastName}
                                                onChange={handleInputChange}
                                                className="w-full px-3 py-2 bg-gray-800 border border-gray-700 rounded text-white focus:ring-2 focus:ring-yellow-600 focus:border-transparent"
                                            />
                                        ) : (
                                            <p className="text-white">{user?.lastName}</p>
                                        )}
                                    </div>
                                </div>

                                <div>
                                    <label className="block text-sm font-medium text-gray-300 mb-2">
                                        Email Address
                                    </label>
                                    {isEditing ? (
                                        <input
                                            type="email"
                                            name="email"
                                            value={formData.email}
                                            onChange={handleInputChange}
                                            className="w-full px-3 py-2 bg-gray-800 border border-gray-700 rounded text-white focus:ring-2 focus:ring-yellow-600 focus:border-transparent"
                                        />
                                    ) : (
                                        <p className="text-white">{user?.email}</p>
                                    )}
                                    {user?.emailVerified && (
                                        <p className="text-sm text-green-400 mt-1">✓ Verified</p>
                                    )}
                                </div>

                                <div>
                                    <label className="block text-sm font-medium text-gray-300 mb-2">
                                        Company Name
                                    </label>
                                    {isEditing ? (
                                        <input
                                            type="text"
                                            name="companyName"
                                            value={formData.companyName}
                                            onChange={handleInputChange}
                                            className="w-full px-3 py-2 bg-gray-800 border border-gray-700 rounded text-white focus:ring-2 focus:ring-yellow-600 focus:border-transparent"
                                        />
                                    ) : (
                                        <p className="text-white">{user?.companyName}</p>
                                    )}
                                </div>
                            </div>
                        </div>

                        {/* Security Section */}
                        <div className="bg-gray-900 border border-gray-800 rounded-lg mt-6">
                            <div className="p-6 border-b border-gray-800">
                                <h2 className="text-lg font-semibold text-white">Security</h2>
                            </div>
                            <div className="p-6 space-y-4">
                                <div className="flex items-center justify-between">
                                    <div>
                                        <h3 className="text-white font-medium">Password</h3>
                                        <p className="text-gray-400 text-sm">Last updated 30 days ago</p>
                                    </div>
                                    <button className="bg-gray-700 hover:bg-gray-600 text-white px-4 py-2 rounded font-medium transition-colors">
                                        Change Password
                                    </button>
                                </div>

                                <div className="flex items-center justify-between">
                                    <div>
                                        <h3 className="text-white font-medium">Two-Factor Authentication</h3>
                                        <p className="text-gray-400 text-sm">Add an extra layer of security</p>
                                    </div>
                                    <button className="bg-gray-700 hover:bg-gray-600 text-white px-4 py-2 rounded font-medium transition-colors">
                                        Enable 2FA
                                    </button>
                                </div>
                            </div>
                        </div>
                    </div>

                    {/* Sidebar */}
                    <div className="space-y-6">
                        {/* Account Overview */}
                        <div className="bg-gray-900 border border-gray-800 rounded-lg p-6">
                            <h3 className="text-lg font-semibold text-white mb-4">Account Overview</h3>
                            <div className="space-y-4">
                                <div className="flex items-center space-x-3">
                                    <User className="w-5 h-5 text-gray-400" />
                                    <div>
                                        <p className="text-white font-medium">{user?.firstName} {user?.lastName}</p>
                                        <p className="text-gray-400 text-sm">Account Owner</p>
                                    </div>
                                </div>

                                <div className="flex items-center space-x-3">
                                    <Building className="w-5 h-5 text-gray-400" />
                                    <div>
                                        <p className="text-white font-medium">{user?.companyName}</p>
                                        <p className="text-gray-400 text-sm">Organization</p>
                                    </div>
                                </div>

                                <div className="flex items-center space-x-3">
                                    <Calendar className="w-5 h-5 text-gray-400" />
                                    <div>
                                        <p className="text-white font-medium">Member since</p>
                                        <p className="text-gray-400 text-sm">December 2024</p>
                                    </div>
                                </div>
                            </div>
                        </div>

                        {/* Subscription Info */}
                        <div className="bg-gray-900 border border-gray-800 rounded-lg p-6">
                            <h3 className="text-lg font-semibold text-white mb-4">Subscription</h3>
                            <div className="space-y-4">
                                <div className="flex items-center justify-between">
                                    <span className="text-gray-300">Plan</span>
                                    <span className="text-white font-medium">{user?.subscriptionStatus || 'Trial'}</span>
                                </div>

                                {user?.trialEndDate && (
                                    <div className="flex items-center justify-between">
                                        <span className="text-gray-300">Trial Ends</span>
                                        <span className="text-yellow-400 font-medium">
                                            {new Date(user.trialEndDate).toLocaleDateString()}
                                        </span>
                                    </div>
                                )}

                                <button className="w-full bg-yellow-600 hover:bg-yellow-700 text-black px-4 py-2 rounded font-medium transition-colors">
                                    Upgrade Plan
                                </button>
                            </div>
                        </div>

                        {/* Quick Actions */}
                        <div className="bg-gray-900 border border-gray-800 rounded-lg p-6">
                            <h3 className="text-lg font-semibold text-white mb-4">Quick Actions</h3>
                            <div className="space-y-2">
                                <button className="w-full text-left p-3 bg-gray-800 hover:bg-gray-700 rounded transition-colors text-white">
                                    Download Data
                                </button>
                                <button className="w-full text-left p-3 bg-gray-800 hover:bg-gray-700 rounded transition-colors text-white">
                                    API Settings
                                </button>
                                <button
                                    onClick={logout}
                                    className="w-full text-left p-3 bg-red-900 hover:bg-red-800 rounded transition-colors text-red-300"
                                >
                                    Sign Out
                                </button>
                            </div>
                        </div>
                    </div>
                </div>
            </div>
        </div>
    );
};

export default ProfilePage;