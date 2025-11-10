#!/bin/bash
set -e

echo "Cleaning up Development Environment via Kubernetes API"

# Configure authentication
KUBE_TOKEN=$(echo -n "$KUBE_USER:$KUBE_PASS" | base64 -w 0)

# Delete namespace
curl -k -H "Authorization: Basic $KUBE_TOKEN" -X DELETE "$KUBE_URL/api/v1/namespaces/$KUBE_NAMESPACE"

echo "Development environment cleaned up"

