# Script to automatically respond to issues when a new release is made
param(
    [Parameter(Mandatory=$true)]
    [string]$Version,
    
    [Parameter(Mandatory=$true)]
    [string]$ReleaseUrl
)

# Function to get issue details
function Get-IssueDetails {
    param(
        [string]$IssueNumber
    )
    
    $issue = gh issue view $IssueNumber --json title,body
    return $issue | ConvertFrom-Json
}

# Function to generate response based on issue content
function Get-IssueResponse {
    param(
        [string]$IssueNumber,
        [string]$Version,
        [string]$ReleaseUrl
    )
    
    $issue = Get-IssueDetails $IssueNumber
    $title = $issue.title.ToLower()
    
    # Define response templates based on issue content
    $responses = @{
        "quality" = "Thank you for your feedback! I'm happy to inform you that the quality selection issue has been fixed in the new release v$Version. The plugin now properly handles quality parameters and won't default to the lowest resolution. Please check out the release notes for more details: $ReleaseUrl"
        
        "settings" = "Thank you for your feedback! I'm happy to inform you that the settings issue has been fixed in the new release v$Version. All settings are now properly saved and loaded. Please check out the release notes for more details: $ReleaseUrl"
        
        "download location" = "Thank you for your feedback! I'm happy to inform you that the download location feature has been implemented in the new release v$Version. You can now change the download location through the plugin settings. Please check out the release notes for more details: $ReleaseUrl"
        
        "default" = "Thank you for your feedback! I'm happy to inform you that your issue has been addressed in the new release v$Version. Please check out the release notes for more details: $ReleaseUrl"
    }
    
    # Determine which response to use
    foreach ($key in $responses.Keys) {
        if ($title -like "*$key*") {
            return $responses[$key]
        }
    }
    
    return $responses["default"]
}

# Get all open issues
$issues = gh issue list --json number --jq '.[].number'

# Respond to each issue
foreach ($issueNumber in $issues) {
    $response = Get-IssueResponse -IssueNumber $issueNumber -Version $Version -ReleaseUrl $ReleaseUrl
    Write-Host "Responding to issue #$issueNumber..."
    gh issue comment $issueNumber --body $response
    Start-Sleep -Seconds 2  # Add delay to avoid rate limiting
}

Write-Host "Finished responding to all issues!" 