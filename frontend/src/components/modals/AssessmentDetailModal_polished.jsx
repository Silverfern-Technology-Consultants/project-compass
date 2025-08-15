import React, { useState, useEffect } from 'react';
import { createPortal } from 'react-dom';
import {
    X, CheckCircle, AlertTriangle, XCircle, FileText, BarChart3, Download,
    Eye, Filter, Search, ChevronLeft, ChevronRight, Server, Database, Network,
    ChevronDown, ChevronUp, Settings, User, Clock, Shield, AlertCircle,
    Target, Zap, Calendar, TrendingUp, Tag, Folder, MapPin, Activity,
    HardDrive, Globe
} from 'lucide-react';
import { assessmentApi, apiUtils } from '../../services/apiService';

const getResourceTypeInfo = (resourceType) => {
    const type = resourceType.toLowerCase();

    //