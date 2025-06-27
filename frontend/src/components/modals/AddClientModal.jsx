import React, { useState, useEffect } from 'react';
import { X, Building2, Plus, Loader2, AlertCircle } from 'lucide-react';
import { clientApi } from '../../services/apiService';

const AddClientModal = ({ isOpen, onClose, onClientAdded }) => {
    const [formData, setFormData] = useState({
        name: '',
        description: '',
        industry: '',
        contactName: '',
        contactEmail: '',
        contactPhone: '',
        address: '',
        city: '',
        state: '',
        country: '',
        postalCode: '',
        timeZone: '',
        contractStartDate: '',
        contractEndDate: ''
    });
    const [isLoading, setIsLoading] = useState(false);
    const [error, setError] = useState(null);
    const [validationErrors, setValidationErrors] = useState({});

    // Reset form when modal opens/closes
    useEffect(() => {
        if (!isOpen) {
            setFormData({
                name: '',
                description: '',
                industry: '',
                contactName: '',
                contactEmail: '',
                contactPhone: '',
                address: '',
                city: '',
                state: '',
                country: '',
                postalCode: '',
                timeZone: '',
                contractStartDate: '',
                contractEndDate: ''
            });
            setError(null);
            setValidationErrors({});
        }
    }, [isOpen]);

    const handleInputChange = (e) => {
        const { name, value } = e.target;
        setFormData(prev => ({
            ...prev,
            [name]: value
        }));
        
        // Clear validation error for this field
        if (validationErrors[name]) {
            setValidationErrors(prev => ({
                ...prev,
                [name]: null
            }));
        }
    };

    const validateForm = () => {
        const errors = {};
        
        if (!formData.name.trim()) {
            errors.name = 'Client name is required';
        }
        
        if (formData.contactEmail && !/\S+@\S+\.\S+/.test(formData.contactEmail)) {
            errors.contactEmail = 'Please enter a valid email address';
        }
        
        if (formData.contractStartDate && formData.contractEndDate) {
            const startDate = new Date(formData.contractStartDate);
            const endDate = new Date(formData.contractEndDate);
            if (endDate <= startDate) {
                errors.contractEndDate = 'End date must be after start date';
            }
        }
        
        setValidationErrors(errors);
        return Object.keys(errors).length === 0;
    };

    const handleSubmit = async (e) => {
        e.preventDefault();
        
        if (!validateForm()) {
            return;
        }
        
        setIsLoading(true);
        setError(null);
        
        try {
            const createData = {
                ...formData,
                contractStartDate: formData.contractStartDate || null,
                contractEndDate: formData.contractEndDate || null
            };
            
            await clientApi.createClient(createData);
            
            if (onClientAdded) {
                onClientAdded();
            }
            
            onClose();
        } catch (error) {
            console.error('Failed to create client:', error);
            setError(
                error.response?.data?.message || 
                error.response?.data?.error || 
                'Failed to create client. Please try again.'
            );
        } finally {
            setIsLoading(false);
        }
    };

    if (!isOpen) return null;

    return (
        <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center p-4 z-50">
            <div className="bg-gray-800 rounded-lg shadow-xl w-full max-w-2xl max-h-[90vh] overflow-y-auto">
                {/* Header */}
                <div className="flex items-center justify-between p-6 border-b border-gray-700">
                    <div className="flex items-center space-x-3">
                        <div className="w-10 h-10 bg-green-600 rounded-lg flex items-center justify-center">
                            <Plus size={20} className="text-white" />
                        </div>
                        <div>
                            <h2 className="text-lg font-semibold text-white">Add New Client</h2>
                            <p className="text-sm text-gray-400">Create a new client profile</p>
                        </div>
                    </div>
                    <button
                        onClick={onClose}
                        className="text-gray-400 hover:text-white p-2 rounded-lg hover:bg-gray-700"
                    >
                        <X size={20} />
                    </button>
                </div>

                {/* Error Display */}
                {error && (
                    <div className="m-6 mb-0 bg-red-900/20 border border-red-800 rounded p-4">
                        <div className="flex items-center space-x-3">
                            <AlertCircle className="text-red-400" size={20} />
                            <div>
                                <h4 className="text-red-400 font-medium">Creation Failed</h4>
                                <p className="text-red-300 text-sm mt-1">{error}</p>
                            </div>
                        </div>
                    </div>
                )}

                {/* Form */}
                <form onSubmit={handleSubmit} className="p-6">
                    <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
                        {/* Client Name */}
                        <div className="md:col-span-2">
                            <label className="block text-sm font-medium text-gray-300 mb-2">
                                Client Name *
                            </label>
                            <input
                                type="text"
                                name="name"
                                value={formData.name}
                                onChange={handleInputChange}
                                className={`w-full px-3 py-2 bg-gray-700 border rounded-lg text-white placeholder-gray-400 focus:outline-none focus:ring-2 focus:ring-yellow-600 ${
                                    validationErrors.name ? 'border-red-500' : 'border-gray-600'
                                }`}
                                placeholder="Enter client name"
                                required
                            />
                            {validationErrors.name && (
                                <p className="text-red-400 text-sm mt-1">{validationErrors.name}</p>
                            )}
                        </div>

                        {/* Description */}
                        <div className="md:col-span-2">
                            <label className="block text-sm font-medium text-gray-300 mb-2">
                                Description
                            </label>
                            <textarea
                                name="description"
                                value={formData.description}
                                onChange={handleInputChange}
                                rows={3}
                                className="w-full px-3 py-2 bg-gray-700 border border-gray-600 rounded-lg text-white placeholder-gray-400 focus:outline-none focus:ring-2 focus:ring-yellow-600"
                                placeholder="Brief description of the client and their business"
                            />
                        </div>

                        {/* Industry */}
                        <div>
                            <label className="block text-sm font-medium text-gray-300 mb-2">
                                Industry
                            </label>
                            <select
                                name="industry"
                                value={formData.industry}
                                onChange={handleInputChange}
                                className="w-full px-3 py-2 bg-gray-700 border border-gray-600 rounded-lg text-white focus:outline-none focus:ring-2 focus:ring-yellow-600"
                            >
                                <option value="">Select Industry</option>
                                <option value="Technology">Technology</option>
                                <option value="Healthcare">Healthcare</option>
                                <option value="Finance">Finance</option>
                                <option value="Manufacturing">Manufacturing</option>
                                <option value="Retail">Retail</option>
                                <option value="Education">Education</option>
                                <option value="Government">Government</option>
                                <option value="Non-Profit">Non-Profit</option>
                                <option value="Professional Services">Professional Services</option>
                                <option value="Other">Other</option>
                            </select>
                        </div>

                        {/* Time Zone */}
                        <div>
                            <label className="block text-sm font-medium text-gray-300 mb-2">
                                Time Zone
                            </label>
                            <select
                                name="timeZone"
                                value={formData.timeZone}
                                onChange={handleInputChange}
                                className="w-full px-3 py-2 bg-gray-700 border border-gray-600 rounded-lg text-white focus:outline-none focus:ring-2 focus:ring-yellow-600"
                            >
                                <option value="">Select Time Zone</option>
                                <option value="America/New_York">Eastern Time</option>
                                <option value="America/Chicago">Central Time</option>
                                <option value="America/Denver">Mountain Time</option>
                                <option value="America/Los_Angeles">Pacific Time</option>
                                <option value="America/Toronto">Toronto</option>
                                <option value="America/Vancouver">Vancouver</option>
                            </select>
                        </div>

                        {/* Contact Information */}
                        <div className="md:col-span-2">
                            <h3 className="text-lg font-medium text-white mb-4">Contact Information</h3>
                        </div>

                        <div>
                            <label className="block text-sm font-medium text-gray-300 mb-2">
                                Contact Name
                            </label>
                            <input
                                type="text"
                                name="contactName"
                                value={formData.contactName}
                                onChange={handleInputChange}
                                className="w-full px-3 py-2 bg-gray-700 border border-gray-600 rounded-lg text-white placeholder-gray-400 focus:outline-none focus:ring-2 focus:ring-yellow-600"
                                placeholder="Primary contact name"
                            />
                        </div>

                        <div>
                            <label className="block text-sm font-medium text-gray-300 mb-2">
                                Contact Email
                            </label>
                            <input
                                type="email"
                                name="contactEmail"
                                value={formData.contactEmail}
                                onChange={handleInputChange}
                                className={`w-full px-3 py-2 bg-gray-700 border rounded-lg text-white placeholder-gray-400 focus:outline-none focus:ring-2 focus:ring-yellow-600 ${
                                    validationErrors.contactEmail ? 'border-red-500' : 'border-gray-600'
                                }`}
                                placeholder="contact@client.com"
                            />
                            {validationErrors.contactEmail && (
                                <p className="text-red-400 text-sm mt-1">{validationErrors.contactEmail}</p>
                            )}
                        </div>

                        <div>
                            <label className="block text-sm font-medium text-gray-300 mb-2">
                                Phone Number
                            </label>
                            <input
                                type="tel"
                                name="contactPhone"
                                value={formData.contactPhone}
                                onChange={handleInputChange}
                                className="w-full px-3 py-2 bg-gray-700 border border-gray-600 rounded-lg text-white placeholder-gray-400 focus:outline-none focus:ring-2 focus:ring-yellow-600"
                                placeholder="+1 (555) 123-4567"
                            />
                        </div>

                        <div></div> {/* Empty div for grid alignment */}

                        {/* Address Information */}
                        <div className="md:col-span-2">
                            <h3 className="text-lg font-medium text-white mb-4 mt-4">Address Information (Optional)</h3>
                        </div>

                        <div className="md:col-span-2">
                            <label className="block text-sm font-medium text-gray-300 mb-2">
                                Address
                            </label>
                            <input
                                type="text"
                                name="address"
                                value={formData.address}
                                onChange={handleInputChange}
                                className="w-full px-3 py-2 bg-gray-700 border border-gray-600 rounded-lg text-white placeholder-gray-400 focus:outline-none focus:ring-2 focus:ring-yellow-600"
                                placeholder="Street address"
                            />
                        </div>

                        <div>
                            <label className="block text-sm font-medium text-gray-300 mb-2">
                                City
                            </label>
                            <input
                                type="text"
                                name="city"
                                value={formData.city}
                                onChange={handleInputChange}
                                className="w-full px-3 py-2 bg-gray-700 border border-gray-600 rounded-lg text-white placeholder-gray-400 focus:outline-none focus:ring-2 focus:ring-yellow-600"
                                placeholder="City"
                            />
                        </div>

                        <div>
                            <label className="block text-sm font-medium text-gray-300 mb-2">
                                State/Province
                            </label>
                            <input
                                type="text"
                                name="state"
                                value={formData.state}
                                onChange={handleInputChange}
                                className="w-full px-3 py-2 bg-gray-700 border border-gray-600 rounded-lg text-white placeholder-gray-400 focus:outline-none focus:ring-2 focus:ring-yellow-600"
                                placeholder="State or Province"
                            />
                        </div>

                        <div>
                            <label className="block text-sm font-medium text-gray-300 mb-2">
                                Country
                            </label>
                            <select
                                name="country"
                                value={formData.country}
                                onChange={handleInputChange}
                                className="w-full px-3 py-2 bg-gray-700 border border-gray-600 rounded-lg text-white focus:outline-none focus:ring-2 focus:ring-yellow-600"
                            >
                                <option value="">Select Country</option>
                                <option value="United States">United States</option>
                                <option value="Canada">Canada</option>
                                <option value="United Kingdom">United Kingdom</option>
                                <option value="Australia">Australia</option>
                                <option value="Other">Other</option>
                            </select>
                        </div>

                        <div>
                            <label className="block text-sm font-medium text-gray-300 mb-2">
                                Postal Code
                            </label>
                            <input
                                type="text"
                                name="postalCode"
                                value={formData.postalCode}
                                onChange={handleInputChange}
                                className="w-full px-3 py-2 bg-gray-700 border border-gray-600 rounded-lg text-white placeholder-gray-400 focus:outline-none focus:ring-2 focus:ring-yellow-600"
                                placeholder="ZIP/Postal Code"
                            />
                        </div>

                        {/* Contract Information */}
                        <div className="md:col-span-2">
                            <h3 className="text-lg font-medium text-white mb-4 mt-4">Contract Information (Optional)</h3>
                        </div>

                        <div>
                            <label className="block text-sm font-medium text-gray-300 mb-2">
                                Contract Start Date
                            </label>
                            <input
                                type="date"
                                name="contractStartDate"
                                value={formData.contractStartDate}
                                onChange={handleInputChange}
                                className="w-full px-3 py-2 bg-gray-700 border border-gray-600 rounded-lg text-white focus:outline-none focus:ring-2 focus:ring-yellow-600"
                            />
                        </div>

                        <div>
                            <label className="block text-sm font-medium text-gray-300 mb-2">
                                Contract End Date
                            </label>
                            <input
                                type="date"
                                name="contractEndDate"
                                value={formData.contractEndDate}
                                onChange={handleInputChange}
                                className={`w-full px-3 py-2 bg-gray-700 border rounded-lg text-white focus:outline-none focus:ring-2 focus:ring-yellow-600 ${
                                    validationErrors.contractEndDate ? 'border-red-500' : 'border-gray-600'
                                }`}
                            />
                            {validationErrors.contractEndDate && (
                                <p className="text-red-400 text-sm mt-1">{validationErrors.contractEndDate}</p>
                            )}
                        </div>
                    </div>

                    {/* Footer */}
                    <div className="flex items-center justify-end space-x-3 pt-6 mt-6 border-t border-gray-700">
                        <button
                            type="button"
                            onClick={onClose}
                            className="px-4 py-2 text-gray-400 hover:text-white transition-colors"
                            disabled={isLoading}
                        >
                            Cancel
                        </button>
                        <button
                            type="submit"
                            disabled={isLoading}
                            className="px-6 py-2 bg-yellow-600 hover:bg-yellow-700 text-black rounded font-medium flex items-center space-x-2 disabled:opacity-50"
                        >
                            {isLoading ? (
                                <>
                                    <Loader2 size={16} className="animate-spin" />
                                    <span>Creating...</span>
                                </>
                            ) : (
                                <>
                                    <Plus size={16} />
                                    <span>Create Client</span>
                                </>
                            )}
                        </button>
                    </div>
                </form>
            </div>
        </div>
    );
};

export default AddClientModal;