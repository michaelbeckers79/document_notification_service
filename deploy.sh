#!/bin/bash

# Document Notification Service Deployment Script
# This script helps deploy the service to Kubernetes

set -e

NAMESPACE="${NAMESPACE:-default}"
IMAGE_TAG="${IMAGE_TAG:-latest}"
REGISTRY="${REGISTRY:-document-notification-service}"

echo "Document Notification Service Kubernetes Deployment"
echo "=================================================="
echo "Namespace: $NAMESPACE"
echo "Image: $REGISTRY:$IMAGE_TAG"
echo ""

# Function to wait for deployment
wait_for_deployment() {
    echo "Waiting for $1 to be ready..."
    kubectl wait --for=condition=available deployment/$1 -n $NAMESPACE --timeout=300s
}

# Function to wait for job completion
wait_for_job() {
    echo "Waiting for job $1 to complete..."
    kubectl wait --for=condition=complete job/$1 -n $NAMESPACE --timeout=300s
}

case "$1" in
    "install")
        echo "Installing Document Notification Service..."
        
        # Apply configuration
        kubectl apply -f k8s/configmap.yaml -n $NAMESPACE
        kubectl apply -f k8s/secret.yaml -n $NAMESPACE
        
        # Run database migration
        kubectl apply -f k8s/job-migrate.yaml -n $NAMESPACE
        wait_for_job "document-notification-migrate"
        
        # Deploy the service
        kubectl apply -f k8s/deployment.yaml -n $NAMESPACE
        kubectl apply -f k8s/cronjob-process.yaml -n $NAMESPACE
        
        wait_for_deployment "document-notification-service"
        
        echo "Installation complete!"
        ;;
        
    "upgrade")
        echo "Upgrading Document Notification Service..."
        
        # Update configuration
        kubectl apply -f k8s/configmap.yaml -n $NAMESPACE
        
        # Update deployment
        kubectl apply -f k8s/deployment.yaml -n $NAMESPACE
        kubectl apply -f k8s/cronjob-process.yaml -n $NAMESPACE
        
        # Restart deployment to pick up new changes
        kubectl rollout restart deployment/document-notification-service -n $NAMESPACE
        wait_for_deployment "document-notification-service"
        
        echo "Upgrade complete!"
        ;;
        
    "uninstall")
        echo "Uninstalling Document Notification Service..."
        
        kubectl delete -f k8s/ -n $NAMESPACE --ignore-not-found=true
        
        echo "Uninstallation complete!"
        ;;
        
    "status")
        echo "Document Notification Service Status"
        echo "====================================="
        
        echo ""
        echo "Deployments:"
        kubectl get deployments -l app=document-notification-service -n $NAMESPACE
        
        echo ""
        echo "Pods:"
        kubectl get pods -l app=document-notification-service -n $NAMESPACE
        
        echo ""
        echo "Jobs:"
        kubectl get jobs -l app=document-notification-service -n $NAMESPACE
        
        echo ""
        echo "CronJobs:"
        kubectl get cronjobs -l app=document-notification-service -n $NAMESPACE
        
        echo ""
        echo "ConfigMaps:"
        kubectl get configmaps -l app.kubernetes.io/name=document-notification-service -n $NAMESPACE
        
        echo ""
        echo "Secrets:"
        kubectl get secrets -l app.kubernetes.io/name=document-notification-service -n $NAMESPACE
        ;;
        
    "logs")
        echo "Recent logs from Document Notification Service:"
        kubectl logs -l app=document-notification-service -n $NAMESPACE --tail=100
        ;;
        
    "exec")
        POD=$(kubectl get pods -l app=document-notification-service -n $NAMESPACE -o jsonpath='{.items[0].metadata.name}')
        echo "Executing command in pod: $POD"
        shift
        kubectl exec -it $POD -n $NAMESPACE -- "$@"
        ;;
        
    "process")
        echo "Running manual document processing..."
        kubectl create job manual-process-$(date +%s) --from=cronjob/document-notification-process -n $NAMESPACE
        ;;
        
    "migrate")
        echo "Running database migration..."
        kubectl apply -f k8s/job-migrate.yaml -n $NAMESPACE
        wait_for_job "document-notification-migrate"
        ;;
        
    *)
        echo "Usage: $0 {install|upgrade|uninstall|status|logs|exec|process|migrate}"
        echo ""
        echo "Commands:"
        echo "  install   - Install the service to Kubernetes"
        echo "  upgrade   - Upgrade existing installation"
        echo "  uninstall - Remove the service from Kubernetes"
        echo "  status    - Show status of all resources"
        echo "  logs      - Show recent logs"
        echo "  exec      - Execute command in running pod"
        echo "  process   - Run manual document processing job"
        echo "  migrate   - Run database migration job"
        echo ""
        echo "Environment variables:"
        echo "  NAMESPACE - Kubernetes namespace (default: default)"
        echo "  IMAGE_TAG - Docker image tag (default: latest)"
        echo "  REGISTRY  - Docker registry/image name"
        exit 1
        ;;
esac