import React, { useState } from 'react';
import { Shield, BarChart3, Users, CheckCircle, ArrowRight, Menu, X, Star, Award, TrendingUp } from 'lucide-react';

const NavigationBar = () => {
    const [isMenuOpen, setIsMenuOpen] = useState(false);

    return (
        <nav className="bg-gray-950 border-b border-gray-800">
            <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
                <div className="flex items-center justify-between h-16">
                    {/* Logo */}
                    <div className="flex items-center space-x-3">
                        <div className="w-8 h-8 bg-gradient-to-br from-yellow-400 to-yellow-600 rounded flex items-center justify-center">
                            <span className="text-black font-bold text-sm">C</span>
                        </div>
                        <span className="text-white font-bold text-xl">Compass</span>
                    </div>

                    {/* Desktop Navigation */}
                    <div className="hidden md:block">
                        <div className="ml-10 flex items-baseline space-x-8">
                            <a href="#features" className="text-gray-300 hover:text-white transition-colors">Features</a>
                            <a href="#pricing" className="text-gray-300 hover:text-white transition-colors">Pricing</a>
                            <a href="#about" className="text-gray-300 hover:text-white transition-colors">About</a>
                            <a href="#contact" className="text-gray-300 hover:text-white transition-colors">Contact</a>
                        </div>
                    </div>

                    {/* Desktop CTA */}
                    <div className="hidden md:flex items-center space-x-4">
                        <button className="text-gray-300 hover:text-white transition-colors">Sign In</button>
                        <button className="bg-yellow-600 hover:bg-yellow-700 text-black px-4 py-2 rounded font-medium transition-colors">
                            Start Free Trial
                        </button>
                    </div>

                    {/* Mobile menu button */}
                    <div className="md:hidden">
                        <button
                            onClick={() => setIsMenuOpen(!isMenuOpen)}
                            className="text-gray-300 hover:text-white"
                        >
                            {isMenuOpen ? <X size={24} /> : <Menu size={24} />}
                        </button>
                    </div>
                </div>

                {/* Mobile menu */}
                {isMenuOpen && (
                    <div className="md:hidden border-t border-gray-800">
                        <div className="px-2 pt-2 pb-3 space-y-1">
                            <a href="#features" className="block px-3 py-2 text-gray-300 hover:text-white">Features</a>
                            <a href="#pricing" className="block px-3 py-2 text-gray-300 hover:text-white">Pricing</a>
                            <a href="#about" className="block px-3 py-2 text-gray-300 hover:text-white">About</a>
                            <a href="#contact" className="block px-3 py-2 text-gray-300 hover:text-white">Contact</a>
                            <div className="pt-4 pb-2 border-t border-gray-800 mt-4">
                                <button className="block w-full text-left px-3 py-2 text-gray-300 hover:text-white">Sign In</button>
                                <button className="block w-full mt-2 bg-yellow-600 hover:bg-yellow-700 text-black px-3 py-2 rounded font-medium">
                                    Start Free Trial
                                </button>
                            </div>
                        </div>
                    </div>
                )}
            </div>
        </nav>
    );
};

const HeroSection = () => {
    return (
        <div className="relative bg-gray-950 overflow-hidden">
            {/* Background Pattern */}
            <div className="absolute inset-0 bg-gradient-to-br from-gray-900 via-gray-950 to-black opacity-50"></div>

            <div className="relative max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-24">
                <div className="text-center">
                    <h1 className="text-4xl md:text-6xl font-bold text-white mb-6">
                        Azure Governance
                        <span className="block text-yellow-400">Made Simple</span>
                    </h1>
                    <p className="text-xl text-gray-400 mb-8 max-w-3xl mx-auto">
                        Compass automatically assesses your Azure environment for naming conventions,
                        tagging compliance, and security best practices. Get actionable insights in minutes, not hours.
                    </p>

                    <div className="flex flex-col sm:flex-row gap-4 justify-center items-center">
                        <button className="bg-yellow-600 hover:bg-yellow-700 text-black px-8 py-3 rounded-lg font-semibold text-lg transition-colors flex items-center space-x-2">
                            <span>Start Free Assessment</span>
                            <ArrowRight size={20} />
                        </button>
                        <button className="border border-gray-700 hover:border-gray-600 text-white px-8 py-3 rounded-lg font-semibold text-lg transition-colors">
                            Watch Demo
                        </button>
                    </div>

                    <div className="mt-12 flex justify-center items-center space-x-8 text-gray-500">
                        <div className="flex items-center space-x-2">
                            <CheckCircle size={16} className="text-green-400" />
                            <span>No Azure permissions required</span>
                        </div>
                        <div className="flex items-center space-x-2">
                            <CheckCircle size={16} className="text-green-400" />
                            <span>5-minute setup</span>
                        </div>
                        <div className="flex items-center space-x-2">
                            <CheckCircle size={16} className="text-green-400" />
                            <span>Instant results</span>
                        </div>
                    </div>
                </div>
            </div>
        </div>
    );
};

