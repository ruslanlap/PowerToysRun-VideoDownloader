#!/bin/bash

# Simple script to respond to GitHub issues after a release
# Usage: ./respond_to_issues.sh <version>

if [ -z "$1" ]; then
    echo "Usage: $0 <version>"
    echo "Example: $0 1.0.6"
    exit 1
fi

VERSION=$1
REPO="ruslanlap/PowerToysRun-VideoDownloader"
RELEASE_URL="https://github.com/$REPO/releases/tag/v$VERSION"

# Get all open issues
echo "Fetching open issues..."
ISSUES=$(gh issue list --repo $REPO --json number,title --jq '.[] | "\(.number)|\(.title)"')

# Process each issue
echo "$ISSUES" | while IFS="|" read -r number title; do
    echo "Processing issue #$number: $title"
    
    # Generate response based on issue title
    if [[ $title == *"quality"* ]]; then
        response="Thank you for your feedback! I'm happy to inform you that the quality selection issue has been fixed in the new release v$VERSION. The plugin now properly handles quality parameters and won't default to the lowest resolution. Please check out the release notes for more details: $RELEASE_URL"
    elif [[ $title == *"settings"* ]]; then
        response="Thank you for your feedback! I'm happy to inform you that the settings issue has been fixed in the new release v$VERSION. All settings are now properly saved and loaded. Please check out the release notes for more details: $RELEASE_URL"
    elif [[ $title == *"download location"* ]]; then
        response="Thank you for your feedback! I'm happy to inform you that the download location feature has been implemented in the new release v$VERSION. You can now change the download location through the plugin settings. Please check out the release notes for more details: $RELEASE_URL"
    else
        response="Thank you for your feedback! I'm happy to inform you that your issue has been addressed in the new release v$VERSION. Please check out the release notes for more details: $RELEASE_URL"
    fi
    
    # Post response
    echo "Posting response to issue #$number..."
    gh issue comment $number --repo $REPO --body "$response"
    
    # Wait a bit to avoid rate limiting
    sleep 2
done

echo "Done! All issues have been responded to." 