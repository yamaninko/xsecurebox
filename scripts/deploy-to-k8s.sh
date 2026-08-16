#!/bin/bash
#
# Secure Box Kubernetes Deployment Script
# Bu script Secure Box uygulamasını Kubernetes cluster'ına deploy eder
#

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Configuration
NAMESPACE="securebox"
KUBECTL="kubectl"
TIMEOUT="300s"

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

check_prerequisites() {
    print_info "Checking prerequisites..."
    
    # Check kubectl
    if ! command -v kubectl &> /dev/null; then
        print_error "kubectl not found. Please install kubectl first."
        exit 1
    fi
    
    # Check cluster connection
    if ! kubectl cluster-info &> /dev/null; then
        print_error "Cannot connect to Kubernetes cluster. Please check your kubeconfig."
        exit 1
    fi
    
    print_info "Prerequisites check passed ✓"
}

create_namespace() {
    print_info "Creating namespace: $NAMESPACE"
    kubectl create namespace $NAMESPACE --dry-run=client -o yaml | kubectl apply -f -
}

create_harbor_secret() {
    print_info "Creating Harbor registry secret..."
    
    if [ -z "$REGISTRY_URL" ] || [ -z "$REGISTRY_USERNAME" ] || [ -z "$REGISTRY_TOKEN" ]; then
        print_error "Harbor credentials not set. Please set:"
        print_error "  export REGISTRY_URL=your-harbor-url"
        print_error "  export REGISTRY_USERNAME=your-username"
        print_error "  export REGISTRY_TOKEN=your-token"
        exit 1
    fi
    
    kubectl create secret docker-registry harbor-registry-secret \
        --docker-server=$REGISTRY_URL \
        --docker-username=$REGISTRY_USERNAME \
        --docker-password=$REGISTRY_TOKEN \
        --namespace=$NAMESPACE \
        --dry-run=client -o yaml | kubectl apply -f -
    
    print_info "Harbor secret created ✓"
}

