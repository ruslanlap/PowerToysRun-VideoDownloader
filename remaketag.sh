#!/bin/bash

# =============================================================================
# REMAKETAG - Professional Git Tag Recreation Script
# =============================================================================
# Author: RandomGen Project
# Description: Safely recreates Git tags with enhanced features and validation
# Version: 2.0.0
# =============================================================================

set -euo pipefail

# Color definitions
readonly RED='\033[0;31m'
readonly GREEN='\033[0;32m'
readonly YELLOW='\033[1;33m'
readonly BLUE='\033[0;34m'
readonly PURPLE='\033[0;35m'
readonly CYAN='\033[0;36m'
readonly WHITE='\033[1;37m'
readonly NC='\033[0m' # No Color

# Configuration
readonly SCRIPT_NAME=$(basename "$0")
readonly SCRIPT_DIR=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
readonly LOG_FILE="${SCRIPT_DIR}/remaketag.log"

# Default values
DEFAULT_TAG="v1.0.0"
DEFAULT_MESSAGE="RandomGen v1.0.0 release with enhanced features and fixes"

# =============================================================================
# UTILITY FUNCTIONS
# =============================================================================

log() {
    local level="$1"
    shift
    local message="$*"
    local timestamp=$(date '+%Y-%m-%d %H:%M:%S')
    
    echo -e "${timestamp} [${level}] ${message}" | tee -a "$LOG_FILE"
}

print_header() {
    echo -e "${CYAN}"
    echo "╔══════════════════════════════════════════════════════════════╗"
    echo "║                    REMAKETAG v2.0.0                        ║"
    echo "║              Professional Git Tag Recreation                ║"
    echo "╚══════════════════════════════════════════════════════════════╝"
    echo -e "${NC}"
}

print_success() {
    echo -e "${GREEN}✓ $1${NC}"
}

print_warning() {
    echo -e "${YELLOW}⚠ $1${NC}"
}

print_error() {
    echo -e "${RED}✗ $1${NC}"
}

print_info() {
    echo -e "${BLUE}ℹ $1${NC}"
}

# =============================================================================
# VALIDATION FUNCTIONS
# =============================================================================

validate_git_repo() {
    if ! git rev-parse --is-inside-work-tree &>/dev/null; then
        print_error "Not in a Git repository!"
        exit 1
    fi
}

validate_remote() {
    if ! git ls-remote --exit-code origin &>/dev/null; then
        print_error "Cannot connect to remote 'origin'!"
        exit 1
    fi
}

