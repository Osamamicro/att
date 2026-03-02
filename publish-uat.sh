#!/bin/bash

# =====================================================
#   Attendance System - UAT Publish Script
# =====================================================
# Usage: ./publish-uat.sh [output-path]
# Example: ./publish-uat.sh ./Publish_UAT

set -e

OUTPUT_PATH="${1:-.\/Publish_UAT}"

echo ""
echo "====== Attendance System - UAT Deployment ======"
echo "Output Path: $OUTPUT_PATH"
echo "Configuration: Release"
echo "Runtime: win-x64 (Windows Server)"
echo ""

# Clean previous publish
if [ -d "$OUTPUT_PATH" ]; then
    echo "Removing previous publish directory..."
    rm -rf "$OUTPUT_PATH"
fi

# Change to Dashboard directory
cd src/Presentation/Dashboard

echo ""
echo "Building application in Release mode..."
dotnet build --configuration Release --no-incremental

if [ $? -ne 0 ]; then
    echo "ERROR: Build failed!"
    exit 1
fi

echo ""
echo "Publishing application..."
dotnet publish \
    --configuration Release \
    --framework net10.0 \
    --runtime win-x64 \
    --self-contained \
    --output "$OUTPUT_PATH" \
    -p:EnvironmentName=Staging \
    -p:PublishReadyToRun=true \
    -p:PublishSingleFile=false \
    -p:DebugType=none \
    -p:DebugSymbols=false

if [ $? -ne 0 ]; then
    echo "ERROR: Publish failed!"
    exit 1
fi

echo ""
echo "====== Publish Completed Successfully! ======"
echo "Output Directory: $OUTPUT_PATH"
echo ""
echo "Next Steps:"
echo "1. Copy contents to UAT IIS server (e.g., C:\inetpub\wwwroot\AttendanceApp)"
echo "2. Or use deploy-iis.ps1 for automated IIS deployment"
echo "3. Set ASPNETCORE_ENVIRONMENT=Staging environment variable"
echo "4. Update appsettings.Staging.json with UAT settings"
echo "5. Run database migrations: dotnet ef database update --environment Staging"
echo "6. Configure IIS Application Pool and Website"
echo "7. Start application via IIS or run Dashboard.exe"
echo ""
