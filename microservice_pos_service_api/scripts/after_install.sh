#!/bin/bash
WEB_DIR=$1
REGION=$2
PASSPORT=$3
PARAMATER=$4
INSTALL_OPTIONS=$5
ENVIRONMENT=$6
MIDDLEWARE=$7

TARGET_DIR="$WEB_DIR/app/microservice_delivergate_api"
if [ ! -d "$TARGET_DIR" ]; then
  mkdir -p "$TARGET_DIR"
fi
ln -sfn $MIDDLEWARE "$TARGET_DIR/"
ln -sfn "$MIDDLEWARE/CustomConfig/applications.php" "$WEB_DIR/config/applications.php"

# Update composer.json autoload path dynamically
release_path=$(find $MIDDLEWARE -maxdepth 1 -type l  -name "*release*")
composer_json="$WEB_DIR/composer.json"
temp_composer_json="$WEB_DIR/composer.temp.json"

jq --arg path $release_path '.autoload["psr-4"]["App\\microservice_delivergate_api\\"] = $path' $composer_json > $temp_composer_json && mv $temp_composer_json $composer_json

# Get parameters and put it into .env file inside application root
aws ssm get-parameter \
  --with-decryption \
  --name $PARAMATER \
  --region $REGION \
  --with-decryption \
  --query Parameter.Value \
  --output text > $WEB_DIR/.env

aws ssm get-parameter \
  --with-decryption \
  --name $PASSPORT \
  --region $REGION \
  --with-decryption \
  --query Parameter.Value \
  --output text >> $WEB_DIR/.env

cd $WEB_DIR/ && /usr/local/bin/composer install $INSTALL_OPTIONS
/usr/bin/php $WEB_DIR/artisan config:cache
/usr/bin/php $WEB_DIR/artisan route:cache
/usr/bin/php $WEB_DIR/artisan view:cache
cd $WEB_DIR/ && chmod 777 -R storage

if [ "$ENVIRONMENT" == "dev" ]; then
  echo "Running unit tests..."
  #/usr/bin/php $WEB_DIR/vendor/bin/phpunit
fi