const StatsSection = () => {
    const stats = [
        { value: '1M+', label: 'Resources Analyzed' },
        { value: '500+', label: 'MSPs Served' },
        { value: '99.9%', label: 'Uptime' },
        { value: '24/7', label: 'Support' }
    ];

    return (
        <div className="bg-gray-900 border-y border-gray-800">
            <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-16">
                <div className="grid grid-cols-2 md:grid-cols-4 gap-8">
                    {stats.map((stat, index) => (
                        <div key={index} className="text-center">
                            <div className="text-3xl md:text-4xl font-bold text-white mb-2">{stat.value}</div>
                            <div className="text-gray-400">{stat.label}</div>
                        </div>
                    ))}
                </div>
            </div>
        </div>
    );
};

const FeaturesSection = () => {
    const features = [
        {
            icon: BarChart3,
            title: 'Automated Assessments',
            description: 'Run comprehensive Azure governance assessments with a single click. Analyze naming conventions, tagging strategies, and compliance in minutes.'
        },
        {
            icon: Shield,
            title: 'Security & Compliance',
            description: 'Ensure your Azure resources meet industry standards and regulatory requirements with our built-in compliance frameworks.'
        },
        {
            icon: Users,
            title: 'Team Collaboration',
            description: 'Share reports, assign tasks, and collaborate with your team. Perfect for MSPs managing multiple client environments.'
        },
        {
            icon: TrendingUp,
            title: 'Trend Analysis',
            description: 'Track your governance improvements over time with detailed analytics and historical reporting.'
        },
        {
            icon: Award,
            title: 'Best Practices',
            description: 'Get actionable recommendations based on Microsoft Azure Well-Architected Framework and industry best practices.'
        },
        {
            icon: Star,
            title: 'Custom Rules',
            description: 'Define your own governance rules and policies to match your organization\'s specific requirements.'
        }
    ];

    return (
        <div id="features" className="bg-gray-950 py-24">
            <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
                <div className="text-center mb-16">
                    <h2 className="text-3xl md:text-4xl font-bold text-white mb-4">
                        Everything you need for Azure governance
                    </h2>
                    <p className="text-xl text-gray-400 max-w-3xl mx-auto">
                        Comprehensive tools to assess, monitor, and improve your Azure environment's
                        governance posture with minimal effort.
                    </p>
                </div>

                <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-8">
                    {features.map((feature, index) => (
                        <div key={index} className="bg-gray-900 border border-gray-800 rounded-lg p-6 hover:border-gray-700 transition-colors">
                            <div className="p-3 bg-yellow-600 rounded-lg w-fit mb-4">
                                <feature.icon size={24} className="text-black" />
                            </div>
                            <h3 className="text-xl font-semibold text-white mb-3">{feature.title}</h3>
                            <p className="text-gray-400">{feature.description}</p>
                        </div>
                    ))}
                </div>
            </div>
        </div>
    );
};

const TestimonialSection = () => {
    const testimonials = [
        {
            quote: "Compass saved us hours of manual Azure governance reviews. The automated assessments are incredibly detailed and actionable.",
            author: "Sarah Johnson",
            title: "IT Director",
            company: "TechFlow Solutions"
        },
        {
            quote: "As an MSP, we need to ensure our clients' Azure environments are properly governed. Compass makes this process seamless and professional.",
            author: "Michael Chen",
            title: "Solutions Architect",
            company: "CloudFirst Consulting"
        },
        {
            quote: "The compliance reporting features have been game-changing for our SOC 2 audits. Everything we need in one place.",
            author: "Emily Rodriguez",
            title: "Security Manager",
            company: "SecureOps Inc"
        }
    ];

    return (
        <div className="bg-gray-900 py-24">
            <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
                <div className="text-center mb-16">
                    <h2 className="text-3xl md:text-4xl font-bold text-white mb-4">
                        Trusted by leading MSPs and enterprises
                    </h2>
                    <p className="text-xl text-gray-400">
                        See what our customers are saying about Compass
                    </p>
                </div>

                <div className="grid grid-cols-1 md:grid-cols-3 gap-8">
                    {testimonials.map((testimonial, index) => (
                        <div key={index} className="bg-gray-950 border border-gray-800 rounded-lg p-6">
                            <div className="flex mb-4">
                                {[...Array(5)].map((_, i) => (
                                    <Star key={i} size={16} className="text-yellow-400 fill-current" />
                                ))}
                            </div>
                            <p className="text-gray-300 mb-4 italic">"{testimonial.quote}"</p>
                            <div>
                                <div className="font-semibold text-white">{testimonial.author}</div>
                                <div className="text-sm text-gray-400">{testimonial.title}</div>
                                <div className="text-sm text-gray-500">{testimonial.company}</div>
                            </div>
                        </div>
                    ))}
                </div>
            </div>
        </div>
    );
};