validate_tag_format() {
    local tag="$1"
    if [[ ! "$tag" =~ ^v[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
        print_error "Invalid tag format! Use semantic versioning (e.g., v1.2.3)"
        exit 1
    fi
}

check_uncommitted_changes() {
    if ! git diff-index --quiet HEAD --; then
        print_warning "You have uncommitted changes!"
        read -p "Continue anyway? (y/N): " -n 1 -r
        echo
        if [[ ! $REPLY =~ ^[Yy]$ ]]; then
            exit 1
        fi
    fi
}

# =============================================================================
# CORE FUNCTIONS
# =============================================================================

show_current_tags() {
    print_info "Current tags:"
    git tag -l | sed 's/^/  /' || echo "  No tags found"
}

get_latest_commit_info() {
    local commit_hash=$(git rev-parse --short HEAD)
    local commit_msg=$(git log -1 --pretty=%B | head -n1)
    echo "Latest commit: ${commit_hash} - ${commit_msg}"
}

delete_local_tag() {
    local tag="$1"
    if git tag -l | grep -q "^${tag}$"; then
        print_info "Deleting local tag: $tag"
        git tag -d "$tag"
        print_success "Local tag deleted"
    else
        print_info "Local tag $tag does not exist"
    fi
}

delete_remote_tag() {
    local tag="$1"
    if git ls-remote --tags origin | grep -q "refs/tags/${tag}"; then
        print_info "Deleting remote tag: $tag"
        git push origin ":refs/tags/${tag}"
        print_success "Remote tag deleted"
    else
        print_info "Remote tag $tag does not exist"
    fi
}

create_tag() {
    local tag="$1"
    local message="$2"
    
    print_info "Creating new tag: $tag"
    git tag -a "$tag" -m "$message"
    print_success "Tag created locally"
}

push_tag() {
    local tag="$1"
    print_info "Pushing tag to remote: $tag"
    git push origin "$tag"
    print_success "Tag pushed to remote"
}

show_tag_info() {
    local tag="$1"
    print_info "Tag details:"
    git show --stat "$tag" | sed 's/^/  /'
}

# =============================================================================
# INTERACTIVE MODE
# =============================================================================

interactive_mode() {
    print_header
    
    echo -e "${PURPLE}Current repository status:${NC}"
    echo "  Repository: $(basename "$(git rev-parse --show-toplevel)")"
    echo "  Branch: $(git rev-parse --abbrev-ref HEAD)"
    echo "  $(get_latest_commit_info)"
    echo
    
    show_current_tags
    echo
    
    read -p "Enter tag name [${DEFAULT_TAG}]: " tag_name
    tag_name=${tag_name:-$DEFAULT_TAG}
    
    validate_tag_format "$tag_name"
    
    read -p "Enter tag message [${DEFAULT_MESSAGE}]: " tag_message
    tag_message=${tag_message:-$DEFAULT_MESSAGE}
    
    echo
    print_info "Summary:"
    echo "  Tag: $tag_name"
    echo "  Message: $tag_message"
    echo
    
    read -p "Proceed with tag recreation? (y/N): " -n 1 -r
    echo
    if [[ ! $REPLY =~ ^[Yy]$ ]]; then
        print_info "Operation cancelled"
        exit 0
    fi
    
    recreate_tag "$tag_name" "$tag_message"
}

# =============================================================================
# MAIN FUNCTION
# =============================================================================

recreate_tag() {
    local tag="${1:-$DEFAULT_TAG}"
    local message="${2:-$DEFAULT_MESSAGE}"
    
    print_header
    
    log "INFO" "Starting tag recreation for: $tag"
    
    # Validation
    validate_git_repo
    validate_remote
    validate_tag_format "$tag"
    check_uncommitted_changes
    
    echo
    print_info "Recreating tag: $tag"
    echo
    
    # Delete existing tags
    delete_local_tag "$tag"
    delete_remote_tag "$tag"
    
    echo
    
    # Create and push new tag
    create_tag "$tag" "$message"
    push_tag "$tag"
    
    echo
    print_success "Tag recreation completed successfully!"
    echo
    
    show_tag_info "$tag"
    
    log "INFO" "Tag recreation completed for: $tag"
}

# =============================================================================
# HELP FUNCTION
# =============================================================================

show_help() {
    print_header
    cat << EOF
${WHITE}USAGE:${NC}
    $SCRIPT_NAME [OPTIONS] [TAG] [MESSAGE]

${WHITE}DESCRIPTION:${NC}
    Recreates Git tags with enhanced features, validation, and safety checks.

${WHITE}OPTIONS:${NC}
    -h, --help      Show this help message
    -i, --interactive   Interactive mode (default)
    -f, --force     Skip confirmation prompts
    -v, --verbose   Enable verbose logging
    -q, --quiet     Suppress output (errors only)

${WHITE}EXAMPLES:${NC}
    $SCRIPT_NAME                    # Interactive mode
    $SCRIPT_NAME v1.2.3            # Recreate tag v1.2.3
    $SCRIPT_NAME v2.0.0 "Release v2.0.0"
    $SCRIPT_NAME -f v1.1.0         # Force recreation

${WHITE}FEATURES:${NC}
    ✓ Semantic version validation
    ✓ Remote connectivity checks
    ✓ Uncommitted changes warning
    ✓ Detailed logging
    ✓ Color-coded output
    ✓ Interactive and batch modes

EOF
}

# =============================================================================
# COMMAND LINE PARSING
# =============================================================================

parse_args() {
    local interactive=false
    local force=false
    local verbose=false
    local quiet=false
    local tag=""
    local message=""
    
    while [[ $# -gt 0 ]]; do
        case $1 in
            -h|--help)
                show_help
                exit 0
                ;;
            -i|--interactive)
                interactive=true
                shift
                ;;
            -f|--force)
                force=true
                shift
                ;;
            -v|--verbose)
                verbose=true
                shift
                ;;
            -q|--quiet)
                quiet=true
                shift
                ;;
            -*)
                print_error "Unknown option: $1"
                show_help
                exit 1
                ;;
            *)
                if [[ -z "$tag" ]]; then
                    tag="$1"
                elif [[ -z "$message" ]]; then
                    message="$1"
                else
                    print_error "Too many arguments"
                    show_help
                    exit 1
                fi
                shift
                ;;
        esac
    done
    
    # Set defaults if not provided
    tag=${tag:-$DEFAULT_TAG}
    message=${message:-$DEFAULT_MESSAGE}
    
    # Handle quiet mode
    if [[ "$quiet" == true ]]; then
        exec 1>/dev/null
    fi
    
    # Handle verbose mode
    if [[ "$verbose" == true ]]; then
        set -x
    fi
    
    # Determine mode
    if [[ "$interactive" == true ]] || [[ $# -eq 0 ]]; then
        interactive_mode
    else
        recreate_tag "$tag" "$message"
    fi
}

# =============================================================================
# MAIN EXECUTION
# =============================================================================

# Initialize log file
touch "$LOG_FILE" 2>/dev/null || true

# Trap errors
trap 'print_error "Script failed at line $LINENO"' ERR

# Execute main function
parse_args "$@"
