import React, { useState } from 'react';
import { createPortal } from 'react-dom';
import { X, BookOpen, Save, AlertCircle, CheckCircle, Info } from 'lucide-react';

const ImproveDetectionModal = ({ 
    isOpen, 
    onClose, 
    resourceName, 
    unknownComponent, 
    onSaveMapping,
    client 
}) => {
    const [newAbbreviation, setNewAbbreviation] = useState(unknownComponent || '');
    const [newFullName, setNewFullName] = useState('');
    const [componentType, setComponentType] = useState('service');
    const [applyToSimilar, setApplyToSimilar] = useState(true);
    const [isSaving, setIsSaving] = useState(false);
    const [validationError, setValidationError] = useState('');

    // Handle component type changes
    const handleComponentTypeChange = (newType) => {
        setComponentType(newType);
        
        // Auto-populate fields for random strings
        if (newType === 'random') {
            if (!newAbbreviation && unknownComponent) {
                setNewAbbreviation(unknownComponent);
            }
            setNewFullName('Random/Generated String');
        } else if (newType !== 'random' && newFullName === 'Random/Generated String') {
            // Clear the auto-populated full name if switching away from random
            setNewFullName('');
        }
    };

    // Reserved words that cannot be used as abbreviations
    const reservedWords = [
        'vm', 'sql', 'kv', 'st', 'rg', 'vnet', 'nsg', 'pip', 'nic', 'lb', 'ag', 'fw',
        'api', 'app', 'web', 'func', 'plan', 'db', 'cache', 'queue', 'topic', 'ns'
    ];

    const componentTypes = [
        { value: 'service', label: 'Service/Application', description: 'Business function or application identifier' },
        { value: 'company', label: 'Company', description: 'Organization identifier' },
        { value: 'environment', label: 'Environment', description: 'Deployment environment (dev, prod, etc.)' },
        { value: 'location', label: 'Location', description: 'Azure region or location identifier' },
        { value: 'custom', label: 'Custom/Other', description: 'Custom identifier that doesn\'t fit standard categories' },
        { value: 'random', label: 'Random/Generated String', description: 'Auto-generated identifier that should be ignored in naming analysis' }
    ];

    const validateInput = () => {
        const errors = [];
        const abbreviation = newAbbreviation.trim();
        const fullName = newFullName.trim();

        // For random strings, we don't need detailed validation
        if (componentType === 'random') {
            if (!abbreviation) {
                errors.push('Please specify the random string component');
            }
            return errors;
        }

        // Standard validation for meaningful abbreviations
        if (!abbreviation || !fullName) {
            errors.push('Both abbreviation and full name are required');
        }

        if (abbreviation.length < 2 || abbreviation.length > 10) {
            errors.push('Abbreviation must be 2-10 characters long');
        }

        if (fullName.length < 3 || fullName.length > 50) {
            errors.push('Full name must be 3-50 characters long');
        }

        if (!/^[a-zA-Z0-9]+$/.test(abbreviation)) {
            errors.push('Abbreviation must contain only letters and numbers');
        }

        if (reservedWords.includes(abbreviation.toLowerCase())) {
            errors.push('This abbreviation is reserved for Azure resource types');
        }

        return errors;
    };

    const handleSave = async () => {
        const errors = validateInput();
        if (errors.length > 0) {
            setValidationError(errors.join(', '));
            return;
        }

        setIsSaving(true);
        setValidationError('');

        try {
            const mapping = {
                abbreviation: componentType === 'random' ? (unknownComponent || newAbbreviation.trim()) : newAbbreviation.trim(),
                fullName: componentType === 'random' ? 'Random/Generated String' : newFullName.trim(),
                componentType,
                applyToSimilar,
                createdDate: new Date().toISOString(),
                createdBy: 'Interactive Learning',
                isRandomString: componentType === 'random'
            };

            await onSaveMapping(mapping);
            onClose();
        } catch (error) {
            console.error('Failed to save mapping:', error);
            setValidationError('Failed to save mapping. Please try again.');
        } finally {
            setIsSaving(false);
        }
    };

    const parseResourceName = (name) => {
        const parts = name.split(/[-_.]/);
        return parts.map((part, index) => ({
            part,
            index,
            isUnknown: part === unknownComponent,
            suggestedType: index === 0 ? 'company' : 
                         index === 1 ? 'environment' : 
                         index === parts.length - 1 ? 'instance' : 'service'
        }));
    };

    if (!isOpen) return null;

    const resourceParts = parseResourceName(resourceName);

    return createPortal(
        <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center p-4 z-50">
            <div className="bg-gray-800 rounded-lg shadow-xl w-full max-w-2xl max-h-[90vh] overflow-y-auto">
                {/* Header */}
                <div className="flex items-center justify-between p-6 border-b border-gray-700">
                    <div className="flex items-center space-x-3">
                        <div className="w-10 h-10 bg-blue-600 rounded-lg flex items-center justify-center">
                            <BookOpen size={20} className="text-white" />
                        </div>
                        <div>
                            <h2 className="text-lg font-semibold text-white">Improve Detection</h2>
                            <p className="text-sm text-gray-400">
                                Help the system learn about client-specific abbreviations
                            </p>
                        </div>
                    </div>
                    <button
                        onClick={onClose}
                        className="text-gray-400 hover:text-white p-2 rounded-lg hover:bg-gray-700"
                    >
                        <X size={20} />
                    </button>
                </div>

                {/* Content */}
                <div className="p-6 space-y-6">
                    {/* Resource Name Breakdown */}
                    <div className="bg-gray-700 rounded-lg p-4">
                        <h3 className="text-white font-medium mb-3">Resource Name Breakdown</h3>
                        <div className="bg-gray-800 rounded p-3 font-mono text-sm">
                            <div className="flex flex-wrap items-center gap-2">
                                {resourceParts.map((part, index) => (
                                    <React.Fragment key={index}>
                                        <span 
                                            className={`px-2 py-1 rounded ${
                                                part.isUnknown 
                                                    ? 'bg-red-600 text-white' 
                                                    : 'bg-gray-600 text-gray-300'
                                            }`}
                                        >
                                            {part.part}
                                            {part.isUnknown && (
                                                <span className="text-red-200 ml-1">(?)</span>
                                            )}
                                        </span>
                                        {index < resourceParts.length - 1 && (
                                            <span className="text-gray-500">-</span>
                                        )}
                                    </React.Fragment>
                                ))}
                            </div>
                        </div>
                        <p className="text-gray-400 text-sm mt-2">
                            <span className="inline-block w-3 h-3 bg-red-600 rounded mr-2"></span>
                            Unknown component that will be taught to the system
                        </p>
                    </div>

                    {/* Mapping Form */}
                    <div className="bg-gray-700 rounded-lg p-4 space-y-4">
                        <h3 className="text-white font-medium">Define Component Meaning</h3>
                        
                        {/* Abbreviation */}
                        <div>
                            <label className="block text-sm font-medium text-gray-300 mb-2">
                                Abbreviation {componentType !== 'random' && <span className="text-red-400">*</span>}
                            </label>
                            <input
                                type="text"
                                value={componentType === 'random' ? (unknownComponent || newAbbreviation) : newAbbreviation}
                                onChange={(e) => setNewAbbreviation(e.target.value)}
                                className="w-full px-3 py-2 bg-gray-600 border border-gray-500 rounded text-white placeholder-gray-400 focus:border-yellow-500 focus:outline-none"
                                placeholder={componentType === 'random' ? 'The random string from the resource name' : 'Enter the abbreviation to define'}
                                maxLength={10}
                                disabled={componentType === 'random'}
                            />
                            <p className="text-xs text-gray-400 mt-1">
                                {componentType === 'random' 
                                    ? 'Random strings are automatically handled and don\'t need manual definition'
                                    : '2-10 characters, letters and numbers only (case-sensitive)'
                                }
                            </p>
                        </div>

                        {/* Full Name */}
                        <div>
                            <label className="block text-sm font-medium text-gray-300 mb-2">
                                Full Name {componentType !== 'random' && <span className="text-red-400">*</span>}
                            </label>
                            <input
                                type="text"
                                value={newFullName}
                                onChange={(e) => setNewFullName(e.target.value)}
                                placeholder={componentType === 'random' ? 'Random/Generated String' : 'What does this abbreviation stand for?'}
                                className="w-full px-3 py-2 bg-gray-600 border border-gray-500 rounded text-white placeholder-gray-400 focus:border-yellow-500 focus:outline-none"
                                maxLength={50}
                                disabled={componentType === 'random'}
                            />
                            <p className="text-xs text-gray-400 mt-1">
                                {componentType === 'random'
                                    ? 'Random strings will be automatically excluded from naming convention analysis'
                                    : '3-50 characters, descriptive name'
                                }
                            </p>
                        </div>

                        {/* Component Type */}
                        <div>
                            <label className="block text-sm font-medium text-gray-300 mb-2">
                                Component Type
                            </label>
                            <p className="text-xs text-gray-400 mb-3">
                                Select the category that best describes this component. Use "Custom/Other" for unique identifiers that don't fit standard patterns.
                            </p>
                            <div className="space-y-2">
                                {componentTypes.map((type) => (
                                    <label key={type.value} className="flex items-start space-x-3 cursor-pointer">
                                        <input
                                            type="radio"
                                            name="componentType"
                                            value={type.value}
                                            checked={componentType === type.value}
                                            onChange={(e) => handleComponentTypeChange(e.target.value)}
                                            className="mt-1 text-yellow-600 focus:ring-yellow-600"
                                        />
                                        <div>
                                            <div className="text-white font-medium">{type.label}</div>
                                            <div className="text-gray-400 text-sm">{type.description}</div>
                                        </div>
                                    </label>
                                ))}
                            </div>
                        </div>

                        {/* Apply to Similar Resources */}
                        <div>
                            <label className="flex items-center space-x-3 cursor-pointer">
                                <input
                                    type="checkbox"
                                    checked={applyToSimilar}
                                    onChange={(e) => setApplyToSimilar(e.target.checked)}
                                    className="text-yellow-600 focus:ring-yellow-600"
                                />
                                <div>
                                    <div className="text-white font-medium">Apply to similar resources</div>
                                    <div className="text-gray-400 text-sm">
                                        Automatically improve detection for other resources with the same pattern
                                    </div>
                                </div>
                            </label>
                        </div>
                    </div>

                    {/* Validation Error */}
                    {validationError && (
                        <div className="bg-red-900/20 border border-red-800 rounded p-3">
                            <div className="flex items-center space-x-2">
                                <AlertCircle className="text-red-400" size={16} />
                                <p className="text-red-300 text-sm">{validationError}</p>
                            </div>
                        </div>
                    )}

                    {/* Benefits Information */}
                    <div className="bg-blue-900/20 border border-blue-800 rounded-lg p-4">
                        <div className="flex items-start space-x-3">
                            <Info className="text-blue-400 mt-0.5" size={20} />
                            <div>
                                <h4 className="text-blue-400 font-medium">How This Improves Future Assessments</h4>
                                <div className="text-blue-300 text-sm mt-2 space-y-1">
                                    <div className="flex items-center space-x-2">
                                        <CheckCircle className="text-green-400" size={14} />
                                        <span>Unknown components will be recognized automatically</span>
                                    </div>
                                    <div className="flex items-center space-x-2">
                                        <CheckCircle className="text-green-400" size={14} />
                                        <span>Reduces false positives in naming convention findings</span>
                                    </div>
                                    <div className="flex items-center space-x-2">
                                        <CheckCircle className="text-green-400" size={14} />
                                        <span>Provides better context-aware recommendations</span>
                                    </div>
                                    <div className="flex items-center space-x-2">
                                        <CheckCircle className="text-green-400" size={14} />
                                        <span>Learning applies only to {client?.Name || 'this client'}</span>
                                    </div>
                                </div>
                            </div>
                        </div>
                    </div>
                </div>

                {/* Footer */}
                <div className="flex items-center justify-between p-6 border-t border-gray-700">
                    <div className="text-sm text-gray-400">
                        Teaching the system improves accuracy for all future assessments
                    </div>
                    <div className="flex items-center space-x-3">
                        <button
                            type="button"
                            onClick={onClose}
                            className="px-4 py-2 text-gray-400 hover:text-white transition-colors"
                            disabled={isSaving}
                        >
                            Cancel
                        </button>
                        <button
                            onClick={handleSave}
                            disabled={isSaving || 
                                (componentType === 'random' && !(unknownComponent || newAbbreviation.trim())) ||
                                (componentType !== 'random' && (!newAbbreviation.trim() || !newFullName.trim()))
                            }
                            className="px-6 py-2 bg-yellow-600 hover:bg-yellow-700 text-black rounded font-medium flex items-center space-x-2 disabled:opacity-50 disabled:cursor-not-allowed"
                        >
                            {isSaving ? (
                                <>
                                    <div className="w-4 h-4 border-2 border-black border-t-transparent rounded-full animate-spin"></div>
                                    <span>Saving...</span>
                                </>
                            ) : (
                                <>
                                    <Save size={16} />
                                    <span>Save & Apply</span>
                                </>
                            )}
                        </button>
                    </div>
                </div>
            </div>
        </div>,
        document.body
    );
};

export default ImproveDetectionModal;