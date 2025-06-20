import React, { useState } from 'react';
import { Settings, User, Bell, Shield, Key, CreditCard, Globe } from 'lucide-react';

const SettingsPage = () => {
  const [activeTab, setActiveTab] = useState('profile');
  const [notifications, setNotifications] = useState({
    assessmentComplete: true,
    weeklyReport: true,
    securityAlerts: true,
    teamInvites: false
  });

  const tabs = [
    { id: 'profile', label: 'Profile', icon: User },
    { id: 'notifications', label: 'Notifications', icon: Bell },
    { id: 'security', label: 'Security', icon: Shield },
    { id: 'billing', label: 'Billing', icon: CreditCard },
    { id: 'preferences', label: 'Preferences', icon: Settings }
  ];

  const ProfileTab = () => (
    <div className="space-y-6">
      <div>
        <h3 className="text-lg font-semibold text-white mb-4">Profile Information</h3>
        <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
          <div>
            <label className="block text-sm font-medium text-gray-300 mb-2">Full Name</label>
            <input
              type="text"
              defaultValue="John Smith"
              className="w-full bg-gray-800 border border-gray-700 rounded px-3 py-2 text-white focus:outline-none focus:border-yellow-600"
            />
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-300 mb-2">Email</label>
            <input
              type="email"
              defaultValue="john@safehaven.com"
              className="w-full bg-gray-800 border border-gray-700 rounded px-3 py-2 text-white focus:outline-none focus:border-yellow-600"
            />
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-300 mb-2">Company</label>
            <input
              type="text"
              defaultValue="Safe Haven Technologies"
              className="w-full bg-gray-800 border border-gray-700 rounded px-3 py-2 text-white focus:outline-none focus:border-yellow-600"
            />
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-300 mb-2">Role</label>
            <input
              type="text"
              defaultValue="IT Director"
              className="w-full bg-gray-800 border border-gray-700 rounded px-3 py-2 text-white focus:outline-none focus:border-yellow-600"
            />
          </div>
        </div>
        <div className="mt-6">
          <button className="bg-yellow-600 hover:bg-yellow-700 text-black px-4 py-2 rounded font-medium transition-colors">
            Save Changes
          </button>
        </div>
      </div>
    </div>
  );

  const NotificationsTab = () => (
    <div className="space-y-6">
      <div>
        <h3 className="text-lg font-semibold text-white mb-4">Email Notifications</h3>
        <div className="space-y-4">
          {Object.entries(notifications).map(([key, value]) => (
            <div key={key} className="flex items-center justify-between p-4 bg-gray-800 rounded">
              <div>
                <h4 className="font-medium text-white">
                  {key === 'assessmentComplete' && 'Assessment Completed'}
                  {key === 'weeklyReport' && 'Weekly Summary Report'}
                  {key === 'securityAlerts' && 'Security Alerts'}
                  {key === 'teamInvites' && 'Team Invitations'}
                </h4>
                <p className="text-sm text-gray-400">
                  {key === 'assessmentComplete' && 'Get notified when assessments finish'}
                  {key === 'weeklyReport' && 'Receive weekly compliance reports'}
                  {key === 'securityAlerts' && 'Important security notifications'}
                  {key === 'teamInvites' && 'New team member invitations'}
                </p>
              </div>
              <label className="relative inline-flex items-center cursor-pointer">
                <input
                  type="checkbox"
                  checked={value}
                  onChange={(e) => setNotifications({...notifications, [key]: e.target.checked})}
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
      <div>
        <h3 className="text-lg font-semibold text-white mb-4">Password & Security</h3>
        <div className="space-y-4">
          <div className="p-4 bg-gray-800 rounded">
            <h4 className="font-medium text-white mb-2">Change Password</h4>
            <div className="grid grid-cols-1 gap-4">
              <input
                type="password"
                placeholder="Current password"
                className="bg-gray-700 border border-gray-600 rounded px-3 py-2 text-white focus:outline-none focus:border-yellow-600"
              />
              <input
                type="password"
                placeholder="New password"
                className="bg-gray-700 border border-gray-600 rounded px-3 py-2 text-white focus:outline-none focus:border-yellow-600"
              />
              <input
                type="password"
                placeholder="Confirm new password"
                className="bg-gray-700 border border-gray-600 rounded px-3 py-2 text-white focus:outline-none focus:border-yellow-600"
              />
            </div>
            <button className="mt-4 bg-yellow-600 hover:bg-yellow-700 text-black px-4 py-2 rounded font-medium transition-colors">
              Update Password
            </button>
          </div>

          <div className="p-4 bg-gray-800 rounded">
            <h4 className="font-medium text-white mb-2">Two-Factor Authentication</h4>
            <p className="text-gray-400 text-sm mb-4">Add an extra layer of security to your account</p>
            <button className="bg-green-600 hover:bg-green-700 text-white px-4 py-2 rounded font-medium transition-colors">
              Enable 2FA
            </button>
          </div>
        </div>
      </div>
    </div>
  );

  const BillingTab = () => (
    <div className="space-y-6">
      <div>
        <h3 className="text-lg font-semibold text-white mb-4">Subscription & Billing</h3>
        
        <div className="bg-gray-800 border border-gray-700 rounded p-6 mb-6">
          <div className="flex items-center justify-between mb-4">
            <div>
              <h4 className="text-lg font-semibold text-white">Professional Plan</h4>
              <p className="text-gray-400">$149/month • Billed monthly</p>
            </div>
            <div className="bg-green-600 text-white px-3 py-1 rounded text-sm font-medium">Active</div>
          </div>
          <div className="grid grid-cols-2 md:grid-cols-4 gap-4 text-sm">
            <div>
              <p className="text-gray-400">Resources</p>
              <p className="font-semibold text-white">247 / 1,000</p>
            </div>
            <div>
              <p className="text-gray-400">Assessments</p>
              <p className="font-semibold text-white">Unlimited</p>
            </div>
            <div>
              <p className="text-gray-400">Team Members</p>
              <p className="font-semibold text-white">6 / 10</p>
            </div>
            <div>
              <p className="text-gray-400">Next Billing</p>
              <p className="font-semibold text-white">Jul 15, 2024</p>
            </div>
          </div>
          <div className="mt-4 flex space-x-3">
            <button className="bg-yellow-600 hover:bg-yellow-700 text-black px-4 py-2 rounded font-medium transition-colors">
              Upgrade Plan
            </button>
            <button className="border border-gray-600 hover:border-gray-500 text-white px-4 py-2 rounded transition-colors">
              Manage Billing
            </button>
          </div>
        </div>

        <div className="bg-gray-800 border border-gray-700 rounded p-6">
          <h4 className="font-medium text-white mb-4">Payment Method</h4>
          <div className="flex items-center justify-between p-4 bg-gray-700 rounded mb-4">
            <div className="flex items-center space-x-3">
              <div className="w-8 h-6 bg-blue-600 rounded"></div>
              <span className="text-white">•••• •••• •••• 4242</span>
              <span className="text-gray-400">Expires 12/25</span>
            </div>
            <button className="text-yellow-400 hover:text-yellow-300 text-sm">Update</button>
          </div>
          <button className="text-yellow-400 hover:text-yellow-300 text-sm">+ Add payment method</button>
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
            <h4 className="font-medium text-white mb-2">Default Assessment Type</h4>
            <select className="w-full bg-gray-700 border border-gray-600 rounded px-3 py-2 text-white focus:outline-none focus:border-yellow-600">
              <option value="Full">Full Assessment</option>
              <option value="NamingConvention">Naming Conventions Only</option>
              <option value="Tagging">Tagging Compliance Only</option>
            </select>
          </div>

          <div className="p-4 bg-gray-800 rounded">
            <h4 className="font-medium text-white mb-2">Time Zone</h4>
            <select className="w-full bg-gray-700 border border-gray-600 rounded px-3 py-2 text-white focus:outline-none focus:border-yellow-600">
              <option value="UTC-5">Eastern Time (UTC-5)</option>
              <option value="UTC-6">Central Time (UTC-6)</option>
              <option value="UTC-7">Mountain Time (UTC-7)</option>
              <option value="UTC-8">Pacific Time (UTC-8)</option>
            </select>
          </div>

          <div className="p-4 bg-gray-800 rounded">
            <h4 className="font-medium text-white mb-2">Date Format</h4>
            <select className="w-full bg-gray-700 border border-gray-600 rounded px-3 py-2 text-white focus:outline-none focus:border-yellow-600">
              <option value="MM/DD/YYYY">MM/DD/YYYY</option>
              <option value="DD/MM/YYYY">DD/MM/YYYY</option>
              <option value="YYYY-MM-DD">YYYY-MM-DD</option>
            </select>
          </div>

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
                className={`flex items-center space-x-2 py-4 px-1 border-b-2 font-medium text-sm transition-colors ${
                  activeTab === tab.id
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