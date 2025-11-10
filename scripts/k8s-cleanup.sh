#!/bin/bash
set -e

echo "Cleaning up Development Environment via Kubernetes API"

# Configure authentication
if [ -n "$KUBE_TOKEN" ]; then
  AUTH_HEADER="Authorization: Bearer $KUBE_TOKEN"
else
  BASIC_TOKEN=$(echo -n "$KUBE_USER:$KUBE_PASS" | base64 -w 0)
  AUTH_HEADER="Authorization: Basic $BASIC_TOKEN"
fi

# Delete namespace
curl -k -H "$AUTH_HEADER" -X DELETE "$KUBE_URL/api/v1/namespaces/$KUBE_NAMESPACE"

echo "Development environment cleaned up"