const PricingSection = () => {
    const plans = [
        {
            name: 'Starter',
            price: '$49',
            period: '/month',
            description: 'Perfect for small teams getting started with Azure governance',
            features: [
                'Up to 100 resources',
                '5 assessments per month',
                'Basic reporting',
                'Email support',
                '1 team member'
            ],
            cta: 'Start Free Trial',
            popular: false
        },
        {
            name: 'Professional',
            price: '$149',
            period: '/month',
            description: 'Ideal for growing MSPs and medium-sized organizations',
            features: [
                'Up to 1,000 resources',
                'Unlimited assessments',
                'Advanced reporting',
                'Priority support',
                '10 team members',
                'Custom compliance rules',
                'API access'
            ],
            cta: 'Start Free Trial',
            popular: true
        },
        {
            name: 'Enterprise',
            price: 'Custom',
            period: '',
            description: 'For large MSPs and enterprises with complex requirements',
            features: [
                'Unlimited resources',
                'Unlimited assessments',
                'White-label reporting',
                '24/7 dedicated support',
                'Unlimited team members',
                'Custom integrations',
                'On-premise deployment'
            ],
            cta: 'Contact Sales',
            popular: false
        }
    ];

    return (
        <div id="pricing" className="bg-gray-950 py-24">
            <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
                <div className="text-center mb-16">
                    <h2 className="text-3xl md:text-4xl font-bold text-white mb-4">
                        Simple, transparent pricing
                    </h2>
                    <p className="text-xl text-gray-400">
                        Choose the plan that fits your organization's needs
                    </p>
                </div>

                <div className="grid grid-cols-1 md:grid-cols-3 gap-8">
                    {plans.map((plan, index) => (
                        <div key={index} className={`relative bg-gray-900 border rounded-lg p-8 ${plan.popular ? 'border-yellow-600' : 'border-gray-800'
                            }`}>
                            {plan.popular && (
                                <div className="absolute -top-4 left-1/2 transform -translate-x-1/2">
                                    <span className="bg-yellow-600 text-black px-4 py-1 rounded-full text-sm font-medium">
                                        Most Popular
                                    </span>
                                </div>
                            )}

                            <div className="text-center mb-8">
                                <h3 className="text-2xl font-bold text-white mb-2">{plan.name}</h3>
                                <div className="mb-4">
                                    <span className="text-4xl font-bold text-white">{plan.price}</span>
                                    <span className="text-gray-400">{plan.period}</span>
                                </div>
                                <p className="text-gray-400">{plan.description}</p>
                            </div>

                            <ul className="space-y-3 mb-8">
                                {plan.features.map((feature, featureIndex) => (
                                    <li key={featureIndex} className="flex items-center space-x-3">
                                        <CheckCircle size={16} className="text-green-400 flex-shrink-0" />
                                        <span className="text-gray-300">{feature}</span>
                                    </li>
                                ))}
                            </ul>

                            <button className={`w-full py-3 px-4 rounded-lg font-semibold transition-colors ${plan.popular
                                    ? 'bg-yellow-600 hover:bg-yellow-700 text-black'
                                    : 'border border-gray-700 hover:border-gray-600 text-white'
                                }`}>
                                {plan.cta}
                            </button>
                        </div>
                    ))}
                </div>
            </div>
        </div>
    );
};

