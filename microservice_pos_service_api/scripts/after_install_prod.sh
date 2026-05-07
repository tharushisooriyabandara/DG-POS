#!/bin/bash
PARAMATER="pos_service_prod"
PASSPORT="passport_keys"
REGION="eu-west-2"
WEB_DIR="/var/www/microservice_pos_service_api"

FROM="$WEB_DIR/app/microservice_delivergate_api/CustomConfig/applications.php"
TO="$WEB_DIR/config/applications.php"
cp $FROM $TO
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

cd $WEB_DIR/ && /usr/local/bin/composer install --optimize-autoloader --no-dev
/usr/bin/php $WEB_DIR/artisan config:cache
/usr/bin/php $WEB_DIR/artisan route:cache
/usr/bin/php $WEB_DIR/artisan view:cache
cd $WEB_DIR/ && chmod 777 -R storage