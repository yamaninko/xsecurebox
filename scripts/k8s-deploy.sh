#!/bin/bash
set -e

echo "=========================================="
echo "AUTONOMOUS KUBERNETES DEPLOYMENT via API"
echo "=========================================="

# Step 1 - Authentication
echo "Step 1/10 - Configuring authentication..."
if [ -n "$KUBE_TOKEN" ]; then
  echo "Using Bearer Token authentication"
  export AUTH_HEADER="Authorization: Bearer $KUBE_TOKEN"
else
  echo "Using Basic Auth (fallback)"
  BASIC_TOKEN=$(echo -n "$KUBE_USER:$KUBE_PASS" | base64 -w 0)
  export AUTH_HEADER="Authorization: Basic $BASIC_TOKEN"
fi
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
echo "Step 4/10 - Updating image tags to ${IMAGE_TAG}..."
sed -i "s|\${GTECH_REPO_URL}|$GTECH_REPO_URL|g" kubernetes/base/*.yaml
sed -i "s|:latest|:$IMAGE_TAG|g" kubernetes/base/api-deployment.yaml
sed -i "s|:latest|:$IMAGE_TAG|g" kubernetes/base/portal-deployment.yaml

# Step 5 - Deploy ConfigMaps, Secrets and Persistent Volumes
echo "Step 5/10 - Deploying ConfigMaps, Secrets and Persistent Volumes..."
curl -k -H "$AUTH_HEADER" -X POST "$KUBE_API/api/v1/namespaces/$KUBE_NAMESPACE/configmaps" \
  -H "Content-Type:application/yaml" --data-binary @kubernetes/base/configmap.yaml || echo "ConfigMap exists"
curl -k -H "$AUTH_HEADER" -X POST "$KUBE_API/api/v1/namespaces/$KUBE_NAMESPACE/secrets" \
  -H "Content-Type:application/yaml" --data-binary @kubernetes/base/secrets.yaml || echo "Secret exists"

echo "  Cleaning up old PVCs and corrupt data..."
# Delete old PVCs first (this will also trigger PV release)
curl -k -H "$AUTH_HEADER" -X DELETE "$KUBE_API/api/v1/namespaces/$KUBE_NAMESPACE/persistentvolumeclaims/postgres-pvc" 2>/dev/null || true
curl -k -H "$AUTH_HEADER" -X DELETE "$KUBE_API/api/v1/namespaces/$KUBE_NAMESPACE/persistentvolumeclaims/mongodb-pvc" 2>/dev/null || true
curl -k -H "$AUTH_HEADER" -X DELETE "$KUBE_API/api/v1/namespaces/$KUBE_NAMESPACE/persistentvolumeclaims/redis-pvc" 2>/dev/null || true
curl -k -H "$AUTH_HEADER" -X DELETE "$KUBE_API/api/v1/namespaces/$KUBE_NAMESPACE/persistentvolumeclaims/rabbitmq-pvc" 2>/dev/null || true
sleep 5

echo "  Deleting old conflicting PVs if any..."
# Try to delete old PVs with conflicting names
curl -k -H "$AUTH_HEADER" -X DELETE "$KUBE_API/api/v1/persistentvolumes/postgres-pv" 2>/dev/null || true
curl -k -H "$AUTH_HEADER" -X DELETE "$KUBE_API/api/v1/persistentvolumes/mongodb-pv" 2>/dev/null || true
curl -k -H "$AUTH_HEADER" -X DELETE "$KUBE_API/api/v1/persistentvolumes/redis-pv" 2>/dev/null || true
curl -k -H "$AUTH_HEADER" -X DELETE "$KUBE_API/api/v1/persistentvolumes/rabbitmq-pv" 2>/dev/null || true
sleep 5

echo "  Creating new Persistent Volumes with unique names..."
csplit -s -f /tmp/pv- kubernetes/base/persistent-volumes.yaml '/^---$/' '{*}' 2>/dev/null || cp kubernetes/base/persistent-volumes.yaml /tmp/pv-00

for yaml_part in /tmp/pv-*; do
  if [ -s "$yaml_part" ]; then
    if grep -q "kind: PersistentVolume" "$yaml_part" && ! grep -q "kind: PersistentVolumeClaim" "$yaml_part"; then
      curl -k -H "$AUTH_HEADER" -X POST "$KUBE_API/api/v1/persistentvolumes" \
        -H "Content-Type:application/yaml" --data-binary @"$yaml_part" 2>/dev/null || echo "PV exists"
    elif grep -q "kind: PersistentVolumeClaim" "$yaml_part"; then
      curl -k -H "$AUTH_HEADER" -X POST "$KUBE_API/api/v1/namespaces/$KUBE_NAMESPACE/persistentvolumeclaims" \
        -H "Content-Type:application/yaml" --data-binary @"$yaml_part" 2>/dev/null || echo "PVC exists"
    fi
  fi
done
rm -f /tmp/pv-*

# Step 6 - Deploy Infrastructure
echo "Step 6/10 - Deploying infrastructure services..."
echo "  Deleting old database deployments to start fresh..."
for service in postgres mongodb redis rabbitmq; do
  curl -k -H "$AUTH_HEADER" -X DELETE "$KUBE_API/apis/apps/v1/namespaces/$KUBE_NAMESPACE/deployments/$service" 2>/dev/null || true
done
sleep 10

for service in postgres mongodb redis rabbitmq; do
  echo "  Deploying $service deployment and service..."
  # Split YAML file by --- delimiter and POST each resource
  csplit -s -f /tmp/${service}- kubernetes/base/${service}-deployment.yaml '/^---$/' '{*}' 2>/dev/null || cp kubernetes/base/${service}-deployment.yaml /tmp/${service}-00
  
  for yaml_part in /tmp/${service}-*; do
    if [ -s "$yaml_part" ]; then
      # Detect resource type
      if grep -q "kind: Deployment" "$yaml_part"; then
        curl -k -H "$AUTH_HEADER" -X POST "$KUBE_API/apis/apps/v1/namespaces/$KUBE_NAMESPACE/deployments" \
          -H "Content-Type:application/yaml" --data-binary @"$yaml_part" 2>/dev/null || echo "$service deployment created"
      elif grep -q "kind: Service" "$yaml_part"; then
        curl -k -H "$AUTH_HEADER" -X POST "$KUBE_API/api/v1/namespaces/$KUBE_NAMESPACE/services" \
          -H "Content-Type:application/yaml" --data-binary @"$yaml_part" 2>/dev/null || echo "$service service exists"
      fi
    fi
  done
  rm -f /tmp/${service}-*
done
sleep 30

# Step 7 - Deploy API
echo "Step 7/10 - Deploying SecureBox API..."
csplit -s -f /tmp/api- kubernetes/base/api-deployment.yaml '/^---$/' '{*}' 2>/dev/null || cp kubernetes/base/api-deployment.yaml /tmp/api-00

for yaml_part in /tmp/api-*; do
  if [ -s "$yaml_part" ]; then
    if grep -q "kind: Deployment" "$yaml_part"; then
      echo "  Updating API deployment with new image..."
      curl -k -H "$AUTH_HEADER" -X PUT "$KUBE_API/apis/apps/v1/namespaces/$KUBE_NAMESPACE/deployments/securebox-api" \
        -H "Content-Type:application/yaml" --data-binary @"$yaml_part" 2>/dev/null || \
      curl -k -H "$AUTH_HEADER" -X POST "$KUBE_API/apis/apps/v1/namespaces/$KUBE_NAMESPACE/deployments" \
        -H "Content-Type:application/yaml" --data-binary @"$yaml_part" 2>/dev/null
    elif grep -q "kind: Service" "$yaml_part"; then
      curl -k -H "$AUTH_HEADER" -X POST "$KUBE_API/api/v1/namespaces/$KUBE_NAMESPACE/services" \
        -H "Content-Type:application/yaml" --data-binary @"$yaml_part" 2>/dev/null || echo "API service exists"
    elif grep -q "kind: HorizontalPodAutoscaler" "$yaml_part"; then
      curl -k -H "$AUTH_HEADER" -X POST "$KUBE_API/apis/autoscaling/v2/namespaces/$KUBE_NAMESPACE/horizontalpodautoscalers" \
        -H "Content-Type:application/yaml" --data-binary @"$yaml_part" 2>/dev/null || echo "API HPA exists"
    fi
  fi
done
rm -f /tmp/api-*
sleep 20

# Step 8 - Deploy Portal
echo "Step 8/10 - Deploying SecureBox Portal..."
csplit -s -f /tmp/portal- kubernetes/base/portal-deployment.yaml '/^---$/' '{*}' 2>/dev/null || cp kubernetes/base/portal-deployment.yaml /tmp/portal-00

for yaml_part in /tmp/portal-*; do
  if [ -s "$yaml_part" ]; then
    if grep -q "kind: Deployment" "$yaml_part"; then
      echo "  Updating Portal deployment with new image..."
      curl -k -H "$AUTH_HEADER" -X PUT "$KUBE_API/apis/apps/v1/namespaces/$KUBE_NAMESPACE/deployments/securebox-portal" \
        -H "Content-Type:application/yaml" --data-binary @"$yaml_part" 2>/dev/null || \
      curl -k -H "$AUTH_HEADER" -X POST "$KUBE_API/apis/apps/v1/namespaces/$KUBE_NAMESPACE/deployments" \
        -H "Content-Type:application/yaml" --data-binary @"$yaml_part" 2>/dev/null
    elif grep -q "kind: Service" "$yaml_part"; then
      curl -k -H "$AUTH_HEADER" -X POST "$KUBE_API/api/v1/namespaces/$KUBE_NAMESPACE/services" \
        -H "Content-Type:application/yaml" --data-binary @"$yaml_part" 2>/dev/null || echo "Portal service exists"
    elif grep -q "kind: HorizontalPodAutoscaler" "$yaml_part"; then
      curl -k -H "$AUTH_HEADER" -X POST "$KUBE_API/apis/autoscaling/v2/namespaces/$KUBE_NAMESPACE/horizontalpodautoscalers" \
        -H "Content-Type:application/yaml" --data-binary @"$yaml_part" 2>/dev/null || echo "Portal HPA exists"
    fi
  fi
done
rm -f /tmp/portal-*
sleep 20

# Step 9 - Deploy NGINX
echo "Step 9/10 - Deploying NGINX Gateway..."
curl -k -H "$AUTH_HEADER" -X POST "$KUBE_API/api/v1/namespaces/$KUBE_NAMESPACE/configmaps" \
  -H "Content-Type:application/yaml" --data-binary @kubernetes/base/nginx-configmap.yaml 2>/dev/null || echo "NGINX ConfigMap exists"

csplit -s -f /tmp/nginx- kubernetes/base/nginx-deployment.yaml '/^---$/' '{*}' 2>/dev/null || cp kubernetes/base/nginx-deployment.yaml /tmp/nginx-00

for yaml_part in /tmp/nginx-*; do
  if [ -s "$yaml_part" ]; then
    if grep -q "kind: Deployment" "$yaml_part"; then
      echo "  Updating NGINX deployment..."
      curl -k -H "$AUTH_HEADER" -X PUT "$KUBE_API/apis/apps/v1/namespaces/$KUBE_NAMESPACE/deployments/nginx" \
        -H "Content-Type:application/yaml" --data-binary @"$yaml_part" 2>/dev/null || \
      curl -k -H "$AUTH_HEADER" -X POST "$KUBE_API/apis/apps/v1/namespaces/$KUBE_NAMESPACE/deployments" \
        -H "Content-Type:application/yaml" --data-binary @"$yaml_part" 2>/dev/null
    elif grep -q "kind: Service" "$yaml_part"; then
      curl -k -H "$AUTH_HEADER" -X POST "$KUBE_API/api/v1/namespaces/$KUBE_NAMESPACE/services" \
        -H "Content-Type:application/yaml" --data-binary @"$yaml_part" 2>/dev/null || echo "NGINX service exists"
    elif grep -q "kind: Ingress" "$yaml_part"; then
      curl -k -H "$AUTH_HEADER" -X POST "$KUBE_API/apis/networking.k8s.io/v1/namespaces/$KUBE_NAMESPACE/ingresses" \
        -H "Content-Type:application/yaml" --data-binary @"$yaml_part" 2>/dev/null || echo "NGINX ingress exists"
    fi
  fi
done
rm -f /tmp/nginx-*
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

