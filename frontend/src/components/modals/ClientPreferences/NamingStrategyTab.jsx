import React from 'react';
import { Plus, ArrowUp, ArrowDown, Trash2, X } from 'lucide-react';

const NamingStrategyTab = ({ formData, setFormData, handleRadioChange }) => {
    // Available component types for naming scheme builder
    const availableComponentTypes = [
        { 
            type: 'company', 
            name: 'Company/Organization', 
            description: 'Company or organization identifier',
            examples: ['acme', 'corp', 'mycompany'],
            defaultFormat: '3-8 letter abbreviation'
        },
        { 
            type: 'environment', 
            name: 'Environment', 
            description: 'Deployment environment classification',
            examples: ['prod', 'dev', 'test', 'staging'],
            defaultFormat: 'lowercase standard values'
        },
        { 
            type: 'service', 
            name: 'Service/Application', 
            description: 'Service or application identifier',
            examples: ['web', 'api', 'database', 'auth'],
            defaultFormat: 'lowercase descriptive'
        },
        { 
            type: 'resource-type', 
            name: 'Resource Type', 
            description: 'Azure resource type abbreviation',
            examples: ['vm', 'st', 'kv', 'app', 'sql'],
            defaultFormat: 'official Azure abbreviations'
        },
        { 
            type: 'instance', 
            name: 'Instance Number', 
            description: 'Sequential instance identifier',
            examples: ['01', '02', '001', '1', '2'],
            defaultFormat: 'numbers (with or without padding)'
        },
        { 
            type: 'location', 
            name: 'Location/Region', 
            description: 'Geographic location or Azure region',
            examples: ['eus', 'wus', 'eastus', 'central'],
            defaultFormat: 'region abbreviation'
        }
    ];

    const namingTemplates = {
        standardized: {
            title: "Standardized Naming",
            description: "All resources follow consistent patterns (recommended for new environments)",
            patterns: ["[company]-[env]-[service]-[number]", "[env]-[resourcetype]-[purpose]"],
            example: "acme-prod-web-01, dev-vm-database"
        },
        mixed: {
            title: "Mixed Conventions",
            description: "Allow multiple naming styles (good for existing environments)",
            patterns: ["Flexible patterns allowed"],
            example: "CORP-AZ-BACKUP, DataProcessPlan, workers-sg"
        },
        legacy: {
            title: "Legacy Preservation",
            description: "Keep existing naming, focus on tagging for governance",
            patterns: ["No naming requirements"],
            example: "Existing names maintained"
        }
    };

    // Naming scheme management functions
    const addNamingComponent = (componentType) => {
        const newComponent = {
            componentType: componentType,
            position: (formData.namingScheme?.components?.length || 0) + 1,
            isRequired: true,
            allowedValues: [],
            defaultValue: ''
        };

        setFormData(prev => ({
            ...prev,
            namingScheme: {
                ...prev.namingScheme,
                components: [...(prev.namingScheme?.components || []), newComponent]
            }
        }));
    };

    const removeNamingComponent = (position) => {
        setFormData(prev => {
            const updatedComponents = (prev.namingScheme?.components || [])
                .filter(comp => comp.position !== position)
                .map((comp, index) => ({
                    ...comp,
                    position: index + 1
                }));

            return {
                ...prev,
                namingScheme: {
                    ...prev.namingScheme,
                    components: updatedComponents
                }
            };
        });
    };

    const moveNamingComponent = (fromPosition, toPosition) => {
        setFormData(prev => {
            const components = [...(prev.namingScheme?.components || [])];
            const fromIndex = components.findIndex(c => c.position === fromPosition);
            const toIndex = toPosition - 1;

            if (fromIndex !== -1 && toIndex >= 0 && toIndex < components.length) {
                const [moved] = components.splice(fromIndex, 1);
                components.splice(toIndex, 0, moved);

                const reorderedComponents = components.map((comp, index) => ({
                    ...comp,
                    position: index + 1
                }));

                return {
                    ...prev,
                    namingScheme: {
                        ...prev.namingScheme,
                        components: reorderedComponents
                    }
                };
            }

            return prev;
        });
    };

    const updateNamingComponent = (position, field, value) => {
        setFormData(prev => ({
            ...prev,
            namingScheme: {
                ...prev.namingScheme,
                components: (prev.namingScheme?.components || []).map(comp => 
                    comp.position === position 
                        ? { ...comp, [field]: value }
                        : comp
                )
            }
        }));
    };

    // Company names management
    const handleAddCompanyName = () => {
        const companyName = formData.companyNameInput?.trim();
        if (companyName && !(formData.acceptedCompanyNames || []).includes(companyName)) {
            setFormData(prev => ({
                ...prev,
                acceptedCompanyNames: [...(prev.acceptedCompanyNames || []), companyName],
                companyNameInput: ''
            }));
        }
    };

    const handleRemoveCompanyName = (companyToRemove) => {
        setFormData(prev => ({
            ...prev,
            acceptedCompanyNames: (prev.acceptedCompanyNames || []).filter(company => company !== companyToRemove)
        }));
    };

    const generateNamingExample = () => {
        const separator = formData.namingScheme?.separator || '-';
        if (!formData.namingScheme?.components || formData.namingScheme.components.length === 0) {
            return '';
        }
        
        const caseFormat = formData.namingScheme?.caseFormat || 'lowercase';
        
        const components = formData.namingScheme.components
            .sort((a, b) => a.position - b.position)
            .map(comp => {
                switch (comp.componentType) {
                    case 'company': return formData.acceptedCompanyNames?.[0] || 'abc';
                    case 'environment': return 'prod';
                    case 'service': return 'web';
                    case 'resource-type': return 'vm';
                    case 'instance': return '01';
                    case 'location': return 'eus';
                    default: return comp.defaultValue || 'comp';
                }
            });
            
        const example = components.join(separator);
        
        // Apply case formatting
        switch (caseFormat) {
            case 'uppercase':
                return example.toUpperCase();
            case 'PascalCase':
                return example.split(separator)
                    .map(part => part.charAt(0).toUpperCase() + part.slice(1).toLowerCase())
                    .join(separator);
            case 'camelCase':
                const parts = example.split(separator);
                return parts[0].toLowerCase() + 
                    parts.slice(1)
                        .map(part => part.charAt(0).toUpperCase() + part.slice(1).toLowerCase())
                        .join('');
            case 'lowercase':
            default:
                return example.toLowerCase();
        }
    };

    return (
        <div className="space-y-6">
            {/* Naming Convention Approach */}
            <div>
            <h3 className="text-white font-medium mb-3">Naming Convention Approach</h3>
            <div className="grid grid-cols-1 gap-2">
            {Object.entries(namingTemplates).map(([key, template]) => (
            <label key={key} className="flex items-start space-x-3 p-3 bg-gray-700 rounded cursor-pointer hover:bg-gray-600">
            <input
            type="radio"
            name="namingStyle"
            value={key}
            checked={formData.namingStyle === key}
            onChange={(e) => handleRadioChange('namingStyle', e.target.value)}
            className="mt-0.5 text-yellow-600 focus:ring-yellow-600"
            />
            <div className="flex-1 min-w-0">
            <div className="text-white text-sm font-medium">{template.title}</div>
            <div className="text-gray-300 text-xs mt-1 leading-relaxed">{template.description}</div>
            <div className="text-gray-400 text-xs mt-1">
            <strong>Example:</strong> {template.example}
            </div>
            </div>
            </label>
            ))}
            </div>
            </div>

            {/* Custom Naming Scheme Builder */}
            <div className="border-t border-gray-700 pt-6">
                <div className="bg-gray-700 rounded-lg p-4 space-y-4">
                    <h4 className="text-white font-medium mb-3">Custom Naming Scheme Builder</h4>
                    <p className="text-gray-400 text-sm mb-4">
                        Define the exact order and rules for your naming components. Leave empty to use standard naming patterns.
                    </p>
                        
                    {/* Separator Configuration */}
                    <div className="grid grid-cols-2 gap-4">
                        <div>
                        <label className="block text-gray-300 text-sm font-medium mb-2">Separator</label>
                        <select
                        value={formData.namingScheme.separator}
                        onChange={(e) => setFormData(prev => ({
                        ...prev,
                        namingScheme: { ...prev.namingScheme, separator: e.target.value }
                        }))}
                        disabled={formData.namingScheme.caseFormat === 'camelCase'}
                            className="w-full px-3 py-2 bg-gray-600 border border-gray-500 rounded text-white disabled:opacity-50 disabled:cursor-not-allowed"
                        >
                        <option value="-">Hyphen (-)</option>
                        <option value="_">Underscore (_)</option>
                            <option value="">No separator</option>
                            </select>
                                        {formData.namingScheme.caseFormat === 'camelCase' && (
                                            <p className="text-gray-400 text-xs mt-1">
                                                Separator disabled for camelCase formatting
                                            </p>
                                        )}
                                    </div>
                        <div>
                            <label className="block text-gray-300 text-sm font-medium mb-2">Case Format</label>
                            <select
                            value={formData.namingScheme.caseFormat}
                            onChange={(e) => {
                            const newCaseFormat = e.target.value;
                            setFormData(prev => ({
                                    ...prev,
                                    namingScheme: { 
                                            ...prev.namingScheme, 
                                                        caseFormat: newCaseFormat,
                                                        // Auto-set separator to empty for camelCase
                                                        separator: newCaseFormat === 'camelCase' ? '' : prev.namingScheme.separator
                                                    }
                                                }));
                                            }}
                                            className="w-full px-3 py-2 bg-gray-600 border border-gray-500 rounded text-white"
                                        >
                                <option value="lowercase">lowercase</option>
                                <option value="uppercase">UPPERCASE</option>
                                <option value="PascalCase">PascalCase</option>
                                <option value="camelCase">camelCase</option>
                            </select>
                        </div>
                    </div>

                    {/* Component Builder */}
                    <div>
                        <div className="flex items-center justify-between mb-3">
                            <h5 className="text-gray-300 font-medium">Naming Components</h5>
                            <div className="text-gray-400 text-sm">
                                {(formData.namingScheme?.components || []).length} components
                            </div>
                        </div>

                        {/* Existing Components */}
                        {(formData.namingScheme?.components || []).length > 0 && (
                            <div className="space-y-2 mb-4">
                                {(formData.namingScheme?.components || [])
                                    .sort((a, b) => a.position - b.position)
                                    .map((component) => {
                                        const componentInfo = availableComponentTypes.find(t => t.type === component.componentType);
                                        return (
                                            <div key={component.position} className="bg-gray-600 rounded-lg p-3 flex items-center space-x-3">
                                                <div className="flex items-center space-x-2">
                                                    <div className="w-8 h-8 bg-yellow-600 text-black rounded-full flex items-center justify-center text-sm font-bold">
                                                        {component.position}
                                                    </div>
                                                </div>
                                                <div className="flex-1">
                                                    <div className="text-white font-medium">{componentInfo?.name || component.componentType}</div>
                                                    <div className="text-gray-400 text-xs">{componentInfo?.description}</div>
                                                </div>
                                                <div className="flex items-center space-x-2">
                                                    <label className="flex items-center space-x-1">
                                                        <input
                                                            type="checkbox"
                                                            checked={component.isRequired}
                                                            onChange={(e) => updateNamingComponent(component.position, 'isRequired', e.target.checked)}
                                                            className="text-yellow-600 text-xs"
                                                        />
                                                        <span className="text-gray-300 text-xs">Required</span>
                                                    </label>
                                                    <button
                                                        onClick={() => moveNamingComponent(component.position, Math.max(1, component.position - 1))}
                                                        disabled={component.position === 1}
                                                        className="p-1 text-gray-400 hover:text-white disabled:opacity-50"
                                                    >
                                                        <ArrowUp size={14} />
                                                    </button>
                                                    <button
                                                        onClick={() => moveNamingComponent(component.position, Math.min((formData.namingScheme?.components || []).length, component.position + 1))}
                                                        disabled={component.position === (formData.namingScheme?.components || []).length}
                                                        className="p-1 text-gray-400 hover:text-white disabled:opacity-50"
                                                    >
                                                        <ArrowDown size={14} />
                                                    </button>
                                                    <button
                                                        onClick={() => removeNamingComponent(component.position)}
                                                        className="p-1 text-red-400 hover:text-red-300"
                                                    >
                                                        <Trash2 size={14} />
                                                    </button>
                                                </div>
                                            </div>
                                        );
                                    })}
                            </div>
                        )}

                        {/* Add Component */}
                        <div className="border-2 border-dashed border-gray-600 rounded-lg p-4">
                            <h6 className="text-gray-300 font-medium mb-3">Add Component</h6>
                            <div className="grid grid-cols-2 gap-2">
                                {availableComponentTypes
                                    .filter(type => !(formData.namingScheme?.components || []).some(comp => comp.componentType === type.type))
                                    .map(componentType => (
                                        <button
                                            key={componentType.type}
                                            onClick={() => addNamingComponent(componentType.type)}
                                            className="flex items-center space-x-2 p-2 bg-gray-800 hover:bg-gray-600 rounded text-sm text-gray-300 hover:text-white transition-colors"
                                        >
                                            <Plus size={14} />
                                            <span>{componentType.name}</span>
                                        </button>
                                    ))
                                }
                            </div>
                            {(formData.namingScheme?.components || []).length >= availableComponentTypes.length && (
                                <div className="text-gray-400 text-sm mt-2">
                                    All component types have been added
                                </div>
                            )}
                        </div>

                        {/* Live Preview */}
                        {(formData.namingScheme?.components || []).length > 0 && (
                            <div className="bg-gray-800 rounded-lg p-4 border border-gray-600">
                                <h6 className="text-gray-300 font-medium mb-2">Live Preview</h6>
                                <div className="bg-gray-900 rounded px-3 py-2 font-mono text-green-400">
                                    {generateNamingExample() || 'Add components to see preview'}
                                </div>
                                <div className="text-gray-400 text-xs mt-2">
                                    Example for a virtual machine resource using {formData.namingScheme?.caseFormat || 'lowercase'} formatting
                                </div>
                            </div>
                        )}
                    </div>

                    {/* Company Names Configuration */}
                    <div>
                        <h5 className="text-gray-300 font-medium mb-3">Accepted Company Names</h5>
                        <p className="text-gray-400 text-sm mb-3">
                            Define valid company identifiers for naming validation
                        </p>
                        
                        {/* Add Company Name */}
                        <div className="flex space-x-3 mb-3">
                            <input
                                type="text"
                                value={formData.companyNameInput || ''}
                                onChange={(e) => setFormData(prev => ({ 
                                    ...prev, 
                                    companyNameInput: e.target.value 
                                }))}
                                onKeyPress={(e) => e.key === 'Enter' && handleAddCompanyName()}
                                placeholder="Enter company name or abbreviation..."
                                className="flex-1 px-3 py-2 bg-gray-600 border border-gray-500 rounded text-white placeholder-gray-400"
                            />
                            <button
                                onClick={handleAddCompanyName}
                                disabled={!(formData.companyNameInput || '').trim()}
                                className="px-4 py-2 bg-yellow-600 hover:bg-yellow-700 disabled:bg-gray-600 disabled:cursor-not-allowed text-black rounded font-medium"
                            >
                                Add
                            </button>
                        </div>
                        <p className="text-gray-400 text-xs mb-3">
                            Note: Company name matching is case-insensitive ("Acme" matches "acme", "ACME", etc.)
                        </p>

                        {/* Display Company Names */}
                        {(formData.acceptedCompanyNames || []).length > 0 && (
                            <div className="flex flex-wrap gap-2">
                                {(formData.acceptedCompanyNames || []).map(company => (
                                    <div key={company} className="flex items-center space-x-2 bg-blue-600 text-white px-3 py-1 rounded-full text-sm">
                                        <span>{company}</span>
                                        <button
                                            onClick={() => handleRemoveCompanyName(company)}
                                            className="text-white hover:text-gray-200"
                                        >
                                            <X size={14} />
                                        </button>
                                    </div>
                                ))}
                            </div>
                        )}
                    </div>
                </div>
            </div>
        </div>
    );
};

export default NamingStrategyTab;