import React, { useState, useRef, useEffect } from 'react';
import { Play, ChevronDown, Shield, Database, Activity, Users } from 'lucide-react';

const AssessmentTypeDropdown = ({ onSelectType, selectedClient }) => {
    const [isOpen, setIsOpen] = useState(false);
    const [dropdownPosition, setDropdownPosition] = useState('left');
    const dropdownRef = useRef(null);
    const buttonRef = useRef(null);

    const assessmentCategories = [
        {
            id: 'resource-governance',
            name: 'Resource Governance Assessment',
            description: 'Analyze naming conventions and tagging compliance',
            icon: Database,
            color: 'text-blue-400'
        },
        {
            id: 'identity-access',
            name: 'Identity Access Assessment', 
            description: 'Evaluate RBAC, permissions, and identity management',
            icon: Users,
            color: 'text-green-400'
        },
        {
            id: 'bcdr',
            name: 'BCDR Assessment',
            description: 'Review backup, disaster recovery, and business continuity',
            icon: Activity,
            color: 'text-yellow-400'
        },
        {
            id: 'security-posture',
            name: 'Security Posture Assessment',
            description: 'Comprehensive security analysis and threat assessment',
            icon: Shield,
            color: 'text-red-400'
        }
    ];

    // Close dropdown when clicking outside
    useEffect(() => {
        const handleClickOutside = (event) => {
            if (dropdownRef.current && !dropdownRef.current.contains(event.target)) {
                setIsOpen(false);
            }
        };

        document.addEventListener('mousedown', handleClickOutside);
        return () => document.removeEventListener('mousedown', handleClickOutside);
    }, []);

    // Calculate dropdown position when opening
    const calculatePosition = () => {
        if (buttonRef.current) {
            const buttonRect = buttonRef.current.getBoundingClientRect();
            const dropdownWidth = 320; // w-80 = 320px
            const viewportWidth = window.innerWidth;
            
            // If there's not enough space on the right, position it to the left
            if (buttonRect.right + dropdownWidth > viewportWidth - 20) {
                setDropdownPosition('right');
            } else {
                setDropdownPosition('left');
            }
        }
    };

    const handleSelectCategory = (category) => {
        setIsOpen(false);
        onSelectType(category.id);
    };

    const handleButtonClick = () => {
        if (!isOpen) {
            calculatePosition();
        }
        setIsOpen(!isOpen);
    };

    return (
        <div className="relative" ref={dropdownRef}>
            <button
                ref={buttonRef}
                onClick={handleButtonClick}
                className="flex items-center space-x-2 bg-yellow-600 hover:bg-yellow-700 text-black px-4 py-2 rounded font-medium transition-colors"
            >
                <Play size={16} />
                <span>New Assessment</span>
                <ChevronDown size={14} className={`transition-transform ${isOpen ? 'rotate-180' : ''}`} />
            </button>

            {isOpen && (
                <div className={`absolute ${dropdownPosition === 'right' ? 'right-0' : 'left-0'} top-full mt-2 w-80 bg-gray-800 border border-gray-700 rounded-lg shadow-xl z-50`}>
                    <div className="p-2">
                        <div className="p-3 border-b border-gray-700">
                            <h3 className="text-sm font-medium text-gray-300">Choose Assessment Category</h3>
                            {selectedClient && (
                                <p className="text-xs text-gray-400 mt-1">
                                    Assessment for: <span className="text-white">{selectedClient.Name}</span>
                                </p>
                            )}
                        </div>
                        <div className="py-2">
                            {assessmentCategories.map((category) => {
                                const IconComponent = category.icon;
                                return (
                                    <button
                                        key={category.id}
                                        onClick={() => handleSelectCategory(category)}
                                        className="w-full p-3 text-left hover:bg-gray-700 rounded-lg transition-colors group"
                                    >
                                        <div className="flex items-start space-x-3">
                                            <IconComponent size={20} className={`${category.color} mt-0.5`} />
                                            <div className="flex-1">
                                                <h4 className="text-sm font-medium text-white group-hover:text-yellow-400 transition-colors">
                                                    {category.name}
                                                </h4>
                                                <p className="text-xs text-gray-400 mt-1">
                                                    {category.description}
                                                </p>
                                            </div>
                                        </div>
                                    </button>
                                );
                            })}
                        </div>
                    </div>
                </div>
            )}
        </div>
    );
};

export default AssessmentTypeDropdown;