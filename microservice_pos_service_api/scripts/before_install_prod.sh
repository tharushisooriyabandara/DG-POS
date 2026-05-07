#!/bin/bash
WEB_DIR="/var/www/microservice_pos_service_api"
if [ -d $WEB_DIR ] 
then
  if [ -d "$WEB_DIR/app" ]
  then
    find $WEB_DIR -mindepth 1 ! -regex "^$WEB_DIR/app\(/.*\)?" -delete
    find $WEB_DIR/app -mindepth 1 ! -regex "^$WEB_DIR/app/microservice_delivergate_api\(/.*\)?" -delete 
  else
    find $WEB_DIR -mindepth 1 -delete	
  fi
fi