import React from 'react';
import { X } from 'lucide-react';

const TaggingStrategyTab = ({ formData, setFormData, handleRadioChange, handleCheckboxChange }) => {
    const taggingTemplates = {
        comprehensive: {
            title: "Comprehensive Tagging",
            description: "Detailed tagging for cost allocation and governance",
            tags: ["Environment", "CostCenter", "Owner", "Application", "Project", "Backup", "Compliance"]
        },
        basic: {
            title: "Essential Tagging",
            description: "Core tags for basic governance and cost tracking",
            tags: ["Environment", "CostCenter", "Owner", "Application"]
        },
        minimal: {
            title: "Minimal Tagging",
            description: "Only environment tagging for basic organization",
            tags: ["Environment"]
        },
        custom: {
            title: "Custom Selection",
            description: "Choose specific tags that meet your needs",
            tags: []
        }
    };

    const availableTags = [
        { name: "Environment", description: "prod, dev, test, staging" },
        { name: "CostCenter", description: "Department or cost allocation code" },
        { name: "Owner", description: "Resource owner or team responsible" },
        { name: "Application", description: "Application or service name" },
        { name: "Project", description: "Project or initiative name" },
        { name: "Backup", description: "Backup schedule or requirement" },
        { name: "Compliance", description: "Compliance framework requirements" },
        { name: "Location", description: "Geographic location or region" },
        { name: "Department", description: "Business department" },
        { name: "Schedule", description: "Operating schedule (24x7, business hours)" },
        { name: "Criticality", description: "Business criticality level" },
        { name: "DataClassification", description: "Data sensitivity level" }
    ];

    const handleAddCustomTag = () => {
        const tagName = formData.customTagInput.trim();
        if (tagName && !formData.customTags.includes(tagName) && !formData.selectedTags.includes(tagName)) {
            setFormData(prev => ({
                ...prev,
                customTags: [...prev.customTags, tagName],
                selectedTags: [...prev.selectedTags, tagName],
                customTagInput: ''
            }));
        }
    };

    const handleCustomTagInputChange = (e) => {
        setFormData(prev => ({
            ...prev,
            customTagInput: e.target.value
        }));
    };

    const handleCustomTagKeyPress = (e) => {
        if (e.key === 'Enter') {
            e.preventDefault();
            handleAddCustomTag();
        }
    };

    const handleRemoveCustomTag = (tagToRemove) => {
        setFormData(prev => ({
            ...prev,
            customTags: prev.customTags.filter(tag => tag !== tagToRemove),
            selectedTags: prev.selectedTags.filter(tag => tag !== tagToRemove)
        }));
    };

    return (
        <div className="space-y-6">
            {/* Tagging Approach */}
            <div>
                <h3 className="text-white font-medium mb-4">Tagging Strategy</h3>
                <div className="space-y-3">
                    {Object.entries(taggingTemplates).map(([key, template]) => (
                        <label key={key} className="flex items-start space-x-3 p-4 bg-gray-700 rounded-lg cursor-pointer hover:bg-gray-600">
                            <input
                                type="radio"
                                name="taggingApproach"
                                value={key}
                                checked={formData.taggingApproach === key}
                                onChange={(e) => handleRadioChange('taggingApproach', e.target.value)}
                                className="mt-1 text-yellow-600 focus:ring-yellow-600"
                            />
                            <div className="flex-1">
                                <div className="text-white font-medium">{template.title}</div>
                                <div className="text-gray-300 text-sm mt-1">{template.description}</div>
                                {template.tags.length > 0 && (
                                    <div className="text-gray-400 text-xs mt-2">
                                        <strong>Includes:</strong> {template.tags.join(', ')}
                                    </div>
                                )}
                            </div>
                        </label>
                    ))}
                </div>
            </div>

            {/* Tag Selection */}
            <div>
                <h3 className="text-white font-medium mb-4">Required Tags</h3>
                <p className="text-gray-400 text-sm mb-4">
                    Select the tags that must be present on all resources for proper governance and cost tracking.
                </p>
                <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
                    {availableTags.map(tag => (
                        <label key={tag.name} className="flex items-start space-x-3 p-3 bg-gray-700 rounded cursor-pointer hover:bg-gray-600">
                            <input
                                type="checkbox"
                                checked={formData.selectedTags.includes(tag.name)}
                                onChange={() => handleCheckboxChange('selectedTags', tag.name)}
                                className="mt-1 text-yellow-600 focus:ring-yellow-600"
                            />
                            <div className="flex-1">
                                <div className="text-gray-300 text-sm font-medium">{tag.name}</div>
                                <div className="text-gray-400 text-xs mt-1">{tag.description}</div>
                            </div>
                        </label>
                    ))}
                </div>
            </div>

            {/* Custom Tags */}
            <div>
                <h3 className="text-white font-medium mb-4">Custom Tags</h3>
                <p className="text-gray-400 text-sm mb-4">
                    Add organization-specific tags that aren't covered by the standard options.
                </p>

                {/* Custom Tag Input */}
                <div className="mb-4">
                    <div className="flex space-x-3">
                        <input
                            type="text"
                            value={formData.customTagInput}
                            onChange={handleCustomTagInputChange}
                            onKeyPress={handleCustomTagKeyPress}
                            placeholder="Enter custom tag name..."
                            className="flex-1 px-3 py-2 bg-gray-700 border border-gray-600 rounded text-white placeholder-gray-400 focus:outline-none focus:ring-2 focus:ring-yellow-600"
                        />
                        <button
                            onClick={handleAddCustomTag}
                            disabled={!formData.customTagInput.trim()}
                            className="px-4 py-2 bg-yellow-600 hover:bg-yellow-700 disabled:bg-gray-600 disabled:cursor-not-allowed text-black rounded text-sm font-medium"
                        >
                            Add Tag
                        </button>
                    </div>
                </div>

                {/* Display Custom Tags */}
                {formData.customTags.length > 0 && (
                    <div className="mb-4">
                        <div className="flex flex-wrap gap-2">
                            {formData.customTags.map(tag => (
                                <div key={tag} className="flex items-center space-x-2 bg-yellow-600 text-black px-3 py-1 rounded-full text-sm">
                                    <span>{tag}</span>
                                    <button
                                        onClick={() => handleRemoveCustomTag(tag)}
                                        className="text-black hover:text-gray-700"
                                    >
                                        <X size={14} />
                                    </button>
                                </div>
                            ))}
                        </div>
                    </div>
                )}
            </div>

            {/* Tag Compliance */}
            <div>
                <h3 className="text-white font-medium mb-4">Tag Compliance</h3>
                <label className="flex items-center space-x-3">
                    <input
                        type="checkbox"
                        checked={formData.enforceTagCompliance}
                        onChange={(e) => setFormData(prev => ({ ...prev, enforceTagCompliance: e.target.checked }))}
                        className="text-yellow-600 focus:ring-yellow-600"
                    />
                    <div>
                        <span className="text-white font-medium">Enforce Tag Compliance</span>
                        <p className="text-gray-400 text-sm">Flag resources as non-compliant if missing required tags</p>
                    </div>
                </label>
            </div>
        </div>
    );
};

export default TaggingStrategyTab;