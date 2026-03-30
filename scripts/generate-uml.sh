#!/usr/bin/env bash
set -euo pipefail

source_file="${1:-README.md}"
output_dir="${2:-docs/uml}"

if [[ ! -f "$source_file" ]]; then
  echo "Source markdown not found: $source_file" >&2
  exit 1
fi

mkdir -p "$output_dir"
find "$output_dir" -maxdepth 1 -type f -name '*.puml' -delete
find "$output_dir" -maxdepth 1 -type f -name '*.svg' -delete

generated_manifest="$output_dir/.generated-files"

awk -v out="$output_dir" '
function slugify(text, value) {
  value = tolower(text)
  sub(/^[[:space:]]+/, "", value)
  sub(/[[:space:]]+$/, "", value)
  sub(/^[A-Za-z]\)[[:space:]]+/, "", value)
  gsub(/[`"()]/, "", value)
  gsub(/[^a-z0-9]+/, "-", value)
  gsub(/^-+/, "", value)
  gsub(/-+$/, "", value)
  if (value == "") {
    value = "diagram-" ++fallback
  }
  return value
}
function flush(target) {
  if (!in_block) {
    return
  }

  target = out "/" current_file ".puml"
  printf "%s", buffer > target
  close(target)
  files[++count] = current_file ".puml"
  buffer = ""
  in_block = 0
}
/^#+[[:space:]]+/ {
  current_heading = $0
  sub(/^#+[[:space:]]+/, "", current_heading)
  gsub(/^[[:space:]]+|[[:space:]]+$/, "", current_heading)
  next
}
/^```plantuml[[:space:]]*$/ {
  flush()
  in_block = 1
  current_file = slugify(current_heading)
  buffer = ""
  next
}
/^```[[:space:]]*$/ {
  if (in_block) {
    flush()
  }
  next
}
{
  if (in_block) {
    buffer = buffer $0 ORS
  }
}
END {
  flush()
  for (i = 1; i <= count; i++) {
    print files[i]
  }
}
' "$source_file" | sort > "$generated_manifest"

if [[ ! -s "$generated_manifest" ]]; then
  rm -f "$generated_manifest"
  echo "No PlantUML blocks found in $source_file" >&2
  exit 1
fi

mapfile -t generated_files < "$generated_manifest"
renderer_status="source-only"

if command -v plantuml >/dev/null 2>&1; then
  for file in "${generated_files[@]}"; do
    plantuml -tsvg "$output_dir/$file"
  done
  renderer_status="plantuml-cli"
elif [[ -n "${PLANTUML_JAR:-}" && -f "${PLANTUML_JAR}" ]]; then
  for file in "${generated_files[@]}"; do
    /opt/jdk-16.0.2/bin/java -jar "$PLANTUML_JAR" -tsvg "$output_dir/$file"
  done
  renderer_status="plantuml-jar"
fi

{
  echo "# Generated UML Artifacts"
  echo
  echo "Source markdown: \`$source_file\`"
  echo
  echo "Generated PlantUML files:"
  for file in "${generated_files[@]}"; do
    echo "- \`$file\`"
  done
  echo
  echo "Render mode: \`$renderer_status\`"
  if [[ "$renderer_status" == "source-only" ]]; then
    echo
    echo "Set \`PLANTUML_JAR\` or install the \`plantuml\` CLI to render SVG files automatically."
  fi
} > "$output_dir/README.md"

rm -f "$generated_manifest"
echo "Generated ${#generated_files[@]} UML artifact(s) in $output_dir"
