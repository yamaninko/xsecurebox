#!/bin/bash
#
# Secure Box Kubernetes Cleanup Script
# Bu script Secure Box uygulamasını Kubernetes'ten temizler
#

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Configuration
NAMESPACE="securebox"

# Functions
print_info() {
    echo -e "${GREEN}[INFO]${NC} $1"
}

print_warning() {
    echo -e "${YELLOW}[WARNING]${NC} $1"
}

print_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

confirm_action() {
    echo -e "${YELLOW}WARNING:${NC} This will delete all Secure Box resources from Kubernetes!"
    echo -e "${YELLOW}Namespace: $NAMESPACE${NC}"
    echo ""
    read -p "Are you sure you want to continue? (yes/no): " response
    
    if [ "$response" != "yes" ]; then
        print_info "Cleanup cancelled."
        exit 0
    fi
}

delete_namespace() {
    print_info "Deleting namespace: $NAMESPACE"
    
    if kubectl get namespace $NAMESPACE &> /dev/null; then
        kubectl delete namespace $NAMESPACE
        print_info "Namespace deleted ✓"
    else
        print_warning "Namespace $NAMESPACE not found"
    fi
}

delete_resources_individually() {
    print_info "Deleting resources individually..."
    
    # Delete application
    print_info "Deleting application deployments..."
    kubectl delete deployment nginx securebox-portal securebox-api -n $NAMESPACE --ignore-not-found=true
    
    # Delete databases
    print_info "Deleting database deployments..."
    kubectl delete deployment postgres mongodb redis rabbitmq -n $NAMESPACE --ignore-not-found=true
    
    # Delete services
    print_info "Deleting services..."
    kubectl delete svc --all -n $NAMESPACE --ignore-not-found=true
    
    # Delete PVCs
    print_info "Deleting persistent volume claims..."
    kubectl delete pvc --all -n $NAMESPACE --ignore-not-found=true
    
    # Delete ConfigMaps and Secrets
    print_info "Deleting configmaps and secrets..."
    kubectl delete configmap --all -n $NAMESPACE --ignore-not-found=true
    kubectl delete secret --all -n $NAMESPACE --ignore-not-found=true
    
    # Delete HPA
    print_info "Deleting HPAs..."
    kubectl delete hpa --all -n $NAMESPACE --ignore-not-found=true
    
    # Delete Ingress
    print_info "Deleting ingress..."
    kubectl delete ingress --all -n $NAMESPACE --ignore-not-found=true
    
    print_info "Resources deleted ✓"
}

verify_cleanup() {
    print_info "Verifying cleanup..."
    
    if kubectl get namespace $NAMESPACE &> /dev/null; then
        print_warning "Namespace still exists. Checking resources..."
        kubectl get all -n $NAMESPACE
    else
        print_info "Namespace successfully deleted ✓"
    fi
}

# Main cleanup flow
main() {
    print_info "Starting Secure Box cleanup from Kubernetes..."
    
    # Check if kubectl is available
    if ! command -v kubectl &> /dev/null; then
        print_error "kubectl not found. Please install kubectl first."
        exit 1
    fi
    
    # Check if namespace exists
    if ! kubectl get namespace $NAMESPACE &> /dev/null; then
        print_warning "Namespace $NAMESPACE does not exist. Nothing to clean up."
        exit 0
    fi
    
    # Confirm action
    confirm_action
    
    # Choose cleanup method
    echo ""
    echo "Cleanup options:"
    echo "  1) Delete entire namespace (fast, recommended)"
    echo "  2) Delete resources individually (slower, keeps namespace)"
    echo ""
    read -p "Select option (1 or 2): " option
    
    case $option in
        1)
            delete_namespace
            ;;
        2)
            delete_resources_individually
            ;;
        *)
            print_error "Invalid option"
            exit 1
            ;;
    esac
    
    verify_cleanup
    
    print_info "Cleanup completed successfully! ✓"
}

# Run main function
main "$@"

