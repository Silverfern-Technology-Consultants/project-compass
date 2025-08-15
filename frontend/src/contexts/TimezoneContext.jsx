import React, { createContext, useContext } from 'react';
import { useAuth } from './AuthContext';

/**
 * Timezone utility functions and context
 */

// Default timezone if user hasn't set one
const DEFAULT_TIMEZONE = 'America/New_York';

/**
 * Available timezone options
 */
export const TIMEZONES = [
    { value: 'America/New_York', label: 'Eastern Time (UTC-5/-4)' },
    { value: 'America/Chicago', label: 'Central Time (UTC-6/-5)' },
    { value: 'America/Denver', label: 'Mountain Time (UTC-7/-6)' },
    { value: 'America/Los_Angeles', label: 'Pacific Time (UTC-8/-7)' },
    { value: 'UTC', label: 'UTC (GMT+0)' },
    { value: 'Europe/London', label: 'London Time (UTC+0/+1)' },
    { value: 'Europe/Berlin', label: 'Central European Time (UTC+1/+2)' },
    { value: 'Asia/Tokyo', label: 'Japan Time (UTC+9)' },
    { value: 'Australia/Sydney', label: 'Sydney Time (UTC+10/+11)' }
];

/**
 * Get user's timezone preference from user object
 */
export const getUserTimezone = (user) => {
    return user?.TimeZone || user?.timezone || DEFAULT_TIMEZONE;
};

/**
 * Formats a date string according to user's timezone preference
 * @param {string|Date} dateInput - ISO date string or Date object from backend
 * @param {object} user - User object with timezone preference
 * @param {object} options - Formatting options
 * @returns {string} Formatted date string
 */
export const formatDateWithTimezone = (dateInput, user, options = {}) => {
    if (!dateInput) return 'Unknown';
    
    try {
        const date = dateInput instanceof Date ? dateInput : new Date(dateInput);
        const timezone = getUserTimezone(user);
        
        // Default formatting options
        const formatOptions = {
            year: 'numeric',
            month: 'short',
            day: 'numeric',
            hour: '2-digit',
            minute: '2-digit',
            timeZoneName: 'short',
            timeZone: timezone,
            ...options
        };
        
        return date.toLocaleString('en-US', formatOptions);
    } catch (error) {
        console.error('Error formatting date:', error);
        return String(dateInput); // Fallback to original string
    }
};

/**
 * Formats a date for display in assessment details
 * @param {string|Date} dateInput - ISO date string or Date object from backend
 * @param {object} user - User object with timezone preference
 * @returns {string} Formatted date string
 */
export const formatAssessmentDate = (dateInput, user) => {
    return formatDateWithTimezone(dateInput, user, {
        year: 'numeric',
        month: 'short',
        day: 'numeric',
        hour: '2-digit',
        minute: '2-digit',
        timeZoneName: 'short'
    });
};

/**
 * Gets relative time (e.g., "2 hours ago") with timezone awareness
 * @param {string|Date} dateInput - ISO date string or Date object from backend
 * @param {object} user - User object with timezone preference
 * @returns {string} Relative time string
 */
export const getRelativeTime = (dateInput, user) => {
    if (!dateInput) return 'Unknown time';
    
    try {
        const date = dateInput instanceof Date ? dateInput : new Date(dateInput);
        const now = new Date();
        const diffMs = now - date;
        
        if (diffMs < 60000) { // Less than 1 minute
            return 'Just now';
        } else if (diffMs < 3600000) { // Less than 1 hour
            const minutes = Math.floor(diffMs / 60000);
            return `${minutes} minute${minutes !== 1 ? 's' : ''} ago`;
        } else if (diffMs < 86400000) { // Less than 1 day
            const hours = Math.floor(diffMs / 3600000);
            return `${hours} hour${hours !== 1 ? 's' : ''} ago`;
        } else if (diffMs < 604800000) { // Less than 1 week
            const days = Math.floor(diffMs / 86400000);
            return `${days} day${days !== 1 ? 's' : ''} ago`;
        } else {
            // For older dates, show the formatted date
            return formatAssessmentDate(dateInput, user);
        }
    } catch (error) {
        console.error('Error calculating relative time:', error);
        return 'Unknown time';
    }
};

/**
 * Formats time only (no date) with timezone
 * @param {string|Date} dateInput - ISO date string or Date object
 * @param {object} user - User object with timezone preference
 * @returns {string} Formatted time string
 */
export const formatTimeWithTimezone = (dateInput, user) => {
    return formatDateWithTimezone(dateInput, user, {
        hour: '2-digit',
        minute: '2-digit',
        timeZoneName: 'short'
    });
};

/**
 * Formats date only (no time) with timezone
 * @param {string|Date} dateInput - ISO date string or Date object
 * @param {object} user - User object with timezone preference
 * @returns {string} Formatted date string
 */
export const formatDateOnlyWithTimezone = (dateInput, user) => {
    return formatDateWithTimezone(dateInput, user, {
        year: 'numeric',
        month: 'short',
        day: 'numeric'
    });
};

/**
 * Context for timezone utilities
 */
const TimezoneContext = createContext();

export const useTimezone = () => {
    const { user } = useAuth(); // Always call at top level
    const context = useContext(TimezoneContext);
    
    if (!context) {
        // If context is not available, create minimal implementation
        return {
            formatDate: (date, options) => formatDateWithTimezone(date, user, options),
            formatAssessmentDate: (date) => formatAssessmentDate(date, user),
            getRelativeTime: (date) => getRelativeTime(date, user),
            formatTime: (date) => formatTimeWithTimezone(date, user),
            formatDateOnly: (date) => formatDateOnlyWithTimezone(date, user),
            userTimezone: getUserTimezone(user),
            timezones: TIMEZONES
        };
    }
    return context;
};

export const TimezoneProvider = ({ children }) => {
    const { user } = useAuth();
    
    const value = {
        formatDate: (date, options) => formatDateWithTimezone(date, user, options),
        formatAssessmentDate: (date) => formatAssessmentDate(date, user),
        getRelativeTime: (date) => getRelativeTime(date, user),
        formatTime: (date) => formatTimeWithTimezone(date, user),
        formatDateOnly: (date) => formatDateOnlyWithTimezone(date, user),
        userTimezone: getUserTimezone(user),
        timezones: TIMEZONES
    };
    
    return (
        <TimezoneContext.Provider value={value}>
            {children}
        </TimezoneContext.Provider>
    );
};

// All functions are already exported individually above
