#!/bin/bash
set -e

echo "Building Apple Music Discord RPC for macOS..."
swift build -c release --arch arm64 --arch x86_64

APP_NAME="AppleMusicDiscordRPC.app"
BIN_NAME="AppleMusicDiscordRPC"
RELEASE_DIR=".build/apple/Products/Release"

# Fallback to standard release dir if universal binary path differs
if [ ! -f "$RELEASE_DIR/$BIN_NAME" ]; then
    RELEASE_DIR=".build/release"
fi

rm -rf "$APP_NAME"
mkdir -p "$APP_NAME/Contents/MacOS"
mkdir -p "$APP_NAME/Contents/Resources"

cp "$RELEASE_DIR/$BIN_NAME" "$APP_NAME/Contents/MacOS/$BIN_NAME"
chmod +x "$APP_NAME/Contents/MacOS/$BIN_NAME"

# Create Info.plist
cat <<EOF > "$APP_NAME/Contents/Info.plist"
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleExecutable</key>
    <string>$BIN_NAME</string>
    <key>CFBundleIdentifier</key>
    <string>com.vortexcrypx.applemusicdiscordrpc</string>
    <key>CFBundleName</key>
    <string>Apple Music RPC</string>
    <key>CFBundlePackageType</key>
    <string>APPL</string>
    <key>CFBundleShortVersionString</key>
    <string>1.0.0</string>
    <key>LSUIElement</key>
    <true/>
    <key>NSHighResolutionCapable</key>
    <true/>
</dict>
</plist>
EOF

echo "Packaging into AppleMusicDiscordRPC-macOS.zip..."
zip -r -y "AppleMusicDiscordRPC-macOS.zip" "$APP_NAME"
echo "Build complete: AppleMusicDiscordRPC-macOS.zip"
