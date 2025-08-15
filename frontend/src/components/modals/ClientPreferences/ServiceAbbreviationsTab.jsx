import React, { useState } from 'react';
import { Plus, Trash2, AlertCircle, CheckCircle, Info } from 'lucide-react';

const ServiceAbbreviationsTab = ({ formData, setFormData }) => {
    const [newAbbreviation, setNewAbbreviation] = useState('');
    const [newFullName, setNewFullName] = useState('');
    const [validationError, setValidationError] = useState('');

    // Reserved words that cannot be used as abbreviations
    const reservedWords = [
        'vm', 'sql', 'kv', 'st', 'rg', 'vnet', 'nsg', 'pip', 'nic', 'lb', 'ag', 'fw',
        'api', 'app', 'web', 'func', 'plan', 'db', 'cache', 'queue', 'topic', 'ns'
    ];

    // Common industry abbreviations for quick import
    const commonAbbreviations = [
        { abbreviation: 'auth', fullName: 'Authentication Service' },
        { abbreviation: 'cms', fullName: 'Content Management System' },
        { abbreviation: 'crm', fullName: 'Customer Relationship Management' },
        { abbreviation: 'erp', fullName: 'Enterprise Resource Planning' },
        { abbreviation: 'hr', fullName: 'Human Resources' },
        { abbreviation: 'inv', fullName: 'Inventory Management' },
        { abbreviation: 'log', fullName: 'Logging Service' },
        { abbreviation: 'mon', fullName: 'Monitoring Service' },
        { abbreviation: 'pay', fullName: 'Payment Processing' },
        { abbreviation: 'repo', fullName: 'Repository Service' }
    ];

    const validateAbbreviation = (abbreviation, fullName) => {
        const errors = [];

        // Check length
        if (abbreviation.length < 2 || abbreviation.length > 10) {
            errors.push('Abbreviation must be 2-10 characters long');
        }

        if (fullName.length < 3 || fullName.length > 50) {
            errors.push('Service name must be 3-50 characters long');
        }

        // Check alphanumeric only
        if (!/^[a-zA-Z0-9]+$/.test(abbreviation)) {
            errors.push('Abbreviation must contain only letters and numbers');
        }

        // Check reserved words (case-insensitive)
        if (reservedWords.includes(abbreviation.toLowerCase())) {
            errors.push('This abbreviation is reserved for Azure resource types');
        }

        // Check for duplicates (case-sensitive as per requirements)
        const isDuplicate = formData.serviceAbbreviations?.some(sa => sa.abbreviation === abbreviation);
        if (isDuplicate) {
            errors.push('This abbreviation already exists');
        }

        return errors;
    };

    const handleAddAbbreviation = () => {
        const abbreviation = newAbbreviation.trim();
        const fullName = newFullName.trim();

        if (!abbreviation || !fullName) {
            setValidationError('Both abbreviation and service name are required');
            return;
        }

        const errors = validateAbbreviation(abbreviation, fullName);
        if (errors.length > 0) {
            setValidationError(errors.join(', '));
            return;
        }

        const newServiceAbbreviation = {
            abbreviation,
            fullName,
            createdDate: new Date().toISOString(),
            createdBy: 'Current User' // This would come from auth context
        };

        setFormData(prev => ({
            ...prev,
            serviceAbbreviations: [...(prev.serviceAbbreviations || []), newServiceAbbreviation]
        }));

        // Clear form
        setNewAbbreviation('');
        setNewFullName('');
        setValidationError('');
    };

    const handleRemoveAbbreviation = (index) => {
        setFormData(prev => ({
            ...prev,
            serviceAbbreviations: prev.serviceAbbreviations.filter((_, i) => i !== index)
        }));
    };

    const handleImportCommon = (commonAbbr) => {
        const errors = validateAbbreviation(commonAbbr.abbreviation, commonAbbr.fullName);
        if (errors.length === 0) {
            const newServiceAbbreviation = {
                ...commonAbbr,
                createdDate: new Date().toISOString(),
                createdBy: 'System Import'
            };

            setFormData(prev => ({
                ...prev,
                serviceAbbreviations: [...(prev.serviceAbbreviations || []), newServiceAbbreviation]
            }));
        }
    };

    const serviceAbbreviations = formData.serviceAbbreviations || [];

    return (
        <div className="space-y-6">
            {/* Header with explanation */}
            <div className="bg-blue-900/20 border border-blue-800 rounded-lg p-4">
                <div className="flex items-start space-x-3">
                    <Info className="text-blue-400 mt-0.5" size={20} />
                    <div>
                        <h4 className="text-blue-400 font-medium">Service Abbreviations</h4>
                        <p className="text-blue-300 text-sm mt-1">
                            Teach the system about your client-specific abbreviations to improve naming convention analysis. 
                            For example, if "cmp" stands for "compass" in your environment, add it here to avoid false flags.
                        </p>
                    </div>
                </div>
            </div>

            {/* Add New Abbreviation Form */}
            <div className="bg-gray-700 rounded-lg p-4">
                <h3 className="text-white font-medium mb-4">Add New Service Abbreviation</h3>
                
                <div className="grid grid-cols-1 md:grid-cols-2 gap-4 mb-4">
                    <div>
                        <label className="block text-sm font-medium text-gray-300 mb-2">
                            Abbreviation <span className="text-red-400">*</span>
                        </label>
                        <input
                            type="text"
                            value={newAbbreviation}
                            onChange={(e) => setNewAbbreviation(e.target.value)}
                            placeholder="cmp"
                            className="w-full px-3 py-2 bg-gray-600 border border-gray-500 rounded text-white placeholder-gray-400 focus:border-yellow-500 focus:outline-none"
                            maxLength={10}
                        />
                        <p className="text-xs text-gray-400 mt-1">2-10 characters, letters and numbers only</p>
                    </div>
                    
                    <div>
                        <label className="block text-sm font-medium text-gray-300 mb-2">
                            Service Name <span className="text-red-400">*</span>
                        </label>
                        <input
                            type="text"
                            value={newFullName}
                            onChange={(e) => setNewFullName(e.target.value)}
                            placeholder="Compass Application"
                            className="w-full px-3 py-2 bg-gray-600 border border-gray-500 rounded text-white placeholder-gray-400 focus:border-yellow-500 focus:outline-none"
                            maxLength={50}
                        />
                        <p className="text-xs text-gray-400 mt-1">3-50 characters, descriptive service name</p>
                    </div>
                </div>

                {validationError && (
                    <div className="bg-red-900/20 border border-red-800 rounded p-3 mb-4">
                        <div className="flex items-center space-x-2">
                            <AlertCircle className="text-red-400" size={16} />
                            <p className="text-red-300 text-sm">{validationError}</p>
                        </div>
                    </div>
                )}

                <button
                    type="button"
                    onClick={handleAddAbbreviation}
                    className="flex items-center space-x-2 px-4 py-2 bg-yellow-600 hover:bg-yellow-700 text-black rounded font-medium transition-colors"
                >
                    <Plus size={16} />
                    <span>Add Abbreviation</span>
                </button>
            </div>

            {/* Common Abbreviations Quick Import */}
            <div className="bg-gray-700 rounded-lg p-4">
                <h3 className="text-white font-medium mb-4">Quick Import Common Abbreviations</h3>
                <div className="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-5 gap-2">
                    {commonAbbreviations.map((abbr) => {
                        const isAlreadyAdded = serviceAbbreviations.some(sa => sa.abbreviation === abbr.abbreviation);
                        return (
                            <button
                                key={abbr.abbreviation}
                                type="button"
                                onClick={() => handleImportCommon(abbr)}
                                disabled={isAlreadyAdded}
                                className={`p-2 rounded text-sm transition-colors ${
                                    isAlreadyAdded 
                                        ? 'bg-gray-600 text-gray-400 cursor-not-allowed' 
                                        : 'bg-gray-600 hover:bg-gray-500 text-white'
                                }`}
                                title={isAlreadyAdded ? 'Already added' : `Import ${abbr.abbreviation} (${abbr.fullName})`}
                            >
                                <div className="font-mono font-bold">{abbr.abbreviation}</div>
                                <div className="text-xs opacity-75">{abbr.fullName}</div>
                            </button>
                        );
                    })}
                </div>
            </div>

            {/* Current Service Abbreviations */}
            <div className="bg-gray-700 rounded-lg p-4">
                <h3 className="text-white font-medium mb-4">
                    Current Service Abbreviations ({serviceAbbreviations.length})
                </h3>
                
                {serviceAbbreviations.length === 0 ? (
                    <div className="text-center py-8 text-gray-400">
                        <Info size={48} className="mx-auto mb-4 opacity-50" />
                        <p>No service abbreviations configured yet.</p>
                        <p className="text-sm mt-1">Add abbreviations above to improve naming analysis accuracy.</p>
                    </div>
                ) : (
                    <div className="space-y-2">
                        <div className="grid grid-cols-12 gap-4 text-sm text-gray-400 font-medium pb-2 border-b border-gray-600">
                            <div className="col-span-3">Abbreviation</div>
                            <div className="col-span-5">Service Name</div>
                            <div className="col-span-3">Added By</div>
                            <div className="col-span-1">Actions</div>
                        </div>
                        
                        {serviceAbbreviations.map((abbr, index) => (
                            <div key={index} className="grid grid-cols-12 gap-4 py-2 text-sm items-center border-b border-gray-600/50 last:border-b-0">
                                <div className="col-span-3">
                                    <code className="bg-gray-600 px-2 py-1 rounded text-yellow-400 font-mono">
                                        {abbr.abbreviation}
                                    </code>
                                </div>
                                <div className="col-span-5 text-white">{abbr.fullName}</div>
                                <div className="col-span-3 text-gray-400">{abbr.createdBy}</div>
                                <div className="col-span-1">
                                    <button
                                        type="button"
                                        onClick={() => handleRemoveAbbreviation(index)}
                                        className="text-red-400 hover:text-red-300 hover:bg-red-900/20 p-1 rounded transition-colors"
                                        title="Remove abbreviation"
                                    >
                                        <Trash2 size={16} />
                                    </button>
                                </div>
                            </div>
                        ))}
                    </div>
                )}
            </div>

            {/* Usage Information */}
            <div className="bg-gray-700 rounded-lg p-4">
                <h3 className="text-white font-medium mb-3">How This Improves Assessments</h3>
                <div className="space-y-2 text-sm text-gray-300">
                    <div className="flex items-start space-x-2">
                        <CheckCircle className="text-green-400 mt-0.5" size={16} />
                        <span>Unknown components in resource names will be checked against your abbreviations</span>
                    </div>
                    <div className="flex items-start space-x-2">
                        <CheckCircle className="text-green-400 mt-0.5" size={16} />
                        <span>Reduces false positives in naming convention findings</span>
                    </div>
                    <div className="flex items-start space-x-2">
                        <CheckCircle className="text-green-400 mt-0.5" size={16} />
                        <span>Provides context-aware recommendations for your environment</span>
                    </div>
                    <div className="flex items-start space-x-2">
                        <CheckCircle className="text-green-400 mt-0.5" size={16} />
                        <span>Abbreviations are case-sensitive and apply only to this client</span>
                    </div>
                </div>
            </div>
        </div>
    );
};

export default ServiceAbbreviationsTab;