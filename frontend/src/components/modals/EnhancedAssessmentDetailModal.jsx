import React from 'react';
import ResourceGovernanceAssessmentModal from './ResourceGovernanceAssessmentModal';
import IdentityAccessAssessmentModal from './IdentityAccessAssessmentModal';
import BusinessContinuityAssessmentModal from './BusinessContinuityAssessmentModal';
import SecurityPostureAssessmentModal from './SecurityPostureAssessmentModal';
import AssessmentDetailModal from './AssessmentDetailModal'; // Fallback to existing modal

const EnhancedAssessmentDetailModal = ({ isOpen, onClose, assessment }) => {
    // Determine assessment category based on type
    const getAssessmentCategory = () => {
        if (!assessment) return null;
        
        const type = assessment.type || assessment.AssessmentType;
        
        // Category 0: Resource Governance
        if (type === 0 || type === 1 || type === 2 || 
            type === 'NamingConvention' || type === 'Tagging' || type === 'Full') {
            return 'governance';
        }
        
        // Category 1: Identity & Access Management  
        if (type === 3 || type === 4 || type === 5 || type === 6 || type === 7 ||
            type === 'EnterpriseApplications' || type === 'StaleUsersDevices' || 
            type === 'ResourceIAMRBAC' || type === 'ConditionalAccess' || type === 'IdentityFull') {
            return 'identity';
        }
        
        // Category 2: Business Continuity
        if (type === 8 || type === 9 || type === 10 ||
            type === 'BackupCoverage' || type === 'RecoveryConfiguration' || type === 'BusinessContinuityFull') {
            return 'continuity';
        }
        
        // Category 3: Security Posture
        if (type === 11 || type === 12 || type === 13 ||
            type === 'NetworkSecurity' || type === 'DefenderForCloud' || type === 'SecurityFull') {
            return 'security';
        }
        
        // Default fallback
        return 'fallback';
    };

    const category = getAssessmentCategory();

    // Render the appropriate modal based on category
    switch (category) {
        case 'governance':
            return (
                <ResourceGovernanceAssessmentModal 
                    isOpen={isOpen} 
                    onClose={onClose} 
                    assessment={assessment} 
                />
            );
            
        case 'identity':
            return (
                <IdentityAccessAssessmentModal 
                    isOpen={isOpen} 
                    onClose={onClose} 
                    assessment={assessment} 
                />
            );
            
        case 'continuity':
            return (
                <BusinessContinuityAssessmentModal 
                    isOpen={isOpen} 
                    onClose={onClose} 
                    assessment={assessment} 
                />
            );
            
        case 'security':
            return (
                <SecurityPostureAssessmentModal 
                    isOpen={isOpen} 
                    onClose={onClose} 
                    assessment={assessment} 
                />
            );
            
        default:
            // Fallback to existing modal for unknown types
            return (
                <AssessmentDetailModal 
                    isOpen={isOpen} 
                    onClose={onClose} 
                    assessment={assessment} 
                />
            );
    }
};

export default EnhancedAssessmentDetailModal;