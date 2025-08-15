import React, { useState, useEffect } from 'react';
import { Settings, User, Bell, Shield, Eye, Accessibility, Monitor, Clock, Globe, Mail, Smartphone, Laptop, History, Key } from 'lucide-react';
import { useAuth } from '../../contexts/AuthContext';
import { TIMEZONES } from '../../contexts/TimezoneContext';
import MfaSettings from '../ui/MfaSettings';
import { apiClient } from '../../services/apiService';

const SettingsPage = ({ defaultTab = 'profile' }) => {
    const { user } = useAuth();
    const [activeTab, setActiveTab] = useState(defaultTab);

    // Profile state
    const [formData, setFormData] = useState({
        firstName: user?.FirstName || '',
        lastName: user?.LastName || '',
        email: user?.Email || '',
        timezone: user?.Timezone || 'America/New_York'
    });
    const [isEditing, setIsEditing] = useState(false);

    // Notifications state
    const [notifications, setNotifications] = useState({
        assessmentComplete: { email: true, inApp: true, push: false },
        assessmentStarted: { email: false, inApp: true, push: false },
        assessmentFailed: { email: true, inApp: true, push: true },
        weeklyReport: { email: true, inApp: false, push: false },
        securityAlerts: { email: true, inApp: true, push: true },
        teamInvites: { email: true, inApp: true, push: false },
        clientUpdates: { email: false, inApp: true, push: false }
    });

    // Preferences state
    const [preferences, setPreferences] = useState({
        dashboardLayout: 'detailed', // compact, detailed
        assessmentView: 'cards', // cards, table, list
        clientPageLayout: 'grid', // grid, list
        autoRefresh: 'medium', // off, low, medium, high
        defaultAssessmentType: 'full', // full, naming, tagging
        dateFormat: 'MM/dd/yyyy', // MM/dd/yyyy, dd/MM/yyyy, yyyy-MM-dd
        timeFormat: '12h', // 12h, 24h
        showTimezone: true,
        theme: 'dark' // dark, light
    });

    // Security state
    const [loginHistoryRange, setLoginHistoryRange] = useState('7days');
    const [loginHistory, setLoginHistory] = useState([]);
    const [loadingHistory, setLoadingHistory] = useState(false);

    // Accessibility state
    const [accessibility, setAccessibility] = useState({
        highContrast: false,
        colorBlindMode: 'none', // none, protanopia, deuteranopia, tritanopia
        fontSize: 'medium', // small, medium, large, xl
        screenReader: false,
        keyboardNavigation: true,
        reduceMotion: false
    });

    // Load login history when tab changes or range changes
    useEffect(() => {
        if (activeTab === 'security') {
            loadLoginHistory();
        }
    }, [activeTab, loginHistoryRange]);

    useEffect(() => {
        if (user) {
            setFormData({
                firstName: user.FirstName || '',
                lastName: user.LastName || '',
                email: user.Email || '',
                timezone: user.Timezone || 'America/New_York'
            });
        }
    }, [user]);

    // Fallback mock data when API is not available
    const generateMockLoginHistory = (range) => {
        const baseSessions = [
            { loginActivityId: '1', location: 'New York, NY', deviceInfo: 'Chrome on Windows', timeAgo: '2 hours ago', isCurrentSession: true, ipAddress: '192.168.1.100' },
            { loginActivityId: '2', location: 'New York, NY', deviceInfo: 'Safari on iPhone', timeAgo: '1 day ago', isCurrentSession: false, ipAddress: '192.168.1.101' },
            { loginActivityId: '3', location: 'Boston, MA', deviceInfo: 'Chrome on Windows', timeAgo: '3 days ago', isCurrentSession: false, ipAddress: '10.0.0.50' }
        ];

        if (range === '1day') return baseSessions.slice(0, 1);
        if (range === '7days') return baseSessions;
        if (range === '30days') return [
            ...baseSessions,
            { loginActivityId: '4', location: 'San Francisco, CA', deviceInfo: 'Firefox on Mac', timeAgo: '1 week ago', isCurrentSession: false, ipAddress: '172.16.0.10' },
            { loginActivityId: '5', location: 'New York, NY', deviceInfo: 'Edge on Windows', timeAgo: '2 weeks ago', isCurrentSession: false, ipAddress: '192.168.1.100' }
        ];
        return [
            ...baseSessions,
            { loginActivityId: '4', location: 'San Francisco, CA', deviceInfo: 'Firefox on Mac', timeAgo: '1 week ago', isCurrentSession: false, ipAddress: '172.16.0.10' },
            { loginActivityId: '5', location: 'New York, NY', deviceInfo: 'Edge on Windows', timeAgo: '2 weeks ago', isCurrentSession: false, ipAddress: '192.168.1.100' },
            { loginActivityId: '6', location: 'Chicago, IL', deviceInfo: 'Chrome on Android', timeAgo: '1 month ago', isCurrentSession: false, ipAddress: '203.0.113.5' },
            { loginActivityId: '7', location: 'Miami, FL', deviceInfo: 'Safari on iPad', timeAgo: '2 months ago', isCurrentSession: false, ipAddress: '198.51.100.25' }
        ];
    };

    const loadLoginHistory = async () => {
        try {
            setLoadingHistory(true);

            const days = {
                '1day': 1,
                '7days': 7,
                '30days': 30,
                '90days': 90
            }[loginHistoryRange] || 7;

            // Use the imported apiClient directly
            const response = await apiClient.get(`/Account/login-history?days=${days}`);

            console.log('Login history response:', response.data);

            // Backend returns PascalCase, so use LoginHistory not loginHistory
            const loginHistoryData = response.data.LoginHistory || response.data.loginHistory || [];
            setLoginHistory(loginHistoryData);
        } catch (error) {
            console.error('Failed to load login history:', error);
            // Fall back to mock data if API fails
            setLoginHistory(generateMockLoginHistory(loginHistoryRange));
        } finally {
            setLoadingHistory(false);
        }
    };

    const handleRevokeSession = async (loginActivityId) => {
        try {
            await apiClient.post('/Account/revoke-session', { loginActivityId });

            // Reload login history to reflect changes
            await loadLoginHistory();
        } catch (error) {
            console.error('Failed to revoke session:', error);
            alert('Failed to revoke session. Please try again.');
        }
    };

    // Helper function to calculate time ago from timestamp
    const getTimeAgo = (dateTimeString) => {
        if (!dateTimeString) return 'Unknown time';

        try {
            const loginTime = new Date(dateTimeString);
            const now = new Date();
            const diffMs = now - loginTime;

            if (diffMs < 60000) { // Less than 1 minute
                return 'Just now';
            } else if (diffMs < 3600000) { // Less than 1 hour
                const minutes = Math.floor(diffMs / 60000);
                return `${minutes} minute${minutes !== 1 ? 's' : ''} ago`;
            } else if (diffMs < 86400000) { // Less than 1 day
                const hours = Math.floor(diffMs / 3600000);
                return `${hours} hour${hours !== 1 ? 's' : ''} ago`;
            } else if (diffMs < 604800000) { // Less than 1 week
                const days = Math.floor(diffMs / 86400000);
                return `${days} day${days !== 1 ? 's' : ''} ago`;
            } else {
                const weeks = Math.floor(diffMs / 604800000);
                return `${weeks} week${weeks !== 1 ? 's' : ''} ago`;
            }
        } catch (error) {
            console.error('Error calculating time ago:', error);
            return 'Unknown time';
        }
    };

    const tabs = [
        { id: 'profile', label: 'Profile', icon: User },
        { id: 'notifications', label: 'Notifications', icon: Bell },
        { id: 'security', label: 'Security', icon: Shield },
        { id: 'preferences', label: 'Preferences', icon: Settings },
        { id: 'accessibility', label: 'Accessibility', icon: Accessibility }
    ];

    // Use TIMEZONES from TimezoneContext instead of local array
    const timezones = TIMEZONES;

    const handleInputChange = (e) => {
        const { name, value } = e.target;
        setFormData(prev => ({ ...prev, [name]: value }));
    };

    const handleSave = async () => {
        try {
            // Prepare update request matching AccountController.UpdateProfileRequest
            const updateRequest = {
                companyName: formData.firstName && formData.lastName ? null : user?.CompanyName, // Don't change company name
                contactName: `${formData.firstName} ${formData.lastName}`.trim(),
                contactEmail: formData.email,
                timeZone: formData.timezone
            };

            console.log('Updating profile with:', updateRequest);
            
            // Call the backend API
            const response = await apiClient.put('/Account/profile', updateRequest);
            
            console.log('Profile update response:', response.data);
            
            // Update the user context with new data
            // The user object should be refreshed on next page load or we could trigger a refresh
            
            setIsEditing(false);
            
            // Show success message
            alert('Profile updated successfully! Your timezone preference has been saved.');
            
        } catch (error) {
            console.error('Failed to update profile:', error);
            alert('Failed to update profile. Please try again.');
        }
    };

    const handleCancel = () => {
        setFormData({
            firstName: user?.FirstName || '',
            lastName: user?.LastName || '',
            email: user?.Email || '',
            timezone: user?.Timezone || 'America/New_York'
        });
        setIsEditing(false);
    };

    const updateNotification = (type, channel, value) => {
        setNotifications(prev => ({
            ...prev,
            [type]: { ...prev[type], [channel]: value }
        }));
    };

    const ProfileTab = () => (
        <div className="space-y-6">
            <div className="bg-gray-900 border border-gray-800 rounded p-6">
                <div className="flex items-center justify-between mb-6">
                    <h3 className="text-lg font-semibold text-white">Profile Information</h3>
                    {!isEditing ? (
                        <button
                            onClick={() => setIsEditing(true)}
                            className="bg-yellow-600 hover:bg-yellow-700 text-black px-4 py-2 rounded font-medium transition-colors"
                        >
                            Edit Profile
                        </button>
                    ) : (
                        <div className="flex space-x-3">
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
                        </div>
                    )}
                </div>

                <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
                    <div>
                        <label className="block text-sm font-medium text-gray-300 mb-2">First Name</label>
                        <input
                            type="text"
                            name="firstName"
                            value={formData.firstName}
                            onChange={handleInputChange}
                            disabled={!isEditing}
                            className={`w-full border rounded px-3 py-2 text-white transition-colors ${isEditing
                                ? 'bg-gray-800 border-gray-700 focus:outline-none focus:border-yellow-600'
                                : 'bg-gray-800/50 border-gray-700/50 cursor-default'
                                }`}
                        />
                    </div>
                    <div>
                        <label className="block text-sm font-medium text-gray-300 mb-2">Last Name</label>
                        <input
                            type="text"
                            name="lastName"
                            value={formData.lastName}
                            onChange={handleInputChange}
                            disabled={!isEditing}
                            className={`w-full border rounded px-3 py-2 text-white transition-colors ${isEditing
                                ? 'bg-gray-800 border-gray-700 focus:outline-none focus:border-yellow-600'
                                : 'bg-gray-800/50 border-gray-700/50 cursor-default'
                                }`}
                        />
                    </div>
                    <div>
                        <label className="block text-sm font-medium text-gray-300 mb-2">Email</label>
                        <input
                            type="email"
                            name="email"
                            value={formData.email}
                            onChange={handleInputChange}
                            disabled={!isEditing}
                            className={`w-full border rounded px-3 py-2 text-white transition-colors ${isEditing
                                ? 'bg-gray-800 border-gray-700 focus:outline-none focus:border-yellow-600'
                                : 'bg-gray-800/50 border-gray-700/50 cursor-default'
                                }`}
                        />
                        {user && !user.emailVerified && (
                            <p className="mt-1 text-sm text-yellow-400">Email not verified</p>
                        )}
                    </div>
                    <div>
                        <label className="block text-sm font-medium text-gray-300 mb-2">Timezone</label>
                        <select
                            name="timezone"
                            value={formData.timezone}
                            onChange={handleInputChange}
                            disabled={!isEditing}
                            className={`w-full border rounded px-3 py-2 text-white transition-colors ${isEditing
                                ? 'bg-gray-800 border-gray-700 focus:outline-none focus:border-yellow-600'
                                : 'bg-gray-800/50 border-gray-700/50 cursor-default'
                                }`}
                        >
                            {timezones.map(tz => (
                                <option key={tz.value} value={tz.value}>{tz.label}</option>
                            ))}
                        </select>
                    </div>
                </div>
            </div>

            {/* Account Status */}
            <div className="bg-gray-900 border border-gray-800 rounded p-6">
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
                    <div className="flex justify-between items-center">
                        <span className="text-gray-300">Email Verified</span>
                        <span className={`px-2 py-1 rounded text-sm font-medium ${user?.emailVerified ? 'bg-green-600 text-white' : 'bg-red-600 text-white'
                            }`}>
                            {user?.emailVerified ? 'Verified' : 'Not Verified'}
                        </span>
                    </div>
                </div>
            </div>
        </div>
    );

    const NotificationsTab = () => (
        <div className="space-y-6">
            <div className="bg-gray-900 border border-gray-800 rounded p-6">
                <h3 className="text-lg font-semibold text-white mb-6">Notification Preferences</h3>

                <div className="space-y-4">
                    {Object.entries(notifications).map(([key, channels]) => (
                        <div key={key} className="bg-gray-800 rounded p-4">
                            <div className="flex items-start justify-between mb-4">
                                <div>
                                    <h4 className="font-medium text-white capitalize mb-1">
                                        {key.replace(/([A-Z])/g, ' $1').trim()}
                                    </h4>
                                    <p className="text-sm text-gray-400">
                                        {key === 'assessmentComplete' && 'When assessments finish processing'}
                                        {key === 'assessmentStarted' && 'When assessments begin processing'}
                                        {key === 'assessmentFailed' && 'When assessments encounter errors'}
                                        {key === 'weeklyReport' && 'Weekly compliance summary reports'}
                                        {key === 'securityAlerts' && 'Important security notifications'}
                                        {key === 'teamInvites' && 'Team member invitation notifications'}
                                        {key === 'clientUpdates' && 'Client configuration changes'}
                                    </p>
                                </div>
                            </div>

                            <div className="grid grid-cols-3 gap-4">
                                {['email', 'inApp', 'push'].map(channel => (
                                    <div key={channel} className="flex items-center justify-between p-3 bg-gray-700 rounded">
                                        <div className="flex items-center space-x-2">
                                            {channel === 'email' && <Mail size={16} className="text-gray-400" />}
                                            {channel === 'inApp' && <Monitor size={16} className="text-gray-400" />}
                                            {channel === 'push' && <Smartphone size={16} className="text-gray-400" />}
                                            <span className="text-sm text-gray-300 capitalize">{channel === 'inApp' ? 'In-App' : channel}</span>
                                        </div>
                                        <label className="relative inline-flex items-center cursor-pointer">
                                            <input
                                                type="checkbox"
                                                checked={channels[channel]}
                                                onChange={(e) => updateNotification(key, channel, e.target.checked)}
                                                className="sr-only peer"
                                            />
                                            <div className="w-11 h-6 bg-gray-600 peer-focus:outline-none rounded-full peer peer-checked:after:translate-x-full peer-checked:after:border-white after:content-[''] after:absolute after:top-[2px] after:left-[2px] after:bg-white after:rounded-full after:h-5 after:w-5 after:transition-all peer-checked:bg-yellow-600"></div>
                                        </label>
                                    </div>
                                ))}
                            </div>
                        </div>
                    ))}
                </div>
            </div>
        </div>
    );

    const SecurityTab = () => (
        <div className="space-y-6">
            {/* MFA Settings */}
            <MfaSettings />

            {/* Password Settings */}
            <div className="bg-gray-900 border border-gray-800 rounded p-6">
                <h3 className="text-lg font-semibold text-white mb-4 flex items-center space-x-2">
                    <Key size={20} className="text-yellow-600" />
                    <span>Password Settings</span>
                </h3>
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

            {/* Login History */}
            <div className="bg-gray-900 border border-gray-800 rounded p-6">
                <div className="flex items-center justify-between mb-4">
                    <h3 className="text-lg font-semibold text-white flex items-center space-x-2">
                        <History size={20} className="text-yellow-600" />
                        <span>Recent Login Activity</span>
                    </h3>
                    <select
                        value={loginHistoryRange}
                        onChange={(e) => setLoginHistoryRange(e.target.value)}
                        className="bg-gray-800 border border-gray-700 rounded px-3 py-2 text-white text-sm focus:outline-none focus:border-yellow-600"
                    >
                        <option value="1day">Last 24 hours</option>
                        <option value="7days">Last 7 days</option>
                        <option value="30days">Last 30 days</option>
                        <option value="90days">Last 90 days</option>
                    </select>
                </div>
                <div className="space-y-3">
                    {loadingHistory ? (
                        <div className="flex items-center justify-center py-8">
                            <div className="animate-spin rounded-full h-6 w-6 border-b-2 border-yellow-600"></div>
                            <span className="ml-2 text-gray-400">Loading login history...</span>
                        </div>
                    ) : loginHistory.length > 0 ? (
                        loginHistory.map((session) => (
                            <div key={session.LoginActivityId || session.loginActivityId || session.id} className="p-4 bg-gray-800 rounded flex items-center justify-between">
                                <div className="flex items-center space-x-3">
                                    <Laptop size={16} className="text-gray-400" />
                                    <div>
                                        <p className="text-white font-medium">
                                            {session.DeviceInfo || session.deviceInfo || `${session.Browser || session.browser || 'Unknown'} on ${session.OperatingSystem || session.operatingSystem || 'Unknown'}`}
                                        </p>
                                        <p className="text-sm text-gray-400">
                                            {session.Location || session.location || session.LocationDisplay || session.locationDisplay || 'Unknown Location'} � {session.TimeAgo || session.timeAgo || getTimeAgo(session.LoginTime || session.loginTime)}
                                        </p>
                                        <p className="text-xs text-gray-500">IP: {session.IpAddress || session.ipAddress || 'Unknown'}</p>
                                    </div>
                                    {(session.IsCurrentSession || session.isCurrentSession) && (
                                        <span className="bg-green-600 text-white px-2 py-1 rounded text-xs font-medium">Current</span>
                                    )}
                                    {(session.SuspiciousActivity || session.suspiciousActivity) && (
                                        <span className="bg-red-600 text-white px-2 py-1 rounded text-xs font-medium">Suspicious</span>
                                    )}
                                </div>
                                {!(session.IsCurrentSession || session.isCurrentSession) && (
                                    <button
                                        onClick={() => handleRevokeSession(session.LoginActivityId || session.loginActivityId)}
                                        className="text-red-400 hover:text-red-300 text-sm transition-colors"
                                    >
                                        Revoke
                                    </button>
                                )}
                            </div>
                        ))
                    ) : (
                        <div className="text-center py-8 text-gray-400">
                            <Laptop size={48} className="mx-auto mb-2 opacity-50" />
                            <p>No login activity found for the selected time range</p>
                        </div>
                    )}
                </div>
            </div>
        </div>
    );

    const PreferencesTab = () => (
        <div className="space-y-6">
            {/* Appearance Settings */}
            <div className="bg-gray-900 border border-gray-800 rounded p-6">
                <h3 className="text-lg font-semibold text-white mb-4">Appearance</h3>
                <div className="space-y-4">
                    <div className="p-4 bg-gray-800 rounded">
                        <label className="block text-sm font-medium text-gray-300 mb-2">Theme</label>
                        <select
                            value={preferences.theme}
                            onChange={(e) => setPreferences(prev => ({ ...prev, theme: e.target.value }))}
                            className="w-full bg-gray-700 border border-gray-600 rounded px-3 py-2 text-white focus:outline-none focus:border-yellow-600"
                        >
                            <option value="dark">Dark Mode</option>
                            <option value="light">Light Mode</option>
                            <option value="auto">Auto (System)</option>
                        </select>
                        <p className="text-sm text-gray-400 mt-2">Choose your preferred color theme</p>
                    </div>
                </div>
            </div>

            {/* Layout Preferences */}
            <div className="bg-gray-900 border border-gray-800 rounded p-6">
                <h3 className="text-lg font-semibold text-white mb-4">Layout Preferences</h3>
                <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
                    <div>
                        <label className="block text-sm font-medium text-gray-300 mb-2">Dashboard Layout</label>
                        <select
                            value={preferences.dashboardLayout}
                            onChange={(e) => setPreferences(prev => ({ ...prev, dashboardLayout: e.target.value }))}
                            className="w-full bg-gray-800 border border-gray-700 rounded px-3 py-2 text-white focus:outline-none focus:border-yellow-600"
                        >
                            <option value="compact">Compact View</option>
                            <option value="detailed">Detailed View</option>
                        </select>
                    </div>
                    <div>
                        <label className="block text-sm font-medium text-gray-300 mb-2">Assessment View</label>
                        <select
                            value={preferences.assessmentView}
                            onChange={(e) => setPreferences(prev => ({ ...prev, assessmentView: e.target.value }))}
                            className="w-full bg-gray-800 border border-gray-700 rounded px-3 py-2 text-white focus:outline-none focus:border-yellow-600"
                        >
                            <option value="cards">Cards</option>
                            <option value="table">Table</option>
                            <option value="list">List</option>
                        </select>
                    </div>
                    <div>
                        <label className="block text-sm font-medium text-gray-300 mb-2">Client Page Layout</label>
                        <select
                            value={preferences.clientPageLayout}
                            onChange={(e) => setPreferences(prev => ({ ...prev, clientPageLayout: e.target.value }))}
                            className="w-full bg-gray-800 border border-gray-700 rounded px-3 py-2 text-white focus:outline-none focus:border-yellow-600"
                        >
                            <option value="grid">Grid View</option>
                            <option value="list">List View</option>
                        </select>
                    </div>
                    <div>
                        <label className="block text-sm font-medium text-gray-300 mb-2">Auto-Refresh Interval</label>
                        <select
                            value={preferences.autoRefresh}
                            onChange={(e) => setPreferences(prev => ({ ...prev, autoRefresh: e.target.value }))}
                            className="w-full bg-gray-800 border border-gray-700 rounded px-3 py-2 text-white focus:outline-none focus:border-yellow-600"
                        >
                            <option value="off">Disabled</option>
                            <option value="low">Low (5 minutes)</option>
                            <option value="medium">Medium (2 minutes)</option>
                            <option value="high">High (30 seconds)</option>
                        </select>
                    </div>
                </div>
            </div>

            {/* Default Settings */}
            <div className="bg-gray-900 border border-gray-800 rounded p-6">
                <h3 className="text-lg font-semibold text-white mb-4">Default Settings</h3>
                <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
                    <div>
                        <label className="block text-sm font-medium text-gray-300 mb-2">Default Assessment Type</label>
                        <select
                            value={preferences.defaultAssessmentType}
                            onChange={(e) => setPreferences(prev => ({ ...prev, defaultAssessmentType: e.target.value }))}
                            className="w-full bg-gray-800 border border-gray-700 rounded px-3 py-2 text-white focus:outline-none focus:border-yellow-600"
                        >
                            <option value="full">Full Assessment</option>
                            <option value="naming">Naming Convention Only</option>
                            <option value="tagging">Tagging Only</option>
                        </select>
                    </div>
                </div>
            </div>

            {/* Display Preferences */}
            <div className="bg-gray-900 border border-gray-800 rounded p-6">
                <h3 className="text-lg font-semibold text-white mb-4">Display Preferences</h3>
                <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
                    <div>
                        <label className="block text-sm font-medium text-gray-300 mb-2">Date Format</label>
                        <select
                            value={preferences.dateFormat}
                            onChange={(e) => setPreferences(prev => ({ ...prev, dateFormat: e.target.value }))}
                            className="w-full bg-gray-800 border border-gray-700 rounded px-3 py-2 text-white focus:outline-none focus:border-yellow-600"
                        >
                            <option value="MM/dd/yyyy">MM/DD/YYYY</option>
                            <option value="dd/MM/yyyy">DD/MM/YYYY</option>
                            <option value="yyyy-MM-dd">YYYY-MM-DD</option>
                        </select>
                    </div>
                    <div>
                        <label className="block text-sm font-medium text-gray-300 mb-2">Time Format</label>
                        <select
                            value={preferences.timeFormat}
                            onChange={(e) => setPreferences(prev => ({ ...prev, timeFormat: e.target.value }))}
                            className="w-full bg-gray-800 border border-gray-700 rounded px-3 py-2 text-white focus:outline-none focus:border-yellow-600"
                        >
                            <option value="12h">12-hour (AM/PM)</option>
                            <option value="24h">24-hour</option>
                        </select>
                    </div>
                    <div className="flex items-center justify-between p-4 bg-gray-800 rounded">
                        <div>
                            <h4 className="font-medium text-white">Show Timezone</h4>
                            <p className="text-sm text-gray-400">Display timezone in timestamps</p>
                        </div>
                        <label className="relative inline-flex items-center cursor-pointer">
                            <input
                                type="checkbox"
                                checked={preferences.showTimezone}
                                onChange={(e) => setPreferences(prev => ({ ...prev, showTimezone: e.target.checked }))}
                                className="sr-only peer"
                            />
                            <div className="w-11 h-6 bg-gray-600 peer-focus:outline-none rounded-full peer peer-checked:after:translate-x-full peer-checked:after:border-white after:content-[''] after:absolute after:top-[2px] after:left-[2px] after:bg-white after:rounded-full after:h-5 after:w-5 after:transition-all peer-checked:bg-yellow-600"></div>
                        </label>
                    </div>
                </div>
            </div>
        </div>
    );

    const AccessibilityTab = () => (
        <div className="space-y-6">
            {/* Visual Accessibility */}
            <div className="bg-gray-900 border border-gray-800 rounded p-6">
                <h3 className="text-lg font-semibold text-white mb-4 flex items-center space-x-2">
                    <Eye size={20} className="text-yellow-600" />
                    <span>Visual Accessibility</span>
                </h3>
                <div className="space-y-4">
                    <div className="flex items-center justify-between p-4 bg-gray-800 rounded">
                        <div>
                            <h4 className="font-medium text-white">High Contrast Mode</h4>
                            <p className="text-sm text-gray-400">Enhanced contrast for better visibility</p>
                        </div>
                        <label className="relative inline-flex items-center cursor-pointer">
                            <input
                                type="checkbox"
                                checked={accessibility.highContrast}
                                onChange={(e) => setAccessibility(prev => ({ ...prev, highContrast: e.target.checked }))}
                                className="sr-only peer"
                            />
                            <div className="w-11 h-6 bg-gray-600 peer-focus:outline-none rounded-full peer peer-checked:after:translate-x-full peer-checked:after:border-white after:content-[''] after:absolute after:top-[2px] after:left-[2px] after:bg-white after:rounded-full after:h-5 after:w-5 after:transition-all peer-checked:bg-yellow-600"></div>
                        </label>
                    </div>

                    <div className="p-4 bg-gray-800 rounded">
                        <label className="block text-sm font-medium text-gray-300 mb-2">Color Blind Support</label>
                        <select
                            value={accessibility.colorBlindMode}
                            onChange={(e) => setAccessibility(prev => ({ ...prev, colorBlindMode: e.target.value }))}
                            className="w-full bg-gray-700 border border-gray-600 rounded px-3 py-2 text-white focus:outline-none focus:border-yellow-600"
                        >
                            <option value="none">No Adjustment</option>
                            <option value="protanopia">Protanopia (Red-Blind)</option>
                            <option value="deuteranopia">Deuteranopia (Green-Blind)</option>
                            <option value="tritanopia">Tritanopia (Blue-Blind)</option>
                        </select>
                    </div>

                    <div className="p-4 bg-gray-800 rounded">
                        <label className="block text-sm font-medium text-gray-300 mb-2">Font Size</label>
                        <select
                            value={accessibility.fontSize}
                            onChange={(e) => setAccessibility(prev => ({ ...prev, fontSize: e.target.value }))}
                            className="w-full bg-gray-700 border border-gray-600 rounded px-3 py-2 text-white focus:outline-none focus:border-yellow-600"
                        >
                            <option value="small">Small</option>
                            <option value="medium">Medium (Default)</option>
                            <option value="large">Large</option>
                            <option value="xl">Extra Large</option>
                        </select>
                    </div>
                </div>
            </div>

            {/* Interaction Accessibility */}
            <div className="bg-gray-900 border border-gray-800 rounded p-6">
                <h3 className="text-lg font-semibold text-white mb-4">Interaction Preferences</h3>
                <div className="space-y-4">
                    <div className="flex items-center justify-between p-4 bg-gray-800 rounded">
                        <div>
                            <h4 className="font-medium text-white">Screen Reader Optimization</h4>
                            <p className="text-sm text-gray-400">Enhanced accessibility for screen readers</p>
                        </div>
                        <label className="relative inline-flex items-center cursor-pointer">
                            <input
                                type="checkbox"
                                checked={accessibility.screenReader}
                                onChange={(e) => setAccessibility(prev => ({ ...prev, screenReader: e.target.checked }))}
                                className="sr-only peer"
                            />
                            <div className="w-11 h-6 bg-gray-600 peer-focus:outline-none rounded-full peer peer-checked:after:translate-x-full peer-checked:after:border-white after:content-[''] after:absolute after:top-[2px] after:left-[2px] after:bg-white after:rounded-full after:h-5 after:w-5 after:transition-all peer-checked:bg-yellow-600"></div>
                        </label>
                    </div>

                    <div className="flex items-center justify-between p-4 bg-gray-800 rounded">
                        <div>
                            <h4 className="font-medium text-white">Enhanced Keyboard Navigation</h4>
                            <p className="text-sm text-gray-400">Improved keyboard shortcuts and focus indicators</p>
                        </div>
                        <label className="relative inline-flex items-center cursor-pointer">
                            <input
                                type="checkbox"
                                checked={accessibility.keyboardNavigation}
                                onChange={(e) => setAccessibility(prev => ({ ...prev, keyboardNavigation: e.target.checked }))}
                                className="sr-only peer"
                            />
                            <div className="w-11 h-6 bg-gray-600 peer-focus:outline-none rounded-full peer peer-checked:after:translate-x-full peer-checked:after:border-white after:content-[''] after:absolute after:top-[2px] after:left-[2px] after:bg-white after:rounded-full after:h-5 after:w-5 after:transition-all peer-checked:bg-yellow-600"></div>
                        </label>
                    </div>

                    <div className="flex items-center justify-between p-4 bg-gray-800 rounded">
                        <div>
                            <h4 className="font-medium text-white">Reduce Motion</h4>
                            <p className="text-sm text-gray-400">Minimize animations and transitions</p>
                        </div>
                        <label className="relative inline-flex items-center cursor-pointer">
                            <input
                                type="checkbox"
                                checked={accessibility.reduceMotion}
                                onChange={(e) => setAccessibility(prev => ({ ...prev, reduceMotion: e.target.checked }))}
                                className="sr-only peer"
                            />
                            <div className="w-11 h-6 bg-gray-600 peer-focus:outline-none rounded-full peer peer-checked:after:translate-x-full peer-checked:after:border-white after:content-[''] after:absolute after:top-[2px] after:left-[2px] after:bg-white after:rounded-full after:h-5 after:w-5 after:transition-all peer-checked:bg-yellow-600"></div>
                        </label>
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
            case 'preferences': return <PreferencesTab />;
            case 'accessibility': return <AccessibilityTab />;
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