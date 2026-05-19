#~/usr/bin/env bash
set -euo pipefail

#usage ./extract-svg-path.sh "https://raw.githubusercontent.com/microsoft/fluentui-system-icons/refs/heads/master/assets/Search/SVG/ic_fluent_search_24_regular.svg"

if [[ $# -ne 1 ]]; then
    echo "Usge: $0 <svg-url>" >&2
    exit 1
fi

svg_url="$1"

paths="$(
    curl -fssL "$svg_url" |
        tr '\n' ' ' |
        grep -Eo '<path[^>]+d="[^"]+"' |
        sed -E 's/.*d="([^"]+)"/\1/'
)"

if [[ -z "$paths" ]]; then
    echo 'No SVG <path d="..." elements found.' >&2
    exit 1
fi

printf '%s\n' "$paths"

