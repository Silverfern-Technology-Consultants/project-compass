import React from 'react';
import { Settings, Clock, Building2, CreditCard, Shield, Bell } from 'lucide-react';

const CompanySettingsPage = () => {
    return (
        <div className="space-y-6">
            {/* Header */}
            <div className="flex items-center justify-between">
                <div>
                    <h1 className="text-2xl font-bold text-white">Company Settings</h1>
                    <p className="text-gray-400">Manage your organization settings and preferences</p>
                </div>
            </div>

            {/* Coming Soon Card */}
            <div className="bg-gray-900 border border-gray-800 rounded p-12 text-center">
                <div className="max-w-md mx-auto">
                    <div className="w-16 h-16 bg-yellow-600 rounded-full flex items-center justify-center mx-auto mb-6">
                        <Settings size={32} className="text-black" />
                    </div>

                    <h2 className="text-xl font-semibold text-white mb-3">Company Settings Coming Soon</h2>
                    <p className="text-gray-400 mb-6">
                        We're developing comprehensive organization-level settings to give you
                        complete control over your MSP's Compass configuration.
                    </p>

                    {/* Feature Preview */}
                    <div className="bg-gray-800 border border-gray-700 rounded p-4 text-left">
                        <h3 className="text-sm font-semibold text-white mb-3">What's Coming:</h3>
                        <div className="space-y-2 text-sm text-gray-300">
                            <div className="flex items-center space-x-2">
                                <div className="w-1.5 h-1.5 bg-yellow-600 rounded-full"></div>
                                <span>Organization profile and branding</span>
                            </div>
                            <div className="flex items-center space-x-2">
                                <div className="w-1.5 h-1.5 bg-yellow-600 rounded-full"></div>
                                <span>Billing and subscription management</span>
                            </div>
                            <div className="flex items-center space-x-2">
                                <div className="w-1.5 h-1.5 bg-yellow-600 rounded-full"></div>
                                <span>Security and compliance settings</span>
                            </div>
                            <div className="flex items-center space-x-2">
                                <div className="w-1.5 h-1.5 bg-yellow-600 rounded-full"></div>
                                <span>Notification preferences</span>
                            </div>
                            <div className="flex items-center space-x-2">
                                <div className="w-1.5 h-1.5 bg-yellow-600 rounded-full"></div>
                                <span>Default assessment preferences</span>
                            </div>
                        </div>
                    </div>

                    <div className="mt-6 flex items-center justify-center space-x-2 text-sm text-gray-500">
                        <Clock size={16} />
                        <span>Currently in development</span>
                    </div>
                </div>
            </div>

            {/* Settings Categories Preview */}
            <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
                <div className="bg-gray-900 border border-gray-800 rounded p-6">
                    <div className="flex items-center space-x-3 mb-4">
                        <div className="w-10 h-10 bg-blue-600 rounded flex items-center justify-center">
                            <Building2 size={20} className="text-white" />
                        </div>
                        <div>
                            <h3 className="font-semibold text-white">Organization</h3>
                            <p className="text-xs text-gray-400">Company details</p>
                        </div>
                    </div>
                    <p className="text-sm text-gray-400">
                        Manage company name, logo, contact information, and business details.
                    </p>
                    <div className="mt-3 text-xs text-gray-500">Coming soon</div>
                </div>

                <div className="bg-gray-900 border border-gray-800 rounded p-6">
                    <div className="flex items-center space-x-3 mb-4">
                        <div className="w-10 h-10 bg-green-600 rounded flex items-center justify-center">
                            <CreditCard size={20} className="text-white" />
                        </div>
                        <div>
                            <h3 className="font-semibold text-white">Billing</h3>
                            <p className="text-xs text-gray-400">Subscription & payments</p>
                        </div>
                    </div>
                    <p className="text-sm text-gray-400">
                        View subscription details, payment methods, and billing history.
                    </p>
                    <div className="mt-3 text-xs text-gray-500">Coming soon</div>
                </div>

                <div className="bg-gray-900 border border-gray-800 rounded p-6">
                    <div className="flex items-center space-x-3 mb-4">
                        <div className="w-10 h-10 bg-purple-600 rounded flex items-center justify-center">
                            <Shield size={20} className="text-white" />
                        </div>
                        <div>
                            <h3 className="font-semibold text-white">Security</h3>
                            <p className="text-xs text-gray-400">Access & compliance</p>
                        </div>
                    </div>
                    <p className="text-sm text-gray-400">
                        Configure security policies, access controls, and compliance settings.
                    </p>
                    <div className="mt-3 text-xs text-gray-500">Coming soon</div>
                </div>

                <div className="bg-gray-900 border border-gray-800 rounded p-6">
                    <div className="flex items-center space-x-3 mb-4">
                        <div className="w-10 h-10 bg-yellow-600 rounded flex items-center justify-center">
                            <Bell size={20} className="text-black" />
                        </div>
                        <div>
                            <h3 className="font-semibold text-white">Notifications</h3>
                            <p className="text-xs text-gray-400">Alerts & updates</p>
                        </div>
                    </div>
                    <p className="text-sm text-gray-400">
                        Set up email notifications, alerts, and communication preferences.
                    </p>
                    <div className="mt-3 text-xs text-gray-500">Coming soon</div>
                </div>

                <div className="bg-gray-900 border border-gray-800 rounded p-6">
                    <div className="flex items-center space-x-3 mb-4">
                        <div className="w-10 h-10 bg-orange-600 rounded flex items-center justify-center">
                            <Settings size={20} className="text-white" />
                        </div>
                        <div>
                            <h3 className="font-semibold text-white">Defaults</h3>
                            <p className="text-xs text-gray-400">Assessment preferences</p>
                        </div>
                    </div>
                    <p className="text-sm text-gray-400">
                        Configure default naming conventions and assessment settings.
                    </p>
                    <div className="mt-3 text-xs text-gray-500">Coming soon</div>
                </div>

                <div className="bg-gray-900 border border-gray-800 rounded p-6">
                    <div className="flex items-center space-x-3 mb-4">
                        <div className="w-10 h-10 bg-red-600 rounded flex items-center justify-center">
                            <Shield size={20} className="text-white" />
                        </div>
                        <div>
                            <h3 className="font-semibold text-white">API & Integrations</h3>
                            <p className="text-xs text-gray-400">External connections</p>
                        </div>
                    </div>
                    <p className="text-sm text-gray-400">
                        Manage API keys, webhooks, and third-party integrations.
                    </p>
                    <div className="mt-3 text-xs text-gray-500">Coming soon</div>
                </div>
            </div>
        </div>
    );
};

export default CompanySettingsPage;