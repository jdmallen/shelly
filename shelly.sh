#!/usr/bin/env bash

# shelly - Shell command suggester using Claude Haiku
# Requires: curl, jq

[[ -f ~/.anthropic_creds ]] && source ~/.anthropic_creds

MODEL="claude-haiku-4-5-20251001"
API_URL="https://api.anthropic.com/v1/messages"

# Colors
CYAN='\033[0;36m'
GREEN='\033[0;32m'
YELLOW='\033[0;33m'
RED='\033[0;31m'
RESET='\033[0m'

die() {
    echo -e "${RED}Error: $1${RESET}" >&2
    exit 1
}

# Check dependencies
command -v curl >/dev/null 2>&1 || die "curl is required but not installed"
command -v jq >/dev/null 2>&1 || die "jq is required but not installed"
[[ -n "${ANTHROPIC_API_KEY:-}" ]] || die "ANTHROPIC_API_KEY environment variable not set"

# Clipboard function (cross-platform)
copy_to_clipboard() {
    if command -v pbcopy >/dev/null 2>&1; then
        echo -n "$1" | pbcopy
    elif command -v xclip >/dev/null 2>&1; then
        echo -n "$1" | xclip -selection clipboard
    elif command -v xsel >/dev/null 2>&1; then
        echo -n "$1" | xsel --clipboard --input
    elif command -v wl-copy >/dev/null 2>&1; then
        echo -n "$1" | wl-copy
    else
        echo -e "${YELLOW}No clipboard utility found (pbcopy/xclip/xsel/wl-copy)${RESET}"
        return 1
    fi
}

# Get shell context
get_context() {
    local ctx="Shell: ${SHELL##*/}"
    ctx+=", OS: $(uname -s)"
    ctx+=", PWD: $(pwd)"
    echo "$ctx"
}

# Call Claude API
get_suggestion() {
    local prompt="$1"
    local context
    context=$(get_context)
    
    local system_prompt="You are a shell command expert. Given a description of what the user wants to do, output ONLY the shell command(s) that accomplish it. No explanations, no markdown, no code fences---just the raw command(s). If multiple commands are needed, separate them with && or use appropriate shell syntax. Context: $context"
    
    local payload
    payload=$(jq -n \
        --arg model "$MODEL" \
        --arg system "$system_prompt" \
        --arg prompt "$prompt" \
        '{
            model: $model,
            max_tokens: 500,
            system: $system,
            messages: [{ role: "user", content: $prompt }]
        }')
    
    local response
    response=$(curl -s "$API_URL" \
        -H "Content-Type: application/json" \
        -H "x-api-key: $ANTHROPIC_API_KEY" \
        -H "anthropic-version: 2023-06-01" \
        -d "$payload")
    
    # Check for API errors
    local error
    error=$(echo "$response" | jq -r '.error.message // empty')
    if [[ -n "$error" ]]; then
        die "API error: $error"
    fi
    
    echo "$response" | jq -r '.content[0].text // empty'
}

# Main interaction loop
main() {
    local prompt="${*:-}"
    
    if [[ -z "$prompt" ]]; then
        echo -e "${CYAN}What do you want to do?${RESET}"
        read -r prompt < /dev/tty
        [[ -z "$prompt" ]] && die "No prompt provided"
    fi
    
    while true; do
        echo -e "${CYAN}Thinking...${RESET}"
        local suggestion
        suggestion=$(get_suggestion "$prompt")
        
        if [[ -z "$suggestion" ]]; then
            die "No suggestion received from API"
        fi
        
        echo ""
        echo -e "${GREEN}Suggestion:${RESET}"
        echo -e "${YELLOW}$suggestion${RESET}"
        echo ""
        echo -e "[${GREEN}e${RESET}]xecute  [${GREEN}c${RESET}]opy  [${GREEN}r${RESET}]etry  [${GREEN}q${RESET}]uit"
        read -rn1 choice < /dev/tty
        echo ""
        
        case "$choice" in
            e|E)
                echo -e "${CYAN}Executing...${RESET}"
                eval "$suggestion"
                exit $?
                ;;
            c|C)
                if copy_to_clipboard "$suggestion"; then
                    echo -e "${GREEN}Copied to clipboard${RESET}"
                fi
                exit 0
                ;;
            r|R)
                echo -e "${CYAN}What should be different?${RESET}"
                read -r refinement < /dev/tty
                if [[ -n "$refinement" ]]; then
                    prompt="$prompt (refinement: $refinement)"
                fi
                # Loop continues with new prompt
                ;;
            q|Q|$'\e')
                echo "Bye"
                exit 0
                ;;
            *)
                echo -e "${YELLOW}Invalid choice${RESET}"
                ;;
        esac
    done
}

main "$@"