const CTASection = () => {
    return (
        <div className="bg-gradient-to-r from-yellow-600 to-yellow-500">
            <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-16">
                <div className="text-center">
                    <h2 className="text-3xl md:text-4xl font-bold text-black mb-4">
                        Ready to transform your Azure governance?
                    </h2>
                    <p className="text-xl text-black opacity-80 mb-8 max-w-2xl mx-auto">
                        Join hundreds of MSPs and enterprises who trust Compass for their Azure governance needs.
                        Start your free trial today.
                    </p>

                    <div className="flex flex-col sm:flex-row gap-4 justify-center items-center">
                        <button className="bg-black hover:bg-gray-900 text-white px-8 py-3 rounded-lg font-semibold text-lg transition-colors flex items-center space-x-2">
                            <span>Start Free Trial</span>
                            <ArrowRight size={20} />
                        </button>
                        <button className="border-2 border-black hover:bg-black hover:text-white text-black px-8 py-3 rounded-lg font-semibold text-lg transition-colors">
                            Schedule Demo
                        </button>
                    </div>

                    <div className="mt-8 text-black opacity-60 text-sm">
                        No credit card required • 14-day free trial • Cancel anytime
                    </div>
                </div>
            </div>
        </div>
    );
};

const Footer = () => {
    return (
        <footer className="bg-gray-950 border-t border-gray-800">
            <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-12">
                <div className="grid grid-cols-1 md:grid-cols-4 gap-8">
                    {/* Company Info */}
                    <div className="md:col-span-1">
                        <div className="flex items-center space-x-3 mb-4">
                            <div className="w-8 h-8 bg-gradient-to-br from-yellow-400 to-yellow-600 rounded flex items-center justify-center">
                                <span className="text-black font-bold text-sm">C</span>
                            </div>
                            <span className="text-white font-bold text-xl">Compass</span>
                        </div>
                        <p className="text-gray-400 mb-4">
                            Simplifying Azure governance for MSPs and enterprises worldwide.
                        </p>
                        <div className="text-sm text-gray-500">
                            © 2024 Silverfern Technology Consultants. All rights reserved.
                        </div>
                    </div>

                    {/* Product */}
                    <div>
                        <h3 className="text-white font-semibold mb-4">Product</h3>
                        <ul className="space-y-2 text-gray-400">
                            <li><a href="#features" className="hover:text-white transition-colors">Features</a></li>
                            <li><a href="#pricing" className="hover:text-white transition-colors">Pricing</a></li>
                            <li><a href="#" className="hover:text-white transition-colors">API Documentation</a></li>
                            <li><a href="#" className="hover:text-white transition-colors">Integrations</a></li>
                        </ul>
                    </div>

                    {/* Company */}
                    <div>
                        <h3 className="text-white font-semibold mb-4">Company</h3>
                        <ul className="space-y-2 text-gray-400">
                            <li><a href="#about" className="hover:text-white transition-colors">About Us</a></li>
                            <li><a href="#" className="hover:text-white transition-colors">Careers</a></li>
                            <li><a href="#" className="hover:text-white transition-colors">Blog</a></li>
                            <li><a href="#contact" className="hover:text-white transition-colors">Contact</a></li>
                        </ul>
                    </div>

                    {/* Support */}
                    <div>
                        <h3 className="text-white font-semibold mb-4">Support</h3>
                        <ul className="space-y-2 text-gray-400">
                            <li><a href="#" className="hover:text-white transition-colors">Help Center</a></li>
                            <li><a href="#" className="hover:text-white transition-colors">Documentation</a></li>
                            <li><a href="#" className="hover:text-white transition-colors">Community</a></li>
                            <li><a href="#" className="hover:text-white transition-colors">Status</a></li>
                        </ul>
                    </div>
                </div>

                <div className="border-t border-gray-800 mt-8 pt-8 flex flex-col md:flex-row justify-between items-center">
                    <div className="flex space-x-6 text-gray-400 text-sm">
                        <a href="#" className="hover:text-white transition-colors">Privacy Policy</a>
                        <a href="#" className="hover:text-white transition-colors">Terms of Service</a>
                        <a href="#" className="hover:text-white transition-colors">Security</a>
                    </div>
                    <div className="text-gray-400 text-sm mt-4 md:mt-0">
                        Made with ❤️ for the Azure community
                    </div>
                </div>
            </div>
        </footer>
    );
};

const CompassLandingPage = () => {
    return (
        <div className="min-h-screen bg-gray-950">
            <NavigationBar />
            <HeroSection />
            <StatsSection />
            <FeaturesSection />
            <TestimonialSection />
            <PricingSection />
            <CTASection />
            <Footer />
        </div>
    );
};

export default CompassLandingPage;