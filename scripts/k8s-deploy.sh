#!/bin/bash
set -e

echo "=========================================="
echo "AUTONOMOUS KUBERNETES DEPLOYMENT via API"
echo "=========================================="

# Step 1 - Authentication
echo "Step 1/10 - Configuring authentication..."
KUBE_TOKEN=$(echo -n "$KUBE_USER:$KUBE_PASS" | base64 -w 0)
export AUTH_HEADER="Authorization: Basic $KUBE_TOKEN"
curl -k -H "$AUTH_HEADER" "$KUBE_API/version"

# Step 2 - Create Namespace
echo "Step 2/10 - Creating namespace securebox..."
curl -k -H "$AUTH_HEADER" -X POST "$KUBE_API/api/v1/namespaces" \
  -H "Content-Type:application/json" \
  -d "{\"apiVersion\":\"v1\",\"kind\":\"Namespace\",\"metadata\":{\"name\":\"$KUBE_NAMESPACE\"}}" || echo "Namespace exists"

# Step 3 - Create Harbor Secret
echo "Step 3/10 - Creating Harbor registry secret..."
DOCKER_AUTH=$(echo -n "$GTECH_REPO_USERNAME:$GTECH_REPO_TOKEN" | base64 -w 0)
DOCKER_CONFIG=$(echo -n "{\"auths\":{\"$GTECH_REPO_URL\":{\"username\":\"$GTECH_REPO_USERNAME\",\"password\":\"$GTECH_REPO_TOKEN\",\"auth\":\"$DOCKER_AUTH\"}}}" | base64 -w 0)
curl -k -H "$AUTH_HEADER" -X POST "$KUBE_API/api/v1/namespaces/$KUBE_NAMESPACE/secrets" \
  -H "Content-Type:application/json" \
  -d "{\"apiVersion\":\"v1\",\"kind\":\"Secret\",\"type\":\"kubernetes.io/dockerconfigjson\",\"metadata\":{\"name\":\"harbor-registry-secret\"},\"data\":{\".dockerconfigjson\":\"$DOCKER_CONFIG\"}}" || echo "Secret exists"

# Step 4 - Update Image Tags
echo "Step 4/10 - Updating image tags..."
export IMAGE_TAG=$CI_COMMIT_SHORT_SHA
sed -i "s|\${GTECH_REPO_URL}|$GTECH_REPO_URL|g" kubernetes/base/*.yaml
sed -i "s|:latest|:$IMAGE_TAG|g" kubernetes/base/api-deployment.yaml
sed -i "s|:latest|:$IMAGE_TAG|g" kubernetes/base/portal-deployment.yaml

# Step 5 - Deploy ConfigMaps and Secrets
echo "Step 5/10 - Deploying ConfigMaps and Secrets..."
curl -k -H "$AUTH_HEADER" -X POST "$KUBE_API/api/v1/namespaces/$KUBE_NAMESPACE/configmaps" \
  -H "Content-Type:application/yaml" --data-binary @kubernetes/base/configmap.yaml || echo "ConfigMap exists"
curl -k -H "$AUTH_HEADER" -X POST "$KUBE_API/api/v1/namespaces/$KUBE_NAMESPACE/secrets" \
  -H "Content-Type:application/yaml" --data-binary @kubernetes/base/secrets.yaml || echo "Secret exists"

# Step 6 - Deploy Infrastructure
echo "Step 6/10 - Deploying infrastructure services..."
for service in postgres mongodb redis rabbitmq; do
  echo "  Deploying $service..."
  curl -k -H "$AUTH_HEADER" -X POST "$KUBE_API/apis/apps/v1/namespaces/$KUBE_NAMESPACE/deployments" \
    -H "Content-Type:application/yaml" --data-binary @kubernetes/base/${service}-deployment.yaml || echo "$service exists"
done
sleep 30

# Step 7 - Deploy API
echo "Step 7/10 - Deploying SecureBox API..."
curl -k -H "$AUTH_HEADER" -X POST "$KUBE_API/apis/apps/v1/namespaces/$KUBE_NAMESPACE/deployments" \
  -H "Content-Type:application/yaml" --data-binary @kubernetes/base/api-deployment.yaml || echo "API exists"
sleep 20

# Step 8 - Deploy Portal
echo "Step 8/10 - Deploying SecureBox Portal..."
curl -k -H "$AUTH_HEADER" -X POST "$KUBE_API/apis/apps/v1/namespaces/$KUBE_NAMESPACE/deployments" \
  -H "Content-Type:application/yaml" --data-binary @kubernetes/base/portal-deployment.yaml || echo "Portal exists"
sleep 20

# Step 9 - Deploy NGINX
echo "Step 9/10 - Deploying NGINX Gateway..."
curl -k -H "$AUTH_HEADER" -X POST "$KUBE_API/api/v1/namespaces/$KUBE_NAMESPACE/configmaps" \
  -H "Content-Type:application/yaml" --data-binary @kubernetes/base/nginx-configmap.yaml || echo "NGINX ConfigMap exists"
curl -k -H "$AUTH_HEADER" -X POST "$KUBE_API/apis/apps/v1/namespaces/$KUBE_NAMESPACE/deployments" \
  -H "Content-Type:application/yaml" --data-binary @kubernetes/base/nginx-deployment.yaml || echo "NGINX exists"
sleep 20

# Step 10 - Status Check
echo "Step 10/10 - Checking deployment status..."
curl -k -H "$AUTH_HEADER" "$KUBE_API/apis/apps/v1/namespaces/$KUBE_NAMESPACE/deployments" | grep -o '"name":"[^"]*"' | head -10
curl -k -H "$AUTH_HEADER" "$KUBE_API/api/v1/namespaces/$KUBE_NAMESPACE/pods" | grep -o '"name":"[^"]*"' | head -10
curl -k -H "$AUTH_HEADER" "$KUBE_API/api/v1/namespaces/$KUBE_NAMESPACE/services" | grep -o '"name":"[^"]*"' | head -10

echo "Access URL: http://<node-ip>:30222"
echo "=========================================="
echo "DEPLOYMENT COMPLETED"
echo "=========================================="

