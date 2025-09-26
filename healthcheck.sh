#!/bin/bash
# Simple health check script for Docker
# This script can be used in Docker HEALTHCHECK instruction

# Check if the main process is running
pgrep -f "DocumentNotificationService.dll" > /dev/null
if [ $? -eq 0 ]; then
    echo "Service process is running"
    exit 0
else
    echo "Service process is not running"
    exit 1
fi