import React, { useState, useEffect } from 'react';
import { Settings, User, Bell, Shield, Key, CreditCard, Globe } from 'lucide-react';
import { useAuth } from '../../contexts/AuthContext';
import MfaSettings from '../ui/MfaSettings';

const SettingsPage = ({ defaultTab = 'profile' }) => {
    const { user } = useAuth();
    const [activeTab, setActiveTab] = useState(defaultTab);

    // Check for tab navigation from localStorage
    useEffect(() => {
        if (user) {
            setFormData({
                firstName: user.FirstName || '',
                lastName: user.LastName || '',
                email: user.Email || '',
                companyName: user.CompanyName || ''
            });
        }
    }, [user]);

    const [notifications, setNotifications] = useState({
        assessmentComplete: true,
        weeklyReport: true,
        securityAlerts: true,
        teamInvites: false
    });
    const [formData, setFormData] = useState({
        firstName: user?.FirstName || '',
        lastName: user?.LastName || '',
        email: user?.Email || '',
        companyName: user?.CompanyName || ''
    });
    const [isEditing, setIsEditing] = useState(false);

    const tabs = [
        { id: 'profile', label: 'Profile', icon: User },
        { id: 'notifications', label: 'Notifications', icon: Bell },
        { id: 'security', label: 'Security', icon: Shield },
        { id: 'billing', label: 'Billing', icon: CreditCard },
        { id: 'preferences', label: 'Preferences', icon: Settings }
    ];

    const handleInputChange = (e) => {
        const { name, value } = e.target;
        setFormData(prev => ({
            ...prev,
            [name]: value
        }));
    };

    const handleSave = async () => {
        // TODO: Implement profile update API call
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

    const ProfileTab = () => (
        <div className="space-y-6">
            <div>
                <h3 className="text-lg font-semibold text-white mb-4">Profile Information</h3>
                <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
                    <div>
                        <label className="block text-sm font-medium text-gray-300 mb-2">First Name</label>
                        {isEditing ? (
                            <input
                                type="text"
                                name="firstName"
                                value={formData.firstName}
                                onChange={handleInputChange}
                                className="w-full bg-gray-800 border border-gray-700 rounded px-3 py-2 text-white focus:outline-none focus:border-yellow-600"
                            />
                        ) : (
                                <div className="w-full bg-gray-800 border border-gray-700 rounded px-3 py-2 text-white">
                                    {user?.FirstName || 'Not provided'}
                                </div>
                        )}
                    </div>
                    <div>
                        <label className="block text-sm font-medium text-gray-300 mb-2">Last Name</label>
                        {isEditing ? (
                            <input
                                type="text"
                                name="lastName"
                                value={formData.lastName}
                                onChange={handleInputChange}
                                className="w-full bg-gray-800 border border-gray-700 rounded px-3 py-2 text-white focus:outline-none focus:border-yellow-600"
                            />
                        ) : (
                            <div className="w-full bg-gray-800 border border-gray-700 rounded px-3 py-2 text-white">
                                {user?.LastName || 'Not provided'}
                            </div>
                        )}
                    </div>
                    <div>
                        <label className="block text-sm font-medium text-gray-300 mb-2">Email</label>
                        {isEditing ? (
                            <input
                                type="email"
                                name="email"
                                value={formData.email}
                                onChange={handleInputChange}
                                className="w-full bg-gray-800 border border-gray-700 rounded px-3 py-2 text-white focus:outline-none focus:border-yellow-600"
                            />
                        ) : (
                            <div className="w-full bg-gray-800 border border-gray-700 rounded px-3 py-2 text-white">
                                {user?.email || 'Not provided'}
                            </div>
                        )}
                        {user && !user.emailVerified && (
                            <p className="mt-1 text-sm text-yellow-400">Email not verified</p>
                        )}
                    </div>
                    <div>
                        <label className="block text-sm font-medium text-gray-300 mb-2">Company</label>
                        {isEditing ? (
                            <input
                                type="text"
                                name="companyName"
                                value={formData.companyName}
                                onChange={handleInputChange}
                                className="w-full bg-gray-800 border border-gray-700 rounded px-3 py-2 text-white focus:outline-none focus:border-yellow-600"
                            />
                        ) : (
                            <div className="w-full bg-gray-800 border border-gray-700 rounded px-3 py-2 text-white">
                                {user?.CompanyName || 'Not provided'}
                            </div>
                        )}
                    </div>
                </div>

                <div className="flex justify-end mt-6 space-x-3">
                    {!isEditing ? (
                        <button
                            onClick={() => setIsEditing(true)}
                            className="bg-yellow-600 hover:bg-yellow-700 text-black px-4 py-2 rounded font-medium transition-colors"
                        >
                            Edit Profile
                        </button>
                    ) : (
                        <>
                            <button
                                onClick={handleCancel}
                                className="bg-gray-600 hover:bg-gray-700 text-white px-4 py-2 rounded font-medium transition-colors"
                            >
                                Cancel
                            </button>
                            <button
                                onClick={handleSave}
                                className="bg-green-600 hover:bg-green-700 text-white px-4 py-2 rounded font-medium transition-colors"
                            >
                                Save Changes
                            </button>
                        </>
                    )}
                </div>
            </div>

            {/* Account Status */}
            <div className="border-t border-gray-800 pt-6">
                <h4 className="text-md font-semibold text-white mb-4">Account Status</h4>
                <div className="space-y-3">
                    <div className="flex justify-between items-center">
                        <span className="text-gray-300">Subscription Status</span>
                        <span className={`px-2 py-1 rounded text-sm font-medium ${user?.SubscriptionStatus === 'Active' ? 'bg-green-600 text-white' :
                                user?.SubscriptionStatus === 'Trial' ? 'bg-yellow-600 text-black' :
                                    'bg-gray-600 text-gray-300'
                            }`}>
                            {user?.SubscriptionStatus === 'Active' ? 'Active' :
                                user?.SubscriptionStatus === 'Trial' ? 'Free Trial' :
                                    'Unknown'}
                        </span>
                    </div>
                    {user?.SubscriptionStatus === 'Trial' && user?.TrialEndDate && (
                        <div className="flex justify-between items-center mt-2">
                            <span className="text-gray-300">Trial Expires</span>
                            <span className="text-yellow-400 text-sm">
                                {new Date(user.TrialEndDate).toLocaleDateString()}
                            </span>
                        </div>
                    )}
                    <div className="flex justify-between items-center">
                        <span className="text-gray-300">Email Verified</span>
                        <span className={`px-2 py-1 rounded text-sm font-medium ${user?.emailVerified ? 'bg-green-600 text-white' : 'bg-red-600 text-white'
                            }`}>
                            {user?.emailVerified ? 'Verified' : 'Not Verified'}
                        </span>
                    </div>
                    {user?.trialEndDate && (
                        <div className="flex justify-between items-center">
                            <span className="text-gray-300">Trial End Date</span>
                            <span className="text-white">
                                {new Date(user.trialEndDate).toLocaleDateString()}
                            </span>
                        </div>
                    )}
                </div>
            </div>
        </div>
    );

    const NotificationsTab = () => (
        <div className="space-y-6">
            <div>
                <h3 className="text-lg font-semibold text-white mb-4">Notification Preferences</h3>
                <div className="space-y-4">
                    {Object.entries(notifications).map(([key, value]) => (
                        <div key={key} className="flex items-center justify-between p-4 bg-gray-800 rounded">
                            <div>
                                <h4 className="font-medium text-white capitalize">
                                    {key.replace(/([A-Z])/g, ' $1').trim()}
                                </h4>
                                <p className="text-sm text-gray-400">
                                    {key === 'assessmentComplete' && 'Get notified when assessments finish'}
                                    {key === 'weeklyReport' && 'Receive weekly compliance summaries'}
                                    {key === 'securityAlerts' && 'Important security notifications'}
                                    {key === 'teamInvites' && 'Team member invitation notifications'}
                                </p>
                            </div>
                            <label className="relative inline-flex items-center cursor-pointer">
                                <input
                                    type="checkbox"
                                    checked={value}
                                    onChange={(e) => setNotifications(prev => ({ ...prev, [key]: e.target.checked }))}
                                    className="sr-only peer"
                                />
                                <div className="w-11 h-6 bg-gray-600 peer-focus:outline-none rounded-full peer peer-checked:after:translate-x-full peer-checked:after:border-white after:content-[''] after:absolute after:top-[2px] after:left-[2px] after:bg-white after:rounded-full after:h-5 after:w-5 after:transition-all peer-checked:bg-yellow-600"></div>
                            </label>
                        </div>
                    ))}
                </div>
            </div>
        </div>
    );

    const SecurityTab = () => (
        <div className="space-y-6">
            {/* MFA Settings Component */}
            <MfaSettings />

            {/* Password Change Section */}
            <div className="bg-gray-900 border border-gray-800 rounded p-6">
                <h3 className="text-lg font-semibold text-white mb-4">Password Settings</h3>
                <div className="space-y-4">
                    <div className="p-4 bg-gray-800 rounded">
                        <div className="flex items-center justify-between">
                            <div>
                                <h4 className="font-medium text-white">Password</h4>
                                <p className="text-sm text-gray-400">Last updated 30 days ago</p>
                            </div>
                            <button className="bg-gray-700 hover:bg-gray-600 text-white px-4 py-2 rounded font-medium transition-colors">
                                Change Password
                            </button>
                        </div>
                    </div>
                </div>
            </div>
        </div>
    );

    const BillingTab = () => (
        <div className="space-y-6">
            <div>
                <h3 className="text-lg font-semibold text-white mb-4">Billing Information</h3>
                <div className="space-y-4">
                    <div className="p-4 bg-gray-800 rounded">
                        <h4 className="font-medium text-white mb-2">Current Plan</h4>
                        <p className="text-gray-300">
                            {user?.SubscriptionStatus === 'Trial' ? 'Free Trial' :
                                user?.SubscriptionStatus === 'Active' ? 'Professional Plan' :
                                    'No Active Plan'}
                        </p>
                        {user?.SubscriptionStatus === 'Trial' && user?.TrialEndDate && (
                            <p className="text-sm text-yellow-400 mt-2">
                                Trial expires on {new Date(user.TrialEndDate).toLocaleDateString()}
                            </p>
                        )}
                    </div>
                </div>
            </div>
        </div>
    );

    const PreferencesTab = () => (
        <div className="space-y-6">
            <div>
                <h3 className="text-lg font-semibold text-white mb-4">Application Preferences</h3>
                <div className="space-y-4">
                    <div className="p-4 bg-gray-800 rounded">
                        <div className="flex items-center justify-between">
                            <div>
                                <h4 className="font-medium text-white">Dark Mode</h4>
                                <p className="text-sm text-gray-400">Use dark theme (currently enabled)</p>
                            </div>
                            <label className="relative inline-flex items-center cursor-pointer">
                                <input type="checkbox" checked={true} className="sr-only peer" />
                                <div className="w-11 h-6 bg-gray-600 peer-focus:outline-none rounded-full peer peer-checked:after:translate-x-full peer-checked:after:border-white after:content-[''] after:absolute after:top-[2px] after:left-[2px] after:bg-white after:rounded-full after:h-5 after:w-5 after:transition-all peer-checked:bg-yellow-600"></div>
                            </label>
                        </div>
                    </div>
                </div>
            </div>
        </div>
    );

    const renderTabContent = () => {
        switch (activeTab) {
            case 'profile': return <ProfileTab />;
            case 'notifications': return <NotificationsTab />;
            case 'security': return <SecurityTab />;
            case 'billing': return <BillingTab />;
            case 'preferences': return <PreferencesTab />;
            default: return <ProfileTab />;
        }
    };

    return (
        <div className="space-y-6">
            <div className="bg-gray-950 border border-gray-800 rounded">
                {/* Tab Navigation */}
                <div className="border-b border-gray-800">
                    <nav className="flex space-x-8 px-6">
                        {tabs.map((tab) => (
                            <button
                                key={tab.id}
                                onClick={() => setActiveTab(tab.id)}
                                className={`flex items-center space-x-2 py-4 px-1 border-b-2 font-medium text-sm transition-colors ${activeTab === tab.id
                                    ? 'border-yellow-600 text-yellow-600'
                                    : 'border-transparent text-gray-500 hover:text-gray-300 hover:border-gray-300'
                                    }`}
                            >
                                <tab.icon size={16} />
                                <span>{tab.label}</span>
                            </button>
                        ))}
                    </nav>
                </div>

                {/* Tab Content */}
                <div className="p-6">
                    {renderTabContent()}
                </div>
            </div>
        </div>
    );
};

export default SettingsPage;