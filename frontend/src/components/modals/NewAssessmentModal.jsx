import React, { useState } from 'react';
import { Play } from 'lucide-react';

const NewAssessmentModal = ({ isOpen, onClose, onStart }) => {
    const [formData, setFormData] = useState({
        name: '',
        environment: '',
        subscriptions: '',
        type: 'Full'
    });

    if (!isOpen) return null;

    const handleSubmit = (e) => {
        e.preventDefault();
        onStart(formData);
        onClose();
        setFormData({ name: '', environment: '', subscriptions: '', type: 'Full' });
    };

    return (
        <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50">
            <div className="bg-gray-900 border border-gray-800 rounded w-full max-w-md mx-4">
                <div className="p-6 border-b border-gray-800">
                    <h2 className="text-xl font-semibold text-white">Start New Assessment</h2>
                </div>

                <form onSubmit={handleSubmit} className="p-6 space-y-4">
                    <div>
                        <label className="block text-sm font-medium text-gray-300 mb-2">Assessment Name</label>
                        <input
                            type="text"
                            value={formData.name}
                            onChange={(e) => setFormData({ ...formData, name: e.target.value })}
                            className="w-full bg-gray-800 border border-gray-700 rounded px-3 py-2 text-white focus:outline-none focus:border-yellow-600"
                            placeholder="Production Environment Assessment"
                            required
                        />
                    </div>

                    <div>
                        <label className="block text-sm font-medium text-gray-300 mb-2">Environment</label>
                        <select
                            value={formData.environment}
                            onChange={(e) => setFormData({ ...formData, environment: e.target.value })}
                            className="w-full bg-gray-800 border border-gray-700 rounded px-3 py-2 text-white focus:outline-none focus:border-yellow-600"
                            required
                        >
                            <option value="">Select Environment</option>
                            <option value="Production">Production</option>
                            <option value="Staging">Staging</option>
                            <option value="Development">Development</option>
                            <option value="Testing">Testing</option>
                        </select>
                    </div>

                    <div>
                        <label className="block text-sm font-medium text-gray-300 mb-2">Assessment Type</label>
                        <select
                            value={formData.type}
                            onChange={(e) => setFormData({ ...formData, type: e.target.value })}
                            className="w-full bg-gray-800 border border-gray-700 rounded px-3 py-2 text-white focus:outline-none focus:border-yellow-600"
                        >
                            <option value="Full">Full Assessment</option>
                            <option value="NamingConvention">Naming Conventions Only</option>
                            <option value="Tagging">Tagging Compliance Only</option>
                        </select>
                    </div>

                    <div>
                        <label className="block text-sm font-medium text-gray-300 mb-2">Azure Subscriptions</label>
                        <textarea
                            value={formData.subscriptions}
                            onChange={(e) => setFormData({ ...formData, subscriptions: e.target.value })}
                            className="w-full bg-gray-800 border border-gray-700 rounded px-3 py-2 text-white focus:outline-none focus:border-yellow-600"
                            placeholder="Enter subscription IDs (one per line)"
                            rows="3"
                            required
                        />
                    </div>

                    <div className="flex items-center justify-end space-x-3 pt-4">
                        <button
                            type="button"
                            onClick={onClose}
                            className="px-4 py-2 text-gray-300 hover:text-white transition-colors"
                        >
                            Cancel
                        </button>
                        <button
                            type="submit"
                            className="flex items-center space-x-2 bg-yellow-600 hover:bg-yellow-700 text-black px-4 py-2 rounded font-medium transition-colors"
                        >
                            <Play size={16} />
                            <span>Start Assessment</span>
                        </button>
                    </div>
                </form>
            </div>
        </div>
    );
};

export default NewAssessmentModal;