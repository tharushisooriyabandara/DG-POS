#!/bin/bash
set -e  # Exit immediately if a command exits with a non-zero status.

# Hardcoded project name
PROJECT_NAME="pos-service-golang"

# Determine environment based on the branch
if [[ "$ENVIRONMENT" == "prod" ]]; then
    PARAMETER_NAME="prod-pos-service-golang"
    ENV_FILE_PATH="${RELEASE_DIR}/.env"
elif [[ "$ENVIRONMENT" == "qa" ]]; then
    PARAMETER_NAME="qa-pos-service-golang"
    ENV_FILE_PATH="${RELEASE_DIR}/.env"
else
    PARAMETER_NAME="dev-pos-service-golang"
    ENV_FILE_PATH="${RELEASE_DIR}/.env"
fi

echo "Fetching parameters from AWS SSM Parameter Store: $PARAMETER_NAME"

# Fetch parameters from AWS Parameter Store
PARAMS_JSON=$(aws ssm get-parameter --name "$PARAMETER_NAME" --with-decryption --query "Parameter.Value" --output text)

# Check if parameters were retrieved successfully
if [[ -z "$PARAMS_JSON" ]]; then
    echo "Error: Failed to retrieve parameters from AWS Parameter Store."
    exit 1
fi

# Ensure the directory for the .env file exists
ENV_DIR=$(dirname "$ENV_FILE_PATH")
if [ ! -d "$ENV_DIR" ]; then
    echo "Creating directory $ENV_DIR..."
    mkdir -p "$ENV_DIR"
fi

# Write parameters to .env file
echo "Writing parameters to .env file at $ENV_FILE_PATH..."
echo "$PARAMS_JSON" > "$ENV_FILE_PATH"
chmod 600 "$ENV_FILE_PATH"
chown $USER:$USER "$ENV_FILE_PATH"

echo "✅ .env file successfully created at $ENV_FILE_PATH!"