update_image_references() {
    print_info "Updating image references with Harbor URL..."
    
    if [ -z "$REGISTRY_URL" ]; then
        print_error "REGISTRY_URL not set"
        exit 1
    fi
    
    # Create temporary directory for modified files
    TMP_DIR=$(mktemp -d)
    cp -r kubernetes/base/* $TMP_DIR/
    
    # Replace placeholder with actual registry URL
    find $TMP_DIR -type f -name '*.yaml' -exec sed -i.bak "s|\${REGISTRY_URL}|$REGISTRY_URL|g" {} \;
    
    echo $TMP_DIR
}

deploy_databases() {
    print_info "Deploying databases..."
    
    kubectl apply -f kubernetes/base/configmap.yaml
    kubectl apply -f kubernetes/base/secrets.yaml
    kubectl apply -f kubernetes/base/postgres-deployment.yaml
    kubectl apply -f kubernetes/base/mongodb-deployment.yaml
    kubectl apply -f kubernetes/base/redis-deployment.yaml
    kubectl apply -f kubernetes/base/rabbitmq-deployment.yaml
    
    print_info "Waiting for databases to be ready..."
    kubectl wait --for=condition=ready pod -l app=postgres -n $NAMESPACE --timeout=$TIMEOUT || true
    kubectl wait --for=condition=ready pod -l app=mongodb -n $NAMESPACE --timeout=$TIMEOUT || true
    kubectl wait --for=condition=ready pod -l app=redis -n $NAMESPACE --timeout=$TIMEOUT || true
    kubectl wait --for=condition=ready pod -l app=rabbitmq -n $NAMESPACE --timeout=$TIMEOUT || true
    
    print_info "Databases deployed ✓"
}

deploy_application() {
    print_info "Deploying application..."
    
    # Get temporary directory with updated images
    TMP_DIR=$(update_image_references)
    
    kubectl apply -f $TMP_DIR/api-deployment.yaml
    kubectl apply -f $TMP_DIR/portal-deployment.yaml
    kubectl apply -f $TMP_DIR/nginx-configmap.yaml
    kubectl apply -f $TMP_DIR/nginx-deployment.yaml
    
    # Cleanup temporary directory
    rm -rf $TMP_DIR
    
    print_info "Waiting for application to be ready..."
    kubectl rollout status deployment/securebox-api -n $NAMESPACE --timeout=$TIMEOUT
    kubectl rollout status deployment/securebox-portal -n $NAMESPACE --timeout=$TIMEOUT
    kubectl rollout status deployment/nginx -n $NAMESPACE --timeout=$TIMEOUT
    
    print_info "Application deployed ✓"
}

verify_deployment() {
    print_info "Verifying deployment..."
    
    echo ""
    echo "=== Deployments ==="
    kubectl get deployments -n $NAMESPACE
    
    echo ""
    echo "=== Pods ==="
    kubectl get pods -n $NAMESPACE
    
    echo ""
    echo "=== Services ==="
    kubectl get services -n $NAMESPACE
    
    echo ""
    echo "=== Ingress ==="
    kubectl get ingress -n $NAMESPACE 2>/dev/null || echo "No ingress found"
    
    echo ""
    print_info "Deployment verification complete ✓"
}

run_smoke_tests() {
    print_info "Running smoke tests..."
    
    # Get LoadBalancer IP
    NGINX_SERVICE=$(kubectl get svc nginx-service -n $NAMESPACE -o jsonpath='{.status.loadBalancer.ingress[0].ip}' 2>/dev/null || echo "")
    
    if [ -z "$NGINX_SERVICE" ]; then
        print_warning "LoadBalancer IP not available yet"
        print_info "Using port-forward for testing..."
        kubectl port-forward -n $NAMESPACE svc/nginx-service 8080:80 &
        PF_PID=$!
        sleep 5
        NGINX_SERVICE="localhost:8080"
    fi
    
    print_info "Testing API health endpoint..."
    if curl -f -s http://${NGINX_SERVICE}/api/health > /dev/null; then
        print_info "API health check passed ✓"
    else
        print_error "API health check failed ✗"
    fi
    
    print_info "Testing Portal..."
    if curl -f -s http://${NGINX_SERVICE}/ > /dev/null; then
        print_info "Portal check passed ✓"
    else
        print_error "Portal check failed ✗"
    fi
    
    # Kill port-forward if started
    if [ ! -z "$PF_PID" ]; then
        kill $PF_PID 2>/dev/null || true
    fi
    
    print_info "Smoke tests complete"
}

print_access_info() {
    echo ""
    echo "======================================"
    print_info "Secure Box Deployment Complete! 🎉"
    echo "======================================"
    echo ""
    
    NGINX_SERVICE=$(kubectl get svc nginx-service -n $NAMESPACE -o jsonpath='{.status.loadBalancer.ingress[0].ip}' 2>/dev/null || echo "pending...")
    
    echo "Access URLs:"
    echo "  Portal: http://${NGINX_SERVICE}/"
    echo "  API: http://${NGINX_SERVICE}/api/"
    echo "  Health: http://${NGINX_SERVICE}/api/health"
    echo ""
    echo "Default Credentials:"
    echo "  Username: admin"
    echo "  Password: value of ADMIN_PASSWORD"
    echo ""
    echo "Useful Commands:"
    echo "  kubectl get pods -n $NAMESPACE"
    echo "  kubectl logs -f -l app=securebox-api -n $NAMESPACE"
    echo "  kubectl port-forward -n $NAMESPACE svc/nginx-service 8080:80"
    echo ""
}

# Main deployment flow
main() {
    print_info "Starting Secure Box deployment to Kubernetes..."
    
    check_prerequisites
    create_namespace
    create_harbor_secret
    deploy_databases
    deploy_application
    verify_deployment
    run_smoke_tests
    print_access_info
    
    print_info "Deployment completed successfully! ✓"
}

# Run main function
main "$@"

