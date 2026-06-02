# shelly zsh integration
#
# Source this from your ~/.zshrc:
#
#     source /path/to/shelly.zsh
#
# It wraps the `shelly` binary so the [e]dit action can drop the suggested
# command onto your next prompt (via zsh's `print -z`), where you can edit it
# before running. Without this wrapper, shelly still works — the edit option
# just isn't offered.

shelly() {
	local edit_file
	edit_file=$(mktemp "${TMPDIR:-/tmp}/shelly-edit.XXXXXX") || {
		command shelly "$@"
		return $?
	}

	SHELLY_EDIT_FILE=$edit_file command shelly "$@"
	local rc=$?

	if [[ -s $edit_file ]]; then
		# Push the chosen command onto the editing buffer for the next prompt.
		print -z -- "$(<$edit_file)"
	fi

	rm -f -- "$edit_file"

	return $rc
}
